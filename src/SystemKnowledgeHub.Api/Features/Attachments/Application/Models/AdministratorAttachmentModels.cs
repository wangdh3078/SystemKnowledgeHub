namespace SystemKnowledgeHub.Api.Features.Attachments.Application.Models;

public sealed record AdministratorAttachmentListQuery(
    string? Query,
    string? Kind,
    string? Extension,
    string? ReferenceStatus,
    string? StorageState,
    int? Page,
    int? PageSize);

public sealed record AdministratorAttachmentOwnerResponse(
    long DocumentId,
    string Title,
    string LifecycleStatus,
    bool IsDeleted);

public sealed record AdministratorAttachmentListItemResponse(
    long AttachmentId,
    string OriginalFileName,
    string Extension,
    string Kind,
    string ContentType,
    long SizeBytes,
    string CreatedByDisplayName,
    DateTimeOffset CreatedAt,
    AdministratorAttachmentOwnerResponse Owner,
    int ReferenceCount,
    int CurrentReferenceCount,
    int HistoricalReferenceCount,
    string ReferenceStatus,
    string StorageState,
    string StorageHealth,
    string PreviewMode,
    bool CanPreview,
    string Sha256);

public sealed record AdministratorAttachmentListResponse(
    IReadOnlyList<AdministratorAttachmentListItemResponse> Items,
    int Page,
    int PageSize,
    long Total);

public sealed record AdministratorAttachmentReferenceResponse(
    long RevisionNumber,
    bool IsCurrent,
    DateTimeOffset CreatedAt);

public sealed record AdministratorAttachmentDetailResponse(
    long AttachmentId,
    string OriginalFileName,
    string Extension,
    string Kind,
    string ContentType,
    long SizeBytes,
    string Sha256,
    DateTimeOffset CreatedAt,
    long CreatedByUserId,
    string CreatedByDisplayName,
    string StorageState,
    string StorageHealth,
    string PreviewMode,
    bool CanPreview,
    string ConcurrencyToken,
    AdministratorAttachmentOwnerResponse Owner,
    int ReferenceCount,
    int CurrentReferenceCount,
    int HistoricalReferenceCount,
    string ReferenceStatus,
    IReadOnlyList<AdministratorAttachmentReferenceResponse> References,
    bool ReferencesTruncated);

public sealed record AdministratorAttachmentStatisticItemResponse(
    long AttachmentId,
    string OriginalFileName,
    string Kind,
    long SizeBytes,
    DateTimeOffset CreatedAt);

public sealed record AdministratorAttachmentStatisticsResponse(
    long TotalCount,
    long TotalSizeBytes,
    long ImageCount,
    long ImageSizeBytes,
    long FileCount,
    long FileSizeBytes,
    long OrphanCount,
    long OrphanSizeBytes,
    long ReferencedCount,
    long CurrentReferencedCount,
    long HistoricalOnlyCount,
    long DeletedOwnerCount,
    long ReadyCount,
    long DeletePendingCount,
    int RecentWindowDays,
    long RecentUploadCount,
    IReadOnlyList<AdministratorAttachmentStatisticItemResponse> LargestAttachments,
    IReadOnlyList<AdministratorAttachmentStatisticItemResponse> RecentUploads);

public sealed record AdministratorAttachmentIntegrityResponse(
    long AttachmentId,
    string Status,
    long SizeBytes,
    long? ActualSizeBytes,
    string Sha256,
    string? ActualSha256,
    DateTimeOffset CheckedAt);

public sealed record AdministratorAttachmentListResult(
    AdministratorAttachmentListResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors);

public sealed record AdministratorAttachmentDetailResult(
    AdministratorAttachmentDetailResponse? Response,
    AttachmentFailure Failure);

public sealed record AdministratorAttachmentIntegrityResult(
    AdministratorAttachmentIntegrityResponse? Response,
    AttachmentFailure Failure);
