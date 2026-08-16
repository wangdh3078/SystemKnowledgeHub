using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class IntegrationsApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;
    public IntegrationsApiTests(BootstrapWebApplicationFactory factory) { _factory = factory; _client = factory.CreateClient(); }

    [Fact]
    public async Task C17_requires_a_registered_party_and_a_type_specific_endpoint_then_Q14_reads_the_created_integration()
    {
        using var noSystem = await _client.PostAsJsonAsync("/api/integrations", RabbitPayload("No System", null, null, "topic.no-system"));
        Assert.Equal(HttpStatusCode.BadRequest, noSystem.StatusCode);
        using var noEndpoint = await _client.PostAsJsonAsync("/api/integrations", RabbitPayload("No Endpoint", 12, null, null));
        Assert.Equal(HttpStatusCode.BadRequest, noEndpoint.StatusCode);

        var created = await CreateRabbit("VS11 Create " + Guid.NewGuid().ToString("N")[..8]);
        Assert.Equal("Unknown", created.GetProperty("knowledgeStatus").GetString());
        using var detailResponse = await _client.GetAsync($"/api/integrations/{created.GetProperty("id").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MES", detail.GetProperty("sourceParty").GetProperty("displayName").GetString());
        Assert.Equal("equipment.status.changed", detail.GetProperty("endpoint").GetProperty("topic").GetString());
        Assert.Empty(detail.GetProperty("contractFields").EnumerateArray());
    }

    [Fact]
    public async Task C18_and_C19_update_only_overview_and_ordered_contract_fields()
    {
        var created = await CreateRabbit("VS11 Edit " + Guid.NewGuid().ToString("N")[..8]);
        var id = created.GetProperty("id").GetInt64();
        using var relation = await _client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "BusinessFunction", id = 77 }, relationType = "UsesIntegration",
            target = new { type = "Integration", id }, description = "使用状态消息", actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, relation.StatusCode);
        using var evidence = await _client.PostAsJsonAsync("/api/evidence", new
        {
            evidenceType = "MqMessage", subject = new { type = "Integration", id }, subjectDetailKey = (string?)null,
            sourceTitle = "equipment.status.changed", sourceReference = "rabbitmq://mes/topic", sourceLocator = (object?)null,
            summary = "消息引用", supportReason = "支持集成定义", confidence = "High", provider = Person("证据提供人"),
        });
        Assert.Equal(HttpStatusCode.Created, evidence.StatusCode);
        using var overview = await _client.PutAsJsonAsync($"/api/integrations/{id}/overview", new
        {
            name = "VS11 Updated " + Guid.NewGuid().ToString("N")[..8], integrationType = "RabbitMq",
            sourceParty = new { systemId = 12, displayName = "MES" }, targetParty = new { systemId = (long?)null, displayName = "Equipment Gateway" },
            flowDirection = "OneWay", purpose = "更新后用途", endpoint = new { exchange = "mes.exchange", topic = "equipment.status.changed", queue = "gateway.status" },
            databaseSourceId = (long?)null, databaseObjectId = (long?)null, actor = Actor(), concurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, overview.StatusCode);
        var updated = await overview.Content.ReadFromJsonAsync<JsonElement>();
        using var fields = await _client.PutAsJsonAsync($"/api/integrations/{id}/contract-fields", new
        {
            fields = new[]
            {
                new { order = 1, fieldName = "equipmentId", dataType = "VARCHAR2(20)", required = true, description = "设备编号", sampleValue = "EQP-01" },
                new { order = 2, fieldName = "state", dataType = "VARCHAR2(2)", required = false, description = "状态", sampleValue = "30" },
            }, actor = Actor(), concurrencyToken = updated.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, fields.StatusCode);
        using var detailResponse = await _client.GetAsync($"/api/integrations/{id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unknown", detail.GetProperty("header").GetProperty("knowledgeStatus").GetString());
        Assert.Single(detail.GetProperty("relatedFunctions").EnumerateArray());
        Assert.Single(detail.GetProperty("evidence").EnumerateArray());
        Assert.Equal("equipmentId", detail.GetProperty("contractFields")[0].GetProperty("fieldName").GetString());
        Assert.Equal("state", detail.GetProperty("contractFields")[1].GetProperty("fieldName").GetString());
    }

    [Fact]
    public async Task C32d_rejects_a_stale_preview_without_partial_write_then_applies_the_exact_integration_update()
    {
        var created = await CreateRabbit("VS11 Apply " + Guid.NewGuid().ToString("N")[..8]);
        var integrationId = created.GetProperty("id").GetInt64();
        var before = Snapshot(created.GetProperty("name").GetString()!, "原始用途");
        var after = Snapshot(created.GetProperty("name").GetString()!, "调查确认后的用途");
        var flow = await PrepareResolution(integrationId, before, after);

        using var mismatch = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.ItemId}/knowledge-updates/{flow.UpdateId}/apply-integration", new
        {
            integrationId, integration = Snapshot(created.GetProperty("name").GetString()!, "与预览不一致"), knowledgeStatusChange = (object?)null,
            applier = Person("知识更新执行人"), concurrencyToken = flow.ItemToken, targetConcurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        await AssertApplyState(integrationId, "原始用途", flow.UpdateId, KnowledgeUpdateStatus.Proposed, 0);

        using var applied = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.ItemId}/knowledge-updates/{flow.UpdateId}/apply-integration", new
        {
            integrationId, integration = after, knowledgeStatusChange = (object?)null,
            applier = Person("知识更新执行人"), concurrencyToken = flow.ItemToken, targetConcurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, applied.StatusCode);
        await AssertApplyState(integrationId, "调查确认后的用途", flow.UpdateId, KnowledgeUpdateStatus.Applied, 1);
    }

    private async Task<JsonElement> CreateRabbit(string name)
    {
        using var response = await _client.PostAsJsonAsync("/api/integrations", RabbitPayload(name, 12, null, "equipment.status.changed"));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<(long ItemId, long UpdateId, string ItemToken)> PrepareResolution(long integrationId, object before, object after)
    {
        using var create = await _client.PostAsJsonAsync("/api/unknown-items", new
        {
            systemId = 12, question = "集成用途是否正确？", context = "VS-11 C32d", priority = "High",
            primaryTarget = new { type = "Integration", id = integrationId }, relatedTargets = Array.Empty<object>(), creator = Person("创建人"),
        });
        var item = await create.Content.ReadFromJsonAsync<JsonElement>(); var itemId = item.GetProperty("id").GetInt64();
        using var start = await _client.PostAsJsonAsync($"/api/unknown-items/{itemId}/start-investigation", new { actor = Person("调查人"), concurrencyToken = item.GetProperty("concurrencyToken").GetString() });
        var started = await start.Content.ReadFromJsonAsync<JsonElement>();
        using var evidence = await _client.PostAsJsonAsync($"/api/unknown-items/{itemId}/evidence", new
        {
            evidenceType = "MqMessage", subject = new { type = "UnknownItem", id = itemId }, subjectDetailKey = (string?)null,
            sourceTitle = "RabbitMQ 消息样本", sourceReference = "rabbitmq://vs11", sourceLocator = (object?)null, summary = "支持用途结论",
            supportReason = "调查证据", confidence = "High", provider = Person("业务专家"), concurrencyToken = started.GetProperty("concurrencyToken").GetString(),
        });
        var evidenced = await evidence.Content.ReadFromJsonAsync<JsonElement>();
        using var resolution = await _client.PutAsJsonAsync($"/api/unknown-items/{itemId}/resolution", new
        {
            conclusion = "集成用途需要修订。", knowledgeUpdates = new[] { new { id = (long?)null, target = new { type = "Integration", id = integrationId }, subjectDetailKey = (string?)null, applyAction = "UpdateIntegration", changeSummary = "更新集成关系用途", before, after, knowledgeStatusBefore = (string?)null, knowledgeStatusAfter = (string?)null } },
            actor = Person("调查人"), concurrencyToken = evidenced.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, resolution.StatusCode);
        var saved = await resolution.Content.ReadFromJsonAsync<JsonElement>();
        return (itemId, saved.GetProperty("knowledgeUpdates")[0].GetProperty("id").GetInt64(), saved.GetProperty("concurrencyToken").GetString()!);
    }

    private async Task AssertApplyState(long integrationId, string purpose, long updateId, KnowledgeUpdateStatus status, int activityCount)
    {
        await using var scope = _factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(purpose, (await db.Integrations.AsNoTracking().SingleAsync(item => item.Id == integrationId)).Purpose);
        Assert.Equal(status, (await db.KnowledgeUpdates.AsNoTracking().SingleAsync(item => item.Id == updateId)).Status);
        Assert.Equal(activityCount, await db.UnknownItemActivities.AsNoTracking().CountAsync(item => item.RelatedType == "KnowledgeUpdate" && item.RelatedId == updateId));
    }

    private static object RabbitPayload(string name, long? sourceSystemId, long? targetSystemId, string? topic) => new
    {
        name, integrationType = "RabbitMq", sourceParty = new { systemId = sourceSystemId, displayName = "MES" }, targetParty = new { systemId = targetSystemId, displayName = "Equipment Gateway" },
        flowDirection = "OneWay", purpose = "原始用途", endpoint = new { exchange = "mes.exchange", topic, queue = (string?)null }, databaseSourceId = (long?)null, databaseObjectId = (long?)null, actor = Actor(),
    };
    private static object Snapshot(string name, string purpose) => new
    {
        name, integrationType = "RabbitMq", sourceParty = new { systemId = 12L, displayName = "MES" }, targetParty = new { systemId = (long?)null, displayName = "Equipment Gateway" }, flowDirection = "OneWay", purpose,
        endpoint = new { exchange = "mes.exchange", topic = "equipment.status.changed", queue = (string?)null }, databaseSourceId = (long?)null, databaseObjectId = (long?)null,
    };
    private static object Actor() => new { displayName = "王敏", role = "知识整理人员" };
    private static object Person(string role) => new { displayName = "王敏", roleOrIdentity = role, occurredAt = DateTimeOffset.UtcNow, team = "制造系统组", externalUserKey = (string?)null, source = "Manual", note = (string?)null };
}
