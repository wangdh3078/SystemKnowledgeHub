using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AuthenticationMethodDisablementTests
{
    [Fact]
    public async Task Disabled_local_authentication_rejects_an_existing_local_principal_and_clears_the_cookie()
    {
        using var factory = new AuthenticationOptionsWebApplicationFactory(false, true);
        var descriptor = await AddLocalCredentialToDefaultAdministrator(factory);
        using var client = factory.CreateClient();
        AddDescriptorHeaders(client, AuthenticationClaims.LocalMethod, descriptor.IdentityId, 1, descriptor.UserId);

        using var response = await client.GetAsync("/api/current-user");

        await AssertMethodDisabled(response);
    }

    [Fact]
    public async Task Disabled_oidc_authentication_rejects_an_existing_oidc_principal_and_clears_the_cookie()
    {
        using var factory = new AuthenticationOptionsWebApplicationFactory(true, false);
        long userId;
        long identityId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var mapping = await db.LoginIdentities.SingleAsync();
            userId = mapping.UserId;
            identityId = mapping.Id;
        }
        using var client = factory.CreateClient();
        AddDescriptorHeaders(client, AuthenticationClaims.OidcMethod, identityId, 1, userId);

        using var response = await client.GetAsync("/api/current-user");

        await AssertMethodDisabled(response);
    }

    private static async Task<(long IdentityId, long UserId)> AddLocalCredentialToDefaultAdministrator(
        AuthenticationOptionsWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        var userId = await db.Users.Select(user => user.Id).SingleAsync();
        var timestamp = DateTimeOffset.UtcNow;
        var credential = new LocalLoginCredential
        {
            UserId = userId,
            Username = "disabled-local-test",
            NormalizedUsername = "DISABLED-LOCAL-TEST",
            IsActive = true,
            SessionVersion = 1,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastPasswordChangedAt = timestamp,
            Version = 1,
        };
        credential.PasswordHash = passwords.Hash(credential, "disabled local method password");
        db.LocalLoginCredentials.Add(credential);
        await db.SaveChangesAsync();
        return (credential.Id, userId);
    }

    private static void AddDescriptorHeaders(
        HttpClient client,
        string method,
        long identityId,
        long authVersion,
        long userId)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.AuthMethodHeader, method);
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.AuthIdentityHeader, identityId.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.AuthVersionHeader, authVersion.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.UserHeader, userId.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.AccessLevelHeader, AccessLevel.Administrator.ToString());
    }

    private static async Task AssertMethodDisabled(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("session_expired", error.GetProperty("code").GetString());
        Assert.Equal("authentication_method_disabled", error.GetProperty("details").GetProperty("reason").GetString());
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), value =>
            value.Contains(CurrentUserContext.CookieName, StringComparison.Ordinal)
            && value.Contains("expires=Thu, 01 Jan 1970", StringComparison.OrdinalIgnoreCase));
    }
}
