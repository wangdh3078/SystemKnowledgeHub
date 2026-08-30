using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

public sealed class DatabaseDiscoveryRun
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public long ProfileConfigurationRevision { get; set; }
    public long SecretVersion { get; set; }
    public long? BaseSnapshotId { get; set; }
    public long? ScopeGenerationId { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public DatabaseDiscoveryRunStatus Status { get; set; }
    public string? LeaseOwnerId { get; set; }
    public string? LeaseToken { get; set; }
    public DateTimeOffset? LeaseHeartbeatAt { get; set; }
    public DateTimeOffset? LeaseExpiresAt { get; set; }
    public DateTimeOffset? CancellationRequestedAt { get; set; }
    public long? CancellationRequestedByUserId { get; set; }
    public DatabaseProviderType ProviderType { get; set; }
    public string? ProviderVersion { get; set; }
    public string RequestedIncludedSchemasJson { get; set; } = "[]";
    public string RequestedProviderSpecificOptionsJson { get; set; } = "{\"version\":1}";
    public string? ScopeFingerprint { get; set; }
    public string? CapabilitySnapshotJson { get; set; }
    public string? ErrorCode { get; set; }
    public string? ErrorSummary { get; set; }
    public string? SafeErrorMetadataJson { get; set; }
    public string? ObjectCountsJson { get; set; }
    public long RequestedByUserId { get; set; }
    public string RequestedByDisplayName { get; set; } = string.Empty;
    public long Version { get; set; } = 1;

    public DatabaseConnectionProfile Profile { get; set; } = null!;
    public DatabaseDiscoveryScopeGeneration? ScopeGeneration { get; set; }
    public DatabaseDiscoverySnapshot? Snapshot { get; set; }
}

public sealed class DatabaseDiscoveryScopeGeneration
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public string ScopeFingerprint { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }

    public DatabaseConnectionProfile Profile { get; set; } = null!;
}

public sealed class DatabaseDiscoverySnapshot
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public long ProfileId { get; set; }
    public DateTimeOffset CapturedAt { get; set; }
    public int FormatVersion { get; set; }
    public int IdentityAlgorithmVersion { get; set; }
    public long ScopeGenerationId { get; set; }
    public string ScopeFingerprint { get; set; } = string.Empty;
    public DatabaseDiscoveryCompleteness Completeness { get; set; }
    public string CanonicalContentJson { get; set; } = string.Empty;
    public string ContentSha256 { get; set; } = string.Empty;
    public string CountsJson { get; set; } = string.Empty;

    public DatabaseDiscoveryRun Run { get; set; } = null!;
    public DatabaseConnectionProfile Profile { get; set; } = null!;
    public DatabaseDiscoveryScopeGeneration ScopeGeneration { get; set; } = null!;
}

public sealed class DatabaseDiscoveryDifference
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public long? BaseSnapshotId { get; set; }
    public long TargetSnapshotId { get; set; }
    public long ScopeGenerationId { get; set; }
    public int AlgorithmVersion { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public string SummaryCountsJson { get; set; } = string.Empty;
    public string ContentSha256 { get; set; } = string.Empty;

    public ICollection<DatabaseDiscoveryDifferenceEntry> Entries { get; set; } = [];
}

public sealed class DatabaseDiscoveryDifferenceEntry
{
    public long Id { get; set; }
    public long DifferenceId { get; set; }
    public DatabaseDiscoveryEntityKind EntityKind { get; set; }
    public string LogicalIdentity { get; set; } = string.Empty;
    public string? ParentLogicalIdentity { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public DatabaseDiscoveryDifferenceState State { get; set; }
    public string? BeforeJson { get; set; }
    public string? AfterJson { get; set; }

    public DatabaseDiscoveryDifference Difference { get; set; } = null!;
}
