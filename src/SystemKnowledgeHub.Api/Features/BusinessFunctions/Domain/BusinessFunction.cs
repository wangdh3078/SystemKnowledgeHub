using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;

public sealed class BusinessFunction
{
    public long Id { get; set; }
    public long SystemId { get; set; }
    public KnowledgeSystem System { get; set; } = null!;
    public string Name { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public string FunctionType { get; set; } = string.Empty;
    public string? Purpose { get; set; }
    public string? CallerSummary { get; set; }
    public string? InputDescription { get; set; }
    public string? OutputDescription { get; set; }
    public RewriteStatus RewriteStatus { get; set; } = RewriteStatus.Unknown;
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

    public ICollection<BusinessProcessStep> ProcessSteps { get; set; } = [];
}
