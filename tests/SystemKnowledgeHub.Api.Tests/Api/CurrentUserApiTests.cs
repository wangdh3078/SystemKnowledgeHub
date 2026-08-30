using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class CurrentUserApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public CurrentUserApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Authenticated_principal_resolves_canonical_user_and_ignores_forged_header()
    {
        var role = await CreateKnowledgeRole();
        var user = await CreateUser([role.GetProperty("id").GetInt64()]);
        using var authenticatedClient = await _factory.CreateAuthenticatedClientAsync(user.GetProperty("id").GetInt64());
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/current-user");
        request.Headers.TryAddWithoutValidation("X-Current-User-Id", "9007199254740991");

        using var response = await authenticatedClient.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var currentUser = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(user.GetProperty("id").GetInt64(), currentUser.GetProperty("id").GetInt64());
        Assert.Equal("U03 当前操作者", currentUser.GetProperty("displayName").GetString());
        Assert.True(currentUser.GetProperty("isActive").GetBoolean());
        Assert.Equal(role.GetProperty("id").GetInt64(), currentUser.GetProperty("knowledgeRoles")[0].GetProperty("id").GetInt64());
        Assert.False(currentUser.TryGetProperty("concurrencyToken", out _));
        Assert.Equal("Viewer", currentUser.GetProperty("accessLevel").GetString());
        Assert.Equal("oidc", currentUser.GetProperty("authenticationMethod").GetString());
        Assert.False(currentUser.GetProperty("mustChangePassword").GetBoolean());
    }

    [Fact]
    public async Task Unauthenticated_unmapped_and_inactive_principals_return_security_errors()
    {
        using var missingResponse = await _factory.CreateClient().GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.Unauthorized, missingResponse.StatusCode);
        Assert.Equal("unauthenticated", await ErrorCode(missingResponse));

        var sameEmailUser = await CreateUser([]);
        using var unmappedClient = _factory.CreateClient();
        unmappedClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Auth-Method", "oidc");
        unmappedClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Auth-Identity-Id", "9007199254740991");
        unmappedClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Auth-Version", "1");
        unmappedClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-User-Id", "9007199254740991");
        unmappedClient.DefaultRequestHeaders.TryAddWithoutValidation("X-Test-Access-Level", "Viewer");
        using var unmappedResponse = await unmappedClient.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.Forbidden, unmappedResponse.StatusCode);
        Assert.Equal("identity_unmapped", await ErrorCode(unmappedResponse));

        var user = await CreateUser([]);
        var userId = user.GetProperty("id").GetInt64();
        using var disableResponse = await _client.PutAsJsonAsync($"/api/users/{userId}/active-state", new
        {
            isActive = false,
            actor = Actor(),
            concurrencyToken = user.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, disableResponse.StatusCode);

        using var inactiveClient = await _factory.CreateAuthenticatedClientAsync(userId);
        using var inactiveResponse = await inactiveClient.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.Forbidden, inactiveResponse.StatusCode);
        Assert.Equal("account_inactive", await ErrorCode(inactiveResponse));

        var identityUser = await CreateUser([]);
        var identityUserId = identityUser.GetProperty("id").GetInt64();
        using var identityClient = await _factory.CreateAuthenticatedClientAsync(identityUserId);
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var identity = await dbContext.LoginIdentities.SingleAsync(item =>
                item.UserId == identityUserId && item.Provider == "TestOidc");
            identity.IsActive = false;
            identity.UpdatedAt = DateTimeOffset.UtcNow;
            identity.Version += 1;
            await dbContext.SaveChangesAsync();
        }
        using var inactiveIdentityResponse = await identityClient.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.Forbidden, inactiveIdentityResponse.StatusCode);
        Assert.Equal("identity_inactive", await ErrorCode(inactiveIdentityResponse));

        using var adminResponse = await _client.GetAsync("/api/users?isActive=true&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, adminResponse.StatusCode);
    }

    private async Task<JsonElement> CreateKnowledgeRole()
    {
        using var response = await _client.PostAsJsonAsync("/api/knowledge-roles", new
        {
            name = $"U03 角色-{UniqueSuffix()}",
            description = "Current User integration test",
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<JsonElement> CreateUser(IReadOnlyList<long> roleIds)
    {
        var suffix = UniqueSuffix();
        using var response = await _client.PostAsJsonAsync("/api/users", new
        {
            employeeNo = $"U03-{suffix}",
            displayName = "U03 当前操作者",
            email = $"u03-{suffix}@example.com",
            departmentOrTeam = "知识平台组",
            jobTitle = "知识工程师",
            knowledgeRoleIds = roleIds,
            loginSetup = new { type = "none" },
            actor = Actor(),
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<string?> ErrorCode(HttpResponseMessage response)
    {
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        return error.GetProperty("code").GetString();
    }

    private static object Actor() => new { displayName = "U03 测试人员", role = "知识整理人员" };

    private static string UniqueSuffix() => Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();
}
