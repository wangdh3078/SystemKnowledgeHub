using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class CurrentProjectionHistoricalBoundaryApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory factory;
    private readonly HttpClient client;

    public CurrentProjectionHistoricalBoundaryApiTests(BootstrapWebApplicationFactory factory)
    {
        this.factory = factory;
        client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Deleted_document_is_absent_from_current_and_FTS_but_evidence_and_revisions_use_tombstone()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var document = await CreateDocument("Requirement", $"DELETE B03 historical {suffix}", $"fts_boundary_{suffix}");
        var documentId = document.GetProperty("id").GetInt64();
        long evidenceId;
        long confirmationId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var timestamp = DateTimeOffset.UtcNow;
            var evidence = Evidence(documentId, EvidenceType.CodeReference, "B03 code evidence", timestamp);
            var confirmation = Evidence(documentId, EvidenceType.HumanConfirmation, "B03 human confirmation", timestamp.AddMinutes(1));
            confirmation.KnowledgeDocumentRevisionNumberSnapshot = 1;
            db.Evidence.AddRange(evidence, confirmation);
            await db.SaveChangesAsync();
            evidenceId = evidence.Id;
            confirmationId = confirmation.Id;

            var entity = await db.KnowledgeDocuments.SingleAsync(item => item.Id == documentId);
            entity.IsDeleted = true;
            entity.DeletedAt = timestamp;
            entity.DeletedByUserId = entity.UpdatedByUserId;
            entity.DeletedByDisplayName = entity.UpdatedByDisplayName;
            await db.SaveChangesAsync();
        }

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/knowledge-documents/{documentId}")).StatusCode);
        var list = await GetJson($"/api/knowledge-documents?query={Uri.EscapeDataString(suffix)}&page=1&pageSize=20");
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt64() == documentId);
        var search = await GetJson($"/api/search?q=fts_boundary_{suffix}&types=KnowledgeDocument");
        Assert.DoesNotContain(SearchDocumentItems(search), item => item.GetProperty("id").GetInt64() == documentId);

        foreach (var id in new[] { evidenceId, confirmationId })
        {
            var detail = await GetJson($"/api/evidence/{id}");
            Assert.True(detail.GetProperty("subjectIdentity").GetProperty("isDeleted").GetBoolean());
            Assert.False(detail.GetProperty("subjectIdentity").GetProperty("isNavigable").GetBoolean());
            Assert.Equal(JsonValueKind.Null, detail.GetProperty("subjectContext").ValueKind);
            Assert.Empty(detail.GetProperty("availableActions").EnumerateArray());
        }
        var evidenceList = await GetJson($"/api/evidence?subjectType=KnowledgeDocument&subjectId={documentId}");
        Assert.True(evidenceList.GetProperty("subject").GetProperty("isDeleted").GetBoolean());
        Assert.Equal(2, evidenceList.GetProperty("items").GetArrayLength());

        var revisions = await GetJson($"/api/knowledge-documents/{documentId}/revisions?page=1&pageSize=20");
        Assert.True(revisions.GetProperty("owner").GetProperty("isDeleted").GetBoolean());
        Assert.NotEmpty(revisions.GetProperty("items").EnumerateArray());
        var revision = await GetJson($"/api/knowledge-documents/{documentId}/revisions/1");
        Assert.True(revision.GetProperty("owner").GetProperty("isDeleted").GetBoolean());
        Assert.Equal($"fts_boundary_{suffix}", revision.GetProperty("bodyMarkdown").GetString());

        using var restore = await client.PostAsJsonAsync($"/api/knowledge-documents/{documentId}/revisions/1/restore", new
        {
            concurrencyToken = document.GetProperty("concurrencyToken").GetString(),
            reason = "must remain denied",
        });
        Assert.Equal(HttpStatusCode.NotFound, restore.StatusCode);

        using var addAfterDelete = await client.PostAsJsonAsync("/api/evidence", EvidenceRequest(documentId));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, addAfterDelete.StatusCode);

        Assert.Equal(0, await KnowledgeDocumentSearchMaintenanceCommand.RunAsync(
            [KnowledgeDocumentSearchMaintenanceCommand.CommandName], factory.Services));
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(0, await verificationDb.Database.SqlQuery<long>(
            $"SELECT count(*) AS Value FROM knowledge_documents_fts WHERE rowid={documentId}").SingleAsync());
    }

    [Fact]
    public async Task Closed_unknown_and_applied_update_keep_original_deleted_target_identity()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var system = await CreateSystem($"B03 workflow system {suffix}");
        var systemId = system.GetProperty("id").GetInt64();
        var function = await Created("/api/business-functions", new
        {
            systemId,
            name = $"B03 workflow function {suffix}",
            functionType = "Query",
            rewriteStatus = "Unknown",
            actor = Actor(),
        });
        var functionId = function.GetProperty("id").GetInt64();
        long unknownId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var timestamp = DateTimeOffset.UtcNow;
            var item = new UnknownItem
            {
                ItemCode = $"UNK-B03-{suffix}",
                SystemId = systemId,
                Question = "历史闭环仍需解释",
                Priority = UnknownItemPriority.Medium,
                Status = UnknownItemStatus.Closed,
                ConclusionConfirmedAt = timestamp,
                ClosedAt = timestamp,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                CreatedByName = "B03 test",
                Version = 1,
            };
            item.Targets.Add(new UnknownItemTarget
            {
                TargetType = KnowledgeTargetType.BusinessFunction,
                TargetId = functionId,
                IsPrimary = true,
                DisplaySnapshot = $"B03 workflow function {suffix}",
            });
            item.KnowledgeUpdates.Add(new KnowledgeUpdate
            {
                TargetType = KnowledgeTargetType.BusinessFunction,
                TargetId = functionId,
                ChangeSummary = "已应用的历史更新",
                BeforeJson = "{}",
                AfterJson = "{\"purpose\":\"verified\"}",
                Status = KnowledgeUpdateStatus.Applied,
                AppliedByName = "B03 test",
                AppliedByRole = "test",
                AppliedAt = timestamp,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
            });
            item.Activities.Add(new UnknownItemActivity
            {
                ActivityType = UnknownItemActivityType.Closed,
                ActorName = "B03 test",
                ActorRole = "test",
                OccurredAt = timestamp,
                Note = "closed",
            });
            db.UnknownItems.Add(item);
            await db.SaveChangesAsync();
            unknownId = item.Id;

            var target = await db.BusinessFunctions.SingleAsync(entity => entity.Id == functionId);
            target.IsDeleted = true;
            target.DeletedAt = timestamp;
            target.DeletedByUserId = target.CreatedByUserId;
            target.DeletedByDisplayName = target.CreatedByName;
            await db.SaveChangesAsync();
        }

        var replacement = await Created("/api/business-functions", new
        {
            systemId,
            name = $"B03 workflow function {suffix}",
            functionType = "Query",
            rewriteStatus = "Unknown",
            actor = Actor(),
        });
        var replacementId = replacement.GetProperty("id").GetInt64();

        var detail = await GetJson($"/api/unknown-items/{unknownId}");
        var identity = detail.GetProperty("relatedObjects")[0].GetProperty("identity");
        Assert.Equal(functionId, identity.GetProperty("id").GetInt64());
        Assert.NotEqual(replacementId, identity.GetProperty("id").GetInt64());
        Assert.True(identity.GetProperty("isDeleted").GetBoolean());
        var updateIdentity = detail.GetProperty("knowledgeUpdates")[0].GetProperty("targetIdentity");
        Assert.Equal(functionId, updateIdentity.GetProperty("id").GetInt64());
        Assert.True(updateIdentity.GetProperty("isDeleted").GetBoolean());
        Assert.Empty(detail.GetProperty("availableActions").EnumerateArray());

        var list = await GetJson($"/api/unknown-items?status=Closed&keyword={Uri.EscapeDataString(suffix)}&page=1&pageSize=20");
        Assert.Contains(list.GetProperty("items").EnumerateArray(), row => row.GetProperty("id").GetInt64() == unknownId);
    }

    [Fact]
    public async Task Deleted_relation_endpoints_are_excluded_while_physically_missing_endpoints_fail_closed()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var requirement = await CreateDocument("Requirement", $"B03 trace requirement {suffix}", "requirement");
        var specification = await CreateDocument("Specification", $"B03 trace specification {suffix}", "specification");
        var testCase = await CreateDocument("TestCase", $"B03 trace test {suffix}", "test");
        var system = await CreateSystem($"B03 impact system {suffix}");
        var requirementId = requirement.GetProperty("id").GetInt64();
        var specificationId = specification.GetProperty("id").GetInt64();
        var testId = testCase.GetProperty("id").GetInt64();
        var systemId = system.GetProperty("id").GetInt64();
        long deletedEndpointRelationId;

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var timestamp = DateTimeOffset.UtcNow;
            var specifiedBy = Relation(requirementId, specificationId, RelationType.SpecifiedBy, timestamp);
            var verifiedBy = Relation(specificationId, testId, RelationType.VerifiedBy, timestamp);
            var appliesTo = Relation(requirementId, systemId, RelationType.AppliesTo, timestamp, KnowledgeTargetType.System);
            db.KnowledgeRelations.AddRange(specifiedBy, verifiedBy, appliesTo);
            await db.SaveChangesAsync();
            deletedEndpointRelationId = appliesTo.Id;

            var deletedSpecification = await db.KnowledgeDocuments.SingleAsync(item => item.Id == specificationId);
            deletedSpecification.IsDeleted = true;
            deletedSpecification.DeletedAt = timestamp;
            deletedSpecification.DeletedByUserId = deletedSpecification.UpdatedByUserId;
            deletedSpecification.DeletedByDisplayName = deletedSpecification.UpdatedByDisplayName;
            var deletedSystem = await db.Systems.SingleAsync(item => item.Id == systemId);
            deletedSystem.IsDeleted = true;
            deletedSystem.DeletedAt = timestamp;
            deletedSystem.DeletedByUserId = deletedSystem.CreatedByUserId;
            deletedSystem.DeletedByDisplayName = deletedSystem.CreatedByName;
            await db.SaveChangesAsync();
        }

        var trace = await GetJson($"/api/knowledge-documents/{requirementId}/traceability");
        Assert.False(trace.GetProperty("coverage").GetProperty("hasSpecification").GetBoolean());
        Assert.Empty(trace.GetProperty("specifications").EnumerateArray());
        var impact = await GetJson($"/api/knowledge-documents/{requirementId}/traceability/impact?page=1&pageSize=100");
        Assert.Equal(0, impact.GetProperty("total").GetInt32());
        var related = await GetJson($"/api/relationships?objectType=KnowledgeDocument&objectId={requirementId}");
        Assert.Empty(related.EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/relationships/{deletedEndpointRelationId}")).StatusCode);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            db.KnowledgeRelations.Add(Relation(requirementId, 9_007_199_254_740_000, RelationType.AppliesTo,
                DateTimeOffset.UtcNow, KnowledgeTargetType.System));
            await db.SaveChangesAsync();
        }
        using var malformed = await client.GetAsync($"/api/knowledge-documents/{requirementId}/traceability/impact?page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, malformed.StatusCode);
    }

    private async Task<JsonElement> CreateDocument(string type, string title, string body) => await Created(
        "/api/knowledge-documents",
        new { documentType = type, title, summary = "DELETE-B03", bodyMarkdown = body });

    private async Task<JsonElement> CreateSystem(string name) => await Created("/api/systems", new
    {
        name,
        displayName = name,
        systemType = "Service",
        lifecycle = "Running",
        purpose = "DELETE-B03",
        actor = Actor(),
    });

    private async Task<JsonElement> Created(string uri, object body)
    {
        using var response = await client.PostAsJsonAsync(uri, body);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<JsonElement> GetJson(string uri)
    {
        using var response = await client.GetAsync(uri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static Evidence Evidence(long documentId, EvidenceType type, string title, DateTimeOffset at) => new()
    {
        EvidenceType = type,
        SubjectType = EvidenceSubjectType.KnowledgeDocument,
        SubjectId = documentId,
        SourceTitle = title,
        SourceReference = "B03",
        SupportReason = "DELETE-B03 historical boundary",
        ProviderName = "B03 test",
        ProviderRole = "test",
        ProviderSource = "Test",
        ProvidedAt = at,
        CreatedAt = at,
        UpdatedAt = at,
        Version = 1,
    };

    private static KnowledgeRelation Relation(
        long sourceId,
        long targetId,
        RelationType relationType,
        DateTimeOffset at,
        KnowledgeTargetType targetType = KnowledgeTargetType.KnowledgeDocument) => new()
    {
        SourceType = KnowledgeTargetType.KnowledgeDocument,
        SourceId = sourceId,
        TargetType = targetType,
        TargetId = targetId,
        RelationType = relationType,
        CreatedAt = at,
        CreatedByName = "B03 test",
        CreatedByRole = "test",
        UpdatedAt = at,
        KnowledgeStatus = KnowledgeStatus.Unknown,
        KnowledgeStatusChangedAt = at,
        KnowledgeStatusChangedByName = "B03 test",
        KnowledgeStatusChangedByRole = "test",
        Version = 1,
    };

    private static object EvidenceRequest(long documentId) => new
    {
        evidenceType = "CodeReference",
        subject = new { type = "KnowledgeDocument", id = documentId },
        subjectDetailKey = (string?)null,
        sourceTitle = "denied after delete",
        sourceReference = "B03",
        sourceLocator = new { repository = "test", file = "test.cs", startLine = 1, endLine = 1 },
        summary = "denied",
        supportReason = "deleted subject",
        confidence = "High",
        provider = new
        {
            displayName = "B03 test",
            roleOrIdentity = "test",
            occurredAt = "2026-08-28T00:00:00Z",
            team = (string?)null,
            externalUserKey = (string?)null,
            source = "Test",
            note = (string?)null,
        },
    };

    private static JsonElement[] SearchDocumentItems(JsonElement response)
    {
        var group = response.GetProperty("groups").EnumerateArray()
            .FirstOrDefault(item => item.GetProperty("objectType").GetString() == "KnowledgeDocument");
        return group.ValueKind == JsonValueKind.Undefined
            ? []
            : group.GetProperty("items").EnumerateArray().ToArray();
    }

    private static object Actor() => new { displayName = "B03 test", role = "test" };
}
