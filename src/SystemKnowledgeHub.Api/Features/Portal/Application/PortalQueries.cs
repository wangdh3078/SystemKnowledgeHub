using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public sealed class PortalQueries(
    KnowledgeHubDbContext dbContext,
    PortalTargetResolver targetResolver,
    PortalB04ProjectionService b04Projections,
    ILogger<PortalQueries> logger)
{
    public async Task<PortalHomeResult> GetHomeAsync(CancellationToken cancellationToken)
    {
        var context = await LoadReadableTreeAsync(cancellationToken);
        if (context.Failure != PortalReadFailure.None)
            return new(context.Failure);

        var categories = context.OrderedNodes
            .Where(node => node.ParentId is null)
            .Select(node => new PortalHomeCategoryResponse(
                node.Id,
                node.Title,
                node.NodeKind,
                node.PortalPageId))
            .ToArray();

        var recentPages = context.EligiblePages.Values
            .Where(page => page.PublishedAt is not null)
            .OrderByDescending(page => page.PublishedAt)
            .ThenByDescending(page => page.Id)
            .Take(PortalLimits.MaximumRecentPages)
            .ToArray();
        var primaryKeys = recentPages
            .Select(page => new PortalTargetKey(page.PrimaryTargetType, page.PrimaryTargetId))
            .Distinct()
            .ToArray();
        var primaryTargets = await targetResolver.ResolveEligibleIdentitiesAsync(
            primaryKeys,
            cancellationToken);
        var byId = context.OrderedNodes.ToDictionary(node => node.Id);
        var recent = recentPages
            .Where(page => primaryTargets.ContainsKey(new(page.PrimaryTargetType, page.PrimaryTargetId)))
            .Select(page =>
            {
                var primary = primaryTargets[new(page.PrimaryTargetType, page.PrimaryTargetId)];
                var canonicalPath = context.OrderedNodes
                    .Where(node => node.NodeKind == PortalPageNodeKind.Page && node.PortalPageId == page.Id)
                    .Select(node => BuildPath(node, byId))
                    .OrderBy(path => path, PortalNodePathComparer.Instance)
                    .First();
                return new PortalRecentPageResponse(
                    page.Id,
                    page.Title,
                    new(primary.Type, primary.Id, primary.Title),
                    canonicalPath.Take(canonicalPath.Count - 1)
                        .Select(node => new PortalBreadcrumbItemResponse(node.Id, node.Title))
                        .ToArray(),
                    page.PublishedAt!.Value);
            })
            .ToArray();

        return new(
            PortalReadFailure.None,
            new PortalHomeResponse("系统知识中心", categories, recent));
    }

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

        return await ProjectPageAsync(page, canonicalPath, CreatePortalLinks(context), cancellationToken);
    }

    public async Task<PortalPageResult> GetAdminPreviewPageAsync(
        PortalPage page,
        CancellationToken cancellationToken)
    {
        var context = await LoadReadableTreeAsync(cancellationToken);
        var links = context.Failure == PortalReadFailure.None
            ? CreatePortalLinks(context)
            : new Dictionary<PortalTargetKey, long>();
        return await ProjectPageAsync(page, [], links, cancellationToken);
    }

    public async Task<PortalSearchResult> SearchAsync(
        string query,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
    {
        var context = await LoadReadableTreeAsync(cancellationToken);
        if (context.Failure != PortalReadFailure.None) return new(context.Failure);

        var pages = context.EligiblePages.Values.ToArray();
        var targetKeys = pages.SelectMany(GetTargetKeys).Distinct().ToArray();
        var identities = await targetResolver.ResolveEligibleIdentitiesAsync(targetKeys, cancellationToken);
        var documentIds = targetKeys.Where(item => item.Type == PortalTargetType.KnowledgeDocument)
            .Select(item => item.Id).Distinct().ToArray();
        var documents = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => documentIds.Contains(item.Id) && item.LifecycleStatus == DocumentLifecycleStatus.Published)
            .Select(item => new { item.Id, item.Title, item.Summary, item.BodyMarkdown })
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        var byNodeId = context.OrderedNodes.ToDictionary(node => node.Id);
        var normalized = query.Trim();
        var results = new List<(int Rank, PortalSearchItemResponse Item)>();
        foreach (var portalPage in pages)
        {
            var primaryKey = new PortalTargetKey(portalPage.PrimaryTargetType, portalPage.PrimaryTargetId);
            if (!identities.TryGetValue(primaryKey, out var primary)) continue;
            var explicitKeys = portalPage.Sections
                .Where(item => item.SourceKind == PortalPageSectionSourceKind.ExplicitReference)
                .Select(item => new PortalTargetKey(item.ReferenceTargetType!.Value, item.ReferenceTargetId!.Value))
                .Distinct().ToArray();
            var targetTitles = explicitKeys.Prepend(primaryKey)
                .Where(identities.ContainsKey).Select(item => identities[item].Title).ToArray();
            var pageDocuments = explicitKeys.Prepend(primaryKey)
                .Where(item => item.Type == PortalTargetType.KnowledgeDocument && documents.ContainsKey(item.Id))
                .Select(item => documents[item.Id]).DistinctBy(item => item.Id).ToArray();
            var documentText = string.Join(" ", pageDocuments.Select(item => $"{item.Title} {item.Summary} {KnowledgeDocumentSearchText.ToPlainText(item.BodyMarkdown)}"));
            var rank = Rank(portalPage.Title, normalized, 0)
                ?? targetTitles.Select(title => Rank(title, normalized, 3)).Where(value => value is not null).Min()
                ?? Rank(documentText, normalized, 6);
            if (rank is null) continue;
            var placement = context.OrderedNodes
                .Where(node => node.NodeKind == PortalPageNodeKind.Page && node.PortalPageId == portalPage.Id)
                .Select(node => BuildPath(node, byNodeId)).OrderBy(path => path, PortalNodePathComparer.Instance).First();
            var snippetDocument = pageDocuments.FirstOrDefault(item =>
                ($"{item.Title} {item.Summary} {KnowledgeDocumentSearchText.ToPlainText(item.BodyMarkdown)}")
                    .Contains(normalized, StringComparison.OrdinalIgnoreCase));
            var snippet = snippetDocument is null
                ? targetTitles.FirstOrDefault(title => title.Contains(normalized, StringComparison.OrdinalIgnoreCase)) ?? portalPage.Title
                : KnowledgeDocumentSearchText.CreateSnippet(snippetDocument.Title, snippetDocument.Summary, snippetDocument.BodyMarkdown, normalized);
            results.Add((rank.Value, new(
                portalPage.Id,
                portalPage.Title,
                primary.Type,
                primary.Title,
                placement.Take(placement.Count - 1).Select(node => new PortalBreadcrumbItemResponse(node.Id, node.Title)).ToArray(),
                snippet)));
        }
        var ordered = results.OrderBy(item => item.Rank).ThenBy(item => item.Item.Title, StringComparer.Ordinal).ThenBy(item => item.Item.PageId).ToArray();
        var items = ordered.Skip((page - 1) * pageSize).Take(pageSize).Select(item => item.Item).ToArray();
        return new(PortalReadFailure.None, new(items, page, pageSize, ordered.Length));
    }

    public async Task<long?> GetAuthorizedAttachmentDocumentIdAsync(
        long pageId,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var context = await LoadReadableTreeAsync(cancellationToken);
        if (context.Failure != PortalReadFailure.None || !context.EligiblePages.TryGetValue(pageId, out var page)) return null;
        var allowedDocumentIds = GetTargetKeys(page)
            .Where(item => item.Type == PortalTargetType.KnowledgeDocument)
            .Select(item => item.Id).Distinct().ToArray();
        if (allowedDocumentIds.Length == 0) return null;
        return await (
            from document in dbContext.KnowledgeDocuments.AsNoTracking()
            join revision in dbContext.KnowledgeDocumentRevisions.AsNoTracking()
                on new { document.Id, RevisionNumber = document.CurrentRevisionNumber }
                equals new { Id = revision.KnowledgeDocumentId, revision.RevisionNumber }
            join reference in dbContext.AttachmentReferences.AsNoTracking() on revision.Id equals reference.KnowledgeDocumentRevisionId
            join attachment in dbContext.Attachments.AsNoTracking() on reference.AttachmentId equals attachment.Id
            where allowedDocumentIds.Contains(document.Id)
                && document.LifecycleStatus == DocumentLifecycleStatus.Published
                && attachment.Id == attachmentId
                && attachment.KnowledgeDocumentId == document.Id
                && reference.KnowledgeDocumentId == document.Id
                && attachment.StorageState == AttachmentStorageState.Ready
            select (long?)document.Id).SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<PortalPageResult> ProjectPageAsync(
        PortalPage page,
        IReadOnlyList<PortalPageNode> canonicalPath,
        IReadOnlyDictionary<PortalTargetKey, long> portalLinks,
        CancellationToken cancellationToken)
    {
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
            logger.LogWarning("Portal page {PortalPageId} failed closed because a projection target is unavailable.", page.Id);
            return new(PortalReadFailure.NotFound);
        }

        var sections = new List<PortalPageSectionResponse>(page.Sections.Count);
        foreach (var section in page.Sections.OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
        {
            PortalResolvedTarget? target = null;
            if (section.SourceKind != PortalPageSectionSourceKind.Derived)
            {
                var key = ResolveSectionTargetKey(page, section);
                if (!targets.TryGetValue(key, out target))
                {
                    logger.LogWarning("Portal page {PortalPageId} failed closed because section {PortalSectionId} target is unavailable.", page.Id, section.Id);
                    return new(PortalReadFailure.NotFound);
                }
            }
            PortalSectionContentResponse? content;
            if (TryCreateContent(section.ProjectionKind, target, out content))
            {
                if (content is PortalKnowledgeDocumentBodyContentResponse body)
                    content = body with { ImageAttachmentIds = await b04Projections.GetCurrentImageAttachmentIdsAsync(body.DocumentId, cancellationToken) };
            }
            else
            {
                content = await b04Projections.ProjectAsync(page, section, target, portalLinks, cancellationToken);
            }
            if (content is null)
            {
                logger.LogWarning("Portal page {PortalPageId} failed closed because section {PortalSectionId} cannot be projected.", page.Id, section.Id);
                return new(PortalReadFailure.NotFound);
            }
            sections.Add(new(
                section.Id,
                section.Heading,
                section.SourceKind,
                section.ProjectionKind,
                content));
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
                && page.Sections.All(section => PortalCompositionValidator.IsReadableProjection(section.ProjectionKind)))
            {
                structurallyValidPages.Add(page);
            }
            else
            {
                logger.LogWarning("Portal page {PortalPageId} was excluded because its persisted composition is not Portal-readable.", page.Id);
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
            _ => throw new InvalidOperationException("Derived Portal projections do not have a reference target."),
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
        PortalResolvedTarget? target,
        out PortalSectionContentResponse? content)
    {
        content = projectionKind switch
        {
            PortalPageProjectionKind.Summary when target is not null => new PortalSummaryContentResponse(
                target.Type,
                target.Id,
                target.Title,
                target.Summary),
            PortalPageProjectionKind.KnowledgeDocumentBody when target is PortalResolvedKnowledgeDocument document =>
                new PortalKnowledgeDocumentBodyContentResponse(
                    document.Id,
                    document.Title,
                    document.DocumentType,
                    document.BodyMarkdown,
                    []),
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
                    databaseObject.DatabaseComment,
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
            databaseObject.DatabaseComment,
            databaseObject.EstimatedRows,
            databaseObject.AccessMode,
            databaseObject.BusinessKeyColumns);

    private static IReadOnlyDictionary<PortalTargetKey, long> CreatePortalLinks(ReadableTreeContext context) =>
        context.EligiblePages.Values
            .OrderBy(page => page.Title, StringComparer.Ordinal)
            .ThenBy(page => page.Id)
            .GroupBy(page => new PortalTargetKey(page.PrimaryTargetType, page.PrimaryTargetId))
            .ToDictionary(group => group.Key, group => group.First().Id);

    private static int? Rank(string value, string query, int offset)
    {
        if (string.Equals(value, query, StringComparison.OrdinalIgnoreCase)) return offset;
        if (value.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return offset + 1;
        return value.Contains(query, StringComparison.OrdinalIgnoreCase) ? offset + 2 : null;
    }

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
