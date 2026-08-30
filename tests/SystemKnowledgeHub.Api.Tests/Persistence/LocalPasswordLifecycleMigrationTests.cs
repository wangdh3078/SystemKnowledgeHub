using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class LocalPasswordLifecycleMigrationTests
{
    private const string PreviousMigration = "20260829150313_AddDatabaseDiscoveryConnectionFoundation";
    private const string TargetMigration = "20260829233658_AddLocalPasswordLifecycleSafety";

    [Fact]
    public async Task Migration_preserves_existing_credentials_with_must_change_password_false()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new KnowledgeHubDbContext(
            new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);

        await Execute(connection, """
            INSERT INTO users (display_name, is_active, created_at, updated_at, version, access_level)
            VALUES ('migration-user', 1, '2026-08-30T00:00:00+00:00', '2026-08-30T00:00:00+00:00', 1, 'Viewer');
            INSERT INTO local_login_credentials
                (user_id, username, normalized_username, password_hash, is_active, failed_login_attempts,
                 session_version, created_at, updated_at, last_password_changed_at, version)
            VALUES
                (1, 'migration-local', 'MIGRATION-LOCAL', 'safe-test-hash', 1, 0,
                 1, '2026-08-30T00:00:00+00:00', '2026-08-30T00:00:00+00:00', '2026-08-30T00:00:00+00:00', 1);
            """);

        await migrator.MigrateAsync(TargetMigration);

        Assert.Equal(0L, await Scalar(connection,
            "SELECT must_change_password FROM local_login_credentials WHERE username='migration-local';"));
        Assert.Equal("0", await Text(connection,
            "SELECT dflt_value FROM pragma_table_info('local_login_credentials') WHERE name='must_change_password';"));
        var tableSql = await Text(connection,
            "SELECT sql FROM sqlite_master WHERE type='table' AND name='local_login_credentials';");
        Assert.Contains("ck_local_login_credentials_must_change_password", tableSql, StringComparison.Ordinal);
        Assert.Contains("must_change_password IN (0,1)", tableSql, StringComparison.Ordinal);
        Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_check;"));
    }

    private static async Task Execute(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
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
