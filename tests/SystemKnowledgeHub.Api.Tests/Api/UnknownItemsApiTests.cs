using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class UnknownItemsApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public UnknownItemsApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Create_list_and_detail_persist_primary_target_and_created_activity_atomically()
    {
        var created = await Create($"VS09A 创建闭环 {Guid.NewGuid():N}");
        Assert.Equal("Open", created.GetProperty("status").GetString());
        var id = created.GetProperty("id").GetInt64();

        using var listResponse = await _client.GetAsync($"/api/unknown-items?systemId=12&keyword=VS09A&page=1&pageSize=20&sort=updatedAt:desc");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(list.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt64() == id);

        using var detailResponse = await _client.GetAsync($"/api/unknown-items/{id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("BusinessFunction", detail.GetProperty("relatedObjects")[0].GetProperty("target").GetProperty("type").GetString());
        Assert.Equal("Created", detail.GetProperty("activity")[0].GetProperty("type").GetString());
        Assert.Equal("StartInvestigation", detail.GetProperty("availableActions")[0].GetString());
    }

    [Fact]
    public async Task Start_and_finding_enforce_state_and_concurrency_while_recording_business_snapshots()
    {
        var created = await Create($"VS09A 调查状态 {Guid.NewGuid():N}");
        var id = created.GetProperty("id").GetInt64();
        var openToken = created.GetProperty("concurrencyToken").GetString();
        using var startedResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/start-investigation", new { actor = Person("调查人"), concurrencyToken = openToken });
        Assert.Equal(HttpStatusCode.OK, startedResponse.StatusCode);
        var started = await startedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Investigating", started.GetProperty("status").GetString());

        using var duplicate = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/start-investigation", new { actor = Person("调查人"), concurrencyToken = started.GetProperty("concurrencyToken").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        using var findingResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/findings", new
        {
            content = "代码中将状态值 30 与 Offline 分支一起处理。",
            recorder = Person("调查人"),
            concurrencyToken = started.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Created, findingResponse.StatusCode);
        var finding = await findingResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("王敏", finding.GetProperty("finding").GetProperty("recordedBy").GetProperty("displayName").GetString());
        Assert.NotEqual(started.GetProperty("concurrencyToken").GetString(), finding.GetProperty("concurrencyToken").GetString());
    }

    [Fact]
    public async Task Investigation_evidence_and_resolution_remain_scoped_and_do_not_change_target_knowledge()
    {
        var created = await Create($"VS09A 证据结论 {Guid.NewGuid():N}");
        var id = created.GetProperty("id").GetInt64();
        var token = created.GetProperty("concurrencyToken").GetString();
        using var startResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/start-investigation", new { actor = Person("调查人"), concurrencyToken = token });
        var started = await startResponse.Content.ReadFromJsonAsync<JsonElement>();
        token = started.GetProperty("concurrencyToken").GetString();

        using var foreignSubject = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/evidence", EvidenceRequest(id + 999, token!));
        Assert.Equal(HttpStatusCode.UnprocessableEntity, foreignSubject.StatusCode);

        using var evidenceResponse = await _client.PostAsJsonAsync($"/api/unknown-items/{id}/evidence", EvidenceRequest(id, token!));
        Assert.Equal(HttpStatusCode.Created, evidenceResponse.StatusCode);
        var evidence = await evidenceResponse.Content.ReadFromJsonAsync<JsonElement>();
        token = evidence.GetProperty("concurrencyToken").GetString();

        using var resolutionResponse = await _client.PutAsJsonAsync($"/api/unknown-items/{id}/resolution", new
        {
            conclusion = "当前调查认为状态值 30 表示 Unknown / Offline。",
            knowledgeUpdates = Array.Empty<object>(),
            actor = Person("调查人"),
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.OK, resolutionResponse.StatusCode);
        var resolution = await resolutionResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Investigating", resolution.GetProperty("status").GetString());
        Assert.Equal(0, resolution.GetProperty("knowledgeUpdates").GetArrayLength());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var target = await db.BusinessFunctions.AsNoTracking().SingleAsync(item => item.Id == 77);
        var storedEvidence = await db.Evidence.AsNoTracking().SingleAsync(item => item.Id == evidence.GetProperty("evidence").GetProperty("id").GetInt64());
        Assert.Equal(KnowledgeStatus.Inferred, target.KnowledgeStatus);
        Assert.Equal(EvidenceSubjectType.UnknownItem, storedEvidence.SubjectType);
        Assert.Equal(2, await db.UnknownItemActivities.CountAsync(item => item.UnknownItemId == id && (item.ActivityType == UnknownItemActivityType.EvidenceAdded || item.ActivityType == UnknownItemActivityType.ResolutionRecorded)));
    }

    private async Task<JsonElement> Create(string question)
    {
        using var response = await _client.PostAsJsonAsync("/api/unknown-items", new
        {
            systemId = 12,
            question,
            context = "从 Business Function Detail 发现尚未确认的业务含义。",
            priority = "High",
            primaryTarget = new { type = "BusinessFunction", id = 77 },
            relatedTargets = Array.Empty<object>(),
            creator = Person("创建人"),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private static object EvidenceRequest(long subjectId, string token) => new
    {
        evidenceType = "CodeReference",
        subject = new { type = "UnknownItem", id = subjectId },
        subjectDetailKey = (string?)null,
        sourceTitle = "EquipmentStatusService.cs : line 184",
        sourceReference = "EquipmentStatusService.cs",
        sourceLocator = new { repository = "mes-legacy", file = "EquipmentStatusService.cs", startLine = 184 },
        summary = "状态分支证据",
        supportReason = "代码分支直接支持当前调查判断",
        confidence = "High",
        provider = Person("证据提供人"),
        concurrencyToken = token,
    };

    private static object Person(string role) => new
    {
        displayName = "王敏", roleOrIdentity = role, occurredAt = "2026-08-15T10:00:00Z",
        team = "制造系统组", externalUserKey = (string?)null, source = "Manual", note = (string?)null,
    };
}
