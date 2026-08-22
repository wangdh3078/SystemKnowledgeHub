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

public sealed class KnowledgeResolutionApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public KnowledgeResolutionApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Concrete_apply_confirm_close_and_reopen_keep_applied_knowledge_and_investigation_facts()
    {
        var value = "VS09B-" + Guid.NewGuid().ToString("N")[..8];
        var flow = await PrepareColumnKnownValueFlow(value, "复核后的业务含义");
        var columnToken = await GetColumnToken();

        using var applyResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.Id}/knowledge-updates/{flow.UpdateId}/apply-column-known-value", new
        {
            columnId = 123, value, meaning = "复核后的业务含义", sortOrder = 90, knowledgeStatusChange = (object?)null,
            applier = Person("知识更新执行人"), concurrencyToken = flow.Token, targetConcurrencyToken = columnToken,
        });
        Assert.Equal(HttpStatusCode.OK, applyResponse.StatusCode);
        var applied = await applyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Applied", applied.GetProperty("knowledgeUpdate").GetProperty("status").GetString());
        var token = applied.GetProperty("concurrencyToken").GetString();

        using var duplicateApply = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.Id}/knowledge-updates/{flow.UpdateId}/apply-column-known-value", new
        {
            columnId = 123, value, meaning = "复核后的业务含义", sortOrder = 90, knowledgeStatusChange = (object?)null,
            applier = Person("知识更新执行人"), concurrencyToken = token,
            targetConcurrencyToken = applied.GetProperty("targetConcurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, duplicateApply.StatusCode);

        using var confirmResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.Id}/confirm-conclusion", new
        {
            confirmer = Person("结论确认人"), concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ConclusionConfirmed", confirmed.GetProperty("status").GetString());

        using var closeResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.Id}/close", new
        {
            closeNote = "知识更新已核对。", actor = Person("调查人"),
            concurrencyToken = confirmed.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, closeResponse.StatusCode);
        var closed = await closeResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Closed", closed.GetProperty("status").GetString());

        using var reopenResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.Id}/reopen", new
        {
            reason = "出现新的样本，需要继续调查。", actor = Person("调查人"),
            concurrencyToken = closed.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, reopenResponse.StatusCode);
        var reopened = await reopenResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(reopened.GetProperty("appliedKnowledgeUpdatesRetained").GetBoolean());
        Assert.Equal("Investigating", reopened.GetProperty("status").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal("复核后的业务含义", (await db.ColumnKnownValues.AsNoTracking().SingleAsync(item => item.DatabaseColumnId == 123 && item.ValueText == value)).Meaning);
        Assert.Equal(KnowledgeStatus.Inferred, (await db.DatabaseColumns.AsNoTracking().SingleAsync(item => item.Id == 123)).KnowledgeStatus);
        Assert.Equal(KnowledgeUpdateStatus.Applied, (await db.KnowledgeUpdates.AsNoTracking().SingleAsync(item => item.Id == flow.UpdateId)).Status);
        Assert.True(await db.Findings.AsNoTracking().AnyAsync(item => item.UnknownItemId == flow.Id));
        Assert.True(await db.Evidence.AsNoTracking().AnyAsync(item => item.SubjectId == flow.Id));
        Assert.NotNull(await db.Resolutions.AsNoTracking().SingleAsync(item => item.UnknownItemId == flow.Id));
        Assert.Equal(1, await db.UnknownItemActivities.CountAsync(item => item.UnknownItemId == flow.Id && item.ActivityType == UnknownItemActivityType.KnowledgeUpdateApplied));
        Assert.Equal(1, await db.UnknownItemActivities.CountAsync(item => item.UnknownItemId == flow.Id && item.ActivityType == UnknownItemActivityType.Reopened));
    }

    [Fact]
    public async Task Failed_apply_rolls_back_target_update_status_and_activity()
    {
        var value = "VS09B-" + Guid.NewGuid().ToString("N")[..8];
        var flow = await PrepareColumnKnownValueFlow(value, "Preview 含义");
        using var response = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.Id}/knowledge-updates/{flow.UpdateId}/apply-column-known-value", new
        {
            columnId = 123, value, meaning = "与 Preview 不一致", sortOrder = 91, knowledgeStatusChange = (object?)null,
            applier = Person("知识更新执行人"), concurrencyToken = flow.Token, targetConcurrencyToken = await GetColumnToken(),
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await db.ColumnKnownValues.AsNoTracking().AnyAsync(item => item.DatabaseColumnId == 123 && item.ValueText == value));
        var update = await db.KnowledgeUpdates.AsNoTracking().SingleAsync(item => item.Id == flow.UpdateId);
        Assert.Equal(KnowledgeUpdateStatus.Proposed, update.Status);
        Assert.Null(update.AppliedAt);
        Assert.False(await db.UnknownItemActivities.AsNoTracking().AnyAsync(item => item.UnknownItemId == flow.Id && item.ActivityType == UnknownItemActivityType.KnowledgeUpdateApplied));
    }

    [Fact]
    public async Task Confirm_and_close_reject_illegal_order_without_changing_workflow_state()
    {
        var flow = await PrepareColumnKnownValueFlow("VS09B-" + Guid.NewGuid().ToString("N")[..8], "尚未应用");
        using var confirm = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.Id}/confirm-conclusion", new
        {
            confirmer = Person("结论确认人"), concurrencyToken = flow.Token,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, confirm.StatusCode);

        using var close = await _client.PostAsJsonAsync($"/api/unknown-items/{flow.Id}/close", new
        {
            closeNote = "错误顺序", actor = Person("调查人"), concurrencyToken = flow.Token,
        });
        Assert.Equal(HttpStatusCode.Conflict, close.StatusCode);

        using var detailResponse = await _client.GetAsync($"/api/unknown-items/{flow.Id}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Investigating", detail.GetProperty("question").GetProperty("status").GetString());
    }

    private async Task<(long Id, long UpdateId, string Token)> PrepareColumnKnownValueFlow(string value, string meaning)
    {
        using var createResponse = await _client.PostAsJsonAsync("/api/unknown-items", new
        {
            systemId = 12, question = $"字段值 {value} 表示什么？", context = "VS-09B 原子闭环验证", priority = "High",
            primaryTarget = new { type = "DatabaseColumn", id = 123 }, relatedTargets = Array.Empty<object>(), creator = Person("创建人"),
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt64();
        using var startResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/start-investigation", new
        {
            actor = Person("调查人"), concurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        var started = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = started.GetProperty("concurrencyToken").GetString();
        using var findingResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/findings", new
        {
            content = "代码与数据库样本共同支持当前结论。", recorder = Person("调查人"), concurrencyToken = token,
        });
        var finding = await findingResponse.Content.ReadFromJsonAsync<JsonElement>();
        token = finding.GetProperty("concurrencyToken").GetString();
        using var evidenceResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/evidence", new
        {
            evidenceType = "DatabaseSample", subject = new { type = "UnknownItem", id }, subjectDetailKey = (string?)null,
            sourceTitle = "VS-09B SQLite Sample", sourceReference = "database://MES.TABLE_EQP", sourceLocator = (object?)null,
            summary = "样本支持字段值含义", supportReason = "用于确认当前调查结论", confidence = "High",
            provider = Person("证据提供人"), concurrencyToken = token,
        });
        var evidence = await evidenceResponse.Content.ReadFromJsonAsync<JsonElement>();
        token = evidence.GetProperty("concurrencyToken").GetString();
        using var resolutionResponse = await _client.PutAsJsonAsync($"/api/unknown-items/{id}/resolution", new
        {
            conclusion = $"{value} 表示 {meaning}。",
            knowledgeUpdates = new[] { new {
                id = (long?)null, target = new { type = "DatabaseColumn", id = 123L }, subjectDetailKey = $"KnownValues:{value}",
                applyAction = "AddColumnKnownValue", changeSummary = $"新增 {value} 的业务含义", before = (object?)null,
                after = new { value, meaning }, knowledgeStatusBefore = (string?)null, knowledgeStatusAfter = (string?)null,
            } },
            actor = Person("调查人"), concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.OK, resolutionResponse.StatusCode);
        var resolution = await resolutionResponse.Content.ReadFromJsonAsync<JsonElement>();
        return (id, resolution.GetProperty("knowledgeUpdates")[0].GetProperty("id").GetInt64(), resolution.GetProperty("concurrencyToken").GetString()!);
    }

    private async Task<string> GetColumnToken()
    {
        using var response = await _client.GetAsync("/api/database-columns/123");
        var column = await response.Content.ReadFromJsonAsync<JsonElement>();
        return column.GetProperty("concurrencyToken").GetString()!;
    }

    private static object Person(string role) => new
    {
        displayName = "王敏", roleOrIdentity = role, occurredAt = DateTimeOffset.UtcNow,
        team = "制造系统组", externalUserKey = (string?)null, source = "Manual", note = (string?)null,
    };
}
