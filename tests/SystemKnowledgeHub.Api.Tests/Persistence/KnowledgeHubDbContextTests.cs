using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Configuration;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class KnowledgeHubDbContextTests
    : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public KnowledgeHubDbContextTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task DbContext_resolves_and_applies_sqlite_configuration()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider
            .GetRequiredService<KnowledgeHubDbContext>();

        Assert.Equal(
            "Microsoft.EntityFrameworkCore.Sqlite",
            dbContext.Database.ProviderName);

        await dbContext.Database.OpenConnectionAsync();

        await using var foreignKeysCommand = dbContext.Database
            .GetDbConnection()
            .CreateCommand();
        foreignKeysCommand.CommandText = "PRAGMA foreign_keys;";

        var foreignKeys = Convert.ToInt32(
            await foreignKeysCommand.ExecuteScalarAsync());

        await using var busyTimeoutCommand = dbContext.Database
            .GetDbConnection()
            .CreateCommand();
        busyTimeoutCommand.CommandText = "PRAGMA busy_timeout;";

        var busyTimeout = Convert.ToInt32(
            await busyTimeoutCommand.ExecuteScalarAsync());

        Assert.Equal(1, foreignKeys);
        Assert.Equal(5000, busyTimeout);
    }

    [Fact]
    public async Task Configured_sqlite_default_and_busy_timeouts_are_wired_to_the_real_provider()
    {
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "sqlite-configuration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["ConnectionStrings:KnowledgeHub"] = "Data Source=state/configuration-test.db;Pooling=False",
                })
                .Build();
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddKnowledgeHubPersistence(
                configuration,
                new TestWebHostEnvironment(temporaryRoot),
                new SqlitePersistenceOptions
                {
                    DefaultTimeoutSeconds = 17,
                    BusyTimeoutMilliseconds = 4_321,
                });

            await using (var provider = services.BuildServiceProvider())
            {
                await using var scope = provider.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
                await dbContext.Database.OpenConnectionAsync();
                var connection = Assert.IsType<SqliteConnection>(dbContext.Database.GetDbConnection());

                Assert.Equal(17, connection.DefaultTimeout);
                Assert.True(Path.IsPathFullyQualified(connection.DataSource));
                Assert.StartsWith(
                    Path.GetFullPath(temporaryRoot),
                    Path.GetFullPath(connection.DataSource),
                    OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
                Assert.Equal(1, await PragmaInt(connection, "foreign_keys"));
                Assert.Equal(4_321, await PragmaInt(connection, "busy_timeout"));
                Assert.Equal("wal", (await PragmaText(connection, "journal_mode")).ToLowerInvariant());

                await dbContext.Database.CloseConnectionAsync();
            }
        }
        finally
        {
            if (Directory.Exists(temporaryRoot))
            {
                Directory.Delete(temporaryRoot, recursive: true);
            }
        }
    }

    private static async Task<int> PragmaInt(SqliteConnection connection, string name) =>
        Convert.ToInt32(await PragmaScalar(connection, name));

    private static async Task<string> PragmaText(SqliteConnection connection, string name) =>
        Convert.ToString(await PragmaScalar(connection, name))
        ?? throw new InvalidOperationException($"PRAGMA {name} returned null.");

    private static async Task<object?> PragmaScalar(SqliteConnection connection, string name)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA {name};";
        return await command.ExecuteScalarAsync();
    }
}
