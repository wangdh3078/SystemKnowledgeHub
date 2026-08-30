using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Api.Contracts;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Api;

[ApiController]
[Route("api/database-discovery")]
[Authorize]
public sealed class DatabaseDiscoveryRunsController(
    DatabaseDiscoveryRunService runService,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [HttpGet("runs")]
    public async Task<ActionResult<DatabaseDiscoveryRunPageResponse>> ListRuns(
        [FromQuery] long? profileId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        return Ok(await runService.List(profileId, page, pageSize, access.IsAdministrator, cancellationToken));
    }

    [HttpGet("runs/{id:long}")]
    public async Task<ActionResult<DatabaseDiscoveryRunResponse>> GetRun(long id, CancellationToken cancellationToken)
    {
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetRun(id, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现运行。")) : Ok(response);
    }

    [Authorize(Policy = AccessPolicies.Administrator)]
    [HttpPost("runs/{id:long}/cancel")]
    public async Task<ActionResult<DatabaseDiscoveryRunResponse>> Cancel(
        long id,
        [FromBody] CancelDatabaseDiscoveryRunRequest request,
        CancellationToken cancellationToken)
    {
        var creator = await CurrentUserApiResolution.ResolveCreator(currentUserContext, cancellationToken);
        if (creator.Error is not null) return StatusCode(creator.StatusCode!.Value, creator.Error);
        var result = await runService.Cancel(
            id, request.ConcurrencyToken, new DatabaseConnectionActor(creator.Creator!), cancellationToken);
        return result.Failure switch
        {
            DatabaseDiscoveryFailure.None => Ok(result.Response),
            DatabaseDiscoveryFailure.Validation => BadRequest(new ApiErrorResponse("validation_error", "请求内容无效。", result.FieldErrors, null)),
            DatabaseDiscoveryFailure.NotFound => NotFound(Error("not_found", "未找到指定发现运行。")),
            DatabaseDiscoveryFailure.ConcurrencyConflict => Conflict(Error("ConcurrencyConflict", "发现运行已变化，请重新加载后重试。")),
            DatabaseDiscoveryFailure.TerminalRun => Conflict(Error("ConcurrencyConflict", "发现运行已经结束。")),
            _ => throw new InvalidOperationException("Unsupported Discovery cancel failure."),
        };
    }

    [HttpGet("snapshots/{id:long}")]
    public async Task<ActionResult<DatabaseDiscoverySnapshotResponse>> GetSnapshot(long id, CancellationToken cancellationToken)
    {
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshot(id, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现快照。")) : Ok(response);
    }

    [HttpGet("differences/{id:long}")]
    public async Task<ActionResult<DatabaseDiscoveryDifferenceResponse>> GetDifference(long id, CancellationToken cancellationToken)
    {
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetDifference(id, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现差异。")) : Ok(response);
    }

    [HttpGet("differences/{id:long}/entries")]
    public async Task<ActionResult<DatabaseDiscoveryDifferenceEntryPageResponse>> GetDifferenceEntries(
        long id,
        [FromQuery] string? state,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DatabaseDiscoveryDifferenceState>(state, false, out var parsed)
            || parsed.ToString() != state)
        {
            return BadRequest(new ApiErrorResponse(
                "validation_error", "请求内容无效。",
                new Dictionary<string, string[]> { ["state"] = ["state 必须是 Added、Changed、MissingFromSource 或 Unchanged。"] }, null));
        }
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetDifferenceEntries(id, parsed, page, pageSize, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现差异。")) : Ok(response);
    }

    private async Task<AccessResolution> ResolveAccess(CancellationToken cancellationToken)
    {
        var actor = await CurrentUserApiResolution.ResolveSoftDeleteActor(currentUserContext, cancellationToken);
        return actor.Error is null
            ? new(actor.Actor!.AccessLevel == AccessLevel.Administrator, null, null)
            : new(false, actor.StatusCode, actor.Error);
    }

    private static ApiErrorResponse Error(string code, string message) => new(code, message, null, null);

    private sealed record AccessResolution(bool IsAdministrator, int? StatusCode, ApiErrorResponse? Error);
}
