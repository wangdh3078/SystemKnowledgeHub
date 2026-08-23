using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class KnowledgeDocumentSearchApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _editor;

    public KnowledgeDocumentSearchApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _editor = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Global_search_finds_title_summary_and_chinese_body_content_with_a_plain_text_snippet()
    {
        var document = await Create("SOP · Oracle 连接处理", "Oracle 连接恢复说明", "## Procedure\n检查 Oracle 数据库监听服务是否正常运行。出现 ORA-12541 时：\n```sql\nSELECT * FROM v$listener;\n```");

        var bodyResult = await Search("监听");
        var bodyItem = DocumentItems(bodyResult).Single(item => item.GetProperty("id").GetInt64() == document.Id);
        Assert.Equal("Sop", bodyItem.GetProperty("contentType").GetString());
        Assert.Equal("Draft", bodyItem.GetProperty("lifecycleStatus").GetString());
        Assert.Contains("监听服务", bodyItem.GetProperty("shortDescription").GetString());
        Assert.DoesNotContain("```", bodyItem.GetProperty("shortDescription").GetString());

        Assert.Contains(DocumentItems(await Search("恢复说明")), item => item.GetProperty("id").GetInt64() == document.Id);
        Assert.Contains(DocumentItems(await Search("连接处理")), item => item.GetProperty("id").GetInt64() == document.Id);
        Assert.Contains(DocumentItems(await Search("ORA-12541")), item => item.GetProperty("id").GetInt64() == document.Id);
        Assert.Contains(DocumentItems(await Search("SELECT * FROM")), item => item.GetProperty("id").GetInt64() == document.Id);
    }

    [Fact]
    public async Task Content_updates_replace_the_fts_entry_and_archived_documents_are_excluded_until_restored()
    {
        var document = await Create("索引同步", null, "旧关键字 AAA");
        Assert.Contains(DocumentItems(await Search("AAA")), item => item.GetProperty("id").GetInt64() == document.Id);

        var updated = await UpdateContent(document.Id, "索引同步新标题", "新摘要 BBB", "新关键字 BBB", document.ConcurrencyToken);
        Assert.DoesNotContain(DocumentItems(await Search("AAA")), item => item.GetProperty("id").GetInt64() == document.Id);
        Assert.Contains(DocumentItems(await Search("BBB")), item => item.GetProperty("id").GetInt64() == document.Id);

        var published = await UpdateLifecycle(document.Id, "Published", updated.ConcurrencyToken);
        var archived = await UpdateLifecycle(document.Id, "Archived", published.ConcurrencyToken);
        Assert.DoesNotContain(DocumentItems(await Search("BBB")), item => item.GetProperty("id").GetInt64() == document.Id);

        await UpdateLifecycle(document.Id, "Draft", archived.ConcurrencyToken);
        Assert.Contains(DocumentItems(await Search("BBB")), item => item.GetProperty("id").GetInt64() == document.Id);
    }

    [Fact]
    public async Task Title_hits_rank_before_body_only_hits_and_viewer_can_search_documents()
    {
        var titleMatch = await Create("Oracle Listener 操作说明", null, "普通正文");
        var bodyMatch = await Create("一般说明", null, "Oracle Listener 仅在正文出现");

        var items = DocumentItems(await Search("Oracle Listener"));
        Assert.Equal(titleMatch.Id, items.First(item => item.GetProperty("id").GetInt64() is var id && (id == titleMatch.Id || id == bodyMatch.Id)).GetProperty("id").GetInt64());

        var viewerId = await CreateUser(AccessLevel.Viewer);
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);
        using var response = await viewer.GetAsync("/api/search?q=Listener&types=KnowledgeDocument");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains(DocumentItems((await response.Content.ReadFromJsonAsync<JsonElement>()).Clone()), item => item.GetProperty("id").GetInt64() == titleMatch.Id);
    }

    [Theory]
    [InlineData("\"")]
    [InlineData("'")]
    [InlineData("+")]
    [InlineData("MES/EAP")]
    [InlineData("(")]
    public async Task Global_search_treats_special_characters_as_plain_input(string query)
    {
        using var response = await _editor.GetAsync($"/api/search?q={Uri.EscapeDataString(query)}&types=KnowledgeDocument");
        Assert.NotEqual(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private async Task<DocumentResponse> Create(string title, string? summary, string bodyMarkdown)
    {
        using var response = await _editor.PostAsJsonAsync("/api/knowledge-documents", new { documentType = "Sop", title, summary, bodyMarkdown });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return ReadDocument(await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<DocumentResponse> UpdateContent(long id, string title, string? summary, string bodyMarkdown, string concurrencyToken)
    {
        using var response = await _editor.PutAsJsonAsync($"/api/knowledge-documents/{id}/content", new { title, summary, bodyMarkdown, concurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ReadDocument(await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<DocumentResponse> UpdateLifecycle(long id, string targetLifecycleStatus, string concurrencyToken)
    {
        using var response = await _editor.PutAsJsonAsync($"/api/knowledge-documents/{id}/lifecycle", new { targetLifecycleStatus, concurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return ReadDocument(await response.Content.ReadFromJsonAsync<JsonElement>());
    }

    private async Task<JsonElement> Search(string query)
    {
        using var response = await _editor.GetAsync($"/api/search?q={Uri.EscapeDataString(query)}&types=KnowledgeDocument");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static JsonElement[] DocumentItems(JsonElement response)
    {
        var group = response.GetProperty("groups").EnumerateArray()
            .FirstOrDefault(item => item.GetProperty("objectType").GetString() == "KnowledgeDocument");
        return group.ValueKind == JsonValueKind.Undefined
            ? []
            : group.GetProperty("items").EnumerateArray().ToArray();
    }

    private async Task<long> CreateUser(AccessLevel accessLevel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User { DisplayName = $"Search Viewer {Guid.NewGuid():N}", IsActive = true, AccessLevel = accessLevel, CreatedAt = timestamp, UpdatedAt = timestamp, Version = 1 };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static DocumentResponse ReadDocument(JsonElement document) => new(document.GetProperty("id").GetInt64(), document.GetProperty("concurrencyToken").GetString()!);

    private sealed record DocumentResponse(long Id, string ConcurrencyToken);
}
