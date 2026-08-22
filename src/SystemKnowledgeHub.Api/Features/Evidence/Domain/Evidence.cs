namespace SystemKnowledgeHub.Api.Features.Evidence.Domain;

/// <summary>
/// 保存支撑某条知识可信度的 Evidence 历史事实，而非通用附件或审批记录。
/// </summary>
/// <remarks>
/// Provider 的 canonical User/KnowledgeRole ID 用于追踪当时来源；人员字段保存写入时 Snapshot，
/// 不会因 User 或 KnowledgeRole 后续变化而动态更新。显式 Evidence correction 可按其 Use Case 修正记录。
/// </remarks>
public sealed class Evidence
{
    public long Id { get; set; }
    public EvidenceType EvidenceType { get; set; }
    public EvidenceSubjectType SubjectType { get; set; }
    public long SubjectId { get; set; }
    public string? SubjectDetailKey { get; set; }
    public string SourceTitle { get; set; } = string.Empty;
    public string? SourceReference { get; set; }
    public string? SourceLocatorJson { get; set; }
    public string? Summary { get; set; }
    public string SupportReason { get; set; } = string.Empty;
    public EvidenceConfidence? Confidence { get; set; }
    public long? ProviderUserId { get; set; }
    public long? ProviderKnowledgeRoleId { get; set; }
    public string? ProviderEmployeeNo { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderRole { get; set; } = string.Empty;
    public string? ProviderTeam { get; set; }
    public string? ProviderJobTitle { get; set; }
    public string? ProviderExternalKey { get; set; }
    public string? ProviderSource { get; set; }
    public string? ProviderNote { get; set; }
    public DateTimeOffset ProvidedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
}

/// <summary>
/// Evidence 的受控来源类别，说明知识结论基于何种可定位依据。
/// </summary>
public enum EvidenceType
{
    CodeReference,
    Sql,
    DatabaseSample,
    DatabaseComment,
    Api,
    MqMessage,
    ExistingDocument,
    /// <summary>以人工确认事实形成的 Evidence，不表示审批、权限或认证证明。</summary>
    HumanConfirmation,
}

/// <summary>可被 Evidence 支持的知识对象类别。</summary>
public enum EvidenceSubjectType
{
    System,
    DatabaseSource,
    BusinessFunction,
    DatabaseObject,
    DatabaseColumn,
    BusinessRule,
    Integration,
    KnowledgeRelation,
    UnknownItem,
    Finding,
    Resolution,
    KnowledgeUpdate,
}

/// <summary>普通 Evidence 对来源可信程度的受控标记。</summary>
public enum EvidenceConfidence
{
    High,
    Medium,
    Low,
}
