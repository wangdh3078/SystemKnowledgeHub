using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;
using EvidenceEntity = SystemKnowledgeHub.Api.Features.Evidence.Domain.Evidence;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class KnowledgeDocumentRevisionApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public KnowledgeDocumentRevisionApiTests(BootstrapWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Create_and_semantic_saves_create_trusted_contiguous_revisions_while_noop_and_stale_writes_do_nothing()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Revision Editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var created = await CreateDocument(editor, "  Revision title  ", " initial summary ", "body\r\nline");
        var id = created.GetProperty("id").GetInt64();
        var createToken = created.GetProperty("concurrencyToken").GetString()!;
        Assert.Equal(1, created.GetProperty("currentRevisionNumber").GetInt64());
        Assert.Equal(JsonValueKind.Null, created.GetProperty("latestPublishedRevisionNumber").ValueKind);
        Assert.Equal("NoConfirmation", created.GetProperty("confirmationCoverage").GetProperty("state").GetString());

        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var document = await dbContext.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == id);
            var revision = await dbContext.KnowledgeDocumentRevisions.AsNoTracking().SingleAsync(item => item.KnowledgeDocumentId == id);
            Assert.Equal(RevisionOrigin.Created, revision.RevisionOrigin);
            Assert.Equal(DocumentLifecycleStatus.Draft, revision.LifecycleContext);
            Assert.Equal(document.Title, revision.Title);
            Assert.Equal(document.Summary, revision.Summary);
            Assert.Equal(document.BodyMarkdown, revision.BodyMarkdown);
            Assert.Equal(editorId, revision.AuthorUserId);
            Assert.Equal("Revision Editor", revision.AuthorDisplayNameSnapshot);
            Assert.Equal(document.CreatedAt, revision.CreatedAt);
        }

        var titleOnly = await SaveContent(editor, id, "Revision title 2", "initial summary", "body\nline", createToken, "  title changed  ");
        var summaryOnly = await SaveContent(editor, id, "Revision title 2", "summary 2", "body\nline", titleOnly.GetProperty("concurrencyToken").GetString()!, null);
        var bodyOnly = await SaveContent(editor, id, "Revision title 2", "summary 2", "body\rchanged", summaryOnly.GetProperty("concurrencyToken").GetString()!, "body changed");
        Assert.Equal(4, bodyOnly.GetProperty("currentRevisionNumber").GetInt64());

        long versionBeforeNoop;
        DateTimeOffset updatedAtBeforeNoop;
        using (var scope = _factory.Services.CreateScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var document = await dbContext.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == id);
            versionBeforeNoop = document.Version;
            updatedAtBeforeNoop = document.UpdatedAt;
        }

        var noOp = await SaveContent(
            editor,
            id,
            "  Revision title 2  ",
            "  summary 2  ",
            "body\r\nchanged",
            bodyOnly.GetProperty("concurrencyToken").GetString()!,
            "change summary alone must not create history");
        Assert.Equal(bodyOnly.GetProperty("concurrencyToken").GetString(), noOp.GetProperty("concurrencyToken").GetString());
        Assert.Equal(4, noOp.GetProperty("currentRevisionNumber").GetInt64());
        Assert.Equal(updatedAtBeforeNoop, noOp.GetProperty("updatedAt").GetDateTimeOffset());

        using var stale = await editor.PutAsJsonAsync($"/api/knowledge-documents/{id}/content", new
        {
            title = "stale title",
            summary = "stale summary",
            bodyMarkdown = "stale body",
            concurrencyToken = createToken,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var head = await verificationDb.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == id);
        var revisions = await verificationDb.KnowledgeDocumentRevisions.AsNoTracking()
            .Where(item => item.KnowledgeDocumentId == id)
            .OrderBy(item => item.RevisionNumber)
            .ToArrayAsync();
        Assert.Equal(versionBeforeNoop, head.Version);
        Assert.Equal(4, head.CurrentRevisionNumber);
        Assert.Equal("Revision title 2", head.Title);
        Assert.Equal("summary 2", head.Summary);
        Assert.Equal("body\nchanged", head.BodyMarkdown);
        Assert.Equal([1L, 2L, 3L, 4L], revisions.Select(item => item.RevisionNumber));
        Assert.Equal([RevisionOrigin.Created, RevisionOrigin.ContentSave, RevisionOrigin.ContentSave, RevisionOrigin.ContentSave], revisions.Select(item => item.RevisionOrigin));
        Assert.Equal("title changed", revisions[1].ChangeSummary);
        Assert.Null(revisions[2].ChangeSummary);
        Assert.Equal("Revision title", revisions[0].Title);
        Assert.Equal("Revision title 2", revisions[1].Title);
        Assert.Equal("summary 2", revisions[2].Summary);
        Assert.Equal("body\nchanged", revisions[3].BodyMarkdown);
        Assert.All(revisions, revision => Assert.Equal(editorId, revision.AuthorUserId));
        Assert.Equal(head.UpdatedAt, revisions[^1].CreatedAt);
    }

    [Fact]
    public async Task Published_draft_and_archived_semantics_keep_revision_and_publication_pointers_independent()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Lifecycle Revision Editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var created = await CreateDocument(editor, "Lifecycle revision", "summary", "body");
        var id = created.GetProperty("id").GetInt64();

        var published = await ChangeLifecycle(editor, id, "Published", created.GetProperty("concurrencyToken").GetString()!);
        var firstPublishedAt = published.GetProperty("publishedAt").GetDateTimeOffset();
        Assert.Equal(1, published.GetProperty("currentRevisionNumber").GetInt64());
        Assert.Equal(1, published.GetProperty("latestPublishedRevisionNumber").GetInt64());
        Assert.Equal(1L, await RevisionCount(id));

        var publishedNoOp = await SaveContent(editor, id, "Lifecycle revision", "summary", "body", published.GetProperty("concurrencyToken").GetString()!, "ignored");
        Assert.Equal(published.GetProperty("concurrencyToken").GetString(), publishedNoOp.GetProperty("concurrencyToken").GetString());
        Assert.Equal(firstPublishedAt, publishedNoOp.GetProperty("publishedAt").GetDateTimeOffset());
        Assert.Equal(1L, await RevisionCount(id));

        var publishedSave = await SaveContent(editor, id, "Lifecycle revision", "published summary", "body", publishedNoOp.GetProperty("concurrencyToken").GetString()!, "published change");
        Assert.Equal("Published", publishedSave.GetProperty("lifecycleStatus").GetString());
        Assert.Equal(2, publishedSave.GetProperty("currentRevisionNumber").GetInt64());
        Assert.Equal(2, publishedSave.GetProperty("latestPublishedRevisionNumber").GetInt64());
        Assert.True(publishedSave.GetProperty("publishedAt").GetDateTimeOffset() >= firstPublishedAt);

        var draft = await ChangeLifecycle(editor, id, "Draft", publishedSave.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(2, draft.GetProperty("latestPublishedRevisionNumber").GetInt64());
        Assert.Equal(publishedSave.GetProperty("publishedAt").GetDateTimeOffset(), draft.GetProperty("publishedAt").GetDateTimeOffset());
        Assert.Equal(2L, await RevisionCount(id));

        var draftSave = await SaveContent(editor, id, "Lifecycle revision", "published summary", "draft body", draft.GetProperty("concurrencyToken").GetString()!, null);
        Assert.Equal(3, draftSave.GetProperty("currentRevisionNumber").GetInt64());
        Assert.Equal(2, draftSave.GetProperty("latestPublishedRevisionNumber").GetInt64());

        var republished = await ChangeLifecycle(editor, id, "Published", draftSave.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(3, republished.GetProperty("latestPublishedRevisionNumber").GetInt64());
        Assert.Equal(3L, await RevisionCount(id));
        var archived = await ChangeLifecycle(editor, id, "Archived", republished.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(3, archived.GetProperty("latestPublishedRevisionNumber").GetInt64());
        Assert.Equal(republished.GetProperty("publishedAt").GetDateTimeOffset(), archived.GetProperty("publishedAt").GetDateTimeOffset());

        var before = await ReadHeadAndFts(id);
        using var rejected = await editor.PutAsJsonAsync($"/api/knowledge-documents/{id}/content", new
        {
            title = "must reject",
            summary = "must reject",
            bodyMarkdown = "must reject",
            concurrencyToken = archived.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, rejected.StatusCode);
        Assert.Equal("invalid_state", (await rejected.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        var after = await ReadHeadAndFts(id);
        Assert.Equal(before, after);

        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var revisions = await dbContext.KnowledgeDocumentRevisions.AsNoTracking()
            .Where(item => item.KnowledgeDocumentId == id)
            .OrderBy(item => item.RevisionNumber)
            .ToArrayAsync();
        Assert.Equal([DocumentLifecycleStatus.Draft, DocumentLifecycleStatus.Published, DocumentLifecycleStatus.Draft], revisions.Select(item => item.LifecycleContext));
        Assert.Equal(RevisionOrigin.Created, revisions[0].RevisionOrigin);
        Assert.Equal(RevisionOrigin.ContentSave, revisions[1].RevisionOrigin);
        Assert.Equal(RevisionOrigin.ContentSave, revisions[2].RevisionOrigin);
        Assert.Equal(SystemKnowledgeHub.Api.Shared.Domain.KnowledgeStatus.Unknown, (await dbContext.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == id)).KnowledgeStatus);
    }

    [Fact]
    public async Task HumanConfirmation_captures_current_revision_and_projects_all_coverage_states_without_changing_knowledge_status()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Confirmation Revision Editor");
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        var document = await CreateDocument(editor, "Confirmation coverage", null, "body");
        var id = document.GetProperty("id").GetInt64();
        Assert.Equal("NoConfirmation", document.GetProperty("confirmationCoverage").GetProperty("state").GetString());

        using var ordinary = await editor.PostAsJsonAsync("/api/evidence", new
        {
            evidenceType = "ExistingDocument",
            subject = new { type = "KnowledgeDocument", id },
            subjectDetailKey = (string?)null,
            sourceTitle = "ordinary evidence",
            sourceReference = "REV-B01-EVIDENCE",
            sourceLocator = (object?)null,
            summary = (string?)null,
            supportReason = "ordinary evidence remains document-level",
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
        Assert.Equal(HttpStatusCode.Created, ordinary.StatusCode);
        Assert.Equal(JsonValueKind.Null, (await ordinary.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("knowledgeDocumentRevisionNumberSnapshot").ValueKind);

        var confirmation = await AddConfirmation(editor, id, 1);
        Assert.Equal(HttpStatusCode.Created, confirmation.StatusCode);
        var confirmationBody = await confirmation.Content.ReadFromJsonAsync<JsonElement>();
        var confirmationId = confirmationBody.GetProperty("id").GetInt64();
        Assert.Equal(1, confirmationBody.GetProperty("knowledgeDocumentRevisionNumberSnapshot").GetInt64());

        using var currentDetailResponse = await editor.GetAsync($"/api/knowledge-documents/{id}");
        var currentDetail = (await currentDetailResponse.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        Assert.Equal("CurrentRevisionConfirmed", currentDetail.GetProperty("confirmationCoverage").GetProperty("state").GetString());
        Assert.Equal(1, currentDetail.GetProperty("confirmationCoverage").GetProperty("lastConfirmedRevisionNumber").GetInt64());

        var changed = await SaveContent(editor, id, "Confirmation coverage", null, "body changed", currentDetail.GetProperty("concurrencyToken").GetString()!, null);
        Assert.Equal("ChangedSinceConfirmation", changed.GetProperty("confirmationCoverage").GetProperty("state").GetString());
        Assert.Equal(1, changed.GetProperty("confirmationCoverage").GetProperty("lastConfirmedRevisionNumber").GetInt64());
        Assert.Equal("Unknown", changed.GetProperty("knowledgeStatus").GetString());

        var evidenceCountBeforeStale = await EvidenceCount(id);
        using var stale = await AddConfirmation(editor, id, 1);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal(evidenceCountBeforeStale, await EvidenceCount(id));

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var otherSubjectId = await dbContext.Systems.AsNoTracking().Select(item => item.Id).FirstAsync();
            using var invalidOtherSubject = await editor.PostAsJsonAsync("/api/evidence/human-confirmations", new
            {
                subject = new { type = "System", id = otherSubjectId },
                subjectRevisionNumber = 1,
                subjectDetailKey = (string?)null,
                knowledgeRoleId = (long?)null,
                confirmationMethod = "Meeting",
                confirmedAt = "2026-08-23T01:00:00Z",
                confirmationStatement = "invalid revision context",
                supportReason = "other subjects cannot submit a document revision",
                sourceNote = (string?)null,
            });
            Assert.Equal(HttpStatusCode.BadRequest, invalidOtherSubject.StatusCode);
        }

        using var listResponse = await editor.GetAsync($"/api/evidence?subjectType=KnowledgeDocument&subjectId={id}");
        var listItems = (await listResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(1, listItems.Single(item => item.GetProperty("id").GetInt64() == confirmationId).GetProperty("knowledgeDocumentRevisionNumberSnapshot").GetInt64());
        Assert.Equal(JsonValueKind.Null, listItems.Single(item => item.GetProperty("evidenceType").GetString() == "ExistingDocument").GetProperty("knowledgeDocumentRevisionNumberSnapshot").ValueKind);
        using var confirmationDetailResponse = await editor.GetAsync($"/api/evidence/{confirmationId}");
        Assert.Equal(1, (await confirmationDetailResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("knowledgeDocumentRevisionNumberSnapshot").GetInt64());

        var legacyDocument = await CreateDocument(editor, "Legacy confirmation coverage", null, "legacy body");
        var legacyId = legacyDocument.GetProperty("id").GetInt64();
        await AddLegacyConfirmation(legacyId);
        using var legacyDetailResponse = await editor.GetAsync($"/api/knowledge-documents/{legacyId}");
        var legacyCoverage = (await legacyDetailResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("confirmationCoverage");
        Assert.Equal("LegacyConfirmationUnknown", legacyCoverage.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, legacyCoverage.GetProperty("lastConfirmedRevisionNumber").ValueKind);

        await using var integrityScope = _factory.Services.CreateAsyncScope();
        var integrityDb = integrityScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        await integrityDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE evidence SET knowledge_document_revision_number_snapshot = {99L} WHERE id = {confirmationId};");
        var queries = integrityScope.ServiceProvider.GetRequiredService<KnowledgeDocumentQueries>();
        await Assert.ThrowsAsync<InvalidOperationException>(() => queries.GetDetail(id, CancellationToken.None));
        await integrityDb.Database.ExecuteSqlInterpolatedAsync($"UPDATE evidence SET knowledge_document_revision_number_snapshot = {1L} WHERE id = {confirmationId};");
    }

    [Fact]
    public async Task Create_rolls_back_document_revision_and_fts_when_revision_insert_fails()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "Atomic Revision Editor");
        const string title = "Atomic revision failure";
        await using (var setupScope = _factory.Services.CreateAsyncScope())
        {
            var dbContext = setupScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await dbContext.Database.ExecuteSqlRawAsync("""
                CREATE TRIGGER rev_b01_fail_revision
                BEFORE INSERT ON knowledge_document_revisions
                WHEN NEW.title = 'Atomic revision failure'
                BEGIN
                    SELECT RAISE(ABORT, 'REV-B01 forced revision failure');
                END;
                """);
        }

        try
        {
            await using var scope = _factory.Services.CreateAsyncScope();
            var service = scope.ServiceProvider.GetRequiredService<KnowledgeDocumentService>();
            await Assert.ThrowsAsync<DbUpdateException>(() => service.Create(
                new CreateKnowledgeDocumentCommand(
                    "KnowledgeArticle",
                    title,
                    null,
                    "body",
                    new KnowledgeDocumentAuthor(editorId, "Atomic Revision Editor")),
                CancellationToken.None));
        }
        finally
        {
            await using var cleanupScope = _factory.Services.CreateAsyncScope();
            var cleanupDb = cleanupScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await cleanupDb.Database.ExecuteSqlRawAsync("DROP TRIGGER IF EXISTS rev_b01_fail_revision;");
        }

        await using var verificationScope = _factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await verificationDb.KnowledgeDocuments.AsNoTracking().AnyAsync(item => item.Title == title));
        Assert.False(await verificationDb.KnowledgeDocumentRevisions.AsNoTracking().AnyAsync(item => item.Title == title));
        await verificationDb.Database.OpenConnectionAsync();
        await using var command = verificationDb.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT count(*) FROM knowledge_documents_fts WHERE title = $title;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$title";
        parameter.Value = title;
        command.Parameters.Add(parameter);
        Assert.Equal(0L, (long)(await command.ExecuteScalarAsync() ?? 0L));
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
        string token,
        string? changeSummary)
    {
        using var response = await client.PutAsJsonAsync($"/api/knowledge-documents/{id}/content", new
        {
            title,
            summary,
            bodyMarkdown = body,
            changeSummary,
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

    private async Task<long> RevisionCount(long id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>()
            .KnowledgeDocumentRevisions.AsNoTracking().CountAsync(item => item.KnowledgeDocumentId == id);
    }

    private async Task<(long Version, long Revision, string Title, string Body, string FtsTitle, string FtsBody)> ReadHeadAndFts(long id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var document = await dbContext.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == id);
        await dbContext.Database.OpenConnectionAsync();
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT title, body_text FROM knowledge_documents_fts WHERE rowid = $id;";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        return (document.Version, document.CurrentRevisionNumber, document.Title, document.BodyMarkdown, reader.GetString(0), reader.GetString(1));
    }

    private static Task<HttpResponseMessage> AddConfirmation(HttpClient client, long id, long revisionNumber) =>
        client.PostAsJsonAsync("/api/evidence/human-confirmations", new
        {
            subject = new { type = "KnowledgeDocument", id },
            subjectRevisionNumber = revisionNumber,
            subjectDetailKey = (string?)null,
            knowledgeRoleId = (long?)null,
            confirmationMethod = "Meeting",
            confirmedAt = "2026-08-23T01:00:00Z",
            confirmationStatement = "Current document revision confirmed",
            supportReason = "Explicit confirmation for the displayed revision",
            sourceNote = "REV-B01",
        });

    private async Task<long> EvidenceCount(long id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>()
            .Evidence.AsNoTracking().CountAsync(item => item.SubjectType == EvidenceSubjectType.KnowledgeDocument && item.SubjectId == id);
    }

    private async Task AddLegacyConfirmation(long documentId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        dbContext.Evidence.Add(new EvidenceEntity
        {
            EvidenceType = EvidenceType.HumanConfirmation,
            SubjectType = EvidenceSubjectType.KnowledgeDocument,
            SubjectId = documentId,
            SourceTitle = "Legacy confirmation",
            SourceReference = "legacy meeting",
            Summary = "legacy",
            SupportReason = "migration-era confirmation",
            ProviderName = "Legacy Expert",
            ProviderRole = "Expert",
            ProvidedAt = timestamp,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        });
        await dbContext.SaveChangesAsync();
    }
}
