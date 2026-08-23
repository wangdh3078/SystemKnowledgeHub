using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class RelationshipVocabularyMigrationTests
{
    [Fact]
    public async Task Migration_preserves_legitimate_documents_relation_rejects_legacy_values_and_restores_them_on_down()
    {
        var path = Path.Combine(Path.GetTempPath(), $"relationship-vocabulary-migration-{Guid.NewGuid():N}.db");
        try
        {
            await using var connection = new SqliteConnection($"Data Source={path};Foreign Keys=True;Pooling=False");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options;
            await using var dbContext = new KnowledgeHubDbContext(options);
            var migrator = dbContext.Database.GetService<IMigrator>();
            await migrator.MigrateAsync("20260822223000_AddKnowledgeDocumentSearchFts");

            await InsertRelation(connection, "Documents", 1, 12);
            await migrator.MigrateAsync();

            Assert.Equal(1L, await ScalarLong(connection, "SELECT count(*) FROM knowledge_relations WHERE relation_type = 'Documents';"));
            await Assert.ThrowsAsync<SqliteException>(() => InsertRelation(connection, "RelatedTo", 2, 12));
            await InsertRelation(connection, "References", 3, 12);
            Assert.Equal(1L, await ScalarLong(connection, "SELECT count(*) FROM sqlite_master WHERE type = 'index' AND name = 'IX_knowledge_relations_source_type_source_id_target_type_target_id_relation_type';"));

            await migrator.MigrateAsync("20260822223000_AddKnowledgeDocumentSearchFts");
            await InsertRelation(connection, "RelatedTo", 2, 12);
            Assert.Equal(1L, await ScalarLong(connection, "SELECT count(*) FROM knowledge_relations WHERE relation_type = 'RelatedTo';"));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static async Task InsertRelation(SqliteConnection connection, string relationType, long sourceId, long targetId)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO knowledge_relations (
                source_type, source_id, target_type, target_id, relation_type, description,
                created_at, created_by_name, created_by_role, updated_at, knowledge_status,
                knowledge_status_reason, knowledge_status_changed_at, knowledge_status_changed_by_name,
                knowledge_status_changed_by_role, version)
            VALUES (
                'KnowledgeDocument', $sourceId, 'System', $targetId, $relationType, NULL,
                $timestamp, 'migration-test', NULL, $timestamp, 'Unknown', NULL,
                $timestamp, 'migration-test', 'test', 1);
            """;
        command.Parameters.AddWithValue("$sourceId", sourceId);
        command.Parameters.AddWithValue("$targetId", targetId);
        command.Parameters.AddWithValue("$relationType", relationType);
        command.Parameters.AddWithValue("$timestamp", DateTimeOffset.UtcNow);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> ScalarLong(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }
}
