using System.Diagnostics;
using System.Security.Cryptography;
using Microsoft.Data.Sqlite;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Configuration;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Runtime;

[Collection("Repository SQLite Sentinel")]
public sealed class VerificationRuntimeStorageProcessTests
{
    [Fact]
    public async Task Verification_startup_matrix_and_testing_factory_preserve_repository_sqlite()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var repository = FindRepositoryRoot();
        var repositoryDatabase = Path.Combine(
            repository,
            "src",
            "SystemKnowledgeHub.Api",
            "App_Data",
            "system-knowledge-hub.db");
        var sentinel = RepositorySqliteFingerprint.Capture(repositoryDatabase);
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "dbsafe-process",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        try
        {
            await AssertRejectedAsync(
                Path.Combine(temporaryRoot, "missing"),
                connectionString: null);
            await AssertRejectedAsync(
                Path.Combine(temporaryRoot, "relative"),
                "Data Source=App_Data/system-knowledge-hub.db");
            await AssertRejectedAsync(
                Path.Combine(temporaryRoot, "repository-absolute"),
                $"Data Source={repositoryDatabase}");

            var acceptedRoot = Path.Combine(temporaryRoot, "accepted");
            var acceptedDatabase = Path.Combine(acceptedRoot, "knowledge-hub.db");
            await AssertAcceptedAsync(acceptedRoot, acceptedDatabase);

            using (var factory = new BootstrapWebApplicationFactory())
            using (var client = factory.CreateClient())
            {
                using var response = await client.GetAsync("/api/auth/options");
                response.EnsureSuccessStatusCode();
                Assert.False(File.Exists(factory.GuardDatabasePath));
            }

        }
        finally
        {
            var finalSentinel = RepositorySqliteFingerprint.Capture(repositoryDatabase);
            await DeleteTemporaryRootAsync(temporaryRoot);
            Assert.Equal(sentinel, finalSentinel);
        }
    }

    [Fact]
    public void Design_time_factory_without_explicit_path_fails_closed_and_preserves_repository_sqlite()
    {
        var repositoryDatabase = Path.Combine(
            FindRepositoryRoot(),
            "src",
            "SystemKnowledgeHub.Api",
            "App_Data",
            "system-knowledge-hub.db");
        var sentinel = RepositorySqliteFingerprint.Capture(repositoryDatabase);
        var previous = Environment.GetEnvironmentVariable(
            KnowledgeHubDesignTimeDbContextFactory.DatabasePathEnvironmentVariable);

        try
        {
            Environment.SetEnvironmentVariable(
                KnowledgeHubDesignTimeDbContextFactory.DatabasePathEnvironmentVariable,
                null);
            var exception = Assert.Throws<InvalidOperationException>(
                () => new KnowledgeHubDesignTimeDbContextFactory().CreateDbContext([]));
            Assert.Contains(
                KnowledgeHubDesignTimeDbContextFactory.DatabasePathEnvironmentVariable,
                exception.Message,
                StringComparison.Ordinal);
            Assert.Equal(sentinel, RepositorySqliteFingerprint.Capture(repositoryDatabase));
        }
        finally
        {
            Environment.SetEnvironmentVariable(
                KnowledgeHubDesignTimeDbContextFactory.DatabasePathEnvironmentVariable,
                previous);
        }
    }

    private static async Task AssertRejectedAsync(string scenarioRoot, string? connectionString)
    {
        Directory.CreateDirectory(scenarioRoot);
        using var process = CreateVerificationProcess(scenarioRoot, connectionString);
        var result = await RunToExitAsync(process);

        Assert.Equal(1, result.ExitCode);
        Assert.Contains("启动配置错误（环境：Verification）", result.StandardError);
        Assert.Contains(IsolatedRuntimeStorageGuard.SqliteError, result.StandardError);
        Assert.DoesNotContain("Unhandled exception", result.StandardError);
        Assert.False(File.Exists(Path.Combine(scenarioRoot, "knowledge-hub.db")));
    }

    private static async Task AssertAcceptedAsync(string scenarioRoot, string databasePath)
    {
        Directory.CreateDirectory(scenarioRoot);
        var port = ReservePort();
        using var process = CreateVerificationProcess(
            scenarioRoot,
            $"Data Source={databasePath};Pooling=False");
        process.StartInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";

        Assert.True(process.Start());
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        try
        {
            using var client = new HttpClient(new HttpClientHandler
            {
                UseProxy = false,
                AllowAutoRedirect = false,
            })
            {
                BaseAddress = new Uri($"http://127.0.0.1:{port}"),
            };
            HttpResponseMessage? response = null;
            for (var attempt = 0; attempt < 100 && !process.HasExited; attempt++)
            {
                try
                {
                    response = await client.GetAsync("/api/auth/options");
                    break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100);
                }
            }

            if (process.HasExited)
            {
                Assert.Fail($"Verification API exited before readiness. {await standardErrorTask}");
            }
            Assert.NotNull(response);
            Assert.Equal(System.Net.HttpStatusCode.OK, response.StatusCode);
        }
        finally
        {
            await StopProcessAsync(process);
            _ = await standardOutputTask;
            _ = await standardErrorTask;
        }

        Assert.True(File.Exists(databasePath));
        await using (var connection = new SqliteConnection($"Data Source={databasePath};Mode=ReadOnly;Pooling=False"))
        {
            await connection.OpenAsync();
            await using var migrations = connection.CreateCommand();
            migrations.CommandText = "SELECT COUNT(*) FROM __EFMigrationsHistory;";
            Assert.True(Convert.ToInt32(await migrations.ExecuteScalarAsync()) > 0);

            await using var seedData = connection.CreateCommand();
            seedData.CommandText = "SELECT COUNT(*) FROM database_objects;";
            Assert.Equal(0, Convert.ToInt32(await seedData.ExecuteScalarAsync()));
        }

        var logText = string.Join(
            Environment.NewLine,
            Directory.EnumerateFiles(Path.Combine(scenarioRoot, "logs"), "*.log")
                .Select(File.ReadAllText));
        Assert.Contains("System Knowledge Hub host is starting in Verification", logText);
        Assert.Contains("Verification runtime SQLite Data Source resolved to", logText);
        Assert.Contains(databasePath, logText);
    }

    private static Process CreateVerificationProcess(string scenarioRoot, string? connectionString)
    {
        var outputDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)
            ?? throw new InvalidOperationException("The API output directory could not be resolved.");
        var executablePath = Path.Combine(outputDirectory, "SystemKnowledgeHub.Api.exe");
        Assert.True(File.Exists(executablePath), $"Expected API executable at '{executablePath}'.");

        var process = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = executablePath,
                WorkingDirectory = outputDirectory,
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            },
        };
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] =
            IsolatedRuntimeStorageGuard.VerificationEnvironmentName;
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] =
            IsolatedRuntimeStorageGuard.VerificationEnvironmentName;
        process.StartInfo.Environment["Authentication__Local__Enabled"] = "true";
        process.StartInfo.Environment["Authentication__Oidc__Enabled"] = "false";
        process.StartInfo.Environment["DataProtection__KeyPath"] = Path.Combine(scenarioRoot, "keys");
        process.StartInfo.Environment["Attachments__StorageRoot"] = Path.Combine(scenarioRoot, "attachments");
        process.StartInfo.Environment["Serilog__WriteTo__1__Args__path"] =
            Path.Combine(scenarioRoot, "logs", "verification-.log");
        process.StartInfo.Environment["DOTNET_DISABLE_GUI_ERRORS"] = "1";
        if (connectionString is null)
        {
            process.StartInfo.Environment.Remove("ConnectionStrings__KnowledgeHub");
        }
        else
        {
            process.StartInfo.Environment["ConnectionStrings__KnowledgeHub"] = connectionString;
        }
        return process;
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunToExitAsync(
        Process process)
    {
        Assert.True(process.Start());
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        await process.WaitForExitAsync(timeout.Token);
        return (process.ExitCode, await standardOutputTask, await standardErrorTask);
    }

    private static async Task StopProcessAsync(Process process)
    {
        if (!process.HasExited)
        {
            process.Kill(entireProcessTree: true);
            await process.WaitForExitAsync();
        }
    }

    private static int ReservePort()
    {
        using var reservation = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback,
            0);
        reservation.Start();
        return ((System.Net.IPEndPoint)reservation.LocalEndpoint).Port;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "SystemKnowledgeHub.sln"))
                && Directory.Exists(Path.Combine(directory.FullName, "src", "SystemKnowledgeHub.Api")))
            {
                return directory.FullName;
            }
            directory = directory.Parent;
        }
        throw new InvalidOperationException("The repository root could not be located.");
    }

    private static async Task DeleteTemporaryRootAsync(string temporaryRoot)
    {
        for (var attempt = 0; attempt < 20; attempt++)
        {
            try
            {
                if (Directory.Exists(temporaryRoot))
                {
                    Directory.Delete(temporaryRoot, recursive: true);
                }
                return;
            }
            catch (IOException) when (attempt < 19)
            {
                await Task.Delay(50);
            }
            catch (UnauthorizedAccessException) when (attempt < 19)
            {
                await Task.Delay(50);
            }
        }
    }

    private sealed record RepositorySqliteFingerprint(
        FileFingerprint Database,
        FileFingerprint Wal,
        FileFingerprint Shm)
    {
        public static RepositorySqliteFingerprint Capture(string databasePath) => new(
            FileFingerprint.Capture(databasePath),
            FileFingerprint.Capture($"{databasePath}-wal"),
            FileFingerprint.Capture($"{databasePath}-shm"));
    }

    private sealed record FileFingerprint(
        bool Exists,
        long? Size,
        DateTime? LastWriteTimeUtc,
        string? Sha256)
    {
        public static FileFingerprint Capture(string path)
        {
            if (!File.Exists(path))
            {
                return new FileFingerprint(false, null, null, null);
            }

            var info = new FileInfo(path);
            using var stream = new FileStream(
                path,
                FileMode.Open,
                FileAccess.Read,
                FileShare.ReadWrite | FileShare.Delete);
            return new FileFingerprint(
                true,
                info.Length,
                info.LastWriteTimeUtc,
                Convert.ToHexString(SHA256.HashData(stream)));
        }
    }
}

[CollectionDefinition("Repository SQLite Sentinel", DisableParallelization = true)]
public sealed class RepositorySqliteSentinelCollection;
