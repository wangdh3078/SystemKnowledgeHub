using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Api;

[ApiController]
[Route("api/database-columns")]
public sealed class DatabaseColumnsController(DatabaseKnowledgeQueries queries) : ControllerBase
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
}
