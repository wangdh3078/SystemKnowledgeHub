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
    DiscoveryRunTriggered,
    DiscoveryRunCancellationRequested,
    DiscoveryRunResult,
}

public enum DatabaseConnectionAuditOutcome
{
    Succeeded,
    Failed,
    Superseded,
}

public enum DatabaseDiscoveryRunStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
}

public enum DatabaseDiscoveryCompleteness
{
    Complete,
}

public enum DatabaseDiscoveryDifferenceState
{
    Added,
    Changed,
    MissingFromSource,
    Unchanged,
}

public enum DatabaseDiscoveryEntityKind
{
    Schema,
    DatabaseObject,
    Column,
    PrimaryKey,
    ForeignKey,
    UniqueConstraint,
    Index,
    Sequence,
}

public enum DatabaseDiscoveryObjectType
{
    Table,
    View,
}

public enum DatabaseDiscoveryCapabilityState
{
    Supported,
    NotSupported,
    Unavailable,
    NotApplicable,
}

public enum DatabaseDiscoveryNativeTypeOrigin
{
    CatalogDeclared,
    ProviderImplicit,
}

public enum DatabaseDiscoveryMeasureKind
{
    Exact,
    Unbounded,
    NotApplicable,
    Unknown,
}

public enum DatabaseDiscoveryLengthUnit
{
    Bytes,
    Characters,
    Bits,
    ProviderUnits,
}

public enum DatabaseDiscoverySortDirection
{
    Ascending,
    Descending,
    Unspecified,
}

public enum DatabaseDiscoveryNonKeyPartRole
{
    Included,
    Stored,
    Partitioning,
    UnorderedMember,
}
