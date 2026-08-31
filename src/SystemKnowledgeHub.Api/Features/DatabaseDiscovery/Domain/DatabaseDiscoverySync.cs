using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

public enum DatabaseDiscoverySyncPlanStatus { Draft, Ready, Applied, Superseded }

public enum DatabaseDiscoverySyncActionType
{
    CreateDatabaseObject, LinkExistingDatabaseObject,
    CreateDatabaseColumn, LinkExistingDatabaseColumn,
    UpdateDatabaseObjectStructure, UpdateDatabaseColumnStructure,
    MarkObjectSourceMissing, ClearObjectSourceMissing,
    MarkColumnSourceMissing, ClearColumnSourceMissing,
}

public enum DatabaseDiscoveryReconciliationStatus { Applicable, NoAction, Conflict, Unsupported }

public enum DatabaseDiscoverySyncAuditAction
{
    PlanCreated, SelectionChanged, PreviewGenerated, PlanConfirmed, PlanApplied, PlanSuperseded,
}

public sealed class DatabaseObjectDiscoveryBinding
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public long ScopeGenerationId { get; set; }
    public int IdentityAlgorithmVersion { get; set; }
    public string SchemaLogicalIdentity { get; set; } = string.Empty;
    public string LogicalIdentity { get; set; } = string.Empty;
    public long DatabaseObjectId { get; set; }
    public long FirstAppliedSnapshotId { get; set; }
    public long LastAppliedSnapshotId { get; set; }
    public long? SourceMissingSinceSnapshotId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
    public DatabaseConnectionProfile Profile { get; set; } = null!;
    public DatabaseDiscoveryScopeGeneration ScopeGeneration { get; set; } = null!;
    public DatabaseObject DatabaseObject { get; set; } = null!;
}

public sealed class DatabaseColumnDiscoveryBinding
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public long ScopeGenerationId { get; set; }
    public int IdentityAlgorithmVersion { get; set; }
    public string SchemaLogicalIdentity { get; set; } = string.Empty;
    public string ParentObjectLogicalIdentity { get; set; } = string.Empty;
    public string LogicalIdentity { get; set; } = string.Empty;
    public long DatabaseColumnId { get; set; }
    public long FirstAppliedSnapshotId { get; set; }
    public long LastAppliedSnapshotId { get; set; }
    public long? SourceMissingSinceSnapshotId { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
    public DatabaseConnectionProfile Profile { get; set; } = null!;
    public DatabaseDiscoveryScopeGeneration ScopeGeneration { get; set; } = null!;
    public DatabaseColumn DatabaseColumn { get; set; } = null!;
}

public sealed class DatabaseDiscoverySyncPlan
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public long DatabaseSourceId { get; set; }
    public long ProfileConfigurationRevision { get; set; }
    public long? BaseSnapshotId { get; set; }
    public long TargetSnapshotId { get; set; }
    public long? TargetDifferenceId { get; set; }
    public long ScopeGenerationId { get; set; }
    public int IdentityAlgorithmVersion { get; set; }
    public DatabaseDiscoverySyncPlanStatus Status { get; set; }
    public int SelectionFormatVersion { get; set; } = 1;
    public string SelectionJson { get; set; } = "[]";
    public int? PreviewFormatVersion { get; set; }
    public string? PreviewPayloadJson { get; set; }
    public string? PreviewHash { get; set; }
    public string? ConfirmedPreviewHash { get; set; }
    public long CreatedByUserId { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long? ConfirmedByUserId { get; set; }
    public DateTimeOffset? ConfirmedAt { get; set; }
    public DateTimeOffset? AppliedAt { get; set; }
    public long Version { get; set; } = 1;
    public DatabaseConnectionProfile Profile { get; set; } = null!;
    public DatabaseSource DatabaseSource { get; set; } = null!;
    public DatabaseDiscoverySnapshot? BaseSnapshot { get; set; }
    public DatabaseDiscoverySnapshot TargetSnapshot { get; set; } = null!;
    public DatabaseDiscoveryDifference? TargetDifference { get; set; }
    public DatabaseDiscoveryScopeGeneration ScopeGeneration { get; set; } = null!;
    public DatabaseDiscoverySyncApplyResult? ApplyResult { get; set; }
}

public sealed class DatabaseDiscoverySyncApplyResult
{
    public long Id { get; set; }
    public long PlanId { get; set; }
    public int CreatedObjects { get; set; }
    public int LinkedObjects { get; set; }
    public int CreatedColumns { get; set; }
    public int LinkedColumns { get; set; }
    public int UpdatedObjects { get; set; }
    public int UpdatedColumns { get; set; }
    public int MarkedMissing { get; set; }
    public int ClearedMissing { get; set; }
    public DateTimeOffset AppliedAt { get; set; }
    public long AppliedByUserId { get; set; }
    public string AppliedByDisplayName { get; set; } = string.Empty;
    public DatabaseDiscoverySyncPlan Plan { get; set; } = null!;
}

public sealed class DatabaseDiscoverySyncAuditEvent
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public long? PlanId { get; set; }
    public DatabaseDiscoverySyncAuditAction Action { get; set; }
    public DatabaseConnectionAuditOutcome Outcome { get; set; }
    public string? ReasonCode { get; set; }
    public string? SafeMetadataJson { get; set; }
    public long ActorUserId { get; set; }
    public string ActorDisplayName { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }
    public DatabaseConnectionProfile Profile { get; set; } = null!;
    public DatabaseDiscoverySyncPlan? Plan { get; set; }
    public User Actor { get; set; } = null!;
}
