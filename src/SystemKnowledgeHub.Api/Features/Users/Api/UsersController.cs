using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Users.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Features.Users.Api;

/// <summary>
/// 提供 canonical User 的列表、详情、创建、更新与 Active lifecycle HTTP API。
/// </summary>
[Microsoft.AspNetCore.Authorization.Authorize(Policy = SystemKnowledgeHub.Api.Shared.Security.AccessPolicies.Administrator)]
[ApiController]
[Route("api/users")]
public sealed class UsersController(UserQueries queries, UserService service) : ControllerBase
{
    /// <summary>
    /// 返回受控筛选、排序和分页的 User 管理列表。
    /// </summary>
    /// <param name="keyword">为 null 时不按姓名、工号或邮箱筛选。</param>
    /// <param name="isActive">为 null 时包含 Active 与 inactive User。</param>
    /// <param name="sort">为 null 时按默认排序；受控取值由 <see cref="UsersListQuery"/> 定义。</param>
    /// <param name="page">为 null 时使用第 1 页。</param>
    /// <param name="pageSize">为 null 时使用默认大小；已提供的值必须在受控范围内。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回 <c>200</c> 列表；无效筛选、排序或分页参数返回 <c>400</c>。</returns>
    [HttpGet]
    [ProducesResponseType<UsersListResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<UsersListResponse>> GetUsersList(
        [FromQuery] string? keyword,
        [FromQuery] bool? isActive,
        [FromQuery] string? sort,
        [FromQuery] int? page,
        [FromQuery] int? pageSize,
        CancellationToken cancellationToken)
    {
        var result = await queries.GetUsersList(
            new UsersListQuery(keyword, isActive, sort, page, pageSize),
            cancellationToken);
        return result.FieldErrors is null
            ? Ok(result.Response)
            : BadRequest(ValidationError(result.FieldErrors));
    }

    /// <summary>
    /// 返回一个 canonical User 的可编辑详情。
    /// </summary>
    /// <param name="id">路由 User ID；须满足 <see cref="ApiIdParser"/> 的 JavaScript safe integer 边界。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回 <c>200</c> 详情、无效 ID 的 <c>400</c> 或不存在 User 的 <c>404</c>。</returns>
    [HttpGet("{id:long}")]
    [ProducesResponseType<UserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserDetailResponse>> GetUser(
        long id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(InvalidId());
        }

        var response = await queries.GetUser(id, cancellationToken);
        return response is null ? NotFound(NotFoundError(id)) : Ok(response);
    }

    /// <summary>返回新增用户时可选择的登录方式及当前部署启用状态。</summary>
    [HttpGet("login-setup-options")]
    [ProducesResponseType<UserLoginSetupOptionsResponse>(StatusCodes.Status200OK)]
    public ActionResult<UserLoginSetupOptionsResponse> GetLoginSetupOptions() =>
        Ok(queries.GetLoginSetupOptions());

    /// <summary>返回指定 User 的本地账号与 OIDC 映射元数据；绝不返回密码哈希或 SessionVersion。</summary>
    [HttpGet("{id:long}/login-methods")]
    [ProducesResponseType<UserLoginMethodsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<UserLoginMethodsResponse>> GetUserLoginMethods(
        long id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(InvalidId());
        }

        var response = await queries.GetUserLoginMethods(id, cancellationToken);
        return response is null ? NotFound(NotFoundError(id)) : Ok(response);
    }

    /// <summary>
    /// 创建 canonical User 及其初始 KnowledgeRole assignment。
    /// </summary>
    /// <param name="request">Profile、可选初始 assignment 与显式操作人标签。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>
    /// 异步操作完成后，返回 <c>201</c> 详情；输入无效返回 <c>400</c>，重复工号或邮箱返回 <c>409</c>，
    /// 不可新分配的 KnowledgeRole 返回 <c>422</c>。
    /// </returns>
    [HttpPost]
    [ProducesResponseType<UserDetailResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDetailResponse>> CreateUser(
        [FromBody] CreateUserRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateUser(
            new CreateUserCommand(
                request.EmployeeNo,
                request.DisplayName ?? string.Empty,
                request.Email,
                request.DepartmentOrTeam,
                request.JobTitle,
                request.KnowledgeRoleIds,
                request.LoginSetup is null
                    ? null
                    : new CreateUserLoginSetupCommand(
                        request.LoginSetup.Type,
                        request.LoginSetup.Username,
                        request.LoginSetup.InitialPassword,
                        request.LoginSetup.Provider,
                        request.LoginSetup.Subject),
                Actor(request.Actor)),
            cancellationToken);
        return MapCreateResult(result);
    }

    /// <summary>
    /// 更新 canonical User Profile 并替换当前 KnowledgeRole assignment。
    /// </summary>
    /// <remarks>
    /// request 中的 concurrencyToken 是 opaque token，必须来自最近一次读取并原样回传。有效但 stale 的 token 返回
    /// <c>409</c>，不会覆盖较新的修改。
    /// </remarks>
    /// <param name="id">路由 User ID；须满足 <see cref="ApiIdParser"/> 的 JavaScript safe integer 边界。</param>
    /// <param name="request">完整 Profile、目标 assignment、显式操作人标签与 opaque concurrencyToken。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>
    /// 异步操作完成后，返回 <c>200</c> 详情；无效输入为 <c>400</c>，不存在为 <c>404</c>，重复或 stale write
    /// 为 <c>409</c>，不可新分配的 KnowledgeRole 为 <c>422</c>。
    /// </returns>
    [HttpPut("{id:long}")]
    [ProducesResponseType<UserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDetailResponse>> UpdateUser(
        long id,
        [FromBody] UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(InvalidId());
        }

        var result = await service.UpdateUser(
            new UpdateUserCommand(
                id,
                request.EmployeeNo,
                request.DisplayName ?? string.Empty,
                request.Email,
                request.DepartmentOrTeam,
                request.JobTitle,
                request.KnowledgeRoleIds,
                Actor(request.Actor),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);
        return MapUpdateResult(result, id);
    }

    /// <summary>
    /// 显式激活或停用 canonical User。
    /// </summary>
    /// <remarks>
    /// request 中的 concurrencyToken 是 opaque token，必须原样回传；stale token 返回 <c>409</c>，相同状态重复提交
    /// 返回 <c>422</c>，并且停用不删除 User 或其既有引用。
    /// </remarks>
    /// <param name="id">路由 User ID；须满足 <see cref="ApiIdParser"/> 的 JavaScript safe integer 边界。</param>
    /// <param name="request">所需 Active 状态、显式操作人标签与 opaque concurrencyToken。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步操作完成后，返回 <c>200</c> 详情，或对应的 <c>400</c>、<c>404</c>、<c>409</c> 或 <c>422</c> 错误。</returns>
    [HttpPut("{id:long}/active-state")]
    [ProducesResponseType<UserDetailResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserDetailResponse>> SetActiveState(
        long id,
        [FromBody] SetUserActiveStateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id))
        {
            return BadRequest(InvalidId());
        }

        var result = await service.SetUserActiveState(
            new SetUserActiveStateCommand(
                id,
                request.IsActive,
                Actor(request.Actor),
                request.ConcurrencyToken ?? string.Empty),
            cancellationToken);
        return MapUpdateResult(result, id);
    }

    /// <summary>独立修改 User AccessLevel；该安全操作不与普通 Profile 更新合并。</summary>
    [HttpPut("{id:long}/access-level")]
    [ProducesResponseType<UserAccessLevelResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status404NotFound)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status409Conflict)]
    [ProducesResponseType<ApiErrorResponse>(StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<UserAccessLevelResponse>> SetAccessLevel(
        long id,
        [FromBody] SetUserAccessLevelRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return BadRequest(InvalidId());
        if (request.AccessLevel is null) return BadRequest(ValidationError(new Dictionary<string, string[]> { ["accessLevel"] = ["访问等级无效。"] }));
        var result = await service.SetUserAccessLevel(new(id, request.AccessLevel.Value, request.ConcurrencyToken ?? string.Empty), cancellationToken);
        return result.Failure == UserWriteFailure.None
            ? Ok(new UserAccessLevelResponse(id, request.AccessLevel.Value, result.Response!.ConcurrencyToken))
            : MapAccessLevelResult(result, id);
    }

    private ActionResult<UserDetailResponse> MapCreateResult(UserWriteResult result)
    {
        return result.Failure switch
        {
            UserWriteFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            UserWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UserWriteFailure.Duplicate => Conflict(ConflictError(result.FieldErrors!)),
            UserWriteFailure.InactiveKnowledgeRole => UnprocessableEntity(RoleUnavailable(result.FieldErrors!)),
            _ => throw new InvalidOperationException("Unsupported Create User result."),
        };
    }

    private ActionResult<UserDetailResponse> MapUpdateResult(UserWriteResult result, long id)
    {
        return result.Failure switch
        {
            UserWriteFailure.None => Ok(result.Response),
            UserWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            UserWriteFailure.NotFound => NotFound(NotFoundError(id)),
            UserWriteFailure.Conflict => Conflict(ConcurrencyConflict(id)),
            UserWriteFailure.Duplicate => Conflict(ConflictError(result.FieldErrors!)),
            UserWriteFailure.InactiveKnowledgeRole => UnprocessableEntity(RoleUnavailable(result.FieldErrors!)),
            UserWriteFailure.NoChange => UnprocessableEntity(new ApiErrorResponse(
                "business_rule_violation",
                "目标启用状态与当前值相同。",
                null,
                new { resourceType = "User", resourceId = id })),
            UserWriteFailure.LastUsableAdministrator => UnprocessableEntity(new ApiErrorResponse(
                "business_rule_violation",
                "系统必须保留至少一个可登录的启用 Administrator。",
                null,
                new { resourceType = "User", resourceId = id })),
            _ => throw new InvalidOperationException("Unsupported User write result."),
        };
    }

    private ActionResult<UserAccessLevelResponse> MapAccessLevelResult(UserWriteResult result, long id) => result.Failure switch
    {
        UserWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
        UserWriteFailure.NotFound => NotFound(NotFoundError(id)),
        UserWriteFailure.Conflict => Conflict(ConcurrencyConflict(id)),
        UserWriteFailure.NoChange => UnprocessableEntity(new ApiErrorResponse("business_rule_violation", "目标访问等级与当前值相同。", null, new { resourceType = "User", resourceId = id })),
        UserWriteFailure.LastUsableAdministrator => UnprocessableEntity(new ApiErrorResponse("business_rule_violation", "系统必须保留至少一个可登录的启用 Administrator。", null, new { resourceType = "User", resourceId = id })),
        _ => throw new InvalidOperationException("Unsupported AccessLevel result."),
    };

    private static UserActorContext Actor(UserActorRequest? actor) => new(
        actor?.DisplayName ?? string.Empty,
        actor?.Role);

    private static ApiErrorResponse InvalidId() => ValidationError(
        new Dictionary<string, string[]> { ["id"] = ["用户 ID 必须是 JavaScript 安全范围内的正整数。"] });

    private static ApiErrorResponse NotFoundError(long id) => new(
        "not_found",
        "未找到指定用户。",
        null,
        new { resourceType = "User", resourceId = id });

    private static ApiErrorResponse ConcurrencyConflict(long id) => new(
        "conflict",
        "用户资料已被其他操作修改，请刷新后重试。",
        null,
        new { resourceType = "User", resourceId = id });

    private static ApiErrorResponse ConflictError(IReadOnlyDictionary<string, string[]> fieldErrors) => new(
        "conflict",
        "用户资料或登录方式已存在。",
        fieldErrors,
        null);

    private static ApiErrorResponse RoleUnavailable(IReadOnlyDictionary<string, string[]> fieldErrors) => new(
        "reference_invalid",
        "只能新分配当前启用的知识身份。",
        fieldErrors,
        null);

    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> fieldErrors) => new(
        "validation_error",
        "请求内容无效。",
        fieldErrors,
        null);
}
