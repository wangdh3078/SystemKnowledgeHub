using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class DatabaseDiscoveryRunMigrationTests
{
    private const string PreviousMigration = "20260829233658_AddLocalPasswordLifecycleSafety";

    [Fact]
    public async Task B02_migration_adds_run_snapshot_difference_scope_and_database_enforced_active_run_constraint()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new KnowledgeHubDbContext(
            new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var before = await Tables(connection);

        await migrator.MigrateAsync();

        var added = (await Tables(connection)).Except(before, StringComparer.Ordinal).OrderBy(item => item).ToArray();
        Assert.Equal(
        [
            "database_discovery_difference_entries",
            "database_discovery_differences",
            "database_discovery_runs",
            "database_discovery_scope_generations",
            "database_discovery_snapshots",
        ], added);
        var activeIndex = await Text(connection,
            "SELECT sql FROM sqlite_master WHERE type='index' AND name='ux_database_discovery_runs_one_active_profile';");
        Assert.Contains("UNIQUE INDEX", activeIndex, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("WHERE status IN ('Queued','Running')", activeIndex, StringComparison.Ordinal);
        var runSql = await Text(connection,
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='database_discovery_runs';");
        Assert.Contains("ck_database_discovery_runs_terminal", runSql, StringComparison.Ordinal);
        Assert.Contains("ck_database_discovery_runs_lease", runSql, StringComparison.Ordinal);
        var entrySql = await Text(connection,
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='database_discovery_difference_entries';");
        Assert.Contains("Added", entrySql, StringComparison.Ordinal);
        Assert.Contains("Changed", entrySql, StringComparison.Ordinal);
        Assert.Contains("MissingFromSource", entrySql, StringComparison.Ordinal);
        Assert.DoesNotContain("Unchanged'", entrySql, StringComparison.Ordinal);
        Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_check;"));
        Assert.DoesNotContain(await Tables(connection), table => table.Contains("sync_plan", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(await Tables(connection), table => table.Contains("discovery_binding", StringComparison.OrdinalIgnoreCase));

        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(before, await Tables(connection));
        Assert.Equal(1L, await Scalar(connection, "PRAGMA foreign_keys;"));
    }

    private static async Task<string[]> Tables(SqliteConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        var result = new List<string>();
        await using var reader = await command.ExecuteReaderAsync();
        while (await reader.ReadAsync()) result.Add(reader.GetString(0));
        return result.ToArray();
    }

    private static async Task<long> Scalar(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync());
    }

    private static async Task<string> Text(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return Convert.ToString(await command.ExecuteScalarAsync()) ?? string.Empty;
    }
}
