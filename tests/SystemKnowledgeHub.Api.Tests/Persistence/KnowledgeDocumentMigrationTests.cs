using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class KnowledgeDocumentMigrationTests
{
    [Fact]
    public async Task Migration_from_pre_knowledge_document_latest_preserves_existing_security_evidence_system_and_relationship_rows()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:;Foreign Keys=True");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<KnowledgeHubDbContext>().UseSqlite(connection).Options;
        await using var dbContext = new KnowledgeHubDbContext(options);
        var migrator = dbContext.Database.GetService<IMigrator>();
        await migrator.MigrateAsync("20260822025403_AddOidcAuthenticationFoundation");

        var timestamp = DateTimeOffset.UtcNow;
        var user = new User { DisplayName = "KC-B01 migration user", IsActive = true, AccessLevel = AccessLevel.Administrator, CreatedAt = timestamp, UpdatedAt = timestamp, Version = 1 };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        var loginIdentity = new LoginIdentity { UserId = user.Id, Provider = "TestOidc", Subject = "kc-b01-migration", IsActive = true, CreatedAt = timestamp, UpdatedAt = timestamp, Version = 1 };
        dbContext.LoginIdentities.Add(loginIdentity);
        var systemA = System("KC-B01-A", timestamp);
        var systemB = System("KC-B01-B", timestamp);
        await dbContext.SaveChangesAsync();
        systemA.Id = 6101;
        systemB.Id = 6102;
        await InsertTargetEraSystem(connection, systemA);
        await InsertTargetEraSystem(connection, systemB);
        var relation = new KnowledgeRelation
        {
            SourceType = KnowledgeTargetType.System,
            SourceId = systemA.Id,
            TargetType = KnowledgeTargetType.System,
            TargetId = systemB.Id,
            RelationType = RelationType.DependsOn,
            CreatedAt = timestamp,
            CreatedByName = user.DisplayName,
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = user.DisplayName,
            KnowledgeStatusChangedByRole = "创建人",
            Version = 1,
        };
        dbContext.KnowledgeRelations.Add(relation);
        await dbContext.SaveChangesAsync();
        const long confirmationId = 7001;
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = """
                INSERT INTO evidence (
                    id,
                    evidence_type,
                    subject_type,
                    subject_id,
                    source_title,
                    source_locator_json,
                    support_reason,
                    provider_user_id,
                    provider_name,
                    provider_role,
                    provided_at,
                    created_at,
                    updated_at,
                    version)
                VALUES (
                    $id,
                    'HumanConfirmation',
                    'System',
                    $subjectId,
                    'Migration HumanConfirmation',
                    '{"method":"Meeting"}',
                    'Preserve reference',
                    $providerUserId,
                    $providerName,
                    '知识整理人员',
                    $timestamp,
                    $timestamp,
                    $timestamp,
                    1);
                """;
            command.Parameters.AddWithValue("$id", confirmationId);
            command.Parameters.AddWithValue("$subjectId", systemA.Id);
            command.Parameters.AddWithValue("$providerUserId", user.Id);
            command.Parameters.AddWithValue("$providerName", user.DisplayName);
            command.Parameters.AddWithValue("$timestamp", timestamp.ToString("O"));
            await command.ExecuteNonQueryAsync();
        }

        await migrator.MigrateAsync();

        Assert.Equal(user.Id, (await dbContext.Users.SingleAsync()).Id);
        Assert.Equal(loginIdentity.Id, (await dbContext.LoginIdentities.SingleAsync()).Id);
        Assert.Equal(systemA.Id, (await dbContext.Systems.SingleAsync(item => item.Name == systemA.Name)).Id);
        Assert.Equal(relation.Id, (await dbContext.KnowledgeRelations.SingleAsync()).Id);
        var preservedConfirmation = await dbContext.Evidence.SingleAsync();
        Assert.Equal(confirmationId, preservedConfirmation.Id);
        Assert.Equal(user.Id, preservedConfirmation.ProviderUserId);
        Assert.Equal("HumanConfirmation", preservedConfirmation.EvidenceType.ToString());
        Assert.Null(preservedConfirmation.KnowledgeDocumentRevisionNumberSnapshot);
        Assert.Equal(0, await dbContext.KnowledgeDocuments.CountAsync());
        Assert.Equal(
            dbContext.Database.GetMigrations().ToArray(),
            (await dbContext.Database.GetAppliedMigrationsAsync()).ToArray());
        Assert.Equal("ok", await Scalar<string>(connection, "PRAGMA integrity_check;"));
        Assert.Equal(0L, await Scalar<long>(connection, "SELECT count(*) FROM pragma_foreign_key_check;"));
    }

    private static KnowledgeSystem System(string name, DateTimeOffset timestamp) => new()
    {
        Name = name,
        DisplayName = name,
        SystemType = "Test",
        Lifecycle = SystemLifecycle.Running,
        CreatedAt = timestamp,
        CreatedByName = "migration",
        UpdatedAt = timestamp,
        KnowledgeStatus = KnowledgeStatus.Unknown,
        KnowledgeStatusChangedAt = timestamp,
        KnowledgeStatusChangedByName = "migration",
        KnowledgeStatusChangedByRole = "创建人",
        Version = 1,
    };

    private static async Task InsertTargetEraSystem(SqliteConnection connection, KnowledgeSystem system)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO systems (
                id, name, display_name, system_type, lifecycle, created_at, created_by_name, updated_at,
                knowledge_status, knowledge_status_changed_at, knowledge_status_changed_by_name,
                knowledge_status_changed_by_role, version)
            VALUES (
                $id, $name, $displayName, $systemType, $lifecycle, $createdAt, $createdByName, $updatedAt,
                $knowledgeStatus, $knowledgeStatusChangedAt, $knowledgeStatusChangedByName,
                $knowledgeStatusChangedByRole, $version);
            """;
        command.Parameters.AddWithValue("$id", system.Id);
        command.Parameters.AddWithValue("$name", system.Name);
        command.Parameters.AddWithValue("$displayName", system.DisplayName);
        command.Parameters.AddWithValue("$systemType", system.SystemType);
        command.Parameters.AddWithValue("$lifecycle", system.Lifecycle.ToString());
        command.Parameters.AddWithValue("$createdAt", system.CreatedAt.ToString("O"));
        command.Parameters.AddWithValue("$createdByName", system.CreatedByName);
        command.Parameters.AddWithValue("$updatedAt", system.UpdatedAt.ToString("O"));
        command.Parameters.AddWithValue("$knowledgeStatus", system.KnowledgeStatus.ToString());
        command.Parameters.AddWithValue("$knowledgeStatusChangedAt", system.KnowledgeStatusChangedAt.ToString("O"));
        command.Parameters.AddWithValue("$knowledgeStatusChangedByName", system.KnowledgeStatusChangedByName);
        command.Parameters.AddWithValue("$knowledgeStatusChangedByRole", system.KnowledgeStatusChangedByRole);
        command.Parameters.AddWithValue("$version", system.Version);
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
