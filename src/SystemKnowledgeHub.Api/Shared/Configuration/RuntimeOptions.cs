namespace SystemKnowledgeHub.Api.Shared.Configuration;

public sealed class AuthenticationCookieOptions
{
    public const string SectionName = "Authentication:Cookie";
    public const int DefaultExpireHours = 8;

    public int ExpireHours { get; init; } = DefaultExpireHours;
    public bool SlidingExpiration { get; init; } = true;

    public string? GetValidationError() => ExpireHours is < 1 or > 720
        ? "Authentication:Cookie:ExpireHours must be between 1 and 720."
        : null;
}

public sealed class PasswordHashingOptions
{
    public const string SectionName = "Authentication:Local:PasswordHasher";
    public const int DefaultIterationCount = 220_000;
    public const int MinimumIterationCount = 220_000;
    private const int MaximumIterationCount = 2_000_000;

    public int IterationCount { get; init; } = DefaultIterationCount;

    public string? GetValidationError() =>
        IterationCount is < MinimumIterationCount or > MaximumIterationCount
            ? $"Authentication:Local:PasswordHasher:IterationCount must be between {MinimumIterationCount} and {MaximumIterationCount}."
            : null;
}

public static class SerilogConfigurationValidator
{
    private static readonly string[] RequiredWarningOverrides =
    [
        "Microsoft",
        "Microsoft.AspNetCore",
        "Microsoft.EntityFrameworkCore.Database.Command",
    ];

    public static string? GetValidationError(IConfiguration configuration)
    {
        var section = configuration.GetSection("Serilog");
        if (!TryReadLevel(section["MinimumLevel:Default"], out var defaultLevel))
        {
            return "Serilog:MinimumLevel:Default must be a valid Serilog level.";
        }
        if (defaultLevel < Serilog.Events.LogEventLevel.Information)
        {
            return "Serilog:MinimumLevel:Default must be Information or higher to preserve the sensitive-data logging boundary.";
        }

        var overrideLevels = new Dictionary<string, Serilog.Events.LogEventLevel>(StringComparer.OrdinalIgnoreCase);
        foreach (var item in section.GetSection("MinimumLevel:Override").GetChildren())
        {
            if (!TryReadLevel(item.Value, out var level))
            {
                return $"Serilog:MinimumLevel:Override:{item.Key} must be a valid Serilog level.";
            }
            if ((string.Equals(item.Key, "Microsoft", StringComparison.OrdinalIgnoreCase)
                    || item.Key.StartsWith("Microsoft.", StringComparison.OrdinalIgnoreCase))
                && level < Serilog.Events.LogEventLevel.Warning)
            {
                return $"Serilog:MinimumLevel:Override:{item.Key} must be Warning or higher to prevent framework request and database-command detail logging.";
            }
            if (level < Serilog.Events.LogEventLevel.Information)
            {
                return $"Serilog:MinimumLevel:Override:{item.Key} must be Information or higher to preserve the sensitive-data logging boundary.";
            }
            overrideLevels[item.Key] = level;
        }
        foreach (var requiredNamespace in RequiredWarningOverrides)
        {
            if (!overrideLevels.TryGetValue(requiredNamespace, out var level)
                || level < Serilog.Events.LogEventLevel.Warning)
            {
                return $"Serilog:MinimumLevel:Override:{requiredNamespace} is required and must be Warning or higher.";
            }
        }

        var sinks = section.GetSection("WriteTo").GetChildren().ToArray();
        if (!sinks.Any(item => string.Equals(item["Name"], "Console", StringComparison.Ordinal)))
        {
            return "Serilog:WriteTo must contain the Console sink.";
        }
        var files = sinks
            .Where(item => string.Equals(item["Name"], "File", StringComparison.Ordinal))
            .ToArray();
        if (files.Length != 1)
        {
            return "Serilog:WriteTo must contain exactly one rolling File sink.";
        }
        var file = files[0];
        var path = file["Args:path"]?.Trim();
        if (string.IsNullOrWhiteSpace(path))
        {
            return "Serilog File sink requires a non-empty path.";
        }
        try
        {
            var resolved = Path.GetFullPath(path);
            if (string.Equals(
                resolved.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                Path.GetPathRoot(resolved)?.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar),
                OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal))
            {
                return "Serilog File sink path cannot be a filesystem root.";
            }
        }
        catch (Exception exception) when (exception is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return "Serilog File sink path is invalid.";
        }
        if (!Enum.TryParse<Serilog.RollingInterval>(file["Args:rollingInterval"], true, out var rollingInterval)
            || !Enum.IsDefined(rollingInterval))
        {
            return "Serilog File sink rollingInterval is invalid.";
        }
        if (!int.TryParse(file["Args:retainedFileCountLimit"], out var retained)
            || retained is < 1 or > 3_650)
        {
            return "Serilog File sink retainedFileCountLimit must be between 1 and 3650.";
        }
        if (!long.TryParse(file["Args:fileSizeLimitBytes"], out var fileSize)
            || fileSize is < 1_048_576 or > 1_073_741_824)
        {
            return "Serilog File sink fileSizeLimitBytes must be between 1048576 and 1073741824.";
        }
        if (!bool.TryParse(file["Args:rollOnFileSizeLimit"], out _))
        {
            return "Serilog File sink rollOnFileSizeLimit must be true or false.";
        }
        return null;
    }

    private static bool TryReadLevel(
        string? value,
        out Serilog.Events.LogEventLevel level) =>
        Enum.TryParse(value, true, out level) && Enum.IsDefined(level);
}

