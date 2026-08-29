using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public sealed class DatabaseDiscoveryWebApplicationFactory : BootstrapWebApplicationFactory
{
    public ControlledDatabaseConnectionTester Tester { get; } = new();
    public TestLogSink LogSink { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IDatabaseConnectionTester>();
            services.AddSingleton(Tester);
            services.AddSingleton<IDatabaseConnectionTester>(provider =>
                provider.GetRequiredService<ControlledDatabaseConnectionTester>());
            services.AddSingleton<ILoggerProvider>(new TestLoggerProvider(LogSink));
        });
    }
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
        "APP_PDB",
        "APP_PDB");
}

public sealed class PendingDatabaseConnectionTest(DatabaseDiscoveryConnectionContext context)
{
    public DatabaseDiscoveryConnectionContext Context { get; } = context;
    public TaskCompletionSource<DatabaseConnectionTestResult> Completion { get; } =
        new(TaskCreationOptions.RunContinuationsAsynchronously);
}

public sealed class TestLogSink
{
    private readonly ConcurrentQueue<string> entries = new();
    public IReadOnlyCollection<string> Entries => entries.ToArray();
    internal void Add(string message) => entries.Enqueue(message);
}

internal sealed class TestLoggerProvider(TestLogSink sink) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new TestLogger(sink);
    public void Dispose() { }

    private sealed class TestLogger(TestLogSink sink) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter) => sink.Add(formatter(state, exception));
    }
}
