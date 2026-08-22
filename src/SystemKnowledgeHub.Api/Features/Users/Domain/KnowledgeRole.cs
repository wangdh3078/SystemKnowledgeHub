namespace SystemKnowledgeHub.Api.Features.Users.Domain;

/// <summary>
/// 表示 User 参与知识提供、解释或确认时采用的知识领域身份。
/// </summary>
/// <remarks>
/// KnowledgeRole 不是 Viewer、Editor 或 Administrator 等 AccessLevel，也不是 Permission、authorization、
/// security role 或 RBAC role。停用后不能用于新的 User assignment，但既有 assignment 会保留。
/// </remarks>
public sealed class KnowledgeRole
{
    public long Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public bool IsActive { get; set; } = true;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
}
