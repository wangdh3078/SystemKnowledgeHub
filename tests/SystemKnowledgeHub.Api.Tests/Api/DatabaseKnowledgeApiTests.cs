using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class DatabaseKnowledgeApiTests
    : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly HttpClient _client;

    public DatabaseKnowledgeApiTests(BootstrapWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Database_object_detail_returns_frozen_shape_and_token()
    {
        using var response = await _client.GetAsync("/api/database-objects/45?selectedColumnId=123");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(45, payload.GetProperty("id").GetInt64());
        Assert.Equal("MES.TABLE_EQP", payload.GetProperty("overview").GetProperty("qualifiedName").GetString());
        Assert.Equal("Inferred", payload.GetProperty("overview").GetProperty("knowledgeStatus").GetString());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("concurrencyToken").GetString()));
        Assert.Equal(123, payload.GetProperty("selectedColumnDrawer").GetProperty("columnId").GetInt64());
    }

    [Theory]
    [InlineData("/api/database-objects/0")]
    [InlineData("/api/database-objects/not-a-number")]
    public async Task Database_object_detail_rejects_invalid_id(string route)
    {
        using var response = await _client.GetAsync(route);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Database_object_detail_returns_not_found_and_invalid_selection_statuses()
    {
        using var missing = await _client.GetAsync("/api/database-objects/999");
        using var invalidSelection = await _client.GetAsync(
            "/api/database-objects/45?selectedColumnId=999");

        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, invalidSelection.StatusCode);
    }

    [Fact]
    public async Task Column_detail_returns_frozen_shape_and_known_values()
    {
        using var response = await _client.GetAsync("/api/database-columns/123");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("STATE_FLAG", payload.GetProperty("databaseMetadata").GetProperty("columnName").GetString());
        Assert.Equal(3, payload.GetProperty("knownValues").GetArrayLength());
        Assert.False(string.IsNullOrWhiteSpace(payload.GetProperty("concurrencyToken").GetString()));
    }

    [Theory]
    [InlineData("/api/database-columns/999", HttpStatusCode.NotFound)]
    [InlineData("/api/database-columns/-1", HttpStatusCode.BadRequest)]
    public async Task Column_detail_returns_expected_failure_status(
        string route,
        HttpStatusCode expectedStatus)
    {
        using var response = await _client.GetAsync(route);
        Assert.Equal(expectedStatus, response.StatusCode);
    }
}
