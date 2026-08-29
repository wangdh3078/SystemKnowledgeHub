using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.DataProtection;
using System.Text.Json;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Persistence;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public class BootstrapWebApplicationFactory : WebApplicationFactory<Program>
{
    private readonly SqliteConnection _connection;
    private readonly string _attachmentStorageRoot;
    private long _defaultLoginIdentityId;
    private long _defaultUserId;

    public BootstrapWebApplicationFactory()
    {
        _attachmentStorageRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "attachments",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_attachmentStorageRoot);
        _connection = new SqliteConnection(
            "Data Source=:memory:;Foreign Keys=True;Default Timeout=5");
        _connection.Open();

        using var command = _connection.CreateCommand();
        command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;";
        command.ExecuteNonQuery();
    }

    protected virtual bool UsesTestAuthentication => true;
    protected virtual string TestEnvironmentName => "Testing";

    protected virtual void ConfigureAuthenticationMode(IWebHostBuilder builder)
    {
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        ConfigureAuthenticationMode(builder);
        builder.UseEnvironment(TestEnvironmentName);
        builder.UseSetting("Attachments:StorageRoot", _attachmentStorageRoot);
        builder.ConfigureServices(services =>
        {
            services.AddDataProtection().UseEphemeralDataProtectionProvider();
            services.RemoveAll<DbContextOptions<KnowledgeHubDbContext>>();
            services.AddDbContext<KnowledgeHubDbContext>(options =>
                options.UseSqlite(_connection));
            if (UsesTestAuthentication)
            {
                services
                    .AddAuthentication(options =>
                    {
                        options.DefaultAuthenticateScheme = TestAuthenticationHandler.TestScheme;
                        options.DefaultChallengeScheme = TestAuthenticationHandler.TestScheme;
                    })
                    .AddScheme<AuthenticationSchemeOptions, TestAuthenticationHandler>(
                        TestAuthenticationHandler.TestScheme,
                        _ => { });
            }
        });
    }

    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);
        using var scope = host.Services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        dbContext.Database.Migrate();
        DatabaseKnowledgeDevelopmentData.SeedAsync(dbContext).GetAwaiter().GetResult();
        BusinessFunctionDevelopmentData.SeedAsync(dbContext).GetAwaiter().GetResult();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = "SEC-01 Test Principal",
            EmployeeNo = "SEC01-TEST-PRINCIPAL",
            Email = "sec01-test-principal@example.test",
            IsActive = true,
            AccessLevel = AccessLevel.Administrator,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.Users.Add(user);
        dbContext.SaveChanges();
        var identity = new LoginIdentity
        {
            UserId = user.Id,
            Provider = "TestOidc",
            Subject = "sec-01-test-principal",
            IsActive = true,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.LoginIdentities.Add(identity);
        dbContext.SaveChanges();
        _defaultUserId = user.Id;
        _defaultLoginIdentityId = identity.Id;
        return host;
    }

    public HttpClient CreateAuthenticatedClient()
    {
        var client = CreateClient();
        ConfigureAuthenticationHeaders(client, _defaultLoginIdentityId, 1, _defaultUserId, AccessLevel.Administrator);
        ConfigureAntiforgery(client);
        return client;
    }

    public string AttachmentStorageRoot => _attachmentStorageRoot;

    public HttpClient CreateAuthenticatedClientWithoutAntiforgery()
    {
        var client = CreateClient();
        ConfigureAuthenticationHeaders(client, _defaultLoginIdentityId, 1, _defaultUserId, AccessLevel.Administrator);
        return client;
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(long userId)
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await dbContext.Users.SingleAsync(item => item.Id == userId);
        var identity = await dbContext.LoginIdentities.SingleOrDefaultAsync(item =>
            item.Provider == "TestOidc" && item.Subject == $"test-user-{userId}");
        if (identity is null)
        {
            var timestamp = DateTimeOffset.UtcNow;
            identity = new LoginIdentity
            {
                UserId = user.Id,
                Provider = "TestOidc",
                Subject = $"test-user-{userId}",
                IsActive = true,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Version = 1,
            };
            dbContext.LoginIdentities.Add(identity);
            await dbContext.SaveChangesAsync();
        }

        var client = CreateClient();
        ConfigureAuthenticationHeaders(client, identity.Id, identity.Version, user.Id, user.AccessLevel);
        ConfigureAntiforgery(client);
        return client;
    }

    private static void ConfigureAntiforgery(HttpClient client)
    {
        using var response = client.GetAsync("/api/antiforgery/token").GetAwaiter().GetResult();
        if (!response.IsSuccessStatusCode)
        {
            return;
        }
        using var document = JsonDocument.Parse(response.Content.ReadAsStream());
        var token = document.RootElement.GetProperty("requestToken").GetString();
        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("The antiforgery token endpoint returned no request token.");
        }

        client.DefaultRequestHeaders.TryAddWithoutValidation("X-CSRF-TOKEN", token);
    }

    private static void ConfigureAuthenticationHeaders(
        HttpClient client,
        long loginIdentityId,
        long authVersion,
        long userId,
        AccessLevel accessLevel)
    {
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthenticationHandler.AuthMethodHeader,
            AuthenticationClaims.OidcMethod);
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthenticationHandler.AuthIdentityHeader,
            loginIdentityId.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthenticationHandler.AuthVersionHeader,
            authVersion.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthenticationHandler.UserHeader,
            userId.ToString());
        client.DefaultRequestHeaders.TryAddWithoutValidation(
            TestAuthenticationHandler.AccessLevelHeader,
            accessLevel.ToString());
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing)
        {
            _connection.Dispose();
            if (Directory.Exists(_attachmentStorageRoot))
            {
                Directory.Delete(_attachmentStorageRoot, recursive: true);
            }
        }
    }
}
