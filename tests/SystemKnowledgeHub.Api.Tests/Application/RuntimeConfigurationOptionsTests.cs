using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Shared.Configuration;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class RuntimeConfigurationOptionsTests
{
    [Fact]
    public void Runtime_option_defaults_are_valid_and_match_the_supported_baseline()
    {
        var cookie = new AuthenticationCookieOptions();
        var passwordHashing = new PasswordHashingOptions();
        var sqlite = new SqlitePersistenceOptions();

        Assert.Null(cookie.GetValidationError());
        Assert.Equal(8, cookie.ExpireHours);
        Assert.True(cookie.SlidingExpiration);
        Assert.Null(passwordHashing.GetValidationError());
        Assert.Equal(220_000, passwordHashing.IterationCount);
        Assert.Null(sqlite.GetValidationError());
        Assert.Equal(5, sqlite.DefaultTimeoutSeconds);
        Assert.Equal(5_000, sqlite.BusyTimeoutMilliseconds);
    }

    [Theory]
    [InlineData(0, false)]
    [InlineData(1, true)]
    [InlineData(720, true)]
    [InlineData(721, false)]
    public void Cookie_expiration_validation_is_bounded(int hours, bool valid)
    {
        var error = new AuthenticationCookieOptions { ExpireHours = hours }.GetValidationError();
        Assert.Equal(valid, error is null);
    }

    [Theory]
    [InlineData(219_999, false)]
    [InlineData(220_000, true)]
    [InlineData(2_000_000, true)]
    [InlineData(2_000_001, false)]
    public void Password_iteration_validation_preserves_the_security_floor(int iterations, bool valid)
    {
        var error = new PasswordHashingOptions { IterationCount = iterations }.GetValidationError();
        Assert.Equal(valid, error is null);
    }

    [Theory]
    [InlineData(0, 5_000, false)]
    [InlineData(1, 1, true)]
    [InlineData(300, 300_000, true)]
    [InlineData(301, 5_000, false)]
    [InlineData(5, 300_001, false)]
    public void Sqlite_timeout_validation_is_bounded(
        int defaultTimeoutSeconds,
        int busyTimeoutMilliseconds,
        bool valid)
    {
        var error = new SqlitePersistenceOptions
        {
            DefaultTimeoutSeconds = defaultTimeoutSeconds,
            BusyTimeoutMilliseconds = busyTimeoutMilliseconds,
        }.GetValidationError();
        Assert.Equal(valid, error is null);
    }

    [Fact]
    public void Cors_validation_requires_explicit_unique_origins_and_a_development_origin()
    {
        Assert.NotNull(new CorsRuntimeOptions().GetValidationError(requireOrigins: true));
        Assert.Null(new CorsRuntimeOptions().GetValidationError(requireOrigins: false));
        Assert.Null(new CorsRuntimeOptions
        {
            AllowedOrigins = ["http://localhost:5173", "https://portal.example.test"],
        }.GetValidationError(requireOrigins: true));
        Assert.NotNull(new CorsRuntimeOptions
        {
            AllowedOrigins = ["https://portal.example.test", "HTTPS://PORTAL.EXAMPLE.TEST"],
        }.GetValidationError(requireOrigins: false));
        Assert.NotNull(new CorsRuntimeOptions
        {
            AllowedOrigins = ["https://*.example.test"],
        }.GetValidationError(requireOrigins: false));
        Assert.NotNull(new CorsRuntimeOptions
        {
            AllowedOrigins = ["https://portal.example.test/path"],
        }.GetValidationError(requireOrigins: false));
    }

    [Fact]
    public void Serilog_validation_accepts_the_required_console_and_rolling_file_sinks()
    {
        var configuration = SerilogConfiguration();
        Assert.Null(SerilogConfigurationValidator.GetValidationError(configuration));
    }

    [Theory]
    [InlineData("Serilog:MinimumLevel:Default", "NoSuchLevel", "valid Serilog level")]
    [InlineData("Serilog:MinimumLevel:Default", "Debug", "Information or higher")]
    [InlineData("Serilog:MinimumLevel:Override:Microsoft.AspNetCore", "Information", "Warning or higher")]
    [InlineData("Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command", "Debug", "Warning or higher")]
    [InlineData("Serilog:MinimumLevel:Override:Microsoft.AspNetCore.Mvc", "Debug", "Warning or higher")]
    [InlineData("Serilog:WriteTo:1:Args:path", "", "non-empty path")]
    [InlineData("Serilog:WriteTo:1:Args:rollingInterval", "NeverEver", "rollingInterval")]
    [InlineData("Serilog:WriteTo:1:Args:retainedFileCountLimit", "0", "between 1 and 3650")]
    [InlineData("Serilog:WriteTo:1:Args:fileSizeLimitBytes", "1024", "between 1048576")]
    [InlineData("Serilog:WriteTo:1:Args:rollOnFileSizeLimit", "sometimes", "true or false")]
    public void Serilog_validation_fails_closed_for_invalid_sink_settings(
        string key,
        string value,
        string expectedError)
    {
        var values = ValidSerilogValues();
        values[key] = value;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var error = SerilogConfigurationValidator.GetValidationError(configuration);

        Assert.NotNull(error);
        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Serilog_validation_requires_explicit_framework_safety_overrides()
    {
        var values = ValidSerilogValues();
        values.Remove("Serilog:MinimumLevel:Override:Microsoft.AspNetCore");
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        var error = SerilogConfigurationValidator.GetValidationError(configuration);

        Assert.NotNull(error);
        Assert.Contains("Microsoft.AspNetCore is required", error, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("Testing")]
    [InlineData("Verification")]
    public void Isolated_runtime_storage_guard_accepts_only_explicit_task_owned_absolute_paths(
        string environmentName)
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "storage-guard",
            Guid.NewGuid().ToString("N"));
        var contentRoot = Path.Combine(testRoot, "content-root");
        var taskRoot = Path.Combine(testRoot, "task-owned");
        var environment = new TestWebHostEnvironment(contentRoot)
        {
            EnvironmentName = environmentName,
        };
        var configuration = IsolatedRuntimeConfiguration(taskRoot);

        Assert.True(IsolatedRuntimeStorageGuard.TryResolve(
            configuration,
            environment,
            out var paths,
            out var error));
        Assert.Null(error);
        Assert.NotNull(paths);
        Assert.Equal(Path.Combine(taskRoot, "knowledge-hub.db"), paths.SqliteDataSourcePath);
        Assert.Equal(Path.Combine(taskRoot, "keys"), paths.DataProtectionKeyPath);
        Assert.Equal(Path.Combine(taskRoot, "attachments"), paths.AttachmentStorageRoot);
        Assert.Equal(Path.Combine(taskRoot, "logs", "runtime-.log"), paths.SerilogFilePath);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Data Source=App_Data/system-knowledge-hub.db")]
    [InlineData("Data Source=:memory:")]
    public void Isolated_runtime_storage_guard_rejects_missing_relative_or_memory_sqlite(
        string? connectionString)
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "storage-guard",
            Guid.NewGuid().ToString("N"));
        var environment = new TestWebHostEnvironment(Path.Combine(testRoot, "content-root"))
        {
            EnvironmentName = IsolatedRuntimeStorageGuard.VerificationEnvironmentName,
        };
        var values = IsolatedRuntimeValues(Path.Combine(testRoot, "task-owned"));
        values["ConnectionStrings:KnowledgeHub"] = connectionString;
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.False(IsolatedRuntimeStorageGuard.TryResolve(
            configuration,
            environment,
            out var paths,
            out var error));
        Assert.Null(paths);
        Assert.Equal(IsolatedRuntimeStorageGuard.SqliteError, error);
    }

    [Fact]
    public void Isolated_runtime_storage_guard_rejects_repository_or_content_root_paths()
    {
        var contentRoot = Path.GetFullPath(Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "storage-guard-content",
            Guid.NewGuid().ToString("N")));
        var environment = new TestWebHostEnvironment(contentRoot)
        {
            EnvironmentName = IsolatedRuntimeStorageGuard.TestingEnvironmentName,
        };
        var values = IsolatedRuntimeValues(Path.Combine(Path.GetTempPath(), "safe-task", Guid.NewGuid().ToString("N")));
        values["ConnectionStrings:KnowledgeHub"] =
            $"Data Source={Path.Combine(contentRoot, "App_Data", "system-knowledge-hub.db")}";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.False(IsolatedRuntimeStorageGuard.TryResolve(
            configuration,
            environment,
            out _,
            out var error));
        Assert.Equal(IsolatedRuntimeStorageGuard.SqliteError, error);
    }

    [Theory]
    [InlineData("src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db")]
    [InlineData("publish/state/knowledge-hub.db")]
    [InlineData("bin/Release/state/knowledge-hub.db")]
    public void Isolated_runtime_storage_guard_rejects_source_and_build_output_paths(
        string unsafeRelativePath)
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "storage-guard",
            Guid.NewGuid().ToString("N"));
        var environment = new TestWebHostEnvironment(Path.Combine(testRoot, "unrelated-content-root"))
        {
            EnvironmentName = IsolatedRuntimeStorageGuard.VerificationEnvironmentName,
        };
        var values = IsolatedRuntimeValues(Path.Combine(testRoot, "task-owned"));
        values["ConnectionStrings:KnowledgeHub"] =
            $"Data Source={Path.GetFullPath(Path.Combine(testRoot, "outside-content", unsafeRelativePath))}";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.False(IsolatedRuntimeStorageGuard.TryResolve(
            configuration,
            environment,
            out _,
            out var error));
        Assert.Equal(IsolatedRuntimeStorageGuard.SqliteError, error);
    }

    [Theory]
    [InlineData("DataProtection:KeyPath", "Data Protection key path")]
    [InlineData("Attachments:StorageRoot", "Attachment StorageRoot")]
    [InlineData("Serilog:WriteTo:1:Args:path", "Serilog file path")]
    public void Isolated_runtime_storage_guard_rejects_inherited_relative_auxiliary_paths(
        string key,
        string expectedError)
    {
        var testRoot = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "storage-guard",
            Guid.NewGuid().ToString("N"));
        var environment = new TestWebHostEnvironment(Path.Combine(testRoot, "content-root"))
        {
            EnvironmentName = IsolatedRuntimeStorageGuard.VerificationEnvironmentName,
        };
        var values = IsolatedRuntimeValues(Path.Combine(testRoot, "task-owned"));
        values[key] = "relative/path";
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(values).Build();

        Assert.False(IsolatedRuntimeStorageGuard.TryResolve(
            configuration,
            environment,
            out _,
            out var error));
        Assert.Contains(expectedError, error, StringComparison.Ordinal);
    }

    [Fact]
    public void Configured_cookie_and_password_hashing_values_are_wired_into_framework_options()
    {
        using var factory = new ConfiguredBootstrapWebApplicationFactory(new Dictionary<string, string>
        {
            ["Authentication:Cookie:ExpireHours"] = "36",
            ["Authentication:Cookie:SlidingExpiration"] = "false",
            ["Authentication:Local:PasswordHasher:IterationCount"] = "345678",
        });
        using var client = factory.CreateClient();

        var typedCookie = factory.Services.GetRequiredService<IOptions<AuthenticationCookieOptions>>().Value;
        var typedPassword = factory.Services.GetRequiredService<IOptions<PasswordHashingOptions>>().Value;
        var cookie = factory.Services
            .GetRequiredService<IOptionsMonitor<CookieAuthenticationOptions>>()
            .Get(CurrentUserContext.CookieScheme);
        var hasher = factory.Services.GetRequiredService<IOptions<PasswordHasherOptions>>().Value;

        Assert.Equal(36, typedCookie.ExpireHours);
        Assert.False(typedCookie.SlidingExpiration);
        Assert.Equal(345_678, typedPassword.IterationCount);
        Assert.Equal(TimeSpan.FromHours(36), cookie.ExpireTimeSpan);
        Assert.False(cookie.SlidingExpiration);
        Assert.Equal(PasswordHasherCompatibilityMode.IdentityV3, hasher.CompatibilityMode);
        Assert.Equal(345_678, hasher.IterationCount);
    }

    private static IConfiguration SerilogConfiguration() =>
        new ConfigurationBuilder().AddInMemoryCollection(ValidSerilogValues()).Build();

    private static IConfiguration IsolatedRuntimeConfiguration(string taskRoot) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(IsolatedRuntimeValues(taskRoot))
            .Build();

    private static Dictionary<string, string?> IsolatedRuntimeValues(string taskRoot) => new()
    {
        ["ConnectionStrings:KnowledgeHub"] = $"Data Source={Path.Combine(taskRoot, "knowledge-hub.db")}",
        ["DataProtection:KeyPath"] = Path.Combine(taskRoot, "keys"),
        ["Attachments:StorageRoot"] = Path.Combine(taskRoot, "attachments"),
        ["Serilog:WriteTo:0:Name"] = "Console",
        ["Serilog:WriteTo:1:Name"] = "File",
        ["Serilog:WriteTo:1:Args:path"] = Path.Combine(taskRoot, "logs", "runtime-.log"),
    };

    private static Dictionary<string, string?> ValidSerilogValues() => new()
    {
        ["Serilog:MinimumLevel:Default"] = "Information",
        ["Serilog:MinimumLevel:Override:Microsoft"] = "Warning",
        ["Serilog:MinimumLevel:Override:Microsoft.AspNetCore"] = "Warning",
        ["Serilog:MinimumLevel:Override:Microsoft.EntityFrameworkCore.Database.Command"] = "Warning",
        ["Serilog:WriteTo:0:Name"] = "Console",
        ["Serilog:WriteTo:1:Name"] = "File",
        ["Serilog:WriteTo:1:Args:path"] = Path.Combine(
            Path.GetTempPath(),
            "SystemKnowledgeHub.Api.Tests",
            "configuration",
            "system-knowledge-hub-test-.log"),
        ["Serilog:WriteTo:1:Args:rollingInterval"] = "Day",
        ["Serilog:WriteTo:1:Args:retainedFileCountLimit"] = "14",
        ["Serilog:WriteTo:1:Args:fileSizeLimitBytes"] = "104857600",
        ["Serilog:WriteTo:1:Args:rollOnFileSizeLimit"] = "true",
    };
}
