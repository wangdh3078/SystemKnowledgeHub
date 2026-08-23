using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.StatusProgression.Application;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Relationships.Application;

public sealed class RelationshipService(
    KnowledgeHubDbContext dbContext,
    RelationshipTargetResolver targetResolver,
    RelationshipEndpointPolicy endpointPolicy,
    KnowledgeStatusPolicy statusPolicy,
    ConcurrencyTokenCodec tokenCodec)
{
    public async Task<RelationshipCommandResult> Add(
        AddRelationshipCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateEndpoints(request.Source, request.Target, request.RelationType, request.Actor, out var sourceType, out var targetType, out var relationType);
        if (errors.Count > 0) return new(null, errors, RelationshipFailure.Validation);

        var source = await targetResolver.Resolve(sourceType, request.Source!.Id, cancellationToken);
        var target = await targetResolver.Resolve(targetType, request.Target!.Id, cancellationToken);
        if (source is null || target is null) return new(null, null, RelationshipFailure.NotFound, "关系 Source 或 Target 不存在。");
        var endpointError = endpointPolicy.Validate(sourceType, request.Source.Id, source, relationType, targetType, request.Target.Id, target);
        if (endpointError is not null) return new(null, null, RelationshipFailure.ReferenceInvalid, endpointError);

        var existingId = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(item => item.SourceType == sourceType && item.SourceId == request.Source.Id
                && item.TargetType == targetType && item.TargetId == request.Target.Id && item.RelationType == relationType)
            .Select(item => (long?)item.Id).SingleOrDefaultAsync(cancellationToken);
        if (existingId.HasValue)
        {
            return new(null, null, RelationshipFailure.Duplicate, "该精确关系已经存在。", new { existingRelationId = existingId.Value });
        }

        var now = DateTimeOffset.UtcNow;
        var role = NormalizeOptional(request.Actor!.Role) ?? "创建人";
        var item = new KnowledgeRelation
        {
            SourceType = sourceType, SourceId = request.Source.Id,
            TargetType = targetType, TargetId = request.Target.Id,
            RelationType = relationType, Description = NormalizeOptional(request.Description),
            CreatedAt = now, CreatedByName = request.Actor.DisplayName.Trim(), CreatedByRole = NormalizeOptional(request.Actor.Role), UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown, KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = request.Actor.DisplayName.Trim(), KnowledgeStatusChangedByRole = role, Version = 1,
        };
        dbContext.KnowledgeRelations.Add(item);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException)
        {
            return new(null, null, RelationshipFailure.Duplicate, "该精确关系已经存在。");
        }

        return new(new AddRelationshipResponse(item.Id,
            new(sourceType.ToString(), item.SourceId), relationType.ToString(), new(targetType.ToString(), item.TargetId),
            item.KnowledgeStatus.ToString(), tokenCodec.Encode(item.Version)), null, RelationshipFailure.None);
    }

    public async Task<RelationshipCommandResult> UpdateDescription(UpdateRelationshipDescriptionCommand request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.RelationshipId)) errors["id"] = ["Relationship ID 无效。"];
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载。"];
        if (errors.Count > 0) return new(null, errors, RelationshipFailure.Validation);

        var item = await dbContext.KnowledgeRelations.SingleOrDefaultAsync(x => x.Id == request.RelationshipId, cancellationToken);
        if (item is null) return new(null, null, RelationshipFailure.NotFound);
        if (item.Version != expectedVersion) return new(null, null, RelationshipFailure.Conflict);
        if (!await EndpointsRemainValid(item, cancellationToken)) return new(null, null, RelationshipFailure.ReferenceInvalid, "关系端点不存在或组合已失效。");
        item.Description = NormalizeOptional(request.Description);
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(null, null, RelationshipFailure.Conflict); }
        return new(new UpdateRelationshipDescriptionResponse(item.Id, item.Description, item.KnowledgeStatus.ToString(), tokenCodec.Encode(item.Version)), null, RelationshipFailure.None);
    }

    public async Task<RelationshipCommandResult> Delete(long relationshipId, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(relationshipId))
        {
            return new(null, new Dictionary<string, string[]> { ["id"] = ["Relationship ID 无效。"] }, RelationshipFailure.Validation);
        }
        var item = await dbContext.KnowledgeRelations.SingleOrDefaultAsync(x => x.Id == relationshipId, cancellationToken);
        if (item is null) return new(null, null, RelationshipFailure.NotFound);
        dbContext.KnowledgeRelations.Remove(item);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new(null, null, RelationshipFailure.None);
    }

    public async Task<RelationshipCommandResult> ChangeStatus(ChangeRelationshipStatusCommand request, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.RelationshipId)) errors["id"] = ["Relationship ID 无效。"];
        if (!RelationshipQueries.TryParseExact(request.TargetStatus, out KnowledgeStatus targetStatus)) errors["targetStatus"] = ["目标知识状态无效。"];
        if (string.IsNullOrWhiteSpace(request.Actor.DisplayName)) errors["actor"] = ["当前操作者不能为空。"];
        if (string.IsNullOrWhiteSpace(request.Actor.RoleOrIdentity)) errors["actor"] = ["当前操作者身份不能为空。"];
        if (request.Actor.OccurredAt == default) errors["actor"] = ["当前操作者时间不能为空。"];
        if (!tokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载。"];
        if (errors.Count > 0) return new(null, errors, RelationshipFailure.Validation);

        var item = await dbContext.KnowledgeRelations.SingleOrDefaultAsync(x => x.Id == request.RelationshipId, cancellationToken);
        if (item is null) return new(null, null, RelationshipFailure.NotFound);
        if (item.Version != expectedVersion) return new(null, null, RelationshipFailure.Conflict);
        if (!await EndpointsRemainValid(item, cancellationToken)) return new(null, null, RelationshipFailure.ReferenceInvalid, "关系端点不存在或组合已失效。");

        var evidence = await dbContext.Evidence.AsNoTracking()
            .Where(e => e.SubjectType == EvidenceSubjectType.KnowledgeRelation && e.SubjectId == item.Id)
            .Select(e => new { e.EvidenceType, e.SourceReference, e.SourceLocatorJson, e.ProviderName, e.ProviderRole, e.ProvidedAt })
            .ToArrayAsync(cancellationToken);
        var hasEvidence = evidence.Any(e => !string.IsNullOrWhiteSpace(e.SourceReference) || !string.IsNullOrWhiteSpace(e.SourceLocatorJson));
        var hasHuman = evidence.Any(e => e.EvidenceType == EvidenceType.HumanConfirmation
            && !string.IsNullOrWhiteSpace(e.ProviderName) && !string.IsNullOrWhiteSpace(e.ProviderRole) && e.ProvidedAt != default);
        var reason = NormalizeOptional(request.Reason);
        var decision = statusPolicy.Validate(item.KnowledgeStatus, targetStatus, reason, hasEvidence, hasHuman);
        if (!decision.IsAllowed)
        {
            return new(null, null,
                decision.Failure == KnowledgeStatusFailure.Conflict ? RelationshipFailure.Conflict : RelationshipFailure.BusinessRuleViolation,
                decision.Message, new { currentStatus = item.KnowledgeStatus.ToString(), targetStatus = targetStatus.ToString(), missingRequirement = decision.MissingRequirement });
        }

        var previous = item.KnowledgeStatus;
        item.KnowledgeStatus = targetStatus;
        item.KnowledgeStatusReason = reason;
        item.KnowledgeStatusChangedAt = request.Actor.OccurredAt;
        item.KnowledgeStatusChangedByName = request.Actor.DisplayName.Trim();
        item.KnowledgeStatusChangedByRole = request.Actor.RoleOrIdentity.Trim();
        item.UpdatedAt = DateTimeOffset.UtcNow;
        item.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(null, null, RelationshipFailure.Conflict); }
        return new(new ChangeRelationshipStatusResponse(item.Id, previous.ToString(), item.KnowledgeStatus.ToString(), reason,
            request.Actor.OccurredAt, tokenCodec.Encode(item.Version)), null, RelationshipFailure.None);
    }

    private async Task<bool> EndpointsRemainValid(KnowledgeRelation item, CancellationToken cancellationToken)
    {
        var source = await targetResolver.Resolve(item.SourceType, item.SourceId, cancellationToken);
        var target = await targetResolver.Resolve(item.TargetType, item.TargetId, cancellationToken);
        return source is not null && target is not null
            && endpointPolicy.Validate(item.SourceType, item.SourceId, source, item.RelationType, item.TargetType, item.TargetId, target) is null;
    }

    private static Dictionary<string, string[]> ValidateEndpoints(
        RelationshipTargetCommand? source, RelationshipTargetCommand? target, string relationTypeValue, RelationshipActorCommand? actor,
        out KnowledgeTargetType sourceType, out KnowledgeTargetType targetType, out RelationType relationType)
    {
        var errors = new Dictionary<string, string[]>();
        sourceType = default; targetType = default; relationType = default;
        if (source is null) errors["source"] = ["必须指定关系 Source。"];
        else { if (!RelationshipQueries.TryParseExact(source.Type, out sourceType)) errors["source.type"] = ["SourceType 无效。"]; if (!ApiIdParser.IsSafePositive(source.Id)) errors["source.id"] = ["Source ID 无效。"]; }
        if (target is null) errors["target"] = ["必须指定关系 Target。"];
        else { if (!RelationshipQueries.TryParseExact(target.Type, out targetType)) errors["target.type"] = ["TargetType 无效。"]; if (!ApiIdParser.IsSafePositive(target.Id)) errors["target.id"] = ["Target ID 无效。"]; }
        if (!RelationshipQueries.TryParseExact(relationTypeValue, out relationType)) errors["relationType"] = ["RelationType 无效。"];
        if (actor is null || string.IsNullOrWhiteSpace(actor.DisplayName)) errors["actor.displayName"] = ["创建人不能为空。"];
        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
