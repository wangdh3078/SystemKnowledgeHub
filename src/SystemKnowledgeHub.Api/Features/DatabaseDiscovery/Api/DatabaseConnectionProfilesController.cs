using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Api.Contracts;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;
using SystemKnowledgeHub.Api.Shared.Security;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Api;

[ApiController]
[Authorize(Policy = AccessPolicies.Administrator)]
[Route("api/admin/database-connection-profiles")]
public sealed class DatabaseConnectionProfilesController(
    DatabaseConnectionProfileService profileService,
    DatabaseConnectionTestService testService,
    DatabaseDiscoveryRunService runService,
    ICurrentUserContext currentUserContext) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<DatabaseConnectionProfileResponse>>> List(
        CancellationToken cancellationToken) =>
        Ok(await profileService.List(cancellationToken));

    [HttpGet("database-sources")]
    public async Task<ActionResult<IReadOnlyList<DatabaseConnectionSourceOptionResponse>>> ListDatabaseSources(
        [FromQuery] string? search,
        CancellationToken cancellationToken) =>
        Ok(await profileService.ListSourceOptions(search, cancellationToken));

    [HttpGet("{id:long}")]
    public async Task<ActionResult<DatabaseConnectionProfileResponse>> Get(
        long id,
        CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return BadRequest(IdValidation());
        var profile = await profileService.Get(id, cancellationToken);
        return profile is null ? NotFound(Error("not_found", "未找到指定连接配置。")) : Ok(profile);
    }

    [HttpPost]
    public async Task<ActionResult<DatabaseConnectionProfileResponse>> Create(
        [FromBody] CreateDatabaseConnectionProfileRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await profileService.Create(
            new DatabaseConnectionProfileInput(
                request.DatabaseSourceId ?? 0,
                request.Name ?? string.Empty,
                request.ProviderType ?? string.Empty,
                request.Host ?? string.Empty,
                request.Port,
                request.DatabaseName,
                request.ServiceName,
                request.AuthenticationMode ?? string.Empty,
                request.Username ?? string.Empty,
                request.ProviderSpecificOptions,
                request.IncludedSchemas,
                request.IsEnabled ?? false),
            actor.Actor!,
            cancellationToken);
        return result.Failure == DatabaseConnectionFailure.None
            ? StatusCode(StatusCodes.Status201Created, result.Response)
            : MapProfileFailure(result);
    }

    [HttpPut("{id:long}")]
    public async Task<ActionResult<DatabaseConnectionProfileResponse>> Update(
        long id,
        [FromBody] UpdateDatabaseConnectionProfileRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await profileService.Update(
            new DatabaseConnectionProfileUpdateInput(
                id,
                request.Name ?? string.Empty,
                request.ProviderType ?? string.Empty,
                request.Host ?? string.Empty,
                request.Port,
                request.DatabaseName,
                request.ServiceName,
                request.AuthenticationMode ?? string.Empty,
                request.Username ?? string.Empty,
                request.ProviderSpecificOptions,
                request.IncludedSchemas,
                request.ConcurrencyToken),
            actor.Actor!,
            cancellationToken);
        return result.Failure == DatabaseConnectionFailure.None ? Ok(result.Response) : MapProfileFailure(result);
    }

    [HttpPut("{id:long}/enabled-state")]
    public async Task<ActionResult<DatabaseConnectionProfileResponse>> SetEnabled(
        long id,
        [FromBody] SetDatabaseConnectionProfileEnabledRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await profileService.SetEnabled(
            id, request.IsEnabled, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        return result.Failure == DatabaseConnectionFailure.None ? Ok(result.Response) : MapProfileFailure(result);
    }

    [HttpPost("{id:long}/secret")]
    public async Task<ActionResult<DatabaseConnectionProfileResponse>> SetSecret(
        long id,
        [FromBody] SetDatabaseConnectionSecretRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await profileService.SetSecret(
            id, request.Password, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        return result.Failure == DatabaseConnectionFailure.None ? Ok(result.Response) : MapProfileFailure(result);
    }

    [HttpPut("{id:long}/secret")]
    public async Task<ActionResult<DatabaseConnectionProfileResponse>> ReplaceSecret(
        long id,
        [FromBody] SetDatabaseConnectionSecretRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await profileService.ReplaceSecret(
            id, request.Password, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        return result.Failure == DatabaseConnectionFailure.None ? Ok(result.Response) : MapProfileFailure(result);
    }

    [HttpDelete("{id:long}/secret")]
    public async Task<ActionResult<DatabaseConnectionProfileResponse>> ClearSecret(
        long id,
        [FromBody] ClearDatabaseConnectionSecretRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await profileService.ClearSecret(
            id, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        return result.Failure == DatabaseConnectionFailure.None ? Ok(result.Response) : MapProfileFailure(result);
    }

    [HttpPost("{id:long}/test-connection")]
    public async Task<ActionResult<DatabaseConnectionTestResponse>> TestConnection(
        long id,
        [FromBody] TestDatabaseConnectionRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await testService.Test(id, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        if (result.Failure == DatabaseConnectionFailure.None) return Ok(result.Response);
        if (result.Failure == DatabaseConnectionFailure.Validation) return BadRequest(Validation(result.FieldErrors!));
        if (result.Failure == DatabaseConnectionFailure.NotFound) return NotFound(Error("not_found", "未找到指定连接配置。"));
        if (result.Failure == DatabaseConnectionFailure.ConcurrencyConflict)
            return Conflict(Error("concurrency_conflict", "连接配置、密钥或测试尝试已变化，请重新加载后重试。"));
        if (result.Failure == DatabaseConnectionFailure.ActiveDiscoveryRun)
            return Conflict(Error("DiscoveryAlreadyRunning", "发现运行期间不能测试或修改连接配置。"));

        var status = result.Failure switch
        {
            DatabaseConnectionFailure.Timeout => StatusCodes.Status504GatewayTimeout,
            DatabaseConnectionFailure.Cancelled => StatusCodes.Status408RequestTimeout,
            _ => StatusCodes.Status422UnprocessableEntity,
        };
        var safeDetails = result.Response is null
            ? new DatabaseConnectionTestErrorDetails(id, result.Failure.ToString(), result.VendorCode, null)
            : new DatabaseConnectionTestErrorDetails(
                result.Response.ProfileId,
                result.Response.ErrorCode,
                result.Response.VendorCode,
                result.Response.Summary);
        return StatusCode(status, new ApiErrorResponse(
            result.Failure.ToString(),
            result.Response?.Summary ?? FailureMessage(result.Failure),
            null,
            safeDetails));
    }

    [HttpPost("{id:long}/discovery-runs")]
    public async Task<ActionResult<DatabaseDiscoveryRunResponse>> TriggerDiscoveryRun(
        long id,
        [FromBody] TriggerDatabaseDiscoveryRunRequest request,
        CancellationToken cancellationToken)
    {
        var actor = await ResolveActor(cancellationToken);
        if (actor.Error is not null) return StatusCode(actor.StatusCode!.Value, actor.Error);
        var result = await runService.Trigger(id, request.ConcurrencyToken, actor.Actor!, cancellationToken);
        return result.Failure switch
        {
            DatabaseDiscoveryFailure.None => Accepted(result.Response),
            DatabaseDiscoveryFailure.Validation => BadRequest(Validation(result.FieldErrors!)),
            DatabaseDiscoveryFailure.NotFound => NotFound(Error("not_found", "未找到指定连接配置。")),
            DatabaseDiscoveryFailure.ReferenceInvalid => UnprocessableEntity(Error("reference_invalid", "关联数据库来源不存在或已不可用。")),
            DatabaseDiscoveryFailure.Disabled => UnprocessableEntity(Error("Disabled", "连接配置已停用。")),
            DatabaseDiscoveryFailure.SecretMissing => UnprocessableEntity(Error("SecretMissing", "尚未设置数据库连接密码。")),
            DatabaseDiscoveryFailure.ConcurrencyConflict => Conflict(Error("ConcurrencyConflict", "连接配置已变化，请重新加载后重试。")),
            DatabaseDiscoveryFailure.DiscoveryAlreadyRunning => Conflict(Error("DiscoveryAlreadyRunning", "该连接配置已有排队中或运行中的发现任务。")),
            _ => throw new InvalidOperationException("Unsupported Discovery trigger failure."),
        };
    }

    private async Task<ActorResolution> ResolveActor(CancellationToken cancellationToken)
    {
        var creator = await CurrentUserApiResolution.ResolveCreator(currentUserContext, cancellationToken);
        return creator.Error is null
            ? new(new DatabaseConnectionActor(creator.Creator!), null, null)
            : new(null, creator.StatusCode, creator.Error);
    }

    private ActionResult<DatabaseConnectionProfileResponse> MapProfileFailure(
        DatabaseConnectionOperationResult<DatabaseConnectionProfileResponse> result) => result.Failure switch
    {
        DatabaseConnectionFailure.Validation => BadRequest(Validation(result.FieldErrors!)),
        DatabaseConnectionFailure.NotFound => NotFound(Error("not_found", "未找到指定连接配置。")),
        DatabaseConnectionFailure.ReferenceInvalid => UnprocessableEntity(Error("reference_invalid", "引用的数据库来源不存在、已删除或与 ProviderType 不匹配。")),
        DatabaseConnectionFailure.DuplicateSource => Conflict(Error("conflict", "该数据库来源已绑定连接配置。")),
        DatabaseConnectionFailure.DuplicateName => Conflict(Error("conflict", "连接配置名称已存在。")),
        DatabaseConnectionFailure.ConcurrencyConflict => Conflict(Error("concurrency_conflict", "连接配置已被其他操作修改，请重新加载后重试。")),
        DatabaseConnectionFailure.ActiveDiscoveryRun => Conflict(Error("DiscoveryAlreadyRunning", "发现运行期间不能修改连接配置或密码。")),
        DatabaseConnectionFailure.DiscoveryTargetImmutable => Conflict(Error(
            "DiscoveryTargetImmutable", "已有成功发现快照后不能修改 Provider 或数据库目标；请创建新的连接配置。")),
        DatabaseConnectionFailure.SecretAlreadySet => UnprocessableEntity(Error("secret_already_set", "连接密码已设置；请使用 Replace Secret。")),
        DatabaseConnectionFailure.SecretMissing => UnprocessableEntity(Error("SecretMissing", "尚未设置连接密码。")),
        _ => throw new InvalidOperationException("Unsupported database connection profile failure."),
    };

    private static string FailureMessage(DatabaseConnectionFailure failure) => failure switch
    {
        DatabaseConnectionFailure.Disabled => "连接配置已停用。",
        DatabaseConnectionFailure.ReferenceInvalid => "关联数据库来源不存在或已不可用。",
        DatabaseConnectionFailure.SecretMissing => "尚未设置数据库连接密码。",
        DatabaseConnectionFailure.SecretUnavailable => "数据库连接密码无法解密，请重新设置。",
        DatabaseConnectionFailure.ProviderUnavailable => "当前 Provider 尚未提供测试连接实现。",
        DatabaseConnectionFailure.AuthenticationFailed => "数据库用户名或密码验证失败。",
        DatabaseConnectionFailure.InsufficientPrivilege => "数据库账号缺少必要权限。",
        DatabaseConnectionFailure.UnsupportedDatabaseVersion => "数据库版本不受支持。",
        DatabaseConnectionFailure.Timeout => "数据库连接测试超时。",
        DatabaseConnectionFailure.Cancelled => "数据库连接测试已取消。",
        _ => "数据库连接测试失败。",
    };

    private static ApiErrorResponse IdValidation() => Validation(
        new Dictionary<string, string[]> { ["id"] = ["连接配置必须是有效 ID。"] });
    private static ApiErrorResponse Validation(IReadOnlyDictionary<string, string[]> errors) =>
        new("validation_error", "请求内容无效。", errors, null);
    private static ApiErrorResponse Error(string code, string message) => new(code, message, null, null);

    private sealed record ActorResolution(
        DatabaseConnectionActor? Actor,
        int? StatusCode,
        ApiErrorResponse? Error);
    private sealed record DatabaseConnectionTestErrorDetails(
        long ProfileId,
        string? NormalizedCode,
        string? VendorCode,
        string? Summary);
}
