using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

public sealed class ConfiguredBootstrapWebApplicationFactory(
    IReadOnlyDictionary<string, string> settings) : BootstrapWebApplicationFactory
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        foreach (var setting in settings)
        {
            builder.UseSetting(setting.Key, setting.Value);
        }
    }
}

internal sealed class TestWebHostEnvironment(string contentRootPath) : IWebHostEnvironment
{
    public string ApplicationName { get; set; } = "SystemKnowledgeHub.Api.Tests";
    public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
    public string WebRootPath { get; set; } = contentRootPath;
    public string EnvironmentName { get; set; } = Environments.Development;
    public string ContentRootPath { get; set; } = contentRootPath;
    public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
}
