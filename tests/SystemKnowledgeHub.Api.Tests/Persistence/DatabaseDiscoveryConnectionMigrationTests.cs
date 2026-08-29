using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class DatabaseDiscoveryConnectionMigrationTests
{
    private const string PreviousMigration = "20260829012501_AddAttachmentFoundation";

    [Fact]
    public async Task B01_migration_adds_only_profile_secret_and_audit_with_restrictive_constraints()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new KnowledgeHubDbContext(
            new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var before = await Tables(connection);

        await migrator.MigrateAsync();

        var after = await Tables(connection);
        var added = after.Except(before, StringComparer.Ordinal).OrderBy(item => item).ToArray();
        Assert.Equal(
            ["database_connection_audit_events", "database_connection_profiles", "database_connection_secrets"],
            added);
        Assert.Equal(2L, await Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_list('database_connection_profiles') WHERE on_delete='RESTRICT';"));
        Assert.Equal(1L, await Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_list('database_connection_secrets') WHERE on_delete='RESTRICT';"));
        Assert.Equal(2L, await Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_list('database_connection_audit_events') WHERE on_delete='RESTRICT';"));
        Assert.Equal(1L, await Scalar(connection, "SELECT count(*) FROM pragma_index_list('database_connection_profiles') WHERE name='IX_database_connection_profiles_database_source_id' AND [unique]=1;"));
        Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_check;"));
        var profileSql = await Text(connection, "SELECT sql FROM sqlite_master WHERE type='table' AND name='database_connection_profiles';");
        Assert.Contains("ck_database_connection_profiles_locator", profileSql, StringComparison.Ordinal);
        Assert.Contains("ck_database_connection_profiles_provider", profileSql, StringComparison.Ordinal);
        var secretSql = await Text(connection, "SELECT sql FROM sqlite_master WHERE type='table' AND name='database_connection_secrets';");
        Assert.Contains("protected_payload", secretSql, StringComparison.Ordinal);
        Assert.Contains("NULL", secretSql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(after, table => table.Contains("discovery_run", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(after, table => table.Contains("snapshot", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(after, table => table.Contains("difference", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(after, table => table.Contains("sync_plan", StringComparison.OrdinalIgnoreCase));
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
