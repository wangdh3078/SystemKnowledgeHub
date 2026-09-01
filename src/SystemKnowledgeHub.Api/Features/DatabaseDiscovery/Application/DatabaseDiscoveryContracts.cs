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

public sealed class DatabaseDiscoveryProviderException(
    string errorCode,
    string safeSummary,
    string? vendorCode = null) : Exception
{
    public string ErrorCode { get; } = errorCode;
    public string SafeSummary { get; } = safeSummary;
    public string? VendorCode { get; } = vendorCode;
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

    public int ConnectionTimeoutSeconds { get; set; } = 15;
    public int CatalogCommandTimeoutSeconds { get; set; } = 60;
    public int OverallTimeoutSeconds { get; set; } = 900;
    public int MaximumIncludedSchemas { get; set; } = 128;
    public int MaximumObjects { get; set; } = 25_000;
    public int MaximumColumns { get; set; } = 250_000;
    public int MaximumConstraintsAndIndexes { get; set; } = 250_000;
    public int MaximumSequences { get; set; } = 10_000;
    public int MaximumCanonicalSnapshotBytes { get; set; } = 128 * 1024 * 1024;
    public int MaximumSyncPlanActions { get; set; } = 2_000;
    public bool SqlServerTrustServerCertificate { get; set; }
    public int LeaseDurationSeconds { get; set; } = 30;
    public int HeartbeatIntervalSeconds { get; set; } = 5;
    public int QueuePollIntervalMilliseconds { get; set; } = 2_000;

    public DatabaseDiscoveryLimits Limits => new(
        MaximumIncludedSchemas,
        MaximumObjects,
        MaximumColumns,
        MaximumConstraintsAndIndexes,
        MaximumSequences,
        MaximumCanonicalSnapshotBytes);

    public void Validate()
    {
        Require(ConnectionTimeoutSeconds is >= 1 and <= 300,
            "DatabaseDiscovery:ConnectionTimeoutSeconds must be between 1 and 300.");
        Require(CatalogCommandTimeoutSeconds is >= 1 and <= 3_600,
            "DatabaseDiscovery:CatalogCommandTimeoutSeconds must be between 1 and 3600.");
        Require(OverallTimeoutSeconds is >= 1 and <= 86_400,
            "DatabaseDiscovery:OverallTimeoutSeconds must be between 1 and 86400.");
        Require(MaximumIncludedSchemas is >= 1 and <= 1_024,
            "DatabaseDiscovery:MaximumIncludedSchemas must be between 1 and 1024.");
        Require(MaximumObjects >= 1, "DatabaseDiscovery:MaximumObjects must be positive.");
        Require(MaximumColumns >= 1, "DatabaseDiscovery:MaximumColumns must be positive.");
        Require(MaximumConstraintsAndIndexes >= 1,
            "DatabaseDiscovery:MaximumConstraintsAndIndexes must be positive.");
        Require(MaximumSequences >= 1, "DatabaseDiscovery:MaximumSequences must be positive.");
        Require(MaximumCanonicalSnapshotBytes is >= 1_024 and <= 536_870_912,
            "DatabaseDiscovery:MaximumCanonicalSnapshotBytes must be between 1024 and 536870912.");
        Require(MaximumSyncPlanActions is >= 1 and <= 10_000,
            "DatabaseDiscovery:MaximumSyncPlanActions must be between 1 and 10000.");
        Require(LeaseDurationSeconds is >= 2 and <= 3_600,
            "DatabaseDiscovery:LeaseDurationSeconds must be between 2 and 3600.");
        Require(HeartbeatIntervalSeconds >= 1 && HeartbeatIntervalSeconds < LeaseDurationSeconds,
            "DatabaseDiscovery:HeartbeatIntervalSeconds must be positive and shorter than the lease duration.");
        Require(QueuePollIntervalMilliseconds is >= 25 and <= 60_000,
            "DatabaseDiscovery:QueuePollIntervalMilliseconds must be between 25 and 60000.");
        Require(QueuePollIntervalMilliseconds != HeartbeatIntervalSeconds * 1_000,
            "DatabaseDiscovery queue polling and heartbeat intervals must differ.");
    }

    private static void Require(bool condition, string error)
    {
        if (!condition) throw new InvalidOperationException(error);
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
