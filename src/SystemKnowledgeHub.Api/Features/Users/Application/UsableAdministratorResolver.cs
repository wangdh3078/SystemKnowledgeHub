using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

/// <summary>
/// 按当前启用的认证方式和当前批准的 OIDC Provider，统一判定是否仍存在可登录的 Active Administrator。
/// 锁定和 MustChangePassword 都是可恢复状态，不会使管理员失去“可用”资格。
/// </summary>
public sealed class UsableAdministratorResolver(
    KnowledgeHubDbContext dbContext,
    IOptions<LocalAuthenticationOptions> localOptions,
    IOptions<OidcAuthenticationOptions> oidcOptions)
{
    public Task<bool> IsUserUsableAsync(long userId, CancellationToken cancellationToken = default)
    {
        var localEnabled = localOptions.Value.Enabled;
        var oidcEnabled = oidcOptions.Value.Enabled && !string.IsNullOrWhiteSpace(oidcOptions.Value.Provider);
        var approvedProvider = oidcOptions.Value.Provider;
        return dbContext.Users.AsNoTracking().AnyAsync(user =>
            user.Id == userId
            && user.IsActive
            && user.AccessLevel == AccessLevel.Administrator
            && ((localEnabled && dbContext.LocalLoginCredentials.Any(credential =>
                    credential.UserId == user.Id && credential.IsActive))
                || (oidcEnabled && dbContext.LoginIdentities.Any(identity =>
                    identity.UserId == user.Id
                    && identity.IsActive
                    && identity.Provider == approvedProvider))),
            cancellationToken);
    }

    public Task<bool> IsLoginIdentityUsableAdministratorAccessAsync(
        long loginIdentityId,
        CancellationToken cancellationToken = default)
    {
        if (!oidcOptions.Value.Enabled || string.IsNullOrWhiteSpace(oidcOptions.Value.Provider))
        {
            return Task.FromResult(false);
        }
        var approvedProvider = oidcOptions.Value.Provider;
        return dbContext.LoginIdentities.AsNoTracking().AnyAsync(identity =>
            identity.Id == loginIdentityId
            && identity.IsActive
            && identity.Provider == approvedProvider
            && dbContext.Users.Any(user =>
                user.Id == identity.UserId
                && user.IsActive
                && user.AccessLevel == AccessLevel.Administrator),
            cancellationToken);
    }

    public Task<bool> HasAnyAsync(
        long? excludedUserId = null,
        long? excludedLoginIdentityId = null,
        CancellationToken cancellationToken = default)
    {
        var localEnabled = localOptions.Value.Enabled;
        var oidcEnabled = oidcOptions.Value.Enabled && !string.IsNullOrWhiteSpace(oidcOptions.Value.Provider);
        var approvedProvider = oidcOptions.Value.Provider;

        return dbContext.Users.AsNoTracking().AnyAsync(user =>
            (!excludedUserId.HasValue || user.Id != excludedUserId.Value)
            && user.IsActive
            && user.AccessLevel == AccessLevel.Administrator
            && ((localEnabled && dbContext.LocalLoginCredentials.Any(credential =>
                    credential.UserId == user.Id && credential.IsActive))
                || (oidcEnabled && dbContext.LoginIdentities.Any(identity =>
                    identity.UserId == user.Id
                    && identity.IsActive
                    && identity.Provider == approvedProvider
                    && (!excludedLoginIdentityId.HasValue || identity.Id != excludedLoginIdentityId.Value)))),
            cancellationToken);
    }
}
