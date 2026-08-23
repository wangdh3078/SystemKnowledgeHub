using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;

namespace SystemKnowledgeHub.Api.Features.Relationships.Application;

public sealed class RelationshipEndpointPolicy
{
    private static readonly KnowledgeTargetType[] DocumentStructuredTargets = [KnowledgeTargetType.System, KnowledgeTargetType.BusinessFunction, KnowledgeTargetType.DatabaseObject, KnowledgeTargetType.BusinessRule, KnowledgeTargetType.Integration];
    public IReadOnlyList<KnowledgeTargetType> AllowedTargets(RelationType relationType, KnowledgeTargetType sourceType)
    {
        if (sourceType == KnowledgeTargetType.KnowledgeDocument) return relationType switch
        {
            RelationType.Documents => DocumentStructuredTargets,
            RelationType.References => [.. DocumentStructuredTargets, KnowledgeTargetType.KnowledgeDocument],
            RelationType.Supersedes or RelationType.SpecifiedBy or RelationType.VerifiedBy => [KnowledgeTargetType.KnowledgeDocument],
            RelationType.AppliesTo => [KnowledgeTargetType.System, KnowledgeTargetType.BusinessFunction, KnowledgeTargetType.DatabaseObject, KnowledgeTargetType.Integration],
            _ => [],
        };
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

    public IReadOnlyList<DocumentType>? AllowedDocumentTargetTypes(
        RelationshipEndpointContext source,
        RelationType relationType,
        KnowledgeTargetType targetType)
    {
        if (source.DocumentType is null || targetType != KnowledgeTargetType.KnowledgeDocument)
        {
            return null;
        }

        return (source.DocumentType, relationType) switch
        {
            (_, RelationType.Supersedes) => [source.DocumentType.Value],
            (DocumentType.Requirement, RelationType.SpecifiedBy) => [DocumentType.Specification],
            (DocumentType.Requirement or DocumentType.Specification, RelationType.VerifiedBy) => [DocumentType.TestCase],
            (DocumentType.DesignNote, RelationType.References) => [DocumentType.Specification],
            _ => null,
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
        if (sourceType == KnowledgeTargetType.KnowledgeDocument)
        {
            var documentError = ValidateDocument(source.DocumentType, relationType, targetType, target.DocumentType);
            if (documentError is not null) return documentError;
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

    private static string? ValidateDocument(DocumentType? source, RelationType relation, KnowledgeTargetType targetType, DocumentType? target)
    {
        if (source is null) return "KnowledgeDocument 的文档类型无效。";
        if (relation == RelationType.Supersedes && target != source) return "Supersedes 仅允许替代同一 DocumentType 的知识文档。";
        if (relation == RelationType.Documents)
        {
            return null;
        }
        if (relation == RelationType.References && source != DocumentType.DesignNote)
        {
            return null;
        }

        return (source, relation, targetType, target) switch
        {
            (DocumentType.Requirement, RelationType.AppliesTo, KnowledgeTargetType.System or KnowledgeTargetType.BusinessFunction, _) => null,
            (DocumentType.Requirement, RelationType.SpecifiedBy, KnowledgeTargetType.KnowledgeDocument, DocumentType.Specification) => null,
            (DocumentType.Requirement or DocumentType.Specification, RelationType.VerifiedBy, KnowledgeTargetType.KnowledgeDocument, DocumentType.TestCase) => null,
            (DocumentType.Sop, RelationType.AppliesTo, KnowledgeTargetType.System or KnowledgeTargetType.BusinessFunction or KnowledgeTargetType.DatabaseObject or KnowledgeTargetType.Integration, _) => null,
            (DocumentType.Troubleshooting, RelationType.AppliesTo, KnowledgeTargetType.System or KnowledgeTargetType.DatabaseObject or KnowledgeTargetType.Integration, _) => null,
            (DocumentType.DesignNote, RelationType.References, KnowledgeTargetType.KnowledgeDocument, DocumentType.Specification) => null,
            (_, RelationType.Supersedes, _, _) => null,
            _ => "该 DocumentType 不支持所选关系类型、目标类型或目标文档类型。",
        };
    }
}
