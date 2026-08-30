using System.Text.Json;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;

public enum DatabaseDiscoveryFailure
{
    None,
    Validation,
    NotFound,
    ReferenceInvalid,
    Disabled,
    SecretMissing,
    ConcurrencyConflict,
    DiscoveryAlreadyRunning,
    TerminalRun,
}

public sealed record DatabaseDiscoveryOperationResult<T>(
    T? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    DatabaseDiscoveryFailure Failure);

public sealed record DatabaseDiscoveryRunResponse(
    long Id,
    long ProfileId,
    long DatabaseSourceId,
    string ProfileName,
    DatabaseProviderType ProviderType,
    DatabaseDiscoveryRunStatus Status,
    long? BaseSnapshotId,
    long? ScopeGenerationId,
    long? SnapshotId,
    long? DifferenceId,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    DateTimeOffset? CancellationRequestedAt,
    string? ProviderVersion,
    string? ScopeFingerprint,
    IReadOnlyList<CanonicalCapability> Capabilities,
    CanonicalSnapshotCounts? ObjectCounts,
    string? ErrorCode,
    string? ErrorSummary,
    string ConcurrencyToken);

public sealed record DatabaseDiscoveryRunPageResponse(
    IReadOnlyList<DatabaseDiscoveryRunResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DatabaseDiscoverySnapshotResponse(
    long Id,
    long RunId,
    long ProfileId,
    DateTimeOffset CapturedAt,
    int FormatVersion,
    int IdentityAlgorithmVersion,
    long ScopeGenerationId,
    string ScopeFingerprint,
    DatabaseDiscoveryCompleteness Completeness,
    string ContentSha256,
    CanonicalSnapshotCounts Counts,
    JsonElement Content);

public sealed record DatabaseDiscoveryDifferenceResponse(
    long Id,
    long ProfileId,
    long? BaseSnapshotId,
    long TargetSnapshotId,
    long ScopeGenerationId,
    int AlgorithmVersion,
    DateTimeOffset CreatedAt,
    DatabaseDiscoveryDifferenceCounts SummaryCounts,
    string ContentSha256);

public sealed record DatabaseDiscoveryDifferenceEntryResponse(
    long? Id,
    DatabaseDiscoveryEntityKind EntityKind,
    string LogicalIdentity,
    string? ParentLogicalIdentity,
    string DisplayName,
    DatabaseDiscoveryDifferenceState State,
    JsonElement? Before,
    JsonElement? After);

public sealed record DatabaseDiscoveryDifferenceEntryPageResponse(
    IReadOnlyList<DatabaseDiscoveryDifferenceEntryResponse> Items,
    int Page,
    int PageSize,
    int Total);
