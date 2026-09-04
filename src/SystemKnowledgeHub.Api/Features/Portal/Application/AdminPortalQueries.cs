using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public sealed class AdminPortalQueries(
    KnowledgeHubDbContext dbContext,
    PortalTargetResolver targetResolver,
    PortalPublicationReadiness readiness,
    PortalQueries portalQueries,
    ConcurrencyTokenCodec tokenCodec)
{
    public async Task<AdminPortalQueryResult<AdminPortalPageListResponse>> GetPagesAsync(
        int? page,
        int? pageSize,
        string? search,
        CancellationToken cancellationToken)
    {
        var paging = ValidatePaging(page, pageSize);
        if (paging.Errors is not null) return new(null, paging.Errors);
        var normalizedSearch = search?.Trim();
        if (normalizedSearch?.Length > 200)
            return new(null, new Dictionary<string, string[]> { ["search"] = ["搜索内容不能超过 200 个字符。"] });

        IQueryable<PortalPage> query = dbContext.PortalPages.AsNoTracking();
        if (!string.IsNullOrWhiteSpace(normalizedSearch))
        {
            var matchingTargets = await FindMatchingTargetKeysAsync(normalizedSearch, cancellationToken);
            var systemIds = matchingTargets.Where(key => key.Type == PortalTargetType.System).Select(key => key.Id).ToArray();
            var functionIds = matchingTargets.Where(key => key.Type == PortalTargetType.BusinessFunction).Select(key => key.Id).ToArray();
            var databaseObjectIds = matchingTargets.Where(key => key.Type == PortalTargetType.DatabaseObject).Select(key => key.Id).ToArray();
            var documentIds = matchingTargets.Where(key => key.Type == PortalTargetType.KnowledgeDocument).Select(key => key.Id).ToArray();
            var integrationIds = matchingTargets.Where(key => key.Type == PortalTargetType.Integration).Select(key => key.Id).ToArray();
            query = query.Where(item => item.Title.Contains(normalizedSearch)
                || (item.PrimaryTargetType == PortalTargetType.System && systemIds.Contains(item.PrimaryTargetId))
                || (item.PrimaryTargetType == PortalTargetType.BusinessFunction && functionIds.Contains(item.PrimaryTargetId))
                || (item.PrimaryTargetType == PortalTargetType.DatabaseObject && databaseObjectIds.Contains(item.PrimaryTargetId))
                || (item.PrimaryTargetType == PortalTargetType.KnowledgeDocument && documentIds.Contains(item.PrimaryTargetId))
                || (item.PrimaryTargetType == PortalTargetType.Integration && integrationIds.Contains(item.PrimaryTargetId)));
        }

        var total = await query.CountAsync(cancellationToken);
        var pages = await query
            .OrderByDescending(item => item.Id)
            .Skip((paging.Page - 1) * paging.PageSize)
            .Take(paging.PageSize)
            .Include(item => item.Sections)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var keys = pages.SelectMany(PortalPublicationReadiness.GetTargetKeys).Distinct().ToArray();
        var targets = await targetResolver.ResolveAdminIdentitiesAsync(keys, cancellationToken);
        var placementCounts = await dbContext.PortalPageNodes.AsNoTracking()
            .Where(node => node.PortalPageId != null && pages.Select(item => item.Id).Contains(node.PortalPageId.Value))
            .GroupBy(node => node.PortalPageId!.Value)
            .Select(group => new { PageId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.PageId, item => item.Count, cancellationToken);

        var items = new List<AdminPortalPageListItemResponse>(pages.Count);
        foreach (var item in pages)
        {
            var pageReadiness = await readiness.EvaluateAsync(item, cancellationToken);
            var primary = TargetSummary(new(item.PrimaryTargetType, item.PrimaryTargetId), targets);
            items.Add(new(
                item.Id,
                item.Title,
                primary,
                item.IsPublished,
                item.IsPublished ? "已发布" : "未发布",
                PortalPublicationReadiness.ToHealth(pageReadiness),
                placementCounts.GetValueOrDefault(item.Id),
                item.UpdatedAt,
                tokenCodec.Encode(item.Version)));
        }
        return new(new(items, paging.Page, paging.PageSize, total));
    }

    public async Task<AdminPortalQueryResult<AdminPortalPageDetailResponse>> GetPageAsync(
        long pageId,
        CancellationToken cancellationToken)
    {
        var page = await dbContext.PortalPages.AsNoTracking()
            .Include(item => item.Sections)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == pageId, cancellationToken);
        if (page is null) return new(null, Failure: AdminPortalFailure.NotFound);

        var keys = PortalPublicationReadiness.GetTargetKeys(page).Distinct().ToArray();
        var targets = await targetResolver.ResolveAdminIdentitiesAsync(keys, cancellationToken);
        var pageReadiness = await readiness.EvaluateAsync(page, cancellationToken);
        var nodes = await dbContext.PortalPageNodes.AsNoTracking().ToListAsync(cancellationToken);
        var byId = nodes.ToDictionary(item => item.Id);
        var placements = nodes
            .Where(node => node.NodeKind == PortalPageNodeKind.Page && node.PortalPageId == page.Id)
            .OrderBy(node => BuildPath(node, byId), StringComparer.Ordinal)
            .Select(node => new AdminPortalPlacementResponse(
                node.Id,
                BuildPath(node, byId),
                node.IsPublished,
                PortalPublicationReadiness.IsEffectivePublished(node, byId)))
            .ToArray();
        var sections = page.Sections
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.Id)
            .Select(section =>
            {
                AdminPortalTargetSummaryResponse? reference = null;
                var healthy = PortalCompositionValidator.IsReadableProjection(section.ProjectionKind);
                var message = healthy ? "正常" : "当前版本暂不支持此章节。";
                if (section.SourceKind == PortalPageSectionSourceKind.PrimaryTarget
                    && !targets.ContainsKey(new(page.PrimaryTargetType, page.PrimaryTargetId)))
                {
                    healthy = false;
                    message = "主知识对象已失效，需要重新选择。";
                }
                if (section.SourceKind == PortalPageSectionSourceKind.ExplicitReference)
                {
                    var key = new PortalTargetKey(section.ReferenceTargetType!.Value, section.ReferenceTargetId!.Value);
                    reference = TargetSummary(key, targets);
                    if (!targets.ContainsKey(key))
                    {
                        healthy = false;
                        message = "引用已失效，需要删除或更换引用。";
                    }
                }
                return new AdminPortalSectionResponse(
                    section.Id,
                    section.Heading,
                    section.SourceKind,
                    reference,
                    section.ProjectionKind,
                    section.SortOrder,
                    healthy,
                    message);
            })
            .ToArray();

        return new(new(
            page.Id,
            page.Title,
            TargetSummary(new(page.PrimaryTargetType, page.PrimaryTargetId), targets),
            page.IsPublished,
            page.IsPublished ? "已发布" : "未发布",
            sections,
            placements,
            PortalPublicationReadiness.ToHealth(pageReadiness),
            page.UpdatedAt,
            tokenCodec.Encode(page.Version)));
    }

    public async Task<AdminPortalQueryResult<AdminPortalPreviewResponse>> GetPreviewAsync(
        long pageId,
        CancellationToken cancellationToken)
    {
        var page = await dbContext.PortalPages.AsNoTracking()
            .Include(item => item.Sections)
            .AsSplitQuery()
            .SingleOrDefaultAsync(item => item.Id == pageId, cancellationToken);
        if (page is null) return new(null, Failure: AdminPortalFailure.NotFound);
        var result = await readiness.EvaluateAsync(page, cancellationToken);
        PortalPageResponse? projection = null;
        if (result.CanPublish)
        {
            var projected = await portalQueries.GetAdminPreviewPageAsync(page, cancellationToken);
            projection = projected.Response;
        }
        return new(new(projection, result));
    }

    public async Task<AdminPortalQueryResult<AdminPortalTreeResponse>> GetTreeAsync(
        CancellationToken cancellationToken)
    {
        var nodes = await dbContext.PortalPageNodes.AsNoTracking()
            .OrderBy(item => item.Id)
            .Take(PortalLimits.MaximumEffectiveTreeNodes + 1)
            .ToListAsync(cancellationToken);
        if (nodes.Count > PortalLimits.MaximumEffectiveTreeNodes)
            return new(null, Failure: AdminPortalFailure.LimitExceeded);
        var pageIds = nodes.Where(node => node.PortalPageId != null).Select(node => node.PortalPageId!.Value).Distinct().ToArray();
        var pages = await dbContext.PortalPages.AsNoTracking()
            .Where(page => pageIds.Contains(page.Id))
            .Include(page => page.Sections)
            .AsSplitQuery()
            .ToListAsync(cancellationToken);
        var pagesById = pages.ToDictionary(page => page.Id);
        var keys = pages.SelectMany(PortalPublicationReadiness.GetTargetKeys).Distinct().ToArray();
        var targets = await targetResolver.ResolveAdminIdentitiesAsync(keys, cancellationToken);
        var ordered = OrderNodes(nodes);
        var byId = nodes.ToDictionary(item => item.Id);
        var items = ordered.Select(node =>
        {
            var health = new AdminPortalHealthResponse("healthy", "正常", true);
            string? pageTitle = null;
            if (node.NodeKind == PortalPageNodeKind.Page)
            {
                if (!pagesById.TryGetValue(node.PortalPageId!.Value, out var page))
                    health = new("page_missing", "关联页面已失效。", false);
                else
                {
                    pageTitle = page.Title;
                    if (page.Sections.Any(section => !PortalCompositionValidator.IsReadableProjection(section.ProjectionKind)))
                        health = new("projection_unsupported", "页面存在当前版本暂不支持的章节。", false);
                    else if (PortalPublicationReadiness.GetTargetKeys(page).Any(key => !targets.ContainsKey(key)))
                        health = new("reference_missing", "页面存在失效引用。", false);
                    else if (!page.IsPublished)
                        health = new("page_unpublished", "页面内容尚未发布。", false);
                }
            }
            return new AdminPortalTreeNodeResponse(
                node.Id,
                node.ParentId,
                node.Title,
                node.NodeKind,
                node.PortalPageId,
                pageTitle,
                node.IsPublished,
                PortalPublicationReadiness.IsEffectivePublished(node, byId),
                health,
                tokenCodec.Encode(node.Version));
        }).ToArray();
        return new(new(items, items.Length));
    }

    public async Task<AdminPortalQueryResult<AdminPortalTargetListResponse>> GetTargetsAsync(
        PortalTargetType? type,
        string? search,
        int? page,
        int? pageSize,
        CancellationToken cancellationToken)
    {
        var paging = ValidatePaging(page, pageSize);
        if (paging.Errors is not null) return new(null, paging.Errors);
        if (type is null || !Enum.IsDefined(type.Value))
            return new(null, new Dictionary<string, string[]> { ["type"] = ["请选择有效的知识类型。"] });
        var normalizedSearch = search?.Trim() ?? string.Empty;
        if (normalizedSearch.Length > 200)
            return new(null, new Dictionary<string, string[]> { ["search"] = ["搜索内容不能超过 200 个字符。"] });

        var result = type.Value switch
        {
            PortalTargetType.System => await GetSystemTargets(normalizedSearch, paging.Page, paging.PageSize, cancellationToken),
            PortalTargetType.BusinessFunction => await GetFunctionTargets(normalizedSearch, paging.Page, paging.PageSize, cancellationToken),
            PortalTargetType.DatabaseObject => await GetDatabaseObjectTargets(normalizedSearch, paging.Page, paging.PageSize, cancellationToken),
            PortalTargetType.KnowledgeDocument => await GetDocumentTargets(normalizedSearch, paging.Page, paging.PageSize, cancellationToken),
            PortalTargetType.Integration => await GetIntegrationTargets(normalizedSearch, paging.Page, paging.PageSize, cancellationToken),
            _ => throw new InvalidOperationException("Unsupported Portal target type."),
        };
        return new(new(result.Items, paging.Page, paging.PageSize, result.Total));
    }

    private async Task<TargetPage> GetSystemTargets(string search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Systems.AsNoTracking()
            .Where(item => search == string.Empty || item.Name.Contains(search) || item.DisplayName.Contains(search));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.DisplayName).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new { item.Id, item.Name, item.DisplayName, item.SystemType, item.Lifecycle })
            .ToListAsync(cancellationToken);
        return new(rows.Select(item => new AdminPortalTargetSummaryResponse(
            PortalTargetType.System, item.Id, Display(item.DisplayName, item.Name), item.Name,
            "可编排", null, item.Lifecycle.ToString())).ToArray(), total);
    }

    private async Task<TargetPage> GetFunctionTargets(string search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.BusinessFunctions.AsNoTracking()
            .Where(item => (search == string.Empty || item.Name.Contains(search) || (item.DisplayName != null && item.DisplayName.Contains(search)))
                && dbContext.Systems.Any(system => system.Id == item.SystemId));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.DisplayName).ThenBy(item => item.Name).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new { item.Id, item.Name, item.DisplayName, SystemName = item.System.DisplayName, SystemFallback = item.System.Name })
            .ToListAsync(cancellationToken);
        return new(rows.Select(item => new AdminPortalTargetSummaryResponse(
            PortalTargetType.BusinessFunction, item.Id, Display(item.DisplayName, item.Name),
            $"所属系统：{Display(item.SystemName, item.SystemFallback)}", "可编排", null, null)).ToArray(), total);
    }

    private async Task<TargetPage> GetDatabaseObjectTargets(string search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.DatabaseObjects.AsNoTracking()
            .Where(item => (search == string.Empty || item.SchemaName.Contains(search) || item.ObjectName.Contains(search))
                && dbContext.DatabaseSources.Any(source => source.Id == item.DatabaseSourceId
                    && dbContext.Systems.Any(system => system.Id == source.SystemId)));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.SchemaName).ThenBy(item => item.ObjectName).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new
            {
                item.Id,
                item.SchemaName,
                item.ObjectName,
                SourceName = item.DatabaseSource.Name,
                SystemName = item.DatabaseSource.System.DisplayName,
                SystemFallback = item.DatabaseSource.System.Name,
            }).ToListAsync(cancellationToken);
        return new(rows.Select(item => new AdminPortalTargetSummaryResponse(
            PortalTargetType.DatabaseObject, item.Id, $"{item.SchemaName}.{item.ObjectName}",
            $"{Display(item.SystemName, item.SystemFallback)} · {item.SourceName}", "可编排", null, null)).ToArray(), total);
    }

    private async Task<TargetPage> GetDocumentTargets(string search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => search == string.Empty || item.Title.Contains(search));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.Title).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new { item.Id, item.Title, item.DocumentType, item.LifecycleStatus })
            .ToListAsync(cancellationToken);
        return new(rows.Select(item => new AdminPortalTargetSummaryResponse(
            PortalTargetType.KnowledgeDocument, item.Id, item.Title, null,
            item.LifecycleStatus == DocumentLifecycleStatus.Published ? "可发布" : "可编排，发布受阻",
            item.DocumentType.ToString(), item.LifecycleStatus.ToString())).ToArray(), total);
    }

    private async Task<TargetPage> GetIntegrationTargets(string search, int page, int pageSize, CancellationToken cancellationToken)
    {
        var query = dbContext.Integrations.AsNoTracking()
            .Where(item => (search == string.Empty || item.Name.Contains(search))
                && (item.SourceSystemId == null || dbContext.Systems.Any(system => system.Id == item.SourceSystemId))
                && (item.TargetSystemId == null || dbContext.Systems.Any(system => system.Id == item.TargetSystemId)));
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderBy(item => item.Name).ThenBy(item => item.Id)
            .Skip((page - 1) * pageSize).Take(pageSize)
            .Select(item => new { item.Id, item.Name, item.SourcePartyName, item.TargetPartyName, item.FlowDirection })
            .ToListAsync(cancellationToken);
        return new(rows.Select(item => new AdminPortalTargetSummaryResponse(
            PortalTargetType.Integration, item.Id, item.Name,
            $"{item.SourcePartyName} → {item.TargetPartyName} · {item.FlowDirection}", "可编排", null, null)).ToArray(), total);
    }

    private async Task<PortalTargetKey[]> FindMatchingTargetKeysAsync(string search, CancellationToken cancellationToken)
    {
        var systems = await dbContext.Systems.AsNoTracking()
            .Where(item => item.Name.Contains(search) || item.DisplayName.Contains(search))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var functions = await dbContext.BusinessFunctions.AsNoTracking()
            .Where(item => item.Name.Contains(search) || (item.DisplayName != null && item.DisplayName.Contains(search)))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var objects = await dbContext.DatabaseObjects.AsNoTracking()
            .Where(item => item.SchemaName.Contains(search) || item.ObjectName.Contains(search))
            .Select(item => item.Id).ToListAsync(cancellationToken);
        var documents = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(item => item.Title.Contains(search)).Select(item => item.Id).ToListAsync(cancellationToken);
        var integrations = await dbContext.Integrations.AsNoTracking()
            .Where(item => item.Name.Contains(search)).Select(item => item.Id).ToListAsync(cancellationToken);
        return systems.Select(id => new PortalTargetKey(PortalTargetType.System, id))
            .Concat(functions.Select(id => new PortalTargetKey(PortalTargetType.BusinessFunction, id)))
            .Concat(objects.Select(id => new PortalTargetKey(PortalTargetType.DatabaseObject, id)))
            .Concat(documents.Select(id => new PortalTargetKey(PortalTargetType.KnowledgeDocument, id)))
            .Concat(integrations.Select(id => new PortalTargetKey(PortalTargetType.Integration, id)))
            .ToArray();
    }

    private static PagingValidation ValidatePaging(int? page, int? pageSize)
    {
        var actualPage = page ?? 1;
        var actualPageSize = pageSize ?? 20;
        var errors = new Dictionary<string, string[]>();
        if (actualPage < 1) errors["page"] = ["页码必须大于或等于 1。"];
        if (actualPageSize is not (20 or 50 or 100)) errors["pageSize"] = ["每页数量仅支持 20、50 或 100。"];
        return new(actualPage, actualPageSize, errors.Count == 0 ? null : errors);
    }

    private static AdminPortalTargetSummaryResponse TargetSummary(
        PortalTargetKey key,
        IReadOnlyDictionary<PortalTargetKey, PortalTargetIdentity> targets) =>
        targets.TryGetValue(key, out var target)
            ? new(key.Type, key.Id, target.Title, null, "可编排", target.DocumentType, target.Lifecycle)
            : new(key.Type, key.Id, "引用已失效", null, "需要处理", null, null);

    private static IReadOnlyList<PortalPageNode> OrderNodes(IEnumerable<PortalPageNode> nodes)
    {
        var byParent = nodes.ToLookup(item => item.ParentId);
        var result = new List<PortalPageNode>();
        AddChildren(null, byParent, result);
        return result;
    }

    private static void AddChildren(long? parentId, ILookup<long?, PortalPageNode> byParent, ICollection<PortalPageNode> result)
    {
        foreach (var node in byParent[parentId].OrderBy(item => item.SortOrder).ThenBy(item => item.Id))
        {
            result.Add(node);
            AddChildren(node.Id, byParent, result);
        }
    }

    private static string BuildPath(PortalPageNode node, IReadOnlyDictionary<long, PortalPageNode> byId)
    {
        var titles = new List<string> { node.Title };
        var cursor = node;
        var seen = new HashSet<long> { node.Id };
        while (cursor.ParentId is not null && seen.Add(cursor.ParentId.Value) && byId.TryGetValue(cursor.ParentId.Value, out var parent))
        {
            titles.Add(parent.Title);
            cursor = parent;
        }
        titles.Reverse();
        return string.Join(" / ", titles);
    }

    private static string Display(string? preferred, string fallback) =>
        string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;

    private sealed record PagingValidation(int Page, int PageSize, IReadOnlyDictionary<string, string[]>? Errors);
    private sealed record TargetPage(IReadOnlyList<AdminPortalTargetSummaryResponse> Items, int Total);
}
