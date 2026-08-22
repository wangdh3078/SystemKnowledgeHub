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
