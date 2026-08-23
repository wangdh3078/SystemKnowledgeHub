using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class SystemKnowledgeViewApiTests(BootstrapWebApplicationFactory factory) : IClassFixture<BootstrapWebApplicationFactory>
{
    [Fact]
    public async Task Knowledge_view_is_a_bounded_read_projection_with_deduplicated_related_documents()
    {
        long documentId;
        long systemVersion;
        long viewerId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var now = DateTimeOffset.UtcNow;
            var user = await db.Users.OrderBy(item => item.Id).FirstAsync();
            var viewer = new User { DisplayName = $"Unified Viewer {Guid.NewGuid():N}", IsActive = true, AccessLevel = AccessLevel.Viewer, CreatedAt = now, UpdatedAt = now, Version = 1 };
            db.Users.Add(viewer);
            await db.SaveChangesAsync();
            viewerId = viewer.Id;
            var system = await db.Systems.SingleAsync(item => item.Id == 12);
            systemVersion = system.Version;
            var document = new KnowledgeDocument
            {
                DocumentType = DocumentType.Sop, Title = "Unified view SOP", BodyMarkdown = "正文", LifecycleStatus = DocumentLifecycleStatus.Published,
                KnowledgeStatus = KnowledgeStatus.Inferred, KnowledgeStatusChangedAt = now, KnowledgeStatusChangedByName = user.DisplayName, KnowledgeStatusChangedByRole = "测试",
                CreatedByUserId = user.Id, CreatedByDisplayName = user.DisplayName, UpdatedByUserId = user.Id, UpdatedByDisplayName = user.DisplayName,
                CreatedAt = now, UpdatedAt = now, Version = 1,
            };
            var archived = new KnowledgeDocument
            {
                DocumentType = DocumentType.Sop, Title = "Archived unified view SOP", BodyMarkdown = "正文", LifecycleStatus = DocumentLifecycleStatus.Archived,
                KnowledgeStatus = KnowledgeStatus.Unknown, KnowledgeStatusChangedAt = now, KnowledgeStatusChangedByName = user.DisplayName, KnowledgeStatusChangedByRole = "测试",
                CreatedByUserId = user.Id, CreatedByDisplayName = user.DisplayName, UpdatedByUserId = user.Id, UpdatedByDisplayName = user.DisplayName,
                CreatedAt = now, UpdatedAt = now, Version = 1,
            };
            db.KnowledgeDocuments.AddRange(document, archived);
            await db.SaveChangesAsync();
            documentId = document.Id;
            db.KnowledgeRelations.AddRange(
                Relation(KnowledgeTargetType.KnowledgeDocument, document.Id, KnowledgeTargetType.System, 12, RelationType.AppliesTo, now),
                Relation(KnowledgeTargetType.System, 12, KnowledgeTargetType.KnowledgeDocument, document.Id, RelationType.Documents, now),
                Relation(KnowledgeTargetType.KnowledgeDocument, archived.Id, KnowledgeTargetType.System, 12, RelationType.AppliesTo, now));
            db.Evidence.Add(new Evidence
            {
                EvidenceType = EvidenceType.CodeReference, SubjectType = EvidenceSubjectType.System, SubjectId = 12, SourceTitle = "系统级证据", SourceReference = "https://example.test/unified-view", SupportReason = "投影验证", ProviderName = user.DisplayName, ProviderRole = "测试", ProvidedAt = now, CreatedAt = now, UpdatedAt = now, Version = 1,
            });
            var unknown = new UnknownItem
            {
                ItemCode = "UV-OPEN", SystemId = 12, Question = "统一视图待确认问题", Priority = UnknownItemPriority.High, Status = UnknownItemStatus.Open,
                CreatedAt = now, CreatedByName = user.DisplayName, UpdatedAt = now, Version = 1,
            };
            unknown.Targets.Add(new UnknownItemTarget { TargetType = KnowledgeTargetType.System, TargetId = 12, IsPrimary = true, DisplaySnapshot = "MES" });
            db.UnknownItems.Add(unknown);
            await db.SaveChangesAsync();
        }

        using var viewerClient = await factory.CreateAuthenticatedClientAsync(viewerId);
        using var response = await viewerClient.GetAsync("/api/systems/12/knowledge-view");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.True(payload.GetProperty("overview").GetProperty("businessFunctionCount").GetInt32() >= 5);
        Assert.True(payload.GetProperty("overview").GetProperty("databaseObjectCount").GetInt32() >= 1);
        Assert.True(payload.GetProperty("overview").GetProperty("documentCount").GetInt32() >= 1);
        Assert.True(payload.GetProperty("overview").GetProperty("evidenceCount").GetInt32() >= 1);
        Assert.True(payload.GetProperty("overview").GetProperty("openUnknownItemCount").GetInt32() >= 1);
        var documentItem = Assert.Single(payload.GetProperty("documents").EnumerateArray().Where(item => item.GetProperty("id").GetInt64() == documentId));
        Assert.Equal("Sop", documentItem.GetProperty("documentType").GetString());
        Assert.Equal("Published", documentItem.GetProperty("lifecycleStatus").GetString());
        Assert.Equal(new[] { "AppliesTo", "Documents" }, documentItem.GetProperty("relationTypes").EnumerateArray().Select(item => item.GetString()));
        Assert.DoesNotContain(payload.GetProperty("documents").EnumerateArray(), item => item.GetProperty("title").GetString() == "Archived unified view SOP");
        Assert.Contains(payload.GetProperty("unknownItems").EnumerateArray(), item => item.GetProperty("itemCode").GetString() == "UV-OPEN");

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(systemVersion, (await verificationDb.Systems.SingleAsync(item => item.Id == 12)).Version);
        Assert.Equal(2, await verificationDb.KnowledgeRelations.CountAsync(item => item.SourceId == documentId || item.TargetId == documentId));
    }

    [Fact]
    public async Task Knowledge_view_returns_not_found_for_an_unknown_system()
    {
        using var client = factory.CreateAuthenticatedClient();
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/api/systems/999999/knowledge-view")).StatusCode);
    }

    private static KnowledgeRelation Relation(KnowledgeTargetType sourceType, long sourceId, KnowledgeTargetType targetType, long targetId, RelationType type, DateTimeOffset now)
        => new()
        {
            SourceType = sourceType, SourceId = sourceId, TargetType = targetType, TargetId = targetId, RelationType = type,
            CreatedAt = now, UpdatedAt = now, CreatedByName = "测试", KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now, KnowledgeStatusChangedByName = "测试", KnowledgeStatusChangedByRole = "测试", Version = 1,
        };
}
