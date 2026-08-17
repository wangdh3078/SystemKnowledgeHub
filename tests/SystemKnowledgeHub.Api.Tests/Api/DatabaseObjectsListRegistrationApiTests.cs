using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class DatabaseObjectsListRegistrationApiTests
    : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DatabaseObjectsListRegistrationApiTests(BootstrapWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Database_objects_list_scopes_source_and_rolls_column_match_into_object()
    {
        using var response = await _client.GetAsync(
            "/api/database-objects?systemId=12&databaseSourceId=9&search=STATE_FLAG&page=1&pageSize=20&sort=objectName:asc");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        var item = Assert.Single(payload.GetProperty("items").EnumerateArray());

        Assert.Equal(45, item.GetProperty("id").GetInt64());
        Assert.Equal("MES.TABLE_EQP", $"{item.GetProperty("schema").GetString()}.{item.GetProperty("objectName").GetString()}");
        Assert.Equal("STATE_FLAG", item.GetProperty("matchedColumn").GetProperty("columnName").GetString());
        Assert.Equal(9, item.GetProperty("databaseSource").GetProperty("id").GetInt64());
        Assert.Equal("MES", payload.GetProperty("browseContext").GetProperty("system").GetProperty("name").GetString());
    }

    [Fact]
    public async Task Create_database_source_persists_source_for_system_browse_context()
    {
        var request = new
        {
            systemId = 12,
            name = "MES 历史库测试来源",
            engine = "Oracle",
            environment = "Test",
            isPrimary = false,
            actor = new { displayName = "测试人员", role = "知识整理人员" },
        };

        using var createResponse = await _client.PostAsJsonAsync("/api/database-sources", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdId = created.GetProperty("id").GetInt64();
        Assert.False(string.IsNullOrWhiteSpace(created.GetProperty("concurrencyToken").GetString()));

        using var listResponse = await _client.GetAsync("/api/database-objects?systemId=12&page=1&pageSize=20&sort=objectName:asc");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var browseContext = (await listResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("browseContext");
        Assert.Contains(
            browseContext.GetProperty("databaseSources").EnumerateArray(),
            item => item.GetProperty("id").GetInt64() == createdId);
    }

    [Fact]
    public async Task Register_database_object_returns_unknown_and_is_immediately_listed()
    {
        var request = new
        {
            databaseSourceId = 9,
            schemaName = "MES",
            objectName = "TABLE_EQP_HISTORY_TEST",
            objectType = "Table",
            accessMode = "Read",
            actor = new { displayName = "测试人员", role = "知识整理人员" },
        };

        using var createResponse = await _client.PostAsJsonAsync("/api/database-objects", request);
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);
        var created = await createResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdId = created.GetProperty("id").GetInt64();
        Assert.Equal("Unknown", created.GetProperty("knowledgeStatus").GetString());

        using var listResponse = await _client.GetAsync(
            "/api/database-objects?databaseSourceId=9&search=TABLE_EQP_HISTORY_TEST&page=1&pageSize=20&sort=objectName:asc");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var item = Assert.Single((await listResponse.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("items").EnumerateArray());
        Assert.Equal(createdId, item.GetProperty("id").GetInt64());
        Assert.Equal("Unknown", item.GetProperty("knowledgeStatus").GetString());
    }
}
