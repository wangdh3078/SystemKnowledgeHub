using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Shared.Api;

public static class SoftDeleteApiResponses
{
    public static ApiErrorResponse Validation(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("validation_error", "请求内容无效。", fieldErrors, null);

    public static ApiErrorResponse NotFound(string resourceType, long resourceId) =>
        new("not_found", "未找到指定资源。", null, new { resourceType, resourceId });

    public static ApiErrorResponse Forbidden(string resourceType, long resourceId) =>
        new("forbidden", "无权删除该资源。", null, new { resourceType, resourceId });

    public static ApiErrorResponse Conflict(string resourceType, long resourceId) =>
        new("conflict", "内容已被其他操作修改，请刷新后重试。", null, new { resourceType, resourceId });

    public static ApiErrorResponse Dependencies(
        string resourceType,
        long resourceId,
        IReadOnlyList<DeleteDependencyBlocker> blockers) =>
        new(
            "business_rule_violation",
            "无法删除，仍存在依赖项。",
            null,
            new { resourceType, resourceId, blockers = blockers.Take(8).ToArray() });
}
