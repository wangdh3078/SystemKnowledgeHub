namespace SystemKnowledgeHub.Api.Features.Attachments.Application.Models;

public sealed record AttachmentMetadataResponse(
    long AttachmentId,
    string Kind,
    string OriginalFileName,
    string Extension,
    string ContentType,
    long SizeBytes,
    string Sha256,
    string PreviewMode,
    bool CanPreview,
    bool CanDownload,
    string? ConcurrencyToken = null,
    int? ReferenceCount = null);

public sealed record AttachmentTextPreviewResponse(
    AttachmentMetadataResponse Attachment,
    string Mode,
    string Text,
    bool Truncated,
    int ReturnedBytes,
    int MaximumBytes);

public sealed record AttachmentCsvPreviewResponse(
    AttachmentMetadataResponse Attachment,
    string Mode,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    bool Truncated,
    IReadOnlyList<string> TruncationReasons,
    int MaximumRows,
    int MaximumColumns,
    int MaximumCharacters);

public sealed record AttachmentSpreadsheetSheetResponse(
    string Name,
    string Visibility);

public sealed record AttachmentSpreadsheetRowResponse(
    int RowNumber,
    IReadOnlyList<string> Cells);

public sealed record AttachmentSpreadsheetPreviewResponse(
    AttachmentMetadataResponse Attachment,
    string Mode,
    IReadOnlyList<AttachmentSpreadsheetSheetResponse> Sheets,
    string SelectedSheet,
    IReadOnlyList<AttachmentSpreadsheetRowResponse> Rows,
    bool Truncated,
    IReadOnlyList<string> TruncationReasons,
    int MaximumSheets,
    int MaximumRows,
    int MaximumColumns);

public sealed record AttachmentUploadResult(
    AttachmentMetadataResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    AttachmentFailure Failure);

public sealed record AttachmentContentResult(
    AttachmentContent? Content,
    AttachmentFailure Failure);

public sealed record AttachmentPreviewResult(
    object? Response,
    AttachmentContent? InlineContent,
    AttachmentFailure Failure);

public sealed record AttachmentContent(
    Stream Stream,
    string ContentType,
    string OriginalFileName,
    long SizeBytes);

public sealed record AdministratorAttachmentResult(
    AttachmentMetadataResponse? Response,
    AttachmentFailure Failure);

public enum AttachmentFailure
{
    None,
    Validation,
    NotFound,
    InvalidState,
    Conflict,
    PayloadTooLarge,
    UnsupportedMediaType,
    PreviewNotSupported,
    PreviewLimitExceeded,
    StorageUnavailable,
    Referenced,
}
