using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Api;

[ApiController]
[Route("api/database-discovery")]
[Authorize]
public sealed class DatabaseDiscoverySyncController(
    DatabaseDiscoverySyncService service,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [HttpGet("reconciliation")]
    public async Task<ActionResult<DatabaseDiscoveryReconciliationPageResponse>> GetReconciliation(
        [FromQuery] long profileId,
        [FromQuery] string? category,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var error = ValidatePagination(page, pageSize);
        if (error is not null) return BadRequest(error);
        var result = await service.GetReconciliation(profileId, category, search, page, pageSize, cancellationToken);
        return result is null ? NotFound(ApiError("not_found", "未找到连接配置或可同步的完整快照。")) : Ok(result);
    }

    [HttpPost("reconciliation/object-groups/query")]
    public async Task<ActionResult<DatabaseDiscoveryReconciliationObjectGroupPageResponse>> QueryObjectGroups(
        [FromBody] DatabaseDiscoveryReconciliationObjectQueryRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidatePagination(request.Page, request.PageSize, maximumPageSize: 200);
        if (error is not null) return BadRequest(error);
        return MapRead(await service.QueryObjectGroups(request, cancellationToken));
    }

    [HttpPost("reconciliation/object-children/query")]
    public async Task<ActionResult<DatabaseDiscoveryReconciliationObjectChildrenPageResponse>> QueryObjectChildren(
        [FromBody] DatabaseDiscoveryReconciliationObjectChildrenQueryRequest request,
        CancellationToken cancellationToken)
    {
        var error = ValidatePagination(request.Page, request.PageSize, maximumPageSize: 200);
        if (error is not null) return BadRequest(error);
        return MapRead(await service.QueryObjectChildren(request, cancellationToken));
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("reconciliation/object-selection")]
    public async Task<ActionResult<DatabaseDiscoveryReconciliationObjectSelectionResponse>> ExpandObjectSelection(
        [FromBody] DatabaseDiscoveryReconciliationObjectSelectionRequest request,
        CancellationToken cancellationToken) =>
        MapRead(await service.ExpandObjectSelection(request, cancellationToken));

    [HttpGet("sync-plans")]
    public async Task<ActionResult<DatabaseDiscoverySyncPlanPageResponse>> ListPlans(
        [FromQuery] long? profileId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var error = ValidatePagination(page, pageSize);
        if (error is not null) return BadRequest(error);
        return Ok(await service.ListPlans(profileId, page, pageSize, cancellationToken));
    }

    [HttpGet("sync-plans/{id:long}")]
    public async Task<ActionResult<DatabaseDiscoverySyncPlanResponse>> GetPlan(long id, CancellationToken cancellationToken)
    {
        var result = await service.GetPlan(id, cancellationToken);
        return result is null ? NotFound(ApiError("not_found", "未找到同步计划。")) : Ok(result);
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("sync-plans")]
    public async Task<ActionResult<DatabaseDiscoverySyncPlanResponse>> CreatePlan(
        [FromBody] CreateDatabaseDiscoverySyncPlanRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await service.CreatePlan(request, actor.Actor!, cancellationToken);
        return Map(result, created: true);
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPut("sync-plans/{id:long}/actions")]
    public async Task<ActionResult<DatabaseDiscoverySyncPlanResponse>> UpdateSelections(
        long id,
        [FromBody] UpdateDatabaseDiscoverySyncSelectionsRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        return Map(await service.UpdateSelections(id, request, actor.Actor!, cancellationToken));
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("sync-plans/{id:long}/preview")]
    public async Task<ActionResult<DatabaseDiscoverySyncPlanResponse>> Preview(
        long id,
        [FromBody] DatabaseDiscoverySyncPlanMutationRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        return Map(await service.Preview(id, request.ConcurrencyToken, actor.Actor!, cancellationToken));
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("sync-plans/{id:long}/confirm")]
    public async Task<ActionResult<DatabaseDiscoverySyncPlanResponse>> Confirm(
        long id,
        [FromBody] ConfirmDatabaseDiscoverySyncPlanRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        return Map(await service.Confirm(id, request, actor.Actor!, cancellationToken));
    }

    [Authorize(Policy = AccessPolicies.Editor)]
    [HttpPost("sync-plans/{id:long}/apply")]
    public async Task<ActionResult<DatabaseDiscoverySyncPlanResponse>> Apply(
        long id,
        [FromBody] ApplyDatabaseDiscoverySyncPlanRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        return Map(await service.Apply(id, request, actor.Actor!, cancellationToken));
    }

    private ActionResult<DatabaseDiscoverySyncPlanResponse> Map(
        DatabaseDiscoverySyncOperationResult<DatabaseDiscoverySyncPlanResponse> result,
        bool created = false) => result.Failure switch
    {
        DatabaseDiscoverySyncFailure.None when created => CreatedAtAction(nameof(GetPlan), new { id = result.Response!.Id }, result.Response),
        DatabaseDiscoverySyncFailure.None => Ok(result.Response),
        DatabaseDiscoverySyncFailure.Validation => BadRequest(new ApiErrorResponse("validation_error", "请求内容无效。", result.FieldErrors, null)),
        DatabaseDiscoverySyncFailure.NotFound => NotFound(ApiError("not_found", "未找到同步计划。")),
        DatabaseDiscoverySyncFailure.AlreadyApplied => Conflict(ApiError("AlreadyApplied", "同步计划已经应用。")),
        DatabaseDiscoverySyncFailure.NotConfirmed => Conflict(ApiError("ConfirmationRequired", "必须确认当前预览后才能应用。")),
        DatabaseDiscoverySyncFailure.UnsupportedIdentifierCollision => Conflict(ApiError("UnsupportedIdentifierCollision", "技术标识与现有知识发生无法安全合并的碰撞。")),
        DatabaseDiscoverySyncFailure.OrdinalCollision => Conflict(ApiError("OrdinalCollision", "字段名称或序号发生冲突。")),
        DatabaseDiscoverySyncFailure.LatestSnapshotChanged => Conflict(ApiError("LatestSnapshotChanged", "最新完整快照已变化，计划已失效。")),
        _ => Conflict(ApiError(result.ReasonCode ?? "ConcurrencyConflict", "同步计划或目标数据已变化，请重新预览。")),
    };

    private ActionResult<T> MapRead<T>(DatabaseDiscoverySyncOperationResult<T> result) => result.Failure switch
    {
        DatabaseDiscoverySyncFailure.None => Ok(result.Response),
        DatabaseDiscoverySyncFailure.Validation when result.ReasonCode == "ActionLimitExceeded" =>
            BadRequest(ApiError("ActionLimitExceeded", "该选择将超过单个同步计划允许的最大操作数，请减少选择范围。")),
        DatabaseDiscoverySyncFailure.Validation =>
            BadRequest(new ApiErrorResponse("validation_error", "请求内容无效。", result.FieldErrors, null)),
        DatabaseDiscoverySyncFailure.NotFound =>
            NotFound(ApiError("not_found", "未找到连接配置、可同步的完整快照或数据库对象。")),
        DatabaseDiscoverySyncFailure.LatestSnapshotChanged =>
            Conflict(ApiError("LatestSnapshotChanged", "最新完整快照已变化，请重新加载 Reconciliation。")),
        _ => Conflict(ApiError(result.ReasonCode ?? "SelectionNoLongerApplicable", "当前选择已变化，请重新加载 Reconciliation。")),
    };

    private async Task<ActorResolution> ResolveActor(CancellationToken cancellationToken)
    {
        var result = await CurrentUserApiResolution.ResolveSoftDeleteActor(currentUserContext, cancellationToken);
        return result.Error is null
            ? new(new DatabaseDiscoverySyncActor(result.Actor!.UserId, result.Actor.DisplayName, result.Actor.AccessLevel.ToString()), null, null)
            : new(null, result.StatusCode, result.Error);
    }

    private static ApiErrorResponse? ValidatePagination(
        int? page,
        int? pageSize,
        int maximumPageSize = 100)
    {
        var errors = new Dictionary<string, string[]>();
        if (page is <= 0 or > 1_000_000) errors["page"] = ["page 必须是正整数。"];
        if (pageSize is <= 0 || pageSize > maximumPageSize)
        {
            errors["pageSize"] = [$"pageSize 必须是 1 到 {maximumPageSize} 之间的整数。"];
        }
        return errors.Count == 0 ? null : new("validation_error", "请求内容无效。", errors, null);
    }

    private static ApiErrorResponse ApiError(string code, string message) => new(code, message, null, null);
    private sealed record ActorResolution(DatabaseDiscoverySyncActor? Actor, int? StatusCode, ApiErrorResponse? Error);
}
