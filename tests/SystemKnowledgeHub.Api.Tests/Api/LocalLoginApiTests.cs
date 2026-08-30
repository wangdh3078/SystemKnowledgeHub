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

public sealed class LocalLoginApiTests : IClassFixture<LocalLoginWebApplicationFactory>
{
    private readonly LocalLoginWebApplicationFactory _factory;

    public LocalLoginApiTests(LocalLoginWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Local_login_uses_antiforgery_issues_the_application_cookie_and_resolves_current_user()
    {
        var (username, password, userId) = await CreateCredentialAsync(AccessLevel.Administrator);
        using var client = CreateCookieClient();
        await AddAntiforgeryTokenAsync(client);

        using var login = await client.PostAsJsonAsync("/auth/local/login", new { username, password });

        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var currentUser = await client.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.OK, currentUser.StatusCode);
        var profile = await currentUser.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(userId, profile.GetProperty("id").GetInt64());
        Assert.Equal("Administrator", profile.GetProperty("accessLevel").GetString());
        Assert.Equal("local", profile.GetProperty("authenticationMethod").GetString());
        Assert.False(profile.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Local_login_requires_antiforgery_and_keeps_public_failures_generic()
    {
        var (username, password, _) = await CreateCredentialAsync(AccessLevel.Viewer);
        using var noTokenClient = CreateCookieClient();
        using var missingToken = await noTokenClient.PostAsJsonAsync("/auth/local/login", new { username, password });
        Assert.Equal(HttpStatusCode.Forbidden, missingToken.StatusCode);
        Assert.Equal("antiforgery_failed", await ErrorCode(missingToken));

        using var invalidCredentialsClient = CreateCookieClient();
        await AddAntiforgeryTokenAsync(invalidCredentialsClient);
        using var wrongPassword = await invalidCredentialsClient.PostAsJsonAsync("/auth/local/login", new { username, password = "wrong password that is intentionally long" });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal("invalid_credentials", await ErrorCode(wrongPassword));
    }

    [Fact]
    public async Task Local_session_rejects_disabled_or_replaced_credential_version_without_reviving_after_reenable()
    {
        var (username, password, _) = await CreateCredentialAsync(AccessLevel.Editor);
        using var client = CreateCookieClient();
        await AddAntiforgeryTokenAsync(client);
        using var login = await client.PostAsJsonAsync("/auth/local/login", new { username, password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var credential = await dbContext.LocalLoginCredentials.SingleAsync(item => item.Username == username);
            credential.IsActive = false;
            credential.SessionVersion += 1;
            credential.UpdatedAt = DateTimeOffset.UtcNow;
            credential.Version += 1;
            await dbContext.SaveChangesAsync();
            credential.IsActive = true;
            credential.UpdatedAt = DateTimeOffset.UtcNow;
            credential.Version += 1;
            await dbContext.SaveChangesAsync();
        }

        using var response = await client.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("session_expired", await ErrorCode(response));
    }

    [Fact]
    public async Task Local_sessions_enter_the_existing_viewer_editor_and_administrator_authorization_policies()
    {
        var viewer = await CreateCredentialAsync(AccessLevel.Viewer);
        using var viewerClient = await LoginAsync(viewer.Username, viewer.Password);
        using var viewerRead = await viewerClient.GetAsync("/api/systems?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, viewerRead.StatusCode);
        using var viewerWrite = await viewerClient.PostAsJsonAsync("/api/systems", SystemRequest("viewer"));
        Assert.Equal(HttpStatusCode.Forbidden, viewerWrite.StatusCode);

        var editor = await CreateCredentialAsync(AccessLevel.Editor);
        using var editorClient = await LoginAsync(editor.Username, editor.Password);
        using var editorWrite = await editorClient.PostAsJsonAsync("/api/systems", SystemRequest("editor"));
        Assert.Equal(HttpStatusCode.Created, editorWrite.StatusCode);

        var administrator = await CreateCredentialAsync(AccessLevel.Administrator);
        using var administratorClient = await LoginAsync(administrator.Username, administrator.Password);
        using var administratorRead = await administratorClient.GetAsync("/api/users?page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, administratorRead.StatusCode);
    }

    [Fact]
    public async Task Local_credential_database_constraints_enforce_one_user_binding_and_normalized_username_uniqueness()
    {
        var (username, _, userId) = await CreateCredentialAsync(AccessLevel.Viewer);
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        var timestamp = DateTimeOffset.UtcNow;
        var secondForSameUser = Credential(userId, "another-local-name", "ANOTHER-LOCAL-NAME", timestamp, passwords);
        dbContext.LocalLoginCredentials.Add(secondForSameUser);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        var otherUser = new User
        {
            DisplayName = "重复用户名测试用户",
            IsActive = true,
            AccessLevel = AccessLevel.Viewer,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.Users.Add(otherUser);
        await dbContext.SaveChangesAsync();
        var duplicateNormalized = Credential(otherUser.Id, username.ToUpperInvariant(), username.ToUpperInvariant(), timestamp, passwords);
        dbContext.LocalLoginCredentials.Add(duplicateNormalized);
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }

    [Fact]
    public async Task Authentication_options_exposes_only_the_enabled_method_flags_and_friendly_oidc_label()
    {
        using var oidcOnly = new AuthenticationOptionsWebApplicationFactory(false, true);
        using var oidcResponse = await oidcOnly.CreateClient().GetAsync("/api/auth/options");
        Assert.Equal(HttpStatusCode.OK, oidcResponse.StatusCode);
        Assert.Equal("no-store", oidcResponse.Headers.CacheControl?.ToString());
        var oidcOptions = await oidcResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(oidcOptions.GetProperty("localLoginEnabled").GetBoolean());
        Assert.True(oidcOptions.GetProperty("oidcLoginEnabled").GetBoolean());
        Assert.Equal("使用企业账号登录", oidcOptions.GetProperty("oidcDisplayName").GetString());
        using var disabledLocalLogin = await oidcOnly.CreateClient().PostAsJsonAsync("/auth/local/login", new { username = "local-user", password = "unused password that is sufficiently long" });
        Assert.Equal(HttpStatusCode.NotFound, disabledLocalLogin.StatusCode);

        using var both = new AuthenticationOptionsWebApplicationFactory(true, true);
        using var bothResponse = await both.CreateClient().GetAsync("/api/auth/options");
        var bothOptions = await bothResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(bothOptions.GetProperty("localLoginEnabled").GetBoolean());
        Assert.True(bothOptions.GetProperty("oidcLoginEnabled").GetBoolean());
        Assert.False(bothOptions.TryGetProperty("authority", out _));
        Assert.False(bothOptions.TryGetProperty("clientId", out _));
    }

    [Fact]
    public async Task Failed_login_window_locks_the_credential_without_extending_the_lock_on_later_attempts()
    {
        var (username, _, _) = await CreateCredentialAsync(AccessLevel.Viewer);
        using var client = CreateCookieClient();
        await AddAntiforgeryTokenAsync(client);
        for (var attempt = 0; attempt < 5; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/auth/local/login", new { username, password = "wrong password that is intentionally long" });
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("invalid_credentials", await ErrorCode(response));
        }

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var credential = await dbContext.LocalLoginCredentials.SingleAsync(item => item.Username == username);
        Assert.Equal(5, credential.FailedLoginAttempts);
        Assert.NotNull(credential.LockedUntil);
        var lockedUntil = credential.LockedUntil;
        using var lockedAttempt = await client.PostAsJsonAsync("/auth/local/login", new { username, password = "wrong password that is intentionally long" });
        Assert.Equal(HttpStatusCode.Unauthorized, lockedAttempt.StatusCode);
        await dbContext.Entry(credential).ReloadAsync();
        Assert.Equal(lockedUntil, credential.LockedUntil);
    }

    [Fact]
    public async Task Local_login_endpoint_uses_the_named_ip_rate_limiter_and_shared_error_contract()
    {
        using var rateLimitedFactory = new RateLimitedLocalLoginWebApplicationFactory();
        using var client = rateLimitedFactory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = true });
        await AddAntiforgeryTokenAsync(client);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var allowed = await client.PostAsJsonAsync("/auth/local/login", new { username = "missing-user", password = "wrong password that is intentionally long" });
            Assert.Equal(HttpStatusCode.Unauthorized, allowed.StatusCode);
        }
        using var rejected = await client.PostAsJsonAsync("/auth/local/login", new { username = "missing-user", password = "wrong password that is intentionally long" });
        Assert.Equal(HttpStatusCode.TooManyRequests, rejected.StatusCode);
        Assert.Equal("too_many_requests", await ErrorCode(rejected));
    }

    private HttpClient CreateCookieClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        HandleCookies = true,
    });

    private async Task<HttpClient> LoginAsync(string username, string password)
    {
        var client = CreateCookieClient();
        await AddAntiforgeryTokenAsync(client);
        using var login = await client.PostAsJsonAsync("/auth/local/login", new { username, password });
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        await AddAntiforgeryTokenAsync(client);
        return client;
    }

    private static object SystemRequest(string suffix) => new
    {
        name = $"LOCAL-{suffix}-{Guid.NewGuid():N}",
        displayName = "本地登录授权测试系统",
        systemType = "Quality Management System",
        lifecycle = "Running",
        purpose = "验证现有 Editor policy。",
        actor = new { displayName = "授权测试", role = "知识整理人员" },
    };

    private async Task<(string Username, string Password, long UserId)> CreateCredentialAsync(AccessLevel accessLevel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"本地登录测试 {suffix}",
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var password = $"本地密码 {suffix} ! 长度充足";
        var credential = Credential(user.Id, $"local-{suffix}", $"LOCAL-{suffix}".ToUpperInvariant(), timestamp, passwords, password);
        dbContext.LocalLoginCredentials.Add(credential);
        await dbContext.SaveChangesAsync();
        return (credential.Username, password, user.Id);
    }

    private static LocalLoginCredential Credential(
        long userId,
        string username,
        string normalizedUsername,
        DateTimeOffset timestamp,
        LocalPasswordService passwords,
        string password = "安全且足够长的本地测试密码 2026!")
    {
        var credential = new LocalLoginCredential
        {
            UserId = userId,
            Username = username,
            NormalizedUsername = normalizedUsername,
            IsActive = true,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastPasswordChangedAt = timestamp,
            SessionVersion = 1,
            Version = 1,
        };
        credential.PasswordHash = passwords.Hash(credential, password);
        return credential;
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
}
