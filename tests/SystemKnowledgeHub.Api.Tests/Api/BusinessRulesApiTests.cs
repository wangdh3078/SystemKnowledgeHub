using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class BusinessRulesApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;
    public BusinessRulesApiTests(BootstrapWebApplicationFactory factory) { _factory = factory; _client = factory.CreateAuthenticatedClient(); }

    [Fact]
    public async Task Create_and_Q13_persist_rule_and_enforce_unique_name_per_system()
    {
        var name = "VS10 Rule " + Guid.NewGuid().ToString("N")[..8];
        var created = await CreateRule(name, "创建与读取验证");
        Assert.Equal("Unknown", created.GetProperty("knowledgeStatus").GetString());

        using var duplicate = await _client.PostAsJsonAsync("/api/business-rules", new
        {
            systemId = 12, name, description = "重复", condition = (string?)null, result = (string?)null,
            inputData = Array.Empty<object>(), actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var detailResponse = await _client.GetAsync($"/api/business-rules/{created.GetProperty("id").GetInt64()}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(name, detail.GetProperty("header").GetProperty("name").GetString());
        Assert.Equal("MES", detail.GetProperty("system").GetProperty("name").GetString());
        Assert.Equal("STATE_FLAG", detail.GetProperty("inputData")[0].GetProperty("name").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await db.BusinessRules.AsNoTracking().SingleAsync(item => item.Id == created.GetProperty("id").GetInt64());
        Assert.Equal(KnowledgeStatus.Unknown, stored.KnowledgeStatus);
        Assert.Equal(1, stored.Version);
    }

    [Fact]
    public async Task C16_updates_only_rule_fields_and_preserves_status_relationship_and_evidence()
    {
        var created = await CreateRule("VS10 Edit " + Guid.NewGuid().ToString("N")[..8], "编辑前");
        var id = created.GetProperty("id").GetInt64();
        using var relationResponse = await _client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "BusinessFunction", id = 77 }, relationType = "AppliesRule",
            target = new { type = "BusinessRule", id }, description = "功能应用此规则", actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, relationResponse.StatusCode);
        using var evidenceResponse = await _client.PostAsJsonAsync("/api/evidence", new
        {
            evidenceType = "Sql", subject = new { type = "BusinessRule", id }, subjectDetailKey = "Condition",
            sourceTitle = "VS10Rule.sql", sourceReference = "repo://VS10Rule.sql", sourceLocator = (object?)null,
            summary = "SQL 条件", supportReason = "支持规则条件", confidence = "High", provider = Person("证据提供人"),
        });
        Assert.Equal(HttpStatusCode.Created, evidenceResponse.StatusCode);

        var updateName = "VS10 Edited " + Guid.NewGuid().ToString("N")[..8];
        using var updateResponse = await _client.PutAsJsonAsync($"/api/business-rules/{id}", new
        {
            name = updateName, description = "编辑后", condition = "STATE_FLAG = '30'", result = "Offline",
            inputData = new[] { new { name = "STATE_FLAG", description = "设备状态" } }, actor = Actor(),
            concurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);

        using var detailResponse = await _client.GetAsync($"/api/business-rules/{id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(updateName, detail.GetProperty("header").GetProperty("name").GetString());
        Assert.Equal("Unknown", detail.GetProperty("header").GetProperty("knowledgeStatus").GetString());
        Assert.Single(detail.GetProperty("relatedFunctions").EnumerateArray());
        Assert.Single(detail.GetProperty("evidence").EnumerateArray());
    }

    [Fact]
    public async Task C32c_rolls_back_mismatched_preview_then_atomically_applies_exact_business_rule_update()
    {
        var created = await CreateRule("VS10 Apply " + Guid.NewGuid().ToString("N")[..8], "原始描述");
        var ruleId = created.GetProperty("id").GetInt64();
        var before = new { name = created.GetProperty("name").GetString(), description = "原始描述", condition = "STATE_FLAG IN ('10','20','30')", result = "DisplayStatus", inputData = new[] { new { name = "STATE_FLAG", description = "设备状态" } } };
        var after = new { name = created.GetProperty("name").GetString(), description = "调查确认后的描述", condition = "STATE_FLAG = '30'", result = "Offline", inputData = new[] { new { name = "STATE_FLAG", description = "设备状态" } } };
        var flow = await PrepareRuleResolution(ruleId, before, after);

        using var mismatch = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.ItemId}/knowledge-updates/{flow.UpdateId}/apply-business-rule", new
        {
            businessRuleId = ruleId, rule = new { after.name, description = "与 Preview 不一致", after.condition, after.result, after.inputData },
            knowledgeStatusChange = (object?)null, applier = Person("知识更新执行人"), concurrencyToken = flow.ItemToken,
            targetConcurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, mismatch.StatusCode);
        await AssertRuleState(ruleId, "原始描述", flow.UpdateId, KnowledgeUpdateStatus.Proposed, 0);

        using var appliedResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.ItemId}/knowledge-updates/{flow.UpdateId}/apply-business-rule", new
        {
            businessRuleId = ruleId, rule = after, knowledgeStatusChange = (object?)null, applier = Person("知识更新执行人"),
            concurrencyToken = flow.ItemToken, targetConcurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, appliedResponse.StatusCode);
        await AssertRuleState(ruleId, "调查确认后的描述", flow.UpdateId, KnowledgeUpdateStatus.Applied, 1);
    }

    private async Task<JsonElement> CreateRule(string name, string description)
    {
        using var response = await _client.PostAsJsonAsync("/api/business-rules", new
        {
            systemId = 12, name, description, condition = "STATE_FLAG IN ('10','20','30')", result = "DisplayStatus",
            inputData = new[] { new { name = "STATE_FLAG", description = "设备状态" } }, actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private async Task<(long ItemId, long UpdateId, string ItemToken)> PrepareRuleResolution(long ruleId, object before, object after)
    {
        using var create = await _client.PostAsJsonAsync("/api/unknown-items", new
        {
            systemId = 12, question = "业务规则定义是否正确？", context = "VS-10 C32c", priority = "High",
            primaryTarget = new { type = "BusinessRule", id = ruleId }, relatedTargets = Array.Empty<object>(), creator = Person("创建人"),
        });
        var item = await create.Content.ReadFromJsonAsync<JsonElement>(); var id = item.GetProperty("id").GetInt64();
        using var start = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/start-investigation", new { actor = Person("调查人"), concurrencyToken = item.GetProperty("concurrencyToken").GetString() });
        var started = await start.Content.ReadFromJsonAsync<JsonElement>();
        using var evidence = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/evidence", new
        {
            evidenceType = "DatabaseSample", subject = new { type = "UnknownItem", id }, subjectDetailKey = (string?)null,
            sourceTitle = "规则样本", sourceReference = "database://vs10", sourceLocator = (object?)null, summary = "确认规则定义",
            supportReason = "支持调查结论", confidence = "High", provider = Person("业务专家"), concurrencyToken = started.GetProperty("concurrencyToken").GetString(),
        });
        var evidenced = await evidence.Content.ReadFromJsonAsync<JsonElement>();
        using var resolution = await _client.PutAsJsonAsync($"/api/unknown-items/{id}/resolution", new
        {
            conclusion = "规则定义需要修订。", knowledgeUpdates = new[] { new { id = (long?)null, target = new { type = "BusinessRule", id = ruleId }, subjectDetailKey = (string?)null, applyAction = "UpdateBusinessRule", changeSummary = "更新业务规则定义", before, after, knowledgeStatusBefore = (string?)null, knowledgeStatusAfter = (string?)null } },
            actor = Person("调查人"), concurrencyToken = evidenced.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, resolution.StatusCode);
        var resolved = await resolution.Content.ReadFromJsonAsync<JsonElement>();
        return (id, resolved.GetProperty("knowledgeUpdates")[0].GetProperty("id").GetInt64(), resolved.GetProperty("concurrencyToken").GetString()!);
    }

    private async Task AssertRuleState(long ruleId, string description, long updateId, KnowledgeUpdateStatus updateStatus, int activityCount)
    {
        await using var scope = _factory.Services.CreateAsyncScope(); var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(description, (await db.BusinessRules.AsNoTracking().SingleAsync(item => item.Id == ruleId)).Description);
        Assert.Equal(updateStatus, (await db.KnowledgeUpdates.AsNoTracking().SingleAsync(item => item.Id == updateId)).Status);
        Assert.Equal(activityCount, await db.UnknownItemActivities.AsNoTracking().CountAsync(item => item.RelatedType == "KnowledgeUpdate" && item.RelatedId == updateId));
    }

    private static object Actor() => new { displayName = "王敏", role = "知识整理人员" };
    private static object Person(string role) => new { displayName = "王敏", roleOrIdentity = role, occurredAt = DateTimeOffset.UtcNow, team = "制造系统组", externalUserKey = (string?)null, source = "Manual", note = (string?)null };
}
