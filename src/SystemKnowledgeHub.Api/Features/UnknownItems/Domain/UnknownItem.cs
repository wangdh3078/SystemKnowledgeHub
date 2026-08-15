using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.UnknownItems.Domain;

public sealed class UnknownItem
{
    public long Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public long SystemId { get; set; }
    public KnowledgeSystem System { get; set; } = null!;
    public string Question { get; set; } = string.Empty;
    public string? Context { get; set; }
    public UnknownItemPriority Priority { get; set; }
    public UnknownItemStatus Status { get; set; } = UnknownItemStatus.Open;
    public DateTimeOffset? InvestigationStartedAt { get; set; }
    public DateTimeOffset? ConclusionConfirmedAt { get; set; }
    public DateTimeOffset? ClosedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? CreatedByRole { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
    public ICollection<UnknownItemTarget> Targets { get; set; } = [];
    public ICollection<Finding> Findings { get; set; } = [];
    public Resolution? Resolution { get; set; }
    public ICollection<KnowledgeUpdate> KnowledgeUpdates { get; set; } = [];
    public ICollection<UnknownItemActivity> Activities { get; set; } = [];
}

public sealed class UnknownItemTarget
{
    public long Id { get; set; }
    public long UnknownItemId { get; set; }
    public UnknownItem UnknownItem { get; set; } = null!;
    public KnowledgeTargetType TargetType { get; set; }
    public long TargetId { get; set; }
    public bool IsPrimary { get; set; }
    public string DisplaySnapshot { get; set; } = string.Empty;
}

public sealed class Finding
{
    public long Id { get; set; }
    public long UnknownItemId { get; set; }
    public UnknownItem UnknownItem { get; set; } = null!;
    public string Content { get; set; } = string.Empty;
    public string RecordedByName { get; set; } = string.Empty;
    public string RecordedByRole { get; set; } = string.Empty;
    public string? RecordedByTeam { get; set; }
    public string? RecordedByExternalKey { get; set; }
    public string? RecordedBySource { get; set; }
    public string? RecordedByNote { get; set; }
    public DateTimeOffset RecordedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class Resolution
{
    public long Id { get; set; }
    public long UnknownItemId { get; set; }
    public UnknownItem UnknownItem { get; set; } = null!;
    public string Conclusion { get; set; } = string.Empty;
    public string? ConfirmedByName { get; set; }
    public string? ConfirmedByRole { get; set; }
    public string? ConfirmedByTeam { get; set; }
    public string? ConfirmedByExternalKey { get; set; }
    public string? ConfirmedBySource { get; set; }
    public string? ConfirmedByNote { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class KnowledgeUpdate
{
    public long Id { get; set; }
    public long UnknownItemId { get; set; }
    public UnknownItem UnknownItem { get; set; } = null!;
    public KnowledgeTargetType TargetType { get; set; }
    public long TargetId { get; set; }
    public string? SubjectDetailKey { get; set; }
    public string ChangeSummary { get; set; } = string.Empty;
    public string BeforeJson { get; set; } = "null";
    public string AfterJson { get; set; } = "null";
    public KnowledgeUpdateStatus Status { get; set; } = KnowledgeUpdateStatus.Proposed;
    public KnowledgeStatus? KnowledgeStatusBefore { get; set; }
    public KnowledgeStatus? KnowledgeStatusAfter { get; set; }
    public string? AppliedByName { get; set; }
    public string? AppliedByRole { get; set; }
    public string? AppliedByTeam { get; set; }
    public string? AppliedByExternalKey { get; set; }
    public string? AppliedBySource { get; set; }
    public string? AppliedByNote { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}

public sealed class UnknownItemActivity
{
    public long Id { get; set; }
    public long UnknownItemId { get; set; }
    public UnknownItem UnknownItem { get; set; } = null!;
    public UnknownItemActivityType ActivityType { get; set; }
    public string ActorName { get; set; } = string.Empty;
    public string ActorRole { get; set; } = string.Empty;
    public string? ActorTeam { get; set; }
    public string? ActorExternalKey { get; set; }
    public string? ActorSource { get; set; }
    public string? ActorNote { get; set; }
    public DateTimeOffset OccurredAt { get; set; }
    public string? Note { get; set; }
    public string? RelatedType { get; set; }
    public long? RelatedId { get; set; }
}

public enum UnknownItemPriority { High, Medium, Low }
public enum UnknownItemStatus { Open, Investigating, ConclusionConfirmed, Closed }
public enum KnowledgeUpdateStatus { Proposed, Applied }
public enum UnknownItemActivityType
{
    Created,
    StatusChanged,
    FindingAdded,
    EvidenceAdded,
    ResolutionRecorded,
    KnowledgeUpdateApplied,
    Closed,
    Reopened,
}
