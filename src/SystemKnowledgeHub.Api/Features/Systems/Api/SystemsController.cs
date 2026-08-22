using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Systems.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Systems.Application;
using SystemKnowledgeHub.Api.Features.Systems.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Systems.Api;

[ApiController]
[Route("api/systems")]
public sealed class SystemsController(
    SystemQueries queries,
    SystemService service) : ControllerBase
{
    [HttpGet("{id:long}")]
    [ProducesResponseType<SystemDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<SystemDetailResponse>> GetSystemDetail(
        long id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["系统 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var response = await queries.GetSystemDetail(id, cancellationToken);
        if (response is null)
        {
            return NotFound(new ApiErrorResponse(
                "not_found",
                "未找到指定系统。",
                null,
                new { resourceType = "System", resourceId = id }));
        }

        return Ok(response);
    }

    [HttpGet]
    [ProducesResponseType<SystemsListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<SystemsListResponse>> GetSystemsList(
        [FromQuery] string? search,
        [FromQuery] string? lifecycle,
        [FromQuery] string? technology,
        [FromQuery] string? knowledgeStatus,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetSystemsList(
            new SystemsListQuery(
                search,
                lifecycle,
                technology,
                knowledgeStatus,
                sort,
                page,
                pageSize),
            cancellationToken);

        if (result.FieldErrors is not null)
        {
            return BadRequest(ValidationError(result.FieldErrors));
        }

        return Ok(result.Response);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPost]
    [ProducesResponseType<CreateSystemResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CreateSystemResponse>> CreateSystem(
        [FromBody] CreateSystemRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateSystem(
            new CreateSystemCommand(
                request.Name ?? string.Empty,
                request.DisplayName ?? string.Empty,
                request.SystemType ?? string.Empty,
                request.Lifecycle ?? string.Empty,
                request.Purpose,
                new ActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role)),
            cancellationToken);

        if (result.FieldErrors is not null)
        {
            return BadRequest(ValidationError(result.FieldErrors));
        }

        if (result.DuplicateName)
        {
            return Conflict(new ApiErrorResponse(
                "conflict",
                "系统名称已存在。",
                new Dictionary<string, string[]>
                {
                    ["name"] = ["请使用唯一的系统名称。"],
                },
                null));
        }

        return StatusCode(StatusCodes.Status201Created, result.Response);
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}/overview")]
    [ProducesResponseType<UpdateSystemOverviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateSystemOverviewResponse>> UpdateSystemOverview(
        long id,
        [FromBody] UpdateSystemOverviewRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["系统 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await service.UpdateSystemOverview(
            new UpdateSystemOverviewCommand(
                id,
                request.DisplayName ?? string.Empty,
                request.SystemType ?? string.Empty,
                request.Purpose,
                request.MainUsers,
                request.Repository is null
                    ? null
                    : new UpdateSystemRepository(request.Repository.Name, request.Repository.Url),
                request.Deployment?.Select(item =>
                    new UpdateSystemDeployment(item.Environment, item.Description)).ToArray(),
                request.MainProjects,
                request.MainEntryPoints,
                request.Notes,
                new ActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);

        return result.Failure switch
        {
            UpdateSystemOverviewFailure.None => Ok(result.Response),
            UpdateSystemOverviewFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UpdateSystemOverviewFailure.NotFound => NotFound(new ApiErrorResponse(
                "not_found",
                "未找到指定系统。",
                null,
                new { resourceType = "System", resourceId = id })),
            UpdateSystemOverviewFailure.Conflict => Conflict(new ApiErrorResponse(
                "conflict",
                "内容已被其他操作修改，请刷新后重试。",
                null,
                new { resourceType = "System", resourceId = id })),
            _ => throw new InvalidOperationException("Unsupported System overview result."),
        };
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}/technology")]
    [ProducesResponseType<UpdateSystemTechnologyResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<UpdateSystemTechnologyResponse>> UpdateSystemTechnology(
        long id,
        [FromBody] UpdateSystemTechnologyRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["系统 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await service.UpdateSystemTechnology(
            new UpdateSystemTechnologyCommand(
                id,
                request.Technologies,
                new ActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);

        return result.Failure switch
        {
            UpdateSystemTechnologyFailure.None => Ok(result.Response),
            UpdateSystemTechnologyFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UpdateSystemTechnologyFailure.NotFound => NotFound(SystemNotFound(id)),
            UpdateSystemTechnologyFailure.Conflict => Conflict(ConcurrencyConflict(id)),
            _ => throw new InvalidOperationException("Unsupported System technology result."),
        };
    }

    [Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Editor)]
    [HttpPut("{id:long}/lifecycle")]
    [ProducesResponseType<UpdateSystemLifecycleResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UpdateSystemLifecycleResponse>> UpdateSystemLifecycle(
        long id,
        [FromBody] UpdateSystemLifecycleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(ValidationError(new Dictionary<string, string[]>
            {
                ["id"] = ["系统 ID 必须是 JavaScript 安全范围内的正整数。"],
            }));
        }

        var result = await service.UpdateSystemLifecycle(
            new UpdateSystemLifecycleCommand(
                id,
                request.TargetLifecycle ?? string.Empty,
                new ActorContext(
                    request.Actor?.DisplayName ?? string.Empty,
                    request.Actor?.Role),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);

        return result.Failure switch
        {
            UpdateSystemLifecycleFailure.None => Ok(result.Response),
            UpdateSystemLifecycleFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UpdateSystemLifecycleFailure.NotFound => NotFound(SystemNotFound(id)),
            UpdateSystemLifecycleFailure.Conflict => Conflict(ConcurrencyConflict(id)),
            UpdateSystemLifecycleFailure.NoChange => UnprocessableEntity(new ApiErrorResponse(
                "business_rule_violation",
                "目标生命周期与当前值相同。",
                null,
                new { resourceType = "System", resourceId = id })),
            _ => throw new InvalidOperationException("Unsupported System lifecycle result."),
        };
    }

    private static ApiErrorResponse SystemNotFound(long id) => new(
        "not_found",
        "未找到指定系统。",
        null,
        new { resourceType = "System", resourceId = id });

    private static ApiErrorResponse ConcurrencyConflict(long id) => new(
        "conflict",
        "内容已被其他操作修改，请刷新后重试。",
        null,
        new { resourceType = "System", resourceId = id });

    private static ApiErrorResponse ValidationError(
        IReadOnlyDictionary<string, string[]> fieldErrors)
    {
        return new ApiErrorResponse(
            "validation_error",
            "请求内容无效。",
            fieldErrors,
            null);
    }
}
