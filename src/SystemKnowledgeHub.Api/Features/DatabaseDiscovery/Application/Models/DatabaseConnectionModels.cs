using System.Text.Json;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;

public sealed record DatabaseConnectionProfileInput(
    long DatabaseSourceId,
    string Name,
    string ProviderType,
    string Host,
    int? Port,
    string? DatabaseName,
    string? ServiceName,
    string AuthenticationMode,
    string Username,
    JsonElement? ProviderSpecificOptions,
    IReadOnlyList<string>? IncludedSchemas,
    bool IsEnabled);

public sealed record DatabaseConnectionProfileUpdateInput(
    long Id,
    string Name,
    string ProviderType,
    string Host,
    int? Port,
    string? DatabaseName,
    string? ServiceName,
    string AuthenticationMode,
    string Username,
    JsonElement? ProviderSpecificOptions,
    IReadOnlyList<string>? IncludedSchemas,
    string? ConcurrencyToken);

public sealed record DatabaseProviderSpecificOptionsResponse(int Version);

public sealed record DatabaseConnectionProfileResponse(
    long Id,
    long DatabaseSourceId,
    string Name,
    DatabaseProviderType ProviderType,
    string Host,
    int Port,
    string? DatabaseName,
    string? ServiceName,
    DatabaseAuthenticationMode AuthenticationMode,
    string Username,
    DatabaseProviderSpecificOptionsResponse ProviderSpecificOptions,
    IReadOnlyList<string> IncludedSchemas,
    bool IsEnabled,
    DatabaseConnectionStatus ConnectionStatus,
    bool HasSecret,
    DateTimeOffset? SecretUpdatedAt,
    DateTimeOffset? LastConnectionTestAt,
    string? LastConnectionTestErrorCode,
    string? LastConnectionTestVendorCode,
    string? LastConnectionTestSummary,
    DateTimeOffset? LastDiscoveryAt,
    DateTimeOffset? LastSuccessfulDiscoveryAt,
    long ConfigurationRevision,
    string ConcurrencyToken);

public sealed record DatabaseConnectionTestResponse(
    long ProfileId,
    bool Succeeded,
    string? ErrorCode,
    string? VendorCode,
    string Summary,
    string? ProviderVersion,
    string? ServiceName,
    string? ContainerName,
    string ConcurrencyToken);

public enum DatabaseConnectionFailure
{
    None,
    Validation,
    NotFound,
    ReferenceInvalid,
    DuplicateSource,
    DuplicateName,
    ConcurrencyConflict,
    ActiveDiscoveryRun,
    DiscoveryTargetImmutable,
    SecretAlreadySet,
    SecretMissing,
    SecretUnavailable,
    Disabled,
    ProviderUnavailable,
    ConnectionFailed,
    AuthenticationFailed,
    InsufficientPrivilege,
    UnsupportedDatabaseVersion,
    Timeout,
    Cancelled,
}

public sealed record DatabaseConnectionOperationResult<T>(
    T? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    DatabaseConnectionFailure Failure,
    string? VendorCode = null);

public sealed record DatabaseConnectionActor(CanonicalCreator Creator);
