using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public sealed class PortalQueries(
    KnowledgeHubDbContext dbContext,
    PortalTargetResolver targetResolver,
    ILogger<PortalQueries> logger)
{
    public async Task<PortalTreeResult> GetTreeAsync(CancellationToken cancellationToken)
    {
        var context = await LoadReadableTreeAsync(cancellationToken);
        if (context.Failure != PortalReadFailure.None)
            return new(context.Failure);

        var items = context.OrderedNodes
            .Select(node => new PortalTreeNodeResponse(
                node.Id,
                node.ParentId,
                node.Title,
                node.NodeKind,
                node.PortalPageId))
            .ToArray();
        return new(PortalReadFailure.None, new PortalTreeResponse(items, items.Length));
    }

    public async Task<PortalPageResult> GetPageAsync(long pageId, CancellationToken cancellationToken)
    {
        var context = await LoadReadableTreeAsync(cancellationToken);
        if (context.Failure != PortalReadFailure.None)
            return new(context.Failure);
        if (!context.EligiblePages.TryGetValue(pageId, out var page))
            return new(PortalReadFailure.NotFound);

        var placements = context.OrderedNodes
            .Where(node => node.NodeKind == PortalPageNodeKind.Page && node.PortalPageId == pageId)
            .ToArray();
        if (placements.Length == 0)
            return new(PortalReadFailure.NotFound);

        var byId = context.OrderedNodes.ToDictionary(node => node.Id);
        var canonicalPath = placements
            .Select(node => BuildPath(node, byId))
            .OrderBy(path => path, PortalNodePathComparer.Instance)
            .First();

        var targetKeys = GetTargetKeys(page).ToArray();
        var structureIds = page.Sections
            .Where(section => section.ProjectionKind == PortalPageProjectionKind.DatabaseStructure)
            .Select(section => ResolveSectionTargetKey(page, section))
            .Where(key => key.Type == PortalTargetType.DatabaseObject)
            .Select(key => key.Id)
            .ToArray();
        var targets = await targetResolver.ResolveEligibleTargetsAsync(
            targetKeys,
            structureIds,
            cancellationToken);
        if (targetKeys.Any(key => !targets.ContainsKey(key)))
        {
            logger.LogWarning("Portal page {PortalPageId} failed closed because a projection target is unavailable.", pageId);
            return new(PortalReadFailure.NotFound);
        }

        var sections = new List<PortalPageSectionResponse>(page.Sections.Count);
        foreach (var section in page.Sections.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
        {
            var key = ResolveSectionTargetKey(page, section);
            if (!targets.TryGetValue(key, out var target)
                || !TryCreateContent(section.ProjectionKind, target, out var content))
            {
                logger.LogWarning("Portal page {PortalPageId} failed closed because section {PortalSectionId} cannot be projected.", pageId, section.Id);
                return new(PortalReadFailure.NotFound);
            }
            sections.Add(new(
                section.Id,
                section.Heading,
                section.SourceKind,
                section.ProjectionKind,
                content!));
        }

        var primaryKey = new PortalTargetKey(page.PrimaryTargetType, page.PrimaryTargetId);
        var primary = targets[primaryKey];
        return new(
            PortalReadFailure.None,
            new PortalPageResponse(
                page.Id,
                page.Title,
                new(primary.Type, primary.Id, primary.Title),
                canonicalPath.Take(canonicalPath.Count - 1)
                    .Select(node => new PortalBreadcrumbItemResponse(node.Id, node.Title))
                    .ToArray(),
                sections));
    }

    private async Task<ReadableTreeContext> LoadReadableTreeAsync(CancellationToken cancellationToken)
    {
        var nodes = new List<PortalPageNode>();
        var frontier = await dbContext.PortalPageNodes.AsNoTracking()
            .Where(node => node.IsPublished && node.ParentId == null)
            .OrderBy(node => node.SortOrder)
            .ThenBy(node => node.Id)
            .Take(PortalLimits.MaximumEffectiveTreeNodes + 1)
            .ToListAsync(cancellationToken);
        if (frontier.Count > PortalLimits.MaximumEffectiveTreeNodes)
            return ReadableTreeContext.Failed(PortalReadFailure.LimitExceeded);
        nodes.AddRange(frontier);

        for (var depth = 2; depth <= PortalLimits.MaximumTreeDepth && frontier.Count > 0; depth++)
        {
            var parentIds = frontier.Select(node => node.Id).ToArray();
            var remaining = PortalLimits.MaximumEffectiveTreeNodes - nodes.Count;
            frontier = await dbContext.PortalPageNodes.AsNoTracking()
                .Where(node => node.IsPublished && node.ParentId != null && parentIds.Contains(node.ParentId.Value))
                .OrderBy(node => node.ParentId)
                .ThenBy(node => node.SortOrder)
                .ThenBy(node => node.Id)
                .Take(remaining + 1)
                .ToListAsync(cancellationToken);
            if (frontier.Count > remaining)
                return ReadableTreeContext.Failed(PortalReadFailure.LimitExceeded);
            nodes.AddRange(frontier);
        }

        if (frontier.Count > 0)
        {
            var deepestIds = frontier.Select(node => node.Id).ToArray();
            if (await dbContext.PortalPageNodes.AsNoTracking()
                .AnyAsync(node => node.IsPublished && node.ParentId != null
                    && deepestIds.Contains(node.ParentId.Value), cancellationToken))
                return ReadableTreeContext.Failed(PortalReadFailure.LimitExceeded);
        }

        var orderedNodes = OrderNodes(nodes);
        var pageIds = orderedNodes
            .Where(node => node.NodeKind == PortalPageNodeKind.Page && node.PortalPageId is not null)
            .Select(node => node.PortalPageId!.Value)
            .Distinct()
            .ToArray();
        var pages = await dbContext.PortalPages.AsNoTracking()
            .Where(page => page.IsPublished && pageIds.Contains(page.Id))
            .Include(page => page.Sections)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);

        var structurallyValidPages = new List<PortalPage>();
        foreach (var page in pages)
        {
            if (PortalCompositionValidator.ValidatePage(page).Count == 0
                && page.Sections.All(section => PortalCompositionValidator.IsB01ReadableProjection(section.ProjectionKind)))
            {
                structurallyValidPages.Add(page);
            }
            else
            {
                logger.LogWarning("Portal page {PortalPageId} was excluded because its persisted composition is not B01-readable.", page.Id);
            }
        }
        var requiredKeys = structurallyValidPages.SelectMany(GetTargetKeys).Distinct().ToArray();
        var identities = await targetResolver.ResolveEligibleIdentitiesAsync(requiredKeys, cancellationToken);
        var eligiblePages = structurallyValidPages
            .Where(page => GetTargetKeys(page).All(identities.ContainsKey))
            .ToDictionary(page => page.Id);

        var visibleNodeIds = new HashSet<long>();
        var visibleNodes = new List<PortalPageNode>();
        foreach (var node in orderedNodes)
        {
            var parentVisible = node.ParentId is null || visibleNodeIds.Contains(node.ParentId.Value);
            var nodeReadable = node.NodeKind == PortalPageNodeKind.Folder
                || eligiblePages.ContainsKey(node.PortalPageId!.Value);
            if (!parentVisible || !nodeReadable) continue;
            visibleNodeIds.Add(node.Id);
            visibleNodes.Add(node);
        }

        return new(PortalReadFailure.None, visibleNodes, eligiblePages);
    }

    private static IEnumerable<PortalTargetKey> GetTargetKeys(PortalPage page)
    {
        yield return new(page.PrimaryTargetType, page.PrimaryTargetId);
        foreach (var section in page.Sections.Where(item => item.SourceKind == PortalPageSectionSourceKind.ExplicitReference))
            yield return new(section.ReferenceTargetType!.Value, section.ReferenceTargetId!.Value);
    }

    private static PortalTargetKey ResolveSectionTargetKey(PortalPage page, PortalPageSection section) =>
        section.SourceKind switch
        {
            PortalPageSectionSourceKind.PrimaryTarget => new(page.PrimaryTargetType, page.PrimaryTargetId),
            PortalPageSectionSourceKind.ExplicitReference => new(section.ReferenceTargetType!.Value, section.ReferenceTargetId!.Value),
            _ => throw new InvalidOperationException("B01 does not execute derived Portal projections."),
        };

    private static IReadOnlyList<PortalPageNode> OrderNodes(IEnumerable<PortalPageNode> nodes)
    {
        var children = nodes.ToLookup(node => node.ParentId);
        var result = new List<PortalPageNode>();
        AddChildren(null, children, result);
        return result;
    }

    private static void AddChildren(
        long? parentId,
        ILookup<long?, PortalPageNode> children,
        ICollection<PortalPageNode> result)
    {
        foreach (var node in children[parentId].OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
        {
            result.Add(node);
            AddChildren(node.Id, children, result);
        }
    }

    private static IReadOnlyList<PortalPageNode> BuildPath(
        PortalPageNode leaf,
        IReadOnlyDictionary<long, PortalPageNode> byId)
    {
        var path = new List<PortalPageNode> { leaf };
        var cursor = leaf;
        while (cursor.ParentId is not null && byId.TryGetValue(cursor.ParentId.Value, out var parent))
        {
            path.Add(parent);
            cursor = parent;
        }
        path.Reverse();
        return path;
    }

    private static bool TryCreateContent(
        PortalPageProjectionKind projectionKind,
        PortalResolvedTarget target,
        out PortalSectionContentResponse? content)
    {
        content = projectionKind switch
        {
            PortalPageProjectionKind.Summary => new PortalSummaryContentResponse(
                target.Type,
                target.Id,
                target.Title,
                target.Summary),
            PortalPageProjectionKind.KnowledgeDocumentBody when target is PortalResolvedKnowledgeDocument document =>
                new PortalKnowledgeDocumentBodyContentResponse(
                    document.Id,
                    document.Title,
                    document.DocumentType,
                    document.BodyMarkdown),
            PortalPageProjectionKind.StructuredOverview when target is PortalResolvedSystem system =>
                new PortalSystemOverviewContentResponse(
                    system.Id,
                    system.Name,
                    system.DisplayName,
                    system.SystemType,
                    system.Lifecycle,
                    system.Summary),
            PortalPageProjectionKind.StructuredOverview when target is PortalResolvedBusinessFunction function =>
                new PortalBusinessFunctionOverviewContentResponse(
                    function.Id,
                    function.Name,
                    function.DisplayName,
                    function.FunctionType,
                    function.SystemName,
                    function.Summary,
                    function.CallerSummary,
                    function.InputDescription,
                    function.OutputDescription),
            PortalPageProjectionKind.StructuredOverview when target is PortalResolvedDatabaseObject databaseObject =>
                CreateDatabaseObjectOverview(databaseObject),
            PortalPageProjectionKind.StructuredOverview when target is PortalResolvedIntegration integration =>
                new PortalIntegrationOverviewContentResponse(
                    integration.Id,
                    integration.Title,
                    integration.IntegrationType,
                    integration.SourcePartyName,
                    integration.TargetPartyName,
                    integration.FlowDirection,
                    integration.Summary),
            PortalPageProjectionKind.DatabaseStructure when target is PortalResolvedDatabaseObject databaseObject =>
                new PortalDatabaseStructureContentResponse(
                    databaseObject.Id,
                    databaseObject.SchemaName,
                    databaseObject.ObjectName,
                    databaseObject.ObjectType,
                    databaseObject.Summary,
                    databaseObject.EstimatedRows,
                    databaseObject.AccessMode,
                    databaseObject.BusinessKeyColumns,
                    databaseObject.Columns.Select(column => new PortalDatabaseColumnResponse(
                        column.OrdinalPosition,
                        column.ColumnName,
                        column.DataType,
                        column.IsNullable,
                        column.DatabaseComment)).ToArray()),
            _ => null,
        };
        return content is not null;
    }

    private static PortalDatabaseObjectOverviewContentResponse CreateDatabaseObjectOverview(
        PortalResolvedDatabaseObject databaseObject) => new(
            databaseObject.Id,
            databaseObject.SchemaName,
            databaseObject.ObjectName,
            databaseObject.ObjectType,
            databaseObject.Summary,
            databaseObject.EstimatedRows,
            databaseObject.AccessMode,
            databaseObject.BusinessKeyColumns);

    private sealed record ReadableTreeContext(
        PortalReadFailure Failure,
        IReadOnlyList<PortalPageNode> OrderedNodes,
        IReadOnlyDictionary<long, PortalPage> EligiblePages)
    {
        public static ReadableTreeContext Failed(PortalReadFailure failure) =>
            new(failure, [], new Dictionary<long, PortalPage>());
    }

    private sealed class PortalNodePathComparer : IComparer<IReadOnlyList<PortalPageNode>>
    {
        public static PortalNodePathComparer Instance { get; } = new();

        public int Compare(IReadOnlyList<PortalPageNode>? left, IReadOnlyList<PortalPageNode>? right)
        {
            if (ReferenceEquals(left, right)) return 0;
            if (left is null) return -1;
            if (right is null) return 1;
            for (var index = 0; index < Math.Min(left.Count, right.Count); index++)
            {
                var order = left[index].SortOrder.CompareTo(right[index].SortOrder);
                if (order != 0) return order;
                var id = left[index].Id.CompareTo(right[index].Id);
                if (id != 0) return id;
            }
            return left.Count.CompareTo(right.Count);
        }
    }
}
