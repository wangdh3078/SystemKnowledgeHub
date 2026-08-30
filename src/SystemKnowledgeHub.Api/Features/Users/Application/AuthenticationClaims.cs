using System.Globalization;
using System.Security.Claims;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

public static class AuthenticationClaims
{
    public const string AuthMethod = "systemknowledgehub/auth_method";
    public const string AuthIdentityId = "systemknowledgehub/auth_identity_id";
    public const string AuthVersion = "systemknowledgehub/auth_version";
    public const string UserId = "systemknowledgehub/user_id";
    public const string AccessLevel = "systemknowledgehub/access_level";

    public const string OidcMethod = "oidc";
    public const string LocalMethod = "local";
}

public sealed record AuthenticationSessionDescriptor(
    string Method,
    long IdentityId,
    long AuthVersion,
    long UserId);

public static class AuthenticationSessionDescriptorReader
{
    public static bool TryRead(ClaimsPrincipal principal, out AuthenticationSessionDescriptor descriptor)
    {
        descriptor = default!;
        var method = principal.FindFirstValue(AuthenticationClaims.AuthMethod);
        if ((method != AuthenticationClaims.LocalMethod && method != AuthenticationClaims.OidcMethod)
            || !TryReadId(principal, AuthenticationClaims.AuthIdentityId, out var identityId)
            || !TryReadId(principal, AuthenticationClaims.AuthVersion, out var authVersion)
            || !TryReadId(principal, AuthenticationClaims.UserId, out var userId))
        {
            return false;
        }

        descriptor = new AuthenticationSessionDescriptor(method, identityId, authVersion, userId);
        return true;
    }

    private static bool TryReadId(ClaimsPrincipal principal, string claimType, out long id)
    {
        var raw = principal.FindFirstValue(claimType);
        return long.TryParse(raw, NumberStyles.None, CultureInfo.InvariantCulture, out id)
            && ApiIdParser.IsSafePositive(id);
    }
}
