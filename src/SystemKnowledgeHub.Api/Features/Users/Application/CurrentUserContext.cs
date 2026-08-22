using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

/// <summary>
/// 为业务 Use Case 提供当前已认证身份映射后的 canonical User context。
/// </summary>
/// <remarks>
/// Current User 是可信操作者的业务层上下文，不是认证凭据、原始 Principal 或 HttpContext 包装。
/// 请求 body 或 header 不得提供另一个 User ID 来切换或覆盖该上下文；需要 HumanConfirmation 等可信操作者归属的
/// Use Case 可通过此契约取得 canonical User。
/// </remarks>
public interface ICurrentUserContext
{
    /// <summary>
    /// 解析当前 authenticated Principal 对应的 canonical User context。
    /// </summary>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>
    /// 异步操作完成后，返回可用的 canonical User profile，或表示未认证、会话失效、身份映射失效、LoginIdentity 停用
    /// 或 canonical User 停用的明确解析结果。
    /// </returns>
    Task<CurrentUserResolution> ResolveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 从服务器已经建立的 authenticated Principal 解析 Current User 的 Web adapter。
/// </summary>
/// <remarks>
/// 每次解析都会根据认证来源重新确认 Principal 与 LoginIdentity 或 LocalLoginCredential、canonical User 的映射及其
/// Active / session version 状态，并投影最新的 AccessLevel；因此浏览器提交的任意 User 标识不能改变最终结果。
/// </remarks>
public sealed class CurrentUserContext(
    IHttpContextAccessor httpContextAccessor,
    IAuthenticationService authenticationService,
    KnowledgeHubDbContext dbContext,
    UserQueries userQueries) : ICurrentUserContext
{
    public const string CookieScheme = "ApplicationCookie";
    public const string CookieName = "SystemKnowledgeHub.Auth";

    /// <inheritdoc />
    public async Task<CurrentUserResolution> ResolveAsync(CancellationToken cancellationToken)
    {
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext is null || httpContext.User.Identity?.IsAuthenticated != true)
        {
            return new CurrentUserResolution(
                httpContext?.Request.Cookies.ContainsKey(CookieName) == true
                    ? CurrentUserResolutionStatus.SessionExpired
                    : CurrentUserResolutionStatus.Unauthenticated,
                null);
        }

        var authMethod = httpContext.User.FindFirstValue(AuthenticationClaims.AuthMethod);
        if (!TryReadId(httpContext.User, AuthenticationClaims.AuthIdentityId, out var authIdentityId)
            || !TryReadId(httpContext.User, AuthenticationClaims.AuthVersion, out var authVersion)
            || !TryReadId(httpContext.User, AuthenticationClaims.UserId, out var principalUserId))
        {
            await RejectSessionAsync(httpContext);
            return new CurrentUserResolution(CurrentUserResolutionStatus.SessionExpired, null);
        }

        var identityStatus = authMethod switch
        {
            AuthenticationClaims.OidcMethod => await ResolveOidcIdentityAsync(authIdentityId, authVersion, principalUserId, cancellationToken),
            AuthenticationClaims.LocalMethod => await ResolveLocalCredentialAsync(authIdentityId, authVersion, principalUserId, cancellationToken),
            _ => CurrentUserResolutionStatus.SessionExpired,
        };
        if (identityStatus != CurrentUserResolutionStatus.Available)
        {
            await RejectSessionAsync(httpContext);
            return new CurrentUserResolution(identityStatus, null);
        }

        var user = await userQueries.GetUser(principalUserId, cancellationToken);
        if (user is null)
        {
            await RejectSessionAsync(httpContext);
            return new CurrentUserResolution(CurrentUserResolutionStatus.IdentityUnmapped, null);
        }
        if (!user.IsActive)
        {
            await RejectSessionAsync(httpContext);
            return new CurrentUserResolution(CurrentUserResolutionStatus.AccountInactive, null);
        }

        var accessLevel = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == user.Id)
            .Select(item => item.AccessLevel)
            .SingleAsync(cancellationToken);
        ProjectLatestAccessLevel(httpContext.User, accessLevel.ToString());

        return new CurrentUserResolution(
            CurrentUserResolutionStatus.Available,
            new CurrentUserResponse(
                user.Id,
                user.EmployeeNo,
                user.DisplayName,
                user.Email,
                user.DepartmentOrTeam,
                user.JobTitle,
                user.IsActive,
                user.KnowledgeRoles,
                accessLevel.ToString()));
    }

    private static bool TryReadId(ClaimsPrincipal principal, string claimType, out long id)
    {
        var raw = principal.FindFirstValue(claimType);
        return long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out id)
            && ApiIdParser.IsSafePositive(id);
    }

    private async Task<CurrentUserResolutionStatus> ResolveOidcIdentityAsync(
        long identityId,
        long authVersion,
        long userId,
        CancellationToken cancellationToken)
    {
        var identity = await dbContext.LoginIdentities
            .AsNoTracking()
            .Where(item => item.Id == identityId)
            .Select(item => new { item.UserId, item.IsActive, item.Version })
            .SingleOrDefaultAsync(cancellationToken);
        if (identity is null || identity.UserId != userId) return CurrentUserResolutionStatus.IdentityUnmapped;
        if (!identity.IsActive) return CurrentUserResolutionStatus.IdentityInactive;
        return identity.Version == authVersion
            ? CurrentUserResolutionStatus.Available
            : CurrentUserResolutionStatus.SessionExpired;
    }

    private async Task<CurrentUserResolutionStatus> ResolveLocalCredentialAsync(
        long credentialId,
        long authVersion,
        long userId,
        CancellationToken cancellationToken)
    {
        var credential = await dbContext.LocalLoginCredentials
            .AsNoTracking()
            .Where(item => item.Id == credentialId)
            .Select(item => new { item.UserId, item.IsActive, item.SessionVersion })
            .SingleOrDefaultAsync(cancellationToken);
        if (credential is null || credential.UserId != userId || !credential.IsActive || credential.SessionVersion != authVersion)
        {
            return CurrentUserResolutionStatus.SessionExpired;
        }
        return CurrentUserResolutionStatus.Available;
    }

    private async Task RejectSessionAsync(HttpContext httpContext)
    {
        await authenticationService.SignOutAsync(httpContext, CookieScheme, null);
    }

    private static void ProjectLatestAccessLevel(ClaimsPrincipal principal, string accessLevel)
    {
        if (principal.Identity is not ClaimsIdentity identity)
        {
            return;
        }

        foreach (var claim in identity.FindAll(AuthenticationClaims.AccessLevel).ToArray())
        {
            identity.RemoveClaim(claim);
        }
        identity.AddClaim(new Claim(AuthenticationClaims.AccessLevel, accessLevel));
    }
}
