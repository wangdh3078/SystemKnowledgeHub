namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

public sealed class DatabaseConnectionSecret
{
    public long ProfileId { get; set; }
    public string? ProtectedPayload { get; set; }
    public int PayloadFormatVersion { get; set; } = 1;
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;

    public DatabaseConnectionProfile Profile { get; set; } = null!;
}
