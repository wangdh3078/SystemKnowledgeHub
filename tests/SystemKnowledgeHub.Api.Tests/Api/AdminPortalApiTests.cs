using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AdminPortalApiTests
{
    [Fact]
    public async Task Admin_routes_require_administrator_and_antiforgery()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/admin/portal/pages")).StatusCode);

        var viewerId = await CreateUser(factory, AccessLevel.Viewer, "Portal viewer");
        var editorId = await CreateUser(factory, AccessLevel.Editor, "Portal editor");
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        Assert.Equal(HttpStatusCode.Forbidden, (await viewer.GetAsync("/api/admin/portal/pages")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await editor.GetAsync("/api/admin/portal/pages")).StatusCode);

        using var administrator = factory.CreateAuthenticatedClient();
        Assert.Equal(HttpStatusCode.OK, (await administrator.GetAsync("/api/admin/portal/pages")).StatusCode);
        using var noCsrf = factory.CreateAuthenticatedClientWithoutAntiforgery();
        var systemId = await FirstSystemId(factory);
        using var rejected = await noCsrf.PostAsJsonAsync("/api/admin/portal/pages", PageRequest("CSRF", systemId));
        Assert.Equal(HttpStatusCode.Forbidden, rejected.StatusCode);
    }

    [Fact]
    public async Task Page_composition_is_whole_replace_concurrent_and_published_safe()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var systemId = await FirstSystemId(factory);
        var first = await CreatePage(client, "Portal composition", systemId);
        var pageId = first.GetProperty("id").GetInt64();
        var token = first.GetProperty("concurrencyToken").GetString()!;
        var sectionId = first.GetProperty("sections")[0].GetProperty("id").GetInt64();

        using var updatedResponse = await client.PutAsJsonAsync($"/api/admin/portal/pages/{pageId}", new
        {
            title = "Portal composition updated",
            primaryTarget = new { type = "System", id = systemId },
            sections = new[]
            {
                new { id = (long?)sectionId, heading = "系统概览", sourceKind = "PrimaryTarget", referenceTarget = (object?)null, projectionKind = "StructuredOverview", sortOrder = 0 },
                new { id = (long?)null, heading = "摘要", sourceKind = "ExplicitReference", referenceTarget = (object?)new { type = "System", id = systemId }, projectionKind = "Summary", sortOrder = 1 },
            },
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.OK, updatedResponse.StatusCode);
        var updated = await updatedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(2, updated.GetProperty("sections").GetArrayLength());

        using var stale = await client.PutAsJsonAsync($"/api/admin/portal/pages/{pageId}", new
        {
            title = "stale",
            primaryTarget = new { type = "System", id = systemId },
            sections = Array.Empty<object>(),
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);

        var currentToken = updated.GetProperty("concurrencyToken").GetString()!;
        using var publishedResponse = await client.PostAsJsonAsync($"/api/admin/portal/pages/{pageId}/publish", new { concurrencyToken = currentToken });
        Assert.Equal(HttpStatusCode.OK, publishedResponse.StatusCode);
        var published = await publishedResponse.Content.ReadFromJsonAsync<JsonElement>();
        using var blocked = await client.PutAsJsonAsync($"/api/admin/portal/pages/{pageId}", new
        {
            title = "blocked",
            primaryTarget = new { type = "System", id = systemId },
            sections = Array.Empty<object>(),
            concurrencyToken = published.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, blocked.StatusCode);
        Assert.Equal("invalid_state", (await blocked.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());

        using var unpublishedResponse = await client.PostAsJsonAsync($"/api/admin/portal/pages/{pageId}/unpublish", new
        {
            concurrencyToken = published.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, unpublishedResponse.StatusCode);
        var unpublished = await unpublishedResponse.Content.ReadFromJsonAsync<JsonElement>();
        var createdSectionId = updated.GetProperty("sections")[1].GetProperty("id").GetInt64();
        using var reorderedResponse = await client.PutAsJsonAsync($"/api/admin/portal/pages/{pageId}", new
        {
            title = "Portal composition updated",
            primaryTarget = new { type = "System", id = systemId },
            sections = new[]
            {
                new { id = (long?)createdSectionId, heading = "摘要在前", sourceKind = "ExplicitReference", referenceTarget = (object?)new { type = "System", id = systemId }, projectionKind = "Summary", sortOrder = 0 },
                new { id = (long?)sectionId, heading = "系统概览", sourceKind = "PrimaryTarget", referenceTarget = (object?)null, projectionKind = "StructuredOverview", sortOrder = 1 },
            },
            concurrencyToken = unpublished.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, reorderedResponse.StatusCode);
        var reordered = await reorderedResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(createdSectionId, reordered.GetProperty("sections")[0].GetProperty("id").GetInt64());
        using var removedResponse = await client.PutAsJsonAsync($"/api/admin/portal/pages/{pageId}", new
        {
            title = "Portal composition updated",
            primaryTarget = new { type = "System", id = systemId },
            sections = new[]
            {
                new { id = (long?)createdSectionId, heading = "摘要在前", sourceKind = "ExplicitReference", referenceTarget = (object?)new { type = "System", id = systemId }, projectionKind = "Summary", sortOrder = 0 },
            },
            concurrencyToken = reordered.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, removedResponse.StatusCode);
        Assert.Equal(1, (await removedResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sections").GetArrayLength());
    }

    [Fact]
    public async Task Tree_publication_requires_ancestors_and_page_then_unpublish_hides_effectively()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var systemId = await FirstSystemId(factory);
        var page = await CreatePage(client, "Visible page", systemId);
        var pageId = page.GetProperty("id").GetInt64();
        using var pagePublish = await client.PostAsJsonAsync($"/api/admin/portal/pages/{pageId}/publish", new { concurrencyToken = page.GetProperty("concurrencyToken").GetString() });
        Assert.Equal(HttpStatusCode.OK, pagePublish.StatusCode);
        var publishedPage = await pagePublish.Content.ReadFromJsonAsync<JsonElement>();

        var root = await CreateNode(client, "MES", "Folder", null, null, 0);
        var rootId = root.GetProperty("nodeId").GetInt64();
        var child = await CreateNode(client, "生产管理", "Folder", rootId, null, 0);
        var childId = child.GetProperty("nodeId").GetInt64();
        var placement = await CreateNode(client, "Visible page", "Page", childId, pageId, 0);
        var placementId = placement.GetProperty("nodeId").GetInt64();

        using var premature = await client.PostAsJsonAsync($"/api/admin/portal/nodes/{childId}/publish", new { concurrencyToken = child.GetProperty("concurrencyToken").GetString() });
        Assert.Equal(HttpStatusCode.Conflict, premature.StatusCode);
        await AssertSuccess(client.PostAsJsonAsync($"/api/admin/portal/nodes/{rootId}/publish", new { concurrencyToken = root.GetProperty("concurrencyToken").GetString() }));
        await AssertSuccess(client.PostAsJsonAsync($"/api/admin/portal/nodes/{childId}/publish", new { concurrencyToken = child.GetProperty("concurrencyToken").GetString() }));
        await AssertSuccess(client.PostAsJsonAsync($"/api/admin/portal/nodes/{placementId}/publish", new { concurrencyToken = placement.GetProperty("concurrencyToken").GetString() }));

        using var anonymous = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/portal/pages/{pageId}")).StatusCode);
        using var pageUnpublish = await client.PostAsJsonAsync($"/api/admin/portal/pages/{pageId}/unpublish", new
        {
            concurrencyToken = publishedPage.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, pageUnpublish.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/portal/pages/{pageId}")).StatusCode);
        var unpublishedPage = await pageUnpublish.Content.ReadFromJsonAsync<JsonElement>();
        using var pageRepublish = await client.PostAsJsonAsync($"/api/admin/portal/pages/{pageId}/publish", new
        {
            concurrencyToken = unpublishedPage.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, pageRepublish.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await anonymous.GetAsync($"/api/portal/pages/{pageId}")).StatusCode);
        using var tree = await client.GetAsync("/api/admin/portal/tree");
        var treeJson = await tree.Content.ReadFromJsonAsync<JsonElement>();
        var rootCurrent = treeJson.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("nodeId").GetInt64() == rootId);
        await AssertSuccess(client.PostAsJsonAsync($"/api/admin/portal/nodes/{rootId}/unpublish", new { concurrencyToken = rootCurrent.GetProperty("concurrencyToken").GetString() }));
        Assert.Equal(HttpStatusCode.NotFound, (await anonymous.GetAsync($"/api/portal/pages/{pageId}")).StatusCode);

        using var after = await client.GetAsync("/api/admin/portal/tree");
        var afterJson = await after.Content.ReadFromJsonAsync<JsonElement>();
        var childAfter = afterJson.GetProperty("items").EnumerateArray().Single(item => item.GetProperty("nodeId").GetInt64() == childId);
        Assert.True(childAfter.GetProperty("isPublished").GetBoolean());
        Assert.False(childAfter.GetProperty("isEffectivelyPublished").GetBoolean());
    }

    [Fact]
    public async Task Reorder_is_atomic_and_rejects_stale_or_incomplete_sibling_sets()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var first = await CreateNode(client, "A", "Folder", null, null, 0);
        var second = await CreateNode(client, "B", "Folder", null, null, 1);
        var firstId = first.GetProperty("nodeId").GetInt64();
        var secondId = second.GetProperty("nodeId").GetInt64();
        using var reordered = await client.PutAsJsonAsync("/api/admin/portal/nodes/reorder", new
        {
            parentId = (long?)null,
            items = new[]
            {
                new { id = secondId, concurrencyToken = second.GetProperty("concurrencyToken").GetString() },
                new { id = firstId, concurrencyToken = first.GetProperty("concurrencyToken").GetString() },
            },
        });
        Assert.Equal(HttpStatusCode.OK, reordered.StatusCode);
        var result = await reordered.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(secondId, result.GetProperty("items")[0].GetProperty("nodeId").GetInt64());

        using var stale = await client.PutAsJsonAsync("/api/admin/portal/nodes/reorder", new
        {
            parentId = (long?)null,
            items = new[]
            {
                new { id = firstId, concurrencyToken = first.GetProperty("concurrencyToken").GetString() },
                new { id = secondId, concurrencyToken = second.GetProperty("concurrencyToken").GetString() },
            },
        });
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        using var incomplete = await client.PutAsJsonAsync("/api/admin/portal/nodes/reorder", new
        {
            parentId = (long?)null,
            items = new[] { new { id = firstId, concurrencyToken = "v1_AAAAAAAAAAE" } },
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, incomplete.StatusCode);
    }

    [Fact]
    public async Task Inventory_picker_and_preview_are_paged_safe_and_do_not_copy_knowledge()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var systemId = await FirstSystemId(factory);
        var page = await CreatePage(client, "Searchable portal", systemId);
        var pageId = page.GetProperty("id").GetInt64();

        using var picker = await client.GetAsync("/api/admin/portal/targets?type=System&search=&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, picker.StatusCode);
        var pickerJson = await picker.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(pickerJson.GetProperty("total").GetInt32() > 0);
        Assert.DoesNotContain("connectionString", pickerJson.ToString(), StringComparison.OrdinalIgnoreCase);

        using var inventory = await client.GetAsync("/api/admin/portal/pages?page=1&pageSize=20&search=Searchable");
        Assert.Equal(HttpStatusCode.OK, inventory.StatusCode);
        var inventoryJson = await inventory.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(1, inventoryJson.GetProperty("total").GetInt32());
        Assert.DoesNotContain("version", inventoryJson.ToString(), StringComparison.OrdinalIgnoreCase);

        using var preview = await client.GetAsync($"/api/admin/portal/pages/{pageId}/preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewJson = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.True(previewJson.GetProperty("readiness").GetProperty("canPublish").GetBoolean());
        Assert.Equal("Searchable portal", previewJson.GetProperty("page").GetProperty("title").GetString());
        Assert.DoesNotContain("createdBy", previewJson.ToString(), StringComparison.OrdinalIgnoreCase);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var stored = await db.PortalPages.AsNoTracking().Include(item => item.Sections).SingleAsync(item => item.Id == pageId);
        Assert.DoesNotContain("Searchable portal", stored.Sections.Select(item => item.Heading));
        Assert.Empty(await db.KnowledgeRelations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Picker_supports_all_five_types_and_draft_document_blocks_publication_with_safe_message()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var (documentId, _) = await SeedDocumentAndIntegration(factory, published: false);
        using var client = factory.CreateAuthenticatedClient();
        foreach (var type in new[] { "System", "BusinessFunction", "DatabaseObject", "KnowledgeDocument", "Integration" })
        {
            using var response = await client.GetAsync($"/api/admin/portal/targets?type={type}&page=1&pageSize=20");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            var json = await response.Content.ReadFromJsonAsync<JsonElement>();
            Assert.True(json.GetProperty("total").GetInt32() > 0, type);
            Assert.DoesNotContain("secret", json.ToString(), StringComparison.OrdinalIgnoreCase);
        }
        using var searched = await client.GetAsync("/api/admin/portal/targets?type=KnowledgeDocument&search=Lot%20Track%20In&page=1&pageSize=50");
        Assert.Equal(HttpStatusCode.OK, searched.StatusCode);
        var searchedJson = await searched.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(50, searchedJson.GetProperty("pageSize").GetInt32());
        Assert.Equal(1, searchedJson.GetProperty("total").GetInt32());
        Assert.Equal("Sop", searchedJson.GetProperty("items")[0].GetProperty("documentType").GetString());
        Assert.Equal("Draft", searchedJson.GetProperty("items")[0].GetProperty("lifecycle").GetString());
        Assert.Equal(HttpStatusCode.BadRequest, (await client.GetAsync("/api/admin/portal/targets?type=KnowledgeDocument&page=1&pageSize=25")).StatusCode);

        using var created = await client.PostAsJsonAsync("/api/admin/portal/pages", new
        {
            title = "Draft document page",
            primaryTarget = new { type = "KnowledgeDocument", id = documentId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var page = await created.Content.ReadFromJsonAsync<JsonElement>();
        using var preview = await client.GetAsync($"/api/admin/portal/pages/{page.GetProperty("id").GetInt64()}/preview");
        Assert.Equal(HttpStatusCode.OK, preview.StatusCode);
        var previewJson = await preview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(previewJson.GetProperty("readiness").GetProperty("canPublish").GetBoolean());
        Assert.Contains("草稿", previewJson.GetProperty("readiness").GetProperty("blockers").ToString());
        Assert.DoesNotContain(documentId.ToString(), previewJson.GetProperty("readiness").ToString(), StringComparison.Ordinal);

        using var publish = await client.PostAsJsonAsync(
            $"/api/admin/portal/pages/{page.GetProperty("id").GetInt64()}/publish",
            new { concurrencyToken = page.GetProperty("concurrencyToken").GetString() });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, publish.StatusCode);
        Assert.Equal("reference_invalid", (await publish.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Cross_page_section_stealing_and_tree_cycle_are_rejected_without_partial_write()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var systemId = await FirstSystemId(factory);
        var first = await CreatePage(client, "First", systemId);
        var second = await CreatePage(client, "Second", systemId);
        var foreignSectionId = second.GetProperty("sections")[0].GetProperty("id").GetInt64();
        using var steal = await client.PutAsJsonAsync($"/api/admin/portal/pages/{first.GetProperty("id").GetInt64()}", new
        {
            title = "First changed",
            primaryTarget = new { type = "System", id = systemId },
            sections = new[]
            {
                new { id = foreignSectionId, heading = "steal", sourceKind = "PrimaryTarget", referenceTarget = (object?)null, projectionKind = "Summary", sortOrder = 0 },
            },
            concurrencyToken = first.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, steal.StatusCode);
        Assert.Equal("reference_invalid", (await steal.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        using var unchanged = await client.GetAsync($"/api/admin/portal/pages/{first.GetProperty("id").GetInt64()}");
        Assert.Equal("First", (await unchanged.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("title").GetString());

        var root = await CreateNode(client, "Root", "Folder", null, null, 0);
        var child = await CreateNode(client, "Child", "Folder", root.GetProperty("nodeId").GetInt64(), null, 0);
        using var cycle = await client.PutAsJsonAsync($"/api/admin/portal/nodes/{root.GetProperty("nodeId").GetInt64()}", new
        {
            title = "Root",
            nodeKind = "Folder",
            parentId = child.GetProperty("nodeId").GetInt64(),
            portalPageId = (long?)null,
            sortOrder = 0,
            concurrencyToken = root.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.BadRequest, cycle.StatusCode);
        using var deleteParent = await client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, $"/api/admin/portal/nodes/{root.GetProperty("nodeId").GetInt64()}")
        {
            Content = JsonContent.Create(new { concurrencyToken = root.GetProperty("concurrencyToken").GetString() }),
        });
        Assert.Equal(HttpStatusCode.UnprocessableEntity, deleteParent.StatusCode);
    }

    [Fact]
    public async Task Page_delete_requires_no_placement_and_only_removes_composition_metadata()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var systemId = await FirstSystemId(factory);
        var page = await CreatePage(client, "Disposable composition", systemId);
        var pageId = page.GetProperty("id").GetInt64();
        var node = await CreateNode(client, "Disposable placement", "Page", null, pageId, 0);

        using var blocked = await Delete(client, $"/api/admin/portal/pages/{pageId}", page.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
        using var removedNode = await Delete(client, $"/api/admin/portal/nodes/{node.GetProperty("nodeId").GetInt64()}", node.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(HttpStatusCode.NoContent, removedNode.StatusCode);
        using var removedPage = await Delete(client, $"/api/admin/portal/pages/{pageId}", page.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(HttpStatusCode.NoContent, removedPage.StatusCode);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.True((await db.PortalPages.IgnoreQueryFilters().SingleAsync(item => item.Id == pageId)).IsDeleted);
        Assert.True(await db.Systems.AnyAsync(item => item.Id == systemId));
        Assert.Empty(await db.PortalPageSections.IgnoreQueryFilters().Where(item => item.PortalPageId == pageId).ToListAsync());
        Assert.Empty(await db.KnowledgeRelations.AsNoTracking().ToListAsync());
    }

    [Fact]
    public async Task Section_validation_rejects_incompatible_derived_unsupported_and_limit_excess_without_partial_write()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var (documentId, _) = await SeedDocumentAndIntegration(factory, published: true);
        using var client = factory.CreateAuthenticatedClient();
        var systemId = await FirstSystemId(factory);
        var page = await CreatePage(client, "Validation baseline", systemId);
        var pageId = page.GetProperty("id").GetInt64();
        var token = page.GetProperty("concurrencyToken").GetString();

        foreach (var section in new object[]
        {
            new { id = (long?)null, heading = "wrong target", sourceKind = "PrimaryTarget", referenceTarget = (object?)null, projectionKind = "KnowledgeDocumentBody", sortOrder = 0 },
            new { id = (long?)null, heading = "derived", sourceKind = "Derived", referenceTarget = (object?)null, projectionKind = "RelatedKnowledge", sortOrder = 0 },
            new { id = (long?)null, heading = "deferred", sourceKind = "ExplicitReference", referenceTarget = (object?)new { type = "KnowledgeDocument", id = documentId }, projectionKind = "AttachmentList", sortOrder = 0 },
        })
        {
            using var response = await client.PutAsJsonAsync($"/api/admin/portal/pages/{pageId}", new
            {
                title = "must not persist",
                primaryTarget = new { type = "System", id = systemId },
                sections = new[] { section },
                concurrencyToken = token,
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }

        var tooMany = Enumerable.Range(0, 31).Select(index => new
        {
            id = (long?)null,
            heading = $"Section {index}",
            sourceKind = "PrimaryTarget",
            referenceTarget = (object?)null,
            projectionKind = "Summary",
            sortOrder = index,
        }).ToArray();
        using var tooManyResponse = await client.PutAsJsonAsync($"/api/admin/portal/pages/{pageId}", new
        {
            title = "must not persist",
            primaryTarget = new { type = "System", id = systemId },
            sections = tooMany,
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooManyResponse.StatusCode);

        var tooManyBodies = Enumerable.Range(0, 6).Select(index => new
        {
            id = (long?)null,
            heading = $"Body {index}",
            sourceKind = "ExplicitReference",
            referenceTarget = new { type = "KnowledgeDocument", id = documentId },
            projectionKind = "KnowledgeDocumentBody",
            sortOrder = index,
        }).ToArray();
        using var tooManyBodiesResponse = await client.PutAsJsonAsync($"/api/admin/portal/pages/{pageId}", new
        {
            title = "must not persist",
            primaryTarget = new { type = "System", id = systemId },
            sections = tooManyBodies,
            concurrencyToken = token,
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooManyBodiesResponse.StatusCode);

        using var unchanged = await client.GetAsync($"/api/admin/portal/pages/{pageId}");
        var unchangedJson = await unchanged.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Validation baseline", unchangedJson.GetProperty("title").GetString());
        Assert.Equal(1, unchangedJson.GetProperty("sections").GetArrayLength());
    }

    [Fact]
    public async Task Archived_deleted_and_unsupported_targets_are_reported_as_safe_publication_blockers()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var (documentId, _) = await SeedDocumentAndIntegration(factory, published: true);
        using var client = factory.CreateAuthenticatedClient();
        using var created = await client.PostAsJsonAsync("/api/admin/portal/pages", new
        {
            title = "Lifecycle blockers",
            primaryTarget = new { type = "KnowledgeDocument", id = documentId },
        });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var page = await created.Content.ReadFromJsonAsync<JsonElement>();
        var pageId = page.GetProperty("id").GetInt64();

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var document = await db.KnowledgeDocuments.SingleAsync(item => item.Id == documentId);
            document.LifecycleStatus = DocumentLifecycleStatus.Archived;
            document.ArchivedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync();
        }
        using var archivedPreview = await client.GetAsync($"/api/admin/portal/pages/{pageId}/preview");
        var archivedJson = await archivedPreview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(archivedJson.GetProperty("readiness").GetProperty("canPublish").GetBoolean());
        Assert.Contains("已归档", archivedJson.GetProperty("readiness").ToString());

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var document = await db.KnowledgeDocuments.IgnoreQueryFilters().SingleAsync(item => item.Id == documentId);
            document.IsDeleted = true;
            document.DeletedAt = DateTimeOffset.UtcNow;
            document.DeletedByUserId = await db.Users.Select(item => item.Id).FirstAsync();
            document.DeletedByDisplayName = "Portal fixture";
            var section = await db.PortalPageSections.SingleAsync(item => item.PortalPageId == pageId);
            section.ProjectionKind = SystemKnowledgeHub.Api.Features.Portal.Domain.PortalPageProjectionKind.AttachmentList;
            await db.SaveChangesAsync();
        }
        using var detail = await client.GetAsync($"/api/admin/portal/pages/{pageId}");
        Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        var detailJson = await detail.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("引用已失效", detailJson.GetProperty("primaryTarget").GetProperty("title").GetString());
        Assert.False(detailJson.GetProperty("referenceHealth").GetProperty("isHealthy").GetBoolean());
        Assert.False(detailJson.GetProperty("sections")[0].GetProperty("isHealthy").GetBoolean());
        using var brokenPreview = await client.GetAsync($"/api/admin/portal/pages/{pageId}/preview");
        Assert.Equal(HttpStatusCode.OK, brokenPreview.StatusCode);
        var brokenJson = await brokenPreview.Content.ReadFromJsonAsync<JsonElement>();
        Assert.False(brokenJson.GetProperty("readiness").GetProperty("canPublish").GetBoolean());
        Assert.Equal(JsonValueKind.Null, brokenJson.GetProperty("page").ValueKind);
        Assert.DoesNotContain(documentId.ToString(), brokenJson.GetProperty("readiness").ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Node_move_rename_depth_and_published_mutation_rules_are_enforced()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var systemId = await FirstSystemId(factory);
        var page = await CreatePage(client, "Node rules", systemId);
        var pageNode = await CreateNode(client, "Page before publish", "Page", null, page.GetProperty("id").GetInt64(), 0);
        using var pageNodePublish = await client.PostAsJsonAsync($"/api/admin/portal/nodes/{pageNode.GetProperty("nodeId").GetInt64()}/publish", new
        {
            concurrencyToken = pageNode.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, pageNodePublish.StatusCode);

        var firstRoot = await CreateNode(client, "A", "Folder", null, null, 1);
        var secondRoot = await CreateNode(client, "B", "Folder", null, null, 2);
        var child = await CreateNode(client, "Child", "Folder", firstRoot.GetProperty("nodeId").GetInt64(), null, 0);
        using var moved = await client.PutAsJsonAsync($"/api/admin/portal/nodes/{child.GetProperty("nodeId").GetInt64()}", new
        {
            title = "Child renamed",
            nodeKind = "Folder",
            parentId = secondRoot.GetProperty("nodeId").GetInt64(),
            portalPageId = (long?)null,
            sortOrder = 0,
            concurrencyToken = child.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, moved.StatusCode);
        var movedJson = await moved.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("Child renamed", movedJson.GetProperty("title").GetString());
        Assert.Equal(secondRoot.GetProperty("nodeId").GetInt64(), movedJson.GetProperty("parentNodeId").GetInt64());
        await AssertSuccess(client.PostAsJsonAsync($"/api/admin/portal/nodes/{secondRoot.GetProperty("nodeId").GetInt64()}/publish", new
        {
            concurrencyToken = secondRoot.GetProperty("concurrencyToken").GetString(),
        }));
        using var childPublish = await client.PostAsJsonAsync($"/api/admin/portal/nodes/{movedJson.GetProperty("nodeId").GetInt64()}/publish", new
        {
            concurrencyToken = movedJson.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, childPublish.StatusCode);
        var publishedChild = await childPublish.Content.ReadFromJsonAsync<JsonElement>();
        using var publishedUpdate = await client.PutAsJsonAsync($"/api/admin/portal/nodes/{movedJson.GetProperty("nodeId").GetInt64()}", new
        {
            title = "blocked",
            nodeKind = "Folder",
            parentId = secondRoot.GetProperty("nodeId").GetInt64(),
            portalPageId = (long?)null,
            sortOrder = 0,
            concurrencyToken = publishedChild.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.Conflict, publishedUpdate.StatusCode);

        var deep = await CreateNode(client, "Depth 1", "Folder", null, null, 3);
        for (var depth = 2; depth <= 10; depth++)
            deep = await CreateNode(client, $"Depth {depth}", "Folder", deep.GetProperty("nodeId").GetInt64(), null, 0);
        using var tooDeep = await client.PostAsJsonAsync("/api/admin/portal/nodes", new
        {
            title = "Depth 11",
            nodeKind = "Folder",
            parentId = deep.GetProperty("nodeId").GetInt64(),
            portalPageId = (long?)null,
            sortOrder = 0,
        });
        Assert.Equal(HttpStatusCode.BadRequest, tooDeep.StatusCode);
    }

    private static object PageRequest(string title, long systemId) =>
        new { title, primaryTarget = new { type = "System", id = systemId } };

    private static async Task<JsonElement> CreatePage(HttpClient client, string title, long systemId)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/portal/pages", PageRequest(title, systemId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> CreateNode(
        HttpClient client,
        string title,
        string kind,
        long? parentId,
        long? pageId,
        int sortOrder)
    {
        using var response = await client.PostAsJsonAsync("/api/admin/portal/nodes", new
        {
            title,
            nodeKind = kind,
            parentId,
            portalPageId = pageId,
            sortOrder,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task AssertSuccess(Task<HttpResponseMessage> request)
    {
        using var response = await request;
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static Task<HttpResponseMessage> Delete(HttpClient client, string path, string concurrencyToken) =>
        client.SendAsync(new HttpRequestMessage(HttpMethod.Delete, path)
        {
            Content = JsonContent.Create(new { concurrencyToken }),
        });

    private static async Task<long> FirstSystemId(BootstrapWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>()
            .Systems.AsNoTracking().Select(item => item.Id).FirstAsync();
    }

    private static async Task<long> CreateUser(
        BootstrapWebApplicationFactory factory,
        AccessLevel accessLevel,
        string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = displayName,
            EmployeeNo = $"PORTAL-{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.test",
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<(long DocumentId, long IntegrationId)> SeedDocumentAndIntegration(
        BootstrapWebApplicationFactory factory,
        bool published)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var systemId = await db.Systems.Select(item => item.Id).FirstAsync();
        var userId = await db.Users.Select(item => item.Id).FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var document = new KnowledgeDocument
        {
            DocumentType = DocumentType.Sop,
            Title = "Lot Track In 业务说明",
            Summary = "canonical summary",
            BodyMarkdown = "# canonical body",
            LifecycleStatus = published ? DocumentLifecycleStatus.Published : DocumentLifecycleStatus.Draft,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = "Portal fixture",
            KnowledgeStatusChangedByRole = "Administrator",
            CreatedByUserId = userId,
            CreatedByDisplayName = "Portal fixture",
            UpdatedByUserId = userId,
            UpdatedByDisplayName = "Portal fixture",
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = published ? now : null,
            LatestPublishedRevisionNumber = published ? 1 : null,
        };
        var integration = new Integration
        {
            Name = "MES outbound",
            IntegrationType = IntegrationType.HttpApi,
            SourceSystemId = systemId,
            SourcePartyName = "MES",
            TargetPartyName = "WMS",
            FlowDirection = IntegrationFlowDirection.OneWay,
            Purpose = "canonical integration",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByName = "Portal fixture",
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = "Portal fixture",
            KnowledgeStatusChangedByRole = "Administrator",
        };
        db.AddRange(document, integration);
        await db.SaveChangesAsync();
        return (document.Id, integration.Id);
    }
}
