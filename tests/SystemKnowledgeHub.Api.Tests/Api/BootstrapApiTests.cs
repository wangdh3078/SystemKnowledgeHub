using System.Net;
using System.Net.Http.Json;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class BootstrapApiTests
    : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly HttpClient _client;

    public BootstrapApiTests(BootstrapWebApplicationFactory factory)
    {
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Host_starts_and_bootstrap_status_traverses_sqlite()
    {
        using var response = await _client.GetAsync("/api/bootstrap/status");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var payload = await response.Content.ReadFromJsonAsync<BootstrapStatusPayload>();
        Assert.NotNull(payload);
        Assert.Equal("ok", payload.Status);
        Assert.Equal("SQLite", payload.DatabaseProvider);
    }

    private sealed record BootstrapStatusPayload(
        string Status,
        string DatabaseProvider);
}
