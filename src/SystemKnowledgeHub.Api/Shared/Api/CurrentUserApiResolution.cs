using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Shared.Api.Contracts;

namespace SystemKnowledgeHub.Api.Shared.Api;

public sealed record CanonicalCreatorApiResolution(
    CanonicalCreator? Creator,
    int? StatusCode,
    ApiErrorResponse? Error);

public static class CurrentUserApiResolution
{
    public static async Task<CanonicalCreatorApiResolution> ResolveCreator(
        ICurrentUserContext currentUserContext,
        CancellationToken cancellationToken)
    {
        var resolution = await currentUserContext.ResolveAsync(cancellationToken);
        if (resolution.Status == CurrentUserResolutionStatus.Available && resolution.CurrentUser is not null)
        {
            return new CanonicalCreatorApiResolution(
                new CanonicalCreator(resolution.CurrentUser.Id, resolution.CurrentUser.DisplayName),
                null,
                null);
        }

        return resolution.Status switch
        {
            CurrentUserResolutionStatus.Unauthenticated => Failure(
                StatusCodes.Status401Unauthorized, "unauthenticated", "尚未登录。", "missing"),
            CurrentUserResolutionStatus.SessionExpired => Failure(
                StatusCodes.Status401Unauthorized, "session_expired", "登录会话已失效，请重新认证。", "expired"),
            CurrentUserResolutionStatus.IdentityUnmapped => Failure(
                StatusCodes.Status403Forbidden, "identity_unmapped", "当前登录身份尚未绑定系统用户。", "unmapped"),
            CurrentUserResolutionStatus.IdentityInactive => Failure(
                StatusCodes.Status403Forbidden, "identity_inactive", "当前登录身份已停用。", "identity_inactive"),
            CurrentUserResolutionStatus.AccountInactive => Failure(
                StatusCodes.Status403Forbidden, "account_inactive", "当前用户已停用。", "inactive"),
            _ => throw new InvalidOperationException("Unsupported Current User resolution."),
        };
    }

    private static CanonicalCreatorApiResolution Failure(
        int statusCode,
        string code,
        string message,
        string authStatus) => new(
            null,
            statusCode,
            new ApiErrorResponse(code, message, null, new { authStatus }));
}
