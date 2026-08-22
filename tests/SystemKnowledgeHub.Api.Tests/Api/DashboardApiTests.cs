using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class DashboardApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public DashboardApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task GetDashboard_returns_real_knowledge_summary_without_mixing_unknown_item_status()
    {
        using var response = await _client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var overview = payload.GetProperty("knowledgeOverview");
        Assert.Equal(6, overview.GetProperty("systems").GetInt32());
        Assert.Equal(6, overview.GetProperty("businessFunctions").GetInt32());
        Assert.Equal(1, overview.GetProperty("databaseObjects").GetInt32());
        Assert.Equal(8, overview.GetProperty("columns").GetInt32());

        var progress = payload.GetProperty("knowledgeProgress");
        Assert.Equal(8, progress.GetProperty("confirmed").GetInt32());
        Assert.Equal(10, progress.GetProperty("inferred").GetInt32());
        Assert.Equal(3, progress.GetProperty("unknown").GetInt32());
    }

    [Fact]
    public async Task GetDashboard_limits_and_orders_recent_activity_and_only_counts_open_high_priority_items()
    {
        var baseTime = new DateTimeOffset(2030, 1, 1, 9, 0, 0, TimeSpan.Zero);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            for (var index = 1; index <= 5; index++)
            {
                dbContext.UnknownItems.Add(new UnknownItem
                {
                    ItemCode = $"UNK-DASHBOARD-{index}",
                    SystemId = 12,
                    Question = $"Dashboard 最近整理测试 {index}",
                    Priority = UnknownItemPriority.High,
                    Status = index == 5 ? UnknownItemStatus.Closed : UnknownItemStatus.Investigating,
                    InvestigationStartedAt = baseTime.AddMinutes(index),
                    ClosedAt = index == 5 ? baseTime.AddMinutes(index) : null,
                    CreatedAt = baseTime.AddMinutes(index),
                    CreatedByName = "测试人员",
                    CreatedByRole = "知识整理人员",
                    UpdatedAt = baseTime.AddMinutes(index),
                    Version = 1,
                });
            }
            await dbContext.SaveChangesAsync();
        }

        using var response = await _client.GetAsync("/api/dashboard");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var progress = payload.GetProperty("knowledgeProgress");
        Assert.Equal(3, progress.GetProperty("unknown").GetInt32());
        Assert.Equal(4, progress.GetProperty("openUnknownItems").GetInt32());

        var attention = payload.GetProperty("needsAttention").EnumerateArray()
            .Single(item => item.GetProperty("kind").GetString() == "HighPriorityUnknownItem");
        Assert.Equal(4, attention.GetProperty("count").GetInt32());

        var recent = payload.GetProperty("recentActivity").EnumerateArray().ToArray();
        Assert.Equal(4, recent.Length);
        Assert.All(recent, item => Assert.Equal("UnknownItem", item.GetProperty("objectType").GetString()));
        Assert.Equal("Dashboard 最近整理测试 5", recent[0].GetProperty("title").GetString());
        Assert.Equal("Dashboard 最近整理测试 2", recent[3].GetProperty("title").GetString());
    }
}
