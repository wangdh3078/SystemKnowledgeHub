using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Features.Attachments.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Application;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Portal.Api;

[ApiController]
[AllowAnonymous]
[Route("api/portal/pages/{pageId}/attachments")]
public sealed class PortalAttachmentsController(
    PortalQueries portalQueries,
    AttachmentService attachmentService) : ControllerBase
{
    [HttpGet("{attachmentId}/content")]
    public Task<IActionResult> Content(string pageId, string attachmentId, CancellationToken cancellationToken) =>
        Deliver(pageId, attachmentId, null, true, cancellationToken);

    [HttpGet("{attachmentId}/download")]
    public Task<IActionResult> Download(string pageId, string attachmentId, CancellationToken cancellationToken) =>
        Deliver(pageId, attachmentId, null, false, cancellationToken);

    [HttpGet("{attachmentId}/preview")]
    public Task<IActionResult> Preview(
        string pageId,
        string attachmentId,
        [FromQuery] string? sheet,
        CancellationToken cancellationToken) => Deliver(pageId, attachmentId, sheet, null, cancellationToken);

    private async Task<IActionResult> Deliver(
        string rawPageId,
        string rawAttachmentId,
        string? sheet,
        bool? inline,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(rawPageId, out var pageId) || !ApiIdParser.TryParse(rawAttachmentId, out var attachmentId))
            return BadRequest(Error("validation_error", "页面 ID 与附件 ID 必须是 JavaScript 安全范围内的正整数。"));
        if (sheet is not null && (sheet.Length is < 1 or > 255 || sheet.Any(char.IsControl)))
            return BadRequest(Error("validation_error", "工作表名称无效。"));
        var documentId = await portalQueries.GetAuthorizedAttachmentDocumentIdAsync(pageId, attachmentId, cancellationToken);
        if (documentId is null) return NotFound(Error("not_found", "未找到请求上下文中的附件。"));

        if (inline is not null)
        {
            var result = inline.Value
                ? await attachmentService.GetCurrentContent(documentId.Value, attachmentId, cancellationToken)
                : await attachmentService.DownloadCurrent(documentId.Value, attachmentId, cancellationToken);
            if (result.Failure != AttachmentFailure.None) return Failure(result.Failure);
            SetDeliveryHeaders(result.Content!.OriginalFileName, inline.Value);
            return File(result.Content.Stream, result.Content.ContentType, enableRangeProcessing: false);
        }

        var preview = await attachmentService.PreviewCurrent(documentId.Value, attachmentId, sheet, cancellationToken);
        if (preview.Failure != AttachmentFailure.None) return Failure(preview.Failure);
        if (preview.InlineContent is not null)
        {
            SetDeliveryHeaders(preview.InlineContent.OriginalFileName, true);
            return File(preview.InlineContent.Stream, preview.InlineContent.ContentType, enableRangeProcessing: false);
        }
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return Ok(SanitizePreview(preview.Response!));
    }

    private static object SanitizePreview(object response) => response switch
    {
        AttachmentTextPreviewResponse text => new PortalAttachmentTextPreviewResponse(
            text.Mode, text.Text, text.Truncated, text.ReturnedBytes, text.MaximumBytes),
        AttachmentCsvPreviewResponse csv => new PortalAttachmentCsvPreviewResponse(
            csv.Mode, csv.Rows, csv.Truncated, csv.TruncationReasons,
            csv.MaximumRows, csv.MaximumColumns, csv.MaximumCharacters),
        AttachmentSpreadsheetPreviewResponse spreadsheet => new PortalAttachmentSpreadsheetPreviewResponse(
            spreadsheet.Mode,
            spreadsheet.Sheets.Select(item => item.Name).ToArray(),
            spreadsheet.SelectedSheet,
            spreadsheet.Rows.Select(item => new PortalAttachmentSpreadsheetRowResponse(item.RowNumber, item.Cells)).ToArray(),
            spreadsheet.Truncated,
            spreadsheet.TruncationReasons,
            spreadsheet.MaximumSheets,
            spreadsheet.MaximumRows,
            spreadsheet.MaximumColumns),
        _ => throw new InvalidOperationException("Unsupported Portal attachment preview."),
    };

    private IActionResult Failure(AttachmentFailure failure) => failure switch
    {
        AttachmentFailure.NotFound => NotFound(Error("not_found", "未找到请求上下文中的附件。")),
        AttachmentFailure.Validation => BadRequest(Error("validation_error", "工作表不存在或超过可预览的工作表上限。")),
        AttachmentFailure.PreviewNotSupported => UnprocessableEntity(Error("preview_not_supported", "该附件不支持此预览方式。")),
        AttachmentFailure.PreviewLimitExceeded => UnprocessableEntity(Error("preview_limit_exceeded", "该附件超过安全预览限制，但仍可下载。")),
        AttachmentFailure.StorageUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, Error("attachment_unavailable", "附件内容暂不可用。")),
        _ => throw new InvalidOperationException("Unsupported Portal attachment delivery failure."),
    };

    private void SetDeliveryHeaders(string fileName, bool inline)
    {
        var disposition = new ContentDispositionHeaderValue(inline ? "inline" : "attachment")
        {
            FileName = CreateAsciiFallback(fileName),
            FileNameStar = fileName,
        };
        Response.Headers[HeaderNames.ContentDisposition] = disposition.ToString();
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
    }

    private static string CreateAsciiFallback(string fileName)
    {
        var characters = fileName.Select(character => character is >= (char)0x20 and <= (char)0x7e && character is not '"' and not '\\'
            ? character : '_').ToArray();
        var fallback = new string(characters).Trim();
        return string.IsNullOrEmpty(fallback) ? "attachment" : fallback;
    }

    private static ApiErrorResponse Error(string code, string message) => new(code, message, null, null);
}
