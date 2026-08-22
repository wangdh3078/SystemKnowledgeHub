using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class BootstrapLocalAdministratorCommandTests
{
    [Fact]
    public async Task Password_stdin_bootstrap_creates_an_administrator_and_one_hashed_local_credential()
    {
        await using var database = new TemporaryDatabase();
        using var services = database.CreateServices();
        const string password = "安全本地管理员密码 2026!";

        var exitCode = await BootstrapLocalAdministratorCommand.RunAsync(
            ["bootstrap-local-admin", "--username", "王大虎01", "--display-name", "本地管理员", "--password-stdin"],
            services,
            new LocalAuthenticationOptions { Enabled = true },
            new StringReader(password + Environment.NewLine),
            new StringWriter(),
            new StringWriter());

        Assert.Equal(0, exitCode);
        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await dbContext.Users.SingleAsync();
        var credential = await dbContext.LocalLoginCredentials.SingleAsync();
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();
        Assert.Equal(AccessLevel.Administrator, user.AccessLevel);
        Assert.True(user.IsActive);
        Assert.Equal(user.Id, credential.UserId);
        Assert.Equal("王大虎01", credential.Username);
        Assert.NotEqual(password, credential.PasswordHash);
        Assert.Equal(PasswordVerificationResult.Success, passwords.Verify(credential, credential.PasswordHash, password));

        var duplicate = await BootstrapLocalAdministratorCommand.RunAsync(
            ["bootstrap-local-admin", "--username", "another-admin", "--password-stdin"],
            services,
            new LocalAuthenticationOptions { Enabled = true },
            new StringReader(password + Environment.NewLine),
            new StringWriter(),
            new StringWriter());
        Assert.Equal(1, duplicate);
    }

    [Fact]
    public async Task Bootstrap_rejects_plaintext_argument_disabled_local_authentication_and_invalid_password()
    {
        await using var database = new TemporaryDatabase();
        using var services = database.CreateServices();

        var disabled = await BootstrapLocalAdministratorCommand.RunAsync(
            ["bootstrap-local-admin", "--username", "local-admin", "--password-stdin"],
            services,
            new LocalAuthenticationOptions { Enabled = false },
            new StringReader("安全本地管理员密码 2026!" + Environment.NewLine),
            new StringWriter(),
            new StringWriter());
        Assert.Equal(1, disabled);

        var plaintextArgument = await BootstrapLocalAdministratorCommand.RunAsync(
            ["bootstrap-local-admin", "--username", "local-admin", "--password", "secret"],
            services,
            new LocalAuthenticationOptions { Enabled = true },
            new StringReader(string.Empty),
            new StringWriter(),
            new StringWriter());
        Assert.Equal(1, plaintextArgument);

        var shortPassword = await BootstrapLocalAdministratorCommand.RunAsync(
            ["bootstrap-local-admin", "--username", "local-admin", "--password-stdin"],
            services,
            new LocalAuthenticationOptions { Enabled = true },
            new StringReader("too-short" + Environment.NewLine),
            new StringWriter(),
            new StringWriter());
        Assert.Equal(1, shortPassword);
    }

    private sealed class TemporaryDatabase : IAsyncDisposable
    {
        private readonly string _path = Path.Combine(Path.GetTempPath(), $"system-knowledge-hub-auth-b01-{Guid.NewGuid():N}.db");

        public ServiceProvider CreateServices()
        {
            var services = new ServiceCollection();
            services.AddDbContext<KnowledgeHubDbContext>(options => options.UseSqlite($"Data Source={_path};Foreign Keys=True;Pooling=False"));
            services.Configure<PasswordHasherOptions>(options =>
            {
                options.CompatibilityMode = PasswordHasherCompatibilityMode.IdentityV3;
                options.IterationCount = 220_000;
            });
            services.AddSingleton<LocalPasswordService>();
            services.AddScoped<LocalAdminBootstrapService>();
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
