using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class PortalAnonymousReadApiTests
{
    [Fact]
    public async Task Anonymous_tree_and_page_resolve_all_targets_and_only_allowlisted_projection_fields()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        using var client = factory.CreateClient();

        using var treeResponse = await client.GetAsync("/api/portal/tree");
        Assert.Equal(HttpStatusCode.OK, treeResponse.StatusCode);
        using var tree = JsonDocument.Parse(await treeResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, tree.RootElement.GetProperty("total").GetInt32());
        Assert.Equal("Portal root", tree.RootElement.GetProperty("items")[0].GetProperty("title").GetString());
        Assert.Equal(fixture.PageId, tree.RootElement.GetProperty("items")[1].GetProperty("pageId").GetInt64());

        using var pageResponse = await client.GetAsync($"/api/portal/pages/{fixture.PageId}");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        var json = await pageResponse.Content.ReadAsStringAsync();
        using var page = JsonDocument.Parse(json);
        Assert.Equal("Portal composite", page.RootElement.GetProperty("title").GetString());
        Assert.Equal("System", page.RootElement.GetProperty("primaryTarget").GetProperty("type").GetString());
        Assert.Equal("Portal root", page.RootElement.GetProperty("breadcrumb")[0].GetProperty("title").GetString());
        var kinds = page.RootElement.GetProperty("sections")
            .EnumerateArray()
            .Select(section => section.GetProperty("content").GetProperty("kind").GetString())
            .ToArray();
        Assert.Contains("Summary", kinds);
        Assert.Contains("KnowledgeDocumentBody", kinds);
        Assert.Contains("SystemOverview", kinds);
        Assert.Contains("BusinessFunctionOverview", kinds);
        Assert.Contains("DatabaseObjectOverview", kinds);
        Assert.Contains("IntegrationOverview", kinds);
        Assert.Contains("DatabaseStructure", kinds);
        Assert.Contains("AttachmentList", kinds);
        Assert.Contains("TrustSummary", kinds);
        Assert.Contains("RelatedKnowledge", kinds);
        Assert.Contains("# Published body", json);
        Assert.Contains("estimatedRows\":48000", json);
        Assert.DoesNotContain("portal-secret-repository", json);
        Assert.DoesNotContain("portal-secret-endpoint", json);
        Assert.DoesNotContain("portal-secret-technical-identity", json);
        Assert.DoesNotContain("portal-secret-status-reason", json);
        Assert.DoesNotContain("Portal audit actor", json);
        Assert.DoesNotContain("Portal evidence provider", json);

        foreach (var protectedPath in new[]
        {
            "/api/systems",
            "/api/database-objects",
            "/api/database-discovery/runs",
            "/api/users",
            "/api/knowledge-documents/1/attachments/1/content",
        })
        {
            using var protectedResponse = await client.GetAsync(protectedPath);
            Assert.Equal(HttpStatusCode.Unauthorized, protectedResponse.StatusCode);
        }

        using var writeResponse = await client.PostAsync("/api/portal/tree", content: null);
        Assert.Equal(HttpStatusCode.Unauthorized, writeResponse.StatusCode);
    }

    [Theory]
    [InlineData("Portal composite")]
    [InlineData("Portal System")]
    [InlineData("Portal Document")]
    [InlineData("Published body")]
    public async Task Search_matches_page_primary_explicit_and_published_document_body(string query)
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        using var response = await factory.CreateClient().GetAsync($"/api/portal/search?q={Uri.EscapeDataString(query)}&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(1, json.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(fixture.PageId, json.RootElement.GetProperty("items")[0].GetProperty("pageId").GetInt64());
        Assert.DoesNotContain("Portal audit actor", json.RootElement.ToString());
    }

    [Theory]
    [InlineData("", 1, 20)]
    [InlineData("x", 0, 20)]
    [InlineData("x", 1, 101)]
    public async Task Search_rejects_invalid_query_and_paging(string query, int page, int pageSize)
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var response = await factory.CreateClient().GetAsync($"/api/portal/search?q={query}&page={page}&pageSize={pageSize}");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("validation_error", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Search_accepts_exact_100_character_query_and_rejects_101()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var accepted = await factory.CreateClient().GetAsync($"/api/portal/search?q={new string('a', 100)}");
        using var rejected = await factory.CreateClient().GetAsync($"/api/portal/search?q={new string('a', 101)}");
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    [Fact]
    public async Task Search_paging_is_server_side_and_deterministic()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var first = await SeedCompositePage(factory, 0);
        var second = await SeedCompositePage(factory, 1);
        using var client = factory.CreateClient();

        using var firstPage = await client.GetAsync("/api/portal/search?q=Portal%20composite&page=1&pageSize=1");
        using var secondPage = await client.GetAsync("/api/portal/search?q=Portal%20composite&page=2&pageSize=1");
        using var firstJson = JsonDocument.Parse(await firstPage.Content.ReadAsStringAsync());
        using var secondJson = JsonDocument.Parse(await secondPage.Content.ReadAsStringAsync());

        Assert.Equal(2, firstJson.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(1, firstJson.RootElement.GetProperty("pageSize").GetInt32());
        Assert.Equal(first.PageId, firstJson.RootElement.GetProperty("items")[0].GetProperty("pageId").GetInt64());
        Assert.Equal(second.PageId, secondJson.RootElement.GetProperty("items")[0].GetProperty("pageId").GetInt64());
    }

    [Fact]
    public async Task Search_and_attachment_authorization_fail_closed_when_page_is_unpublished()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            (await db.PortalPages.SingleAsync(item => item.Id == fixture.PageId)).IsPublished = false;
            await db.SaveChangesAsync();
        }
        using var client = factory.CreateClient();
        using var search = await client.GetAsync("/api/portal/search?q=Published%20body");
        using var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        Assert.Equal(0, searchJson.RootElement.GetProperty("total").GetInt32());
        using var attachment = await client.GetAsync($"/api/portal/pages/{fixture.PageId}/attachments/1/download");
        Assert.Equal(HttpStatusCode.NotFound, attachment.StatusCode);
    }

    [Theory]
    [InlineData(DocumentLifecycleStatus.Draft)]
    [InlineData(DocumentLifecycleStatus.Archived)]
    public async Task Search_page_and_attachment_fail_closed_when_an_explicit_document_is_not_published(
        DocumentLifecycleStatus lifecycle)
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            (await db.KnowledgeDocuments.SingleAsync(item => item.Id == fixture.DocumentId)).LifecycleStatus = lifecycle;
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var search = await client.GetAsync("/api/portal/search?q=Published%20body");
        using var searchJson = JsonDocument.Parse(await search.Content.ReadAsStringAsync());
        Assert.Equal(0, searchJson.RootElement.GetProperty("total").GetInt32());
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/portal/pages/{fixture.PageId}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound,
            (await client.GetAsync($"/api/portal/pages/{fixture.PageId}/attachments/{fixture.PdfAttachmentId}/download")).StatusCode);
    }

    [Fact]
    public async Task Anonymous_attachment_delivery_is_page_scoped_current_revision_only_and_uses_safety_headers()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        using var client = factory.CreateClient();
        using var image = await client.GetAsync($"/api/portal/pages/{fixture.PageId}/attachments/{fixture.ImageAttachmentId}/content");
        Assert.Equal(HttpStatusCode.OK, image.StatusCode);
        Assert.Equal("image/png", image.Content.Headers.ContentType?.MediaType);
        Assert.Equal("nosniff", image.Headers.GetValues("X-Content-Type-Options").Single());
        Assert.Contains("inline", image.Content.Headers.ContentDisposition?.ToString());

        using var pdf = await client.GetAsync($"/api/portal/pages/{fixture.PageId}/attachments/{fixture.PdfAttachmentId}/preview");
        Assert.Equal(HttpStatusCode.OK, pdf.StatusCode);
        Assert.Equal("application/pdf", pdf.Content.Headers.ContentType?.MediaType);
        using var download = await client.GetAsync($"/api/portal/pages/{fixture.PageId}/attachments/{fixture.PdfAttachmentId}/download");
        Assert.Contains("attachment", download.Content.Headers.ContentDisposition?.ToString());

        using var wrongPage = await client.GetAsync($"/api/portal/pages/9007199254740991/attachments/{fixture.PdfAttachmentId}/download");
        Assert.Equal(HttpStatusCode.NotFound, wrongPage.StatusCode);
        Assert.DoesNotContain("objects/", await wrongPage.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Anonymous_attachment_delivery_rejects_non_current_references_and_reports_missing_storage_safely()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        string imagePath;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var image = await db.Attachments.SingleAsync(item => item.Id == fixture.ImageAttachmentId);
            imagePath = Path.Combine(factory.AttachmentStorageRoot, image.StorageKey.Replace('/', Path.DirectorySeparatorChar));
            File.Delete(imagePath);
        }

        using var client = factory.CreateClient();
        using var missing = await client.GetAsync($"/api/portal/pages/{fixture.PageId}/attachments/{fixture.ImageAttachmentId}/content");
        Assert.Equal(HttpStatusCode.ServiceUnavailable, missing.StatusCode);
        Assert.DoesNotContain(factory.AttachmentStorageRoot, await missing.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var revision = await db.KnowledgeDocumentRevisions.SingleAsync(item => item.KnowledgeDocumentId == fixture.DocumentId);
            revision.RevisionNumber = 2;
            await db.SaveChangesAsync();
        }
        using var historical = await client.GetAsync($"/api/portal/pages/{fixture.PageId}/attachments/{fixture.PdfAttachmentId}/download");
        Assert.Equal(HttpStatusCode.NotFound, historical.StatusCode);
    }

    [Fact]
    public async Task Trust_summary_keeps_direct_single_target_semantics_for_all_five_target_types()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        using var response = await factory.CreateClient().GetAsync($"/api/portal/pages/{fixture.PageId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var trust = json.RootElement.GetProperty("sections").EnumerateArray()
            .Where(section => section.GetProperty("content").GetProperty("kind").GetString() == "TrustSummary")
            .ToDictionary(section => section.GetProperty("heading").GetString()!, section => section.GetProperty("content"));

        Assert.Equal(5, trust.Count);
        Assert.Equal(1, trust["System trust"].GetProperty("evidenceCount").GetInt32());
        Assert.Equal(1, trust["System trust"].GetProperty("humanConfirmationCount").GetInt32());
        Assert.Equal(JsonValueKind.Null, trust["System trust"].GetProperty("confirmationCoverage").ValueKind);
        Assert.Equal("NoConfirmation", trust["Document trust"].GetProperty("confirmationCoverage").GetString());
        foreach (var heading in new[] { "Function trust", "Database trust", "Integration trust" })
        {
            Assert.Equal(0, trust[heading].GetProperty("evidenceCount").GetInt32());
            Assert.Equal(JsonValueKind.Null, trust[heading].GetProperty("confirmationCoverage").ValueKind);
        }
        Assert.DoesNotContain("Portal evidence provider", json.RootElement.ToString());
    }

    [Fact]
    public async Task Related_knowledge_is_bounded_deterministic_lifecycle_filtered_and_only_links_published_portal_pages()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        long linkedPageId;
        int relationCount;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            var functions = Enumerable.Range(0, 21).Select(index => new BusinessFunction
            {
                SystemId = fixture.SystemId,
                Name = $"related-{index:00}-{Guid.NewGuid():N}",
                DisplayName = $"Related {index:00}",
                FunctionType = "Workflow",
                CreatedAt = now,
                CreatedByUserId = userId,
                CreatedByName = "Related actor",
                UpdatedAt = now,
                KnowledgeStatus = KnowledgeStatus.Confirmed,
                KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = "Related actor",
                KnowledgeStatusChangedByRole = "Administrator",
            }).ToArray();
            var archived = new KnowledgeDocument
            {
                DocumentType = DocumentType.KnowledgeArticle,
                Title = "Archived related secret",
                BodyMarkdown = "must not render",
                LifecycleStatus = DocumentLifecycleStatus.Archived,
                KnowledgeStatus = KnowledgeStatus.Confirmed,
                KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = "Related actor",
                KnowledgeStatusChangedByRole = "Administrator",
                CreatedByUserId = userId,
                CreatedByDisplayName = "Related actor",
                UpdatedByUserId = userId,
                UpdatedByDisplayName = "Related actor",
                CreatedAt = now,
                UpdatedAt = now,
                CurrentRevisionNumber = 1,
            };
            db.AddRange(functions);
            db.Add(archived);
            await db.SaveChangesAsync();
            KnowledgeRelation Relation(KnowledgeTargetType sourceType, long sourceId, KnowledgeTargetType targetType, long targetId, RelationType type) => new()
            {
                SourceType = sourceType,
                SourceId = sourceId,
                TargetType = targetType,
                TargetId = targetId,
                RelationType = type,
                CreatedAt = now,
                CreatedByName = "Related actor",
                UpdatedAt = now,
                KnowledgeStatus = KnowledgeStatus.Confirmed,
                KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = "Related actor",
                KnowledgeStatusChangedByRole = "Administrator",
            };
            db.KnowledgeRelations.AddRange(functions.Select(item => Relation(
                KnowledgeTargetType.System, fixture.SystemId, KnowledgeTargetType.BusinessFunction, item.Id, RelationType.References)));
            db.KnowledgeRelations.Add(Relation(
                KnowledgeTargetType.DatabaseObject, fixture.DatabaseObjectId, KnowledgeTargetType.System, fixture.SystemId, RelationType.DependsOn));
            db.KnowledgeRelations.Add(Relation(
                KnowledgeTargetType.System, fixture.SystemId, KnowledgeTargetType.KnowledgeDocument, archived.Id, RelationType.References));

            var linkedPage = new PortalPage
            {
                Title = "Related portal page",
                PrimaryTargetType = PortalTargetType.BusinessFunction,
                PrimaryTargetId = functions[0].Id,
                IsPublished = true,
                PublishedAt = now,
                PublishedByUserId = userId,
                PublishedByDisplayName = "Related actor",
                CreatedAt = now,
                CreatedByUserId = userId,
                CreatedByDisplayName = "Related actor",
                UpdatedAt = now,
                UpdatedByUserId = userId,
                UpdatedByDisplayName = "Related actor",
                Sections =
                [
                    new PortalPageSection
                    {
                        Heading = "Summary",
                        SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
                        ProjectionKind = PortalPageProjectionKind.Summary,
                    },
                ],
            };
            db.PortalPages.Add(linkedPage);
            await db.SaveChangesAsync();
            var linkedNode = PublishedNode("Related portal page", PortalPageNodeKind.Page, 2, userId, now);
            linkedNode.PortalPageId = linkedPage.Id;
            db.PortalPageNodes.Add(linkedNode);
            await db.SaveChangesAsync();
            linkedPageId = linkedPage.Id;
            relationCount = await db.KnowledgeRelations.CountAsync();
        }

        using var response = await factory.CreateClient().GetAsync($"/api/portal/pages/{fixture.PageId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var related = json.RootElement.GetProperty("sections").EnumerateArray()
            .Single(section => section.GetProperty("content").GetProperty("kind").GetString() == "RelatedKnowledge")
            .GetProperty("content");
        var references = related.GetProperty("groups").EnumerateArray().Single(group =>
            group.GetProperty("relationType").GetString() == "References" && group.GetProperty("direction").GetString() == "Outgoing");
        var items = references.GetProperty("items").EnumerateArray().ToArray();
        Assert.Equal(20, items.Length);
        Assert.Equal(items.Select(item => item.GetProperty("targetTitle").GetString()).Order(StringComparer.Ordinal),
            items.Select(item => item.GetProperty("targetTitle").GetString()));
        Assert.Equal(linkedPageId, items.Single(item => item.GetProperty("targetTitle").GetString() == "Related 00").GetProperty("portalPageId").GetInt64());
        Assert.DoesNotContain("Archived related secret", related.ToString());
        var incoming = related.GetProperty("groups").EnumerateArray().Single(group =>
            group.GetProperty("relationType").GetString() == "DependsOn" && group.GetProperty("direction").GetString() == "Incoming");
        Assert.Equal(JsonValueKind.Null, incoming.GetProperty("items")[0].GetProperty("portalPageId").ValueKind);

        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationDb = verificationScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(relationCount, await verificationDb.KnowledgeRelations.CountAsync());
    }

    [Fact]
    public async Task Traceability_projects_published_requirement_specification_and_test_paths()
    {
        using var factory = new BootstrapWebApplicationFactory();
        long requirementPageId;
        long specificationPageId;
        long testPageId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            KnowledgeDocument Document(DocumentType type, string title) => new()
            {
                DocumentType = type,
                Title = title,
                BodyMarkdown = $"# {title}",
                LifecycleStatus = DocumentLifecycleStatus.Published,
                KnowledgeStatus = KnowledgeStatus.Confirmed,
                KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = "Trace actor",
                KnowledgeStatusChangedByRole = "Administrator",
                CreatedByUserId = userId,
                CreatedByDisplayName = "Trace actor",
                UpdatedByUserId = userId,
                UpdatedByDisplayName = "Trace actor",
                CreatedAt = now,
                UpdatedAt = now,
                PublishedAt = now,
                CurrentRevisionNumber = 1,
                LatestPublishedRevisionNumber = 1,
            };
            var requirement = Document(DocumentType.Requirement, "Lot Track requirement");
            var specification = Document(DocumentType.Specification, "Lot Track specification");
            var test = Document(DocumentType.TestCase, "Lot Track test");
            db.AddRange(requirement, specification, test);
            await db.SaveChangesAsync();
            KnowledgeRelation Relation(long sourceId, long targetId, RelationType type) => new()
            {
                SourceType = KnowledgeTargetType.KnowledgeDocument,
                SourceId = sourceId,
                TargetType = KnowledgeTargetType.KnowledgeDocument,
                TargetId = targetId,
                RelationType = type,
                CreatedAt = now,
                CreatedByName = "Trace actor",
                UpdatedAt = now,
                KnowledgeStatus = KnowledgeStatus.Confirmed,
                KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = "Trace actor",
                KnowledgeStatusChangedByRole = "Administrator",
            };
            db.KnowledgeRelations.AddRange(
                Relation(requirement.Id, specification.Id, RelationType.SpecifiedBy),
                Relation(specification.Id, test.Id, RelationType.VerifiedBy),
                Relation(requirement.Id, test.Id, RelationType.VerifiedBy));
            PortalPage TracePage(string title, KnowledgeDocument primary) => new()
            {
                Title = title,
                PrimaryTargetType = PortalTargetType.KnowledgeDocument,
                PrimaryTargetId = primary.Id,
                IsPublished = true,
                PublishedAt = now,
                PublishedByUserId = userId,
                PublishedByDisplayName = "Trace actor",
                CreatedAt = now,
                CreatedByUserId = userId,
                CreatedByDisplayName = "Trace actor",
                UpdatedAt = now,
                UpdatedByUserId = userId,
                UpdatedByDisplayName = "Trace actor",
                Sections = [new PortalPageSection { Heading = "Trace", SourceKind = PortalPageSectionSourceKind.Derived, ProjectionKind = PortalPageProjectionKind.Traceability }],
            };
            var requirementPage = TracePage("Requirement trace", requirement);
            var specificationPage = TracePage("Specification trace", specification);
            var testPage = TracePage("Test trace", test);
            db.PortalPages.AddRange(requirementPage, specificationPage, testPage);
            await db.SaveChangesAsync();
            var pages = new[] { requirementPage, specificationPage, testPage };
            foreach (var (page, index) in pages.Select((item, index) => (item, index)))
            {
                var node = PublishedNode(page.Title, PortalPageNodeKind.Page, index, userId, now);
                node.PortalPageId = page.Id;
                db.PortalPageNodes.Add(node);
            }
            await db.SaveChangesAsync();
            requirementPageId = requirementPage.Id;
            specificationPageId = specificationPage.Id;
            testPageId = testPage.Id;
        }

        using var client = factory.CreateClient();
        async Task<JsonDocument> Trace(long pageId)
        {
            using var response = await client.GetAsync($"/api/portal/pages/{pageId}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        }
        using var requirementJson = await Trace(requirementPageId);
        using var specificationJson = await Trace(specificationPageId);
        using var testJson = await Trace(testPageId);
        var requirementTrace = requirementJson.RootElement.GetProperty("sections")[0].GetProperty("content");
        var specificationTrace = specificationJson.RootElement.GetProperty("sections")[0].GetProperty("content");
        var testTrace = testJson.RootElement.GetProperty("sections")[0].GetProperty("content");
        Assert.Contains(requirementTrace.GetProperty("paths").EnumerateArray(), path => path.GetProperty("kind").GetString() == "DirectTest");
        Assert.Contains(requirementTrace.GetProperty("paths").EnumerateArray(), path => path.GetProperty("kind").GetString() == "SpecificationTest");
        Assert.Contains(specificationTrace.GetProperty("paths").EnumerateArray(), path => path.GetProperty("kind").GetString() == "UpstreamRequirement");
        Assert.Contains(specificationTrace.GetProperty("paths").EnumerateArray(), path => path.GetProperty("kind").GetString() == "SpecificationTest");
        Assert.Contains(testTrace.GetProperty("paths").EnumerateArray(), path => path.GetProperty("kind").GetString() == "DirectRequirement");
        Assert.Contains(testTrace.GetProperty("paths").EnumerateArray(), path => path.GetProperty("kind").GetString() == "RequirementSpecification");
        Assert.Empty(requirementTrace.GetProperty("missingLinkCodes").EnumerateArray());
        Assert.Equal(2, requirementTrace.GetProperty("limits").GetProperty("maxDepth").GetInt32());
    }

    [Fact]
    public async Task Traceability_reports_missing_links_cycles_lifecycle_filtering_and_hard_limits()
    {
        using var factory = new BootstrapWebApplicationFactory();
        long pageId;
        long requirementId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            var requirement = TraceDocument(DocumentType.Requirement, "Bounded requirement", DocumentLifecycleStatus.Published, userId, now);
            db.KnowledgeDocuments.Add(requirement);
            await db.SaveChangesAsync();
            var page = new PortalPage
            {
                Title = "Bounded trace",
                PrimaryTargetType = PortalTargetType.KnowledgeDocument,
                PrimaryTargetId = requirement.Id,
                IsPublished = true,
                PublishedAt = now,
                PublishedByUserId = userId,
                PublishedByDisplayName = "Trace actor",
                CreatedAt = now,
                CreatedByUserId = userId,
                CreatedByDisplayName = "Trace actor",
                UpdatedAt = now,
                UpdatedByUserId = userId,
                UpdatedByDisplayName = "Trace actor",
                Sections =
                [
                    new PortalPageSection
                    {
                        Heading = "Trace",
                        SourceKind = PortalPageSectionSourceKind.Derived,
                        ProjectionKind = PortalPageProjectionKind.Traceability,
                    },
                ],
            };
            db.PortalPages.Add(page);
            await db.SaveChangesAsync();
            var node = PublishedNode(page.Title, PortalPageNodeKind.Page, 0, userId, now);
            node.PortalPageId = page.Id;
            db.PortalPageNodes.Add(node);
            await db.SaveChangesAsync();
            pageId = page.Id;
            requirementId = requirement.Id;
        }

        using var client = factory.CreateClient();
        using (var missingResponse = await client.GetAsync($"/api/portal/pages/{pageId}"))
        using (var missingJson = JsonDocument.Parse(await missingResponse.Content.ReadAsStringAsync()))
        {
            var missing = missingJson.RootElement.GetProperty("sections")[0].GetProperty("content").GetProperty("missingLinkCodes");
            Assert.Contains(missing.EnumerateArray(), item => item.GetString() == "MissingSpecification");
            Assert.Contains(missing.EnumerateArray(), item => item.GetString() == "MissingTestDefinition");
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            var specifications = Enumerable.Range(0, 201)
                .Select(index => TraceDocument(DocumentType.Specification, $"Specification {index:000}", DocumentLifecycleStatus.Published, userId, now))
                .ToArray();
            var archivedTest = TraceDocument(DocumentType.TestCase, "Archived test secret", DocumentLifecycleStatus.Archived, userId, now);
            db.KnowledgeDocuments.AddRange(specifications);
            db.KnowledgeDocuments.Add(archivedTest);
            await db.SaveChangesAsync();
            KnowledgeRelation Relation(long source, long target, RelationType type) => new()
            {
                SourceType = KnowledgeTargetType.KnowledgeDocument,
                SourceId = source,
                TargetType = KnowledgeTargetType.KnowledgeDocument,
                TargetId = target,
                RelationType = type,
                CreatedAt = now,
                CreatedByName = "Trace actor",
                UpdatedAt = now,
                KnowledgeStatus = KnowledgeStatus.Confirmed,
                KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = "Trace actor",
                KnowledgeStatusChangedByRole = "Administrator",
            };
            db.KnowledgeRelations.AddRange(specifications.Select(item => Relation(requirementId, item.Id, RelationType.SpecifiedBy)));
            db.KnowledgeRelations.Add(Relation(specifications[0].Id, requirementId, RelationType.SpecifiedBy));
            db.KnowledgeRelations.Add(Relation(requirementId, archivedTest.Id, RelationType.VerifiedBy));
            await db.SaveChangesAsync();
        }

        using var boundedResponse = await client.GetAsync($"/api/portal/pages/{pageId}");
        Assert.Equal(HttpStatusCode.OK, boundedResponse.StatusCode);
        using var boundedJson = JsonDocument.Parse(await boundedResponse.Content.ReadAsStringAsync());
        var trace = boundedJson.RootElement.GetProperty("sections")[0].GetProperty("content");
        Assert.True(trace.GetProperty("cycleDetected").GetBoolean());
        Assert.True(trace.GetProperty("isTruncated").GetBoolean());
        Assert.Contains(trace.GetProperty("missingLinkCodes").EnumerateArray(), item => item.GetString() == "MissingTestDefinition");
        Assert.DoesNotContain("Archived test secret", trace.ToString());
        Assert.True(trace.GetProperty("paths").GetArrayLength() <= 200);
        Assert.Equal(200, trace.GetProperty("limits").GetProperty("maxNodes").GetInt32());
        Assert.Equal(300, trace.GetProperty("limits").GetProperty("maxEdges").GetInt32());
    }

    [Theory]
    [InlineData("page")]
    [InlineData("node")]
    [InlineData("ancestor")]
    [InlineData("primary-target")]
    [InlineData("function-deleted")]
    [InlineData("database-object-deleted")]
    [InlineData("document-deleted")]
    [InlineData("integration-deleted")]
    [InlineData("explicit-reference-draft")]
    [InlineData("explicit-reference-archived")]
    [InlineData("page-deleted")]
    [InlineData("node-deleted")]
    public async Task Anonymous_page_fails_closed_when_publication_or_target_eligibility_breaks(string caseName)
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            switch (caseName)
            {
                case "page":
                    (await db.PortalPages.SingleAsync(item => item.Id == fixture.PageId)).IsPublished = false;
                    break;
                case "node":
                    (await db.PortalPageNodes.SingleAsync(item => item.Id == fixture.PageNodeId)).IsPublished = false;
                    break;
                case "ancestor":
                    (await db.PortalPageNodes.SingleAsync(item => item.Id == fixture.RootNodeId)).IsPublished = false;
                    break;
                case "primary-target":
                    var deletedSystem = await db.Systems.SingleAsync(item => item.Id == fixture.SystemId);
                    deletedSystem.IsDeleted = true;
                    deletedSystem.DeletedAt = DateTimeOffset.UtcNow;
                    deletedSystem.DeletedByUserId = await db.Users.Select(item => item.Id).FirstAsync();
                    deletedSystem.DeletedByDisplayName = "Portal audit actor";
                    break;
                case "function-deleted":
                    var deletedFunction = await db.BusinessFunctions.SingleAsync(item => item.Id == fixture.FunctionId);
                    deletedFunction.IsDeleted = true;
                    deletedFunction.DeletedAt = DateTimeOffset.UtcNow;
                    deletedFunction.DeletedByUserId = await db.Users.Select(item => item.Id).FirstAsync();
                    deletedFunction.DeletedByDisplayName = "Portal audit actor";
                    break;
                case "database-object-deleted":
                    var deletedObject = await db.DatabaseObjects.SingleAsync(item => item.Id == fixture.DatabaseObjectId);
                    deletedObject.IsDeleted = true;
                    deletedObject.DeletedAt = DateTimeOffset.UtcNow;
                    deletedObject.DeletedByUserId = await db.Users.Select(item => item.Id).FirstAsync();
                    deletedObject.DeletedByDisplayName = "Portal audit actor";
                    break;
                case "document-deleted":
                    var deletedDocument = await db.KnowledgeDocuments.SingleAsync(item => item.Id == fixture.DocumentId);
                    deletedDocument.IsDeleted = true;
                    deletedDocument.DeletedAt = DateTimeOffset.UtcNow;
                    deletedDocument.DeletedByUserId = await db.Users.Select(item => item.Id).FirstAsync();
                    deletedDocument.DeletedByDisplayName = "Portal audit actor";
                    break;
                case "integration-deleted":
                    var deletedIntegration = await db.Integrations.SingleAsync(item => item.Id == fixture.IntegrationId);
                    deletedIntegration.IsDeleted = true;
                    deletedIntegration.DeletedAt = DateTimeOffset.UtcNow;
                    deletedIntegration.DeletedByUserId = await db.Users.Select(item => item.Id).FirstAsync();
                    deletedIntegration.DeletedByDisplayName = "Portal audit actor";
                    break;
                case "explicit-reference-draft":
                    (await db.KnowledgeDocuments.SingleAsync(item => item.Id == fixture.DocumentId)).LifecycleStatus = DocumentLifecycleStatus.Draft;
                    break;
                case "explicit-reference-archived":
                    (await db.KnowledgeDocuments.SingleAsync(item => item.Id == fixture.DocumentId)).LifecycleStatus = DocumentLifecycleStatus.Archived;
                    break;
                case "page-deleted":
                    var deletedPage = await db.PortalPages.SingleAsync(item => item.Id == fixture.PageId);
                    deletedPage.IsDeleted = true;
                    deletedPage.DeletedAt = DateTimeOffset.UtcNow;
                    deletedPage.DeletedByUserId = await db.Users.Select(item => item.Id).FirstAsync();
                    deletedPage.DeletedByDisplayName = "Portal audit actor";
                    break;
                case "node-deleted":
                    var deletedNode = await db.PortalPageNodes.SingleAsync(item => item.Id == fixture.PageNodeId);
                    deletedNode.IsDeleted = true;
                    deletedNode.DeletedAt = DateTimeOffset.UtcNow;
                    deletedNode.DeletedByUserId = await db.Users.Select(item => item.Id).FirstAsync();
                    deletedNode.DeletedByDisplayName = "Portal audit actor";
                    break;
                default:
                    throw new InvalidOperationException();
            }
            await db.SaveChangesAsync();
        }

        using var response = await factory.CreateClient().GetAsync($"/api/portal/pages/{fixture.PageId}");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("primary-target", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reference", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Attachment_list_projection_is_readable_and_keeps_the_page_in_the_tree()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var documentSection = await db.PortalPageSections
                .FirstAsync(item => item.ReferenceTargetType == PortalTargetType.KnowledgeDocument);
            documentSection.ProjectionKind = PortalPageProjectionKind.AttachmentList;
            await db.SaveChangesAsync();
        }

        using var client = factory.CreateClient();
        using var pageResponse = await client.GetAsync($"/api/portal/pages/{fixture.PageId}");
        Assert.Equal(HttpStatusCode.OK, pageResponse.StatusCode);
        using var page = JsonDocument.Parse(await pageResponse.Content.ReadAsStringAsync());
        Assert.Contains(
            page.RootElement.GetProperty("sections").EnumerateArray(),
            section => section.GetProperty("content").GetProperty("kind").GetString() == "AttachmentList");
        using var treeResponse = await client.GetAsync("/api/portal/tree");
        using var tree = JsonDocument.Parse(await treeResponse.Content.ReadAsStringAsync());
        Assert.Equal(2, tree.RootElement.GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task Invalid_page_id_returns_validation_error_without_resource_disclosure()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var response = await factory.CreateClient().GetAsync("/api/portal/pages/9007199254740992");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("validation_error", json.RootElement.GetProperty("code").GetString());
        Assert.True(json.RootElement.GetProperty("fieldErrors").TryGetProperty("id", out _));
    }

    [Fact]
    public async Task Missing_safe_page_id_returns_sanitized_not_found()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var response = await factory.CreateClient().GetAsync("/api/portal/pages/9007199254740991");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("not_found", json.RootElement.GetProperty("code").GetString());
        Assert.Equal(JsonValueKind.Null, json.RootElement.GetProperty("details").ValueKind);
    }

    [Theory]
    [InlineData(2000, HttpStatusCode.OK)]
    [InlineData(2001, HttpStatusCode.UnprocessableEntity)]
    public async Task Tree_enforces_the_exact_effective_node_limit(int nodeCount, HttpStatusCode expected)
    {
        using var factory = new BootstrapWebApplicationFactory();
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            db.PortalPageNodes.AddRange(Enumerable.Range(0, nodeCount).Select(index => new PortalPageNode
            {
                Title = $"Folder {index:D4}",
                NodeKind = PortalPageNodeKind.Folder,
                SortOrder = index,
                IsPublished = true,
                PublishedAt = now,
                PublishedByUserId = userId,
                PublishedByDisplayName = "Portal audit actor",
                CreatedAt = now,
                CreatedByUserId = userId,
                CreatedByDisplayName = "Portal audit actor",
                UpdatedAt = now,
                UpdatedByUserId = userId,
                UpdatedByDisplayName = "Portal audit actor",
                Version = 1,
            }));
            await db.SaveChangesAsync();
        }

        using var response = await factory.CreateClient().GetAsync("/api/portal/tree");
        Assert.Equal(expected, response.StatusCode);
        if (nodeCount == 2000)
        {
            using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            Assert.Equal(2000, json.RootElement.GetProperty("total").GetInt32());
        }
    }

    [Fact]
    public async Task Canonical_breadcrumb_uses_first_published_path_by_ancestor_order_then_id()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory, rootSortOrder: 5);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            var root = PublishedNode("Earlier root", PortalPageNodeKind.Folder, 1, userId, now);
            db.PortalPageNodes.Add(root);
            await db.SaveChangesAsync();
            var leaf = PublishedNode("Alternate placement", PortalPageNodeKind.Page, 0, userId, now);
            leaf.ParentId = root.Id;
            leaf.PortalPageId = fixture.PageId;
            db.PortalPageNodes.Add(leaf);
            await db.SaveChangesAsync();
        }

        using var response = await factory.CreateClient().GetAsync($"/api/portal/pages/{fixture.PageId}");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Earlier root", json.RootElement.GetProperty("breadcrumb")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task Anonymous_home_returns_only_top_level_categories_and_safe_recent_pages()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);

        using var response = await factory.CreateClient().GetAsync("/api/portal/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        using var json = JsonDocument.Parse(body);

        Assert.Equal("系统知识中心", json.RootElement.GetProperty("portalName").GetString());
        var categories = json.RootElement.GetProperty("categories");
        Assert.Equal(1, categories.GetArrayLength());
        Assert.Equal("Portal root", categories[0].GetProperty("title").GetString());
        Assert.Equal("Folder", categories[0].GetProperty("nodeKind").GetString());
        Assert.Equal(1, json.RootElement.GetProperty("recentPages").GetArrayLength());
        Assert.Equal(fixture.PageId, json.RootElement.GetProperty("recentPages")[0].GetProperty("id").GetInt64());
        Assert.Equal("Portal root", json.RootElement.GetProperty("recentPages")[0]
            .GetProperty("breadcrumb")[0].GetProperty("title").GetString());
        Assert.DoesNotContain("Portal audit actor", body);
        Assert.DoesNotContain("portal-secret", body);
        Assert.DoesNotContain("concurrency", body, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Anonymous_home_limits_recent_pages_to_eight_and_orders_by_publish_time_then_id()
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var baseline = DateTimeOffset.UtcNow.AddDays(1);
            for (var index = 0; index < 9; index++)
            {
                var publishedAt = baseline.AddMinutes(index);
                var page = new PortalPage
                {
                    Title = $"Recent {index}",
                    PrimaryTargetType = PortalTargetType.System,
                    PrimaryTargetId = fixture.SystemId,
                    IsPublished = true,
                    PublishedAt = publishedAt,
                    PublishedByUserId = userId,
                    PublishedByDisplayName = "Portal audit actor",
                    CreatedAt = publishedAt,
                    CreatedByUserId = userId,
                    CreatedByDisplayName = "Portal audit actor",
                    UpdatedAt = publishedAt,
                    UpdatedByUserId = userId,
                    UpdatedByDisplayName = "Portal audit actor",
                    Version = 1,
                    Sections =
                    [
                        new PortalPageSection
                        {
                            Heading = "Summary",
                            SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
                            ProjectionKind = PortalPageProjectionKind.Summary,
                            SortOrder = 0,
                        },
                    ],
                };
                db.PortalPages.Add(page);
                await db.SaveChangesAsync();
                var node = PublishedNode($"Recent {index}", PortalPageNodeKind.Page, index + 1, userId, publishedAt);
                node.ParentId = fixture.RootNodeId;
                node.PortalPageId = page.Id;
                db.PortalPageNodes.Add(node);
                await db.SaveChangesAsync();
            }
        }

        using var response = await factory.CreateClient().GetAsync("/api/portal/home");
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var recent = json.RootElement.GetProperty("recentPages");
        Assert.Equal(8, recent.GetArrayLength());
        Assert.Equal(
            Enumerable.Range(1, 8).Select(offset => $"Recent {9 - offset}").ToArray(),
            recent.EnumerateArray().Select(item => item.GetProperty("title").GetString()).ToArray());
    }

    [Theory]
    [InlineData("page-unpublished")]
    [InlineData("node-unpublished")]
    [InlineData("ancestor-unpublished")]
    [InlineData("primary-target-deleted")]
    [InlineData("document-draft")]
    [InlineData("document-archived")]
    [InlineData("explicit-target-deleted")]
    public async Task Anonymous_home_excludes_pages_that_are_not_currently_readable(string caseName)
    {
        using var factory = new BootstrapWebApplicationFactory();
        var fixture = await SeedCompositePage(factory);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var userId = await db.Users.Select(item => item.Id).FirstAsync();
            var now = DateTimeOffset.UtcNow;
            switch (caseName)
            {
                case "page-unpublished":
                    (await db.PortalPages.SingleAsync(item => item.Id == fixture.PageId)).IsPublished = false;
                    break;
                case "node-unpublished":
                    (await db.PortalPageNodes.SingleAsync(item => item.Id == fixture.PageNodeId)).IsPublished = false;
                    break;
                case "ancestor-unpublished":
                    (await db.PortalPageNodes.SingleAsync(item => item.Id == fixture.RootNodeId)).IsPublished = false;
                    break;
                case "primary-target-deleted":
                    var system = await db.Systems.SingleAsync(item => item.Id == fixture.SystemId);
                    system.IsDeleted = true;
                    system.DeletedAt = now;
                    system.DeletedByUserId = userId;
                    system.DeletedByDisplayName = "Portal audit actor";
                    break;
                case "document-draft":
                    (await db.KnowledgeDocuments.SingleAsync(item => item.Id == fixture.DocumentId)).LifecycleStatus = DocumentLifecycleStatus.Draft;
                    break;
                case "document-archived":
                    (await db.KnowledgeDocuments.SingleAsync(item => item.Id == fixture.DocumentId)).LifecycleStatus = DocumentLifecycleStatus.Archived;
                    break;
                case "explicit-target-deleted":
                    var document = await db.KnowledgeDocuments.SingleAsync(item => item.Id == fixture.DocumentId);
                    document.IsDeleted = true;
                    document.DeletedAt = now;
                    document.DeletedByUserId = userId;
                    document.DeletedByDisplayName = "Portal audit actor";
                    break;
                default:
                    throw new InvalidOperationException();
            }
            await db.SaveChangesAsync();
        }

        using var response = await factory.CreateClient().GetAsync("/api/portal/home");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Empty(json.RootElement.GetProperty("recentPages").EnumerateArray());
    }

    private static async Task<PortalFixture> SeedCompositePage(
        BootstrapWebApplicationFactory factory,
        int rootSortOrder = 0)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var userId = await db.Users.Select(item => item.Id).FirstAsync();
        var now = DateTimeOffset.UtcNow;
        const string actor = "Portal audit actor";
        var system = new KnowledgeSystem
        {
            Name = $"portal-system-{Guid.NewGuid():N}",
            DisplayName = "Portal System",
            SystemType = "Application",
            Lifecycle = SystemLifecycle.Running,
            Purpose = "System purpose",
            RepositoryUrl = "portal-secret-repository",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByName = actor,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusReason = "portal-secret-status-reason",
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = actor,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        };
        db.Systems.Add(system);
        await db.SaveChangesAsync();
        var function = new BusinessFunction
        {
            SystemId = system.Id,
            Name = $"portal-function-{Guid.NewGuid():N}",
            DisplayName = "Portal Function",
            FunctionType = "Workflow",
            Purpose = "Function purpose",
            CallerSummary = "Caller",
            InputDescription = "Input",
            OutputDescription = "Output",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByName = actor,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = actor,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        };
        var source = new DatabaseSource
        {
            SystemId = system.Id,
            Name = $"portal-source-{Guid.NewGuid():N}",
            Engine = "Oracle",
            IsPrimary = false,
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByName = actor,
            UpdatedAt = now,
            Version = 1,
        };
        var document = new KnowledgeDocument
        {
            DocumentType = DocumentType.KnowledgeArticle,
            Title = "Portal Document",
            Summary = "Document summary",
            BodyMarkdown = "# Published body",
            LifecycleStatus = DocumentLifecycleStatus.Published,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = actor,
            KnowledgeStatusChangedByRole = "Administrator",
            CreatedByUserId = userId,
            CreatedByDisplayName = actor,
            UpdatedByUserId = userId,
            UpdatedByDisplayName = actor,
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = now,
            CurrentRevisionNumber = 1,
            LatestPublishedRevisionNumber = 1,
            Version = 1,
        };
        db.AddRange(function, source, document);
        await db.SaveChangesAsync();
        var revision = new KnowledgeDocumentRevision
        {
            KnowledgeDocumentId = document.Id,
            RevisionNumber = 1,
            Title = document.Title,
            Summary = document.Summary,
            BodyMarkdown = document.BodyMarkdown,
            AuthorUserId = userId,
            AuthorDisplayNameSnapshot = actor,
            CreatedAt = now,
            LifecycleContext = DocumentLifecycleStatus.Published,
            RevisionOrigin = RevisionOrigin.Created,
        };
        db.KnowledgeDocumentRevisions.Add(revision);
        await db.SaveChangesAsync();
        var imageBytes = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10, 0, 0, 0, 0 };
        var pdfBytes = "%PDF-1.4\n%%EOF"u8.ToArray();
        Attachment StoredAttachment(string name, string extension, AttachmentKind kind, string contentType, byte[] bytes, string objectName)
        {
            var storageKey = $"objects/{objectName[..2]}/{objectName}.bin";
            var directory = Path.Combine(factory.AttachmentStorageRoot, "objects", objectName[..2]);
            Directory.CreateDirectory(directory);
            File.WriteAllBytes(Path.Combine(directory, $"{objectName}.bin"), bytes);
            return new Attachment
            {
                KnowledgeDocumentId = document.Id,
                OriginalFileName = name,
                Extension = extension,
                Kind = kind,
                ContentType = contentType,
                SizeBytes = bytes.Length,
                StorageKey = storageKey,
                Sha256 = SHA256.HashData(bytes),
                StorageState = AttachmentStorageState.Ready,
                CreatedByUserId = userId,
                CreatedByDisplayNameSnapshot = actor,
                CreatedAt = now,
            };
        }
        var imageAttachment = StoredAttachment("diagram.png", ".png", AttachmentKind.Image, "image/png", imageBytes, Guid.NewGuid().ToString("N"));
        var pdfAttachment = StoredAttachment("runbook.pdf", ".pdf", AttachmentKind.File, "application/pdf", pdfBytes, Guid.NewGuid().ToString("N"));
        db.Attachments.AddRange(imageAttachment, pdfAttachment);
        await db.SaveChangesAsync();
        db.AttachmentReferences.AddRange(
            new AttachmentReference { KnowledgeDocumentId = document.Id, KnowledgeDocumentRevisionId = revision.Id, AttachmentId = imageAttachment.Id },
            new AttachmentReference { KnowledgeDocumentId = document.Id, KnowledgeDocumentRevisionId = revision.Id, AttachmentId = pdfAttachment.Id });
        await db.SaveChangesAsync();
        var databaseObject = new DatabaseObject
        {
            DatabaseSourceId = source.Id,
            SchemaName = "PORTAL",
            ObjectName = "ORDERS",
            ObjectType = DatabaseObjectType.Table,
            DatabaseComment = "Database comment",
            TechnicalIdentityAlgorithmVersion = 1,
            TechnicalIdentity = "portal-secret-technical-identity",
            BusinessDescription = "Orders structure",
            EstimatedRows = 48000,
            AccessMode = DatabaseAccessMode.Read,
            BusinessKeyColumnsJson = "[\"ORDER_ID\"]",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByName = actor,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = actor,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        };
        var integration = new Integration
        {
            Name = $"Portal Integration {Guid.NewGuid():N}",
            IntegrationType = IntegrationType.HttpApi,
            SourceSystemId = system.Id,
            SourcePartyName = "Portal System",
            TargetPartyName = "External Party",
            FlowDirection = IntegrationFlowDirection.OneWay,
            Purpose = "Integration purpose",
            EndpointJson = "{\"secret\":\"portal-secret-endpoint\"}",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByName = actor,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = actor,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        };
        db.AddRange(databaseObject, integration);
        await db.SaveChangesAsync();
        db.KnowledgeRelations.Add(new KnowledgeRelation
        {
            SourceType = KnowledgeTargetType.System,
            SourceId = system.Id,
            TargetType = KnowledgeTargetType.BusinessFunction,
            TargetId = function.Id,
            RelationType = RelationType.Documents,
            CreatedAt = now,
            CreatedByName = actor,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = actor,
            KnowledgeStatusChangedByRole = "Administrator",
        });
        db.Evidence.Add(new Evidence
        {
            EvidenceType = EvidenceType.HumanConfirmation,
            SubjectType = EvidenceSubjectType.System,
            SubjectId = system.Id,
            SourceTitle = "Portal confirmation",
            SourceReference = "portal://test/confirmation",
            SupportReason = "Verified for Portal projection",
            ProviderName = "Portal evidence provider",
            ProviderRole = "Owner",
            ProvidedAt = now,
            CreatedAt = now,
            UpdatedAt = now,
        });
        await db.SaveChangesAsync();
        db.DatabaseColumns.Add(new DatabaseColumn
        {
            DatabaseObjectId = databaseObject.Id,
            OrdinalPosition = 1,
            ColumnName = "ORDER_ID",
            DataType = "NUMBER(19)",
            IsNullable = false,
            DefaultValue = null,
            BusinessDescription = "Order identifier",
            DatabaseComment = "Primary key",
            TechnicalIdentityAlgorithmVersion = 1,
            TechnicalIdentity = "portal-secret-column-identity",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByDisplayName = actor,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = actor,
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        });
        await db.SaveChangesAsync();

        var page = new PortalPage
        {
            Title = "Portal composite",
            PrimaryTargetType = PortalTargetType.System,
            PrimaryTargetId = system.Id,
            IsPublished = true,
            PublishedAt = now,
            PublishedByUserId = userId,
            PublishedByDisplayName = actor,
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByDisplayName = actor,
            UpdatedAt = now,
            UpdatedByUserId = userId,
            UpdatedByDisplayName = actor,
            Version = 1,
        };
        page.Sections = CreateSections(page, system.Id, function.Id, databaseObject.Id, document.Id, integration.Id);
        db.PortalPages.Add(page);
        await db.SaveChangesAsync();
        var root = PublishedNode("Portal root", PortalPageNodeKind.Folder, rootSortOrder, userId, now);
        db.PortalPageNodes.Add(root);
        await db.SaveChangesAsync();
        var leaf = PublishedNode("Portal page", PortalPageNodeKind.Page, 0, userId, now);
        leaf.ParentId = root.Id;
        leaf.PortalPageId = page.Id;
        db.PortalPageNodes.Add(leaf);
        await db.SaveChangesAsync();
        return new(page.Id, root.Id, leaf.Id, system.Id, function.Id, databaseObject.Id, document.Id, integration.Id, imageAttachment.Id, pdfAttachment.Id);
    }

    private static List<PortalPageSection> CreateSections(
        PortalPage page,
        long systemId,
        long functionId,
        long databaseObjectId,
        long documentId,
        long integrationId)
    {
        var sections = new List<PortalPageSection>();
        void Add(string heading, PortalPageProjectionKind projection, PortalTargetType? type = null, long? id = null)
        {
            sections.Add(new PortalPageSection
            {
                PortalPage = page,
                Heading = heading,
                SourceKind = type is null
                    ? PortalPageSectionSourceKind.PrimaryTarget
                    : PortalPageSectionSourceKind.ExplicitReference,
                ReferenceTargetType = type,
                ReferenceTargetId = id,
                ProjectionKind = projection,
                SortOrder = sections.Count,
            });
        }
        Add("System summary", PortalPageProjectionKind.Summary);
        Add("Function summary", PortalPageProjectionKind.Summary, PortalTargetType.BusinessFunction, functionId);
        Add("Database summary", PortalPageProjectionKind.Summary, PortalTargetType.DatabaseObject, databaseObjectId);
        Add("Document summary", PortalPageProjectionKind.Summary, PortalTargetType.KnowledgeDocument, documentId);
        Add("Integration summary", PortalPageProjectionKind.Summary, PortalTargetType.Integration, integrationId);
        Add("Document body", PortalPageProjectionKind.KnowledgeDocumentBody, PortalTargetType.KnowledgeDocument, documentId);
        Add("System overview", PortalPageProjectionKind.StructuredOverview, PortalTargetType.System, systemId);
        Add("Function overview", PortalPageProjectionKind.StructuredOverview, PortalTargetType.BusinessFunction, functionId);
        Add("Database overview", PortalPageProjectionKind.StructuredOverview, PortalTargetType.DatabaseObject, databaseObjectId);
        Add("Integration overview", PortalPageProjectionKind.StructuredOverview, PortalTargetType.Integration, integrationId);
        Add("Database structure", PortalPageProjectionKind.DatabaseStructure, PortalTargetType.DatabaseObject, databaseObjectId);
        Add("Attachments", PortalPageProjectionKind.AttachmentList, PortalTargetType.KnowledgeDocument, documentId);
        Add("System trust", PortalPageProjectionKind.TrustSummary);
        Add("Function trust", PortalPageProjectionKind.TrustSummary, PortalTargetType.BusinessFunction, functionId);
        Add("Database trust", PortalPageProjectionKind.TrustSummary, PortalTargetType.DatabaseObject, databaseObjectId);
        Add("Document trust", PortalPageProjectionKind.TrustSummary, PortalTargetType.KnowledgeDocument, documentId);
        Add("Integration trust", PortalPageProjectionKind.TrustSummary, PortalTargetType.Integration, integrationId);
        sections.Add(new PortalPageSection
        {
            PortalPage = page,
            Heading = "Related knowledge",
            SourceKind = PortalPageSectionSourceKind.Derived,
            ProjectionKind = PortalPageProjectionKind.RelatedKnowledge,
            SortOrder = sections.Count,
        });
        return sections;
    }

    private static PortalPageNode PublishedNode(
        string title,
        PortalPageNodeKind kind,
        int sortOrder,
        long userId,
        DateTimeOffset now) => new()
        {
            Title = title,
            NodeKind = kind,
            SortOrder = sortOrder,
            IsPublished = true,
            PublishedAt = now,
            PublishedByUserId = userId,
            PublishedByDisplayName = "Portal audit actor",
            CreatedAt = now,
            CreatedByUserId = userId,
            CreatedByDisplayName = "Portal audit actor",
            UpdatedAt = now,
            UpdatedByUserId = userId,
            UpdatedByDisplayName = "Portal audit actor",
            Version = 1,
        };

    private static KnowledgeDocument TraceDocument(
        DocumentType type,
        string title,
        DocumentLifecycleStatus lifecycle,
        long userId,
        DateTimeOffset now) => new()
        {
            DocumentType = type,
            Title = title,
            BodyMarkdown = $"# {title}",
            LifecycleStatus = lifecycle,
            KnowledgeStatus = KnowledgeStatus.Confirmed,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = "Trace actor",
            KnowledgeStatusChangedByRole = "Administrator",
            CreatedByUserId = userId,
            CreatedByDisplayName = "Trace actor",
            UpdatedByUserId = userId,
            UpdatedByDisplayName = "Trace actor",
            CreatedAt = now,
            UpdatedAt = now,
            PublishedAt = lifecycle == DocumentLifecycleStatus.Published ? now : null,
            CurrentRevisionNumber = 1,
            LatestPublishedRevisionNumber = lifecycle == DocumentLifecycleStatus.Published ? 1 : null,
        };

    private sealed record PortalFixture(
        long PageId,
        long RootNodeId,
        long PageNodeId,
        long SystemId,
        long FunctionId,
        long DatabaseObjectId,
        long DocumentId,
        long IntegrationId,
        long ImageAttachmentId,
        long PdfAttachmentId);
}
