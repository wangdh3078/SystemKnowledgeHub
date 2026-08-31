using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;

public sealed class DatabaseColumn
{
    public long Id { get; set; }
    public long DatabaseObjectId { get; set; }
    public int OrdinalPosition { get; set; }
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsNullable { get; set; }
    public string? DefaultValue { get; set; }
    public string? BusinessDescription { get; set; }
    public string? DatabaseComment { get; set; }
    public int TechnicalIdentityAlgorithmVersion { get; set; } = 1;
    public string TechnicalIdentity { get; set; } = $"manual:column:v1:{Guid.NewGuid():N}";
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public string? CreatedByDisplayName { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public KnowledgeStatus KnowledgeStatus { get; set; }
    public string? KnowledgeStatusReason { get; set; }
    public DateTimeOffset KnowledgeStatusChangedAt { get; set; }
    public string KnowledgeStatusChangedByName { get; set; } = string.Empty;
    public string KnowledgeStatusChangedByRole { get; set; } = string.Empty;
    public long Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long? DeletedByUserId { get; set; }
    public string? DeletedByDisplayName { get; set; }

    public DatabaseObject DatabaseObject { get; set; } = null!;
    public ICollection<ColumnKnownValue> KnownValues { get; set; } = [];
}
