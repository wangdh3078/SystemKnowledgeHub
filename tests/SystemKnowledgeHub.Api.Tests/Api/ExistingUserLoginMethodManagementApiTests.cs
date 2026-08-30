using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class ExistingUserLoginMethodManagementApiTests
{
    private const string InitialPassword = "AUTH-B03 exact canary 密码  空格";
    private const string AdministratorPassword = "AUTH-B03 administrator password";

    [Fact]
    public async Task Existing_active_user_adds_local_and_initial_password_enters_must_change_gate()
    {
        using var factory = new LocalLoginWebApplicationFactory();
        using var administrator = await CreateLocalAdministratorClient(factory);
        var userId = await AddUser(factory, isActive: true);

        using var created = await administrator.PostAsJsonAsync($"/api/users/{userId}/local-credential", new
        {
            username = $"existing-{Suffix()}",
            initialPassword = InitialPassword,
        });

        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var body = await created.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InitialPassword, body, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionVersion", body, StringComparison.OrdinalIgnoreCase);
        var projection = JsonDocument.Parse(body).RootElement;
        var username = projection.GetProperty("username").GetString();
        Assert.True(projection.GetProperty("isActive").GetBoolean());
        Assert.True(projection.GetProperty("mustChangePassword").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(projection.GetProperty("concurrencyToken").GetString()));

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var credential = await db.LocalLoginCredentials.SingleAsync(item => item.UserId == userId);
            Assert.Equal(1, credential.SessionVersion);
            Assert.Equal(1, credential.Version);
            Assert.Equal(0, credential.FailedLoginAttempts);
            Assert.Null(credential.FailedLoginWindowStartedAt);
            Assert.Null(credential.LockedUntil);
            Assert.Equal(credential.CreatedAt, credential.LastPasswordChangedAt);
            Assert.DoesNotContain(InitialPassword, credential.PasswordHash, StringComparison.Ordinal);
            var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
            Assert.NotEqual(PasswordVerificationResult.Failed, passwords.Verify(credential, credential.PasswordHash, InitialPassword));
        }

        using var localUser = factory.CreateClient();
        using var login = await LoginLocal(localUser, username!, InitialPassword);
        Assert.Equal(HttpStatusCode.NoContent, login.StatusCode);
        using var current = await localUser.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.OK, current.StatusCode);
        var currentUser = await current.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(currentUser.GetProperty("mustChangePassword").GetBoolean());
        Assert.Equal("local", currentUser.GetProperty("authenticationMethod").GetString());
    }

    [Fact]
    public async Task Inactive_user_can_be_prepared_but_cannot_authenticate_and_global_disable_remains_authoritative()
    {
        using (var factory = new LocalLoginWebApplicationFactory())
        using (var administrator = await CreateLocalAdministratorClient(factory))
        {
            var inactiveUserId = await AddUser(factory, isActive: false);
            var username = $"inactive-{Suffix()}";
            using var created = await AddLocal(administrator, inactiveUserId, username);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            using var inactiveClient = factory.CreateClient();
            using var login = await LoginLocal(inactiveClient, username, InitialPassword);
            Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        }

        using (var disabledFactory = new BootstrapWebApplicationFactory())
        {
            using var administrator = disabledFactory.CreateAuthenticatedClient();
            var userId = await AddUser(disabledFactory, isActive: true);
            var username = $"global-disabled-{Suffix()}";
            using var created = await AddLocal(administrator, userId, username);
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var projection = await created.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(projection.GetProperty("globallyEnabled").GetBoolean());
            using var disabledClient = disabledFactory.CreateClient();
            using var login = await LoginLocal(disabledClient, username, InitialPassword);
            Assert.Equal(HttpStatusCode.NotFound, login.StatusCode);
        }
    }

    [Fact]
    public async Task Duplicate_username_and_concurrent_same_user_creation_return_safe_conflicts()
    {
        using var factory = new ConcurrentUserManagementFactory();
        using var firstAdministrator = factory.CreateAuthenticatedClient();
        using var secondAdministrator = factory.CreateAuthenticatedClient();
        var firstUserId = await AddUser(factory, isActive: true);
        var secondUserId = await AddUser(factory, isActive: true);
        var duplicateUsername = $"duplicate-{Suffix()}";
        using var first = await AddLocal(firstAdministrator, firstUserId, duplicateUsername);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await AddLocal(secondAdministrator, secondUserId, duplicateUsername.ToUpperInvariant());
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("username", duplicateBody, StringComparison.Ordinal);
        Assert.DoesNotContain(InitialPassword, duplicateBody, StringComparison.Ordinal);

        var concurrentUserId = await AddUser(factory, isActive: true);
        var attempts = await Task.WhenAll(
            AddLocal(firstAdministrator, concurrentUserId, $"parallel-a-{Suffix()}"),
            AddLocal(secondAdministrator, concurrentUserId, $"parallel-b-{Suffix()}"));
        try
        {
            Assert.Equal(1, attempts.Count(response => response.StatusCode == HttpStatusCode.Created));
            Assert.Equal(1, attempts.Count(response => response.StatusCode == HttpStatusCode.Conflict));
        }
        finally
        {
            foreach (var response in attempts) response.Dispose();
        }
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(1, await db.LocalLoginCredentials.CountAsync(item => item.UserId == concurrentUserId));
    }

    [Fact]
    public async Task Add_and_state_management_are_administrator_only_and_require_antiforgery()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var userId = await AddUser(factory, isActive: true);
        var editorId = await AddUser(factory, isActive: true, accessLevel: AccessLevel.Editor);
        var viewerId = await AddUser(factory, isActive: true, accessLevel: AccessLevel.Viewer);
        using var administrator = factory.CreateAuthenticatedClient();
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);
        using var missingAntiforgery = factory.CreateAuthenticatedClientWithoutAntiforgery();

        using var editorResult = await AddLocal(editor, userId, $"editor-{Suffix()}");
        using var viewerResult = await AddLocal(viewer, userId, $"viewer-{Suffix()}");
        using var antiforgeryResult = await AddLocal(missingAntiforgery, userId, $"csrf-{Suffix()}");
        Assert.Equal(HttpStatusCode.Forbidden, editorResult.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewerResult.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, antiforgeryResult.StatusCode);
        Assert.Contains("antiforgery_failed", await antiforgeryResult.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var created = await AddLocal(administrator, userId, $"authorized-{Suffix()}");
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var token = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("concurrencyToken").GetString();
        using var editorState = await editor.PutAsJsonAsync($"/api/users/{userId}/local-credential/active-state", new { isActive = false, concurrencyToken = token });
        using var viewerState = await viewer.PutAsJsonAsync($"/api/users/{userId}/local-credential/active-state", new { isActive = false, concurrencyToken = token });
        using var antiforgeryState = await missingAntiforgery.PutAsJsonAsync($"/api/users/{userId}/local-credential/active-state", new { isActive = false, concurrencyToken = token });
        Assert.Equal(HttpStatusCode.Forbidden, editorState.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewerState.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, antiforgeryState.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.True((await db.LocalLoginCredentials.SingleAsync(item => item.UserId == userId)).IsActive);
    }

    [Fact]
    public async Task Disable_invalidates_local_sessions_stale_token_conflicts_and_enable_restores_login()
    {
        using var factory = new LocalLoginWebApplicationFactory();
        using var administrator = await CreateLocalAdministratorClient(factory);
        var userId = await AddUser(factory, isActive: true);
        var username = $"toggle-{Suffix()}";
        using var created = await AddLocal(administrator, userId, username);
        var createdProjection = await created.Content.ReadFromJsonAsync<JsonElement>();
        var firstToken = createdProjection.GetProperty("concurrencyToken").GetString();

        using var existingSession = factory.CreateClient();
        using var initialLogin = await LoginLocal(existingSession, username, InitialPassword);
        Assert.Equal(HttpStatusCode.NoContent, initialLogin.StatusCode);
        using var disabled = await administrator.PutAsJsonAsync($"/api/users/{userId}/local-credential/active-state", new { isActive = false, concurrencyToken = firstToken });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        var disabledProjection = await disabled.Content.ReadFromJsonAsync<JsonElement>();
        var secondToken = disabledProjection.GetProperty("concurrencyToken").GetString();
        Assert.False(disabledProjection.GetProperty("isActive").GetBoolean());

        using var expired = await existingSession.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.Unauthorized, expired.StatusCode);
        using var rejectedClient = factory.CreateClient();
        using var rejectedLogin = await LoginLocal(rejectedClient, username, InitialPassword);
        Assert.Equal(HttpStatusCode.Unauthorized, rejectedLogin.StatusCode);

        using var stale = await administrator.PutAsJsonAsync($"/api/users/{userId}/local-credential/active-state", new { isActive = true, concurrencyToken = firstToken });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var staleBody = await stale.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("LocalLoginCredential", staleBody.GetProperty("details").GetProperty("resourceType").GetString());

        using var enabled = await administrator.PutAsJsonAsync($"/api/users/{userId}/local-credential/active-state", new { isActive = true, concurrencyToken = secondToken });
        Assert.Equal(HttpStatusCode.OK, enabled.StatusCode);
        using var restoredClient = factory.CreateClient();
        using var restoredLogin = await LoginLocal(restoredClient, username, InitialPassword);
        Assert.Equal(HttpStatusCode.NoContent, restoredLogin.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var credential = await db.LocalLoginCredentials.SingleAsync(item => item.UserId == userId);
        Assert.True(credential.IsActive);
        Assert.Equal(3, credential.SessionVersion);
        Assert.Equal(3, credential.Version);
        Assert.True((await db.Users.SingleAsync(item => item.Id == userId)).IsActive);
    }

    [Fact]
    public async Task Last_usable_administrator_guard_and_local_oidc_coexistence_remain_method_scoped()
    {
        using (var localOnly = new LocalLoginWebApplicationFactory())
        using (var administrator = await CreateLocalAdministratorClient(localOnly))
        {
            long administratorId;
            await using (var scope = localOnly.Services.CreateAsyncScope())
            {
                administratorId = await scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>()
                    .Users.Where(item => item.AccessLevel == AccessLevel.Administrator).Select(item => item.Id).SingleAsync();
            }
            var local = await GetLocalProjection(administrator, administratorId);
            using var rejected = await administrator.PutAsJsonAsync($"/api/users/{administratorId}/local-credential/active-state", new
            {
                isActive = false,
                concurrencyToken = local.GetProperty("concurrencyToken").GetString(),
            });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
            var error = await rejected.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("last_usable_administrator", error.GetProperty("details").GetProperty("reason").GetString());
        }

        using (var coexistence = new EnabledLocalOidcFactory())
        using (var administrator = coexistence.CreateAuthenticatedClient())
        {
            var userId = await AddUser(coexistence, isActive: true, addOidcIdentity: true);
            using var created = await AddLocal(administrator, userId, $"coexist-{Suffix()}");
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
            var local = await created.Content.ReadFromJsonAsync<JsonElement>();
            using var disabled = await administrator.PutAsJsonAsync($"/api/users/{userId}/local-credential/active-state", new
            {
                isActive = false,
                concurrencyToken = local.GetProperty("concurrencyToken").GetString(),
            });
            Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
            await using var scope = coexistence.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.False((await db.LocalLoginCredentials.SingleAsync(item => item.UserId == userId)).IsActive);
            Assert.True((await db.LoginIdentities.SingleAsync(item => item.UserId == userId)).IsActive);
        }
    }

    [Fact]
    public async Task Existing_oidc_management_uses_approved_provider_and_does_not_infer_subject()
    {
        using var factory = new EnabledLocalOidcFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var userId = await AddUser(factory, isActive: true);
        using var invalid = await administrator.PostAsJsonAsync($"/api/users/{userId}/login-identities", new
        {
            provider = "UnapprovedProvider",
            subject = "not-inferred@example.test",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var valid = await administrator.PostAsJsonAsync($"/api/users/{userId}/login-identities", new
        {
            provider = "TestOidc",
            subject = "explicit-subject-" + Suffix(),
        });
        Assert.Equal(HttpStatusCode.Created, valid.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var identity = await db.LoginIdentities.SingleAsync(item => item.UserId == userId);
        Assert.Equal("TestOidc", identity.Provider);
        Assert.NotEqual((await db.Users.SingleAsync(item => item.Id == userId)).Email, identity.Subject);
        Assert.False(await db.LocalLoginCredentials.AnyAsync(item => item.UserId == userId));
    }

    [Fact]
    public async Task Structured_audit_is_complete_and_password_hash_and_session_version_never_leak()
    {
        using var factory = new AuditedEnabledLocalOidcFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var userId = await AddUser(factory, isActive: true);
        using var created = await AddLocal(administrator, userId, $"audit-{Suffix()}");
        var createdBody = await created.Content.ReadAsStringAsync();
        var local = JsonDocument.Parse(createdBody).RootElement;
        using var disabled = await administrator.PutAsJsonAsync($"/api/users/{userId}/local-credential/active-state", new
        {
            isActive = false,
            concurrencyToken = local.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        using var oidc = await administrator.PostAsJsonAsync($"/api/users/{userId}/login-identities", new
        {
            provider = "TestOidc",
            subject = "audit-subject-" + Suffix(),
        });
        Assert.Equal(HttpStatusCode.Created, oidc.StatusCode);

        var logs = string.Join(Environment.NewLine, factory.LogSink.Entries);
        Assert.Contains("EventType=LocalCredentialCreated", logs, StringComparison.Ordinal);
        Assert.Contains("EventType=LocalCredentialDisabled", logs, StringComparison.Ordinal);
        Assert.Contains("EventType=LoginIdentityCreated", logs, StringComparison.Ordinal);
        Assert.Contains("ActorUserId=", logs, StringComparison.Ordinal);
        Assert.Contains("TargetUserId=", logs, StringComparison.Ordinal);
        Assert.Contains("Result=success", logs, StringComparison.Ordinal);
        Assert.Contains("ReasonCode=", logs, StringComparison.Ordinal);
        Assert.Contains("OccurredAt=", logs, StringComparison.Ordinal);
        Assert.Contains("CorrelationId=", logs, StringComparison.Ordinal);
        Assert.DoesNotContain(InitialPassword, logs, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("SessionVersion", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InitialPassword, createdBody, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", createdBody, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionVersion", createdBody, StringComparison.OrdinalIgnoreCase);
    }

    private static Task<HttpResponseMessage> AddLocal(HttpClient client, long userId, string username) =>
        client.PostAsJsonAsync($"/api/users/{userId}/local-credential", new
        {
            username,
            initialPassword = InitialPassword,
        });

    private static async Task<JsonElement> GetLocalProjection(HttpClient client, long userId)
    {
        using var response = await client.GetAsync($"/api/users/{userId}/login-methods");
        response.EnsureSuccessStatusCode();
        var methods = await response.Content.ReadFromJsonAsync<JsonElement>();
        return methods.GetProperty("local").Clone();
    }

    private static async Task<long> AddUser(
        BootstrapWebApplicationFactory factory,
        bool isActive,
        AccessLevel accessLevel = AccessLevel.Viewer,
        bool addOidcIdentity = false)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var suffix = Suffix();
        var user = new User
        {
            DisplayName = $"AUTH-B03 用户 {suffix}",
            EmployeeNo = $"B03-{suffix}",
            Email = $"auth-b03-{suffix}@example.test",
            IsActive = isActive,
            AccessLevel = accessLevel,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        if (addOidcIdentity)
        {
            db.LoginIdentities.Add(new LoginIdentity
            {
                UserId = user.Id,
                Provider = "TestOidc",
                Subject = $"auth-b03-subject-{suffix}",
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1,
            });
            await db.SaveChangesAsync();
        }
        return user.Id;
    }

    private static async Task<HttpClient> CreateLocalAdministratorClient(LocalLoginWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await db.Users.SingleAsync(item => item.AccessLevel == AccessLevel.Administrator);
        var now = DateTimeOffset.UtcNow;
        var rawUsername = $"auth-b03-admin-{Suffix()}";
        Assert.True(LocalCredentialSecurity.TryNormalizeUsername(rawUsername, out var username, out var normalizedUsername));
        var credential = new LocalLoginCredential
        {
            UserId = user.Id,
            Username = username,
            NormalizedUsername = normalizedUsername,
            IsActive = true,
            MustChangePassword = false,
            SessionVersion = 1,
            CreatedAt = now,
            UpdatedAt = now,
            LastPasswordChangedAt = now,
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

    private static string Suffix() => Guid.NewGuid().ToString("N")[..10];

    private class EnabledLocalOidcFactory : BootstrapWebApplicationFactory
    {
        protected override void ConfigureAuthenticationMode(IWebHostBuilder builder)
        {
            builder.UseSetting("Authentication:Local:Enabled", "true");
            builder.UseSetting("Authentication:Oidc:Enabled", "true");
            builder.UseSetting("Authentication:Oidc:Provider", "TestOidc");
            builder.UseSetting("Authentication:Oidc:Authority", "https://test-oidc.invalid");
            builder.UseSetting("Authentication:Oidc:ClientId", "auth-b03-tests");
        }
    }

    private sealed class AuditedEnabledLocalOidcFactory : EnabledLocalOidcFactory
    {
        public TestLogSink LogSink { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.UseIsolatedTestSerilog(LogFilePath, LogSink));
        }
    }

    private sealed class ConcurrentUserManagementFactory : BootstrapWebApplicationFactory
    {
        private readonly SqliteConnection anchor;
        private readonly string connectionString;

        public ConcurrentUserManagementFactory()
        {
            connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = $"SystemKnowledgeHubAuthB03-{Guid.NewGuid():N}",
                Mode = SqliteOpenMode.Memory,
                Cache = SqliteCacheMode.Shared,
                ForeignKeys = true,
                DefaultTimeout = 5,
                Pooling = false,
            }.ToString();
            anchor = new SqliteConnection(connectionString);
            anchor.Open();
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<KnowledgeHubDbContext>>();
                services.AddDbContext<KnowledgeHubDbContext>(options => options.UseSqlite(connectionString));
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing) anchor.Dispose();
        }
    }
}
