using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class SoftDeleteOwnershipFoundationApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;

    public SoftDeleteOwnershipFoundationApiTests(BootstrapWebApplicationFactory factory)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task All_root_creates_capture_canonical_creator_and_database_source_exposes_its_token()
    {
        var currentUser = await GetCurrentUser();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var system = await Created("/api/systems", new
        {
            name = $"DELETE_B01_{suffix}", displayName = "所有权验证系统", systemType = "Service",
            lifecycle = "Running", purpose = "DELETE-B01", actor = ForgedActor(),
        });
        var systemId = system.GetProperty("id").GetInt64();

        var source = await Created("/api/database-sources", new
        {
            systemId, name = $"source_{suffix}", engine = "SQLite", environment = "Test",
            instanceName = (string?)null, serviceName = (string?)null, databaseName = "ownership",
            description = "DELETE-B01", isPrimary = true, actor = ForgedActor(),
        });
        var sourceId = source.GetProperty("id").GetInt64();

        var function = await Created("/api/business-functions", new
        {
            systemId, name = $"function_{suffix}", displayName = "所有权功能", functionType = "Query",
            purpose = "DELETE-B01", rewriteStatus = "Unknown", actor = ForgedActor(),
        });
        var databaseObject = await Created("/api/database-objects", new
        {
            databaseSourceId = sourceId, schemaName = "main", objectName = $"object_{suffix}", objectType = "Table",
            estimatedRows = 1L, accessMode = "Read", primaryKeyColumns = Array.Empty<string>(),
            businessKeyColumns = Array.Empty<string>(), businessDescription = "DELETE-B01", actor = ForgedActor(),
        });
        var objectId = databaseObject.GetProperty("id").GetInt64();
        var column = await Created($"/api/database-objects/{objectId}/columns", new
        {
            ordinalPosition = 1, columnName = $"column_{suffix}", dataType = "TEXT", nullable = false,
            defaultValue = (string?)null, databaseComment = (string?)null, businessDescription = "DELETE-B01",
            actor = ForgedActor(), concurrencyToken = databaseObject.GetProperty("concurrencyToken").GetString(),
        });
        var rule = await Created("/api/business-rules", new
        {
            systemId, name = $"rule_{suffix}", description = "DELETE-B01", condition = "x=1", result = "allow",
            inputData = Array.Empty<object>(), actor = ForgedActor(),
        });
        var integration = await Created("/api/integrations", new
        {
            name = $"integration_{suffix}", integrationType = "RabbitMq",
            sourceParty = new { systemId = (long?)systemId, displayName = "Spoofed source" },
            targetParty = new { systemId = (long?)null, displayName = "External" },
            flowDirection = "OneWay", purpose = "DELETE-B01",
            endpoint = new { exchange = "delete-b01", topic = $"topic.{suffix}", queue = (string?)null },
            databaseSourceId = (long?)null, databaseObjectId = (long?)null, actor = ForgedActor(),
        });
        var document = await Created("/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle", title = $"DELETE-B01 FTS {suffix}", summary = "ownership", bodyMarkdown = "staleftsdefense",
        });

        using var browseResponse = await _client.GetAsync($"/api/database-objects?systemId={systemId}&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, browseResponse.StatusCode);
        var browse = await browseResponse.Content.ReadFromJsonAsync<JsonElement>();
        var sourceContext = browse.GetProperty("browseContext").GetProperty("databaseSources").EnumerateArray()
            .Single(item => item.GetProperty("id").GetInt64() == sourceId);
        Assert.Equal(source.GetProperty("concurrencyToken").GetString(), sourceContext.GetProperty("concurrencyToken").GetString());

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var storedSystem = await db.Systems.AsNoTracking().SingleAsync(item => item.Id == systemId);
        AssertCreator(storedSystem.CreatedByUserId, storedSystem.CreatedByName, currentUser);
        var storedSource = await db.DatabaseSources.AsNoTracking().SingleAsync(item => item.Id == sourceId);
        AssertCreator(storedSource.CreatedByUserId, storedSource.CreatedByName, currentUser);
        var storedFunction = await db.BusinessFunctions.AsNoTracking().SingleAsync(item => item.Id == function.GetProperty("id").GetInt64());
        AssertCreator(storedFunction.CreatedByUserId, storedFunction.CreatedByName, currentUser);
        var storedObject = await db.DatabaseObjects.AsNoTracking().SingleAsync(item => item.Id == objectId);
        AssertCreator(storedObject.CreatedByUserId, storedObject.CreatedByName, currentUser);
        var storedColumn = await db.DatabaseColumns.AsNoTracking().SingleAsync(item => item.Id == column.GetProperty("column").GetProperty("id").GetInt64());
        Assert.Equal(currentUser.Id, storedColumn.CreatedByUserId);
        Assert.Equal(currentUser.DisplayName, storedColumn.CreatedByDisplayName);
        var storedRule = await db.BusinessRules.AsNoTracking().SingleAsync(item => item.Id == rule.GetProperty("id").GetInt64());
        AssertCreator(storedRule.CreatedByUserId, storedRule.CreatedByName, currentUser);
        var storedIntegration = await db.Integrations.AsNoTracking().SingleAsync(item => item.Id == integration.GetProperty("id").GetInt64());
        AssertCreator(storedIntegration.CreatedByUserId, storedIntegration.CreatedByName, currentUser);
        var storedDocument = await db.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == document.GetProperty("id").GetInt64());
        Assert.Equal(currentUser.Id, storedDocument.CreatedByUserId);
        Assert.Equal(currentUser.DisplayName, storedDocument.CreatedByDisplayName);
        Assert.Equal(1L, await db.DatabaseSources.Where(item => item.Id == sourceId).Select(item => item.Version).SingleAsync());
    }

    [Fact]
    public async Task Deleted_parents_block_new_children_and_stale_FTS_rows_never_surface()
    {
        var currentUser = await GetCurrentUser();
        var suffix = Guid.NewGuid().ToString("N")[..8];

        var deletedSystem = await CreateSystem($"parent_system_{suffix}");
        await MarkDeleted("systems", deletedSystem.GetProperty("id").GetInt64(), currentUser);
        using var function = await _client.PostAsJsonAsync("/api/business-functions", new
        {
            systemId = deletedSystem.GetProperty("id").GetInt64(), name = $"blocked_function_{suffix}", functionType = "Query",
            rewriteStatus = "Unknown", actor = ForgedActor(),
        });
        Assert.Equal(HttpStatusCode.NotFound, function.StatusCode);

        var sourceSystem = await CreateSystem($"source_system_{suffix}");
        var deletedSource = await CreateSource(sourceSystem.GetProperty("id").GetInt64(), $"deleted_source_{suffix}");
        await MarkDeleted("database_sources", deletedSource.GetProperty("id").GetInt64(), currentUser);
        using var databaseObject = await _client.PostAsJsonAsync("/api/database-objects", new
        {
            databaseSourceId = deletedSource.GetProperty("id").GetInt64(), schemaName = "main", objectName = $"blocked_object_{suffix}",
            objectType = "Table", estimatedRows = (long?)null, accessMode = "Read", primaryKeyColumns = Array.Empty<string>(),
            businessKeyColumns = Array.Empty<string>(), businessDescription = (string?)null, actor = ForgedActor(),
        });
        Assert.Equal(HttpStatusCode.NotFound, databaseObject.StatusCode);

        var objectSystem = await CreateSystem($"object_system_{suffix}");
        var activeSource = await CreateSource(objectSystem.GetProperty("id").GetInt64(), $"active_source_{suffix}");
        var deletedObject = await Created("/api/database-objects", new
        {
            databaseSourceId = activeSource.GetProperty("id").GetInt64(), schemaName = "main", objectName = $"deleted_object_{suffix}",
            objectType = "Table", estimatedRows = (long?)null, accessMode = "Read", primaryKeyColumns = Array.Empty<string>(),
            businessKeyColumns = Array.Empty<string>(), businessDescription = (string?)null, actor = ForgedActor(),
        });
        await MarkDeleted("database_objects", deletedObject.GetProperty("id").GetInt64(), currentUser);
        using var databaseColumn = await _client.PostAsJsonAsync($"/api/database-objects/{deletedObject.GetProperty("id").GetInt64()}/columns", new
        {
            ordinalPosition = 1, columnName = $"blocked_column_{suffix}", dataType = "TEXT", nullable = false,
            defaultValue = (string?)null, databaseComment = (string?)null, businessDescription = (string?)null,
            actor = ForgedActor(), concurrencyToken = deletedObject.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.NotFound, databaseColumn.StatusCode);

        var document = await Created("/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle", title = $"Stale FTS {suffix}", bodyMarkdown = $"stalefts_{suffix}",
        });
        var documentId = document.GetProperty("id").GetInt64();
        await MarkDeleted("knowledge_documents", documentId, currentUser);

        using var detail = await _client.GetAsync($"/api/knowledge-documents/{documentId}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        using var staleSearch = await _client.GetAsync($"/api/search?q=stalefts_{suffix}&types=KnowledgeDocument");
        Assert.Equal(HttpStatusCode.OK, staleSearch.StatusCode);
        Assert.DoesNotContain(DocumentItems(await staleSearch.Content.ReadFromJsonAsync<JsonElement>()),
            item => item.GetProperty("id").GetInt64() == documentId);

        await using var scope = _factory.Services.CreateAsyncScope();
        var index = scope.ServiceProvider.GetRequiredService<KnowledgeDocumentSearchIndex>();
        await index.Rebuild(CancellationToken.None);
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(0, await db.Database.SqlQuery<long>(
            $"SELECT count(*) AS Value FROM knowledge_documents_fts WHERE rowid={documentId}").SingleAsync());
    }

    private async Task<JsonElement> CreateSystem(string name) => await Created("/api/systems", new
    {
        name, displayName = name, systemType = "Service", lifecycle = "Running", purpose = (string?)null, actor = ForgedActor(),
    });

    private async Task<JsonElement> CreateSource(long systemId, string name) => await Created("/api/database-sources", new
    {
        systemId, name, engine = "SQLite", environment = "Test", instanceName = (string?)null, serviceName = (string?)null,
        databaseName = name, description = (string?)null, isPrimary = false, actor = ForgedActor(),
    });

    private async Task<JsonElement> Created(string uri, object payload)
    {
        using var response = await _client.PostAsJsonAsync(uri, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<(long Id, string DisplayName)> GetCurrentUser()
    {
        using var response = await _client.GetAsync("/api/current-user");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var user = await response.Content.ReadFromJsonAsync<JsonElement>();
        return (user.GetProperty("id").GetInt64(), user.GetProperty("displayName").GetString()!);
    }

    private async Task MarkDeleted(string table, long id, (long Id, string DisplayName) user)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var sql = table switch
        {
            "systems" => "UPDATE systems SET is_deleted=1, deleted_at={0}, deleted_by_user_id={1}, deleted_by_display_name={2} WHERE id={3}",
            "database_sources" => "UPDATE database_sources SET is_deleted=1, deleted_at={0}, deleted_by_user_id={1}, deleted_by_display_name={2} WHERE id={3}",
            "database_objects" => "UPDATE database_objects SET is_deleted=1, deleted_at={0}, deleted_by_user_id={1}, deleted_by_display_name={2} WHERE id={3}",
            "knowledge_documents" => "UPDATE knowledge_documents SET is_deleted=1, deleted_at={0}, deleted_by_user_id={1}, deleted_by_display_name={2} WHERE id={3}",
            _ => throw new ArgumentOutOfRangeException(nameof(table), table, "Unsupported root table."),
        };
        await db.Database.ExecuteSqlRawAsync(
            sql,
            DateTimeOffset.UtcNow, user.Id, user.DisplayName, id);
    }

    private static void AssertCreator(long? createdByUserId, string createdByName, (long Id, string DisplayName) user)
    {
        Assert.Equal(user.Id, createdByUserId);
        Assert.Equal(user.DisplayName, createdByName);
    }

    private static JsonElement[] DocumentItems(JsonElement response)
    {
        var group = response.GetProperty("groups").EnumerateArray()
            .FirstOrDefault(item => item.GetProperty("objectType").GetString() == "KnowledgeDocument");
        return group.ValueKind == JsonValueKind.Undefined
            ? []
            : group.GetProperty("items").EnumerateArray().ToArray();
    }

    private static object ForgedActor() => new { displayName = "Forged Request Actor", role = "Forged Role" };
}
