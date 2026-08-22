using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Application;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public sealed class TestAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    public const string TestScheme = "TestAuthentication";
    public const string AuthMethodHeader = "X-Test-Auth-Method";
    public const string AuthIdentityHeader = "X-Test-Auth-Identity-Id";
    public const string AuthVersionHeader = "X-Test-Auth-Version";
    public const string UserHeader = "X-Test-User-Id";
    public const string AccessLevelHeader = "X-Test-Access-Level";

    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(AuthMethodHeader, out var authMethod)
            || !Request.Headers.TryGetValue(AuthIdentityHeader, out var authIdentityId)
            || !Request.Headers.TryGetValue(AuthVersionHeader, out var authVersion)
            || !Request.Headers.TryGetValue(UserHeader, out var userId)
            || !Request.Headers.TryGetValue(AccessLevelHeader, out var accessLevel))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(TestScheme);
        identity.AddClaim(new Claim(AuthenticationClaims.AuthMethod, authMethod.ToString()));
        identity.AddClaim(new Claim(AuthenticationClaims.AuthIdentityId, authIdentityId.ToString()));
        identity.AddClaim(new Claim(AuthenticationClaims.AuthVersion, authVersion.ToString()));
        identity.AddClaim(new Claim(AuthenticationClaims.UserId, userId.ToString()));
        identity.AddClaim(new Claim(AuthenticationClaims.AccessLevel, accessLevel.ToString()));
        return Task.FromResult(AuthenticateResult.Success(
            new AuthenticationTicket(new ClaimsPrincipal(identity), TestScheme)));
    }
}
