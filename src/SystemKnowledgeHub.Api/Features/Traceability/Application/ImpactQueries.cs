using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Traceability.Application.Models;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;

namespace SystemKnowledgeHub.Api.Features.Traceability.Application;

/// <summary>
/// Builds the fixed, path-explained Impact Context projection from current canonical truth.
/// </summary>
public sealed class ImpactQueries(
    KnowledgeHubDbContext dbContext,
    HistoricalTargetResolver historicalTargetResolver)
{
    public const int DefaultPageSize = 20;
    public const int MaximumPageSize = 100;
    public const int MaximumDepth = 2;

    public async Task<ImpactQueryResult> Get(
        long id,
        long page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var root = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(document => document.Id == id)
            .Select(document => new RootSnapshot(document.Id, document.DocumentType))
            .SingleOrDefaultAsync(cancellationToken);
        if (root is null)
        {
            return new ImpactQueryResult(null, ImpactQueryFailure.NotFound);
        }
        if (root.DocumentType is not (
            DocumentType.Requirement or DocumentType.Specification or DocumentType.TestCase))
        {
            return new ImpactQueryResult(null, ImpactQueryFailure.UnsupportedDocumentType);
        }

        var pathResult = root.DocumentType switch
        {
            DocumentType.Requirement => await LoadRequirementPaths(root.Id, cancellationToken),
            DocumentType.Specification => await LoadSpecificationPaths(root.Id, cancellationToken),
            DocumentType.TestCase => await LoadTestCasePaths(root.Id, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported Impact root type."),
        };
        if (pathResult.ReferenceInvalid)
        {
            return new ImpactQueryResult(null, ImpactQueryFailure.ReferenceInvalid);
        }

        var candidates = pathResult.Paths
            .DistinctBy(path => path.ExactPathKey)
            .ToArray();
        var targets = await LoadTargetMetadata(candidates, cancellationToken);
        var currentCandidates = new List<ImpactPathCandidate>();
        foreach (var candidate in candidates)
        {
            if (targets.ContainsKey((candidate.TargetType, candidate.TargetId)))
            {
                currentCandidates.Add(candidate);
                continue;
            }
            var historical = await historicalTargetResolver.Resolve(
                ToKnowledgeTargetType(candidate.TargetType),
                candidate.TargetId,
                cancellationToken);
            if (historical is null)
            {
                return new ImpactQueryResult(null, ImpactQueryFailure.ReferenceInvalid);
            }
        }

        var ordered = currentCandidates
            .Select(candidate => ToResponse(candidate, targets[(candidate.TargetType, candidate.TargetId)]))
            .OrderBy(item => PathCategoryRank(item.PathKind))
            .ThenBy(item => TargetTypeRank(item.Target.Type))
            .ThenBy(item => item.Target.Title, StringComparer.OrdinalIgnoreCase)
            .ThenBy(item => item.Target.Id)
            .ThenBy(item => item.PathKind)
            .ThenBy(item => string.Join(',', item.Path.Select(segment => segment.RelationshipId)), StringComparer.Ordinal)
            .ToArray();
        var offset = (page - 1) * pageSize;
        var items = offset >= ordered.Length
            ? []
            : ordered.Skip((int)offset).Take(pageSize).ToArray();
        return new ImpactQueryResult(
            new ImpactResponse(items, page, pageSize, ordered.Length, MaximumDepth),
            ImpactQueryFailure.None);
    }

    private async Task<PathLoadResult> LoadRequirementPaths(
        long rootId,
        CancellationToken cancellationToken)
    {
        var rootRelations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(relation => relation.SourceType == KnowledgeTargetType.KnowledgeDocument
                && relation.SourceId == rootId
                && (relation.RelationType == RelationType.AppliesTo
                    || relation.RelationType == RelationType.Documents
                    || relation.RelationType == RelationType.SpecifiedBy))
            .ToArrayAsync(cancellationToken);
        var paths = new List<ImpactPathCandidate>();
        var specificationRelations = new List<KnowledgeRelation>();
        foreach (var relation in rootRelations)
        {
            switch (relation.RelationType)
            {
                case RelationType.AppliesTo when TryTargetType(relation.TargetType, out var appliesTarget)
                    && appliesTarget is ImpactTargetType.System or ImpactTargetType.BusinessFunction:
                    paths.Add(DirectPath(
                        relation,
                        appliesTarget,
                        ImpactPathKind.DirectAppliesTo,
                        ImpactMeaning.ExplicitRequirementScope));
                    break;
                case RelationType.Documents when TryTargetType(relation.TargetType, out var documentsTarget):
                    paths.Add(DirectPath(
                        relation,
                        documentsTarget,
                        ImpactPathKind.DirectDocuments,
                        ImpactMeaning.DocumentedByRequirement));
                    break;
                case RelationType.SpecifiedBy when relation.TargetType == KnowledgeTargetType.KnowledgeDocument:
                    specificationRelations.Add(relation);
                    break;
                default:
                    return PathLoadResult.Invalid;
            }
        }

        var specificationIds = specificationRelations.Select(relation => relation.TargetId).Distinct().ToArray();
        var specificationSelection = await SelectCurrentDocuments(
            specificationIds, [DocumentType.Specification], cancellationToken);
        if (specificationSelection.ReferenceInvalid)
        {
            return PathLoadResult.Invalid;
        }
        specificationIds = specificationSelection.ActiveIds;
        specificationRelations = specificationRelations
            .Where(relation => specificationIds.Contains(relation.TargetId))
            .ToList();
        if (specificationIds.Length == 0)
        {
            return new PathLoadResult(paths, false);
        }

        var documentsRelations = await LoadOutgoingStructuredRelations(
            specificationIds,
            RelationType.Documents,
            cancellationToken);
        var definingRelations = specificationRelations.ToDictionary(relation => relation.TargetId);
        foreach (var relation in documentsRelations)
        {
            if (!TryTargetType(relation.TargetType, out var targetType)
                || !definingRelations.TryGetValue(relation.SourceId, out var definingRelation))
            {
                return PathLoadResult.Invalid;
            }
            paths.Add(new ImpactPathCandidate(
                targetType,
                relation.TargetId,
                ImpactPathKind.ViaSpecificationDocuments,
                ImpactMeaning.DocumentedBySpecification,
                [
                    Segment(definingRelation, TraceDirection.Outgoing),
                    Segment(relation, TraceDirection.Outgoing),
                ]));
        }
        return new PathLoadResult(paths, false);
    }

    private async Task<PathLoadResult> LoadSpecificationPaths(
        long rootId,
        CancellationToken cancellationToken)
    {
        var paths = new List<ImpactPathCandidate>();
        var directDocuments = await LoadOutgoingStructuredRelations(
            [rootId],
            RelationType.Documents,
            cancellationToken);
        foreach (var relation in directDocuments)
        {
            if (!TryTargetType(relation.TargetType, out var targetType))
            {
                return PathLoadResult.Invalid;
            }
            paths.Add(DirectPath(
                relation,
                targetType,
                ImpactPathKind.DirectDocuments,
                ImpactMeaning.DocumentedBySpecification));
        }

        var definingRelations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(relation => relation.TargetType == KnowledgeTargetType.KnowledgeDocument
                && relation.TargetId == rootId
                && relation.RelationType == RelationType.SpecifiedBy)
            .ToArrayAsync(cancellationToken);
        if (definingRelations.Any(relation => relation.SourceType != KnowledgeTargetType.KnowledgeDocument))
        {
            return PathLoadResult.Invalid;
        }
        var requirementIds = definingRelations.Select(relation => relation.SourceId).Distinct().ToArray();
        var requirementSelection = await SelectCurrentDocuments(
            requirementIds, [DocumentType.Requirement], cancellationToken);
        if (requirementSelection.ReferenceInvalid)
        {
            return PathLoadResult.Invalid;
        }
        requirementIds = requirementSelection.ActiveIds;
        definingRelations = definingRelations
            .Where(relation => requirementIds.Contains(relation.SourceId))
            .ToArray();
        var upstreamRelations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(relation => relation.SourceType == KnowledgeTargetType.KnowledgeDocument
                && requirementIds.Contains(relation.SourceId)
                && (relation.RelationType == RelationType.AppliesTo
                    || relation.RelationType == RelationType.Documents))
            .ToArrayAsync(cancellationToken);
        var requirementRelations = definingRelations.ToDictionary(relation => relation.SourceId);
        foreach (var relation in upstreamRelations)
        {
            if (!TryTargetType(relation.TargetType, out var targetType)
                || !requirementRelations.TryGetValue(relation.SourceId, out var definingRelation)
                || relation.RelationType == RelationType.AppliesTo
                    && targetType is not (ImpactTargetType.System or ImpactTargetType.BusinessFunction))
            {
                return PathLoadResult.Invalid;
            }
            var appliesTo = relation.RelationType == RelationType.AppliesTo;
            paths.Add(new ImpactPathCandidate(
                targetType,
                relation.TargetId,
                appliesTo ? ImpactPathKind.ViaRequirementAppliesTo : ImpactPathKind.ViaRequirementDocuments,
                appliesTo ? ImpactMeaning.UpstreamRequirementScope : ImpactMeaning.UpstreamRequirementDocumentedContext,
                [
                    Segment(definingRelation, TraceDirection.Incoming),
                    Segment(relation, TraceDirection.Outgoing),
                ]));
        }
        return new PathLoadResult(paths, false);
    }

    private async Task<PathLoadResult> LoadTestCasePaths(
        long rootId,
        CancellationToken cancellationToken)
    {
        var paths = new List<ImpactPathCandidate>();
        var directDocuments = await LoadOutgoingStructuredRelations(
            [rootId],
            RelationType.Documents,
            cancellationToken);
        foreach (var relation in directDocuments)
        {
            if (!TryTargetType(relation.TargetType, out var targetType))
            {
                return PathLoadResult.Invalid;
            }
            paths.Add(DirectPath(
                relation,
                targetType,
                ImpactPathKind.DirectDocuments,
                ImpactMeaning.DocumentedByTestCase));
        }

        var verifiedRelations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(relation => relation.TargetType == KnowledgeTargetType.KnowledgeDocument
                && relation.TargetId == rootId
                && relation.RelationType == RelationType.VerifiedBy)
            .ToArrayAsync(cancellationToken);
        if (verifiedRelations.Any(relation => relation.SourceType != KnowledgeTargetType.KnowledgeDocument))
        {
            return PathLoadResult.Invalid;
        }
        var sourceIds = verifiedRelations.Select(relation => relation.SourceId).Distinct().ToArray();
        var sourceSelection = await SelectCurrentDocuments(
            sourceIds, [DocumentType.Requirement, DocumentType.Specification], cancellationToken);
        if (sourceSelection.ReferenceInvalid)
        {
            return PathLoadResult.Invalid;
        }
        sourceIds = sourceSelection.ActiveIds;
        verifiedRelations = verifiedRelations.Where(relation => sourceIds.Contains(relation.SourceId)).ToArray();
        var sourceTypes = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(document => sourceIds.Contains(document.Id))
            .Select(document => new { document.Id, document.DocumentType })
            .ToArrayAsync(cancellationToken);
        var typeById = sourceTypes.ToDictionary(document => document.Id, document => document.DocumentType);
        var selectedRelations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(relation => relation.SourceType == KnowledgeTargetType.KnowledgeDocument
                && sourceIds.Contains(relation.SourceId)
                && (relation.RelationType == RelationType.AppliesTo
                    || relation.RelationType == RelationType.Documents))
            .ToArrayAsync(cancellationToken);
        var verifiedBySource = verifiedRelations.ToDictionary(relation => relation.SourceId);
        foreach (var relation in selectedRelations)
        {
            if (!typeById.TryGetValue(relation.SourceId, out var sourceType)
                || !verifiedBySource.TryGetValue(relation.SourceId, out var verifiedRelation))
            {
                return PathLoadResult.Invalid;
            }
            var allowed = sourceType switch
            {
                DocumentType.Requirement => relation.RelationType == RelationType.AppliesTo,
                DocumentType.Specification => relation.RelationType == RelationType.Documents,
                _ => false,
            };
            if (!allowed)
            {
                continue;
            }
            if (!TryTargetType(relation.TargetType, out var targetType)
                || sourceType == DocumentType.Requirement
                    && targetType is not (ImpactTargetType.System or ImpactTargetType.BusinessFunction))
            {
                return PathLoadResult.Invalid;
            }
            paths.Add(new ImpactPathCandidate(
                targetType,
                relation.TargetId,
                sourceType == DocumentType.Requirement
                    ? ImpactPathKind.ViaVerifiedRequirementAppliesTo
                    : ImpactPathKind.ViaVerifiedSpecificationDocuments,
                sourceType == DocumentType.Requirement
                    ? ImpactMeaning.VerifiedRequirementScope
                    : ImpactMeaning.VerifiedSpecificationDocumentedContext,
                [
                    Segment(verifiedRelation, TraceDirection.Incoming),
                    Segment(relation, TraceDirection.Outgoing),
                ]));
        }
        return new PathLoadResult(paths, false);
    }

    private async Task<KnowledgeRelation[]> LoadOutgoingStructuredRelations(
        IReadOnlyList<long> sourceIds,
        RelationType relationType,
        CancellationToken cancellationToken)
    {
        if (sourceIds.Count == 0)
        {
            return [];
        }
        return await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(relation => relation.SourceType == KnowledgeTargetType.KnowledgeDocument
                && sourceIds.Contains(relation.SourceId)
                && relation.RelationType == relationType)
            .ToArrayAsync(cancellationToken);
    }

    private async Task<DocumentSelection> SelectCurrentDocuments(
        IReadOnlyList<long> ids,
        IReadOnlyList<DocumentType> expectedTypes,
        CancellationToken cancellationToken)
    {
        if (ids.Count == 0)
        {
            return new DocumentSelection([], false);
        }
        var physicalRows = await dbContext.KnowledgeDocuments.IgnoreQueryFilters().AsNoTracking()
            .Where(document => ids.Contains(document.Id))
            .Select(document => new { document.Id, document.DocumentType })
            .ToArrayAsync(cancellationToken);
        if (physicalRows.Length != ids.Count
            || physicalRows.Any(document => !expectedTypes.Contains(document.DocumentType)))
        {
            return new DocumentSelection([], true);
        }
        var activeIds = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(document => ids.Contains(document.Id))
            .Select(document => document.Id)
            .ToArrayAsync(cancellationToken);
        return new DocumentSelection(activeIds, false);
    }

    private static KnowledgeTargetType ToKnowledgeTargetType(ImpactTargetType type) => type switch
    {
        ImpactTargetType.System => KnowledgeTargetType.System,
        ImpactTargetType.BusinessFunction => KnowledgeTargetType.BusinessFunction,
        ImpactTargetType.DatabaseObject => KnowledgeTargetType.DatabaseObject,
        ImpactTargetType.BusinessRule => KnowledgeTargetType.BusinessRule,
        ImpactTargetType.Integration => KnowledgeTargetType.Integration,
        _ => throw new InvalidOperationException($"Unsupported impact target type {type}."),
    };

    private async Task<Dictionary<(ImpactTargetType Type, long Id), TargetMetadata>> LoadTargetMetadata(
        IReadOnlyList<ImpactPathCandidate> candidates,
        CancellationToken cancellationToken)
    {
        var targets = new Dictionary<(ImpactTargetType Type, long Id), TargetMetadata>();
        var systemIds = TargetIds(candidates, ImpactTargetType.System);
        if (systemIds.Length > 0)
        {
            var rows = await dbContext.Systems.AsNoTracking()
                .Where(item => systemIds.Contains(item.Id))
                .Select(item => new { item.Id, item.Name })
                .ToArrayAsync(cancellationToken);
            foreach (var row in rows)
            {
                targets[(ImpactTargetType.System, row.Id)] = new TargetMetadata(
                    row.Name,
                    [new ImpactSystemContextResponse(row.Id, row.Name)]);
            }
        }

        var functionIds = TargetIds(candidates, ImpactTargetType.BusinessFunction);
        if (functionIds.Length > 0)
        {
            var rows = await dbContext.BusinessFunctions.AsNoTracking()
                .Where(item => functionIds.Contains(item.Id))
                .Select(item => new { item.Id, item.Name, SystemId = item.System.Id, SystemName = item.System.Name })
                .ToArrayAsync(cancellationToken);
            foreach (var row in rows)
            {
                targets[(ImpactTargetType.BusinessFunction, row.Id)] = new TargetMetadata(
                    row.Name,
                    [new ImpactSystemContextResponse(row.SystemId, row.SystemName)]);
            }
        }

        var databaseObjectIds = TargetIds(candidates, ImpactTargetType.DatabaseObject);
        if (databaseObjectIds.Length > 0)
        {
            var rows = await dbContext.DatabaseObjects.AsNoTracking()
                .Where(item => databaseObjectIds.Contains(item.Id))
                .Select(item => new
                {
                    item.Id,
                    Title = item.SchemaName + "." + item.ObjectName,
                    SystemId = item.DatabaseSource.System.Id,
                    SystemName = item.DatabaseSource.System.Name,
                })
                .ToArrayAsync(cancellationToken);
            foreach (var row in rows)
            {
                targets[(ImpactTargetType.DatabaseObject, row.Id)] = new TargetMetadata(
                    row.Title,
                    [new ImpactSystemContextResponse(row.SystemId, row.SystemName)]);
            }
        }

        var businessRuleIds = TargetIds(candidates, ImpactTargetType.BusinessRule);
        if (businessRuleIds.Length > 0)
        {
            var rows = await dbContext.BusinessRules.AsNoTracking()
                .Where(item => businessRuleIds.Contains(item.Id))
                .Select(item => new { item.Id, item.Name, SystemId = item.System.Id, SystemName = item.System.Name })
                .ToArrayAsync(cancellationToken);
            foreach (var row in rows)
            {
                targets[(ImpactTargetType.BusinessRule, row.Id)] = new TargetMetadata(
                    row.Name,
                    [new ImpactSystemContextResponse(row.SystemId, row.SystemName)]);
            }
        }

        var integrationIds = TargetIds(candidates, ImpactTargetType.Integration);
        if (integrationIds.Length > 0)
        {
            var rows = await dbContext.Integrations.AsNoTracking()
                .Include(item => item.SourceSystem)
                .Include(item => item.TargetSystem)
                .Where(item => integrationIds.Contains(item.Id))
                .ToArrayAsync(cancellationToken);
            foreach (var row in rows)
            {
                targets[(ImpactTargetType.Integration, row.Id)] = new TargetMetadata(
                    row.Name,
                    IntegrationSystems(row));
            }
        }
        return targets;
    }

    private static long[] TargetIds(
        IEnumerable<ImpactPathCandidate> candidates,
        ImpactTargetType targetType) => candidates
        .Where(candidate => candidate.TargetType == targetType)
        .Select(candidate => candidate.TargetId)
        .Distinct()
        .ToArray();

    private static ImpactSystemContextResponse[] IntegrationSystems(Integration integration) =>
        new[] { integration.SourceSystem, integration.TargetSystem }
            .Where(system => system is not null)
            .Select(system => new ImpactSystemContextResponse(system!.Id, system.Name))
            .DistinctBy(system => system.Id)
            .OrderBy(system => system.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(system => system.Id)
            .ToArray();

    private static ImpactPathCandidate DirectPath(
        KnowledgeRelation relation,
        ImpactTargetType targetType,
        ImpactPathKind pathKind,
        ImpactMeaning meaning) => new(
            targetType,
            relation.TargetId,
            pathKind,
            meaning,
            [Segment(relation, TraceDirection.Outgoing)]);

    private static ImpactPathSegmentResponse Segment(
        KnowledgeRelation relation,
        TraceDirection direction) => new(
            relation.Id,
            relation.RelationType,
            direction);

    private static bool TryTargetType(
        KnowledgeTargetType targetType,
        out ImpactTargetType impactTargetType)
    {
        switch (targetType)
        {
            case KnowledgeTargetType.System:
                impactTargetType = ImpactTargetType.System;
                return true;
            case KnowledgeTargetType.BusinessFunction:
                impactTargetType = ImpactTargetType.BusinessFunction;
                return true;
            case KnowledgeTargetType.DatabaseObject:
                impactTargetType = ImpactTargetType.DatabaseObject;
                return true;
            case KnowledgeTargetType.BusinessRule:
                impactTargetType = ImpactTargetType.BusinessRule;
                return true;
            case KnowledgeTargetType.Integration:
                impactTargetType = ImpactTargetType.Integration;
                return true;
            default:
                impactTargetType = default;
                return false;
        }
    }

    private static ImpactItemResponse ToResponse(
        ImpactPathCandidate candidate,
        TargetMetadata target) => new(
            candidate.PathKind,
            candidate.Meaning,
            new ImpactTargetResponse(
                candidate.TargetType,
                candidate.TargetId,
                target.Title,
                target.SystemContext),
            candidate.Path);

    private static int PathCategoryRank(ImpactPathKind pathKind) => pathKind switch
    {
        ImpactPathKind.DirectAppliesTo => 0,
        ImpactPathKind.DirectDocuments => 1,
        _ => 2,
    };

    private static int TargetTypeRank(ImpactTargetType targetType) => targetType switch
    {
        ImpactTargetType.System => 0,
        ImpactTargetType.BusinessFunction => 1,
        ImpactTargetType.DatabaseObject => 2,
        ImpactTargetType.BusinessRule => 3,
        ImpactTargetType.Integration => 4,
        _ => throw new InvalidOperationException("Unsupported Impact target type."),
    };

    private sealed record RootSnapshot(long Id, DocumentType DocumentType);

    private sealed record ImpactPathCandidate(
        ImpactTargetType TargetType,
        long TargetId,
        ImpactPathKind PathKind,
        ImpactMeaning Meaning,
        IReadOnlyList<ImpactPathSegmentResponse> Path)
    {
        public string ExactPathKey => string.Join(
            '|',
            TargetType,
            TargetId,
            PathKind,
            string.Join(',', Path.Select(segment => segment.RelationshipId)));
    }

    private sealed record TargetMetadata(
        string Title,
        IReadOnlyList<ImpactSystemContextResponse> SystemContext);

    private sealed record DocumentSelection(long[] ActiveIds, bool ReferenceInvalid);

    private sealed record PathLoadResult(
        IReadOnlyList<ImpactPathCandidate> Paths,
        bool ReferenceInvalid)
    {
        public static PathLoadResult Invalid { get; } = new([], true);
    }
}
