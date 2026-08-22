using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;

public sealed class KnowledgeDocument
{
    public long Id { get; set; }
    public DocumentType DocumentType { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Summary { get; set; }
    public string BodyMarkdown { get; set; } = string.Empty;
    public DocumentLifecycleStatus LifecycleStatus { get; set; }
    public KnowledgeStatus KnowledgeStatus { get; set; }
    public string? KnowledgeStatusReason { get; set; }
    public DateTimeOffset KnowledgeStatusChangedAt { get; set; }
    public string KnowledgeStatusChangedByName { get; set; } = string.Empty;
    public string KnowledgeStatusChangedByRole { get; set; } = string.Empty;
    public long CreatedByUserId { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public long UpdatedByUserId { get; set; }
    public string UpdatedByDisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public DateTimeOffset? PublishedAt { get; set; }
    public DateTimeOffset? ArchivedAt { get; set; }
    public long Version { get; set; } = 1;
}
