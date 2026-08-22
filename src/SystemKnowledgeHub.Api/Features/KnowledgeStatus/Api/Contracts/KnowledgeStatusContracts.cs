namespace SystemKnowledgeHub.Api.Features.StatusProgression.Api.Contracts;

/// <summary>
/// 指定需要执行显式知识状态变更的统一知识对象。
/// </summary>
/// <param name="Type">服务端支持的知识对象类型。</param>
/// <param name="Id">该对象的 JavaScript 安全正整数标识符。</param>
public sealed record KnowledgeStatusTargetRequest(string? Type, long Id);

public sealed record KnowledgeStatusActorRequest(
    string? DisplayName,
    string? RoleOrIdentity,
    DateTimeOffset? OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);

/// <summary>
/// 请求对知识对象执行显式的可信度状态变更。
/// </summary>
/// <param name="Target">需要变更状态的知识对象；缺失时请求无效。</param>
/// <param name="ConcurrencyToken">
/// 客户端从最新对象详情取得后原样回传的并发令牌。该值对客户端不透明，不能解析、生成、比较或修改；
/// 缺失或过期时服务端将按当前 API 的并发失败语义拒绝写入。
/// </param>
public sealed record ChangeKnowledgeStatusRequest(
    KnowledgeStatusTargetRequest? Target,
    string? TargetStatus,
    string? Reason,
    KnowledgeStatusActorRequest? Actor,
    string? ConcurrencyToken);
