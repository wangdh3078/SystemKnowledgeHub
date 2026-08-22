using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

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
public sealed class CurrentUserController(ICurrentUserContext currentUserContext) : ControllerBase
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
            CurrentUserResolutionStatus.Unauthenticated => Unauthorized(Error(
                "unauthenticated",
                "尚未登录。",
                "missing")),
            CurrentUserResolutionStatus.SessionExpired => Unauthorized(Error(
                "session_expired",
                "登录会话已失效，请重新认证。",
                "expired")),
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

    private static ApiErrorResponse Error(string code, string message, string authStatus) => new(
        code,
        message,
        null,
        new { authStatus });
}
