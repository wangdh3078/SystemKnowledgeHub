using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Integrations.Domain;

public sealed class Integration
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public IntegrationType IntegrationType { get; set; }
    public long? SourceSystemId { get; set; }
    public KnowledgeSystem? SourceSystem { get; set; }
    public string SourcePartyName { get; set; } = string.Empty;
    public long? TargetSystemId { get; set; }
    public KnowledgeSystem? TargetSystem { get; set; }
    public string TargetPartyName { get; set; } = string.Empty;
    public IntegrationFlowDirection FlowDirection { get; set; }
    public string? Purpose { get; set; }
    public string? TopicOrQueue { get; set; }
    public string? EndpointDisplay { get; set; }
    public string? EndpointJson { get; set; }
    public long? DatabaseSourceId { get; set; }
    public DatabaseSource? DatabaseSource { get; set; }
    public long? DatabaseObjectId { get; set; }
    public DatabaseObject? DatabaseObject { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? CreatedByRole { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public KnowledgeStatus KnowledgeStatus { get; set; } = KnowledgeStatus.Unknown;
    public string? KnowledgeStatusReason { get; set; }
    public DateTimeOffset KnowledgeStatusChangedAt { get; set; }
    public string KnowledgeStatusChangedByName { get; set; } = string.Empty;
    public string KnowledgeStatusChangedByRole { get; set; } = string.Empty;
    public long Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long? DeletedByUserId { get; set; }
    public string? DeletedByDisplayName { get; set; }
    public ICollection<IntegrationContractField> ContractFields { get; set; } = [];
}

public enum IntegrationType { HttpApi, RabbitMq, FileExchange, DatabaseDependency }
public enum IntegrationFlowDirection { OneWay, Bidirectional }
