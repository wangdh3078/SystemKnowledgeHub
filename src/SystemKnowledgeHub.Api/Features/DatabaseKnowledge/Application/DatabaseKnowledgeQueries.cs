using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;

public sealed class DatabaseKnowledgeQueries(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private static readonly string[] DatabaseObjectActions =
    [
        "UpdateDatabaseObjectKnowledge",
        "RegisterDatabaseColumn",
        "AddKnowledgeRelation",
    ];

    private static readonly string[] DatabaseColumnActions =
    [
        "UpdateDatabaseColumnKnowledge",
        "AddColumnKnownValue",
        "AddEvidence",
        "ChangeKnowledgeStatus",
    ];

    public async Task<DatabaseObjectDetailQueryResult> GetDatabaseObjectDetail(
        long databaseObjectId,
        long? selectedColumnId,
        CancellationToken cancellationToken)
    {
        var databaseObject = await dbContext.DatabaseObjects
            .AsNoTracking()
            .Where(item => item.Id == databaseObjectId)
            .Select(item => new
            {
                item.Id,
                item.SchemaName,
                item.ObjectName,
                item.ObjectType,
                item.BusinessDescription,
                item.AccessMode,
                item.KnowledgeStatus,
                item.EstimatedRows,
                item.PrimaryKeyColumnsJson,
                item.BusinessKeyColumnsJson,
                item.Version,
                SourceId = item.DatabaseSource.Id,
                SourceName = item.DatabaseSource.Name,
                item.DatabaseSource.Engine,
                SystemId = item.DatabaseSource.System.Id,
                SystemName = item.DatabaseSource.System.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (databaseObject is null)
        {
            return new DatabaseObjectDetailQueryResult(null, false);
        }

        if (selectedColumnId.HasValue)
        {
            var selectedColumnBelongs = await dbContext.DatabaseColumns
                .AsNoTracking()
                .AnyAsync(
                    column => column.Id == selectedColumnId.Value
                        && column.DatabaseObjectId == databaseObjectId,
                    cancellationToken);

            if (!selectedColumnBelongs)
            {
                return new DatabaseObjectDetailQueryResult(null, true);
            }
        }

        var columnRows = await dbContext.DatabaseColumns
            .AsNoTracking()
            .Where(column => column.DatabaseObjectId == databaseObjectId)
            .OrderBy(column => column.OrdinalPosition)
            .Select(column => new
            {
                column.Id,
                column.OrdinalPosition,
                column.ColumnName,
                column.DataType,
                column.IsNullable,
                column.BusinessDescription,
                column.KnowledgeStatus,
            })
            .ToListAsync(cancellationToken);

        var columns = columnRows
            .Select(column => new DatabaseColumnSummary(
                column.Id,
                column.OrdinalPosition,
                column.ColumnName,
                column.DataType,
                column.IsNullable,
                column.BusinessDescription,
                EvidenceCount: 0,
                UnknownCount: 0,
                column.KnowledgeStatus.ToString(),
                Selected: selectedColumnId == column.Id))
            .ToArray();

        var qualifiedName = $"{databaseObject.SchemaName}.{databaseObject.ObjectName}";
        var response = new DatabaseObjectDetailResponse(
            databaseObject.Id,
            new SystemContext(databaseObject.SystemId, databaseObject.SystemName),
            new DatabaseSourceContext(
                databaseObject.SourceId,
                databaseObject.SourceName,
                databaseObject.Engine),
            concurrencyTokenCodec.Encode(databaseObject.Version),
            new DatabaseObjectOverview(
                qualifiedName,
                databaseObject.ObjectType.ToString(),
                databaseObject.BusinessDescription,
                databaseObject.AccessMode.ToString(),
                databaseObject.KnowledgeStatus.ToString()),
            new DatabaseObjectMetadata(
                databaseObject.EstimatedRows,
                ParseStringArray(databaseObject.PrimaryKeyColumnsJson),
                ParseStringArray(databaseObject.BusinessKeyColumnsJson)),
            columns,
            new DatabaseObjectContextRail([], 0, 0, 0),
            selectedColumnId.HasValue ? new SelectedColumnDrawer(selectedColumnId.Value) : null,
            DatabaseObjectActions);

        return new DatabaseObjectDetailQueryResult(response, false);
    }

    public async Task<DatabaseColumnDetailResponse?> GetColumnDetail(
        long databaseColumnId,
        CancellationToken cancellationToken)
    {
        var column = await dbContext.DatabaseColumns
            .AsNoTracking()
            .Where(item => item.Id == databaseColumnId)
            .Select(item => new
            {
                item.Id,
                item.OrdinalPosition,
                item.ColumnName,
                item.DataType,
                item.IsNullable,
                item.DefaultValue,
                item.BusinessDescription,
                item.KnowledgeStatus,
                item.Version,
                DatabaseObjectId = item.DatabaseObject.Id,
                item.DatabaseObject.SchemaName,
                item.DatabaseObject.ObjectName,
                SystemId = item.DatabaseObject.DatabaseSource.System.Id,
                SystemName = item.DatabaseObject.DatabaseSource.System.Name,
            })
            .SingleOrDefaultAsync(cancellationToken);

        if (column is null)
        {
            return null;
        }

        var knownValues = await dbContext.ColumnKnownValues
            .AsNoTracking()
            .Where(value => value.DatabaseColumnId == databaseColumnId)
            .OrderBy(value => value.SortOrder)
            .ThenBy(value => value.ValueText)
            .Select(value => new ColumnKnownValueResponse(
                value.Id,
                value.ValueText,
                value.Meaning))
            .ToListAsync(cancellationToken);

        var evidence = await dbContext.Evidence
            .AsNoTracking()
            .Where(item => item.SubjectType == EvidenceSubjectType.DatabaseColumn
                && item.SubjectId == databaseColumnId)
            .OrderByDescending(item => item.Id)
            .Select(item => new ColumnEvidenceSummary(
                item.Id,
                item.EvidenceType.ToString(),
                item.SourceTitle,
                item.SupportReason))
            .ToListAsync(cancellationToken);

        return new DatabaseColumnDetailResponse(
            column.Id,
            new ColumnParent(
                column.DatabaseObjectId,
                $"{column.SchemaName}.{column.ObjectName}"),
            new SystemContext(column.SystemId, column.SystemName),
            concurrencyTokenCodec.Encode(column.Version),
            new ColumnDatabaseMetadata(
                column.ColumnName,
                column.DataType,
                column.IsNullable,
                column.DefaultValue,
                column.OrdinalPosition),
            new ColumnBusinessKnowledge(
                column.BusinessDescription,
                column.KnowledgeStatus.ToString()),
            knownValues,
            evidence,
            Relations: [],
            UnknownItems: [],
            DatabaseColumnActions);
    }

    private static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }
}
