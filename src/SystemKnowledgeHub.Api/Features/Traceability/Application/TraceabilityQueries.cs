using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Traceability.Application.Models;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Traceability.Application;

/// <summary>
/// Builds the fixed Requirement/Specification/TestCase read projection from current canonical truth.
/// </summary>
public sealed class TraceabilityQueries(KnowledgeHubDbContext dbContext)
{
    public const int MaximumDepth = 2;
    public const int MaximumNodes = 200;
    public const int MaximumEdges = 300;
    public const int MaximumLineageEntries = 20;

    private static readonly TraceLimitsResponse Limits = new(
        MaximumDepth,
        MaximumNodes,
        MaximumEdges,
        MaximumLineageEntries);

    private IQueryable<KnowledgeDocument> PhysicalDocuments =>
        dbContext.KnowledgeDocuments.IgnoreQueryFilters().AsNoTracking();

    public async Task<TraceabilityQueryResult> Get(long id, CancellationToken cancellationToken)
    {
        var rootEntity = await dbContext.KnowledgeDocuments.AsNoTracking()
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);
        if (rootEntity is null)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.NotFound);
        }
        if (rootEntity.DocumentType is not (
            DocumentType.Requirement or DocumentType.Specification or DocumentType.TestCase))
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.UnsupportedDocumentType);
        }

        var root = DocumentSnapshot.From(rootEntity);
        if (root.LifecycleStatus == DocumentLifecycleStatus.Archived)
        {
            return await BuildArchived(root, cancellationToken);
        }

        return root.DocumentType switch
        {
            DocumentType.Requirement => await BuildRequirement(root, cancellationToken),
            DocumentType.Specification => await BuildSpecification(root, cancellationToken),
            DocumentType.TestCase => await BuildTestCase(root, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported traceability root type."),
        };
    }

    private async Task<TraceabilityQueryResult> BuildArchived(
        DocumentSnapshot root,
        CancellationToken cancellationToken)
    {
        var lineageResult = await LoadLineage(root, cancellationToken);
        if (lineageResult.ReferenceInvalid)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }
        var trust = await LoadTrust(
            [root],
            lineageResult.Entries.Select(entry => entry.RelationshipId),
            cancellationToken);
        var rootResponse = ToDocument(root, trust);
        var lineage = BuildLineage(lineageResult, trust);
        ITraceabilityResponse response = root.DocumentType switch
        {
            DocumentType.Requirement => new RequirementTraceabilityResponse(
                rootResponse,
                new TraceRequirementCoverageResponse(
                    TraceCoverageEligibility.ExcludedArchived,
                    false,
                    false,
                    false,
                    false,
                    []),
                [], [], [], lineage, lineageResult.CycleDetected, false, [], Limits),
            DocumentType.Specification => new SpecificationTraceabilityResponse(
                rootResponse,
                new TraceSpecificationCoverageResponse(
                    TraceCoverageEligibility.ExcludedArchived,
                    false,
                    []),
                [], [], lineage, lineageResult.CycleDetected, false, [], Limits),
            DocumentType.TestCase => new TestCaseTraceabilityResponse(
                rootResponse,
                new TraceTestCaseCoverageResponse(TraceCoverageEligibility.ExcludedArchived, []),
                [], [], lineage, lineageResult.CycleDetected, false, [], Limits),
            _ => throw new InvalidOperationException("Unsupported traceability root type."),
        };
        return new TraceabilityQueryResult(response, TraceabilityQueryFailure.None);
    }

    private async Task<TraceabilityQueryResult> BuildRequirement(
        DocumentSnapshot root,
        CancellationToken cancellationToken)
    {
        var directRelations = dbContext.KnowledgeRelations.AsNoTracking().Where(relation =>
            relation.SourceType == KnowledgeTargetType.KnowledgeDocument
            && relation.SourceId == root.Id
            && (relation.RelationType == RelationType.SpecifiedBy
                || relation.RelationType == RelationType.VerifiedBy));

        var invalidDirect = await directRelations.AnyAsync(relation =>
            relation.TargetType != KnowledgeTargetType.KnowledgeDocument
            || !PhysicalDocuments.Any(document =>
                document.Id == relation.TargetId
                && (relation.RelationType == RelationType.SpecifiedBy
                    ? document.DocumentType == DocumentType.Specification
                    : document.DocumentType == DocumentType.TestCase)), cancellationToken);
        if (invalidDirect)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }

        var activeSpecifications =
            from relation in directRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            where relation.RelationType == RelationType.SpecifiedBy
                && document.DocumentType == DocumentType.Specification
                && document.LifecycleStatus != DocumentLifecycleStatus.Archived
            select document.Id;

        var nestedRelations = dbContext.KnowledgeRelations.AsNoTracking().Where(relation =>
            relation.SourceType == KnowledgeTargetType.KnowledgeDocument
            && activeSpecifications.Contains(relation.SourceId)
            && relation.RelationType == RelationType.VerifiedBy);
        var invalidNested = await nestedRelations.AnyAsync(relation =>
            relation.TargetType != KnowledgeTargetType.KnowledgeDocument
            || !PhysicalDocuments.Any(document =>
                document.Id == relation.TargetId
                && document.DocumentType == DocumentType.TestCase), cancellationToken);
        if (invalidNested)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }

        var direct = await (
            from relation in directRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            where document.LifecycleStatus != DocumentLifecycleStatus.Archived
            orderby relation.RelationType == RelationType.SpecifiedBy ? 0 : 1,
                document.Title, document.Id, relation.Id
            select EdgeCandidate.Create(relation, document, TraceDirection.Outgoing, root.Id))
            .Take(MaximumEdges + 1)
            .ToArrayAsync(cancellationToken);

        var nested = await (
            from relation in nestedRelations
            join source in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.SourceId equals source.Id
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            where document.LifecycleStatus != DocumentLifecycleStatus.Archived
            orderby source.Title, source.Id, document.Title, document.Id, relation.Id
            select EdgeCandidate.Create(relation, document, TraceDirection.Outgoing, source.Id))
            .Take(MaximumEdges + 1)
            .ToArrayAsync(cancellationToken);

        var candidateSpecificationIds = direct
            .Where(candidate => candidate.Document.DocumentType == DocumentType.Specification)
            .Select(candidate => candidate.Document.Id)
            .Distinct()
            .ToArray();
        var testedSpecificationIds = await (
            from relation in nestedRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            where candidateSpecificationIds.Contains(relation.SourceId)
                && document.LifecycleStatus != DocumentLifecycleStatus.Archived
            select relation.SourceId)
            .Distinct()
            .ToArrayAsync(cancellationToken);
        var testedSpecifications = testedSpecificationIds.ToHashSet();

        var hasSpecification = await activeSpecifications.AnyAsync(cancellationToken);
        var hasDirectTestDefinition = await (
            from relation in directRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            where relation.RelationType == RelationType.VerifiedBy
                && document.LifecycleStatus != DocumentLifecycleStatus.Archived
            select relation.Id).AnyAsync(cancellationToken);
        var hasSpecificationTestDefinition = await (
            from relation in nestedRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            where document.LifecycleStatus != DocumentLifecycleStatus.Archived
            select relation.Id).AnyAsync(cancellationToken);

        var lineageResult = await LoadLineage(root, cancellationToken);
        if (lineageResult.ReferenceInvalid)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }
        var allCandidates = direct.Concat(nested).Concat(lineageResult.Entries).ToArray();
        var trust = await LoadTrust(
            allCandidates.Select(candidate => candidate.Document).Append(root),
            allCandidates.Select(candidate => candidate.RelationshipId),
            cancellationToken);

        var limiter = new DisplayLimiter(root.Id);
        var guard = new TraceTraversalGuard();
        var nestedBySpecification = nested
            .GroupBy(candidate => candidate.ParentDocumentId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var specifications = new List<TraceSpecificationBranchResponse>();
        foreach (var specification in direct.Where(candidate =>
                     candidate.RelationType == RelationType.SpecifiedBy))
        {
            if (!guard.ObservePath(
                    [(root.DocumentType, root.Id), (specification.Document.DocumentType, specification.Document.Id)],
                    [specification.RelationshipId])
                || !limiter.TryAdd(specification))
            {
                continue;
            }

            var tests = new List<TraceDocumentRelationResponse>();
            if (nestedBySpecification.TryGetValue(specification.Document.Id, out var nestedTests))
            {
                foreach (var test in nestedTests)
                {
                    if (!guard.ObservePath(
                            [
                                (root.DocumentType, root.Id),
                                (specification.Document.DocumentType, specification.Document.Id),
                                (test.Document.DocumentType, test.Document.Id),
                            ],
                            [specification.RelationshipId, test.RelationshipId])
                        || !limiter.TryAdd(test))
                    {
                        continue;
                    }
                    tests.Add(ToDocumentRelation(test, trust));
                }
            }
            var branchHasTest = testedSpecifications.Contains(specification.Document.Id);
            specifications.Add(new TraceSpecificationBranchResponse(
                ToRelationship(specification, trust),
                ToDocument(specification.Document, trust),
                new TraceSpecificationBranchCoverageResponse(
                    branchHasTest,
                    branchHasTest ? [] : [TraceMissingLinkCode.MissingTestDefinition]),
                tests));
        }

        var directTests = new List<TraceDocumentRelationResponse>();
        foreach (var test in direct.Where(candidate => candidate.RelationType == RelationType.VerifiedBy))
        {
            if (!guard.ObservePath(
                    [(root.DocumentType, root.Id), (test.Document.DocumentType, test.Document.Id)],
                    [test.RelationshipId])
                || !limiter.TryAdd(test))
            {
                continue;
            }
            directTests.Add(ToDocumentRelation(test, trust));
        }

        var missing = new List<TraceMissingLinkCode>();
        if (!hasSpecification) missing.Add(TraceMissingLinkCode.MissingSpecification);
        if (!hasDirectTestDefinition && !hasSpecificationTestDefinition)
        {
            missing.Add(TraceMissingLinkCode.MissingTestDefinition);
        }
        var response = new RequirementTraceabilityResponse(
            ToDocument(root, trust),
            new TraceRequirementCoverageResponse(
                TraceCoverageEligibility.Active,
                hasSpecification,
                hasDirectTestDefinition,
                hasSpecificationTestDefinition,
                hasDirectTestDefinition || hasSpecificationTestDefinition,
                missing),
            specifications,
            directTests,
            [],
            BuildLineage(lineageResult, trust),
            guard.CycleDetected || lineageResult.CycleDetected,
            limiter.IsTruncated,
            limiter.Reasons,
            Limits);
        return new TraceabilityQueryResult(response, TraceabilityQueryFailure.None);
    }

    private async Task<TraceabilityQueryResult> BuildSpecification(
        DocumentSnapshot root,
        CancellationToken cancellationToken)
    {
        var incomingRelations = dbContext.KnowledgeRelations.AsNoTracking().Where(relation =>
            relation.TargetType == KnowledgeTargetType.KnowledgeDocument
            && relation.TargetId == root.Id
            && relation.RelationType == RelationType.SpecifiedBy);
        var outgoingRelations = dbContext.KnowledgeRelations.AsNoTracking().Where(relation =>
            relation.SourceType == KnowledgeTargetType.KnowledgeDocument
            && relation.SourceId == root.Id
            && relation.RelationType == RelationType.VerifiedBy);
        var invalidIncoming = await incomingRelations.AnyAsync(relation =>
            relation.SourceType != KnowledgeTargetType.KnowledgeDocument
            || !PhysicalDocuments.Any(document =>
                document.Id == relation.SourceId
                && document.DocumentType == DocumentType.Requirement), cancellationToken);
        var invalidOutgoing = await outgoingRelations.AnyAsync(relation =>
            relation.TargetType != KnowledgeTargetType.KnowledgeDocument
            || !PhysicalDocuments.Any(document =>
                document.Id == relation.TargetId
                && document.DocumentType == DocumentType.TestCase), cancellationToken);
        if (invalidIncoming || invalidOutgoing)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }

        var upstream = await (
            from relation in incomingRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.SourceId equals document.Id
            where document.LifecycleStatus != DocumentLifecycleStatus.Archived
            orderby document.Title, document.Id, relation.Id
            select EdgeCandidate.Create(relation, document, TraceDirection.Incoming, root.Id))
            .Take(MaximumEdges + 1)
            .ToArrayAsync(cancellationToken);
        var tests = await (
            from relation in outgoingRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            where document.LifecycleStatus != DocumentLifecycleStatus.Archived
            orderby document.Title, document.Id, relation.Id
            select EdgeCandidate.Create(relation, document, TraceDirection.Outgoing, root.Id))
            .Take(MaximumEdges + 1)
            .ToArrayAsync(cancellationToken);
        var hasTestDefinition = await (
            from relation in outgoingRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            where document.LifecycleStatus != DocumentLifecycleStatus.Archived
            select relation.Id).AnyAsync(cancellationToken);

        var lineageResult = await LoadLineage(root, cancellationToken);
        if (lineageResult.ReferenceInvalid)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }
        var allCandidates = upstream.Concat(tests).Concat(lineageResult.Entries).ToArray();
        var trust = await LoadTrust(
            allCandidates.Select(candidate => candidate.Document).Append(root),
            allCandidates.Select(candidate => candidate.RelationshipId),
            cancellationToken);
        var limiter = new DisplayLimiter(root.Id);
        var guard = new TraceTraversalGuard();
        var upstreamResponses = AddDirectRelations(root, upstream, limiter, guard, trust);
        var testResponses = AddDirectRelations(root, tests, limiter, guard, trust);
        var response = new SpecificationTraceabilityResponse(
            ToDocument(root, trust),
            new TraceSpecificationCoverageResponse(
                TraceCoverageEligibility.Active,
                hasTestDefinition,
                hasTestDefinition ? [] : [TraceMissingLinkCode.MissingTestDefinition]),
            upstreamResponses,
            testResponses,
            BuildLineage(lineageResult, trust),
            guard.CycleDetected || lineageResult.CycleDetected,
            limiter.IsTruncated,
            limiter.Reasons,
            Limits);
        return new TraceabilityQueryResult(response, TraceabilityQueryFailure.None);
    }

    private async Task<TraceabilityQueryResult> BuildTestCase(
        DocumentSnapshot root,
        CancellationToken cancellationToken)
    {
        var incomingRelations = dbContext.KnowledgeRelations.AsNoTracking().Where(relation =>
            relation.TargetType == KnowledgeTargetType.KnowledgeDocument
            && relation.TargetId == root.Id
            && relation.RelationType == RelationType.VerifiedBy);
        var invalidIncoming = await incomingRelations.AnyAsync(relation =>
            relation.SourceType != KnowledgeTargetType.KnowledgeDocument
            || !PhysicalDocuments.Any(document =>
                document.Id == relation.SourceId
                && (document.DocumentType == DocumentType.Requirement
                    || document.DocumentType == DocumentType.Specification)), cancellationToken);
        if (invalidIncoming)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }

        var incoming = await (
            from relation in incomingRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.SourceId equals document.Id
            where document.LifecycleStatus != DocumentLifecycleStatus.Archived
            orderby document.DocumentType == DocumentType.Requirement ? 0 : 1,
                document.Title, document.Id, relation.Id
            select EdgeCandidate.Create(relation, document, TraceDirection.Incoming, root.Id))
            .Take(MaximumEdges + 1)
            .ToArrayAsync(cancellationToken);

        var activeSpecifications =
            from relation in incomingRelations
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.SourceId equals document.Id
            where document.DocumentType == DocumentType.Specification
                && document.LifecycleStatus != DocumentLifecycleStatus.Archived
            select document.Id;
        var upstreamRelations = dbContext.KnowledgeRelations.AsNoTracking().Where(relation =>
            relation.TargetType == KnowledgeTargetType.KnowledgeDocument
            && activeSpecifications.Contains(relation.TargetId)
            && relation.RelationType == RelationType.SpecifiedBy);
        var invalidUpstream = await upstreamRelations.AnyAsync(relation =>
            relation.SourceType != KnowledgeTargetType.KnowledgeDocument
            || !PhysicalDocuments.Any(document =>
                document.Id == relation.SourceId
                && document.DocumentType == DocumentType.Requirement), cancellationToken);
        if (invalidUpstream)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }
        var upstreamRequirements = await (
            from relation in upstreamRelations
            join target in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals target.Id
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.SourceId equals document.Id
            where document.LifecycleStatus != DocumentLifecycleStatus.Archived
            orderby target.Title, target.Id, document.Title, document.Id, relation.Id
            select EdgeCandidate.Create(relation, document, TraceDirection.Incoming, target.Id))
            .Take(MaximumEdges + 1)
            .ToArrayAsync(cancellationToken);

        var lineageResult = await LoadLineage(root, cancellationToken);
        if (lineageResult.ReferenceInvalid)
        {
            return new TraceabilityQueryResult(null, TraceabilityQueryFailure.ReferenceInvalid);
        }
        var allCandidates = incoming.Concat(upstreamRequirements).Concat(lineageResult.Entries).ToArray();
        var trust = await LoadTrust(
            allCandidates.Select(candidate => candidate.Document).Append(root),
            allCandidates.Select(candidate => candidate.RelationshipId),
            cancellationToken);
        var limiter = new DisplayLimiter(root.Id);
        var guard = new TraceTraversalGuard();
        var directRequirements = AddDirectRelations(
            root,
            incoming.Where(candidate => candidate.Document.DocumentType == DocumentType.Requirement),
            limiter,
            guard,
            trust);
        var upstreamBySpecification = upstreamRequirements
            .GroupBy(candidate => candidate.ParentDocumentId)
            .ToDictionary(group => group.Key, group => group.ToArray());
        var specifications = new List<TraceUpstreamSpecificationResponse>();
        foreach (var specification in incoming.Where(candidate =>
                     candidate.Document.DocumentType == DocumentType.Specification))
        {
            if (!guard.ObservePath(
                    [(root.DocumentType, root.Id), (specification.Document.DocumentType, specification.Document.Id)],
                    [specification.RelationshipId])
                || !limiter.TryAdd(specification))
            {
                continue;
            }
            var requirements = new List<TraceDocumentRelationResponse>();
            if (upstreamBySpecification.TryGetValue(specification.Document.Id, out var rows))
            {
                foreach (var requirement in rows)
                {
                    if (!guard.ObservePath(
                            [
                                (root.DocumentType, root.Id),
                                (specification.Document.DocumentType, specification.Document.Id),
                                (requirement.Document.DocumentType, requirement.Document.Id),
                            ],
                            [specification.RelationshipId, requirement.RelationshipId])
                        || !limiter.TryAdd(requirement))
                    {
                        continue;
                    }
                    requirements.Add(ToDocumentRelation(requirement, trust));
                }
            }
            specifications.Add(new TraceUpstreamSpecificationResponse(
                ToRelationship(specification, trust),
                ToDocument(specification.Document, trust),
                requirements));
        }

        var response = new TestCaseTraceabilityResponse(
            ToDocument(root, trust),
            new TraceTestCaseCoverageResponse(TraceCoverageEligibility.Active, []),
            directRequirements,
            specifications,
            BuildLineage(lineageResult, trust),
            guard.CycleDetected || lineageResult.CycleDetected,
            limiter.IsTruncated,
            limiter.Reasons,
            Limits);
        return new TraceabilityQueryResult(response, TraceabilityQueryFailure.None);
    }

    private static IReadOnlyList<TraceDocumentRelationResponse> AddDirectRelations(
        DocumentSnapshot root,
        IEnumerable<EdgeCandidate> candidates,
        DisplayLimiter limiter,
        TraceTraversalGuard guard,
        TrustContext trust)
    {
        var responses = new List<TraceDocumentRelationResponse>();
        foreach (var candidate in candidates)
        {
            if (!guard.ObservePath(
                    [(root.DocumentType, root.Id), (candidate.Document.DocumentType, candidate.Document.Id)],
                    [candidate.RelationshipId])
                || !limiter.TryAdd(candidate))
            {
                continue;
            }
            responses.Add(ToDocumentRelation(candidate, trust));
        }
        return responses;
    }

    private async Task<LineageResult> LoadLineage(
        DocumentSnapshot root,
        CancellationToken cancellationToken)
    {
        var outgoing = dbContext.KnowledgeRelations.AsNoTracking().Where(relation =>
            relation.SourceType == KnowledgeTargetType.KnowledgeDocument
            && relation.SourceId == root.Id
            && relation.RelationType == RelationType.Supersedes);
        var incoming = dbContext.KnowledgeRelations.AsNoTracking().Where(relation =>
            relation.TargetType == KnowledgeTargetType.KnowledgeDocument
            && relation.TargetId == root.Id
            && relation.RelationType == RelationType.Supersedes);
        var invalidOutgoing = await outgoing.AnyAsync(relation =>
            relation.TargetType != KnowledgeTargetType.KnowledgeDocument
            || !PhysicalDocuments.Any(document =>
                document.Id == relation.TargetId && document.DocumentType == root.DocumentType), cancellationToken);
        var invalidIncoming = await incoming.AnyAsync(relation =>
            relation.SourceType != KnowledgeTargetType.KnowledgeDocument
            || !PhysicalDocuments.Any(document =>
                document.Id == relation.SourceId && document.DocumentType == root.DocumentType), cancellationToken);
        if (invalidOutgoing || invalidIncoming)
        {
            return new LineageResult([], 0, false, true);
        }

        var outgoingCount = await (
            from relation in outgoing
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            select relation.Id).CountAsync(cancellationToken);
        var incomingCount = await (
            from relation in incoming
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.SourceId equals document.Id
            select relation.Id).CountAsync(cancellationToken);
        var outgoingRows = await (
            from relation in outgoing
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            orderby document.Title, document.Id, relation.Id
            select EdgeCandidate.Create(relation, document, TraceDirection.Outgoing, root.Id))
            .Take(MaximumLineageEntries)
            .ToArrayAsync(cancellationToken);
        var remaining = MaximumLineageEntries - outgoingRows.Length;
        var incomingRows = remaining == 0
            ? []
            : await (
                from relation in incoming
                join document in dbContext.KnowledgeDocuments.AsNoTracking()
                    on relation.SourceId equals document.Id
                orderby document.Title, document.Id, relation.Id
                select EdgeCandidate.Create(relation, document, TraceDirection.Incoming, root.Id))
                .Take(remaining)
                .ToArrayAsync(cancellationToken);
        var activeOutgoingTargetIds = from relation in outgoing
            join document in dbContext.KnowledgeDocuments.AsNoTracking()
                on relation.TargetId equals document.Id
            select document.Id;
        var cycleDetected = await dbContext.KnowledgeRelations.AnyAsync(incomingRelation =>
                activeOutgoingTargetIds.Contains(incomingRelation.SourceId)
                && incomingRelation.RelationType == RelationType.Supersedes
                && incomingRelation.SourceType == KnowledgeTargetType.KnowledgeDocument
                && incomingRelation.TargetType == KnowledgeTargetType.KnowledgeDocument
                && incomingRelation.TargetId == root.Id,
            cancellationToken);
        return new LineageResult(
            outgoingRows.Concat(incomingRows).ToArray(),
            outgoingCount + incomingCount,
            cycleDetected,
            false);
    }

    private async Task<TrustContext> LoadTrust(
        IEnumerable<DocumentSnapshot> documents,
        IEnumerable<long> relationshipIds,
        CancellationToken cancellationToken)
    {
        var documentRows = documents.DistinctBy(document => document.Id).ToArray();
        var documentIds = documentRows.Select(document => document.Id).ToArray();
        var relationIds = relationshipIds.Distinct().ToArray();
        var aggregates = await dbContext.Evidence.AsNoTracking()
            .Where(evidence =>
                evidence.SubjectType == EvidenceSubjectType.KnowledgeDocument
                    && documentIds.Contains(evidence.SubjectId)
                || evidence.SubjectType == EvidenceSubjectType.KnowledgeRelation
                    && relationIds.Contains(evidence.SubjectId))
            .GroupBy(evidence => new { evidence.SubjectType, evidence.SubjectId })
            .Select(group => new EvidenceAggregate(
                group.Key.SubjectType,
                group.Key.SubjectId,
                group.Count(),
                group.Count(evidence => evidence.EvidenceType == EvidenceType.HumanConfirmation),
                group.Where(evidence => evidence.EvidenceType == EvidenceType.HumanConfirmation)
                    .Max(evidence => evidence.KnowledgeDocumentRevisionNumberSnapshot)))
            .ToArrayAsync(cancellationToken);
        return new TrustContext(
            documentRows.ToDictionary(document => document.Id),
            aggregates.Where(item => item.SubjectType == EvidenceSubjectType.KnowledgeDocument)
                .ToDictionary(item => item.SubjectId),
            aggregates.Where(item => item.SubjectType == EvidenceSubjectType.KnowledgeRelation)
                .ToDictionary(item => item.SubjectId));
    }

    private static TraceDocumentResponse ToDocument(DocumentSnapshot document, TrustContext trust)
    {
        trust.DocumentEvidence.TryGetValue(document.Id, out var aggregate);
        var evidenceCount = aggregate?.EvidenceCount ?? 0;
        var confirmationCount = aggregate?.HumanConfirmationCount ?? 0;
        var lastConfirmed = aggregate?.LastConfirmedRevisionNumber;
        var coverage = confirmationCount switch
        {
            0 => new TraceConfirmationCoverageResponse(
                TraceConfirmationCoverageState.NoConfirmation, null),
            _ when lastConfirmed is null => new TraceConfirmationCoverageResponse(
                TraceConfirmationCoverageState.LegacyConfirmationUnknown, null),
            _ when lastConfirmed == document.CurrentRevisionNumber => new TraceConfirmationCoverageResponse(
                TraceConfirmationCoverageState.CurrentRevisionConfirmed, lastConfirmed),
            _ when lastConfirmed < document.CurrentRevisionNumber => new TraceConfirmationCoverageResponse(
                TraceConfirmationCoverageState.ChangedSinceConfirmation, lastConfirmed),
            _ => throw new InvalidOperationException(
                $"KnowledgeDocument {document.Id} has a HumanConfirmation snapshot newer than current revision {document.CurrentRevisionNumber}."),
        };
        return new TraceDocumentResponse(
            document.Id,
            document.DocumentType,
            document.Title,
            document.LifecycleStatus,
            document.KnowledgeStatus,
            document.CurrentRevisionNumber,
            evidenceCount,
            confirmationCount,
            coverage);
    }

    private static TraceRelationshipResponse ToRelationship(EdgeCandidate candidate, TrustContext trust)
    {
        trust.RelationshipEvidence.TryGetValue(candidate.RelationshipId, out var aggregate);
        return new TraceRelationshipResponse(
            candidate.RelationshipId,
            candidate.RelationType,
            candidate.Direction,
            candidate.RelationshipKnowledgeStatus,
            aggregate?.EvidenceCount ?? 0,
            aggregate?.HumanConfirmationCount ?? 0);
    }

    private static TraceDocumentRelationResponse ToDocumentRelation(
        EdgeCandidate candidate,
        TrustContext trust) => new(
            ToRelationship(candidate, trust),
            ToDocument(candidate.Document, trust));

    private static TraceLineageResponse BuildLineage(LineageResult result, TrustContext trust) => new(
        result.Entries.Where(entry => entry.Direction == TraceDirection.Incoming)
            .Select(entry => ToDocumentRelation(entry, trust)).ToArray(),
        result.Entries.Where(entry => entry.Direction == TraceDirection.Outgoing)
            .Select(entry => ToDocumentRelation(entry, trust)).ToArray(),
        result.Total,
        result.Total > MaximumLineageEntries);

    private sealed class DisplayLimiter(long rootId)
    {
        private readonly HashSet<long> _nodes = [rootId];
        private int _edges;
        private bool _maxNodes;
        private bool _maxEdges;

        public bool IsTruncated => _maxNodes || _maxEdges;
        public IReadOnlyList<TraceTruncationReason> Reasons =>
            (_maxNodes, _maxEdges) switch
            {
                (true, true) => [TraceTruncationReason.MaxNodes, TraceTruncationReason.MaxEdges],
                (true, false) => [TraceTruncationReason.MaxNodes],
                (false, true) => [TraceTruncationReason.MaxEdges],
                _ => [],
            };

        public bool TryAdd(EdgeCandidate candidate)
        {
            if (_edges >= MaximumEdges)
            {
                _maxEdges = true;
                return false;
            }
            if (!_nodes.Contains(candidate.Document.Id) && _nodes.Count >= MaximumNodes)
            {
                _maxNodes = true;
                return false;
            }
            _edges++;
            _nodes.Add(candidate.Document.Id);
            return true;
        }
    }

    private sealed record DocumentSnapshot(
        long Id,
        DocumentType DocumentType,
        string Title,
        DocumentLifecycleStatus LifecycleStatus,
        KnowledgeStatus KnowledgeStatus,
        long CurrentRevisionNumber)
    {
        public static DocumentSnapshot From(KnowledgeDocument document) => new(
            document.Id,
            document.DocumentType,
            document.Title,
            document.LifecycleStatus,
            document.KnowledgeStatus,
            document.CurrentRevisionNumber);
    }

    private sealed record EdgeCandidate(
        long RelationshipId,
        RelationType RelationType,
        KnowledgeStatus RelationshipKnowledgeStatus,
        TraceDirection Direction,
        long ParentDocumentId,
        DocumentSnapshot Document)
    {
        public static EdgeCandidate Create(
            KnowledgeRelation relationship,
            KnowledgeDocument document,
            TraceDirection direction,
            long parentDocumentId) => new(
                relationship.Id,
                relationship.RelationType,
                relationship.KnowledgeStatus,
                direction,
                parentDocumentId,
                DocumentSnapshot.From(document));
    }

    private sealed record EvidenceAggregate(
        EvidenceSubjectType SubjectType,
        long SubjectId,
        int EvidenceCount,
        int HumanConfirmationCount,
        long? LastConfirmedRevisionNumber);

    private sealed record TrustContext(
        IReadOnlyDictionary<long, DocumentSnapshot> Documents,
        IReadOnlyDictionary<long, EvidenceAggregate> DocumentEvidence,
        IReadOnlyDictionary<long, EvidenceAggregate> RelationshipEvidence);

    private sealed record LineageResult(
        IReadOnlyList<EdgeCandidate> Entries,
        int Total,
        bool CycleDetected,
        bool ReferenceInvalid);
}
