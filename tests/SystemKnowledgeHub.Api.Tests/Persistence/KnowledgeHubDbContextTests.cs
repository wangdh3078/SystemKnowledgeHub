using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Persistence;
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
}
