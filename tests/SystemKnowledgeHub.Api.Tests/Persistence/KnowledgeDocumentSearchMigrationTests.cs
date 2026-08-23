using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class KnowledgeDocumentSearchMigrationTests
{
    [Fact]
    public async Task Migration_backfills_existing_documents_into_the_fts_index_without_changing_canonical_rows()
    {
        var path = Path.Combine(Path.GetTempPath(), $"knowledge-document-search-migration-{Guid.NewGuid():N}.db");
        try
        {
            await using var connection = new SqliteConnection($"Data Source={path};Foreign Keys=True;Pooling=False");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options;
            await using var dbContext = new KnowledgeHubDbContext(options);
            var migrator = dbContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260822213000_AddKnowledgeDocumentEvidenceSubject");

            var timestamp = DateTimeOffset.UtcNow;
            var user = new User { DisplayName = "FTS migration user", IsActive = true, AccessLevel = AccessLevel.Administrator, CreatedAt = timestamp, UpdatedAt = timestamp, Version = 1 };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync();
            await using (var insert = connection.CreateCommand())
            {
                insert.CommandText = """
                    INSERT INTO knowledge_documents (
                        document_type,title,summary,body_markdown,lifecycle_status,knowledge_status,
                        knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,
                        created_by_user_id,created_by_display_name,updated_by_user_id,updated_by_display_name,
                        created_at,updated_at,published_at,version)
                    VALUES (
                        'Sop','旧 Oracle SOP','已有文档回填','检查 Oracle 数据库监听服务后再重启。','Published','Inferred',
                        $timestamp,$displayName,'测试',$userId,$displayName,$userId,$displayName,$timestamp,$timestamp,$timestamp,1);
                    """;
                insert.Parameters.AddWithValue("$timestamp", timestamp);
                insert.Parameters.AddWithValue("$displayName", user.DisplayName);
                insert.Parameters.AddWithValue("$userId", user.Id);
                await insert.ExecuteNonQueryAsync();
            }

            await migrator.MigrateAsync();

            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT count(*) FROM knowledge_documents_fts WHERE knowledge_documents_fts MATCH $query;";
            command.Parameters.AddWithValue("$query", "\"监\" AND \"听\"");
            Assert.Equal(1L, (long)(await command.ExecuteScalarAsync() ?? 0L));
            Assert.Equal("检查 Oracle 数据库监听服务后再重启。", (await dbContext.KnowledgeDocuments.SingleAsync()).BodyMarkdown);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }
}
