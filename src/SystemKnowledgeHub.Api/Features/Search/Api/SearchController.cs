using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.Search.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Search.Api;

[ApiController]
[Route("api/search")]
public sealed class SearchController(SearchQueries queries) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<SearchKnowledgeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Search(
        [FromQuery(Name = "q")] string? query,
        [FromQuery] string? types,
        [FromQuery] int? limitPerGroup,
        CancellationToken cancellationToken)
    {
        var result = await queries.SearchKnowledge(
            new SearchKnowledgeQuery(query, types, limitPerGroup),
            cancellationToken);

        return result.FieldErrors is null
            ? Ok(result.Response)
            : BadRequest(new ApiErrorResponse(
                "validation_error",
                "查询条件无效。",
                result.FieldErrors,
                null));
    }
}
