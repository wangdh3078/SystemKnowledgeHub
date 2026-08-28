using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class DeleteCapabilityProjectionApiTests(BootstrapWebApplicationFactory factory)
    : IClassFixture<BootstrapWebApplicationFactory>
{
    [Fact]
    public async Task Eight_root_read_contracts_follow_administrator_owner_other_legacy_and_viewer_policy()
    {
        var ownerId = await CreateUser(AccessLevel.Editor, "DELETE B04 Owner");
        var otherEditorId = await CreateUser(AccessLevel.Editor, "DELETE B04 Other Editor");
        var viewerId = await CreateUser(AccessLevel.Viewer, "DELETE B04 Viewer");
        using var owner = await factory.CreateAuthenticatedClientAsync(ownerId);
        using var otherEditor = await factory.CreateAuthenticatedClientAsync(otherEditorId);
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);
        using var administrator = factory.CreateAuthenticatedClient();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var system = await Created(owner, "/api/systems", new
        {
            name = $"DELETE_B04_{suffix}", displayName = "删除能力系统", systemType = "Service",
            lifecycle = "Running", purpose = "DELETE-B04", actor = Actor(),
        });
        var systemId = Id(system);
        var source = await Created(owner, "/api/database-sources", new
        {
            systemId, name = $"source_{suffix}", engine = "SQLite", environment = "Test",
            instanceName = (string?)null, serviceName = (string?)null, databaseName = "capability",
            description = "DELETE-B04", isPrimary = true, actor = Actor(),
        });
        var sourceId = Id(source);
        var function = await Created(owner, "/api/business-functions", new
        {
            systemId, name = $"function_{suffix}", displayName = "删除能力功能", functionType = "Query",
            purpose = "DELETE-B04", rewriteStatus = "Unknown", actor = Actor(),
        });
        var databaseObject = await Created(owner, "/api/database-objects", new
        {
            databaseSourceId = sourceId, schemaName = "main", objectName = $"object_{suffix}", objectType = "Table",
            estimatedRows = 1L, accessMode = "Read", primaryKeyColumns = Array.Empty<string>(),
            businessKeyColumns = Array.Empty<string>(), businessDescription = "DELETE-B04", actor = Actor(),
        });
        var objectId = Id(databaseObject);
        var columnWrite = await Created(owner, $"/api/database-objects/{objectId}/columns", new
        {
            ordinalPosition = 1, columnName = $"column_{suffix}", dataType = "TEXT", nullable = false,
            defaultValue = (string?)null, databaseComment = (string?)null, businessDescription = "DELETE-B04",
            actor = Actor(), concurrencyToken = databaseObject.GetProperty("concurrencyToken").GetString(),
        });
        var columnId = columnWrite.GetProperty("column").GetProperty("id").GetInt64();
        var rule = await Created(owner, "/api/business-rules", new
        {
            systemId, name = $"rule_{suffix}", description = "DELETE-B04", condition = "x=1", result = "allow",
            inputData = Array.Empty<object>(), actor = Actor(),
        });
        var integration = await Created(owner, "/api/integrations", new
        {
            name = $"integration_{suffix}", integrationType = "RabbitMq",
            sourceParty = new { systemId = (long?)systemId, displayName = "Source" },
            targetParty = new { systemId = (long?)null, displayName = "External" },
            flowDirection = "OneWay", purpose = "DELETE-B04",
            endpoint = new { exchange = "delete-b04", topic = $"topic.{suffix}", queue = (string?)null },
            databaseSourceId = (long?)null, databaseObjectId = (long?)null, actor = Actor(),
        });
        var document = await Created(owner, "/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle", title = $"DELETE-B04 {suffix}", summary = "capability", bodyMarkdown = "body",
        });

        var rootUris = new[]
        {
            ("System", $"/api/systems/{systemId}"),
            ("BusinessFunction", $"/api/business-functions/{Id(function)}"),
            ("DatabaseObject", $"/api/database-objects/{objectId}"),
            ("DatabaseColumn", $"/api/database-columns/{columnId}"),
            ("BusinessRule", $"/api/business-rules/{Id(rule)}"),
            ("Integration", $"/api/integrations/{Id(integration)}"),
            ("KnowledgeDocument", $"/api/knowledge-documents/{Id(document)}"),
        };

        foreach (var (name, uri) in rootUris)
        {
            await AssertCanDelete(administrator, uri, true, $"Administrator {name}");
            await AssertCanDelete(owner, uri, true, $"Editor owner {name}");
            await AssertCanDelete(otherEditor, uri, false, $"Editor other {name}");
            await AssertCanDelete(viewer, uri, false, $"Viewer {name}");
        }
        await AssertSourceCanDelete(administrator, systemId, sourceId, true, "Administrator DatabaseSource");
        await AssertSourceCanDelete(owner, systemId, sourceId, true, "Editor owner DatabaseSource");
        await AssertSourceCanDelete(otherEditor, systemId, sourceId, false, "Editor other DatabaseSource");
        await AssertSourceCanDelete(viewer, systemId, sourceId, false, "Viewer DatabaseSource");

        var legacy = await Created(administrator, "/api/systems", new
        {
            name = $"DELETE_B04_LEGACY_{suffix}", displayName = "未知所有者系统", systemType = "Service",
            lifecycle = "Running", purpose = "DELETE-B04", actor = Actor(),
        });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await db.Systems.Where(item => item.Id == Id(legacy))
                .ExecuteUpdateAsync(setters => setters.SetProperty(item => item.CreatedByUserId, (long?)null));
        }
        await AssertCanDelete(owner, $"/api/systems/{Id(legacy)}", false, "Editor legacy unknown owner");
        await AssertCanDelete(administrator, $"/api/systems/{Id(legacy)}", true, "Administrator legacy unknown owner");
    }

    private async Task<long> CreateUser(AccessLevel accessLevel, string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"{displayName} {Guid.NewGuid():N}", IsActive = true, AccessLevel = accessLevel,
            CreatedAt = now, UpdatedAt = now, Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<JsonElement> Created(HttpClient client, string uri, object payload)
    {
        using var response = await client.PostAsJsonAsync(uri, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task AssertCanDelete(HttpClient client, string uri, bool expected, string because)
    {
        using var response = await client.GetAsync(uri);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(body.TryGetProperty("canDelete", out var capability), $"{because}: canDelete is missing");
        Assert.Equal(expected, capability.GetBoolean());
    }

    private static async Task AssertSourceCanDelete(HttpClient client, long systemId, long sourceId, bool expected, string because)
    {
        using var response = await client.GetAsync($"/api/database-objects?systemId={systemId}&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        var source = body.GetProperty("browseContext").GetProperty("databaseSources").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt64() == sourceId);
        Assert.True(source.TryGetProperty("canDelete", out var capability), $"{because}: canDelete is missing");
        Assert.Equal(expected, capability.GetBoolean());
    }

    private static long Id(JsonElement value) => value.GetProperty("id").GetInt64();
    private static object Actor() => new { displayName = "Ignored Actor", role = "Ignored Role" };
}
