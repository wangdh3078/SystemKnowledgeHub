namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;

public sealed record DatabaseKnowledgeActorContext(string DisplayName, string? Role);

public sealed record CreateDatabaseSourceCommand(
    long SystemId,
    string Name,
    string Engine,
    string? Environment,
    string? InstanceName,
    string? ServiceName,
    string? DatabaseName,
    string? Description,
    bool IsPrimary,
    DatabaseKnowledgeActorContext Actor);

public sealed record CreateDatabaseSourceResponse(
    long Id,
    long SystemId,
    string Name,
    string Engine,
    string ConcurrencyToken);

public enum CreateDatabaseSourceFailure
{
    None,
    Validation,
    SystemNotFound,
    DuplicateName,
    PrimaryConflict,
}

public sealed record CreateDatabaseSourceResult(
    CreateDatabaseSourceResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    CreateDatabaseSourceFailure Failure);

public sealed record RegisterDatabaseObjectCommand(
    long DatabaseSourceId,
    string SchemaName,
    string ObjectName,
    string ObjectType,
    long? EstimatedRows,
    string AccessMode,
    IReadOnlyList<string>? PrimaryKeyColumns,
    IReadOnlyList<string>? BusinessKeyColumns,
    string? BusinessDescription,
    DatabaseKnowledgeActorContext Actor);

public sealed record RegisterDatabaseObjectResponse(
    long Id,
    long DatabaseSourceId,
    string QualifiedName,
    string ObjectType,
    string KnowledgeStatus,
    string ConcurrencyToken);

public enum RegisterDatabaseObjectFailure
{
    None,
    Validation,
    DatabaseSourceNotFound,
    DuplicateObject,
}

public sealed record RegisterDatabaseObjectResult(
    RegisterDatabaseObjectResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    RegisterDatabaseObjectFailure Failure);

public sealed record RegisterDatabaseColumnCommand(
    long DatabaseObjectId,
    int? OrdinalPosition,
    string ColumnName,
    string DataType,
    bool? Nullable,
    string? DefaultValue,
    string? DatabaseComment,
    string? BusinessDescription,
    DatabaseKnowledgeActorContext Actor,
    string? ConcurrencyToken);

public sealed record RegisteredDatabaseColumnResponse(
    long Id,
    string ColumnName,
    string KnowledgeStatus,
    string ConcurrencyToken);

public sealed record RegisterDatabaseColumnResponse(
    RegisteredDatabaseColumnResponse Column,
    string ParentConcurrencyToken);

public enum RegisterDatabaseColumnFailure
{
    None,
    Validation,
    DatabaseObjectNotFound,
    ConcurrencyConflict,
    DuplicateColumnName,
    DuplicateOrdinalPosition,
}

public sealed record RegisterDatabaseColumnResult(
    RegisterDatabaseColumnResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    RegisterDatabaseColumnFailure Failure);

public sealed record UpdateDatabaseObjectKnowledgeCommand(
    long DatabaseObjectId,
    string? BusinessDescription,
    string AccessMode,
    IReadOnlyList<string>? BusinessKeyColumns,
    DatabaseKnowledgeActorContext Actor,
    string? ConcurrencyToken);

public sealed record DatabaseObjectKnowledgeResponse(
    long Id,
    string? BusinessDescription,
    string AccessMode,
    IReadOnlyList<string> BusinessKeyColumns,
    string KnowledgeStatus,
    string ConcurrencyToken);

public enum UpdateDatabaseObjectKnowledgeFailure
{
    None,
    Validation,
    DatabaseObjectNotFound,
    ConcurrencyConflict,
    ReferenceInvalid,
}

public sealed record UpdateDatabaseObjectKnowledgeResult(
    DatabaseObjectKnowledgeResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    UpdateDatabaseObjectKnowledgeFailure Failure);

public sealed record UpdateDatabaseColumnKnowledgeCommand(
    long DatabaseColumnId,
    string? BusinessDescription,
    DatabaseKnowledgeActorContext Actor,
    string? ConcurrencyToken);

public sealed record DatabaseColumnKnowledgeResponse(
    long Id,
    string? BusinessDescription,
    string KnowledgeStatus,
    string ConcurrencyToken);

public enum UpdateDatabaseColumnKnowledgeFailure
{
    None,
    Validation,
    DatabaseColumnNotFound,
    ConcurrencyConflict,
}

public sealed record UpdateDatabaseColumnKnowledgeResult(
    DatabaseColumnKnowledgeResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    UpdateDatabaseColumnKnowledgeFailure Failure);

public sealed record AddColumnKnownValueCommand(
    long DatabaseColumnId,
    string Value,
    string Meaning,
    int? SortOrder,
    DatabaseKnowledgeActorContext Actor,
    string? ConcurrencyToken);

public sealed record ColumnKnownValueWriteResponse(
    long Id,
    string Value,
    string Meaning,
    int SortOrder);

public sealed record AddColumnKnownValueResponse(
    ColumnKnownValueWriteResponse KnownValue,
    string KnowledgeStatus,
    string ConcurrencyToken);

public enum AddColumnKnownValueFailure
{
    None,
    Validation,
    DatabaseColumnNotFound,
    ConcurrencyConflict,
    DuplicateValue,
}

public sealed record AddColumnKnownValueResult(
    AddColumnKnownValueResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    AddColumnKnownValueFailure Failure);

public sealed record RemoveColumnKnownValueCommand(
    long DatabaseColumnId,
    long KnownValueId,
    bool? Confirmed,
    DatabaseKnowledgeActorContext Actor,
    string? ConcurrencyToken);

public sealed record RemoveColumnKnownValueResponse(
    long ColumnId,
    IReadOnlyList<ColumnKnownValueWriteResponse> KnownValues,
    string ConcurrencyToken);

public enum RemoveColumnKnownValueFailure
{
    None,
    Validation,
    DatabaseColumnNotFound,
    KnownValueNotFound,
    ConcurrencyConflict,
    ReferenceInvalid,
}

public sealed record RemoveColumnKnownValueResult(
    RemoveColumnKnownValueResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    RemoveColumnKnownValueFailure Failure);
