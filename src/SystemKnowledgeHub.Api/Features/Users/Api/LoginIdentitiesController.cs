using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.Users.Api.Contracts;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.Users.Api;

/// <summary>提供 canonical User 的 LoginIdentity 映射管理员 API。</summary>
/// <remarks>Provider 与 Subject 是外部身份的显式映射，不按邮箱、工号或显示名自动绑定。</remarks>
[Microsoft.AspNetCore.Authorization.Authorize(Policy = AccessPolicies.Administrator)]
[ApiController]
[Route("api/users/{userId:long}/login-identities")]
public sealed class LoginIdentitiesController(UserService service) : ControllerBase
{
    /// <summary>返回指定 User 的全部 LoginIdentity 映射。</summary>
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<LoginIdentityResponse>>> Get(
        long userId,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(userId)) return BadRequest(InvalidUserId());
        return Ok(await service.GetLoginIdentities(userId, cancellationToken));
    }

    /// <summary>为指定 User 建立一条新的 active LoginIdentity 显式映射。</summary>
    [HttpPost]
    public async Task<ActionResult<LoginIdentityResponse>> Create(
        long userId,
        [FromBody] CreateLoginIdentityRequest request,
        CancellationToken cancellationToken)
    {
        var result = await service.CreateLoginIdentity(new(userId, request.Provider ?? string.Empty, request.Subject ?? string.Empty), cancellationToken);
        return result.Failure switch
        {
            LoginIdentityWriteFailure.None => StatusCode(StatusCodes.Status201Created, result.Response),
            LoginIdentityWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            LoginIdentityWriteFailure.NotFound => NotFound(NotFoundUser(userId)),
            LoginIdentityWriteFailure.Duplicate => Conflict(ConflictError(result.FieldErrors!)),
            _ => throw new InvalidOperationException("Unsupported LoginIdentity create result."),
        };
    }

    /// <summary>显式启用或停用 LoginIdentity，并防止移除最后一个可登录 Active Administrator。</summary>
    [HttpPut("{id:long}/active-state")]
    public async Task<ActionResult<LoginIdentityResponse>> SetActiveState(
        long userId,
        long id,
        [FromBody] SetLoginIdentityActiveStateRequest request,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(userId) || !ApiIdParser.IsSafePositive(id)) return BadRequest(InvalidUserId());
        var result = await service.SetLoginIdentityActiveState(new(userId, id, request.IsActive, request.ConcurrencyToken ?? string.Empty), cancellationToken);
        return result.Failure switch
        {
            LoginIdentityWriteFailure.None => Ok(result.Response),
            LoginIdentityWriteFailure.Validation => BadRequest(ValidationError(result.FieldErrors!)),
            LoginIdentityWriteFailure.NotFound => NotFound(NotFoundUser(userId)),
            LoginIdentityWriteFailure.Conflict => Conflict(ConflictError(null)),
            LoginIdentityWriteFailure.NoChange => UnprocessableEntity(BusinessRule(userId)),
            LoginIdentityWriteFailure.LastUsableAdministrator => UnprocessableEntity(LastAdministrator(userId)),
            _ => throw new InvalidOperationException("Unsupported LoginIdentity active-state result."),
        };
    }

    private static ApiErrorResponse InvalidUserId() => ValidationError(new Dictionary<string, string[]> { ["userId"] = ["用户 ID 必须是 JavaScript 安全范围内的正整数。"] });
    private static ApiErrorResponse NotFoundUser(long userId) => new("not_found", "未找到指定用户或登录映射。", null, new { resourceType = "User", resourceId = userId });
    private static ApiErrorResponse ValidationError(IReadOnlyDictionary<string, string[]> errors) => new("validation_error", "请求内容无效。", errors, null);
    private static ApiErrorResponse ConflictError(IReadOnlyDictionary<string, string[]>? errors) => new("conflict", "登录映射已被其他操作修改或已存在。", errors, null);
    private static ApiErrorResponse BusinessRule(long userId) => new("business_rule_violation", "目标启用状态与当前值相同。", null, new { resourceType = "User", resourceId = userId });
    private static ApiErrorResponse LastAdministrator(long userId) => new("business_rule_violation", "系统必须保留至少一个可登录的启用 Administrator。", null, new { resourceType = "User", resourceId = userId });
}
