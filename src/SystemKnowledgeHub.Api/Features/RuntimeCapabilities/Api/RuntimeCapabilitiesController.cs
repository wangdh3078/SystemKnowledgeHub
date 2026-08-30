using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Attachments.Application;

namespace SystemKnowledgeHub.Api.Features.RuntimeCapabilities.Api;

[ApiController]
[Authorize]
[Route("api/runtime-capabilities")]
public sealed class RuntimeCapabilitiesController(AttachmentOptions attachments) : ControllerBase
{
    [HttpGet("attachments")]
    public ActionResult<AttachmentRuntimeCapabilitiesResponse> GetAttachments() => Ok(new AttachmentRuntimeCapabilitiesResponse(
        attachments.AllowedImageExtensions,
        attachments.AllowedFileExtensions,
        attachments.MaxImageBytes,
        attachments.MaxFileBytes,
        attachments.MaxStoredAttachmentsPerDocument));
}

public sealed record AttachmentRuntimeCapabilitiesResponse(
    IReadOnlyList<string> AllowedImageExtensions,
    IReadOnlyList<string> AllowedFileExtensions,
    long MaxImageBytes,
    long MaxFileBytes,
    int MaxStoredAttachmentsPerDocument);
