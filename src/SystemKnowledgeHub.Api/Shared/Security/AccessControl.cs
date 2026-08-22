using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Shared.Security;

internal static class AccessPolicies
{
    public const string Viewer = "Viewer";
    public const string Editor = "Editor";
    public const string Administrator = "Administrator";
}

internal sealed class AccessLevelRequirement(AccessLevel minimumAccessLevel) : IAuthorizationRequirement
{
    public AccessLevel MinimumAccessLevel { get; } = minimumAccessLevel;
}

internal sealed class AccessLevelAuthorizationHandler(
    ICurrentUserContext currentUserContext) : AuthorizationHandler<AccessLevelRequirement>
{
    protected override async Task HandleRequirementAsync(
        AuthorizationHandlerContext context,
        AccessLevelRequirement requirement)
    {
        var cancellationToken = (context.Resource as HttpContext)?.RequestAborted ?? CancellationToken.None;
        var resolution = await currentUserContext.ResolveAsync(cancellationToken);
        if (resolution.Status != CurrentUserResolutionStatus.Available
            || resolution.CurrentUser is null
            || !Enum.TryParse<AccessLevel>(resolution.CurrentUser.AccessLevel, out var currentAccessLevel)
            || currentAccessLevel < requirement.MinimumAccessLevel)
        {
            return;
        }

        context.Succeed(requirement);
    }
}

internal sealed class ApiAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private static readonly AuthorizationMiddlewareResultHandler DefaultHandler = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Succeeded)
        {
            await next(context);
            return;
        }

        if (!context.Request.Path.StartsWithSegments("/api", StringComparison.OrdinalIgnoreCase))
        {
            await DefaultHandler.HandleAsync(next, context, policy, authorizeResult);
            return;
        }

        var currentUserContext = context.RequestServices.GetRequiredService<ICurrentUserContext>();
        var resolution = await currentUserContext.ResolveAsync(context.RequestAborted);
        var (statusCode, error) = resolution.Status switch
        {
            CurrentUserResolutionStatus.Unauthenticated => (
                StatusCodes.Status401Unauthorized,
                Error("unauthenticated", "尚未登录。", "missing")),
            CurrentUserResolutionStatus.SessionExpired => (
                StatusCodes.Status401Unauthorized,
                Error("session_expired", "登录会话已失效，请重新认证。", "expired")),
            CurrentUserResolutionStatus.IdentityUnmapped => (
                StatusCodes.Status403Forbidden,
                Error("identity_unmapped", "当前登录身份尚未绑定系统用户。", "unmapped")),
            CurrentUserResolutionStatus.IdentityInactive => (
                StatusCodes.Status403Forbidden,
                Error("identity_inactive", "当前登录身份已停用。", "identity_inactive")),
            CurrentUserResolutionStatus.AccountInactive => (
                StatusCodes.Status403Forbidden,
                Error("account_inactive", "当前用户已停用。", "inactive")),
            _ => (
                StatusCodes.Status403Forbidden,
                new ApiErrorResponse(
                    "forbidden",
                    "无权执行此操作。",
                    null,
                    new { currentAccessLevel = resolution.CurrentUser?.AccessLevel }))
        };

        context.Response.StatusCode = statusCode;
        await context.Response.WriteAsJsonAsync(error, context.RequestAborted);
    }

    private static ApiErrorResponse Error(string code, string message, string authStatus) => new(
        code,
        message,
        null,
        new { authStatus });
}
