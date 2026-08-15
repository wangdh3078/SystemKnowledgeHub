using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.BusinessRules.Domain;

public sealed class BusinessRule
{
    public long Id { get; set; }
    public long SystemId { get; set; }
    public KnowledgeSystem System { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? ConditionText { get; set; }
    public string? ResultText { get; set; }
    public string? InputDataJson { get; set; }
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
