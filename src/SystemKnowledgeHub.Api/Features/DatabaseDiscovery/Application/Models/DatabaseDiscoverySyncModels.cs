using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;

public enum DatabaseDiscoverySyncFailure
{
    None, Validation, NotFound, Conflict, StalePlan, LatestSnapshotChanged,
    UnsupportedIdentifierCollision, OrdinalCollision, AlreadyApplied, NotConfirmed,
}

public sealed record DatabaseDiscoveryReconciliationCandidateResponse(
    string Key,
    string Category,
    DatabaseDiscoveryEntityKind EntityKind,
    DatabaseDiscoveryReconciliationStatus Status,
    DatabaseDiscoverySyncActionType? SuggestedAction,
    string? BlockCode,
    string SchemaLogicalIdentity,
    string LogicalIdentity,
    string? ParentLogicalIdentity,
    string SchemaName,
    string ObjectName,
    string? ChildName,
    long? TargetId,
    string? TargetConcurrencyToken,
    string Summary);

public sealed record DatabaseDiscoveryReconciliationPageResponse(
    long ProfileId,
    string ProfileName,
    long DatabaseSourceId,
    string DatabaseSourceName,
    DatabaseProviderType ProviderType,
    long TargetSnapshotId,
    long? TargetDifferenceId,
    long ScopeGenerationId,
    int IdentityAlgorithmVersion,
    IReadOnlyList<DatabaseDiscoveryReconciliationCandidateResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DatabaseDiscoveryReconciliationObjectQueryRequest(
    long ProfileId,
    long? TargetSnapshotId,
    string? Category,
    string? Search,
    int? Page,
    int? PageSize,
    IReadOnlyList<DatabaseDiscoverySyncSelectionRequest>? SelectedActions);

public sealed record DatabaseDiscoveryReconciliationObjectChildrenQueryRequest(
    long ProfileId,
    long TargetSnapshotId,
    string? ObjectLogicalIdentity,
    string? Category,
    string? Search,
    int? Page,
    int? PageSize,
    IReadOnlyList<DatabaseDiscoverySyncSelectionRequest>? SelectedActions);

public sealed record DatabaseDiscoveryReconciliationObjectSelectionRequest(
    long ProfileId,
    long TargetSnapshotId,
    string? ObjectLogicalIdentity,
    bool Selected,
    IReadOnlyList<DatabaseDiscoverySyncSelectionRequest>? CurrentActions);

public sealed record DatabaseDiscoveryReconciliationObjectGroupResponse(
    string Key,
    string SchemaLogicalIdentity,
    string ObjectLogicalIdentity,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    long? TargetId,
    DatabaseDiscoveryReconciliationStatus Status,
    IReadOnlyList<DatabaseDiscoveryReconciliationCandidateResponse> ObjectCandidates,
    DatabaseDiscoverySyncSelectionRequest? RequiredParentAction,
    int TotalColumnCount,
    int SelectableColumnCount,
    int TotalChildCount,
    int SelectableCount,
    int SelectedCount,
    int ConflictCount,
    int UnsupportedCount,
    int NoActionCount,
    string Summary);

public sealed record DatabaseDiscoveryReconciliationObjectGroupPageResponse(
    long ProfileId,
    string ProfileName,
    long DatabaseSourceId,
    string DatabaseSourceName,
    DatabaseProviderType ProviderType,
    long TargetSnapshotId,
    long? TargetDifferenceId,
    long ScopeGenerationId,
    int IdentityAlgorithmVersion,
    int MaximumSyncPlanActions,
    int UngroupedReviewOnlyCount,
    IReadOnlyList<DatabaseDiscoveryReconciliationObjectGroupResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DatabaseDiscoveryReconciliationChildResponse(
    string Key,
    DatabaseDiscoveryEntityKind EntityKind,
    string LogicalIdentity,
    string? Name,
    DatabaseDiscoveryReconciliationStatus Status,
    IReadOnlyList<DatabaseDiscoveryReconciliationCandidateResponse> Candidates,
    int SelectableCount,
    int SelectedCount,
    IReadOnlyList<string> BlockCodes,
    string Summary);

public sealed record DatabaseDiscoveryReconciliationObjectChildrenPageResponse(
    long ProfileId,
    long TargetSnapshotId,
    string ObjectLogicalIdentity,
    IReadOnlyList<DatabaseDiscoveryReconciliationChildResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DatabaseDiscoveryReconciliationObjectSelectionResponse(
    IReadOnlyList<DatabaseDiscoverySyncSelectionRequest> Actions,
    int SelectedCount,
    int MaximumSyncPlanActions,
    int ObjectSelectableCount,
    int ObjectSelectedCount);

public sealed record DatabaseDiscoverySyncSelectionRequest(
    DatabaseDiscoverySyncActionType ActionType,
    string LogicalIdentity,
    long? TargetId);

public sealed record CreateDatabaseDiscoverySyncPlanRequest(
    long ProfileId,
    long TargetSnapshotId,
    IReadOnlyList<DatabaseDiscoverySyncSelectionRequest>? Actions);

public sealed record UpdateDatabaseDiscoverySyncSelectionsRequest(
    IReadOnlyList<DatabaseDiscoverySyncSelectionRequest>? Actions,
    string? ConcurrencyToken);

public sealed record DatabaseDiscoverySyncPlanMutationRequest(string? ConcurrencyToken);
public sealed record ConfirmDatabaseDiscoverySyncPlanRequest(string? PreviewHash, string? ConcurrencyToken);
public sealed record ApplyDatabaseDiscoverySyncPlanRequest(string? PreviewHash, string? ConcurrencyToken);

public sealed record DatabaseDiscoverySyncStructureResponse(
    string? SchemaName,
    string? Name,
    string? ObjectType,
    string? DatabaseComment,
    IReadOnlyList<string>? PrimaryKeyColumns,
    int? OrdinalPosition,
    string? DataType,
    bool? IsNullable,
    string? DefaultValue);

public sealed record DatabaseDiscoverySyncPreviewActionResponse(
    DatabaseDiscoverySyncActionType ActionType,
    DatabaseDiscoveryEntityKind EntityKind,
    string SchemaLogicalIdentity,
    string LogicalIdentity,
    string? ParentLogicalIdentity,
    long? TargetId,
    long? ExpectedTargetVersion,
    long? ExpectedBindingVersion,
    long? ExpectedParentTargetId,
    long? ExpectedParentTargetVersion,
    DatabaseDiscoverySyncStructureResponse? Before,
    DatabaseDiscoverySyncStructureResponse? After,
    string Summary);

public sealed record DatabaseDiscoverySyncPreviewCounts(
    int CreateObjects,
    int LinkObjects,
    int CreateColumns,
    int LinkColumns,
    int UpdateObjects,
    int UpdateColumns,
    int MarkMissing,
    int ClearMissing);

public sealed record DatabaseDiscoverySyncPreviewResponse(
    long PlanId,
    long TargetSnapshotId,
    long ScopeGenerationId,
    string PreviewHash,
    DatabaseDiscoverySyncPreviewCounts Counts,
    IReadOnlyList<DatabaseDiscoverySyncPreviewActionResponse> Actions,
    IReadOnlyList<string> Warnings);

public sealed record DatabaseDiscoverySyncApplyResultResponse(
    int CreatedObjects,
    int LinkedObjects,
    int CreatedColumns,
    int LinkedColumns,
    int UpdatedObjects,
    int UpdatedColumns,
    int MarkedMissing,
    int ClearedMissing,
    DateTimeOffset AppliedAt,
    string AppliedByDisplayName);

public sealed record DatabaseDiscoverySyncPlanResponse(
    long Id,
    long ProfileId,
    string ProfileName,
    long DatabaseSourceId,
    string DatabaseSourceName,
    long ProfileConfigurationRevision,
    long? BaseSnapshotId,
    long TargetSnapshotId,
    long? TargetDifferenceId,
    long ScopeGenerationId,
    int IdentityAlgorithmVersion,
    DatabaseDiscoverySyncPlanStatus Status,
    IReadOnlyList<DatabaseDiscoverySyncSelectionRequest> Actions,
    DatabaseDiscoverySyncPreviewResponse? Preview,
    string? ConfirmedPreviewHash,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? ConfirmedAt,
    DateTimeOffset? AppliedAt,
    DatabaseDiscoverySyncApplyResultResponse? Result,
    string ConcurrencyToken);

public sealed record DatabaseDiscoverySyncPlanPageResponse(
    IReadOnlyList<DatabaseDiscoverySyncPlanResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record DatabaseDiscoverySyncOperationResult<T>(
    T? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    DatabaseDiscoverySyncFailure Failure,
    string? ReasonCode = null);

public sealed record DatabaseDiscoverySyncActor(long UserId, string DisplayName, string? Role);
