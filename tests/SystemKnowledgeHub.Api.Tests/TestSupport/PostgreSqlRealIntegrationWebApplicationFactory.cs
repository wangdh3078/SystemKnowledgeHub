using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public sealed class PostgreSqlRealIntegrationWebApplicationFactory : BootstrapWebApplicationFactory
{
    private readonly string databaseStorageRoot;
    private readonly string databaseConnectionString;
    private int disposeStarted;

    public PostgreSqlRealIntegrationWebApplicationFactory()
    {
        databaseStorageRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "postgresql-real-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(databaseStorageRoot);
        DatabasePath = Path.Combine(databaseStorageRoot, "discovery.db");
        IntegrationLogFilePath = Path.Combine(databaseStorageRoot, "postgresql-integration-.log");
        databaseConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = false,
        }.ToString();
    }

    public string DatabasePath { get; }
    public string IntegrationLogFilePath { get; }
    public TestLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<KnowledgeHubDbContext>>();
            services.AddDbContext<KnowledgeHubDbContext>(options =>
                options.UseSqlite(databaseConnectionString));
            services.PostConfigure<DatabaseDiscoveryOptions>(options =>
            {
                options.ConnectionTimeoutSeconds = 5;
                options.CatalogCommandTimeoutSeconds = 15;
                options.OverallTimeoutSeconds = 45;
                options.LeaseDurationSeconds = 6;
                options.HeartbeatIntervalSeconds = 1;
                options.QueuePollIntervalMilliseconds = 750;
            });
            services.UseIsolatedTestSerilog(IntegrationLogFilePath, LogSink);

            // Bootstrap tests remove hosted services. This integration factory deliberately
            // restores the production worker and leaves production testers/providers intact.
            services.AddHostedService<DatabaseDiscoveryWorker>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        // WebApplicationFactory disposal may re-enter virtual Dispose while its async host
        // shutdown is in progress. Let the outer call finish before deleting task-owned files.
        if (disposing && Interlocked.Exchange(ref disposeStarted, 1) != 0) return;
        ILoggerFactory? loggerFactory = null;
        if (disposing)
        {
            try { loggerFactory = Services.GetService<ILoggerFactory>(); }
            catch (ObjectDisposedException) { }
        }
        base.Dispose(disposing);
        loggerFactory?.Dispose();
        if (disposing && Directory.Exists(databaseStorageRoot))
            Directory.Delete(databaseStorageRoot, recursive: true);
    }
}

internal sealed record PostgreSqlRealIntegrationEnvironment(
    string Host,
    int Port,
    string Database,
    string OwnerUsername,
    string OwnerPassword,
    string DiscoveryUsername,
    string DiscoveryPassword)
{
    public const string EnabledVariable = "SKH_DBDISC_PG_INTEGRATION";

    public static bool TryLoad(out PostgreSqlRealIntegrationEnvironment? environment)
    {
        environment = null;
        var enabled = Environment.GetEnvironmentVariable(EnabledVariable);
        if (!string.Equals(enabled, "1", StringComparison.Ordinal)
            && !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var resourcePrefix = Required("SKH_DBDISC_PG_RESOURCE_PREFIX");
        if (!string.Equals(resourcePrefix, "skh-dbdisc-postgres-b01", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "SKH_DBDISC_PG_RESOURCE_PREFIX must be skh-dbdisc-postgres-b01 for this destructive task-owned fixture test.");

        var host = Required("SKH_DBDISC_PG_HOST");
        if (host is not ("127.0.0.1" or "localhost" or "::1" or "[::1]"))
            throw new InvalidOperationException(
                "SKH_DBDISC_PG_HOST must be a loopback host for this destructive task-owned fixture test.");
        var database = Required("SKH_DBDISC_PG_DATABASE");
        var ownerUsername = Required("SKH_DBDISC_PG_OWNER_USERNAME");
        var ownerPassword = Required("SKH_DBDISC_PG_OWNER_PASSWORD");
        var discoveryUsername = Required("SKH_DBDISC_PG_DISCOVERY_USERNAME");
        var discoveryPassword = Required("SKH_DBDISC_PG_DISCOVERY_PASSWORD");
        if (!int.TryParse(Required("SKH_DBDISC_PG_PORT"), out var port) || port is < 1 or > 65535)
            throw new InvalidOperationException("SKH_DBDISC_PG_PORT must be an integer from 1 through 65535.");
        if (string.Equals(ownerUsername, discoveryUsername, StringComparison.Ordinal))
            throw new InvalidOperationException("The fixture owner and discovery principal must be different PostgreSQL roles.");

        environment = new PostgreSqlRealIntegrationEnvironment(
            host,
            port,
            database,
            ownerUsername,
            ownerPassword,
            discoveryUsername,
            discoveryPassword);
        return true;
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required when {EnabledVariable}=1.");
}
