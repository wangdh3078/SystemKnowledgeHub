using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class KnowledgeDocumentEvidenceStatusApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public KnowledgeDocumentEvidenceStatusApiTests(BootstrapWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Evidence_human_confirmation_and_explicit_knowledge_progression_integrate_with_documents()
    {
        var editorId = await CreateUser(AccessLevel.Editor);
        var viewerId = await CreateUser(AccessLevel.Viewer);
        using var editor = await _factory.CreateAuthenticatedClientAsync(editorId);
        using var create = await editor.PostAsJsonAsync("/api/knowledge-documents", new { documentType = "Specification", title = "Evidence integration document", bodyMarkdown = "body" });
        Assert.Equal(HttpStatusCode.Created, create.StatusCode);
        var document = (await create.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        var id = document.GetProperty("id").GetInt64();
        var token = document.GetProperty("concurrencyToken").GetString()!;

        using var missingEvidence = await ChangeStatus(editor, id, "Inferred", token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, missingEvidence.StatusCode);

        using var evidence = await editor.PostAsJsonAsync("/api/evidence", new
        {
            evidenceType = "ExistingDocument", subject = new { type = "KnowledgeDocument", id }, subjectDetailKey = (string?)null,
            sourceTitle = "已批准的需求说明", sourceReference = "REQ-001", sourceLocator = new { section = "scope" },
            summary = "文档范围依据", supportReason = "该需求明确支持文档描述的业务结论。", confidence = "High",
            provider = new { displayName = "测试提供者", roleOrIdentity = "业务代表", occurredAt = "2026-08-22T02:30:00Z", team = "知识平台组", externalUserKey = (string?)null, source = "测试", note = (string?)null },
        });
        Assert.Equal(HttpStatusCode.Created, evidence.StatusCode);
        Assert.Equal("Unknown", (await evidence.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subjectKnowledgeStatus").GetString());

        using var viewer = await _factory.CreateAuthenticatedClientAsync(viewerId);
        using var list = await viewer.GetAsync($"/api/evidence?subjectType=KnowledgeDocument&subjectId={id}");
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        var listBody = await list.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Single(listBody.GetProperty("items").EnumerateArray());
        Assert.Equal("ExistingDocument", listBody.GetProperty("items")[0].GetProperty("evidenceType").GetString());

        using var infer = await ChangeStatus(editor, id, "Inferred", token);
        Assert.Equal(HttpStatusCode.OK, infer.StatusCode);
        var inferred = (await infer.Content.ReadFromJsonAsync<JsonElement>()).Clone();
        token = inferred.GetProperty("concurrencyToken").GetString()!;

        using var confirmation = await editor.PostAsJsonAsync("/api/evidence/human-confirmations", new
        {
            subject = new { type = "KnowledgeDocument", id }, subjectRevisionNumber = document.GetProperty("currentRevisionNumber").GetInt64(), subjectDetailKey = (string?)null, knowledgeRoleId = (long?)null,
            confirmationMethod = "Meeting", confirmedAt = "2026-08-22T02:30:00Z", confirmationStatement = "确认该知识内容的业务结论正确。",
            supportReason = "当前操作者完成了明确确认。", sourceNote = "KC-B05 test",
        });
        Assert.Equal(HttpStatusCode.Created, confirmation.StatusCode);
        Assert.Equal("Inferred", (await confirmation.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("subjectKnowledgeStatus").GetString());

        using var confirm = await ChangeStatus(editor, id, "Confirmed", token);
        Assert.Equal(HttpStatusCode.OK, confirm.StatusCode);
        var confirmed = await confirm.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Confirmed", confirmed.GetProperty("knowledgeStatus").GetString());

        using var published = await editor.PutAsJsonAsync($"/api/knowledge-documents/{id}/lifecycle", new { targetLifecycleStatus = "Published", concurrencyToken = confirmed.GetProperty("concurrencyToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, published.StatusCode);
        var publishedDocument = await published.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Published", publishedDocument.GetProperty("lifecycleStatus").GetString());
        Assert.Equal("Confirmed", publishedDocument.GetProperty("knowledgeStatus").GetString());
    }

    private async Task<long> CreateUser(AccessLevel accessLevel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User { DisplayName = $"KC-B05 {Guid.NewGuid():N}", IsActive = true, AccessLevel = accessLevel, CreatedAt = timestamp, UpdatedAt = timestamp, Version = 1 };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        return user.Id;
    }

    private static Task<HttpResponseMessage> ChangeStatus(HttpClient client, long id, string targetStatus, string token) => client.PutAsJsonAsync("/api/knowledge-status", new
    {
        target = new { type = "KnowledgeDocument", id }, targetStatus, reason = (string?)null,
        actor = new { displayName = "知识状态测试人员", roleOrIdentity = "知识整理人员", occurredAt = "2026-08-22T02:30:00Z", team = "知识平台组", externalUserKey = (string?)null, source = "测试", note = (string?)null },
        concurrencyToken = token,
    });
}
