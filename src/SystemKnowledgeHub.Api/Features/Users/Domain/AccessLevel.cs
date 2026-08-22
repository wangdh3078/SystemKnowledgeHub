namespace SystemKnowledgeHub.Api.Features.Users.Domain;

/// <summary>
/// 定义 User 的系统访问等级。
/// </summary>
/// <remarks>
/// AccessLevel 是 authorization classification，与记录知识领域身份的 KnowledgeRole 无关，不能由
/// KnowledgeRole assignment 推导或替代。
/// </remarks>
public enum AccessLevel
{
    Viewer,
    Editor,
    Administrator,
}
