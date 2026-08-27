using SystemKnowledgeHub.Api.Features.Systems.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;

public sealed class DatabaseSource
{
    public long Id { get; set; }
    public long SystemId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Engine { get; set; } = string.Empty;
    public string? Environment { get; set; }
    public string? InstanceName { get; set; }
    public string? ServiceName { get; set; }
    public string? DatabaseName { get; set; }
    public string? Description { get; set; }
    public bool IsPrimary { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public long? CreatedByUserId { get; set; }
    public string CreatedByName { get; set; } = string.Empty;
    public string? CreatedByRole { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;
    public bool IsDeleted { get; set; }
    public DateTimeOffset? DeletedAt { get; set; }
    public long? DeletedByUserId { get; set; }
    public string? DeletedByDisplayName { get; set; }

    public KnowledgeSystem System { get; set; } = null!;
    public ICollection<DatabaseObject> DatabaseObjects { get; set; } = [];
}
