using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class KnowledgeDocumentRevisionMigrationTests
{
    private const string PreRevisionMigration = "20260823022046_TightenRelationshipVocabulary";

    [Fact]
    public async Task Migration_handles_zero_and_existing_databases_with_exact_baselines_and_safe_down()
    {
        await using (var emptyConnection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True"))
        {
            await emptyConnection.OpenAsync();
            await using var emptyContext = CreateContext(emptyConnection);
            var emptyMigrator = emptyContext.GetService<IMigrator>();
            await emptyMigrator.MigrateAsync();
            Assert.Equal(0L, await Scalar<long>(emptyConnection, "SELECT count(*) FROM knowledge_document_revisions;"));
            await emptyMigrator.MigrateAsync(PreRevisionMigration);
            Assert.Equal(0L, await Scalar<long>(emptyConnection, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='knowledge_document_revisions';"));
        }

        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var dbContext = CreateContext(connection);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(PreRevisionMigration);
        await SeedPreRevisionData(connection);

        await migrator.MigrateAsync();

        Assert.Equal(3L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_documents;"));
        Assert.Equal(3L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_document_revisions;"));
        Assert.Equal(3L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_document_revisions WHERE revision_number=1 AND revision_origin='MigrationBaseline' AND author_user_id IS NULL AND author_display_name_snapshot IS NULL AND change_summary IS NULL AND restore_reason IS NULL AND restored_from_revision_number IS NULL;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(DISTINCT created_at) FROM knowledge_document_revisions;"));
        Assert.Equal(0L, await Scalar<long>(connection, """
            SELECT count(*)
            FROM knowledge_documents AS document
            JOIN knowledge_document_revisions AS revision
              ON revision.knowledge_document_id=document.id AND revision.revision_number=1
            WHERE revision.title<>document.title
               OR NOT (revision.summary IS document.summary)
               OR revision.body_markdown<>document.body_markdown
               OR revision.lifecycle_context<>document.lifecycle_status;
            """));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT current_revision_number FROM knowledge_documents WHERE id=6001;"));
        Assert.Null(await Scalar<object?>(connection, "SELECT latest_published_revision_number FROM knowledge_documents WHERE id=6001;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT latest_published_revision_number FROM knowledge_documents WHERE id=6002;"));
        Assert.Null(await Scalar<object?>(connection, "SELECT latest_published_revision_number FROM knowledge_documents WHERE id=6003;"));
        Assert.Null(await Scalar<object?>(connection, "SELECT knowledge_document_revision_number_snapshot FROM evidence WHERE id=7001;"));
        Assert.Equal("Confirmed", await Scalar<string>(connection, "SELECT knowledge_status FROM knowledge_documents WHERE id=6002;"));
        Assert.Equal(4L, await Scalar<long>(connection, "SELECT version FROM knowledge_documents WHERE id=6002;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM evidence WHERE id=7001;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_relations WHERE id=8001;"));
        Assert.Equal(3L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_documents_fts;"));
        Assert.Equal(0L, await Scalar<long>(connection, "PRAGMA foreign_key_check;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_index_list('knowledge_document_revisions') WHERE name='IX_knowledge_document_revisions_knowledge_document_id_revision_number' AND [unique]=1;"));
        Assert.Equal(2L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_foreign_key_list('knowledge_document_revisions') WHERE [table] IN ('knowledge_documents','users') AND on_delete='RESTRICT';"));

        await AssertRevisionConstraint(connection, """
            INSERT INTO knowledge_document_revisions
                (knowledge_document_id,revision_number,title,body_markdown,author_user_id,author_display_name_snapshot,created_at,lifecycle_context,revision_origin)
            VALUES (6001,1,'duplicate','body',5001,'Migration User','2026-08-23T01:00:00+00:00','Draft','ContentSave');
            """);
        await AssertRevisionConstraint(connection, """
            INSERT INTO knowledge_document_revisions
                (knowledge_document_id,revision_number,title,body_markdown,author_user_id,author_display_name_snapshot,created_at,lifecycle_context,revision_origin)
            VALUES (6001,2,'invalid origin','body',5001,'Migration User','2026-08-23T01:00:00+00:00','Draft','UnknownOrigin');
            """);
        await AssertRevisionConstraint(connection, """
            INSERT INTO knowledge_document_revisions
                (knowledge_document_id,revision_number,title,body_markdown,created_at,lifecycle_context,revision_origin)
            VALUES (6001,2,'missing actor','body','2026-08-23T01:00:00+00:00','Draft','ContentSave');
            """);
        await AssertRevisionConstraint(connection, """
            INSERT INTO knowledge_document_revisions
                (knowledge_document_id,revision_number,title,body_markdown,author_user_id,author_display_name_snapshot,created_at,lifecycle_context,restore_reason,restored_from_revision_number,revision_origin)
            VALUES (6001,2,'invalid restore','body',5001,'Migration User','2026-08-23T01:00:00+00:00','Draft','bad',2,'Restore');
            """);
        await Execute(connection, """
            SAVEPOINT valid_revision_shapes;
            INSERT INTO knowledge_document_revisions
                (knowledge_document_id,revision_number,title,body_markdown,author_user_id,author_display_name_snapshot,created_at,lifecycle_context,change_summary,revision_origin)
            VALUES (6001,2,'valid save','body',5001,'Migration User','2026-08-23T01:00:00+00:00','Draft','changed','ContentSave');
            INSERT INTO knowledge_document_revisions
                (knowledge_document_id,revision_number,title,body_markdown,author_user_id,author_display_name_snapshot,created_at,lifecycle_context,restore_reason,restored_from_revision_number,revision_origin)
            VALUES (6001,3,'valid restore','body',5001,'Migration User','2026-08-23T01:00:00+00:00','Draft','restore prior content',1,'Restore');
            ROLLBACK TO valid_revision_shapes;
            RELEASE valid_revision_shapes;
            """);

        await migrator.MigrateAsync(PreRevisionMigration);

        Assert.Equal(0L, await Scalar<long>(connection, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='knowledge_document_revisions';"));
        Assert.Equal(0L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_table_info('knowledge_documents') WHERE name IN ('current_revision_number','latest_published_revision_number');"));
        Assert.Equal(0L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_table_info('evidence') WHERE name='knowledge_document_revision_number_snapshot';"));
        Assert.Equal(3L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_documents;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM evidence WHERE id=7001;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_relations WHERE id=8001;"));
        Assert.Equal(3L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_documents_fts;"));
        Assert.Equal(0L, await Scalar<long>(connection, "PRAGMA foreign_key_check;"));
    }

    [Fact]
    public async Task Down_refuses_to_drop_real_revision_history()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var dbContext = CreateContext(connection);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync();
        await Execute(connection, """
            INSERT INTO users (id,display_name,access_level,is_active,created_at,updated_at,version)
            VALUES (5001,'Revision User','Administrator',1,'2026-08-23T01:00:00+00:00','2026-08-23T01:00:00+00:00',1);
            INSERT INTO knowledge_documents (
                id,document_type,title,summary,body_markdown,lifecycle_status,knowledge_status,
                knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,
                created_by_user_id,created_by_display_name,updated_by_user_id,updated_by_display_name,
                created_at,updated_at,current_revision_number,version)
            VALUES (
                6001,'KnowledgeArticle','Created after migration',NULL,'body','Draft','Unknown',
                '2026-08-23T01:00:00+00:00','Revision User','创建人',
                5001,'Revision User',5001,'Revision User',
                '2026-08-23T01:00:00+00:00','2026-08-23T01:00:00+00:00',1,1);
            INSERT INTO knowledge_document_revisions (
                knowledge_document_id,revision_number,title,body_markdown,author_user_id,author_display_name_snapshot,
                created_at,lifecycle_context,revision_origin)
            VALUES (6001,1,'Created after migration','body',5001,'Revision User','2026-08-23T01:00:00+00:00','Draft','Created');
            """);

        await Assert.ThrowsAsync<SqliteException>(() => migrator.MigrateAsync(PreRevisionMigration));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM knowledge_document_revisions;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM __EFMigrationsHistory WHERE MigrationId LIKE '%AddImmutableKnowledgeDocumentRevisions';"));
    }

    private static KnowledgeHubDbContext CreateContext(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);

    private static async Task SeedPreRevisionData(SqliteConnection connection)
    {
        await Execute(connection, """
            INSERT INTO users (id,display_name,access_level,is_active,created_at,updated_at,version)
            VALUES (5001,'Migration User','Administrator',1,'2026-08-20T01:00:00+00:00','2026-08-20T01:00:00+00:00',1);

            INSERT INTO knowledge_documents (
                id,document_type,title,summary,body_markdown,lifecycle_status,knowledge_status,knowledge_status_reason,
                knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,
                created_by_user_id,created_by_display_name,updated_by_user_id,updated_by_display_name,
                created_at,updated_at,published_at,archived_at,version)
            VALUES
                (6001,'KnowledgeArticle','Draft baseline','draft summary','draft body','Draft','Unknown',NULL,
                 '2026-08-20T01:00:00+00:00','Migration User','创建人',5001,'Migration User',5001,'Migration User',
                 '2026-08-20T01:00:00+00:00','2026-08-21T01:00:00+00:00',NULL,NULL,3),
                (6002,'Specification','Published baseline',NULL,'published body','Published','Confirmed','verified',
                 '2026-08-20T01:00:00+00:00','Migration User','专家',5001,'Migration User',5001,'Migration User',
                 '2026-08-20T01:00:00+00:00','2026-08-22T01:00:00+00:00','2026-08-22T01:00:00+00:00',NULL,4),
                (6003,'Sop','Archived baseline','archive summary','archived body','Archived','Inferred',NULL,
                 '2026-08-20T01:00:00+00:00','Migration User','编辑者',5001,'Migration User',5001,'Migration User',
                 '2026-08-20T01:00:00+00:00','2026-08-23T01:00:00+00:00','2026-08-21T01:00:00+00:00','2026-08-23T01:00:00+00:00',5);

            INSERT INTO knowledge_documents_fts(rowid,title,summary,body_text) VALUES
                (6001,'Draft baseline','draft summary','draft body'),
                (6002,'Published baseline','','published body'),
                (6003,'Archived baseline','archive summary','archived body');

            INSERT INTO evidence (
                id,evidence_type,subject_type,subject_id,source_title,source_reference,summary,support_reason,
                provider_user_id,provider_name,provider_role,provided_at,created_at,updated_at,version)
            VALUES (
                7001,'HumanConfirmation','KnowledgeDocument',6002,'Legacy confirmation','meeting notes','confirmed','legacy confirmation support',
                5001,'Migration User','专家','2026-08-22T01:00:00+00:00','2026-08-22T01:00:00+00:00','2026-08-22T01:00:00+00:00',2);

            INSERT INTO knowledge_relations (
                id,source_type,source_id,target_type,target_id,relation_type,description,created_at,created_by_name,created_by_role,
                updated_at,knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,version)
            VALUES (
                8001,'KnowledgeDocument',6002,'KnowledgeDocument',6001,'Supersedes','migration preservation',
                '2026-08-22T01:00:00+00:00','Migration User','专家','2026-08-22T01:00:00+00:00','Inferred',
                '2026-08-22T01:00:00+00:00','Migration User','专家',2);
            """);
    }

    private static async Task AssertRevisionConstraint(SqliteConnection connection, string sql)
    {
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, sql));
    }

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
}
