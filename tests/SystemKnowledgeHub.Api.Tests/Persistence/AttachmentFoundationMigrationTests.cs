using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class AttachmentFoundationMigrationTests
{
    private const string PreAttachmentMigration = "20260827144345_AddSoftDeleteOwnershipFoundation";

    [Fact]
    public async Task Additive_migration_creates_exact_restrictive_schema_without_inventing_references()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        await using var dbContext = CreateContext(connection);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync(PreAttachmentMigration);
        await SeedDocumentAndRevision(connection);

        await migrator.MigrateAsync();

        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='attachments';"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='attachment_references';"));
        Assert.Equal(0L, await Scalar<long>(connection, "SELECT count(*) FROM attachment_references;"));
        Assert.Equal(0L, await Scalar<long>(connection, "PRAGMA foreign_key_check;"));
        Assert.Equal(2L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_foreign_key_list('attachments') WHERE on_delete='RESTRICT';"));
        Assert.Equal(2L, await Scalar<long>(connection, "SELECT count(DISTINCT id) FROM pragma_foreign_key_list('attachment_references') WHERE on_delete='RESTRICT';"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_index_list('attachments') WHERE name='IX_attachments_storage_key' AND [unique]=1;"));
        Assert.Equal(1L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_index_list('attachment_references') WHERE name='IX_attachment_references_knowledge_document_revision_id_attachment_id' AND [unique]=1;"));

        await Execute(connection, ValidAttachmentSql(8001, 6001, "objects/aa/aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa.bin"));
        await Execute(connection, "INSERT INTO attachment_references (knowledge_document_id,knowledge_document_revision_id,attachment_id) VALUES (6001,7001,8001);");
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection,
            "INSERT INTO attachment_references (knowledge_document_id,knowledge_document_revision_id,attachment_id) VALUES (6001,7001,8001);"));

        await Execute(connection, "SAVEPOINT ownership_checks;");
        await Execute(connection, "INSERT INTO knowledge_documents (id,document_type,title,body_markdown,lifecycle_status,knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,created_by_user_id,created_by_display_name,updated_by_user_id,updated_by_display_name,created_at,updated_at,current_revision_number,version,is_deleted) VALUES (6002,'KnowledgeArticle','Other','body','Draft','Unknown','2026-08-29T00:00:00+00:00','Attachment User','创建人',5001,'Attachment User',5001,'Attachment User','2026-08-29T00:00:00+00:00','2026-08-29T00:00:00+00:00',1,1,0);");
        await Execute(connection, "INSERT INTO knowledge_document_revisions (id,knowledge_document_id,revision_number,title,body_markdown,author_user_id,author_display_name_snapshot,created_at,lifecycle_context,revision_origin) VALUES (7002,6002,1,'Other','body',5001,'Attachment User','2026-08-29T00:00:00+00:00','Draft','Created');");
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection,
            "INSERT INTO attachment_references (knowledge_document_id,knowledge_document_revision_id,attachment_id) VALUES (6002,7002,8001);"));
        await Execute(connection, "ROLLBACK TO ownership_checks; RELEASE ownership_checks;");

        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, ValidAttachmentSql(
            8002,
            6001,
            "objects/bb/bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb.bin",
            shaExpression: "X'00'")));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, "DELETE FROM attachments WHERE id=8001;"));
        await Assert.ThrowsAsync<SqliteException>(() => Execute(connection, "DELETE FROM knowledge_document_revisions WHERE id=7001;"));
        Assert.Equal(0L, await Scalar<long>(connection, "PRAGMA foreign_key_check;"));
    }

    private static KnowledgeHubDbContext CreateContext(SqliteConnection connection) => new(
        new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options);

    private static async Task SeedDocumentAndRevision(SqliteConnection connection)
    {
        await Execute(connection, """
            INSERT INTO users (id,display_name,access_level,is_active,created_at,updated_at,version)
            VALUES (5001,'Attachment User','Administrator',1,'2026-08-29T00:00:00+00:00','2026-08-29T00:00:00+00:00',1);
            INSERT INTO knowledge_documents (
                id,document_type,title,body_markdown,lifecycle_status,knowledge_status,
                knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,
                created_by_user_id,created_by_display_name,updated_by_user_id,updated_by_display_name,
                created_at,updated_at,current_revision_number,version,is_deleted)
            VALUES (
                6001,'KnowledgeArticle','Attachment baseline','body','Draft','Unknown',
                '2026-08-29T00:00:00+00:00','Attachment User','创建人',
                5001,'Attachment User',5001,'Attachment User',
                '2026-08-29T00:00:00+00:00','2026-08-29T00:00:00+00:00',1,1,0);
            INSERT INTO knowledge_document_revisions (
                id,knowledge_document_id,revision_number,title,body_markdown,author_user_id,author_display_name_snapshot,
                created_at,lifecycle_context,revision_origin)
            VALUES (7001,6001,1,'Attachment baseline','body',5001,'Attachment User','2026-08-29T00:00:00+00:00','Draft','Created');
            """);
    }

    private static string ValidAttachmentSql(
        long id,
        long documentId,
        string storageKey,
        string shaExpression = "zeroblob(32)") => $"""
            INSERT INTO attachments (
                id,knowledge_document_id,original_file_name,extension,kind,content_type,size_bytes,storage_key,sha256,
                storage_state,created_by_user_id,created_by_display_name_snapshot,created_at,version)
            VALUES ({id},{documentId},'manual.pdf','.pdf','File','application/pdf',9,'{storageKey}',{shaExpression},
                'Ready',5001,'Attachment User','2026-08-29T00:00:00+00:00',1);
            """;

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
