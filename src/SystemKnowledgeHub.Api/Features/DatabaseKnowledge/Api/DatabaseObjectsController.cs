using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api;

[ApiController]
[Route("api/database-objects")]
public sealed class DatabaseObjectsController(DatabaseKnowledgeQueries queries) : ControllerBase
{
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
