namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;

public sealed class KnowledgeDocumentRevision
{
    public long Id { get; set; }
    public long KnowledgeDocumentId { get; set; }
    public long RevisionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string BodyMarkdown { get; set; } = string.Empty;
    public long? AuthorUserId { get; set; }
    public string? AuthorDisplayNameSnapshot { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DocumentLifecycleStatus LifecycleContext { get; set; }
    public string? ChangeSummary { get; set; }
    public string? RestoreReason { get; set; }
    public long? RestoredFromRevisionNumber { get; set; }
    public RevisionOrigin RevisionOrigin { get; set; }
}

public enum RevisionOrigin
{
    Created,
    ContentSave,
    Restore,
    MigrationBaseline,
}
