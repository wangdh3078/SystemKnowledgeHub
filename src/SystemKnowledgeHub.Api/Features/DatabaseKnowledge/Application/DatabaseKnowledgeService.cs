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

public sealed class DatabaseKnowledgeService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private const long MaximumJavaScriptSafeInteger = 9_007_199_254_740_991;

    private static readonly string[] SensitiveMarkers =
    [
        "password",
        "pwd=",
        "secret",
        "api key",
        "apikey",
        "token=",
    ];

    public async Task<CreateDatabaseSourceResult> CreateDatabaseSource(
        CreateDatabaseSourceCommand request,
        CancellationToken cancellationToken)
    {
        var name = request.Name.Trim();
        var engine = request.Engine.Trim();
        var actorName = request.Actor.DisplayName.Trim();
        var errors = ValidateSource(request, name, engine, actorName);
        if (errors.Count > 0)
        {
            return new CreateDatabaseSourceResult(null, errors, CreateDatabaseSourceFailure.Validation);
        }

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        if (!await dbContext.Systems.AnyAsync(system => system.Id == request.SystemId, cancellationToken))
        {
            return new CreateDatabaseSourceResult(null, null, CreateDatabaseSourceFailure.SystemNotFound);
        }

        if (await dbContext.DatabaseSources.AnyAsync(
                source => source.SystemId == request.SystemId && source.Name == name,
                cancellationToken))
        {
            return new CreateDatabaseSourceResult(null, null, CreateDatabaseSourceFailure.DuplicateName);
        }

        if (request.IsPrimary && await dbContext.DatabaseSources.AnyAsync(
                source => source.SystemId == request.SystemId && source.IsPrimary,
                cancellationToken))
        {
            return new CreateDatabaseSourceResult(null, null, CreateDatabaseSourceFailure.PrimaryConflict);
        }

        var now = DateTimeOffset.UtcNow;
        var source = new DatabaseSource
        {
            SystemId = request.SystemId,
            Name = name,
            Engine = engine,
            Environment = NormalizeOptional(request.Environment),
            InstanceName = NormalizeOptional(request.InstanceName),
            ServiceName = NormalizeOptional(request.ServiceName),
            DatabaseName = NormalizeOptional(request.DatabaseName),
            Description = NormalizeOptional(request.Description),
            IsPrimary = request.IsPrimary,
            CreatedAt = now,
            CreatedByUserId = request.Creator.UserId,
            CreatedByName = request.Creator.DisplayName,
            CreatedByRole = NormalizeOptional(request.Actor.Role),
            UpdatedAt = now,
            Version = 1,
        };
        dbContext.DatabaseSources.Add(source);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new CreateDatabaseSourceResult(null, null, CreateDatabaseSourceFailure.DuplicateName);
        }

        await transaction.CommitAsync(cancellationToken);

        return new CreateDatabaseSourceResult(
            new CreateDatabaseSourceResponse(
                source.Id,
                source.SystemId,
                source.Name,
                source.Engine,
                concurrencyTokenCodec.Encode(source.Version)),
            null,
            CreateDatabaseSourceFailure.None);
    }

    public async Task<RegisterDatabaseObjectResult> RegisterDatabaseObject(
        RegisterDatabaseObjectCommand request,
        CancellationToken cancellationToken)
    {
        var schemaName = request.SchemaName.Trim();
        var objectName = request.ObjectName.Trim();
        var actorName = request.Actor.DisplayName.Trim();
        var errors = ValidateObject(
            request,
            schemaName,
            objectName,
            actorName,
            out var objectType,
            out var accessMode,
            out var primaryKeyColumns,
            out var businessKeyColumns);
        if (errors.Count > 0)
        {
            return new RegisterDatabaseObjectResult(null, errors, RegisterDatabaseObjectFailure.Validation);
        }

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var source = await dbContext.DatabaseSources
            .SingleOrDefaultAsync(item => item.Id == request.DatabaseSourceId, cancellationToken);
        if (source is null)
        {
            return new RegisterDatabaseObjectResult(null, null, RegisterDatabaseObjectFailure.DatabaseSourceNotFound);
        }

        if (await dbContext.DatabaseObjects.AnyAsync(
                item => item.DatabaseSourceId == request.DatabaseSourceId
                    && item.SchemaName == schemaName
                    && item.ObjectName == objectName,
                cancellationToken))
        {
            return new RegisterDatabaseObjectResult(null, null, RegisterDatabaseObjectFailure.DuplicateObject);
        }

        var now = DateTimeOffset.UtcNow;
        var databaseObject = new DatabaseObject
        {
            DatabaseSourceId = source.Id,
            SchemaName = schemaName,
            ObjectName = objectName,
            ObjectType = objectType,
            TechnicalIdentityAlgorithmVersion = 1,
            TechnicalIdentity = $"manual:object:v1:{Guid.NewGuid():N}",
            BusinessDescription = NormalizeOptional(request.BusinessDescription),
            EstimatedRows = request.EstimatedRows,
            AccessMode = accessMode,
            PrimaryKeyColumnsJson = primaryKeyColumns.Length == 0
                ? null
                : JsonSerializer.Serialize(primaryKeyColumns),
            BusinessKeyColumnsJson = businessKeyColumns.Length == 0
                ? null
                : JsonSerializer.Serialize(businessKeyColumns),
            CreatedAt = now,
            CreatedByUserId = request.Creator.UserId,
            CreatedByName = request.Creator.DisplayName,
            CreatedByRole = NormalizeOptional(request.Actor.Role),
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = request.Creator.DisplayName,
            KnowledgeStatusChangedByRole = NormalizeOptional(request.Actor.Role) ?? "创建人",
            Version = 1,
        };
        dbContext.DatabaseObjects.Add(databaseObject);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new RegisterDatabaseObjectResult(null, null, RegisterDatabaseObjectFailure.DuplicateObject);
        }

        await transaction.CommitAsync(cancellationToken);

        return new RegisterDatabaseObjectResult(
            new RegisterDatabaseObjectResponse(
                databaseObject.Id,
                databaseObject.DatabaseSourceId,
                $"{databaseObject.SchemaName}.{databaseObject.ObjectName}",
                databaseObject.ObjectType.ToString(),
                databaseObject.KnowledgeStatus.ToString(),
                concurrencyTokenCodec.Encode(databaseObject.Version)),
            null,
            RegisterDatabaseObjectFailure.None);
    }

    public async Task<RegisterDatabaseColumnResult> RegisterDatabaseColumn(
        RegisterDatabaseColumnCommand request,
        CancellationToken cancellationToken)
    {
        var columnName = request.ColumnName.Trim();
        var dataType = request.DataType.Trim();
        var actorName = request.Actor.DisplayName.Trim();
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.DatabaseObjectId)) errors["id"] = ["数据库对象必须是有效 ID。"];
        if (request.OrdinalPosition is null or <= 0) errors["ordinalPosition"] = ["字段顺序必须大于 0。"];
        if (string.IsNullOrWhiteSpace(columnName)) errors["columnName"] = ["字段名称不能为空。"];
        if (string.IsNullOrWhiteSpace(dataType)) errors["dataType"] = ["数据类型不能为空。"];
        if (request.Nullable is null) errors["nullable"] = ["必须明确字段是否允许为空。"];
        if (string.IsNullOrWhiteSpace(actorName)) errors["actor.displayName"] = ["创建人姓名不能为空。"];
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
        {
            errors["concurrencyToken"] = ["并发令牌无效。"];
        }
        if (errors.Count > 0) return new(null, errors, RegisterDatabaseColumnFailure.Validation);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var databaseObject = await dbContext.DatabaseObjects
            .SingleOrDefaultAsync(item => item.Id == request.DatabaseObjectId, cancellationToken);
        if (databaseObject is null) return new(null, null, RegisterDatabaseColumnFailure.DatabaseObjectNotFound);
        if (databaseObject.Version != expectedVersion) return new(null, null, RegisterDatabaseColumnFailure.ConcurrencyConflict);

        if (await dbContext.DatabaseColumns.AnyAsync(item => item.DatabaseObjectId == databaseObject.Id && item.ColumnName == columnName, cancellationToken))
        {
            return new(null, null, RegisterDatabaseColumnFailure.DuplicateColumnName);
        }
        if (await dbContext.DatabaseColumns.AnyAsync(item => item.DatabaseObjectId == databaseObject.Id && item.OrdinalPosition == request.OrdinalPosition, cancellationToken))
        {
            return new(null, null, RegisterDatabaseColumnFailure.DuplicateOrdinalPosition);
        }

        var now = DateTimeOffset.UtcNow;
        var column = new DatabaseColumn
        {
            DatabaseObjectId = databaseObject.Id,
            OrdinalPosition = request.OrdinalPosition!.Value,
            ColumnName = columnName,
            DataType = dataType,
            IsNullable = request.Nullable ?? false,
            DefaultValue = NormalizeOptional(request.DefaultValue),
            DatabaseComment = NormalizeOptional(request.DatabaseComment),
            TechnicalIdentityAlgorithmVersion = 1,
            TechnicalIdentity = $"manual:column:v1:{Guid.NewGuid():N}",
            BusinessDescription = NormalizeOptional(request.BusinessDescription),
            CreatedAt = now,
            CreatedByUserId = request.Creator.UserId,
            CreatedByDisplayName = request.Creator.DisplayName,
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = request.Creator.DisplayName,
            KnowledgeStatusChangedByRole = NormalizeOptional(request.Actor.Role) ?? "创建人",
            Version = 1,
        };
        dbContext.DatabaseColumns.Add(column);
        databaseObject.UpdatedAt = now;
        databaseObject.Version++;

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new(null, null, RegisterDatabaseColumnFailure.DuplicateColumnName);
        }

        await transaction.CommitAsync(cancellationToken);

        return new(
            new RegisterDatabaseColumnResponse(
                new RegisteredDatabaseColumnResponse(column.Id, column.ColumnName, column.KnowledgeStatus.ToString(), concurrencyTokenCodec.Encode(column.Version)),
                concurrencyTokenCodec.Encode(databaseObject.Version)),
            null,
            RegisterDatabaseColumnFailure.None);
    }

    public async Task<UpdateDatabaseObjectKnowledgeResult> UpdateDatabaseObjectKnowledge(
        UpdateDatabaseObjectKnowledgeCommand request,
        CancellationToken cancellationToken)
    {
        var actorName = request.Actor.DisplayName.Trim();
        var errors = new Dictionary<string, string[]>();
        var businessKeyColumns = NormalizeIdentifiers(request.BusinessKeyColumns, "businessKeyColumns", errors);
        if (!ApiIdParser.IsSafePositive(request.DatabaseObjectId)) errors["id"] = ["数据库对象必须是有效 ID。"];
        if (request.EstimatedRows is < 0 or > MaximumJavaScriptSafeInteger)
        {
            errors["estimatedRows"] = ["估算行数必须为空或 0 至 9007199254740991 之间的整数。"];
        }
        if (!Enum.TryParse<DatabaseAccessMode>(request.AccessMode, false, out var accessMode) || accessMode.ToString() != request.AccessMode)
        {
            errors["accessMode"] = ["读写方式值无效。"];
        }
        if (string.IsNullOrWhiteSpace(actorName)) errors["actor.displayName"] = ["操作人姓名不能为空。"];
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发令牌无效。"];
        if (errors.Count > 0) return new(null, errors, UpdateDatabaseObjectKnowledgeFailure.Validation);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var databaseObject = await dbContext.DatabaseObjects
            .Include(item => item.Columns)
            .SingleOrDefaultAsync(item => item.Id == request.DatabaseObjectId, cancellationToken);
        if (databaseObject is null) return new(null, null, UpdateDatabaseObjectKnowledgeFailure.DatabaseObjectNotFound);
        if (databaseObject.Version != expectedVersion) return new(null, null, UpdateDatabaseObjectKnowledgeFailure.ConcurrencyConflict);

        var registeredNames = databaseObject.Columns.Select(item => item.ColumnName).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var missingBusinessKeys = businessKeyColumns.Where(column => !registeredNames.Contains(column)).ToArray();
        if (missingBusinessKeys.Length > 0)
        {
            return new(null, new Dictionary<string, string[]>
            {
                ["businessKeyColumns"] = [$"业务唯一键必须引用当前对象已登记字段：{string.Join("、", missingBusinessKeys)}。"],
            }, UpdateDatabaseObjectKnowledgeFailure.ReferenceInvalid);
        }

        databaseObject.BusinessDescription = NormalizeOptional(request.BusinessDescription);
        databaseObject.EstimatedRows = request.EstimatedRows;
        databaseObject.AccessMode = accessMode;
        databaseObject.BusinessKeyColumnsJson = businessKeyColumns.Length == 0 ? null : JsonSerializer.Serialize(businessKeyColumns);
        databaseObject.UpdatedAt = DateTimeOffset.UtcNow;
        databaseObject.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(
            new DatabaseObjectKnowledgeResponse(
                databaseObject.Id,
                databaseObject.BusinessDescription,
                databaseObject.EstimatedRows,
                databaseObject.AccessMode.ToString(),
                businessKeyColumns,
                databaseObject.KnowledgeStatus.ToString(),
                concurrencyTokenCodec.Encode(databaseObject.Version)),
            null,
            UpdateDatabaseObjectKnowledgeFailure.None);
    }

    public async Task<UpdateDatabaseColumnKnowledgeResult> UpdateDatabaseColumnKnowledge(
        UpdateDatabaseColumnKnowledgeCommand request,
        CancellationToken cancellationToken)
    {
        var actorName = request.Actor.DisplayName.Trim();
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.DatabaseColumnId)) errors["id"] = ["字段必须是有效 ID。"];
        if (string.IsNullOrWhiteSpace(actorName)) errors["actor.displayName"] = ["操作人姓名不能为空。"];
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发令牌无效。"];
        if (errors.Count > 0) return new(null, errors, UpdateDatabaseColumnKnowledgeFailure.Validation);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var column = await dbContext.DatabaseColumns.SingleOrDefaultAsync(item => item.Id == request.DatabaseColumnId, cancellationToken);
        if (column is null) return new(null, null, UpdateDatabaseColumnKnowledgeFailure.DatabaseColumnNotFound);
        if (column.Version != expectedVersion) return new(null, null, UpdateDatabaseColumnKnowledgeFailure.ConcurrencyConflict);

        column.BusinessDescription = NormalizeOptional(request.BusinessDescription);
        column.UpdatedAt = DateTimeOffset.UtcNow;
        column.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new(
            new DatabaseColumnKnowledgeResponse(column.Id, column.BusinessDescription, column.KnowledgeStatus.ToString(), concurrencyTokenCodec.Encode(column.Version)),
            null,
            UpdateDatabaseColumnKnowledgeFailure.None);
    }

    public async Task<AddColumnKnownValueResult> AddColumnKnownValue(
        AddColumnKnownValueCommand request,
        CancellationToken cancellationToken)
    {
        var value = request.Value.Trim();
        var meaning = request.Meaning.Trim();
        var actorName = request.Actor.DisplayName.Trim();
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.DatabaseColumnId)) errors["id"] = ["字段必须是有效 ID。"];
        if (string.IsNullOrWhiteSpace(value)) errors["value"] = ["已知值不能为空。"];
        if (string.IsNullOrWhiteSpace(meaning)) errors["meaning"] = ["值含义不能为空。"];
        if (request.SortOrder is < 0) errors["sortOrder"] = ["排序值不能小于 0。"];
        if (string.IsNullOrWhiteSpace(actorName)) errors["actor.displayName"] = ["操作人姓名不能为空。"];
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发令牌无效。"];
        if (errors.Count > 0) return new(null, errors, AddColumnKnownValueFailure.Validation);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var column = await dbContext.DatabaseColumns.SingleOrDefaultAsync(item => item.Id == request.DatabaseColumnId, cancellationToken);
        if (column is null) return new(null, null, AddColumnKnownValueFailure.DatabaseColumnNotFound);
        if (column.Version != expectedVersion) return new(null, null, AddColumnKnownValueFailure.ConcurrencyConflict);
        if (await dbContext.ColumnKnownValues.AnyAsync(item => item.DatabaseColumnId == column.Id && item.ValueText == value, cancellationToken))
        {
            return new(null, null, AddColumnKnownValueFailure.DuplicateValue);
        }

        var now = DateTimeOffset.UtcNow;
        var knownValue = new ColumnKnownValue
        {
            DatabaseColumnId = column.Id,
            ValueText = value,
            Meaning = meaning,
            SortOrder = request.SortOrder ?? 0,
            CreatedAt = now,
            UpdatedAt = now,
        };
        dbContext.ColumnKnownValues.Add(knownValue);
        column.UpdatedAt = now;
        column.Version++;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return new(null, null, AddColumnKnownValueFailure.DuplicateValue);
        }

        await transaction.CommitAsync(cancellationToken);

        return new(
            new AddColumnKnownValueResponse(
                new ColumnKnownValueWriteResponse(knownValue.Id, knownValue.ValueText, knownValue.Meaning, knownValue.SortOrder),
                column.KnowledgeStatus.ToString(),
                concurrencyTokenCodec.Encode(column.Version)),
            null,
            AddColumnKnownValueFailure.None);
    }

    public async Task<RemoveColumnKnownValueResult> RemoveColumnKnownValue(
        RemoveColumnKnownValueCommand request,
        CancellationToken cancellationToken)
    {
        var actorName = request.Actor.DisplayName.Trim();
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.DatabaseColumnId)) errors["id"] = ["字段必须是有效 ID。"];
        if (!ApiIdParser.IsSafePositive(request.KnownValueId)) errors["knownValueId"] = ["已知值必须是有效 ID。"];
        if (request.Confirmed != true) errors["confirmed"] = ["必须明确确认移除已知值。"];
        if (string.IsNullOrWhiteSpace(actorName)) errors["actor.displayName"] = ["操作人姓名不能为空。"];
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发令牌无效。"];
        if (errors.Count > 0) return new(null, errors, RemoveColumnKnownValueFailure.Validation);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var column = await dbContext.DatabaseColumns.SingleOrDefaultAsync(item => item.Id == request.DatabaseColumnId, cancellationToken);
        if (column is null) return new(null, null, RemoveColumnKnownValueFailure.DatabaseColumnNotFound);
        if (column.Version != expectedVersion) return new(null, null, RemoveColumnKnownValueFailure.ConcurrencyConflict);
        var knownValue = await dbContext.ColumnKnownValues.SingleOrDefaultAsync(item => item.Id == request.KnownValueId && item.DatabaseColumnId == column.Id, cancellationToken);
        if (knownValue is null) return new(null, null, RemoveColumnKnownValueFailure.KnownValueNotFound);

        var detailKey = $"KnownValues:{knownValue.ValueText}";
        var evidenceReferences = await dbContext.Evidence.AnyAsync(item =>
            item.SubjectType == EvidenceSubjectType.DatabaseColumn
            && item.SubjectId == column.Id
            && item.SubjectDetailKey == detailKey,
            cancellationToken);
        var unknownReferences = await dbContext.KnowledgeUpdates.AnyAsync(item =>
            item.TargetType == KnowledgeTargetType.DatabaseColumn
            && item.TargetId == column.Id
            && item.SubjectDetailKey == detailKey
            && item.UnknownItem.Status != UnknownItemStatus.Closed,
            cancellationToken);
        if (evidenceReferences || unknownReferences)
        {
            return new(null, null, RemoveColumnKnownValueFailure.ReferenceInvalid);
        }

        dbContext.ColumnKnownValues.Remove(knownValue);
        column.UpdatedAt = DateTimeOffset.UtcNow;
        column.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);

        var remaining = await dbContext.ColumnKnownValues
            .AsNoTracking()
            .Where(item => item.DatabaseColumnId == column.Id)
            .OrderBy(item => item.SortOrder)
            .ThenBy(item => item.ValueText)
            .Select(item => new ColumnKnownValueWriteResponse(item.Id, item.ValueText, item.Meaning, item.SortOrder))
            .ToArrayAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(new RemoveColumnKnownValueResponse(column.Id, remaining, concurrencyTokenCodec.Encode(column.Version)), null, RemoveColumnKnownValueFailure.None);
    }

    private static Dictionary<string, string[]> ValidateSource(
        CreateDatabaseSourceCommand request,
        string name,
        string engine,
        string actorName)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(request.SystemId))
        {
            errors["systemId"] = ["所属系统必须是有效 ID。"];
        }

        if (string.IsNullOrWhiteSpace(name))
        {
            errors["name"] = ["数据库来源名称不能为空。"];
        }

        if (string.IsNullOrWhiteSpace(engine))
        {
            errors["engine"] = ["数据库类型不能为空。"];
        }

        if (string.IsNullOrWhiteSpace(actorName))
        {
            errors["actor.displayName"] = ["创建人姓名不能为空。"];
        }

        var credentialFields = new Dictionary<string, string?>
        {
            ["name"] = request.Name,
            ["environment"] = request.Environment,
            ["instanceName"] = request.InstanceName,
            ["serviceName"] = request.ServiceName,
            ["databaseName"] = request.DatabaseName,
            ["description"] = request.Description,
        };
        foreach (var field in credentialFields.Where(item => ContainsSensitiveMarker(item.Value)))
        {
            errors[field.Key] = ["不得登记连接密码、密钥或凭据。"];
        }

        return errors;
    }

    private static Dictionary<string, string[]> ValidateObject(
        RegisterDatabaseObjectCommand request,
        string schemaName,
        string objectName,
        string actorName,
        out DatabaseObjectType objectType,
        out DatabaseAccessMode accessMode,
        out string[] primaryKeyColumns,
        out string[] businessKeyColumns)
    {
        var errors = new Dictionary<string, string[]>();
        objectType = default;
        accessMode = default;
        primaryKeyColumns = NormalizeIdentifiers(request.PrimaryKeyColumns, "primaryKeyColumns", errors);
        businessKeyColumns = NormalizeIdentifiers(request.BusinessKeyColumns, "businessKeyColumns", errors);

        if (!ApiIdParser.IsSafePositive(request.DatabaseSourceId))
        {
            errors["databaseSourceId"] = ["数据库来源必须是有效 ID。"];
        }

        if (string.IsNullOrWhiteSpace(schemaName))
        {
            errors["schemaName"] = ["Schema 名称不能为空。"];
        }

        if (string.IsNullOrWhiteSpace(objectName))
        {
            errors["objectName"] = ["对象名称不能为空。"];
        }

        if (!Enum.TryParse<DatabaseObjectType>(request.ObjectType, false, out objectType)
            || objectType.ToString() != request.ObjectType)
        {
            errors["objectType"] = ["对象类型必须是 Table 或 View。"];
        }

        if (string.IsNullOrWhiteSpace(request.AccessMode))
        {
            accessMode = DatabaseAccessMode.Unknown;
        }
        else if (!Enum.TryParse<DatabaseAccessMode>(request.AccessMode, false, out accessMode)
            || accessMode.ToString() != request.AccessMode)
        {
            errors["accessMode"] = ["读写方式值无效。"];
        }

        if (request.EstimatedRows is < 0)
        {
            errors["estimatedRows"] = ["估算行数不能小于 0。"];
        }

        if (string.IsNullOrWhiteSpace(actorName))
        {
            errors["actor.displayName"] = ["创建人姓名不能为空。"];
        }

        return errors;
    }

    private static string[] NormalizeIdentifiers(
        IReadOnlyList<string>? values,
        string fieldName,
        IDictionary<string, string[]> errors)
    {
        if (values is null)
        {
            return [];
        }

        var normalized = values.Select(value => value?.Trim() ?? string.Empty).ToArray();
        if (normalized.Any(string.IsNullOrWhiteSpace))
        {
            errors[fieldName] = ["键字段名称不能为空。"];
            return [];
        }

        if (normalized.Distinct(StringComparer.OrdinalIgnoreCase).Count() != normalized.Length)
        {
            errors[fieldName] = ["键字段名称不能重复。"];
            return [];
        }

        return normalized;
    }

    private static bool ContainsSensitiveMarker(string? value)
    {
        return value is not null && SensitiveMarkers.Any(marker =>
            value.Contains(marker, StringComparison.OrdinalIgnoreCase));
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }
}
