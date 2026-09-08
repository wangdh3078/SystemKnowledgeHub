namespace SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Domain;

public enum AnalysisNodeType { Folder, Document }

public sealed class AnalysisNode
{
    public long Id { get; set; }
    public long? ParentId { get; set; }
    public AnalysisNodeType NodeType { get; set; }
    public string? Title { get; set; }
    public long? KnowledgeDocumentId { get; set; }
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
}
