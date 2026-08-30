using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class LocalPasswordLifecycleApiTests : IClassFixture<AuditedLocalLoginWebApplicationFactory>
{
    private readonly AuditedLocalLoginWebApplicationFactory _factory;

    public LocalPasswordLifecycleApiTests(AuditedLocalLoginWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Forced_password_change_allows_only_the_lifecycle_whitelist()
    {
        var credential = await CreateCredentialAsync(mustChangePassword: true);
        using var client = await LoginAsync(credential.Username, credential.Password);

        using var current = await client.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var profile = await current.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("local", profile.GetProperty("authenticationMethod").GetString());
        Assert.True(profile.GetProperty("mustChangePassword").GetBoolean());

        using var business = await client.GetAsync("/api/dashboard");
        Assert.Equal(HttpStatusCode.Forbidden, business.StatusCode);
        var error = await business.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("must_change_password", error.GetProperty("code").GetString());
        Assert.Equal("must_change_password", error.GetProperty("details").GetProperty("authStatus").GetString());

        using var explicitlyAuthorizedBusiness = await client.GetAsync(
            "/api/knowledge-documents/1/attachments/1/content");
        Assert.Equal(HttpStatusCode.Forbidden, explicitlyAuthorizedBusiness.StatusCode);
        Assert.Equal("must_change_password", await ErrorCode(explicitlyAuthorizedBusiness));

        using var antiforgery = await client.GetAsync("/api/antiforgery/token");
        Assert.Equal(HttpStatusCode.OK, antiforgery.StatusCode);
        using var logout = await client.PostAsync("/auth/logout", null);
        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
    }

    [Fact]
    public async Task Password_change_validates_inputs_updates_atomically_and_invalidates_all_old_local_sessions()
    {
        var credential = await CreateCredentialAsync(mustChangePassword: true);
        using var firstSession = await LoginAsync(credential.Username, credential.Password);
        using var secondSession = await LoginAsync(credential.Username, credential.Password);
        var newPassword = $"新密码 {Guid.NewGuid():N} !";

        using var tooShort = await firstSession.PutAsJsonAsync("/api/current-user/password", new
        {
            currentPassword = credential.Password,
            newPassword = "short",
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        var tooShortError = await tooShort.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_error", tooShortError.GetProperty("code").GetString());
        Assert.Equal("newPassword", tooShortError.GetProperty("fieldErrors").EnumerateObject().Single().Name);

        using var wrongCurrent = await firstSession.PutAsJsonAsync("/api/current-user/password", new
        {
            currentPassword = "错误但长度足够的当前密码",
            newPassword,
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);
        Assert.Equal("currentPassword", await FirstFieldError(wrongCurrent));

        using var unchanged = await firstSession.PutAsJsonAsync("/api/current-user/password", new
        {
            currentPassword = credential.Password,
            newPassword = credential.Password,
        });
        Assert.Equal(HttpStatusCode.BadRequest, unchanged.StatusCode);
        Assert.Equal("newPassword", await FirstFieldError(unchanged));

        long oldSessionVersion;
        long oldVersion;
        DateTimeOffset oldPasswordChangedAt;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var stored = await db.LocalLoginCredentials.SingleAsync(item => item.Id == credential.CredentialId);
            oldSessionVersion = stored.SessionVersion;
            oldVersion = stored.Version;
            oldPasswordChangedAt = stored.LastPasswordChangedAt;
            stored.FailedLoginAttempts = 4;
            stored.FailedLoginWindowStartedAt = DateTimeOffset.UtcNow.AddMinutes(-1);
            stored.LockedUntil = DateTimeOffset.UtcNow.AddMinutes(5);
            stored.UpdatedAt = DateTimeOffset.UtcNow;
            stored.Version += 1;
            oldVersion += 1;
            await db.SaveChangesAsync();
        }

        using var changed = await firstSession.PutAsJsonAsync("/api/current-user/password", new
        {
            currentPassword = credential.Password,
            newPassword,
        });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var stored = await db.LocalLoginCredentials.SingleAsync(item => item.Id == credential.CredentialId);
            var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
            Assert.False(stored.MustChangePassword);
            Assert.Equal(0, stored.FailedLoginAttempts);
            Assert.Null(stored.FailedLoginWindowStartedAt);
            Assert.Null(stored.LockedUntil);
            Assert.Equal(oldSessionVersion + 1, stored.SessionVersion);
            Assert.Equal(oldVersion + 1, stored.Version);
            Assert.True(stored.LastPasswordChangedAt > oldPasswordChangedAt);
            Assert.NotEqual(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,
                passwords.Verify(stored, stored.PasswordHash, newPassword));
            Assert.Equal(Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed,
                passwords.Verify(stored, stored.PasswordHash, credential.Password));
        }

        using var currentCookieCleared = await firstSession.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.Unauthorized, currentCookieCleared.StatusCode);
        using var otherOldSession = await secondSession.PutAsJsonAsync("/api/current-user/password", new
        {
            currentPassword = credential.Password,
            newPassword = $"another new password {Guid.NewGuid():N}",
        });
        Assert.Equal(HttpStatusCode.Unauthorized, otherOldSession.StatusCode);
        Assert.Equal("session_expired", await ErrorCode(otherOldSession));

        using var oldPasswordClient = CreateCookieClient();
        await AddAntiforgeryTokenAsync(oldPasswordClient);
        using var oldPasswordLogin = await oldPasswordClient.PostAsJsonAsync("/auth/local/login", new
        {
            username = credential.Username,
            password = credential.Password,
        });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        using var newPasswordClient = await LoginAsync(credential.Username, newPassword);
        using var newCurrent = await newPasswordClient.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.OK, newCurrent.StatusCode);
        Assert.False((await newCurrent.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mustChangePassword").GetBoolean());

        var auditEntries = _factory.LogSink.Entries
            .Where(entry => entry.Contains("LocalPasswordChangedByUser", StringComparison.Ordinal))
            .ToArray();
        Assert.Contains(auditEntries, entry => entry.Contains("result=success", StringComparison.Ordinal));
        Assert.Contains(auditEntries, entry => entry.Contains("result=rejected", StringComparison.Ordinal));
        Assert.Contains(auditEntries, entry => entry.Contains($"actor_user_id={credential.UserId}", StringComparison.Ordinal));
        Assert.Contains(auditEntries, entry => entry.Contains($"local_credential_id={credential.CredentialId}", StringComparison.Ordinal));
        Assert.DoesNotContain(auditEntries, entry =>
            entry.Contains(credential.Password, StringComparison.Ordinal)
            || entry.Contains(newPassword, StringComparison.Ordinal));
    }

    [Fact]
    public async Task Oidc_session_cannot_call_the_local_password_endpoint()
    {
        using var oidcFactory = new BootstrapWebApplicationFactory();
        using var client = oidcFactory.CreateAuthenticatedClient();
        using var response = await client.PutAsJsonAsync("/api/current-user/password", new
        {
            currentPassword = "irrelevant current password",
            newPassword = "irrelevant new password",
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("password_change_not_available", await ErrorCode(response));
    }

    [Fact]
    public async Task Concurrent_password_changes_allow_exactly_one_committed_winner()
    {
        var credential = await CreateCredentialAsync(mustChangePassword: false);
        using var first = await LoginAsync(credential.Username, credential.Password);
        using var second = await LoginAsync(credential.Username, credential.Password);
        var firstPassword = $"并发新密码 A {Guid.NewGuid():N}";
        var secondPassword = $"并发新密码 B {Guid.NewGuid():N}";

        var responses = await Task.WhenAll(
            first.PutAsJsonAsync("/api/current-user/password", new
            {
                currentPassword = credential.Password,
                newPassword = firstPassword,
            }),
            second.PutAsJsonAsync("/api/current-user/password", new
            {
                currentPassword = credential.Password,
                newPassword = secondPassword,
            }));
        try
        {
            Assert.Single(responses, response => response.StatusCode == HttpStatusCode.NoContent);
            Assert.Single(responses, response => response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Conflict);
        }
        finally
        {
            foreach (var response in responses) response.Dispose();
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await db.LocalLoginCredentials.SingleAsync(item => item.Id == credential.CredentialId);
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        Assert.Equal(2, stored.SessionVersion);
        Assert.Equal(2, stored.Version);
        var firstMatches = passwords.Verify(stored, stored.PasswordHash, firstPassword)
            != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
        var secondMatches = passwords.Verify(stored, stored.PasswordHash, secondPassword)
            != Microsoft.AspNetCore.Identity.PasswordVerificationResult.Failed;
        Assert.NotEqual(firstMatches, secondMatches);
    }

    private async Task<CredentialFixture> CreateCredentialAsync(bool mustChangePassword)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"密码生命周期测试 {suffix}",
            IsActive = true,
            AccessLevel = AccessLevel.Administrator,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var password = $"原密码 {suffix} ! 足够长";
        var local = new LocalLoginCredential
        {
            UserId = user.Id,
            Username = $"lifecycle-{suffix}",
            NormalizedUsername = $"LIFECYCLE-{suffix.ToUpperInvariant()}",
            IsActive = true,
            MustChangePassword = mustChangePassword,
            SessionVersion = 1,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastPasswordChangedAt = timestamp,
            Version = 1,
        };
        local.PasswordHash = passwords.Hash(local, password);
        db.LocalLoginCredentials.Add(local);
        await db.SaveChangesAsync();
        return new CredentialFixture(local.Id, user.Id, local.Username, password);
    }

    private HttpClient CreateCookieClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
    });

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = CreateCookieClient();
        await AddAntiforgeryTokenAsync(client);
        using var response = await client.PostAsJsonAsync("/auth/local/login", new { username, password });
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
        await AddAntiforgeryTokenAsync(client);
        return client;
    }

    private static async Task AddAntiforgeryTokenAsync(HttpClient client)
    {
        using var response = await client.GetAsync("/api/antiforgery/token");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var token = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestToken").GetString();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-CSRF-TOKEN", token);
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();

    private static async Task<string> FirstFieldError(HttpResponseMessage response)
    {
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("fieldErrors").EnumerateObject().Single().Name;
    }

    private sealed record CredentialFixture(long CredentialId, long UserId, string Username, string Password);
}
