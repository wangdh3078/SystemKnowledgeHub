using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class BootstrapAdministratorCommandTests
{
    [Fact]
    public async Task Bootstrap_creates_the_only_active_administrator_and_exact_login_identity()
    {
        await using var database = new TemporaryDatabase();
        using var services = database.CreateServices();

        var exitCode = await BootstrapAdministratorCommand.RunAsync(
            ["bootstrap-admin", "--provider", "TestOidc", "--subject", "CaseSensitive-Subject", "--display-name", "SEC-01 管理员", "--email", "admin@example.test"],
            services,
            Options());

        Assert.Equal(0, exitCode);
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await dbContext.Users.SingleAsync();
        var identity = await dbContext.LoginIdentities.SingleAsync();
        Assert.Equal(AccessLevel.Administrator, user.AccessLevel);
        Assert.True(user.IsActive);
        Assert.Equal(user.Id, identity.UserId);
        Assert.Equal("TestOidc", identity.Provider);
        Assert.Equal("CaseSensitive-Subject", identity.Subject);
        Assert.True(identity.IsActive);

        var duplicateAdministrator = await BootstrapAdministratorCommand.RunAsync(
            ["bootstrap-admin", "--provider", "TestOidc", "--subject", "another-subject", "--display-name", "另一个管理员"],
            services,
            Options());
        Assert.Equal(1, duplicateAdministrator);
    }

    [Fact]
    public async Task Bootstrap_rejects_provider_outside_allowlist_and_existing_identity_mapping()
    {
        await using var database = new TemporaryDatabase();
        using var services = database.CreateServices();

        var invalidProvider = await BootstrapAdministratorCommand.RunAsync(
            ["bootstrap-admin", "--provider", "Untrusted", "--subject", "subject", "--display-name", "管理员"],
            services,
            Options());
        Assert.Equal(1, invalidProvider);

        await using (var scope = services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await dbContext.Database.MigrateAsync();
            var timestamp = DateTimeOffset.UtcNow;
            var user = new User
            {
                DisplayName = "已绑定用户",
                IsActive = true,
                AccessLevel = AccessLevel.Viewer,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Version = 1,
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            dbContext.LoginIdentities.Add(new LoginIdentity
            {
                UserId = user.Id,
                Provider = "TestOidc",
                Subject = "already-bound",
                IsActive = true,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Version = 1,
            });
            await dbContext.SaveChangesAsync();
        }

        var existingIdentity = await BootstrapAdministratorCommand.RunAsync(
            ["bootstrap-admin", "--provider", "TestOidc", "--subject", "already-bound", "--display-name", "管理员"],
            services,
            Options());
        Assert.Equal(1, existingIdentity);
    }

    private static OidcAuthenticationOptions Options() => new() { Enabled = true, Provider = "TestOidc" };

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"system-knowledge-hub-sec01-{Guid.NewGuid():N}.db");

        public ServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            services.AddDbContext<KnowledgeHubDbContext>(options => options.UseSqlite($"Data Source={_path};Foreign Keys=True;Pooling=False"));
            services.AddSingleton<Microsoft.Extensions.Options.IOptions<LocalAuthenticationOptions>>(
                Microsoft.Extensions.Options.Options.Create(new LocalAuthenticationOptions()));
            services.AddSingleton<Microsoft.Extensions.Options.IOptions<OidcAuthenticationOptions>>(
                Microsoft.Extensions.Options.Options.Create(Options()));
            services.AddScoped<UsableAdministratorResolver>();
            return services.BuildServiceProvider();
        }

        public ValueTask DisposeAsync()
        {
            foreach (var path in new[] { _path, $"{_path}-wal", $"{_path}-shm" })
            {
                if (File.Exists(path)) File.Delete(path);
            }
            return ValueTask.CompletedTask;
        }
    }
}
