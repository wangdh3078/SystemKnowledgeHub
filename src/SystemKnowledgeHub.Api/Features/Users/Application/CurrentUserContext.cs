using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

/// <summary>为业务 Use Case 提供当前已认证身份映射后的 canonical User context。</summary>
public interface ICurrentUserContext
{
    Task<CurrentUserResolution> ResolveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// 每次请求都以数据库和当前认证配置重新确认身份、用户、版本及强制改密状态；浏览器 claim 不是最终授权事实。
/// </summary>
public sealed class CurrentUserContext(
    IHttpContextAccessor httpContextAccessor,
    IAuthenticationService authenticationService,
    KnowledgeHubDbContext dbContext,
    UserQueries userQueries,
    IOptions<LocalAuthenticationOptions> localOptions,
    IOptions<OidcAuthenticationOptions> oidcOptions) : ICurrentUserContext
{
    public const string CookieScheme = "ApplicationCookie";
    public const string CookieName = "SystemKnowledgeHub.Auth";

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

        if (!AuthenticationSessionDescriptorReader.TryRead(httpContext.User, out var descriptor))
        {
            return await RejectSessionAsync(httpContext, "invalid_session_descriptor");
        }

        if (descriptor.Method == AuthenticationClaims.LocalMethod && !localOptions.Value.Enabled)
        {
            return await RejectSessionAsync(httpContext, "authentication_method_disabled");
        }
        if (descriptor.Method == AuthenticationClaims.OidcMethod && !oidcOptions.Value.Enabled)
        {
            return await RejectSessionAsync(httpContext, "authentication_method_disabled");
        }

        var identityResolution = descriptor.Method switch
        {
            AuthenticationClaims.OidcMethod => await ResolveOidcIdentityAsync(descriptor, cancellationToken),
            AuthenticationClaims.LocalMethod => await ResolveLocalCredentialAsync(descriptor, cancellationToken),
            _ => new IdentityResolution(CurrentUserResolutionStatus.SessionExpired, false, "invalid_authentication_method"),
        };
        if (identityResolution.Status != CurrentUserResolutionStatus.Available
            && identityResolution.Status != CurrentUserResolutionStatus.PasswordChangeRequired)
        {
            await authenticationService.SignOutAsync(httpContext, CookieScheme, null);
            return new CurrentUserResolution(identityResolution.Status, null, identityResolution.Reason);
        }

        var user = await userQueries.GetUser(descriptor.UserId, cancellationToken);
        if (user is null)
        {
            await authenticationService.SignOutAsync(httpContext, CookieScheme, null);
            return new CurrentUserResolution(CurrentUserResolutionStatus.IdentityUnmapped, null);
        }
        if (!user.IsActive)
        {
            await authenticationService.SignOutAsync(httpContext, CookieScheme, null);
            return new CurrentUserResolution(CurrentUserResolutionStatus.AccountInactive, null);
        }

        var accessLevel = await dbContext.Users
            .AsNoTracking()
            .Where(item => item.Id == user.Id)
            .Select(item => item.AccessLevel)
            .SingleAsync(cancellationToken);
        ProjectLatestAccessLevel(httpContext.User, accessLevel.ToString());

        return new CurrentUserResolution(
            identityResolution.Status,
            new CurrentUserResponse(
                user.Id,
                user.EmployeeNo,
                user.DisplayName,
                user.Email,
                user.DepartmentOrTeam,
                user.JobTitle,
                user.IsActive,
                user.KnowledgeRoles,
                accessLevel.ToString(),
                descriptor.Method,
                identityResolution.MustChangePassword));
    }

    private async Task<IdentityResolution> ResolveOidcIdentityAsync(
        AuthenticationSessionDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var identity = await dbContext.LoginIdentities
            .AsNoTracking()
            .Where(item => item.Id == descriptor.IdentityId)
            .Select(item => new { item.UserId, item.Provider, item.IsActive, item.Version })
            .SingleOrDefaultAsync(cancellationToken);
        if (identity is null || identity.UserId != descriptor.UserId)
        {
            return new IdentityResolution(CurrentUserResolutionStatus.IdentityUnmapped, false, null);
        }
        if (!identity.IsActive)
        {
            return new IdentityResolution(CurrentUserResolutionStatus.IdentityInactive, false, null);
        }
        if (!string.Equals(identity.Provider, oidcOptions.Value.Provider, StringComparison.Ordinal))
        {
            return new IdentityResolution(CurrentUserResolutionStatus.SessionExpired, false, "authentication_provider_disabled");
        }
        return identity.Version == descriptor.AuthVersion
            ? new IdentityResolution(CurrentUserResolutionStatus.Available, false, null)
            : new IdentityResolution(CurrentUserResolutionStatus.SessionExpired, false, "session_version_changed");
    }

    private async Task<IdentityResolution> ResolveLocalCredentialAsync(
        AuthenticationSessionDescriptor descriptor,
        CancellationToken cancellationToken)
    {
        var credential = await dbContext.LocalLoginCredentials
            .AsNoTracking()
            .Where(item => item.Id == descriptor.IdentityId)
            .Select(item => new { item.UserId, item.IsActive, item.SessionVersion, item.MustChangePassword })
            .SingleOrDefaultAsync(cancellationToken);
        if (credential is null || credential.UserId != descriptor.UserId || !credential.IsActive)
        {
            return new IdentityResolution(CurrentUserResolutionStatus.SessionExpired, false, "local_credential_inactive");
        }
        if (credential.SessionVersion != descriptor.AuthVersion)
        {
            return new IdentityResolution(CurrentUserResolutionStatus.SessionExpired, false, "session_version_changed");
        }
        return credential.MustChangePassword
            ? new IdentityResolution(CurrentUserResolutionStatus.PasswordChangeRequired, true, null)
            : new IdentityResolution(CurrentUserResolutionStatus.Available, false, null);
    }

    private async Task<CurrentUserResolution> RejectSessionAsync(HttpContext httpContext, string reason)
    {
        await authenticationService.SignOutAsync(httpContext, CookieScheme, null);
        return new CurrentUserResolution(CurrentUserResolutionStatus.SessionExpired, null, reason);
    }

    private static void ProjectLatestAccessLevel(ClaimsPrincipal principal, string accessLevel)
    {
        if (principal.Identity is not ClaimsIdentity identity) return;
        foreach (var claim in identity.FindAll(AuthenticationClaims.AccessLevel).ToArray()) identity.RemoveClaim(claim);
        identity.AddClaim(new Claim(AuthenticationClaims.AccessLevel, accessLevel));
    }

    private sealed record IdentityResolution(
        CurrentUserResolutionStatus Status,
        bool MustChangePassword,
        string? Reason);
}
