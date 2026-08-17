using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api.Contracts;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api;

[ApiController]
[Route("api/database-columns")]
public sealed class DatabaseColumnsController(
    DatabaseKnowledgeQueries queries,
    DatabaseKnowledgeService service) : ControllerBase
{
    [HttpGet("{id}")]
    [ProducesResponseType<DatabaseColumnDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DatabaseColumnDetailResponse>> GetColumnDetail(
        string id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var databaseColumnId))
        {
            return BadRequest(new ApiErrorResponse(
                "validation_error",
                "请求内容无效。",
                new Dictionary<string, string[]>
                {
                    ["id"] = ["ID 必须是 JavaScript 安全范围内的正整数。"],
                },
                null));
        }

        var detail = await queries.GetColumnDetail(databaseColumnId, cancellationToken);
        if (detail is null)
        {
            return NotFound(new ApiErrorResponse(
                "not_found",
                "数据库字段不存在。",
                null,
                new { resourceType = "DatabaseColumn", resourceId = databaseColumnId }));
        }

        return Ok(detail);
    }

    [HttpPut("{id}/knowledge")]
    [ProducesResponseType<DatabaseColumnKnowledgeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<DatabaseColumnKnowledgeResponse>> UpdateDatabaseColumnKnowledge(
        string id,
        [FromBody] UpdateDatabaseColumnKnowledgeRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var databaseColumnId)) return BadRequest(InvalidId("id"));
        var result = await service.UpdateDatabaseColumnKnowledge(
            new UpdateDatabaseColumnKnowledgeCommand(
                databaseColumnId,
                request.BusinessDescription,
                new DatabaseKnowledgeActorContext(request.Actor?.DisplayName ?? string.Empty, request.Actor?.Role),
                request.ConcurrencyToken),
            cancellationToken);

        return result.Failure switch
        {
            UpdateDatabaseColumnKnowledgeFailure.None => Ok(result.Response),
            UpdateDatabaseColumnKnowledgeFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UpdateDatabaseColumnKnowledgeFailure.DatabaseColumnNotFound => NotFound(NotFoundError(databaseColumnId)),
            UpdateDatabaseColumnKnowledgeFailure.ConcurrencyConflict => Conflict(ConflictError("concurrencyToken", "数据库字段已被其他操作更新，请刷新后重试。")),
            _ => throw new InvalidOperationException("Unsupported database column knowledge update result."),
        };
    }

    [HttpPost("{id}/known-values")]
    [ProducesResponseType<AddColumnKnownValueResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<AddColumnKnownValueResponse>> AddColumnKnownValue(
        string id,
        [FromBody] AddColumnKnownValueRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var databaseColumnId)) return BadRequest(InvalidId("id"));
        var result = await service.AddColumnKnownValue(
            new AddColumnKnownValueCommand(
                databaseColumnId,
                request.Value ?? string.Empty,
                request.Meaning ?? string.Empty,
                request.SortOrder,
                new DatabaseKnowledgeActorContext(request.Actor?.DisplayName ?? string.Empty, request.Actor?.Role),
                request.ConcurrencyToken),
            cancellationToken);

        return result.Failure switch
        {
            AddColumnKnownValueFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            AddColumnKnownValueFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            AddColumnKnownValueFailure.DatabaseColumnNotFound => NotFound(NotFoundError(databaseColumnId)),
            AddColumnKnownValueFailure.ConcurrencyConflict => Conflict(ConflictError("concurrencyToken", "数据库字段已被其他操作更新，请刷新后重试。")),
            AddColumnKnownValueFailure.DuplicateValue => Conflict(ConflictError("value", "当前字段已存在相同已知值。")),
            _ => throw new InvalidOperationException("Unsupported add column known value result."),
        };
    }

    [HttpPost("{id}/known-values/{knownValueId}/remove")]
    [ProducesResponseType<RemoveColumnKnownValueResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<RemoveColumnKnownValueResponse>> RemoveColumnKnownValue(
        string id,
        string knownValueId,
        [FromBody] RemoveColumnKnownValueRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.TryParse(id, out var databaseColumnId)) return BadRequest(InvalidId("id"));
        if (!ApiIdParser.TryParse(knownValueId, out var parsedKnownValueId)) return BadRequest(InvalidId("knownValueId"));
        var result = await service.RemoveColumnKnownValue(
            new RemoveColumnKnownValueCommand(
                databaseColumnId,
                parsedKnownValueId,
                request.Confirmed,
                new DatabaseKnowledgeActorContext(request.Actor?.DisplayName ?? string.Empty, request.Actor?.Role),
                request.ConcurrencyToken),
            cancellationToken);

        return result.Failure switch
        {
            RemoveColumnKnownValueFailure.None => Ok(result.Response),
            RemoveColumnKnownValueFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            RemoveColumnKnownValueFailure.DatabaseColumnNotFound => NotFound(NotFoundError(databaseColumnId)),
            RemoveColumnKnownValueFailure.KnownValueNotFound => NotFound(NotFoundError(parsedKnownValueId)),
            RemoveColumnKnownValueFailure.ConcurrencyConflict => Conflict(ConflictError("concurrencyToken", "数据库字段已被其他操作更新，请刷新后重试。")),
            RemoveColumnKnownValueFailure.ReferenceInvalid => UnprocessableEntity(new ApiErrorResponse(
                "reference_invalid",
                "该已知值仍被证据或开放待确认事项精确引用，无法移除。",
                null,
                new { databaseColumnId, knownValueId = parsedKnownValueId })),
            _ => throw new InvalidOperationException("Unsupported remove column known value result."),
        };
    }

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors) =>
        new("validation_error", "请求内容无效。", fieldErrors, null);

    private static ApiErrorResponse NotFoundError(long resourceId) =>
        new("not_found", "数据库字段不存在。", null, new { resourceType = "DatabaseColumn", resourceId });

    private static ApiErrorResponse ConflictError(string field, string message) =>
        new("conflict", message, new Dictionary<string, string[]> { [field] = [message] }, null);

    private static ApiErrorResponse InvalidId(string fieldName) =>
        new("validation_error", "请求内容无效.", new Dictionary<string, string[]>
        {
            [fieldName] = ["ID 必须是 JavaScript 安全范围内的正整数。"],
        }, null);
}
