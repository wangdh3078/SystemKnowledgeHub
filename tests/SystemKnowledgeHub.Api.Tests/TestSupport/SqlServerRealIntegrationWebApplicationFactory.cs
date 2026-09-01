using Microsoft.AspNetCore.Hosting;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public sealed class SqlServerRealIntegrationWebApplicationFactory : BootstrapWebApplicationFactory
{
    private readonly string storageRoot;
    private readonly string connectionString;
    private int disposeStarted;

    public SqlServerRealIntegrationWebApplicationFactory()
    {
        storageRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "sqlserver-real-integration",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(storageRoot);
        DatabasePath = Path.Combine(storageRoot, "discovery.db");
        IntegrationLogFilePath = Path.Combine(storageRoot, "sqlserver-integration-.log");
        connectionString = new SqliteConnectionStringBuilder
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
            services.AddDbContext<KnowledgeHubDbContext>(options => options.UseSqlite(connectionString));
            services.PostConfigure<DatabaseDiscoveryOptions>(options =>
            {
                options.ConnectionTimeoutSeconds = 5;
                options.CatalogCommandTimeoutSeconds = 15;
                options.OverallTimeoutSeconds = 45;
                options.SqlServerTrustServerCertificate = true;
                options.LeaseDurationSeconds = 6;
                options.HeartbeatIntervalSeconds = 1;
                options.QueuePollIntervalMilliseconds = 750;
            });
            services.UseIsolatedTestSerilog(IntegrationLogFilePath, LogSink);
            services.AddHostedService<DatabaseDiscoveryWorker>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && Interlocked.Exchange(ref disposeStarted, 1) != 0) return;
        ILoggerFactory? loggerFactory = null;
        if (disposing)
        {
            try { loggerFactory = Services.GetService<ILoggerFactory>(); }
            catch (ObjectDisposedException) { }
        }
        base.Dispose(disposing);
        loggerFactory?.Dispose();
        if (disposing && Directory.Exists(storageRoot)) Directory.Delete(storageRoot, recursive: true);
    }
}

internal sealed record SqlServerRealIntegrationEnvironment(
    string Host,
    int Port,
    string Database,
    string OwnerUsername,
    string OwnerPassword,
    string DiscoveryUsername,
    string DiscoveryPassword)
{
    public const string EnabledVariable = "SKH_DBDISC_SQLSERVER_INTEGRATION";

    public static bool TryLoad(out SqlServerRealIntegrationEnvironment? environment)
    {
        environment = null;
        var enabled = Environment.GetEnvironmentVariable(EnabledVariable);
        if (!string.Equals(enabled, "1", StringComparison.Ordinal)
            && !string.Equals(enabled, "true", StringComparison.OrdinalIgnoreCase)) return false;

        if (!string.Equals(Required("SKH_DBDISC_SQLSERVER_RESOURCE_PREFIX"),
                "skh-dbdisc-sqlserver-b01", StringComparison.Ordinal))
            throw new InvalidOperationException(
                "SKH_DBDISC_SQLSERVER_RESOURCE_PREFIX must identify the task-owned SQL Server fixture.");
        var host = Required("SKH_DBDISC_SQLSERVER_HOST");
        if (host is not ("127.0.0.1" or "localhost" or "::1" or "[::1]"))
            throw new InvalidOperationException("The SQL Server fixture host must be loopback-only.");
        if (!int.TryParse(Required("SKH_DBDISC_SQLSERVER_PORT"), out var port)
            || port is < 1 or > 65535)
            throw new InvalidOperationException("The SQL Server fixture port is invalid.");

        var owner = Required("SKH_DBDISC_SQLSERVER_OWNER_USERNAME");
        var discovery = Required("SKH_DBDISC_SQLSERVER_DISCOVERY_USERNAME");
        if (string.Equals(owner, discovery, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Fixture owner and discovery login must be different principals.");
        environment = new(
            host,
            port,
            Required("SKH_DBDISC_SQLSERVER_DATABASE"),
            owner,
            Required("SKH_DBDISC_SQLSERVER_OWNER_PASSWORD"),
            discovery,
            Required("SKH_DBDISC_SQLSERVER_DISCOVERY_PASSWORD"));
        return true;
    }

    private static string Required(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"{name} is required when {EnabledVariable}=1.");
}
