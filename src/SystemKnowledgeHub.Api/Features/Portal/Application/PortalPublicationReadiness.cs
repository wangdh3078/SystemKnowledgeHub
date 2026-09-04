using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public sealed class PortalPublicationReadiness(
    KnowledgeHubDbContext dbContext,
    PortalTargetResolver targetResolver)
{
    public async Task<AdminPortalReadinessResponse> EvaluateAsync(
        PortalPage page,
        CancellationToken cancellationToken)
    {
        var checks = new List<AdminPortalReadinessItemResponse>();
        var blockers = new List<AdminPortalReadinessItemResponse>();
        var warnings = new List<AdminPortalReadinessItemResponse>();

        var validation = PortalCompositionValidator.ValidatePage(page);
        if (validation.Count == 0)
            checks.Add(new("composition_valid", "页面与章节配置有效。"));
        else
            blockers.Add(new("composition_invalid", "页面或章节配置无效，请修正后再发布。"));

        if (page.Sections.All(section => PortalCompositionValidator.IsReadableProjection(section.ProjectionKind)))
            checks.Add(new("projections_supported", "所有章节均可在当前门户中展示。"));
        else
            blockers.Add(new("projection_unsupported", "存在当前版本暂不支持的章节，请删除或更换章节类型。"));

        var keys = GetTargetKeys(page).Distinct().ToArray();
        var adminTargets = await targetResolver.ResolveAdminIdentitiesAsync(keys, cancellationToken);
        var primaryKey = new PortalTargetKey(page.PrimaryTargetType, page.PrimaryTargetId);
        if (!adminTargets.ContainsKey(primaryKey))
            blockers.Add(new("primary_target_missing", "主知识对象已失效，请重新选择。"));
        else
            checks.Add(new("primary_target_valid", "主知识对象有效。"));

        var missingReferences = page.Sections
            .Where(section => section.SourceKind == PortalPageSectionSourceKind.ExplicitReference)
            .Select(section => new PortalTargetKey(section.ReferenceTargetType!.Value, section.ReferenceTargetId!.Value))
            .Where(key => !adminTargets.ContainsKey(key))
            .Distinct()
            .ToArray();
        if (missingReferences.Length > 0)
            blockers.Add(new("explicit_reference_missing", "存在已失效的章节引用，请删除或更换引用。"));
        else
            checks.Add(new("explicit_references_valid", "章节引用目标有效。"));

        var documentIds = keys
            .Where(key => key.Type == PortalTargetType.KnowledgeDocument && adminTargets.ContainsKey(key))
            .Select(key => key.Id)
            .Distinct()
            .ToArray();
        var documentStates = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(document => documentIds.Contains(document.Id))
            .Select(document => new { document.Id, document.Title, document.DocumentType, document.LifecycleStatus })
            .ToListAsync(cancellationToken);
        foreach (var document in documentStates.Where(document => document.LifecycleStatus != DocumentLifecycleStatus.Published))
        {
            var lifecycle = document.LifecycleStatus == DocumentLifecycleStatus.Draft ? "草稿" : "已归档";
            blockers.Add(new(
                "knowledge_document_not_published",
                $"知识文档《{document.Title}》当前为{lifecycle}，请先发布该文档。"));
        }
        if (documentStates.All(document => document.LifecycleStatus == DocumentLifecycleStatus.Published))
            checks.Add(new("knowledge_documents_published", "引用的知识文档均已发布。"));

        if (page.Sections.Any(section => section.ProjectionKind == PortalPageProjectionKind.Traceability))
        {
            var primaryDocument = documentStates.SingleOrDefault(document => document.Id == page.PrimaryTargetId);
            if (page.PrimaryTargetType != PortalTargetType.KnowledgeDocument
                || primaryDocument is null
                || primaryDocument.DocumentType is not (DocumentType.Requirement or DocumentType.Specification or DocumentType.TestCase))
                blockers.Add(new("traceability_target_invalid", "追溯区块仅支持需求、规格或测试用例知识文档作为主目标。"));
            else
                checks.Add(new("traceability_target_valid", "追溯区块主目标有效。"));
        }

        var nodes = await dbContext.PortalPageNodes.AsNoTracking().ToListAsync(cancellationToken);
        var placements = nodes.Where(node => node.NodeKind == PortalPageNodeKind.Page && node.PortalPageId == page.Id).ToArray();
        var byId = nodes.ToDictionary(node => node.Id);
        var publishedPlacements = placements.Count(node => IsEffectivePublished(node, byId));
        if (publishedPlacements == 0)
            warnings.Add(new("no_published_placement", "页面尚未放入已发布的导航路径；发布页面内容后仍需发布对应目录和页面节点。"));
        else
            checks.Add(new("published_placement_available", $"页面已有 {publishedPlacements} 个可见导航位置。"));

        return new(blockers.Count == 0, checks, blockers, warnings);
    }

    public static AdminPortalHealthResponse ToHealth(AdminPortalReadinessResponse readiness)
    {
        var blocker = readiness.Blockers.FirstOrDefault();
        if (blocker is not null)
            return new(blocker.Code, blocker.Message, false);
        var warning = readiness.Warnings.FirstOrDefault();
        if (warning is not null)
            return new(warning.Code, warning.Message, false);
        return new("healthy", "正常", true);
    }

    public static IEnumerable<PortalTargetKey> GetTargetKeys(PortalPage page)
    {
        yield return new(page.PrimaryTargetType, page.PrimaryTargetId);
        foreach (var section in page.Sections.Where(section => section.SourceKind == PortalPageSectionSourceKind.ExplicitReference))
            yield return new(section.ReferenceTargetType!.Value, section.ReferenceTargetId!.Value);
    }

    public static bool IsEffectivePublished(
        PortalPageNode node,
        IReadOnlyDictionary<long, PortalPageNode> byId)
    {
        if (!node.IsPublished) return false;
        var seen = new HashSet<long> { node.Id };
        var cursor = node;
        while (cursor.ParentId is not null)
        {
            if (!seen.Add(cursor.ParentId.Value)
                || !byId.TryGetValue(cursor.ParentId.Value, out var parent)
                || !parent.IsPublished)
                return false;
            cursor = parent;
        }
        return true;
    }
}
