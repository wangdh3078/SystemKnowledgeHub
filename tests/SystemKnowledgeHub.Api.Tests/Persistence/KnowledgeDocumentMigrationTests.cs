using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
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
        await using var connection = new SqliteConnection($"Data Source={Path.Combine(Path.GetTempPath(), $"knowledge-document-migration-{Guid.NewGuid():N}.db")};Foreign Keys=True;Pooling=False");
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
        dbContext.Systems.AddRange(systemA, systemB);
        await dbContext.SaveChangesAsync();
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
        var confirmation = new Evidence
        {
            EvidenceType = EvidenceType.HumanConfirmation,
            SubjectType = EvidenceSubjectType.System,
            SubjectId = systemA.Id,
            SourceTitle = "Migration HumanConfirmation",
            SourceLocatorJson = "{\"method\":\"Meeting\"}",
            SupportReason = "Preserve reference",
            ProviderUserId = user.Id,
            ProviderName = user.DisplayName,
            ProviderRole = "知识整理人员",
            ProvidedAt = timestamp,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.Evidence.Add(confirmation);
        await dbContext.SaveChangesAsync();

        await migrator.MigrateAsync();

        Assert.Equal(user.Id, (await dbContext.Users.SingleAsync()).Id);
        Assert.Equal(loginIdentity.Id, (await dbContext.LoginIdentities.SingleAsync()).Id);
        Assert.Equal(systemA.Id, (await dbContext.Systems.SingleAsync(item => item.Name == systemA.Name)).Id);
        Assert.Equal(relation.Id, (await dbContext.KnowledgeRelations.SingleAsync()).Id);
        var preservedConfirmation = await dbContext.Evidence.SingleAsync();
        Assert.Equal(confirmation.Id, preservedConfirmation.Id);
        Assert.Equal(user.Id, preservedConfirmation.ProviderUserId);
        Assert.Equal("HumanConfirmation", preservedConfirmation.EvidenceType.ToString());
        Assert.Equal(0, await dbContext.KnowledgeDocuments.CountAsync());
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
}
