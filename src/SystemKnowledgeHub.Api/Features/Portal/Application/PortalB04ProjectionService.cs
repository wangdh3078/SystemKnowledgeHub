using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public sealed class PortalB04ProjectionService(
    KnowledgeHubDbContext dbContext,
    PortalTargetResolver targetResolver)
{
    private readonly Dictionary<long, Task<IReadOnlyList<long>>> imageIds = [];
    private readonly Dictionary<long, Task<PortalAttachmentListContentResponse?>> attachmentLists = [];
    private readonly Dictionary<PortalTargetKey, Task<PortalTrustSummaryContentResponse?>> trustSummaries = [];
    private readonly Dictionary<PortalTargetKey, Task<PortalRelatedKnowledgeContentResponse>> relatedKnowledge = [];
    private readonly Dictionary<long, Task<PortalTraceabilityContentResponse?>> traceability = [];

    public async Task<PortalSectionContentResponse?> ProjectAsync(
        PortalPage page,
        PortalPageSection section,
        PortalResolvedTarget? target,
        IReadOnlyDictionary<PortalTargetKey, long> portalLinks,
        CancellationToken cancellationToken) => section.ProjectionKind switch
    {
        PortalPageProjectionKind.AttachmentList when target is PortalResolvedKnowledgeDocument document =>
            await Cached(attachmentLists, document.Id, () => ProjectAttachmentsAsync(document.Id, cancellationToken)),
        PortalPageProjectionKind.TrustSummary when target is not null =>
            await Cached(trustSummaries, new(target.Type, target.Id), () => ProjectTrustAsync(new(target.Type, target.Id), target.Title, cancellationToken)),
        PortalPageProjectionKind.RelatedKnowledge when section.SourceKind == PortalPageSectionSourceKind.Derived =>
            await Cached(relatedKnowledge, new(page.PrimaryTargetType, page.PrimaryTargetId), () => ProjectRelatedAsync(new(page.PrimaryTargetType, page.PrimaryTargetId), portalLinks, cancellationToken)),
        PortalPageProjectionKind.Traceability when section.SourceKind == PortalPageSectionSourceKind.Derived
            && page.PrimaryTargetType == PortalTargetType.KnowledgeDocument =>
            await Cached(traceability, page.PrimaryTargetId, () => ProjectTraceAsync(page.PrimaryTargetId, portalLinks, cancellationToken)),
        _ => null,
    };

    public async Task<IReadOnlyList<long>> GetCurrentImageAttachmentIdsAsync(
        long documentId,
        CancellationToken cancellationToken) => await Cached(
        imageIds,
        documentId,
        async () => (IReadOnlyList<long>)await (
            from document in dbContext.KnowledgeDocuments.AsNoTracking()
            join revision in dbContext.KnowledgeDocumentRevisions.AsNoTracking()
                on new { document.Id, RevisionNumber = document.CurrentRevisionNumber }
                equals new { Id = revision.KnowledgeDocumentId, revision.RevisionNumber }
            join reference in dbContext.AttachmentReferences.AsNoTracking()
                on revision.Id equals reference.KnowledgeDocumentRevisionId
            join attachment in dbContext.Attachments.AsNoTracking()
                on reference.AttachmentId equals attachment.Id
            where document.Id == documentId
                && document.LifecycleStatus == DocumentLifecycleStatus.Published
                && reference.KnowledgeDocumentId == document.Id
                && attachment.KnowledgeDocumentId == document.Id
                && attachment.Kind == AttachmentKind.Image
                && attachment.StorageState == AttachmentStorageState.Ready
            orderby attachment.Id
            select attachment.Id).ToArrayAsync(cancellationToken));

    private async Task<PortalAttachmentListContentResponse?> ProjectAttachmentsAsync(
        long documentId,
        CancellationToken cancellationToken)
    {
        var attachments = await (
            from document in dbContext.KnowledgeDocuments.AsNoTracking()
            join revision in dbContext.KnowledgeDocumentRevisions.AsNoTracking()
                on new { document.Id, RevisionNumber = document.CurrentRevisionNumber }
                equals new { Id = revision.KnowledgeDocumentId, revision.RevisionNumber }
            join reference in dbContext.AttachmentReferences.AsNoTracking()
                on revision.Id equals reference.KnowledgeDocumentRevisionId
            join attachment in dbContext.Attachments.AsNoTracking()
                on reference.AttachmentId equals attachment.Id
            where document.Id == documentId
                && document.LifecycleStatus == DocumentLifecycleStatus.Published
                && reference.KnowledgeDocumentId == document.Id
                && attachment.KnowledgeDocumentId == document.Id
                && attachment.StorageState == AttachmentStorageState.Ready
            orderby attachment.OriginalFileName, attachment.Id
            select attachment).ToArrayAsync(cancellationToken);
        if (!await dbContext.KnowledgeDocuments.AsNoTracking().AnyAsync(
                item => item.Id == documentId && item.LifecycleStatus == DocumentLifecycleStatus.Published,
                cancellationToken))
            return null;

        return new(documentId, attachments.Select(attachment =>
        {
            var previewMode = AttachmentFilePolicy.GetPreviewMode(attachment);
            return new PortalAttachmentResponse(
                attachment.Id,
                attachment.OriginalFileName,
                attachment.Kind.ToString(),
                attachment.ContentType,
                attachment.SizeBytes,
                previewMode.ToString(),
                previewMode != PreviewMode.None,
                true);
        }).ToArray());
    }

    private async Task<PortalTrustSummaryContentResponse?> ProjectTrustAsync(
        PortalTargetKey key,
        string title,
        CancellationToken cancellationToken)
    {
        var trusts = await LoadTargetTrustAsync([key], cancellationToken);
        if (!trusts.TryGetValue(key, out var trust)) return null;
        return new(
            key.Type,
            title,
            trust.Status,
            trust.EvidenceCount,
            trust.HumanConfirmationCount,
            trust.Coverage);
    }

    private async Task<PortalRelatedKnowledgeContentResponse> ProjectRelatedAsync(
        PortalTargetKey root,
        IReadOnlyDictionary<PortalTargetKey, long> portalLinks,
        CancellationToken cancellationToken)
    {
        var rootType = ToKnowledgeTargetType(root.Type);
        var relations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(item => (item.SourceType == rootType && item.SourceId == root.Id)
                || (item.TargetType == rootType && item.TargetId == root.Id))
            .OrderBy(item => item.RelationType)
            .ThenBy(item => item.Id)
            .Take(PortalLimits.MaximumRelatedResultsPerGroup * 32)
            .ToArrayAsync(cancellationToken);
        var candidates = relations.Select(relation =>
        {
            var outgoing = relation.SourceType == rootType && relation.SourceId == root.Id;
            var type = outgoing ? relation.TargetType : relation.SourceType;
            var id = outgoing ? relation.TargetId : relation.SourceId;
            return new RelatedCandidate(relation, outgoing ? "Outgoing" : "Incoming", ToPortalTargetType(type), id);
        }).Where(item => item.TargetType is not null).ToArray();
        var keys = candidates.Select(item => new PortalTargetKey(item.TargetType!.Value, item.TargetId)).Distinct().ToArray();
        var identities = await targetResolver.ResolveEligibleIdentitiesAsync(keys, cancellationToken);
        var targetTrust = await LoadTargetTrustAsync(keys, cancellationToken);
        var relationTrust = await LoadEvidenceAsync(
            EvidenceSubjectType.KnowledgeRelation,
            candidates.Select(item => item.Relation.Id),
            cancellationToken);

        var groups = candidates
            .Where(item => identities.ContainsKey(new(item.TargetType!.Value, item.TargetId)))
            .GroupBy(item => new { item.Relation.RelationType, item.Direction })
            .OrderBy(group => group.Key.RelationType)
            .ThenBy(group => group.Key.Direction, StringComparer.Ordinal)
            .Select(group => new PortalRelatedKnowledgeGroupResponse(
                group.Key.RelationType,
                RelationLabel(group.Key.RelationType),
                group.Key.Direction,
                group.Select(item =>
                    {
                        var key = new PortalTargetKey(item.TargetType!.Value, item.TargetId);
                        var trust = targetTrust.GetValueOrDefault(key) ?? TargetTrust.Empty;
                        var relationEvidence = relationTrust.GetValueOrDefault(item.Relation.Id) ?? EvidenceAggregate.Empty;
                        return new PortalRelatedKnowledgeItemResponse(
                            key.Type,
                            identities[key].Title,
                            trust.Status,
                            trust.EvidenceCount,
                            trust.HumanConfirmationCount,
                            item.Relation.KnowledgeStatus,
                            relationEvidence.EvidenceCount,
                            relationEvidence.HumanConfirmationCount,
                            portalLinks.TryGetValue(key, out var pageId) ? pageId : null);
                    })
                    .OrderBy(item => item.TargetTitle, StringComparer.Ordinal)
                    .ThenBy(item => item.TargetType)
                    .Take(PortalLimits.MaximumRelatedResultsPerGroup)
                    .ToArray()))
            .ToArray();
        return new(groups);
    }

    private async Task<PortalTraceabilityContentResponse?> ProjectTraceAsync(
        long documentId,
        IReadOnlyDictionary<PortalTargetKey, long> portalLinks,
        CancellationToken cancellationToken)
    {
        var root = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => item.Id == documentId
                && item.LifecycleStatus == DocumentLifecycleStatus.Published
                && (item.DocumentType == DocumentType.Requirement
                    || item.DocumentType == DocumentType.Specification
                    || item.DocumentType == DocumentType.TestCase))
            .Select(item => new TraceDocument(item.Id, item.DocumentType, item.Title, item.KnowledgeStatus, item.CurrentRevisionNumber))
            .SingleOrDefaultAsync(cancellationToken);
        if (root is null) return null;

        var publishedDocuments = dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => item.LifecycleStatus == DocumentLifecycleStatus.Published);
        var relations = await (
            from relation in dbContext.KnowledgeRelations.AsNoTracking()
            join source in publishedDocuments on relation.SourceId equals source.Id
            join target in publishedDocuments on relation.TargetId equals target.Id
            where relation.SourceType == KnowledgeTargetType.KnowledgeDocument
                && relation.TargetType == KnowledgeTargetType.KnowledgeDocument
                && (relation.RelationType == RelationType.SpecifiedBy || relation.RelationType == RelationType.VerifiedBy)
                && (relation.SourceId == documentId || relation.TargetId == documentId
                    || dbContext.KnowledgeRelations.Any(first =>
                        first.SourceType == KnowledgeTargetType.KnowledgeDocument
                        && first.TargetType == KnowledgeTargetType.KnowledgeDocument
                        && (first.RelationType == RelationType.SpecifiedBy || first.RelationType == RelationType.VerifiedBy)
                        && ((first.SourceId == documentId && (first.TargetId == relation.SourceId || first.TargetId == relation.TargetId))
                            || (first.TargetId == documentId && (first.SourceId == relation.SourceId || first.SourceId == relation.TargetId)))))
            orderby relation.Id
            select new TraceRelation(
                relation.Id,
                relation.RelationType,
                relation.KnowledgeStatus,
                new(source.Id, source.DocumentType, source.Title, source.KnowledgeStatus, source.CurrentRevisionNumber),
                new(target.Id, target.DocumentType, target.Title, target.KnowledgeStatus, target.CurrentRevisionNumber)))
            .Take(301)
            .ToArrayAsync(cancellationToken);
        var truncated = relations.Length > 300;
        relations = relations.Take(300).ToArray();

        var nodes = relations.SelectMany(item => new[] { item.Source, item.Target }).Append(root)
            .DistinctBy(item => item.Id).OrderBy(item => item.Title).ThenBy(item => item.Id).Take(200).ToArray();
        if (relations.SelectMany(item => new[] { item.Source.Id, item.Target.Id }).Append(root.Id).Distinct().Count() > 200)
            truncated = true;
        var allowedIds = nodes.Select(item => item.Id).ToHashSet();
        relations = relations.Where(item => allowedIds.Contains(item.Source.Id) && allowedIds.Contains(item.Target.Id)).ToArray();
        var documentTrust = await LoadTargetTrustAsync(
            nodes.Select(item => new PortalTargetKey(PortalTargetType.KnowledgeDocument, item.Id)), cancellationToken);
        var relationTrust = await LoadEvidenceAsync(EvidenceSubjectType.KnowledgeRelation, relations.Select(item => item.Id), cancellationToken);

        PortalTraceNodeResponse Node(TraceDocument document)
        {
            var key = new PortalTargetKey(PortalTargetType.KnowledgeDocument, document.Id);
            var trust = documentTrust.GetValueOrDefault(key) ?? TargetTrust.Empty;
            return new(document.DocumentType, document.Title, document.KnowledgeStatus,
                trust.EvidenceCount, trust.HumanConfirmationCount, trust.Coverage ?? "NoConfirmation",
                portalLinks.TryGetValue(key, out var pageId) ? pageId : null);
        }
        PortalTraceEdgeResponse Edge(TraceRelation relation)
        {
            var evidence = relationTrust.GetValueOrDefault(relation.Id) ?? EvidenceAggregate.Empty;
            return new(relation.RelationType, relation.KnowledgeStatus, evidence.EvidenceCount, evidence.HumanConfirmationCount);
        }

        var paths = new List<PortalTracePathResponse>();
        if (root.DocumentType == DocumentType.Requirement)
        {
            foreach (var direct in relations.Where(item => item.Source.Id == root.Id && item.RelationType == RelationType.VerifiedBy && item.Target.DocumentType == DocumentType.TestCase))
                paths.Add(new("DirectTest", [Node(root), Node(direct.Target)], [Edge(direct)]));
            foreach (var specified in relations.Where(item => item.Source.Id == root.Id && item.RelationType == RelationType.SpecifiedBy && item.Target.DocumentType == DocumentType.Specification))
            {
                var tests = relations.Where(item => item.Source.Id == specified.Target.Id && item.RelationType == RelationType.VerifiedBy && item.Target.DocumentType == DocumentType.TestCase).ToArray();
                if (tests.Length == 0) paths.Add(new("Specification", [Node(root), Node(specified.Target)], [Edge(specified)]));
                foreach (var test in tests) paths.Add(new("SpecificationTest", [Node(root), Node(specified.Target), Node(test.Target)], [Edge(specified), Edge(test)]));
            }
        }
        else if (root.DocumentType == DocumentType.Specification)
        {
            foreach (var test in relations.Where(item => item.Source.Id == root.Id && item.RelationType == RelationType.VerifiedBy && item.Target.DocumentType == DocumentType.TestCase))
                paths.Add(new("SpecificationTest", [Node(root), Node(test.Target)], [Edge(test)]));
            foreach (var requirement in relations.Where(item => item.Target.Id == root.Id && item.RelationType == RelationType.SpecifiedBy && item.Source.DocumentType == DocumentType.Requirement))
                paths.Add(new("UpstreamRequirement", [Node(requirement.Source), Node(root)], [Edge(requirement)]));
        }
        else
        {
            foreach (var incoming in relations.Where(item => item.Target.Id == root.Id && item.RelationType == RelationType.VerifiedBy))
            {
                if (incoming.Source.DocumentType == DocumentType.Requirement)
                    paths.Add(new("DirectRequirement", [Node(incoming.Source), Node(root)], [Edge(incoming)]));
                if (incoming.Source.DocumentType == DocumentType.Specification)
                {
                    var upstream = relations.Where(item => item.Target.Id == incoming.Source.Id && item.RelationType == RelationType.SpecifiedBy && item.Source.DocumentType == DocumentType.Requirement).ToArray();
                    if (upstream.Length == 0) paths.Add(new("UpstreamSpecification", [Node(incoming.Source), Node(root)], [Edge(incoming)]));
                    foreach (var requirement in upstream)
                        paths.Add(new("RequirementSpecification", [Node(requirement.Source), Node(incoming.Source), Node(root)], [Edge(requirement), Edge(incoming)]));
                }
            }
        }

        var missing = new List<string>();
        if (root.DocumentType == DocumentType.Requirement)
        {
            if (!paths.Any(item => item.Kind is "Specification" or "SpecificationTest")) missing.Add("MissingSpecification");
            if (!paths.Any(item => item.Kind is "DirectTest" or "SpecificationTest")) missing.Add("MissingTestDefinition");
        }
        else if (root.DocumentType == DocumentType.Specification && !paths.Any(item => item.Kind == "SpecificationTest"))
            missing.Add("MissingTestDefinition");
        var cycle = relations.Any(first => relations.Any(second => first.Source.Id == second.Target.Id && first.Target.Id == second.Source.Id));
        return new(Node(root), paths, missing, cycle, truncated, new(2, 200, 300));
    }

    private async Task<IReadOnlyDictionary<PortalTargetKey, TargetTrust>> LoadTargetTrustAsync(
        IEnumerable<PortalTargetKey> requestedKeys,
        CancellationToken cancellationToken)
    {
        var keys = requestedKeys.Distinct().ToArray();
        var result = new Dictionary<PortalTargetKey, TargetTrust>();
        async Task AddNonDocuments(PortalTargetType type, IQueryable<(long Id, KnowledgeStatus Status)> query)
        {
            foreach (var row in await query.ToArrayAsync(cancellationToken)) result[new(type, row.Id)] = new(row.Status, 0, 0, null);
        }
        var systemIds = keys.Where(item => item.Type == PortalTargetType.System).Select(item => item.Id).ToArray();
        await AddNonDocuments(PortalTargetType.System, dbContext.Systems.AsNoTracking().Where(item => systemIds.Contains(item.Id)).Select(item => new ValueTuple<long, KnowledgeStatus>(item.Id, item.KnowledgeStatus)));
        var functionIds = keys.Where(item => item.Type == PortalTargetType.BusinessFunction).Select(item => item.Id).ToArray();
        await AddNonDocuments(PortalTargetType.BusinessFunction, dbContext.BusinessFunctions.AsNoTracking().Where(item => functionIds.Contains(item.Id)).Select(item => new ValueTuple<long, KnowledgeStatus>(item.Id, item.KnowledgeStatus)));
        var objectIds = keys.Where(item => item.Type == PortalTargetType.DatabaseObject).Select(item => item.Id).ToArray();
        await AddNonDocuments(PortalTargetType.DatabaseObject, dbContext.DatabaseObjects.AsNoTracking().Where(item => objectIds.Contains(item.Id)).Select(item => new ValueTuple<long, KnowledgeStatus>(item.Id, item.KnowledgeStatus)));
        var integrationIds = keys.Where(item => item.Type == PortalTargetType.Integration).Select(item => item.Id).ToArray();
        await AddNonDocuments(PortalTargetType.Integration, dbContext.Integrations.AsNoTracking().Where(item => integrationIds.Contains(item.Id)).Select(item => new ValueTuple<long, KnowledgeStatus>(item.Id, item.KnowledgeStatus)));
        var documentIds = keys.Where(item => item.Type == PortalTargetType.KnowledgeDocument).Select(item => item.Id).ToArray();
        var documents = await dbContext.KnowledgeDocuments.AsNoTracking().Where(item => documentIds.Contains(item.Id) && item.LifecycleStatus == DocumentLifecycleStatus.Published)
            .Select(item => new { item.Id, item.KnowledgeStatus, item.CurrentRevisionNumber }).ToArrayAsync(cancellationToken);
        foreach (var document in documents) result[new(PortalTargetType.KnowledgeDocument, document.Id)] = new(document.KnowledgeStatus, 0, 0, "NoConfirmation", document.CurrentRevisionNumber);

        foreach (var type in Enum.GetValues<PortalTargetType>())
        {
            var subjectType = ToEvidenceSubjectType(type);
            var ids = keys.Where(item => item.Type == type).Select(item => item.Id).ToArray();
            var aggregates = await LoadEvidenceAsync(subjectType, ids, cancellationToken);
            foreach (var pair in aggregates)
            {
                var key = new PortalTargetKey(type, pair.Key);
                if (!result.TryGetValue(key, out var trust)) continue;
                var coverage = trust.CurrentRevision is null ? null : pair.Value.HumanConfirmationCount switch
                {
                    0 => "NoConfirmation",
                    _ when pair.Value.LastConfirmedRevision is null => "LegacyConfirmationUnknown",
                    _ when pair.Value.LastConfirmedRevision == trust.CurrentRevision => "CurrentRevisionConfirmed",
                    _ => "ChangedSinceConfirmation",
                };
                result[key] = trust with { EvidenceCount = pair.Value.EvidenceCount, HumanConfirmationCount = pair.Value.HumanConfirmationCount, Coverage = coverage };
            }
        }
        return result;
    }

    private async Task<IReadOnlyDictionary<long, EvidenceAggregate>> LoadEvidenceAsync(
        EvidenceSubjectType subjectType,
        IEnumerable<long> subjectIds,
        CancellationToken cancellationToken)
    {
        var ids = subjectIds.Distinct().ToArray();
        return await dbContext.Evidence.AsNoTracking()
            .Where(item => item.SubjectType == subjectType && ids.Contains(item.SubjectId))
            .GroupBy(item => item.SubjectId)
            .Select(group => new EvidenceAggregate(group.Key, group.Count(),
                group.Count(item => item.EvidenceType == EvidenceType.HumanConfirmation),
                group.Where(item => item.EvidenceType == EvidenceType.HumanConfirmation).Max(item => item.KnowledgeDocumentRevisionNumberSnapshot)))
            .ToDictionaryAsync(item => item.SubjectId, cancellationToken);
    }

    private static KnowledgeTargetType ToKnowledgeTargetType(PortalTargetType type) => type switch
    {
        PortalTargetType.System => KnowledgeTargetType.System,
        PortalTargetType.BusinessFunction => KnowledgeTargetType.BusinessFunction,
        PortalTargetType.DatabaseObject => KnowledgeTargetType.DatabaseObject,
        PortalTargetType.KnowledgeDocument => KnowledgeTargetType.KnowledgeDocument,
        PortalTargetType.Integration => KnowledgeTargetType.Integration,
        _ => throw new InvalidOperationException("Unsupported Portal target type."),
    };
    private static PortalTargetType? ToPortalTargetType(KnowledgeTargetType type) => type switch
    {
        KnowledgeTargetType.System => PortalTargetType.System,
        KnowledgeTargetType.BusinessFunction => PortalTargetType.BusinessFunction,
        KnowledgeTargetType.DatabaseObject => PortalTargetType.DatabaseObject,
        KnowledgeTargetType.KnowledgeDocument => PortalTargetType.KnowledgeDocument,
        KnowledgeTargetType.Integration => PortalTargetType.Integration,
        _ => null,
    };
    private static EvidenceSubjectType ToEvidenceSubjectType(PortalTargetType type) => type switch
    {
        PortalTargetType.System => EvidenceSubjectType.System,
        PortalTargetType.BusinessFunction => EvidenceSubjectType.BusinessFunction,
        PortalTargetType.DatabaseObject => EvidenceSubjectType.DatabaseObject,
        PortalTargetType.KnowledgeDocument => EvidenceSubjectType.KnowledgeDocument,
        PortalTargetType.Integration => EvidenceSubjectType.Integration,
        _ => throw new InvalidOperationException("Unsupported Portal target type."),
    };
    private static string RelationLabel(RelationType type) => type switch
    {
        RelationType.Calls => "调用",
        RelationType.Reads => "读取",
        RelationType.Writes => "写入",
        RelationType.UsesField => "使用字段",
        RelationType.AppliesRule => "应用规则",
        RelationType.PublishesVia => "通过发布",
        RelationType.ConsumesVia => "通过消费",
        RelationType.UsesIntegration => "使用集成",
        RelationType.DependsOn => "依赖",
        RelationType.Documents => "文档说明",
        RelationType.References => "引用",
        RelationType.AppliesTo => "适用于",
        RelationType.SpecifiedBy => "由规格定义",
        RelationType.VerifiedBy => "由测试验证",
        RelationType.Supersedes => "替代",
        _ => type.ToString(),
    };

    private static Task<TValue> Cached<TKey, TValue>(
        IDictionary<TKey, Task<TValue>> cache,
        TKey key,
        Func<Task<TValue>> factory) where TKey : notnull
    {
        if (cache.TryGetValue(key, out var existing)) return existing;
        var created = factory();
        cache[key] = created;
        return created;
    }

    private sealed record RelatedCandidate(KnowledgeRelation Relation, string Direction, PortalTargetType? TargetType, long TargetId);
    private sealed record TargetTrust(KnowledgeStatus Status, int EvidenceCount, int HumanConfirmationCount, string? Coverage, long? CurrentRevision = null)
    {
        public static TargetTrust Empty { get; } = new(KnowledgeStatus.Unknown, 0, 0, null);
    }
    private sealed record EvidenceAggregate(long SubjectId, int EvidenceCount, int HumanConfirmationCount, long? LastConfirmedRevision)
    {
        public static EvidenceAggregate Empty { get; } = new(0, 0, 0, null);
    }
    private sealed record TraceDocument(long Id, DocumentType DocumentType, string Title, KnowledgeStatus KnowledgeStatus, long CurrentRevision);
    private sealed record TraceRelation(long Id, RelationType RelationType, KnowledgeStatus KnowledgeStatus, TraceDocument Source, TraceDocument Target);
}
