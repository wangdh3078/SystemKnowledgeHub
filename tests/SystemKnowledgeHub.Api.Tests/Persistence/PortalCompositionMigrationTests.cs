using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class PortalCompositionMigrationTests
{
    private const string PreviousMigration = "20260831170031_AddManualDiscoverySyncFoundation";

    [Fact]
    public async Task B01_migration_is_additive_and_creates_only_the_three_portal_tables()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new KnowledgeHubDbContext(
            new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(PreviousMigration);
        var before = await Tables(connection);

        await migrator.MigrateAsync();

        var added = (await Tables(connection)).Except(before, StringComparer.Ordinal).OrderBy(name => name).ToArray();
        Assert.Equal(["portal_page_nodes", "portal_page_sections", "portal_pages"], added);
        Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM portal_pages;"));
        Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM portal_page_nodes;"));
        Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM portal_page_sections;"));
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM pragma_index_list('portal_page_nodes') WHERE name='IX_portal_page_nodes_active_root_sort_order' AND [unique]=1 AND partial=1;"));
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM pragma_index_list('portal_page_nodes') WHERE name='IX_portal_page_nodes_active_parent_sort_order' AND [unique]=1 AND partial=1;"));
        Assert.Equal(1L, await Scalar(connection,
            "SELECT count(*) FROM pragma_index_list('portal_page_sections') WHERE name='IX_portal_page_sections_portal_page_id_sort_order' AND [unique]=1;"));
        Assert.Equal(0L, await Scalar(connection, "SELECT count(*) FROM pragma_foreign_key_check;"));

        await migrator.MigrateAsync(PreviousMigration);
        Assert.Equal(before, await Tables(connection));
    }

    [Fact]
    public async Task B01_database_constraints_reject_invalid_shape_and_duplicate_active_sibling_order()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var db = new KnowledgeHubDbContext(
            new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);
        await db.Database.MigrateAsync();
        await Execute(connection, """
            INSERT INTO users (id, display_name, access_level, is_active, created_at, updated_at, version)
            VALUES (88001, 'Portal actor', 'Administrator', 1, '2026-09-03T00:00:00+00:00', '2026-09-03T00:00:00+00:00', 1);
            INSERT INTO portal_pages (id, title, primary_target_type, primary_target_id, is_published,
                created_at, created_by_user_id, created_by_display_name, updated_at, updated_by_user_id,
                updated_by_display_name, version, is_deleted)
            VALUES (88001, 'Page', 'System', 1, 0, '2026-09-03T00:00:00+00:00', 88001, 'Portal actor',
                '2026-09-03T00:00:00+00:00', 88001, 'Portal actor', 1, 0);
            INSERT INTO portal_page_nodes (id, title, node_kind, sort_order, is_published,
                created_at, created_by_user_id, created_by_display_name, updated_at, updated_by_user_id,
                updated_by_display_name, version, is_deleted)
            VALUES (88001, 'Root A', 'Folder', 0, 0, '2026-09-03T00:00:00+00:00', 88001, 'Portal actor',
                '2026-09-03T00:00:00+00:00', 88001, 'Portal actor', 1, 0);
            INSERT INTO portal_page_nodes (id, parent_id, title, node_kind, portal_page_id, sort_order, is_published,
                created_at, created_by_user_id, created_by_display_name, updated_at, updated_by_user_id,
                updated_by_display_name, version, is_deleted)
            VALUES (88004, 88001, 'Page child', 'Page', 88001, 0, 0, '2026-09-03T00:00:00+00:00', 88001, 'Portal actor',
                '2026-09-03T00:00:00+00:00', 88001, 'Portal actor', 1, 0);
            INSERT INTO portal_page_sections (id, portal_page_id, heading, source_kind, projection_kind, sort_order)
            VALUES (88004, 88001, 'Summary', 'PrimaryTarget', 'Summary', 0);
            """);

        Assert.Equal(0L, await Scalar(connection, "SELECT is_published FROM portal_pages WHERE id=88001;"));
        Assert.Equal(1L, await Scalar(connection, "SELECT version FROM portal_pages WHERE id=88001;"));

        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            INSERT INTO portal_page_nodes (id, title, node_kind, sort_order, is_published,
                created_at, created_by_user_id, created_by_display_name, updated_at, updated_by_user_id,
                updated_by_display_name, version, is_deleted)
            VALUES (88002, 'Root B', 'Folder', 0, 0, '2026-09-03T00:00:00+00:00', 88001, 'Portal actor',
                '2026-09-03T00:00:00+00:00', 88001, 'Portal actor', 1, 0);
            """));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            INSERT INTO portal_page_nodes (id, parent_id, title, node_kind, portal_page_id, sort_order, is_published,
                created_at, created_by_user_id, created_by_display_name, updated_at, updated_by_user_id,
                updated_by_display_name, version, is_deleted)
            VALUES (88005, 88001, 'Duplicate child', 'Page', 88001, 0, 0, '2026-09-03T00:00:00+00:00', 88001, 'Portal actor',
                '2026-09-03T00:00:00+00:00', 88001, 'Portal actor', 1, 0);
            """));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            INSERT INTO portal_page_nodes (id, title, node_kind, sort_order, is_published,
                created_at, created_by_user_id, created_by_display_name, updated_at, updated_by_user_id,
                updated_by_display_name, version, is_deleted)
            VALUES (88006, 'Bad page', 'Page', 6, 0, '2026-09-03T00:00:00+00:00', 88001, 'Portal actor',
                '2026-09-03T00:00:00+00:00', 88001, 'Portal actor', 1, 0);
            """));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection,
            "UPDATE portal_page_nodes SET parent_id=id WHERE id=88001;"));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            INSERT INTO portal_page_nodes (id, title, node_kind, portal_page_id, sort_order, is_published,
                created_at, created_by_user_id, created_by_display_name, updated_at, updated_by_user_id,
                updated_by_display_name, version, is_deleted)
            VALUES (88003, 'Bad folder', 'Folder', 88001, 1, 0, '2026-09-03T00:00:00+00:00', 88001, 'Portal actor',
                '2026-09-03T00:00:00+00:00', 88001, 'Portal actor', 1, 0);
            """));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            INSERT INTO portal_page_sections (id, portal_page_id, heading, source_kind, reference_target_type,
                reference_target_id, projection_kind, sort_order)
            VALUES (88001, 88001, 'Bad section', 'PrimaryTarget', 'System', 1, 'Summary', 0);
            """));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            INSERT INTO portal_page_sections (id, portal_page_id, heading, source_kind, projection_kind, sort_order)
            VALUES (88006, 88001, 'Missing reference', 'ExplicitReference', 'Summary', 6);
            """));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            INSERT INTO portal_page_sections (id, portal_page_id, heading, source_kind, reference_target_type,
                reference_target_id, projection_kind, sort_order)
            VALUES (88007, 88001, 'Derived reference', 'Derived', 'System', 1, 'RelatedKnowledge', 7);
            """));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            INSERT INTO portal_page_sections (id, portal_page_id, heading, source_kind, projection_kind, sort_order)
            VALUES (88005, 88001, 'Duplicate section', 'PrimaryTarget', 'Summary', 0);
            """));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, "DELETE FROM portal_pages WHERE id=88001;"));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, "DELETE FROM portal_page_nodes WHERE id=88001;"));

        foreach (var invalidPageSql in new[]
        {
            "INSERT INTO portal_pages (id,title,primary_target_type,primary_target_id,is_published,created_at,created_by_user_id,created_by_display_name,updated_at,updated_by_user_id,updated_by_display_name,version,is_deleted) VALUES (88101,'   ','System',1,0,'2026-09-03T00:00:00+00:00',88001,'Portal actor','2026-09-03T00:00:00+00:00',88001,'Portal actor',1,0);",
            "INSERT INTO portal_pages (id,title,primary_target_type,primary_target_id,is_published,created_at,created_by_user_id,created_by_display_name,updated_at,updated_by_user_id,updated_by_display_name,version,is_deleted) VALUES (88102,'Invalid type','BusinessRule',1,0,'2026-09-03T00:00:00+00:00',88001,'Portal actor','2026-09-03T00:00:00+00:00',88001,'Portal actor',1,0);",
            "INSERT INTO portal_pages (id,title,primary_target_type,primary_target_id,is_published,created_at,created_by_user_id,created_by_display_name,updated_at,updated_by_user_id,updated_by_display_name,version,is_deleted) VALUES (88103,'Invalid target','System',9007199254740992,0,'2026-09-03T00:00:00+00:00',88001,'Portal actor','2026-09-03T00:00:00+00:00',88001,'Portal actor',1,0);",
            "INSERT INTO portal_pages (id,title,primary_target_type,primary_target_id,is_published,created_at,created_by_user_id,created_by_display_name,updated_at,updated_by_user_id,updated_by_display_name,version,is_deleted) VALUES (88104,'Invalid version','System',1,0,'2026-09-03T00:00:00+00:00',88001,'Portal actor','2026-09-03T00:00:00+00:00',88001,'Portal actor',0,0);",
        })
        {
            await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, invalidPageSql));
        }
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
