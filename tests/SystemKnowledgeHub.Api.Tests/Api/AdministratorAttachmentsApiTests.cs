using System.IO.Compression;
using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AdministratorAttachmentsApiTests
{
    [Fact]
    public async Task Empty_list_statistics_and_invalid_filter_are_bounded()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        using var list = await administrator.GetAsync("/api/admin/attachments");
        var listJson = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(0, listJson.GetProperty("total").GetInt64());
        Assert.Empty(listJson.GetProperty("items").EnumerateArray());

        using var stats = await administrator.GetAsync("/api/admin/attachments/statistics");
        var statsJson = await stats.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        Assert.Equal(0, statsJson.GetProperty("totalCount").GetInt64());
        Assert.Equal(0, statsJson.GetProperty("totalSizeBytes").GetInt64());
        Assert.Empty(statsJson.GetProperty("largestAttachments").EnumerateArray());
        Assert.Empty(statsJson.GetProperty("recentUploads").EnumerateArray());

        using var invalid = await administrator.GetAsync("/api/admin/attachments?kind=Executable&pageSize=101");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
    }

    [Fact]
    public async Task List_filters_detail_statistics_and_role_isolation_use_all_revision_truth()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var editorId = await CreateUser(factory, AccessLevel.Editor, "Attachment admin editor");
        var viewerId = await CreateUser(factory, AccessLevel.Viewer, "Attachment admin viewer");
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);

        var document = await CreateDocument(editor, "附件治理文档");
        var documentId = document.GetProperty("id").GetInt64();
        var image = await UploadJson(editor, documentId, "current-diagram.png", "image/png", BuildPng());
        var historical = await UploadJson(editor, documentId, "historical-notes.txt", "text/plain", "history"u8.ToArray());
        var orphan = await UploadJson(editor, documentId, "orphan-package.zip", "application/zip", BuildZip());
        var imageId = image.GetProperty("attachmentId").GetInt64();
        var historicalId = historical.GetProperty("attachmentId").GetInt64();
        var orphanId = orphan.GetProperty("attachmentId").GetInt64();
        var revision2 = await SaveContent(
            editor,
            documentId,
            "附件治理文档",
            $"![diagram](attachment:{imageId})",
            document.GetProperty("concurrencyToken").GetString()!,
            [historicalId]);
        _ = await SaveContent(
            editor,
            documentId,
            "附件治理文档",
            $"![diagram](attachment:{imageId})",
            revision2.GetProperty("concurrencyToken").GetString()!,
            []);

        var deletedDocument = await CreateDocument(editor, "已删除附件所属文档");
        var deletedDocumentId = deletedDocument.GetProperty("id").GetInt64();
        var deletedFile = await UploadJson(editor, deletedDocumentId, "deleted-owner.pdf", "application/pdf", "%PDF-1.4\n%%EOF"u8.ToArray());
        var deletedFileId = deletedFile.GetProperty("attachmentId").GetInt64();
        var deletedSaved = await SaveContent(
            editor,
            deletedDocumentId,
            "已删除附件所属文档",
            "deleted owner",
            deletedDocument.GetProperty("concurrencyToken").GetString()!,
            [deletedFileId]);
        using (var deleteOwner = await editor.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/knowledge-documents/{deletedDocumentId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = deletedSaved.GetProperty("concurrencyToken").GetString() }),
        }))
        {
            Assert.Equal(HttpStatusCode.NoContent, deleteOwner.StatusCode);
        }

        using var list = await administrator.GetAsync("/api/admin/attachments?page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listJson = (await list.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.Equal(4, listJson.GetProperty("total").GetInt64());
        Assert.DoesNotContain("storageKey", listJson.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.AttachmentStorageRoot, listJson.ToString(), StringComparison.OrdinalIgnoreCase);

        await AssertFilter(administrator, "referenceStatus=Orphan", orphanId, [imageId, historicalId]);
        await AssertFilter(administrator, "referenceStatus=HistoricalOnly", historicalId, [orphanId, imageId]);
        await AssertFilter(administrator, "kind=Image", imageId, [historicalId, orphanId]);
        await AssertFilter(administrator, "extension=.txt", historicalId, [imageId, orphanId]);
        await AssertFilter(administrator, "query=orphan-package", orphanId, [imageId, historicalId]);

        using var historicalDetail = await administrator.GetAsync($"/api/admin/attachments/{historicalId}");
        Assert.Equal(HttpStatusCode.OK, historicalDetail.StatusCode);
        var historicalJson = (await historicalDetail.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.Equal(1, historicalJson.GetProperty("referenceCount").GetInt32());
        Assert.Equal(0, historicalJson.GetProperty("currentReferenceCount").GetInt32());
        Assert.Equal("HistoricalOnly", historicalJson.GetProperty("referenceStatus").GetString());
        Assert.Equal("附件治理文档", historicalJson.GetProperty("owner").GetProperty("title").GetString());
        Assert.Matches("^[a-f0-9]{64}$", historicalJson.GetProperty("sha256").GetString()!);
        Assert.DoesNotContain("storageKey", historicalJson.ToString(), StringComparison.OrdinalIgnoreCase);

        using var deletedDetail = await administrator.GetAsync($"/api/admin/attachments/{deletedFileId}");
        var deletedJson = (await deletedDetail.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.True(deletedJson.GetProperty("owner").GetProperty("isDeleted").GetBoolean());
        Assert.Equal("已删除附件所属文档", deletedJson.GetProperty("owner").GetProperty("title").GetString());
        Assert.True(deletedJson.GetProperty("referenceCount").GetInt32() > 0);

        using var stats = await administrator.GetAsync("/api/admin/attachments/statistics");
        Assert.Equal(HttpStatusCode.OK, stats.StatusCode);
        var statistics = (await stats.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.Equal(4, statistics.GetProperty("totalCount").GetInt64());
        Assert.Equal(1, statistics.GetProperty("imageCount").GetInt64());
        Assert.Equal(3, statistics.GetProperty("fileCount").GetInt64());
        Assert.Equal(1, statistics.GetProperty("orphanCount").GetInt64());
        Assert.Equal(3, statistics.GetProperty("referencedCount").GetInt64());
        Assert.Equal(1, statistics.GetProperty("historicalOnlyCount").GetInt64());
        Assert.Equal(1, statistics.GetProperty("deletedOwnerCount").GetInt64());
        Assert.NotEmpty(statistics.GetProperty("largestAttachments").EnumerateArray());
        Assert.NotEmpty(statistics.GetProperty("recentUploads").EnumerateArray());

        foreach (var client in new[] { editor, viewer })
        {
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/attachments")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync("/api/admin/attachments/statistics")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.GetAsync($"/api/admin/attachments/{orphanId}")).StatusCode);
            Assert.Equal(HttpStatusCode.Forbidden, (await client.PostAsync($"/api/admin/attachments/{orphanId}/integrity-check", null)).StatusCode);
            Assert.Equal(
                HttpStatusCode.Forbidden,
                (await Delete(client, orphanId, orphan.GetProperty("concurrencyToken").GetString()!)).StatusCode);
        }
    }

    [Fact]
    public async Task Physical_delete_rejects_current_historical_stale_and_confirm_race_but_removes_only_zero_reference_orphan()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var editorId = await CreateUser(factory, AccessLevel.Editor, "Attachment delete editor");
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Attachment deletion invariants");
        var documentId = document.GetProperty("id").GetInt64();
        var current = await UploadJson(editor, documentId, "current.txt", "text/plain", "current"u8.ToArray());
        var historical = await UploadJson(editor, documentId, "historical.txt", "text/plain", "historical"u8.ToArray());
        var race = await UploadJson(editor, documentId, "race.txt", "text/plain", "race"u8.ToArray());
        var stale = await UploadJson(editor, documentId, "stale.txt", "text/plain", "stale"u8.ToArray());
        var deletable = await UploadJson(editor, documentId, "delete-me.txt", "text/plain", "delete me"u8.ToArray());
        var currentId = current.GetProperty("attachmentId").GetInt64();
        var historicalId = historical.GetProperty("attachmentId").GetInt64();
        var raceId = race.GetProperty("attachmentId").GetInt64();
        var staleId = stale.GetProperty("attachmentId").GetInt64();
        var deletableId = deletable.GetProperty("attachmentId").GetInt64();
        var revision2 = await SaveContent(
            editor,
            documentId,
            "Attachment deletion invariants",
            "revision 2",
            document.GetProperty("concurrencyToken").GetString()!,
            [currentId, historicalId]);
        var revision3 = await SaveContent(
            editor,
            documentId,
            "Attachment deletion invariants",
            "revision 3",
            revision2.GetProperty("concurrencyToken").GetString()!,
            [currentId]);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Delete(administrator, currentId, current.GetProperty("concurrencyToken").GetString()!)).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Delete(administrator, historicalId, historical.GetProperty("concurrencyToken").GetString()!)).StatusCode);

        var raceDetail = await ReadDetail(administrator, raceId);
        _ = await SaveContent(
            editor,
            documentId,
            "Attachment deletion invariants",
            "race reference wins",
            revision3.GetProperty("concurrencyToken").GetString()!,
            [currentId, raceId]);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await Delete(administrator, raceId, raceDetail.GetProperty("concurrencyToken").GetString()!)).StatusCode);

        var staleDetail = await ReadDetail(administrator, staleId);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var staleEntity = await db.Attachments.SingleAsync(item => item.Id == staleId);
            staleEntity.Version++;
            await db.SaveChangesAsync();
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(administrator, staleId, staleDetail.GetProperty("concurrencyToken").GetString()!)).StatusCode);

        var deleteDetail = await ReadDetail(administrator, deletableId);
        var deletePath = await StoredPath(factory, deletableId);
        Assert.True(File.Exists(deletePath));
        using var deleted = await Delete(administrator, deletableId, deleteDetail.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        Assert.False(File.Exists(deletePath));

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await verifyDb.Attachments.AnyAsync(item => item.Id == deletableId));
        Assert.True(await verifyDb.Attachments.AnyAsync(item => item.Id == currentId));
        Assert.True(await verifyDb.Attachments.AnyAsync(item => item.Id == historicalId));
        Assert.True(await verifyDb.Attachments.AnyAsync(item => item.Id == raceId));
        Assert.Equal(3, await verifyDb.AttachmentReferences.CountAsync(item => item.AttachmentId == currentId));
        Assert.Equal(1, await verifyDb.AttachmentReferences.CountAsync(item => item.AttachmentId == historicalId));
    }

    [Fact]
    public async Task Filesystem_failure_stays_delete_pending_blocks_reference_and_retry_reconciles_metadata_and_object()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var editorId = await CreateUser(factory, AccessLevel.Editor, "Attachment retry editor");
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Attachment retry");
        var documentId = document.GetProperty("id").GetInt64();
        var orphan = await UploadJson(editor, documentId, "retry.txt", "text/plain", "retry bytes"u8.ToArray());
        var orphanId = orphan.GetProperty("attachmentId").GetInt64();
        var originalToken = orphan.GetProperty("concurrencyToken").GetString()!;
        var path = await StoredPath(factory, orphanId);

        using (var exclusiveLock = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
        using (var failedDelete = await Delete(administrator, orphanId, originalToken))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failedDelete.StatusCode);
        }

        var pending = await ReadDetail(administrator, orphanId);
        Assert.Equal("DeletePending", pending.GetProperty("storageState").GetString());
        Assert.Equal("DeletePending", pending.GetProperty("storageHealth").GetString());
        using (var pendingList = await administrator.GetAsync("/api/admin/attachments?storageState=DeletePending"))
        {
            var pendingListJson = await pendingList.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Contains(pendingListJson.GetProperty("items").EnumerateArray(), item => item.GetProperty("attachmentId").GetInt64() == orphanId);
        }
        Assert.Equal(HttpStatusCode.Conflict, (await Delete(administrator, orphanId, originalToken)).StatusCode);

        using (var referencePending = await editor.PutAsJsonAsync($"/api/knowledge-documents/{documentId}/content", new
        {
            title = "Attachment retry",
            summary = (string?)null,
            bodyMarkdown = "pending must not attach",
            changeSummary = "pending race",
            concurrencyToken = document.GetProperty("concurrencyToken").GetString(),
            fileAttachmentIds = new[] { orphanId },
        }))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, referencePending.StatusCode);
        }

        using var retry = await Delete(administrator, orphanId, pending.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(HttpStatusCode.NoContent, retry.StatusCode);
        Assert.False(File.Exists(path));
        Assert.Equal(HttpStatusCode.NotFound, (await administrator.GetAsync($"/api/admin/attachments/{orphanId}")).StatusCode);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await db.Attachments.AnyAsync(item => item.Id == orphanId));
        Assert.Equal(1, (await db.KnowledgeDocuments.SingleAsync(item => item.Id == documentId)).CurrentRevisionNumber);
    }

    [Fact]
    public async Task Integrity_check_reports_ready_corrupt_length_mismatch_and_missing_without_canonical_mutation()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var editorId = await CreateUser(factory, AccessLevel.Editor, "Attachment integrity editor");
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Attachment integrity");
        var documentId = document.GetProperty("id").GetInt64();
        var bytes = "integrity bytes"u8.ToArray();
        var upload = await UploadJson(editor, documentId, "integrity.txt", "text/plain", bytes);
        var attachmentId = upload.GetProperty("attachmentId").GetInt64();
        var path = await StoredPath(factory, attachmentId);
        var recordedSha = upload.GetProperty("sha256").GetString();

        Assert.Equal("Ready", (await CheckIntegrity(administrator, attachmentId)).GetProperty("status").GetString());
        await File.WriteAllBytesAsync(path, RandomNumberGenerator.GetBytes(bytes.Length));
        Assert.Equal("Corrupt", (await CheckIntegrity(administrator, attachmentId)).GetProperty("status").GetString());
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        Assert.Equal("LengthMismatch", (await CheckIntegrity(administrator, attachmentId)).GetProperty("status").GetString());
        File.Delete(path);
        var missing = await CheckIntegrity(administrator, attachmentId);
        Assert.Equal("Missing", missing.GetProperty("status").GetString());
        Assert.DoesNotContain("storageKey", missing.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(factory.AttachmentStorageRoot, missing.ToString(), StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var attachment = await db.Attachments.AsNoTracking().SingleAsync(item => item.Id == attachmentId);
        Assert.Equal(recordedSha, Convert.ToHexString(attachment.Sha256).ToLowerInvariant());
        Assert.Equal(AttachmentStorageState.Ready, attachment.StorageState);
    }

    private static async Task AssertFilter(
        HttpClient administrator,
        string query,
        long includedId,
        IReadOnlyCollection<long> excludedIds)
    {
        using var response = await administrator.GetAsync($"/api/admin/attachments?{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadFromJsonAsync<JsonElement>();
        var ids = json.GetProperty("items").EnumerateArray().Select(item => item.GetProperty("attachmentId").GetInt64()).ToArray();
        Assert.Contains(includedId, ids);
        foreach (var excludedId in excludedIds) Assert.DoesNotContain(excludedId, ids);
    }

    private static async Task<long> CreateUser(
        BootstrapWebApplicationFactory factory,
        AccessLevel accessLevel,
        string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = displayName,
            EmployeeNo = $"ATTACH-B04-{Guid.NewGuid():N}",
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<JsonElement> CreateDocument(HttpClient client, string title)
    {
        using var response = await client.PostAsJsonAsync("/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle",
            title,
            summary = (string?)null,
            bodyMarkdown = "body",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> SaveContent(
        HttpClient client,
        long documentId,
        string title,
        string bodyMarkdown,
        string concurrencyToken,
        IReadOnlyList<long> fileAttachmentIds)
    {
        using var response = await client.PutAsJsonAsync($"/api/knowledge-documents/{documentId}/content", new
        {
            title,
            summary = (string?)null,
            bodyMarkdown,
            changeSummary = "ATTACH-B04 fixture",
            concurrencyToken,
            fileAttachmentIds,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> UploadJson(
        HttpClient client,
        long documentId,
        string fileName,
        string contentType,
        byte[] bytes)
    {
        using var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = System.Net.Http.Headers.MediaTypeHeaderValue.Parse(contentType);
        using var multipart = new MultipartFormDataContent();
        multipart.Add(fileContent, "file", fileName);
        using var response = await client.PostAsync($"/api/knowledge-documents/{documentId}/attachments", multipart);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> ReadDetail(HttpClient client, long attachmentId)
    {
        using var response = await client.GetAsync($"/api/admin/attachments/{attachmentId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static Task<HttpResponseMessage> Delete(HttpClient client, long attachmentId, string token) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/attachments/{attachmentId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = token }),
        });

    private static async Task<JsonElement> CheckIntegrity(HttpClient client, long attachmentId)
    {
        using var response = await client.PostAsync($"/api/admin/attachments/{attachmentId}/integrity-check", null);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<string> StoredPath(BootstrapWebApplicationFactory factory, long attachmentId)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var attachment = await db.Attachments.AsNoTracking().SingleAsync(item => item.Id == attachmentId);
        return Path.Combine(factory.AttachmentStorageRoot, attachment.StorageKey.Replace('/', Path.DirectorySeparatorChar));
    }

    private static byte[] BuildPng()
    {
        var png = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        png[11] = 13;
        Encoding.ASCII.GetBytes("IHDR").CopyTo(png, 12);
        png[19] = 1;
        png[23] = 1;
        return png;
    }

    private static byte[] BuildZip()
    {
        using var result = new MemoryStream();
        using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            var entry = archive.CreateEntry("readme.txt");
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write("orphan archive");
        }
        return result.ToArray();
    }
}
