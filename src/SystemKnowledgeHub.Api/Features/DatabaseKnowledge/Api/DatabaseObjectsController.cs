using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api.Contracts;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api;

[ApiController]
[Route("api/database-objects")]
public sealed class DatabaseObjectsController(
    DatabaseKnowledgeQueries queries,
    DatabaseKnowledgeService service,
    DatabaseKnowledgeDeleteService deleteService,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> DeleteDatabaseObject(long id, [FromBody] DeleteDatabaseObjectRequest request, CancellationToken cancellationToken)
    {
        var actor = await CurrentUserApiResolution.ResolveSoftDeleteActor(currentUserContext, cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await deleteService.DeleteDatabaseObject(id, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        return result.Failure switch
        {
            SoftDeleteFailure.None => NoContent(),
            SoftDeleteFailure.Validation => BadRequest(SoftDeleteApiResponses.Validation(result.FieldErrors!)),
            SoftDeleteFailure.NotFound => NotFound(SoftDeleteApiResponses.NotFound("DatabaseObject", id)),
            SoftDeleteFailure.Forbidden => StatusCode(StatusCodes.Status403Forbidden, SoftDeleteApiResponses.Forbidden("DatabaseObject", id)),
            SoftDeleteFailure.Conflict => Conflict(SoftDeleteApiResponses.Conflict("DatabaseObject", id)),
            SoftDeleteFailure.Dependencies => UnprocessableEntity(SoftDeleteApiResponses.Dependencies("DatabaseObject", id, result.Blockers!)),
            _ => throw new InvalidOperationException("Unsupported DatabaseObject delete result."),
        };
    }

    [HttpGet]
    [ProducesResponseType<DatabaseObjectsListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DatabaseObjectsListResponse>> GetDatabaseObjectsList(
        [FromQuery] string? systemId,
        [FromQuery] string? databaseSourceId,
        [FromQuery] string? schema,
        [FromQuery] string? objectType,
        [FromQuery] string? knowledgeStatus,
        [FromQuery] string? search,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!TryParseOptionalId(systemId, "systemId", out var parsedSystemId, out var invalidId)
            || !TryParseOptionalId(databaseSourceId, "databaseSourceId", out var parsedSourceId, out invalidId))
        {
            return BadRequest(invalidId!);
        }

        var result = await queries.GetDatabaseObjectsList(
            new DatabaseObjectListQuery(
                parsedSystemId,
                parsedSourceId,
                schema,
                objectType,
                knowledgeStatus,
                search,
                sort,
                page,
                pageSize),
            cancellationToken);

        return result.Failure switch
        {
            DatabaseObjectsListFailure.None => Ok(result.Response),
            DatabaseObjectsListFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            DatabaseObjectsListFailure.SystemNotFound => NotFound(NotFoundError("System", parsedSystemId!.Value)),
            DatabaseObjectsListFailure.DatabaseSourceNotFound => NotFound(NotFoundError("DatabaseSource", parsedSourceId!.Value)),
            DatabaseObjectsListFailure.DatabaseSourceOutsideSystem => UnprocessableEntity(new ApiErrorResponse(
                "reference_invalid",
                "数据库来源不属于当前系统上下文。",
                null,
                new { systemId = parsedSystemId, databaseSourceId = parsedSourceId })),
            _ => throw new InvalidOperationException("Unsupported database objects list result."),
        };
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost]
    [ProducesResponseType<RegisterDatabaseObjectResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterDatabaseObjectResponse>> RegisterDatabaseObject(
        [FromBody] RegisterDatabaseObjectRequest request,
        CancellationToken cancellationToken)
    {
        var creator = await CurrentUserApiResolution.ResolveCreator(currentUserContext, cancellationToken);
        if (creator.Error is not null) return StatusCode(creator.StatusCode!.Value, creator.Error);

        var result = await service.RegisterDatabaseObject(
            new RegisterDatabaseObjectCommand(
                request.DatabaseSourceId ?? 0,
                request.SchemaName ?? string.Empty,
                request.ObjectName ?? string.Empty,
                request.ObjectType ?? string.Empty,
                request.EstimatedRows,
                request.AccessMode ?? string.Empty,
                request.PrimaryKeyColumns,
                request.BusinessKeyColumns,
                request.BusinessDescription,
                new DatabaseKnowledgeActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role),
                creator.Creator!),
            cancellationToken);

        return result.Failure switch
        {
            RegisterDatabaseObjectFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            RegisterDatabaseObjectFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            RegisterDatabaseObjectFailure.DatabaseSourceNotFound => UnprocessableEntity(new ApiErrorResponse(
                "reference_invalid",
                "数据库来源不存在或已不可用。",
                null,
                new { resourceType = "DatabaseSource", resourceId = request.DatabaseSourceId ?? 0 })),
            RegisterDatabaseObjectFailure.DuplicateObject => Conflict(ConflictError(
                "objectName",
                "同一数据库来源下 Schema 与对象名称组合已存在。")),
            _ => throw new InvalidOperationException("Unsupported database object registration result."),
        };
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost("{id}/columns")]
    [ProducesResponseType<RegisterDatabaseColumnResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<RegisterDatabaseColumnResponse>> RegisterDatabaseColumn(
        string id,
        [FromBody] RegisterDatabaseColumnRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var databaseObjectId)) return BadRequest(InvalidId("id"));

        var creator = await CurrentUserApiResolution.ResolveCreator(currentUserContext, cancellationToken);
        if (creator.Error is not null) return StatusCode(creator.StatusCode!.Value, creator.Error);

        var result = await service.RegisterDatabaseColumn(
            new RegisterDatabaseColumnCommand(
                databaseObjectId,
                request.OrdinalPosition,
                request.ColumnName ?? string.Empty,
                request.DataType ?? string.Empty,
                request.Nullable,
                request.DefaultValue,
                request.DatabaseComment,
                request.BusinessDescription,
                new DatabaseKnowledgeActorContext(request.Actor?.DisplayName ?? string.Empty, request.Actor?.Role),
                request.ConcurrencyToken,
                creator.Creator!),
            cancellationToken);

        return result.Failure switch
        {
            RegisterDatabaseColumnFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            RegisterDatabaseColumnFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            RegisterDatabaseColumnFailure.DatabaseObjectNotFound => NotFound(NotFoundError("DatabaseObject", databaseObjectId)),
            RegisterDatabaseColumnFailure.ConcurrencyConflict => Conflict(ConflictError("concurrencyToken", "数据库对象已被其他操作更新，请刷新后重试。")),
            RegisterDatabaseColumnFailure.DuplicateColumnName => Conflict(ConflictError("columnName", "当前对象下字段名称已存在。")),
            RegisterDatabaseColumnFailure.DuplicateOrdinalPosition => Conflict(ConflictError("ordinalPosition", "当前对象下字段顺序已存在。")),
            _ => throw new InvalidOperationException("Unsupported database column registration result."),
        };
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id}/knowledge")]
    [ProducesResponseType<DatabaseObjectKnowledgeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DatabaseObjectKnowledgeResponse>> UpdateDatabaseObjectKnowledge(
        string id,
        [FromBody] UpdateDatabaseObjectKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var databaseObjectId)) return BadRequest(InvalidId("id"));
        if (!TryParseEstimatedRows(request.EstimatedRows, out var estimatedRows, out var estimatedRowsError))
        {
            return BadRequest(estimatedRowsError);
        }

        var result = await service.UpdateDatabaseObjectKnowledge(
            new UpdateDatabaseObjectKnowledgeCommand(
                databaseObjectId,
                request.BusinessDescription,
                estimatedRows,
                request.AccessMode ?? string.Empty,
                request.BusinessKeyColumns,
                new DatabaseKnowledgeActorContext(request.Actor?.DisplayName ?? string.Empty, request.Actor?.Role),
                request.ConcurrencyToken),
            cancellationToken);

        return result.Failure switch
        {
            UpdateDatabaseObjectKnowledgeFailure.None => Ok(result.Response),
            UpdateDatabaseObjectKnowledgeFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UpdateDatabaseObjectKnowledgeFailure.DatabaseObjectNotFound => NotFound(NotFoundError("DatabaseObject", databaseObjectId)),
            UpdateDatabaseObjectKnowledgeFailure.ConcurrencyConflict => Conflict(ConflictError("concurrencyToken", "数据库对象已被其他操作更新，请刷新后重试。")),
            UpdateDatabaseObjectKnowledgeFailure.ReferenceInvalid => UnprocessableEntity(new ApiErrorResponse(
                "reference_invalid",
                "业务唯一键必须引用当前对象已登记字段。",
                result.FieldErrors,
                null)),
            _ => throw new InvalidOperationException("Unsupported database object knowledge update result."),
        };
    }

    [HttpGet("{id}")]
    [ProducesResponseType<DatabaseObjectDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<DatabaseObjectDetailResponse>> GetDatabaseObjectDetail(
        string id,
        [FromQuery] string? selectedColumnId,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var databaseObjectId))
        {
            return BadRequest(InvalidId("id"));
        }

        long? parsedSelectedColumnId = null;
        if (selectedColumnId is not null)
        {
            if (!ApiIdParser.TryParse(selectedColumnId, out var columnId))
            {
                return BadRequest(InvalidId("selectedColumnId"));
            }

            parsedSelectedColumnId = columnId;
        }

        var result = await queries.GetDatabaseObjectDetail(
            databaseObjectId,
            parsedSelectedColumnId,
            cancellationToken);

        if (result.SelectedColumnInvalid)
        {
            return UnprocessableEntity(new ApiErrorResponse(
                "reference_invalid",
                "所选字段不属于当前数据库对象。",
                null,
                new { databaseObjectId, selectedColumnId = parsedSelectedColumnId }));
        }

        if (result.Detail is null)
        {
            return NotFound(new ApiErrorResponse(
                "not_found",
                "数据库对象不存在。",
                null,
                new { resourceType = "DatabaseObject", resourceId = databaseObjectId }));
        }

        return Ok(result.Detail);
    }

    private static bool TryParseOptionalId(
        string? value,
        string fieldName,
        out long? parsedId,
        out ApiErrorResponse? error)
    {
        parsedId = null;
        error = null;
        if (value is null)
        {
            return true;
        }

        if (!ApiIdParser.TryParse(value, out var id))
        {
            error = InvalidId(fieldName);
            return false;
        }

        parsedId = id;
        return true;
    }

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("validation_error", "请求内容无效。", fieldErrors, null);

    private static ApiErrorResponse NotFoundError(string resourceType, long resourceId) =>
        new("not_found", "未找到指定资源。", null, new { resourceType, resourceId });

    private static ApiErrorResponse ConflictError(string field, string message) =>
        new("conflict", message, new Dictionary<string, string[]> { [field] = [message] }, null);

    private static bool TryParseEstimatedRows(
        JsonElement? value,
        out long? estimatedRows,
        out ApiErrorResponse? error)
    {
        const long maximumSafeInteger = 9_007_199_254_740_991;
        estimatedRows = null;
        error = null;
        if (value is null || value.Value.ValueKind == JsonValueKind.Null)
        {
            return true;
        }

        if (value.Value.ValueKind == JsonValueKind.Number
            && value.Value.TryGetInt64(out var parsed)
            && parsed >= 0
            && parsed <= maximumSafeInteger)
        {
            estimatedRows = parsed;
            return true;
        }

        error = ValidationError(new Dictionary<string, string[]>
        {
            ["estimatedRows"] = ["估算行数必须为空或 0 至 9007199254740991 之间的整数。"],
        });
        return false;
    }

    private static ApiErrorResponse InvalidId(string fieldName)
    {
        return new ApiErrorResponse(
            "validation_error",
            "请求内容无效。",
            new Dictionary<string, string[]>
            {
                [fieldName] = ["ID 必须是 JavaScript 安全范围内的正整数。"],
            },
            null);
    }
}
