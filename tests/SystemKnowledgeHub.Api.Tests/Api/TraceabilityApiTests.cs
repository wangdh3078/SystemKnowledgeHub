using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Traceability.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;
using Xunit.Abstractions;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class TraceabilityApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;
    private readonly HttpClient _client;
    private readonly ITestOutputHelper _output;
    private int _sequence;

    public TraceabilityApiTests(BootstrapWebApplicationFactory factory, ITestOutputHelper output)
    {
        _factory = factory;
        _client = factory.CreateAuthenticatedClient();
        _output = output;
    }

    [Fact]
    public async Task Requirement_projection_covers_all_frozen_missing_link_cases_and_stable_order()
    {
        var missingBoth = await AddDocument(DocumentType.Requirement, "R missing both");
        var missingResponse = await GetTrace(missingBoth.Id);
        Assert.Equal(new[] { "MissingSpecification", "MissingTestDefinition" },
            Strings(missingResponse.GetProperty("coverage").GetProperty("missingLinkCodes")));

        var directOnly = await AddDocument(DocumentType.Requirement, "R direct only");
        var directTest = await AddDocument(DocumentType.TestCase, "T direct");
        await AddRelation(directOnly.Id, RelationType.VerifiedBy, directTest.Id);
        var directResponse = await GetTrace(directOnly.Id);
        Assert.False(directResponse.GetProperty("coverage").GetProperty("hasSpecification").GetBoolean());
        Assert.True(directResponse.GetProperty("coverage").GetProperty("hasDirectTestDefinition").GetBoolean());
        Assert.False(directResponse.GetProperty("coverage").GetProperty("hasSpecificationTestDefinition").GetBoolean());
        Assert.Equal(new[] { "MissingSpecification" },
            Strings(directResponse.GetProperty("coverage").GetProperty("missingLinkCodes")));

        var specificationOnly = await AddDocument(DocumentType.Requirement, "R specification only");
        var specificationWithoutTest = await AddDocument(DocumentType.Specification, "S without test");
        await AddRelation(specificationOnly.Id, RelationType.SpecifiedBy, specificationWithoutTest.Id);
        var specificationOnlyResponse = await GetTrace(specificationOnly.Id);
        Assert.True(specificationOnlyResponse.GetProperty("coverage").GetProperty("hasSpecification").GetBoolean());
        Assert.False(specificationOnlyResponse.GetProperty("coverage").GetProperty("hasAnyTestDefinition").GetBoolean());
        Assert.Equal("MissingTestDefinition",
            specificationOnlyResponse.GetProperty("specifications")[0]
                .GetProperty("coverage").GetProperty("missingLinkCodes")[0].GetString());

        var partial = await AddDocument(DocumentType.Requirement, "R partial branches");
        var zSpecification = await AddDocument(DocumentType.Specification, "Z specification");
        var aSpecification = await AddDocument(DocumentType.Specification, "A specification");
        var nestedTest = await AddDocument(DocumentType.TestCase, "T nested");
        await AddRelation(partial.Id, RelationType.SpecifiedBy, zSpecification.Id);
        await AddRelation(partial.Id, RelationType.SpecifiedBy, aSpecification.Id);
        await AddRelation(aSpecification.Id, RelationType.VerifiedBy, nestedTest.Id);
        var partialResponse = await GetTrace(partial.Id);
        Assert.True(partialResponse.GetProperty("coverage").GetProperty("hasSpecificationTestDefinition").GetBoolean());
        Assert.True(partialResponse.GetProperty("coverage").GetProperty("hasAnyTestDefinition").GetBoolean());
        Assert.Empty(partialResponse.GetProperty("coverage").GetProperty("missingLinkCodes").EnumerateArray());
        var branches = partialResponse.GetProperty("specifications");
        Assert.Equal(new[] { "A specification", "Z specification" },
            branches.EnumerateArray().Select(item => item.GetProperty("document").GetProperty("title").GetString()));
        Assert.True(branches[0].GetProperty("coverage").GetProperty("hasTestDefinition").GetBoolean());
        Assert.False(branches[1].GetProperty("coverage").GetProperty("hasTestDefinition").GetBoolean());

        var samePathRoot = await AddDocument(DocumentType.Requirement, "R same target paths");
        var samePathSpecification = await AddDocument(DocumentType.Specification, "S same target paths");
        var samePathTest = await AddDocument(DocumentType.TestCase, "T same target paths");
        await AddRelation(samePathRoot.Id, RelationType.SpecifiedBy, samePathSpecification.Id);
        await AddRelation(samePathRoot.Id, RelationType.VerifiedBy, samePathTest.Id);
        await AddRelation(samePathSpecification.Id, RelationType.VerifiedBy, samePathTest.Id);
        var samePathResponse = await GetTrace(samePathRoot.Id);
        Assert.Equal(samePathTest.Id,
            samePathResponse.GetProperty("directTestCases")[0].GetProperty("document").GetProperty("id").GetInt64());
        Assert.Equal(samePathTest.Id,
            samePathResponse.GetProperty("specifications")[0].GetProperty("testCases")[0]
                .GetProperty("document").GetProperty("id").GetInt64());
    }

    [Fact]
    public async Task Specification_and_TestCase_models_preserve_canonical_inverse_context()
    {
        var requirement = await AddDocument(DocumentType.Requirement, "M requirement");
        var specification = await AddDocument(DocumentType.Specification, "A specification root");
        var test = await AddDocument(DocumentType.TestCase, "Z test root");
        await AddRelation(requirement.Id, RelationType.SpecifiedBy, specification.Id);
        await AddRelation(requirement.Id, RelationType.VerifiedBy, test.Id);
        await AddRelation(specification.Id, RelationType.VerifiedBy, test.Id);

        var specificationResponse = await GetTrace(specification.Id);
        Assert.Equal("Specification", specificationResponse.GetProperty("root").GetProperty("documentType").GetString());
        Assert.True(specificationResponse.GetProperty("coverage").GetProperty("hasTestDefinition").GetBoolean());
        Assert.Equal("Incoming", specificationResponse.GetProperty("upstreamRequirements")[0]
            .GetProperty("relationship").GetProperty("direction").GetString());
        Assert.Equal("Outgoing", specificationResponse.GetProperty("testCases")[0]
            .GetProperty("relationship").GetProperty("direction").GetString());

        var testResponse = await GetTrace(test.Id);
        Assert.Equal(requirement.Id, testResponse.GetProperty("directRequirements")[0]
            .GetProperty("document").GetProperty("id").GetInt64());
        var specificationBranch = testResponse.GetProperty("upstreamSpecifications")[0];
        Assert.Equal(specification.Id, specificationBranch.GetProperty("document").GetProperty("id").GetInt64());
        Assert.Equal(requirement.Id, specificationBranch.GetProperty("upstreamRequirements")[0]
            .GetProperty("document").GetProperty("id").GetInt64());
        Assert.Empty(testResponse.GetProperty("coverage").GetProperty("missingLinkCodes").EnumerateArray());
    }

    [Fact]
    public async Task Lifecycle_rules_include_Draft_and_Published_but_exclude_Archived_children_and_gaps()
    {
        var requirement = await AddDocument(DocumentType.Requirement, "Lifecycle requirement");
        var draftSpecification = await AddDocument(DocumentType.Specification, "Draft specification");
        var publishedSpecification = await AddDocument(
            DocumentType.Specification, "Published specification", DocumentLifecycleStatus.Published);
        var archivedSpecification = await AddDocument(
            DocumentType.Specification, "Archived specification", DocumentLifecycleStatus.Archived);
        await AddRelation(requirement.Id, RelationType.SpecifiedBy, draftSpecification.Id);
        await AddRelation(requirement.Id, RelationType.SpecifiedBy, publishedSpecification.Id);
        await AddRelation(requirement.Id, RelationType.SpecifiedBy, archivedSpecification.Id);
        var response = await GetTrace(requirement.Id);
        Assert.Equal(2, response.GetProperty("specifications").GetArrayLength());
        Assert.True(response.GetProperty("coverage").GetProperty("hasSpecification").GetBoolean());

        var archivedOnlyRequirement = await AddDocument(DocumentType.Requirement, "Archived child only");
        await AddRelation(archivedOnlyRequirement.Id, RelationType.SpecifiedBy, archivedSpecification.Id);
        var archivedOnly = await GetTrace(archivedOnlyRequirement.Id);
        Assert.False(archivedOnly.GetProperty("coverage").GetProperty("hasSpecification").GetBoolean());
        Assert.Contains("MissingSpecification", Strings(archivedOnly.GetProperty("coverage").GetProperty("missingLinkCodes")));

        var archivedTest = await AddDocument(DocumentType.TestCase, "Archived test", DocumentLifecycleStatus.Archived);
        await AddRelation(draftSpecification.Id, RelationType.VerifiedBy, archivedTest.Id);
        var specificationResponse = await GetTrace(draftSpecification.Id);
        Assert.False(specificationResponse.GetProperty("coverage").GetProperty("hasTestDefinition").GetBoolean());
        Assert.Equal("MissingTestDefinition",
            specificationResponse.GetProperty("coverage").GetProperty("missingLinkCodes")[0].GetString());

        var archivedRoot = await GetTrace(archivedSpecification.Id);
        Assert.Equal("ExcludedArchived", archivedRoot.GetProperty("coverage").GetProperty("eligibility").GetString());
        Assert.Empty(archivedRoot.GetProperty("coverage").GetProperty("missingLinkCodes").EnumerateArray());
        Assert.Empty(archivedRoot.GetProperty("testCases").EnumerateArray());
    }

    [Fact]
    public async Task Trust_projection_bulk_counts_Evidence_and_HumanConfirmation_without_changing_coverage()
    {
        var requirement = await AddDocument(
            DocumentType.Requirement, "Trusted requirement", knowledgeStatus: KnowledgeStatus.Confirmed,
            currentRevisionNumber: 2);
        var specification = await AddDocument(
            DocumentType.Specification, "Unknown relationship target", knowledgeStatus: KnowledgeStatus.Inferred);
        var relation = await AddRelation(
            requirement.Id, RelationType.SpecifiedBy, specification.Id, KnowledgeStatus.Unknown);
        await AddEvidence(EvidenceSubjectType.KnowledgeDocument, requirement.Id, EvidenceType.ExistingDocument);
        await AddEvidence(
            EvidenceSubjectType.KnowledgeDocument,
            requirement.Id,
            EvidenceType.HumanConfirmation,
            revisionSnapshot: 1);
        await AddEvidence(EvidenceSubjectType.KnowledgeRelation, relation.Id, EvidenceType.Sql);
        await AddEvidence(EvidenceSubjectType.KnowledgeRelation, relation.Id, EvidenceType.HumanConfirmation);

        var response = await GetTrace(requirement.Id);
        var root = response.GetProperty("root");
        Assert.Equal("Confirmed", root.GetProperty("knowledgeStatus").GetString());
        Assert.Equal(2, root.GetProperty("evidenceCount").GetInt32());
        Assert.Equal(1, root.GetProperty("humanConfirmationCount").GetInt32());
        Assert.Equal("ChangedSinceConfirmation",
            root.GetProperty("confirmationCoverage").GetProperty("state").GetString());
        Assert.Equal(1, root.GetProperty("confirmationCoverage")
            .GetProperty("lastConfirmedRevisionNumber").GetInt64());
        var edge = response.GetProperty("specifications")[0].GetProperty("relationship");
        Assert.Equal("Unknown", edge.GetProperty("knowledgeStatus").GetString());
        Assert.Equal(2, edge.GetProperty("evidenceCount").GetInt32());
        Assert.Equal(1, edge.GetProperty("humanConfirmationCount").GetInt32());
        Assert.True(response.GetProperty("coverage").GetProperty("hasSpecification").GetBoolean());

        var legacy = await AddDocument(DocumentType.Requirement, "Legacy confirmation root");
        await AddEvidence(EvidenceSubjectType.KnowledgeDocument, legacy.Id, EvidenceType.HumanConfirmation);
        Assert.Equal("LegacyConfirmationUnknown",
            (await GetTrace(legacy.Id)).GetProperty("root").GetProperty("confirmationCoverage")
                .GetProperty("state").GetString());
        var current = await AddDocument(DocumentType.Requirement, "Current confirmation root");
        await AddEvidence(
            EvidenceSubjectType.KnowledgeDocument,
            current.Id,
            EvidenceType.HumanConfirmation,
            revisionSnapshot: 1);
        Assert.Equal("CurrentRevisionConfirmed",
            (await GetTrace(current.Id)).GetProperty("root").GetProperty("confirmationCoverage")
                .GetProperty("state").GetString());
    }

    [Fact]
    public async Task Supersedes_is_direct_ordered_bounded_and_never_changes_lifecycle_or_coverage()
    {
        var root = await AddDocument(DocumentType.Requirement, "Lineage root");
        var incomingDocument = await AddDocument(
            DocumentType.Requirement, "Incoming replacement", DocumentLifecycleStatus.Archived);
        var outgoingDocument = await AddDocument(DocumentType.Requirement, "Outgoing older");
        await AddRelation(root.Id, RelationType.Supersedes, outgoingDocument.Id);
        await AddRelation(incomingDocument.Id, RelationType.Supersedes, root.Id);
        await AddRelation(outgoingDocument.Id, RelationType.Supersedes, root.Id);

        var response = await GetTrace(root.Id);
        Assert.Equal(3, response.GetProperty("lineage").GetProperty("total").GetInt32());
        Assert.Single(response.GetProperty("lineage").GetProperty("outgoing").EnumerateArray());
        Assert.Equal(2, response.GetProperty("lineage").GetProperty("incoming").GetArrayLength());
        Assert.True(response.GetProperty("cycleDetected").GetBoolean());
        Assert.Equal("Draft", response.GetProperty("root").GetProperty("lifecycleStatus").GetString());
        Assert.Equal(new[] { "MissingSpecification", "MissingTestDefinition" },
            Strings(response.GetProperty("coverage").GetProperty("missingLinkCodes")));
        Assert.Contains(response.GetProperty("lineage").GetProperty("incoming").EnumerateArray(),
            item => item.GetProperty("document").GetProperty("lifecycleStatus").GetString() == "Archived");

        var cappedRoot = await AddDocument(DocumentType.Requirement, "Lineage capped root");
        for (var index = 0; index < 21; index++)
        {
            var older = await AddDocument(DocumentType.Requirement, $"Lineage old {index:00}");
            await AddRelation(cappedRoot.Id, RelationType.Supersedes, older.Id);
        }
        var capped = await GetTrace(cappedRoot.Id);
        Assert.Equal(21, capped.GetProperty("lineage").GetProperty("total").GetInt32());
        Assert.Equal(20, capped.GetProperty("lineage").GetProperty("outgoing").GetArrayLength());
        Assert.True(capped.GetProperty("lineage").GetProperty("isTruncated").GetBoolean());
    }

    [Fact]
    public async Task Node_truncation_does_not_create_false_missing_Test_Definition()
    {
        var root = await AddDocument(DocumentType.Requirement, "Node cap root");
        DocumentFixture? hiddenTestedSpecification = null;
        for (var index = 0; index < 200; index++)
        {
            var specification = await AddDocument(DocumentType.Specification, $"Node spec {index:000}");
            await AddRelation(root.Id, RelationType.SpecifiedBy, specification.Id);
            if (index == 199) hiddenTestedSpecification = specification;
        }
        var test = await AddDocument(DocumentType.TestCase, "Node cap hidden test");
        await AddRelation(hiddenTestedSpecification!.Id, RelationType.VerifiedBy, test.Id);

        var response = await GetTrace(root.Id);
        Assert.True(response.GetProperty("isTruncated").GetBoolean());
        Assert.Contains("MaxNodes", Strings(response.GetProperty("truncationReasons")));
        Assert.Equal(199, response.GetProperty("specifications").GetArrayLength());
        Assert.True(response.GetProperty("coverage").GetProperty("hasSpecificationTestDefinition").GetBoolean());
        Assert.True(response.GetProperty("coverage").GetProperty("hasAnyTestDefinition").GetBoolean());
        Assert.DoesNotContain("MissingTestDefinition",
            Strings(response.GetProperty("coverage").GetProperty("missingLinkCodes")));
    }

    [Fact]
    public async Task Edge_truncation_preserves_distinct_paths_to_the_same_TestCase()
    {
        var root = await AddDocument(DocumentType.Requirement, "Edge cap root");
        var sharedTest = await AddDocument(DocumentType.TestCase, "Shared Test Definition");
        for (var index = 0; index < 151; index++)
        {
            var specification = await AddDocument(DocumentType.Specification, $"Edge spec {index:000}");
            await AddRelation(root.Id, RelationType.SpecifiedBy, specification.Id);
            await AddRelation(specification.Id, RelationType.VerifiedBy, sharedTest.Id);
        }

        var response = await GetTrace(root.Id);
        Assert.True(response.GetProperty("isTruncated").GetBoolean());
        Assert.Contains("MaxEdges", Strings(response.GetProperty("truncationReasons")));
        Assert.Equal(150, response.GetProperty("specifications").GetArrayLength());
        Assert.All(response.GetProperty("specifications").EnumerateArray(), branch =>
            Assert.Equal(sharedTest.Id,
                branch.GetProperty("testCases")[0].GetProperty("document").GetProperty("id").GetInt64()));
        Assert.Equal(300, response.GetProperty("limits").GetProperty("maxEdges").GetInt32());

        using var payloadResponse = await _client.GetAsync(
            $"/api/knowledge-documents/{root.Id}/traceability");
        var payload = await payloadResponse.Content.ReadAsByteArrayAsync();
        var payloadText = System.Text.Encoding.UTF8.GetString(payload);
        Assert.DoesNotContain("bodyMarkdown", payloadText, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("renderedHtml", payloadText, StringComparison.OrdinalIgnoreCase);
        _output.WriteLine(
            "FANOUT nodeCount=152 edgeCount=300 payloadBytes={0} isTruncated=true",
            payload.Length);
    }

    [Fact]
    public async Task Authorization_validation_root_type_and_unknown_depth_follow_existing_contracts()
    {
        var requirement = await AddDocument(DocumentType.Requirement, "Authorization root");
        using var anonymous = _factory.CreateClient();
        Assert.Equal(HttpStatusCode.Unauthorized,
            (await anonymous.GetAsync($"/api/knowledge-documents/{requirement.Id}/traceability")).StatusCode);

        foreach (var accessLevel in new[] { AccessLevel.Viewer, AccessLevel.Editor, AccessLevel.Administrator })
        {
            var userId = await AddUser(accessLevel);
            using var client = await _factory.CreateAuthenticatedClientAsync(userId);
            Assert.Equal(HttpStatusCode.OK,
                (await client.GetAsync($"/api/knowledge-documents/{requirement.Id}/traceability")).StatusCode);
        }

        foreach (var invalidId in new[] { "0", "-1", "9007199254740992", "malformed" })
        {
            using var invalid = await _client.GetAsync($"/api/knowledge-documents/{invalidId}/traceability");
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("validation_error", (await invalid.Content.ReadFromJsonAsync<JsonElement>())
                .GetProperty("code").GetString());
        }
        using var missing = await _client.GetAsync("/api/knowledge-documents/9007199254740991/traceability");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        var sop = await AddDocument(DocumentType.Sop, "Unsupported trace root");
        using var unsupported = await _client.GetAsync($"/api/knowledge-documents/{sop.Id}/traceability");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, unsupported.StatusCode);
        Assert.Equal("business_rule_violation", (await unsupported.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("code").GetString());

        using var ignoredDepth = await _client.GetAsync(
            $"/api/knowledge-documents/{requirement.Id}/traceability?depth=999");
        Assert.Equal(HttpStatusCode.OK, ignoredDepth.StatusCode);
        Assert.Equal(2, (await ignoredDepth.Content.ReadFromJsonAsync<JsonElement>())
            .GetProperty("limits").GetProperty("maxDepth").GetInt32());
    }

    [Fact]
    public async Task Invalid_selected_endpoint_fails_closed_without_target_metadata()
    {
        var requirement = await AddDocument(DocumentType.Requirement, "Invalid reference root");
        var secretTest = await AddDocument(DocumentType.TestCase, "SECRET TARGET TITLE");
        await AddRelation(requirement.Id, RelationType.SpecifiedBy, secretTest.Id);

        using var response = await _client.GetAsync($"/api/knowledge-documents/{requirement.Id}/traceability");
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var bodyText = await response.Content.ReadAsStringAsync();
        Assert.Contains("reference_invalid", bodyText);
        Assert.DoesNotContain("SECRET TARGET TITLE", bodyText);
        Assert.DoesNotContain(secretTest.Id.ToString(), bodyText);
    }

    [Fact]
    public async Task Trace_GET_is_read_only_for_documents_revisions_relations_evidence_and_FTS()
    {
        var requirement = await AddDocument(DocumentType.Requirement, "Read only root");
        var specification = await AddDocument(DocumentType.Specification, "Read only specification");
        await AddRelation(requirement.Id, RelationType.SpecifiedBy, specification.Id);
        await AddEvidence(EvidenceSubjectType.KnowledgeDocument, requirement.Id, EvidenceType.ExistingDocument);
        var before = await Snapshot(requirement.Id);

        using var response = await _client.GetAsync($"/api/knowledge-documents/{requirement.Id}/traceability");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadAsStringAsync();
        Assert.DoesNotContain("bodyMarkdown", payload);
        Assert.DoesNotContain("renderedHtml", payload);

        var after = await Snapshot(requirement.Id);
        Assert.Equal(before, after);
    }

    [Fact]
    public async Task Representative_SQLite_query_plans_use_existing_indexes()
    {
        var root = await AddDocument(DocumentType.Requirement, "Query plan root");
        var plans = new Dictionary<string, string>
        {
            ["root"] = await Explain("SELECT id FROM knowledge_documents WHERE id = $id", root.Id),
            ["outgoing"] = await Explain("SELECT id FROM knowledge_relations WHERE source_type = 'KnowledgeDocument' AND source_id = $id AND relation_type IN ('SpecifiedBy','VerifiedBy')", root.Id),
            ["incoming"] = await Explain("SELECT id FROM knowledge_relations WHERE target_type = 'KnowledgeDocument' AND target_id = $id AND relation_type IN ('SpecifiedBy','VerifiedBy')", root.Id),
            ["evidence"] = await Explain("SELECT subject_id, COUNT(*) FROM evidence WHERE subject_type = 'KnowledgeDocument' AND subject_id = $id GROUP BY subject_id", root.Id),
            ["supersedes"] = await Explain("SELECT id FROM knowledge_relations WHERE source_type = 'KnowledgeDocument' AND source_id = $id AND relation_type = 'Supersedes'", root.Id),
        };
        Assert.Contains("INTEGER PRIMARY KEY", plans["root"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INDEX", plans["outgoing"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INDEX", plans["incoming"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INDEX", plans["evidence"], StringComparison.OrdinalIgnoreCase);
        Assert.Contains("INDEX", plans["supersedes"], StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Fixed_path_cycle_guard_detects_repeated_nodes_and_relationships_without_mutation()
    {
        var guard = new TraceTraversalGuard();
        Assert.True(guard.ObservePath(
            [(DocumentType.Requirement, 1), (DocumentType.Specification, 2)], [10]));
        Assert.False(guard.ObservePath(
            [(DocumentType.Requirement, 1), (DocumentType.Specification, 2), (DocumentType.Requirement, 1)],
            [10, 11]));
        Assert.True(guard.CycleDetected);

        var relationshipGuard = new TraceTraversalGuard();
        Assert.False(relationshipGuard.ObservePath(
            [(DocumentType.Requirement, 1), (DocumentType.Specification, 2), (DocumentType.TestCase, 3)],
            [10, 10]));
        Assert.True(relationshipGuard.CycleDetected);
    }

    private async Task<JsonElement> GetTrace(long id)
    {
        using var response = await _client.GetAsync($"/api/knowledge-documents/{id}/traceability");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private async Task<DocumentFixture> AddDocument(
        DocumentType documentType,
        string title,
        DocumentLifecycleStatus lifecycleStatus = DocumentLifecycleStatus.Draft,
        KnowledgeStatus knowledgeStatus = KnowledgeStatus.Unknown,
        int currentRevisionNumber = 1)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = await dbContext.Users.OrderBy(item => item.Id).FirstAsync();
        var timestamp = DateTimeOffset.UtcNow.AddMilliseconds(++_sequence);
        var document = new KnowledgeDocument
        {
            DocumentType = documentType,
            Title = title,
            BodyMarkdown = "fixture body",
            LifecycleStatus = lifecycleStatus,
            KnowledgeStatus = knowledgeStatus,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = user.DisplayName,
            KnowledgeStatusChangedByRole = "Trace Test",
            CreatedByUserId = user.Id,
            CreatedByDisplayName = user.DisplayName,
            UpdatedByUserId = user.Id,
            UpdatedByDisplayName = user.DisplayName,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            PublishedAt = lifecycleStatus == DocumentLifecycleStatus.Published ? timestamp : null,
            ArchivedAt = lifecycleStatus == DocumentLifecycleStatus.Archived ? timestamp : null,
            CurrentRevisionNumber = currentRevisionNumber,
            LatestPublishedRevisionNumber = lifecycleStatus == DocumentLifecycleStatus.Published
                ? currentRevisionNumber
                : null,
            Version = currentRevisionNumber,
        };
        dbContext.KnowledgeDocuments.Add(document);
        await dbContext.SaveChangesAsync();
        for (var revisionNumber = 1; revisionNumber <= currentRevisionNumber; revisionNumber++)
        {
            dbContext.KnowledgeDocumentRevisions.Add(new KnowledgeDocumentRevision
            {
                KnowledgeDocumentId = document.Id,
                RevisionNumber = revisionNumber,
                Title = title,
                BodyMarkdown = "fixture body",
                AuthorUserId = user.Id,
                AuthorDisplayNameSnapshot = user.DisplayName,
                CreatedAt = timestamp.AddTicks(revisionNumber),
                LifecycleContext = lifecycleStatus,
                RevisionOrigin = revisionNumber == 1 ? RevisionOrigin.Created : RevisionOrigin.ContentSave,
            });
        }
        await dbContext.SaveChangesAsync();
        return new DocumentFixture(document.Id, document.Version, document.UpdatedAt);
    }

    private async Task<RelationFixture> AddRelation(
        long sourceId,
        RelationType relationType,
        long targetId,
        KnowledgeStatus knowledgeStatus = KnowledgeStatus.Unknown)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow.AddMilliseconds(++_sequence);
        var relation = new KnowledgeRelation
        {
            SourceType = KnowledgeTargetType.KnowledgeDocument,
            SourceId = sourceId,
            TargetType = KnowledgeTargetType.KnowledgeDocument,
            TargetId = targetId,
            RelationType = relationType,
            CreatedAt = timestamp,
            CreatedByName = "Trace Test",
            UpdatedAt = timestamp,
            KnowledgeStatus = knowledgeStatus,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "Trace Test",
            KnowledgeStatusChangedByRole = "Trace Test",
            Version = 1,
        };
        dbContext.KnowledgeRelations.Add(relation);
        await dbContext.SaveChangesAsync();
        return new RelationFixture(relation.Id);
    }

    private async Task AddEvidence(
        EvidenceSubjectType subjectType,
        long subjectId,
        EvidenceType evidenceType,
        long? revisionSnapshot = null)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow.AddMilliseconds(++_sequence);
        dbContext.Evidence.Add(new Evidence
        {
            EvidenceType = evidenceType,
            SubjectType = subjectType,
            SubjectId = subjectId,
            SourceTitle = $"Trace source {_sequence}",
            SourceReference = $"trace://{Guid.NewGuid():N}",
            SupportReason = "TRACE-B01 focused fixture",
            ProviderName = "Trace Test",
            ProviderRole = "Trace Test",
            ProvidedAt = timestamp,
            KnowledgeDocumentRevisionNumberSnapshot = revisionSnapshot,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        });
        await dbContext.SaveChangesAsync();
    }

    private async Task<long> AddUser(AccessLevel accessLevel)
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"Trace {accessLevel} {Guid.NewGuid():N}",
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
        var document = await dbContext.KnowledgeDocuments.AsNoTracking().SingleAsync(item => item.Id == documentId);
        await using var command = dbContext.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM knowledge_documents_fts";
        var ftsCount = Convert.ToInt64(await command.ExecuteScalarAsync());
        return new ReadOnlySnapshot(
            await dbContext.KnowledgeDocuments.CountAsync(),
            await dbContext.KnowledgeDocumentRevisions.CountAsync(),
            await dbContext.KnowledgeRelations.CountAsync(),
            await dbContext.Evidence.CountAsync(),
            ftsCount,
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

    private static string[] Strings(JsonElement array) =>
        array.EnumerateArray().Select(item => item.GetString()!).ToArray();

    private sealed record DocumentFixture(long Id, long Version, DateTimeOffset UpdatedAt);
    private sealed record RelationFixture(long Id);
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
