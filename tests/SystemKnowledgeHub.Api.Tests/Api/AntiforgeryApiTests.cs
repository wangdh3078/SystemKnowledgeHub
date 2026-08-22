using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AntiforgeryApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public AntiforgeryApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task Unsafe_api_requests_require_a_valid_antiforgery_token_while_reads_do_not()
    {
        using var readClient = _factory.CreateAuthenticatedClientWithoutAntiforgery();
        using var readResponse = await readClient.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);

        using var missingTokenClient = _factory.CreateAuthenticatedClientWithoutAntiforgery();
        using var missingTokenResponse = await missingTokenClient.PostAsJsonAsync(
            "/api/knowledge-roles",
            KnowledgeRoleRequest());
        Assert.Equal(HttpStatusCode.Forbidden, missingTokenResponse.StatusCode);
        Assert.Equal("antiforgery_failed", await ErrorCode(missingTokenResponse));

        using var invalidTokenClient = _factory.CreateAuthenticatedClient();
        invalidTokenClient.DefaultRequestHeaders.Remove("X-CSRF-TOKEN");
        invalidTokenClient.DefaultRequestHeaders.TryAddWithoutValidation("X-CSRF-TOKEN", "invalid-token");
        using var invalidTokenResponse = await invalidTokenClient.PostAsJsonAsync(
            "/api/knowledge-roles",
            KnowledgeRoleRequest());
        Assert.Equal(HttpStatusCode.Forbidden, invalidTokenResponse.StatusCode);
        Assert.Equal("antiforgery_failed", await ErrorCode(invalidTokenResponse));

        using var validTokenClient = _factory.CreateAuthenticatedClient();
        using var validTokenResponse = await validTokenClient.PostAsJsonAsync(
            "/api/knowledge-roles",
            KnowledgeRoleRequest());
        Assert.Equal(HttpStatusCode.Created, validTokenResponse.StatusCode);
    }

    private static object KnowledgeRoleRequest() => new
    {
        name = $"Antiforgery-{Guid.NewGuid():N}",
        description = "Antiforgery integration test",
        actor = new { displayName = "SEC-03 test", role = "Administrator" },
    };

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        return error.GetProperty("code").GetString();
    }
}
