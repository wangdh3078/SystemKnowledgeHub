using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Runtime;

public sealed class SerilogRuntimeTests
{
    [Fact]
    public void Bootstrap_factories_use_unique_task_owned_rolling_log_paths()
    {
        using var first = new BootstrapWebApplicationFactory();
        using var second = new BootstrapWebApplicationFactory();

        Assert.NotEqual(first.LogStorageRoot, second.LogStorageRoot);
        Assert.NotEqual(first.LogFilePath, second.LogFilePath);
        Assert.Equal(first.LogStorageRoot, Path.GetDirectoryName(first.LogFilePath));
        Assert.Equal(second.LogStorageRoot, Path.GetDirectoryName(second.LogFilePath));
        AssertPathWithin(first.LogStorageRoot, Path.Combine(
            Path.GetTempPath(), "SystemKnowledgeHub.Api.Tests", "logs"));
        AssertPathWithin(second.LogStorageRoot, Path.Combine(
            Path.GetTempPath(), "SystemKnowledgeHub.Api.Tests", "logs"));
    }

    [Fact]
    public void Serilog_writes_to_the_registered_test_sink_and_the_isolated_file_sink()
    {
        using var factory = new CapturingFactory();
        _ = factory.Services;
        var logger = factory.Services.GetRequiredService<ILogger<SerilogRuntimeTests>>();
        var configuration = factory.Services.GetRequiredService<IConfiguration>();

        logger.LogWarning("Serilog test sink canary {Canary}", "R01-CANARY");

        Assert.Equal(
            factory.LogFilePath,
            configuration["Serilog:WriteTo:1:Args:path"]);
        Assert.Contains(
            factory.LogSink.Entries,
            entry => entry.Contains("R01-CANARY", StringComparison.Ordinal));
        var deadline = DateTimeOffset.UtcNow.AddSeconds(2);
        while (!Directory.EnumerateFiles(factory.LogStorageRoot, "*.log").Any()
               && DateTimeOffset.UtcNow < deadline)
        {
            Thread.Sleep(20);
        }
        Assert.Contains(
            Directory.EnumerateFiles(factory.LogStorageRoot, "*.log", SearchOption.TopDirectoryOnly),
            path => Path.GetFileName(path).StartsWith(
                "system-knowledge-hub-test-",
                StringComparison.Ordinal));
    }

    private static void AssertPathWithin(string path, string directory)
    {
        var relative = Path.GetRelativePath(Path.GetFullPath(directory), Path.GetFullPath(path));
        Assert.False(Path.IsPathRooted(relative));
        Assert.NotEqual("..", relative);
        Assert.False(relative.StartsWith(
            $"..{Path.DirectorySeparatorChar}",
            StringComparison.Ordinal));
    }

    private sealed class CapturingFactory : BootstrapWebApplicationFactory
    {
        public TestLogSink LogSink { get; } = new();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            builder.ConfigureServices(services =>
                services.UseIsolatedTestSerilog(LogFilePath, LogSink));
        }
    }
}
