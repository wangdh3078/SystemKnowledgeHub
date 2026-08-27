using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application.Models;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;

namespace SystemKnowledgeHub.Api.Features.UnknownItems.Application;

public sealed class UnknownItemQueries(
    KnowledgeHubDbContext dbContext,
    HistoricalTargetResolver historicalTargetResolver,
    ConcurrencyTokenCodec tokenCodec)
{
    public async Task<UnknownItemsListQueryResult> GetList(
        UnknownItemsListQuery request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request, out var relatedType, out var priority, out var status, out var page, out var pageSize, out var sort);
        if (errors.Count > 0) return new(null, errors);

        var query = dbContext.UnknownItems.AsNoTracking().AsQueryable();
        query = query.Where(item => item.Status == UnknownItemStatus.Closed
            || dbContext.Systems.Any(system => system.Id == item.SystemId)
            && item.Targets.Any(target => target.IsPrimary)
            && item.Targets.All(target =>
                target.TargetType == KnowledgeTargetType.System
                    && dbContext.Systems.Any(entity => entity.Id == target.TargetId)
                || target.TargetType == KnowledgeTargetType.DatabaseSource
                    && dbContext.DatabaseSources.Any(entity => entity.Id == target.TargetId)
                || target.TargetType == KnowledgeTargetType.BusinessFunction
                    && dbContext.BusinessFunctions.Any(entity => entity.Id == target.TargetId)
                || target.TargetType == KnowledgeTargetType.DatabaseObject
                    && dbContext.DatabaseObjects.Any(entity => entity.Id == target.TargetId)
                || target.TargetType == KnowledgeTargetType.DatabaseColumn
                    && dbContext.DatabaseColumns.Any(entity => entity.Id == target.TargetId)
                || target.TargetType == KnowledgeTargetType.BusinessRule
                    && dbContext.BusinessRules.Any(entity => entity.Id == target.TargetId)
                || target.TargetType == KnowledgeTargetType.Integration
                    && dbContext.Integrations.Any(entity => entity.Id == target.TargetId)
                || target.TargetType == KnowledgeTargetType.KnowledgeDocument
                    && dbContext.KnowledgeDocuments.Any(entity => entity.Id == target.TargetId)));
        if (request.SystemId.HasValue) query = query.Where(item => item.SystemId == request.SystemId.Value);
        if (priority.HasValue) query = query.Where(item => item.Priority == priority.Value);
        if (status.HasValue) query = query.Where(item => item.Status == status.Value);
        if (relatedType.HasValue)
        {
            query = query.Where(item => item.Targets.Any(target => target.TargetType == relatedType.Value));
        }
        if (!string.IsNullOrWhiteSpace(request.Keyword))
        {
            var pattern = $"%{request.Keyword.Trim()}%";
            query = query.Where(item => EF.Functions.Like(item.Question, pattern)
                || (item.Context != null && EF.Functions.Like(item.Context, pattern))
                || item.Targets.Any(target => EF.Functions.Like(target.DisplaySnapshot, pattern)));
        }

        var candidates = await query
            .Select(item => new
            {
                item.Id,
                item.ItemCode,
                item.Question,
                item.SystemId,
                Primary = item.Targets.Where(target => target.IsPrimary).Select(target => new
                {
                    target.TargetType,
                    target.TargetId,
                    target.DisplaySnapshot,
                }).Single(),
                item.Priority,
                item.Status,
                FindingCount = item.Findings.Count,
                item.UpdatedAt,
            })
            .ToArrayAsync(cancellationToken);
        var filtered = candidates.AsEnumerable();
        if (request.UpdatedFrom.HasValue) filtered = filtered.Where(item => item.UpdatedAt >= request.UpdatedFrom.Value);
        if (request.UpdatedTo.HasValue) filtered = filtered.Where(item => item.UpdatedAt <= request.UpdatedTo.Value);
        filtered = sort switch
        {
            "updatedAt:asc" => filtered.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Id),
            "priority:desc" => filtered.OrderBy(item => item.Priority).ThenByDescending(item => item.UpdatedAt),
            _ => filtered.OrderByDescending(item => item.UpdatedAt).ThenByDescending(item => item.Id),
        };
        var materialized = filtered.ToArray();
        var total = materialized.Length;
        var rows = materialized.Skip((page - 1) * pageSize).Take(pageSize).ToArray();

        var ids = rows.Select(row => row.Id).ToArray();
        var findingIds = await dbContext.Findings.AsNoTracking()
            .Where(item => ids.Contains(item.UnknownItemId))
            .Select(item => new { item.Id, item.UnknownItemId })
            .ToArrayAsync(cancellationToken);
        var resolutions = await dbContext.Resolutions.AsNoTracking()
            .Where(item => ids.Contains(item.UnknownItemId))
            .Select(item => new { item.Id, item.UnknownItemId })
            .ToArrayAsync(cancellationToken);
        var evidence = ids.Length == 0
            ? []
            : await dbContext.Evidence.AsNoTracking()
                .Where(item => (item.SubjectType == EvidenceSubjectType.UnknownItem && ids.Contains(item.SubjectId))
                    || (item.SubjectType == EvidenceSubjectType.Finding && findingIds.Select(finding => finding.Id).Contains(item.SubjectId))
                    || (item.SubjectType == EvidenceSubjectType.Resolution && resolutions.Select(resolution => resolution.Id).Contains(item.SubjectId)))
                .Select(item => new { item.SubjectType, item.SubjectId })
                .ToArrayAsync(cancellationToken);

        int EvidenceCount(long itemId)
        {
            var itemFindingIds = findingIds.Where(value => value.UnknownItemId == itemId).Select(value => value.Id).ToHashSet();
            var itemResolutionIds = resolutions.Where(value => value.UnknownItemId == itemId).Select(value => value.Id).ToHashSet();
            return evidence.Count(value =>
                value.SubjectType == EvidenceSubjectType.UnknownItem && value.SubjectId == itemId
                || value.SubjectType == EvidenceSubjectType.Finding && itemFindingIds.Contains(value.SubjectId)
                || value.SubjectType == EvidenceSubjectType.Resolution && itemResolutionIds.Contains(value.SubjectId));
        }

        var responses = new List<UnknownItemListRowResponse>();
        foreach (var row in rows)
        {
            var system = await historicalTargetResolver.Resolve(KnowledgeTargetType.System, row.SystemId, cancellationToken);
            var primary = await historicalTargetResolver.Resolve(row.Primary.TargetType, row.Primary.TargetId, cancellationToken);
            if (system is null || primary is null) continue;
            if (row.Status != UnknownItemStatus.Closed && (system.IsDeleted || primary.IsDeleted)) continue;
            responses.Add(new UnknownItemListRowResponse(
                row.Id,
                row.ItemCode,
                row.Question,
                SystemResponse(system),
                new(row.Primary.TargetType.ToString(), row.Primary.TargetId, row.Primary.DisplaySnapshot, primary),
                row.Priority.ToString(),
                row.Status.ToString(),
                row.FindingCount,
                EvidenceCount(row.Id),
                row.UpdatedAt));
        }
        return new(new UnknownItemsListResponse(responses, page, pageSize, total), null);
    }

    public async Task<UnknownItemDetailQueryResult> GetDetail(long id, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return new(null, UnknownItemFailure.Validation, "待确认事项 ID 无效。");
        var item = await dbContext.UnknownItems.AsNoTracking()
            .Include(entry => entry.Targets)
            .Include(entry => entry.Findings)
            .Include(entry => entry.Resolution)
            .Include(entry => entry.KnowledgeUpdates)
            .Include(entry => entry.Activities)
            .SingleOrDefaultAsync(entry => entry.Id == id, cancellationToken);
        if (item is null) return new(null, UnknownItemFailure.NotFound, "未找到待确认事项。");

        var system = await historicalTargetResolver.Resolve(KnowledgeTargetType.System, item.SystemId, cancellationToken);
        if (system is null) return new(null, UnknownItemFailure.ReferenceInvalid, "待确认事项的系统引用无效。");

        var targetIdentities = new Dictionary<(KnowledgeTargetType Type, long Id), HistoricalTargetIdentity>();
        foreach (var target in item.Targets)
        {
            var identity = await historicalTargetResolver.Resolve(target.TargetType, target.TargetId, cancellationToken);
            if (identity is null) return new(null, UnknownItemFailure.ReferenceInvalid, "待确认事项的目标引用无效。");
            targetIdentities[(target.TargetType, target.TargetId)] = identity;
        }
        foreach (var update in item.KnowledgeUpdates)
        {
            var key = (update.TargetType, update.TargetId);
            if (targetIdentities.ContainsKey(key)) continue;
            var identity = await historicalTargetResolver.Resolve(update.TargetType, update.TargetId, cancellationToken);
            if (identity is null) return new(null, UnknownItemFailure.ReferenceInvalid, "知识更新的目标引用无效。");
            targetIdentities[key] = identity;
        }
        var historicalOnly = system.IsDeleted || targetIdentities.Values.Any(identity => identity.IsDeleted);
        if (historicalOnly && item.Status != UnknownItemStatus.Closed)
        {
            return new(null, UnknownItemFailure.NotFound, "未找到待确认事项。");
        }

        var findingIds = item.Findings.Select(finding => finding.Id).ToArray();
        var resolutionId = item.Resolution?.Id;
        var evidenceEntities = await dbContext.Evidence.AsNoTracking()
            .Where(entry => entry.SubjectType == EvidenceSubjectType.UnknownItem && entry.SubjectId == id
                || entry.SubjectType == EvidenceSubjectType.Finding && findingIds.Contains(entry.SubjectId)
                || entry.SubjectType == EvidenceSubjectType.Resolution && resolutionId.HasValue && entry.SubjectId == resolutionId.Value)
            .ToArrayAsync(cancellationToken);
        var evidence = evidenceEntities.OrderByDescending(entry => entry.ProvidedAt)
            .Select(entry => new InvestigationEvidenceResponse(
                entry.Id,
                new(entry.SubjectType.ToString(), entry.SubjectId),
                entry.EvidenceType.ToString(),
                entry.SourceTitle))
            .ToArray();

        var targets = item.Targets.OrderByDescending(target => target.IsPrimary).ThenBy(target => target.Id)
            .Select(target => new UnknownTargetSummaryResponse(
                new(target.TargetType.ToString(), target.TargetId),
                target.DisplaySnapshot,
                target.IsPrimary,
                targetIdentities[(target.TargetType, target.TargetId)]))
            .ToArray();
        var updates = item.KnowledgeUpdates.OrderBy(update => update.Id)
            .Select(update => Update(update, targetIdentities[(update.TargetType, update.TargetId)]))
            .ToArray();
        var impact = updates.Select(update => targetDisplay(item.Targets, update.Target) +
            (string.IsNullOrWhiteSpace(update.SubjectDetailKey) ? string.Empty : " · " + update.SubjectDetailKey)).ToArray();

        return new(new UnknownItemDetailResponse(
            item.Id,
            item.ItemCode,
            SystemResponse(system),
            tokenCodec.Encode(item.Version),
            new(item.Question, item.Context, item.Priority.ToString(), item.Status.ToString(), item.CreatedAt, item.UpdatedAt),
            targets,
            item.Findings.OrderBy(finding => finding.RecordedAt).Select(Finding).ToArray(),
            evidence,
            item.Resolution is null ? null : Resolution(item.Resolution),
            updates,
            item.Activities.OrderByDescending(activity => activity.OccurredAt).ThenByDescending(activity => activity.Id).Select(Activity).ToArray(),
            new(impact, evidence.Length, item.Resolution is null ? 1 : updates.Count(update => update.Status == "Proposed")),
            historicalOnly ? [] : Actions(item)), UnknownItemFailure.None);

        static string targetDisplay(IEnumerable<UnknownItemTarget> targets, UnknownTargetResponse target) =>
            targets.FirstOrDefault(item => item.TargetType.ToString() == target.Type && item.TargetId == target.Id)?.DisplaySnapshot
            ?? $"{target.Type} #{target.Id}";
    }

    private static Dictionary<string, string[]> Validate(
        UnknownItemsListQuery request,
        out KnowledgeTargetType? relatedType,
        out UnknownItemPriority? priority,
        out UnknownItemStatus? status,
        out int page,
        out int pageSize,
        out string sort)
    {
        var errors = new Dictionary<string, string[]>();
        relatedType = Parse<KnowledgeTargetType>(request.RelatedObjectType, "relatedObjectType", errors);
        priority = Parse<UnknownItemPriority>(request.Priority, "priority", errors);
        status = Parse<UnknownItemStatus>(request.Status, "status", errors);
        page = request.Page ?? 1;
        pageSize = request.PageSize ?? 20;
        sort = request.Sort ?? "updatedAt:desc";
        if (request.SystemId.HasValue && !ApiIdParser.IsSafePositive(request.SystemId.Value)) errors["systemId"] = ["系统 ID 无效。"]; 
        if (page < 1) errors["page"] = ["页码必须大于 0。"]; 
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"]; 
        if (sort is not ("updatedAt:desc" or "updatedAt:asc" or "priority:desc")) errors["sort"] = ["排序值无效。"]; 
        if (request.UpdatedFrom.HasValue && request.UpdatedTo.HasValue && request.UpdatedFrom > request.UpdatedTo) errors["updatedFrom"] = ["更新时间范围无效。"]; 
        return errors;
    }

    private static T? Parse<T>(string? value, string field, IDictionary<string, string[]> errors) where T : struct, Enum
    {
        if (string.IsNullOrWhiteSpace(value)) return null;
        if (Enum.TryParse<T>(value, false, out var parsed) && parsed.ToString() == value) return parsed;
        errors[field] = ["枚举值无效。"]; 
        return null;
    }

    private static FindingResponse Finding(Finding finding) => new(finding.Id, finding.Content,
        new(finding.RecordedByName, finding.RecordedByRole, finding.RecordedAt, finding.RecordedByTeam,
            finding.RecordedByExternalKey, finding.RecordedBySource, finding.RecordedByNote));
    private static ResolutionResponse Resolution(Resolution resolution) => new(resolution.Id, resolution.Conclusion,
        resolution.ConfirmedAt.HasValue ? new(resolution.ConfirmedByName!, resolution.ConfirmedByRole!, resolution.ConfirmedAt.Value,
            resolution.ConfirmedByTeam, resolution.ConfirmedByExternalKey, resolution.ConfirmedBySource, resolution.ConfirmedByNote) : null,
        resolution.ConfirmedAt);
    private static KnowledgeUpdateResponse Update(KnowledgeUpdate update, HistoricalTargetIdentity identity) => new(update.Id,
        new(update.TargetType.ToString(), update.TargetId), update.SubjectDetailKey, update.ChangeSummary,
        JsonSerializer.Deserialize<JsonElement>(update.BeforeJson), JsonSerializer.Deserialize<JsonElement>(update.AfterJson),
        update.Status.ToString(), identity);
    private static UnknownSystemResponse SystemResponse(HistoricalTargetIdentity identity) => new(
        identity.Id,
        identity.DisplayName,
        identity.TargetType,
        identity.DisplayName,
        identity.IsDeleted,
        identity.IsNavigable);
    private static UnknownItemActivityResponse Activity(UnknownItemActivity activity) => new(activity.ActivityType.ToString(),
        activity.Note ?? activity.ActivityType.ToString(), activity.OccurredAt);
    private static string[] Actions(UnknownItem item) => item.Status switch
    {
        UnknownItemStatus.Open => ["StartInvestigation"],
        UnknownItemStatus.Investigating => [
            "AddFinding", "AddEvidenceToInvestigation", "SaveResolutionDraft",
            .. item.KnowledgeUpdates.Where(update => update.Status == KnowledgeUpdateStatus.Proposed)
                .Select(ApplyAction).Where(action => action.Length > 0).Distinct(),
            .. (item.Resolution is null ? Array.Empty<string>() : ["ConfirmConclusion"]),
        ],
        UnknownItemStatus.ConclusionConfirmed => ["CloseUnknownItem"],
        UnknownItemStatus.Closed => ["ReopenUnknownItem"],
        _ => [],
    };

    private static string ApplyAction(KnowledgeUpdate update) => update.TargetType switch
    {
        KnowledgeTargetType.DatabaseColumn when update.SubjectDetailKey?.StartsWith("KnownValues:", StringComparison.Ordinal) == true => "ApplyColumnKnownValueUpdate",
        KnowledgeTargetType.DatabaseColumn => "ApplyDatabaseColumnKnowledgeUpdate",
        KnowledgeTargetType.BusinessFunction => "ApplyBusinessFunctionUpdate",
        KnowledgeTargetType.BusinessRule => "ApplyBusinessRuleUpdate",
        KnowledgeTargetType.Integration => "ApplyIntegrationUpdate",
        _ => string.Empty,
    };
}
