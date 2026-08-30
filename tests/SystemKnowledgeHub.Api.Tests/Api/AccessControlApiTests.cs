using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AccessControlApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public AccessControlApiTests(BootstrapWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Anonymous_viewer_editor_and_administrator_follow_the_backend_matrix()
    {
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsJsonAsync("/api/systems", SystemRequest("anonymous"))).StatusCode);

        var viewerId = await CreateUser(AccessLevel.Viewer);
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/dashboard")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PutAsJsonAsync($"/api/users/{viewerId}/access-level", new { accessLevel = "Editor", concurrencyToken = "ignored" })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/systems", SystemRequest("viewer"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/database-objects", new
        {
            databaseSourceId = 9,
            schemaName = "SEC2",
            objectName = $"VIEWER_DENIED_{Guid.NewGuid():N}",
            objectType = "Table",
            accessMode = "Read",
            actor = new { displayName = "SEC-02 viewer" },
        })).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.PostAsJsonAsync("/api/evidence/human-confirmations", new { })).StatusCode);

        var editorId = await CreateUser(AccessLevel.Editor);
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        Assert.Equal(HttpStatusCode.Created, (await editor.PostAsJsonAsync("/api/systems", SystemRequest("editor"))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/users")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PutAsJsonAsync($"/api/users/{editorId}/access-level", new { accessLevel = "Administrator", concurrencyToken = "ignored" })).StatusCode);

        using var administrator = _factory.CreateAuthenticatedClient();
        Assert.Equal(HttpStatusCode.OK, (await administrator.GetAsync("/api/users")).StatusCode);
        var editorDetail = await ReadUser(administrator, editorId);
        using var downgradeEditor = await administrator.PutAsJsonAsync($"/api/users/{editorId}/access-level", new
        {
            accessLevel = "Viewer",
            concurrencyToken = editorDetail.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, downgradeEditor.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.PostAsJsonAsync("/api/systems", SystemRequest("editor-downgraded"))).StatusCode);

        var transitionUserId = await CreateUser(AccessLevel.Viewer);
        var transitionDetail = await ReadUser(administrator, transitionUserId);
        using var invalidNumericAccess = await administrator.PutAsJsonAsync($"/api/users/{transitionUserId}/access-level", new
        {
            accessLevel = 99,
            concurrencyToken = transitionDetail.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidNumericAccess.StatusCode);
        using var promoteToEditor = await administrator.PutAsJsonAsync($"/api/users/{transitionUserId}/access-level", new
        {
            accessLevel = "Editor",
            concurrencyToken = transitionDetail.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, promoteToEditor.StatusCode);
        var editorResult = await promoteToEditor.Content.ReadFromJsonAsync<JsonElement>();
        using var promoteToAdministrator = await administrator.PutAsJsonAsync($"/api/users/{transitionUserId}/access-level", new
        {
            accessLevel = "Administrator",
            concurrencyToken = editorResult.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, promoteToAdministrator.StatusCode);
        await using var transitionScope = _factory.Services.CreateAsyncScope();
        var transitionDb = transitionScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var transitioned = await transitionDb.Users.SingleAsync(user => user.Id == transitionUserId);
        Assert.True(transitioned.IsActive);
        Assert.Equal(AccessLevel.Administrator, transitioned.AccessLevel);
    }

    [Fact]
    public async Task Access_level_and_identity_changes_preserve_the_last_usable_administrator()
    {
        var secondAdminId = await CreateUser(AccessLevel.Administrator, withIdentity: true);
        using var administrator = _factory.CreateAuthenticatedClient();
        var second = await ReadUser(administrator, secondAdminId);
        using var downgrade = await administrator.PutAsJsonAsync($"/api/users/{secondAdminId}/access-level", new
        {
            accessLevel = "Editor",
            concurrencyToken = second.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, downgrade.StatusCode);
        using var staleAccessLevel = await administrator.PutAsJsonAsync($"/api/users/{secondAdminId}/access-level", new
        {
            accessLevel = "Administrator",
            concurrencyToken = second.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, staleAccessLevel.StatusCode);
        using var invalidAccessLevel = await administrator.PutAsJsonAsync($"/api/users/{secondAdminId}/access-level", new
        {
            accessLevel = "Administrator",
            concurrencyToken = "invalid",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidAccessLevel.StatusCode);

        var defaultAdmin = await GetDefaultAdministrator();
        var defaultDetail = await ReadUser(administrator, defaultAdmin.UserId);
        using var blockedDowngrade = await administrator.PutAsJsonAsync($"/api/users/{defaultAdmin.UserId}/access-level", new
        {
            accessLevel = "Viewer",
            concurrencyToken = defaultDetail.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blockedDowngrade.StatusCode);
        var blockedBody = await blockedDowngrade.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("last_usable_administrator", blockedBody.GetProperty("details").GetProperty("reason").GetString());

        using var blockedDeactivate = await administrator.PutAsJsonAsync($"/api/users/{defaultAdmin.UserId}/active-state", new
        {
            isActive = false,
            actor = new { displayName = "SEC-02 test" },
            concurrencyToken = defaultDetail.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blockedDeactivate.StatusCode);

        using var identities = await administrator.GetAsync($"/api/users/{defaultAdmin.UserId}/login-identities");
        var identity = (await identities.Content.ReadFromJsonAsync<JsonElement>())[0];
        using var blockedIdentity = await administrator.PutAsJsonAsync($"/api/users/{defaultAdmin.UserId}/login-identities/{identity.GetProperty("id").GetInt64()}/active-state", new
        {
            isActive = false,
            concurrencyToken = identity.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blockedIdentity.StatusCode);
    }

    private async Task<long> CreateUser(AccessLevel accessLevel, bool withIdentity = false)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User { DisplayName = $"SEC-02 {Guid.NewGuid():N}", IsActive = true, AccessLevel = accessLevel, CreatedAt = timestamp, UpdatedAt = timestamp, Version = 1 };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        if (withIdentity)
        {
            dbContext.LoginIdentities.Add(new LoginIdentity { UserId = user.Id, Provider = "TestOidc", Subject = $"sec02-{user.Id}", IsActive = true, CreatedAt = timestamp, UpdatedAt = timestamp, Version = 1 });
            await dbContext.SaveChangesAsync();
        }
        return user.Id;
    }

    private async Task<(long UserId, long IdentityId)> GetDefaultAdministrator()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        return await dbContext.Users.Where(user => user.AccessLevel == AccessLevel.Administrator && user.IsActive)
            .Join(dbContext.LoginIdentities.Where(identity => identity.IsActive), user => user.Id, identity => identity.UserId, (user, identity) => new ValueTuple<long, long>(user.Id, identity.Id))
            .SingleAsync();
    }

    private static async Task<JsonElement> ReadUser(HttpClient client, long id)
    {
        using var response = await client.GetAsync($"/api/users/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static object SystemRequest(string suffix) => new { name = $"SEC2-{suffix}-{Guid.NewGuid():N}", displayName = "SEC-02", systemType = "Test", lifecycle = "Running", purpose = "authorization", actor = new { displayName = "SEC-02 test" } };
}
