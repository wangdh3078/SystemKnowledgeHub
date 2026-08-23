using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class RelationshipsApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public RelationshipsApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Target_search_add_detail_and_description_update_form_one_real_read_relationship()
    {
        using var search = await _client.GetAsync("/api/knowledge-targets?purpose=RelationTarget&q=TABLE_EQP&systemId=12&sourceType=BusinessFunction&sourceId=77&relationType=Reads&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, search.StatusCode);
        var targets = await search.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(targets.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("target").GetProperty("type").GetString() == "DatabaseObject"
            && item.GetProperty("target").GetProperty("id").GetInt64() == 45
            && item.GetProperty("systemContext")[0].GetProperty("name").GetString() == "MES");

        using var createdResponse = await Add("Reads", "DatabaseObject", 45, "查询设备主记录");
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt64();
        Assert.Equal("Unknown", created.GetProperty("knowledgeStatus").GetString());

        using var detailResponse = await _client.GetAsync($"/api/relationships/{id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Equipment Status Query", detail.GetProperty("source").GetProperty("title").GetString());
        Assert.Equal("MES.TABLE_EQP", detail.GetProperty("target").GetProperty("title").GetString());

        using var update = await _client.PutAsJsonAsync($"/api/relationships/{id}/description", new
        {
            description = "通过 QueryEquipmentStatus.sql 读取设备主记录",
            actor = Actor(),
            concurrencyToken = detail.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("通过 QueryEquipmentStatus.sql 读取设备主记录",
            (await update.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("description").GetString());

        using var functionDetail = await _client.GetAsync("/api/business-functions/77");
        var function = await functionDetail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(function.GetProperty("relatedData").EnumerateArray(), row => row.GetProperty("relationshipId").GetInt64() == id);
    }

    [Fact]
    public async Task Add_rejects_duplicate_illegal_endpoint_and_cross_system_calls()
    {
        using var first = await Add("Writes", "DatabaseObject", 45, null);
        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        using var duplicate = await Add("Writes", "DatabaseObject", 45, null);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.True((await duplicate.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("details").TryGetProperty("existingRelationId", out _));

        using var illegal = await Add("UsesField", "DatabaseObject", 45, null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, illegal.StatusCode);
        Assert.Equal("reference_invalid", (await illegal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var crossSystem = await Add("Calls", "BusinessFunction", 81, null);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, crossSystem.StatusCode);
        Assert.Contains("Integration", (await crossSystem.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("message").GetString());
    }

    [Fact]
    public async Task Relationship_status_requires_direct_evidence_then_human_confirmation()
    {
        using var createdResponse = await Add("Reads", "DatabaseColumn", 123, "读取设备状态字段", sourceId: 79);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt64();
        var token = created.GetProperty("concurrencyToken").GetString()!;

        using var blocked = await ChangeStatus(id, "Inferred", token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);

        using var evidence = await _client.PostAsJsonAsync("/api/evidence", new
        {
            evidenceType = "Sql", subject = new { type = "KnowledgeRelation", id }, subjectDetailKey = (string?)null,
            sourceTitle = "QueryLotTrackIn.sql", sourceReference = "QueryLotTrackIn.sql", sourceLocator = (object?)null,
            summary = "读取 STATE_FLAG", supportReason = "SQL 直接读取目标字段", confidence = "High", provider = Person(),
        });
        Assert.Equal(HttpStatusCode.Created, evidence.StatusCode);
        Assert.Equal("Unknown", (await evidence.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subjectKnowledgeStatus").GetString());

        using var inferredResponse = await ChangeStatus(id, "Inferred", token);
        Assert.Equal(HttpStatusCode.OK, inferredResponse.StatusCode);
        var inferred = await inferredResponse.Content.ReadFromJsonAsync<JsonElement>();
        token = inferred.GetProperty("concurrencyToken").GetString()!;

        using var confirmation = await _client.PostAsJsonAsync("/api/evidence/human-confirmations", new
        {
            subject = new { type = "KnowledgeRelation", id }, subjectDetailKey = (string?)null,
            knowledgeRoleId = (long?)null, confirmationMethod = "Meeting", confirmedAt = "2026-08-22T02:30:00Z",
            confirmationStatement = "确认该功能读取 STATE_FLAG。", supportReason = "MES 业务专家确认关系属实。",
            sourceNote = "VS-08 评审",
        });
        Assert.Equal(HttpStatusCode.Created, confirmation.StatusCode);
        Assert.Equal("Inferred", (await confirmation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subjectKnowledgeStatus").GetString());

        using var confirmed = await ChangeStatus(id, "Confirmed", token);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("Confirmed", (await confirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("knowledgeStatus").GetString());
    }

    [Fact]
    public async Task Knowledge_document_can_relate_to_structured_objects_and_another_document_without_changing_document_status()
    {
        var documentId = await CreateDocument("关系 SOP");
        var otherDocumentId = await CreateDocument("关联说明");

        using var system = await _client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "KnowledgeDocument", id = documentId }, relationType = "AppliesTo",
            target = new { type = "System", id = 12L }, description = (string?)null, actor = new { displayName = "伪造操作者", role = "伪造" },
        });
        Assert.Equal(HttpStatusCode.Created, system.StatusCode);
        var created = await system.Content.ReadFromJsonAsync<JsonElement>();
        using var systemDetail = await _client.GetAsync($"/api/relationships/{created.GetProperty("id").GetInt64()}");
        Assert.Equal("SEC-01 Test Principal", (await systemDetail.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("created").GetProperty("displayName").GetString());

        await AssertCreated(documentId, "Documents", "BusinessFunction", 77L);
        await AssertCreated(documentId, "References", "DatabaseObject", 45L);
        await AssertCreated(documentId, "Documents", "BusinessRule", await CreateBusinessRule("文档关系规则"));
        using var related = await _client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "KnowledgeDocument", id = documentId }, relationType = "References",
            target = new { type = "KnowledgeDocument", id = otherDocumentId }, description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, related.StatusCode);

        using var list = await _client.GetAsync($"/api/relationships?objectType=KnowledgeDocument&objectId={otherDocumentId}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var rows = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(rows.EnumerateArray(), item => item.GetProperty("direction").GetString() == "Incoming"
            && item.GetProperty("related").GetProperty("id").GetInt64() == documentId);
        using var outgoing = await _client.GetAsync($"/api/relationships?objectType=KnowledgeDocument&objectId={documentId}");
        var outgoingRows = await outgoing.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(5, outgoingRows.GetArrayLength());

        using var duplicate = await _client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "KnowledgeDocument", id = documentId }, relationType = "References",
            target = new { type = "KnowledgeDocument", id = otherDocumentId }, description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        using var self = await _client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "KnowledgeDocument", id = documentId }, relationType = "References",
            target = new { type = "KnowledgeDocument", id = documentId }, description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, self.StatusCode);

        foreach (var legacyRelationType in new[] { "RelatedTo", "Implements", "Resolves" })
        {
            using var rejected = await _client.PostAsJsonAsync("/api/relationships", new
            {
                source = new { type = "KnowledgeDocument", id = documentId }, relationType = legacyRelationType,
                target = new { type = "System", id = 12L }, description = (string?)null,
            });
            Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
        }

        var relationId = (await related.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
        using var deleted = await _client.DeleteAsync($"/api/relationships/{relationId}");
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        using var document = await _client.GetAsync($"/api/knowledge-documents/{documentId}");
        Assert.Equal("Unknown", (await document.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("knowledgeStatus").GetString());
    }

    [Fact]
    public async Task Viewer_can_read_document_relationships_but_cannot_create_or_delete_them()
    {
        var documentId = await CreateDocument("只读关系文档");
        await AssertCreated(documentId, "AppliesTo", "System", 12L);
        var viewerId = await CreateUser(AccessLevel.Viewer);
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);

        using var read = await viewer.GetAsync($"/api/relationships?objectType=KnowledgeDocument&objectId={documentId}");
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        var relationshipId = (await read.Content.ReadFromJsonAsync<JsonElement>())[0].GetProperty("id").GetInt64();
        using var add = await viewer.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "KnowledgeDocument", id = documentId }, relationType = "References",
            target = new { type = "System", id = 12L }, description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Forbidden, add.StatusCode);
        using var delete = await viewer.DeleteAsync($"/api/relationships/{relationshipId}");
        Assert.Equal(HttpStatusCode.Forbidden, delete.StatusCode);
    }

    [Fact]
    public async Task Document_type_traceability_rules_filter_targets_and_reject_forged_combinations()
    {
        var requirementId = await CreateDocument("设备状态需求", "Requirement");
        var specificationId = await CreateDocument("设备状态规格", "Specification");
        var testCaseId = await CreateDocument("设备状态测试", "TestCase");
        var sopId = await CreateDocument("设备状态操作", "Sop");
        var designNoteId = await CreateDocument("设备状态设计", "DesignNote");

        await AssertCreated(requirementId, "SpecifiedBy", "KnowledgeDocument", specificationId);
        await AssertCreated(requirementId, "VerifiedBy", "KnowledgeDocument", testCaseId);
        await AssertCreated(specificationId, "VerifiedBy", "KnowledgeDocument", testCaseId);
        await AssertCreated(sopId, "AppliesTo", "System", 12L);
        await AssertCreated(designNoteId, "References", "KnowledgeDocument", specificationId);
        await AssertCreated(sopId, "Supersedes", "KnowledgeDocument", await CreateDocument("旧设备状态操作", "Sop"));

        using var specifiedByTargets = await _client.GetAsync($"/api/knowledge-targets?purpose=RelationTarget&sourceType=KnowledgeDocument&sourceId={requirementId}&relationType=SpecifiedBy&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, specifiedByTargets.StatusCode);
        var specifiedByItems = (await specifiedByTargets.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items");
        Assert.Contains(specifiedByItems.EnumerateArray(), item => item.GetProperty("target").GetProperty("id").GetInt64() == specificationId);
        Assert.DoesNotContain(specifiedByItems.EnumerateArray(), item => item.GetProperty("target").GetProperty("id").GetInt64() == testCaseId);

        foreach (var forged in new[]
        {
            new { sourceId = requirementId, relationType = "SpecifiedBy", targetId = testCaseId },
            new { sourceId = designNoteId, relationType = "References", targetId = testCaseId },
            new { sourceId = sopId, relationType = "Supersedes", targetId = specificationId },
            new { sourceId = testCaseId, relationType = "VerifiedBy", targetId = requirementId },
        })
        {
            using var rejected = await _client.PostAsJsonAsync("/api/relationships", new
            {
                source = new { type = "KnowledgeDocument", id = forged.sourceId }, relationType = forged.relationType,
                target = new { type = "KnowledgeDocument", id = forged.targetId }, description = (string?)null,
            });
            Assert.Equal(HttpStatusCode.UnprocessableEntity, rejected.StatusCode);
        }
    }

    private async Task<long> CreateDocument(string title, string documentType = "Sop")
    {
        using var response = await _client.PostAsJsonAsync("/api/knowledge-documents", new { documentType, title, bodyMarkdown = "正文" });
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    private async Task AssertCreated(long documentId, string relationType, string targetType, long targetId)
    {
        using var response = await _client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "KnowledgeDocument", id = documentId }, relationType,
            target = new { type = targetType, id = targetId }, description = (string?)null,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task<long> CreateBusinessRule(string name)
    {
        using var response = await _client.PostAsJsonAsync("/api/business-rules", new
        {
            systemId = 12L, name, description = "文档关系测试规则", condition = (string?)null,
            result = (string?)null, inputData = Array.Empty<object>(), actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    private async Task<long> CreateIntegration(string name)
    {
        using var response = await _client.PostAsJsonAsync("/api/integrations", new
        {
            name, integrationType = "RabbitMq", sourceParty = new { systemId = 12L, displayName = "MES" },
            targetParty = new { systemId = (long?)null, displayName = "Gateway" }, flowDirection = "OneWay",
            purpose = "文档关系测试", endpoint = new { exchange = "mes.exchange", topic = "document.relation", queue = (string?)null },
            databaseSourceId = (long?)null, databaseObjectId = (long?)null, actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    private async Task<long> CreateUser(AccessLevel accessLevel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"KC-B04 {Guid.NewGuid():N}",
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private Task<HttpResponseMessage> Add(string relationType, string targetType, long targetId, string? description, long sourceId = 77)
        => _client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "BusinessFunction", id = sourceId }, relationType,
            target = new { type = targetType, id = targetId }, description, actor = Actor(),
        });

    private Task<HttpResponseMessage> ChangeStatus(long id, string targetStatus, string token)
        => _client.PutAsJsonAsync($"/api/relationships/{id}/knowledge-status", new
        {
            targetStatus, reason = (string?)null, actor = Person(), concurrencyToken = token,
        });

    private static object Actor() => new { displayName = "王敏", role = "知识整理人员" };
    private static object Person(string name = "王敏", string role = "知识整理人员") => new
    {
        displayName = name, roleOrIdentity = role, occurredAt = "2026-08-15T10:00:00Z",
        team = "制造系统组", externalUserKey = (string?)null, source = "Manual", note = (string?)null,
    };
}
