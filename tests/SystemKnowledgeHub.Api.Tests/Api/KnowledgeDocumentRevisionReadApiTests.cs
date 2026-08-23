using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class KnowledgeDocumentRevisionReadApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public KnowledgeDocumentRevisionReadApiTests(BootstrapWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task List_is_authorized_bounded_newest_first_and_projects_only_immutable_metadata()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Original Revision Author");
        var viewerId = await CreateUser(AccessLevel.Viewer, "Revision Viewer");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);
        using var administrator = _factory.CreateAuthenticatedClient();
        var created = await CreateDocument(editor, "Revision list", "initial", "body 1");
        var documentId = created.GetProperty("id").GetInt64();

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var document = await dbContext.KnowledgeDocuments.SingleAsync(item => item.Id == documentId);
            for (var revisionNumber = 2L; revisionNumber <= 21; revisionNumber++)
            {
                dbContext.KnowledgeDocumentRevisions.Add(new KnowledgeDocumentRevision
                {
                    KnowledgeDocumentId = documentId,
                    RevisionNumber = revisionNumber,
                    Title = $"Revision {revisionNumber}",
                    Summary = $"Summary {revisionNumber}",
                    BodyMarkdown = $"body {revisionNumber}",
                    AuthorUserId = editorId,
                    AuthorDisplayNameSnapshot = "Original Revision Author",
                    CreatedAt = document.CreatedAt.AddMinutes(revisionNumber),
                    LifecycleContext = revisionNumber == 21
                        ? DocumentLifecycleStatus.Draft
                        : DocumentLifecycleStatus.Published,
                    ChangeSummary = revisionNumber == 21 ? "draft after publication" : null,
                    RevisionOrigin = RevisionOrigin.ContentSave,
                });
            }

            document.Title = "Revision 21";
            document.Summary = "Summary 21";
            document.BodyMarkdown = "body 21";
            document.CurrentRevisionNumber = 21;
            document.LatestPublishedRevisionNumber = 20;
            document.LifecycleStatus = DocumentLifecycleStatus.Draft;
            document.UpdatedAt = document.CreatedAt.AddMinutes(21);
            document.Version++;
            var author = await dbContext.Users.SingleAsync(item => item.Id == editorId);
            author.DisplayName = "Renamed Current User";
            await dbContext.SaveChangesAsync();
        }

        foreach (var client in new[] { viewer, editor, administrator })
        {
            using var authorizedResponse = await client.GetAsync($"/api/knowledge-documents/{documentId}/revisions");
            Assert.Equal(HttpStatusCode.OK, authorizedResponse.StatusCode);
        }

        using var response = await viewer.GetAsync($"/api/knowledge-documents/{documentId}/revisions");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, body.GetProperty("page").GetInt32());
        Assert.Equal(20, body.GetProperty("pageSize").GetInt32());
        Assert.Equal(21, body.GetProperty("total").GetInt32());
        var items = body.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(20, items.Length);
        Assert.Equal(Enumerable.Range(2, 20).Reverse().Select(value => (long)value), items.Select(item => item.GetProperty("revisionNumber").GetInt64()));
        Assert.True(items[0].GetProperty("isCurrent").GetBoolean());
        Assert.False(items[0].GetProperty("isLatestPublished").GetBoolean());
        Assert.False(items[1].GetProperty("isCurrent").GetBoolean());
        Assert.True(items[1].GetProperty("isLatestPublished").GetBoolean());
        Assert.Equal("Original Revision Author", items[0].GetProperty("authorDisplayName").GetString());
        Assert.Equal("draft after publication", items[0].GetProperty("changeSummary").GetString());
        Assert.All(items, item =>
        {
            Assert.False(item.TryGetProperty("title", out _));
            Assert.False(item.TryGetProperty("summary", out _));
            Assert.False(item.TryGetProperty("bodyMarkdown", out _));
            Assert.False(item.TryGetProperty("concurrencyToken", out _));
        });

        using var secondPageResponse = await viewer.GetAsync($"/api/knowledge-documents/{documentId}/revisions?page=2&pageSize=20");
        var secondPage = await secondPageResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal([1L], secondPage.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("revisionNumber").GetInt64()));

        using var maximumPageResponse = await viewer.GetAsync($"/api/knowledge-documents/{documentId}/revisions?pageSize=100");
        var maximumPage = await maximumPageResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(21, maximumPage.GetProperty("items").GetArrayLength());
        Assert.Equal(100, maximumPage.GetProperty("pageSize").GetInt32());

        await AssertValidationError(viewer, $"/api/knowledge-documents/{documentId}/revisions?page=0", "page");
        await AssertValidationError(viewer, $"/api/knowledge-documents/{documentId}/revisions?pageSize=101", "pageSize");
        using var missingResponse = await viewer.GetAsync("/api/knowledge-documents/9000000/revisions");
        Assert.Equal(HttpStatusCode.NotFound, missingResponse.StatusCode);

        using var forbiddenWrite = await viewer.PutAsJsonAsync($"/api/knowledge-documents/{documentId}/content", new
        {
            title = "Forbidden",
            summary = (string?)null,
            bodyMarkdown = "Forbidden",
            concurrencyToken = created.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenWrite.StatusCode);
    }

    [Fact]
    public async Task Detail_returns_exact_snapshot_and_supports_baseline_restore_and_not_found_contracts()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Immutable Detail Author");
        var viewerId = await CreateUser(AccessLevel.Viewer, "Detail Viewer");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);
        var baselineDocument = await CreateDocument(editor, "Migrated title", "Migrated summary", "# Migrated body");
        var baselineDocumentId = baselineDocument.GetProperty("id").GetInt64();
        var restoreDocument = await CreateDocument(editor, "Restore source", "Initial summary", "initial body");
        var restoreDocumentId = restoreDocument.GetProperty("id").GetInt64();

        DateTimeOffset baselineCapturedAt;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var baseline = await dbContext.KnowledgeDocumentRevisions.SingleAsync(item =>
                item.KnowledgeDocumentId == baselineDocumentId && item.RevisionNumber == 1);
            baseline.RevisionOrigin = RevisionOrigin.MigrationBaseline;
            baseline.AuthorUserId = null;
            baseline.AuthorDisplayNameSnapshot = null;
            baselineCapturedAt = baseline.CreatedAt;

            var document = await dbContext.KnowledgeDocuments.SingleAsync(item => item.Id == restoreDocumentId);
            var revisionTwoAt = document.CreatedAt.AddMinutes(2);
            dbContext.KnowledgeDocumentRevisions.AddRange(
                new KnowledgeDocumentRevision
                {
                    KnowledgeDocumentId = restoreDocumentId,
                    RevisionNumber = 2,
                    Title = "Saved title",
                    Summary = "Saved summary",
                    BodyMarkdown = "## Saved body",
                    AuthorUserId = editorId,
                    AuthorDisplayNameSnapshot = "Immutable Detail Author",
                    CreatedAt = revisionTwoAt,
                    LifecycleContext = DocumentLifecycleStatus.Published,
                    ChangeSummary = "Published wording",
                    RevisionOrigin = RevisionOrigin.ContentSave,
                },
                new KnowledgeDocumentRevision
                {
                    KnowledgeDocumentId = restoreDocumentId,
                    RevisionNumber = 3,
                    Title = "Restore source",
                    Summary = "Initial summary",
                    BodyMarkdown = "initial body",
                    AuthorUserId = editorId,
                    AuthorDisplayNameSnapshot = "Immutable Detail Author",
                    CreatedAt = revisionTwoAt.AddMinutes(1),
                    LifecycleContext = DocumentLifecycleStatus.Draft,
                    RestoreReason = "Recover the verified initial content",
                    RestoredFromRevisionNumber = 1,
                    RevisionOrigin = RevisionOrigin.Restore,
                });
            document.Title = "Restore source";
            document.Summary = "Initial summary";
            document.BodyMarkdown = "initial body";
            document.CurrentRevisionNumber = 3;
            document.LatestPublishedRevisionNumber = 2;
            document.LifecycleStatus = DocumentLifecycleStatus.Draft;
            document.Version++;
            await dbContext.SaveChangesAsync();

            var author = await dbContext.Users.SingleAsync(item => item.Id == editorId);
            author.DisplayName = "Current Renamed Author";
            await dbContext.SaveChangesAsync();
        }

        using var baselineResponse = await viewer.GetAsync($"/api/knowledge-documents/{baselineDocumentId}/revisions/1");
        Assert.Equal(HttpStatusCode.OK, baselineResponse.StatusCode);
        var baselineBody = await baselineResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(baselineDocumentId, baselineBody.GetProperty("knowledgeDocumentId").GetInt64());
        Assert.Equal("MigrationBaseline", baselineBody.GetProperty("revisionOrigin").GetString());
        Assert.Equal(JsonValueKind.Null, baselineBody.GetProperty("authorUserId").ValueKind);
        Assert.Equal(JsonValueKind.Null, baselineBody.GetProperty("authorDisplayName").ValueKind);
        Assert.Equal(baselineCapturedAt, baselineBody.GetProperty("createdAt").GetDateTimeOffset());
        Assert.Equal("Migrated title", baselineBody.GetProperty("title").GetString());
        Assert.Equal("Migrated summary", baselineBody.GetProperty("summary").GetString());
        Assert.Equal("# Migrated body", baselineBody.GetProperty("bodyMarkdown").GetString());
        Assert.False(baselineBody.TryGetProperty("concurrencyToken", out _));

        using var createdResponse = await viewer.GetAsync($"/api/knowledge-documents/{restoreDocumentId}/revisions/1");
        Assert.Equal("Created", (await createdResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("revisionOrigin").GetString());
        using var savedResponse = await viewer.GetAsync($"/api/knowledge-documents/{restoreDocumentId}/revisions/2");
        var savedBody = await savedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("ContentSave", savedBody.GetProperty("revisionOrigin").GetString());
        Assert.Equal("Published wording", savedBody.GetProperty("changeSummary").GetString());
        Assert.Equal("Immutable Detail Author", savedBody.GetProperty("authorDisplayName").GetString());
        Assert.True(savedBody.GetProperty("isLatestPublished").GetBoolean());
        Assert.False(savedBody.GetProperty("isCurrent").GetBoolean());

        using var restoreResponse = await viewer.GetAsync($"/api/knowledge-documents/{restoreDocumentId}/revisions/3");
        var restoreBody = await restoreResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Restore", restoreBody.GetProperty("revisionOrigin").GetString());
        Assert.Equal("Recover the verified initial content", restoreBody.GetProperty("restoreReason").GetString());
        Assert.Equal(1, restoreBody.GetProperty("restoredFromRevisionNumber").GetInt64());
        Assert.True(restoreBody.GetProperty("isCurrent").GetBoolean());
        Assert.False(restoreBody.TryGetProperty("concurrencyToken", out _));

        await AssertValidationError(viewer, $"/api/knowledge-documents/{restoreDocumentId}/revisions/0", "revisionNumber");
        await AssertValidationError(viewer, $"/api/knowledge-documents/{restoreDocumentId}/revisions/9007199254740992", "revisionNumber");
        using var missingDocument = await viewer.GetAsync("/api/knowledge-documents/9000000/revisions/1");
        Assert.Equal(HttpStatusCode.NotFound, missingDocument.StatusCode);
        using var missingRevision = await viewer.GetAsync($"/api/knowledge-documents/{restoreDocumentId}/revisions/99");
        Assert.Equal(HttpStatusCode.NotFound, missingRevision.StatusCode);
        using var crossDocument = await viewer.GetAsync($"/api/knowledge-documents/{baselineDocumentId}/revisions/2");
        Assert.Equal(HttpStatusCode.NotFound, crossDocument.StatusCode);
    }

    private async Task<long> CreateUser(AccessLevel accessLevel, string displayName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = displayName,
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<JsonElement> CreateDocument(
        HttpClient client,
        string title,
        string? summary,
        string bodyMarkdown)
    {
        using var response = await client.PostAsJsonAsync("/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle",
            title,
            summary,
            bodyMarkdown,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task AssertValidationError(HttpClient client, string path, string field)
    {
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("validation_error", body.GetProperty("code").GetString());
        Assert.True(body.GetProperty("fieldErrors").TryGetProperty(field, out _));
    }
}
