using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Evidence.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Evidence.Application;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Evidence.Api;

/// <summary>提供 Evidence 详情、普通 Evidence 写入、C24 correction 与 C25 HumanConfirmation API。</summary>
/// <remarks>读取需要 Viewer；所有写入（含 HumanConfirmation）要求 Editor 或 Administrator，错误使用 <see cref="ApiErrorResponse"/>。</remarks>
[ApiController]
[Route("api/evidence")]
public sealed class EvidenceController(
    EvidenceQueries queries,
    EvidenceService service,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    /// <summary>返回指定现有知识对象的 Evidence 摘要，包括普通依据与 HumanConfirmation。</summary>
    /// <remarks>读取需要 Viewer。该 API 只展示支持依据；Evidence 的创建和读取不会自动推进 KnowledgeStatus。</remarks>
    /// <param name="subjectType">受控 Evidence Subject 类型。</param>
    /// <param name="subjectId">Subject 的 JavaScript 安全正整数标识符。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回 <c>200</c> Evidence 集合，或当前实现的 <c>400</c>、<c>404</c> API 结果。</returns>
    [HttpGet]
    [ProducesResponseType<EvidenceListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<EvidenceListResponse>> GetEvidenceList(
        [FromQuery] string? subjectType,
        [FromQuery] long subjectId,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetEvidenceList(subjectType, subjectId, cancellationToken);
        return result.Failure switch
        {
            EvidenceFailure.None => Ok(result.Response),
            EvidenceFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            EvidenceFailure.SubjectNotFound => NotFound(ReferenceInvalidError()),
            _ => throw new InvalidOperationException("Unsupported Evidence list result."),
        };
    }

    /// <summary>返回一条 Evidence 的可读详情投影。</summary>
    /// <param name="id">JavaScript 安全范围内的 Evidence 标识符。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回 <c>200</c> 详情，或当前实现的 <c>400</c>、<c>404</c>、<c>422</c> API 结果。</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType<EvidenceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<EvidenceDetailResponse>> GetEvidenceDetail(
        long id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["证据 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await queries.GetEvidenceDetail(id, cancellationToken);
        return result.Failure switch
        {
            EvidenceFailure.None => Ok(result.Response),
            EvidenceFailure.NotFound => NotFound(NotFoundError(id)),
            EvidenceFailure.SubjectNotFound => UnprocessableEntity(ReferenceInvalidError()),
            _ => throw new InvalidOperationException("Unsupported Evidence detail result."),
        };
    }

    /// <summary>创建普通 Evidence；HumanConfirmation 必须使用专用端点。</summary>
    /// <remarks>普通 Evidence 的 Provider Snapshot 来自请求，保存本身不自动推进 KnowledgeStatus。</remarks>
    /// <param name="request">普通 Evidence 来源、支持理由和 Provider Snapshot。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回 <c>201</c> 创建结果，或当前实现的验证与 Subject 引用错误结果。</returns>
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost]
    [ProducesResponseType<AddEvidenceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddEvidenceResponse>> AddEvidence(
        [FromBody] AddEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.AddEvidence(
            new AddEvidenceCommand(
                request.EvidenceType ?? string.Empty,
                ToTarget(request.Subject),
                request.SubjectDetailKey,
                request.SourceTitle ?? string.Empty,
                request.SourceReference,
                request.SourceLocator,
                request.Summary,
                request.SupportReason ?? string.Empty,
                request.Confidence,
                ToPerson(request.Provider)),
            cancellationToken);

        return result.Failure switch
        {
            EvidenceFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            EvidenceFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            EvidenceFailure.SubjectNotFound => UnprocessableEntity(ReferenceInvalidError()),
            _ => throw new InvalidOperationException("Unsupported Add Evidence result."),
        };
    }

    /// <summary>按 opaque concurrencyToken 显式纠正既有 Evidence。</summary>
    /// <param name="id">JavaScript 安全范围内的 Evidence 标识符。</param>
    /// <param name="request">包含更新内容与 opaque concurrencyToken 的 correction 请求。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回 <c>200</c> 详情；过期令牌返回 <c>409</c>，其余结果遵循当前 API contract。</returns>
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}")]
    [ProducesResponseType<EvidenceDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<EvidenceDetailResponse>> UpdateEvidence(
        long id,
        [FromBody] UpdateEvidenceRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["证据 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await service.UpdateEvidence(
            new UpdateEvidenceCommand(
                id,
                request.SourceTitle ?? string.Empty,
                request.SourceReference,
                request.SourceLocator,
                request.Summary,
                request.SupportReason ?? string.Empty,
                request.Confidence,
                ToPerson(request.Provider),
                request.Actor is null
                    ? null
                    : new EvidenceActorCommand(request.Actor.DisplayName ?? string.Empty, request.Actor.Role),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);

        return result.Failure switch
        {
            EvidenceFailure.None => Ok(result.Response),
            EvidenceFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            EvidenceFailure.NotFound => NotFound(NotFoundError(id)),
            EvidenceFailure.SubjectNotFound => UnprocessableEntity(ReferenceInvalidError()),
            EvidenceFailure.Conflict => Conflict(new ApiErrorResponse(
                "conflict",
                "证据已被其他操作修改，请重新加载后重试。",
                null,
                new { resourceType = "Evidence", resourceId = id })),
            _ => throw new InvalidOperationException("Unsupported Update Evidence result."),
        };
    }

    /// <summary>使用 authenticated principal-backed Current User 创建 HumanConfirmation Evidence。</summary>
    /// <remarks>
    /// 客户端只提交确认事实；服务器解析 canonical User 和本次 KnowledgeRole 并生成 Snapshot。浏览器提供的其他 User ID
    /// 不能覆盖确认人身份，且创建成功时 <c>knowledgeStatusChanged</c> 保持 false。
    /// </remarks>
    /// <param name="request">确认事实以及可选 KnowledgeRole 选择。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回 <c>201</c> 创建结果，或当前实现的 <c>400</c>、<c>403</c>、<c>404</c>、<c>422</c> API 结果。</returns>
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("human-confirmations")]
    [ProducesResponseType<AddEvidenceResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AddEvidenceResponse>> AddHumanConfirmation(
        [FromBody] AddHumanConfirmationRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await currentUserContext.ResolveAsync(cancellationToken);
        if (currentUser.Status != CurrentUserResolutionStatus.Available)
        {
            return CurrentUserError(currentUser.Status);
        }

        var result = await service.AddHumanConfirmation(
            new AddHumanConfirmationCommand(
                currentUser.CurrentUser!.Id,
                ToTarget(request.Subject),
                request.SubjectRevisionNumber,
                request.SubjectDetailKey,
                request.KnowledgeRoleId,
                request.ConfirmationMethod ?? string.Empty,
                request.ConfirmedAt,
                request.ConfirmationStatement ?? string.Empty,
                request.SupportReason ?? string.Empty,
                request.SourceNote),
            cancellationToken);

        return result.Failure switch
        {
            EvidenceFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            EvidenceFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            EvidenceFailure.SubjectNotFound => UnprocessableEntity(ReferenceInvalidError()),
            EvidenceFailure.CurrentUserNotFound => NotFound(CurrentUserErrorResponse(
                "identity_unmapped",
                "当前登录身份尚未绑定系统用户。",
                "unmapped")),
            EvidenceFailure.CurrentUserInactive => StatusCode(StatusCodes.Status403Forbidden, CurrentUserErrorResponse(
                "account_inactive",
                "当前用户已停用。",
                "inactive")),
            EvidenceFailure.KnowledgeRoleNotFound => UnprocessableEntity(KnowledgeRoleError(
                "reference_invalid",
                "指定的知识身份不存在，请刷新后重新选择。",
                request.KnowledgeRoleId)),
            EvidenceFailure.KnowledgeRoleInactive => UnprocessableEntity(KnowledgeRoleError(
                "invalid_state",
                "指定的知识身份已停用，请刷新后重新选择。",
                request.KnowledgeRoleId)),
            EvidenceFailure.KnowledgeRoleNotAssigned => UnprocessableEntity(KnowledgeRoleError(
                "reference_invalid",
                "指定的知识身份未分配给当前操作者，请刷新后重新选择。",
                request.KnowledgeRoleId)),
            EvidenceFailure.Conflict => Conflict(new ApiErrorResponse(
                "conflict",
                "文档内容已产生新修订，请重新加载后再确认。",
                null,
                new { resourceType = "KnowledgeDocument", resourceId = request.Subject?.Id })),
            _ => throw new InvalidOperationException("Unsupported Add Human Confirmation result."),
        };
    }

    private ActionResult<AddEvidenceResponse> CurrentUserError(CurrentUserResolutionStatus status)
    {
        return status switch
        {
            CurrentUserResolutionStatus.Unauthenticated => Unauthorized(CurrentUserErrorResponse(
                "unauthenticated",
                "尚未登录。",
                "missing")),
            CurrentUserResolutionStatus.SessionExpired => Unauthorized(CurrentUserErrorResponse(
                "session_expired",
                "登录会话已失效，请重新认证。",
                "expired")),
            CurrentUserResolutionStatus.IdentityUnmapped => StatusCode(StatusCodes.Status403Forbidden, CurrentUserErrorResponse(
                "identity_unmapped",
                "当前登录身份尚未绑定系统用户。",
                "unmapped")),
            CurrentUserResolutionStatus.IdentityInactive => StatusCode(StatusCodes.Status403Forbidden, CurrentUserErrorResponse(
                "identity_inactive",
                "当前登录身份已停用。",
                "identity_inactive")),
            CurrentUserResolutionStatus.AccountInactive => StatusCode(StatusCodes.Status403Forbidden, CurrentUserErrorResponse(
                "account_inactive",
                "当前用户已停用。",
                "inactive")),
            _ => throw new InvalidOperationException("Unsupported Current User resolution."),
        };
    }

    private static EvidenceTargetCommand? ToTarget(EvidenceTargetRequest? target)
    {
        return target is null ? null : new EvidenceTargetCommand(target.Type ?? string.Empty, target.Id);
    }

    private static PersonSnapshotCommand? ToPerson(PersonSnapshotRequest? person)
    {
        return person is null
            ? null
            : new PersonSnapshotCommand(
                person.DisplayName ?? string.Empty,
                person.RoleOrIdentity ?? string.Empty,
                person.OccurredAt ?? default,
                person.Team,
                person.ExternalUserKey,
                person.Source,
                person.Note);
    }

    private static ApiErrorResponse NotFoundError(long id)
    {
        return new ApiErrorResponse(
            "not_found",
            "未找到指定证据。",
            null,
            new { resourceType = "Evidence", resourceId = id });
    }

    private static ApiErrorResponse ReferenceInvalidError()
    {
        return new ApiErrorResponse(
            "reference_invalid",
            "证据关联的知识对象不存在或当前尚不可用。",
            null,
            null);
    }

    private static ApiErrorResponse CurrentUserErrorResponse(string code, string message, string authStatus) => new(
        code,
        message,
        null,
        new { authStatus });

    private static ApiErrorResponse KnowledgeRoleError(string code, string message, long? roleId) => new(
        code,
        message,
        null,
        new { resourceType = "KnowledgeRole", resourceId = roleId });

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        return new ApiErrorResponse("validation_error", "请求内容无效。", fieldErrors, null);
    }
}
