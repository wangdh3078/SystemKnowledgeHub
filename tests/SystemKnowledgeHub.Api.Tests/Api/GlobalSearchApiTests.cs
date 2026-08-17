using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class GlobalSearchApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public GlobalSearchApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Search_returns_grouped_results_across_existing_knowledge_objects()
    {
        using var response = await _client.GetAsync("/api/search?q=%E8%AE%BE%E5%A4%87&limitPerGroup=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("设备", payload.GetProperty("query").GetString());

        var groups = payload.GetProperty("groups").EnumerateArray().ToArray();
        Assert.Contains(groups, group => group.GetProperty("objectType").GetString() == "System");
        Assert.Contains(groups, group => group.GetProperty("objectType").GetString() == "BusinessFunction");
        Assert.Contains(groups, group => group.GetProperty("objectType").GetString() == "DatabaseObject");
        Assert.Contains(groups, group => group.GetProperty("objectType").GetString() == "DatabaseColumn");

        var systemItem = groups
            .Single(group => group.GetProperty("objectType").GetString() == "System")
            .GetProperty("items")
            .EnumerateArray()
            .Single(item => item.GetProperty("title").GetString() == "MES");
        Assert.Equal("MES", systemItem.GetProperty("systemContext").GetString());
        Assert.Equal("Inferred", systemItem.GetProperty("knowledgeStatus").GetString());
        Assert.Equal(JsonValueKind.Null, systemItem.GetProperty("unknownItemStatus").ValueKind);
    }

    [Fact]
    public async Task Search_finds_technical_column_identifier_with_type_filter_limit_and_drawer_navigation()
    {
        using var response = await _client.GetAsync(
            "/api/search?q=STATE_FLAG&types=DatabaseColumn&limitPerGroup=1");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var group = Assert.Single(payload.GetProperty("groups").EnumerateArray());
        Assert.Equal("DatabaseColumn", group.GetProperty("objectType").GetString());
        var item = Assert.Single(group.GetProperty("items").EnumerateArray());
        Assert.Equal("MES.TABLE_EQP.STATE_FLAG", item.GetProperty("title").GetString());
        Assert.Equal("MES", item.GetProperty("systemContext").GetString());
        Assert.Equal("Inferred", item.GetProperty("knowledgeStatus").GetString());

        var navigation = item.GetProperty("navigation");
        Assert.Equal("DatabaseObject", navigation.GetProperty("routeObjectType").GetString());
        Assert.Equal(45, navigation.GetProperty("routeObjectId").GetInt64());
        Assert.Equal("DatabaseColumn", navigation.GetProperty("openDrawer").GetString());
        Assert.Equal(123, navigation.GetProperty("drawerObjectId").GetInt64());
    }

    [Fact]
    public async Task Search_keeps_unknown_item_status_separate_from_knowledge_status()
    {
        const string question = "全局搜索状态隔离测试事项";
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            dbContext.UnknownItems.Add(new UnknownItem
            {
                ItemCode = "UNK-SEARCH-STATUS",
                SystemId = 12,
                Question = question,
                Context = "用于验证搜索结果中的待确认事项状态。",
                Priority = UnknownItemPriority.Medium,
                Status = UnknownItemStatus.Investigating,
                InvestigationStartedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedByName = "测试人员",
                CreatedByRole = "知识整理人员",
                UpdatedAt = DateTimeOffset.UtcNow,
                Version = 1,
            });
            await dbContext.SaveChangesAsync();
        }

        using var response = await _client.GetAsync(
            "/api/search?q=%E7%8A%B6%E6%80%81%E9%9A%94%E7%A6%BB%E6%B5%8B%E8%AF%95&types=UnknownItem&limitPerGroup=5");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var group = Assert.Single(payload.GetProperty("groups").EnumerateArray());
        var item = Assert.Single(group.GetProperty("items").EnumerateArray());
        Assert.Equal(question, item.GetProperty("title").GetString());
        Assert.Equal(JsonValueKind.Null, item.GetProperty("knowledgeStatus").ValueKind);
        Assert.Equal("Investigating", item.GetProperty("unknownItemStatus").GetString());
    }
}
