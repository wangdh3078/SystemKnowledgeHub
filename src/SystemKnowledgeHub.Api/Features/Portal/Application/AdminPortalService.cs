using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public sealed class AdminPortalService(
    KnowledgeHubDbContext dbContext,
    PortalTargetResolver targetResolver,
    PortalCompositionValidator validator,
    PortalPublicationReadiness readiness,
    AdminPortalQueries queries,
    ConcurrencyTokenCodec tokenCodec,
    TimeProvider timeProvider)
{
    public async Task<AdminPortalCommandResult<AdminPortalPageDetailResponse>> CreatePageAsync(
        CreateAdminPortalPageRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var errors = ValidatePageRequest(request.Title, request.PrimaryTarget);
        if (errors.Count > 0) return Validation<AdminPortalPageDetailResponse>(errors);
        var key = new PortalTargetKey(request.PrimaryTarget!.Type, request.PrimaryTarget.Id);
        var targets = await targetResolver.ResolveAdminIdentitiesAsync([key], cancellationToken);
        if (!targets.ContainsKey(key))
            return ReferenceInvalid<AdminPortalPageDetailResponse>("选择的主知识对象不存在或已失效。");

        var now = timeProvider.GetUtcNow();
        var page = new PortalPage
        {
            Title = request.Title!.Trim(),
            PrimaryTargetType = key.Type,
            PrimaryTargetId = key.Id,
            CreatedAt = now,
            CreatedByUserId = actor.UserId,
            CreatedByDisplayName = actor.DisplayName,
            UpdatedAt = now,
            UpdatedByUserId = actor.UserId,
            UpdatedByDisplayName = actor.DisplayName,
            Sections =
            [
                new PortalPageSection
                {
                    Heading = "概览",
                    SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
                    ProjectionKind = PortalPageProjectionKind.Summary,
                    SortOrder = 0,
                },
            ],
        };
        dbContext.PortalPages.Add(page);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ReloadPage(page.Id, cancellationToken);
    }

    public async Task<AdminPortalCommandResult<AdminPortalPageDetailResponse>> UpdatePageAsync(
        long pageId,
        UpdateAdminPortalPageRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var tokenError = DecodeToken(request.ConcurrencyToken, out var expectedVersion);
        var errors = ValidatePageRequest(request.Title, request.PrimaryTarget);
        if (tokenError is not null) errors["concurrencyToken"] = [tokenError];
        if (request.Sections is null) errors["sections"] = ["必须提交完整章节列表。"];
        if (errors.Count > 0) return Validation<AdminPortalPageDetailResponse>(errors);

        var requestedSections = request.Sections!;
        if (requestedSections.Where(item => item.Id is not null).Select(item => item.Id).Distinct().Count()
            != requestedSections.Count(item => item.Id is not null))
            errors["sections.id"] = ["章节 ID 不得重复。"];
        var candidateSections = requestedSections.Select((item, index) => new PortalPageSection
        {
            Id = item.Id ?? index + 1,
            PortalPageId = pageId,
            Heading = item.Heading ?? string.Empty,
            SourceKind = item.SourceKind,
            ReferenceTargetType = item.ReferenceTarget?.Type,
            ReferenceTargetId = item.ReferenceTarget?.Id,
            ProjectionKind = item.ProjectionKind,
            SortOrder = item.SortOrder,
        }).ToArray();
        foreach (var pair in PortalCompositionValidator.ValidateSections(request.PrimaryTarget!.Type, candidateSections))
            errors[pair.Key] = pair.Value;
        if (requestedSections.Any(item => !PortalCompositionValidator.IsReadableProjection(item.ProjectionKind)))
            errors["sections.projectionKind"] = ["章节投影类型无效。"];
        if (errors.Count > 0) return Validation<AdminPortalPageDetailResponse>(errors);

        var targetKeys = new[] { new PortalTargetKey(request.PrimaryTarget.Type, request.PrimaryTarget.Id) }
            .Concat(requestedSections
                .Where(item => item.SourceKind == PortalPageSectionSourceKind.ExplicitReference && item.ReferenceTarget is not null)
                .Select(item => new PortalTargetKey(item.ReferenceTarget!.Type, item.ReferenceTarget.Id)))
            .Distinct()
            .ToArray();
        var targets = await targetResolver.ResolveAdminIdentitiesAsync(targetKeys, cancellationToken);
        if (targetKeys.Any(key => !targets.ContainsKey(key)))
            return ReferenceInvalid<AdminPortalPageDetailResponse>("主知识对象或章节引用不存在，请重新选择。");

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var page = await dbContext.PortalPages
            .Include(item => item.Sections)
            .SingleOrDefaultAsync(item => item.Id == pageId, cancellationToken);
        if (page is null) return NotFound<AdminPortalPageDetailResponse>();
        if (page.Version != expectedVersion) return Conflict<AdminPortalPageDetailResponse>();
        if (page.IsPublished)
            return InvalidState<AdminPortalPageDetailResponse>("请先取消发布，再修改页面内容。");

        var requestedIds = requestedSections.Where(item => item.Id is not null).Select(item => item.Id!.Value).ToHashSet();
        var existingIds = page.Sections.Select(item => item.Id).ToHashSet();
        if (requestedIds.Any(id => !existingIds.Contains(id)))
        {
            var belongsElsewhere = await dbContext.PortalPageSections.IgnoreQueryFilters()
                .AnyAsync(item => requestedIds.Contains(item.Id) && item.PortalPageId != pageId, cancellationToken);
            return belongsElsewhere
                ? ReferenceInvalid<AdminPortalPageDetailResponse>("章节不属于当前页面，不能跨页面移动。")
                : ReferenceInvalid<AdminPortalPageDetailResponse>("章节已不存在，请重新加载页面。");
        }

        var missing = page.Sections.Where(item => !requestedIds.Contains(item.Id)).ToArray();
        dbContext.PortalPageSections.RemoveRange(missing);
        var retained = page.Sections.Where(item => requestedIds.Contains(item.Id)).ToArray();
        var maxOrder = retained.Length == 0 ? 0 : retained.Max(item => item.SortOrder);
        if (maxOrder > int.MaxValue - retained.Length - 1)
            return Validation<AdminPortalPageDetailResponse>(new Dictionary<string, string[]> { ["sections.sortOrder"] = ["章节顺序超出允许范围。"] });
        var offset = maxOrder + retained.Length + 1;
        foreach (var section in retained) section.SortOrder += offset;
        await dbContext.SaveChangesAsync(cancellationToken);

        var existingById = retained.ToDictionary(item => item.Id);
        foreach (var item in requestedSections)
        {
            var section = item.Id is not null ? existingById[item.Id.Value] : new PortalPageSection { PortalPageId = pageId };
            section.Heading = item.Heading!.Trim();
            section.SourceKind = item.SourceKind;
            section.ReferenceTargetType = item.ReferenceTarget?.Type;
            section.ReferenceTargetId = item.ReferenceTarget?.Id;
            section.ProjectionKind = item.ProjectionKind;
            section.SortOrder = item.SortOrder;
            if (item.Id is null) dbContext.PortalPageSections.Add(section);
        }
        page.Title = request.Title!.Trim();
        page.PrimaryTargetType = request.PrimaryTarget.Type;
        page.PrimaryTargetId = request.PrimaryTarget.Id;
        Touch(page, actor);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict<AdminPortalPageDetailResponse>();
        }
        return await ReloadPage(pageId, cancellationToken);
    }

    public async Task<AdminPortalCommandResult<object>> DeletePageAsync(
        long pageId,
        AdminPortalConcurrencyRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var tokenError = DecodeToken(request.ConcurrencyToken, out var expectedVersion);
        if (tokenError is not null)
            return Validation<object>(new Dictionary<string, string[]> { ["concurrencyToken"] = [tokenError] });
        var page = await dbContext.PortalPages.Include(item => item.Sections)
            .SingleOrDefaultAsync(item => item.Id == pageId, cancellationToken);
        if (page is null) return NotFound<object>();
        if (page.Version != expectedVersion) return Conflict<object>();
        if (page.IsPublished) return InvalidState<object>("请先取消发布，再删除页面。");
        if (await dbContext.PortalPageNodes.AnyAsync(node => node.PortalPageId == pageId, cancellationToken))
            return ReferenceInvalid<object>("页面仍有导航位置，请先移除所有页面节点。");
        var now = timeProvider.GetUtcNow();
        dbContext.PortalPageSections.RemoveRange(page.Sections);
        page.IsDeleted = true;
        page.DeletedAt = now;
        page.DeletedByUserId = actor.UserId;
        page.DeletedByDisplayName = actor.DisplayName;
        Touch(page, actor);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict<object>(); }
        return new(AdminPortalFailure.None, new object());
    }

    public async Task<AdminPortalCommandResult<AdminPortalPageDetailResponse>> PublishPageAsync(
        long pageId,
        AdminPortalConcurrencyRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadPageForStateChange(pageId, request.ConcurrencyToken, cancellationToken);
        if (loaded.Result is not null) return loaded.Result;
        var page = loaded.Page!;
        if (page.IsPublished) return InvalidState<AdminPortalPageDetailResponse>("页面已经发布。");
        var publicationReadiness = await readiness.EvaluateAsync(page, cancellationToken);
        if (!publicationReadiness.CanPublish)
            return new(AdminPortalFailure.ReferenceInvalid, Message: "页面未通过发布检查。", Readiness: publicationReadiness);
        var now = timeProvider.GetUtcNow();
        page.IsPublished = true;
        page.PublishedAt = now;
        page.PublishedByUserId = actor.UserId;
        page.PublishedByDisplayName = actor.DisplayName;
        Touch(page, actor);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict<AdminPortalPageDetailResponse>(); }
        return await ReloadPage(pageId, cancellationToken);
    }

    public async Task<AdminPortalCommandResult<AdminPortalPageDetailResponse>> UnpublishPageAsync(
        long pageId,
        AdminPortalConcurrencyRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadPageForStateChange(pageId, request.ConcurrencyToken, cancellationToken);
        if (loaded.Result is not null) return loaded.Result;
        var page = loaded.Page!;
        if (!page.IsPublished) return InvalidState<AdminPortalPageDetailResponse>("页面当前未发布。");
        var now = timeProvider.GetUtcNow();
        page.IsPublished = false;
        page.UnpublishedAt = now;
        page.UnpublishedByUserId = actor.UserId;
        page.UnpublishedByDisplayName = actor.DisplayName;
        Touch(page, actor);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict<AdminPortalPageDetailResponse>(); }
        return await ReloadPage(pageId, cancellationToken);
    }

    public async Task<AdminPortalCommandResult<AdminPortalTreeNodeResponse>> CreateNodeAsync(
        CreateAdminPortalNodeRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var candidate = new PortalPageNode
        {
            Title = request.Title ?? string.Empty,
            NodeKind = request.NodeKind,
            ParentId = request.ParentId,
            PortalPageId = request.PortalPageId,
            SortOrder = request.SortOrder,
        };
        var errors = (await validator.ValidateNodePlacementAsync(candidate, cancellationToken, isNew: true)).ToDictionary();
        if (errors.Count > 0) return Validation<AdminPortalTreeNodeResponse>(errors);
        var now = timeProvider.GetUtcNow();
        candidate.Title = candidate.Title.Trim();
        candidate.CreatedAt = now;
        candidate.CreatedByUserId = actor.UserId;
        candidate.CreatedByDisplayName = actor.DisplayName;
        candidate.UpdatedAt = now;
        candidate.UpdatedByUserId = actor.UserId;
        candidate.UpdatedByDisplayName = actor.DisplayName;
        dbContext.PortalPageNodes.Add(candidate);
        await dbContext.SaveChangesAsync(cancellationToken);
        return await ReloadNode(candidate.Id, cancellationToken);
    }

    public async Task<AdminPortalCommandResult<AdminPortalTreeNodeResponse>> UpdateNodeAsync(
        long nodeId,
        UpdateAdminPortalNodeRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var tokenError = DecodeToken(request.ConcurrencyToken, out var expectedVersion);
        if (tokenError is not null)
            return Validation<AdminPortalTreeNodeResponse>(new Dictionary<string, string[]> { ["concurrencyToken"] = [tokenError] });
        var node = await dbContext.PortalPageNodes.SingleOrDefaultAsync(item => item.Id == nodeId, cancellationToken);
        if (node is null) return NotFound<AdminPortalTreeNodeResponse>();
        if (node.Version != expectedVersion) return Conflict<AdminPortalTreeNodeResponse>();
        if (node.IsPublished) return InvalidState<AdminPortalTreeNodeResponse>("请先取消发布，再修改节点。");
        if (node.NodeKind != request.NodeKind)
            return Validation<AdminPortalTreeNodeResponse>(new Dictionary<string, string[]> { ["nodeKind"] = ["不能修改已有节点的类型。"] });
        var candidate = new PortalPageNode
        {
            Id = node.Id,
            Title = request.Title ?? string.Empty,
            NodeKind = request.NodeKind,
            ParentId = request.ParentId,
            PortalPageId = request.PortalPageId,
            SortOrder = request.SortOrder,
        };
        var errors = await validator.ValidateNodePlacementAsync(candidate, cancellationToken);
        if (errors.Count > 0) return Validation<AdminPortalTreeNodeResponse>(errors);
        node.Title = candidate.Title.Trim();
        node.ParentId = candidate.ParentId;
        node.PortalPageId = candidate.PortalPageId;
        node.SortOrder = candidate.SortOrder;
        Touch(node, actor);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict<AdminPortalTreeNodeResponse>(); }
        return await ReloadNode(node.Id, cancellationToken);
    }

    public async Task<AdminPortalCommandResult<AdminPortalTreeResponse>> ReorderNodesAsync(
        ReorderAdminPortalNodesRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (request.ParentId is not null && !ApiIdParser.IsSafePositive(request.ParentId.Value))
            errors["parentId"] = ["父节点 ID 必须是 JavaScript 安全范围内的正整数。"];
        if (request.Items is null || request.Items.Count == 0)
            errors["items"] = ["必须提交完整的同级节点列表。"];
        else if (request.Items.Select(item => item.Id).Distinct().Count() != request.Items.Count)
            errors["items"] = ["节点 ID 不得重复。"];
        if (errors.Count > 0) return Validation<AdminPortalTreeResponse>(errors);

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var siblings = await dbContext.PortalPageNodes.Where(item => item.ParentId == request.ParentId).ToListAsync(cancellationToken);
        if (siblings.Count != request.Items!.Count || siblings.Select(item => item.Id).ToHashSet().SetEquals(request.Items.Select(item => item.Id)) is false)
            return ReferenceInvalid<AdminPortalTreeResponse>("必须一次提交完整且准确的同级节点集合。");
        if (siblings.Any(item => item.IsPublished))
            return InvalidState<AdminPortalTreeResponse>("请先取消发布同级节点，再调整顺序。");
        var byId = siblings.ToDictionary(item => item.Id);
        foreach (var item in request.Items)
        {
            var tokenError = DecodeToken(item.ConcurrencyToken, out var expectedVersion);
            if (tokenError is not null)
                return Validation<AdminPortalTreeResponse>(new Dictionary<string, string[]> { ["items.concurrencyToken"] = [tokenError] });
            if (byId[item.Id].Version != expectedVersion) return Conflict<AdminPortalTreeResponse>();
        }
        var maxOrder = siblings.Max(item => item.SortOrder);
        if (maxOrder > int.MaxValue - siblings.Count - 1)
            return Validation<AdminPortalTreeResponse>(new Dictionary<string, string[]> { ["items"] = ["节点顺序超出允许范围。"] });
        var offset = maxOrder + siblings.Count + 1;
        foreach (var sibling in siblings) sibling.SortOrder += offset;
        await dbContext.SaveChangesAsync(cancellationToken);
        for (var index = 0; index < request.Items.Count; index++)
        {
            var sibling = byId[request.Items[index].Id];
            sibling.SortOrder = index;
            Touch(sibling, actor);
        }
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException) { return Conflict<AdminPortalTreeResponse>(); }
        var tree = await queries.GetTreeAsync(cancellationToken);
        return new(tree.Failure, tree.Response, tree.FieldErrors);
    }

    public async Task<AdminPortalCommandResult<object>> DeleteNodeAsync(
        long nodeId,
        AdminPortalConcurrencyRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadNodeForStateChange(nodeId, request.ConcurrencyToken, cancellationToken);
        if (loaded.Result is not null) return new(loaded.Result.Failure, FieldErrors: loaded.Result.FieldErrors, Message: loaded.Result.Message);
        var node = loaded.Node!;
        if (node.IsPublished) return InvalidState<object>("请先取消发布，再移除节点。");
        if (node.NodeKind == PortalPageNodeKind.Folder
            && await dbContext.PortalPageNodes.AnyAsync(item => item.ParentId == node.Id, cancellationToken))
            return ReferenceInvalid<object>("目录下仍有节点，请先移动或移除子节点。");
        var now = timeProvider.GetUtcNow();
        node.IsDeleted = true;
        node.DeletedAt = now;
        node.DeletedByUserId = actor.UserId;
        node.DeletedByDisplayName = actor.DisplayName;
        Touch(node, actor);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict<object>(); }
        return new(AdminPortalFailure.None, new object());
    }

    public async Task<AdminPortalCommandResult<AdminPortalTreeNodeResponse>> PublishNodeAsync(
        long nodeId,
        AdminPortalConcurrencyRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadNodeForStateChange(nodeId, request.ConcurrencyToken, cancellationToken);
        if (loaded.Result is not null) return loaded.Result;
        var node = loaded.Node!;
        if (node.IsPublished) return InvalidState<AdminPortalTreeNodeResponse>("节点已经发布。");
        if (node.ParentId is not null)
        {
            var ancestors = await dbContext.PortalPageNodes.AsNoTracking().ToDictionaryAsync(item => item.Id, cancellationToken);
            var cursor = node.ParentId;
            var seen = new HashSet<long>();
            while (cursor is not null)
            {
                if (!seen.Add(cursor.Value)
                    || !ancestors.TryGetValue(cursor.Value, out var parent)
                    || !parent.IsPublished)
                    return InvalidState<AdminPortalTreeNodeResponse>("请先按从根目录到当前节点的顺序发布所有上级目录。");
                cursor = parent.ParentId;
            }
        }
        if (node.NodeKind == PortalPageNodeKind.Page)
        {
            var page = await dbContext.PortalPages.AsNoTracking().Include(item => item.Sections)
                .SingleOrDefaultAsync(item => item.Id == node.PortalPageId, cancellationToken);
            if (page is null || !page.IsPublished)
                return InvalidState<AdminPortalTreeNodeResponse>("请先发布页面内容。");
            var pageReadiness = await readiness.EvaluateAsync(page, cancellationToken);
            if (!pageReadiness.CanPublish)
                return new(AdminPortalFailure.ReferenceInvalid, Message: "页面当前不满足门户可见条件。", Readiness: pageReadiness);
        }
        var now = timeProvider.GetUtcNow();
        node.IsPublished = true;
        node.PublishedAt = now;
        node.PublishedByUserId = actor.UserId;
        node.PublishedByDisplayName = actor.DisplayName;
        Touch(node, actor);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict<AdminPortalTreeNodeResponse>(); }
        return await ReloadNode(node.Id, cancellationToken);
    }

    public async Task<AdminPortalCommandResult<AdminPortalTreeNodeResponse>> UnpublishNodeAsync(
        long nodeId,
        AdminPortalConcurrencyRequest request,
        PortalCommandActor actor,
        CancellationToken cancellationToken)
    {
        var loaded = await LoadNodeForStateChange(nodeId, request.ConcurrencyToken, cancellationToken);
        if (loaded.Result is not null) return loaded.Result;
        var node = loaded.Node!;
        if (!node.IsPublished) return InvalidState<AdminPortalTreeNodeResponse>("节点当前未发布。");
        var now = timeProvider.GetUtcNow();
        node.IsPublished = false;
        node.UnpublishedAt = now;
        node.UnpublishedByUserId = actor.UserId;
        node.UnpublishedByDisplayName = actor.DisplayName;
        Touch(node, actor);
        try { await dbContext.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return Conflict<AdminPortalTreeNodeResponse>(); }
        return await ReloadNode(node.Id, cancellationToken);
    }

    private async Task<(PortalPage? Page, AdminPortalCommandResult<AdminPortalPageDetailResponse>? Result)> LoadPageForStateChange(
        long pageId,
        string? token,
        CancellationToken cancellationToken)
    {
        var tokenError = DecodeToken(token, out var expectedVersion);
        if (tokenError is not null)
            return (null, Validation<AdminPortalPageDetailResponse>(new Dictionary<string, string[]> { ["concurrencyToken"] = [tokenError] }));
        var page = await dbContext.PortalPages.Include(item => item.Sections)
            .SingleOrDefaultAsync(item => item.Id == pageId, cancellationToken);
        if (page is null) return (null, NotFound<AdminPortalPageDetailResponse>());
        return page.Version == expectedVersion
            ? (page, null)
            : (null, Conflict<AdminPortalPageDetailResponse>());
    }

    private async Task<(PortalPageNode? Node, AdminPortalCommandResult<AdminPortalTreeNodeResponse>? Result)> LoadNodeForStateChange(
        long nodeId,
        string? token,
        CancellationToken cancellationToken)
    {
        var tokenError = DecodeToken(token, out var expectedVersion);
        if (tokenError is not null)
            return (null, Validation<AdminPortalTreeNodeResponse>(new Dictionary<string, string[]> { ["concurrencyToken"] = [tokenError] }));
        var node = await dbContext.PortalPageNodes.SingleOrDefaultAsync(item => item.Id == nodeId, cancellationToken);
        if (node is null) return (null, NotFound<AdminPortalTreeNodeResponse>());
        return node.Version == expectedVersion ? (node, null) : (null, Conflict<AdminPortalTreeNodeResponse>());
    }

    private async Task<AdminPortalCommandResult<AdminPortalPageDetailResponse>> ReloadPage(long pageId, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var result = await queries.GetPageAsync(pageId, cancellationToken);
        return new(result.Failure, result.Response, result.FieldErrors);
    }

    private async Task<AdminPortalCommandResult<AdminPortalTreeNodeResponse>> ReloadNode(long nodeId, CancellationToken cancellationToken)
    {
        dbContext.ChangeTracker.Clear();
        var tree = await queries.GetTreeAsync(cancellationToken);
        var node = tree.Response?.Items.SingleOrDefault(item => item.NodeId == nodeId);
        return node is null ? NotFound<AdminPortalTreeNodeResponse>() : new(AdminPortalFailure.None, node);
    }

    private static Dictionary<string, string[]> ValidatePageRequest(string? title, AdminPortalTargetReferenceRequest? target)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(title) || title.Trim().Length > 200)
            errors["title"] = ["页面标题必须为 1 至 200 个字符。"];
        if (target is null || !Enum.IsDefined(target.Type))
            errors["primaryTarget.type"] = ["请选择有效的主知识类型。"];
        if (target is null || !ApiIdParser.IsSafePositive(target.Id))
            errors["primaryTarget.id"] = ["主知识对象 ID 必须是 JavaScript 安全范围内的正整数。"];
        return errors;
    }

    private string? DecodeToken(string? token, out long version)
    {
        if (tokenCodec.TryDecode(token, out version)) return null;
        return "并发标记无效，请重新加载后重试。";
    }

    private void Touch(PortalPage page, PortalCommandActor actor)
    {
        page.UpdatedAt = timeProvider.GetUtcNow();
        page.UpdatedByUserId = actor.UserId;
        page.UpdatedByDisplayName = actor.DisplayName;
        page.Version++;
    }

    private void Touch(PortalPageNode node, PortalCommandActor actor)
    {
        node.UpdatedAt = timeProvider.GetUtcNow();
        node.UpdatedByUserId = actor.UserId;
        node.UpdatedByDisplayName = actor.DisplayName;
        node.Version++;
    }

    private static AdminPortalCommandResult<T> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        new(AdminPortalFailure.Validation, FieldErrors: errors);
    private static AdminPortalCommandResult<T> NotFound<T>() => new(AdminPortalFailure.NotFound);
    private static AdminPortalCommandResult<T> Conflict<T>() => new(AdminPortalFailure.Conflict);
    private static AdminPortalCommandResult<T> InvalidState<T>(string message) => new(AdminPortalFailure.InvalidState, Message: message);
    private static AdminPortalCommandResult<T> ReferenceInvalid<T>(string message) => new(AdminPortalFailure.ReferenceInvalid, Message: message);
}
