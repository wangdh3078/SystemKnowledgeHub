using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Relationships.Application;

public sealed class RelationshipQueries(
    KnowledgeHubDbContext dbContext,
    RelationshipTargetResolver targetResolver,
    RelationshipEndpointPolicy endpointPolicy,
    ConcurrencyTokenCodec tokenCodec)
{
    public async Task<KnowledgeTargetsQueryResult> SearchKnowledgeTargets(
        SearchKnowledgeTargetsQuery request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.Purpose != "RelationTarget") errors["purpose"] = ["当前 Relationship Slice 只接受 RelationTarget。"];
        if (!request.SystemId.HasValue || !ApiIdParser.IsSafePositive(request.SystemId.Value)) errors["systemId"] = ["必须提供有效的 System Context。"];
        if (!request.SourceId.HasValue || !ApiIdParser.IsSafePositive(request.SourceId.Value)) errors["sourceId"] = ["必须提供有效的 Source。"];
        if (!TryParseExact(request.SourceType, out KnowledgeTargetType sourceType)) errors["sourceType"] = ["SourceType 无效。"];
        if (!TryParseExact(request.RelationType, out RelationType relationType)) errors["relationType"] = ["RelationType 无效。"];
        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? 20;
        if (page < 1) errors["page"] = ["页码必须从 1 开始。"];
        if (pageSize is < 1 or > 100) errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"];
        if (errors.Count > 0) return new(null, errors, RelationshipFailure.Validation);

        var source = await targetResolver.Resolve(sourceType, request.SourceId!.Value, cancellationToken);
        if (source is null) return new(null, null, RelationshipFailure.NotFound, "未找到关系源对象。");
        if (!source.Systems.Any(item => item.Id == request.SystemId))
        {
            return new(null, null, RelationshipFailure.ReferenceInvalid, "Source 不属于当前 System Context。");
        }

        var allowed = endpointPolicy.AllowedTargets(relationType, sourceType);
        if (allowed.Count == 0)
        {
            return new(null, null, RelationshipFailure.ReferenceInvalid, "RelationType 与 SourceType 组合无效。");
        }

        var candidates = await targetResolver.Search(allowed, request.SystemId!.Value, request.Query, cancellationToken);
        var valid = candidates
            .Where(item => !(item.Target.Type == sourceType.ToString() && item.Target.Id == request.SourceId))
            .ToArray();
        var items = valid.Skip((page - 1) * pageSize).Take(pageSize).ToArray();
        return new(new KnowledgeTargetsResponse(items, page, pageSize, valid.Length), null, RelationshipFailure.None);
    }

    public async Task<RelationshipDetailQueryResult> GetRelationshipDetail(
        long id,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.KnowledgeRelations.AsNoTracking()
            .SingleOrDefaultAsync(relation => relation.Id == id, cancellationToken);
        if (item is null) return new(null, RelationshipFailure.NotFound);

        var source = await targetResolver.Resolve(item.SourceType, item.SourceId, cancellationToken);
        var target = await targetResolver.Resolve(item.TargetType, item.TargetId, cancellationToken);
        if (source is null || target is null) return new(null, RelationshipFailure.ReferenceInvalid, "关系端点已不存在。");
        var endpointError = endpointPolicy.Validate(item.SourceType, item.SourceId, source, item.RelationType, item.TargetType, item.TargetId, target);
        if (endpointError is not null) return new(null, RelationshipFailure.ReferenceInvalid, endpointError);

        var evidenceRows = await dbContext.Evidence.AsNoTracking()
            .Where(e => e.SubjectType == EvidenceSubjectType.KnowledgeRelation && e.SubjectId == id)
            .Select(e => new { e.Id, e.EvidenceType, e.SourceTitle, e.ProvidedAt })
            .ToArrayAsync(cancellationToken);
        var evidence = evidenceRows.OrderByDescending(e => e.ProvidedAt)
            .Select(e => new RelationshipEvidenceResponse(e.Id, e.EvidenceType.ToString(), e.SourceTitle))
            .ToArray();

        return new(new RelationshipDetailResponse(
            item.Id,
            tokenCodec.Encode(item.Version),
            Endpoint(item.SourceType, item.SourceId, source),
            Endpoint(item.TargetType, item.TargetId, target),
            item.RelationType.ToString(),
            item.Description,
            item.KnowledgeStatus.ToString(),
            evidence,
            [],
            new RelationshipPersonContextResponse(item.CreatedByName, item.CreatedByRole, item.CreatedAt),
            new RelationshipPersonContextResponse(item.KnowledgeStatusChangedByName, item.KnowledgeStatusChangedByRole, item.KnowledgeStatusChangedAt),
            ["UpdateKnowledgeRelationDescription", "AddEvidence", "ChangeRelationKnowledgeStatus"]), RelationshipFailure.None);
    }

    private static RelationshipEndpointResponse Endpoint(KnowledgeTargetType type, long id, RelationshipEndpointContext context)
        => new(new TargetReferenceResponse(type.ToString(), id), context.Title, string.Join(" / ", context.Systems.Select(item => item.Name)));

    internal static bool TryParseExact<T>(string? value, out T parsed) where T : struct, Enum
        => Enum.TryParse(value, false, out parsed) && parsed.ToString() == value;
}
