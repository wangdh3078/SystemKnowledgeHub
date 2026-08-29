using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

public sealed class DatabaseConnectionProfile
{
    public long Id { get; set; }
    public long DatabaseSourceId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DatabaseProviderType ProviderType { get; set; }
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; }
    public string? DatabaseName { get; set; }
    public string? ServiceName { get; set; }
    public DatabaseAuthenticationMode AuthenticationMode { get; set; }
    public string Username { get; set; } = string.Empty;
    public string ProviderSpecificOptionsJson { get; set; } = "{\"version\":1}";
    public string IncludedSchemasJson { get; set; } = "[]";
    public bool IsEnabled { get; set; }
    public DatabaseConnectionStatus ConnectionStatus { get; set; }
    public string? LatestConnectionTestAttemptId { get; set; }
    public DateTimeOffset? LastConnectionTestStartedAt { get; set; }
    public DateTimeOffset? LastConnectionTestAt { get; set; }
    public string? LastConnectionTestErrorCode { get; set; }
    public string? LastConnectionTestVendorCode { get; set; }
    public string? LastConnectionTestSummary { get; set; }
    public DateTimeOffset? LastDiscoveryAt { get; set; }
    public DateTimeOffset? LastSuccessfulDiscoveryAt { get; set; }
    public long ConfigurationRevision { get; set; } = 1;
    public long CreatedByUserId { get; set; }
    public string CreatedByDisplayName { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
    public long Version { get; set; } = 1;

    public DatabaseSource DatabaseSource { get; set; } = null!;
    public DatabaseConnectionSecret? Secret { get; set; }
    public ICollection<DatabaseConnectionAuditEvent> AuditEvents { get; set; } = [];
}
