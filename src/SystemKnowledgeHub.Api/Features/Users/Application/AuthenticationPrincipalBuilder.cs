using System.Globalization;
using System.Security.Claims;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

public sealed class AuthenticationPrincipalBuilder
{
    public ClaimsPrincipal Create(string method, long identityId, long version, User user)
    {
        var identity = new ClaimsIdentity(CurrentUserContext.CookieScheme);
        AddDescriptorClaims(identity, method, identityId, version, user.Id, user.AccessLevel);
        return new ClaimsPrincipal(identity);
    }

    public void AddDescriptorClaims(ClaimsIdentity identity, string method, long identityId, long version, long userId, AccessLevel accessLevel)
    {
        identity.AddClaim(new Claim(AuthenticationClaims.AuthMethod, method));
        identity.AddClaim(new Claim(AuthenticationClaims.AuthIdentityId, identityId.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(AuthenticationClaims.AuthVersion, version.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(AuthenticationClaims.UserId, userId.ToString(CultureInfo.InvariantCulture)));
        identity.AddClaim(new Claim(AuthenticationClaims.AccessLevel, accessLevel.ToString()));
    }
}
