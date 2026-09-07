using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class SearchHardeningApiTests
{
    [Theory]
    [InlineData("STATE_FLAG", "STATEXFLAG", "STATEAFLAG")]
    [InlineData("50%", "500", "50ABC")]
    [InlineData("A_B%C", "AXBZZC", "A_BZZC")]
    [InlineData("A\\B", "AB", "AXXB")]
    [InlineData("LOT", "BOX", "BATCH")]
    [InlineData("设备", "产品", "订单")]
    public async Task User_keywords_are_literal_substrings(string keyword, string decoy1, string decoy2)
    {
        using var factory = new CaptureFactory();
        using var client = factory.CreateAuthenticatedClient();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var names = new[] { keyword, "prefix" + keyword + "suffix", decoy1, decoy2 };
        var systems = names.Select(System).ToArray();
        db.Systems.AddRange(systems);
        await db.SaveChangesAsync();
        using var response = await client.GetAsync($"/api/search?q={Uri.EscapeDataString(keyword)}&types=System&limitPerGroup=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = payload.GetProperty("groups")[0].GetProperty("items").EnumerateArray()
            .Select(item => item.GetProperty("id").GetInt64()).ToArray();
        Assert.Contains(systems[0].Id, ids);
        Assert.Contains(systems[1].Id, ids);
        Assert.DoesNotContain(systems[2].Id, ids);
        Assert.DoesNotContain(systems[3].Id, ids);
        // The same helper is used by the ordinary System list query.
        using var list = await client.GetAsync($"/api/systems?search={Uri.EscapeDataString(keyword)}&page=1&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listIds = (await list.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")
            .EnumerateArray().Select(item => item.GetProperty("id").GetInt64()).ToArray();
        Assert.Contains(systems[0].Id, listIds);
        Assert.DoesNotContain(systems[2].Id, listIds);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(5)]
    [InlineData(20)]
    public async Task Large_fixture_keeps_true_total_and_bounds_every_structured_result_in_SQL(int limit)
    {
        using var factory = new CaptureFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        const string keyword = "R02_MARKER";
        db.Systems.AddRange(Enumerable.Range(0, 520).Select(i => System(keyword + i.ToString("D4"))));
        var removed = System(keyword + "deleted");
        removed.IsDeleted = true;
        removed.DeletedAt = DateTimeOffset.UtcNow;
        removed.DeletedByUserId = await db.Users.Select(u => u.Id).FirstAsync();
        removed.DeletedByDisplayName = "Test administrator";
        db.Systems.Add(removed);
        (await db.BusinessFunctions.FirstAsync()).Purpose = keyword;
        (await db.DatabaseObjects.FirstAsync()).BusinessDescription = keyword;
        (await db.DatabaseColumns.FirstAsync()).BusinessDescription = keyword;
        db.BusinessRules.Add(new() { SystemId = 12, Name = keyword, Description = keyword });
        db.Integrations.Add(new() { Name = keyword, SourceSystemId = 12, SourcePartyName = "MES", TargetPartyName = "External", Purpose = keyword });
        db.UnknownItems.Add(new() { SystemId = 12, ItemCode = "R02-BOUND", Question = keyword });
        await db.SaveChangesAsync();
        factory.Capture.Commands.Clear();
        await using var observed = new KnowledgeHubDbContext(new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseSqlite(db.Database.GetDbConnection()).AddInterceptors(factory.Capture).Options);
        var result = await new SearchQueries(observed).SearchKnowledge(new(keyword,
            "System,BusinessFunction,DatabaseObject,DatabaseColumn,BusinessRule,Integration,UnknownItem", limit), CancellationToken.None);
        Assert.Null(result.FieldErrors);
        Assert.Equal(526, result.Response!.Total);
        Assert.Equal(7, result.Response.Groups.Count);
        Assert.Equal(limit, result.Response.Groups.Single(g => g.ObjectType == "System").Items.Count);
        Assert.All(result.Response.Groups, group => Assert.InRange(group.Items.Count, 1, limit));
        var commands = factory.Capture.Commands.ToArray();
        Assert.Equal(14, commands.Length);
        Assert.Equal(7, commands.Count(sql => sql.Contains("COUNT(*)")));
        var results = commands.Where(sql => !sql.Contains("COUNT(*)")).ToArray();
        Assert.Equal(7, results.Length);
        Assert.All(results, sql =>
        {
            Assert.Contains("ORDER BY", sql);
            Assert.Contains("search_rank", sql);
            Assert.Contains("LIMIT", sql);
            Assert.Contains("ESCAPE", sql);
        });
    }

    [Fact]
    public async Task Ranking_keeps_exact_prefix_context_and_unicode_ordinal_ties()
    {
        using var factory = new CaptureFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var context = System("R02"); db.Systems.Add(context);
        await db.SaveChangesAsync();
        var other = await db.Systems.SingleAsync(s => s.Id == 12);
        // Same ranks and Unicode ordering as the old .NET comparator, then stable Id.
        var candidates = new[] { "zR02", "R02suffix", "r02", "ÅR02", "åR02", "aR02", "😀R02" };
        var functions = candidates.Select((title, index) => new SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain.BusinessFunction
        { Name = title, SystemId = index == 0 ? context.Id : other.Id, FunctionType = "Query", Purpose = "R02" }).ToArray();
        db.BusinessFunctions.AddRange(functions); await db.SaveChangesAsync();
        var expected = functions.OrderBy(f => f.Name.Equals("R02", StringComparison.OrdinalIgnoreCase) ? 0
                : f.Name.StartsWith("R02", StringComparison.OrdinalIgnoreCase) ? 1 : f.SystemId == context.Id ? 2 : 3)
            .ThenBy(f => f.Name, StringComparer.OrdinalIgnoreCase).ThenBy(f => f.Id).Select(f => f.Id).ToArray();
        var result = await scope.ServiceProvider.GetRequiredService<SearchQueries>().SearchKnowledge(new("R02", "BusinessFunction", 20), CancellationToken.None);
        Assert.Equal(expected, result.Response!.Groups.Single().Items.Select(item => item.Id));
        // SQLite LIKE's non-ASCII case behavior remains unchanged (ranking is separate).
        db.Systems.AddRange(System("ÅONLY"), System("åONLY")); await db.SaveChangesAsync();
        var unicode = await scope.ServiceProvider.GetRequiredService<SearchQueries>().SearchKnowledge(new("ÅONLY", "System", 20), CancellationToken.None);
        Assert.Equal(1, unicode.Response!.Total);
    }

    [Theory]
    [InlineData("", 5, "System", 400)]
    [InlineData("LOT", 21, "System", 400)]
    [InlineData("LOT", 5, "NoSuchType", 400)]
    [InlineData("LOT", 1, "System", 200)]
    public async Task Api_validation_is_preserved(string query, int limit, string types, int status)
    {
        using var factory = new CaptureFactory(); using var client = factory.CreateAuthenticatedClient();
        using var response = await client.GetAsync($"/api/search?q={query}&limitPerGroup={limit}&types={types}");
        Assert.Equal(status, (int)response.StatusCode);
    }

    [Fact]
    public async Task Query_length_boundaries_are_preserved()
    {
        using var factory = new CaptureFactory(); using var client = factory.CreateAuthenticatedClient();
        using var valid = await client.GetAsync($"/api/search?q={new string('a', 100)}");
        using var invalid = await client.GetAsync($"/api/search?q={new string('a', 101)}");
        Assert.Equal(HttpStatusCode.OK, valid.StatusCode); Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }


    [Fact]
    public async Task Total_excludes_closed_unknown_items_whose_system_is_deleted()
    {
        using var factory = new CaptureFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var system = System("R02-parent");
        db.Systems.Add(system); await db.SaveChangesAsync();
        db.UnknownItems.Add(new() { SystemId = system.Id, ItemCode = "R02-HISTORY", Question = "R02-history",
            Status = SystemKnowledgeHub.Api.Features.UnknownItems.Domain.UnknownItemStatus.Closed, ClosedAt = DateTimeOffset.UtcNow });
        system.IsDeleted = true; system.DeletedAt = DateTimeOffset.UtcNow;
        system.DeletedByUserId = await db.Users.Select(u => u.Id).FirstAsync(); system.DeletedByDisplayName = "test";
        await db.SaveChangesAsync();
        var result = await new SearchQueries(db).SearchKnowledge(new("R02-history", "UnknownItem", 20), CancellationToken.None);
        Assert.Equal(0, result.Response!.Total); Assert.Empty(result.Response.Groups);
    }

    [Fact]
    public async Task Integration_context_rank_matches_its_display_projection()
    {
        using var factory = new CaptureFactory();
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var left = System("LEFT"); var right = System("RIGHT"); db.Systems.AddRange(left, right); await db.SaveChangesAsync();
        var both = new SystemKnowledgeHub.Api.Features.Integrations.Domain.Integration
            { Name = "Z context", SourceSystemId = left.Id, TargetSystemId = right.Id, SourcePartyName = "source", TargetPartyName = "target", Purpose = "LEFT → RIGHT" };
        var other = new SystemKnowledgeHub.Api.Features.Integrations.Domain.Integration
            { Name = "A other", SourceSystemId = left.Id, SourcePartyName = "source", TargetPartyName = "target", Purpose = "LEFT → RIGHT" };
        db.Integrations.AddRange(both, other); await db.SaveChangesAsync();
        var result = await new SearchQueries(db).SearchKnowledge(new("LEFT → RIGHT", "Integration", 1), CancellationToken.None);
        Assert.Equal(2, result.Response!.Total);
        var first = Assert.Single(result.Response.Groups.Single().Items);
        Assert.Equal(both.Id, first.Id); Assert.Equal("LEFT → RIGHT", first.SystemContext);
    }

    private static KnowledgeSystem System(string name) => new()
    { Name = name, DisplayName = name, SystemType = "Internal", CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow };

    private sealed class CaptureFactory : BootstrapWebApplicationFactory
    {
        public SqlCapture Capture { get; } = new();

    }
    private sealed class SqlCapture : DbCommandInterceptor
    {
        public List<string> Commands { get; } = [];
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
            CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        { Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
    }
}
