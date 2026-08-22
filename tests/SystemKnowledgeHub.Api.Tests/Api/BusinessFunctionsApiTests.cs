using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class BusinessFunctionsApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public BusinessFunctionsApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task CreateBusinessFunction_writes_unknown_function_and_lists_it_in_system_context()
    {
        var request = new
        {
            systemId = 12,
            name = "Cycle Count Query",
            displayName = "盘点查询",
            functionType = "Query",
            purpose = "查询当前盘点任务",
            rewriteStatus = "Unknown",
            actor = new { displayName = "王敏", role = "知识整理人员" },
        };

        using var createResponse = await _client.PostAsJsonAsync("/api/business-functions", request);

        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("MES", created.GetProperty("system").GetProperty("name").GetString());
        Assert.Equal("Unknown", created.GetProperty("knowledgeStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("concurrencyToken").GetString()));

        var id = created.GetProperty("id").GetInt64();
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.BusinessFunctions.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(KnowledgeStatus.Unknown, stored.KnowledgeStatus);
        Assert.Equal(1, stored.Version);

        using var listResponse = await _client.GetAsync(
            "/api/business-functions?systemId=12&search=Cycle%20Count%20Query&page=1&pageSize=20&sort=updatedAt:desc");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal(id, list.GetProperty("items")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Overview_and_process_updates_increment_version_and_reject_stale_token()
    {
        var createRequest = new
        {
            systemId = 12,
            name = "VS05 Editable Function",
            displayName = (string?)null,
            functionType = "Query",
            purpose = (string?)null,
            rewriteStatus = "Unknown",
            actor = new { displayName = "王敏", role = "知识整理人员" },
        };
        using var createResponse = await _client.PostAsJsonAsync("/api/business-functions", createRequest);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var id = created.GetProperty("id").GetInt64();
        var staleToken = created.GetProperty("concurrencyToken").GetString();

        var overviewRequest = new
        {
            name = "VS05 Editable Function",
            displayName = "可编辑功能",
            functionType = "ServiceQuery",
            purpose = "验证概览内联编辑",
            caller = "System Detail",
            input = "systemId",
            output = "FunctionSummary",
            rewriteStatus = "Change",
            actor = new { displayName = "王敏", role = (string?)null },
            concurrencyToken = staleToken,
        };
        using var overviewResponse = await _client.PutAsJsonAsync(
            $"/api/business-functions/{id}/overview",
            overviewRequest);
        Assert.Equal(HttpStatusCode.OK, overviewResponse.StatusCode);
        var updatedOverview = await overviewResponse.Content.ReadFromJsonAsync<JsonElement>();
        var currentToken = updatedOverview.GetProperty("concurrencyToken").GetString();
        Assert.NotEqual(staleToken, currentToken);
        Assert.Equal("验证概览内联编辑", updatedOverview.GetProperty("overview").GetProperty("purpose").GetString());

        var process = new[]
        {
            new { order = 1, name = "接收请求", description = (string?)null },
            new { order = 2, name = "读取系统上下文", description = (string?)"使用 systemId" },
            new { order = 3, name = "返回结果", description = (string?)null },
        };
        using var staleResponse = await _client.PutAsJsonAsync(
            $"/api/business-functions/{id}/process-steps",
            new
            {
                steps = process,
                actor = new { displayName = "王敏", role = (string?)null },
                concurrencyToken = staleToken,
            });
        Assert.Equal(HttpStatusCode.Conflict, staleResponse.StatusCode);

        using var processResponse = await _client.PutAsJsonAsync(
            $"/api/business-functions/{id}/process-steps",
            new
            {
                steps = process,
                actor = new { displayName = "王敏", role = (string?)null },
                concurrencyToken = currentToken,
            });
        Assert.Equal(HttpStatusCode.OK, processResponse.StatusCode);
        var updatedProcess = await processResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(3, updatedProcess.GetProperty("steps").GetArrayLength());
        Assert.NotEqual(currentToken, updatedProcess.GetProperty("concurrencyToken").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await dbContext.BusinessFunctions
            .AsNoTracking()
            .Include(item => item.ProcessSteps)
            .SingleAsync(item => item.Id == id);
        Assert.Equal("可编辑功能", stored.DisplayName);
        Assert.Equal("验证概览内联编辑", stored.Purpose);
        Assert.Equal(3, stored.Version);
        Assert.Equal(
            ["接收请求", "读取系统上下文", "返回结果"],
            stored.ProcessSteps.OrderBy(step => step.StepOrder).Select(step => step.Name).ToArray());
    }

    [Fact]
    public async Task GetBusinessFunctionsList_filters_by_system_context_and_status()
    {
        using var response = await _client.GetAsync(
            "/api/business-functions?systemId=12&search=Equipment&functionType=Query&rewriteStatus=Keep&knowledgeStatus=Inferred&page=1&pageSize=20&sort=updatedAt:desc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, payload.GetProperty("total").GetInt32());
        var item = Assert.Single(payload.GetProperty("items").EnumerateArray());
        Assert.Equal(77, item.GetProperty("id").GetInt64());
        Assert.Equal("Equipment Status Query", item.GetProperty("name").GetString());
        Assert.Equal(12, item.GetProperty("system").GetProperty("id").GetInt64());
        Assert.Equal("MES", item.GetProperty("system").GetProperty("name").GetString());
        Assert.Equal("Inferred", item.GetProperty("knowledgeStatus").GetString());
        Assert.Equal(0, item.GetProperty("relatedDataCount").GetInt32());
    }

    [Fact]
    public async Task GetBusinessFunctionDetail_returns_overview_and_ordered_process_projection()
    {
        using var response = await _client.GetAsync("/api/business-functions/77");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Equipment Status Query", payload.GetProperty("header").GetProperty("name").GetString());
        Assert.Equal("EQP_ID", payload.GetProperty("overview").GetProperty("input").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("concurrencyToken").GetString()));

        var steps = payload.GetProperty("businessProcess").EnumerateArray().ToArray();
        Assert.Equal(6, steps.Length);
        Assert.Equal(1, steps[0].GetProperty("order").GetInt32());
        Assert.Equal("接收请求", steps[0].GetProperty("name").GetString());
        Assert.Equal(6, steps[^1].GetProperty("order").GetInt32());
        Assert.Equal("返回结果", steps[^1].GetProperty("name").GetString());

        Assert.Empty(payload.GetProperty("relatedData").EnumerateArray());
        Assert.Empty(payload.GetProperty("businessRules").EnumerateArray());
        Assert.Empty(payload.GetProperty("integrations").EnumerateArray());
        Assert.Empty(payload.GetProperty("evidence").EnumerateArray());
        Assert.Empty(payload.GetProperty("unknownItems").EnumerateArray());
        Assert.Contains(
            payload.GetProperty("contextRail").GetProperty("callers").EnumerateArray(),
            caller => caller.GetString() == "MES 设备监控页、生产看板");
    }
}
