using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class SystemsApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SystemsApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateSystem_writes_sqlite_returns_unknown_and_is_immediately_listed()
    {
        var request = new
        {
            name = "QMS",
            displayName = "质量管理系统",
            systemType = "Quality Management System",
            lifecycle = "Running",
            purpose = "管理生产质量检验与异常追踪",
            actor = new { displayName = "王敏", role = "知识整理人员" },
        };

        using var createResponse = await _client.PostAsJsonAsync("/api/systems", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("QMS", created.GetProperty("name").GetString());
        Assert.Equal("Unknown", created.GetProperty("knowledgeStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("concurrencyToken").GetString()));

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.Systems.SingleAsync(system => system.Name == "QMS");
        Assert.Equal(KnowledgeStatus.Unknown, stored.KnowledgeStatus);
        Assert.Equal(1, stored.Version);

        using var listResponse = await _client.GetAsync("/api/systems?search=QMS&page=1&pageSize=20&sort=updatedAt:desc");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal("QMS", list.GetProperty("items")[0].GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetSystemsList_applies_lifecycle_technology_status_and_pagination()
    {
        using var response = await _client.GetAsync(
            "/api/systems?lifecycle=Legacy&technology=Oracle&knowledgeStatus=Inferred&page=1&pageSize=1&sort=name:asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, payload.GetProperty("page").GetInt32());
        Assert.Equal(1, payload.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, payload.GetProperty("total").GetInt32());
        var item = payload.GetProperty("items")[0];
        Assert.Equal("MES", item.GetProperty("name").GetString());
        Assert.Contains(
            item.GetProperty("technologies").EnumerateArray().Select(value => value.GetString()),
            value => value == "Oracle");
        Assert.Equal(1, item.GetProperty("databaseObjectCount").GetInt32());
    }

    [Fact]
    public async Task GetSystemDetail_returns_system_and_existing_database_object_summary()
    {
        using var response = await _client.GetAsync("/api/systems/12");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(12, payload.GetProperty("id").GetInt64());
        Assert.Equal("MES", payload.GetProperty("overview").GetProperty("name").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("concurrencyToken").GetString()));
        var businessFunctions = payload.GetProperty("businessFunctions").EnumerateArray().ToArray();
        Assert.Equal(5, businessFunctions.Length);
        Assert.Contains(
            businessFunctions,
            function => function.GetProperty("name").GetString() == "Equipment Status Query");
        Assert.Empty(payload.GetProperty("integrations").EnumerateArray());
        Assert.Empty(payload.GetProperty("unknownItems").EnumerateArray());

        var databaseObject = Assert.Single(payload.GetProperty("databaseObjects").EnumerateArray());
        Assert.Equal(45, databaseObject.GetProperty("id").GetInt64());
        Assert.Equal("MES.TABLE_EQP", databaseObject.GetProperty("qualifiedName").GetString());
        Assert.Equal("MES 生产库", payload
            .GetProperty("contextRail")
            .GetProperty("mainDatabase")
            .GetProperty("name")
            .GetString());
    }

    [Fact]
    public async Task UpdateSystemOverview_updates_sqlite_and_rejects_stale_token()
    {
        using var detailResponse = await _client.GetAsync("/api/systems/12");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        var staleToken = detail.GetProperty("concurrencyToken").GetString();

        var request = new
        {
            displayName = "制造执行系统（已更新）",
            systemType = "Manufacturing Execution System",
            purpose = "统一管理设备、作业与生产状态",
            mainUsers = new[] { "设备工程师", "生产调度员" },
            repository = new { name = "mes-legacy", url = "https://git.example/mes-legacy" },
            deployment = new[] { new { environment = "Production", description = "MES-APP-01" } },
            mainProjects = new[] { "MES.Web", "MES.Service" },
            mainEntryPoints = new[] { "Global.asax", "EquipmentStatusService.cs" },
            notes = "Overview inline edit verified.",
            actor = new { displayName = "王敏", role = "知识整理人员" },
            concurrencyToken = staleToken,
        };

        using var updateResponse = await _client.PutAsJsonAsync("/api/systems/12/overview", request);
        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("制造执行系统（已更新）", updated.GetProperty("overview").GetProperty("displayName").GetString());
        Assert.NotEqual(staleToken, updated.GetProperty("concurrencyToken").GetString());

        var staleRequest = new
        {
            displayName = "不应覆盖的新名称",
            systemType = request.systemType,
            purpose = request.purpose,
            mainUsers = request.mainUsers,
            repository = request.repository,
            deployment = request.deployment,
            mainProjects = request.mainProjects,
            mainEntryPoints = request.mainEntryPoints,
            notes = request.notes,
            actor = request.actor,
            concurrencyToken = staleToken,
        };
        using var conflictResponse = await _client.PutAsJsonAsync("/api/systems/12/overview", staleRequest);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.Systems.AsNoTracking().SingleAsync(system => system.Id == 12);
        Assert.Equal("制造执行系统（已更新）", stored.DisplayName);
        Assert.Equal("统一管理设备、作业与生产状态", stored.Purpose);
        Assert.Equal("MES", stored.Name);
        Assert.Equal(KnowledgeStatus.Inferred, stored.KnowledgeStatus);
        Assert.True(stored.Version > 1);
    }

    [Fact]
    public async Task UpdateSystemTechnology_replaces_tags_without_changing_lifecycle_or_knowledge_status()
    {
        var created = await CreateSystemForSectionUpdate("TECH", "Running");
        var systemId = created.GetProperty("id").GetInt64();
        var originalToken = created.GetProperty("concurrencyToken").GetString();
        var request = new
        {
            technologies = new[] { ".NET Framework 4.8", "Oracle", "RabbitMQ" },
            actor = new { displayName = "王敏", role = "知识整理人员" },
            concurrencyToken = originalToken,
        };

        using var updateResponse = await _client.PutAsJsonAsync($"/api/systems/{systemId}/technology", request);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updated = await updateResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.NotEqual(originalToken, updated.GetProperty("concurrencyToken").GetString());
        Assert.Equal(
            new[] { ".NET Framework 4.8", "Oracle", "RabbitMQ" },
            updated.GetProperty("technologies").EnumerateArray().Select(value => value.GetString()));

        using var detailResponse = await _client.GetAsync($"/api/systems/{systemId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Running", detail.GetProperty("overview").GetProperty("lifecycle").GetString());
        Assert.Equal("Unknown", detail.GetProperty("overview").GetProperty("knowledgeStatus").GetString());
        Assert.Equal(
            new[] { ".NET Framework 4.8", "Oracle", "RabbitMQ" },
            detail.GetProperty("overview").GetProperty("technologies").EnumerateArray().Select(value => value.GetString()));
    }

    [Fact]
    public async Task UpdateSystemLifecycle_preserves_technology_and_knowledge_status()
    {
        var created = await CreateSystemForSectionUpdate("LIFECYCLE", "Legacy");
        var systemId = created.GetProperty("id").GetInt64();
        var technologies = new
        {
            technologies = new[] { "Oracle" },
            actor = new { displayName = "王敏", role = "知识整理人员" },
            concurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        };
        using var technologyResponse = await _client.PutAsJsonAsync($"/api/systems/{systemId}/technology", technologies);
        var technologyUpdated = await technologyResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, technologyResponse.StatusCode);

        var lifecycleRequest = new
        {
            targetLifecycle = "Maintaining",
            actor = new { displayName = "王敏", role = "系统负责人" },
            concurrencyToken = technologyUpdated.GetProperty("concurrencyToken").GetString(),
        };
        using var lifecycleResponse = await _client.PutAsJsonAsync($"/api/systems/{systemId}/lifecycle", lifecycleRequest);

        Assert.Equal(HttpStatusCode.OK, lifecycleResponse.StatusCode);
        var updated = await lifecycleResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Maintaining", updated.GetProperty("lifecycle").GetString());
        Assert.Equal("Unknown", updated.GetProperty("knowledgeStatus").GetString());

        using var detailResponse = await _client.GetAsync($"/api/systems/{systemId}");
        var detail = await detailResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Maintaining", detail.GetProperty("overview").GetProperty("lifecycle").GetString());
        Assert.Equal("Unknown", detail.GetProperty("overview").GetProperty("knowledgeStatus").GetString());
        Assert.Equal(
            new[] { "Oracle" },
            detail.GetProperty("overview").GetProperty("technologies").EnumerateArray().Select(value => value.GetString()));
    }

    private async Task<JsonElement> CreateSystemForSectionUpdate(string prefix, string lifecycle)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
        var request = new
        {
            name = $"{prefix}_{suffix}",
            displayName = "系统编辑验证",
            systemType = "Verification System",
            lifecycle,
            purpose = "验证系统技术与生命周期独立编辑",
            actor = new { displayName = "王敏", role = "知识整理人员" },
        };
        using var response = await _client.PostAsJsonAsync("/api/systems", request);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }
}
