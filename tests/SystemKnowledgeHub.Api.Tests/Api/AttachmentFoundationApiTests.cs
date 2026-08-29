using System.IO.Compression;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit.Abstractions;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AttachmentFoundationApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly ITestOutputHelper _output;

    public AttachmentFoundationApiTests(
        BootstrapWebApplicationFactory factory,
        ITestOutputHelper output)
    {
        _factory = factory;
        _output = output;
    }

    [Fact]
    public async Task Upload_is_streamed_validated_orphaned_and_role_bounded()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Attachment upload editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Attachment upload");
        var documentId = document.GetProperty("id").GetInt64();
        var pdfBytes = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj\n<<>>\nendobj\n%%EOF");

        using var uploadedResponse = await Upload(editor, documentId, "架构说明.pdf", "application/pdf", pdfBytes);
        Assert.Equal(HttpStatusCode.Created, uploadedResponse.StatusCode);
        var uploaded = (await uploadedResponse.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        var attachmentId = uploaded.GetProperty("attachmentId").GetInt64();
        Assert.Equal("File", uploaded.GetProperty("kind").GetString());
        Assert.Equal("application/pdf", uploaded.GetProperty("contentType").GetString());
        Assert.Equal("Pdf", uploaded.GetProperty("previewMode").GetString());
        Assert.True(uploaded.GetProperty("canPreview").GetBoolean());
        Assert.False(uploaded.GetProperty("canDownload").GetBoolean());
        Assert.Equal(Convert.ToHexString(SHA256.HashData(pdfBytes)).ToLowerInvariant(), uploaded.GetProperty("sha256").GetString());
        Assert.DoesNotContain("storage", uploaded.ToString(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(_factory.AttachmentStorageRoot, uploaded.ToString(), StringComparison.OrdinalIgnoreCase);

        using var orphanDownload = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{attachmentId}/download");
        Assert.Equal(HttpStatusCode.NotFound, orphanDownload.StatusCode);

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var entity = await db.Attachments.AsNoTracking().SingleAsync(item => item.Id == attachmentId);
            Assert.Equal(AttachmentStorageState.Ready, entity.StorageState);
            Assert.Equal(0, await db.AttachmentReferences.CountAsync(reference => reference.AttachmentId == attachmentId));
            Assert.DoesNotContain(entity.OriginalFileName, entity.StorageKey, StringComparison.Ordinal);
            Assert.DoesNotContain("..", entity.StorageKey, StringComparison.Ordinal);
            Assert.True(File.Exists(Path.Combine(_factory.AttachmentStorageRoot, entity.StorageKey.Replace('/', Path.DirectorySeparatorChar))));
        }

        var png = new byte[24];
        new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 }.CopyTo(png, 0);
        png[11] = 13;
        Encoding.ASCII.GetBytes("IHDR").CopyTo(png, 12);
        png[19] = 1;
        png[23] = 1;
        using var imageResponse = await Upload(editor, documentId, "diagram.png", "image/png", png);
        Assert.Equal(HttpStatusCode.Created, imageResponse.StatusCode);
        var image = await imageResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Image", image.GetProperty("kind").GetString());
        Assert.Equal("Image", image.GetProperty("previewMode").GetString());

        using var empty = await Upload(editor, documentId, "empty.txt", "text/plain", []);
        Assert.Equal(HttpStatusCode.BadRequest, empty.StatusCode);
        using var forbidden = await Upload(editor, documentId, "script.svg", "image/svg+xml", "<svg/>"u8.ToArray());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, forbidden.StatusCode);
        using var mismatch = await Upload(editor, documentId, "wrong.pdf", "image/png", pdfBytes);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, mismatch.StatusCode);
        using var badSignature = await Upload(editor, documentId, "wrong.pdf", "application/pdf", "not a pdf"u8.ToArray());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, badSignature.StatusCode);
        using var invalidUtf8 = await Upload(editor, documentId, "invalid.txt", "text/plain", [0xff, 0xfe, 0x00]);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, invalidUtf8.StatusCode);
        using var unsafeName = await Upload(editor, documentId, "../escape.pdf", "application/pdf", pdfBytes);
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, unsafeName.StatusCode);

        var viewerId = await CreateUser(AccessLevel.Viewer, "Attachment viewer");
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);
        using var viewerUpload = await Upload(viewer, documentId, "viewer.txt", "text/plain", "denied"u8.ToArray());
        Assert.Equal(HttpStatusCode.Forbidden, viewerUpload.StatusCode);

        var published = await ChangeLifecycle(editor, documentId, "Published", uploaded: document);
        var archived = await ChangeLifecycle(editor, documentId, "Archived", uploaded: published);
        Assert.Equal("Archived", archived.GetProperty("lifecycleStatus").GetString());
        using var archivedUpload = await Upload(editor, documentId, "archived.txt", "text/plain", "denied"u8.ToArray());
        Assert.Equal(HttpStatusCode.Conflict, archivedUpload.StatusCode);
    }

    [Fact]
    public async Task Real_png_and_jpeg_multipart_preserve_bytes_through_antiforgery_form_and_staging()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Attachment binary editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Attachment binary round trip");
        var documentId = document.GetProperty("id").GetInt64();

        await AssertImageMultipartRoundTrip(
            editor,
            documentId,
            "真实图片.png",
            "image/png",
            Convert.FromBase64String(RealPngBase64),
            "89 50 4E 47 0D 0A 1A 0A 00 00 00 0D 49 48 44 52 00 00 00 01 00 00 00 01");
        await AssertImageMultipartRoundTrip(
            editor,
            documentId,
            "真实照片.jpg",
            "image/jpeg",
            Convert.FromBase64String(RealJpegBase64),
            "FF D8 FF E0 00 10 4A 46 49 46 00 01 01 01 00 60 00 60 00 00 FF DB 00 43");

        using var mislabeled = await Upload(
            editor,
            documentId,
            "微信截图_20260201110642.png",
            "image/png",
            Convert.FromBase64String(RealJpegBase64));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, mislabeled.StatusCode);
        var error = await mislabeled.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Contains(
            "PNG 文件头无效。",
            error.GetProperty("fieldErrors").GetProperty("file")[0].GetString(),
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task Semantic_save_projects_metadata_and_secure_current_preview_download_matrix()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Attachment preview editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Attachment preview");
        var documentId = document.GetProperty("id").GetInt64();

        var pdf = await UploadJson(editor, documentId, "preview.pdf", "application/pdf", "%PDF-1.4\n%%EOF"u8.ToArray());
        var longText = new string('文', 100_000);
        var text = await UploadJson(editor, documentId, "notes.txt", "text/plain", Encoding.UTF8.GetBytes(longText));
        var csvContent = BuildCsv();
        var csv = await UploadJson(editor, documentId, "table.csv", "text/csv", Encoding.UTF8.GetBytes(csvContent));
        var xlsx = await UploadJson(editor, documentId, "sheet.xlsx", XlsxContentType, BuildXlsx());
        var zip = await UploadJson(editor, documentId, "source.zip", "application/zip", BuildZip());
        var ids = new[] { pdf, text, csv, xlsx, zip }.Select(item => item.GetProperty("attachmentId").GetInt64()).ToArray();
        var pdfId = ids[0];
        var textId = ids[1];
        var csvId = ids[2];
        var xlsxId = ids[3];
        var zipId = ids[4];

        var saved = await SaveContent(
            editor,
            documentId,
            "Attachment preview",
            "body",
            document.GetProperty("concurrencyToken").GetString()!,
            ids);
        Assert.Equal(2, saved.GetProperty("currentRevisionNumber").GetInt64());
        var metadata = saved.GetProperty("attachmentReferences").EnumerateArray().ToArray();
        Assert.Equal(5, metadata.Length);
        Assert.Equal("Pdf", FindMetadata(metadata, pdf).GetProperty("previewMode").GetString());
        Assert.Equal("Text", FindMetadata(metadata, text).GetProperty("previewMode").GetString());
        Assert.Equal("Csv", FindMetadata(metadata, csv).GetProperty("previewMode").GetString());
        Assert.Equal("Spreadsheet", FindMetadata(metadata, xlsx).GetProperty("previewMode").GetString());
        Assert.Equal("None", FindMetadata(metadata, zip).GetProperty("previewMode").GetString());
        Assert.All(metadata, item => Assert.True(item.GetProperty("canDownload").GetBoolean()));

        using (var retainResponse = await editor.PutAsJsonAsync($"/api/knowledge-documents/{documentId}/content", new
        {
            title = "Attachment preview",
            summary = (string?)null,
            bodyMarkdown = "body changed without fileAttachmentIds",
            changeSummary = "retain ordinary attachments",
            concurrencyToken = saved.GetProperty("concurrencyToken").GetString(),
        }))
        {
            Assert.Equal(HttpStatusCode.OK, retainResponse.StatusCode);
            saved = (await retainResponse.Content.ReadFromJsonAsync<JsonElement>()).Clone();
            Assert.Equal(5, saved.GetProperty("attachmentReferences").GetArrayLength());
        }

        using (var pdfPreview = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{pdfId}/preview"))
        {
            Assert.Equal(HttpStatusCode.OK, pdfPreview.StatusCode);
            Assert.Equal("application/pdf", pdfPreview.Content.Headers.ContentType?.MediaType);
            Assert.StartsWith("inline", pdfPreview.Content.Headers.ContentDisposition?.DispositionType, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("nosniff", Header(pdfPreview, "X-Content-Type-Options"));
            Assert.Contains("private", Header(pdfPreview, "Cache-Control"));
            Assert.DoesNotContain(_factory.AttachmentStorageRoot, pdfPreview.Headers.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        using (var pdfDownload = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{pdfId}/download"))
        {
            Assert.Equal(HttpStatusCode.OK, pdfDownload.StatusCode);
            Assert.StartsWith("attachment", pdfDownload.Content.Headers.ContentDisposition?.DispositionType, StringComparison.OrdinalIgnoreCase);
            Assert.Equal("application/pdf", pdfDownload.Content.Headers.ContentType?.MediaType);
        }
        using (var textPreview = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{textId}/preview"))
        {
            Assert.Equal(HttpStatusCode.OK, textPreview.StatusCode);
            var body = await textPreview.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Text", body.GetProperty("mode").GetString());
            Assert.True(body.GetProperty("truncated").GetBoolean());
            Assert.True(body.GetProperty("returnedBytes").GetInt32() <= AttachmentOptions.DefaultPreviewTextMaxBytes);
            Assert.DoesNotContain("<html", body.GetProperty("text").GetString(), StringComparison.OrdinalIgnoreCase);
        }
        using (var csvPreview = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{csvId}/preview"))
        {
            Assert.Equal(HttpStatusCode.OK, csvPreview.StatusCode);
            var body = await csvPreview.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(AttachmentOptions.DefaultPreviewCsvMaxRows, body.GetProperty("rows").GetArrayLength());
            Assert.True(body.GetProperty("truncated").GetBoolean());
            Assert.Contains("Rows", body.GetProperty("truncationReasons").EnumerateArray().Select(item => item.GetString()));
            Assert.Contains("Columns", body.GetProperty("truncationReasons").EnumerateArray().Select(item => item.GetString()));
            Assert.Equal("<script>alert(1)</script>", body.GetProperty("rows")[0][0].GetString());
        }
        using (var xlsxPreview = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{xlsxId}/preview?sheet=Data"))
        {
            Assert.Equal(HttpStatusCode.OK, xlsxPreview.StatusCode);
            var body = await xlsxPreview.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("Spreadsheet", body.GetProperty("mode").GetString());
            Assert.Equal("Data", body.GetProperty("selectedSheet").GetString());
            var cells = body.GetProperty("rows")[0].GetProperty("cells");
            Assert.Equal("Header", cells[0].GetString());
            Assert.Equal("2", cells[1].GetString());
            Assert.DoesNotContain("1+1", body.ToString(), StringComparison.Ordinal);
        }
        using (var noPreview = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{zipId}/preview"))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, noPreview.StatusCode);
            Assert.Equal("preview_not_supported", (await noPreview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        using (var zipDownload = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{zipId}/download"))
        {
            Assert.Equal(HttpStatusCode.OK, zipDownload.StatusCode);
            Assert.Equal("application/zip", zipDownload.Content.Headers.ContentType?.MediaType);
        }

        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{pdfId}/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{pdfId}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await editor.GetAsync($"/api/knowledge-documents/{documentId + 1}/attachments/{pdfId}/download")).StatusCode);
    }

    [Fact]
    public async Task Markdown_image_tokens_create_immutable_revision_references_and_use_only_the_image_content_route()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Attachment image editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Attachment image");
        var documentId = document.GetProperty("id").GetInt64();
        var image = await UploadJson(editor, documentId, "diagram.png", "image/png", BuildPng());
        var imageId = image.GetProperty("attachmentId").GetInt64();
        var ordinary = await UploadJson(editor, documentId, "ordinary.txt", "text/plain", "ordinary"u8.ToArray());
        var ordinaryId = ordinary.GetProperty("attachmentId").GetInt64();

        using (var wrongKind = await editor.PutAsJsonAsync($"/api/knowledge-documents/{documentId}/content", new
        {
            title = "Attachment image",
            summary = (string?)null,
            bodyMarkdown = $"![wrong](attachment:{ordinaryId})",
            concurrencyToken = document.GetProperty("concurrencyToken").GetString(),
            fileAttachmentIds = Array.Empty<long>(),
        }))
        {
            Assert.Equal(HttpStatusCode.BadRequest, wrongKind.StatusCode);
        }

        var attached = await SaveContent(
            editor,
            documentId,
            "Attachment image",
            $"![diagram](attachment:{imageId})",
            document.GetProperty("concurrencyToken").GetString()!,
            []);
        Assert.Single(attached.GetProperty("attachmentReferences").EnumerateArray());
        Assert.Equal("Image", attached.GetProperty("attachmentReferences")[0].GetProperty("previewMode").GetString());
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{imageId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{imageId}/preview")).StatusCode);

        var removed = await SaveContent(
            editor,
            documentId,
            "Attachment image",
            "image removed from current body",
            attached.GetProperty("concurrencyToken").GetString()!,
            []);
        Assert.Empty(removed.GetProperty("attachmentReferences").EnumerateArray());
        Assert.Equal(HttpStatusCode.NotFound, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{imageId}/content")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/revisions/2/attachments/{imageId}/content")).StatusCode);
    }

    [Fact]
    public async Task Docx_pptx_and_zip_are_download_only_with_server_authoritative_none_capability()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Download-only editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Download-only attachments");
        var documentId = document.GetProperty("id").GetInt64();
        var docx = await UploadJson(editor, documentId, "manual.docx", DocxContentType, BuildOoxml("docx"));
        var pptx = await UploadJson(editor, documentId, "slides.pptx", PptxContentType, BuildOoxml("pptx"));
        var zip = await UploadJson(editor, documentId, "source.zip", "application/zip", BuildZip());
        var ids = new[] { docx, pptx, zip }.Select(item => item.GetProperty("attachmentId").GetInt64()).ToArray();
        Assert.All(new[] { docx, pptx, zip }, item =>
        {
            Assert.Equal("None", item.GetProperty("previewMode").GetString());
            Assert.False(item.GetProperty("canPreview").GetBoolean());
        });
        await SaveContent(editor, documentId, "Download-only attachments", "body", document.GetProperty("concurrencyToken").GetString()!, ids);
        foreach (var id in ids)
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{id}/preview")).StatusCode);
            Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{id}/download")).StatusCode);
        }
    }

    [Fact]
    public async Task Xlsx_upload_rejects_macros_and_invalid_packages_and_preview_refuses_oversized_workbooks()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "XLSX validation editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "XLSX validation");
        var documentId = document.GetProperty("id").GetInt64();

        using var macro = await Upload(editor, documentId, "macro.xlsx", XlsxContentType, BuildXlsx(macroEnabled: true));
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, macro.StatusCode);
        using var invalid = await Upload(editor, documentId, "invalid.xlsx", XlsxContentType, BuildZip());
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, invalid.StatusCode);

        var oversizedBytes = BuildXlsx(extraBytes: 11 * 1024 * 1024);
        var oversized = await UploadJson(editor, documentId, "large.xlsx", XlsxContentType, oversizedBytes);
        var attachmentId = oversized.GetProperty("attachmentId").GetInt64();
        var saved = await SaveContent(
            editor,
            documentId,
            "XLSX validation",
            "body",
            document.GetProperty("concurrencyToken").GetString()!,
            [attachmentId]);
        Assert.Equal(2, saved.GetProperty("currentRevisionNumber").GetInt64());
        using var preview = await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{attachmentId}/preview");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, preview.StatusCode);
        Assert.Equal("preview_limit_exceeded", (await preview.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{attachmentId}/download")).StatusCode);
    }

    [Fact]
    public async Task Revision_snapshot_restore_and_soft_delete_preserve_exact_historical_delivery()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Attachment history editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Attachment history");
        var documentId = document.GetProperty("id").GetInt64();
        var uploaded = await UploadJson(editor, documentId, "history.txt", "text/plain", "historical attachment"u8.ToArray());
        var attachmentId = uploaded.GetProperty("attachmentId").GetInt64();

        var attached = await SaveContent(editor, documentId, "Attachment history", "body", document.GetProperty("concurrencyToken").GetString()!, [attachmentId]);
        var removed = await SaveContent(editor, documentId, "Attachment history", "body", attached.GetProperty("concurrencyToken").GetString()!, []);
        Assert.Equal(3, removed.GetProperty("currentRevisionNumber").GetInt64());
        Assert.Equal(HttpStatusCode.NotFound, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{attachmentId}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/revisions/2/attachments/{attachmentId}/download")).StatusCode);

        string storedPath;
        byte[] storedBytes;
        await using (var storageScope = _factory.Services.CreateAsyncScope())
        {
            var storageDb = storageScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var entity = await storageDb.Attachments.AsNoTracking().SingleAsync(item => item.Id == attachmentId);
            storedPath = Path.Combine(_factory.AttachmentStorageRoot, entity.StorageKey.Replace('/', Path.DirectorySeparatorChar));
            storedBytes = await File.ReadAllBytesAsync(storedPath);
        }
        File.Delete(storedPath);
        using (var unavailableRestore = await editor.PostAsJsonAsync($"/api/knowledge-documents/{documentId}/revisions/2/restore", new
        {
            concurrencyToken = removed.GetProperty("concurrencyToken").GetString(),
            reason = "验证附件缺失失败",
        }))
        {
            Assert.Equal(HttpStatusCode.Conflict, unavailableRestore.StatusCode);
            Assert.Equal("attachment_unavailable", (await unavailableRestore.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        await File.WriteAllBytesAsync(storedPath, storedBytes);
        await using (var unchangedScope = _factory.Services.CreateAsyncScope())
        {
            var unchangedDb = unchangedScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.Equal(3, (await unchangedDb.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == documentId)).CurrentRevisionNumber);
        }

        using var restoreResponse = await editor.PostAsJsonAsync($"/api/knowledge-documents/{documentId}/revisions/2/restore", new
        {
            concurrencyToken = removed.GetProperty("concurrencyToken").GetString(),
            reason = "恢复附件历史快照",
        });
        Assert.Equal(HttpStatusCode.OK, restoreResponse.StatusCode);
        var restored = (await restoreResponse.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.Equal(4, restored.GetProperty("currentRevisionNumber").GetInt64());
        Assert.Single(restored.GetProperty("attachmentReferences").EnumerateArray());
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{attachmentId}/download")).StatusCode);

        var removedAgain = await SaveContent(editor, documentId, "Attachment history", "body after restore", restored.GetProperty("concurrencyToken").GetString()!, []);
        using var delete = await editor.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/knowledge-documents/{documentId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = removedAgain.GetProperty("concurrencyToken").GetString() }),
        });
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/attachments/{attachmentId}/download")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/revisions/2/attachments/{attachmentId}/preview")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await editor.GetAsync($"/api/knowledge-documents/{documentId}/revisions/4/attachments/{attachmentId}/download")).StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(2, await db.AttachmentReferences.CountAsync(reference => reference.AttachmentId == attachmentId));
    }

    [Fact]
    public async Task Administrator_delete_is_zero_reference_only_concurrent_and_removes_metadata_and_binary()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Attachment delete editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        using var administrator = _factory.CreateAuthenticatedClient();
        var document = await CreateDocument(editor, "Attachment delete");
        var documentId = document.GetProperty("id").GetInt64();
        var orphan = await UploadJson(editor, documentId, "orphan.zip", "application/zip", BuildZip());
        var orphanId = orphan.GetProperty("attachmentId").GetInt64();
        string physicalPath;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var entity = await db.Attachments.AsNoTracking().SingleAsync(item => item.Id == orphanId);
            physicalPath = Path.Combine(_factory.AttachmentStorageRoot, entity.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        }
        Assert.True(File.Exists(physicalPath));
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync($"/api/admin/attachments/{orphanId}")).StatusCode);
        using var adminDetail = await administrator.GetAsync($"/api/admin/attachments/{orphanId}");
        Assert.Equal(HttpStatusCode.OK, adminDetail.StatusCode);
        var metadata = (await adminDetail.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.Equal(0, metadata.GetProperty("referenceCount").GetInt32());
        Assert.DoesNotContain("storageKey", metadata.ToString(), StringComparison.OrdinalIgnoreCase);
        using var delete = await administrator.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/attachments/{orphanId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = metadata.GetProperty("concurrencyToken").GetString() }),
        });
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.False(File.Exists(physicalPath));

        var referenced = await UploadJson(editor, documentId, "referenced.zip", "application/zip", BuildZip());
        var referencedId = referenced.GetProperty("attachmentId").GetInt64();
        await SaveContent(editor, documentId, "Attachment delete", "body", document.GetProperty("concurrencyToken").GetString()!, [referencedId]);
        using var rejectedDelete = await administrator.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/attachments/{referencedId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = referenced.GetProperty("concurrencyToken").GetString() }),
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, rejectedDelete.StatusCode);

        var retryOrphan = await UploadJson(editor, documentId, "retry.txt", "text/plain", "delete retry"u8.ToArray());
        var retryId = retryOrphan.GetProperty("attachmentId").GetInt64();
        string retryPath;
        await using (var pathScope = _factory.Services.CreateAsyncScope())
        {
            var pathDb = pathScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var retryEntity = await pathDb.Attachments.AsNoTracking().SingleAsync(item => item.Id == retryId);
            retryPath = Path.Combine(_factory.AttachmentStorageRoot, retryEntity.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        }
        using (var exclusiveLock = new FileStream(retryPath, FileMode.Open, FileAccess.Read, FileShare.None))
        using (var failedDelete = await administrator.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/attachments/{retryId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = retryOrphan.GetProperty("concurrencyToken").GetString() }),
        }))
        {
            Assert.Equal(HttpStatusCode.ServiceUnavailable, failedDelete.StatusCode);
        }
        await using (var pendingScope = _factory.Services.CreateAsyncScope())
        {
            var pendingDb = pendingScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.Equal(AttachmentStorageState.DeletePending, (await pendingDb.Attachments.AsNoTracking().SingleAsync(item => item.Id == retryId)).StorageState);
        }
        using (var staleRetry = await administrator.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/attachments/{retryId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = retryOrphan.GetProperty("concurrencyToken").GetString() }),
        }))
        {
            Assert.Equal(HttpStatusCode.Conflict, staleRetry.StatusCode);
        }
        using var retryDetail = await administrator.GetAsync($"/api/admin/attachments/{retryId}");
        var retryMetadata = (await retryDetail.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        using var successfulRetry = await administrator.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/attachments/{retryId}")
        {
            Content = JsonContent.Create(new { concurrencyToken = retryMetadata.GetProperty("concurrencyToken").GetString() }),
        });
        Assert.Equal(HttpStatusCode.NoContent, successfulRetry.StatusCode);
        Assert.False(File.Exists(retryPath));

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await verificationDb.Attachments.AnyAsync(item => item.Id == orphanId));
        Assert.True(await verificationDb.Attachments.AnyAsync(item => item.Id == referencedId));
        var reference = await verificationDb.AttachmentReferences.SingleAsync(item => item.AttachmentId == referencedId);
        reference.KnowledgeDocumentId++;
        await Assert.ThrowsAsync<InvalidOperationException>(() => verificationDb.SaveChangesAsync());
    }

    [Fact]
    public async Task Storage_staging_enforces_exact_limit_and_cleans_partial_files()
    {
        var root = Path.Combine(Path.GetTempPath(), "ATTACH-B01-storage", Guid.NewGuid().ToString("N"));
        try
        {
            var storage = new AttachmentStorage(
                new AttachmentOptions { StorageRoot = root },
                NullLogger<AttachmentStorage>.Instance);
            await Assert.ThrowsAsync<AttachmentPayloadTooLargeException>(() => storage.Stage(
                new MemoryStream([1, 2, 3, 4, 5]),
                4,
                CancellationToken.None));
            Assert.Empty(Directory.GetFiles(Path.Combine(root, "staging")));
        }
        finally
        {
            if (Directory.Exists(root)) Directory.Delete(root, recursive: true);
        }
    }

    private async Task<long> CreateUser(AccessLevel accessLevel, string displayName)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = displayName,
            EmployeeNo = $"ATTACH-{Guid.NewGuid():N}",
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

    private static async Task<JsonElement> ChangeLifecycle(
        HttpClient client,
        long documentId,
        string target,
        JsonElement uploaded)
    {
        using var response = await client.PutAsJsonAsync($"/api/knowledge-documents/{documentId}/lifecycle", new
        {
            targetLifecycleStatus = target,
            concurrencyToken = uploaded.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> SaveContent(
        HttpClient client,
        long documentId,
        string title,
        string body,
        string token,
        IReadOnlyList<long> fileAttachmentIds)
    {
        using var response = await client.PutAsJsonAsync($"/api/knowledge-documents/{documentId}/content", new
        {
            title,
            summary = (string?)null,
            bodyMarkdown = body,
            changeSummary = "attachment snapshot",
            concurrencyToken = token,
            fileAttachmentIds,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task AssertImageMultipartRoundTrip(
        HttpClient client,
        long documentId,
        string fileName,
        string contentType,
        byte[] originalBytes,
        string expectedFirst24)
    {
        Assert.Equal(expectedFirst24, HexPrefix(originalBytes));
        var expectedHash = Convert.ToHexString(SHA256.HashData(originalBytes));
        using var fileContent = new ByteArrayContent(originalBytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        using var multipart = new MultipartFormDataContent($"----WebKitFormBoundary{Guid.NewGuid():N}");
        multipart.Add(fileContent, "file", fileName);
        await multipart.LoadIntoBufferAsync();
        var requestContentLength = multipart.Headers.ContentLength;
        Assert.NotNull(requestContentLength);
        Assert.True(requestContentLength > originalBytes.LongLength);

        using var response = await client.PostAsync(
            $"/api/knowledge-documents/{documentId}/attachments",
            multipart);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var formObservation = _factory.Services
            .GetRequiredService<MultipartUploadCapture>()
            .GetRequired(fileName);
        Assert.Equal(requestContentLength, formObservation.RequestContentLength);
        Assert.Equal(originalBytes.LongLength, formObservation.FormFileLength);
        Assert.Equal(expectedFirst24, HexPrefix(formObservation.First24Bytes));
        Assert.Equal(expectedHash, Convert.ToHexString(formObservation.Sha256));
        var metadata = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        var attachmentId = metadata.GetProperty("attachmentId").GetInt64();
        Assert.Equal(originalBytes.LongLength, metadata.GetProperty("sizeBytes").GetInt64());
        Assert.Equal(expectedHash.ToLowerInvariant(), metadata.GetProperty("sha256").GetString());

        string storedPath;
        long stagingSize;
        byte[] stagingHash;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var attachment = await db.Attachments.AsNoTracking().SingleAsync(item => item.Id == attachmentId);
            stagingSize = attachment.SizeBytes;
            stagingHash = attachment.Sha256;
            storedPath = Path.Combine(
                _factory.AttachmentStorageRoot,
                attachment.StorageKey.Replace('/', Path.DirectorySeparatorChar));
        }

        var storedBytes = await File.ReadAllBytesAsync(storedPath);
        Assert.Equal(originalBytes.LongLength, stagingSize);
        Assert.Equal(originalBytes, storedBytes);
        Assert.Equal(expectedFirst24, HexPrefix(storedBytes));
        Assert.Equal(expectedHash, Convert.ToHexString(stagingHash));
        Assert.Equal(expectedHash, Convert.ToHexString(SHA256.HashData(storedBytes)));
        Assert.Empty(Directory.EnumerateFiles(
            Path.Combine(_factory.AttachmentStorageRoot, "staging"),
            "*",
            SearchOption.AllDirectories));

        _output.WriteLine(
            "{0}: request Content-Length={1}; IFormFile.Length={2}; staging SizeBytes={3}; SHA-256={4}; first24={5}",
            fileName,
            formObservation.RequestContentLength,
            formObservation.FormFileLength,
            stagingSize,
            expectedHash,
            expectedFirst24);
    }

    private static string HexPrefix(byte[] bytes) => string.Join(
        ' ',
        bytes.Take(24).Select(value => value.ToString("X2")));

    private static async Task<HttpResponseMessage> Upload(
        HttpClient client,
        long documentId,
        string fileName,
        string contentType,
        byte[] bytes)
    {
        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        var multipart = new MultipartFormDataContent();
        multipart.Add(fileContent, "file", fileName);
        var response = await client.PostAsync($"/api/knowledge-documents/{documentId}/attachments", multipart);
        multipart.Dispose();
        return response;
    }

    private static async Task<JsonElement> UploadJson(
        HttpClient client,
        long documentId,
        string fileName,
        string contentType,
        byte[] bytes)
    {
        using var response = await Upload(client, documentId, fileName, contentType, bytes);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static JsonElement FindMetadata(IEnumerable<JsonElement> items, JsonElement upload)
    {
        var id = upload.GetProperty("attachmentId").GetInt64();
        return items.Single(item => item.GetProperty("attachmentId").GetInt64() == id);
    }

    private static string Header(HttpResponseMessage response, string name) =>
        response.Headers.TryGetValues(name, out var responseValues)
            ? string.Join(",", responseValues)
            : response.Content.Headers.TryGetValues(name, out var contentValues)
                ? string.Join(",", contentValues)
                : string.Empty;

    private static string BuildCsv()
    {
        var builder = new StringBuilder();
        for (var row = 0; row < 205; row++)
        {
            for (var column = 0; column < 52; column++)
            {
                if (column > 0) builder.Append(',');
                builder.Append(row == 0 && column == 0 ? "<script>alert(1)</script>" : $"r{row}c{column}");
            }
            builder.AppendLine();
        }
        return builder.ToString();
    }

    private static byte[] BuildZip()
    {
        using var result = new MemoryStream();
        using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "readme.txt", "safe archive");
        }
        return result.ToArray();
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

    private static byte[] BuildOoxml(string kind)
    {
        var (mainPath, mainType) = kind switch
        {
            "docx" => ("word/document.xml", "application/vnd.openxmlformats-officedocument.wordprocessingml.document.main+xml"),
            "pptx" => ("ppt/presentation.xml", "application/vnd.openxmlformats-officedocument.presentationml.presentation.main+xml"),
            _ => throw new ArgumentOutOfRangeException(nameof(kind)),
        };
        using var result = new MemoryStream();
        using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "[Content_Types].xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Override PartName="/{mainPath}" ContentType="{mainType}" />
                </Types>
                """);
            WriteEntry(archive, mainPath, "<?xml version=\"1.0\" encoding=\"UTF-8\"?><root />");
        }
        return result.ToArray();
    }

    private static byte[] BuildXlsx(bool macroEnabled = false, int extraBytes = 0)
    {
        using var result = new MemoryStream();
        using (var archive = new ZipArchive(result, ZipArchiveMode.Create, leaveOpen: true))
        {
            var workbookType = macroEnabled
                ? "application/vnd.ms-excel.sheet.macroEnabled.main+xml"
                : "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml";
            WriteEntry(archive, "[Content_Types].xml", $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <Types xmlns="http://schemas.openxmlformats.org/package/2006/content-types">
                  <Override PartName="/xl/workbook.xml" ContentType="{workbookType}" />
                  <Override PartName="/xl/worksheets/sheet1.xml" ContentType="application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml" />
                </Types>
                """);
            WriteEntry(archive, "xl/workbook.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
                  <sheets><sheet name="Data" sheetId="1" r:id="rId1" /></sheets>
                </workbook>
                """);
            WriteEntry(archive, "xl/_rels/workbook.xml.rels", """
                <?xml version="1.0" encoding="UTF-8"?>
                <Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
                  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
                </Relationships>
                """);
            WriteEntry(archive, "xl/sharedStrings.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="1" uniqueCount="1"><si><t>Header</t></si></sst>
                """);
            WriteEntry(archive, "xl/worksheets/sheet1.xml", """
                <?xml version="1.0" encoding="UTF-8"?>
                <worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main"><sheetData>
                  <row r="1"><c r="A1" t="s"><v>0</v></c><c r="B1"><f>1+1</f><v>2</v></c></row>
                </sheetData></worksheet>
                """);
            if (extraBytes > 0)
            {
                var entry = archive.CreateEntry("xl/media/random.bin", CompressionLevel.NoCompression);
                using var stream = entry.Open();
                stream.Write(RandomNumberGenerator.GetBytes(extraBytes));
            }
        }
        return result.ToArray();
    }

    private static void WriteEntry(ZipArchive archive, string path, string content)
    {
        var entry = archive.CreateEntry(path, CompressionLevel.Optimal);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
        writer.Write(content);
    }

    private const string XlsxContentType = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";
    private const string DocxContentType = "application/vnd.openxmlformats-officedocument.wordprocessingml.document";
    private const string PptxContentType = "application/vnd.openxmlformats-officedocument.presentationml.presentation";
    private const string RealPngBase64 = "iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mNk+A8AAQUBAScY42YAAAAASUVORK5CYII=";
    private const string RealJpegBase64 = """
        /9j/4AAQSkZJRgABAQEAYABgAAD/2wBDAAMCAgMCAgMDAwMEAwMEBQgFBQQEBQoHBwYIDAoMDAsKCwsNDhIQDQ4RDgsLEBYQERMUFRUVDA8XGBYUGBIUFRT/2wBDAQMEBAUEBQkFBQkUDQsNFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBQUFBT/wAARCAACAAIDASIAAhEBAxEB/8QAHwAAAQUBAQEBAQEAAAAAAAAAAAECAwQFBgcICQoL/8QAtRAAAgEDAwIEAwUFBAQAAAF9AQIDAAQRBRIhMUEGE1FhByJxFDKBkaEII0KxwRVS0fAkM2JyggkKFhcYGRolJicoKSo0NTY3ODk6Q0RFRkdISUpTVFVWV1hZWmNkZWZnaGlqc3R1dnd4eXqDhIWGh4iJipKTlJWWl5iZmqKjpKWmp6ipqrKztLW2t7i5usLDxMXGx8jJytLT1NXW19jZ2uHi4+Tl5ufo6erx8vP09fb3+Pn6/8QAHwEAAwEBAQEBAQEBAQAAAAAAAAECAwQFBgcICQoL/8QAtREAAgECBAQDBAcFBAQAAQJ3AAECAxEEBSExBhJBUQdhcRMiMoEIFEKRobHBCSMzUvAVYnLRChYkNOEl8RcYGRomJygpKjU2Nzg5OkNERUZHSElKU1RVVldYWVpjZGVmZ2hpanN0dXZ3eHl6goOEhYaHiImKkpOUlZaXmJmaoqOkpaanqKmqsrO0tba3uLm6wsPExcbHyMnK0tPU1dbX2Nna4uPk5ebn6Onq8vP09fb3+Pn6/9oADAMBAAIRAxEAPwD7V/Z2+C3w91v9n74ZajqPgTwzf6heeF9MuLm7utHt5JZ5XtImd3dkJZmJJJJySSTRRRXyOL/3ip/if5nwmO/3qr/il+bP/9k=
        """;
}
