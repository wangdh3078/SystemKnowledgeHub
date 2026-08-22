using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Dashboard.Application;
using SystemKnowledgeHub.Api.Features.Dashboard.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Dashboard.Api;

[ApiController]
[Route("api/dashboard")]
public sealed class DashboardController(DashboardQueries queries) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<DashboardResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<DashboardResponse>> GetDashboard(
        [FromQuery] long? systemId,
        CancellationToken cancellationToken)
    {
        if (systemId.HasValue && !ApiIdParser.IsSafePositive(systemId.Value))
        {
            return BadRequest(new ApiErrorResponse(
                "validation_error",
                "查询条件无效。",
                new Dictionary<string, string[]>
                {
                    ["systemId"] = ["系统 ID 必须是 JavaScript 安全范围内的正整数。"],
                },
                null));
        }

        var result = await queries.GetDashboard(new DashboardQuery(systemId), cancellationToken);
        if (result.Failure == DashboardQueryFailure.SystemNotFound)
        {
            return NotFound(new ApiErrorResponse(
                "not_found",
                "未找到指定系统。",
                null,
                new { resourceType = "System", resourceId = systemId }));
        }

        return Ok(result.Response);
    }
}
