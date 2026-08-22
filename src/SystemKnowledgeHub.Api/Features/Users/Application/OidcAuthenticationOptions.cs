namespace SystemKnowledgeHub.Api.Features.Users.Application;

public sealed class OidcAuthenticationOptions
{
    public bool Enabled { get; init; }
    public string? DisplayName { get; init; }
    public string Provider { get; init; } = string.Empty;
    public string Authority { get; init; } = string.Empty;
    public string ClientId { get; init; } = string.Empty;
    public string? ClientSecret { get; init; }
    public string CallbackPath { get; init; } = "/signin-oidc";
    public string[] Scopes { get; init; } = ["openid", "profile"];
}
