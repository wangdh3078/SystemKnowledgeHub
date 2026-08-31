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
    string DatabaseSourceName,
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

public sealed record DatabaseDiscoveryFilterOptionResponse(long Id, string Name);
public sealed record DatabaseDiscoveryRunFilterOptionsResponse(
    IReadOnlyList<DatabaseDiscoveryFilterOptionResponse> Profiles,
    IReadOnlyList<DatabaseDiscoveryFilterOptionResponse> DatabaseSources);

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

public sealed record DatabaseDiscoverySnapshotSummaryResponse(
    long Id,
    long RunId,
    long ProfileId,
    DateTimeOffset CapturedAt,
    DatabaseProviderType ProviderType,
    string ProviderVersion,
    string CurrentDatabaseOrService,
    string? CurrentContainer,
    int FormatVersion,
    int IdentityAlgorithmVersion,
    long ScopeGenerationId,
    string ScopeFingerprint,
    DatabaseDiscoveryCompleteness Completeness,
    string ContentSha256,
    IReadOnlyList<string> IncludedSchemas,
    IReadOnlyList<CanonicalCapability> Capabilities,
    CanonicalSnapshotCounts Counts);

public sealed record DatabaseDiscoverySchemaResponse(string Name, string LogicalIdentity, int ObjectCount, int SequenceCount);

public sealed record DatabaseDiscoverySchemaPageResponse(
    IReadOnlyList<DatabaseDiscoverySchemaResponse> Items, int Page, int PageSize, int Total);

public sealed record DatabaseDiscoveryObjectSummaryResponse(
    string LogicalIdentity,
    string SchemaName,
    string Name,
    DatabaseDiscoveryObjectType ObjectType,
    string? DatabaseComment,
    int ColumnCount,
    int ConstraintCount,
    int IndexCount);

public sealed record DatabaseDiscoveryObjectPageResponse(
    IReadOnlyList<DatabaseDiscoveryObjectSummaryResponse> Items, int Page, int PageSize, int Total);

public sealed record DatabaseDiscoveryObjectHeaderDataResponse(
    string SchemaName,
    string Name,
    DatabaseDiscoveryObjectType ObjectType,
    string? DatabaseComment,
    string LogicalIdentity);

public sealed record DatabaseDiscoveryObjectHeaderResponse(DatabaseDiscoveryObjectHeaderDataResponse Object);

public sealed record DatabaseDiscoveryNativeDataTypeResponse(string Declaration);

public sealed record DatabaseDiscoveryColumnResponse(
    string Name,
    int? SourceOrdinal,
    DatabaseDiscoveryNativeDataTypeResponse NativeDataType,
    bool IsNullable,
    string? DefaultExpression,
    string? DatabaseComment);

public sealed record DatabaseDiscoveryColumnPageResponse(
    IReadOnlyList<DatabaseDiscoveryColumnResponse> Items, int Page, int PageSize, int Total);

public sealed record DatabaseDiscoveryConstraintResponse(
    DatabaseDiscoveryEntityKind EntityKind,
    string Name,
    IReadOnlyList<string> ColumnNames,
    string? ReferencedObjectName,
    string? UpdateRule,
    string? DeleteRule);

public sealed record DatabaseDiscoveryConstraintPageResponse(
    IReadOnlyList<DatabaseDiscoveryConstraintResponse> Items, int Page, int PageSize, int Total);

public sealed record DatabaseDiscoveryIndexPageResponse(
    IReadOnlyList<DatabaseDiscoveryIndexResponse> Items, int Page, int PageSize, int Total);

public sealed record DatabaseDiscoveryIndexResponse(
    string Name,
    string NativeIndexKind,
    bool IsUnique,
    IReadOnlyList<string> KeyParts,
    IReadOnlyList<string> NonKeyParts,
    string? NativePredicate);

public sealed record DatabaseDiscoveryObjectReviewResponse(
    DatabaseDiscoveryObjectHeaderDataResponse Object,
    DatabaseDiscoveryColumnPageResponse Columns,
    DatabaseDiscoveryConstraintPageResponse Constraints,
    DatabaseDiscoveryIndexPageResponse Indexes);

public sealed record DatabaseDiscoverySequencePageResponse(
    IReadOnlyList<DatabaseDiscoverySequenceResponse> Items, int Page, int PageSize, int Total);

public sealed record DatabaseDiscoverySequenceResponse(
    string SchemaName,
    string Name,
    string NativeDataType,
    string? IncrementValue,
    string? MinimumValue,
    string? MaximumValue,
    int? CacheSize,
    bool? IsCyclic,
    bool? IsOrdered,
    string? StartValue);

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
    string? SchemaName,
    string? ObjectName,
    string? ChildName,
    IReadOnlyList<DatabaseDiscoveryFieldChangeResponse> Changes);

public sealed record DatabaseDiscoveryFieldChangeResponse(
    string Field,
    JsonElement? Before,
    JsonElement? After);

public sealed record DatabaseDiscoveryDifferenceEntryPageResponse(
    IReadOnlyList<DatabaseDiscoveryDifferenceEntryResponse> Items,
    int Page,
    int PageSize,
    int Total);
