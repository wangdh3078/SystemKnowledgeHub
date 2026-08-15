using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Relationships.Domain;

public sealed class KnowledgeRelation
{
    public long Id { get; set; }
    public KnowledgeTargetType SourceType { get; set; }
    public long SourceId { get; set; }
    public KnowledgeTargetType TargetType { get; set; }
    public long TargetId { get; set; }
    public RelationType RelationType { get; set; }
    public string? Description { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? CreatedByRole { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public KnowledgeStatus KnowledgeStatus { get; set; } = KnowledgeStatus.Unknown;
    public string? KnowledgeStatusReason { get; set; }
    public DateTimeOffset KnowledgeStatusChangedAt { get; set; }
    public string KnowledgeStatusChangedByName { get; set; } = string.Empty;
    public string KnowledgeStatusChangedByRole { get; set; } = string.Empty;
    public long Version { get; set; } = 1;
}

public enum KnowledgeTargetType
{
    System,
    DatabaseSource,
    BusinessFunction,
    DatabaseObject,
    DatabaseColumn,
    BusinessRule,
    Integration,
}

public enum RelationType
{
    Calls,
    Reads,
    Writes,
    UsesField,
    AppliesRule,
    PublishesVia,
    ConsumesVia,
    UsesIntegration,
    DependsOn,
}
