using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

/// <summary>为已有 User 创建、启用或停用其唯一 Local credential。</summary>
public sealed class LocalCredentialManagementService(
    KnowledgeHubDbContext dbContext,
    UserQueries queries,
    ConcurrencyTokenCodec concurrencyTokenCodec,
    LocalPasswordService passwords,
    UsableAdministratorResolver usableAdministrators,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LocalCredentialManagementService> logger)
{
    public async Task<LocalCredentialWriteResult> CreateAsync(
        CreateUserLocalCredentialCommand command,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!LocalCredentialSecurity.TryNormalizeUsername(
                command.Username,
                out var username,
                out var normalizedUsername))
        {
            errors["username"] = ["登录用户名必须为 3～64 个字符，且只能包含字母、数字、点、下划线、连字符或 @。"];
        }
        if (!LocalCredentialSecurity.IsValidPassword(command.InitialPassword))
        {
            errors["initialPassword"] = ["初始密码长度必须为 8～128 个字符。"];
        }
        if (errors.Count > 0)
        {
            Audit("LocalCredentialCreated", command.UserId, null, "rejected", "validation_failed");
            return new(null, errors, LocalCredentialWriteFailure.Validation, "password_or_username_invalid");
        }

        if (!await dbContext.Users.AsNoTracking().AnyAsync(user => user.Id == command.UserId, cancellationToken))
        {
            Audit("LocalCredentialCreated", command.UserId, null, "rejected", "user_not_found");
            return new(null, null, LocalCredentialWriteFailure.NotFound, "user_not_found");
        }
        if (await dbContext.LocalLoginCredentials.AsNoTracking()
            .AnyAsync(credential => credential.UserId == command.UserId, cancellationToken))
        {
            Audit("LocalCredentialCreated", command.UserId, null, "rejected", "local_credential_exists");
            return new(
                null,
                new Dictionary<string, string[]> { ["username"] = ["该用户已配置本地账号。"] },
                LocalCredentialWriteFailure.Conflict,
                "local_credential_exists");
        }
        if (await dbContext.LocalLoginCredentials.AsNoTracking()
            .AnyAsync(credential => credential.NormalizedUsername == normalizedUsername, cancellationToken))
        {
            Audit("LocalCredentialCreated", command.UserId, null, "rejected", "username_duplicate");
            return new(
                null,
                new Dictionary<string, string[]> { ["username"] = ["登录用户名已存在。"] },
                LocalCredentialWriteFailure.Conflict,
                "username_duplicate");
        }

        var timestamp = DateTimeOffset.UtcNow;
        var credential = new LocalLoginCredential
        {
            UserId = command.UserId,
            Username = username,
            NormalizedUsername = normalizedUsername,
            IsActive = true,
            MustChangePassword = true,
            FailedLoginAttempts = 0,
            FailedLoginWindowStartedAt = null,
            LockedUntil = null,
            SessionVersion = 1,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastPasswordChangedAt = timestamp,
            Version = 1,
        };
        credential.PasswordHash = passwords.Hash(credential, command.InitialPassword!);
        dbContext.LocalLoginCredentials.Add(credential);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            Audit("LocalCredentialCreated", command.UserId, null, "rejected", "credential_or_username_conflict");
            return new(
                null,
                new Dictionary<string, string[]> { ["username"] = ["登录用户名已存在，或该用户已配置本地账号。"] },
                LocalCredentialWriteFailure.Conflict,
                "credential_or_username_conflict");
        }

        Audit("LocalCredentialCreated", command.UserId, credential.Id, "success", "created");
        return new(
            (await queries.GetUserLoginMethods(command.UserId, cancellationToken))!.Local,
            null,
            LocalCredentialWriteFailure.None);
    }

    public async Task<LocalCredentialWriteResult> SetActiveStateAsync(
        SetLocalCredentialActiveStateCommand command,
        CancellationToken cancellationToken)
    {
        var eventType = command.IsActive ? "LocalCredentialEnabled" : "LocalCredentialDisabled";
        if (!concurrencyTokenCodec.TryDecode(command.ConcurrencyToken ?? string.Empty, out var expectedVersion))
        {
            Audit(eventType, command.UserId, null, "rejected", "invalid_concurrency_token");
            return new(
                null,
                new Dictionary<string, string[]> { ["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"] },
                LocalCredentialWriteFailure.Validation,
                "invalid_concurrency_token");
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var credential = await dbContext.LocalLoginCredentials
            .SingleOrDefaultAsync(item => item.UserId == command.UserId, cancellationToken);
        if (credential is null)
        {
            Audit(eventType, command.UserId, null, "rejected", "credential_not_found");
            return new(null, null, LocalCredentialWriteFailure.NotFound, "credential_not_found");
        }
        if (credential.Version != expectedVersion)
        {
            Audit(eventType, command.UserId, credential.Id, "rejected", "concurrency_conflict");
            return new(null, null, LocalCredentialWriteFailure.Conflict, "concurrency_conflict");
        }
        if (credential.IsActive == command.IsActive)
        {
            Audit(eventType, command.UserId, credential.Id, "rejected", "state_unchanged");
            return new(null, null, LocalCredentialWriteFailure.NoChange, "state_unchanged");
        }
        if (!command.IsActive
            && await usableAdministrators.IsLocalCredentialUsableAdministratorAccessAsync(
                credential.Id,
                cancellationToken)
            && !await usableAdministrators.HasAnyAsync(
                excludedLocalCredentialId: credential.Id,
                cancellationToken: cancellationToken))
        {
            Audit(eventType, command.UserId, credential.Id, "rejected", "last_usable_administrator");
            return new(null, null, LocalCredentialWriteFailure.LastUsableAdministrator, "last_usable_administrator");
        }

        credential.IsActive = command.IsActive;
        credential.SessionVersion += 1;
        credential.Version = expectedVersion + 1;
        credential.UpdatedAt = DateTimeOffset.UtcNow;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            Audit(eventType, command.UserId, credential.Id, "rejected", "concurrency_conflict");
            return new(null, null, LocalCredentialWriteFailure.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            Audit(eventType, command.UserId, credential.Id, "rejected", "concurrency_conflict");
            return new(null, null, LocalCredentialWriteFailure.Conflict, "concurrency_conflict");
        }

        Audit(eventType, command.UserId, credential.Id, "success", command.IsActive ? "enabled" : "disabled");
        return new(
            (await queries.GetUserLoginMethods(command.UserId, cancellationToken))!.Local,
            null,
            LocalCredentialWriteFailure.None);
    }

    private void Audit(
        string eventType,
        long targetUserId,
        long? credentialId,
        string result,
        string reasonCode)
    {
        long? actorUserId = null;
        var httpContext = httpContextAccessor.HttpContext;
        if (httpContext?.User is not null
            && AuthenticationSessionDescriptorReader.TryRead(httpContext.User, out var descriptor))
        {
            actorUserId = descriptor.UserId;
        }
        logger.LogInformation(
            "SecurityEvent EventType={EventType} ActorUserId={ActorUserId} TargetUserId={TargetUserId} CredentialId={CredentialId} LoginIdentityId={LoginIdentityId} Result={Result} ReasonCode={ReasonCode} OccurredAt={OccurredAt} CorrelationId={CorrelationId}",
            eventType,
            actorUserId,
            targetUserId,
            credentialId,
            null,
            result,
            reasonCode,
            DateTimeOffset.UtcNow,
            httpContext?.TraceIdentifier);
    }
}
