using SystemKnowledgeHub.Api.Features.Portal.Domain;

namespace SystemKnowledgeHub.Api.Features.Portal.Application.Models;

public sealed record AdminPortalTargetReferenceRequest(PortalTargetType Type, long Id);

public sealed record AdminPortalTargetSummaryResponse(
    PortalTargetType Type,
    long Id,
    string Title,
    string? Context,
    string Status,
    string? DocumentType,
    string? Lifecycle);

public sealed record AdminPortalPageListResponse(
    IReadOnlyList<AdminPortalPageListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record AdminPortalPageListItemResponse(
    long Id,
    string Title,
    AdminPortalTargetSummaryResponse PrimaryTarget,
    bool IsPublished,
    string PublicationLabel,
    AdminPortalHealthResponse ReferenceHealth,
    int NodePlacementCount,
    DateTimeOffset UpdatedAt,
    string ConcurrencyToken);

public sealed record AdminPortalPageDetailResponse(
    long Id,
    string Title,
    AdminPortalTargetSummaryResponse PrimaryTarget,
    bool IsPublished,
    string PublicationLabel,
    IReadOnlyList<AdminPortalSectionResponse> Sections,
    IReadOnlyList<AdminPortalPlacementResponse> Placements,
    AdminPortalHealthResponse ReferenceHealth,
    DateTimeOffset UpdatedAt,
    string ConcurrencyToken);

public sealed record AdminPortalSectionResponse(
    long Id,
    string Heading,
    PortalPageSectionSourceKind SourceKind,
    AdminPortalTargetSummaryResponse? ReferenceTarget,
    PortalPageProjectionKind ProjectionKind,
    int SortOrder,
    bool IsHealthy,
    string HealthMessage);

public sealed record AdminPortalPlacementResponse(
    long NodeId,
    string Path,
    bool IsPublished,
    bool IsEffectivelyPublished);

public sealed record AdminPortalHealthResponse(string Code, string Message, bool IsHealthy);

public sealed record AdminPortalReadinessResponse(
    bool CanPublish,
    IReadOnlyList<AdminPortalReadinessItemResponse> Checks,
    IReadOnlyList<AdminPortalReadinessItemResponse> Blockers,
    IReadOnlyList<AdminPortalReadinessItemResponse> Warnings);

public sealed record AdminPortalReadinessItemResponse(string Code, string Message);

public sealed record AdminPortalPreviewResponse(
    PortalPageResponse? Page,
    AdminPortalReadinessResponse Readiness);

public sealed record AdminPortalTreeResponse(IReadOnlyList<AdminPortalTreeNodeResponse> Items, int Total);

public sealed record AdminPortalTreeNodeResponse(
    long NodeId,
    long? ParentNodeId,
    string Title,
    PortalPageNodeKind NodeKind,
    long? PageId,
    string? PageTitle,
    bool IsPublished,
    bool IsEffectivelyPublished,
    AdminPortalHealthResponse Health,
    string ConcurrencyToken);

public sealed record AdminPortalTargetListResponse(
    IReadOnlyList<AdminPortalTargetSummaryResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record CreateAdminPortalPageRequest(
    string? Title,
    AdminPortalTargetReferenceRequest? PrimaryTarget);

public sealed record UpdateAdminPortalPageRequest(
    string? Title,
    AdminPortalTargetReferenceRequest? PrimaryTarget,
    IReadOnlyList<AdminPortalSectionRequest>? Sections,
    string? ConcurrencyToken);

public sealed record AdminPortalSectionRequest(
    long? Id,
    string? Heading,
    PortalPageSectionSourceKind SourceKind,
    AdminPortalTargetReferenceRequest? ReferenceTarget,
    PortalPageProjectionKind ProjectionKind,
    int SortOrder);

public sealed record AdminPortalConcurrencyRequest(string? ConcurrencyToken);

public sealed record CreateAdminPortalNodeRequest(
    string? Title,
    PortalPageNodeKind NodeKind,
    long? ParentId,
    long? PortalPageId,
    int SortOrder);

public sealed record UpdateAdminPortalNodeRequest(
    string? Title,
    PortalPageNodeKind NodeKind,
    long? ParentId,
    long? PortalPageId,
    int SortOrder,
    string? ConcurrencyToken);

public sealed record ReorderAdminPortalNodesRequest(
    long? ParentId,
    IReadOnlyList<ReorderAdminPortalNodeItemRequest>? Items);

public sealed record ReorderAdminPortalNodeItemRequest(long Id, string? ConcurrencyToken);

public sealed record PortalCommandActor(long UserId, string DisplayName);

public enum AdminPortalFailure
{
    None,
    Validation,
    NotFound,
    Conflict,
    InvalidState,
    ReferenceInvalid,
    LimitExceeded,
}

public sealed record AdminPortalCommandResult<TResponse>(
    AdminPortalFailure Failure,
    TResponse? Response = default,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    string? Message = null,
    AdminPortalReadinessResponse? Readiness = null);

public sealed record AdminPortalQueryResult<TResponse>(
    TResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors = null,
    AdminPortalFailure Failure = AdminPortalFailure.None);
