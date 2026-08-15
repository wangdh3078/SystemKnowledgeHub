using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;

namespace SystemKnowledgeHub.Api.Features.Relationships.Application;

public sealed class RelationshipEndpointPolicy
{
    public IReadOnlyList<KnowledgeTargetType> AllowedTargets(RelationType relationType, KnowledgeTargetType sourceType)
    {
        return (relationType, sourceType) switch
        {
            (RelationType.Calls, KnowledgeTargetType.BusinessFunction) => [KnowledgeTargetType.BusinessFunction],
            (RelationType.Reads or RelationType.Writes, KnowledgeTargetType.BusinessFunction) => [KnowledgeTargetType.DatabaseObject, KnowledgeTargetType.DatabaseColumn],
            (RelationType.UsesField, KnowledgeTargetType.BusinessFunction or KnowledgeTargetType.BusinessRule) => [KnowledgeTargetType.DatabaseColumn],
            (RelationType.AppliesRule, KnowledgeTargetType.BusinessFunction) => [KnowledgeTargetType.BusinessRule],
            (RelationType.PublishesVia or RelationType.ConsumesVia, KnowledgeTargetType.System or KnowledgeTargetType.BusinessFunction) => [KnowledgeTargetType.Integration],
            (RelationType.UsesIntegration, KnowledgeTargetType.BusinessFunction or KnowledgeTargetType.BusinessRule) => [KnowledgeTargetType.Integration],
            (RelationType.DependsOn, KnowledgeTargetType.System or KnowledgeTargetType.BusinessFunction or KnowledgeTargetType.Integration) => [KnowledgeTargetType.System, KnowledgeTargetType.DatabaseSource, KnowledgeTargetType.DatabaseObject],
            _ => [],
        };
    }

    public string? Validate(
        KnowledgeTargetType sourceType,
        long sourceId,
        RelationshipEndpointContext source,
        RelationType relationType,
        KnowledgeTargetType targetType,
        long targetId,
        RelationshipEndpointContext target)
    {
        if (sourceType == targetType && sourceId == targetId)
        {
            return "关系两端不能是同一个知识对象。";
        }
        if (!AllowedTargets(relationType, sourceType).Contains(targetType))
        {
            return $"{relationType} 不允许从 {sourceType} 指向 {targetType}。";
        }

        var hasSharedSystem = source.Systems.Any(left => target.Systems.Any(right => right.Id == left.Id));
        if (relationType == RelationType.Calls && !hasSharedSystem)
        {
            return "Calls 只允许同一系统内的 BusinessFunction → BusinessFunction；跨系统交互必须通过 Integration 表达。";
        }
        if (relationType is RelationType.Reads or RelationType.Writes or RelationType.UsesField or RelationType.AppliesRule
            && !hasSharedSystem)
        {
            return "该关系的 Source 与 Target 必须位于同一 System Context。";
        }

        return null;
    }
}
