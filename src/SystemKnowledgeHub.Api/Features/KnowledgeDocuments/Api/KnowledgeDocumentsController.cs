using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Api.Contracts;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Api;

[ApiController]
[Route("api/knowledge-documents")]
public sealed class KnowledgeDocumentsController(
    KnowledgeDocumentQueries queries,
    KnowledgeDocumentService service,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<KnowledgeDocumentsListResponse>> GetList(
        [FromQuery] string? query,
        [FromQuery] string? documentType,
        [FromQuery] string? lifecycleStatus,
        [FromQuery] string? knowledgeStatus,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetList(new KnowledgeDocumentListQuery(query, documentType, lifecycleStatus, knowledgeStatus, sort, page, pageSize), cancellationToken);
        return result.FieldErrors is null ? Ok(result.Response) : BadRequest(ValidationError(result.FieldErrors));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<KnowledgeDocumentDetailResponse>> GetDetail(long id, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return BadRequest(ValidationError(new Dictionary<string, string[]> { ["id"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"] }));
        var response = await queries.GetDetail(id, cancellationToken);
        return response is null ? NotFound(NotFound(id)) : Ok(response);
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost]
    public async Task<ActionResult<KnowledgeDocumentDetailResponse>> Create(
        [FromBody] CreateKnowledgeDocumentRequest request,
        CancellationToken cancellationToken)
    {
        var author = await ResolveAuthor(cancellationToken);
        if (author.Result is not null) return author.Result;
        var result = await service.Create(new CreateKnowledgeDocumentCommand(request.DocumentType ?? string.Empty, request.Title ?? string.Empty, request.Summary, request.BodyMarkdown, author.Author!), cancellationToken);
        return result.Failure switch
        {
            KnowledgeDocumentWriteFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            KnowledgeDocumentWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            _ => throw new InvalidOperationException("Unsupported KnowledgeDocument create result."),
        };
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPut("{id:long}/content")]
    public async Task<ActionResult<KnowledgeDocumentDetailResponse>> UpdateContent(
        long id,
        [FromBody] UpdateKnowledgeDocumentContentRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return BadRequest(ValidationError(new Dictionary<string, string[]> { ["id"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"] }));
        var author = await ResolveAuthor(cancellationToken);
        if (author.Result is not null) return author.Result;
        var result = await service.UpdateContent(new UpdateKnowledgeDocumentContentCommand(id, request.Title ?? string.Empty, request.Summary, request.BodyMarkdown, request.ConcurrencyToken ?? string.Empty, author.Author!), cancellationToken);
        return result.Failure switch
        {
            KnowledgeDocumentWriteFailure.None => Ok(result.Response),
            KnowledgeDocumentWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            KnowledgeDocumentWriteFailure.NotFound => NotFound(NotFound(id)),
            KnowledgeDocumentWriteFailure.Conflict => Conflict(new ApiErrorResponse("conflict", "内容已被其他操作修改，请刷新后重试。", null, new { resourceType = "KnowledgeDocument", resourceId = id })),
            _ => throw new InvalidOperationException("Unsupported KnowledgeDocument content result."),
        };
    }

    private async Task<(KnowledgeDocumentAuthor? Author, ActionResult<KnowledgeDocumentDetailResponse>? Result)> ResolveAuthor(CancellationToken cancellationToken)
    {
        var resolution = await currentUserContext.ResolveAsync(cancellationToken);
        if (resolution.Status == CurrentUserResolutionStatus.Available && resolution.CurrentUser is not null)
        {
            return (new KnowledgeDocumentAuthor(resolution.CurrentUser.Id, resolution.CurrentUser.DisplayName), null);
        }
        var error = resolution.Status switch
        {
            CurrentUserResolutionStatus.Unauthenticated => Unauthorized(CurrentUserError("unauthenticated", "尚未登录。", "missing")),
            CurrentUserResolutionStatus.SessionExpired => Unauthorized(CurrentUserError("session_expired", "登录会话已失效，请重新认证。", "expired")),
            CurrentUserResolutionStatus.IdentityUnmapped => StatusCode(StatusCodes.Status403Forbidden, CurrentUserError("identity_unmapped", "当前登录身份尚未绑定系统用户。", "unmapped")),
            CurrentUserResolutionStatus.IdentityInactive => StatusCode(StatusCodes.Status403Forbidden, CurrentUserError("identity_inactive", "当前登录身份已停用。", "identity_inactive")),
            CurrentUserResolutionStatus.AccountInactive => StatusCode(StatusCodes.Status403Forbidden, CurrentUserError("account_inactive", "当前用户已停用。", "inactive")),
            _ => throw new InvalidOperationException("Unsupported Current User resolution."),
        };
        return (null, error);
    }

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors) => new("validation_error", "请求内容无效。", fieldErrors, null);
    private static ApiErrorResponse NotFound(long id) => new("not_found", "未找到指定知识文档。", null, new { resourceType = "KnowledgeDocument", resourceId = id });
    private static ApiErrorResponse CurrentUserError(string code, string message, string authStatus) => new(code, message, null, new { authStatus });
}
