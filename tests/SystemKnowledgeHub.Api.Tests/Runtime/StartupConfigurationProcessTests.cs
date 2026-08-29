using System.Diagnostics;

namespace SystemKnowledgeHub.Api.Tests.Runtime;

public sealed class StartupConfigurationProcessTests
{
    [Fact]
    public async Task DirectExecutable_WithAuthenticationDisabledInProduction_ExitsWithActionableDiagnostic()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var outputDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)
            ?? throw new InvalidOperationException("The API output directory could not be resolved.");
        var executablePath = Path.Combine(outputDirectory, "SystemKnowledgeHub.Api.exe");
        Assert.True(File.Exists(executablePath), $"Expected API executable at '{executablePath}'.");

        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);

        using var process = new Process
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
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = "Production";
        process.StartInfo.Environment["Authentication__Local__Enabled"] = "false";
        process.StartInfo.Environment["Authentication__Oidc__Enabled"] = "false";
        process.StartInfo.Environment["ConnectionStrings__KnowledgeHub"] =
            $"Data Source={Path.Combine(temporaryRoot, "knowledge-hub.db")}";
        process.StartInfo.Environment["DataProtection__KeyPath"] = Path.Combine(temporaryRoot, "keys");
        process.StartInfo.Environment["Attachments__StorageRoot"] = Path.Combine(temporaryRoot, "attachments");
        process.StartInfo.Environment["DOTNET_DISABLE_GUI_ERRORS"] = "1";

        try
        {
            Assert.True(process.Start());
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));

            await process.WaitForExitAsync(timeout.Token);
            var standardOutput = await standardOutputTask;
            var standardError = await standardErrorTask;

            Assert.Equal(1, process.ExitCode);
            Assert.Empty(standardOutput);
            Assert.Contains("启动配置错误（环境：Production）", standardError);
            Assert.Contains("至少必须启用 Authentication:Local 或 Authentication:Oidc 之一。", standardError);
            Assert.Contains("直接启动 SystemKnowledgeHub.Api.exe 不会应用 Properties/launchSettings.json。", standardError);
            Assert.DoesNotContain("Unhandled exception", standardError);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                await process.WaitForExitAsync();
            }

            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("oidc", "启用 OIDC 时必须配置 Authentication:Oidc Provider、Authority 和 ClientId。")]
    [InlineData("data-protection-application-name", "Production Data Protection requires DataProtection:ApplicationName.")]
    [InlineData("data-protection-missing", "Production Data Protection requires DataProtection:KeyPath.")]
    [InlineData("data-protection-relative", "Production DataProtection:KeyPath must be an absolute persistent path outside the application deployment directory.")]
    [InlineData("sqlite-relative", "Production ConnectionStrings:KnowledgeHub must use an absolute persistent SQLite Data Source path.")]
    [InlineData("attachment-storage-missing", "Attachments:StorageRoot is required outside Development and must identify isolated persistent storage.")]
    [InlineData("attachment-storage-relative", "Attachments:StorageRoot must be an absolute persistent path outside the application deployment directory.")]
    public async Task DirectExecutable_WithOtherInvalidProductionConfiguration_ExitsWithActionableDiagnostic(
        string scenario,
        string expectedDiagnostic)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var temporaryRoot = CreateTemporaryRoot();
        using var process = CreateApiProcess("Production", temporaryRoot);
        process.StartInfo.Environment["Authentication__Local__Enabled"] = "true";
        process.StartInfo.Environment["Authentication__Oidc__Enabled"] = "false";

        switch (scenario)
        {
            case "oidc":
                process.StartInfo.Environment["Authentication__Local__Enabled"] = "false";
                process.StartInfo.Environment["Authentication__Oidc__Enabled"] = "true";
                process.StartInfo.Environment["Authentication__Oidc__Provider"] = "";
                process.StartInfo.Environment["Authentication__Oidc__Authority"] = "";
                process.StartInfo.Environment["Authentication__Oidc__ClientId"] = "";
                break;
            case "data-protection-application-name":
                process.StartInfo.Environment["DataProtection__ApplicationName"] = "";
                break;
            case "data-protection-missing":
                process.StartInfo.Environment["DataProtection__KeyPath"] = "";
                break;
            case "data-protection-relative":
                process.StartInfo.Environment["DataProtection__KeyPath"] = "keys";
                break;
            case "sqlite-relative":
                process.StartInfo.Environment["ConnectionStrings__KnowledgeHub"] =
                    "Data Source=App_Data/production.db";
                break;
            case "attachment-storage-missing":
                process.StartInfo.Environment["Attachments__StorageRoot"] = "";
                break;
            case "attachment-storage-relative":
                process.StartInfo.Environment["Attachments__StorageRoot"] = "App_Data/attachments";
                break;
            default:
                throw new InvalidOperationException($"Unknown test scenario '{scenario}'.");
        }

        try
        {
            var result = await RunToExitAsync(process);

            Assert.Equal(1, result.ExitCode);
            Assert.Empty(result.StandardOutput);
            Assert.Contains("启动配置错误（环境：Production）", result.StandardError);
            Assert.Contains(expectedDiagnostic, result.StandardError);
            Assert.DoesNotContain("Unhandled exception", result.StandardError);
            Assert.DoesNotContain("0xe0434352", result.StandardError, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            await StopProcessAsync(process);
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    [Theory]
    [InlineData("Production", true)]
    [InlineData("Development", false)]
    public async Task DirectExecutable_WithIsolatedValidConfiguration_StartsAndKeepsAuthenticationClosed(
        string environment,
        bool explicitlyEnableLocal)
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        var temporaryRoot = CreateTemporaryRoot();
        using var portReservation = new System.Net.Sockets.TcpListener(
            System.Net.IPAddress.Loopback,
            0);
        portReservation.Start();
        var port = ((System.Net.IPEndPoint)portReservation.LocalEndpoint).Port;
        portReservation.Stop();

        using var process = CreateApiProcess(environment, temporaryRoot);
        process.StartInfo.Environment["ASPNETCORE_URLS"] = $"http://127.0.0.1:{port}";
        process.StartInfo.Environment["Authentication__Oidc__Enabled"] = "false";
        if (explicitlyEnableLocal)
        {
            process.StartInfo.Environment["Authentication__Local__Enabled"] = "true";
        }
        else
        {
            process.StartInfo.Environment.Remove("Authentication__Local__Enabled");
        }

        try
        {
            Assert.True(process.Start());
            var standardOutputTask = process.StandardOutput.ReadToEndAsync();
            var standardErrorTask = process.StandardError.ReadToEndAsync();
            using var client = new HttpClient { BaseAddress = new Uri($"http://127.0.0.1:{port}") };

            HttpResponseMessage? optionsResponse = null;
            for (var attempt = 0; attempt < 60; attempt++)
            {
                if (process.HasExited)
                {
                    break;
                }

                try
                {
                    optionsResponse = await client.GetAsync("/api/auth/options");
                    break;
                }
                catch (HttpRequestException)
                {
                    await Task.Delay(100);
                }
            }

            if (process.HasExited)
            {
                Assert.Fail($"The API exited before the smoke request. {await standardErrorTask}");
            }
            Assert.NotNull(optionsResponse);
            Assert.Equal(System.Net.HttpStatusCode.OK, optionsResponse.StatusCode);
            var optionsBody = await optionsResponse.Content.ReadAsStringAsync();
            Assert.Contains("\"localLoginEnabled\":true", optionsBody);
            Assert.Contains("\"oidcLoginEnabled\":false", optionsBody);

            var currentUserResponse = await client.GetAsync("/api/current-user");
            Assert.Equal(System.Net.HttpStatusCode.Unauthorized, currentUserResponse.StatusCode);
        }
        finally
        {
            await StopProcessAsync(process);
            Directory.Delete(temporaryRoot, recursive: true);
        }
    }

    private static string CreateTemporaryRoot()
    {
        var temporaryRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(temporaryRoot);
        return temporaryRoot;
    }

    private static Process CreateApiProcess(string environment, string temporaryRoot)
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
        process.StartInfo.Environment["ASPNETCORE_ENVIRONMENT"] = environment;
        process.StartInfo.Environment["DOTNET_ENVIRONMENT"] = environment;
        process.StartInfo.Environment["ConnectionStrings__KnowledgeHub"] =
            $"Data Source={Path.Combine(temporaryRoot, "knowledge-hub.db")}";
        process.StartInfo.Environment["DataProtection__KeyPath"] = Path.Combine(temporaryRoot, "keys");
        process.StartInfo.Environment["Attachments__StorageRoot"] = Path.Combine(temporaryRoot, "attachments");
        process.StartInfo.Environment["DOTNET_DISABLE_GUI_ERRORS"] = "1";
        return process;
    }

    private static async Task<(int ExitCode, string StandardOutput, string StandardError)> RunToExitAsync(
        Process process)
    {
        Assert.True(process.Start());
        var standardOutputTask = process.StandardOutput.ReadToEndAsync();
        var standardErrorTask = process.StandardError.ReadToEndAsync();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(15));
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
}
