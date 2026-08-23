using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class KnowledgeDocumentRestoreApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public KnowledgeDocumentRestoreApiTests(BootstrapWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Draft_restore_creates_trusted_next_revision_and_preserves_document_level_facts()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "REV-B04 Restore Editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var revisionOne = await CreateDocument(editor, "Oracle Listener", "初始摘要", "# Oracle Listener\n\n检查监听服务。");
        var id = revisionOne.GetProperty("id").GetInt64();
        var revisionTwo = await SaveContent(
            editor,
            id,
            "Oracle Listener",
            "监听检查摘要",
            "# Oracle Listener\n\n检查监听服务。\n\n- 检查端口",
            revisionOne.GetProperty("concurrencyToken").GetString()!);

        await AddOrdinaryEvidence(editor, id);
        var inferred = await ChangeStatus(
            editor,
            id,
            "Inferred",
            revisionTwo.GetProperty("concurrencyToken").GetString()!);
        await AddConfirmation(editor, id, 2);
        var confirmed = await ChangeStatus(
            editor,
            id,
            "Confirmed",
            inferred.GetProperty("concurrencyToken").GetString()!);
        var published = await ChangeLifecycle(
            editor,
            id,
            "Published",
            confirmed.GetProperty("concurrencyToken").GetString()!);
        var publishedRevision = await SaveContent(
            editor,
            id,
            "Oracle Listener Published",
            "已发布摘要",
            "# Oracle Listener\n\n已发布的新步骤。",
            published.GetProperty("concurrencyToken").GetString()!);
        var draft = await ChangeLifecycle(
            editor,
            id,
            "Draft",
            publishedRevision.GetProperty("concurrencyToken").GetString()!);

        await AddRelationship(id);
        var before = await ReadPreservedState(id);
        var beforeRequest = DateTimeOffset.UtcNow;
        using var response = await editor.PostAsJsonAsync($"/api/knowledge-documents/{id}/revisions/1/restore", new
        {
            concurrencyToken = draft.GetProperty("concurrencyToken").GetString(),
            reason = "  恢复被误删的处理步骤  ",
            title = "forged title",
            actor = new { userId = 999999, displayName = "Forged Actor" },
            timestamp = "2000-01-01T00:00:00Z",
        });
        var afterRequest = DateTimeOffset.UtcNow;

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var restored = (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.Equal(4, restored.GetProperty("currentRevisionNumber").GetInt64());
        Assert.Equal(3, restored.GetProperty("latestPublishedRevisionNumber").GetInt64());
        Assert.Equal("Draft", restored.GetProperty("lifecycleStatus").GetString());
        Assert.Equal("Confirmed", restored.GetProperty("knowledgeStatus").GetString());
        Assert.Equal("Oracle Listener", restored.GetProperty("title").GetString());
        Assert.Equal("初始摘要", restored.GetProperty("summary").GetString());
        Assert.Equal("# Oracle Listener\n\n检查监听服务。", restored.GetProperty("bodyMarkdown").GetString());
        Assert.NotEqual(draft.GetProperty("concurrencyToken").GetString(), restored.GetProperty("concurrencyToken").GetString());
        Assert.Equal("ChangedSinceConfirmation", restored.GetProperty("confirmationCoverage").GetProperty("state").GetString());
        Assert.Equal(2, restored.GetProperty("confirmationCoverage").GetProperty("lastConfirmedRevisionNumber").GetInt64());

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var head = await dbContext.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == id);
        var revisions = await dbContext.KnowledgeDocumentRevisions.AsNoTracking()
            .Where(item => item.KnowledgeDocumentId == id)
            .OrderBy(item => item.RevisionNumber)
            .ToArrayAsync();
        Assert.Equal([1L, 2L, 3L, 4L], revisions.Select(item => item.RevisionNumber));
        Assert.Equal(RevisionOrigin.Restore, revisions[^1].RevisionOrigin);
        Assert.Equal(1, revisions[^1].RestoredFromRevisionNumber);
        Assert.Equal("恢复被误删的处理步骤", revisions[^1].RestoreReason);
        Assert.Null(revisions[^1].ChangeSummary);
        Assert.Equal(DocumentLifecycleStatus.Draft, revisions[^1].LifecycleContext);
        Assert.Equal(editorId, revisions[^1].AuthorUserId);
        Assert.Equal("REV-B04 Restore Editor", revisions[^1].AuthorDisplayNameSnapshot);
        Assert.InRange(revisions[^1].CreatedAt, beforeRequest, afterRequest);
        Assert.Equal(before.Version + 1, head.Version);
        Assert.Equal(before.LatestPublishedRevisionNumber, head.LatestPublishedRevisionNumber);
        Assert.Equal(before.PublishedAt, head.PublishedAt);
        Assert.Equal(before.KnowledgeStatus, head.KnowledgeStatus);
        Assert.Equal(before.KnowledgeStatusReason, head.KnowledgeStatusReason);
        Assert.Equal(before.KnowledgeStatusChangedAt, head.KnowledgeStatusChangedAt);
        Assert.Equal(before.KnowledgeStatusChangedByName, head.KnowledgeStatusChangedByName);
        Assert.Equal(before.Evidence, await ReadEvidenceState(dbContext, id));
        Assert.Equal(before.Relationships, await ReadRelationshipState(dbContext, id));
        Assert.Equal(revisionOne.GetProperty("title").GetString(), revisions[0].Title);
        Assert.Equal(revisionOne.GetProperty("bodyMarkdown").GetString(), revisions[0].BodyMarkdown);
        Assert.Equal(
            (
                KnowledgeDocumentSearchText.ToIndexText(head.Title),
                KnowledgeDocumentSearchText.ToIndexText(head.Summary),
                KnowledgeDocumentSearchText.ToIndexText(head.BodyMarkdown)
            ),
            await ReadFts(dbContext, id));
    }

    [Fact]
    public async Task Restore_rejects_forbidden_stale_current_identical_and_non_draft_requests_without_writes()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "REV-B04 Negative Editor");
        var viewerId = await CreateUser(AccessLevel.Viewer, "REV-B04 Restore Viewer");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);
        var created = await CreateDocument(editor, "Restore A", null, "body A");
        var id = created.GetProperty("id").GetInt64();
        var revisionTwo = await SaveContent(
            editor,
            id,
            "Restore B",
            null,
            "body B",
            created.GetProperty("concurrencyToken").GetString()!);

        Assert.Equal(HttpStatusCode.Forbidden, (await Restore(viewer, id, 1, revisionTwo.GetProperty("concurrencyToken").GetString()!, "查看者不能恢复")).StatusCode);
        await AssertError(Restore(editor, id, 1, created.GetProperty("concurrencyToken").GetString()!, "过期并发标记恢复"), HttpStatusCode.Conflict, "conflict");
        await AssertError(Restore(editor, id, 2, revisionTwo.GetProperty("concurrencyToken").GetString()!, "不能恢复当前修订"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        var revisionThree = await SaveContent(
            editor,
            id,
            "Restore A",
            null,
            "body A",
            revisionTwo.GetProperty("concurrencyToken").GetString()!);
        await AssertError(Restore(editor, id, 1, revisionThree.GetProperty("concurrencyToken").GetString()!, "内容相同不能恢复"), HttpStatusCode.UnprocessableEntity, "business_rule_violation");

        var published = await ChangeLifecycle(editor, id, "Published", revisionThree.GetProperty("concurrencyToken").GetString()!);
        await AssertError(Restore(editor, id, 2, published.GetProperty("concurrencyToken").GetString()!, "已发布状态不能恢复"), HttpStatusCode.Conflict, "invalid_state");
        var archived = await ChangeLifecycle(editor, id, "Archived", published.GetProperty("concurrencyToken").GetString()!);
        await AssertError(Restore(editor, id, 2, archived.GetProperty("concurrencyToken").GetString()!, "已归档状态不能恢复"), HttpStatusCode.Conflict, "invalid_state");

        await AssertError(Restore(editor, 9_007_199_254_740_991, 1, archived.GetProperty("concurrencyToken").GetString()!, "不存在文档不能恢复"), HttpStatusCode.NotFound, "not_found");
        await AssertError(Restore(editor, id, 99, archived.GetProperty("concurrencyToken").GetString()!, "不存在修订不能恢复"), HttpStatusCode.NotFound, "not_found");
        var other = await CreateDocument(editor, "Other restore owner", null, "other body");
        await AssertError(Restore(editor, other.GetProperty("id").GetInt64(), 2, other.GetProperty("concurrencyToken").GetString()!, "跨文档修订不能恢复"), HttpStatusCode.NotFound, "not_found");

        using var invalidId = await editor.PostAsJsonAsync("/api/knowledge-documents/9007199254740992/revisions/1/restore", new { concurrencyToken = archived.GetProperty("concurrencyToken").GetString(), reason = "非法文档编号恢复" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidId.StatusCode);
        using var invalidRevision = await editor.PostAsJsonAsync($"/api/knowledge-documents/{id}/revisions/0/restore", new { concurrencyToken = archived.GetProperty("concurrencyToken").GetString(), reason = "非法修订编号恢复" });
        Assert.Equal(HttpStatusCode.BadRequest, invalidRevision.StatusCode);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(3, await dbContext.KnowledgeDocumentRevisions.CountAsync(item => item.KnowledgeDocumentId == id));
    }

    [Fact]
    public async Task Restore_validates_token_and_trimmed_reason_boundaries()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "REV-B04 Validation Editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var created = await CreateDocument(editor, "Reason source", null, "source");
        var id = created.GetProperty("id").GetInt64();
        var changed = await SaveContent(editor, id, "Reason target", null, "target", created.GetProperty("concurrencyToken").GetString()!);
        var token = changed.GetProperty("concurrencyToken").GetString()!;

        foreach (var reason in new[] { "", "    ", "四个字a" })
        {
            await AssertError(Restore(editor, id, 1, token, reason), HttpStatusCode.BadRequest, "validation_error");
        }
        await AssertError(Restore(editor, id, 1, token, new string('x', 501)), HttpStatusCode.BadRequest, "validation_error");
        await AssertError(Restore(editor, id, 1, "not-a-token", "有效恢复原因"), HttpStatusCode.BadRequest, "validation_error");

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(2, await dbContext.KnowledgeDocumentRevisions.CountAsync(item => item.KnowledgeDocumentId == id));
    }

    [Fact]
    public async Task Forced_restore_revision_failure_rolls_back_head_pointer_version_and_fts()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "REV-B04 Atomic Editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var created = await CreateDocument(editor, "Atomic source", null, "source body");
        var id = created.GetProperty("id").GetInt64();
        var changed = await SaveContent(editor, id, "Atomic current", null, "current body", created.GetProperty("concurrencyToken").GetString()!);
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER rev_b04_fail_restore
                BEFORE INSERT ON knowledge_document_revisions
                WHEN NEW.title = 'Atomic source' AND NEW.revision_origin = 'Restore'
                BEGIN
                    SELECT RAISE(ABORT, 'REV-B04 forced restore failure');
                END;
                """);
        }

        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<KnowledgeDocumentService>();
            await Assert.ThrowsAsync<DbUpdateException>(() => service.RestoreRevision(
                new RestoreKnowledgeDocumentRevisionCommand(
                    id,
                    1,
                    changed.GetProperty("concurrencyToken").GetString()!,
                    "强制事务失败验证",
                    new KnowledgeDocumentAuthor(editorId, "REV-B04 Atomic Editor")),
                CancellationToken.None));
        }
        finally
        {
            await using var cleanupScope = _factory.Services.CreateAsyncScope();
            var dbContext = cleanupScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS rev_b04_fail_restore;");
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var head = await verificationDb.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == id);
        Assert.Equal(2, head.CurrentRevisionNumber);
        Assert.Equal("Atomic current", head.Title);
        Assert.Equal("current body", head.BodyMarkdown);
        Assert.Equal(2, await verificationDb.KnowledgeDocumentRevisions.CountAsync(item => item.KnowledgeDocumentId == id));
        Assert.Equal(
            (
                KnowledgeDocumentSearchText.ToIndexText(head.Title),
                KnowledgeDocumentSearchText.ToIndexText(head.Summary),
                KnowledgeDocumentSearchText.ToIndexText(head.BodyMarkdown)
            ),
            await ReadFts(verificationDb, id));
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

    private static async Task<JsonElement> CreateDocument(HttpClient client, string title, string? summary, string body)
    {
        using var response = await client.PostAsJsonAsync("/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle",
            title,
            summary,
            bodyMarkdown = body,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> SaveContent(
        HttpClient client,
        long id,
        string title,
        string? summary,
        string body,
        string token)
    {
        using var response = await client.PutAsJsonAsync($"/api/knowledge-documents/{id}/content", new
        {
            title,
            summary,
            bodyMarkdown = body,
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> ChangeLifecycle(HttpClient client, long id, string target, string token)
    {
        using var response = await client.PutAsJsonAsync($"/api/knowledge-documents/{id}/lifecycle", new
        {
            targetLifecycleStatus = target,
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> ChangeStatus(HttpClient client, long id, string target, string token)
    {
        using var response = await client.PutAsJsonAsync("/api/knowledge-status", new
        {
            target = new { type = "KnowledgeDocument", id },
            targetStatus = target,
            reason = "REV-B04 explicit status progression",
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task AddOrdinaryEvidence(HttpClient client, long id)
    {
        using var response = await client.PostAsJsonAsync("/api/evidence", new
        {
            evidenceType = "ExistingDocument",
            subject = new { type = "KnowledgeDocument", id },
            subjectDetailKey = (string?)null,
            sourceTitle = "REV-B04 evidence",
            sourceReference = "REV-B04-EVIDENCE",
            sourceLocator = (object?)null,
            summary = "preservation fixture",
            supportReason = "验证恢复不会改变文档级证据。",
            confidence = "High",
            provider = new
            {
                displayName = "Evidence Provider",
                roleOrIdentity = "Expert",
                occurredAt = "2026-08-23T01:00:00Z",
                team = (string?)null,
                externalUserKey = (string?)null,
                source = (string?)null,
                note = (string?)null,
            },
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private static async Task AddConfirmation(HttpClient client, long id, long revisionNumber)
    {
        using var response = await client.PostAsJsonAsync("/api/evidence/human-confirmations", new
        {
            subject = new { type = "KnowledgeDocument", id },
            subjectRevisionNumber = revisionNumber,
            subjectDetailKey = (string?)null,
            knowledgeRoleId = (long?)null,
            confirmationMethod = "Meeting",
            confirmedAt = "2026-08-23T01:00:00Z",
            confirmationStatement = "确认当前修订内容。",
            supportReason = "用于验证恢复后的确认覆盖状态。",
            sourceNote = "REV-B04",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
    }

    private async Task AddRelationship(long documentId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var systemId = await dbContext.Systems.AsNoTracking().Select(item => item.Id).FirstAsync();
        var timestamp = DateTimeOffset.UtcNow;
        dbContext.KnowledgeRelations.Add(new KnowledgeRelation
        {
            SourceType = KnowledgeTargetType.KnowledgeDocument,
            SourceId = documentId,
            TargetType = KnowledgeTargetType.System,
            TargetId = systemId,
            RelationType = RelationType.References,
            Description = "REV-B04 preservation relation",
            CreatedAt = timestamp,
            CreatedByName = "REV-B04",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Inferred,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "REV-B04",
            KnowledgeStatusChangedByRole = "测试",
            Version = 1,
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<PreservedState> ReadPreservedState(long id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var document = await dbContext.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == id);
        return new PreservedState(
            document.Version,
            document.LatestPublishedRevisionNumber,
            document.PublishedAt,
            document.KnowledgeStatus,
            document.KnowledgeStatusReason,
            document.KnowledgeStatusChangedAt,
            document.KnowledgeStatusChangedByName,
            await ReadEvidenceState(dbContext, id),
            await ReadRelationshipState(dbContext, id));
    }

    private static async Task<(long Id, string Type, long? Revision, long Version)[]> ReadEvidenceState(
        KnowledgeHubDbContext dbContext,
        long id) => await dbContext.Evidence.AsNoTracking()
        .Where(item => item.SubjectType == EvidenceSubjectType.KnowledgeDocument
            && item.SubjectId == id)
        .OrderBy(item => item.Id)
        .Select(item => new ValueTuple<long, string, long?, long>(
            item.Id,
            item.EvidenceType.ToString(),
            item.KnowledgeDocumentRevisionNumberSnapshot,
            item.Version))
        .ToArrayAsync();

    private static async Task<(long Id, string Description, long Version)[]> ReadRelationshipState(
        KnowledgeHubDbContext dbContext,
        long id) => await dbContext.KnowledgeRelations.AsNoTracking()
        .Where(item => item.SourceType == KnowledgeTargetType.KnowledgeDocument && item.SourceId == id)
        .OrderBy(item => item.Id)
        .Select(item => new ValueTuple<long, string, long>(item.Id, item.Description ?? string.Empty, item.Version))
        .ToArrayAsync();

    private static async Task<(string Title, string Summary, string Body)> ReadFts(
        KnowledgeHubDbContext dbContext,
        long id)
    {
        await dbContext.Database.OpenConnectionAsync();
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT title, summary, body_text FROM knowledge_documents_fts WHERE rowid = $id;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (reader.GetString(0), reader.GetString(1), reader.GetString(2));
    }

    private static Task<HttpResponseMessage> Restore(
        HttpClient client,
        long id,
        long revisionNumber,
        string token,
        string reason) => client.PostAsJsonAsync(
        $"/api/knowledge-documents/{id}/revisions/{revisionNumber}/restore",
        new { concurrencyToken = token, reason });

    private static async Task AssertError(
        Task<HttpResponseMessage> responseTask,
        HttpStatusCode status,
        string code)
    {
        using var response = await responseTask;
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private sealed record PreservedState(
        long Version,
        long? LatestPublishedRevisionNumber,
        DateTimeOffset? PublishedAt,
        KnowledgeStatus KnowledgeStatus,
        string? KnowledgeStatusReason,
        DateTimeOffset KnowledgeStatusChangedAt,
        string KnowledgeStatusChangedByName,
        (long Id, string Type, long? Revision, long Version)[] Evidence,
        (long Id, string Description, long Version)[] Relationships);
}
