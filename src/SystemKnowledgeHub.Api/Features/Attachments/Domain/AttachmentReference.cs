namespace SystemKnowledgeHub.Api.Features.Attachments.Domain;

public sealed class AttachmentReference
{
    public long Id { get; set; }
    public long KnowledgeDocumentId { get; set; }
    public long KnowledgeDocumentRevisionId { get; set; }
    public long AttachmentId { get; set; }
}
