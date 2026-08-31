using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class DatabaseDiscoverySyncMigrationTests
{
    private const string PreviousMigration = "20260830030122_AddDatabaseDiscoveryRunSnapshotDiffFoundation";

    [Fact]
    public async Task B04_migration_preserves_legacy_rows_backfills_technical_identity_and_adds_typed_sync_tables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new KnowledgeHubDbContext(
            new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        await Execute(connection, """
            INSERT INTO users (id, display_name, access_level, is_active, created_at, updated_at, version)
            VALUES (9001, 'Legacy migration actor', 'Administrator', 1, '2026-08-31T00:00:00+00:00', '2026-08-31T00:00:00+00:00', 1);
            INSERT INTO systems (id, name, display_name, system_type, lifecycle, created_at, created_by_user_id, created_by_name, updated_at,
                knowledge_status, knowledge_status_changed_at, knowledge_status_changed_by_name, knowledge_status_changed_by_role, version, is_deleted)
            VALUES (9001, 'legacy-system', 'Legacy System', 'Application', 'Running', '2026-08-31T00:00:00+00:00', 9001, 'Legacy migration actor', '2026-08-31T00:00:00+00:00',
                'Unknown', '2026-08-31T00:00:00+00:00', 'Legacy migration actor', 'Administrator', 1, 0);
            INSERT INTO database_sources (id, system_id, name, engine, is_primary, created_at, created_by_user_id, created_by_name, updated_at, version, is_deleted)
            VALUES (9001, 9001, 'Legacy Source', 'Oracle', 1, '2026-08-31T00:00:00+00:00', 9001, 'Legacy migration actor', '2026-08-31T00:00:00+00:00', 1, 0);
            INSERT INTO database_objects (id, database_source_id, schema_name, object_name, object_type, business_description, business_key_columns_json, access_mode, created_at, created_by_user_id,
                created_by_name, updated_at, knowledge_status, knowledge_status_changed_at, knowledge_status_changed_by_name,
                knowledge_status_changed_by_role, version, is_deleted)
            VALUES (9001, 9001, 'LEGACY', 'TABLE_A', 'Table', 'Legacy business description', '["ID"]', 'Read', '2026-08-31T00:00:00+00:00', 9001,
                'Legacy migration actor', '2026-08-31T00:00:00+00:00', 'Unknown', '2026-08-31T00:00:00+00:00', 'Legacy migration actor', 'Administrator', 1, 0);
            INSERT INTO database_columns (id, database_object_id, ordinal_position, column_name, data_type, is_nullable, business_description, created_at, created_by_user_id,
                created_by_display_name, updated_at, knowledge_status, knowledge_status_changed_at, knowledge_status_changed_by_name,
                knowledge_status_changed_by_role, version, is_deleted)
            VALUES (9001, 9001, 1, 'ID', 'NUMBER(19)', 0, 'Legacy column meaning', '2026-08-31T00:00:00+00:00', 9001,
                'Legacy migration actor', '2026-08-31T00:00:00+00:00', 'Unknown', '2026-08-31T00:00:00+00:00', 'Legacy migration actor', 'Administrator', 1, 0);
            """);
        var before = await Tables(connection);
        var objectCount = await Scalar(connection, "SELECT count(*) FROM database_objects;");
        var columnCount = await Scalar(connection, "SELECT count(*) FROM database_columns;");
        Assert.True(objectCount > 0);
        Assert.True(columnCount > 0);

        await migrator.MigrateAsync();

        Assert.Equal(objectCount, await Scalar(connection, "SELECT count(*) FROM database_objects;"));
        Assert.Equal(columnCount, await Scalar(connection, "SELECT count(*) FROM database_columns;"));
        Assert.Equal(0L, await Scalar(connection,
            "SELECT count(*) FROM database_objects WHERE technical_identity <> 'legacy:object:v1:' || id OR technical_identity IS NULL;"));
        Assert.Equal(0L, await Scalar(connection,
            "SELECT count(*) FROM database_columns WHERE technical_identity <> 'legacy:column:v1:' || id OR technical_identity IS NULL;"));
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM database_objects WHERE id=9001 AND business_description='Legacy business description' AND business_key_columns_json='[\"ID\"]' AND access_mode='Read';"));
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM database_columns WHERE id=9001 AND business_description='Legacy column meaning';"));
        var added = (await Tables(connection)).Except(before, StringComparer.Ordinal).OrderBy(x => x).ToArray();
        Assert.Equal([
            "database_column_discovery_bindings",
            "database_discovery_sync_apply_results",
            "database_discovery_sync_audit_events",
            "database_discovery_sync_plans",
            "database_object_discovery_bindings",
        ], added);
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM pragma_index_list('database_object_discovery_bindings') WHERE name='IX_database_object_discovery_bindings_database_object_id' AND [unique]=1;"));
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM pragma_index_list('database_column_discovery_bindings') WHERE name='IX_database_column_discovery_bindings_database_column_id' AND [unique]=1;"));
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM pragma_index_list('database_object_discovery_bindings') WHERE name='IX_database_object_discovery_bindings_profile_id_scope_generation_id_identity_algorithm_version_logical_identity' AND [unique]=1;"));
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM pragma_index_list('database_column_discovery_bindings') WHERE name='IX_database_column_discovery_bindings_profile_id_scope_generation_id_identity_algorithm_version_logical_identity' AND [unique]=1;"));
        Assert.Equal(0L, await Scalar(connection,
            "SELECT count(*) FROM sqlite_master WHERE type='table' AND (name LIKE 'oracle_%' OR name LIKE 'postgresql_%');"));
        Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_check;"));

        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(before, await Tables(connection));
        Assert.Equal(0L, await Scalar(connection,
            "SELECT count(*) FROM pragma_table_info('database_objects') WHERE name IN ('technical_identity','technical_identity_algorithm_version','database_comment');"));
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

    private static async Task Execute(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
