using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Systems.Domain;

public sealed class KnowledgeSystem
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string SystemType { get; set; } = string.Empty;
    public SystemLifecycle Lifecycle { get; set; }
    public string? Purpose { get; set; }
    public string? MainUsersJson { get; set; }
    public string? RepositoryName { get; set; }
    public string? RepositoryUrl { get; set; }
    public string? DeploymentJson { get; set; }
    public string? MainProjectsJson { get; set; }
    public string? MainEntryPointsJson { get; set; }
    public string? Notes { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? CreatedByRole { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public KnowledgeStatus KnowledgeStatus { get; set; }
    public string? KnowledgeStatusReason { get; set; }
    public DateTimeOffset KnowledgeStatusChangedAt { get; set; }
    public string KnowledgeStatusChangedByName { get; set; } = string.Empty;
    public string KnowledgeStatusChangedByRole { get; set; } = string.Empty;
    public long Version { get; set; } = 1;

    public ICollection<SystemTechnologyTag> TechnologyTags { get; set; } = [];
}
