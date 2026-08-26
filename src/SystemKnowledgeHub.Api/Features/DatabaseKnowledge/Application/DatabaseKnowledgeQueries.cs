using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;

public sealed class DatabaseKnowledgeQueries(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private static readonly string[] DatabaseObjectActions =
    [
        "UpdateDatabaseObjectKnowledge",
        "RegisterDatabaseColumn",
        "AddKnowledgeRelation",
        "AddEvidence",
        "ChangeKnowledgeStatus",
    ];

    private static readonly string[] DatabaseColumnActions =
    [
        "UpdateDatabaseColumnKnowledge",
        "AddColumnKnownValue",
        "AddEvidence",
        "ChangeKnowledgeStatus",
    ];

    public async Task<DatabaseObjectsListQueryResult> GetDatabaseObjectsList(
        DatabaseObjectListQuery request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateListQuery(
            request,
            out var objectType,
            out var knowledgeStatus,
            out var sort);
        if (errors.Count > 0)
        {
            return new DatabaseObjectsListQueryResult(
                null,
                errors,
                DatabaseObjectsListFailure.Validation);
        }

        SystemContext? systemContext = null;
        if (request.SystemId.HasValue)
        {
            systemContext = await dbContext.Systems
                .AsNoTracking()
                .Where(system => system.Id == request.SystemId.Value)
                .Select(system => new SystemContext(system.Id, system.Name))
                .SingleOrDefaultAsync(cancellationToken);
            if (systemContext is null)
            {
                return new DatabaseObjectsListQueryResult(
                    null,
                    null,
                    DatabaseObjectsListFailure.SystemNotFound);
            }
        }

        if (request.DatabaseSourceId.HasValue)
        {
            var sourceContext = await dbContext.DatabaseSources
                .AsNoTracking()
                .Where(source => source.Id == request.DatabaseSourceId.Value)
                .Select(source => new
                {
                    Source = new DatabaseSourceContext(source.Id, source.Name, source.Engine),
                    System = new SystemContext(source.System.Id, source.System.Name),
                })
                .SingleOrDefaultAsync(cancellationToken);
            if (sourceContext is null)
            {
                return new DatabaseObjectsListQueryResult(
                    null,
                    null,
                    DatabaseObjectsListFailure.DatabaseSourceNotFound);
            }

            if (systemContext is not null && sourceContext.System.Id != systemContext.Id)
            {
                return new DatabaseObjectsListQueryResult(
                    null,
                    null,
                    DatabaseObjectsListFailure.DatabaseSourceOutsideSystem);
            }

            systemContext ??= sourceContext.System;
        }

        var sourceQuery = dbContext.DatabaseSources.AsNoTracking();
        if (systemContext is not null)
        {
            sourceQuery = sourceQuery.Where(source => source.SystemId == systemContext.Id);
        }

        var sourceContexts = await sourceQuery
            .OrderByDescending(source => source.IsPrimary)
            .ThenBy(source => source.Name)
            .Select(source => new DatabaseSourceContext(source.Id, source.Name, source.Engine))
            .ToArrayAsync(cancellationToken);

        var objectQuery = dbContext.DatabaseObjects.AsNoTracking();
        if (systemContext is not null)
        {
            objectQuery = objectQuery.Where(item => item.DatabaseSource.SystemId == systemContext.Id);
        }

        if (request.DatabaseSourceId.HasValue)
        {
            objectQuery = objectQuery.Where(item => item.DatabaseSourceId == request.DatabaseSourceId.Value);
        }

        var schemas = await objectQuery
            .Select(item => item.SchemaName)
            .Distinct()
            .OrderBy(item => item)
            .ToArrayAsync(cancellationToken);

        var schema = NormalizeOptional(request.Schema);
        if (schema is not null)
        {
            objectQuery = objectQuery.Where(item => item.SchemaName == schema);
        }

        if (objectType.HasValue)
        {
            objectQuery = objectQuery.Where(item => item.ObjectType == objectType.Value);
        }

        if (knowledgeStatus.HasValue)
        {
            objectQuery = objectQuery.Where(item => item.KnowledgeStatus == knowledgeStatus.Value);
        }

        var search = NormalizeOptional(request.Search);
        if (search is not null)
        {
            var pattern = $"%{search}%";
            objectQuery = objectQuery.Where(item =>
                EF.Functions.Like(item.ObjectName, pattern)
                || (item.BusinessDescription != null && EF.Functions.Like(item.BusinessDescription, pattern))
                || item.Columns.Any(column =>
                    EF.Functions.Like(column.ColumnName, pattern)
                    || (column.BusinessDescription != null
                        && EF.Functions.Like(column.BusinessDescription, pattern))));
        }

        var rows = await objectQuery
            .Select(item => new DatabaseObjectListRow(
                item.Id,
                item.DatabaseSourceId,
                item.DatabaseSource.Name,
                item.DatabaseSource.Engine,
                item.SchemaName,
                item.ObjectName,
                item.ObjectType,
                item.BusinessDescription,
                item.EstimatedRows,
                item.AccessMode,
                item.KnowledgeStatus))
            .ToArrayAsync(cancellationToken);

        var objectIds = rows.Select(row => row.Id).ToArray();
        var relatedFunctionCounts = await GetRelatedFunctionCounts(objectIds, cancellationToken);
        var unknownCounts = await dbContext.UnknownItemTargets
            .AsNoTracking()
            .Where(target => target.TargetType == KnowledgeTargetType.DatabaseObject
                && objectIds.Contains(target.TargetId)
                && target.UnknownItem.Status != UnknownItemStatus.Closed)
            .GroupBy(target => target.TargetId)
            .Select(group => new { DatabaseObjectId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.DatabaseObjectId, item => item.Count, cancellationToken);
        var matchedColumns = await GetMatchedColumns(objectIds, search, cancellationToken);

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? DefaultPageSize;
        var items = ApplySort(rows, sort, unknownCounts)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(row => new DatabaseObjectListItem(
                row.Id,
                new DatabaseSourceContext(row.DatabaseSourceId, row.DatabaseSourceName, row.DatabaseSourceEngine),
                row.SchemaName,
                row.ObjectName,
                row.ObjectType.ToString(),
                row.BusinessDescription,
                row.EstimatedRows,
                row.AccessMode.ToString(),
                relatedFunctionCounts.GetValueOrDefault(row.Id),
                unknownCounts.GetValueOrDefault(row.Id),
                row.KnowledgeStatus.ToString(),
                matchedColumns.GetValueOrDefault(row.Id)))
            .ToArray();

        return new DatabaseObjectsListQueryResult(
            new DatabaseObjectsListResponse(
                new DatabaseObjectBrowseContext(systemContext, sourceContexts, schemas),
                items,
                page,
                pageSize,
                rows.Length),
            null,
            DatabaseObjectsListFailure.None);
    }

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

        var columnIds = columnRows.Select(item => item.Id).ToArray();
        var evidenceCounts = await dbContext.Evidence
            .AsNoTracking()
            .Where(item => item.SubjectType == EvidenceSubjectType.DatabaseColumn && columnIds.Contains(item.SubjectId))
            .GroupBy(item => item.SubjectId)
            .Select(group => new { ColumnId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ColumnId, item => item.Count, cancellationToken);
        var columnUnknownCounts = await dbContext.UnknownItemTargets
            .AsNoTracking()
            .Where(item => item.TargetType == KnowledgeTargetType.DatabaseColumn
                && columnIds.Contains(item.TargetId)
                && item.UnknownItem.Status != UnknownItemStatus.Closed)
            .GroupBy(item => item.TargetId)
            .Select(group => new { ColumnId = group.Key, Count = group.Count() })
            .ToDictionaryAsync(item => item.ColumnId, item => item.Count, cancellationToken);

        var columns = columnRows
            .Select(column => new DatabaseColumnSummary(
                column.Id,
                column.OrdinalPosition,
                column.ColumnName,
                column.DataType,
                column.IsNullable,
                column.BusinessDescription,
                evidenceCounts.GetValueOrDefault(column.Id),
                columnUnknownCounts.GetValueOrDefault(column.Id),
                column.KnowledgeStatus.ToString(),
                Selected: selectedColumnId == column.Id))
            .ToArray();

        var objectRelations = await dbContext.KnowledgeRelations
            .AsNoTracking()
            .Where(item => (item.SourceType == KnowledgeTargetType.DatabaseObject && item.SourceId == databaseObjectId)
                || (item.TargetType == KnowledgeTargetType.DatabaseObject && item.TargetId == databaseObjectId))
            .ToArrayAsync(cancellationToken);
        var functionRelationEntries = objectRelations
            .Where(item => item.SourceType == KnowledgeTargetType.BusinessFunction || item.TargetType == KnowledgeTargetType.BusinessFunction)
            .Select(item => new
            {
                FunctionId = item.SourceType == KnowledgeTargetType.BusinessFunction ? item.SourceId : item.TargetId,
                item.RelationType,
                item.Description,
            })
            .ToArray();
        var functionIds = functionRelationEntries.Select(item => item.FunctionId).Distinct().ToArray();
        var functionNames = await dbContext.BusinessFunctions.AsNoTracking()
            .Where(item => functionIds.Contains(item.Id))
            .Select(item => new { item.Id, item.Name })
            .ToDictionaryAsync(item => item.Id, item => item.Name, cancellationToken);
        var usedByFunctions = functionRelationEntries
            .Where(item => functionNames.ContainsKey(item.FunctionId))
            .OrderBy(item => functionNames[item.FunctionId])
            .Select(item => new UsedByFunctionSummary(item.FunctionId, functionNames[item.FunctionId], item.RelationType.ToString(), item.Description))
            .ToArray();
        var objectUnknownCount = await dbContext.UnknownItemTargets.AsNoTracking()
            .CountAsync(item => item.TargetType == KnowledgeTargetType.DatabaseObject
                && item.TargetId == databaseObjectId
                && item.UnknownItem.Status != UnknownItemStatus.Closed, cancellationToken);

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
            new DatabaseObjectContextRail(
                usedByFunctions,
                objectRelations.Count(item => item.SourceType == KnowledgeTargetType.BusinessRule || item.TargetType == KnowledgeTargetType.BusinessRule),
                objectRelations.Count(item => item.SourceType == KnowledgeTargetType.Integration || item.TargetType == KnowledgeTargetType.Integration),
                objectUnknownCount),
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

        var relationRows = await dbContext.KnowledgeRelations
            .AsNoTracking()
            .Where(item => (item.SourceType == KnowledgeTargetType.DatabaseColumn && item.SourceId == databaseColumnId)
                || (item.TargetType == KnowledgeTargetType.DatabaseColumn && item.TargetId == databaseColumnId))
            .ToArrayAsync(cancellationToken);
        var otherEndpoints = relationRows
            .Select(item => item.SourceType == KnowledgeTargetType.DatabaseColumn
                ? (item.TargetType, item.TargetId)
                : (item.SourceType, item.SourceId))
            .Distinct()
            .ToArray();
        var relatedTitles = await GetRelatedTitles(otherEndpoints, cancellationToken);
        var relations = relationRows
            .Select(item =>
            {
                var other = item.SourceType == KnowledgeTargetType.DatabaseColumn
                    ? (item.TargetType, item.TargetId)
                    : (item.SourceType, item.SourceId);
                return new ColumnRelationSummary(
                    item.Id,
                    item.RelationType.ToString(),
                    new RelatedObjectSummary(
                        other.Item1.ToString(),
                        other.Item2,
                        relatedTitles.GetValueOrDefault(other, $"{other.Item1} #{other.Item2}")));
            })
            .OrderBy(item => item.OtherObject.Title, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var unknownItems = await dbContext.UnknownItemTargets
            .AsNoTracking()
            .Where(item => item.TargetType == KnowledgeTargetType.DatabaseColumn
                && item.TargetId == databaseColumnId
                && item.UnknownItem.Status != UnknownItemStatus.Closed)
            .OrderByDescending(item => item.UnknownItem.Id)
            .Select(item => new ColumnUnknownItemSummary(item.UnknownItem.Id, item.UnknownItem.Question, item.UnknownItem.Status.ToString()))
            .ToArrayAsync(cancellationToken);

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
            relations,
            unknownItems,
            DatabaseColumnActions);
    }

    private async Task<Dictionary<(KnowledgeTargetType Type, long Id), string>> GetRelatedTitles(
        IReadOnlyList<(KnowledgeTargetType Type, long Id)> endpoints,
        CancellationToken cancellationToken)
    {
        var result = new Dictionary<(KnowledgeTargetType Type, long Id), string>();
        foreach (var endpoint in endpoints)
        {
            var title = endpoint.Type switch
            {
                KnowledgeTargetType.System => await dbContext.Systems.AsNoTracking().Where(item => item.Id == endpoint.Id).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken),
                KnowledgeTargetType.DatabaseSource => await dbContext.DatabaseSources.AsNoTracking().Where(item => item.Id == endpoint.Id).Select(item => item.System.Name + " · " + item.Name).SingleOrDefaultAsync(cancellationToken),
                KnowledgeTargetType.BusinessFunction => await dbContext.BusinessFunctions.AsNoTracking().Where(item => item.Id == endpoint.Id).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken),
                KnowledgeTargetType.DatabaseObject => await dbContext.DatabaseObjects.AsNoTracking().Where(item => item.Id == endpoint.Id).Select(item => item.SchemaName + "." + item.ObjectName).SingleOrDefaultAsync(cancellationToken),
                KnowledgeTargetType.DatabaseColumn => await dbContext.DatabaseColumns.AsNoTracking().Where(item => item.Id == endpoint.Id).Select(item => item.DatabaseObject.SchemaName + "." + item.DatabaseObject.ObjectName + "." + item.ColumnName).SingleOrDefaultAsync(cancellationToken),
                KnowledgeTargetType.BusinessRule => await dbContext.BusinessRules.AsNoTracking().Where(item => item.Id == endpoint.Id).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken),
                KnowledgeTargetType.Integration => await dbContext.Integrations.AsNoTracking().Where(item => item.Id == endpoint.Id).Select(item => item.Name).SingleOrDefaultAsync(cancellationToken),
                _ => null,
            };
            if (title is not null) result[endpoint] = title;
        }
        return result;
    }

    private static IReadOnlyList<string> ParseStringArray(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return [];
        }

        return JsonSerializer.Deserialize<string[]>(json) ?? [];
    }

    private async Task<Dictionary<long, int>> GetRelatedFunctionCounts(
        IReadOnlyCollection<long> objectIds,
        CancellationToken cancellationToken)
    {
        if (objectIds.Count == 0)
        {
            return [];
        }

        var relations = await dbContext.KnowledgeRelations
            .AsNoTracking()
            .Where(relation =>
                (relation.SourceType == KnowledgeTargetType.DatabaseObject
                    && objectIds.Contains(relation.SourceId)
                    && relation.TargetType == KnowledgeTargetType.BusinessFunction)
                || (relation.TargetType == KnowledgeTargetType.DatabaseObject
                    && objectIds.Contains(relation.TargetId)
                    && relation.SourceType == KnowledgeTargetType.BusinessFunction))
            .Select(relation => new
            {
                DatabaseObjectId = relation.SourceType == KnowledgeTargetType.DatabaseObject
                    ? relation.SourceId
                    : relation.TargetId,
                BusinessFunctionId = relation.SourceType == KnowledgeTargetType.BusinessFunction
                    ? relation.SourceId
                    : relation.TargetId,
            })
            .ToArrayAsync(cancellationToken);

        return relations
            .GroupBy(relation => relation.DatabaseObjectId)
            .ToDictionary(group => group.Key, group => group.Select(item => item.BusinessFunctionId).Distinct().Count());
    }

    private async Task<Dictionary<long, DatabaseObjectMatchedColumn>> GetMatchedColumns(
        IReadOnlyCollection<long> objectIds,
        string? search,
        CancellationToken cancellationToken)
    {
        if (objectIds.Count == 0 || search is null)
        {
            return [];
        }

        var pattern = $"%{search}%";
        var matches = await dbContext.DatabaseColumns
            .AsNoTracking()
            .Where(column => objectIds.Contains(column.DatabaseObjectId)
                && (EF.Functions.Like(column.ColumnName, pattern)
                    || (column.BusinessDescription != null
                        && EF.Functions.Like(column.BusinessDescription, pattern))))
            .OrderBy(column => column.OrdinalPosition)
            .Select(column => new
            {
                column.DatabaseObjectId,
                column.Id,
                column.ColumnName,
            })
            .ToArrayAsync(cancellationToken);

        return matches
            .GroupBy(match => match.DatabaseObjectId)
            .ToDictionary(
                group => group.Key,
                group => new DatabaseObjectMatchedColumn(group.First().Id, group.First().ColumnName));
    }

    private static Dictionary<string, string[]> ValidateListQuery(
        DatabaseObjectListQuery request,
        out DatabaseObjectType? objectType,
        out KnowledgeStatus? knowledgeStatus,
        out DatabaseObjectSort sort)
    {
        var errors = new Dictionary<string, string[]>();
        objectType = null;
        knowledgeStatus = null;
        sort = DatabaseObjectSort.ObjectNameAscending;

        if (request.SystemId.HasValue && !ApiIdParser.IsSafePositive(request.SystemId.Value))
        {
            errors["systemId"] = ["系统 ID 必须是 JavaScript 安全范围内的正整数。"];
        }

        if (request.DatabaseSourceId.HasValue && !ApiIdParser.IsSafePositive(request.DatabaseSourceId.Value))
        {
            errors["databaseSourceId"] = ["数据库来源 ID 必须是 JavaScript 安全范围内的正整数。"];
        }

        if (request.Page is < 1)
        {
            errors["page"] = ["页码必须从 1 开始。"];
        }

        if (request.PageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"];
        }

        if (request.ObjectType is not null)
        {
            if (!Enum.TryParse<DatabaseObjectType>(request.ObjectType, false, out var parsed)
                || parsed.ToString() != request.ObjectType)
            {
                errors["objectType"] = ["对象类型筛选值无效。"];
            }
            else
            {
                objectType = parsed;
            }
        }

        if (request.KnowledgeStatus is not null)
        {
            if (!Enum.TryParse<KnowledgeStatus>(request.KnowledgeStatus, false, out var parsed)
                || parsed.ToString() != request.KnowledgeStatus)
            {
                errors["knowledgeStatus"] = ["知识状态筛选值无效。"];
            }
            else
            {
                knowledgeStatus = parsed;
            }
        }

        if (request.Sort is not null && !TryParseSort(request.Sort, out sort))
        {
            errors["sort"] = ["排序值无效。"];
        }

        return errors;
    }

    private static bool TryParseSort(string value, out DatabaseObjectSort sort)
    {
        sort = value switch
        {
            "objectName:asc" => DatabaseObjectSort.ObjectNameAscending,
            "objectName:desc" => DatabaseObjectSort.ObjectNameDescending,
            "schema:asc" => DatabaseObjectSort.SchemaAscending,
            "schema:desc" => DatabaseObjectSort.SchemaDescending,
            "estimatedRows:asc" => DatabaseObjectSort.EstimatedRowsAscending,
            "estimatedRows:desc" => DatabaseObjectSort.EstimatedRowsDescending,
            "knowledgeStatus:asc" => DatabaseObjectSort.KnowledgeStatusAscending,
            "knowledgeStatus:desc" => DatabaseObjectSort.KnowledgeStatusDescending,
            "unknownCount:asc" => DatabaseObjectSort.UnknownCountAscending,
            "unknownCount:desc" => DatabaseObjectSort.UnknownCountDescending,
            _ => DatabaseObjectSort.Invalid,
        };
        return sort != DatabaseObjectSort.Invalid;
    }

    private static IEnumerable<DatabaseObjectListRow> ApplySort(
        IReadOnlyList<DatabaseObjectListRow> rows,
        DatabaseObjectSort sort,
        IReadOnlyDictionary<long, int> unknownCounts)
    {
        return sort switch
        {
            DatabaseObjectSort.ObjectNameAscending => rows.OrderBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.ObjectNameDescending => rows.OrderByDescending(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.SchemaAscending => rows.OrderBy(row => row.SchemaName, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.SchemaDescending => rows.OrderByDescending(row => row.SchemaName, StringComparer.OrdinalIgnoreCase).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.EstimatedRowsAscending => rows.OrderBy(row => row.EstimatedRows).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.EstimatedRowsDescending => rows.OrderByDescending(row => row.EstimatedRows).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.KnowledgeStatusAscending => rows.OrderBy(row => row.KnowledgeStatus).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.KnowledgeStatusDescending => rows.OrderByDescending(row => row.KnowledgeStatus).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.UnknownCountAscending => rows.OrderBy(row => unknownCounts.GetValueOrDefault(row.Id)).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            DatabaseObjectSort.UnknownCountDescending => rows.OrderByDescending(row => unknownCounts.GetValueOrDefault(row.Id)).ThenBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
            _ => rows.OrderBy(row => row.ObjectName, StringComparer.OrdinalIgnoreCase),
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private sealed record DatabaseObjectListRow(
        long Id,
        long DatabaseSourceId,
        string DatabaseSourceName,
        string DatabaseSourceEngine,
        string SchemaName,
        string ObjectName,
        DatabaseObjectType ObjectType,
        string? BusinessDescription,
        long? EstimatedRows,
        DatabaseAccessMode AccessMode,
        KnowledgeStatus KnowledgeStatus);

    private enum DatabaseObjectSort
    {
        Invalid,
        ObjectNameAscending,
        ObjectNameDescending,
        SchemaAscending,
        SchemaDescending,
        EstimatedRowsAscending,
        EstimatedRowsDescending,
        KnowledgeStatusAscending,
        KnowledgeStatusDescending,
        UnknownCountAscending,
        UnknownCountDescending,
    }
}
