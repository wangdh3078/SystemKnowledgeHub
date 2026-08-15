using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class RelationshipsApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RelationshipsApiTests(BootstrapWebApplicationFactory factory) => _client = factory.CreateClient();

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
            confirmationStatement = "确认该功能读取 STATE_FLAG。", supportReason = "MES 业务专家确认关系属实。",
            sourceNote = "VS-08 评审", confirmer = Person("李工", "MES 业务专家"),
        });
        Assert.Equal(HttpStatusCode.Created, confirmation.StatusCode);
        Assert.Equal("Inferred", (await confirmation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subjectKnowledgeStatus").GetString());

        using var confirmed = await ChangeStatus(id, "Confirmed", token);
        Assert.Equal(HttpStatusCode.OK, confirmed.StatusCode);
        Assert.Equal("Confirmed", (await confirmed.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("knowledgeStatus").GetString());
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
