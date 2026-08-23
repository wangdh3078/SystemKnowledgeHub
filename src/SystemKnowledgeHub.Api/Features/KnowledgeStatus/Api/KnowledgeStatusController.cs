using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.StatusProgression.Api.Contracts;
using SystemKnowledgeHub.Api.Features.StatusProgression.Application;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.StatusProgression.Api;

[ApiController]
[Route("api/knowledge-status")]
public sealed class KnowledgeStatusController(KnowledgeStatusService service, ICurrentUserContext currentUserContext) : ControllerBase
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut]
    [ProducesResponseType<ChangeKnowledgeStatusResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<ChangeKnowledgeStatusResponse>> ChangeKnowledgeStatus(
        [FromBody] ChangeKnowledgeStatusRequest request,
        CancellationToken cancellationToken)
    {
        var currentUser = await currentUserContext.ResolveAsync(cancellationToken);
        if (currentUser.Status != CurrentUserResolutionStatus.Available || currentUser.CurrentUser is null)
        {
            return currentUser.Status switch
            {
                CurrentUserResolutionStatus.Unauthenticated or CurrentUserResolutionStatus.SessionExpired => Unauthorized(new ApiErrorResponse("unauthenticated", "尚未登录。", null, null)),
                CurrentUserResolutionStatus.IdentityUnmapped => StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse("identity_unmapped", "当前登录身份尚未绑定系统用户。", null, null)),
                CurrentUserResolutionStatus.IdentityInactive => StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse("identity_inactive", "当前登录身份已停用。", null, null)),
                CurrentUserResolutionStatus.AccountInactive => StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse("account_inactive", "当前用户已停用。", null, null)),
                _ => throw new InvalidOperationException("Unsupported Current User resolution."),
            };
        }
        var result = await service.ChangeKnowledgeStatus(
            new ChangeKnowledgeStatusCommand(
                request.Target is null
                    ? null
                    : new KnowledgeStatusTargetCommand(request.Target.Type ?? string.Empty, request.Target.Id),
                request.TargetStatus ?? string.Empty,
                request.Reason,
                new KnowledgeStatusActorCommand(currentUser.CurrentUser.DisplayName, currentUser.CurrentUser.AccessLevel.ToString(), DateTimeOffset.UtcNow),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);

        return result.Failure switch
        {
            KnowledgeStatusFailure.None => Ok(result.Response),
            KnowledgeStatusFailure.Validation => BadRequest(new ApiErrorResponse(
                "validation_error", "请求内容无效。", result.FieldErrors, null)),
            KnowledgeStatusFailure.NotFound => NotFound(new ApiErrorResponse(
                "not_found", "未找到指定知识对象。", null,
                request.Target is null ? null : new { resourceType = request.Target.Type, resourceId = request.Target.Id })),
            KnowledgeStatusFailure.Conflict => Conflict(new ApiErrorResponse(
                "conflict", result.Message ?? "当前状态或并发标记已变化。", null, result.Details)),
            KnowledgeStatusFailure.Unsupported or KnowledgeStatusFailure.BusinessRuleViolation => UnprocessableEntity(new ApiErrorResponse(
                "business_rule_violation", result.Message ?? "当前操作不满足知识状态规则。", null, result.Details)),
            _ => throw new InvalidOperationException("Unsupported KnowledgeStatus result."),
        };
    }
}
