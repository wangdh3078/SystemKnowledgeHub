namespace SystemKnowledgeHub.Api.Features.Users.Domain;

/// <summary>
/// canonical User 与 KnowledgeRole 之间当前生效的多对多 assignment。
/// </summary>
/// <remarks>
/// 该复合键关系没有独立业务 identity。它表达当前 canonical relationship；变更不会动态回写既有
/// Evidence 或 HumanConfirmation 的历史 Snapshot。
/// </remarks>
public sealed class UserKnowledgeRole
{
    public long UserId { get; set; }
    public long KnowledgeRoleId { get; set; }
}
