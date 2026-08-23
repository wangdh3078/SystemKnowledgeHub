namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;

public sealed record KnowledgeDocumentListQuery(
    string? Query,
    string? DocumentType,
    string? LifecycleStatus,
    string? KnowledgeStatus,
    string? Sort,
    int? Page,
    int? PageSize);

public sealed record KnowledgeDocumentListItemResponse(
    long Id,
    string DocumentType,
    string Title,
    string? Summary,
    string LifecycleStatus,
    string KnowledgeStatus,
    string CreatedByDisplayName,
    string UpdatedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record KnowledgeDocumentsListResponse(
    IReadOnlyList<KnowledgeDocumentListItemResponse> Items,
    int Page,
    int PageSize,
    int Total);

public sealed record KnowledgeDocumentListQueryResult(
    KnowledgeDocumentsListResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors);

public sealed record KnowledgeDocumentDetailResponse(
    long Id,
    string DocumentType,
    string Title,
    string? Summary,
    string BodyMarkdown,
    string LifecycleStatus,
    string KnowledgeStatus,
    long CurrentRevisionNumber,
    long? LatestPublishedRevisionNumber,
    KnowledgeDocumentConfirmationCoverageResponse ConfirmationCoverage,
    long CreatedByUserId,
    string CreatedByDisplayName,
    long UpdatedByUserId,
    string UpdatedByDisplayName,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? PublishedAt,
    DateTimeOffset? ArchivedAt,
    string ConcurrencyToken);

public sealed record KnowledgeDocumentConfirmationCoverageResponse(
    string State,
    long? LastConfirmedRevisionNumber);

public sealed record KnowledgeDocumentAuthor(long UserId, string DisplayName);

public sealed record CreateKnowledgeDocumentCommand(
    string DocumentType,
    string Title,
    string? Summary,
    string? BodyMarkdown,
    KnowledgeDocumentAuthor Author);

public sealed record UpdateKnowledgeDocumentContentCommand(
    long KnowledgeDocumentId,
    string Title,
    string? Summary,
    string? BodyMarkdown,
    string? ChangeSummary,
    string ConcurrencyToken,
    KnowledgeDocumentAuthor Author);

public sealed record UpdateKnowledgeDocumentLifecycleCommand(
    long KnowledgeDocumentId,
    string TargetLifecycleStatus,
    string ConcurrencyToken,
    KnowledgeDocumentAuthor Author);

public enum KnowledgeDocumentWriteFailure
{
    None,
    Validation,
    NotFound,
    Conflict,
    InvalidState,
}

public sealed record KnowledgeDocumentWriteResult(
    KnowledgeDocumentDetailResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    KnowledgeDocumentWriteFailure Failure);
