using System.Text.Json;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Evidence.Application.Models;

/// <summary>将 API Subject 转为 Evidence Use Case 使用的受控知识目标。</summary>
public sealed record EvidenceTargetCommand(string Type, long Id);

/// <summary>普通 Evidence 写入时由调用方提供的人员事实 Snapshot。</summary>
public sealed record PersonSnapshotCommand(
    string DisplayName,
    string RoleOrIdentity,
    DateTimeOffset OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);

/// <summary>执行 C24 Evidence correction 时记录的操作人标签。</summary>
public sealed record EvidenceActorCommand(string DisplayName, string? Role);

/// <summary>创建普通 Evidence 的 Use Case 输入；普通 Evidence 的 Provider Snapshot 由客户端提供。</summary>
public sealed record AddEvidenceCommand(
    string EvidenceType,
    EvidenceTargetCommand? Subject,
    string? SubjectDetailKey,
    string SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string SupportReason,
    string? Confidence,
    PersonSnapshotCommand? Provider);

/// <summary>显式纠正既有 Evidence 可编辑内容的 Use Case 输入。</summary>
/// <remarks>ConcurrencyToken 是 opaque 值；此操作不会因 canonical User 或 KnowledgeRole 后续变化而自动发生。</remarks>
public sealed record UpdateEvidenceCommand(
    long EvidenceId,
    string SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string SupportReason,
    string? Confidence,
    PersonSnapshotCommand? Provider,
    EvidenceActorCommand? Actor,
    string ConcurrencyToken);

/// <summary>创建 HumanConfirmation Evidence 的确认事实输入。</summary>
/// <remarks>
/// CurrentUserId 仅由 API 已解析的 Current User 传入。服务端在同一事务内重新读取 canonical User 和 KnowledgeRole，
/// 生成确认人 Snapshot；客户端不提供确认人的 Profile 或身份字段。
/// </remarks>
public sealed record AddHumanConfirmationCommand(
    long CurrentUserId,
    EvidenceTargetCommand? Subject,
    long? SubjectRevisionNumber,
    string? SubjectDetailKey,
    long? KnowledgeRoleId,
    string ConfirmationMethod,
    DateTimeOffset? ConfirmedAt,
    string ConfirmationStatement,
    string SupportReason,
    string? SourceNote);

/// <summary>Evidence 详情中被其支持的知识目标投影。</summary>
public sealed record EvidenceTargetResponse(string Type, long Id);

/// <summary>Evidence 写入时保留的提供者历史事实投影，而非当前 User 或 KnowledgeRole 的动态视图。</summary>
public sealed record PersonSnapshotResponse(
    string DisplayName,
    string RoleOrIdentity,
    DateTimeOffset OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);

/// <summary>Evidence 详情附带的当前 Subject 显示上下文。</summary>
public sealed record EvidenceSubjectContextResponse(string Title, string KnowledgeStatus);

/// <summary>Evidence 的可读详情投影，组合证据内容、Provider Snapshot、Subject 上下文与可编辑并发令牌。</summary>
public sealed record EvidenceDetailResponse(
    long Id,
    string ConcurrencyToken,
    string EvidenceType,
    EvidenceTargetResponse Subject,
    string? SubjectDetailKey,
    long? KnowledgeDocumentRevisionNumberSnapshot,
    string SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string SupportReason,
    string? Confidence,
    PersonSnapshotResponse Provider,
    EvidenceSubjectContextResponse SubjectContext,
    IReadOnlyList<string> AvailableActions);

/// <summary>某一知识对象的 Evidence 摘要投影，用于详情页展示其支持依据与人工确认记录。</summary>
/// <remarks>该投影不包含可编辑并发令牌；状态推进仍由独立的显式 KnowledgeStatus 操作执行。</remarks>
public sealed record EvidenceListItemResponse(
    long Id,
    string EvidenceType,
    long? KnowledgeDocumentRevisionNumberSnapshot,
    string SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string SupportReason,
    PersonSnapshotResponse Provider);

/// <summary>按明确 Subject 返回的 Evidence 摘要集合。</summary>
public sealed record EvidenceListResponse(IReadOnlyList<EvidenceListItemResponse> Items);

/// <summary>创建普通 Evidence 或 HumanConfirmation 后返回的简要结果。</summary>
/// <remarks>KnowledgeStatusChanged 对创建操作保持 false；Evidence 可作为后续显式状态推进的依据，但不执行推进。</remarks>
public sealed record AddEvidenceResponse(
    long Id,
    string EvidenceType,
    EvidenceTargetResponse Subject,
    string? SubjectDetailKey,
    long? KnowledgeDocumentRevisionNumberSnapshot,
    string SourceTitle,
    string SubjectKnowledgeStatus,
    bool KnowledgeStatusChanged,
    string ConcurrencyToken);

/// <summary>Subject resolver 为 Evidence Use Case 返回的名称与当前知识状态。</summary>
public sealed record EvidenceSubjectContext(string Title, KnowledgeStatus KnowledgeStatus);

/// <summary>Evidence Application Use Case 的显式结果分类，而非 HTTP 或异常列表。</summary>
public enum EvidenceFailure
{
    None,
    Validation,
    NotFound,
    /// <summary>Subject 不存在或当前不能作为 Evidence 目标。</summary>
    SubjectNotFound,
    /// <summary>提交时重新读取不到 Current User 对应的 canonical User。</summary>
    CurrentUserNotFound,
    /// <summary>提交时 canonical User 已停用，不能生成确认人 Snapshot。</summary>
    CurrentUserInactive,
    KnowledgeRoleNotFound,
    KnowledgeRoleInactive,
    /// <summary>显式选择的 KnowledgeRole 不属于当前确认人的现有 assignment。</summary>
    KnowledgeRoleNotAssigned,
    /// <summary>C24 correction 使用的 concurrencyToken 过期或并发写入发生冲突。</summary>
    Conflict,
}

/// <summary>Evidence 写 Use Case 的成功响应、字段错误与失败分类。</summary>
public sealed record EvidenceCommandResult(
    object? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    EvidenceFailure Failure,
    long? CurrentRevisionNumber = null);

/// <summary>Evidence 详情查询的投影或未找到/Subject 无效结果。</summary>
public sealed record EvidenceDetailQueryResult(
    EvidenceDetailResponse? Response,
    EvidenceFailure Failure);

/// <summary>Evidence 列表查询的投影或 Subject 无效结果。</summary>
public sealed record EvidenceListQueryResult(
    EvidenceListResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    EvidenceFailure Failure);
