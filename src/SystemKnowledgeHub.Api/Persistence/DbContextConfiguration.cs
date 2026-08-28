using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Persistence;

public static class DbContextConfiguration
{
    public static string? GetProductionConfigurationError(IConfiguration configuration)
    {
        var configuredConnectionString = configuration.GetConnectionString("KnowledgeHub");
        if (string.IsNullOrWhiteSpace(configuredConnectionString))
        {
            return "Production requires ConnectionStrings:KnowledgeHub.";
        }

        SqliteConnectionStringBuilder builder;
        try
        {
            builder = new SqliteConnectionStringBuilder(configuredConnectionString);
        }
        catch (ArgumentException)
        {
            return "Production ConnectionStrings:KnowledgeHub must be a valid SQLite connection string.";
        }

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            return "Production ConnectionStrings:KnowledgeHub must define a SQLite Data Source.";
        }

        if (builder.DataSource is ":memory:"
            || builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            || !Path.IsPathRooted(builder.DataSource))
        {
            return "Production ConnectionStrings:KnowledgeHub must use an absolute persistent SQLite Data Source path.";
        }

        return null;
    }

    public static IServiceCollection AddKnowledgeHubPersistence(
        this IServiceCollection services,
        IConfiguration configuration,
        IWebHostEnvironment environment)
    {
        var configuredConnectionString = configuration.GetConnectionString("KnowledgeHub")
            ?? throw new InvalidOperationException(
                "Connection string 'KnowledgeHub' is required.");

        var connectionString = ResolveConnectionString(
            configuredConnectionString,
            environment.ContentRootPath);

        services.AddDbContext<KnowledgeHubDbContext>(options =>
        {
            options.UseSqlite(connectionString);
            options.AddInterceptors(SqlitePragmaInterceptor.Instance);
        });
        services.AddSingleton<ConcurrencyTokenCodec>();

        return services;
    }

    private static string ResolveConnectionString(
        string configuredConnectionString,
        string contentRootPath)
    {
        var builder = new SqliteConnectionStringBuilder(configuredConnectionString)
        {
            ForeignKeys = true,
            DefaultTimeout = 5,
        };

        if (string.IsNullOrWhiteSpace(builder.DataSource))
        {
            throw new InvalidOperationException(
                "Connection string 'KnowledgeHub' must define a SQLite Data Source.");
        }

        if (builder.DataSource is not ":memory:"
            && !builder.DataSource.StartsWith("file:", StringComparison.OrdinalIgnoreCase)
            && !Path.IsPathRooted(builder.DataSource))
        {
            var absolutePath = Path.GetFullPath(
                Path.Combine(contentRootPath, builder.DataSource));
            var directory = Path.GetDirectoryName(absolutePath)
                ?? throw new InvalidOperationException(
                    "The configured SQLite Data Source has no parent directory.");

            Directory.CreateDirectory(directory);
            builder.DataSource = absolutePath;
        }

        return builder.ToString();
    }

    private sealed class SqlitePragmaInterceptor : DbConnectionInterceptor
    {
        public static readonly SqlitePragmaInterceptor Instance = new();

        public override void ConnectionOpened(
            DbConnection connection,
            ConnectionEndEventData eventData)
        {
            ApplyPragmas(connection);
        }

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await ApplyPragmasAsync(connection, cancellationToken);
        }

        private static void ApplyPragmas(DbConnection connection)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000; PRAGMA journal_mode = WAL;";
            command.ExecuteNonQuery();
        }

        private static async Task ApplyPragmasAsync(
            DbConnection connection,
            CancellationToken cancellationToken)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000; PRAGMA journal_mode = WAL;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
