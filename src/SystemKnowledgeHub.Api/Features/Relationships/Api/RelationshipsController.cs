using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Relationships.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Relationships.Api;

[ApiController]
[Route("api/relationships")]
public sealed class RelationshipsController(RelationshipQueries queries, RelationshipService service) : ControllerBase
{
    [HttpGet("{id}")]
    public async Task<ActionResult<RelationshipDetailResponse>> Get(long id, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return BadRequest(Error("validation_error", "Relationship ID 无效。"));
        var result = await queries.GetRelationshipDetail(id, cancellationToken);
        return result.Failure switch
        {
            RelationshipFailure.None => Ok(result.Response),
            RelationshipFailure.NotFound => NotFound(Error("not_found", "未找到指定关系。")),
            RelationshipFailure.ReferenceInvalid => UnprocessableEntity(Error("reference_invalid", result.Message ?? "关系端点无效。")),
            _ => throw new InvalidOperationException("Unsupported relationship query result."),
        };
    }

    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddRelationshipRequest request, CancellationToken cancellationToken)
    {
        var result = await service.Add(new(
            Target(request.Source), request.RelationType ?? string.Empty, Target(request.Target), request.Description,
            request.Actor is null ? null : new(request.Actor.DisplayName ?? string.Empty, request.Actor.Role)), cancellationToken);
        return Command(result, created: true);
    }

    [HttpPut("{id}/description")]
    public async Task<IActionResult> UpdateDescription(long id, [FromBody] UpdateRelationshipDescriptionRequest request, CancellationToken cancellationToken)
    {
        var result = await service.UpdateDescription(new(id, request.Description,
            request.Actor is null ? null : new(request.Actor.DisplayName ?? string.Empty, request.Actor.Role),
            request.ConcurrencyToken ?? string.Empty), cancellationToken);
        return Command(result);
    }

    [HttpPut("{id}/knowledge-status")]
    public async Task<IActionResult> ChangeStatus(long id, [FromBody] ChangeRelationshipStatusRequest request, CancellationToken cancellationToken)
    {
        var result = await service.ChangeStatus(new(id, request.TargetStatus ?? string.Empty, request.Reason,
            request.Actor is null ? null : new(
                request.Actor.DisplayName ?? string.Empty,
                request.Actor.RoleOrIdentity ?? string.Empty,
                request.Actor.OccurredAt ?? default),
            request.ConcurrencyToken ?? string.Empty), cancellationToken);
        return Command(result);
    }

    private IActionResult Command(RelationshipCommandResult result, bool created = false)
    {
        return result.Failure switch
        {
            RelationshipFailure.None => created
                ? CreatedAtAction(nameof(Get), new { id = ((AddRelationshipResponse)result.Response!).Id }, result.Response)
                : Ok(result.Response),
            RelationshipFailure.Validation => BadRequest(new ApiErrorResponse("validation_error", "请求内容无效。", result.FieldErrors, null)),
            RelationshipFailure.NotFound => NotFound(Error("not_found", result.Message ?? "未找到指定关系或端点。")),
            RelationshipFailure.ReferenceInvalid => UnprocessableEntity(Error("reference_invalid", result.Message ?? "关系端点无效。", result.Details)),
            RelationshipFailure.Duplicate => Conflict(Error("conflict", result.Message ?? "关系已存在。", result.Details)),
            RelationshipFailure.Conflict => Conflict(Error("conflict", result.Message ?? "内容已被修改，请重新加载。", result.Details)),
            RelationshipFailure.BusinessRuleViolation => UnprocessableEntity(Error("business_rule_violation", result.Message ?? "状态变更不满足规则。", result.Details)),
            _ => throw new InvalidOperationException("Unsupported relationship command result."),
        };
    }

    private static RelationshipTargetCommand? Target(RelationshipTargetRequest? target)
        => target is null ? null : new(target.Type ?? string.Empty, target.Id);
    private static ApiErrorResponse Error(string code, string message, object? details = null)
        => new(code, message, null, details);
}
