using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public sealed class DatabaseDiscoverySyncService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec tokenCodec,
    IOptions<DatabaseDiscoveryOptions> options)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly DatabaseDiscoveryOptions settings = Validate(options.Value);

    public async Task<DatabaseDiscoveryReconciliationPageResponse?> GetReconciliation(
        long profileId,
        string? category,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var context = await LoadContext(profileId, cancellationToken);
        if (context is null) return null;
        var candidates = await BuildCandidates(context, cancellationToken);
        var normalizedCategory = category?.Trim();
        var normalizedSearch = search?.Trim();
        var filtered = candidates
            .Where(x => string.IsNullOrWhiteSpace(normalizedCategory)
                || string.Equals(x.Category, normalizedCategory, StringComparison.Ordinal))
            .Where(x => string.IsNullOrWhiteSpace(normalizedSearch)
                || x.SchemaName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || x.ObjectName.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase)
                || (x.ChildName?.Contains(normalizedSearch, StringComparison.OrdinalIgnoreCase) ?? false))
            .OrderBy(x => x.SchemaName, StringComparer.Ordinal)
            .ThenBy(x => x.ObjectName, StringComparer.Ordinal)
            .ThenBy(x => x.ChildName, StringComparer.Ordinal)
            .ThenBy(x => x.Key, StringComparer.Ordinal)
            .ToArray();
        var resolvedPage = page ?? 1;
        var resolvedPageSize = pageSize ?? 50;
        return new(
            context.Profile.Id,
            context.Profile.Name,
            context.Profile.DatabaseSourceId,
            context.Profile.DatabaseSource.Name,
            context.Profile.ProviderType,
            context.SnapshotEntity.Id,
            context.DifferenceId,
            context.SnapshotEntity.ScopeGenerationId,
            context.SnapshotEntity.IdentityAlgorithmVersion,
            filtered.Skip((resolvedPage - 1) * resolvedPageSize).Take(resolvedPageSize).ToArray(),
            resolvedPage,
            resolvedPageSize,
            filtered.Length);
    }

    public async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPlanResponse>> CreatePlan(
        CreateDatabaseDiscoverySyncPlanRequest request,
        DatabaseDiscoverySyncActor actor,
        CancellationToken cancellationToken)
    {
        var errors = ValidateSelectionRequest(request);
        if (errors.Count > 0) return new(null, errors, DatabaseDiscoverySyncFailure.Validation);
        var context = await LoadContext(request.ProfileId, cancellationToken);
        if (context is null || context.SnapshotEntity.Id != request.TargetSnapshotId
            || context.Profile.ConfigurationRevision != context.SnapshotEntity.Run.ProfileConfigurationRevision)
            return new(null, null, DatabaseDiscoverySyncFailure.LatestSnapshotChanged, "LatestSnapshotChanged");
        var actions = NormalizeSelections(request.Actions!);
        if (actions.Count > settings.MaximumSyncPlanActions)
            return Validation<DatabaseDiscoverySyncPlanResponse>("actions", $"同步计划最多包含 {settings.MaximumSyncPlanActions} 个操作。");
        var validation = await ValidateSelections(context, actions, cancellationToken);
        if (validation is not null) return validation;

        var now = DateTimeOffset.UtcNow;
        var plan = new DatabaseDiscoverySyncPlan
        {
            ProfileId = context.Profile.Id,
            DatabaseSourceId = context.Profile.DatabaseSourceId,
            ProfileConfigurationRevision = context.Profile.ConfigurationRevision,
            BaseSnapshotId = context.BaseSnapshotId,
            TargetSnapshotId = context.SnapshotEntity.Id,
            TargetDifferenceId = context.DifferenceId,
            ScopeGenerationId = context.SnapshotEntity.ScopeGenerationId,
            IdentityAlgorithmVersion = context.SnapshotEntity.IdentityAlgorithmVersion,
            Status = DatabaseDiscoverySyncPlanStatus.Draft,
            SelectionFormatVersion = 1,
            SelectionJson = JsonSerializer.Serialize(actions, JsonOptions),
            CreatedByUserId = actor.UserId,
            CreatedByDisplayName = actor.DisplayName,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        dbContext.DatabaseDiscoverySyncPlans.Add(plan);
        AddAudit(plan, DatabaseDiscoverySyncAuditAction.PlanCreated, actor, now,
            new { actionCount = actions.Count, targetSnapshotId = plan.TargetSnapshotId });
        AddAudit(plan, DatabaseDiscoverySyncAuditAction.SelectionChanged, actor, now,
            new { actionCount = actions.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(await ToResponse(plan, cancellationToken), null, DatabaseDiscoverySyncFailure.None);
    }

    public async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPlanResponse>> UpdateSelections(
        long id,
        UpdateDatabaseDiscoverySyncSelectionsRequest request,
        DatabaseDiscoverySyncActor actor,
        CancellationToken cancellationToken)
    {
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
            return Validation<DatabaseDiscoverySyncPlanResponse>("concurrencyToken", "并发令牌无效。");
        var errors = ValidateActions(request.Actions);
        if (errors.Count > 0)
            return new(null, errors, DatabaseDiscoverySyncFailure.Validation);

        var plan = await dbContext.DatabaseDiscoverySyncPlans.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null) return new(null, null, DatabaseDiscoverySyncFailure.NotFound);
        if (plan.Version != expectedVersion) return new(null, null, DatabaseDiscoverySyncFailure.StalePlan, "ConcurrencyConflict");
        if (plan.Status == DatabaseDiscoverySyncPlanStatus.Applied)
            return new(null, null, DatabaseDiscoverySyncFailure.AlreadyApplied);
        if (plan.Status == DatabaseDiscoverySyncPlanStatus.Superseded)
            return new(null, null, DatabaseDiscoverySyncFailure.StalePlan, "PlanSuperseded");

        var context = await LoadContext(plan.ProfileId, cancellationToken);
        if (context is null || context.SnapshotEntity.Id != plan.TargetSnapshotId
            || context.Profile.ConfigurationRevision != plan.ProfileConfigurationRevision
            || context.SnapshotEntity.Run.ProfileConfigurationRevision != plan.ProfileConfigurationRevision
            || context.SnapshotEntity.ScopeGenerationId != plan.ScopeGenerationId
            || context.SnapshotEntity.IdentityAlgorithmVersion != plan.IdentityAlgorithmVersion)
            return await Supersede(plan, actor, "LatestSnapshotChanged", cancellationToken);

        var actions = NormalizeSelections(request.Actions!);
        var validation = await ValidateSelections(context, actions, cancellationToken);
        if (validation is not null) return validation;

        var now = DateTimeOffset.UtcNow;
        plan.SelectionJson = JsonSerializer.Serialize(actions, JsonOptions);
        plan.PreviewFormatVersion = null;
        plan.PreviewPayloadJson = null;
        plan.PreviewHash = null;
        plan.ConfirmedPreviewHash = null;
        plan.ConfirmedByUserId = null;
        plan.ConfirmedAt = null;
        plan.Status = DatabaseDiscoverySyncPlanStatus.Draft;
        plan.UpdatedAt = now;
        plan.Version++;
        AddAudit(plan, DatabaseDiscoverySyncAuditAction.SelectionChanged, actor, now,
            new { actionCount = actions.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(await ToResponse(plan, cancellationToken), null, DatabaseDiscoverySyncFailure.None);
    }

    public async Task<DatabaseDiscoverySyncPlanResponse?> GetPlan(long id, CancellationToken cancellationToken)
    {
        var plan = await PlanQuery().SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        return plan is null ? null : ToResponse(plan);
    }

    public async Task<DatabaseDiscoverySyncPlanPageResponse> ListPlans(
        long? profileId, int? page, int? pageSize, CancellationToken cancellationToken)
    {
        var resolvedPage = page ?? 1;
        var resolvedPageSize = pageSize ?? 20;
        var query = PlanQuery().Where(x => profileId == null || x.ProfileId == profileId);
        var total = await query.CountAsync(cancellationToken);
        var plans = await query.OrderByDescending(x => x.Id)
            .Skip((resolvedPage - 1) * resolvedPageSize).Take(resolvedPageSize)
            .ToArrayAsync(cancellationToken);
        return new(plans.Select(ToResponse).ToArray(), resolvedPage, resolvedPageSize, total);
    }

    public async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPlanResponse>> Preview(
        long id,
        string? concurrencyToken,
        DatabaseDiscoverySyncActor actor,
        CancellationToken cancellationToken)
    {
        if (!tokenCodec.TryDecode(concurrencyToken, out var expectedVersion))
            return Validation<DatabaseDiscoverySyncPlanResponse>("concurrencyToken", "并发令牌无效。");
        var plan = await dbContext.DatabaseDiscoverySyncPlans.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null) return new(null, null, DatabaseDiscoverySyncFailure.NotFound);
        if (plan.Version != expectedVersion) return new(null, null, DatabaseDiscoverySyncFailure.StalePlan, "ConcurrencyConflict");
        if (plan.Status == DatabaseDiscoverySyncPlanStatus.Applied)
            return new(null, null, DatabaseDiscoverySyncFailure.AlreadyApplied);
        var context = await LoadContext(plan.ProfileId, cancellationToken);
        if (context is null || context.SnapshotEntity.Id != plan.TargetSnapshotId
            || context.Profile.ConfigurationRevision != plan.ProfileConfigurationRevision
            || context.SnapshotEntity.Run.ProfileConfigurationRevision != plan.ProfileConfigurationRevision
            || context.SnapshotEntity.ScopeGenerationId != plan.ScopeGenerationId
            || context.SnapshotEntity.IdentityAlgorithmVersion != plan.IdentityAlgorithmVersion)
        {
            return await Supersede(plan, actor, "LatestSnapshotChanged", cancellationToken);
        }
        var actions = DeserializeSelections(plan.SelectionJson);
        var preview = await BuildPreview(plan.Id, context, actions, cancellationToken);
        if (preview.Failure != DatabaseDiscoverySyncFailure.None)
            return new(null, preview.FieldErrors, preview.Failure, preview.ReasonCode);

        var now = DateTimeOffset.UtcNow;
        plan.PreviewFormatVersion = 1;
        plan.PreviewPayloadJson = JsonSerializer.Serialize(preview.Response, JsonOptions);
        plan.PreviewHash = preview.Response!.PreviewHash;
        plan.ConfirmedPreviewHash = null;
        plan.ConfirmedAt = null;
        plan.ConfirmedByUserId = null;
        plan.Status = DatabaseDiscoverySyncPlanStatus.Draft;
        plan.UpdatedAt = now;
        plan.Version++;
        AddAudit(plan, DatabaseDiscoverySyncAuditAction.PreviewGenerated, actor, now,
            new { previewHash = plan.PreviewHash, actionCount = actions.Count });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(await ToResponse(plan, cancellationToken), null, DatabaseDiscoverySyncFailure.None);
    }

    public async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPlanResponse>> Confirm(
        long id,
        ConfirmDatabaseDiscoverySyncPlanRequest request,
        DatabaseDiscoverySyncActor actor,
        CancellationToken cancellationToken)
    {
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
            return Validation<DatabaseDiscoverySyncPlanResponse>("concurrencyToken", "并发令牌无效。");
        if (!IsSha256(request.PreviewHash)) return Validation<DatabaseDiscoverySyncPlanResponse>("previewHash", "预览哈希无效。");
        var plan = await dbContext.DatabaseDiscoverySyncPlans.SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null) return new(null, null, DatabaseDiscoverySyncFailure.NotFound);
        if (plan.Version != expectedVersion) return new(null, null, DatabaseDiscoverySyncFailure.StalePlan, "ConcurrencyConflict");
        if (plan.Status == DatabaseDiscoverySyncPlanStatus.Applied)
            return new(null, null, DatabaseDiscoverySyncFailure.AlreadyApplied);
        if (plan.PreviewHash is null || !FixedEquals(plan.PreviewHash, request.PreviewHash!))
            return new(null, null, DatabaseDiscoverySyncFailure.StalePlan, "PreviewChanged");
        var context = await LoadContext(plan.ProfileId, cancellationToken);
        if (context is null || context.SnapshotEntity.Id != plan.TargetSnapshotId
            || context.Profile.ConfigurationRevision != plan.ProfileConfigurationRevision
            || context.SnapshotEntity.Run.ProfileConfigurationRevision != plan.ProfileConfigurationRevision)
            return await Supersede(plan, actor, "LatestSnapshotChanged", cancellationToken);
        var now = DateTimeOffset.UtcNow;
        plan.ConfirmedPreviewHash = plan.PreviewHash;
        plan.ConfirmedByUserId = actor.UserId;
        plan.ConfirmedAt = now;
        plan.Status = DatabaseDiscoverySyncPlanStatus.Ready;
        plan.UpdatedAt = now;
        plan.Version++;
        AddAudit(plan, DatabaseDiscoverySyncAuditAction.PlanConfirmed, actor, now,
            new { previewHash = plan.PreviewHash });
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(await ToResponse(plan, cancellationToken), null, DatabaseDiscoverySyncFailure.None);
    }

    public async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPlanResponse>> Apply(
        long id,
        ApplyDatabaseDiscoverySyncPlanRequest request,
        DatabaseDiscoverySyncActor actor,
        CancellationToken cancellationToken)
    {
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
            return Validation<DatabaseDiscoverySyncPlanResponse>("concurrencyToken", "并发令牌无效。");
        if (!IsSha256(request.PreviewHash)) return Validation<DatabaseDiscoverySyncPlanResponse>("previewHash", "预览哈希无效。");

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var plan = await dbContext.DatabaseDiscoverySyncPlans
            .Include(x => x.ApplyResult)
            .SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (plan is null) return new(null, null, DatabaseDiscoverySyncFailure.NotFound);
        if (plan.Version != expectedVersion) return new(null, null, DatabaseDiscoverySyncFailure.StalePlan, "ConcurrencyConflict");
        if (plan.Status == DatabaseDiscoverySyncPlanStatus.Applied)
            return new(null, null, DatabaseDiscoverySyncFailure.AlreadyApplied);
        if (plan.Status != DatabaseDiscoverySyncPlanStatus.Ready
            || plan.ConfirmedPreviewHash is null
            || plan.PreviewHash is null
            || !FixedEquals(plan.PreviewHash, request.PreviewHash!)
            || !FixedEquals(plan.ConfirmedPreviewHash, request.PreviewHash!))
            return new(null, null, DatabaseDiscoverySyncFailure.NotConfirmed, "ConfirmationRequired");

        var context = await LoadContext(plan.ProfileId, cancellationToken);
        if (context is null || context.SnapshotEntity.Id != plan.TargetSnapshotId
            || context.Profile.ConfigurationRevision != plan.ProfileConfigurationRevision
            || context.SnapshotEntity.Run.ProfileConfigurationRevision != plan.ProfileConfigurationRevision
            || context.SnapshotEntity.ScopeGenerationId != plan.ScopeGenerationId
            || context.SnapshotEntity.IdentityAlgorithmVersion != plan.IdentityAlgorithmVersion)
        {
            await SupersedeInTransaction(plan, actor, "LatestSnapshotChanged", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(null, null, DatabaseDiscoverySyncFailure.LatestSnapshotChanged, "LatestSnapshotChanged");
        }

        var actions = DeserializeSelections(plan.SelectionJson);
        var fresh = await BuildPreview(plan.Id, context, actions, cancellationToken);
        if (fresh.Failure != DatabaseDiscoverySyncFailure.None
            || fresh.Response is null
            || !FixedEquals(fresh.Response.PreviewHash, plan.PreviewHash))
        {
            await SupersedeInTransaction(plan, actor, fresh.ReasonCode ?? "PreviewChanged", cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new(null, fresh.FieldErrors, DatabaseDiscoverySyncFailure.StalePlan, fresh.ReasonCode ?? "PreviewChanged");
        }

        try
        {
            var counts = await ApplyActions(plan, context, fresh.Response.Actions, actor, cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var result = new DatabaseDiscoverySyncApplyResult
            {
                PlanId = plan.Id,
                CreatedObjects = counts.CreateObjects,
                LinkedObjects = counts.LinkObjects,
                CreatedColumns = counts.CreateColumns,
                LinkedColumns = counts.LinkColumns,
                UpdatedObjects = counts.UpdateObjects,
                UpdatedColumns = counts.UpdateColumns,
                MarkedMissing = counts.MarkMissing,
                ClearedMissing = counts.ClearMissing,
                AppliedAt = now,
                AppliedByUserId = actor.UserId,
                AppliedByDisplayName = actor.DisplayName,
            };
            dbContext.DatabaseDiscoverySyncApplyResults.Add(result);
            plan.Status = DatabaseDiscoverySyncPlanStatus.Applied;
            plan.AppliedAt = now;
            plan.UpdatedAt = now;
            plan.Version++;
            AddAudit(plan, DatabaseDiscoverySyncAuditAction.PlanApplied, actor, now, counts);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            plan.ApplyResult = result;
            return new(await ToResponse(plan, cancellationToken), null, DatabaseDiscoverySyncFailure.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(null, null, DatabaseDiscoverySyncFailure.StalePlan, "ConcurrencyConflict");
        }
        catch (DbUpdateException)
        {
            return new(null, null, DatabaseDiscoverySyncFailure.UnsupportedIdentifierCollision, "DatabaseConstraintConflict");
        }
    }

    private async Task<DatabaseDiscoverySyncPreviewCounts> ApplyActions(
        DatabaseDiscoverySyncPlan plan,
        SyncContext context,
        IReadOnlyList<DatabaseDiscoverySyncPreviewActionResponse> actions,
        DatabaseDiscoverySyncActor actor,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        var canonicalObjects = context.Snapshot.Objects.ToDictionary(x => x.LogicalIdentity, StringComparer.Ordinal);
        var canonicalColumns = context.Snapshot.Columns.ToDictionary(x => x.LogicalIdentity, StringComparer.Ordinal);
        var objectBindings = await dbContext.DatabaseObjectDiscoveryBindings
            .Where(x => x.ProfileId == plan.ProfileId && x.ScopeGenerationId == plan.ScopeGenerationId
                && x.IdentityAlgorithmVersion == plan.IdentityAlgorithmVersion)
            .ToDictionaryAsync(x => x.LogicalIdentity, StringComparer.Ordinal, cancellationToken);
        var columnBindings = await dbContext.DatabaseColumnDiscoveryBindings
            .Where(x => x.ProfileId == plan.ProfileId && x.ScopeGenerationId == plan.ScopeGenerationId
                && x.IdentityAlgorithmVersion == plan.IdentityAlgorithmVersion)
            .ToDictionaryAsync(x => x.LogicalIdentity, StringComparer.Ordinal, cancellationToken);
        var objectTargets = await dbContext.DatabaseObjects.IgnoreQueryFilters()
            .Where(x => x.DatabaseSourceId == plan.DatabaseSourceId)
            .ToDictionaryAsync(x => x.Id, cancellationToken);
        var columnTargetIds = columnBindings.Values.Select(x => x.DatabaseColumnId)
            .Concat(actions.Where(x => x.EntityKind == DatabaseDiscoveryEntityKind.Column && x.TargetId != null)
                .Select(x => x.TargetId!.Value)).Distinct().ToArray();
        var columnTargets = await dbContext.DatabaseColumns.IgnoreQueryFilters()
            .Where(x => columnTargetIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);

        var objectActions = actions.Where(x => x.EntityKind == DatabaseDiscoveryEntityKind.DatabaseObject).ToArray();
        foreach (var action in objectActions)
        {
            switch (action.ActionType)
            {
                case DatabaseDiscoverySyncActionType.CreateDatabaseObject:
                {
                    var source = canonicalObjects[action.LogicalIdentity];
                    var target = CreateObject(plan, context, source, actor, now);
                    dbContext.DatabaseObjects.Add(target);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    objectTargets[target.Id] = target;
                    objectBindings[action.LogicalIdentity] = AddObjectBinding(plan, context, source, target.Id, now);
                    break;
                }
                case DatabaseDiscoverySyncActionType.LinkExistingDatabaseObject:
                {
                    var source = canonicalObjects[action.LogicalIdentity];
                    var target = objectTargets[action.TargetId!.Value];
                    target.TechnicalIdentityAlgorithmVersion = plan.IdentityAlgorithmVersion;
                    target.TechnicalIdentity = source.LogicalIdentity;
                    target.UpdatedAt = now;
                    target.Version++;
                    objectBindings[action.LogicalIdentity] = AddObjectBinding(plan, context, source, target.Id, now);
                    break;
                }
                case DatabaseDiscoverySyncActionType.UpdateDatabaseObjectStructure:
                {
                    var source = canonicalObjects[action.LogicalIdentity];
                    var binding = objectBindings[action.LogicalIdentity];
                    var target = objectTargets[binding.DatabaseObjectId];
                    ApplyObjectStructure(target, context, source, now);
                    Touch(binding, plan.TargetSnapshotId, now);
                    break;
                }
                case DatabaseDiscoverySyncActionType.MarkObjectSourceMissing:
                {
                    var binding = objectBindings[action.LogicalIdentity];
                    binding.SourceMissingSinceSnapshotId ??= plan.TargetSnapshotId;
                    Touch(binding, plan.TargetSnapshotId, now);
                    break;
                }
                case DatabaseDiscoverySyncActionType.ClearObjectSourceMissing:
                {
                    var binding = objectBindings[action.LogicalIdentity];
                    binding.SourceMissingSinceSnapshotId = null;
                    Touch(binding, plan.TargetSnapshotId, now);
                    break;
                }
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var updateColumnActions = actions.Where(x => x.ActionType == DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure).ToArray();
        if (updateColumnActions.Length > 0)
        {
            var affectedObjectIds = updateColumnActions.Select(x => columnTargets[x.TargetId!.Value].DatabaseObjectId).Distinct().ToArray();
            foreach (var objectId in affectedObjectIds)
            {
                var currentMaximum = await dbContext.DatabaseColumns.IgnoreQueryFilters()
                    .Where(x => x.DatabaseObjectId == objectId && !x.IsDeleted)
                    .MaxAsync(x => (int?)x.OrdinalPosition, cancellationToken) ?? 0;
                var stage = currentMaximum + 1;
                foreach (var action in updateColumnActions.Where(x => columnTargets[x.TargetId!.Value].DatabaseObjectId == objectId))
                    columnTargets[action.TargetId!.Value].OrdinalPosition = stage++;
            }
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var columnActions = actions.Where(x => x.EntityKind == DatabaseDiscoveryEntityKind.Column).ToArray();
        foreach (var action in columnActions)
        {
            switch (action.ActionType)
            {
                case DatabaseDiscoverySyncActionType.CreateDatabaseColumn:
                {
                    var source = canonicalColumns[action.LogicalIdentity];
                    var objectBinding = objectBindings[source.ParentObjectLogicalIdentity];
                    var target = CreateColumn(plan, source, objectBinding.DatabaseObjectId, actor, now);
                    dbContext.DatabaseColumns.Add(target);
                    await dbContext.SaveChangesAsync(cancellationToken);
                    columnTargets[target.Id] = target;
                    columnBindings[action.LogicalIdentity] = AddColumnBinding(plan, context, source, target.Id, now);
                    break;
                }
                case DatabaseDiscoverySyncActionType.LinkExistingDatabaseColumn:
                {
                    var source = canonicalColumns[action.LogicalIdentity];
                    var target = columnTargets[action.TargetId!.Value];
                    target.TechnicalIdentityAlgorithmVersion = plan.IdentityAlgorithmVersion;
                    target.TechnicalIdentity = source.LogicalIdentity;
                    target.UpdatedAt = now;
                    target.Version++;
                    columnBindings[action.LogicalIdentity] = AddColumnBinding(plan, context, source, target.Id, now);
                    break;
                }
                case DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure:
                {
                    var source = canonicalColumns[action.LogicalIdentity];
                    var binding = columnBindings[action.LogicalIdentity];
                    var target = columnTargets[binding.DatabaseColumnId];
                    ApplyColumnStructure(target, source, plan.IdentityAlgorithmVersion, now);
                    Touch(binding, plan.TargetSnapshotId, now);
                    break;
                }
                case DatabaseDiscoverySyncActionType.MarkColumnSourceMissing:
                {
                    var binding = columnBindings[action.LogicalIdentity];
                    binding.SourceMissingSinceSnapshotId ??= plan.TargetSnapshotId;
                    Touch(binding, plan.TargetSnapshotId, now);
                    break;
                }
                case DatabaseDiscoverySyncActionType.ClearColumnSourceMissing:
                {
                    var binding = columnBindings[action.LogicalIdentity];
                    binding.SourceMissingSinceSnapshotId = null;
                    Touch(binding, plan.TargetSnapshotId, now);
                    break;
                }
            }
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        return Counts(actions);
    }

    private async Task<IReadOnlyList<DatabaseDiscoveryReconciliationCandidateResponse>> BuildCandidates(
        SyncContext context,
        CancellationToken cancellationToken)
    {
        var snapshot = context.Snapshot;
        var profileId = context.Profile.Id;
        var sourceId = context.Profile.DatabaseSourceId;
        var objectBindings = await dbContext.DatabaseObjectDiscoveryBindings.AsNoTracking()
            .Where(x => x.ProfileId == profileId)
            .ToArrayAsync(cancellationToken);
        var columnBindings = await dbContext.DatabaseColumnDiscoveryBindings.AsNoTracking()
            .Where(x => x.ProfileId == profileId)
            .ToArrayAsync(cancellationToken);
        var activeObjects = await dbContext.DatabaseObjects.AsNoTracking()
            .Where(x => x.DatabaseSourceId == sourceId).ToArrayAsync(cancellationToken);
        var objectIds = activeObjects.Select(x => x.Id).ToArray();
        var activeColumns = await dbContext.DatabaseColumns.AsNoTracking()
            .Where(x => objectIds.Contains(x.DatabaseObjectId)).ToArrayAsync(cancellationToken);
        var objectById = activeObjects.ToDictionary(x => x.Id);
        var columnById = activeColumns.ToDictionary(x => x.Id);
        var objectBindingByIdentity = objectBindings
            .Where(x => Compatible(x, context)).ToDictionary(x => x.LogicalIdentity, StringComparer.Ordinal);
        var columnBindingByIdentity = columnBindings
            .Where(x => Compatible(x, context)).ToDictionary(x => x.LogicalIdentity, StringComparer.Ordinal);
        var boundObjectTargets = objectBindings.ToDictionary(x => x.DatabaseObjectId, x => x);
        var boundColumnTargets = columnBindings.ToDictionary(x => x.DatabaseColumnId, x => x);
        var schemas = snapshot.Schemas.ToDictionary(x => x.LogicalIdentity, x => x.Name, StringComparer.Ordinal);
        var candidates = new List<DatabaseDiscoveryReconciliationCandidateResponse>();

        foreach (var binding in objectBindings.Where(x => !Compatible(x, context)))
        {
            if (!objectById.TryGetValue(binding.DatabaseObjectId, out var target)) continue;
            var identityChanged = binding.IdentityAlgorithmVersion != context.SnapshotEntity.IdentityAlgorithmVersion;
            candidates.Add(new(Key(null, binding.LogicalIdentity), identityChanged ? "RebaselineRequired" : "OutOfScope",
                DatabaseDiscoveryEntityKind.DatabaseObject,
                identityChanged ? DatabaseDiscoveryReconciliationStatus.Conflict : DatabaseDiscoveryReconciliationStatus.NoAction,
                null, identityChanged ? "RebaselineRequired" : "OutOfScope",
                binding.SchemaLogicalIdentity, binding.LogicalIdentity, null,
                target.SchemaName, target.ObjectName, null, target.Id, tokenCodec.Encode(target.Version),
                identityChanged ? "技术身份算法版本已变化，必须重新建立基线。" : "绑定不属于当前 Scope Generation，不标记为来源未发现。"));
        }
        foreach (var binding in columnBindings.Where(x => !Compatible(x, context)))
        {
            if (!columnById.TryGetValue(binding.DatabaseColumnId, out var target)
                || !objectById.TryGetValue(target.DatabaseObjectId, out var parentTarget)) continue;
            var identityChanged = binding.IdentityAlgorithmVersion != context.SnapshotEntity.IdentityAlgorithmVersion;
            candidates.Add(new(Key(null, binding.LogicalIdentity), identityChanged ? "RebaselineRequired" : "OutOfScope",
                DatabaseDiscoveryEntityKind.Column,
                identityChanged ? DatabaseDiscoveryReconciliationStatus.Conflict : DatabaseDiscoveryReconciliationStatus.NoAction,
                null, identityChanged ? "RebaselineRequired" : "OutOfScope",
                binding.SchemaLogicalIdentity, binding.LogicalIdentity, binding.ParentObjectLogicalIdentity,
                parentTarget.SchemaName, parentTarget.ObjectName, target.ColumnName, target.Id, tokenCodec.Encode(target.Version),
                identityChanged ? "字段技术身份算法版本已变化，必须重新建立基线。" : "字段绑定不属于当前 Scope Generation，不标记为来源未发现。"));
        }

        foreach (var source in snapshot.Objects)
        {
            var schemaName = schemas.GetValueOrDefault(source.SchemaLogicalIdentity, source.SchemaName);
            if (objectBindingByIdentity.TryGetValue(source.LogicalIdentity, out var binding))
            {
                if (!objectById.TryGetValue(binding.DatabaseObjectId, out var target))
                {
                    candidates.Add(Candidate("Conflict", DatabaseDiscoveryEntityKind.DatabaseObject,
                        DatabaseDiscoveryReconciliationStatus.Conflict, null, "BoundTargetUnavailable", source,
                        null, null, "已绑定的数据库对象不存在或已删除。"));
                    continue;
                }
                if (!string.Equals(target.SchemaName, schemaName, StringComparison.Ordinal)
                    || !string.Equals(target.ObjectName, source.Name, StringComparison.Ordinal))
                {
                    candidates.Add(Candidate("Conflict", DatabaseDiscoveryEntityKind.DatabaseObject,
                        DatabaseDiscoveryReconciliationStatus.Conflict, null, "RenameNotSupported", source,
                        null, target, "发现身份对应的名称已变化；B04 不自动重命名知识对象。"));
                    continue;
                }
                if (binding.SourceMissingSinceSnapshotId is not null)
                    candidates.Add(Candidate("Reappeared", DatabaseDiscoveryEntityKind.DatabaseObject,
                        DatabaseDiscoveryReconciliationStatus.Applicable, DatabaseDiscoverySyncActionType.ClearObjectSourceMissing,
                        null, source, null, target, "来源对象重新出现，需显式清除来源未发现标记。"));
                if (!ObjectStructureMatches(target, context, source))
                {
                    var collision = activeObjects.Any(x => x.Id != target.Id
                        && string.Equals(x.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(x.ObjectName, source.Name, StringComparison.OrdinalIgnoreCase));
                    candidates.Add(Candidate(collision ? "Conflict" : "StructuralChange",
                        DatabaseDiscoveryEntityKind.DatabaseObject,
                        collision ? DatabaseDiscoveryReconciliationStatus.Conflict : DatabaseDiscoveryReconciliationStatus.Applicable,
                        collision ? null : DatabaseDiscoverySyncActionType.UpdateDatabaseObjectStructure,
                        collision ? "UnsupportedIdentifierCollision" : null, source, null, target,
                        collision ? "目标名称与现有知识对象发生大小写不敏感碰撞。" : "可更新外部来源拥有的对象结构字段。"));
                }
                else if (binding.SourceMissingSinceSnapshotId is null)
                    candidates.Add(Candidate("NoAction", DatabaseDiscoveryEntityKind.DatabaseObject,
                        DatabaseDiscoveryReconciliationStatus.NoAction, null, null, source, null, target, "对象结构已一致。"));
                continue;
            }

            var exact = activeObjects.Where(x => string.Equals(x.SchemaName, schemaName, StringComparison.Ordinal)
                && string.Equals(x.ObjectName, source.Name, StringComparison.Ordinal)).ToArray();
            var caseCollision = activeObjects.Any(x => string.Equals(x.SchemaName, schemaName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ObjectName, source.Name, StringComparison.OrdinalIgnoreCase));
            var priorBinding = exact.Length == 1 ? boundObjectTargets.GetValueOrDefault(exact[0].Id) : null;
            if (priorBinding is not null && !Compatible(priorBinding, context))
            {
                candidates.Add(Candidate("RebaselineRequired", DatabaseDiscoveryEntityKind.DatabaseObject,
                    DatabaseDiscoveryReconciliationStatus.Conflict, null, "RebaselineRequired", source,
                    null, exact[0], "现有对象绑定属于不兼容的 Scope 或技术身份版本，必须重新建立基线。"));
            }
            else if (exact.Length == 1 && exact[0].ObjectType.ToString() == source.ObjectType.ToString()
                && !boundObjectTargets.ContainsKey(exact[0].Id))
            {
                candidates.Add(Candidate("New", DatabaseDiscoveryEntityKind.DatabaseObject,
                    DatabaseDiscoveryReconciliationStatus.Applicable, DatabaseDiscoverySyncActionType.LinkExistingDatabaseObject,
                    null, source, null, exact[0], "可显式链接到 exact match 的现有知识对象。"));
            }
            else if (caseCollision)
            {
                candidates.Add(Candidate("Conflict", DatabaseDiscoveryEntityKind.DatabaseObject,
                    DatabaseDiscoveryReconciliationStatus.Conflict, null, "UnsupportedIdentifierCollision",
                    source, null, exact.FirstOrDefault(), "名称、类型或已有绑定不满足严格链接条件。"));
            }
            else
            {
                candidates.Add(Candidate("New", DatabaseDiscoveryEntityKind.DatabaseObject,
                    DatabaseDiscoveryReconciliationStatus.Applicable, DatabaseDiscoverySyncActionType.CreateDatabaseObject,
                    null, source, null, null, "可创建新的数据库知识对象。"));
            }
        }

        var snapshotObjectIds = snapshot.Objects.Select(x => x.LogicalIdentity).ToHashSet(StringComparer.Ordinal);
        foreach (var binding in objectBindingByIdentity.Values.Where(x => !snapshotObjectIds.Contains(x.LogicalIdentity)))
        {
            if (!objectById.TryGetValue(binding.DatabaseObjectId, out var target)) continue;
            var action = binding.SourceMissingSinceSnapshotId is null
                ? DatabaseDiscoverySyncActionType.MarkObjectSourceMissing : (DatabaseDiscoverySyncActionType?)null;
            candidates.Add(new(
                Key(action, binding.LogicalIdentity),
                action is null ? "NoAction" : "MissingFromSource",
                DatabaseDiscoveryEntityKind.DatabaseObject,
                action is null ? DatabaseDiscoveryReconciliationStatus.NoAction : DatabaseDiscoveryReconciliationStatus.Applicable,
                action, null, binding.SchemaLogicalIdentity, binding.LogicalIdentity, null,
                target.SchemaName, target.ObjectName, null, target.Id, tokenCodec.Encode(target.Version),
                action is null ? "对象已标记为来源未发现。" : "仅标记 binding，不删除或修改知识对象状态。"));
        }

        var snapshotObjects = snapshot.Objects.ToDictionary(x => x.LogicalIdentity, StringComparer.Ordinal);
        foreach (var source in snapshot.Columns)
        {
            if (!snapshotObjects.TryGetValue(source.ParentObjectLogicalIdentity, out var parent)) continue;
            var schemaName = schemas.GetValueOrDefault(parent.SchemaLogicalIdentity, parent.SchemaName);
            if (source.SourceOrdinal is null or <= 0)
            {
                candidates.Add(ColumnCandidate("Conflict", DatabaseDiscoveryReconciliationStatus.Conflict, null,
                    "UnsupportedOrdinal", parent, source, null, "来源字段缺少有效序号。"));
                continue;
            }
            if (columnBindingByIdentity.TryGetValue(source.LogicalIdentity, out var binding))
            {
                if (!columnById.TryGetValue(binding.DatabaseColumnId, out var target))
                {
                    candidates.Add(ColumnCandidate("Conflict", DatabaseDiscoveryReconciliationStatus.Conflict, null,
                        "BoundTargetUnavailable", parent, source, null, "已绑定的数据库字段不存在或已删除。"));
                    continue;
                }
                if (!string.Equals(target.ColumnName, source.Name, StringComparison.Ordinal))
                {
                    candidates.Add(ColumnCandidate("Conflict", DatabaseDiscoveryReconciliationStatus.Conflict, null,
                        "RenameNotSupported", parent, source, target,
                        "发现身份对应的字段名称已变化；B04 不自动重命名知识字段。"));
                    continue;
                }
                if (binding.SourceMissingSinceSnapshotId is not null)
                    candidates.Add(ColumnCandidate("Reappeared", DatabaseDiscoveryReconciliationStatus.Applicable,
                        DatabaseDiscoverySyncActionType.ClearColumnSourceMissing, null, parent, source, target,
                        "来源字段重新出现，需显式清除来源未发现标记。"));
                if (!ColumnStructureMatches(target, source))
                {
                    var ordinalConflict = activeColumns.Any(x => x.DatabaseObjectId == target.DatabaseObjectId
                        && x.Id != target.Id && x.OrdinalPosition == source.SourceOrdinal.Value);
                    candidates.Add(ColumnCandidate(ordinalConflict ? "Conflict" : "StructuralChange",
                        ordinalConflict ? DatabaseDiscoveryReconciliationStatus.Conflict : DatabaseDiscoveryReconciliationStatus.Applicable,
                        ordinalConflict ? null : DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure,
                        ordinalConflict ? "ActiveOrdinalConflict" : null, parent, source, target,
                        ordinalConflict ? "目标序号被未选择的活动字段占用，无法安全更新。" : "可更新外部来源拥有的字段结构。"));
                }
                else if (binding.SourceMissingSinceSnapshotId is null)
                    candidates.Add(ColumnCandidate("NoAction", DatabaseDiscoveryReconciliationStatus.NoAction,
                        null, null, parent, source, target, "字段结构已一致。"));
                continue;
            }

            DatabaseObject? targetParent = null;
            if (objectBindingByIdentity.TryGetValue(parent.LogicalIdentity, out var parentBinding))
                objectById.TryGetValue(parentBinding.DatabaseObjectId, out targetParent);
            if (targetParent is null)
                targetParent = activeObjects.SingleOrDefault(x => string.Equals(x.SchemaName, schemaName, StringComparison.Ordinal)
                    && string.Equals(x.ObjectName, parent.Name, StringComparison.Ordinal)
                    && x.ObjectType.ToString() == parent.ObjectType.ToString());
            if (targetParent is null)
            {
                candidates.Add(ColumnCandidate("New", DatabaseDiscoveryReconciliationStatus.Applicable,
                    DatabaseDiscoverySyncActionType.CreateDatabaseColumn, null, parent, source, null,
                    "将在对象创建或链接后创建字段。"));
                continue;
            }
            var parentColumns = activeColumns.Where(x => x.DatabaseObjectId == targetParent.Id).ToArray();
            var exact = parentColumns.Where(x => string.Equals(x.ColumnName, source.Name, StringComparison.Ordinal)
                && x.OrdinalPosition == source.SourceOrdinal.Value && ColumnStructureMatches(x, source)).ToArray();
            var collides = parentColumns.Any(x => string.Equals(x.ColumnName, source.Name, StringComparison.OrdinalIgnoreCase)
                || x.OrdinalPosition == source.SourceOrdinal.Value);
            var priorColumnBinding = exact.Length == 1 ? boundColumnTargets.GetValueOrDefault(exact[0].Id) : null;
            if (priorColumnBinding is not null && !Compatible(priorColumnBinding, context))
                candidates.Add(ColumnCandidate("RebaselineRequired", DatabaseDiscoveryReconciliationStatus.Conflict,
                    null, "RebaselineRequired", parent, source, exact[0],
                    "现有字段绑定属于不兼容的 Scope 或技术身份版本，必须重新建立基线。"));
            else if (exact.Length == 1 && !boundColumnTargets.ContainsKey(exact[0].Id))
                candidates.Add(ColumnCandidate("New", DatabaseDiscoveryReconciliationStatus.Applicable,
                    DatabaseDiscoverySyncActionType.LinkExistingDatabaseColumn, null, parent, source, exact[0],
                    "可显式链接到 exact match 的现有字段。"));
            else if (collides)
                candidates.Add(ColumnCandidate("Conflict", DatabaseDiscoveryReconciliationStatus.Conflict,
                    null, "UnsupportedIdentifierCollision", parent, source, exact.FirstOrDefault(),
                    "字段名称、序号、结构或已有绑定不满足严格链接条件。"));
            else
                candidates.Add(ColumnCandidate("New", DatabaseDiscoveryReconciliationStatus.Applicable,
                    DatabaseDiscoverySyncActionType.CreateDatabaseColumn, null, parent, source, null,
                    "可创建新的数据库知识字段。"));
        }

        var snapshotColumnIds = snapshot.Columns.Select(x => x.LogicalIdentity).ToHashSet(StringComparer.Ordinal);
        foreach (var binding in columnBindingByIdentity.Values.Where(x => !snapshotColumnIds.Contains(x.LogicalIdentity)))
        {
            if (!columnById.TryGetValue(binding.DatabaseColumnId, out var target)
                || !objectById.TryGetValue(target.DatabaseObjectId, out var parentTarget)) continue;
            var action = binding.SourceMissingSinceSnapshotId is null
                ? DatabaseDiscoverySyncActionType.MarkColumnSourceMissing : (DatabaseDiscoverySyncActionType?)null;
            candidates.Add(new(Key(action, binding.LogicalIdentity), action is null ? "NoAction" : "MissingFromSource",
                DatabaseDiscoveryEntityKind.Column,
                action is null ? DatabaseDiscoveryReconciliationStatus.NoAction : DatabaseDiscoveryReconciliationStatus.Applicable,
                action, null, binding.SchemaLogicalIdentity, binding.LogicalIdentity, binding.ParentObjectLogicalIdentity,
                parentTarget.SchemaName, parentTarget.ObjectName, target.ColumnName, target.Id, tokenCodec.Encode(target.Version),
                action is null ? "字段已标记为来源未发现。" : "仅标记 binding，不删除字段或修改知识状态。"));
        }

        AddUnsupported(candidates, context);
        return candidates;
    }

    private async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPreviewResponse>> BuildPreview(
        long planId,
        SyncContext context,
        IReadOnlyList<DatabaseDiscoverySyncSelectionRequest> selections,
        CancellationToken cancellationToken)
    {
        var selectionValidation = await ValidateSelections(context, selections, cancellationToken);
        if (selectionValidation is not null)
            return new(null, selectionValidation.FieldErrors, selectionValidation.Failure, selectionValidation.ReasonCode);

        var objectBindings = await dbContext.DatabaseObjectDiscoveryBindings.AsNoTracking()
            .Where(x => x.ProfileId == context.Profile.Id && x.ScopeGenerationId == context.SnapshotEntity.ScopeGenerationId
                && x.IdentityAlgorithmVersion == context.SnapshotEntity.IdentityAlgorithmVersion)
            .ToDictionaryAsync(x => x.LogicalIdentity, StringComparer.Ordinal, cancellationToken);
        var columnBindings = await dbContext.DatabaseColumnDiscoveryBindings.AsNoTracking()
            .Where(x => x.ProfileId == context.Profile.Id && x.ScopeGenerationId == context.SnapshotEntity.ScopeGenerationId
                && x.IdentityAlgorithmVersion == context.SnapshotEntity.IdentityAlgorithmVersion)
            .ToDictionaryAsync(x => x.LogicalIdentity, StringComparer.Ordinal, cancellationToken);
        var objectIds = objectBindings.Values.Select(x => x.DatabaseObjectId)
            .Concat(selections.Where(x => IsObjectAction(x.ActionType) && x.TargetId != null).Select(x => x.TargetId!.Value))
            .Distinct().ToArray();
        var objects = await dbContext.DatabaseObjects.IgnoreQueryFilters().AsNoTracking()
            .Where(x => objectIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var columnIds = columnBindings.Values.Select(x => x.DatabaseColumnId)
            .Concat(selections.Where(x => IsColumnAction(x.ActionType) && x.TargetId != null).Select(x => x.TargetId!.Value))
            .Distinct().ToArray();
        var columns = await dbContext.DatabaseColumns.IgnoreQueryFilters().AsNoTracking()
            .Where(x => columnIds.Contains(x.Id)).ToDictionaryAsync(x => x.Id, cancellationToken);
        var snapshotObjects = context.Snapshot.Objects.ToDictionary(x => x.LogicalIdentity, StringComparer.Ordinal);
        var snapshotColumns = context.Snapshot.Columns.ToDictionary(x => x.LogicalIdentity, StringComparer.Ordinal);
        var selectedObjectActions = selections.Where(x => IsObjectAction(x.ActionType))
            .GroupBy(x => x.LogicalIdentity, StringComparer.Ordinal)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.Ordinal);
        var previews = new List<DatabaseDiscoverySyncPreviewActionResponse>();

        foreach (var selection in selections)
        {
            if (IsObjectAction(selection.ActionType))
            {
                snapshotObjects.TryGetValue(selection.LogicalIdentity, out var source);
                objectBindings.TryGetValue(selection.LogicalIdentity, out var binding);
                DatabaseObject? target = null;
                var targetId = selection.TargetId ?? binding?.DatabaseObjectId;
                if (targetId is not null) objects.TryGetValue(targetId.Value, out target);
                var schemaLogical = source?.SchemaLogicalIdentity ?? binding?.SchemaLogicalIdentity ?? string.Empty;
                previews.Add(new(
                    selection.ActionType,
                    DatabaseDiscoveryEntityKind.DatabaseObject,
                    schemaLogical,
                    selection.LogicalIdentity,
                    null,
                    targetId,
                    target?.Version,
                    binding?.Version,
                    null,
                    null,
                    target is null ? null : ObjectStructure(target),
                    source is null ? null : ObjectStructure(context, source),
                    ActionSummary(selection.ActionType)));
                continue;
            }

            snapshotColumns.TryGetValue(selection.LogicalIdentity, out var columnSource);
            columnBindings.TryGetValue(selection.LogicalIdentity, out var columnBinding);
            DatabaseColumn? columnTarget = null;
            var columnTargetId = selection.TargetId ?? columnBinding?.DatabaseColumnId;
            if (columnTargetId is not null) columns.TryGetValue(columnTargetId.Value, out columnTarget);
            var parentLogical = columnSource?.ParentObjectLogicalIdentity ?? columnBinding?.ParentObjectLogicalIdentity;
            DatabaseObjectDiscoveryBinding? parentBinding = null;
            DatabaseObject? parentTarget = null;
            if (parentLogical is not null && objectBindings.TryGetValue(parentLogical, out parentBinding))
                objects.TryGetValue(parentBinding.DatabaseObjectId, out parentTarget);
            if (parentTarget is null && parentLogical is not null
                && selectedObjectActions.TryGetValue(parentLogical, out var parentAction)
                && parentAction.TargetId is not null)
            {
                objects.TryGetValue(parentAction.TargetId.Value, out parentTarget);
            }
            if (columnSource is not null && parentTarget is null
                && (!selectedObjectActions.TryGetValue(columnSource.ParentObjectLogicalIdentity, out var parentSelection)
                    || parentSelection.ActionType != DatabaseDiscoverySyncActionType.CreateDatabaseObject))
            {
                return new(null, null, DatabaseDiscoverySyncFailure.Conflict, "ParentObjectActionRequired");
            }
            previews.Add(new(
                selection.ActionType,
                DatabaseDiscoveryEntityKind.Column,
                columnSource is null
                    ? columnBinding?.SchemaLogicalIdentity ?? string.Empty
                    : snapshotObjects[columnSource.ParentObjectLogicalIdentity].SchemaLogicalIdentity,
                selection.LogicalIdentity,
                parentLogical,
                columnTargetId,
                columnTarget?.Version,
                columnBinding?.Version,
                parentTarget?.Id,
                parentTarget?.Version,
                columnTarget is null ? null : ColumnStructure(columnTarget),
                columnSource is null ? null : ColumnStructure(columnSource),
                ActionSummary(selection.ActionType)));
        }

        var collision = await ValidatePlanCollisions(context, previews, cancellationToken);
        if (collision is not null) return collision;
        var ordered = previews.OrderBy(x => x.ActionType).ThenBy(x => x.LogicalIdentity, StringComparer.Ordinal).ToArray();
        var counts = Counts(ordered);
        var hashPayload = new PreviewHashPayload(
            1, context.Profile.Id, context.Profile.DatabaseSourceId, context.Profile.ConfigurationRevision, context.SnapshotEntity.Id,
            context.SnapshotEntity.ContentSha256, context.SnapshotEntity.ScopeGenerationId,
            context.SnapshotEntity.IdentityAlgorithmVersion, ordered);
        var canonical = JsonSerializer.Serialize(hashPayload, JsonOptions);
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical))).ToLowerInvariant();
        var warnings = context.Snapshot.ForeignKeys.Count + context.Snapshot.UniqueConstraints.Count
            + context.Snapshot.Indexes.Count + context.Snapshot.Sequences.Count > 0
            ? new[] { "外键、唯一约束、索引与序列仅供审查，本计划不会写入这些结构。" }
            : Array.Empty<string>();
        return new(new(planId, context.SnapshotEntity.Id, context.SnapshotEntity.ScopeGenerationId,
            hash, counts, ordered, warnings), null, DatabaseDiscoverySyncFailure.None);
    }

    private async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPlanResponse>?> ValidateSelections(
        SyncContext context,
        IReadOnlyList<DatabaseDiscoverySyncSelectionRequest> actions,
        CancellationToken cancellationToken)
    {
        var candidates = await BuildCandidates(context, cancellationToken);
        foreach (var action in actions)
        {
            var matches = candidates.Where(x => x.Status == DatabaseDiscoveryReconciliationStatus.Applicable
                && x.SuggestedAction == action.ActionType
                && string.Equals(x.LogicalIdentity, action.LogicalIdentity, StringComparison.Ordinal)).ToArray();
            if (matches.Length != 1)
                return new(null, null, DatabaseDiscoverySyncFailure.Conflict, "SelectionNoLongerApplicable");
            var candidate = matches[0];
            if (action.ActionType is DatabaseDiscoverySyncActionType.LinkExistingDatabaseObject or DatabaseDiscoverySyncActionType.LinkExistingDatabaseColumn)
            {
                if (action.TargetId is null || candidate.TargetId != action.TargetId)
                    return new(null, null, DatabaseDiscoverySyncFailure.Conflict, "ExactLinkTargetRequired");
            }
            else if (action.TargetId is not null && candidate.TargetId != action.TargetId)
            {
                return new(null, null, DatabaseDiscoverySyncFailure.Conflict, "TargetMismatch");
            }
        }
        return null;
    }

    private async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPreviewResponse>?> ValidatePlanCollisions(
        SyncContext context,
        IReadOnlyList<DatabaseDiscoverySyncPreviewActionResponse> actions,
        CancellationToken cancellationToken)
    {
        var activeObjects = await dbContext.DatabaseObjects.AsNoTracking()
            .Where(x => x.DatabaseSourceId == context.Profile.DatabaseSourceId).ToArrayAsync(cancellationToken);
        var finalObjects = actions.Where(x => x.EntityKind == DatabaseDiscoveryEntityKind.DatabaseObject
                && x.After is not null
                && x.ActionType is DatabaseDiscoverySyncActionType.CreateDatabaseObject or DatabaseDiscoverySyncActionType.UpdateDatabaseObjectStructure)
            .ToArray();
        foreach (var action in finalObjects)
        {
            if (activeObjects.Any(x => x.Id != action.TargetId
                && string.Equals(x.SchemaName, action.After!.SchemaName, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.ObjectName, action.After.Name, StringComparison.OrdinalIgnoreCase)))
                return new(null, null, DatabaseDiscoverySyncFailure.UnsupportedIdentifierCollision, "UnsupportedIdentifierCollision");
        }
        var finalColumns = actions.Where(x => x.EntityKind == DatabaseDiscoveryEntityKind.Column && x.After is not null
            && x.ActionType is DatabaseDiscoverySyncActionType.CreateDatabaseColumn or DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure).ToArray();
        foreach (var parent in finalColumns.GroupBy(x => x.ExpectedParentTargetId))
        {
            if (parent.Key is null) continue;
            var active = await dbContext.DatabaseColumns.AsNoTracking()
                .Where(x => x.DatabaseObjectId == parent.Key.Value).ToArrayAsync(cancellationToken);
            var selectedIds = parent.Where(x => x.TargetId != null).Select(x => x.TargetId!.Value).ToHashSet();
            foreach (var action in parent)
            {
                if (active.Any(x => !selectedIds.Contains(x.Id)
                    && (string.Equals(x.ColumnName, action.After!.Name, StringComparison.OrdinalIgnoreCase)
                        || x.OrdinalPosition == action.After.OrdinalPosition)))
                    return new(null, null, DatabaseDiscoverySyncFailure.OrdinalCollision, "OrdinalOrIdentifierCollision");
            }
            if (parent.GroupBy(x => x.After!.Name, StringComparer.OrdinalIgnoreCase).Any(x => x.Count() > 1)
                || parent.GroupBy(x => x.After!.OrdinalPosition).Any(x => x.Count() > 1))
                return new(null, null, DatabaseDiscoverySyncFailure.OrdinalCollision, "OrdinalOrIdentifierCollision");
            var updateCount = parent.Count(x => x.ActionType == DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure);
            if (updateCount > 0)
            {
                var maximum = active.Length == 0 ? 0 : active.Max(x => x.OrdinalPosition);
                if (maximum > int.MaxValue - updateCount)
                    return new(null, null, DatabaseDiscoverySyncFailure.OrdinalCollision, "UnsupportedOrdinal");
            }
        }
        return null;
    }

    private async Task<SyncContext?> LoadContext(long profileId, CancellationToken cancellationToken)
    {
        var profile = await dbContext.DatabaseConnectionProfiles.AsNoTracking()
            .Include(x => x.DatabaseSource)
            .SingleOrDefaultAsync(x => x.Id == profileId, cancellationToken);
        if (profile is null) return null;
        var snapshot = await dbContext.DatabaseDiscoverySnapshots.AsNoTracking().Include(x => x.Run)
            .Where(x => x.ProfileId == profileId)
            .OrderByDescending(x => x.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (snapshot is null) return null;
        var content = JsonSerializer.Deserialize<CanonicalDatabaseDiscoverySnapshot>(snapshot.CanonicalContentJson, JsonOptions)
            ?? throw new InvalidOperationException("Canonical discovery snapshot content is invalid.");
        var difference = await dbContext.DatabaseDiscoveryDifferences.AsNoTracking()
            .Where(x => x.TargetSnapshotId == snapshot.Id)
            .Select(x => new { x.Id, x.BaseSnapshotId })
            .SingleOrDefaultAsync(cancellationToken);
        return new(profile, snapshot, difference?.Id, difference?.BaseSnapshotId, content);
    }

    private IQueryable<DatabaseDiscoverySyncPlan> PlanQuery() =>
        dbContext.DatabaseDiscoverySyncPlans.AsNoTracking()
            .Include(x => x.Profile).ThenInclude(x => x.DatabaseSource)
            .Include(x => x.ApplyResult);

    private async Task<DatabaseDiscoverySyncPlanResponse> ToResponse(
        DatabaseDiscoverySyncPlan plan,
        CancellationToken cancellationToken)
    {
        var loaded = await PlanQuery().SingleAsync(x => x.Id == plan.Id, cancellationToken);
        return ToResponse(loaded);
    }

    private DatabaseDiscoverySyncPlanResponse ToResponse(DatabaseDiscoverySyncPlan plan)
    {
        var selections = DeserializeSelections(plan.SelectionJson);
        var preview = plan.PreviewPayloadJson is null
            ? null
            : JsonSerializer.Deserialize<DatabaseDiscoverySyncPreviewResponse>(plan.PreviewPayloadJson, JsonOptions);
        var result = plan.ApplyResult is null ? null : new DatabaseDiscoverySyncApplyResultResponse(
            plan.ApplyResult.CreatedObjects, plan.ApplyResult.LinkedObjects,
            plan.ApplyResult.CreatedColumns, plan.ApplyResult.LinkedColumns,
            plan.ApplyResult.UpdatedObjects, plan.ApplyResult.UpdatedColumns,
            plan.ApplyResult.MarkedMissing, plan.ApplyResult.ClearedMissing,
            plan.ApplyResult.AppliedAt, plan.ApplyResult.AppliedByDisplayName);
        return new(plan.Id, plan.ProfileId, plan.Profile.Name, plan.DatabaseSourceId,
            plan.Profile.DatabaseSource.Name, plan.ProfileConfigurationRevision, plan.BaseSnapshotId,
            plan.TargetSnapshotId, plan.TargetDifferenceId,
            plan.ScopeGenerationId, plan.IdentityAlgorithmVersion, plan.Status, selections,
            preview, plan.ConfirmedPreviewHash, plan.CreatedAt, plan.UpdatedAt,
            plan.ConfirmedAt, plan.AppliedAt, result, tokenCodec.Encode(plan.Version));
    }

    private async Task<DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPlanResponse>> Supersede(
        DatabaseDiscoverySyncPlan plan,
        DatabaseDiscoverySyncActor actor,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        await SupersedeInTransaction(plan, actor, reasonCode, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(null, null, DatabaseDiscoverySyncFailure.LatestSnapshotChanged, reasonCode);
    }

    private async Task SupersedeInTransaction(
        DatabaseDiscoverySyncPlan plan,
        DatabaseDiscoverySyncActor actor,
        string reasonCode,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        plan.Status = DatabaseDiscoverySyncPlanStatus.Superseded;
        plan.UpdatedAt = now;
        plan.Version++;
        AddAudit(plan, DatabaseDiscoverySyncAuditAction.PlanSuperseded, actor, now, new { reasonCode },
            DatabaseConnectionAuditOutcome.Superseded, reasonCode);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private void AddAudit(
        DatabaseDiscoverySyncPlan plan,
        DatabaseDiscoverySyncAuditAction action,
        DatabaseDiscoverySyncActor actor,
        DateTimeOffset now,
        object? metadata,
        DatabaseConnectionAuditOutcome outcome = DatabaseConnectionAuditOutcome.Succeeded,
        string? reasonCode = null)
    {
        dbContext.DatabaseDiscoverySyncAuditEvents.Add(new DatabaseDiscoverySyncAuditEvent
        {
            ProfileId = plan.ProfileId,
            Plan = plan,
            Action = action,
            Outcome = outcome,
            ReasonCode = reasonCode,
            SafeMetadataJson = metadata is null ? null : JsonSerializer.Serialize(metadata, JsonOptions),
            ActorUserId = actor.UserId,
            ActorDisplayName = actor.DisplayName,
            OccurredAt = now,
        });
    }

    private DatabaseObjectDiscoveryBinding AddObjectBinding(
        DatabaseDiscoverySyncPlan plan,
        SyncContext context,
        CanonicalDatabaseObject source,
        long targetId,
        DateTimeOffset now)
    {
        var binding = new DatabaseObjectDiscoveryBinding
        {
            ProfileId = plan.ProfileId,
            ScopeGenerationId = plan.ScopeGenerationId,
            IdentityAlgorithmVersion = plan.IdentityAlgorithmVersion,
            SchemaLogicalIdentity = source.SchemaLogicalIdentity,
            LogicalIdentity = source.LogicalIdentity,
            DatabaseObjectId = targetId,
            FirstAppliedSnapshotId = plan.TargetSnapshotId,
            LastAppliedSnapshotId = plan.TargetSnapshotId,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        dbContext.DatabaseObjectDiscoveryBindings.Add(binding);
        return binding;
    }

    private DatabaseColumnDiscoveryBinding AddColumnBinding(
        DatabaseDiscoverySyncPlan plan,
        SyncContext context,
        CanonicalColumn source,
        long targetId,
        DateTimeOffset now)
    {
        var parent = context.Snapshot.Objects.Single(x => x.LogicalIdentity == source.ParentObjectLogicalIdentity);
        var binding = new DatabaseColumnDiscoveryBinding
        {
            ProfileId = plan.ProfileId,
            ScopeGenerationId = plan.ScopeGenerationId,
            IdentityAlgorithmVersion = plan.IdentityAlgorithmVersion,
            SchemaLogicalIdentity = parent.SchemaLogicalIdentity,
            ParentObjectLogicalIdentity = source.ParentObjectLogicalIdentity,
            LogicalIdentity = source.LogicalIdentity,
            DatabaseColumnId = targetId,
            FirstAppliedSnapshotId = plan.TargetSnapshotId,
            LastAppliedSnapshotId = plan.TargetSnapshotId,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        dbContext.DatabaseColumnDiscoveryBindings.Add(binding);
        return binding;
    }

    private static void Touch(DatabaseObjectDiscoveryBinding binding, long snapshotId, DateTimeOffset now)
    {
        binding.LastAppliedSnapshotId = snapshotId;
        binding.UpdatedAt = now;
        binding.Version++;
    }

    private static void Touch(DatabaseColumnDiscoveryBinding binding, long snapshotId, DateTimeOffset now)
    {
        binding.LastAppliedSnapshotId = snapshotId;
        binding.UpdatedAt = now;
        binding.Version++;
    }

    private static DatabaseObject CreateObject(
        DatabaseDiscoverySyncPlan plan,
        SyncContext context,
        CanonicalDatabaseObject source,
        DatabaseDiscoverySyncActor actor,
        DateTimeOffset now)
    {
        var target = new DatabaseObject
        {
            DatabaseSourceId = plan.DatabaseSourceId,
            SchemaName = source.SchemaName,
            ObjectName = source.Name,
            ObjectType = Enum.Parse<DatabaseObjectType>(source.ObjectType.ToString()),
            DatabaseComment = source.DatabaseComment,
            TechnicalIdentityAlgorithmVersion = plan.IdentityAlgorithmVersion,
            TechnicalIdentity = source.LogicalIdentity,
            AccessMode = DatabaseAccessMode.Unknown,
            PrimaryKeyColumnsJson = SerializePrimaryKeyNames(context, source.LogicalIdentity),
            CreatedAt = now,
            CreatedByUserId = actor.UserId,
            CreatedByName = actor.DisplayName,
            CreatedByRole = actor.Role,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = actor.DisplayName,
            KnowledgeStatusChangedByRole = actor.Role ?? "Editor",
            Version = 1,
        };
        return target;
    }

    private static DatabaseColumn CreateColumn(
        DatabaseDiscoverySyncPlan plan,
        CanonicalColumn source,
        long parentId,
        DatabaseDiscoverySyncActor actor,
        DateTimeOffset now) => new()
    {
        DatabaseObjectId = parentId,
        OrdinalPosition = source.SourceOrdinal!.Value,
        ColumnName = source.Name,
        DataType = source.NativeDataType.Declaration,
        IsNullable = source.IsNullable,
        DefaultValue = source.DefaultExpression,
        DatabaseComment = source.DatabaseComment,
        TechnicalIdentityAlgorithmVersion = plan.IdentityAlgorithmVersion,
        TechnicalIdentity = source.LogicalIdentity,
        CreatedAt = now,
        CreatedByUserId = actor.UserId,
        CreatedByDisplayName = actor.DisplayName,
        UpdatedAt = now,
        KnowledgeStatus = KnowledgeStatus.Unknown,
        KnowledgeStatusChangedAt = now,
        KnowledgeStatusChangedByName = actor.DisplayName,
        KnowledgeStatusChangedByRole = actor.Role ?? "Editor",
        Version = 1,
    };

    private static void ApplyObjectStructure(
        DatabaseObject target, SyncContext context, CanonicalDatabaseObject source, DateTimeOffset now)
    {
        target.ObjectType = Enum.Parse<DatabaseObjectType>(source.ObjectType.ToString());
        target.DatabaseComment = source.DatabaseComment;
        target.PrimaryKeyColumnsJson = SerializePrimaryKeyNames(context, source.LogicalIdentity);
        target.TechnicalIdentityAlgorithmVersion = context.SnapshotEntity.IdentityAlgorithmVersion;
        target.TechnicalIdentity = source.LogicalIdentity;
        target.UpdatedAt = now;
        target.Version++;
    }

    private static void ApplyColumnStructure(
        DatabaseColumn target, CanonicalColumn source, int identityAlgorithmVersion, DateTimeOffset now)
    {
        target.OrdinalPosition = source.SourceOrdinal!.Value;
        target.DataType = source.NativeDataType.Declaration;
        target.IsNullable = source.IsNullable;
        target.DefaultValue = source.DefaultExpression;
        target.DatabaseComment = source.DatabaseComment;
        target.TechnicalIdentityAlgorithmVersion = identityAlgorithmVersion;
        target.TechnicalIdentity = source.LogicalIdentity;
        target.UpdatedAt = now;
        target.Version++;
    }

    private static DatabaseDiscoveryReconciliationCandidateResponse Candidate(
        string category,
        DatabaseDiscoveryEntityKind kind,
        DatabaseDiscoveryReconciliationStatus status,
        DatabaseDiscoverySyncActionType? action,
        string? blockCode,
        CanonicalDatabaseObject source,
        string? childName,
        DatabaseObject? target,
        string summary) => new(
            Key(action, source.LogicalIdentity), category, kind, status, action, blockCode,
            source.SchemaLogicalIdentity, source.LogicalIdentity, null, source.SchemaName, source.Name,
            childName, target?.Id, target is null ? null : EncodeVersion(target.Version), summary);

    private DatabaseDiscoveryReconciliationCandidateResponse ColumnCandidate(
        string category,
        DatabaseDiscoveryReconciliationStatus status,
        DatabaseDiscoverySyncActionType? action,
        string? blockCode,
        CanonicalDatabaseObject parent,
        CanonicalColumn source,
        DatabaseColumn? target,
        string summary) => new(
            Key(action, source.LogicalIdentity), category, DatabaseDiscoveryEntityKind.Column, status,
            action, blockCode, parent.SchemaLogicalIdentity, source.LogicalIdentity,
            source.ParentObjectLogicalIdentity, parent.SchemaName, parent.Name, source.Name,
            target?.Id, target is null ? null : tokenCodec.Encode(target.Version), summary);

    private void AddUnsupported(
        ICollection<DatabaseDiscoveryReconciliationCandidateResponse> candidates,
        SyncContext context)
    {
        var objects = context.Snapshot.Objects.ToDictionary(x => x.LogicalIdentity, StringComparer.Ordinal);
        foreach (var item in context.Snapshot.ForeignKeys.Select(x => (DatabaseDiscoveryEntityKind.ForeignKey, x.LogicalIdentity, x.ParentObjectLogicalIdentity, x.Name))
            .Concat(context.Snapshot.UniqueConstraints.Select(x => (DatabaseDiscoveryEntityKind.UniqueConstraint, x.LogicalIdentity, x.ParentObjectLogicalIdentity, x.Name)))
            .Concat(context.Snapshot.Indexes.Select(x => (DatabaseDiscoveryEntityKind.Index, x.LogicalIdentity, x.ParentObjectLogicalIdentity, x.Name))))
        {
            if (!objects.TryGetValue(item.ParentObjectLogicalIdentity, out var parent)) continue;
            candidates.Add(new(Key(null, item.LogicalIdentity), "Unsupported", item.Item1,
                DatabaseDiscoveryReconciliationStatus.Unsupported, null, "ReviewOnlyStructure",
                parent.SchemaLogicalIdentity, item.LogicalIdentity, parent.LogicalIdentity,
                parent.SchemaName, parent.Name, item.Name, null, null,
                "当前结构仅供审查，不进入 B04 同步计划。"));
        }
        foreach (var item in context.Snapshot.Sequences)
        {
            var schemaName = context.Snapshot.Schemas.SingleOrDefault(x => x.LogicalIdentity == item.SchemaLogicalIdentity)?.Name ?? string.Empty;
            candidates.Add(new(Key(null, item.LogicalIdentity), "Unsupported", DatabaseDiscoveryEntityKind.Sequence,
                DatabaseDiscoveryReconciliationStatus.Unsupported, null, "ReviewOnlyStructure",
                item.SchemaLogicalIdentity, item.LogicalIdentity, null, schemaName, item.Name, null,
                null, null, "序列仅供审查，不进入 B04 同步计划。"));
        }
    }

    private static DatabaseDiscoverySyncStructureResponse ObjectStructure(DatabaseObject target) => new(
        target.SchemaName, target.ObjectName, target.ObjectType.ToString(), target.DatabaseComment,
        DeserializeNames(target.PrimaryKeyColumnsJson), null, null, null, null);

    private static DatabaseDiscoverySyncStructureResponse ObjectStructure(
        SyncContext context, CanonicalDatabaseObject source) => new(
        source.SchemaName, source.Name, source.ObjectType.ToString(), source.DatabaseComment,
        PrimaryKeyNames(context, source.LogicalIdentity), null, null, null, null);

    private static DatabaseDiscoverySyncStructureResponse ColumnStructure(DatabaseColumn target) => new(
        null, target.ColumnName, null, target.DatabaseComment, null, target.OrdinalPosition,
        target.DataType, target.IsNullable, target.DefaultValue);

    private static DatabaseDiscoverySyncStructureResponse ColumnStructure(CanonicalColumn source) => new(
        null, source.Name, null, source.DatabaseComment, null, source.SourceOrdinal,
        source.NativeDataType.Declaration, source.IsNullable, source.DefaultExpression);

    private static bool ObjectStructureMatches(
        DatabaseObject target, SyncContext context, CanonicalDatabaseObject source) =>
        string.Equals(target.SchemaName, source.SchemaName, StringComparison.Ordinal)
        && string.Equals(target.ObjectName, source.Name, StringComparison.Ordinal)
        && target.ObjectType.ToString() == source.ObjectType.ToString()
        && string.Equals(target.DatabaseComment, source.DatabaseComment, StringComparison.Ordinal)
        && DeserializeNames(target.PrimaryKeyColumnsJson).SequenceEqual(PrimaryKeyNames(context, source.LogicalIdentity), StringComparer.Ordinal);

    private static bool ColumnStructureMatches(DatabaseColumn target, CanonicalColumn source) =>
        source.SourceOrdinal == target.OrdinalPosition
        && string.Equals(target.ColumnName, source.Name, StringComparison.Ordinal)
        && string.Equals(target.DataType, source.NativeDataType.Declaration, StringComparison.Ordinal)
        && target.IsNullable == source.IsNullable
        && string.Equals(target.DefaultValue, source.DefaultExpression, StringComparison.Ordinal)
        && string.Equals(target.DatabaseComment, source.DatabaseComment, StringComparison.Ordinal);

    private static IReadOnlyList<string> PrimaryKeyNames(SyncContext context, string objectIdentity)
    {
        var primaryKey = context.Snapshot.PrimaryKeys.SingleOrDefault(x => x.ParentObjectLogicalIdentity == objectIdentity);
        if (primaryKey is null) return Array.Empty<string>();
        var columns = context.Snapshot.Columns.ToDictionary(x => x.LogicalIdentity, x => x.Name, StringComparer.Ordinal);
        return primaryKey.ColumnLogicalIdentities.Select(x => columns[x]).ToArray();
    }

    private static string? SerializePrimaryKeyNames(SyncContext context, string objectIdentity)
    {
        var names = PrimaryKeyNames(context, objectIdentity);
        return names.Count == 0 ? null : JsonSerializer.Serialize(names, JsonOptions);
    }

    private static IReadOnlyList<string> DeserializeNames(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return Array.Empty<string>();
        return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? Array.Empty<string>();
    }

    private static bool Compatible(DatabaseObjectDiscoveryBinding binding, SyncContext context) =>
        binding.ScopeGenerationId == context.SnapshotEntity.ScopeGenerationId
        && binding.IdentityAlgorithmVersion == context.SnapshotEntity.IdentityAlgorithmVersion;

    private static bool Compatible(DatabaseColumnDiscoveryBinding binding, SyncContext context) =>
        binding.ScopeGenerationId == context.SnapshotEntity.ScopeGenerationId
        && binding.IdentityAlgorithmVersion == context.SnapshotEntity.IdentityAlgorithmVersion;

    private static bool IsObjectAction(DatabaseDiscoverySyncActionType action) => action is
        DatabaseDiscoverySyncActionType.CreateDatabaseObject or DatabaseDiscoverySyncActionType.LinkExistingDatabaseObject
        or DatabaseDiscoverySyncActionType.UpdateDatabaseObjectStructure or DatabaseDiscoverySyncActionType.MarkObjectSourceMissing
        or DatabaseDiscoverySyncActionType.ClearObjectSourceMissing;

    private static bool IsColumnAction(DatabaseDiscoverySyncActionType action) => !IsObjectAction(action);

    private static string Key(DatabaseDiscoverySyncActionType? action, string logicalIdentity) =>
        $"{action?.ToString() ?? "none"}:{logicalIdentity}";

    private static string ActionSummary(DatabaseDiscoverySyncActionType action) => action switch
    {
        DatabaseDiscoverySyncActionType.CreateDatabaseObject => "创建数据库对象",
        DatabaseDiscoverySyncActionType.LinkExistingDatabaseObject => "链接现有数据库对象",
        DatabaseDiscoverySyncActionType.CreateDatabaseColumn => "创建数据库字段",
        DatabaseDiscoverySyncActionType.LinkExistingDatabaseColumn => "链接现有数据库字段",
        DatabaseDiscoverySyncActionType.UpdateDatabaseObjectStructure => "更新对象结构",
        DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure => "更新字段结构",
        DatabaseDiscoverySyncActionType.MarkObjectSourceMissing => "标记对象来源未发现",
        DatabaseDiscoverySyncActionType.ClearObjectSourceMissing => "清除对象来源未发现标记",
        DatabaseDiscoverySyncActionType.MarkColumnSourceMissing => "标记字段来源未发现",
        DatabaseDiscoverySyncActionType.ClearColumnSourceMissing => "清除字段来源未发现标记",
        _ => throw new ArgumentOutOfRangeException(nameof(action)),
    };

    private static DatabaseDiscoverySyncPreviewCounts Counts(
        IReadOnlyList<DatabaseDiscoverySyncPreviewActionResponse> actions) => new(
        actions.Count(x => x.ActionType == DatabaseDiscoverySyncActionType.CreateDatabaseObject),
        actions.Count(x => x.ActionType == DatabaseDiscoverySyncActionType.LinkExistingDatabaseObject),
        actions.Count(x => x.ActionType == DatabaseDiscoverySyncActionType.CreateDatabaseColumn),
        actions.Count(x => x.ActionType == DatabaseDiscoverySyncActionType.LinkExistingDatabaseColumn),
        actions.Count(x => x.ActionType == DatabaseDiscoverySyncActionType.UpdateDatabaseObjectStructure),
        actions.Count(x => x.ActionType == DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure),
        actions.Count(x => x.ActionType is DatabaseDiscoverySyncActionType.MarkObjectSourceMissing or DatabaseDiscoverySyncActionType.MarkColumnSourceMissing),
        actions.Count(x => x.ActionType is DatabaseDiscoverySyncActionType.ClearObjectSourceMissing or DatabaseDiscoverySyncActionType.ClearColumnSourceMissing));

    private static IReadOnlyList<DatabaseDiscoverySyncSelectionRequest> NormalizeSelections(
        IReadOnlyList<DatabaseDiscoverySyncSelectionRequest> actions) => actions
        .Select(x => new DatabaseDiscoverySyncSelectionRequest(x.ActionType, x.LogicalIdentity.Trim(), x.TargetId))
        .OrderBy(x => x.ActionType).ThenBy(x => x.LogicalIdentity, StringComparer.Ordinal).ToArray();

    private static IReadOnlyList<DatabaseDiscoverySyncSelectionRequest> DeserializeSelections(string json) =>
        JsonSerializer.Deserialize<DatabaseDiscoverySyncSelectionRequest[]>(json, JsonOptions) ?? [];

    private Dictionary<string, string[]> ValidateSelectionRequest(CreateDatabaseDiscoverySyncPlanRequest request)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ProfileId <= 0) errors["profileId"] = ["连接配置 ID 无效。"];
        if (request.TargetSnapshotId <= 0) errors["targetSnapshotId"] = ["目标快照 ID 无效。"];
        foreach (var error in ValidateActions(request.Actions)) errors[error.Key] = error.Value;
        return errors;
    }

    private Dictionary<string, string[]> ValidateActions(IReadOnlyList<DatabaseDiscoverySyncSelectionRequest>? actions)
    {
        var errors = new Dictionary<string, string[]>();
        if (actions is null || actions.Count == 0) errors["actions"] = ["至少选择一个同步操作。"];
        else if (actions.Count > settings.MaximumSyncPlanActions)
            errors["actions"] = [$"同步计划最多包含 {settings.MaximumSyncPlanActions} 个操作。"];
        else if (actions.Any(x => !Enum.IsDefined(x.ActionType) || string.IsNullOrWhiteSpace(x.LogicalIdentity)
            || x.LogicalIdentity.Length > 2048 || x.TargetId is <= 0))
            errors["actions"] = ["同步操作包含无效类型、技术身份或目标 ID。"];
        else if (actions.GroupBy(x => $"{(int)x.ActionType}\u001f{x.LogicalIdentity}", StringComparer.Ordinal).Any(x => x.Count() > 1))
            errors["actions"] = ["同步操作不能重复。"];
        return errors;
    }

    private static DatabaseDiscoverySyncOperationResult<T> Validation<T>(string field, string message) =>
        new(default, new Dictionary<string, string[]> { [field] = [message] }, DatabaseDiscoverySyncFailure.Validation);

    private static bool IsSha256(string? value) => value is { Length: 64 }
        && value.All(character => character is >= '0' and <= '9' or >= 'a' and <= 'f');

    private static bool FixedEquals(string left, string right) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(left), Encoding.ASCII.GetBytes(right));

    private static string EncodeVersion(long version)
    {
        Span<byte> bytes = stackalloc byte[sizeof(long)];
        System.Buffers.Binary.BinaryPrimitives.WriteInt64BigEndian(bytes, version);
        return $"v1_{Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
    }

    private static DatabaseDiscoveryOptions Validate(DatabaseDiscoveryOptions value)
    {
        value.Validate();
        return value;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var result = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        result.Converters.Add(new JsonStringEnumConverter());
        return result;
    }

    private sealed record SyncContext(
        DatabaseConnectionProfile Profile,
        DatabaseDiscoverySnapshot SnapshotEntity,
        long? DifferenceId,
        long? BaseSnapshotId,
        CanonicalDatabaseDiscoverySnapshot Snapshot);

    private sealed record PreviewHashPayload(
        int FormatVersion,
        long ProfileId,
        long DatabaseSourceId,
        long ProfileConfigurationRevision,
        long TargetSnapshotId,
        string TargetSnapshotHash,
        long ScopeGenerationId,
        int IdentityAlgorithmVersion,
        IReadOnlyList<DatabaseDiscoverySyncPreviewActionResponse> Actions);

}
