namespace SystemKnowledgeHub.Api.Features.Evidence.Domain;

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
    public string ProviderName { get; set; } = string.Empty;
    public string ProviderRole { get; set; } = string.Empty;
    public string? ProviderTeam { get; set; }
    public string? ProviderExternalKey { get; set; }
    public string? ProviderSource { get; set; }
    public string? ProviderNote { get; set; }
    public DateTimeOffset ProvidedAt { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
}

public enum EvidenceType
{
    CodeReference,
    Sql,
    DatabaseSample,
    DatabaseComment,
    Api,
    MqMessage,
    ExistingDocument,
    HumanConfirmation,
}

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

public enum EvidenceConfidence
{
    High,
    Medium,
    Low,
}
