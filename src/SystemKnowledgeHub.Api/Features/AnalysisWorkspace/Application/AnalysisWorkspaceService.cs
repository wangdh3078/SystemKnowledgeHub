using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Api.Contracts;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application.Models;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using static SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application.AnalysisTreeState;

namespace SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application;

public sealed class AnalysisWorkspaceService(KnowledgeHubDbContext db,
    ConcurrencyTokenCodec tokens, KnowledgeDocumentService documents, TimeProvider clock)
{
    public async Task<AnalysisTreeResponse> GetTree(CancellationToken cancellationToken)
    {
        // The node metadata, organization token and current document identities share one DB snapshot.
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var nodes = await LoadNodes(tracking: false, cancellationToken);
        return await Project(nodes, cancellationToken);
    }

    public async Task<AnalysisMutationResponse> CreateFolder(CreateAnalysisFolderRequest request, CancellationToken cancellationToken)
    {
        var title = FolderTitle(request.Title);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(db, cancellationToken);
        var nodes = await LoadForWrite(request.TreeConcurrencyToken, cancellationToken);
        CheckParent(nodes, request.ParentId);
        var node = NewNode(request.ParentId, AnalysisNodeType.Folder, title, null);
        await Append(nodes, node, cancellationToken);
        return await Complete(nodes, node.Id, transaction, cancellationToken);
    }

    public async Task<AnalysisMutationResponse> RenameFolder(long id, RenameAnalysisFolderRequest request, CancellationToken cancellationToken)
    {
        var title = FolderTitle(request.Title);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(db, cancellationToken);
        var nodes = await LoadForWrite(request.TreeConcurrencyToken, cancellationToken);
        var node = RequireNode(nodes, id, request.NodeConcurrencyToken);
        RequireType(node, AnalysisNodeType.Folder);
        if (!string.Equals(node.Title, title, StringComparison.Ordinal))
        {
            Touch(node);
            node.Title = title;
            await db.SaveChangesAsync(cancellationToken);
        }
        return await Complete(nodes, node.Id, transaction, cancellationToken);
    }

    public async Task<AnalysisMutationResponse> AddPlacement(AddAnalysisPlacementRequest request, CancellationToken cancellationToken)
    {
        CheckId(request.KnowledgeDocumentId, "knowledgeDocumentId");
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(db, cancellationToken);
        var nodes = await LoadForWrite(request.TreeConcurrencyToken, cancellationToken);
        CheckParent(nodes, request.ParentId);
        if (!await db.KnowledgeDocuments.AnyAsync(document => document.Id == request.KnowledgeDocumentId, cancellationToken))
            throw new AnalysisRequestException("reference_invalid", "知识文档不存在或当前不可用。");
        if (nodes.Any(node => node.KnowledgeDocumentId == request.KnowledgeDocumentId))
            throw Rule("该知识文档已在分析目录中。");
        var node = NewNode(request.ParentId, AnalysisNodeType.Document, null, request.KnowledgeDocumentId);
        await Append(nodes, node, cancellationToken);
        return await Complete(nodes, node.Id, transaction, cancellationToken);
    }

    public async Task<AnalysisMutationResponse> CreateDocument(CreateAnalysisDocumentRequest request,
        KnowledgeDocumentAuthor author, CancellationToken cancellationToken)
    {
        var documentType = request.DocumentType ?? "DesignNote";
        if (documentType is not ("DesignNote" or "KnowledgeArticle"))
            throw Invalid("documentType", "新建分析文档只允许设计说明或知识文章。");
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(db, cancellationToken);
        var nodes = await LoadForWrite(request.TreeConcurrencyToken, cancellationToken);
        CheckParent(nodes, request.ParentId);
        // Validate capacity/depth before canonical creation, then re-use its exact validation,
        // actor attribution, revision and FTS writes inside this existing outer transaction.
        var node = NewNode(request.ParentId, AnalysisNodeType.Document, null, null);
        Validate(nodes.Append(node).ToList());
        var created = await documents.Create(new CreateKnowledgeDocumentCommand(documentType,
            request.Title ?? string.Empty, request.Summary, request.BodyMarkdown, author), cancellationToken);
        if (created.Failure == KnowledgeDocumentWriteFailure.Validation)
            throw new AnalysisRequestException("validation_error", "文档内容不符合要求。", created.FieldErrors);
        if (created.Failure != KnowledgeDocumentWriteFailure.None || created.Response is null)
            throw new InvalidOperationException("Unexpected canonical document creation result.");
        node.KnowledgeDocumentId = created.Response.Id;
        await Append(nodes, node, cancellationToken);
        return await Complete(nodes, node.Id, transaction, cancellationToken);
    }

    public async Task<AnalysisMutationResponse> Move(long id, MoveAnalysisNodeRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(db, cancellationToken);
        var nodes = await LoadForWrite(request.TreeConcurrencyToken, cancellationToken);
        var node = RequireNode(nodes, id, request.NodeConcurrencyToken);
        CheckParent(nodes, request.TargetParentId);
        var sourceParent = node.ParentId;
        var source = Children(nodes, sourceParent).Where(item => item.Id != id).ToList();
        var target = sourceParent == request.TargetParentId
            ? source : Children(nodes, request.TargetParentId);
        if (request.TargetPosition < 0 || request.TargetPosition > target.Count)
            throw Invalid("targetPosition", "目标位置必须位于移出源节点后的同级列表范围内。");
        node.ParentId = request.TargetParentId;
        try { Validate(nodes); }
        finally { node.ParentId = sourceParent; }
        target.Insert(request.TargetPosition, node);
        var desired = Ordered(target, request.TargetParentId);
        if (sourceParent != request.TargetParentId)
            foreach (var pair in Ordered(source, sourceParent)) desired.Add(pair.Key, pair.Value);
        await SaveOrder(nodes, desired, cancellationToken);
        return await Complete(nodes, node.Id, transaction, cancellationToken);
    }

    public async Task<AnalysisMutationResponse> Reorder(ReorderAnalysisChildrenRequest request, CancellationToken cancellationToken)
    {
        if (request.Items is null || request.Items.Count > AnalysisLimits.MaxNodes
            || request.Items.Any(item => item is null)
            || request.Items.Select(item => item.Id).Distinct().Count() != request.Items.Count)
            throw Invalid("items", "必须提交完整且不重复的同级节点列表。");
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(db, cancellationToken);
        var nodes = await LoadForWrite(request.TreeConcurrencyToken, cancellationToken);
        CheckParent(nodes, request.ParentId);
        var siblings = Children(nodes, request.ParentId);
        if (!siblings.Select(node => node.Id).ToHashSet().SetEquals(request.Items.Select(item => item.Id)))
            throw new AnalysisRequestException("reference_invalid", "必须提交完整且准确的同级节点集合。");
        var ordered = request.Items.Select(item => RequireNode(nodes, item.Id, item.NodeConcurrencyToken)).ToList();
        await SaveOrder(nodes, Ordered(ordered, request.ParentId), cancellationToken);
        return await Complete(nodes, null, transaction, cancellationToken);
    }

    public async Task<AnalysisMutationResponse> RemovePlacement(long id, RemoveAnalysisNodeRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(db, cancellationToken);
        var nodes = await LoadForWrite(request.TreeConcurrencyToken, cancellationToken);
        var node = RequireNode(nodes, id, request.NodeConcurrencyToken);
        RequireType(node, AnalysisNodeType.Document);
        await RemoveNode(nodes, node, cancellationToken);
        return await Complete(nodes, null, transaction, cancellationToken);
    }

    public async Task<AnalysisMutationResponse> DeleteFolder(long id, RemoveAnalysisNodeRequest request, CancellationToken cancellationToken)
    {
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(db, cancellationToken);
        var nodes = await LoadForWrite(request.TreeConcurrencyToken, cancellationToken);
        var node = RequireNode(nodes, id, request.NodeConcurrencyToken);
        RequireType(node, AnalysisNodeType.Folder);
        if (nodes.Any(child => child.ParentId == id)) throw Rule("只能删除空目录，请先移动或移除子节点。");
        await RemoveNode(nodes, node, cancellationToken);
        return await Complete(nodes, null, transaction, cancellationToken);
    }

    private async Task<List<AnalysisNode>> LoadNodes(bool tracking, CancellationToken cancellationToken)
    {
        var query = tracking ? db.AnalysisNodes : db.AnalysisNodes.AsNoTracking();
        var nodes = await query.OrderBy(node => node.Id).Take(AnalysisLimits.MaxNodes + 1).ToListAsync(cancellationToken);
        Validate(nodes); // Reject an oversized/corrupt tree, never return a truncated editable snapshot.
        return nodes;
    }

    private async Task<List<AnalysisNode>> LoadForWrite(string? treeToken, CancellationToken cancellationToken)
    {
        if (!IsToken(treeToken)) throw Invalid("treeConcurrencyToken", "目录并发标记无效，请重新加载。");
        var nodes = await LoadNodes(tracking: true, cancellationToken);
        if (!string.Equals(Token(nodes), treeToken, StringComparison.Ordinal)) throw Conflict();
        return nodes;
    }

    private async Task<AnalysisTreeResponse> Project(List<AnalysisNode> nodes, CancellationToken cancellationToken)
    {
        var ids = nodes.Where(node => node.KnowledgeDocumentId.HasValue).Select(node => node.KnowledgeDocumentId!.Value).ToArray();
        var identities = await db.KnowledgeDocuments.AsNoTracking().Where(document => ids.Contains(document.Id))
            .Select(document => new { document.Id, document.Title, document.DocumentType, document.LifecycleStatus })
            .ToDictionaryAsync(document => document.Id, cancellationToken);
        var items = nodes.OrderBy(node => node.ParentId).ThenBy(node => node.SortOrder).ThenBy(node => node.Id).Select(node =>
        {
            var document = node.KnowledgeDocumentId is long documentId ? identities.GetValueOrDefault(documentId) : null;
            var folder = node.NodeType == AnalysisNodeType.Folder;
            return new AnalysisNodeResponse(node.Id, node.ParentId, node.NodeType.ToString(), node.SortOrder,
                tokens.Encode(node.Version), folder ? node.Title! : document?.Title ?? "文档不可用",
                node.KnowledgeDocumentId, document?.DocumentType.ToString(), document?.LifecycleStatus.ToString(),
                folder || document is not null ? "Available" : "Unavailable", node.CreatedAt, node.UpdatedAt);
        }).ToArray();
        return new(items, Token(nodes));
    }

    private async Task<AnalysisMutationResponse> Complete(List<AnalysisNode> nodes, long? selectedId,
        SqliteImmediateTransaction transaction, CancellationToken cancellationToken)
    {
        var response = await Project(nodes, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(response.Items.SingleOrDefault(node => node.Id == selectedId), response.Items, response.TreeConcurrencyToken);
    }

    private AnalysisNode NewNode(long? parentId, AnalysisNodeType type, string? title, long? documentId) => new()
    {
        ParentId = parentId, NodeType = type, Title = title, KnowledgeDocumentId = documentId,
        CreatedAt = clock.GetUtcNow(), UpdatedAt = clock.GetUtcNow(), Version = 1,
    };

    private async Task Append(List<AnalysisNode> nodes, AnalysisNode node, CancellationToken cancellationToken)
    {
        var siblings = Children(nodes, node.ParentId);
        siblings.Add(node);
        nodes.Add(node);
        Validate(nodes);
        db.AnalysisNodes.Add(node);
        await SaveOrder(nodes, Ordered(siblings, node.ParentId), cancellationToken);
    }

    private async Task RemoveNode(List<AnalysisNode> nodes, AnalysisNode node, CancellationToken cancellationToken)
    {
        db.AnalysisNodes.Remove(node);
        await db.SaveChangesAsync(cancellationToken);
        nodes.Remove(node);
        await SaveOrder(nodes, Ordered(Children(nodes, node.ParentId), node.ParentId), cancellationToken);
    }

    private async Task SaveOrder(List<AnalysisNode> nodes,
        Dictionary<AnalysisNode, (long? ParentId, int Order)> desired, CancellationToken cancellationToken)
    {
        var changed = desired.Where(pair => pair.Key.Id == 0 || pair.Key.ParentId != pair.Value.ParentId
            || pair.Key.SortOrder != pair.Value.Order).ToArray();
        if (changed.Length == 0) return;
        // A globally disjoint temporary range also handles nodes crossing between two parents.
        var maximum = nodes.Count == 0 ? 0 : nodes.Max(node => node.SortOrder);
        if ((long)maximum + changed.Length > int.MaxValue) throw Rule("目录顺序超出允许范围。");
        if (changed.Any(pair => pair.Key.Version == long.MaxValue)) throw Rule("节点版本超出允许范围。");
        var existing = changed.Where(pair => pair.Key.Id != 0).Select(pair => pair.Key).ToHashSet();
        for (var index = 0; index < changed.Length; index++) changed[index].Key.SortOrder = maximum + index + 1;
        await db.SaveChangesAsync(cancellationToken);
        foreach (var (node, placement) in changed)
        {
            node.ParentId = placement.ParentId;
            node.SortOrder = placement.Order;
            if (existing.Contains(node)) Touch(node);
        }
        await db.SaveChangesAsync(cancellationToken);
    }

    private static Dictionary<AnalysisNode, (long? ParentId, int Order)> Ordered(List<AnalysisNode> nodes, long? parentId) =>
        nodes.Select((node, index) => (node, index)).ToDictionary(item => item.node, item => (parentId, item.index));

    private static List<AnalysisNode> Children(List<AnalysisNode> nodes, long? parentId) =>
        nodes.Where(node => node.ParentId == parentId).OrderBy(node => node.SortOrder).ThenBy(node => node.Id).ToList();

    private AnalysisNode RequireNode(List<AnalysisNode> nodes, long id, string? token)
    {
        CheckId(id, "id");
        if (!tokens.TryDecode(token, out var version)) throw Invalid("nodeConcurrencyToken", "节点并发标记无效。");
        var node = nodes.SingleOrDefault(node => node.Id == id)
            ?? throw new AnalysisRequestException("not_found", "未找到指定分析节点。");
        if (node.Version != version) throw Conflict();
        return node;
    }

    private static void CheckParent(List<AnalysisNode> nodes, long? parentId)
    {
        if (parentId is null) return;
        CheckId(parentId.Value, "parentId");
        if (!nodes.Any(node => node.Id == parentId && node.NodeType == AnalysisNodeType.Folder))
            throw new AnalysisRequestException("reference_invalid", "父节点必须是存在的分析目录。");
    }

    private static void RequireType(AnalysisNode node, AnalysisNodeType expected)
    {
        if (node.NodeType != expected) throw Rule(expected == AnalysisNodeType.Folder ? "该操作只允许目录节点。" : "该操作只允许文档位置。");
    }

    private void Touch(AnalysisNode node)
    {
        if (node.Version == long.MaxValue) throw Rule("节点版本超出允许范围。");
        node.Version++;
        node.UpdatedAt = clock.GetUtcNow();
    }

    private static string FolderTitle(string? value)
    {
        var title = value?.Trim();
        if (string.IsNullOrEmpty(title) || title.Length > 200 || title.Contains('\0'))
            throw Invalid("title", "目录标题必须为 1 到 200 个字符，且不能包含 NUL。");
        return title;
    }

    private static void CheckId(long id, string field)
    {
        if (!ApiIdParser.IsSafePositive(id)) throw Invalid(field, "ID 必须为安全范围内的正整数。");
    }

    private static AnalysisRequestException Invalid(string field, string message) =>
        new("validation_error", message, new Dictionary<string, string[]> { [field] = [message] });
    private static AnalysisRequestException Conflict() => new("conflict", "目录或节点已被其它操作修改，请重新加载后重试。");
}
