using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class ConcurrentLocalLoginApiTests
{
    [Theory]
    [InlineData(0, 1)]
    [InlineData(0, 2)]
    [InlineData(4, 2)]
    public async Task Concurrent_wrong_passwords_count_both_failures_without_extending_an_active_lock(int initialCount, int failures)
    {
        using var factory = new ConcurrentLoginFactory();
        const string username = "stability-login";
        const string password = "Stability test password 2026!";
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var now = DateTimeOffset.UtcNow;
            var user = await db.Users.FirstAsync();
            var credential = new LocalLoginCredential
            {
                UserId = user.Id,
                Username = username,
                NormalizedUsername = username.ToUpperInvariant(),
                IsActive = true,
                Version = 1,
                SessionVersion = 1,
                CreatedAt = now,
                UpdatedAt = now,
                LastPasswordChangedAt = now,
                FailedLoginAttempts = initialCount,
                FailedLoginWindowStartedAt = now,
            };
            credential.PasswordHash = scope.ServiceProvider.GetRequiredService<LocalPasswordService>().Hash(credential, password);
            db.LocalLoginCredentials.Add(credential);
            await db.SaveChangesAsync();
        }
        using var first = factory.CreateClient();
        using var second = factory.CreateClient();
        foreach (var client in new[] { first, second })
        {
            var token = await client.GetFromJsonAsync<JsonElement>("/api/antiforgery/token");
            client.DefaultRequestHeaders.Add("X-CSRF-TOKEN", token.GetProperty("requestToken").GetString());
        }
        // Both independent connections must read the initial credential before either proceeds.
        factory.ReadBarrier.Armed = failures == 2;
        var responses = failures == 1
            ? new[] { await first.PostAsJsonAsync("/auth/local/login", new { username, password = "wrong password" }) }
            : await Task.WhenAll(
            first.PostAsJsonAsync("/auth/local/login", new { username, password = "wrong password" }),
            second.PostAsJsonAsync("/auth/local/login", new { username, password = "wrong password" }));
        foreach (var response in responses)
        {
            using (response)
            {
                Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
                var body = await response.Content.ReadFromJsonAsync<JsonElement>();
                Assert.Equal("invalid_credentials", body.GetProperty("code").GetString());
                Assert.DoesNotContain("Exception", body.ToString());
            }
        }
        await using var verify = factory.Services.CreateAsyncScope();
        var context = verify.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await context.LocalLoginCredentials.AsNoTracking().SingleAsync();
        Assert.Equal(initialCount + failures, stored.FailedLoginAttempts);
        Assert.Equal(failures + 1, stored.Version);
        Assert.Equal(1, stored.SessionVersion);
        Assert.Equal(initialCount == 4, stored.LockedUntil > DateTimeOffset.UtcNow);
        using var correct = await first.PostAsJsonAsync("/auth/local/login", new { username, password });
        Assert.Equal(initialCount == 4 ? HttpStatusCode.Unauthorized : HttpStatusCode.NoContent, correct.StatusCode);
        if (initialCount == 4)
        {
            using var locked = await second.PostAsJsonAsync("/auth/local/login", new { username, password = "wrong again" });
            Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
            var stillLocked = await context.LocalLoginCredentials.AsNoTracking().SingleAsync();
            Assert.Equal(stored.LockedUntil, stillLocked.LockedUntil);
            Assert.Equal(stored.FailedLoginAttempts, stillLocked.FailedLoginAttempts);
        }
        else
        {
            var cleared = await context.LocalLoginCredentials.AsNoTracking().SingleAsync();
            Assert.Equal(0, cleared.FailedLoginAttempts);
            Assert.Null(cleared.FailedLoginWindowStartedAt);
        }
    }

    private sealed class ConcurrentLoginFactory : BootstrapWebApplicationFactory
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"stability-r01-login-{Guid.NewGuid():N}");
        public CredentialReadBarrier ReadBarrier { get; } = new();
        protected override bool UsesTestAuthentication => false;
        protected override string TestEnvironmentName => "Development";
        protected override void ConfigureAuthenticationMode(IWebHostBuilder builder)
        {
            builder.UseSetting("Authentication:Local:Enabled", "true");
            builder.UseSetting("Authentication:Oidc:Enabled", "false");
        }
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            Directory.CreateDirectory(directory);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<KnowledgeHubDbContext>>();
                services.AddDbContext<KnowledgeHubDbContext>(options => options
                    .UseSqlite($"Data Source={Path.Combine(directory, "login.db")};Pooling=False;Default Timeout=10")
                    .AddInterceptors(ReadBarrier));
            });
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CredentialReadBarrier : DbCommandInterceptor
    {
        private readonly TaskCompletionSource gate = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int reads;
        public bool Armed { get; set; }
        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command, CommandExecutedEventData eventData, DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (Armed && command.CommandText.StartsWith("SELECT", StringComparison.Ordinal)
                && command.CommandText.Contains("local_login_credentials", StringComparison.Ordinal))
            {
                var arrival = Interlocked.Increment(ref reads);
                if (arrival == 2) gate.TrySetResult();
                if (arrival <= 2) await gate.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
            }
            return result;
        }
    }
}
