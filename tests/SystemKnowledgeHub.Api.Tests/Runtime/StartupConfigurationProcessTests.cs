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
}
