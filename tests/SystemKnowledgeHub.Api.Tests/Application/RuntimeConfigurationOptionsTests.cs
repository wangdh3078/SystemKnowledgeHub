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
