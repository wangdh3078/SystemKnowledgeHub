using Microsoft.Data.Sqlite;

namespace SystemKnowledgeHub.Api.DeveloperSupport.Demo;

// Explicit developer runtime boundary. No default path may fall back to App_Data.
public sealed record DemoRuntime(string Root, string DatabasePath)
{
    public static DemoRuntime Validate(IConfiguration configuration, IHostEnvironment environment)
    {
        if (!environment.IsDevelopment() || configuration["Demo:Enabled"] != "true")
            throw new InvalidOperationException("Demo 必须显式启用 Demo:Enabled=true，且仅允许 Development。");
        var root = ValidateRoot(configuration["Demo:Root"], environment.ContentRootPath);
        var connection = new SqliteConnectionStringBuilder(configuration.GetConnectionString("KnowledgeHub"));
        var expected = Path.Combine(root, "system-knowledge-hub-demo.db");
        if (!Path.IsPathFullyQualified(connection.DataSource) || !Same(Path.GetFullPath(connection.DataSource), expected)
            || connection.Mode == SqliteOpenMode.Memory)
            throw new InvalidOperationException("Demo SQLite 必须为 Demo root 内的 system-knowledge-hub-demo.db。");
        foreach (var (key, relative) in new[] { ("DataProtection:KeyPath", "keys"), ("Attachments:StorageRoot", "attachments"), ("Serilog:WriteTo:1:Args:path", "logs/api-.log") })
        {
            var value = configuration[key];
            if (value is null || !Path.IsPathFullyQualified(value) || !Same(Path.GetFullPath(value), Path.GetFullPath(Path.Combine(root, relative))))
                throw new InvalidOperationException($"Demo 必须隔离配置 {key}。");
            RejectLinks(Path.GetFullPath(value));
        }
        RejectLinks(expected);
        return new(root, expected);
    }

    public static string ValidateRoot(string? value, string contentRoot)
    {
        if (string.IsNullOrWhiteSpace(value) || !Path.IsPathFullyQualified(value))
            throw new InvalidOperationException("必须提供绝对 Demo root。");
        var root = Path.GetFullPath(value).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var name = Path.GetFileName(root);
        if (!(name.Equals("demo", StringComparison.OrdinalIgnoreCase) || name.StartsWith("demo-", StringComparison.OrdinalIgnoreCase))
            || Same(root, Path.GetPathRoot(root)!) || Same(root, Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)))
            throw new InvalidOperationException("Demo root 必须是独立的 demo 或 demo-* 子目录，不能为盘符或用户根目录。");
        var repository = new DirectoryInfo(contentRoot);
        while (repository.Parent is not null && !Directory.Exists(Path.Combine(repository.FullName, ".git")) && !File.Exists(Path.Combine(repository.FullName, ".git"))) repository = repository.Parent;
        if (repository.Parent is not null && (Contains(repository.FullName, root) || Contains(root, repository.FullName)))
            throw new InvalidOperationException("Demo root 必须位于 repository 外，且不能为其父目录。");
        if (root.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar).Any(segment =>
            segment.Equals("src", StringComparison.OrdinalIgnoreCase) || segment.Equals("App_Data", StringComparison.OrdinalIgnoreCase)
            || segment.Equals(".git", StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException("Demo root 不能位于 src、App_Data 或 .git。");
        RejectLinks(root);
        return root;
    }
    private static bool Same(string a, string b) => string.Equals(a, b, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static bool Contains(string root, string path) => Same(root, path) || path.StartsWith(root.TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar, OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal);
    private static void RejectLinks(string path)
    {
        for (var current = path; current is not null; current = Path.GetDirectoryName(current))
            if ((Directory.Exists(current) || File.Exists(current)) && (File.GetAttributes(current) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Demo 路径不能包含符号链接或目录联接。");
    }
}
