using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

public sealed record LocalAdminBootstrapRequest(string Username, string DisplayName, long? UserId, string Password);

public sealed class LocalAdminBootstrapService(KnowledgeHubDbContext dbContext, LocalPasswordService passwords)
{
    public async Task<(bool Succeeded, string? Error)> BootstrapAsync(LocalAdminBootstrapRequest request, CancellationToken cancellationToken)
    {
        if (!LocalCredentialSecurity.TryNormalizeUsername(request.Username, out var username, out var normalizedUsername))
        {
            return (false, "用户名不符合本地登录规则。");
        }
        if (!LocalCredentialSecurity.IsValidPassword(request.Password))
        {
            return (false, "密码长度必须为 8 到 128 个字符。");
        }

        await dbContext.Database.MigrateAsync(cancellationToken);
        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var hasUsableAdministrator = await dbContext.Users.AnyAsync(user =>
            user.IsActive
            && user.AccessLevel == AccessLevel.Administrator
            && (dbContext.LoginIdentities.Any(identity => identity.UserId == user.Id && identity.IsActive)
                || dbContext.LocalLoginCredentials.Any(credential => credential.UserId == user.Id && credential.IsActive)), cancellationToken);
        if (hasUsableAdministrator)
        {
            return (false, "已存在可用的 Administrator，拒绝再次 bootstrap。");
        }
        if (await dbContext.LocalLoginCredentials.AnyAsync(item => item.NormalizedUsername == normalizedUsername, cancellationToken))
        {
            return (false, "用户名已存在，拒绝覆盖现有凭据。");
        }

        var timestamp = DateTimeOffset.UtcNow;
        User user;
        if (request.UserId is long userId)
        {
            if (!ApiIdParser.IsSafePositive(userId)) return (false, "--user-id 必须是 JavaScript 安全范围内的正整数。");
            user = await dbContext.Users.SingleOrDefaultAsync(item => item.Id == userId, cancellationToken)
                ?? throw new InvalidOperationException("指定的 UserId 不存在。");
            if (!user.IsActive) return (false, "指定的 User 必须为启用状态。");
            if (await dbContext.LocalLoginCredentials.AnyAsync(item => item.UserId == userId, cancellationToken))
            {
                return (false, "指定的 User 已有本地登录凭据。");
            }
            user.AccessLevel = AccessLevel.Administrator;
            user.UpdatedAt = timestamp;
            user.Version += 1;
        }
        else
        {
            user = new User
            {
                DisplayName = request.DisplayName.Trim(),
                IsActive = true,
                AccessLevel = AccessLevel.Administrator,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Version = 1,
            };
            dbContext.Users.Add(user);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var credential = new LocalLoginCredential
        {
            UserId = user.Id,
            Username = username,
            NormalizedUsername = normalizedUsername,
            IsActive = true,
            FailedLoginAttempts = 0,
            SessionVersion = 1,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastPasswordChangedAt = timestamp,
            Version = 1,
        };
        credential.PasswordHash = passwords.Hash(credential, request.Password);
        dbContext.LocalLoginCredentials.Add(credential);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return (true, null);
    }
}
