using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class KnowledgeDocumentsApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public KnowledgeDocumentsApiTests(BootstrapWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_list_detail_and_content_update_use_current_user_and_preserve_document_axes()
    {
        var editorId = await CreateUser(AccessLevel.Editor);
        var otherUserId = await CreateUser(AccessLevel.Editor);
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);

        var types = new[] { "Requirement", "Specification", "TestCase", "Sop", "Troubleshooting", "KnowledgeArticle", "DesignNote" };
        var created = new List<JsonElement>();
        foreach (var documentType in types)
        {
            using var response = await editor.PostAsJsonAsync("/api/knowledge-documents", new
            {
                documentType,
                title = $"{documentType} title",
                summary = $"{documentType} summary",
                bodyMarkdown = "# Heading\r\n\r\n正文",
            });
            Assert.Equal(HttpStatusCode.Created, response.StatusCode);
            var document = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
            Assert.Equal(documentType, document.GetProperty("documentType").GetString());
            Assert.Equal("Draft", document.GetProperty("lifecycleStatus").GetString());
            Assert.Equal("Unknown", document.GetProperty("knowledgeStatus").GetString());
            Assert.Equal(editorId, document.GetProperty("createdByUserId").GetInt64());
            Assert.Equal(editorId, document.GetProperty("updatedByUserId").GetInt64());
            Assert.Equal("# Heading\n\n正文", document.GetProperty("bodyMarkdown").GetString());
            Assert.False(string.IsNullOrWhiteSpace(document.GetProperty("concurrencyToken").GetString()));
            created.Add(document);
        }

        using var administrator = _factory.CreateAuthenticatedClient();
        using var administratorCreate = await administrator.PostAsJsonAsync("/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle",
            title = "Administrator document",
            bodyMarkdown = "administrator body",
        });
        Assert.Equal(HttpStatusCode.Created, administratorCreate.StatusCode);
        var administratorDocument = (await administratorCreate.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        using var administratorUpdate = await administrator.PutAsJsonAsync($"/api/knowledge-documents/{administratorDocument.GetProperty("id").GetInt64()}/content", new
        {
            title = "Administrator updated document",
            bodyMarkdown = "administrator updated body",
            concurrencyToken = administratorDocument.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, administratorUpdate.StatusCode);

        using var typeList = await editor.GetAsync("/api/knowledge-documents?documentType=Specification&lifecycleStatus=Draft&knowledgeStatus=Unknown&sort=title:asc&page=1&pageSize=10");
        Assert.Equal(HttpStatusCode.OK, typeList.StatusCode);
        var list = await typeList.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, list.GetProperty("total").GetInt32());
        Assert.Equal("Specification", list.GetProperty("items")[0].GetProperty("documentType").GetString());

        var target = created[0];
        var targetId = target.GetProperty("id").GetInt64();
        using var queryList = await editor.GetAsync("/api/knowledge-documents?query=Requirement%20summary&sort=updatedAt:desc&page=1&pageSize=1");
        Assert.Equal(HttpStatusCode.OK, queryList.StatusCode);
        Assert.Equal(targetId, (await queryList.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items")[0].GetProperty("id").GetInt64());

        using var detailResponse = await editor.GetAsync($"/api/knowledge-documents/{targetId}");
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = (await detailResponse.Content.ReadFromJsonAsync<JsonElement>()).Clone();

        using var update = await editor.PutAsJsonAsync($"/api/knowledge-documents/{targetId}/content", new
        {
            title = "Requirement updated",
            summary = "updated summary",
            bodyMarkdown = "updated\rbody",
            concurrencyToken = detail.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = (await update.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.Equal("updated\nbody", updated.GetProperty("bodyMarkdown").GetString());
        Assert.Equal(editorId, updated.GetProperty("updatedByUserId").GetInt64());
        Assert.NotEqual(detail.GetProperty("concurrencyToken").GetString(), updated.GetProperty("concurrencyToken").GetString());
        Assert.Equal("Draft", updated.GetProperty("lifecycleStatus").GetString());
        Assert.Equal("Unknown", updated.GetProperty("knowledgeStatus").GetString());

        using var stale = await editor.PutAsJsonAsync($"/api/knowledge-documents/{targetId}/content", new
        {
            title = "stale",
            summary = (string?)null,
            bodyMarkdown = "stale",
            concurrencyToken = detail.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("conflict", await ErrorCode(stale));

        using var invalidToken = await editor.PutAsJsonAsync($"/api/knowledge-documents/{targetId}/content", new
        {
            title = "invalid",
            summary = (string?)null,
            bodyMarkdown = "invalid",
            concurrencyToken = "not-an-opaque-token",
        });
        Assert.Equal(HttpStatusCode.BadRequest, invalidToken.StatusCode);
        Assert.Equal("validation_error", await ErrorCode(invalidToken));

        using var forgedRequest = new HttpRequestMessage(HttpMethod.Post, "/api/knowledge-documents")
        {
            Content = JsonContent.Create(new { documentType = "KnowledgeArticle", title = "Forged actor", bodyMarkdown = "body" }),
        };
        forgedRequest.Headers.TryAddWithoutValidation("X-Current-User-Id", otherUserId.ToString());
        foreach (var header in editor.DefaultRequestHeaders)
        {
            forgedRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
        using var forgedResponse = await editor.SendAsync(forgedRequest);
        Assert.Equal(HttpStatusCode.Created, forgedResponse.StatusCode);
        var forged = await forgedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(editorId, forged.GetProperty("createdByUserId").GetInt64());

        using var missing = await editor.GetAsync("/api/knowledge-documents/9007199254740991");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
    }

    [Fact]
    public async Task Viewer_can_read_but_cannot_write_and_validation_is_consistent()
    {
        var editorId = await CreateUser(AccessLevel.Editor);
        var viewerId = await CreateUser(AccessLevel.Viewer);
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        using var created = await editor.PostAsJsonAsync("/api/knowledge-documents", new { documentType = "KnowledgeArticle", title = "Readable", bodyMarkdown = "content" });
        var document = await created.Content.ReadFromJsonAsync<JsonElement>();
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync("/api/knowledge-documents?page=1&pageSize=20")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await viewer.GetAsync($"/api/knowledge-documents/{document.GetProperty("id").GetInt64()}")).StatusCode);

        using var viewerCreate = await viewer.PostAsJsonAsync("/api/knowledge-documents", new { documentType = "KnowledgeArticle", title = "No", bodyMarkdown = "No" });
        Assert.Equal(HttpStatusCode.Forbidden, viewerCreate.StatusCode);
        using var viewerUpdate = await viewer.PutAsJsonAsync($"/api/knowledge-documents/{document.GetProperty("id").GetInt64()}/content", new { title = "No", bodyMarkdown = "No", concurrencyToken = document.GetProperty("concurrencyToken").GetString() });
        Assert.Equal(HttpStatusCode.Forbidden, viewerUpdate.StatusCode);

        using var invalidCreate = await editor.PostAsJsonAsync("/api/knowledge-documents", new { documentType = "Other", title = " ", bodyMarkdown = "body" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidCreate.StatusCode);
        Assert.Equal("validation_error", await ErrorCode(invalidCreate));
    }

    [Fact]
    public async Task Persistence_constraints_reject_invalid_enum_and_user_foreign_key_values()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var userId = await dbContext.Users.Select(user => user.Id).FirstAsync();
        await dbContext.Database.OpenConnectionAsync();
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        var userParameter = command.CreateParameter();
        userParameter.ParameterName = "$userId";
        userParameter.Value = userId;
        command.Parameters.Add(userParameter);
        command.CommandText = "INSERT INTO knowledge_documents (document_type,title,body_markdown,lifecycle_status,knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,created_by_user_id,created_by_display_name,updated_by_user_id,updated_by_display_name,created_at,updated_at,version) VALUES ('Other','invalid','body','Draft','Unknown','2026-01-01T00:00:00+00:00','test','test',$userId,'test',$userId,'test','2026-01-01T00:00:00+00:00','2026-01-01T00:00:00+00:00',1);";
        await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());
        command.CommandText = "INSERT INTO knowledge_documents (document_type,title,body_markdown,lifecycle_status,knowledge_status,knowledge_status_changed_at,knowledge_status_changed_by_name,knowledge_status_changed_by_role,created_by_user_id,created_by_display_name,updated_by_user_id,updated_by_display_name,created_at,updated_at,version) VALUES ('KnowledgeArticle','invalid-fk','body','Draft','Unknown','2026-01-01T00:00:00+00:00','test','test',999999999,'test',999999999,'test','2026-01-01T00:00:00+00:00','2026-01-01T00:00:00+00:00',1);";
        await Assert.ThrowsAsync<SqliteException>(() => command.ExecuteNonQueryAsync());

        var timestamp = DateTimeOffset.UtcNow;
        dbContext.KnowledgeDocuments.AddRange(
            DocumentWithLifecycle(DocumentLifecycleStatus.Published, userId, timestamp),
            DocumentWithLifecycle(DocumentLifecycleStatus.Archived, userId, timestamp));
        await dbContext.SaveChangesAsync();
        var lifecycleValues = (await dbContext.KnowledgeDocuments.AsNoTracking()
            .Select(item => item.LifecycleStatus)
            .ToArrayAsync())
            .Select(item => item.ToString())
            .OrderBy(item => item)
            .ToArray();
        Assert.Equal(["Archived", "Published"], lifecycleValues);
    }

    private async Task<long> CreateUser(AccessLevel accessLevel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User { DisplayName = $"KC-B01 {Guid.NewGuid():N}", IsActive = true, AccessLevel = accessLevel, CreatedAt = timestamp, UpdatedAt = timestamp, Version = 1 };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static KnowledgeDocument DocumentWithLifecycle(DocumentLifecycleStatus lifecycleStatus, long userId, DateTimeOffset timestamp) => new()
    {
        DocumentType = DocumentType.KnowledgeArticle,
        Title = lifecycleStatus.ToString(),
        BodyMarkdown = "body",
        LifecycleStatus = lifecycleStatus,
        KnowledgeStatus = SystemKnowledgeHub.Api.Shared.Domain.KnowledgeStatus.Unknown,
        KnowledgeStatusChangedAt = timestamp,
        KnowledgeStatusChangedByName = "test",
        KnowledgeStatusChangedByRole = "test",
        CreatedByUserId = userId,
        CreatedByDisplayName = "test",
        UpdatedByUserId = userId,
        UpdatedByDisplayName = "test",
        CreatedAt = timestamp,
        UpdatedAt = timestamp,
        Version = 1,
    };

    private static async Task<string?> ErrorCode(HttpResponseMessage response) => (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString();
}
