using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Data.Sqlite;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using Serilog.Core;
using Serilog.Events;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public sealed class DatabaseDiscoveryWebApplicationFactory : BootstrapWebApplicationFactory
{
    private readonly SqliteConnection databaseAnchor;
    private readonly string databaseConnectionString;

    public DatabaseDiscoveryWebApplicationFactory()
    {
        databaseConnectionString = new SqliteConnectionStringBuilder
        {
            DataSource = $"SystemKnowledgeHubDiscoveryTests-{Guid.NewGuid():N}",
            Mode = SqliteOpenMode.Memory,
            Cache = SqliteCacheMode.Shared,
            ForeignKeys = true,
            DefaultTimeout = 5,
            Pooling = false,
        }.ToString();
        databaseAnchor = new SqliteConnection(databaseConnectionString);
        databaseAnchor.Open();
    }

    public ControlledDatabaseConnectionTester Tester { get; } = new();
    public ControlledDatabaseDiscoveryProvider DiscoveryProvider { get; } = new();
    public TestLogSink LogSink { get; } = new();
    public int WorkerPollIntervalMilliseconds { get; set; } = 25;
    public int WorkerLeaseDurationSeconds { get; set; } = 4;
    public int WorkerHeartbeatIntervalSeconds { get; set; } = 1;
    public int WorkerOverallTimeoutSeconds { get; set; } = 10;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<KnowledgeHubDbContext>>();
            services.AddDbContext<KnowledgeHubDbContext>(options => options.UseSqlite(databaseConnectionString));
            services.RemoveAll<IDatabaseConnectionTester>();
            services.AddSingleton(Tester);
            services.AddSingleton<IDatabaseConnectionTester>(provider =>
                provider.GetRequiredService<ControlledDatabaseConnectionTester>());
            services.RemoveAll<IDatabaseDiscoveryProvider>();
            services.AddSingleton(DiscoveryProvider);
            services.AddSingleton<IDatabaseDiscoveryProvider>(provider =>
                provider.GetRequiredService<ControlledDatabaseDiscoveryProvider>());
            services.PostConfigure<DatabaseDiscoveryOptions>(options =>
            {
                options.OverallTimeoutSeconds = WorkerOverallTimeoutSeconds;
                options.LeaseDurationSeconds = WorkerLeaseDurationSeconds;
                options.HeartbeatIntervalSeconds = WorkerHeartbeatIntervalSeconds;
                options.QueuePollIntervalMilliseconds = WorkerPollIntervalMilliseconds;
            });
            services.UseIsolatedTestSerilog(LogFilePath, LogSink);
            services.AddHostedService<DatabaseDiscoveryWorker>();
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing) databaseAnchor.Dispose();
    }
}

public sealed class ControlledDatabaseDiscoveryProvider : IDatabaseDiscoveryProvider
{
    private readonly Channel<PendingDatabaseDiscovery> pending = Channel.CreateUnbounded<PendingDatabaseDiscovery>();
    private int callCount;

    public DatabaseProviderType ProviderType => DatabaseProviderType.Oracle;
    public int CallCount => Volatile.Read(ref callCount);
    public bool GateCalls { get; set; }
    public Func<DatabaseDiscoveryConnectionContext, DatabaseDiscoveryRequest, int, CanonicalDatabaseDiscoverySnapshot>? SnapshotFactory { get; set; }
    public Func<DatabaseDiscoveryConnectionContext, DatabaseDiscoveryRequest, CancellationToken, Task<CanonicalDatabaseDiscoverySnapshot>>? Handler { get; set; }

