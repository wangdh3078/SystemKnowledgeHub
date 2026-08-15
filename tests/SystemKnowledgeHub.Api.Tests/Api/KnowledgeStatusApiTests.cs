using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class KnowledgeStatusApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public KnowledgeStatusApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Progression_requires_evidence_then_human_confirmation_and_never_advances_automatically()
    {
        var created = await CreateBusinessFunction();
        var id = created.GetProperty("id").GetInt64();
        var token = created.GetProperty("concurrencyToken").GetString()!;

        using var missingEvidence = await Change(id, "Inferred", token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingEvidence.StatusCode);
        Assert.Equal("business_rule_violation", (await missingEvidence.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var evidenceResponse = await _client.PostAsJsonAsync("/api/evidence", OrdinaryEvidence(id));
        Assert.Equal(HttpStatusCode.Created, evidenceResponse.StatusCode);
        var evidence = await evidenceResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unknown", evidence.GetProperty("subjectKnowledgeStatus").GetString());
        Assert.False(evidence.GetProperty("knowledgeStatusChanged").GetBoolean());

        using var inferResponse = await Change(id, "Inferred", token);
        Assert.Equal(HttpStatusCode.OK, inferResponse.StatusCode);
        var inferred = await inferResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unknown", inferred.GetProperty("previousStatus").GetString());
        Assert.Equal("Inferred", inferred.GetProperty("knowledgeStatus").GetString());
        token = inferred.GetProperty("concurrencyToken").GetString()!;

        using var missingConfirmation = await Change(id, "Confirmed", token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingConfirmation.StatusCode);

        using var confirmationResponse = await _client.PostAsJsonAsync(
            "/api/evidence/human-confirmations",
            HumanConfirmation(id));
        Assert.Equal(HttpStatusCode.Created, confirmationResponse.StatusCode);
        var confirmation = await confirmationResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inferred", confirmation.GetProperty("subjectKnowledgeStatus").GetString());
        Assert.False(confirmation.GetProperty("knowledgeStatusChanged").GetBoolean());

        using var confirmResponse = await Change(id, "Confirmed", token);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inferred", confirmed.GetProperty("previousStatus").GetString());
        Assert.Equal("Confirmed", confirmed.GetProperty("knowledgeStatus").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.BusinessFunctions.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(KnowledgeStatus.Confirmed, stored.KnowledgeStatus);
        Assert.Equal("王敏", stored.KnowledgeStatusChangedByName);
        Assert.Equal("知识整理人员", stored.KnowledgeStatusChangedByRole);
    }

    [Fact]
    public async Task Progression_rejects_direct_confirmation_stale_tokens_and_reasonless_rollback()
    {
        var directCreated = await CreateBusinessFunction();
        var directId = directCreated.GetProperty("id").GetInt64();
        var directToken = directCreated.GetProperty("concurrencyToken").GetString()!;
        using var directConfirmation = await Change(directId, "Confirmed", directToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, directConfirmation.StatusCode);

        var created = await CreateBusinessFunction();
        var id = created.GetProperty("id").GetInt64();
        var initialToken = created.GetProperty("concurrencyToken").GetString()!;
        using (var evidenceResponse = await _client.PostAsJsonAsync("/api/evidence", OrdinaryEvidence(id)))
        {
            Assert.Equal(HttpStatusCode.Created, evidenceResponse.StatusCode);
        }
        using var inferResponse = await Change(id, "Inferred", initialToken);
        Assert.Equal(HttpStatusCode.OK, inferResponse.StatusCode);
        var inferred = await inferResponse.Content.ReadFromJsonAsync<JsonElement>();
        var inferredToken = inferred.GetProperty("concurrencyToken").GetString()!;

        using var staleResponse = await Change(id, "Inferred", initialToken);
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using (var confirmationResponse = await _client.PostAsJsonAsync(
            "/api/evidence/human-confirmations",
            HumanConfirmation(id)))
        {
            Assert.Equal(HttpStatusCode.Created, confirmationResponse.StatusCode);
        }
        using var confirmResponse = await Change(id, "Confirmed", inferredToken);
        Assert.Equal(HttpStatusCode.OK, confirmResponse.StatusCode);
        var confirmed = await confirmResponse.Content.ReadFromJsonAsync<JsonElement>();
        var confirmedToken = confirmed.GetProperty("concurrencyToken").GetString()!;

        using var noReasonRollback = await Change(id, "Inferred", confirmedToken);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, noReasonRollback.StatusCode);

        using var rollback = await Change(id, "Inferred", confirmedToken, "业务语义发生变化，需要重新核对。");
        Assert.Equal(HttpStatusCode.OK, rollback.StatusCode);
        var rolledBack = await rollback.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("业务语义发生变化，需要重新核对。", rolledBack.GetProperty("reason").GetString());
        Assert.Equal("Inferred", rolledBack.GetProperty("knowledgeStatus").GetString());
    }

    private async Task<JsonElement> CreateBusinessFunction()
    {
        var request = new
        {
            systemId = 12,
            name = $"Status progression {Guid.NewGuid():N}",
            displayName = "知识状态验证功能",
            functionType = "Query",
            purpose = "验证显式知识状态推进",
            rewriteStatus = "Unknown",
            actor = new { displayName = "王敏", role = "知识整理人员" },
        };
        using var response = await _client.PostAsJsonAsync("/api/business-functions", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    private Task<HttpResponseMessage> Change(long id, string targetStatus, string token, string? reason = null)
    {
        return _client.PutAsJsonAsync("/api/knowledge-status", new
        {
            target = new { type = "BusinessFunction", id },
            targetStatus,
            reason,
            actor = Person(),
            concurrencyToken = token,
        });
    }

    private static object OrdinaryEvidence(long id) => new
    {
        evidenceType = "CodeReference",
        subject = new { type = "BusinessFunction", id },
        subjectDetailKey = (string?)null,
        sourceTitle = "StatusProgressionService.cs : line 42",
        sourceReference = "StatusProgressionService.cs",
        sourceLocator = new { repository = "mes-legacy", file = "StatusProgressionService.cs", startLine = 42 },
        summary = "状态含义判断分支",
        supportReason = "代码分支直接支持当前功能知识",
        confidence = "Medium",
        provider = Person(),
    };

    private static object HumanConfirmation(long id) => new
    {
        subject = new { type = "BusinessFunction", id },
        subjectDetailKey = (string?)null,
        confirmationStatement = "确认该业务功能的用途和处理结果与当前记录一致。",
        supportReason = "MES 业务专家确认当前生产语义",
        sourceNote = "VS-07 运行验证",
        confirmer = Person("李工", "MES 业务专家"),
    };

    private static object Person(string displayName = "王敏", string role = "知识整理人员") => new
    {
        displayName,
        roleOrIdentity = role,
        occurredAt = "2026-08-15T08:00:00Z",
        team = "制造系统组",
        externalUserKey = (string?)null,
        source = "Manual",
        note = (string?)null,
    };
}

