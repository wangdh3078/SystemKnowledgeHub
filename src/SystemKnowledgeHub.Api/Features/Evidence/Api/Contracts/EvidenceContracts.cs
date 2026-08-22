using System.Text.Json;

namespace SystemKnowledgeHub.Api.Features.Evidence.Api.Contracts;

/// <summary>
/// 指定证据所支持的统一知识对象。
/// </summary>
/// <param name="Type">服务端支持的知识对象类型。</param>
/// <param name="Id">该对象的 JavaScript 安全正整数标识符。</param>
public sealed record EvidenceTargetRequest(string? Type, long Id);

/// <summary>
/// 在录入普通证据时随证据一并保存的提供者事实快照。
/// </summary>
/// <remarks>
/// 此请求描述证据提供当时的人员事实，不是规范用户或知识角色的引用；
/// 人类确认使用其独立的服务端确认人记录。
/// </remarks>
/// <param name="DisplayName">提供者在该事实发生时的显示名称。</param>
/// <param name="RoleOrIdentity">提供者在该事实发生时的角色或身份描述。</param>
/// <param name="OccurredAt">该提供者事实对应的发生时间；缺失时为 <see langword="null"/>。</param>
/// <param name="Team">提供者在该事实发生时的可选团队 Snapshot。</param>
/// <param name="ExternalUserKey">普通 Evidence 可保留的外部人员引用。</param>
/// <param name="Source">普通 Evidence 中说明 Snapshot 来源的可选文本。</param>
/// <param name="Note">随 Provider Snapshot 保存的可选补充说明。</param>
public sealed record PersonSnapshotRequest(
    string? DisplayName,
    string? RoleOrIdentity,
    DateTimeOffset? OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);

/// <summary>随 C24 Evidence correction 提交的操作人标签，不是 Provider Snapshot 或 Current User 身份。</summary>
public sealed record EvidenceActorRequest(string? DisplayName, string? Role);

/// <summary>创建普通 Evidence 的请求，记录来源、支持理由及由客户端提供的 Provider Snapshot。</summary>
/// <remarks>普通 Evidence 不接受 HumanConfirmation；人工确认必须使用专用 API。</remarks>
public sealed record AddEvidenceRequest(
    string? EvidenceType,
    EvidenceTargetRequest? Subject,
    string? SubjectDetailKey,
    string? SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string? SupportReason,
    string? Confidence,
    PersonSnapshotRequest? Provider);

/// <summary>
/// 请求更新已有证据的可编辑内容。
/// </summary>
/// <param name="ConcurrencyToken">
/// 客户端从最新证据详情取得后原样回传的并发令牌。该值对客户端不透明，不能解析、生成、比较或修改；
/// 缺失或过期时服务端将按当前 API 的并发失败语义拒绝写入。
/// </param>
/// <param name="SourceTitle">修正后的必填来源标题。</param>
/// <param name="SourceReference">可选来源引用；与 locator 至少应提供一个。</param>
/// <param name="SourceLocator">可选结构化定位信息。</param>
/// <param name="Summary">可选的证据内容摘要。</param>
/// <param name="SupportReason">修正后仍需说明 Evidence 为什么支持知识结论。</param>
/// <param name="Confidence">可选受控可信度标记。</param>
/// <param name="Provider">可被显式纠正的 Provider Snapshot。</param>
/// <param name="Actor">本次 correction 的操作人标签。</param>
public sealed record UpdateEvidenceRequest(
    string? SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string? SupportReason,
    string? Confidence,
    PersonSnapshotRequest? Provider,
    EvidenceActorRequest? Actor,
    string? ConcurrencyToken);

/// <summary>创建 HumanConfirmation Evidence 时由客户端提交的确认事实。</summary>
/// <remarks>
/// 确认人身份和人员 Snapshot 由服务器基于 authenticated principal-backed Current User 生成。为 null 的
/// <paramref name="KnowledgeRoleId"/> 表示：无启用 KnowledgeRole 时使用 fallback Snapshot，唯一启用角色时自动采用；
/// 多个启用角色时必须显式选择。KnowledgeRole 仅表示本次知识身份，不授予 API 权限。
/// </remarks>
/// <param name="KnowledgeRoleId">本次确认采用的已启用且已分配 KnowledgeRole；为 null 时按当前启用角色数量解析。</param>
/// <param name="Subject">被本次确认支持的明确知识对象。</param>
/// <param name="SubjectDetailKey">可选的 Subject 内部细分位置。</param>
/// <param name="ConfirmationMethod">确认事实的受控方式，写入 locator，不是 Provider 身份来源。</param>
/// <param name="ConfirmedAt">确认事实发生时间；服务端保存为 UTC。</param>
/// <param name="ConfirmationStatement">确认人提交的明确结论事实。</param>
/// <param name="SupportReason">说明该确认为什么支持当前知识结论。</param>
/// <param name="SourceNote">随确认事实保存的可选来源上下文。</param>
public sealed record AddHumanConfirmationRequest(
    EvidenceTargetRequest? Subject,
    string? SubjectDetailKey,
    long? KnowledgeRoleId,
    string? ConfirmationMethod,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmationStatement,
    string? SupportReason,
    string? SourceNote);
