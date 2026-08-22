namespace SystemKnowledgeHub.Api.Shared.Domain;

/// <summary>
/// 表示知识可信度的规范演进状态：Unknown、Inferred、Confirmed。
/// </summary>
/// <remarks>
/// 状态仅能通过显式状态变更操作推进；保存对象、证据、人类确认或关系本身不会自动改变该状态。
/// </remarks>
public enum KnowledgeStatus
{
    Unknown,
    Inferred,
    Confirmed,
}
