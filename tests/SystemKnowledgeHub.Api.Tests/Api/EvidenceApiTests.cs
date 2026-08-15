using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class EvidenceApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public EvidenceApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task AddEvidence_persists_complete_provider_and_does_not_change_subject_status()
    {
        using var response = await _client.PostAsJsonAsync("/api/evidence", CreateCodeEvidenceRequest());

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("CodeReference", created.GetProperty("evidenceType").GetString());
        Assert.Equal("Inferred", created.GetProperty("subjectKnowledgeStatus").GetString());
        Assert.False(created.GetProperty("knowledgeStatusChanged").GetBoolean());
        Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("concurrencyToken").GetString()));

        var id = created.GetProperty("id").GetInt64();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.Evidence.AsNoTracking().SingleAsync(item => item.Id == id);
        var subject = await dbContext.BusinessFunctions.AsNoTracking().SingleAsync(item => item.Id == 77);
        Assert.Equal("制造系统组", stored.ProviderTeam);
        Assert.Equal("Manual", stored.ProviderSource);
        Assert.Equal(KnowledgeStatus.Inferred, subject.KnowledgeStatus);

        using var detailResponse = await _client.GetAsync($"/api/evidence/{id}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MES · Equipment Status Query", detail.GetProperty("subjectContext").GetProperty("title").GetString());
        Assert.Equal("EquipmentStatusService.cs", detail.GetProperty("sourceLocator").GetProperty("file").GetString());
    }

    [Fact]
    public async Task UpdateEvidence_changes_only_correctable_fields_and_rejects_stale_token()
    {
        using var createResponse = await _client.PostAsJsonAsync("/api/evidence", CreateCodeEvidenceRequest());
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt64();
        var staleToken = created.GetProperty("concurrencyToken").GetString();

        var updateRequest = new
        {
            sourceTitle = "EquipmentStatusService.cs : line 184-190",
            sourceReference = "EquipmentStatusService.cs",
            sourceLocator = new { repository = "mes-legacy", file = "EquipmentStatusService.cs", startLine = 185, endLine = 190 },
            summary = "状态分支经过人工核对",
            supportReason = "代码分支直接支持该功能的状态计算",
            confidence = "High",
            provider = Person("Manual correction", "修正行号"),
            actor = new { displayName = "王敏", role = "知识整理人员" },
            concurrencyToken = staleToken,
        };
        using var updateResponse = await _client.PutAsJsonAsync($"/api/evidence/{id}", updateRequest);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("EquipmentStatusService.cs : line 184-190", updated.GetProperty("sourceTitle").GetString());
        Assert.Equal(185, updated.GetProperty("sourceLocator").GetProperty("startLine").GetInt32());
        Assert.Equal("BusinessFunction", updated.GetProperty("subject").GetProperty("type").GetString());
        Assert.Equal(77, updated.GetProperty("subject").GetProperty("id").GetInt64());
        Assert.NotEqual(staleToken, updated.GetProperty("concurrencyToken").GetString());

        using var staleResponse = await _client.PutAsJsonAsync($"/api/evidence/{id}", updateRequest);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);
    }

    [Fact]
    public async Task AddHumanConfirmation_persists_confirmer_snapshot_without_confirming_subject()
    {
        var request = new
        {
            subject = new { type = "BusinessFunction", id = 77 },
            subjectDetailKey = "Purpose",
            confirmationStatement = "确认该功能用于查询设备状态并计算展示状态。",
            supportReason = "MES 业务专家确认当前生产语义",
            sourceNote = "现场评审会议",
            confirmer = Person("Human confirmation", null, "李工", "MES 业务专家"),
        };

        using var response = await _client.PostAsJsonAsync("/api/evidence/human-confirmations", request);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var created = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("HumanConfirmation", created.GetProperty("evidenceType").GetString());
        Assert.Equal("Inferred", created.GetProperty("subjectKnowledgeStatus").GetString());
        Assert.False(created.GetProperty("knowledgeStatusChanged").GetBoolean());

        var id = created.GetProperty("id").GetInt64();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.Evidence.AsNoTracking().SingleAsync(item => item.Id == id);
        var subject = await dbContext.BusinessFunctions.AsNoTracking().SingleAsync(item => item.Id == 77);
        Assert.Equal(EvidenceType.HumanConfirmation, stored.EvidenceType);
        Assert.Equal("李工", stored.ProviderName);
        Assert.Equal("MES 业务专家", stored.ProviderRole);
        Assert.Equal("MES 运维组", stored.ProviderTeam);
        Assert.Equal(KnowledgeStatus.Inferred, subject.KnowledgeStatus);
    }

    private static object CreateCodeEvidenceRequest()
    {
        return new
        {
            evidenceType = "CodeReference",
            subject = new { type = "BusinessFunction", id = 77 },
            subjectDetailKey = "Purpose",
            sourceTitle = "EquipmentStatusService.cs : line 184",
            sourceReference = "EquipmentStatusService.cs",
            sourceLocator = new { repository = "mes-legacy", file = "EquipmentStatusService.cs", startLine = 184, endLine = 190 },
            summary = "状态查询分支",
            supportReason = "代码分支直接支持该功能用途",
            confidence = "High",
            provider = Person("Manual", null),
        };
    }

    private static object Person(
        string source,
        string? note,
        string displayName = "王敏",
        string role = "证据提供人")
    {
        return new
        {
            displayName,
            roleOrIdentity = role,
            occurredAt = "2026-08-15T02:00:00Z",
            team = role == "MES 业务专家" ? "MES 运维组" : "制造系统组",
            externalUserKey = (string?)null,
            source,
            note,
        };
    }
}
