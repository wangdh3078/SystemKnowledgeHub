using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class SoftDeleteOwnershipFoundationMigrationTests
{
    private const string PreSoftDeleteMigration = "20260823092808_AddImmutableKnowledgeDocumentRevisions";
    private static readonly IReadOnlyDictionary<string, string> ActiveUniqueIndexes =
        new Dictionary<string, string>
        {
            ["IX_systems_name"] = "is_deleted = 0",
            ["IX_database_sources_system_id_name"] = "is_deleted = 0",
            ["IX_database_sources_system_id"] = "is_primary = 1 AND is_deleted = 0",
            ["IX_business_functions_system_id_name"] = "is_deleted = 0",
            ["IX_database_objects_database_source_id_schema_name_object_name"] = "is_deleted = 0",
            ["IX_database_columns_database_object_id_column_name"] = "is_deleted = 0",
            ["IX_database_columns_database_object_id_ordinal_position"] = "is_deleted = 0",
            ["IX_business_rules_system_id_name"] = "is_deleted = 0",
            ["IX_integrations_integration_type_name_source_party_name_target_party_name"] = "is_deleted = 0",
        };

    [Fact]
    public async Task Fresh_and_upgrade_migrations_preserve_rows_and_install_the_exact_foundation()
    {
        await using (var fresh = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"))
        {
            await fresh.OpenAsync();
            await using var freshContext = CreateContext(fresh);
            await freshContext.Database.MigrateAsync();
            await AssertFoundationMetadata(fresh);
            Assert.Equal("ok", await Scalar<string>(fresh, "PRAGMA integrity_check;"));
            Assert.Equal(0L, await Scalar<long>(fresh, "SELECT count(*) FROM pragma_foreign_key_check;"));
        }

        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var dbContext = CreateContext(connection);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(PreSoftDeleteMigration);
        await SeedPreSoftDeleteRows(connection);

        var before = await Snapshot(connection);
        await migrator.MigrateAsync();
        var after = await Snapshot(connection);

        Assert.Equal(before, after);
        await AssertFoundationMetadata(connection);
        foreach (var table in RootTables)
        {
            Assert.Equal(1L, await Scalar<long>(connection,
                $"SELECT count(*) FROM {table} WHERE is_deleted=0 AND deleted_at IS NULL AND deleted_by_user_id IS NULL AND deleted_by_display_name IS NULL;"));
        }
        Assert.Equal(7L, await Scalar<long>(connection, """
            SELECT
                (SELECT count(*) FROM systems WHERE created_by_user_id IS NULL) +
                (SELECT count(*) FROM database_sources WHERE created_by_user_id IS NULL) +
                (SELECT count(*) FROM business_functions WHERE created_by_user_id IS NULL) +
                (SELECT count(*) FROM database_objects WHERE created_by_user_id IS NULL) +
                (SELECT count(*) FROM database_columns WHERE created_by_user_id IS NULL) +
                (SELECT count(*) FROM business_rules WHERE created_by_user_id IS NULL) +
                (SELECT count(*) FROM integrations WHERE created_by_user_id IS NULL);
            """));
        Assert.Equal(9001L, await Scalar<long>(connection, "SELECT created_by_user_id FROM knowledge_documents WHERE id=8;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT version FROM database_sources WHERE id=2;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_documents_fts WHERE rowid=8;"));
        Assert.Equal("ok", await Scalar<string>(connection, "PRAGMA integrity_check;"));
        Assert.Equal(0L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task Audit_foreign_key_filters_name_reuse_and_restore_conflict_are_database_enforced()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var dbContext = CreateContext(connection);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(PreSoftDeleteMigration);
        await SeedPreSoftDeleteRows(connection);
        await migrator.MigrateAsync();

        foreach (var (table, id) in RootRows)
        {
            await Assert.ThrowsAsync<SqliteException>(() => Execute(connection,
                $"UPDATE {table} SET is_deleted=1 WHERE id={id};"));
            await Assert.ThrowsAsync<SqliteException>(() => Execute(connection,
                $"UPDATE {table} SET deleted_at='2026-08-27T12:00:00+00:00', deleted_by_user_id=9001 WHERE id={id};"));
        }

        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, """
            UPDATE systems
            SET is_deleted=1, deleted_at='2026-08-27T12:00:00+00:00', deleted_by_user_id=99999, deleted_by_display_name='missing'
            WHERE id=1;
            """));
        await Execute(connection, "UPDATE systems SET created_by_user_id=9001 WHERE id=1;");
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, "UPDATE systems SET created_by_user_id=99999 WHERE id=1;"));

        await Execute(connection, """
            UPDATE systems
            SET deleted_at='2026-08-27T12:00:00+00:00', deleted_by_user_id=9001, deleted_by_display_name='Canonical User'
            WHERE id=1;
            """);
        Assert.Equal(0L, await Scalar<long>(connection, "SELECT is_deleted FROM systems WHERE id=1;"));
        await Execute(connection, "UPDATE systems SET deleted_at=NULL, deleted_by_user_id=NULL, deleted_by_display_name=NULL WHERE id=1;");

        await Execute(connection, """
            UPDATE systems
            SET is_deleted=1, deleted_at='2026-08-27T12:00:00+00:00', deleted_by_user_id=9001, deleted_by_display_name='Canonical User'
            WHERE id=1;
            INSERT INTO systems (
                id,name,display_name,system_type,lifecycle,created_at,created_by_user_id,created_by_name,updated_at,
                knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,version)
            VALUES (101,'Legacy System','Replacement','Service','Running','2026-08-27T12:01:00+00:00',9001,'Canonical User','2026-08-27T12:01:00+00:00',
                'Unknown','2026-08-27T12:01:00+00:00','Canonical User','创建人',1);
            """);
        Assert.Equal(2L, await Scalar<long>(connection, "SELECT count(*) FROM systems WHERE name='Legacy System';"));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, "UPDATE systems SET is_deleted=0 WHERE id=1;"));

        foreach (var (table, id) in RootRows.Where(row => row.Table != "systems"))
        {
            await Execute(connection,
                $"UPDATE {table} SET is_deleted=1, deleted_at='2026-08-27T12:00:00+00:00', deleted_by_user_id=9001, deleted_by_display_name='Canonical User' WHERE id={id};");
        }

        Assert.False(await dbContext.Systems.AnyAsync(item => item.Id == 1));
        Assert.False(await dbContext.DatabaseSources.AnyAsync(item => item.Id == 2));
        Assert.False(await dbContext.BusinessFunctions.AnyAsync(item => item.Id == 3));
        Assert.False(await dbContext.DatabaseObjects.AnyAsync(item => item.Id == 4));
        Assert.False(await dbContext.DatabaseColumns.AnyAsync(item => item.Id == 5));
        Assert.False(await dbContext.BusinessRules.AnyAsync(item => item.Id == 6));
        Assert.False(await dbContext.Integrations.AnyAsync(item => item.Id == 7));
        Assert.False(await dbContext.KnowledgeDocuments.AnyAsync(item => item.Id == 8));
        Assert.True(await dbContext.Systems.IgnoreQueryFilters().AnyAsync(item => item.Id == 1));
        Assert.True(await dbContext.DatabaseSources.IgnoreQueryFilters().AnyAsync(item => item.Id == 2));
        Assert.True(await dbContext.BusinessFunctions.IgnoreQueryFilters().AnyAsync(item => item.Id == 3));
        Assert.True(await dbContext.DatabaseObjects.IgnoreQueryFilters().AnyAsync(item => item.Id == 4));
        Assert.True(await dbContext.DatabaseColumns.IgnoreQueryFilters().AnyAsync(item => item.Id == 5));
        Assert.True(await dbContext.BusinessRules.IgnoreQueryFilters().AnyAsync(item => item.Id == 6));
        Assert.True(await dbContext.Integrations.IgnoreQueryFilters().AnyAsync(item => item.Id == 7));
        Assert.True(await dbContext.KnowledgeDocuments.IgnoreQueryFilters().AnyAsync(item => item.Id == 8));
        Assert.Equal("ok", await Scalar<string>(connection, "PRAGMA integrity_check;"));
        Assert.Equal(0L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_foreign_key_check;"));
    }

    [Fact]
    public async Task Representative_active_queries_keep_the_expected_index_and_FTS_plans()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var dbContext = CreateContext(connection);
        await dbContext.Database.MigrateAsync();

        var indexedQueries = new Dictionary<string, string>
        {
            ["SELECT id FROM systems WHERE name='x' AND is_deleted=0"] = "IX_systems_name",
            ["SELECT id FROM database_sources WHERE system_id=1 AND name='x' AND is_deleted=0"] = "IX_database_sources_system_id_name",
            ["SELECT id FROM database_sources WHERE system_id=1 AND is_primary=1 AND is_deleted=0"] = "IX_database_sources_system_id",
            ["SELECT id FROM business_functions WHERE system_id=1 AND name='x' AND is_deleted=0"] = "IX_business_functions_system_id_name",
            ["SELECT id FROM database_objects WHERE database_source_id=1 AND schema_name='main' AND object_name='x' AND is_deleted=0"] = "IX_database_objects_database_source_id_schema_name_object_name",
            ["SELECT id FROM database_columns WHERE database_object_id=1 AND column_name='x' AND is_deleted=0"] = "IX_database_columns_database_object_id_column_name",
            ["SELECT id FROM database_columns WHERE database_object_id=1 AND ordinal_position=1 AND is_deleted=0"] = "IX_database_columns_database_object_id_ordinal_position",
            ["SELECT id FROM business_rules WHERE system_id=1 AND name='x' AND is_deleted=0"] = "IX_business_rules_system_id_name",
            ["SELECT id FROM integrations WHERE integration_type='HttpApi' AND name='x' AND source_party_name='a' AND target_party_name='b' AND is_deleted=0"] = "IX_integrations_integration_type_name_source_party_name_target_party_name",
            ["SELECT id FROM knowledge_documents WHERE document_type='KnowledgeArticle' AND lifecycle_status='Draft' AND is_deleted=0 ORDER BY updated_at DESC"] = "IX_knowledge_documents_document_type_lifecycle_status_updated_at",
        };

        foreach (var (sql, expectedIndex) in indexedQueries)
        {
            var plan = await QueryPlan(connection, sql);
            Assert.Contains(expectedIndex, plan, StringComparison.OrdinalIgnoreCase);
        }

        var ftsPlan = await QueryPlan(connection, """
            SELECT d.id
            FROM knowledge_documents_fts
            JOIN knowledge_documents d ON d.id=knowledge_documents_fts.rowid
            WHERE knowledge_documents_fts MATCH 'legacy' AND d.is_deleted=0
            """);
        Assert.Contains("VIRTUAL TABLE INDEX", ftsPlan, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("PRIMARY KEY", ftsPlan, StringComparison.OrdinalIgnoreCase);
    }

    private static readonly string[] RootTables =
    [
        "systems", "database_sources", "business_functions", "database_objects",
        "database_columns", "business_rules", "integrations", "knowledge_documents",
    ];

    private static readonly (string Table, long Id)[] RootRows =
    [
        ("systems", 1), ("database_sources", 2), ("business_functions", 3), ("database_objects", 4),
        ("database_columns", 5), ("business_rules", 6), ("integrations", 7), ("knowledge_documents", 8),
    ];

    private static KnowledgeHubDbContext CreateContext(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);

    private static async Task AssertFoundationMetadata(SqliteConnection connection)
    {
        foreach (var table in RootTables)
        {
            Assert.Equal(4L, await Scalar<long>(connection,
                $"SELECT count(*) FROM pragma_table_info('{table}') WHERE name IN ('is_deleted','deleted_at','deleted_by_user_id','deleted_by_display_name');"));
            Assert.Equal(1L, await Scalar<long>(connection,
                $"SELECT count(*) FROM sqlite_master WHERE type='table' AND name='{table}' AND sql LIKE '%deletion_audit%';"));
            Assert.Equal(1L, await Scalar<long>(connection,
                $"SELECT count(*) FROM pragma_foreign_key_list('{table}') WHERE [table]='users' AND [from]='deleted_by_user_id' AND on_delete='RESTRICT';"));
        }
        Assert.Equal(1L, await Scalar<long>(connection,
            "SELECT count(*) FROM pragma_table_info('database_sources') WHERE name='version' AND [notnull]=1 AND dflt_value='1';"));
        Assert.Equal(2L, await Scalar<long>(connection,
            "SELECT count(*) FROM pragma_table_info('database_columns') WHERE name IN ('created_by_user_id','created_by_display_name');"));

        foreach (var (indexName, expectedFilter) in ActiveUniqueIndexes)
        {
            var sql = await Scalar<string>(connection,
                $"SELECT sql FROM sqlite_master WHERE type='index' AND name='{indexName}';");
            Assert.NotNull(sql);
            Assert.Contains("CREATE UNIQUE INDEX", sql, StringComparison.OrdinalIgnoreCase);
            Assert.Contains(expectedFilter, sql, StringComparison.OrdinalIgnoreCase);
        }
        Assert.Equal("NOCASE", await Scalar<string>(connection,
            "SELECT coll FROM pragma_index_xinfo('IX_systems_name') WHERE key=1;"));
        Assert.Equal("NOCASE", await Scalar<string>(connection,
            "SELECT coll FROM pragma_index_xinfo('IX_database_columns_database_object_id_column_name') WHERE name='column_name';"));
    }

    private static async Task SeedPreSoftDeleteRows(SqliteConnection connection)
    {
        await Execute(connection, """
            INSERT INTO users (id,display_name,access_level,is_active,created_at,updated_at,version)
            VALUES (9001,'Canonical User','Administrator',1,'2026-08-20T00:00:00+00:00','2026-08-20T00:00:00+00:00',1);

            INSERT INTO systems (id,name,display_name,system_type,lifecycle,purpose,created_at,created_by_name,created_by_role,updated_at,
                knowledge_status,knowledge_status_reason,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,version)
            VALUES (1,'Legacy System','Legacy Display','Service','Running','preserve system','2026-08-20T01:00:00+00:00','Legacy Name','Legacy Role','2026-08-21T01:00:00+00:00',
                'Confirmed','preserve status','2026-08-21T01:00:00+00:00','Legacy Name','Legacy Role',4);

            INSERT INTO database_sources (id,system_id,name,engine,environment,description,is_primary,created_at,created_by_name,created_by_role,updated_at)
            VALUES (2,1,'Legacy Source','SQLite','Test','preserve source',1,'2026-08-20T02:00:00+00:00','Legacy Name','Legacy Role','2026-08-21T02:00:00+00:00');

            INSERT INTO business_functions (id,system_id,name,function_type,purpose,rewrite_status,created_at,created_by_name,created_by_role,updated_at,
                knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,version)
            VALUES (3,1,'Legacy Function','Query','preserve function','Keep','2026-08-20T03:00:00+00:00','Legacy Name','Legacy Role','2026-08-21T03:00:00+00:00',
                'Inferred','2026-08-21T03:00:00+00:00','Legacy Name','Legacy Role',3);

            INSERT INTO database_objects (id,database_source_id,schema_name,object_name,object_type,business_description,access_mode,created_at,created_by_name,created_by_role,updated_at,
                knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,version)
            VALUES (4,2,'main','legacy_object','Table','preserve object','Read','2026-08-20T04:00:00+00:00','Legacy Name','Legacy Role','2026-08-21T04:00:00+00:00',
                'Confirmed','2026-08-21T04:00:00+00:00','Legacy Name','Legacy Role',5);

            INSERT INTO database_columns (id,database_object_id,ordinal_position,column_name,data_type,is_nullable,business_description,created_at,updated_at,
                knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,version)
            VALUES (5,4,1,'legacy_column','TEXT',0,'preserve column','2026-08-20T05:00:00+00:00','2026-08-21T05:00:00+00:00',
                'Unknown','2026-08-21T05:00:00+00:00','Legacy Name','Legacy Role',2);

            INSERT INTO business_rules (id,system_id,name,description,condition_text,result_text,created_at,created_by_name,created_by_role,updated_at,
                knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,version)
            VALUES (6,1,'Legacy Rule','preserve rule','x=1','allow','2026-08-20T06:00:00+00:00','Legacy Name','Legacy Role','2026-08-21T06:00:00+00:00',
                'Inferred','2026-08-21T06:00:00+00:00','Legacy Name','Legacy Role',6);

            INSERT INTO integrations (id,name,integration_type,source_system_id,source_party_name,target_party_name,flow_direction,purpose,created_at,created_by_name,created_by_role,updated_at,
                knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,version)
            VALUES (7,'Legacy Integration','HttpApi',1,'Legacy System','External','OneWay','preserve integration','2026-08-20T07:00:00+00:00','Legacy Name','Legacy Role','2026-08-21T07:00:00+00:00',
                'Confirmed','2026-08-21T07:00:00+00:00','Legacy Name','Legacy Role',7);

            INSERT INTO knowledge_documents (id,document_type,title,summary,body_markdown,lifecycle_status,knowledge_status,knowledge_status_reason,
                knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,created_by_user_id,created_by_display_name,
                updated_by_user_id,updated_by_display_name,created_at,updated_at,current_revision_number,version)
            VALUES (8,'KnowledgeArticle','Legacy Document','preserve summary','preserve body','Draft','Confirmed','preserve document status',
                '2026-08-21T08:00:00+00:00','Canonical User','创建人',9001,'Canonical User',9001,'Canonical User',
                '2026-08-20T08:00:00+00:00','2026-08-21T08:00:00+00:00',1,8);

            INSERT INTO knowledge_document_revisions (id,knowledge_document_id,revision_number,title,summary,body_markdown,author_user_id,author_display_name_snapshot,
                created_at,lifecycle_context,revision_origin)
            VALUES (80,8,1,'Legacy Document','preserve summary','preserve body',9001,'Canonical User','2026-08-20T08:00:00+00:00','Draft','Created');

            INSERT INTO evidence (id,evidence_type,subject_type,subject_id,source_title,source_reference,support_reason,provider_user_id,provider_name,provider_role,
                provided_at,created_at,updated_at,version)
            VALUES (90,'ExistingDocument','KnowledgeDocument',8,'Migration evidence','doc://migration','preserve evidence',9001,'Canonical User','专家',
                '2026-08-20T09:00:00+00:00','2026-08-20T09:00:00+00:00','2026-08-20T09:00:00+00:00',2);

            INSERT INTO knowledge_documents_fts(rowid,title,summary,body_text)
            VALUES (8,'Legacy Document','preserve summary','preserve body');
            """);
    }

    private static async Task<string> Snapshot(SqliteConnection connection) => string.Join('|',
        await Scalar<string>(connection, "SELECT name || ':' || purpose || ':' || lifecycle || ':' || knowledge_status || ':' || updated_at || ':' || version FROM systems WHERE id=1;"),
        await Scalar<string>(connection, "SELECT name || ':' || description || ':' || updated_at FROM database_sources WHERE id=2;"),
        await Scalar<string>(connection, "SELECT name || ':' || purpose || ':' || rewrite_status || ':' || knowledge_status || ':' || updated_at || ':' || version FROM business_functions WHERE id=3;"),
        await Scalar<string>(connection, "SELECT object_name || ':' || business_description || ':' || knowledge_status || ':' || updated_at || ':' || version FROM database_objects WHERE id=4;"),
        await Scalar<string>(connection, "SELECT column_name || ':' || business_description || ':' || knowledge_status || ':' || updated_at || ':' || version FROM database_columns WHERE id=5;"),
        await Scalar<string>(connection, "SELECT name || ':' || description || ':' || knowledge_status || ':' || updated_at || ':' || version FROM business_rules WHERE id=6;"),
        await Scalar<string>(connection, "SELECT name || ':' || purpose || ':' || knowledge_status || ':' || updated_at || ':' || version FROM integrations WHERE id=7;"),
        await Scalar<string>(connection, "SELECT title || ':' || summary || ':' || body_markdown || ':' || lifecycle_status || ':' || knowledge_status || ':' || updated_at || ':' || version FROM knowledge_documents WHERE id=8;"),
        await Scalar<string>(connection, "SELECT count(*) || ':' || sum(revision_number) FROM knowledge_document_revisions WHERE knowledge_document_id=8;"),
        await Scalar<string>(connection, "SELECT count(*) || ':' || sum(version) FROM evidence WHERE subject_type='KnowledgeDocument' AND subject_id=8;"));

    private static async Task Execute(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<T?> Scalar<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        var value = await command.ExecuteScalarAsync();
        return value is null or DBNull ? default : (T)Convert.ChangeType(value, typeof(T));
    }

    private static async Task<string> QueryPlan(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql};";
        await using var reader = await command.ExecuteReaderAsync();
        var details = new List<string>();
        while (await reader.ReadAsync())
        {
            details.Add(reader.GetString(3));
        }

        return string.Join(Environment.NewLine, details);
    }
}
