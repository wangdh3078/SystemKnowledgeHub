using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Application;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application.Models;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.UnknownItems.Application;

public sealed class UnknownItemService(
    KnowledgeHubDbContext dbContext,
    RelationshipTargetResolver targetResolver,
    EvidenceService evidenceService,
    ConcurrencyTokenCodec tokenCodec)
{
    private static readonly string[] InvestigationActions =
        ["AddFinding", "AddEvidenceToInvestigation", "SaveResolutionDraft"];

    public async Task<UnknownItemCommandResult> CreateUnknownItem(
        CreateUnknownItemCommand request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.SystemId)) errors["systemId"] = ["系统 ID 无效。"]; 
        if (string.IsNullOrWhiteSpace(request.Question)) errors["question"] = ["问题不能为空。"]; 
        if (!TryParseExact(request.Priority, out UnknownItemPriority priority)) errors["priority"] = ["优先级无效。"]; 
        ValidatePerson(request.Creator, "creator", errors);
        var targets = new List<(UnknownTargetCommand Request, KnowledgeTargetType Type, bool Primary)>();
        ParseTarget(request.PrimaryTarget, "primaryTarget", true, targets, errors);
        for (var index = 0; index < (request.RelatedTargets?.Count ?? 0); index++)
        {
            ParseTarget(request.RelatedTargets![index], $"relatedTargets[{index}]", false, targets, errors);
        }
        if (targets.GroupBy(item => (item.Type, item.Request.Id)).Any(group => group.Count() > 1))
        {
            errors["relatedTargets"] = ["Primary Target 与相关对象不能重复。"]; 
        }
        if (errors.Count > 0) return Validation(errors);

        var systemExists = await dbContext.Systems.AsNoTracking().AnyAsync(item => item.Id == request.SystemId, cancellationToken);
        if (!systemExists) return Failure(UnknownItemFailure.ReferenceInvalid, "系统不存在。");

        var resolved = new List<(UnknownTargetCommand Request, KnowledgeTargetType Type, bool Primary, string Display)>();
        foreach (var target in targets)
        {
            var context = await targetResolver.Resolve(target.Type, target.Request.Id, cancellationToken);
            if (context is null) return Failure(UnknownItemFailure.ReferenceInvalid, "待确认事项关联的知识对象不存在或尚未实现。");
            if (!context.Systems.Any(system => system.Id == request.SystemId))
            {
                return Failure(UnknownItemFailure.ReferenceInvalid, "关联对象不属于当前系统上下文。");
            }
            resolved.Add((target.Request, target.Type, target.Primary, context.Title));
        }

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var now = DateTimeOffset.UtcNow;
        var creator = request.Creator!;
        var item = new UnknownItem
        {
            ItemCode = $"PENDING-{Guid.NewGuid():N}",
            SystemId = request.SystemId,
            Question = request.Question.Trim(),
            Context = Normalize(request.Context),
            Priority = priority,
            Status = UnknownItemStatus.Open,
            CreatedAt = now,
            CreatedByName = creator.DisplayName.Trim(),
            CreatedByRole = Normalize(creator.RoleOrIdentity),
            UpdatedAt = now,
            Version = 1,
        };
        dbContext.UnknownItems.Add(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        item.ItemCode = $"UNK-{item.Id:000}";
        foreach (var target in resolved)
        {
            item.Targets.Add(new UnknownItemTarget
            {
                TargetType = target.Type,
                TargetId = target.Request.Id,
                IsPrimary = target.Primary,
                DisplaySnapshot = target.Display,
            });
        }
        var activity = CreateActivity(item, UnknownItemActivityType.Created, creator, $"{creator.DisplayName.Trim()}创建待确认事项");
        dbContext.UnknownItemActivities.Add(activity);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        var primary = resolved.Single(target => target.Primary);
        return Success(new CreateUnknownItemResponse(
            item.Id,
            item.ItemCode,
            item.Status.ToString(),
            Target(primary.Type, primary.Request.Id),
            resolved.Where(target => !target.Primary).Select(target => Target(target.Type, target.Request.Id)).ToArray(),
            Activity(activity),
            tokenCodec.Encode(item.Version),
            ["StartInvestigation"]));
    }

    public async Task<UnknownItemCommandResult> UpdateUnknownItemRelatedTargets(
        UpdateUnknownItemRelatedTargetsCommand request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        if (request.Actor is null || string.IsNullOrWhiteSpace(request.Actor.DisplayName)) errors["actor.displayName"] = ["操作人不能为空。"]; 
        var parsedTargets = new List<(UnknownTargetCommand Request, KnowledgeTargetType Type)>();
        for (var index = 0; index < (request.RelatedTargets?.Count ?? 0); index++)
        {
            var input = request.RelatedTargets![index];
            if (!TryParseTarget(input, out var type)) errors[$"relatedTargets[{index}]"] = ["相关对象引用无效。"]; 
            else parsedTargets.Add((input, type));
        }
        if (parsedTargets.GroupBy(item => (item.Type, item.Request.Id)).Any(group => group.Count() > 1)) errors["relatedTargets"] = ["相关对象不能重复。"]; 
        if (errors.Count > 0) return Validation(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.UnknownItems.Include(entry => entry.Targets).SingleOrDefaultAsync(entry => entry.Id == request.UnknownItemId, cancellationToken);
        if (item is null) return Failure(UnknownItemFailure.NotFound, "未找到待确认事项。");
        if (item.Status == UnknownItemStatus.Closed) return Failure(UnknownItemFailure.InvalidState, "已关闭事项不能修改相关对象。");
        if (item.Version != expectedVersion) return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。");
        var primary = item.Targets.Single(target => target.IsPrimary);
        if (parsedTargets.Any(target => target.Type == primary.TargetType && target.Request.Id == primary.TargetId))
        {
            return Validation(new Dictionary<string, string[]> { ["relatedTargets"] = ["相关对象不能包含 Primary Target。"] });
        }

        var resolved = new List<(UnknownTargetCommand Request, KnowledgeTargetType Type, string Display)>();
        foreach (var target in parsedTargets)
        {
            var context = await targetResolver.Resolve(target.Type, target.Request.Id, cancellationToken);
            if (context is null || !context.Systems.Any(system => system.Id == item.SystemId)) return Failure(UnknownItemFailure.ReferenceInvalid, "相关对象不存在或不属于当前系统。");
            resolved.Add((target.Request, target.Type, context.Title));
        }
        var referencedUpdateTargets = await dbContext.KnowledgeUpdates.AsNoTracking()
            .Where(update => update.UnknownItemId == item.Id)
            .Select(update => new { update.TargetType, update.TargetId })
            .ToArrayAsync(cancellationToken);
        var desired = resolved.Select(target => (target.Type, target.Request.Id)).ToHashSet();
        var removingReferenced = item.Targets.Where(target => !target.IsPrimary && !desired.Contains((target.TargetType, target.TargetId)))
            .Any(target => referencedUpdateTargets.Any(update => update.TargetType == target.TargetType && update.TargetId == target.TargetId));
        if (removingReferenced) return Failure(UnknownItemFailure.ReferenceInvalid, "知识更新正在引用相关对象，不能移除。");

        dbContext.UnknownItemTargets.RemoveRange(item.Targets.Where(target => !target.IsPrimary));
        foreach (var target in resolved)
        {
            item.Targets.Add(new UnknownItemTarget { TargetType = target.Type, TargetId = target.Request.Id, DisplaySnapshot = target.Display });
        }
        Touch(item, expectedVersion);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。"); }
        await transaction.CommitAsync(cancellationToken);
        return Success(new UpdateUnknownTargetsResponse(
            Target(primary.TargetType, primary.TargetId),
            resolved.Select(target => Target(target.Type, target.Request.Id)).ToArray(),
            tokenCodec.Encode(item.Version)));
    }

    public async Task<UnknownItemCommandResult> StartInvestigation(
        StartInvestigationCommand request,
        CancellationToken cancellationToken)
    {
        var validation = ValidateWorkflow(request.Actor, request.ConcurrencyToken, out var expectedVersion);
        if (validation.Count > 0) return Validation(validation);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.UnknownItems.SingleOrDefaultAsync(entry => entry.Id == request.UnknownItemId, cancellationToken);
        if (item is null) return Failure(UnknownItemFailure.NotFound, "未找到待确认事项。");
        if (item.Version != expectedVersion) return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。");
        if (item.Status != UnknownItemStatus.Open) return Failure(UnknownItemFailure.InvalidState, "只有待处理事项可以开始调查。");

        var actor = request.Actor!;
        var startedAt = actor.OccurredAt;
        item.Status = UnknownItemStatus.Investigating;
        item.InvestigationStartedAt ??= startedAt;
        Touch(item, expectedVersion);
        var activity = CreateActivity(item, UnknownItemActivityType.StatusChanged, actor, $"{actor.DisplayName.Trim()}开始调查");
        dbContext.UnknownItemActivities.Add(activity);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。"); }
        await transaction.CommitAsync(cancellationToken);
        return Success(new StartInvestigationResponse(item.Id, "Open", item.Status.ToString(), item.InvestigationStartedAt.Value, Activity(activity), tokenCodec.Encode(item.Version), InvestigationActions));
    }

    public async Task<UnknownItemCommandResult> AddFinding(
        AddFindingCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateWorkflow(request.Recorder, request.ConcurrencyToken, out var expectedVersion, "recorder");
        if (string.IsNullOrWhiteSpace(request.Content)) errors["content"] = ["调查发现不能为空。"]; 
        if (errors.Count > 0) return Validation(errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.UnknownItems.SingleOrDefaultAsync(entry => entry.Id == request.UnknownItemId, cancellationToken);
        if (item is null) return Failure(UnknownItemFailure.NotFound, "未找到待确认事项。");
        if (item.Version != expectedVersion) return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。");
        if (item.Status != UnknownItemStatus.Investigating) return Failure(UnknownItemFailure.InvalidState, "只有调查中的事项可以添加调查发现。");
        var recorder = request.Recorder!;
        var now = DateTimeOffset.UtcNow;
        var finding = new Finding
        {
            UnknownItemId = item.Id,
            Content = request.Content.Trim(),
            RecordedByName = recorder.DisplayName.Trim(),
            RecordedByRole = recorder.RoleOrIdentity.Trim(),
            RecordedByTeam = Normalize(recorder.Team),
            RecordedByExternalKey = Normalize(recorder.ExternalUserKey),
            RecordedBySource = Normalize(recorder.Source),
            RecordedByNote = Normalize(recorder.Note),
            RecordedAt = recorder.OccurredAt,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.Findings.Add(finding);
        await dbContext.SaveChangesAsync(cancellationToken);
        Touch(item, expectedVersion);
        var activity = CreateActivity(item, UnknownItemActivityType.FindingAdded, recorder, $"{recorder.DisplayName.Trim()}添加调查发现", "Finding", finding.Id);
        dbContext.UnknownItemActivities.Add(activity);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。"); }
        await transaction.CommitAsync(cancellationToken);
        return Success(new AddFindingResponse(Finding(finding), Activity(activity), item.Status.ToString(), tokenCodec.Encode(item.Version)));
    }

    public async Task<UnknownItemCommandResult> AddEvidenceToInvestigation(
        AddInvestigationEvidenceCommand request,
        CancellationToken cancellationToken)
    {
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
        {
            return Validation(new Dictionary<string, string[]> { ["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"] });
        }
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.UnknownItems.SingleOrDefaultAsync(entry => entry.Id == request.UnknownItemId, cancellationToken);
        if (item is null) return Failure(UnknownItemFailure.NotFound, "未找到待确认事项。");
        if (item.Version != expectedVersion) return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。");
        if (item.Status != UnknownItemStatus.Investigating) return Failure(UnknownItemFailure.InvalidState, "只有调查中的事项可以添加证据。");
        if (!await IsInvestigationSubject(item.Id, request.Evidence.Subject, cancellationToken))
        {
            return Failure(UnknownItemFailure.ReferenceInvalid, "Evidence Subject 不属于当前调查。");
        }

        var evidenceResult = await evidenceService.AddEvidence(request.Evidence, cancellationToken);
        if (evidenceResult.Failure == EvidenceFailure.Validation) return new UnknownItemCommandResult(null, evidenceResult.FieldErrors, UnknownItemFailure.Validation);
        if (evidenceResult.Failure != EvidenceFailure.None) return Failure(UnknownItemFailure.ReferenceInvalid, "调查证据关联的 Subject 不存在。");
        var created = (AddEvidenceResponse)evidenceResult.Response!;
        var provider = request.Evidence.Provider!;
        Touch(item, expectedVersion);
        var activity = CreateActivity(item, UnknownItemActivityType.EvidenceAdded, provider, $"{provider.DisplayName.Trim()}添加证据", "Evidence", created.Id);
        dbContext.UnknownItemActivities.Add(activity);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。"); }
        await transaction.CommitAsync(cancellationToken);
        return Success(new AddInvestigationEvidenceResponse(
            new InvestigationEvidenceResponse(created.Id, new UnknownTargetResponse(created.Subject.Type, created.Subject.Id), created.EvidenceType, created.SourceTitle),
            Activity(activity), item.Status.ToString(), tokenCodec.Encode(item.Version)));
    }

    public async Task<UnknownItemCommandResult> SaveResolutionDraft(
        SaveResolutionDraftCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateWorkflow(request.Actor, request.ConcurrencyToken, out var expectedVersion);
        if (string.IsNullOrWhiteSpace(request.Conclusion)) errors["conclusion"] = ["结论草稿不能为空。"]; 
        ValidateUpdateDrafts(request.KnowledgeUpdates, errors);
        if (errors.Count > 0) return Validation(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.UnknownItems.Include(entry => entry.Resolution).SingleOrDefaultAsync(entry => entry.Id == request.UnknownItemId, cancellationToken);
        if (item is null) return Failure(UnknownItemFailure.NotFound, "未找到待确认事项。");
        if (item.Version != expectedVersion) return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。");
        if (item.Status != UnknownItemStatus.Investigating) return Failure(UnknownItemFailure.InvalidState, "只有调查中的事项可以保存结论草稿。");

        var resolvedDrafts = new List<(KnowledgeUpdateDraftCommand Draft, KnowledgeTargetType Type)>();
        foreach (var draft in request.KnowledgeUpdates ?? [])
        {
            _ = TryParseTarget(draft.Target!, out var targetType);
            var target = await targetResolver.Resolve(targetType, draft.Target!.Id, cancellationToken);
            if (target is null || !target.Systems.Any(system => system.Id == item.SystemId)) return Failure(UnknownItemFailure.ReferenceInvalid, "知识更新目标不存在或不属于当前系统。");
            if (!IsSupportedUpdate(draft.ApplyAction, targetType)) return Failure(UnknownItemFailure.UnsupportedUpdate, "该知识更新意图不属于 MVP 的具体 Apply 语义。");
            resolvedDrafts.Add((draft, targetType));
        }

        var actor = request.Actor!;
        var now = DateTimeOffset.UtcNow;
        var previousConclusion = item.Resolution?.Conclusion;
        if (item.Resolution is null)
        {
            item.Resolution = new Resolution { Conclusion = request.Conclusion.Trim(), CreatedAt = now, UpdatedAt = now };
        }
        else
        {
            item.Resolution.Conclusion = request.Conclusion.Trim();
            item.Resolution.ConfirmedByName = null;
            item.Resolution.ConfirmedByRole = null;
            item.Resolution.ConfirmedByTeam = null;
            item.Resolution.ConfirmedByExternalKey = null;
            item.Resolution.ConfirmedBySource = null;
            item.Resolution.ConfirmedByNote = null;
            item.Resolution.ConfirmedAt = null;
            item.Resolution.UpdatedAt = now;
        }
        await dbContext.SaveChangesAsync(cancellationToken);

        var existingProposed = await dbContext.KnowledgeUpdates.Where(update => update.UnknownItemId == item.Id && update.Status == KnowledgeUpdateStatus.Proposed).ToDictionaryAsync(update => update.Id, cancellationToken);
        foreach (var entry in resolvedDrafts)
        {
            KnowledgeUpdate update;
            if (entry.Draft.Id.HasValue)
            {
                if (!existingProposed.TryGetValue(entry.Draft.Id.Value, out update!)) return Failure(UnknownItemFailure.ReferenceInvalid, "只能修订当前事项已有的 Proposed KnowledgeUpdate。");
            }
            else
            {
                update = new KnowledgeUpdate { UnknownItemId = item.Id, CreatedAt = now, Status = KnowledgeUpdateStatus.Proposed };
                dbContext.KnowledgeUpdates.Add(update);
            }
            update.TargetType = entry.Type;
            update.TargetId = entry.Draft.Target!.Id;
            update.SubjectDetailKey = Normalize(entry.Draft.SubjectDetailKey);
            update.ChangeSummary = entry.Draft.ChangeSummary.Trim();
            update.BeforeJson = Json(entry.Draft.Before);
            update.AfterJson = Json(entry.Draft.After);
            update.KnowledgeStatusBefore = ParseKnowledgeStatus(entry.Draft.KnowledgeStatusBefore);
            update.KnowledgeStatusAfter = ParseKnowledgeStatus(entry.Draft.KnowledgeStatusAfter);
            update.UpdatedAt = now;
        }

        Touch(item, expectedVersion);
        var summary = previousConclusion is null
            ? $"{actor.DisplayName.Trim()}保存当前结论草稿"
            : $"{actor.DisplayName.Trim()}修订结论：{Short(previousConclusion)} → {Short(request.Conclusion)}";
        var activity = CreateActivity(item, UnknownItemActivityType.ResolutionRecorded, actor, summary, "Resolution", item.Resolution.Id);
        dbContext.UnknownItemActivities.Add(activity);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Failure(UnknownItemFailure.Conflict, "待确认事项已被修改，请重新加载后重试。"); }
        await transaction.CommitAsync(cancellationToken);

        var updates = await dbContext.KnowledgeUpdates.AsNoTracking().Where(update => update.UnknownItemId == item.Id).OrderBy(update => update.Id).ToArrayAsync(cancellationToken);
        return Success(new SaveResolutionDraftResponse(
            Resolution(item.Resolution),
            updates.Select(Update).ToArray(),
            item.Status.ToString(),
            Activity(activity),
            tokenCodec.Encode(item.Version)));
    }

    private async Task<bool> IsInvestigationSubject(long unknownItemId, EvidenceTargetCommand? subject, CancellationToken cancellationToken)
    {
        if (subject is null || !ApiIdParser.IsSafePositive(subject.Id)) return false;
        return subject.Type switch
        {
            "UnknownItem" => subject.Id == unknownItemId,
            "Finding" => await dbContext.Findings.AsNoTracking().AnyAsync(finding => finding.Id == subject.Id && finding.UnknownItemId == unknownItemId, cancellationToken),
            "Resolution" => await dbContext.Resolutions.AsNoTracking().AnyAsync(resolution => resolution.Id == subject.Id && resolution.UnknownItemId == unknownItemId, cancellationToken),
            _ => false,
        };
    }

    private static void ValidateUpdateDrafts(IReadOnlyList<KnowledgeUpdateDraftCommand>? drafts, IDictionary<string, string[]> errors)
    {
        for (var index = 0; index < (drafts?.Count ?? 0); index++)
        {
            var draft = drafts![index];
            if (!TryParseTarget(draft.Target, out _)) errors[$"knowledgeUpdates[{index}].target"] = ["知识更新目标无效。"]; 
            if (string.IsNullOrWhiteSpace(draft.ApplyAction)) errors[$"knowledgeUpdates[{index}].applyAction"] = ["必须选择具体 Apply 操作。"]; 
            if (string.IsNullOrWhiteSpace(draft.ChangeSummary)) errors[$"knowledgeUpdates[{index}].changeSummary"] = ["变更摘要不能为空。"]; 
            var beforeValid = TryParseKnowledgeStatus(draft.KnowledgeStatusBefore, out _);
            var afterValid = TryParseKnowledgeStatus(draft.KnowledgeStatusAfter, out _);
            if (!beforeValid || !afterValid || (draft.KnowledgeStatusBefore is null) != (draft.KnowledgeStatusAfter is null))
            {
                errors[$"knowledgeUpdates[{index}].knowledgeStatus"] = ["KnowledgeStatus 前后值必须同时为空或同时为合法值。"]; 
            }
        }
    }

    private static bool IsSupportedUpdate(string action, KnowledgeTargetType targetType) => action switch
    {
        "AddColumnKnownValue" or "UpdateDatabaseColumnKnowledge" => targetType == KnowledgeTargetType.DatabaseColumn,
        "UpdateBusinessRule" => targetType == KnowledgeTargetType.BusinessRule,
        "UpdateIntegration" => targetType == KnowledgeTargetType.Integration,
        "UpdateBusinessFunction" => targetType == KnowledgeTargetType.BusinessFunction,
        _ => false,
    };

    private Dictionary<string, string[]> ValidateWorkflow(PersonSnapshotCommand? person, string token, out long expectedVersion)
    {
        var errors = new Dictionary<string, string[]>();
        ValidatePerson(person, "actor", errors);
        if (!tokenCodec.TryDecode(token, out expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        return errors;
    }

    private Dictionary<string, string[]> ValidateWorkflow(PersonSnapshotCommand? person, string token, out long expectedVersion, string prefix)
    {
        var errors = new Dictionary<string, string[]>();
        ValidatePerson(person, prefix, errors);
        if (!tokenCodec.TryDecode(token, out expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        return errors;
    }

    private static void ParseTarget(UnknownTargetCommand? target, string field, bool primary, ICollection<(UnknownTargetCommand Request, KnowledgeTargetType Type, bool Primary)> output, IDictionary<string, string[]> errors)
    {
        if (!TryParseTarget(target, out var type)) errors[field] = ["知识对象引用无效。"]; 
        else output.Add((target!, type, primary));
    }

    private static bool TryParseTarget(UnknownTargetCommand? target, out KnowledgeTargetType type)
    {
        type = default;
        return target is not null && ApiIdParser.IsSafePositive(target.Id) && TryParseExact(target.Type, out type);
    }

    private static bool TryParseExact<T>(string? value, out T parsed) where T : struct, Enum =>
        Enum.TryParse(value, false, out parsed) && parsed.ToString() == value;

    private static void ValidatePerson(PersonSnapshotCommand? person, string prefix, IDictionary<string, string[]> errors)
    {
        if (person is null) { errors[prefix] = ["必须记录人员快照。"]; return; }
        if (string.IsNullOrWhiteSpace(person.DisplayName)) errors[$"{prefix}.displayName"] = ["人员名称不能为空。"]; 
        if (string.IsNullOrWhiteSpace(person.RoleOrIdentity)) errors[$"{prefix}.roleOrIdentity"] = ["角色或身份不能为空。"]; 
        if (person.OccurredAt == default) errors[$"{prefix}.occurredAt"] = ["发生时间不能为空。"]; 
    }

    private static UnknownItemActivity CreateActivity(UnknownItem item, UnknownItemActivityType type, PersonSnapshotCommand actor, string summary, string? relatedType = null, long? relatedId = null) => new()
    {
        UnknownItemId = item.Id,
        ActivityType = type,
        ActorName = actor.DisplayName.Trim(),
        ActorRole = actor.RoleOrIdentity.Trim(),
        ActorTeam = Normalize(actor.Team),
        ActorExternalKey = Normalize(actor.ExternalUserKey),
        ActorSource = Normalize(actor.Source),
        ActorNote = Normalize(actor.Note),
        OccurredAt = actor.OccurredAt,
        Note = summary,
        RelatedType = relatedType,
        RelatedId = relatedId,
    };

    private static void Touch(UnknownItem item, long expectedVersion)
    {
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version = expectedVersion + 1;
    }

    private static UnknownTargetResponse Target(KnowledgeTargetType type, long id) => new(type.ToString(), id);
    private static UnknownActivityResponse Activity(UnknownItemActivity activity) => new(activity.ActivityType.ToString(), activity.Note ?? activity.ActivityType.ToString(), activity.OccurredAt);
    private static PersonSnapshotResponse Person(string name, string role, DateTimeOffset at, string? team, string? externalKey, string? source, string? note) => new(name, role, at, team, externalKey, source, note);
    private static FindingResponse Finding(Finding finding) => new(finding.Id, finding.Content, Person(finding.RecordedByName, finding.RecordedByRole, finding.RecordedAt, finding.RecordedByTeam, finding.RecordedByExternalKey, finding.RecordedBySource, finding.RecordedByNote));
    private static ResolutionResponse Resolution(Resolution resolution) => new(resolution.Id, resolution.Conclusion, resolution.ConfirmedAt.HasValue ? Person(resolution.ConfirmedByName!, resolution.ConfirmedByRole!, resolution.ConfirmedAt.Value, resolution.ConfirmedByTeam, resolution.ConfirmedByExternalKey, resolution.ConfirmedBySource, resolution.ConfirmedByNote) : null, resolution.ConfirmedAt);
    private static KnowledgeUpdateResponse Update(KnowledgeUpdate update) => new(update.Id, Target(update.TargetType, update.TargetId), update.SubjectDetailKey, update.ChangeSummary, JsonSerializer.Deserialize<JsonElement>(update.BeforeJson), JsonSerializer.Deserialize<JsonElement>(update.AfterJson), update.Status.ToString());
    private static string Json(JsonElement? value) => value.HasValue ? value.Value.GetRawText() : "null";
    private static string? Normalize(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    private static string Short(string value) => value.Length <= 60 ? value : value[..60] + "…";
    private static KnowledgeStatus? ParseKnowledgeStatus(string? value) =>
        value is null ? null : TryParseKnowledgeStatus(value, out var status) ? status : null;
    private static bool TryParseKnowledgeStatus(string? value, out KnowledgeStatus status)
    {
        status = default;
        return value is null || TryParseExact(value, out status);
    }
    private static UnknownItemCommandResult Success(object response) => new(response, null, UnknownItemFailure.None);
    private static UnknownItemCommandResult Validation(IReadOnlyDictionary<string, string[]> errors) => new(null, errors, UnknownItemFailure.Validation);
    private static UnknownItemCommandResult Failure(UnknownItemFailure failure, string message) => new(null, null, failure, message);
}
