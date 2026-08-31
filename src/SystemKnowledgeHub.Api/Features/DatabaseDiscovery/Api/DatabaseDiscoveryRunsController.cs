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
        [FromQuery] long? databaseSourceId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        return Ok(await runService.List(profileId, databaseSourceId, page, pageSize, access.IsAdministrator, cancellationToken));
    }

    [HttpGet("runs/{id:long}")]
    public async Task<ActionResult<DatabaseDiscoveryRunResponse>> GetRun(long id, CancellationToken cancellationToken)
    {
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetRun(id, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现运行。")) : Ok(response);
    }

    [HttpGet("run-filter-options")]
    public async Task<ActionResult<DatabaseDiscoveryRunFilterOptionsResponse>> GetRunFilterOptions(
        CancellationToken cancellationToken)
    {
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        return Ok(await runService.GetRunFilterOptions(access.IsAdministrator, cancellationToken));
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

    [HttpGet("snapshots")]
    public async Task<ActionResult<DatabaseDiscoverySnapshotHistoryPageResponse>> ListSnapshots(
        [FromQuery] long? profileId,
        [FromQuery] long? databaseSourceId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        return Ok(await runService.ListSnapshots(
            profileId, databaseSourceId, page, pageSize, access.IsAdministrator, cancellationToken));
    }

    [HttpGet("snapshots/{id:long}/summary")]
    public async Task<ActionResult<DatabaseDiscoverySnapshotSummaryResponse>> GetSnapshotSummary(
        long id, CancellationToken cancellationToken)
    {
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotSummary(id, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现快照。")) : Ok(response);
    }

    [HttpGet("snapshots/{id:long}/schemas")]
    public async Task<ActionResult<DatabaseDiscoverySchemaPageResponse>> GetSnapshotSchemas(
        long id, [FromQuery] string? search, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotSchemas(id, search, page, pageSize, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现快照。")) : Ok(response);
    }

    [HttpGet("snapshots/{id:long}/objects")]
    public async Task<ActionResult<DatabaseDiscoveryObjectPageResponse>> GetSnapshotObjects(
        long id, [FromQuery] string? schema, [FromQuery] string? objectType, [FromQuery] string? search,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(objectType)
            && (!Enum.TryParse<DatabaseDiscoveryObjectType>(objectType, false, out var parsedObjectType)
                || !Enum.IsDefined(parsedObjectType)
                || parsedObjectType.ToString() != objectType))
            return BadRequest(Validation("objectType", "objectType 必须是 Table 或 View。"));
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotObjects(
            id, schema, objectType, search, page, pageSize, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现快照。")) : Ok(response);
    }

    [HttpGet("snapshots/{id:long}/object-header")]
    public async Task<ActionResult<DatabaseDiscoveryObjectHeaderResponse>> GetSnapshotObjectHeader(
        long id, [FromQuery] string? logicalIdentity, CancellationToken cancellationToken)
    {
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotObjectHeader(id, logicalIdentity, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现对象。")) : Ok(response);
    }

    [HttpGet("snapshots/{id:long}/object-review")]
    public async Task<ActionResult<DatabaseDiscoveryObjectReviewResponse>> GetSnapshotObjectReview(
        long id, [FromQuery] string? logicalIdentity, [FromQuery] int? columnPage,
        [FromQuery] int? constraintPage, [FromQuery] int? indexPage, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(
            pageSize,
            ("columnPage", columnPage),
            ("constraintPage", constraintPage),
            ("indexPage", indexPage));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotObjectReview(
            id, logicalIdentity, columnPage, constraintPage, indexPage, pageSize,
            access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现对象。")) : Ok(response);
    }

    [HttpGet("snapshots/{id:long}/object-columns")]
    public async Task<ActionResult<DatabaseDiscoveryColumnPageResponse>> GetSnapshotObjectColumns(
        long id, [FromQuery] string? logicalIdentity, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotObjectColumns(
            id, logicalIdentity, page, pageSize, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现对象。")) : Ok(response);
    }

    [HttpGet("snapshots/{id:long}/object-constraints")]
    public async Task<ActionResult<DatabaseDiscoveryConstraintPageResponse>> GetSnapshotObjectConstraints(
        long id, [FromQuery] string? logicalIdentity, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotObjectConstraints(
            id, logicalIdentity, page, pageSize, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现对象。")) : Ok(response);
    }

    [HttpGet("snapshots/{id:long}/object-indexes")]
    public async Task<ActionResult<DatabaseDiscoveryIndexPageResponse>> GetSnapshotObjectIndexes(
        long id, [FromQuery] string? logicalIdentity, [FromQuery] int? page, [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotObjectIndexes(
            id, logicalIdentity, page, pageSize, access.IsAdministrator, cancellationToken);
        return response is null ? NotFound(Error("not_found", "未找到指定发现对象。")) : Ok(response);
    }

    [HttpGet("snapshots/{id:long}/sequences")]
    public async Task<ActionResult<DatabaseDiscoverySequencePageResponse>> GetSnapshotSequences(
        long id, [FromQuery] string? schema, [FromQuery] string? search,
        [FromQuery] int? page, [FromQuery] int? pageSize, CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetSnapshotSequences(
            id, schema, search, page, pageSize, access.IsAdministrator, cancellationToken);
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

    [HttpGet("differences")]
    public async Task<ActionResult<DatabaseDiscoveryDifferenceHistoryPageResponse>> ListDifferences(
        [FromQuery] long? profileId,
        [FromQuery] long? databaseSourceId,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        return Ok(await runService.ListDifferences(
            profileId, databaseSourceId, page, pageSize, access.IsAdministrator, cancellationToken));
    }

    [HttpGet("differences/{id:long}/entries")]
    public async Task<ActionResult<DatabaseDiscoveryDifferenceEntryPageResponse>> GetDifferenceEntries(
        long id,
        [FromQuery] string? state,
        [FromQuery] string? entityKind,
        [FromQuery] string? schema,
        [FromQuery] string? search,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        if (!Enum.TryParse<DatabaseDiscoveryDifferenceState>(state, false, out var parsed)
            || !Enum.IsDefined(parsed)
            || parsed.ToString() != state)
        {
            return BadRequest(new ApiErrorResponse(
                "validation_error", "请求内容无效。",
                new Dictionary<string, string[]> { ["state"] = ["state 必须是 Added、Changed、MissingFromSource 或 Unchanged。"] }, null));
        }
        if (!string.IsNullOrWhiteSpace(entityKind)
            && (!Enum.TryParse<DatabaseDiscoveryEntityKind>(entityKind, false, out var parsedKind)
                || !Enum.IsDefined(parsedKind)
                || parsedKind.ToString() != entityKind))
            return BadRequest(Validation("entityKind", "entityKind 不是受支持的发现实体类型。"));
        var paginationError = ValidatePagination(pageSize, ("page", page));
        if (paginationError is not null) return BadRequest(paginationError);
        var access = await ResolveAccess(cancellationToken);
        if (access.Error is not null) return StatusCode(access.StatusCode!.Value, access.Error);
        var response = await runService.GetDifferenceEntries(
            id, parsed, entityKind, schema, search, page, pageSize, access.IsAdministrator, cancellationToken);
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
    private static ApiErrorResponse Validation(string field, string message) => new(
        "validation_error", "请求内容无效。", new Dictionary<string, string[]> { [field] = [message] }, null);

    private static ApiErrorResponse? ValidatePagination(
        int? pageSize,
        params (string Field, int? Value)[] pages)
    {
        const int maximumPage = 1_000_000;
        var errors = new Dictionary<string, string[]>();
        foreach (var (field, value) in pages)
        {
            if (value is <= 0 or > maximumPage)
                errors[field] = [$"{field} 必须是 1 到 {maximumPage} 之间的整数。"];
        }
        if (pageSize is <= 0 or > 100)
        {
            errors["pageSize"] = ["pageSize 必须是 1 到 100 之间的整数。"];
        }
        return errors.Count == 0
            ? null
            : new ApiErrorResponse("validation_error", "请求内容无效。", errors, null);
    }

    private sealed record AccessResolution(bool IsAdministrator, int? StatusCode, ApiErrorResponse? Error);
}
