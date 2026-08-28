namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;

public sealed record SystemContext(long Id, string Name);

public sealed record DatabaseSourceContext(long Id, string Name, string Engine, string ConcurrencyToken, bool CanDelete);

public sealed record DatabaseObjectListQuery(
    long? SystemId,
    long? DatabaseSourceId,
    string? Schema,
    string? ObjectType,
    string? KnowledgeStatus,
    string? Search,
    string? Sort,
    int? Page,
    int? PageSize);

public sealed record DatabaseObjectMatchedColumn(long Id, string ColumnName);

public sealed record DatabaseObjectListItem(
    long Id,
    DatabaseSourceContext DatabaseSource,
    string Schema,
    string ObjectName,
    string ObjectType,
    string? BusinessDescription,
    long? EstimatedRows,
    string AccessMode,
    int RelatedFunctionCount,
    int UnknownCount,
    string KnowledgeStatus,
    DatabaseObjectMatchedColumn? MatchedColumn);

public sealed record DatabaseObjectBrowseContext(
    SystemContext? System,
    IReadOnlyList<DatabaseSourceContext> DatabaseSources,
    IReadOnlyList<string> Schemas);

public sealed record DatabaseObjectsListResponse(
    DatabaseObjectBrowseContext BrowseContext,
    IReadOnlyList<DatabaseObjectListItem> Items,
    int Page,
    int PageSize,
    int Total);

public enum DatabaseObjectsListFailure
{
    None,
    Validation,
    SystemNotFound,
    DatabaseSourceNotFound,
    DatabaseSourceOutsideSystem,
}

public sealed record DatabaseObjectsListQueryResult(
    DatabaseObjectsListResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    DatabaseObjectsListFailure Failure);

public sealed record DatabaseObjectOverview(
    string QualifiedName,
    string ObjectType,
    string? BusinessDescription,
    string AccessMode,
    string KnowledgeStatus);

public sealed record DatabaseObjectMetadata(
    long? EstimatedRows,
    IReadOnlyList<string> PrimaryKeyColumns,
    IReadOnlyList<string> BusinessKeyColumns);

public sealed record DatabaseColumnSummary(
    long Id,
    int OrdinalPosition,
    string ColumnName,
    string DataType,
    bool Nullable,
    string? BusinessDescription,
    int EvidenceCount,
    int UnknownCount,
    string KnowledgeStatus,
    bool Selected);

public sealed record UsedByFunctionSummary(
    long Id,
    string Name,
    string RelationType,
    string? Reference);

public sealed record DatabaseObjectContextRail(
    IReadOnlyList<UsedByFunctionSummary> UsedByFunctions,
    int RelatedRuleCount,
    int IntegrationCount,
    int OpenUnknownCount);

public sealed record SelectedColumnDrawer(long ColumnId);

public sealed record DatabaseObjectDetailResponse(
    long Id,
    SystemContext System,
    DatabaseSourceContext DatabaseSource,
    string ConcurrencyToken,
    DatabaseObjectOverview Overview,
    DatabaseObjectMetadata Metadata,
    IReadOnlyList<DatabaseColumnSummary> Columns,
    DatabaseObjectContextRail ContextRail,
    SelectedColumnDrawer? SelectedColumnDrawer,
    bool CanDelete,
    IReadOnlyList<string> AvailableActions);

public sealed record ColumnParent(long DatabaseObjectId, string QualifiedName);

public sealed record ColumnDatabaseMetadata(
    string ColumnName,
    string DataType,
    bool Nullable,
    string? DefaultValue,
    int OrdinalPosition);

public sealed record ColumnBusinessKnowledge(string? Description, string KnowledgeStatus);

public sealed record ColumnKnownValueResponse(long Id, string Value, string Meaning);

public sealed record ColumnEvidenceSummary(
    long Id,
    string EvidenceType,
    string SourceTitle,
    string SupportReason);

public sealed record RelatedObjectSummary(string Type, long Id, string Title);

public sealed record ColumnRelationSummary(
    long Id,
    string RelationType,
    RelatedObjectSummary OtherObject);

public sealed record ColumnUnknownItemSummary(
    long Id,
    string Question,
    string Status);

public sealed record DatabaseColumnDetailResponse(
    long Id,
    ColumnParent Parent,
    SystemContext System,
    string ConcurrencyToken,
    ColumnDatabaseMetadata DatabaseMetadata,
    ColumnBusinessKnowledge BusinessKnowledge,
    IReadOnlyList<ColumnKnownValueResponse> KnownValues,
    IReadOnlyList<ColumnEvidenceSummary> Evidence,
    IReadOnlyList<ColumnRelationSummary> Relations,
    IReadOnlyList<ColumnUnknownItemSummary> UnknownItems,
    bool CanDelete,
    IReadOnlyList<string> AvailableActions);

public sealed record DatabaseObjectDetailQueryResult(
    DatabaseObjectDetailResponse? Detail,
    bool SelectedColumnInvalid);
