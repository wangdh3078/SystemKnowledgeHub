using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public sealed class LocalLoginWebApplicationFactory : BootstrapWebApplicationFactory
{
    protected override bool UsesTestAuthentication => false;
    protected override string TestEnvironmentName => "Development";

    protected override void ConfigureAuthenticationMode(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Local:Enabled", "true");
        builder.UseSetting("Authentication:Oidc:Enabled", "false");
    }
}

public sealed class AuditedLocalLoginWebApplicationFactory : BootstrapWebApplicationFactory
{
    public TestLogSink LogSink { get; } = new();

    protected override bool UsesTestAuthentication => false;
    protected override string TestEnvironmentName => "Development";

    protected override void ConfigureAuthenticationMode(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Local:Enabled", "true");
        builder.UseSetting("Authentication:Oidc:Enabled", "false");
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        builder.ConfigureServices(services =>
            services.AddSingleton<ILoggerProvider>(new TestLoggerProvider(LogSink)));
    }
}

public sealed class AuthenticationOptionsWebApplicationFactory(bool localEnabled, bool oidcEnabled) : BootstrapWebApplicationFactory
{
    protected override void ConfigureAuthenticationMode(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Local:Enabled", localEnabled.ToString());
        builder.UseSetting("Authentication:Oidc:Enabled", oidcEnabled.ToString());
        builder.UseSetting("Authentication:Oidc:Provider", "TestOidc");
        builder.UseSetting("Authentication:Oidc:Authority", "https://test-oidc.invalid");
        builder.UseSetting("Authentication:Oidc:ClientId", "test-client");
        builder.UseSetting("Authentication:Oidc:DisplayName", "使用企业账号登录");
    }
}

public sealed class RateLimitedLocalLoginWebApplicationFactory : BootstrapWebApplicationFactory
{
    protected override bool UsesTestAuthentication => false;
    protected override string TestEnvironmentName => "Development";

    protected override void ConfigureAuthenticationMode(IWebHostBuilder builder)
    {
        builder.UseSetting("Authentication:Local:Enabled", "true");
        builder.UseSetting("Authentication:Oidc:Enabled", "false");
        builder.UseSetting("Authentication:Local:RateLimit:PermitLimit", "2");
        builder.UseSetting("Authentication:Local:RateLimit:WindowMinutes", "5");
    }
}
