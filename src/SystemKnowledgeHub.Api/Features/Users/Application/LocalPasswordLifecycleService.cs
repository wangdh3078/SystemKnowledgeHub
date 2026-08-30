using System.Collections.Concurrent;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

/// <summary>处理当前 Local 用户的自助改密，并使全部旧 Local 会话立即失效。</summary>
public sealed class LocalPasswordLifecycleService(
    KnowledgeHubDbContext dbContext,
    ICurrentUserContext currentUserContext,
    LocalPasswordService passwords,
    IHttpContextAccessor httpContextAccessor,
    ILogger<LocalPasswordLifecycleService> logger)
{
    private static readonly ConcurrentDictionary<long, SemaphoreSlim> CredentialChangeLocks = new();

    public async Task<LocalPasswordChangeResult> ChangeAsync(
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        var principal = httpContextAccessor.HttpContext?.User;
        AuthenticationSessionDescriptor? descriptor = null;
        if (principal is not null
            && AuthenticationSessionDescriptorReader.TryRead(principal, out var parsedDescriptor))
        {
            descriptor = parsedDescriptor;
        }
        var credentialChangeLock = descriptor?.Method == AuthenticationClaims.LocalMethod
            ? CredentialChangeLocks.GetOrAdd(descriptor.IdentityId, _ => new SemaphoreSlim(1, 1))
            : null;
        if (credentialChangeLock is not null)
        {
            await credentialChangeLock.WaitAsync(cancellationToken);
        }
        try
        {
            return await ChangeCoreAsync(descriptor, currentPassword, newPassword, cancellationToken);
        }
        finally
        {
            credentialChangeLock?.Release();
        }
    }

    private async Task<LocalPasswordChangeResult> ChangeCoreAsync(
        AuthenticationSessionDescriptor? descriptor,
        string? currentPassword,
        string? newPassword,
        CancellationToken cancellationToken)
    {
        var resolution = await currentUserContext.ResolveAsync(cancellationToken);
        if (resolution.Status != CurrentUserResolutionStatus.Available
            && resolution.Status != CurrentUserResolutionStatus.PasswordChangeRequired)
        {
            Audit(descriptor, false, resolution.Reason ?? resolution.Status.ToString());
            return new LocalPasswordChangeResult(LocalPasswordChangeFailure.SessionExpired, null, resolution.Reason);
        }
        if (descriptor is null || descriptor.Method != AuthenticationClaims.LocalMethod)
        {
            Audit(descriptor, false, "authentication_method_not_local");
            return new LocalPasswordChangeResult(LocalPasswordChangeFailure.Forbidden, null, "authentication_method_not_local");
        }

        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrEmpty(currentPassword)
            || currentPassword.Length > LocalCredentialSecurity.MaximumPasswordLength)
        {
            errors["currentPassword"] = ["当前密码不正确。"];
        }
        if (!LocalCredentialSecurity.IsValidPassword(newPassword))
        {
            errors["newPassword"] = ["新密码长度必须为 8 到 128 个字符。"];
        }
        if (errors.Count > 0)
        {
            Audit(descriptor, false, "validation_failed");
            return new LocalPasswordChangeResult(LocalPasswordChangeFailure.Validation, errors);
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        var credential = await dbContext.LocalLoginCredentials
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == descriptor.IdentityId, cancellationToken);
        if (credential is null
            || credential.UserId != descriptor.UserId
            || !credential.IsActive
            || credential.User?.IsActive != true
            || credential.SessionVersion != descriptor.AuthVersion)
        {
            Audit(descriptor, false, "session_version_changed");
            return new LocalPasswordChangeResult(LocalPasswordChangeFailure.SessionExpired, null, "session_version_changed");
        }

        if (passwords.Verify(credential, credential.PasswordHash, currentPassword!) == PasswordVerificationResult.Failed)
        {
            Audit(descriptor, false, "current_password_invalid");
            return new LocalPasswordChangeResult(
                LocalPasswordChangeFailure.Validation,
                new Dictionary<string, string[]> { ["currentPassword"] = ["当前密码不正确。"] });
        }
        if (string.Equals(currentPassword, newPassword, StringComparison.Ordinal))
        {
            Audit(descriptor, false, "password_unchanged");
            return new LocalPasswordChangeResult(
                LocalPasswordChangeFailure.Validation,
                new Dictionary<string, string[]> { ["newPassword"] = ["新密码不能与当前密码相同。"] });
        }

        var timestamp = DateTimeOffset.UtcNow;
        credential.PasswordHash = passwords.Hash(credential, newPassword!);
        credential.LastPasswordChangedAt = timestamp;
        credential.MustChangePassword = false;
        credential.FailedLoginAttempts = 0;
        credential.FailedLoginWindowStartedAt = null;
        credential.LockedUntil = null;
        credential.SessionVersion += 1;
        credential.Version += 1;
        credential.UpdatedAt = timestamp;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            Audit(descriptor, false, "concurrency_conflict");
            return new LocalPasswordChangeResult(LocalPasswordChangeFailure.Conflict, null, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            Audit(descriptor, false, "concurrency_conflict");
            return new LocalPasswordChangeResult(LocalPasswordChangeFailure.Conflict, null, "concurrency_conflict");
        }

        Audit(descriptor, true, "password_changed");
        return new LocalPasswordChangeResult(LocalPasswordChangeFailure.None);
    }

    private void Audit(AuthenticationSessionDescriptor? descriptor, bool success, string reason)
    {
        var httpContext = httpContextAccessor.HttpContext;
        logger.LogInformation(
            "{event_name} actor_user_id={actor_user_id} local_credential_id={local_credential_id} authentication_method={authentication_method} result={result} reason={reason} correlation_id={correlation_id} ip_address={ip_address} user_agent={user_agent} timestamp={timestamp}",
            "LocalPasswordChangedByUser",
            descriptor?.UserId,
            descriptor?.Method == AuthenticationClaims.LocalMethod ? descriptor.IdentityId : null,
            descriptor?.Method,
            success ? "success" : "rejected",
            reason,
            httpContext?.TraceIdentifier,
            httpContext?.Connection.RemoteIpAddress?.ToString(),
            httpContext?.Request.Headers.UserAgent.ToString(),
            DateTimeOffset.UtcNow);
    }
}
