using Microsoft.Data.Sqlite;

namespace SystemKnowledgeHub.Api.Shared.Configuration;

public sealed record IsolatedRuntimeStoragePaths(
    string SqliteDataSourcePath,
    string DataProtectionKeyPath,
    string AttachmentStorageRoot,
    string SerilogFilePath);

public static class IsolatedRuntimeStorageGuard
{
    public const string TestingEnvironmentName = "Testing";
    public const string VerificationEnvironmentName = "Verification";
    public const string SqliteError =
        "Verification/Testing runtime must use an explicit task-owned SQLite database.";

    public static bool IsRequired(IHostEnvironment environment) =>
        environment.IsEnvironment(TestingEnvironmentName)
        || environment.IsEnvironment(VerificationEnvironmentName);

    public static bool TryResolve(
        IConfiguration configuration,
        IHostEnvironment environment,
        out IsolatedRuntimeStoragePaths? paths,
        out string? error)
    {
        paths = null;
        error = null;
        if (!IsRequired(environment))
        {
            return true;
        }

        if (!TryResolveSqlitePath(configuration.GetConnectionString("KnowledgeHub"), out var sqlitePath)
            || !IsTaskOwnedPath(sqlitePath!, environment, pathIsFile: true))
        {
            error = SqliteError;
            return false;
        }

        if (!TryResolveAbsolutePath(configuration["DataProtection:KeyPath"], out var dataProtectionPath)
            || !IsTaskOwnedPath(dataProtectionPath!, environment, pathIsFile: false))
        {
            error = "Verification/Testing runtime must use an explicit task-owned Data Protection key path.";
            return false;
        }

        if (!TryResolveAbsolutePath(configuration["Attachments:StorageRoot"], out var attachmentPath)
            || !IsTaskOwnedPath(attachmentPath!, environment, pathIsFile: false))
        {
            error = "Verification/Testing runtime must use an explicit task-owned Attachment StorageRoot.";
            return false;
        }

        var configuredLogPath = configuration
            .GetSection("Serilog:WriteTo")
            .GetChildren()
            .FirstOrDefault(item => string.Equals(item["Name"], "File", StringComparison.Ordinal))?
            ["Args:path"];
        if (!TryResolveAbsolutePath(configuredLogPath, out var logPath)
            || !IsTaskOwnedPath(logPath!, environment, pathIsFile: true))
        {
            error = "Verification/Testing runtime must use an explicit task-owned Serilog file path.";
            return false;
        }

        paths = new IsolatedRuntimeStoragePaths(
            sqlitePath!,
            dataProtectionPath!,
            attachmentPath!,
            logPath!);
        return true;
    }

    private static bool TryResolveSqlitePath(string? connectionString, out string? path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        try
        {
            var builder = new SqliteConnectionStringBuilder(connectionString);
            if (string.IsNullOrWhiteSpace(builder.DataSource)
                || builder.DataSource == ":memory:"
                || builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
                || !Path.IsPathFullyQualified(builder.DataSource))
            {
                return false;
            }

            path = Path.GetFullPath(builder.DataSource);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool TryResolveAbsolutePath(string? configuredPath, out string? path)
    {
        path = null;
        if (string.IsNullOrWhiteSpace(configuredPath) || !Path.IsPathFullyQualified(configuredPath))
        {
            return false;
        }

        try
        {
            path = Path.GetFullPath(configuredPath);
            return true;
        }
        catch (Exception exception) when (
            exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return false;
        }
    }

    private static bool IsTaskOwnedPath(
        string path,
        IHostEnvironment environment,
        bool pathIsFile)
    {
        var comparison = OperatingSystem.IsWindows()
            ? StringComparison.OrdinalIgnoreCase
            : StringComparison.Ordinal;
        var normalizedPath = path.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        var root = Path.GetPathRoot(normalizedPath)?
            .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        if (string.Equals(normalizedPath, root, comparison))
        {
            return false;
        }
        if (ContainsSourceOrBuildOutputSegment(normalizedPath, comparison))
        {
            return false;
        }

        var contentRoot = Path.GetFullPath(environment.ContentRootPath);
        if (IsPathWithinDirectory(path, contentRoot))
        {
            return false;
        }

        var baseDirectory = Path.GetFullPath(AppContext.BaseDirectory);
        if (IsPathWithinDirectory(path, baseDirectory))
        {
            return false;
        }

        var repositoryRoot = FindRepositoryRoot(contentRoot) ?? FindRepositoryRoot(baseDirectory);
        if (repositoryRoot is not null && IsPathWithinDirectory(path, repositoryRoot))
        {
            return false;
        }

        if (pathIsFile && string.IsNullOrWhiteSpace(Path.GetFileName(path)))
        {
            return false;
        }

        return true;
    }

    private static bool ContainsSourceOrBuildOutputSegment(
        string path,
        StringComparison comparison)
    {
        var segments = path.Split(
            [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar],
            StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < segments.Length; index++)
        {
            if (string.Equals(segments[index], "bin", comparison)
                || string.Equals(segments[index], "obj", comparison)
                || string.Equals(segments[index], "publish", comparison))
            {
                return true;
            }
            if (index + 1 < segments.Length
                && string.Equals(segments[index], "src", comparison)
                && string.Equals(segments[index + 1], "SystemKnowledgeHub.Api", comparison))
            {
                return true;
            }
        }
        return false;
    }

    private static string? FindRepositoryRoot(string startPath)
    {
        var directory = new DirectoryInfo(startPath);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git"))
                || (Directory.Exists(Path.Combine(directory.FullName, "src", "SystemKnowledgeHub.Api"))
                    && File.Exists(Path.Combine(directory.FullName, "SystemKnowledgeHub.sln"))))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        return null;
    }

    private static bool IsPathWithinDirectory(string path, string directory)
    {
        var relativePath = Path.GetRelativePath(
            Path.GetFullPath(directory),
            Path.GetFullPath(path));
        return !Path.IsPathRooted(relativePath)
            && (relativePath == "."
                || (!relativePath.StartsWith($"..{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                    && relativePath != ".."));
    }
}
