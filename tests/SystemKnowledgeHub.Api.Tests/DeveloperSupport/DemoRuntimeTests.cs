using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.FileProviders;
using SystemKnowledgeHub.Api.DeveloperSupport.Demo;

namespace SystemKnowledgeHub.Api.Tests.DeveloperSupport;
public sealed class DemoRuntimeTests
{
    private static readonly string Repo = FindRepository();
    private static string FindRepository() { var directory = new DirectoryInfo(AppContext.BaseDirectory); while (directory.Parent is not null && !File.Exists(Path.Combine(directory.FullName, "AGENTS.md"))) directory = directory.Parent; return directory.FullName; }
    private static IConfiguration Configuration(string root, string enabled = "true", string? database = null) => new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
    {
        ["Demo:Enabled"] = enabled,
        ["Demo:Root"] = root,
        ["ConnectionStrings:KnowledgeHub"] = "Data Source=" + (database ?? Path.Combine(root, "system-knowledge-hub-demo.db")),
        ["DataProtection:KeyPath"] = Path.Combine(root, "keys"),
        ["Attachments:StorageRoot"] = Path.Combine(root, "attachments"),
        ["Serilog:WriteTo:1:Args:path"] = Path.Combine(root, "logs", "api-.log")
    }).Build();
    private sealed class Host(string name) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = name;
        public string ApplicationName { get; set; } = "DemoTests";
        public string ContentRootPath { get; set; } = Path.Combine(Repo, "src", "SystemKnowledgeHub.Api");
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
    [Fact]
    public void ExplicitDevelopmentAcceptsIsolatedPathsWithoutOpeningDatabase()
    {
        var root = Path.Combine(Path.GetTempPath(), "demo-guard-test");
        Assert.Equal(Path.Combine(root, "system-knowledge-hub-demo.db"), DemoRuntime.Validate(Configuration(root), new Host("Development")).DatabasePath);
    }
    [Theory]
    [InlineData("Production", "true")]
    [InlineData("Testing", "true")]
    [InlineData("Development", "false")]
    public void RejectsWrongEnvironmentOrMissingMode(string environment, string enabled)
    {
        var root = Path.Combine(Path.GetTempPath(), "demo-guard-test");
        Assert.Throws<InvalidOperationException>(() => DemoRuntime.Validate(Configuration(root, enabled), new Host(environment)));
    }
    [Fact]
    public void RejectsRepositoryAndEscapingDatabaseAndRootPaths()
    {
        var root = Path.Combine(Path.GetTempPath(), "demo-guard-test");
        foreach (var database in new[] { Path.Combine(Repo, "src", "SystemKnowledgeHub.Api", "App_Data", "system-knowledge-hub.db"), Path.Combine(root, "..", "other.db"), "relative.db" })
            Assert.Throws<InvalidOperationException>(() => DemoRuntime.Validate(Configuration(root, database: database), new Host("Development")));
        foreach (var candidate in new[] { "", Path.GetPathRoot(root)!, Repo, Path.Combine(Repo, "demo"), Path.Combine(Path.GetTempPath(), "src", "demo"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile) })
            Assert.Throws<InvalidOperationException>(() => DemoRuntime.ValidateRoot(candidate, Repo));
    }
}
