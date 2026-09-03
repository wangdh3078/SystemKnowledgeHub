using System.Net;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
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
        Assert.Contains("# Published body", json);
        Assert.Contains("estimatedRows\":48000", json);
        Assert.DoesNotContain("portal-secret-repository", json);
        Assert.DoesNotContain("portal-secret-endpoint", json);
        Assert.DoesNotContain("portal-secret-technical-identity", json);
        Assert.DoesNotContain("portal-secret-status-reason", json);
        Assert.DoesNotContain("Portal audit actor", json);

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
    public async Task Unsupported_persisted_projection_is_excluded_and_direct_read_fails_closed()
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
        Assert.Equal(HttpStatusCode.NotFound, pageResponse.StatusCode);
        using var treeResponse = await client.GetAsync("/api/portal/tree");
        using var tree = JsonDocument.Parse(await treeResponse.Content.ReadAsStringAsync());
        Assert.Equal(1, tree.RootElement.GetProperty("total").GetInt32());
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
        return new(page.Id, root.Id, leaf.Id, system.Id, function.Id, databaseObject.Id, document.Id, integration.Id);
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

    private sealed record PortalFixture(
        long PageId,
        long RootNodeId,
        long PageNodeId,
        long SystemId,
        long FunctionId,
        long DatabaseObjectId,
        long DocumentId,
        long IntegrationId);
}
