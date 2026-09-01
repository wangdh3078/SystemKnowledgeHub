using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SystemKnowledgeHub.Api.Persistence;

public sealed class KnowledgeHubDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<KnowledgeHubDbContext>
{
    public const string DatabasePathEnvironmentVariable =
        "SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH";

    public KnowledgeHubDbContext CreateDbContext(string[] args)
    {
        var databasePath = Environment.GetEnvironmentVariable(DatabasePathEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(databasePath) || !Path.IsPathFullyQualified(databasePath))
            throw new InvalidOperationException(
                $"Set {DatabasePathEnvironmentVariable} to an absolute task-owned SQLite path before running dotnet ef.");
        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = Path.GetFullPath(databasePath),
            ForeignKeys = true,
            Pooling = false,
        }.ToString();
        var options = new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseSqlite(connectionString)
            .Options;

        return new KnowledgeHubDbContext(options);
    }
}
