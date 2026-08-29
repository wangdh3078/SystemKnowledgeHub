namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

public enum DatabaseProviderType
{
    Oracle,
    PostgreSql,
    SqlServer,
}

public enum DatabaseAuthenticationMode
{
    UsernamePassword,
}

public enum DatabaseConnectionStatus
{
    Unknown,
    Succeeded,
    Failed,
}

public enum DatabaseConnectionAuditAction
{
    ProfileCreated,
    ProfileUpdated,
    ProfileEnabled,
    ProfileDisabled,
    SecretSet,
    SecretReplaced,
    SecretCleared,
    ConnectionTestStarted,
    ConnectionTestResult,
}

public enum DatabaseConnectionAuditOutcome
{
    Succeeded,
    Failed,
    Superseded,
}
