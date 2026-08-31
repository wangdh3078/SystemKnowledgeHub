using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public sealed class DatabaseDiscoveryRunService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec tokenCodec,
    CanonicalSnapshotService canonical,
    DatabaseDiscoveryDiffService diffService)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<DatabaseDiscoveryOperationResult<DatabaseDiscoveryRunResponse>> Trigger(
        long profileId,
        string? concurrencyToken,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(profileId)) errors["profileId"] = ["连接配置必须是有效 ID。"];
        if (!tokenCodec.TryDecode(concurrencyToken, out var expectedVersion))
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0) return Validation<DatabaseDiscoveryRunResponse>(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var profile = await dbContext.DatabaseConnectionProfiles
            .Include(item => item.Secret)
            .SingleOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.NotFound);
        if (profile.Version != expectedVersion) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.ConcurrencyConflict);
        if (!profile.IsEnabled) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.Disabled);
        if (!await dbContext.DatabaseSources.AnyAsync(item => item.Id == profile.DatabaseSourceId, cancellationToken))
            return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.ReferenceInvalid);
        if (profile.Secret?.ProtectedPayload is null) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.SecretMissing);
        if (await HasActiveRun(profile.Id, cancellationToken))
            return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.DiscoveryAlreadyRunning);

        var now = DateTimeOffset.UtcNow;
        var run = new DatabaseDiscoveryRun
        {
            ProfileId = profile.Id,
            ProfileConfigurationRevision = profile.ConfigurationRevision,
            SecretVersion = profile.Secret.Version,
            QueuedAt = now,
            Status = DatabaseDiscoveryRunStatus.Queued,
            ProviderType = profile.ProviderType,
            RequestedIncludedSchemasJson = profile.IncludedSchemasJson,
            RequestedProviderSpecificOptionsJson = profile.ProviderSpecificOptionsJson,
            RequestedByUserId = actor.Creator.UserId,
            RequestedByDisplayName = actor.Creator.DisplayName,
            Version = 1,
        };
        dbContext.DatabaseDiscoveryRuns.Add(run);
        dbContext.DatabaseConnectionAuditEvents.Add(new DatabaseConnectionAuditEvent
        {
            ProfileId = profile.Id,
            Action = DatabaseConnectionAuditAction.DiscoveryRunTriggered,
            Outcome = DatabaseConnectionAuditOutcome.Succeeded,
            ActorUserId = actor.Creator.UserId,
            ActorDisplayName = actor.Creator.DisplayName,
            OccurredAt = now,
        });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.DiscoveryAlreadyRunning);
        }

        return Success(await ToResponse(run, profile, cancellationToken));
    }

    public async Task<DatabaseDiscoveryOperationResult<DatabaseDiscoveryRunResponse>> Cancel(
        long runId,
        string? concurrencyToken,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(runId)) errors["runId"] = ["发现运行必须是有效 ID。"];
        if (!tokenCodec.TryDecode(concurrencyToken, out var expectedVersion))
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0) return Validation<DatabaseDiscoveryRunResponse>(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var run = await dbContext.DatabaseDiscoveryRuns
            .Include(item => item.Profile)
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.NotFound);
        if (run.Version != expectedVersion) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.ConcurrencyConflict);
        if (run.Status is DatabaseDiscoveryRunStatus.Succeeded or DatabaseDiscoveryRunStatus.Failed or DatabaseDiscoveryRunStatus.Cancelled)
            return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.TerminalRun);

        var now = DateTimeOffset.UtcNow;
        run.CancellationRequestedAt = now;
        run.CancellationRequestedByUserId = actor.Creator.UserId;
        run.Version++;
        if (run.Status == DatabaseDiscoveryRunStatus.Queued)
        {
            run.Status = DatabaseDiscoveryRunStatus.Cancelled;
            run.CompletedAt = now;
            run.ErrorCode = "Cancelled";
            run.ErrorSummary = "发现运行已取消。";
        }
        dbContext.DatabaseConnectionAuditEvents.Add(new DatabaseConnectionAuditEvent
        {
            ProfileId = run.ProfileId,
            Action = DatabaseConnectionAuditAction.DiscoveryRunCancellationRequested,
            Outcome = DatabaseConnectionAuditOutcome.Succeeded,
            ErrorCode = run.Status == DatabaseDiscoveryRunStatus.Cancelled ? "Cancelled" : null,
            ActorUserId = actor.Creator.UserId,
            ActorDisplayName = actor.Creator.DisplayName,
            OccurredAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(await ToResponse(run, run.Profile, cancellationToken));
    }

    public async Task<DatabaseDiscoveryRunPageResponse> List(
        long? profileId,
        long? databaseSourceId,
        int? page,
        int? pageSize,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Clamp(page ?? 1, 1, 1_000_000);
        var normalizedPageSize = Math.Clamp(pageSize ?? 20, 1, 100);
        var query = AccessibleRuns(includeHistory);
        if (profileId is not null) query = query.Where(item => item.Run.ProfileId == profileId);
        if (databaseSourceId is not null) query = query.Where(item => item.Profile.DatabaseSourceId == databaseSourceId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.Run.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
            .ToArrayAsync(cancellationToken);
        var responses = new List<DatabaseDiscoveryRunResponse>(rows.Length);
        foreach (var row in rows) responses.Add(await ToResponse(row.Run, row.Profile, cancellationToken, row.DatabaseSourceName));
        return new(responses, normalizedPage, normalizedPageSize, total);
    }

    public async Task<DatabaseDiscoveryRunResponse?> GetRun(long id, bool includeHistory, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return null;
        var row = await AccessibleRuns(includeHistory).SingleOrDefaultAsync(item => item.Run.Id == id, cancellationToken);
        return row is null ? null : await ToResponse(row.Run, row.Profile, cancellationToken, row.DatabaseSourceName);
    }

    public async Task<DatabaseDiscoveryRunFilterOptionsResponse> GetRunFilterOptions(
        bool includeHistory, CancellationToken cancellationToken)
    {
        var query = AccessibleRuns(includeHistory);
        var profiles = await query.Select(item => new { item.Run.ProfileId, item.Profile.Name }).Distinct()
            .OrderBy(item => item.Name).ThenBy(item => item.ProfileId).Take(500).ToArrayAsync(cancellationToken);
        var sources = await query.Select(item => new { item.Profile.DatabaseSourceId, Name = item.DatabaseSourceName }).Distinct()
            .OrderBy(item => item.Name).ThenBy(item => item.DatabaseSourceId).Take(500).ToArrayAsync(cancellationToken);
        return new(
            profiles.Select(item => new DatabaseDiscoveryFilterOptionResponse(item.ProfileId, item.Name)).ToArray(),
            sources.Select(item => new DatabaseDiscoveryFilterOptionResponse(item.DatabaseSourceId, item.Name)).ToArray());
    }

    public async Task<DatabaseDiscoverySnapshotHistoryPageResponse> ListSnapshots(
        long? profileId,
        long? databaseSourceId,
        int? page,
        int? pageSize,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var accessibleRuns = AccessibleRuns(includeHistory);
        var accessibleRunIds = accessibleRuns.Select(item => item.Run.Id);
        var query = dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .Where(item => accessibleRunIds.Contains(item.RunId));
        if (profileId is not null) query = query.Where(item => item.ProfileId == profileId);
        if (databaseSourceId is not null)
        {
            var profileIds = accessibleRuns
                .Where(item => item.Profile.DatabaseSourceId == databaseSourceId)
                .Select(item => item.Profile.Id);
            query = query.Where(item => profileIds.Contains(item.ProfileId));
        }

        var total = await query.CountAsync(cancellationToken);
        var snapshots = await query.OrderByDescending(item => item.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArrayAsync(cancellationToken);
        var runIds = snapshots.Select(item => item.RunId).ToArray();
        var runRows = await accessibleRuns.Where(item => runIds.Contains(item.Run.Id))
            .ToArrayAsync(cancellationToken);
        var rowsByRunId = runRows.ToDictionary(item => item.Run.Id);
        var snapshotIds = snapshots.Select(item => item.Id).ToArray();
        var differenceIds = await dbContext.DatabaseDiscoveryDifferences.AsNoTracking()
            .Where(item => snapshotIds.Contains(item.TargetSnapshotId))
            .Select(item => new { item.TargetSnapshotId, item.Id })
            .ToDictionaryAsync(item => item.TargetSnapshotId, item => item.Id, cancellationToken);

        var items = snapshots.Select(snapshot =>
        {
            var row = rowsByRunId[snapshot.RunId];
            return new DatabaseDiscoverySnapshotHistoryItemResponse(
                snapshot.Id,
                snapshot.RunId,
                snapshot.ProfileId,
                row.Profile.Name,
                row.Profile.DatabaseSourceId,
                row.DatabaseSourceName,
                row.Run.ProviderType,
                snapshot.CapturedAt,
                DeserializeIncludedSchemas(row.Run.RequestedIncludedSchemasJson),
                snapshot.ScopeGenerationId,
                row.Run.BaseSnapshotId,
                differenceIds.TryGetValue(snapshot.Id, out var differenceId) ? differenceId : null,
                DeserializeCounts(snapshot.CountsJson)!);
        }).ToArray();
        return new(items, normalizedPage, normalizedPageSize, total);
    }

    public async Task<DatabaseDiscoveryDifferenceHistoryPageResponse> ListDifferences(
        long? profileId,
        long? databaseSourceId,
        int? page,
        int? pageSize,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var accessibleRuns = AccessibleRuns(includeHistory);
        var accessibleRunIds = accessibleRuns.Select(item => item.Run.Id);
        var accessibleSnapshots = dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .Where(item => accessibleRunIds.Contains(item.RunId));
        var accessibleSnapshotIds = accessibleSnapshots.Select(item => item.Id);
        var query = dbContext.DatabaseDiscoveryDifferences.AsNoTracking()
            .Where(item => accessibleSnapshotIds.Contains(item.TargetSnapshotId));
        if (profileId is not null) query = query.Where(item => item.ProfileId == profileId);
        if (databaseSourceId is not null)
        {
            var profileIds = accessibleRuns
                .Where(item => item.Profile.DatabaseSourceId == databaseSourceId)
                .Select(item => item.Profile.Id);
            query = query.Where(item => profileIds.Contains(item.ProfileId));
        }

        var total = await query.CountAsync(cancellationToken);
        var differences = await query.OrderByDescending(item => item.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize)
            .Take(normalizedPageSize)
            .ToArrayAsync(cancellationToken);
        var targetSnapshotIds = differences.Select(item => item.TargetSnapshotId).ToArray();
        var targetSnapshots = await accessibleSnapshots
            .Where(item => targetSnapshotIds.Contains(item.Id))
            .Select(item => new { item.Id, item.RunId })
            .ToArrayAsync(cancellationToken);
        var runIds = targetSnapshots.Select(item => item.RunId).ToArray();
        var runRows = await accessibleRuns.Where(item => runIds.Contains(item.Run.Id))
            .ToArrayAsync(cancellationToken);
        var rowsByRunId = runRows.ToDictionary(item => item.Run.Id);
        var targetRuns = targetSnapshots.ToDictionary(item => item.Id, item => rowsByRunId[item.RunId]);

        var items = differences.Select(difference =>
        {
            var row = targetRuns[difference.TargetSnapshotId];
            return new DatabaseDiscoveryDifferenceHistoryItemResponse(
                difference.Id,
                difference.ProfileId,
                row.Profile.Name,
                row.Profile.DatabaseSourceId,
                row.DatabaseSourceName,
                row.Run.ProviderType,
                difference.BaseSnapshotId,
                difference.TargetSnapshotId,
                difference.CreatedAt,
                JsonSerializer.Deserialize<DatabaseDiscoveryDifferenceCounts>(difference.SummaryCountsJson, JsonOptions)!);
        }).ToArray();
        return new(items, normalizedPage, normalizedPageSize, total);
    }

    public async Task<DatabaseDiscoverySnapshotResponse?> GetSnapshot(long id, bool includeHistory, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return null;
        var accessibleRunIds = AccessibleRuns(includeHistory).Select(item => item.Run.Id);
        var snapshot = await dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && accessibleRunIds.Contains(item.RunId), cancellationToken);
        if (snapshot is null) return null;
        using var document = JsonDocument.Parse(snapshot.CanonicalContentJson);
        return new(
            snapshot.Id, snapshot.RunId, snapshot.ProfileId, snapshot.CapturedAt,
            snapshot.FormatVersion, snapshot.IdentityAlgorithmVersion, snapshot.ScopeGenerationId,
            snapshot.ScopeFingerprint, snapshot.Completeness, snapshot.ContentSha256,
            DeserializeCounts(snapshot.CountsJson)!, document.RootElement.Clone());
    }

    public async Task<DatabaseDiscoverySnapshotSummaryResponse?> GetSnapshotSummary(
        long id, bool includeHistory, CancellationToken cancellationToken)
    {
        var snapshot = await FindAccessibleSnapshot(id, includeHistory, cancellationToken);
        if (snapshot is null) return null;
        var content = canonical.Deserialize(snapshot.CanonicalContentJson);
        return new(
            snapshot.Id, snapshot.RunId, snapshot.ProfileId, snapshot.CapturedAt,
            content.ProviderType, content.ProviderVersion, content.DatabaseInfo.CurrentDatabaseOrService,
            content.DatabaseInfo.CurrentContainer, snapshot.FormatVersion, snapshot.IdentityAlgorithmVersion,
            snapshot.ScopeGenerationId, snapshot.ScopeFingerprint, snapshot.Completeness, snapshot.ContentSha256,
            content.Schemas.Select(item => item.Name).ToArray(), content.Capabilities, content.Counts);
    }

    public async Task<DatabaseDiscoverySchemaPageResponse?> GetSnapshotSchemas(
        long id, string? search, int? page, int? pageSize, bool includeHistory, CancellationToken cancellationToken)
    {
        var content = await ReadAccessibleSnapshot(id, includeHistory, cancellationToken);
        if (content is null) return null;
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var term = search?.Trim();
        var query = content.Schemas.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(item => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        var rows = query.OrderBy(item => item.Name, StringComparer.Ordinal).ToArray();
        var items = rows.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
            .Select(schema => new DatabaseDiscoverySchemaResponse(
                schema.Name,
                schema.LogicalIdentity,
                content.Objects.Count(item => item.SchemaLogicalIdentity == schema.LogicalIdentity),
                content.Sequences.Count(item => item.SchemaLogicalIdentity == schema.LogicalIdentity)))
            .ToArray();
        return new(items, normalizedPage, normalizedPageSize, rows.Length);
    }

    public async Task<DatabaseDiscoveryObjectPageResponse?> GetSnapshotObjects(
        long id, string? schema, string? objectType, string? search, int? page, int? pageSize,
        bool includeHistory, CancellationToken cancellationToken)
    {
        var content = await ReadAccessibleSnapshot(id, includeHistory, cancellationToken);
        if (content is null) return null;
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var query = content.Objects.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(schema))
            query = query.Where(item => string.Equals(item.SchemaName, schema.Trim(), StringComparison.Ordinal));
        if (!string.IsNullOrWhiteSpace(objectType)
            && Enum.TryParse<DatabaseDiscoveryObjectType>(objectType, false, out var parsedObjectType)
            && parsedObjectType.ToString() == objectType)
            query = query.Where(item => item.ObjectType == parsedObjectType);
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(item => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase)
                || item.SchemaName.Contains(term, StringComparison.OrdinalIgnoreCase));
        var rows = query.OrderBy(item => item.SchemaName, StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
        var columnCounts = content.Columns.GroupBy(item => item.ParentObjectLogicalIdentity)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var constraintCounts = content.PrimaryKeys.Select(item => item.ParentObjectLogicalIdentity)
            .Concat(content.ForeignKeys.Select(item => item.ParentObjectLogicalIdentity))
            .Concat(content.UniqueConstraints.Select(item => item.ParentObjectLogicalIdentity))
            .GroupBy(identity => identity).ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var indexCounts = content.Indexes.GroupBy(item => item.ParentObjectLogicalIdentity)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);
        var items = rows.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
            .Select(item => new DatabaseDiscoveryObjectSummaryResponse(
                item.LogicalIdentity, item.SchemaName, item.Name, item.ObjectType, item.DatabaseComment,
                columnCounts.GetValueOrDefault(item.LogicalIdentity),
                constraintCounts.GetValueOrDefault(item.LogicalIdentity),
                indexCounts.GetValueOrDefault(item.LogicalIdentity)))
            .ToArray();
        return new(items, normalizedPage, normalizedPageSize, rows.Length);
    }

    public async Task<DatabaseDiscoveryObjectReviewResponse?> GetSnapshotObjectReview(
        long id, string? logicalIdentity, int? columnPage, int? constraintPage, int? indexPage, int? pageSize,
        bool includeHistory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logicalIdentity)) return null;
        var content = await ReadAccessibleSnapshot(id, includeHistory, cancellationToken);
        var databaseObject = content?.Objects.SingleOrDefault(item => item.LogicalIdentity == logicalIdentity);
        if (content is null || databaseObject is null) return null;
        var size = NormalizePageSize(pageSize);
        var normalizedColumnPage = NormalizePage(columnPage);
        var normalizedConstraintPage = NormalizePage(constraintPage);
        var normalizedIndexPage = NormalizePage(indexPage);
        var columns = content.Columns.Where(item => item.ParentObjectLogicalIdentity == logicalIdentity)
            .OrderBy(item => item.SourceOrdinal).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
        var constraints = ProjectConstraints(content, logicalIdentity);
        var indexes = ProjectIndexes(content, logicalIdentity);
        return new(
            ProjectObjectHeader(databaseObject),
            new(columns.Skip((normalizedColumnPage - 1) * size).Take(size).Select(ProjectColumn).ToArray(), normalizedColumnPage, size, columns.Length),
            new(constraints.Skip((normalizedConstraintPage - 1) * size).Take(size).ToArray(), normalizedConstraintPage, size, constraints.Length),
            new(indexes.Skip((normalizedIndexPage - 1) * size).Take(size).ToArray(), normalizedIndexPage, size, indexes.Length));
    }

    public async Task<DatabaseDiscoveryObjectHeaderResponse?> GetSnapshotObjectHeader(
        long id, string? logicalIdentity, bool includeHistory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logicalIdentity)) return null;
        var content = await ReadAccessibleSnapshot(id, includeHistory, cancellationToken);
        var item = content?.Objects.SingleOrDefault(value => value.LogicalIdentity == logicalIdentity);
        return item is null ? null : new(ProjectObjectHeader(item));
    }

    public async Task<DatabaseDiscoveryColumnPageResponse?> GetSnapshotObjectColumns(
        long id, string? logicalIdentity, int? page, int? pageSize,
        bool includeHistory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logicalIdentity)) return null;
        var content = await ReadAccessibleSnapshot(id, includeHistory, cancellationToken);
        if (content is null || !content.Objects.Any(item => item.LogicalIdentity == logicalIdentity)) return null;
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var rows = content.Columns.Where(item => item.ParentObjectLogicalIdentity == logicalIdentity)
            .OrderBy(item => item.SourceOrdinal).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
        return new(rows.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).Select(ProjectColumn).ToArray(),
            normalizedPage, normalizedPageSize, rows.Length);
    }

    private static DatabaseDiscoveryObjectHeaderDataResponse ProjectObjectHeader(CanonicalDatabaseObject item) => new(
        item.SchemaName,
        item.Name,
        item.ObjectType,
        item.DatabaseComment,
        item.LogicalIdentity);

    private static DatabaseDiscoveryColumnResponse ProjectColumn(CanonicalColumn item) => new(
        item.Name,
        item.SourceOrdinal,
        new DatabaseDiscoveryNativeDataTypeResponse(item.NativeDataType.Declaration),
        item.IsNullable,
        item.DefaultExpression,
        item.DatabaseComment);

    public async Task<DatabaseDiscoveryConstraintPageResponse?> GetSnapshotObjectConstraints(
        long id, string? logicalIdentity, int? page, int? pageSize,
        bool includeHistory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logicalIdentity)) return null;
        var content = await ReadAccessibleSnapshot(id, includeHistory, cancellationToken);
        if (content is null || !content.Objects.Any(item => item.LogicalIdentity == logicalIdentity)) return null;
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var rows = ProjectConstraints(content, logicalIdentity);
        return new(rows.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToArray(),
            normalizedPage, normalizedPageSize, rows.Length);
    }

    public async Task<DatabaseDiscoveryIndexPageResponse?> GetSnapshotObjectIndexes(
        long id, string? logicalIdentity, int? page, int? pageSize,
        bool includeHistory, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(logicalIdentity)) return null;
        var content = await ReadAccessibleSnapshot(id, includeHistory, cancellationToken);
        if (content is null || !content.Objects.Any(item => item.LogicalIdentity == logicalIdentity)) return null;
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var rows = ProjectIndexes(content, logicalIdentity);
        return new(rows.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToArray(),
            normalizedPage, normalizedPageSize, rows.Length);
    }

    public async Task<DatabaseDiscoverySequencePageResponse?> GetSnapshotSequences(
        long id, string? schema, string? search, int? page, int? pageSize,
        bool includeHistory, CancellationToken cancellationToken)
    {
        var content = await ReadAccessibleSnapshot(id, includeHistory, cancellationToken);
        if (content is null) return null;
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var query = content.Sequences.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(schema))
        {
            var schemaId = content.Schemas.SingleOrDefault(item => item.Name == schema.Trim())?.LogicalIdentity;
            query = query.Where(item => item.SchemaLogicalIdentity == schemaId);
        }
        var term = search?.Trim();
        if (!string.IsNullOrWhiteSpace(term))
            query = query.Where(item => item.Name.Contains(term, StringComparison.OrdinalIgnoreCase));
        var schemaNames = content.Schemas.ToDictionary(item => item.LogicalIdentity, item => item.Name, StringComparer.Ordinal);
        var rows = query.OrderBy(item => schemaNames.GetValueOrDefault(item.SchemaLogicalIdentity), StringComparer.Ordinal)
            .ThenBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => new DatabaseDiscoverySequenceResponse(
                schemaNames.GetValueOrDefault(item.SchemaLogicalIdentity) ?? "—", item.Name,
                item.NativeDataType.Declaration, item.IncrementValue, item.MinimumValue, item.MaximumValue,
                item.CacheSize, item.IsCyclic, item.IsOrdered, item.StartValue))
            .ToArray();
        return new(rows.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize).ToArray(),
            normalizedPage, normalizedPageSize, rows.Length);
    }

    private static DatabaseDiscoveryConstraintResponse[] ProjectConstraints(
        CanonicalDatabaseDiscoverySnapshot content, string logicalIdentity)
    {
        var columns = content.Columns.ToDictionary(item => item.LogicalIdentity, item => item.Name, StringComparer.Ordinal);
        var objects = content.Objects.ToDictionary(
            item => item.LogicalIdentity, item => $"{item.SchemaName}.{item.Name}", StringComparer.Ordinal);
        foreach (var reference in content.ForeignKeyReferenceClosure)
        {
            objects.TryAdd(reference.ObjectLogicalIdentity, $"{reference.SchemaName}.{reference.ObjectName}");
        }
        return content.PrimaryKeys.Where(item => item.ParentObjectLogicalIdentity == logicalIdentity)
                .Select(item => new DatabaseDiscoveryConstraintResponse(
                    DatabaseDiscoveryEntityKind.PrimaryKey, item.Name,
                    item.ColumnLogicalIdentities.Select(id => columns.GetValueOrDefault(id) ?? "—").ToArray(),
                    null, null, null))
            .Concat(content.ForeignKeys.Where(item => item.ParentObjectLogicalIdentity == logicalIdentity)
                .Select(item => new DatabaseDiscoveryConstraintResponse(
                    DatabaseDiscoveryEntityKind.ForeignKey, item.Name,
                    item.ColumnLogicalIdentities.Select(id => columns.GetValueOrDefault(id) ?? "—").ToArray(),
                    objects.GetValueOrDefault(item.ReferencedObjectLogicalIdentity), item.UpdateRule, item.DeleteRule)))
            .Concat(content.UniqueConstraints.Where(item => item.ParentObjectLogicalIdentity == logicalIdentity)
                .Select(item => new DatabaseDiscoveryConstraintResponse(
                    DatabaseDiscoveryEntityKind.UniqueConstraint, item.Name,
                    item.ColumnLogicalIdentities.Select(id => columns.GetValueOrDefault(id) ?? "—").ToArray(),
                    null, null, null)))
            .OrderBy(item => item.EntityKind).ThenBy(item => item.Name, StringComparer.Ordinal).ToArray();
    }

    private static DatabaseDiscoveryIndexResponse[] ProjectIndexes(
        CanonicalDatabaseDiscoverySnapshot content, string logicalIdentity)
    {
        var columns = content.Columns.ToDictionary(item => item.LogicalIdentity, item => item.Name, StringComparer.Ordinal);
        return content.Indexes.Where(item => item.ParentObjectLogicalIdentity == logicalIdentity)
            .OrderBy(item => item.Name, StringComparer.Ordinal)
            .Select(item => new DatabaseDiscoveryIndexResponse(
                item.Name, item.NativeIndexKind, item.IsUnique,
                item.KeyParts.OrderBy(part => part.Position).Select(part =>
                    part.ColumnLogicalIdentity is not null
                        ? columns.GetValueOrDefault(part.ColumnLogicalIdentity) ?? "—"
                        : part.NativeExpression ?? "—").ToArray(),
                item.NonKeyParts.OrderBy(part => part.Position)
                    .Select(part => columns.GetValueOrDefault(part.ColumnLogicalIdentity) ?? "—").ToArray(),
                item.NativePredicate)).ToArray();
    }

    public async Task<DatabaseDiscoveryDifferenceResponse?> GetDifference(long id, bool includeHistory, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return null;
        var accessibleSnapshotIds = dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .Where(snapshot => AccessibleRuns(includeHistory).Select(item => item.Run.Id).Contains(snapshot.RunId))
            .Select(snapshot => snapshot.Id);
        var difference = await dbContext.DatabaseDiscoveryDifferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && accessibleSnapshotIds.Contains(item.TargetSnapshotId), cancellationToken);
        return difference is null ? null : new(
            difference.Id, difference.ProfileId, difference.BaseSnapshotId, difference.TargetSnapshotId,
            difference.ScopeGenerationId, difference.AlgorithmVersion, difference.CreatedAt,
            JsonSerializer.Deserialize<DatabaseDiscoveryDifferenceCounts>(difference.SummaryCountsJson, JsonOptions)!,
            difference.ContentSha256);
    }

    public async Task<DatabaseDiscoveryDifferenceEntryPageResponse?> GetDifferenceEntries(
        long id,
        DatabaseDiscoveryDifferenceState state,
        string? entityKind,
        string? schema,
        string? search,
        int? page,
        int? pageSize,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        var difference = await GetDifference(id, includeHistory, cancellationToken);
        if (difference is null) return null;
        var normalizedPage = NormalizePage(page);
        var normalizedPageSize = NormalizePageSize(pageSize);
        var schemaTerm = NormalizeFilter(schema);
        var searchTerm = NormalizeFilter(search);
        if (state != DatabaseDiscoveryDifferenceState.Unchanged)
        {
            var query = dbContext.DatabaseDiscoveryDifferenceEntries.AsNoTracking()
                .Where(item => item.DifferenceId == id && item.State == state);
            if (!string.IsNullOrWhiteSpace(entityKind)
                && Enum.TryParse<DatabaseDiscoveryEntityKind>(entityKind, false, out var parsedKind)
                && parsedKind.ToString() == entityKind)
                query = query.Where(item => item.EntityKind == parsedKind);

            if (schemaTerm is not null || searchTerm is not null)
            {
                var candidates = await query
                    .OrderBy(item => item.EntityKind)
                    .ThenBy(item => item.LogicalIdentity)
                    .Select(item => new DifferenceEntryFilterCandidate(
                        item.Id,
                        item.EntityKind,
                        item.LogicalIdentity,
                        item.ParentLogicalIdentity,
                        item.DisplayName))
                    .ToArrayAsync(cancellationToken);
                var matches = candidates.Where(item => MatchesDifferenceFilters(
                    item.EntityKind,
                    item.LogicalIdentity,
                    item.ParentLogicalIdentity,
                    item.DisplayName,
                    schemaTerm,
                    searchTerm)).ToArray();
                var pageIds = matches.Skip((normalizedPage - 1) * normalizedPageSize)
                    .Take(normalizedPageSize)
                    .Select(item => item.Id)
                    .ToArray();
                if (pageIds.Length == 0)
                    return new([], normalizedPage, normalizedPageSize, matches.Length);
                var pageRows = await query.Where(item => pageIds.Contains(item.Id)).ToArrayAsync(cancellationToken);
                var rowsById = pageRows.ToDictionary(item => item.Id);
                var orderedRows = pageIds.Select(item => rowsById[item]).ToArray();
                return new(
                    orderedRows.Select(item => ToResponse(item, DiscoveryIdentityContext.Empty)).ToArray(),
                    normalizedPage,
                    normalizedPageSize,
                    matches.Length);
            }

            var total = await query.CountAsync(cancellationToken);
            var rows = await query.OrderBy(item => item.EntityKind).ThenBy(item => item.LogicalIdentity)
                .Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
                .ToArrayAsync(cancellationToken);
            return new(rows.Select(item => ToResponse(item, DiscoveryIdentityContext.Empty)).ToArray(), normalizedPage, normalizedPageSize, total);
        }

        if (difference.BaseSnapshotId is null)
            return new([], normalizedPage, normalizedPageSize, 0);
        var snapshots = await dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .Where(item => item.Id == difference.TargetSnapshotId || item.Id == difference.BaseSnapshotId)
            .ToArrayAsync(cancellationToken);
        var target = canonical.Deserialize(snapshots.Single(item => item.Id == difference.TargetSnapshotId).CanonicalContentJson);
        var baseline = canonical.Deserialize(snapshots.Single(item => item.Id == difference.BaseSnapshotId).CanonicalContentJson);
        var identityContext = DiscoveryIdentityContext.Create(target, baseline);
        var unchangedQuery = diffService.DeriveUnchanged(baseline!, target).AsEnumerable();
        if (!string.IsNullOrWhiteSpace(entityKind)
            && Enum.TryParse<DatabaseDiscoveryEntityKind>(entityKind, false, out var unchangedKind)
            && unchangedKind.ToString() == entityKind)
            unchangedQuery = unchangedQuery.Where(item => item.EntityKind == unchangedKind);
        if (schemaTerm is not null || searchTerm is not null)
            unchangedQuery = unchangedQuery.Where(item => MatchesDifferenceFilters(
                item.EntityKind,
                item.LogicalIdentity,
                item.ParentLogicalIdentity,
                item.DisplayName,
                schemaTerm,
                searchTerm));
        var unchanged = unchangedQuery.ToArray();
        var items = unchanged.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
            .Select(item => ToResponse(item, identityContext))
            .ToArray();
        return new(items, normalizedPage, normalizedPageSize, unchanged.Length);
    }

    private async Task<DatabaseDiscoverySnapshot?> FindAccessibleSnapshot(
        long id, bool includeHistory, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return null;
        var accessibleRunIds = AccessibleRuns(includeHistory).Select(item => item.Run.Id);
        return await dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && accessibleRunIds.Contains(item.RunId), cancellationToken);
    }

    private async Task<CanonicalDatabaseDiscoverySnapshot?> ReadAccessibleSnapshot(
        long id, bool includeHistory, CancellationToken cancellationToken)
    {
        var snapshot = await FindAccessibleSnapshot(id, includeHistory, cancellationToken);
        return snapshot is null ? null : canonical.Deserialize(snapshot.CanonicalContentJson);
    }

    private static int NormalizePage(int? page) => Math.Clamp(page ?? 1, 1, 1_000_000);
    private static int NormalizePageSize(int? pageSize) => Math.Clamp(pageSize ?? 20, 1, 100);

    private static string? NormalizeFilter(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static bool MatchesDifferenceFilters(
        DatabaseDiscoveryEntityKind entityKind,
        string logicalIdentity,
        string? parentLogicalIdentity,
        string displayName,
        string? schema,
        string? search)
    {
        var location = DiscoveryIdentityContext.Empty.Resolve(
            entityKind,
            logicalIdentity,
            parentLogicalIdentity,
            null,
            null,
            displayName);
        if (schema is not null
            && !string.Equals(location.SchemaName, schema, StringComparison.Ordinal))
            return false;
        if (search is null) return true;
        return ContainsOrdinalIgnoreCase(displayName, search)
            || ContainsOrdinalIgnoreCase(location.SchemaName, search)
            || ContainsOrdinalIgnoreCase(location.ObjectName, search)
            || ContainsOrdinalIgnoreCase(location.ChildName, search);
    }

    private static bool ContainsOrdinalIgnoreCase(string? value, string term) =>
        value?.Contains(term, StringComparison.OrdinalIgnoreCase) == true;

    public Task<bool> HasActiveRun(long profileId, CancellationToken cancellationToken) =>
        dbContext.DatabaseDiscoveryRuns.AnyAsync(item => item.ProfileId == profileId
            && (item.Status == DatabaseDiscoveryRunStatus.Queued || item.Status == DatabaseDiscoveryRunStatus.Running), cancellationToken);

    private IQueryable<AccessibleRunRow> AccessibleRuns(bool includeHistory)
    {
        var sources = includeHistory ? dbContext.DatabaseSources.IgnoreQueryFilters() : dbContext.DatabaseSources;
        return from run in dbContext.DatabaseDiscoveryRuns.AsNoTracking()
               join profile in dbContext.DatabaseConnectionProfiles.AsNoTracking() on run.ProfileId equals profile.Id
               join source in sources.AsNoTracking() on profile.DatabaseSourceId equals source.Id
               where includeHistory || profile.IsEnabled
               select new AccessibleRunRow { Run = run, Profile = profile, DatabaseSourceName = source.Name };
    }

    private async Task<DatabaseDiscoveryRunResponse> ToResponse(
        DatabaseDiscoveryRun run,
        DatabaseConnectionProfile profile,
        CancellationToken cancellationToken,
        string? databaseSourceName = null)
    {
        var snapshot = await dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .Where(item => item.RunId == run.Id)
            .Select(item => new { item.Id, item.CountsJson })
            .SingleOrDefaultAsync(cancellationToken);
        var differenceId = snapshot is null ? null : await dbContext.DatabaseDiscoveryDifferences.AsNoTracking()
            .Where(item => item.TargetSnapshotId == snapshot.Id)
            .Select(item => (long?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        databaseSourceName ??= await dbContext.DatabaseSources.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == profile.DatabaseSourceId).Select(item => item.Name)
            .SingleOrDefaultAsync(cancellationToken) ?? $"#{profile.DatabaseSourceId}";
        return new(
            run.Id, run.ProfileId, profile.DatabaseSourceId, databaseSourceName, profile.Name, run.ProviderType, run.Status,
            run.BaseSnapshotId, run.ScopeGenerationId,
            snapshot?.Id, differenceId, run.QueuedAt, run.StartedAt, run.CompletedAt,
            run.CancellationRequestedAt, run.ProviderVersion, run.ScopeFingerprint,
            DeserializeCapabilities(run.CapabilitySnapshotJson), DeserializeCounts(run.ObjectCountsJson ?? snapshot?.CountsJson),
            run.ErrorCode, run.ErrorSummary, tokenCodec.Encode(run.Version));
    }

    private static DatabaseDiscoveryDifferenceEntryResponse ToResponse(
        DatabaseDiscoveryDifferenceEntry item,
        DiscoveryIdentityContext context)
    {
        var before = Parse(item.BeforeJson);
        var after = Parse(item.AfterJson);
        var location = context.Resolve(item.EntityKind, item.LogicalIdentity, item.ParentLogicalIdentity, before, after, item.DisplayName);
        return new(item.Id, item.EntityKind, item.LogicalIdentity, item.ParentLogicalIdentity, item.DisplayName, item.State,
            location.SchemaName, location.ObjectName, location.ChildName, BuildChanges(item.EntityKind, before, after));
    }

    private static DatabaseDiscoveryDifferenceEntryResponse ToResponse(
        DerivedDatabaseDiscoveryDifferenceEntry item,
        DiscoveryIdentityContext context)
    {
        var content = Parse(item.ContentJson);
        var location = context.Resolve(item.EntityKind, item.LogicalIdentity, item.ParentLogicalIdentity, content, content, item.DisplayName);
        return new(null, item.EntityKind, item.LogicalIdentity, item.ParentLogicalIdentity, item.DisplayName,
            DatabaseDiscoveryDifferenceState.Unchanged,
            location.SchemaName, location.ObjectName, location.ChildName, BuildChanges(item.EntityKind, content, content));
    }

    private static IReadOnlyList<DatabaseDiscoveryFieldChangeResponse> BuildChanges(
        DatabaseDiscoveryEntityKind entityKind,
        JsonElement? before,
        JsonElement? after)
    {
        var beforeProperties = Properties(before);
        var afterProperties = Properties(after);
        var changes = new List<DatabaseDiscoveryFieldChangeResponse>();
        foreach (var field in SafeDifferenceFields(entityKind))
        {
            var beforeValue = ProjectDifferenceValue(field, beforeProperties);
            var afterValue = ProjectDifferenceValue(field, afterProperties);
            if ((!beforeValue.Present && !afterValue.Present) || JsonEqual(beforeValue.Value, afterValue.Value)) continue;
            changes.Add(new DatabaseDiscoveryFieldChangeResponse(field, beforeValue.Value, afterValue.Value));
        }
        if (changes.Count == 0 && !JsonEqual(before, after))
        {
            return [new("受保护的内部字段", JsonSerializer.SerializeToElement("已隐藏"), JsonSerializer.SerializeToElement("已隐藏"))];
        }
        return changes;
    }

    private static IReadOnlyList<string> SafeDifferenceFields(DatabaseDiscoveryEntityKind entityKind) => entityKind switch
    {
        DatabaseDiscoveryEntityKind.Schema => ["name"],
        DatabaseDiscoveryEntityKind.DatabaseObject => ["schemaName", "name", "objectType", "databaseComment"],
        DatabaseDiscoveryEntityKind.Column =>
            ["name", "sourceOrdinal", "nativeDataType", "isNullable", "defaultExpression", "isPrimaryKey", "databaseComment"],
        DatabaseDiscoveryEntityKind.PrimaryKey => ["name"],
        DatabaseDiscoveryEntityKind.ForeignKey => ["name", "updateRule", "deleteRule"],
        DatabaseDiscoveryEntityKind.UniqueConstraint => ["name"],
        DatabaseDiscoveryEntityKind.Index => ["name", "nativeIndexKind", "isUnique", "nativePredicate"],
        DatabaseDiscoveryEntityKind.Sequence =>
            ["name", "nativeDataType", "incrementValue", "minimumValue", "maximumValue", "cacheSize", "isCyclic", "isOrdered", "startValue"],
        _ => [],
    };

    private static ProjectedDifferenceValue ProjectDifferenceValue(
        string field,
        IReadOnlyDictionary<string, JsonElement?> properties)
    {
        if (!properties.TryGetValue(field, out var value) || value is null) return default;
        if (field == "nativeDataType")
        {
            if (value.Value.ValueKind == JsonValueKind.Null) return new(true, null);
            if (value.Value.ValueKind == JsonValueKind.Object
                && value.Value.TryGetProperty("declaration", out var declaration)
                && declaration.ValueKind == JsonValueKind.String)
                return new(true, declaration.Clone());
            return default;
        }

        return value.Value.ValueKind switch
        {
            JsonValueKind.String or JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False =>
                new(true, value.Value.Clone()),
            JsonValueKind.Null => new(true, null),
            _ => default,
        };
    }

    private static Dictionary<string, JsonElement?> Properties(JsonElement? value) =>
        value is { ValueKind: JsonValueKind.Object }
            ? value.Value.EnumerateObject().ToDictionary(
                item => item.Name, item => (JsonElement?)item.Value.Clone(), StringComparer.Ordinal)
            : new(StringComparer.Ordinal);

    private static bool JsonEqual(JsonElement? left, JsonElement? right) =>
        left is null && right is null
        || left is null && right is { ValueKind: JsonValueKind.Null }
        || right is null && left is { ValueKind: JsonValueKind.Null }
        || left is not null && right is not null && left.Value.GetRawText() == right.Value.GetRawText();

    private static JsonElement? Parse(string? json)
    {
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<CanonicalCapability> DeserializeCapabilities(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<CanonicalCapability[]>(json, JsonOptions) ?? [];

    private static CanonicalSnapshotCounts? DeserializeCounts(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<CanonicalSnapshotCounts>(json, JsonOptions);

    private static IReadOnlyList<string> DeserializeIncludedSchemas(string json) =>
        JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];

    private static DatabaseDiscoveryOperationResult<T> Success<T>(T response) => new(response, null, DatabaseDiscoveryFailure.None);
    private static DatabaseDiscoveryOperationResult<T> Failure<T>(DatabaseDiscoveryFailure failure) => new(default, null, failure);
    private static DatabaseDiscoveryOperationResult<T> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        new(default, errors, DatabaseDiscoveryFailure.Validation);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class DiscoveryIdentityContext
    {
        private readonly Dictionary<string, string> schemas = new(StringComparer.Ordinal);
        private readonly Dictionary<string, ObjectLocation> objects = new(StringComparer.Ordinal);
        public static DiscoveryIdentityContext Empty { get; } = new();

        public static DiscoveryIdentityContext Create(
            CanonicalDatabaseDiscoverySnapshot target,
            CanonicalDatabaseDiscoverySnapshot? baseline)
        {
            var result = new DiscoveryIdentityContext();
            result.Add(target);
            if (baseline is not null) result.Add(baseline);
            return result;
        }

        public EntryLocation Resolve(
            DatabaseDiscoveryEntityKind kind,
            string logicalIdentity,
            string? parentLogicalIdentity,
            JsonElement? before,
            JsonElement? after,
            string displayName)
        {
            if (kind == DatabaseDiscoveryEntityKind.Schema)
                return new(schemas.GetValueOrDefault(logicalIdentity)
                    ?? ParseIdentity(logicalIdentity).ElementAtOrDefault(1) ?? displayName, null, null);
            if (kind == DatabaseDiscoveryEntityKind.DatabaseObject
                && objects.TryGetValue(logicalIdentity, out var databaseObject))
                return new(databaseObject.SchemaName, databaseObject.ObjectName, null);
            if (kind == DatabaseDiscoveryEntityKind.DatabaseObject)
            {
                var components = ParseIdentity(logicalIdentity);
                return new(TryString(after ?? before, "schemaName") ?? components.ElementAtOrDefault(1),
                    TryString(after ?? before, "name") ?? components.ElementAtOrDefault(2) ?? displayName, null);
            }
            if (parentLogicalIdentity is not null && objects.TryGetValue(parentLogicalIdentity, out var parent))
                return new(parent.SchemaName, parent.ObjectName, displayName);
            if (parentLogicalIdentity is not null)
            {
                var parsedParent = ParseIdentity(parentLogicalIdentity);
                if (parsedParent.ElementAtOrDefault(0) == "Object")
                    return new(parsedParent.ElementAtOrDefault(1), parsedParent.ElementAtOrDefault(2), displayName);
                if (kind == DatabaseDiscoveryEntityKind.Sequence
                    && parsedParent.ElementAtOrDefault(0) == "Schema")
                    return new(parsedParent.ElementAtOrDefault(1), null, displayName);
            }
            var content = after ?? before;
            if (kind == DatabaseDiscoveryEntityKind.Sequence
                && TryString(content, "schemaLogicalIdentity") is { } schemaIdentity)
                return new(schemas.GetValueOrDefault(schemaIdentity)
                    ?? ParseIdentity(schemaIdentity).ElementAtOrDefault(1), null, displayName);
            return new(TryString(content, "schemaName"), TryString(content, "name"),
                kind is DatabaseDiscoveryEntityKind.Column or DatabaseDiscoveryEntityKind.PrimaryKey
                    or DatabaseDiscoveryEntityKind.ForeignKey or DatabaseDiscoveryEntityKind.UniqueConstraint
                    or DatabaseDiscoveryEntityKind.Index or DatabaseDiscoveryEntityKind.Sequence
                    ? displayName : null);
        }

        private void Add(CanonicalDatabaseDiscoverySnapshot snapshot)
        {
            foreach (var schema in snapshot.Schemas) schemas.TryAdd(schema.LogicalIdentity, schema.Name);
            foreach (var item in snapshot.Objects)
                objects.TryAdd(item.LogicalIdentity, new(item.SchemaName, item.Name));
        }

        private static string? TryString(JsonElement? value, string propertyName) =>
            value is { ValueKind: JsonValueKind.Object }
            && value.Value.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
                ? property.GetString()
                : null;

        private static IReadOnlyList<string> ParseIdentity(string identity)
        {
            var values = new List<string>();
            var index = 0;
            while (index < identity.Length)
            {
                var colon = identity.IndexOf(':', index);
                if (colon <= index || !int.TryParse(identity.AsSpan(index, colon - index), out var length)
                    || length < 0 || colon + 1 + length > identity.Length) return [];
                values.Add(identity.Substring(colon + 1, length));
                index = colon + 1 + length;
            }
            return values;
        }
    }

    private sealed record ObjectLocation(string SchemaName, string ObjectName);
    private sealed record EntryLocation(string? SchemaName, string? ObjectName, string? ChildName);
    private sealed record DifferenceEntryFilterCandidate(
        long Id,
        DatabaseDiscoveryEntityKind EntityKind,
        string LogicalIdentity,
        string? ParentLogicalIdentity,
        string DisplayName);
    private readonly record struct ProjectedDifferenceValue(bool Present, JsonElement? Value);

    private sealed class AccessibleRunRow
    {
        public required DatabaseDiscoveryRun Run { get; init; }
        public required DatabaseConnectionProfile Profile { get; init; }
        public required string DatabaseSourceName { get; init; }
    }
}
