namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;

public sealed class ColumnKnownValue
{
    public long Id { get; set; }
    public long DatabaseColumnId { get; set; }
    public string ValueText { get; set; } = string.Empty;
    public string Meaning { get; set; } = string.Empty;
    public int SortOrder { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    public DatabaseColumn DatabaseColumn { get; set; } = null!;
}
