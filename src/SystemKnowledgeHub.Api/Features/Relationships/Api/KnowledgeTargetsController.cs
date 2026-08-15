using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Relationships.Api;

[ApiController]
[Route("api/knowledge-targets")]
public sealed class KnowledgeTargetsController(RelationshipQueries queries) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> Search(
        [FromQuery] string? purpose, [FromQuery(Name = "q")] string? query, [FromQuery] long? systemId,
        [FromQuery] string? sourceType, [FromQuery] long? sourceId, [FromQuery] string? relationType,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var result = await queries.SearchKnowledgeTargets(new(purpose, query, systemId, sourceType, sourceId, relationType, page, pageSize), cancellationToken);
        return result.Failure switch
        {
            RelationshipFailure.None => Ok(result.Response),
            RelationshipFailure.Validation => BadRequest(new ApiErrorResponse("validation_error", "查询条件无效。", result.FieldErrors, null)),
            RelationshipFailure.NotFound => NotFound(new ApiErrorResponse("not_found", result.Message ?? "未找到 Source。", null, null)),
            RelationshipFailure.ReferenceInvalid => UnprocessableEntity(new ApiErrorResponse("reference_invalid", result.Message ?? "关系上下文无效。", null, null)),
            _ => throw new InvalidOperationException("Unsupported knowledge target query result."),
        };
    }
}
