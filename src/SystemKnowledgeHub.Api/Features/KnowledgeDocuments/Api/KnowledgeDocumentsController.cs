using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Api.Contracts;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.Traceability.Application;
using SystemKnowledgeHub.Api.Features.Traceability.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Api;

[ApiController]
[Route("api/knowledge-documents")]
public sealed class KnowledgeDocumentsController(
    KnowledgeDocumentQueries queries,
    TraceabilityQueries traceabilityQueries,
    ImpactQueries impactQueries,
    KnowledgeDocumentService service,
    KnowledgeDocumentDeleteService deleteService,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteKnowledgeDocument(long id, [FromBody] DeleteKnowledgeDocumentRequest request, CancellationToken cancellationToken)
    {
        var actor = await CurrentUserApiResolution.ResolveSoftDeleteActor(currentUserContext, cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await deleteService.DeleteKnowledgeDocument(id, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        return result.Failure switch
        {
            SoftDeleteFailure.None => NoContent(),
            SoftDeleteFailure.Validation => BadRequest(SoftDeleteApiResponses.Validation(result.FieldErrors!)),
            SoftDeleteFailure.NotFound => NotFound(SoftDeleteApiResponses.NotFound("KnowledgeDocument", id)),
            SoftDeleteFailure.Forbidden => StatusCode(StatusCodes.Status403Forbidden, SoftDeleteApiResponses.Forbidden("KnowledgeDocument", id)),
            SoftDeleteFailure.Conflict => Conflict(SoftDeleteApiResponses.Conflict("KnowledgeDocument", id)),
            SoftDeleteFailure.Dependencies => UnprocessableEntity(SoftDeleteApiResponses.Dependencies("KnowledgeDocument", id, result.Blockers!)),
            _ => throw new InvalidOperationException("Unsupported KnowledgeDocument delete result."),
        };
    }

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

    [HttpGet("{id}/traceability")]
    public async Task<ActionResult<ITraceabilityResponse>> GetTraceability(
        string id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var knowledgeDocumentId))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await traceabilityQueries.Get(knowledgeDocumentId, cancellationToken);
        return result.Failure switch
        {
            TraceabilityQueryFailure.None => Ok(result.Response),
            TraceabilityQueryFailure.NotFound => NotFound(NotFound(knowledgeDocumentId)),
            TraceabilityQueryFailure.UnsupportedDocumentType => UnprocessableEntity(new ApiErrorResponse(
                "business_rule_violation",
                "只有需求、规格说明和测试用例支持可追溯性读取。",
                null,
                new { resourceType = "KnowledgeDocument", resourceId = knowledgeDocumentId })),
            TraceabilityQueryFailure.ReferenceInvalid => UnprocessableEntity(new ApiErrorResponse(
                "reference_invalid",
                "可追溯性关系包含缺失或不符合契约的端点。",
                null,
                null)),
            _ => throw new InvalidOperationException("Unsupported traceability query result."),
        };
    }

    [HttpGet("{id}/traceability/impact")]
    public async Task<ActionResult<ImpactResponse>> GetImpact(
        string id,
        [FromQuery] string? page,
        [FromQuery] string? pageSize,
        CancellationToken cancellationToken)
    {
        var fieldErrors = new Dictionary<string, string[]>();
        var unsupportedQueryKeys = Request.Query.Keys
            .Where(key => !key.Equals("page", StringComparison.OrdinalIgnoreCase)
                && !key.Equals("pageSize", StringComparison.OrdinalIgnoreCase))
            .OrderBy(key => key, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (unsupportedQueryKeys.Length > 0)
        {
            fieldErrors["query"] =
            [
                $"影响上下文只接受 page 和 pageSize；不支持：{string.Join("、", unsupportedQueryKeys)}。",
            ];
        }
        if (!ApiIdParser.TryParse(id, out var knowledgeDocumentId))
        {
            fieldErrors["id"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"];
        }
        var parsedPage = 1L;
        if (page is not null && !ApiIdParser.TryParse(page, out parsedPage))
        {
            fieldErrors["page"] = ["页码必须是 JavaScript 安全范围内的正整数。"];
        }
        var parsedPageSize = ImpactQueries.DefaultPageSize;
        if (pageSize is not null)
        {
            if (!ApiIdParser.TryParse(pageSize, out var parsedPageSizeValue)
                || parsedPageSizeValue > ImpactQueries.MaximumPageSize)
            {
                fieldErrors["pageSize"] = ["每页数量必须是 1 到 100 之间的整数。"];
            }
            else
            {
                parsedPageSize = (int)parsedPageSizeValue;
            }
        }
        if (fieldErrors.Count > 0)
        {
            return BadRequest(ValidationError(fieldErrors));
        }

        var result = await impactQueries.Get(
            knowledgeDocumentId,
            parsedPage,
            parsedPageSize,
            cancellationToken);
        return result.Failure switch
        {
            ImpactQueryFailure.None => Ok(result.Response),
            ImpactQueryFailure.NotFound => NotFound(NotFound(knowledgeDocumentId)),
            ImpactQueryFailure.UnsupportedDocumentType => UnprocessableEntity(new ApiErrorResponse(
                "business_rule_violation",
                "只有需求、规格说明和测试用例支持影响上下文读取。",
                null,
                new { resourceType = "KnowledgeDocument", resourceId = knowledgeDocumentId })),
            ImpactQueryFailure.ReferenceInvalid => UnprocessableEntity(new ApiErrorResponse(
                "reference_invalid",
                "影响上下文关系包含缺失或不符合契约的端点。",
                null,
                null)),
            _ => throw new InvalidOperationException("Unsupported Impact query result."),
        };
    }

    [HttpGet("{id:long}/revisions")]
    public async Task<ActionResult<KnowledgeDocumentRevisionListResponse>> GetRevisions(
        long id,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await queries.GetRevisions(id, page, pageSize, cancellationToken);
        if (result.FieldErrors is not null) return BadRequest(ValidationError(result.FieldErrors));
        return result.DocumentExists ? Ok(result.Response) : NotFound(NotFound(id));
    }

    [HttpGet("{id:long}/revisions/{revisionNumber:long}")]
    public async Task<ActionResult<KnowledgeDocumentRevisionDetailResponse>> GetRevisionDetail(
        long id,
        long revisionNumber,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id))
        {
            errors["id"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"];
        }
        if (!ApiIdParser.IsSafePositive(revisionNumber))
        {
            errors["revisionNumber"] = ["修订号必须是 JavaScript 安全范围内的正整数。"];
        }
        if (errors.Count > 0) return BadRequest(ValidationError(errors));

        var response = await queries.GetRevisionDetail(id, revisionNumber, cancellationToken);
        return response is null
            ? NotFound(RevisionNotFound(id, revisionNumber))
            : Ok(response);
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("{id:long}/revisions/{revisionNumber:long}/restore")]
    public async Task<ActionResult<KnowledgeDocumentDetailResponse>> RestoreRevision(
        long id,
        long revisionNumber,
        [FromBody] RestoreKnowledgeDocumentRevisionRequest request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id))
        {
            errors["id"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"];
        }
        if (!ApiIdParser.IsSafePositive(revisionNumber))
        {
            errors["revisionNumber"] = ["修订号必须是 JavaScript 安全范围内的正整数。"];
        }
        if (errors.Count > 0) return BadRequest(ValidationError(errors));

        var author = await ResolveAuthor(cancellationToken);
        if (author.Result is not null) return author.Result;
        var result = await service.RestoreRevision(new RestoreKnowledgeDocumentRevisionCommand(
            id,
            revisionNumber,
            request.ConcurrencyToken ?? string.Empty,
            request.Reason,
            author.Author!), cancellationToken);
        return result.Failure switch
        {
            KnowledgeDocumentWriteFailure.None => Ok(result.Response),
            KnowledgeDocumentWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            KnowledgeDocumentWriteFailure.NotFound => NotFound(RevisionNotFound(id, revisionNumber)),
            KnowledgeDocumentWriteFailure.Conflict => Conflict(new ApiErrorResponse(
                "conflict",
                "当前文档已被其他操作修改，请重新加载最新内容后再重试恢复。",
                null,
                new { resourceType = "KnowledgeDocument", resourceId = id })),
            KnowledgeDocumentWriteFailure.InvalidState => Conflict(new ApiErrorResponse(
                "invalid_state",
                "当前文档不处于草稿状态，无法恢复历史内容。",
                null,
                new { resourceType = "KnowledgeDocument", resourceId = id })),
            KnowledgeDocumentWriteFailure.BusinessRuleViolation => UnprocessableEntity(new ApiErrorResponse(
                "business_rule_violation",
                "所选修订不能恢复：请选择早于当前版本且内容不同的历史修订。",
                null,
                new { resourceType = "KnowledgeDocumentRevision", knowledgeDocumentId = id, revisionNumber })),
            _ => throw new InvalidOperationException("Unsupported KnowledgeDocument restore result."),
        };
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
        var result = await service.UpdateContent(new UpdateKnowledgeDocumentContentCommand(id, request.Title ?? string.Empty, request.Summary, request.BodyMarkdown, request.ChangeSummary, request.ConcurrencyToken ?? string.Empty, author.Author!), cancellationToken);
        return result.Failure switch
        {
            KnowledgeDocumentWriteFailure.None => Ok(result.Response),
            KnowledgeDocumentWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            KnowledgeDocumentWriteFailure.NotFound => NotFound(NotFound(id)),
            KnowledgeDocumentWriteFailure.Conflict => Conflict(new ApiErrorResponse("conflict", "内容已被其他操作修改，请刷新后重试。", null, new { resourceType = "KnowledgeDocument", resourceId = id })),
            KnowledgeDocumentWriteFailure.InvalidState => Conflict(new ApiErrorResponse("invalid_state", "已归档文档不允许修改内容。", null, new { resourceType = "KnowledgeDocument", resourceId = id, lifecycleStatus = "Archived" })),
            _ => throw new InvalidOperationException("Unsupported KnowledgeDocument content result."),
        };
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPut("{id:long}/lifecycle")]
    public async Task<ActionResult<KnowledgeDocumentDetailResponse>> UpdateLifecycle(
        long id,
        [FromBody] UpdateKnowledgeDocumentLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return BadRequest(ValidationError(new Dictionary<string, string[]> { ["id"] = ["文档 ID 必须是 JavaScript 安全范围内的正整数。"] }));
        var author = await ResolveAuthor(cancellationToken);
        if (author.Result is not null) return author.Result;
        var result = await service.UpdateLifecycle(new UpdateKnowledgeDocumentLifecycleCommand(id, request.TargetLifecycleStatus ?? string.Empty, request.ConcurrencyToken ?? string.Empty, author.Author!), cancellationToken);
        return result.Failure switch
        {
            KnowledgeDocumentWriteFailure.None => Ok(result.Response),
            KnowledgeDocumentWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            KnowledgeDocumentWriteFailure.NotFound => NotFound(NotFound(id)),
            KnowledgeDocumentWriteFailure.Conflict => Conflict(new ApiErrorResponse("conflict", "文档已被其他操作修改，请刷新后重试。", null, new { resourceType = "KnowledgeDocument", resourceId = id })),
            _ => throw new InvalidOperationException("Unsupported KnowledgeDocument lifecycle result."),
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
    private static ApiErrorResponse RevisionNotFound(long id, long revisionNumber) => new("not_found", "未找到指定知识文档修订。", null, new { resourceType = "KnowledgeDocumentRevision", knowledgeDocumentId = id, revisionNumber });
    private static ApiErrorResponse CurrentUserError(string code, string message, string authStatus) => new(code, message, null, new { authStatus });
}
