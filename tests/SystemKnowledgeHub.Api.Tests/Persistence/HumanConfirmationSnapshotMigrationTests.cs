using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class HumanConfirmationSnapshotMigrationTests
{
    [Fact]
    public async Task U04_migration_preserves_legacy_evidence_and_adds_only_nullable_snapshot_references()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var dbContext = new KnowledgeHubDbContext(options);
        var migrator = dbContext.GetService<IMigrator>();
        await migrator.MigrateAsync("20260820133249_AddUserFoundation");

        await using var insertCommand = connection.CreateCommand();
        insertCommand.CommandText = """
            INSERT INTO evidence (
                id, evidence_type, subject_type, subject_id, subject_detail_key,
                source_title, source_reference, source_locator_json, summary, support_reason,
                provider_name, provider_role, provider_source, provided_at,
                created_at, updated_at, version)
            VALUES (
                99001, 'HumanConfirmation', 'BusinessFunction', 77, 'Purpose',
                '人工确认 · 历史人员', '历史评审', '{"confirmationStatement":"历史确认"}', '历史确认', '历史人员确认',
                '历史人员', '历史业务专家', 'OnSite', '2026-08-15T02:00:00+00:00',
                '2026-08-15T02:01:00+00:00', '2026-08-15T02:01:00+00:00', 3);
            """;
        await insertCommand.ExecuteNonQueryAsync();

        await migrator.MigrateAsync();

        var columns = await ReadFirstColumn(connection, "PRAGMA table_info('evidence');", 1);
        Assert.Contains("provider_user_id", columns);
        Assert.Contains("provider_knowledge_role_id", columns);
        Assert.Contains("provider_employee_no", columns);
        Assert.Contains("provider_job_title", columns);

        var foreignKeys = await ReadRows(connection, "PRAGMA foreign_key_list('evidence');");
        Assert.Contains(foreignKeys, row =>
            row[2] == "users" && row[3] == "provider_user_id" && row[4] == "id" && row[6] == "RESTRICT");
        Assert.Contains(foreignKeys, row =>
            row[2] == "knowledge_roles" && row[3] == "provider_knowledge_role_id" && row[4] == "id" && row[6] == "RESTRICT");

        var indexes = await ReadFirstColumn(connection, "PRAGMA index_list('evidence');", 1);
        Assert.Contains("IX_evidence_provider_user_id", indexes);
        Assert.Contains("IX_evidence_provider_knowledge_role_id", indexes);
        Assert.Contains("IX_evidence_evidence_type_provided_at", indexes);
        Assert.Contains("IX_evidence_source_reference", indexes);
        Assert.Contains("IX_evidence_subject_type_subject_id_subject_detail_key", indexes);

        await using var rowCommand = connection.CreateCommand();
        rowCommand.CommandText = """
            SELECT provider_name, provider_role, provider_source, version,
                   provider_user_id, provider_knowledge_role_id, provider_employee_no, provider_job_title
            FROM evidence WHERE id = 99001;
            """;
        await using var reader = await rowCommand.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal("历史人员", reader.GetString(0));
        Assert.Equal("历史业务专家", reader.GetString(1));
        Assert.Equal("OnSite", reader.GetString(2));
        Assert.Equal(3L, reader.GetInt64(3));
        Assert.True(reader.IsDBNull(4));
        Assert.True(reader.IsDBNull(5));
        Assert.True(reader.IsDBNull(6));
        Assert.True(reader.IsDBNull(7));

        await using var schemaCommand = connection.CreateCommand();
        schemaCommand.CommandText = "SELECT sql FROM sqlite_master WHERE type = 'table' AND name = 'evidence';";
        var tableSql = (string?)await schemaCommand.ExecuteScalarAsync();
        Assert.Contains("ck_evidence_type", tableSql);
        Assert.Contains("ck_evidence_source_locator", tableSql);
        Assert.Contains("ck_evidence_version", tableSql);
        Assert.Contains("KnowledgeDocument", tableSql);
    }

    private static async Task<IReadOnlyList<string>> ReadFirstColumn(
        SqliteConnection connection,
        string sql,
        int ordinal)
    {
        var rows = await ReadRows(connection, sql);
        return rows.Select(row => row[ordinal]).ToArray();
    }

    private static async Task<IReadOnlyList<string[]>> ReadRows(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string[]>();
        while (await reader.ReadAsync())
        {
            var row = new string[reader.FieldCount];
            for (var index = 0; index < reader.FieldCount; index++)
            {
                row[index] = reader.IsDBNull(index) ? string.Empty : reader.GetValue(index).ToString() ?? string.Empty;
            }
            rows.Add(row);
        }
        return rows;
    }
}
