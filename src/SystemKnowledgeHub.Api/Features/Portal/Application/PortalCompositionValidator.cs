using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public sealed class PortalCompositionValidator(KnowledgeHubDbContext dbContext)
{
    public static IReadOnlyDictionary<string, string[]> ValidatePage(PortalPage page)
    {
        var errors = new Dictionary<string, string[]>();
        if (page.Id < 1 || page.Id > ApiIdParser.JavaScriptMaxSafeInteger)
            errors["id"] = ["ID 必须是 JavaScript 安全范围内的正整数。"];
        if (string.IsNullOrWhiteSpace(page.Title) || page.Title.Trim().Length > 200)
            errors["title"] = ["页面标题必须为 1 至 200 个字符。"];
        if (!Enum.IsDefined(page.PrimaryTargetType))
            errors["primaryTargetType"] = ["主目标类型无效。"];
        if (page.PrimaryTargetId < 1 || page.PrimaryTargetId > ApiIdParser.JavaScriptMaxSafeInteger)
            errors["primaryTargetId"] = ["主目标 ID 必须是 JavaScript 安全范围内的正整数。"];

        foreach (var pair in ValidateSections(page.PrimaryTargetType, page.Sections))
            errors[pair.Key] = pair.Value;
        return errors;
    }

    public static IReadOnlyDictionary<string, string[]> ValidateSections(
        PortalTargetType primaryTargetType,
        IEnumerable<PortalPageSection> sections)
    {
        var sectionList = sections.ToList();
        var errors = new Dictionary<string, string[]>();
        if (sectionList.Count > PortalLimits.MaximumSectionsPerPage)
            errors["sections"] = [$"每个页面最多允许 {PortalLimits.MaximumSectionsPerPage} 个区块。"];
        if (sectionList.Count(section => section.ProjectionKind == PortalPageProjectionKind.KnowledgeDocumentBody)
            > PortalLimits.MaximumKnowledgeDocumentBodySectionsPerPage)
            errors["sections"] = [$"每个页面最多允许 {PortalLimits.MaximumKnowledgeDocumentBodySectionsPerPage} 个完整知识文档正文区块。"];
        if (sectionList.Select(section => section.SortOrder).Distinct().Count() != sectionList.Count)
            errors["sections.sortOrder"] = ["页面内区块顺序必须唯一。"];

        for (var index = 0; index < sectionList.Count; index++)
        {
            var section = sectionList[index];
            var prefix = $"sections[{index}]";
            if (string.IsNullOrWhiteSpace(section.Heading) || section.Heading.Trim().Length > 200)
                errors[$"{prefix}.heading"] = ["区块标题必须为 1 至 200 个字符。"];
            if (section.SortOrder < 0)
                errors[$"{prefix}.sortOrder"] = ["区块顺序不得为负数。"];
            if (!Enum.IsDefined(section.SourceKind))
                errors[$"{prefix}.sourceKind"] = ["区块来源类型无效。"];
            if (!Enum.IsDefined(section.ProjectionKind))
                errors[$"{prefix}.projectionKind"] = ["区块投影类型无效。"];

            var referenceShapeValid = section.SourceKind == PortalPageSectionSourceKind.ExplicitReference
                ? section.ReferenceTargetType is not null
                    && Enum.IsDefined(section.ReferenceTargetType.Value)
                    && section.ReferenceTargetId is >= 1 and <= ApiIdParser.JavaScriptMaxSafeInteger
                : section.ReferenceTargetType is null && section.ReferenceTargetId is null;
            if (!referenceShapeValid)
                errors[$"{prefix}.reference"] = ["区块引用与来源类型不匹配。"];

            var targetType = section.SourceKind switch
            {
                PortalPageSectionSourceKind.PrimaryTarget => primaryTargetType,
                PortalPageSectionSourceKind.ExplicitReference => section.ReferenceTargetType,
                _ => null,
            };
            if (!IsProjectionCompatible(section.SourceKind, section.ProjectionKind, targetType))
                errors[$"{prefix}.projectionKind"] = ["区块投影与来源或目标类型不兼容。"];
        }

        return errors;
    }

    public async Task<IReadOnlyDictionary<string, string[]>> ValidateNodePlacementAsync(
        PortalPageNode candidate,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (candidate.Id < 1 || candidate.Id > ApiIdParser.JavaScriptMaxSafeInteger)
            errors["id"] = ["ID 必须是 JavaScript 安全范围内的正整数。"];
        if (string.IsNullOrWhiteSpace(candidate.Title) || candidate.Title.Trim().Length > 200)
            errors["title"] = ["节点标题必须为 1 至 200 个字符。"];
        if (candidate.SortOrder < 0)
            errors["sortOrder"] = ["同级顺序不得为负数。"];
        if (!Enum.IsDefined(candidate.NodeKind)
            || (candidate.NodeKind == PortalPageNodeKind.Folder && candidate.PortalPageId is not null)
            || (candidate.NodeKind == PortalPageNodeKind.Page && candidate.PortalPageId is null))
            errors["nodeKind"] = ["Folder 不得关联页面，Page 必须关联页面。"];
        if (candidate.ParentId == candidate.Id)
            errors["parentId"] = ["节点不能成为自己的父节点。"];

        if (candidate.PortalPageId is not null
            && !await dbContext.PortalPages.AsNoTracking().AnyAsync(page => page.Id == candidate.PortalPageId, cancellationToken))
            errors["portalPageId"] = ["关联页面不存在。"];

        var nodes = await dbContext.PortalPageNodes.AsNoTracking().ToListAsync(cancellationToken);
        if (nodes.Any(node => node.Id != candidate.Id
            && node.ParentId == candidate.ParentId
            && node.SortOrder == candidate.SortOrder))
            errors["sortOrder"] = ["同级节点顺序必须唯一。"];

        if (candidate.ParentId is not null && nodes.All(node => node.Id != candidate.ParentId))
            errors["parentId"] = ["父节点不存在。"];

        var byId = nodes.ToDictionary(node => node.Id);
        var ancestors = new HashSet<long>();
        var cursor = candidate.ParentId;
        var depth = 1;
        while (cursor is not null)
        {
            if (cursor == candidate.Id || !ancestors.Add(cursor.Value))
            {
                errors["parentId"] = ["节点移动会形成循环。"];
                break;
            }
            if (!byId.TryGetValue(cursor.Value, out var parent)) break;
            cursor = parent.ParentId;
            depth++;
        }

        var childrenByParent = nodes.Where(node => node.Id != candidate.Id)
            .ToLookup(node => node.ParentId);
        var subtreeDepth = GetSubtreeDepth(candidate.Id, childrenByParent, new HashSet<long>());
        if (depth + subtreeDepth - 1 > PortalLimits.MaximumTreeDepth)
            errors["parentId"] = [$"知识树最大深度为 {PortalLimits.MaximumTreeDepth}。"];

        return errors;
    }

    public static bool IsB01ReadableProjection(PortalPageProjectionKind projectionKind) =>
        projectionKind is PortalPageProjectionKind.Summary
            or PortalPageProjectionKind.KnowledgeDocumentBody
            or PortalPageProjectionKind.StructuredOverview
            or PortalPageProjectionKind.DatabaseStructure;

    private static bool IsProjectionCompatible(
        PortalPageSectionSourceKind sourceKind,
        PortalPageProjectionKind projectionKind,
        PortalTargetType? targetType) => projectionKind switch
        {
            PortalPageProjectionKind.Summary => sourceKind is not PortalPageSectionSourceKind.Derived && targetType is not null,
            PortalPageProjectionKind.KnowledgeDocumentBody => sourceKind is not PortalPageSectionSourceKind.Derived
                && targetType == PortalTargetType.KnowledgeDocument,
            PortalPageProjectionKind.StructuredOverview => sourceKind is not PortalPageSectionSourceKind.Derived
                && targetType is PortalTargetType.System or PortalTargetType.BusinessFunction
                    or PortalTargetType.DatabaseObject or PortalTargetType.Integration,
            PortalPageProjectionKind.DatabaseStructure => sourceKind is not PortalPageSectionSourceKind.Derived
                && targetType == PortalTargetType.DatabaseObject,
            PortalPageProjectionKind.AttachmentList => sourceKind is not PortalPageSectionSourceKind.Derived
                && targetType == PortalTargetType.KnowledgeDocument,
            PortalPageProjectionKind.TrustSummary => true,
            PortalPageProjectionKind.RelatedKnowledge => sourceKind == PortalPageSectionSourceKind.Derived,
            PortalPageProjectionKind.Traceability => sourceKind == PortalPageSectionSourceKind.Derived,
            _ => false,
        };

    private static int GetSubtreeDepth(
        long nodeId,
        ILookup<long?, PortalPageNode> childrenByParent,
        HashSet<long> path)
    {
        if (!path.Add(nodeId)) return PortalLimits.MaximumTreeDepth + 1;
        var maximum = 1;
        foreach (var child in childrenByParent[nodeId])
            maximum = Math.Max(maximum, 1 + GetSubtreeDepth(child.Id, childrenByParent, path));
        path.Remove(nodeId);
        return maximum;
    }
}
