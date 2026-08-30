using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public interface IDatabaseDiscoveryProvider
{
    DatabaseProviderType ProviderType { get; }

    Task<DatabaseProviderCapabilities> DetectCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);

    Task<CanonicalDatabaseDiscoverySnapshot> DiscoverAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        DatabaseProviderCapabilities capabilities,
        CancellationToken cancellationToken);
}

public sealed record DatabaseProviderCapabilities(
    IReadOnlyList<CanonicalCapability> Capabilities);

public sealed record DatabaseDiscoveryRequest(
    IReadOnlyList<string> IncludedSchemas,
    DatabaseDiscoveryLimits Limits);

public sealed record DatabaseDiscoveryLimits(
    int MaximumSchemas,
    int MaximumObjects,
    int MaximumColumns,
    int MaximumConstraintsAndIndexes,
    int MaximumSequences,
    int MaximumCanonicalSnapshotBytes);

public sealed class DatabaseDiscoveryOptions
{
    public const string SectionName = "DatabaseDiscovery";

    public int OverallTimeoutSeconds { get; set; } = 900;
    public int MaximumIncludedSchemas { get; set; } = 128;
    public int MaximumObjects { get; set; } = 25_000;
    public int MaximumColumns { get; set; } = 250_000;
    public int MaximumConstraintsAndIndexes { get; set; } = 250_000;
    public int MaximumSequences { get; set; } = 10_000;
    public int MaximumCanonicalSnapshotBytes { get; set; } = 128 * 1024 * 1024;
    public int LeaseDurationSeconds { get; set; } = 30;
    public int HeartbeatIntervalSeconds { get; set; } = 5;
    public int QueuePollIntervalMilliseconds { get; set; } = 500;

    public DatabaseDiscoveryLimits Limits => new(
        MaximumIncludedSchemas,
        MaximumObjects,
        MaximumColumns,
        MaximumConstraintsAndIndexes,
        MaximumSequences,
        MaximumCanonicalSnapshotBytes);

    public void Validate()
    {
        if (OverallTimeoutSeconds is < 1 or > 86_400
            || MaximumIncludedSchemas is < 1 or > 1024
            || MaximumObjects < 1
            || MaximumColumns < 1
            || MaximumConstraintsAndIndexes < 1
            || MaximumSequences < 1
            || MaximumCanonicalSnapshotBytes is < 1024 or > 536_870_912
            || LeaseDurationSeconds is < 2 or > 3600
            || HeartbeatIntervalSeconds < 1
            || HeartbeatIntervalSeconds >= LeaseDurationSeconds
            || QueuePollIntervalMilliseconds is < 25 or > 60_000)
        {
            throw new InvalidOperationException("DatabaseDiscovery configuration is invalid.");
        }
    }
}

public sealed record CanonicalSnapshotPreparation(
    CanonicalDatabaseDiscoverySnapshot? Snapshot,
    string? CanonicalJson,
    string? ContentSha256,
    string? ScopeFingerprint,
    string? CountsJson,
    string? ErrorCode,
    string? ErrorSummary)
{
    public bool Succeeded => Snapshot is not null;
}