public sealed class SqlitePersistenceOptions
{
    public const string SectionName = "Persistence:Sqlite";
    public const int DefaultTimeoutSecondsDefault = 5;
    public const int BusyTimeoutMillisecondsDefault = 5_000;

    public int DefaultTimeoutSeconds { get; init; } = DefaultTimeoutSecondsDefault;
    public int BusyTimeoutMilliseconds { get; init; } = BusyTimeoutMillisecondsDefault;

    public string? GetValidationError()
    {
        if (DefaultTimeoutSeconds is < 1 or > 300)
        {
            return "Persistence:Sqlite:DefaultTimeoutSeconds must be between 1 and 300.";
        }
        return BusyTimeoutMilliseconds is < 1 or > 300_000
            ? "Persistence:Sqlite:BusyTimeoutMilliseconds must be between 1 and 300000."
            : null;
    }
}

public sealed class CorsRuntimeOptions
{
    public const string SectionName = "Cors";

    public string[] AllowedOrigins { get; init; } = [];

    public string? GetValidationError(bool requireOrigins)
    {
        if (requireOrigins && AllowedOrigins.Length == 0)
        {
            return "Development requires at least one Cors:AllowedOrigins entry.";
        }
        if (AllowedOrigins.Length != AllowedOrigins.Distinct(StringComparer.OrdinalIgnoreCase).Count())
        {
            return "Cors:AllowedOrigins cannot contain duplicate origins.";
        }
        foreach (var configured in AllowedOrigins)
        {
            if (string.IsNullOrWhiteSpace(configured)
                || configured.Contains('*', StringComparison.Ordinal)
                || !Uri.TryCreate(configured, UriKind.Absolute, out var origin)
                || (origin.Scheme != Uri.UriSchemeHttp && origin.Scheme != Uri.UriSchemeHttps)
                || !string.IsNullOrEmpty(origin.UserInfo)
                || origin.AbsolutePath != "/"
                || !string.IsNullOrEmpty(origin.Query)
                || !string.IsNullOrEmpty(origin.Fragment)
                || !string.Equals(
                    configured.Trim(),
                    origin.GetLeftPart(UriPartial.Authority),
                    StringComparison.OrdinalIgnoreCase))
            {
                return "Cors:AllowedOrigins entries must be explicit HTTP(S) origins without wildcard, credentials, path, query, or fragment.";
            }
        }
        return null;
    }
}
