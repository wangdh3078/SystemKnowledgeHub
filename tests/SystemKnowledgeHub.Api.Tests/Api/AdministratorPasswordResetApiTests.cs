using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Api;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AdministratorPasswordResetApiTests
{
    private const string AdministratorPassword = "AUTH-B04 administrator canary password";
    private const string OldPassword = "AUTH-B04 old local canary password";
    private const string NewTemporaryPassword = "AUTH-B04 new temporary canary 密码  空格";
    private const string SecondTemporaryPassword = "AUTH-B04 second temporary canary password";

    [Fact]
    public async Task Administrator_reset_replaces_password_enters_forced_change_and_invalidates_old_local_sessions()
    {
        using var factory = new LocalLoginWebApplicationFactory();
        using var administrator = await CreateLocalAdministratorClient(factory);
        var target = await AddLocalUser(factory, isUserActive: true, isCredentialActive: true);
        var local = await GetLocalProjection(administrator, target.UserId);

        using var oldSession = factory.CreateClient();
        using var oldLogin = await LoginLocal(oldSession, target.Username, OldPassword);
        Assert.Equal(HttpStatusCode.NoContent, oldLogin.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await oldSession.GetAsync("/api/current-user")).StatusCode);

        using var reset = await administrator.PostAsJsonAsync(
            $"/api/users/{target.UserId}/local-credential/reset-password",
            new
            {
                newPassword = NewTemporaryPassword,
                credentialConcurrencyToken = local.GetProperty("concurrencyToken").GetString(),
            });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var body = await reset.Content.ReadAsStringAsync();
        Assert.DoesNotContain(NewTemporaryPassword, body, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionVersion", body, StringComparison.OrdinalIgnoreCase);
        var projection = JsonDocument.Parse(body).RootElement;
        Assert.True(projection.GetProperty("isActive").GetBoolean());
        Assert.True(projection.GetProperty("mustChangePassword").GetBoolean());
        Assert.NotEqual(local.GetProperty("concurrencyToken").GetString(), projection.GetProperty("concurrencyToken").GetString());

        await using (var resetScope = factory.Services.CreateAsyncScope())
        {
            var resetDb = resetScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var resetCredential = await resetDb.LocalLoginCredentials.SingleAsync(item => item.UserId == target.UserId);
            Assert.True(resetCredential.MustChangePassword);
            Assert.Equal(0, resetCredential.FailedLoginAttempts);
            Assert.Null(resetCredential.FailedLoginWindowStartedAt);
            Assert.Null(resetCredential.LockedUntil);
            Assert.Equal(2, resetCredential.SessionVersion);
            Assert.Equal(2, resetCredential.Version);
        }

        Assert.Equal(HttpStatusCode.Unauthorized, (await oldSession.GetAsync("/api/current-user")).StatusCode);
        using var oldPasswordClient = factory.CreateClient();
        using var oldPasswordLogin = await LoginLocal(oldPasswordClient, target.Username, OldPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);

        using var newPasswordClient = factory.CreateClient();
        using var newPasswordLogin = await LoginLocal(newPasswordClient, target.Username, NewTemporaryPassword);
        Assert.Equal(HttpStatusCode.NoContent, newPasswordLogin.StatusCode);
        using var current = await newPasswordClient.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.True((await current.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(HttpStatusCode.Forbidden, (await newPasswordClient.GetAsync("/api/dashboard")).StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await db.LocalLoginCredentials.SingleAsync(item => item.UserId == target.UserId);
        var passwordService = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        Assert.Equal(PasswordVerificationResult.Success, passwordService.Verify(stored, stored.PasswordHash, NewTemporaryPassword));
        Assert.Equal(PasswordVerificationResult.Failed, passwordService.Verify(stored, stored.PasswordHash, OldPassword));
        Assert.True(stored.MustChangePassword);
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.Null(stored.FailedLoginWindowStartedAt);
        Assert.Null(stored.LockedUntil);
        Assert.Equal(2, stored.SessionVersion);
    }

    [Fact]
    public async Task Reset_clears_lockout_without_enabling_the_credential_or_user()
    {
        using var factory = new LocalLoginWebApplicationFactory();
        using var administrator = await CreateLocalAdministratorClient(factory);
        var target = await AddLocalUser(factory, isUserActive: false, isCredentialActive: false, locked: true);
        var local = await GetLocalProjection(administrator, target.UserId);

        using var reset = await administrator.PostAsJsonAsync(
            $"/api/users/{target.UserId}/local-credential/reset-password",
            new
            {
                newPassword = NewTemporaryPassword,
                credentialConcurrencyToken = local.GetProperty("concurrencyToken").GetString(),
            });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var projection = await reset.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(projection.GetProperty("isActive").GetBoolean());
        Assert.True(projection.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal(JsonValueKind.Null, projection.GetProperty("lockedUntil").ValueKind);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False((await db.Users.SingleAsync(item => item.Id == target.UserId)).IsActive);
        var stored = await db.LocalLoginCredentials.SingleAsync(item => item.UserId == target.UserId);
        Assert.False(stored.IsActive);
        Assert.Equal(0, stored.FailedLoginAttempts);
        Assert.Null(stored.FailedLoginWindowStartedAt);
        Assert.Null(stored.LockedUntil);

        using var loginClient = factory.CreateClient();
        using var login = await LoginLocal(loginClient, target.Username, NewTemporaryPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
    }

    [Fact]
    public async Task Reset_is_administrator_only_antiforgery_protected_and_rejects_stale_credential_token()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var target = await AddLocalUser(factory, isUserActive: true, isCredentialActive: true);
        var editorId = await AddUser(factory, AccessLevel.Editor);
        var viewerId = await AddUser(factory, AccessLevel.Viewer);
        using var administrator = factory.CreateAuthenticatedClient();
        using var missingAntiforgery = factory.CreateAuthenticatedClientWithoutAntiforgery();
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);
        var local = await GetLocalProjection(administrator, target.UserId);
        var token = local.GetProperty("concurrencyToken").GetString();
        var request = new { newPassword = NewTemporaryPassword, credentialConcurrencyToken = token };

        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync($"/api/users/{target.UserId}/local-credential/reset-password", request)).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync($"/api/users/{target.UserId}/local-credential/reset-password", request)).StatusCode);
        using var csrf = await missingAntiforgery.PostAsJsonAsync($"/api/users/{target.UserId}/local-credential/reset-password", request);
        Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
        Assert.Contains("antiforgery_failed", await csrf.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var first = await administrator.PostAsJsonAsync($"/api/users/{target.UserId}/local-credential/reset-password", request);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        using var stale = await administrator.PostAsJsonAsync(
            $"/api/users/{target.UserId}/local-credential/reset-password",
            new { newPassword = SecondTemporaryPassword, credentialConcurrencyToken = token });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var error = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("concurrency_conflict", error.GetProperty("details").GetProperty("reason").GetString());
        Assert.Equal("LocalLoginCredential", error.GetProperty("details").GetProperty("resourceType").GetString());

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await db.LocalLoginCredentials.SingleAsync(item => item.UserId == target.UserId);
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        Assert.Equal(PasswordVerificationResult.Success, passwords.Verify(stored, stored.PasswordHash, NewTemporaryPassword));
        Assert.Equal(PasswordVerificationResult.Failed, passwords.Verify(stored, stored.PasswordHash, SecondTemporaryPassword));
    }

    [Fact]
    public async Task Reset_does_not_invalidate_the_target_users_oidc_session()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var target = await AddLocalUser(factory, isUserActive: true, isCredentialActive: true);
        using var targetOidcSession = await factory.CreateAuthenticatedClientAsync(target.UserId);
        using var administrator = factory.CreateAuthenticatedClient();
        var local = await GetLocalProjection(administrator, target.UserId);

        Assert.Equal(HttpStatusCode.OK, (await targetOidcSession.GetAsync("/api/current-user")).StatusCode);
        using var reset = await administrator.PostAsJsonAsync(
            $"/api/users/{target.UserId}/local-credential/reset-password",
            new
            {
                newPassword = NewTemporaryPassword,
                credentialConcurrencyToken = local.GetProperty("concurrencyToken").GetString(),
            });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);

        using var current = await targetOidcSession.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        Assert.Equal("oidc", (await current.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("authenticationMethod").GetString());
    }

    [Fact]
    public async Task Self_reset_expires_the_administrators_current_local_session()
    {
        using var factory = new LocalLoginWebApplicationFactory();
        using var administrator = await CreateLocalAdministratorClient(factory);
        long administratorId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            administratorId = await scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>()
                .Users.Where(item => item.AccessLevel == AccessLevel.Administrator).Select(item => item.Id).SingleAsync();
        }
        var local = await GetLocalProjection(administrator, administratorId);

        using var reset = await administrator.PostAsJsonAsync(
            $"/api/users/{administratorId}/local-credential/reset-password",
            new
            {
                newPassword = NewTemporaryPassword,
                credentialConcurrencyToken = local.GetProperty("concurrencyToken").GetString(),
            });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await administrator.GetAsync("/api/current-user")).StatusCode);
    }

    [Fact]
    public async Task Reset_audit_and_response_never_disclose_password_hash_or_session_version()
    {
        using var factory = new AuditedLocalLoginWebApplicationFactory();
        using var administrator = await CreateLocalAdministratorClient(factory);
        var target = await AddLocalUser(factory, isUserActive: true, isCredentialActive: true);
        var local = await GetLocalProjection(administrator, target.UserId);

        using var reset = await administrator.PostAsJsonAsync(
            $"/api/users/{target.UserId}/local-credential/reset-password",
            new
            {
                newPassword = NewTemporaryPassword,
                credentialConcurrencyToken = local.GetProperty("concurrencyToken").GetString(),
            });
        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        var responseBody = await reset.Content.ReadAsStringAsync();
        var logs = string.Join(
            Environment.NewLine,
            factory.LogSink.Entries.Where(entry => entry.Contains("SecurityEvent", StringComparison.Ordinal)));
        Assert.Contains("EventType=LocalPasswordResetByAdministrator", logs, StringComparison.Ordinal);
        Assert.Contains("Result=success", logs, StringComparison.Ordinal);
        Assert.Contains("ReasonCode=password_reset", logs, StringComparison.Ordinal);
        Assert.Contains("ActorUserId=", logs, StringComparison.Ordinal);
        Assert.Contains("TargetUserId=", logs, StringComparison.Ordinal);
        Assert.Contains("CredentialId=", logs, StringComparison.Ordinal);
        Assert.Contains("OccurredAt=", logs, StringComparison.Ordinal);
        Assert.Contains("CorrelationId=", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(NewTemporaryPassword, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SessionVersion", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(NewTemporaryPassword, responseBody, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", responseBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionVersion", responseBody, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Oidc_challenge_always_returns_to_dashboard_and_ignores_prior_account_route()
    {
        var controller = new AuthenticationController(
            Options.Create(new LocalAuthenticationOptions { Enabled = false }),
            Options.Create(new OidcAuthenticationOptions { Enabled = true }),
            null!);

        var result = Assert.IsType<ChallengeResult>(controller.Login());

        Assert.Equal("/dashboard", result.Properties?.RedirectUri);
        Assert.Equal("EnterpriseOidc", Assert.Single(result.AuthenticationSchemes));
    }

    private static async Task<(long UserId, string Username)> AddLocalUser(
        BootstrapWebApplicationFactory factory,
        bool isUserActive,
        bool isCredentialActive,
        bool locked = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var user = new User
        {
            DisplayName = $"AUTH-B04 用户 {suffix}",
            EmployeeNo = $"B04-{suffix}",
            IsActive = isUserActive,
            AccessLevel = AccessLevel.Viewer,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        var rawUsername = $"auth-b04-{suffix}";
        Assert.True(LocalCredentialSecurity.TryNormalizeUsername(rawUsername, out var username, out var normalizedUsername));
        var credential = new LocalLoginCredential
        {
            UserId = user.Id,
            Username = username,
            NormalizedUsername = normalizedUsername,
            IsActive = isCredentialActive,
            MustChangePassword = false,
            FailedLoginAttempts = locked ? 4 : 0,
            FailedLoginWindowStartedAt = locked ? timestamp.AddMinutes(-2) : null,
            LockedUntil = locked ? timestamp.AddMinutes(10) : null,
            SessionVersion = 1,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastPasswordChangedAt = timestamp,
            Version = 1,
        };
        credential.PasswordHash = scope.ServiceProvider.GetRequiredService<LocalPasswordService>()
            .Hash(credential, OldPassword);
        db.LocalLoginCredentials.Add(credential);
        await db.SaveChangesAsync();
        return (user.Id, username);
    }

    private static async Task<long> AddUser(BootstrapWebApplicationFactory factory, AccessLevel accessLevel)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var suffix = Guid.NewGuid().ToString("N")[..10];
        var user = new User
        {
            DisplayName = $"AUTH-B04 {accessLevel} {suffix}",
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<JsonElement> GetLocalProjection(HttpClient client, long userId)
    {
        using var response = await client.GetAsync($"/api/users/{userId}/login-methods");
        response.EnsureSuccessStatusCode();
        var methods = await response.Content.ReadFromJsonAsync<JsonElement>();
        return methods.GetProperty("local").Clone();
    }

    private static async Task<HttpClient> CreateLocalAdministratorClient(BootstrapWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await db.Users.SingleAsync(item => item.AccessLevel == AccessLevel.Administrator);
        var timestamp = DateTimeOffset.UtcNow;
        var rawUsername = $"auth-b04-admin-{Guid.NewGuid():N}";
        Assert.True(LocalCredentialSecurity.TryNormalizeUsername(rawUsername, out var username, out var normalizedUsername));
        var credential = new LocalLoginCredential
        {
            UserId = user.Id,
            Username = username,
            NormalizedUsername = normalizedUsername,
            IsActive = true,
            MustChangePassword = false,
            SessionVersion = 1,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastPasswordChangedAt = timestamp,
            Version = 1,
        };
        credential.PasswordHash = scope.ServiceProvider.GetRequiredService<LocalPasswordService>()
            .Hash(credential, AdministratorPassword);
        db.LocalLoginCredentials.Add(credential);
        await db.SaveChangesAsync();

        await AddAntiforgeryToken(client);
        using var login = await client.PostAsJsonAsync("/auth/local/login", new { username, password = AdministratorPassword });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        await AddAntiforgeryToken(client);
        return client;
    }

    private static async Task<HttpResponseMessage> LoginLocal(HttpClient client, string username, string password)
    {
        await AddAntiforgeryToken(client);
        return await client.PostAsJsonAsync("/auth/local/login", new { username, password });
    }

    private static async Task AddAntiforgeryToken(HttpClient client)
    {
        using var tokenResponse = await client.GetAsync("/api/antiforgery/token");
        Assert.Equal(HttpStatusCode.OK, tokenResponse.StatusCode);
        var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestToken").GetString();
        client.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-CSRF-TOKEN", token);
    }
}
