using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class DatabaseKnowledgeAuthoringApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DatabaseKnowledgeAuthoringApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Register_column_persists_unknown_and_rejects_duplicate_name()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var token = await GetObjectToken();
        var request = ColumnRequest($"VS12B_COLUMN_{suffix}", 900000, token);

        using var createdResponse = await _client.PostAsJsonAsync("/api/database-objects/45/columns", request);
        Assert.Equal(HttpStatusCode.Created, createdResponse.StatusCode);
        var created = await createdResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Unknown", created.GetProperty("column").GetProperty("knowledgeStatus").GetString());
        var parentToken = created.GetProperty("parentConcurrencyToken").GetString()!;

        using var duplicateResponse = await _client.PostAsJsonAsync(
            "/api/database-objects/45/columns",
            ColumnRequest($"VS12B_COLUMN_{suffix}", 900001, parentToken));
        Assert.Equal(HttpStatusCode.Conflict, duplicateResponse.StatusCode);
    }

    [Fact]
    public async Task Knowledge_updates_preserve_status_evidence_and_relationships()
    {
        await SeedColumnEvidenceAndRelationship();
        var objectToken = await GetObjectToken();
        using var objectResponse = await _client.PutAsJsonAsync("/api/database-objects/45/knowledge", new
        {
            businessDescription = "VS-12B 对象级业务说明",
            accessMode = "ReadWrite",
            businessKeyColumns = new[] { "STATE_FLAG" },
            actor = Actor(),
            concurrencyToken = objectToken,
        });
        Assert.Equal(HttpStatusCode.OK, objectResponse.StatusCode);
        var objectResult = await objectResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inferred", objectResult.GetProperty("knowledgeStatus").GetString());

        var columnToken = await GetColumnToken();
        using var columnResponse = await _client.PutAsJsonAsync("/api/database-columns/123/knowledge", new
        {
            businessDescription = "VS-12B 字段级业务说明",
            actor = Actor(),
            concurrencyToken = columnToken,
        });
        Assert.Equal(HttpStatusCode.OK, columnResponse.StatusCode);
        var columnResult = await columnResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inferred", columnResult.GetProperty("knowledgeStatus").GetString());

        using var detailResponse = await _client.GetAsync("/api/database-columns/123");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(detail.GetProperty("evidence").EnumerateArray(), item => item.GetProperty("sourceTitle").GetString() == "VS-12B evidence");
        Assert.Contains(detail.GetProperty("relations").EnumerateArray(), item => item.GetProperty("otherObject").GetProperty("id").GetInt64() == 77);
    }

    [Fact]
    public async Task Add_and_remove_unreferenced_known_value_keeps_knowledge_status()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        using var addedResponse = await _client.PostAsJsonAsync("/api/database-columns/123/known-values", new
        {
            value = $"FREE_{suffix}", meaning = "VS-12B 可移除值", sortOrder = 990000, actor = Actor(), concurrencyToken = await GetColumnToken(),
        });
        Assert.Equal(HttpStatusCode.Created, addedResponse.StatusCode);
        var added = await addedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Inferred", added.GetProperty("knowledgeStatus").GetString());

        using var removeResponse = await _client.PostAsJsonAsync($"/api/database-columns/123/known-values/{added.GetProperty("knownValue").GetProperty("id").GetInt64()}/remove", new
        {
            confirmed = true, actor = Actor(), concurrencyToken = added.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, removeResponse.StatusCode);
        Assert.DoesNotContain((await removeResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("knownValues").EnumerateArray(), item => item.GetProperty("value").GetString() == $"FREE_{suffix}");
    }

    [Fact]
    public async Task Remove_known_value_is_blocked_by_exact_evidence_and_open_investigation_reference()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var evidenceValue = $"EVIDENCE_{suffix}";
        var unknownValue = $"UNKNOWN_{suffix}";
        var evidenceAdded = await AddKnownValue(evidenceValue);
        await SeedEvidenceReference(evidenceValue);

        using var evidenceBlocked = await _client.PostAsJsonAsync($"/api/database-columns/123/known-values/{evidenceAdded.Id}/remove", new
        {
            confirmed = true, actor = Actor(), concurrencyToken = evidenceAdded.Token,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, evidenceBlocked.StatusCode);
        Assert.Equal("reference_invalid", (await evidenceBlocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        var unknownAdded = await AddKnownValue(unknownValue);
        await SeedOpenInvestigationReference(unknownValue);
        using var unknownBlocked = await _client.PostAsJsonAsync($"/api/database-columns/123/known-values/{unknownAdded.Id}/remove", new
        {
            confirmed = true, actor = Actor(), concurrencyToken = unknownAdded.Token,
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unknownBlocked.StatusCode);
    }

    private async Task<(long Id, string Token)> AddKnownValue(string value)
    {
        using var response = await _client.PostAsJsonAsync("/api/database-columns/123/known-values", new
        {
            value, meaning = "VS-12B 受控引用验证", sortOrder = 980000, actor = Actor(), concurrencyToken = await GetColumnToken(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (result.GetProperty("knownValue").GetProperty("id").GetInt64(), result.GetProperty("concurrencyToken").GetString()!);
    }

    private async Task<string> GetObjectToken()
    {
        using var response = await _client.GetAsync("/api/database-objects/45");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("concurrencyToken").GetString()!;
    }

    private async Task<string> GetColumnToken()
    {
        using var response = await _client.GetAsync("/api/database-columns/123");
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("concurrencyToken").GetString()!;
    }

    private async Task SeedColumnEvidenceAndRelationship()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.CodeReference, SubjectType = EvidenceSubjectType.DatabaseColumn, SubjectId = 123,
            SourceTitle = "VS-12B evidence", SourceReference = "VS-12B", SupportReason = "验证维护操作不会覆盖字段证据。",
            ProviderName = "测试人员", ProviderRole = "验证", ProvidedAt = now, CreatedAt = now, UpdatedAt = now,
        });
        db.KnowledgeRelations.Add(new KnowledgeRelation
        {
            SourceType = KnowledgeTargetType.BusinessFunction, SourceId = 77, TargetType = KnowledgeTargetType.DatabaseColumn, TargetId = 123,
            RelationType = RelationType.UsesField, Description = "VS-12B 关系", CreatedAt = now, UpdatedAt = now,
            CreatedByName = "测试人员", KnowledgeStatus = KnowledgeStatus.Inferred,
            KnowledgeStatusChangedAt = now, KnowledgeStatusChangedByName = "测试人员", KnowledgeStatusChangedByRole = "验证",
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedEvidenceReference(string value)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        db.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.DatabaseSample, SubjectType = EvidenceSubjectType.DatabaseColumn, SubjectId = 123,
            SubjectDetailKey = $"KnownValues:{value}", SourceTitle = "VS-12B known value evidence", SourceReference = "VS-12B",
            SupportReason = "验证精确证据引用保护。", ProviderName = "测试人员", ProviderRole = "验证", ProvidedAt = now, CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private async Task SeedOpenInvestigationReference(string value)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var item = new UnknownItem
        {
            ItemCode = $"UNK-VS12B-{Guid.NewGuid():N}", SystemId = 12, Question = "VS-12B 已知值调查", Context = "精确引用保护验证",
            Priority = UnknownItemPriority.Medium, Status = UnknownItemStatus.Investigating, InvestigationStartedAt = now,
            CreatedAt = now, CreatedByName = "测试人员", UpdatedAt = now,
        };
        db.UnknownItems.Add(item);
        await db.SaveChangesAsync();
        db.KnowledgeUpdates.Add(new KnowledgeUpdate
        {
            UnknownItemId = item.Id, TargetType = KnowledgeTargetType.DatabaseColumn, TargetId = 123,
            SubjectDetailKey = $"KnownValues:{value}", ChangeSummary = "VS-12B 精确字段值更新", BeforeJson = "null", AfterJson = "{}",
            Status = KnowledgeUpdateStatus.Proposed, CreatedAt = now, UpdatedAt = now,
        });
        await db.SaveChangesAsync();
    }

    private static object ColumnRequest(string columnName, int ordinalPosition, string token) => new
    {
        ordinalPosition, columnName, dataType = "VARCHAR2(40)", nullable = true, defaultValue = (string?)null,
        databaseComment = "VS-12B 测试字段", businessDescription = (string?)null, actor = Actor(), concurrencyToken = token,
    };

    private static object Actor() => new { displayName = "测试人员", role = "知识整理人员" };
}
