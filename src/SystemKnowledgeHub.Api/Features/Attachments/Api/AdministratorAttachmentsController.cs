using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Attachments.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Features.Attachments.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.Attachments.Api;

[ApiController]
[Authorize(Policy = AccessPolicies.Administrator)]
[Route("api/admin/attachments")]
public sealed class AdministratorAttachmentsController(AttachmentService service) : ControllerBase
{
    [HttpGet("{attachmentId:long}")]
    public async Task<ActionResult<AttachmentMetadataResponse>> Get(
        long attachmentId,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(attachmentId)) return BadRequest(ValidationError());
        var result = await service.GetAdministratorMetadata(attachmentId, cancellationToken);
        return result.Failure == AttachmentFailure.None
            ? Ok(result.Response)
            : NotFound(Error("not_found", "未找到指定附件。"));
    }

    [HttpDelete("{attachmentId:long}")]
    public async Task<IActionResult> Delete(
        long attachmentId,
        [FromBody] DeleteAttachmentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(attachmentId)) return BadRequest(ValidationError());
        var failure = await service.DeleteOrphan(
            attachmentId,
            request.ConcurrencyToken ?? string.Empty,
            cancellationToken);
        return failure switch
        {
            AttachmentFailure.None => NoContent(),
            AttachmentFailure.Validation => BadRequest(new ApiErrorResponse(
                "validation_error", "请求内容无效。", new Dictionary<string, string[]> { ["concurrencyToken"] = ["并发标记无效。"] }, null)),
            AttachmentFailure.NotFound => NotFound(Error("not_found", "未找到指定附件。")),
            AttachmentFailure.Conflict => Conflict(Error("conflict", "附件已被其他操作修改，请重新加载后重试。")),
            AttachmentFailure.Referenced => UnprocessableEntity(Error("attachment_referenced", "附件仍被一个或多个修订引用，不能物理删除。")),
            AttachmentFailure.StorageUnavailable => StatusCode(StatusCodes.Status503ServiceUnavailable, Error("attachment_storage_unavailable", "附件存储暂不可用，删除保持待处理状态。")),
            _ => throw new InvalidOperationException("Unsupported administrator attachment delete result."),
        };
    }

    private static ApiErrorResponse ValidationError() => new(
        "validation_error",
        "请求内容无效。",
        new Dictionary<string, string[]> { ["attachmentId"] = ["附件 ID 必须是 JavaScript 安全范围内的正整数。"] },
        null);
    private static ApiErrorResponse Error(string code, string message) => new(code, message, null, null);
}
