using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Serilog;
using Serilog.Events;
using Serilog.Extensions.Logging;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

internal static class TestSerilogRegistration
{
    public static IServiceCollection UseIsolatedTestSerilog(
        this IServiceCollection services,
        string logFilePath,
        TestLogSink? captureSink = null)
    {
        services.RemoveAll<ILoggerFactory>();
        services.AddSingleton<ILoggerFactory>(_ =>
        {
            var configuration = new LoggerConfiguration()
                .MinimumLevel.Information()
                .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
                .MinimumLevel.Override("Microsoft.AspNetCore", LogEventLevel.Warning)
                .MinimumLevel.Override(
                    "Microsoft.EntityFrameworkCore.Database.Command",
                    LogEventLevel.Warning)
                .Enrich.FromLogContext()
                .WriteTo.File(
                    logFilePath,
                    rollingInterval: RollingInterval.Day,
                    retainedFileCountLimit: 2,
                    fileSizeLimitBytes: 4 * 1024 * 1024,
                    rollOnFileSizeLimit: true,
                    shared: false);
            if (captureSink is not null)
            {
                configuration.WriteTo.Sink(captureSink);
            }
            return new SerilogLoggerFactory(configuration.CreateLogger(), dispose: true);
        });
        return services;
    }
}
