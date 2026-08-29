namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

public sealed class DatabaseConnectionAuditEvent
{
    public long Id { get; set; }
    public long ProfileId { get; set; }
    public DatabaseConnectionAuditAction Action { get; set; }
    public DatabaseConnectionAuditOutcome Outcome { get; set; }
    public string? ErrorCode { get; set; }
    public string? VendorCode { get; set; }
    public long ActorUserId { get; set; }
    public string ActorDisplayName { get; set; } = string.Empty;
    public DateTimeOffset OccurredAt { get; set; }

    public DatabaseConnectionProfile Profile { get; set; } = null!;
}
