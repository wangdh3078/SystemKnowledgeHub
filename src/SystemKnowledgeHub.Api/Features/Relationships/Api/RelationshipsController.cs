using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Relationships.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Relationships.Api;

[ApiController]
[Route("api/relationships")]
public sealed class RelationshipsController(RelationshipQueries queries, RelationshipService service, ICurrentUserContext currentUserContext) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetRelated(
        [FromQuery] string? objectType,
        [FromQuery] long objectId,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetRelatedKnowledge(objectType, objectId, cancellationToken);
        return result.Failure switch
        {
            RelationshipFailure.None => Ok(result.Items),
            RelationshipFailure.Validation => BadRequest(Error("validation_error", result.Message ?? "查询条件无效。")),
            RelationshipFailure.NotFound => NotFound(Error("not_found", result.Message ?? "未找到对象。")),
            _ => throw new InvalidOperationException("Unsupported related knowledge query result."),
        };
    }

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

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost]
    public async Task<IActionResult> Add([FromBody] AddRelationshipRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await ResolveCurrentUser(cancellationToken);
        if (currentUser is null) return Unauthorized(Error("unauthenticated", "无法解析当前操作者。"));
        var result = await service.Add(new(
            Target(request.Source), request.RelationType ?? string.Empty, Target(request.Target), request.Description,
            new(currentUser.DisplayName, currentUser.AccessLevel)), cancellationToken);
        return Command(result, created: true);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id}/description")]
    public async Task<IActionResult> UpdateDescription(long id, [FromBody] UpdateRelationshipDescriptionRequest request, CancellationToken cancellationToken)
    {
        if (await ResolveCurrentUser(cancellationToken) is null) return Unauthorized(Error("unauthenticated", "无法解析当前操作者。"));
        var result = await service.UpdateDescription(new(id, request.Description, request.ConcurrencyToken ?? string.Empty), cancellationToken);
        return Command(result);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id}/knowledge-status")]
    public async Task<IActionResult> ChangeStatus(long id, [FromBody] ChangeRelationshipStatusRequest request, CancellationToken cancellationToken)
    {
        var currentUser = await ResolveCurrentUser(cancellationToken);
        if (currentUser is null) return Unauthorized(Error("unauthenticated", "无法解析当前操作者。"));
        var result = await service.ChangeStatus(new(id, request.TargetStatus ?? string.Empty, request.Reason,
            new(currentUser.DisplayName, currentUser.AccessLevel, DateTimeOffset.UtcNow),
            request.ConcurrencyToken ?? string.Empty), cancellationToken);
        return Command(result);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpDelete("{id:long}")]
    public async Task<IActionResult> Delete(long id, CancellationToken cancellationToken)
    {
        var result = await service.Delete(id, cancellationToken);
        return result.Failure switch
        {
            RelationshipFailure.None => Ok(new { }),
            RelationshipFailure.Validation => BadRequest(new ApiErrorResponse("validation_error", "请求内容无效。", result.FieldErrors, null)),
            RelationshipFailure.NotFound => NotFound(Error("not_found", "未找到指定关系。")),
            _ => throw new InvalidOperationException("Unsupported relationship delete result."),
        };
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
    private async Task<SystemKnowledgeHub.Api.Features.Users.Application.Models.CurrentUserResponse?> ResolveCurrentUser(CancellationToken cancellationToken)
        => (await currentUserContext.ResolveAsync(cancellationToken)).CurrentUser;
    private static ApiErrorResponse Error(string code, string message, object? details = null)
        => new(code, message, null, details);
}
