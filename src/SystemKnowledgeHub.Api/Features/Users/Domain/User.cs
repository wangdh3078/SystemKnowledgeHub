namespace SystemKnowledgeHub.Api.Features.Users.Domain;

/// <summary>
/// 系统知识中心中可被引用、归属和记录历史事实的 canonical business person。
/// </summary>
/// <remarks>
/// User 是业务人员 Profile，不是浏览器临时选择值、认证凭据、Password Account、OIDC token、
/// security principal、KnowledgeRole 或 Permission Role。认证 identity 可映射到 User，但两者保持独立。
/// 停用保留 canonical User 及其既有引用；当前 Feature 通过 Active / Inactive lifecycle 管理，而不以停用表示删除。
/// </remarks>
public sealed class User
{
    public long Id { get; set; }
    public string? EmployeeNo { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string? Email { get; set; }
    public string? DepartmentOrTeam { get; set; }
    public string? JobTitle { get; set; }
    public AccessLevel AccessLevel { get; set; } = AccessLevel.Viewer;
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
}