    public Task<DatabaseProviderCapabilities> DetectCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken) => Task.FromResult(new DatabaseProviderCapabilities(
        [
            new("SupportsSequences", DatabaseDiscoveryCapabilityState.Supported, null),
            new("SupportsInvisibleColumns", DatabaseDiscoveryCapabilityState.NotSupported, "FakeProvider"),
        ]));

    public async Task<CanonicalDatabaseDiscoverySnapshot> DiscoverAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        DatabaseProviderCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        var call = Interlocked.Increment(ref callCount);
        if (Handler is not null) return await Handler(connection, request, cancellationToken);
        if (GateCalls)
        {
            var pendingCall = new PendingDatabaseDiscovery(connection, request, capabilities);
            await pending.Writer.WriteAsync(pendingCall, cancellationToken);
            return await pendingCall.Completion.Task.WaitAsync(cancellationToken);
        }
        return (SnapshotFactory ?? ((context, discoveryRequest, version) =>
            CanonicalSnapshotFixtures.Create(context, discoveryRequest, version)))(connection, request, call);
    }

    public Task<PendingDatabaseDiscovery> WaitForCall(CancellationToken cancellationToken = default) =>
        pending.Reader.ReadAsync(cancellationToken).AsTask();
}

public sealed class PendingDatabaseDiscovery(
    DatabaseDiscoveryConnectionContext context,
    DatabaseDiscoveryRequest request,
    DatabaseProviderCapabilities capabilities)
{
    public DatabaseDiscoveryConnectionContext Context { get; } = context;
    public DatabaseDiscoveryRequest Request { get; } = request;
    public DatabaseProviderCapabilities Capabilities { get; } = capabilities;
    public TaskCompletionSource<CanonicalDatabaseDiscoverySnapshot> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public static class CanonicalSnapshotFixtures
{
    public static CanonicalDatabaseDiscoverySnapshot Create(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        int version = 1,
        string targetFingerprint = "fake-target-v1",
        string? schemaName = null)
    {
        var schema = schemaName ?? request.IncludedSchemas.Single();
        var schemaId = Key("Schema", schema);
        var customerId = Key("Object", schema, "CUSTOMERS");
        var orderId = Key("Object", schema, "ORDERS");
        var customerIdColumn = Key("Column", customerId, "ID");
        var customerNameColumn = Key("Column", customerId, "NAME");
        var orderIdColumn = Key("Column", orderId, "ID");
        var orderCustomerColumn = Key("Column", orderId, "CUSTOMER_ID");
        var number = Native("NUMBER(19)", "NUMBER", 19, 0);
        var text = Native(version >= 2 ? "VARCHAR2(200 CHAR)" : "VARCHAR2(100 CHAR)", "VARCHAR2", null, null, version >= 2 ? 200 : 100);
        return new CanonicalDatabaseDiscoverySnapshot(
            1,
            new DateTimeOffset(2026, 8, 30, 0, version, 0, TimeSpan.Zero),
            DatabaseProviderType.Oracle,
            "FakeOracle19c/1",
            new("Oracle", "19.0.0.0.0", "APP_PDB", "APP_PDB", targetFingerprint),
            new(1, 1, [schemaId], [DatabaseDiscoveryObjectType.Table, DatabaseDiscoveryObjectType.View], 1,
                "Ordinal", new Dictionary<string, string> { ["fakeNormalization"] = "v1" }, "visible-v1"),
            1,
            DatabaseDiscoveryCompleteness.Complete,
            [],
            [new(schema, schemaId)],
            [
                new(schemaId, schema, "CUSTOMERS", DatabaseDiscoveryObjectType.Table, "Customer master", customerId, "1001"),
                new(schemaId, schema, "ORDERS", DatabaseDiscoveryObjectType.Table, null, orderId, "1002"),
            ],
            [
                new(customerId, "ID", 1, number, false, null, true, null, customerIdColumn),
                new(customerId, "NAME", 2, text, false, null, false, "Customer name", customerNameColumn),
                new(orderId, "ID", 1, number, false, null, true, null, orderIdColumn),
                new(orderId, "CUSTOMER_ID", 2, number, false, null, false, null, orderCustomerColumn),
            ],
            [
                new("PK_CUSTOMERS", customerId, [customerIdColumn], Key("Constraint", "PK", customerId, "PK_CUSTOMERS")),
                new("PK_ORDERS", orderId, [orderIdColumn], Key("Constraint", "PK", orderId, "PK_ORDERS")),
            ],
            [
                new("FK_ORDERS_CUSTOMER", orderId, [orderCustomerColumn], customerId, [customerIdColumn], null, "NO ACTION",
                    Key("Constraint", "FK", orderId, "FK_ORDERS_CUSTOMER")),
            ],
            [
                new("UQ_CUSTOMER_NAME", customerId, [customerNameColumn], Key("Constraint", "UQ", customerId, "UQ_CUSTOMER_NAME")),
            ],
            [
                new("IX_ORDERS_CUSTOMER", orderId, "NORMAL", false,
                    [new(1, orderCustomerColumn, null, DatabaseDiscoverySortDirection.Ascending)], [], null, null,
                    Key("Index", orderId, "IX_ORDERS_CUSTOMER")),
            ],
            [
                new(schemaId, "ORDER_SEQ", number, "1", "1", "999999999999999999", 20, false, false, null,
                    Key("Sequence", schemaId, "ORDER_SEQ")),
            ],
            [],
            new(0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    public static string Key(params string[] components) =>
        string.Concat(components.Select(component => $"{component.Length}:{component}"));

    private static CanonicalNativeDataType Native(
        string declaration,
        string name,
        int? precision,
        int? scale,
        long? length = null) => new(
            DatabaseDiscoveryNativeTypeOrigin.CatalogDeclared,
            name,
            null,
            declaration,
            length is null
                ? new(DatabaseDiscoveryMeasureKind.NotApplicable, null, null)
                : new(DatabaseDiscoveryMeasureKind.Exact, length, DatabaseDiscoveryLengthUnit.Characters),
            length is null ? null : "CHAR",
            precision is null
                ? new(DatabaseDiscoveryMeasureKind.NotApplicable, null)
                : new(DatabaseDiscoveryMeasureKind.Exact, precision),
            scale is null
                ? new(DatabaseDiscoveryMeasureKind.NotApplicable, null)
                : new(DatabaseDiscoveryMeasureKind.Exact, scale));
}

public sealed class ControlledDatabaseConnectionTester : IDatabaseConnectionTester
{
    private readonly Channel<PendingDatabaseConnectionTest> pending = Channel.CreateUnbounded<PendingDatabaseConnectionTest>();

    public DatabaseProviderType ProviderType => DatabaseProviderType.Oracle;
    public bool GateCalls { get; set; }
    public Func<DatabaseDiscoveryConnectionContext, CancellationToken, Task<DatabaseConnectionTestResult>>? Handler { get; set; }

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        if (Handler is not null) return await Handler(connection, cancellationToken);
        if (GateCalls)
        {
            var call = new PendingDatabaseConnectionTest(connection);
            await pending.Writer.WriteAsync(call, cancellationToken);
            return await call.Completion.Task.WaitAsync(cancellationToken);
        }
        return Success();
    }

    public Task<PendingDatabaseConnectionTest> WaitForCall(CancellationToken cancellationToken = default) =>
        pending.Reader.ReadAsync(cancellationToken).AsTask();

    public static DatabaseConnectionTestResult Success() => DatabaseConnectionTestResult.Success(
        "Oracle 19c 连接、目标上下文与基础目录可见性验证成功。",
        "19.0.0.0.0",
        null,
        "APP_PDB",
        "APP_PDB");
}

public sealed class PendingDatabaseConnectionTest(DatabaseDiscoveryConnectionContext context)
{
    public DatabaseDiscoveryConnectionContext Context { get; } = context;
    public TaskCompletionSource<DatabaseConnectionTestResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class TestLogSink : ILogEventSink
{
    private readonly ConcurrentQueue<string> entries = new();
    public IReadOnlyCollection<string> Entries => entries.ToArray();
    public void Emit(LogEvent logEvent)
    {
        var message = logEvent.RenderMessage().Replace("\"", string.Empty, StringComparison.Ordinal);
        if (logEvent.Exception is not null) message = $"{message}{Environment.NewLine}{logEvent.Exception}";
        entries.Enqueue(message);
    }
}
