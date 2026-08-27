using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;

public sealed class DatabaseObject
{
    public long Id { get; set; }
    public long DatabaseSourceId { get; set; }
    public string SchemaName { get; set; } = string.Empty;
    public string ObjectName { get; set; } = string.Empty;
    public DatabaseObjectType ObjectType { get; set; }
    public string? BusinessDescription { get; set; }
    public long? EstimatedRows { get; set; }
    public DatabaseAccessMode AccessMode { get; set; }
    public string? PrimaryKeyColumnsJson { get; set; }
    public string? BusinessKeyColumnsJson { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? CreatedByRole { get; set; }
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

    public DatabaseSource DatabaseSource { get; set; } = null!;
    public ICollection<DatabaseColumn> Columns { get; set; } = [];
}
