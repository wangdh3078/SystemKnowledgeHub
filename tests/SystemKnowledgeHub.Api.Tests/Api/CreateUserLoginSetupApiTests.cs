using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class CreateUserLoginSetupApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private const string InitialPassword = "AUTH-B02 exact password  空格";
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _administrator;

    public CreateUserLoginSetupApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _administrator = factory.CreateAuthenticatedClient();
    }

    [Theory]
    [InlineData("Viewer")]
    [InlineData("Editor")]
    [InlineData("Administrator")]
    public async Task Create_persists_and_projects_the_explicit_access_level(string accessLevel)
    {
        var suffix = Suffix();
        using var response = await PostUser(_administrator, suffix, new { type = "none" }, accessLevel: accessLevel);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        var userId = created.GetProperty("id").GetInt64();
        Assert.Equal(accessLevel, created.GetProperty("accessLevel").GetString());

        using var detailResponse = await _administrator.GetAsync($"/api/users/{userId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(accessLevel, detail.GetProperty("accessLevel").GetString());

        using var listResponse = await _administrator.GetAsync($"/api/users?keyword=EMP-{suffix}&sort=displayName%3Aasc&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(accessLevel, Assert.Single(list.GetProperty("items").EnumerateArray()).GetProperty("accessLevel").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(Enum.Parse<AccessLevel>(accessLevel), (await db.Users.SingleAsync(item => item.Id == userId)).AccessLevel);
    }

    [Fact]
    public async Task Create_rejects_an_unsupported_access_level()
    {
        using var response = await PostUser(_administrator, Suffix(), new { type = "none" }, accessLevel: "Owner");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var numeric = await PostUser(_administrator, Suffix(), new { type = "none" }, accessLevel: 99);
        Assert.Equal(HttpStatusCode.BadRequest, numeric.StatusCode);
    }

    [Fact]
    public async Task Local_create_persists_required_security_state_and_safe_projection()
    {
        var suffix = Suffix();
        var username = $"local-{suffix}";
        var roleId = await AddKnowledgeRole();
        using var response = await PostUser(_administrator, suffix, new
        {
            type = "local",
            username,
            initialPassword = InitialPassword,
        }, [roleId]);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var createBody = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain(InitialPassword, createBody, StringComparison.Ordinal);
        Assert.DoesNotContain("passwordHash", createBody, StringComparison.OrdinalIgnoreCase);
        using var createDocument = JsonDocument.Parse(createBody);
        var created = createDocument.RootElement;
        var userId = created.GetProperty("id").GetInt64();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await db.Users.SingleAsync(item => item.Id == userId);
        var credential = await db.LocalLoginCredentials.SingleAsync(item => item.UserId == userId);
        Assert.True(await db.UserKnowledgeRoles.AnyAsync(item => item.UserId == userId && item.KnowledgeRoleId == roleId));
        Assert.Equal(AccessLevel.Viewer, user.AccessLevel);
        Assert.True(credential.IsActive);
        Assert.True(credential.MustChangePassword);
        Assert.Equal(1, credential.SessionVersion);
        Assert.Equal(1, credential.Version);
        Assert.Equal(0, credential.FailedLoginAttempts);
        Assert.Null(credential.FailedLoginWindowStartedAt);
        Assert.Null(credential.LockedUntil);
        Assert.Equal(credential.CreatedAt, credential.LastPasswordChangedAt);
        Assert.DoesNotContain(InitialPassword, credential.PasswordHash, StringComparison.Ordinal);
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        Assert.NotEqual(PasswordVerificationResult.Failed, passwords.Verify(credential, credential.PasswordHash, InitialPassword));

        using var methodsResponse = await _administrator.GetAsync($"/api/users/{userId}/login-methods");
        Assert.Equal(HttpStatusCode.OK, methodsResponse.StatusCode);
        var body = await methodsResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain("passwordHash", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("sessionVersion", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(InitialPassword, body, StringComparison.Ordinal);
        using var methods = JsonDocument.Parse(body);
        Assert.True(methods.RootElement.GetProperty("local").GetProperty("exists").GetBoolean());
        Assert.Equal(username, methods.RootElement.GetProperty("local").GetProperty("username").GetString());
        Assert.True(methods.RootElement.GetProperty("local").GetProperty("mustChangePassword").GetBoolean());
        Assert.False(methods.RootElement.GetProperty("local").GetProperty("globallyEnabled").GetBoolean());
    }

    [Fact]
    public async Task Local_validation_and_duplicate_username_do_not_create_partial_users_or_echo_password()
    {
        var firstSuffix = Suffix();
        var username = $"duplicate-{firstSuffix}";
        using var created = await PostUser(_administrator, firstSuffix, new
        {
            type = "local",
            username,
            initialPassword = InitialPassword,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        var duplicateSuffix = Suffix();
        using var duplicate = await PostUser(_administrator, duplicateSuffix, new
        {
            type = "local",
            username = username.ToUpperInvariant(),
            initialPassword = InitialPassword,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        var duplicateBody = await duplicate.Content.ReadAsStringAsync();
        Assert.Contains("loginSetup.username", duplicateBody, StringComparison.Ordinal);
        Assert.DoesNotContain(InitialPassword, duplicateBody, StringComparison.Ordinal);

        using var invalidUsername = await PostUser(_administrator, Suffix(), new
        {
            type = "local",
            username = "bad username",
            initialPassword = InitialPassword,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidUsername.StatusCode);

        const string invalidPassword = "short";
        using var invalidPasswordResponse = await PostUser(_administrator, Suffix(), new
        {
            type = "local",
            username = $"valid-{Suffix()}",
            initialPassword = invalidPassword,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidPasswordResponse.StatusCode);
        Assert.DoesNotContain(invalidPassword, await invalidPasswordResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await db.Users.AnyAsync(item => item.EmployeeNo == $"EMP-{duplicateSuffix}"));
    }

    [Fact]
    public async Task Credential_or_identity_insert_failure_rolls_back_user_and_assignments()
    {
        await AssertMethodInsertFailureRollsBack(
            "local_login_credentials",
            new { type = "local", username = $"rollback-{Suffix()}", initialPassword = InitialPassword });
        await AssertMethodInsertFailureRollsBack(
            "login_identities",
            new { type = "oidc", provider = "TestOidc", subject = $"rollback-{Suffix()}" });
    }

    [Fact]
    public async Task Oidc_create_validates_allowlist_and_duplicate_mapping()
    {
        var subject = $"subject-{Suffix()}";
        using var created = await PostUser(_administrator, Suffix(), new
        {
            type = "oidc",
            provider = "TestOidc",
            subject,
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var userId = (await created.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var identity = await db.LoginIdentities.SingleAsync(item => item.UserId == userId);
            Assert.Equal("TestOidc", identity.Provider);
            Assert.Equal(subject, identity.Subject);
            Assert.True(identity.IsActive);
            Assert.Equal(1, identity.Version);
            Assert.False(await db.LocalLoginCredentials.AnyAsync(item => item.UserId == userId));
        }

        using var duplicate = await PostUser(_administrator, Suffix(), new
        {
            type = "oidc",
            provider = "TestOidc",
            subject,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var invalidProvider = await PostUser(_administrator, Suffix(), new
        {
            type = "oidc",
            provider = "UnapprovedProvider",
            subject = $"subject-{Suffix()}",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidProvider.StatusCode);
        Assert.Contains("loginSetup.provider", await invalidProvider.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var methodsResponse = await _administrator.GetAsync($"/api/users/{userId}/login-methods");
        var methods = await methodsResponse.Content.ReadFromJsonAsync<JsonElement>();
        var projectedIdentity = Assert.Single(methods.GetProperty("oidc").EnumerateArray());
        Assert.Equal("TestOidc", projectedIdentity.GetProperty("provider").GetString());
        Assert.Equal(subject, projectedIdentity.GetProperty("subject").GetString());
        Assert.True(projectedIdentity.GetProperty("globallyEnabled").GetBoolean());
    }

    [Fact]
    public async Task Oidc_disabled_with_provider_allows_preconfiguration_but_missing_provider_rejects_it()
    {
        using (var disabledFactory = new DisabledOidcWithProviderFactory())
        using (var client = await CreateLocalAdministratorClient(disabledFactory))
        {
            using var options = await client.GetAsync("/api/users/login-setup-options");
            var optionJson = await options.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(optionJson.GetProperty("oidcSetupAvailable").GetBoolean());
            Assert.False(optionJson.GetProperty("oidcGloballyEnabled").GetBoolean());

            using var created = await PostUser(client, Suffix(), new
            {
                type = "oidc",
                provider = "TestOidc",
                subject = $"disabled-{Suffix()}",
            });
            Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        }

        using (var noProviderFactory = new NoApprovedProviderFactory())
        using (var client = await CreateLocalAdministratorClient(noProviderFactory))
        {
            using var options = await client.GetAsync("/api/users/login-setup-options");
            var optionJson = await options.Content.ReadFromJsonAsync<JsonElement>();
            Assert.False(optionJson.GetProperty("oidcSetupAvailable").GetBoolean());

            using var rejected = await PostUser(client, Suffix(), new
            {
                type = "oidc",
                provider = "Anything",
                subject = $"missing-provider-{Suffix()}",
            });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }
    }

    [Fact]
    public async Task None_create_is_valid_and_creates_no_login_rows()
    {
        var suffix = Suffix();
        using var response = await PostUser(_administrator, suffix, new { type = "none" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var userId = (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await db.LocalLoginCredentials.AnyAsync(item => item.UserId == userId));
        Assert.False(await db.LoginIdentities.AnyAsync(item => item.UserId == userId));

        using var methodsResponse = await _administrator.GetAsync($"/api/users/{userId}/login-methods");
        var methods = await methodsResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(methods.GetProperty("local").GetProperty("exists").GetBoolean());
        Assert.Empty(methods.GetProperty("oidc").EnumerateArray());
    }

    [Fact]
    public async Task Create_and_login_method_projection_are_administrator_only()
    {
        var editorId = await AddUser(AccessLevel.Editor);
        var viewerId = await AddUser(AccessLevel.Viewer);
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);

        using var editorCreate = await PostUser(editor, Suffix(), new { type = "none" });
        using var viewerCreate = await PostUser(viewer, Suffix(), new { type = "none" });
        Assert.Equal(HttpStatusCode.Forbidden, editorCreate.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, viewerCreate.StatusCode);

        using var administratorCreate = await PostUser(_administrator, Suffix(), new { type = "none" });
        Assert.Equal(HttpStatusCode.Created, administratorCreate.StatusCode);
        var userId = (await administratorCreate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync($"/api/users/{userId}/login-methods")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync($"/api/users/{userId}/login-methods")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await _administrator.GetAsync($"/api/users/{userId}/login-methods")).StatusCode);
    }

    [Fact]
    public async Task Login_setup_is_required_and_none_rejects_credential_fields()
    {
        using var missing = await PostUser(_administrator, Suffix(), null);
        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Contains("loginSetup.type", await missing.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        using var invalidNone = await PostUser(_administrator, Suffix(), new
        {
            type = "none",
            initialPassword = InitialPassword,
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidNone.StatusCode);
        Assert.DoesNotContain(InitialPassword, await invalidNone.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Local_create_requires_antiforgery_and_rejected_request_creates_nothing()
    {
        var suffix = Suffix();
        using var client = _factory.CreateAuthenticatedClientWithoutAntiforgery();
        using var response = await PostUser(client, suffix, new
        {
            type = "local",
            username = $"antiforgery-{suffix}",
            initialPassword = InitialPassword,
        });
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("antiforgery_failed", body, StringComparison.Ordinal);
        Assert.DoesNotContain(InitialPassword, body, StringComparison.Ordinal);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await db.Users.AnyAsync(item => item.EmployeeNo == $"EMP-{suffix}"));
    }

    [Fact]
    public async Task Local_and_oidc_creation_emit_safe_security_audit_events()
    {
        using var factory = new DisabledOidcWithProviderFactory();
        using var client = await CreateLocalAdministratorClient(factory);

        using var local = await PostUser(client, Suffix(), new
        {
            type = "local",
            username = $"audited-{Suffix()}",
            initialPassword = InitialPassword,
        });
        Assert.Equal(HttpStatusCode.Created, local.StatusCode);

        using var oidc = await PostUser(client, Suffix(), new
        {
            type = "oidc",
            provider = "TestOidc",
            subject = $"audited-{Suffix()}",
        });
        Assert.Equal(HttpStatusCode.Created, oidc.StatusCode);

        var localAudit = Assert.Single(factory.LogSink.Entries.Where(entry =>
            entry.Contains("EventType=LocalCredentialCreated", StringComparison.Ordinal)));
        var oidcAudit = Assert.Single(factory.LogSink.Entries.Where(entry =>
            entry.Contains("EventType=LoginIdentityCreated", StringComparison.Ordinal)));
        foreach (var audit in new[] { localAudit, oidcAudit })
        {
            Assert.Contains("ActorUserId=", audit, StringComparison.Ordinal);
            Assert.Contains("TargetUserId=", audit, StringComparison.Ordinal);
            Assert.Contains("Result=success", audit, StringComparison.Ordinal);
            Assert.Contains("ReasonCode=created", audit, StringComparison.Ordinal);
            Assert.Contains("OccurredAt=", audit, StringComparison.Ordinal);
            Assert.Contains("CorrelationId=", audit, StringComparison.Ordinal);
        }

        var allLogs = string.Join(Environment.NewLine, factory.LogSink.Entries);
        Assert.DoesNotContain(InitialPassword, allLogs, StringComparison.Ordinal);
        Assert.DoesNotContain("PasswordHash", allLogs, StringComparison.OrdinalIgnoreCase);
    }

    private async Task AssertMethodInsertFailureRollsBack(string table, object setup)
    {
        var suffix = Suffix();
        var createTriggerSql = table switch
        {
            "local_login_credentials" => "CREATE TRIGGER auth_b02_fail_local BEFORE INSERT ON local_login_credentials BEGIN SELECT RAISE(FAIL, 'AUTH-B02 fixture failure'); END;",
            "login_identities" => "CREATE TRIGGER auth_b02_fail_oidc BEFORE INSERT ON login_identities BEGIN SELECT RAISE(FAIL, 'AUTH-B02 fixture failure'); END;",
            _ => throw new ArgumentOutOfRangeException(nameof(table)),
        };
        var dropTriggerSql = table == "local_login_credentials"
            ? "DROP TRIGGER IF EXISTS auth_b02_fail_local;"
            : "DROP TRIGGER IF EXISTS auth_b02_fail_oidc;";
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        await db.Database.ExecuteSqlRawAsync(createTriggerSql);
        try
        {
            using var response = await PostUser(_administrator, suffix, setup);
            Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
            db.ChangeTracker.Clear();
            Assert.False(await db.Users.AnyAsync(item => item.EmployeeNo == $"EMP-{suffix}"));
        }
        finally
        {
            await db.Database.ExecuteSqlRawAsync(dropTriggerSql);
        }
    }

    private static Task<HttpResponseMessage> PostUser(
        HttpClient client,
        string suffix,
        object? loginSetup,
        IReadOnlyList<long>? roleIds = null,
        object? accessLevel = null) =>
        client.PostAsJsonAsync("/api/users", new
        {
            employeeNo = $"EMP-{suffix}",
            displayName = $"AUTH-B02 用户 {suffix}",
            email = $"auth-b02-{suffix}@example.test",
            departmentOrTeam = "安全平台组",
            jobTitle = "知识工程师",
            accessLevel = accessLevel ?? "Viewer",
            knowledgeRoleIds = roleIds ?? Array.Empty<long>(),
            loginSetup,
            actor = new { displayName = "AUTH-B02 管理员", role = "系统管理员" },
        });

    private async Task<long> AddKnowledgeRole()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var role = new KnowledgeRole
        {
            Name = $"AUTH-B02 知识身份 {Suffix()}",
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.KnowledgeRoles.Add(role);
        await db.SaveChangesAsync();
        return role.Id;
    }

    private async Task<long> AddUser(AccessLevel accessLevel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"AUTH-B02 {accessLevel} {Suffix()}",
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<HttpClient> CreateLocalAdministratorClient(BootstrapWebApplicationFactory factory)
    {
        var client = factory.CreateClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await db.Users.FirstAsync(item => item.AccessLevel == AccessLevel.Administrator);
        var now = DateTimeOffset.UtcNow;
        var rawUsername = $"auth-b02-admin-{Suffix()}";
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
            .Hash(credential, InitialPassword);
        db.LocalLoginCredentials.Add(credential);
        await db.SaveChangesAsync();

        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.AuthMethodHeader, AuthenticationClaims.LocalMethod);
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.AuthIdentityHeader, credential.Id.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.AuthVersionHeader, credential.SessionVersion.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.UserHeader, user.Id.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(TestAuthenticationHandler.AccessLevelHeader, AccessLevel.Administrator.ToString());
        using var tokenResponse = await client.GetAsync("/api/antiforgery/token");
        var token = (await tokenResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("requestToken").GetString();
        client.DefaultRequestHeaders.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        return client;
    }

    private static string Suffix() => Guid.NewGuid().ToString("N")[..10];

    private sealed class DisabledOidcWithProviderFactory : BootstrapWebApplicationFactory
    {
        public TestLogSink LogSink { get; } = new();

        protected override void ConfigureAuthenticationMode(IWebHostBuilder builder)
        {
            builder.UseSetting("Authentication:Local:Enabled", "true");
            builder.UseSetting("Authentication:Oidc:Enabled", "false");
            builder.UseSetting("Authentication:Oidc:Provider", "TestOidc");
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.UseIsolatedTestSerilog(LogFilePath, LogSink));
        }
    }

    private sealed class NoApprovedProviderFactory : BootstrapWebApplicationFactory
    {
        protected override void ConfigureAuthenticationMode(IWebHostBuilder builder)
        {
            builder.UseSetting("Authentication:Local:Enabled", "true");
            builder.UseSetting("Authentication:Oidc:Enabled", "false");
            builder.UseSetting("Authentication:Oidc:Provider", string.Empty);
        }
    }
}
