using System.Text.Json;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Api.Contracts;

public sealed record CreateDatabaseConnectionProfileRequest(
    long? DatabaseSourceId,
    string? Name,
    string? ProviderType,
    string? Host,
    int? Port,
    string? DatabaseName,
    string? ServiceName,
    string? AuthenticationMode,
    string? Username,
    JsonElement? ProviderSpecificOptions,
    IReadOnlyList<string>? IncludedSchemas,
    bool? IsEnabled);

public sealed record UpdateDatabaseConnectionProfileRequest(
    string? Name,
    string? ProviderType,
    string? Host,
    int? Port,
    string? DatabaseName,
    string? ServiceName,
    string? AuthenticationMode,
    string? Username,
    JsonElement? ProviderSpecificOptions,
    IReadOnlyList<string>? IncludedSchemas,
    string? ConcurrencyToken);

public sealed record SetDatabaseConnectionProfileEnabledRequest(
    bool? IsEnabled,
    string? ConcurrencyToken);

public sealed record SetDatabaseConnectionSecretRequest(
    string? Password,
    string? ConcurrencyToken);

public sealed record ClearDatabaseConnectionSecretRequest(string? ConcurrencyToken);
public sealed record TestDatabaseConnectionRequest(string? ConcurrencyToken);
public sealed record TriggerDatabaseDiscoveryRunRequest(string? ConcurrencyToken);
public sealed record CancelDatabaseDiscoveryRunRequest(string? ConcurrencyToken);
