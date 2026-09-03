using System.Text.Json;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api.Contracts;

public sealed record DeleteDatabaseSourceRequest(string? ConcurrencyToken);
public sealed record DeleteDatabaseObjectRequest(string? ConcurrencyToken);
public sealed record DeleteDatabaseColumnRequest(string? ConcurrencyToken);

public sealed record DatabaseKnowledgeActorRequest(string? DisplayName, string? Role);

public sealed record CreateDatabaseSourceRequest(
    long? SystemId,
    string? Name,
    string? Engine,
    string? Environment,
    string? InstanceName,
    string? ServiceName,
    string? DatabaseName,
    string? Description,
    bool? IsPrimary,
    DatabaseKnowledgeActorRequest? Actor);

public sealed record RegisterDatabaseObjectRequest(
    long? DatabaseSourceId,
    string? SchemaName,
    string? ObjectName,
    string? ObjectType,
    long? EstimatedRows,
    string? AccessMode,
    IReadOnlyList<string>? PrimaryKeyColumns,
    IReadOnlyList<string>? BusinessKeyColumns,
    string? BusinessDescription,
    DatabaseKnowledgeActorRequest? Actor);

public sealed record RegisterDatabaseColumnRequest(
    int? OrdinalPosition,
    string? ColumnName,
    string? DataType,
    bool? Nullable,
    string? DefaultValue,
    string? DatabaseComment,
    string? BusinessDescription,
    DatabaseKnowledgeActorRequest? Actor,
    string? ConcurrencyToken);

public sealed record UpdateDatabaseObjectKnowledgeRequest(
    string? BusinessDescription,
    JsonElement? EstimatedRows,
    string? AccessMode,
    IReadOnlyList<string>? BusinessKeyColumns,
    DatabaseKnowledgeActorRequest? Actor,
    string? ConcurrencyToken);

public sealed record UpdateDatabaseColumnKnowledgeRequest(
    string? BusinessDescription,
    DatabaseKnowledgeActorRequest? Actor,
    string? ConcurrencyToken);

public sealed record AddColumnKnownValueRequest(
    string? Value,
    string? Meaning,
    int? SortOrder,
    DatabaseKnowledgeActorRequest? Actor,
    string? ConcurrencyToken);

public sealed record RemoveColumnKnownValueRequest(
    bool? Confirmed,
    DatabaseKnowledgeActorRequest? Actor,
    string? ConcurrencyToken);
