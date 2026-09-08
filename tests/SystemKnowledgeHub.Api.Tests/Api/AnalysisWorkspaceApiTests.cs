using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Api.Contracts;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Tests.TestSupport;
using static SystemKnowledgeHub.Api.Tests.TestSupport.AnalysisWorkspaceTestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AnalysisWorkspaceApiTests
{
    [Fact]
    public async Task Every_route_requires_current_access_and_every_write_requires_antiforgery()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var anonymous = factory.CreateClient();
        using var viewer = await factory.CreateAuthenticatedClientAsync(await User(factory, AccessLevel.Viewer));
        using var editor = await factory.CreateAuthenticatedClientAsync(await User(factory, AccessLevel.Editor));
        using var admin = factory.CreateAuthenticatedClient();
        using var noCsrf = factory.CreateAuthenticatedClientWithoutAntiforgery();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/analysis/tree")).StatusCode);
        foreach (var client in new[] { viewer, editor, admin }) Assert.Empty((await Tree(client)).Items);
        var routes = new[]
        {
            (HttpMethod.Post, "folders"), (HttpMethod.Put, "folders/1/title"),
            (HttpMethod.Post, "document-placements"), (HttpMethod.Post, "documents"),
            (HttpMethod.Post, "nodes/1/move"), (HttpMethod.Put, "children/order"),
            (HttpMethod.Delete, "document-placements/1"), (HttpMethod.Delete, "folders/1"),
        };
        foreach (var (method, route) in routes)
        {
            using var anonymousRequest = Request(method, "/api/analysis/" + route, new { });
            using var viewerRequest = Request(method, "/api/analysis/" + route, new { });
            using var csrfRequest = Request(method, "/api/analysis/" + route, new { });
            using var unauthenticated = await anonymous.SendAsync(anonymousRequest);
            using var forbidden = await viewer.SendAsync(viewerRequest);
            using var csrf = await noCsrf.SendAsync(csrfRequest);
            Assert.Equal(HttpStatusCode.Unauthorized, unauthenticated.StatusCode);
            await Code(forbidden, HttpStatusCode.Forbidden, "forbidden");
            await Code(csrf, HttpStatusCode.Forbidden, "antiforgery_failed");
        }
        await Folder(editor, "Editor may organize");
        await Folder(admin, "Administrator may organize");
        Assert.Equal(2, (await Tree(viewer)).Items.Count);
    }

    [Fact]
    public async Task Folder_rename_move_order_and_empty_delete_have_independent_tokens_and_noop_semantics()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = await factory.CreateAuthenticatedClientAsync(await User(factory, AccessLevel.Editor));
        var empty = await Tree(client);
        Assert.Equal(empty.TreeConcurrencyToken, (await Tree(client)).TreeConcurrencyToken);
        var first = (await Folder(client, "  MES  ")).Node!;
        Assert.Equal("MES", first.Title);
        var second = (await Folder(client, "MES")).Node!;
        var child = (await Folder(client, "流程", first.Id)).Node!;
        var tree = await Tree(client);
        using var invalidToken = await client.PostAsJsonAsync("/api/analysis/folders", new CreateAnalysisFolderRequest(null, "bad token", "invalid"));
        await Code(invalidToken, HttpStatusCode.BadRequest, "validation_error");
        using var noop = await client.PutAsJsonAsync($"/api/analysis/folders/{first.Id}/title",
            new RenameAnalysisFolderRequest(" MES ", first.ConcurrencyToken, tree.TreeConcurrencyToken));
        var unchanged = await Mutation(noop);
        Assert.Equal(first.UpdatedAt, unchanged.Node!.UpdatedAt);
        Assert.Equal(tree.TreeConcurrencyToken, unchanged.TreeConcurrencyToken);
        using var rename = await client.PutAsJsonAsync($"/api/analysis/folders/{first.Id}/title",
            new RenameAnalysisFolderRequest("MES 分析", first.ConcurrencyToken, tree.TreeConcurrencyToken));
        var renamed = await Mutation(rename);
        Assert.NotEqual(first.ConcurrencyToken, renamed.Node!.ConcurrencyToken);
        Assert.NotEqual(tree.TreeConcurrencyToken, renamed.TreeConcurrencyToken);
        using var staleNode = await client.PutAsJsonAsync($"/api/analysis/folders/{first.Id}/title",
            new RenameAnalysisFolderRequest("stale", first.ConcurrencyToken, renamed.TreeConcurrencyToken));
        await Code(staleNode, HttpStatusCode.Conflict, "conflict");
        using var staleTree = await client.PostAsJsonAsync("/api/analysis/folders", new CreateAnalysisFolderRequest(null, "stale", empty.TreeConcurrencyToken));
        await Code(staleTree, HttpStatusCode.Conflict, "conflict");
        using var nonemptyRequest = Request(HttpMethod.Delete, $"/api/analysis/folders/{first.Id}",
            new RemoveAnalysisNodeRequest(renamed.Node.ConcurrencyToken, renamed.TreeConcurrencyToken));
        using var nonempty = await client.SendAsync(nonemptyRequest);
        await Code(nonempty, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        using var move = await client.PostAsJsonAsync($"/api/analysis/nodes/{child.Id}/move",
            new MoveAnalysisNodeRequest(second.Id, 0, child.ConcurrencyToken, renamed.TreeConcurrencyToken));
        var moved = await Mutation(move);
        Assert.Equal(second.Id, moved.Node!.ParentId);
        var root = moved.Items.Where(node => node.ParentId is null).Reverse().ToArray();
        using var reorder = await client.PutAsJsonAsync("/api/analysis/children/order", new ReorderAnalysisChildrenRequest(null,
            root.Select(node => new AnalysisChildOrderItem(node.Id, node.ConcurrencyToken)).ToArray(), moved.TreeConcurrencyToken));
        var ordered = await Mutation(reorder);
        Assert.Equal(new[] { second.Id, first.Id }, ordered.Items.Where(node => node.ParentId is null).Select(node => node.Id));
        Assert.Equal(moved.Node.ConcurrencyToken, ordered.Items.Single(node => node.Id == child.Id).ConcurrencyToken);
        var orderedFirst = ordered.Items.Single(node => node.Id == first.Id);
        using var deleteRequest = Request(HttpMethod.Delete, $"/api/analysis/folders/{first.Id}",
            new RemoveAnalysisNodeRequest(orderedFirst.ConcurrencyToken, ordered.TreeConcurrencyToken));
        using var delete = await client.SendAsync(deleteRequest);
        var deleted = await Mutation(delete);
        Assert.DoesNotContain(deleted.Items, node => node.Id == first.Id);
        Assert.Equal(0, deleted.Items.Single(node => node.Id == second.Id).SortOrder);
        var next = (await Folder(client, "new identity")).Node!;
        Assert.True(next.Id > child.Id);
    }

    [Fact]
    public async Task Move_validates_subtree_depth_cycle_leaf_parent_and_complete_reorder_sets()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var ancestor = (await Folder(client, "ancestor")).Node!;
        var descendant = (await Folder(client, "descendant", ancestor.Id)).Node!;
        var document = await Document(client);
        var leaf = (await Placement(client, document.GetProperty("id").GetInt64())).Node!;
        var tree = await Tree(client);
        foreach (var target in new[] { ancestor.Id, descendant.Id })
        {
            using var cycle = await client.PostAsJsonAsync($"/api/analysis/nodes/{ancestor.Id}/move",
                new MoveAnalysisNodeRequest(target, 0, ancestor.ConcurrencyToken, tree.TreeConcurrencyToken));
            await Code(cycle, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        }
        using var leafParent = await client.PostAsJsonAsync("/api/analysis/folders", new CreateAnalysisFolderRequest(leaf.Id, "bad", tree.TreeConcurrencyToken));
        await Code(leafParent, HttpStatusCode.UnprocessableEntity, "reference_invalid");
        using var renameDocument = await client.PutAsJsonAsync($"/api/analysis/folders/{leaf.Id}/title",
            new RenameAnalysisFolderRequest("override", leaf.ConcurrencyToken, tree.TreeConcurrencyToken));
        await Code(renameDocument, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        var incomplete = new[] { new AnalysisChildOrderItem(ancestor.Id, ancestor.ConcurrencyToken) };
        using var missing = await client.PutAsJsonAsync("/api/analysis/children/order", new ReorderAnalysisChildrenRequest(null, incomplete, tree.TreeConcurrencyToken));
        await Code(missing, HttpStatusCode.UnprocessableEntity, "reference_invalid");
        using var duplicate = await client.PutAsJsonAsync("/api/analysis/children/order", new ReorderAnalysisChildrenRequest(null,
            [incomplete[0], incomplete[0]], tree.TreeConcurrencyToken));
        await Code(duplicate, HttpStatusCode.BadRequest, "validation_error");
        using var foreign = await client.PutAsJsonAsync("/api/analysis/children/order", new ReorderAnalysisChildrenRequest(null,
            [incomplete[0], new(descendant.Id, descendant.ConcurrencyToken)], tree.TreeConcurrencyToken));
        await Code(foreign, HttpStatusCode.UnprocessableEntity, "reference_invalid");
        using var outOfRange = await client.PostAsJsonAsync($"/api/analysis/nodes/{leaf.Id}/move",
            new MoveAnalysisNodeRequest(null, 2, leaf.ConcurrencyToken, tree.TreeConcurrencyToken));
        await Code(outOfRange, HttpStatusCode.BadRequest, "validation_error");
        // Same-parent positions are evaluated after removal, and an unchanged placement is a no-op.
        using var noMove = await client.PostAsJsonAsync($"/api/analysis/nodes/{leaf.Id}/move",
            new MoveAnalysisNodeRequest(null, 1, leaf.ConcurrencyToken, tree.TreeConcurrencyToken));
        Assert.Equal(tree.TreeConcurrencyToken, (await Mutation(noMove)).TreeConcurrencyToken);
        var deep = descendant;
        for (var depth = 3; depth <= 10; depth++) deep = (await Folder(client, $"level {depth}", deep.Id)).Node!;
        tree = await Tree(client);
        using var tooDeep = await client.PostAsJsonAsync("/api/analysis/folders", new CreateAnalysisFolderRequest(deep.Id, "level 11", tree.TreeConcurrencyToken));
        await Code(tooDeep, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        using var moveDeep = await client.PostAsJsonAsync($"/api/analysis/nodes/{leaf.Id}/move",
            new MoveAnalysisNodeRequest(deep.Id, 0, leaf.ConcurrencyToken, tree.TreeConcurrencyToken));
        await Code(moveDeep, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Equal(tree.TreeConcurrencyToken, (await Tree(client)).TreeConcurrencyToken);
        // Moving a two-level folder under depth 9 would put its child at depth 11,
        // even though the moved folder itself would fit at depth 10.
        var subtree = (await Folder(client, "subtree")).Node!;
        await Folder(client, "subtree child", subtree.Id);
        tree = await Tree(client);
        var levelNine = tree.Items.Single(node => node.Title == "level 9");
        using var subtreeTooDeep = await client.PostAsJsonAsync($"/api/analysis/nodes/{subtree.Id}/move",
            new MoveAnalysisNodeRequest(levelNine.Id, 1, subtree.ConcurrencyToken, tree.TreeConcurrencyToken));
        await Code(subtreeTooDeep, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Equal(tree.TreeConcurrencyToken, (await Tree(client)).TreeConcurrencyToken);
    }

    [Fact]
    public async Task Capacity_is_complete_bounded_and_temporary_order_overflow_does_not_write()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.AnalysisNodes.AddRange(Enumerable.Range(0, AnalysisLimits.MaxNodes).Select(index => new AnalysisNode
            { Title = $"folder {index}", NodeType = AnalysisNodeType.Folder, SortOrder = index, CreatedAt = now, UpdatedAt = now }));
            await db.SaveChangesAsync();
        }
        var tree = await Tree(client);
        Assert.Equal(2000, tree.Items.Count);
        using var full = await client.PostAsJsonAsync("/api/analysis/documents", new CreateAnalysisDocumentRequest(null,
            tree.TreeConcurrencyToken, null, "cannot fit", null, null));
        await Code(full, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.False(await db.KnowledgeDocuments.AnyAsync(document => document.Title == "cannot fit"));
            await db.Database.ExecuteSqlRawAsync("DELETE FROM analysis_nodes WHERE sort_order > 0;");
            await db.Database.ExecuteSqlRawAsync("UPDATE analysis_nodes SET sort_order=2147483647;");
        }
        tree = await Tree(client);
        using var overflow = await client.PostAsJsonAsync("/api/analysis/folders", new CreateAnalysisFolderRequest(null, "overflow", tree.TreeConcurrencyToken));
        await Code(overflow, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        Assert.Equal(tree.TreeConcurrencyToken, (await Tree(client)).TreeConcurrencyToken);
        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await db.Database.ExecuteSqlRawAsync("""
                WITH RECURSIVE n(i) AS (VALUES(0) UNION ALL SELECT i+1 FROM n WHERE i<1999)
                INSERT INTO analysis_nodes(node_type,title,sort_order,created_at,updated_at,version)
                SELECT 'Folder','external overflow',i,'2026-09-08','2026-09-08',1 FROM n;
                """);
        }
        using var oversizedRead = await client.GetAsync("/api/analysis/tree");
        await Code(oversizedRead, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
    }

    [Fact]
    public async Task Seven_types_and_all_lifecycles_share_canonical_titles_and_deleted_projection_is_safe()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var types = new[] { "Requirement", "Specification", "TestCase", "Sop", "Troubleshooting", "KnowledgeArticle", "DesignNote" };
        foreach (var type in types)
        {
            var document = await Document(client, "title " + type, type);
            var id = document.GetProperty("id").GetInt64();
            if (type is "Specification" or "Sop")
            {
                using var publish = await client.PutAsJsonAsync($"/api/knowledge-documents/{id}/lifecycle", new
                { targetLifecycleStatus = "Published", concurrencyToken = document.GetProperty("concurrencyToken").GetString() });
                Assert.Equal(HttpStatusCode.OK, publish.StatusCode);
                document = await publish.Content.ReadFromJsonAsync<JsonElement>();
                if (type == "Sop")
                {
                    using var archive = await client.PutAsJsonAsync($"/api/knowledge-documents/{id}/lifecycle", new
                    { targetLifecycleStatus = "Archived", concurrencyToken = document.GetProperty("concurrencyToken").GetString() });
                    Assert.Equal(HttpStatusCode.OK, archive.StatusCode);
                }
            }
            await Placement(client, id);
        }
        var before = await Tree(client);
        Assert.Equal(7, before.Items.Count);
        Assert.Contains(before.Items, node => node.LifecycleStatus == "Archived");
        Assert.Contains(before.Items, node => node.LifecycleStatus == "Published");
        var selected = before.Items.Single(node => node.DocumentType == "DesignNote");
        var current = await client.GetFromJsonAsync<JsonElement>($"/api/knowledge-documents/{selected.KnowledgeDocumentId}");
        using var save = await client.PutAsJsonAsync($"/api/knowledge-documents/{selected.KnowledgeDocumentId}/content", new
        { title = "canonical new title", bodyMarkdown = "canonical body", concurrencyToken = current.GetProperty("concurrencyToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        var saved = await save.Content.ReadFromJsonAsync<JsonElement>();
        var afterSave = await Tree(client);
        Assert.Equal(before.TreeConcurrencyToken, afterSave.TreeConcurrencyToken);
        Assert.Equal("canonical new title", afterSave.Items.Single(node => node.Id == selected.Id).Title);
        using var duplicate = await client.PostAsJsonAsync("/api/analysis/document-placements",
            new AddAnalysisPlacementRequest(null, selected.KnowledgeDocumentId!.Value, afterSave.TreeConcurrencyToken));
        await Code(duplicate, HttpStatusCode.UnprocessableEntity, "business_rule_violation");
        using var deleteRequest = Request(HttpMethod.Delete, $"/api/knowledge-documents/{selected.KnowledgeDocumentId}",
            new { concurrencyToken = saved.GetProperty("concurrencyToken").GetString() });
        using var deleted = await client.SendAsync(deleteRequest);
        Assert.Equal(HttpStatusCode.NoContent, deleted.StatusCode);
        var unavailableTree = await Tree(client);
        var unavailable = unavailableTree.Items.Single(node => node.Id == selected.Id);
        Assert.Equal("Unavailable", unavailable.Availability);
        Assert.Equal("文档不可用", unavailable.Title);
        Assert.Null(unavailable.DocumentType);
        Assert.Null(unavailable.LifecycleStatus);
        var json = await client.GetStringAsync("/api/analysis/tree");
        Assert.DoesNotContain("canonical new title", json);
        Assert.DoesNotContain("bodyMarkdown", json);
        Assert.DoesNotContain("summary", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("attachment", json, StringComparison.OrdinalIgnoreCase);
        using var readd = await client.PostAsJsonAsync("/api/analysis/document-placements",
            new AddAnalysisPlacementRequest(null, selected.KnowledgeDocumentId.Value, unavailableTree.TreeConcurrencyToken));
        await Code(readd, HttpStatusCode.UnprocessableEntity, "reference_invalid");
        using var removeRequest = Request(HttpMethod.Delete, $"/api/analysis/document-placements/{selected.Id}",
            new RemoveAnalysisNodeRequest(selected.ConcurrencyToken, unavailableTree.TreeConcurrencyToken));
        using var removed = await client.SendAsync(removeRequest);
        Assert.Equal(6, (await Mutation(removed)).Items.Count);
    }

    [Fact]
    public async Task Atomic_create_reuses_actor_normalization_revision_and_fts_and_rolls_back_after_inner_commit()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var editorId = await User(factory, AccessLevel.Editor);
        using var client = await factory.CreateAuthenticatedClientAsync(editorId);
        var tree = await Tree(client);
        using var create = await client.PostAsJsonAsync("/api/analysis/documents",
            new CreateAnalysisDocumentRequest(null, tree.TreeConcurrencyToken, null, "  Analysis created  ", " summary ", "one\r\ntwo"));
        var created = await Mutation(create, HttpStatusCode.Created);
        var documentId = created.Node!.KnowledgeDocumentId!.Value;
        var document = await client.GetFromJsonAsync<JsonElement>($"/api/knowledge-documents/{documentId}");
        Assert.Equal("DesignNote", document.GetProperty("documentType").GetString());
        Assert.Equal("Draft", document.GetProperty("lifecycleStatus").GetString());
        Assert.Equal("Unknown", document.GetProperty("knowledgeStatus").GetString());
        Assert.Equal("Analysis created", document.GetProperty("title").GetString());
        Assert.Equal("one\ntwo", document.GetProperty("bodyMarkdown").GetString());
        Assert.Equal(editorId, document.GetProperty("createdByUserId").GetInt64());
        Assert.Equal(1, document.GetProperty("currentRevisionNumber").GetInt64());
        using var disallowed = await client.PostAsJsonAsync("/api/analysis/documents",
            new CreateAnalysisDocumentRequest(null, created.TreeConcurrencyToken, "Requirement", "bad type", null, null));
        await Code(disallowed, HttpStatusCode.BadRequest, "validation_error");
        using var invalid = await client.PostAsJsonAsync("/api/analysis/documents",
            new CreateAnalysisDocumentRequest(null, created.TreeConcurrencyToken, "KnowledgeArticle", "  ", null, null));
        await Code(invalid, HttpStatusCode.BadRequest, "validation_error");
        using (var setup = factory.Services.CreateScope())
        {
            var db = setup.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.Equal(1, await db.KnowledgeDocumentRevisions.CountAsync(revision => revision.KnowledgeDocumentId == documentId));
            Assert.Equal(1, await Scalar(db, $"SELECT count(*) FROM knowledge_documents_fts WHERE rowid={documentId}"));
            await db.Database.ExecuteSqlRawAsync("CREATE TRIGGER analysis_fail_placement BEFORE INSERT ON analysis_nodes BEGIN SELECT RAISE(ABORT, 'analysis test failure'); END;");
        }
        try
        {
            using var scope = factory.Services.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<AnalysisWorkspaceService>();
            await Assert.ThrowsAsync<DbUpdateException>(() => service.CreateDocument(
                new(null, created.TreeConcurrencyToken, "KnowledgeArticle", "Rollback canary", null, "rollback body"),
                new KnowledgeDocumentAuthor(editorId, "Analysis actor"), CancellationToken.None));
        }
        finally
        {
            using var scope = factory.Services.CreateScope();
            await scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>().Database.ExecuteSqlRawAsync("DROP TRIGGER analysis_fail_placement;");
        }
        using var verify = factory.Services.CreateScope();
        var verificationDb = verify.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await verificationDb.KnowledgeDocuments.AnyAsync(row => row.Title == "Rollback canary"));
        Assert.False(await verificationDb.KnowledgeDocumentRevisions.AnyAsync(row => row.Title == "Rollback canary"));
        Assert.Equal(0, await Scalar(verificationDb, "SELECT count(*) FROM knowledge_documents_fts WHERE title='Rollback canary'"));
        Assert.Equal(1, await verificationDb.AnalysisNodes.CountAsync());
        Assert.Equal(created.TreeConcurrencyToken, (await Tree(client)).TreeConcurrencyToken);
    }
}
