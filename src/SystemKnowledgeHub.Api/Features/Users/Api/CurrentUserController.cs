using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.Users.Api;

/// <summary>
/// 提供当前 authenticated Principal 所映射 canonical User profile 的 API。
/// </summary>
/// <remarks>
/// 此 API 不接受浏览器指定的 User ID；响应同时投影当前系统访问等级 AccessLevel 与知识归属用的
/// KnowledgeRole assignment，二者不互为权限定义。
/// </remarks>
[ApiController]
[Route("api/current-user")]
public sealed class CurrentUserController(
    ICurrentUserContext currentUserContext,
    LocalPasswordLifecycleService passwordLifecycleService) : ControllerBase
{
    /// <summary>
    /// 返回当前已认证 canonical User 的 profile。
    /// </summary>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>
    /// 异步操作完成后，成功返回 <c>200</c> 及 <see cref="CurrentUserResponse"/>；没有可用认证身份或会话失效时返回
    /// <c>401</c>，身份未映射、LoginIdentity 停用或 canonical User 停用时返回 <c>403</c> 及
    /// <see cref="ApiErrorResponse"/>。
    /// </returns>
    [HttpGet]
    [Authorize(Policy = AccessPolicies.PasswordLifecycle)]
    [ProducesResponseType<CurrentUserResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    public async Task<ActionResult<CurrentUserResponse>> GetCurrentUser(
        CancellationToken cancellationToken)
    {
        var resolution = await currentUserContext.ResolveAsync(cancellationToken);
        return resolution.Status switch
        {
            CurrentUserResolutionStatus.Available => Ok(resolution.CurrentUser),
            CurrentUserResolutionStatus.PasswordChangeRequired => Ok(resolution.CurrentUser),
            CurrentUserResolutionStatus.Unauthenticated => Unauthorized(Error(
                "unauthenticated",
                "尚未登录。",
                "missing")),
            CurrentUserResolutionStatus.SessionExpired => Unauthorized(Error(
                "session_expired",
                "登录会话已失效，请重新认证。",
                "expired",
                resolution.Reason)),
            CurrentUserResolutionStatus.IdentityUnmapped => StatusCode(StatusCodes.Status403Forbidden, Error(
                "identity_unmapped",
                "当前登录身份尚未绑定系统用户。",
                "unmapped")),
            CurrentUserResolutionStatus.IdentityInactive => StatusCode(StatusCodes.Status403Forbidden, Error(
                "identity_inactive",
                "当前登录身份已停用。",
                "identity_inactive")),
            CurrentUserResolutionStatus.AccountInactive => StatusCode(StatusCodes.Status403Forbidden, Error(
                "account_inactive",
                "当前用户已停用。",
                "inactive")),
            _ => throw new InvalidOperationException("Unsupported Current User resolution."),
        };
    }

    /// <summary>当前 Local 用户修改自己的密码；成功后清除当前 Cookie，客户端必须重新登录。</summary>
    [HttpPut("password")]
    [Authorize(Policy = AccessPolicies.PasswordLifecycle)]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status403Forbidden)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> ChangePassword(
        [FromBody] ChangeMyLocalPasswordRequest? request,
        CancellationToken cancellationToken)
    {
        var result = await passwordLifecycleService.ChangeAsync(
            request?.CurrentPassword,
            request?.NewPassword,
            cancellationToken);
        switch (result.Failure)
        {
            case LocalPasswordChangeFailure.None:
                await HttpContext.SignOutAsync(CurrentUserContext.CookieScheme);
                return NoContent();
            case LocalPasswordChangeFailure.Validation:
                return BadRequest(new ApiErrorResponse(
                    "validation_error",
                    "请检查密码输入。",
                    result.FieldErrors,
                    null));
            case LocalPasswordChangeFailure.Forbidden:
                return StatusCode(StatusCodes.Status403Forbidden, new ApiErrorResponse(
                    "password_change_not_available",
                    "当前认证方式不支持修改本地密码。",
                    null,
                    new { reason = result.Reason }));
            case LocalPasswordChangeFailure.SessionExpired:
                await HttpContext.SignOutAsync(CurrentUserContext.CookieScheme);
                return Unauthorized(Error(
                    "session_expired",
                    "登录会话已失效，请重新认证。",
                    "expired",
                    result.Reason));
            case LocalPasswordChangeFailure.Conflict:
                return Conflict(new ApiErrorResponse(
                    "concurrency_conflict",
                    "凭据已被其他请求更新，请重新登录后再试。",
                    null,
                    new { reason = result.Reason }));
            default:
                throw new InvalidOperationException("Unsupported password change result.");
        }
    }

    private static ApiErrorResponse Error(string code, string message, string authStatus, string? reason = null) => new(
        code,
        message,
        null,
        new { authStatus, reason });
}
