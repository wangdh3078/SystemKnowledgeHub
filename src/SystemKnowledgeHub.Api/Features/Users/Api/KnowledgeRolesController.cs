using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Users.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Users.Api;

/// <summary>
/// 提供 KnowledgeRole 的列表、创建、更新与 Active lifecycle HTTP API。
/// </summary>
[Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Administrator)]
[ApiController]
[Route("api/knowledge-roles")]
public sealed class KnowledgeRolesController(UserQueries queries, UserService service) : ControllerBase
{
    /// <summary>
    /// 返回按名称排序的 KnowledgeRole 管理列表。
    /// </summary>
    /// <param name="isActive">为 null 时包含 Active 与 inactive KnowledgeRole。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回 <c>200</c> 及每项的当前 opaque concurrencyToken。</returns>
    [HttpGet]
    [ProducesResponseType<IReadOnlyList<KnowledgeRoleListItemResponse>>(StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<KnowledgeRoleListItemResponse>>> GetKnowledgeRoles(
        [FromQuery] bool? isActive,
        CancellationToken cancellationToken)
    {
        return Ok(await queries.GetKnowledgeRoles(new KnowledgeRolesListQuery(isActive), cancellationToken));
    }

    /// <summary>
    /// 创建一个可供新 User assignment 使用的 KnowledgeRole。
    /// </summary>
    /// <param name="request">名称、可选说明与显式操作人标签。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回 <c>201</c> 详情；无效输入为 <c>400</c>，重名为 <c>409</c>。</returns>
    [HttpPost]
    [ProducesResponseType<KnowledgeRoleDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeRoleDetailResponse>> CreateKnowledgeRole(
        [FromBody] CreateKnowledgeRoleRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateKnowledgeRole(
            new CreateKnowledgeRoleCommand(
                request.Name ?? string.Empty,
                request.Description,
                Actor(request.Actor)),
            cancellationToken);
        return result.Failure switch
        {
            KnowledgeRoleWriteFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            KnowledgeRoleWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            KnowledgeRoleWriteFailure.Duplicate => Conflict(DuplicateError(result.FieldErrors!)),
            _ => throw new InvalidOperationException("Unsupported Create Knowledge Role result."),
        };
    }

    /// <summary>
    /// 更新 KnowledgeRole 的名称和说明。
    /// </summary>
    /// <remarks>
    /// request 中的 concurrencyToken 是必须原样回传的 opaque token；stale token 返回 <c>409</c>，不会覆盖较新的修改。
    /// </remarks>
    /// <param name="id">路由 KnowledgeRole ID；须满足 <see cref="ApiIdParser"/> 的 JavaScript safe integer 边界。</param>
    /// <param name="request">更新后的内容、显式操作人标签与 opaque concurrencyToken。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回 <c>200</c> 详情，或对应的 <c>400</c>、<c>404</c> 或 <c>409</c> 错误。</returns>
    [HttpPut("{id:long}")]
    [ProducesResponseType<KnowledgeRoleDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<KnowledgeRoleDetailResponse>> UpdateKnowledgeRole(
        long id,
        [FromBody] UpdateKnowledgeRoleRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(InvalidId());
        }

        var result = await service.UpdateKnowledgeRole(
            new UpdateKnowledgeRoleCommand(
                id,
                request.Name ?? string.Empty,
                request.Description,
                Actor(request.Actor),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);
        return MapUpdateResult(result, id);
    }

    /// <summary>
    /// 显式激活或停用 KnowledgeRole 是否可用于新 User assignment。
    /// </summary>
    /// <remarks>
    /// request 中的 concurrencyToken 是 opaque token，必须原样回传；stale token 返回 <c>409</c>，相同状态重复提交
    /// 返回 <c>422</c>。停用不会删除既有 UserKnowledgeRole assignment。
    /// </remarks>
    /// <param name="id">路由 KnowledgeRole ID；须满足 <see cref="ApiIdParser"/> 的 JavaScript safe integer 边界。</param>
    /// <param name="request">所需 Active 状态、显式操作人标签与 opaque concurrencyToken。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回 <c>200</c> 详情，或对应的 <c>400</c>、<c>404</c>、<c>409</c> 或 <c>422</c> 错误。</returns>
    [HttpPut("{id:long}/active-state")]
    [ProducesResponseType<KnowledgeRoleDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<KnowledgeRoleDetailResponse>> SetActiveState(
        long id,
        [FromBody] SetKnowledgeRoleActiveStateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(InvalidId());
        }

        var result = await service.SetKnowledgeRoleActiveState(
            new SetKnowledgeRoleActiveStateCommand(
                id,
                request.IsActive,
                Actor(request.Actor),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);
        return MapUpdateResult(result, id);
    }

    private ActionResult<KnowledgeRoleDetailResponse> MapUpdateResult(
        KnowledgeRoleWriteResult result,
        long id)
    {
        return result.Failure switch
        {
            KnowledgeRoleWriteFailure.None => Ok(result.Response),
            KnowledgeRoleWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            KnowledgeRoleWriteFailure.NotFound => NotFound(NotFoundError(id)),
            KnowledgeRoleWriteFailure.Conflict => Conflict(ConcurrencyConflict(id)),
            KnowledgeRoleWriteFailure.Duplicate => Conflict(DuplicateError(result.FieldErrors!)),
            KnowledgeRoleWriteFailure.NoChange => UnprocessableEntity(new ApiErrorResponse(
                "business_rule_violation",
                "目标启用状态与当前值相同。",
                null,
                new { resourceType = "KnowledgeRole", resourceId = id })),
            _ => throw new InvalidOperationException("Unsupported Knowledge Role write result."),
        };
    }

    private static UserActorContext Actor(UserActorRequest? actor) => new(
        actor?.DisplayName ?? string.Empty,
        actor?.Role);

    private static ApiErrorResponse InvalidId() => ValidationError(
        new Dictionary<string, string[]> { ["id"] = ["知识身份 ID 必须是 JavaScript 安全范围内的正整数。"] });

    private static ApiErrorResponse NotFoundError(long id) => new(
        "not_found",
        "未找到指定知识身份。",
        null,
        new { resourceType = "KnowledgeRole", resourceId = id });

    private static ApiErrorResponse ConcurrencyConflict(long id) => new(
        "conflict",
        "知识身份已被其他操作修改，请刷新后重试。",
        null,
        new { resourceType = "KnowledgeRole", resourceId = id });

    private static ApiErrorResponse DuplicateError(IReadOnlyDictionary<string, string[]> fieldErrors) => new(
        "conflict",
        "知识身份名称已存在。",
        fieldErrors,
        null);

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors) => new(
        "validation_error",
        "请求内容无效。",
        fieldErrors,
        null);
}
