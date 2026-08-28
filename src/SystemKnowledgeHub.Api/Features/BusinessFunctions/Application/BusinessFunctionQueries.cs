using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application.Models;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;

namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Application;

public sealed class BusinessFunctionQueries(
    KnowledgeHubDbContext dbContext,
    RelationshipTargetResolver relationshipTargetResolver,
    SoftDeleteCapabilityResolver capabilityResolver,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<BusinessFunctionsListQueryResult> GetBusinessFunctionsList(
        BusinessFunctionsListQuery request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request, out var rewriteStatus, out var knowledgeStatus, out var hasUnknownItems, out var sort);
        if (errors.Count > 0)
        {
            return new BusinessFunctionsListQueryResult(null, errors);
        }

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? DefaultPageSize;
        var search = NormalizeOptional(request.Search);
        var functionType = NormalizeOptional(request.FunctionType);
        var query = dbContext.BusinessFunctions.AsNoTracking();

        if (request.SystemId.HasValue)
        {
            query = query.Where(function => function.SystemId == request.SystemId.Value);
        }

        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(function =>
                EF.Functions.Like(function.Name, pattern)
                || (function.DisplayName != null && EF.Functions.Like(function.DisplayName, pattern))
                || (function.Purpose != null && EF.Functions.Like(function.Purpose, pattern)));
        }

        if (functionType is not null)
        {
            query = query.Where(function => function.FunctionType == functionType);
        }

        if (rewriteStatus.HasValue)
        {
            query = query.Where(function => function.RewriteStatus == rewriteStatus.Value);
        }

        if (knowledgeStatus.HasValue)
        {
            query = query.Where(function => function.KnowledgeStatus == knowledgeStatus.Value);
        }

        if (hasUnknownItems.HasValue)
        {
            query = hasUnknownItems.Value
                ? query.Where(function => dbContext.UnknownItemTargets.Any(target =>
                    target.TargetType == KnowledgeTargetType.BusinessFunction && target.TargetId == function.Id
                    && target.UnknownItem.Status != UnknownItemStatus.Closed))
                : query.Where(function => !dbContext.UnknownItemTargets.Any(target =>
                    target.TargetType == KnowledgeTargetType.BusinessFunction && target.TargetId == function.Id
                    && target.UnknownItem.Status != UnknownItemStatus.Closed));
        }

        var rows = await query
            .Select(function => new BusinessFunctionListRow(
                function.Id,
                function.Name,
                function.System.Id,
                function.System.Name,
                function.FunctionType,
                function.Purpose,
                function.RewriteStatus,
                function.KnowledgeStatus,
                function.UpdatedAt,
                dbContext.KnowledgeRelations.Count(relation => relation.SourceType == KnowledgeTargetType.BusinessFunction
                    && relation.SourceId == function.Id
                    && (relation.TargetType == KnowledgeTargetType.DatabaseObject
                        && dbContext.DatabaseObjects.Any(item => item.Id == relation.TargetId)
                        || relation.TargetType == KnowledgeTargetType.DatabaseColumn
                        && dbContext.DatabaseColumns.Any(item => item.Id == relation.TargetId))),
                dbContext.KnowledgeRelations.Count(relation => relation.SourceType == KnowledgeTargetType.BusinessFunction
                    && relation.SourceId == function.Id && relation.TargetType == KnowledgeTargetType.BusinessRule
                    && dbContext.BusinessRules.Any(item => item.Id == relation.TargetId)),
                dbContext.UnknownItemTargets.Count(target => target.TargetType == KnowledgeTargetType.BusinessFunction
                    && target.TargetId == function.Id && target.UnknownItem.Status != UnknownItemStatus.Closed)))
            .ToArrayAsync(cancellationToken);
        var total = rows.Length;
        var items = ApplySort(rows, sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(function => new BusinessFunctionSummaryResponse(
                function.Id,
                function.Name,
                new KnowledgeSystemReferenceResponse(function.SystemId, function.SystemName),
                function.FunctionType,
                function.Purpose,
                function.RelatedDataCount,
                function.RuleCount,
                function.UnknownCount,
                function.RewriteStatus.ToString(),
                function.KnowledgeStatus.ToString(),
                function.UpdatedAt))
            .ToArray();

        return new BusinessFunctionsListQueryResult(
            new BusinessFunctionsListResponse(items, page, pageSize, total),
            null);
    }

    public async Task<BusinessFunctionDetailResponse?> GetBusinessFunctionDetail(
        long businessFunctionId,
        CancellationToken cancellationToken)
    {
        var function = await dbContext.BusinessFunctions
            .AsNoTracking()
            .Where(item => item.Id == businessFunctionId)
            .Select(item => new
            {
                item.Id,
                item.Name,
                item.FunctionType,
                item.Purpose,
                item.CallerSummary,
                item.InputDescription,
                item.OutputDescription,
                item.RewriteStatus,
                item.KnowledgeStatus,
                item.Version,
                item.CreatedByUserId,
                SystemId = item.System.Id,
                SystemName = item.System.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (function is null)
        {
            return null;
        }

        var processSteps = await dbContext.BusinessProcessSteps
            .AsNoTracking()
            .Where(step => step.BusinessFunctionId == businessFunctionId)
            .OrderBy(step => step.StepOrder)
            .Select(step => new BusinessProcessStepResponse(step.StepOrder, step.Name, step.Description))
            .ToArrayAsync(cancellationToken);

        var evidenceRows = await dbContext.Evidence
            .AsNoTracking()
            .Where(item => item.SubjectType == EvidenceSubjectType.BusinessFunction
                && item.SubjectId == businessFunctionId)
            .Select(item => new
            {
                item.Id,
                item.EvidenceType,
                item.SourceTitle,
                item.ProvidedAt,
            })
            .ToArrayAsync(cancellationToken);
        var evidence = evidenceRows
            .OrderByDescending(item => item.ProvidedAt)
            .Select(item => new EvidenceSummaryResponse(
                item.Id,
                item.EvidenceType.ToString(),
                item.SourceTitle))
            .ToArray();

        var relations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(item => (item.SourceType == KnowledgeTargetType.BusinessFunction && item.SourceId == businessFunctionId)
                || (item.TargetType == KnowledgeTargetType.BusinessFunction && item.TargetId == businessFunctionId))
            .ToArrayAsync(cancellationToken);
        var relationIds = relations.Select(item => item.Id).ToArray();
        var evidenceCounts = await dbContext.Evidence.AsNoTracking()
            .Where(item => item.SubjectType == EvidenceSubjectType.KnowledgeRelation && relationIds.Contains(item.SubjectId))
            .GroupBy(item => item.SubjectId)
            .Select(group => new { RelationshipId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.RelationshipId, item => item.Count, cancellationToken);
        var relatedData = new List<RelatedDataResponse>();
        var businessRules = new List<BusinessRuleSummaryResponse>();
        var integrations = new List<IntegrationSummaryResponse>();
        var adjacentFunctions = new List<string>();
        var integrationCount = 0;
        foreach (var relation in relations)
        {
            var outgoing = relation.SourceType == KnowledgeTargetType.BusinessFunction && relation.SourceId == businessFunctionId;
            var otherType = outgoing ? relation.TargetType : relation.SourceType;
            var otherId = outgoing ? relation.TargetId : relation.SourceId;
            var other = await relationshipTargetResolver.Resolve(otherType, otherId, cancellationToken);
            if (other is null) continue;
            if (outgoing && otherType is KnowledgeTargetType.DatabaseObject or KnowledgeTargetType.DatabaseColumn
                && relation.RelationType is RelationType.Reads or RelationType.Writes or RelationType.UsesField)
            {
                relatedData.Add(new RelatedDataResponse(relation.Id,
                    new KnowledgeTargetReferenceResponse(otherType.ToString(), otherId), other.Title,
                    relation.RelationType.ToString(), evidenceCounts.GetValueOrDefault(relation.Id)));
            }
            if (relation.RelationType == RelationType.Calls && otherType == KnowledgeTargetType.BusinessFunction)
            {
                adjacentFunctions.Add(other.Title);
            }
            if (outgoing && relation.RelationType == RelationType.AppliesRule && otherType == KnowledgeTargetType.BusinessRule)
            {
                var evidenceCount = await dbContext.Evidence.AsNoTracking().CountAsync(item =>
                    item.SubjectType == EvidenceSubjectType.BusinessRule && item.SubjectId == otherId, cancellationToken);
                businessRules.Add(new BusinessRuleSummaryResponse(relation.Id, otherId, other.Title,
                    other.KnowledgeStatus, evidenceCount));
            }
            if (outgoing && otherType == KnowledgeTargetType.Integration)
            {
                integrations.Add(new IntegrationSummaryResponse(relation.Id, otherId, other.Title, relation.RelationType.ToString()));
                integrationCount++;
            }
        }

        var callers = string.IsNullOrWhiteSpace(function.CallerSummary)
            ? Array.Empty<string>()
            : new[] { function.CallerSummary };
        var unknownItems = await dbContext.UnknownItemTargets.AsNoTracking()
            .Where(target => target.TargetType == KnowledgeTargetType.BusinessFunction
                && target.TargetId == businessFunctionId
                && target.UnknownItem.Status != UnknownItemStatus.Closed)
            .Select(target => new UnknownItemSummaryResponse(
                target.UnknownItem.Id, target.UnknownItem.Question, target.UnknownItem.Status.ToString()))
            .ToArrayAsync(cancellationToken);

        var actor = await capabilityResolver.ResolveActor(cancellationToken);
        return new BusinessFunctionDetailResponse(
            function.Id,
            new KnowledgeSystemReferenceResponse(function.SystemId, function.SystemName),
            concurrencyTokenCodec.Encode(function.Version),
            new BusinessFunctionHeaderResponse(
                function.Name,
                function.FunctionType,
                function.RewriteStatus.ToString(),
                function.KnowledgeStatus.ToString()),
            new BusinessFunctionOverviewResponse(
                function.Purpose,
                function.CallerSummary,
                function.InputDescription,
                function.OutputDescription),
            processSteps,
            relatedData,
            businessRules,
            integrations,
            evidence,
            unknownItems,
            new BusinessFunctionContextRailResponse(
                callers,
                adjacentFunctions.Distinct(StringComparer.OrdinalIgnoreCase).ToArray(),
                integrationCount,
                unknownItems.Length),
            SoftDeleteCapabilityResolver.CanDelete(actor, function.CreatedByUserId),
            ["UpdateBusinessFunctionOverview", "ReplaceBusinessProcessSteps", "AddKnowledgeRelation", "AddEvidence", "ChangeKnowledgeStatus", "CreateUnknownItem"]);
    }

    private static Dictionary<string, string[]> Validate(
        BusinessFunctionsListQuery request,
        out RewriteStatus? rewriteStatus,
        out KnowledgeStatus? knowledgeStatus,
        out bool? hasUnknownItems,
        out BusinessFunctionSort sort)
    {
        var errors = new Dictionary<string, string[]>();
        rewriteStatus = null;
        knowledgeStatus = null;
        hasUnknownItems = null;
        sort = BusinessFunctionSort.UpdatedAtDescending;

        if (request.SystemId.HasValue && !ApiIdParser.IsSafePositive(request.SystemId.Value))
        {
            errors["systemId"] = ["系统 ID 必须是 JavaScript 安全范围内的正整数。"]; 
        }

        if (request.Page is < 1)
        {
            errors["page"] = ["页码必须从 1 开始。"]; 
        }

        if (request.PageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"]; 
        }

        if (request.RewriteStatus is not null)
        {
            if (!Enum.TryParse<RewriteStatus>(request.RewriteStatus, false, out var parsed)
                || parsed.ToString() != request.RewriteStatus)
            {
                errors["rewriteStatus"] = ["改写状态筛选值无效。"]; 
            }
            else
            {
                rewriteStatus = parsed;
            }
        }

        if (request.KnowledgeStatus is not null)
        {
            if (!Enum.TryParse<KnowledgeStatus>(request.KnowledgeStatus, false, out var parsed)
                || parsed.ToString() != request.KnowledgeStatus)
            {
                errors["knowledgeStatus"] = ["知识状态筛选值无效。"]; 
            }
            else
            {
                knowledgeStatus = parsed;
            }
        }

        if (request.HasUnknownItems is not null)
        {
            if (!bool.TryParse(request.HasUnknownItems, out var parsed))
            {
                errors["hasUnknownItems"] = ["待确认事项筛选值必须为 true 或 false。"]; 
            }
            else
            {
                hasUnknownItems = parsed;
            }
        }

        if (request.Sort is not null && !TryParseSort(request.Sort, out sort))
        {
            errors["sort"] = ["排序值无效。"]; 
        }

        return errors;
    }

    private static bool TryParseSort(string value, out BusinessFunctionSort sort)
    {
        sort = value switch
        {
            "name:asc" => BusinessFunctionSort.NameAscending,
            "name:desc" => BusinessFunctionSort.NameDescending,
            "updatedAt:asc" => BusinessFunctionSort.UpdatedAtAscending,
            "updatedAt:desc" => BusinessFunctionSort.UpdatedAtDescending,
            "knowledgeStatus:asc" => BusinessFunctionSort.KnowledgeStatusAscending,
            "knowledgeStatus:desc" => BusinessFunctionSort.KnowledgeStatusDescending,
            _ => BusinessFunctionSort.Invalid,
        };
        return sort != BusinessFunctionSort.Invalid;
    }

    private static IEnumerable<BusinessFunctionListRow> ApplySort(
        IReadOnlyList<BusinessFunctionListRow> rows,
        BusinessFunctionSort sort)
    {
        return sort switch
        {
            BusinessFunctionSort.NameAscending => rows.OrderBy(function => function.Name, StringComparer.OrdinalIgnoreCase),
            BusinessFunctionSort.NameDescending => rows.OrderByDescending(function => function.Name, StringComparer.OrdinalIgnoreCase),
            BusinessFunctionSort.UpdatedAtAscending => rows.OrderBy(function => function.UpdatedAt).ThenBy(function => function.Name, StringComparer.OrdinalIgnoreCase),
            BusinessFunctionSort.KnowledgeStatusAscending => rows.OrderBy(function => function.KnowledgeStatus).ThenBy(function => function.Name, StringComparer.OrdinalIgnoreCase),
            BusinessFunctionSort.KnowledgeStatusDescending => rows.OrderByDescending(function => function.KnowledgeStatus).ThenBy(function => function.Name, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderByDescending(function => function.UpdatedAt).ThenBy(function => function.Name, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private sealed record BusinessFunctionListRow(
        long Id,
        string Name,
        long SystemId,
        string SystemName,
        string FunctionType,
        string? Purpose,
        RewriteStatus RewriteStatus,
        KnowledgeStatus KnowledgeStatus,
        DateTimeOffset UpdatedAt,
        int RelatedDataCount,
        int RuleCount,
        int UnknownCount);

    private enum BusinessFunctionSort
    {
        Invalid,
        NameAscending,
        NameDescending,
        UpdatedAtAscending,
        UpdatedAtDescending,
        KnowledgeStatusAscending,
        KnowledgeStatusDescending,
    }
}
