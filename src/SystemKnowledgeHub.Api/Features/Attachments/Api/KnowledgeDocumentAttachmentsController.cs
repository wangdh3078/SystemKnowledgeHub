using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Net.Http.Headers;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Features.Attachments.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.Attachments.Api;

[ApiController]
[Authorize]
[Route("api/knowledge-documents/{knowledgeDocumentId:long}")]
public sealed class KnowledgeDocumentAttachmentsController(
    AttachmentService service,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("attachments")]
    [DisableRequestSizeLimit]
    public async Task<ActionResult<AttachmentMetadataResponse>> Upload(
        long knowledgeDocumentId,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(knowledgeDocumentId))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["knowledgeDocumentId"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }
        var actor = await ResolveActor(cancellationToken);
        if (actor.Result is not null) return actor.Result;
        if (!Request.HasFormContentType)
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["file"] = ["请求必须使用 multipart/form-data 并包含一个 file 字段。"],
            }));
        }

        IFormCollection form;
        try
        {
            form = await Request.ReadFormAsync(cancellationToken);
        }
        catch (BadHttpRequestException exception) when (exception.StatusCode == StatusCodes.Status413PayloadTooLarge)
        {
            return StatusCode(
                StatusCodes.Status413PayloadTooLarge,
                Error("payload_too_large", "附件超过配置的大小限制。"));
        }
        catch (Exception exception) when (exception is InvalidDataException or IOException)
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["file"] = ["multipart 请求无效或无法完整读取。"],
            }));
        }

        if (form.Files.Count != 1
            || !string.Equals(form.Files[0].Name, "file", StringComparison.Ordinal)
            || string.IsNullOrWhiteSpace(form.Files[0].FileName))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["file"] = ["multipart 请求必须包含且只能包含一个 file 文件字段。"],
            }));
        }

        var file = form.Files[0];
        await using var content = file.OpenReadStream();
        var result = await service.Upload(
            knowledgeDocumentId,
            file.FileName,
            file.ContentType,
            file.Length,
            content,
            actor.Actor!,
            cancellationToken);
        return result.Failure switch
        {
            AttachmentFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            AttachmentFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            AttachmentFailure.NotFound => NotFound(NotFoundError(knowledgeDocumentId)),
            AttachmentFailure.InvalidState => Conflict(Error("invalid_state", "当前文档不可编辑或已达到附件数量上限。")),
            AttachmentFailure.Conflict => Conflict(Error("conflict", "附件写入与当前状态冲突，请重试。")),
            AttachmentFailure.PayloadTooLarge => StatusCode(StatusCodes.Status413PayloadTooLarge, Error("payload_too_large", "附件超过配置的大小限制。")),
            AttachmentFailure.UnsupportedMediaType => StatusCode(StatusCodes.Status415UnsupportedMediaType, new ApiErrorResponse(
                "unsupported_media_type", "附件类型或内容不符合允许规则。", result.FieldErrors, null)),
            AttachmentFailure.StorageUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, Error("attachment_storage_unavailable", "附件存储暂不可用。")),
            _ => throw new InvalidOperationException("Unsupported attachment upload result."),
        };
    }

    [HttpGet("attachments/{attachmentId:long}/content")]
    public async Task<IActionResult> GetCurrentContent(
        long knowledgeDocumentId,
        long attachmentId,
        CancellationToken cancellationToken) =>
        await DeliverContent(
            ValidateIds(knowledgeDocumentId, attachmentId),
            () => service.GetCurrentContent(knowledgeDocumentId, attachmentId, cancellationToken),
            inline: true);

    [HttpGet("revisions/{revisionNumber:long}/attachments/{attachmentId:long}/content")]
    public async Task<IActionResult> GetHistoricalContent(
        long knowledgeDocumentId,
        long revisionNumber,
        long attachmentId,
        CancellationToken cancellationToken) =>
        await DeliverContent(
            ValidateIds(knowledgeDocumentId, attachmentId, revisionNumber),
            () => service.GetHistoricalContent(knowledgeDocumentId, revisionNumber, attachmentId, cancellationToken),
            inline: true);

    [HttpGet("attachments/{attachmentId:long}/download")]
    public async Task<IActionResult> DownloadCurrent(
        long knowledgeDocumentId,
        long attachmentId,
        CancellationToken cancellationToken) =>
        await DeliverContent(
            ValidateIds(knowledgeDocumentId, attachmentId),
            () => service.DownloadCurrent(knowledgeDocumentId, attachmentId, cancellationToken),
            inline: false);

    [HttpGet("revisions/{revisionNumber:long}/attachments/{attachmentId:long}/download")]
    public async Task<IActionResult> DownloadHistorical(
        long knowledgeDocumentId,
        long revisionNumber,
        long attachmentId,
        CancellationToken cancellationToken) =>
        await DeliverContent(
            ValidateIds(knowledgeDocumentId, attachmentId, revisionNumber),
            () => service.DownloadHistorical(knowledgeDocumentId, revisionNumber, attachmentId, cancellationToken),
            inline: false);

    [HttpGet("attachments/{attachmentId:long}/preview")]
    public async Task<IActionResult> PreviewCurrent(
        long knowledgeDocumentId,
        long attachmentId,
        [FromQuery] string? sheet,
        CancellationToken cancellationToken) =>
        await DeliverPreview(
            ValidateIdsAndSheet(knowledgeDocumentId, attachmentId, null, sheet),
            () => service.PreviewCurrent(knowledgeDocumentId, attachmentId, sheet, cancellationToken));

    [HttpGet("revisions/{revisionNumber:long}/attachments/{attachmentId:long}/preview")]
    public async Task<IActionResult> PreviewHistorical(
        long knowledgeDocumentId,
        long revisionNumber,
        long attachmentId,
        [FromQuery] string? sheet,
        CancellationToken cancellationToken) =>
        await DeliverPreview(
            ValidateIdsAndSheet(knowledgeDocumentId, attachmentId, revisionNumber, sheet),
            () => service.PreviewHistorical(knowledgeDocumentId, revisionNumber, attachmentId, sheet, cancellationToken));

    private async Task<IActionResult> DeliverContent(
        IActionResult? validationResult,
        Func<Task<AttachmentContentResult>> resolve,
        bool inline)
    {
        if (validationResult is not null) return validationResult;
        var result = await resolve();
        if (result.Failure != AttachmentFailure.None)
        {
            return DeliveryFailure(result.Failure);
        }
        SetDeliveryHeaders(result.Content!.OriginalFileName, inline);
        return File(result.Content.Stream, result.Content.ContentType, enableRangeProcessing: false);
    }

    private async Task<IActionResult> DeliverPreview(
        IActionResult? validationResult,
        Func<Task<AttachmentPreviewResult>> resolve)
    {
        if (validationResult is not null) return validationResult;
        var result = await resolve();
        if (result.Failure != AttachmentFailure.None)
        {
            return DeliveryFailure(result.Failure);
        }
        if (result.InlineContent is not null)
        {
            SetDeliveryHeaders(result.InlineContent.OriginalFileName, inline: true);
            return File(result.InlineContent.Stream, result.InlineContent.ContentType, enableRangeProcessing: false);
        }
        Response.Headers[HeaderNames.XContentTypeOptions] = "nosniff";
        Response.Headers[HeaderNames.CacheControl] = "private, no-store";
        return Ok(result.Response);
    }

    private IActionResult DeliveryFailure(AttachmentFailure failure) => failure switch
    {
        AttachmentFailure.Validation => BadRequest(new ApiErrorResponse(
            "validation_error", "请求内容无效。", new Dictionary<string, string[]> { ["sheet"] = ["工作表不存在或超过可预览的工作表上限。"] }, null)),
        AttachmentFailure.NotFound => NotFound(Error("not_found", "未找到请求上下文中的附件。")),
        AttachmentFailure.PreviewNotSupported => UnprocessableEntity(Error("preview_not_supported", "该附件不支持此预览方式。")),
        AttachmentFailure.PreviewLimitExceeded => UnprocessableEntity(Error("preview_limit_exceeded", "该附件超过安全预览限制，但仍可下载。")),
        AttachmentFailure.StorageUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, Error("attachment_unavailable", "附件内容暂不可用。")),
        _ => throw new InvalidOperationException("Unsupported attachment delivery failure."),
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

    private IActionResult? ValidateIds(long documentId, long attachmentId, long? revisionNumber = null)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(documentId)) errors["knowledgeDocumentId"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"];
        if (!ApiIdParser.IsSafePositive(attachmentId)) errors["attachmentId"] = ["附件 ID 必须是 JavaScript 安全范围内的正整数。"];
        if (revisionNumber.HasValue && !ApiIdParser.IsSafePositive(revisionNumber.Value)) errors["revisionNumber"] = ["修订号必须是 JavaScript 安全范围内的正整数。"];
        return errors.Count == 0 ? null : BadRequest(ValidationError(errors));
    }

    private IActionResult? ValidateIdsAndSheet(long documentId, long attachmentId, long? revisionNumber, string? sheet)
    {
        var ids = ValidateIds(documentId, attachmentId, revisionNumber);
        if (ids is not null) return ids;
        if (sheet is not null && (sheet.Length is < 1 or > 255 || sheet.Any(char.IsControl)))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["sheet"] = ["工作表名称无效。"],
            }));
        }
        return null;
    }

    private async Task<(KnowledgeDocumentAuthor? Actor, ActionResult<AttachmentMetadataResponse>? Result)> ResolveActor(
        CancellationToken cancellationToken)
    {
        var resolution = await currentUserContext.ResolveAsync(cancellationToken);
        if (resolution.Status == CurrentUserResolutionStatus.Available && resolution.CurrentUser is not null)
        {
            return (new KnowledgeDocumentAuthor(resolution.CurrentUser.Id, resolution.CurrentUser.DisplayName), null);
        }
        var result = resolution.Status is CurrentUserResolutionStatus.Unauthenticated or CurrentUserResolutionStatus.SessionExpired
            ? Unauthorized(Error("unauthenticated", "尚未登录或会话已失效。"))
            : StatusCode(StatusCodes.Status403Forbidden, Error("forbidden", "当前身份不可执行附件上传。"));
        return (null, result);
    }

    private static string CreateAsciiFallback(string fileName)
    {
        var characters = fileName.Select(character => character is >= (char)0x20 and <= (char)0x7e && character is not '"' and not '\\'
            ? character
            : '_').ToArray();
        var fallback = new string(characters).Trim();
        return string.IsNullOrEmpty(fallback) ? "attachment" : fallback;
    }

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("validation_error", "请求内容无效。", fieldErrors, null);
    private static ApiErrorResponse NotFoundError(long id) =>
        new("not_found", "未找到指定知识文档。", null, new { resourceType = "KnowledgeDocument", resourceId = id });
    private static ApiErrorResponse Error(string code, string message) => new(code, message, null, null);
}
