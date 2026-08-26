using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.BusinessRules.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;
using Xunit.Abstractions;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class ImpactApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private int _sequence;

    public ImpactApiTests(BootstrapWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
        _output = output;
    }

    [Fact]
    public async Task Requirement_Specification_and_TestCase_project_every_allowed_path_with_closed_meaning()
    {
        var targets = await AddStructuredTargets("Allowed paths");
        var requirement = await AddDocument(DocumentType.Requirement, "Impact Requirement");
        var specification = await AddDocument(DocumentType.Specification, "Impact Specification");
        var testCase = await AddDocument(DocumentType.TestCase, "Impact TestCase");

        await AddRelation(requirement.Id, RelationType.AppliesTo, KnowledgeTargetType.System, targets.SystemId);
        await AddRelation(requirement.Id, RelationType.AppliesTo, KnowledgeTargetType.BusinessFunction, targets.BusinessFunctionId);
        foreach (var target in targets.All)
        {
            await AddRelation(requirement.Id, RelationType.Documents, target.Type, target.Id);
            await AddRelation(specification.Id, RelationType.Documents, target.Type, target.Id);
            await AddRelation(testCase.Id, RelationType.Documents, target.Type, target.Id);
        }
        await AddRelation(requirement.Id, RelationType.SpecifiedBy, KnowledgeTargetType.KnowledgeDocument, specification.Id);
        await AddRelation(requirement.Id, RelationType.VerifiedBy, KnowledgeTargetType.KnowledgeDocument, testCase.Id);
        await AddRelation(specification.Id, RelationType.VerifiedBy, KnowledgeTargetType.KnowledgeDocument, testCase.Id);

        var requirementImpact = await GetImpact(requirement.Id, pageSize: 100);
        Assert.Equal(12, requirementImpact.GetProperty("total").GetInt32());
        Assert.Equal(2, requirementImpact.GetProperty("maxDepth").GetInt32());
        AssertMeaningCount(requirementImpact, "ExplicitRequirementScope", 2);
        AssertMeaningCount(requirementImpact, "DocumentedByRequirement", 5);
        AssertMeaningCount(requirementImpact, "DocumentedBySpecification", 5);
        Assert.All(requirementImpact.GetProperty("items").EnumerateArray(), AssertPathIsClosed);

        var specificationImpact = await GetImpact(specification.Id, pageSize: 100);
        Assert.Equal(12, specificationImpact.GetProperty("total").GetInt32());
        AssertMeaningCount(specificationImpact, "DocumentedBySpecification", 5);
        AssertMeaningCount(specificationImpact, "UpstreamRequirementScope", 2);
        AssertMeaningCount(specificationImpact, "UpstreamRequirementDocumentedContext", 5);
        Assert.Contains(specificationImpact.GetProperty("items").EnumerateArray(), item =>
            item.GetProperty("pathKind").GetString() == "ViaRequirementAppliesTo"
            && item.GetProperty("path")[0].GetProperty("direction").GetString() == "Incoming");

        var testImpact = await GetImpact(testCase.Id, pageSize: 100);
        Assert.Equal(12, testImpact.GetProperty("total").GetInt32());
        AssertMeaningCount(testImpact, "DocumentedByTestCase", 5);
        AssertMeaningCount(testImpact, "VerifiedRequirementScope", 2);
        AssertMeaningCount(testImpact, "VerifiedSpecificationDocumentedContext", 5);
        Assert.All(testImpact.GetProperty("items").EnumerateArray(), item =>
        {
            AssertPathIsClosed(item);
            Assert.InRange(item.GetProperty("path").GetArrayLength(), 1, 2);
        });

        var databaseObject = requirementImpact.GetProperty("items").EnumerateArray().First(item =>
            item.GetProperty("target").GetProperty("type").GetString() == "DatabaseObject");
        Assert.Equal("dbo.impact_table", databaseObject.GetProperty("target").GetProperty("title").GetString());
        Assert.Equal(targets.SystemId, databaseObject.GetProperty("target")
            .GetProperty("systemContext")[0].GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Forbidden_relations_and_depth_three_path_are_excluded()
    {
        var targets = await AddStructuredTargets("Forbidden paths");
        var requirement = await AddDocument(DocumentType.Requirement, "Depth three Requirement");
        var specification = await AddDocument(DocumentType.Specification, "Depth three Specification");
        var testCase = await AddDocument(DocumentType.TestCase, "Depth three TestCase");
        var older = await AddDocument(DocumentType.Requirement, "Older Requirement");

        await AddRelation(requirement.Id, RelationType.AppliesTo, KnowledgeTargetType.System, targets.SystemId);
        await AddRelation(requirement.Id, RelationType.SpecifiedBy, KnowledgeTargetType.KnowledgeDocument, specification.Id);
        await AddRelation(specification.Id, RelationType.VerifiedBy, KnowledgeTargetType.KnowledgeDocument, testCase.Id);
        await AddRelation(requirement.Id, RelationType.References, KnowledgeTargetType.System, targets.ReferenceOnlySystemId);
        await AddRelation(requirement.Id, RelationType.Supersedes, KnowledgeTargetType.KnowledgeDocument, older.Id);
        await AddRelation(requirement.Id, RelationType.DependsOn, KnowledgeTargetType.System, targets.ForbiddenStructuredSystemId);
        await AddRelation(requirement.Id, RelationType.Calls, KnowledgeTargetType.BusinessFunction, targets.BusinessFunctionId);
        await AddRelation(requirement.Id, RelationType.Reads, KnowledgeTargetType.DatabaseObject, targets.DatabaseObjectId);

        var requirementImpact = await GetImpact(requirement.Id, pageSize: 100);
        var requirementIds = TargetIds(requirementImpact);
        Assert.Contains(targets.SystemId, requirementIds);
        Assert.DoesNotContain(targets.ReferenceOnlySystemId, requirementIds);
        Assert.DoesNotContain(targets.ForbiddenStructuredSystemId, requirementIds);
        Assert.DoesNotContain(older.Id, requirementIds);

        var testImpact = await GetImpact(testCase.Id, pageSize: 100);
        Assert.Equal(0, testImpact.GetProperty("total").GetInt32());
        Assert.Empty(testImpact.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Pagination_ordering_and_same_target_distinct_meanings_are_deterministic()
    {
        var requirement = await AddDocument(DocumentType.Requirement, "Paged Requirement");
        var shared = await AddSystem("A shared target");
        await AddRelation(requirement.Id, RelationType.Documents, KnowledgeTargetType.System, shared.Id);
        await AddRelation(requirement.Id, RelationType.AppliesTo, KnowledgeTargetType.System, shared.Id);
        for (var index = 0; index < 23; index++)
        {
            var system = await AddSystem($"Paged target {23 - index:00}");
            await AddRelation(requirement.Id, RelationType.Documents, KnowledgeTargetType.System, system.Id);
        }

        var defaults = await GetImpact(requirement.Id);
        Assert.Equal(1, defaults.GetProperty("page").GetInt64());
        Assert.Equal(20, defaults.GetProperty("pageSize").GetInt32());
        Assert.Equal(25, defaults.GetProperty("total").GetInt32());
        Assert.Equal(20, defaults.GetProperty("items").GetArrayLength());
        Assert.Equal("DirectAppliesTo", defaults.GetProperty("items")[0].GetProperty("pathKind").GetString());

        var second = await GetImpact(requirement.Id, page: 2, pageSize: 20);
        Assert.Equal(5, second.GetProperty("items").GetArrayLength());
        var repeated = await GetImpact(requirement.Id, pageSize: 100);
        Assert.Equal(
            ItemKeys(repeated),
            ItemKeys(await GetImpact(requirement.Id, pageSize: 100)));
        Assert.Equal(2, repeated.GetProperty("items").EnumerateArray().Count(item =>
            item.GetProperty("target").GetProperty("id").GetInt64() == shared.Id));
        Assert.Equal(new[] { "ExplicitRequirementScope", "DocumentedByRequirement" },
            repeated.GetProperty("items").EnumerateArray()
                .Where(item => item.GetProperty("target").GetProperty("id").GetInt64() == shared.Id)
                .Select(item => item.GetProperty("meaning").GetString()));

        var emptyPage = await GetImpact(requirement.Id, page: 9, pageSize: 20);
        Assert.Empty(emptyPage.GetProperty("items").EnumerateArray());
    }

    [Fact]
    public async Task Validation_root_contract_and_authorization_match_the_authenticated_read_boundary()
    {
        var requirement = await AddDocument(DocumentType.Requirement, "Authorized Impact");
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/knowledge-documents/{requirement.Id}/traceability/impact")).StatusCode);
        foreach (var accessLevel in new[] { AccessLevel.Viewer, AccessLevel.Editor, AccessLevel.Administrator })
        {
            var userId = await AddUser(accessLevel);
            using var client = await _factory.CreateAuthenticatedClientAsync(userId);
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync($"/api/knowledge-documents/{requirement.Id}/traceability/impact")).StatusCode);
        }

        foreach (var query in new[]
        {
            "?page=0", "?page=-1", "?page=unsafe", "?page=9007199254740992",
            "?pageSize=0", "?pageSize=-1", "?pageSize=101", "?pageSize=unsafe",
            "?depth=2", "?relationType=Documents", "?targetType=System", "?path=anything",
            "?pathKind=DirectDocuments", "?graphQuery=all", "?includeReferences=true",
        })
        {
            using var invalid = await _client.GetAsync(
                $"/api/knowledge-documents/{requirement.Id}/traceability/impact{query}");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("validation_error", (await invalid.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code").GetString());
        }
        Assert.Equal(100, (await GetImpact(requirement.Id, pageSize: 100))
            .GetProperty("pageSize").GetInt32());

        foreach (var invalidId in new[] { "0", "-1", "unsafe", "9007199254740992" })
        {
            using var response = await _client.GetAsync(
                $"/api/knowledge-documents/{invalidId}/traceability/impact");
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var missing = await _client.GetAsync(
            "/api/knowledge-documents/9007199254740991/traceability/impact");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var sop = await AddDocument(DocumentType.Sop, "Unsupported Impact root");
        using var unsupported = await _client.GetAsync(
            $"/api/knowledge-documents/{sop.Id}/traceability/impact");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
        Assert.Equal("business_rule_violation", (await unsupported.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString());
    }

    [Fact]
    public async Task Invalid_selected_endpoint_fails_closed_without_metadata()
    {
        var requirement = await AddDocument(DocumentType.Requirement, "Invalid Impact reference");
        const long missingTargetId = 8_765_432;
        await AddRelation(requirement.Id, RelationType.Documents, KnowledgeTargetType.System, missingTargetId);

        using var response = await _client.GetAsync(
            $"/api/knowledge-documents/{requirement.Id}/traceability/impact");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Contains("reference_invalid", body);
        Assert.DoesNotContain(missingTargetId.ToString(), body);
        Assert.DoesNotContain("Invalid Impact reference", body);
    }

    [Fact]
    public async Task Impact_GET_is_read_only_for_canonical_tables_and_current_revision()
    {
        var targets = await AddStructuredTargets("Read only Impact");
        var requirement = await AddDocument(DocumentType.Requirement, "Read only Impact root");
        await AddRelation(requirement.Id, RelationType.AppliesTo, KnowledgeTargetType.System, targets.SystemId);
        var before = await Snapshot(requirement.Id);

        using var response = await _client.GetAsync(
            $"/api/knowledge-documents/{requirement.Id}/traceability/impact");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("bodyMarkdown", payload, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("description", payload, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(before, await Snapshot(requirement.Id));
    }

    [Fact]
    public async Task Archived_root_remains_readable_and_does_not_imply_no_impact()
    {
        var target = await AddSystem("Archived root target");
        var requirement = await AddDocument(
            DocumentType.Requirement,
            "Archived Impact Requirement",
            DocumentLifecycleStatus.Archived);
        await AddRelation(requirement.Id, RelationType.AppliesTo, KnowledgeTargetType.System, target.Id);

        var impact = await GetImpact(requirement.Id);
        Assert.Equal(1, impact.GetProperty("total").GetInt32());
        Assert.Equal(target.Id, impact.GetProperty("items")[0]
            .GetProperty("target").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Canonical_relationship_add_remove_and_readd_refreshes_all_relevant_path_families()
    {
        var targets = await AddStructuredTargets("Mutation Impact");
        var requirement = await AddDocument(DocumentType.Requirement, "Mutation Requirement");
        var specification = await AddDocument(DocumentType.Specification, "Mutation Specification");
        var testCase = await AddDocument(DocumentType.TestCase, "Mutation TestCase");

        var appliesTo = await AddRelation(
            requirement.Id, RelationType.AppliesTo, KnowledgeTargetType.System, targets.SystemId);
        Assert.Contains(targets.SystemId, TargetIds(await GetImpact(requirement.Id)));
        await RemoveRelation(appliesTo);
        Assert.DoesNotContain(targets.SystemId, TargetIds(await GetImpact(requirement.Id)));
        await AddRelation(requirement.Id, RelationType.AppliesTo, KnowledgeTargetType.System, targets.SystemId);
        Assert.Contains(targets.SystemId, TargetIds(await GetImpact(requirement.Id)));

        var documents = await AddRelation(
            requirement.Id, RelationType.Documents, KnowledgeTargetType.BusinessFunction, targets.BusinessFunctionId);
        Assert.Contains(targets.BusinessFunctionId, TargetIds(await GetImpact(requirement.Id)));
        await RemoveRelation(documents);
        Assert.DoesNotContain(targets.BusinessFunctionId, TargetIds(await GetImpact(requirement.Id)));

        await AddRelation(
            specification.Id, RelationType.Documents, KnowledgeTargetType.DatabaseObject, targets.DatabaseObjectId);
        var specifiedBy = await AddRelation(
            requirement.Id, RelationType.SpecifiedBy, KnowledgeTargetType.KnowledgeDocument, specification.Id);
        Assert.Contains(targets.DatabaseObjectId, TargetIds(await GetImpact(requirement.Id)));
        await RemoveRelation(specifiedBy);
        Assert.DoesNotContain(targets.DatabaseObjectId, TargetIds(await GetImpact(requirement.Id)));
        await AddRelation(
            requirement.Id, RelationType.SpecifiedBy, KnowledgeTargetType.KnowledgeDocument, specification.Id);
        Assert.Contains(targets.DatabaseObjectId, TargetIds(await GetImpact(requirement.Id)));

        var verifiedBy = await AddRelation(
            specification.Id, RelationType.VerifiedBy, KnowledgeTargetType.KnowledgeDocument, testCase.Id);
        Assert.Contains(targets.DatabaseObjectId, TargetIds(await GetImpact(testCase.Id)));
        await RemoveRelation(verifiedBy);
        Assert.DoesNotContain(targets.DatabaseObjectId, TargetIds(await GetImpact(testCase.Id)));
        await AddRelation(
            specification.Id, RelationType.VerifiedBy, KnowledgeTargetType.KnowledgeDocument, testCase.Id);
        Assert.Contains(targets.DatabaseObjectId, TargetIds(await GetImpact(testCase.Id)));
    }

    [Fact]
    public async Task Representative_SQLite_query_plans_use_existing_relation_indexes()
    {
        var root = await AddDocument(DocumentType.Requirement, "Impact plan root");
        var plans = new Dictionary<string, string>
        {
            ["directAppliesTo"] = await Explain("SELECT id FROM knowledge_relations WHERE source_type = 'KnowledgeDocument' AND source_id = $id AND relation_type = 'AppliesTo'", root.Id),
            ["directDocuments"] = await Explain("SELECT id FROM knowledge_relations WHERE source_type = 'KnowledgeDocument' AND source_id = $id AND relation_type = 'Documents'", root.Id),
            ["requirementSpecificationDocuments"] = await Explain("SELECT child.id FROM knowledge_relations parent JOIN knowledge_relations child ON child.source_type = 'KnowledgeDocument' AND child.source_id = parent.target_id AND child.relation_type = 'Documents' WHERE parent.source_type = 'KnowledgeDocument' AND parent.source_id = $id AND parent.relation_type = 'SpecifiedBy'", root.Id),
            ["incomingRequirementAppliesTo"] = await Explain("SELECT child.id FROM knowledge_relations parent JOIN knowledge_relations child ON child.source_type = 'KnowledgeDocument' AND child.source_id = parent.source_id AND child.relation_type = 'AppliesTo' WHERE parent.target_type = 'KnowledgeDocument' AND parent.target_id = $id AND parent.relation_type = 'SpecifiedBy'", root.Id),
            ["incomingRequirementDocuments"] = await Explain("SELECT child.id FROM knowledge_relations parent JOIN knowledge_relations child ON child.source_type = 'KnowledgeDocument' AND child.source_id = parent.source_id AND child.relation_type = 'Documents' WHERE parent.target_type = 'KnowledgeDocument' AND parent.target_id = $id AND parent.relation_type = 'SpecifiedBy'", root.Id),
            ["verifiedRequirementAppliesTo"] = await Explain("SELECT child.id FROM knowledge_relations parent JOIN knowledge_relations child ON child.source_type = 'KnowledgeDocument' AND child.source_id = parent.source_id AND child.relation_type = 'AppliesTo' WHERE parent.target_type = 'KnowledgeDocument' AND parent.target_id = $id AND parent.relation_type = 'VerifiedBy'", root.Id),
            ["verifiedSpecificationDocuments"] = await Explain("SELECT child.id FROM knowledge_relations parent JOIN knowledge_relations child ON child.source_type = 'KnowledgeDocument' AND child.source_id = parent.source_id AND child.relation_type = 'Documents' WHERE parent.target_type = 'KnowledgeDocument' AND parent.target_id = $id AND parent.relation_type = 'VerifiedBy'", root.Id),
        };
        Assert.All(plans, plan =>
        {
            _output.WriteLine("{0}: {1}", plan.Key, plan.Value);
            Assert.Contains("INDEX", plan.Value, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("SCAN knowledge_relations", plan.Value, StringComparison.OrdinalIgnoreCase);
        });
    }

    private async Task<JsonElement> GetImpact(long id, long page = 1, int? pageSize = null)
    {
        var query = pageSize.HasValue ? $"?page={page}&pageSize={pageSize.Value}" : string.Empty;
        using var response = await _client.GetAsync(
            $"/api/knowledge-documents/{id}/traceability/impact{query}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<DocumentFixture> AddDocument(
        DocumentType documentType,
        string title,
        DocumentLifecycleStatus lifecycleStatus = DocumentLifecycleStatus.Draft)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await dbContext.Users.OrderBy(item => item.Id).FirstAsync();
        var timestamp = Timestamp();
        var document = new KnowledgeDocument
        {
            DocumentType = documentType,
            Title = Unique(title),
            BodyMarkdown = "impact fixture",
            LifecycleStatus = lifecycleStatus,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = user.DisplayName,
            KnowledgeStatusChangedByRole = "Impact Test",
            CreatedByUserId = user.Id,
            CreatedByDisplayName = user.DisplayName,
            UpdatedByUserId = user.Id,
            UpdatedByDisplayName = user.DisplayName,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            PublishedAt = lifecycleStatus == DocumentLifecycleStatus.Published ? timestamp : null,
            ArchivedAt = lifecycleStatus == DocumentLifecycleStatus.Archived ? timestamp : null,
            CurrentRevisionNumber = 1,
            Version = 1,
        };
        dbContext.KnowledgeDocuments.Add(document);
        await dbContext.SaveChangesAsync();
        dbContext.KnowledgeDocumentRevisions.Add(new KnowledgeDocumentRevision
        {
            KnowledgeDocumentId = document.Id,
            RevisionNumber = 1,
            Title = document.Title,
            BodyMarkdown = document.BodyMarkdown,
            AuthorUserId = user.Id,
            AuthorDisplayNameSnapshot = user.DisplayName,
            CreatedAt = timestamp,
            LifecycleContext = lifecycleStatus,
            RevisionOrigin = RevisionOrigin.Created,
        });
        await dbContext.SaveChangesAsync();
        return new DocumentFixture(document.Id);
    }

    private async Task<long> AddRelation(
        long sourceDocumentId,
        RelationType relationType,
        KnowledgeTargetType targetType,
        long targetId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = Timestamp();
        var relation = new KnowledgeRelation
        {
            SourceType = KnowledgeTargetType.KnowledgeDocument,
            SourceId = sourceDocumentId,
            RelationType = relationType,
            TargetType = targetType,
            TargetId = targetId,
            CreatedAt = timestamp,
            CreatedByName = "Impact Test",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "Impact Test",
            KnowledgeStatusChangedByRole = "Impact Test",
            Version = 1,
        };
        dbContext.KnowledgeRelations.Add(relation);
        await dbContext.SaveChangesAsync();
        return relation.Id;
    }

    private async Task RemoveRelation(long relationshipId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        await dbContext.KnowledgeRelations
            .Where(relation => relation.Id == relationshipId)
            .ExecuteDeleteAsync();
    }

    private async Task<SystemFixture> AddSystem(string name)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = Timestamp();
        var system = new KnowledgeSystem
        {
            Name = Unique(name),
            DisplayName = name,
            SystemType = "Internal",
            Lifecycle = SystemLifecycle.Running,
            CreatedAt = timestamp,
            CreatedByName = "Impact Test",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "Impact Test",
            KnowledgeStatusChangedByRole = "Impact Test",
            Version = 1,
        };
        dbContext.Systems.Add(system);
        await dbContext.SaveChangesAsync();
        return new SystemFixture(system.Id);
    }

    private async Task<StructuredTargets> AddStructuredTargets(string prefix)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = Timestamp();
        var system = NewSystem(Unique(prefix + " MES"), prefix + " MES", timestamp);
        var referenceOnlySystem = NewSystem(Unique(prefix + " reference"), prefix + " reference", timestamp);
        var forbiddenSystem = NewSystem(Unique(prefix + " forbidden"), prefix + " forbidden", timestamp);
        dbContext.Systems.AddRange(system, referenceOnlySystem, forbiddenSystem);
        await dbContext.SaveChangesAsync();

        var function = new BusinessFunction
        {
            SystemId = system.Id,
            Name = Unique(prefix + " BF"),
            FunctionType = "Business",
            CreatedAt = timestamp,
            CreatedByName = "Impact Test",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "Impact Test",
            KnowledgeStatusChangedByRole = "Impact Test",
            Version = 1,
        };
        var source = new DatabaseSource
        {
            SystemId = system.Id,
            Name = Unique(prefix + " DB"),
            Engine = "SQLite",
            IsPrimary = true,
            CreatedAt = timestamp,
            CreatedByName = "Impact Test",
            UpdatedAt = timestamp,
        };
        var rule = new BusinessRule
        {
            SystemId = system.Id,
            Name = Unique(prefix + " BR"),
            Description = "Impact business rule",
            CreatedAt = timestamp,
            CreatedByName = "Impact Test",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "Impact Test",
            KnowledgeStatusChangedByRole = "Impact Test",
            Version = 1,
        };
        var integration = new Integration
        {
            Name = Unique(prefix + " INT"),
            IntegrationType = IntegrationType.HttpApi,
            SourceSystemId = system.Id,
            SourcePartyName = system.Name,
            TargetPartyName = "External",
            FlowDirection = IntegrationFlowDirection.OneWay,
            CreatedAt = timestamp,
            CreatedByName = "Impact Test",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "Impact Test",
            KnowledgeStatusChangedByRole = "Impact Test",
            Version = 1,
        };
        dbContext.AddRange(function, source, rule, integration);
        await dbContext.SaveChangesAsync();
        var databaseObject = new DatabaseObject
        {
            DatabaseSourceId = source.Id,
            SchemaName = "dbo",
            ObjectName = "impact_table",
            ObjectType = DatabaseObjectType.Table,
            AccessMode = DatabaseAccessMode.ReadWrite,
            CreatedAt = timestamp,
            CreatedByName = "Impact Test",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "Impact Test",
            KnowledgeStatusChangedByRole = "Impact Test",
            Version = 1,
        };
        dbContext.DatabaseObjects.Add(databaseObject);
        await dbContext.SaveChangesAsync();
        return new StructuredTargets(
            system.Id,
            function.Id,
            databaseObject.Id,
            rule.Id,
            integration.Id,
            referenceOnlySystem.Id,
            forbiddenSystem.Id);
    }

    private async Task<long> AddUser(AccessLevel accessLevel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = Timestamp();
        var user = new User
        {
            DisplayName = Unique($"Impact {accessLevel}"),
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

    private async Task<ReadOnlySnapshot> Snapshot(long documentId)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var document = await dbContext.KnowledgeDocuments.AsNoTracking()
            .SingleAsync(item => item.Id == documentId);
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM knowledge_documents_fts";
        var ftsRows = Convert.ToInt64(await command.ExecuteScalarAsync());
        return new ReadOnlySnapshot(
            await dbContext.KnowledgeDocuments.CountAsync(),
            await dbContext.KnowledgeDocumentRevisions.CountAsync(),
            await dbContext.KnowledgeRelations.CountAsync(),
            await dbContext.Evidence.CountAsync(),
            ftsRows,
            document.CurrentRevisionNumber,
            document.Version,
            document.UpdatedAt);
    }

    private async Task<string> Explain(string sql, long id)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = $"EXPLAIN QUERY PLAN {sql}";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "$id";
        parameter.Value = id;
        command.Parameters.Add(parameter);
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add(reader.GetString(3));
        return string.Join(" | ", rows);
    }

    private static KnowledgeSystem NewSystem(string name, string displayName, DateTimeOffset timestamp) => new()
    {
        Name = name,
        DisplayName = displayName,
        SystemType = "Internal",
        Lifecycle = SystemLifecycle.Running,
        CreatedAt = timestamp,
        CreatedByName = "Impact Test",
        UpdatedAt = timestamp,
        KnowledgeStatus = KnowledgeStatus.Unknown,
        KnowledgeStatusChangedAt = timestamp,
        KnowledgeStatusChangedByName = "Impact Test",
        KnowledgeStatusChangedByRole = "Impact Test",
        Version = 1,
    };

    private DateTimeOffset Timestamp() => DateTimeOffset.UtcNow.AddMilliseconds(++_sequence);
    private string Unique(string value) => $"{value} {_sequence} {Guid.NewGuid():N}";

    private static void AssertMeaningCount(JsonElement response, string meaning, int count) =>
        Assert.Equal(count, response.GetProperty("items").EnumerateArray().Count(item =>
            item.GetProperty("meaning").GetString() == meaning));

    private static void AssertPathIsClosed(JsonElement item)
    {
        Assert.Contains(item.GetProperty("pathKind").GetString(), new[]
        {
            "DirectAppliesTo", "DirectDocuments", "ViaSpecificationDocuments",
            "ViaRequirementAppliesTo", "ViaRequirementDocuments",
            "ViaVerifiedRequirementAppliesTo", "ViaVerifiedSpecificationDocuments",
        });
        Assert.All(item.GetProperty("path").EnumerateArray(), segment =>
        {
            Assert.True(segment.GetProperty("relationshipId").GetInt64() > 0);
            Assert.Contains(segment.GetProperty("relationType").GetString(),
                new[] { "AppliesTo", "Documents", "SpecifiedBy", "VerifiedBy" });
            Assert.Contains(segment.GetProperty("direction").GetString(),
                new[] { "Outgoing", "Incoming" });
        });
    }

    private static long[] TargetIds(JsonElement response) => response.GetProperty("items")
        .EnumerateArray()
        .Select(item => item.GetProperty("target").GetProperty("id").GetInt64())
        .ToArray();

    private static string[] ItemKeys(JsonElement response) => response.GetProperty("items")
        .EnumerateArray()
        .Select(item => $"{item.GetProperty("pathKind").GetString()}:{item.GetProperty("target").GetProperty("type").GetString()}:{item.GetProperty("target").GetProperty("id").GetInt64()}:{string.Join(',', item.GetProperty("path").EnumerateArray().Select(segment => segment.GetProperty("relationshipId").GetInt64()))}")
        .ToArray();

    private sealed record DocumentFixture(long Id);
    private sealed record SystemFixture(long Id);
    private sealed record StructuredTarget(KnowledgeTargetType Type, long Id);
    private sealed record StructuredTargets(
        long SystemId,
        long BusinessFunctionId,
        long DatabaseObjectId,
        long BusinessRuleId,
        long IntegrationId,
        long ReferenceOnlySystemId,
        long ForbiddenStructuredSystemId)
    {
        public IReadOnlyList<StructuredTarget> All =>
        [
            new(KnowledgeTargetType.System, SystemId),
            new(KnowledgeTargetType.BusinessFunction, BusinessFunctionId),
            new(KnowledgeTargetType.DatabaseObject, DatabaseObjectId),
            new(KnowledgeTargetType.BusinessRule, BusinessRuleId),
            new(KnowledgeTargetType.Integration, IntegrationId),
        ];
    }

    private sealed record ReadOnlySnapshot(
        int Documents,
        int Revisions,
        int Relations,
        int Evidence,
        long FtsRows,
        long CurrentRevisionNumber,
        long Version,
        DateTimeOffset UpdatedAt);
}
