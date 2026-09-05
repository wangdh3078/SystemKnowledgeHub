using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Users.Application;

public enum LocalLoginFailure
{
    None,
    InvalidCredentials,
}

public sealed record LocalLoginResult(LocalLoginFailure Failure, System.Security.Claims.ClaimsPrincipal? Principal);

public sealed class LocalLoginService(
    KnowledgeHubDbContext dbContext,
    LocalPasswordService passwords,
    AuthenticationPrincipalBuilder principalBuilder,
    IOptions<LocalAuthenticationOptions> options)
{
    public async Task<LocalLoginResult> LoginAsync(string? username, string? password, CancellationToken cancellationToken)
    {
        // Passwords are intentionally never normalized, trimmed, or logged.
        if (password is null || password.Length > LocalCredentialSecurity.MaximumPasswordLength
            || !LocalCredentialSecurity.TryNormalizeUsername(username, out _, out var normalizedUsername))
        {
            passwords.VerifyDummy(password ?? string.Empty);
            return new LocalLoginResult(LocalLoginFailure.InvalidCredentials, null);
        }

        var snapshot = await dbContext.LocalLoginCredentials.AsNoTracking()
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.NormalizedUsername == normalizedUsername, cancellationToken);
        if (snapshot is null)
        {
            passwords.VerifyDummy(password);
            return new LocalLoginResult(LocalLoginFailure.InvalidCredentials, null);
        }

        var verification = passwords.Verify(snapshot, snapshot.PasswordHash, password);
        var now = DateTimeOffset.UtcNow;
        if (snapshot.LockedUntil is DateTimeOffset lockedUntil && lockedUntil > now)
        {
            return new LocalLoginResult(LocalLoginFailure.InvalidCredentials, null);
        }

        // Password verification stays outside the short write reservation. Re-read authoritative
        // state inside it so independent requests cannot overwrite failure counts.
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var credential = await dbContext.LocalLoginCredentials.Include(item => item.User)
            .SingleOrDefaultAsync(item => item.Id == snapshot.Id, cancellationToken);
        if (credential is null || credential.PasswordHash != snapshot.PasswordHash
            || credential.SessionVersion != snapshot.SessionVersion
            || credential.NormalizedUsername != normalizedUsername)
        {
            return new LocalLoginResult(LocalLoginFailure.InvalidCredentials, null);
        }
        now = DateTimeOffset.UtcNow;

        if (verification is PasswordVerificationResult.Failed
            || !credential.IsActive
            || credential.User is null
            || !credential.User.IsActive)
        {
            if (verification is PasswordVerificationResult.Failed && credential.IsActive && credential.User?.IsActive == true)
            {
                RecordFailure(credential, now);
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            return new LocalLoginResult(LocalLoginFailure.InvalidCredentials, null);
        }

        if (credential.LockedUntil is DateTimeOffset currentLock && currentLock > now)
        {
            return new LocalLoginResult(LocalLoginFailure.InvalidCredentials, null);
        }

        credential.FailedLoginAttempts = 0;
        credential.FailedLoginWindowStartedAt = null;
        credential.LockedUntil = null;
        credential.UpdatedAt = now;
        if (verification == PasswordVerificationResult.SuccessRehashNeeded)
        {
            credential.PasswordHash = passwords.Hash(credential, password);
            credential.Version += 1;
        }
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new LocalLoginResult(
            LocalLoginFailure.None,
            principalBuilder.Create(AuthenticationClaims.LocalMethod, credential.Id, credential.SessionVersion, credential.User));
    }

    private void RecordFailure(LocalLoginCredential credential, DateTimeOffset now)
    {
        var policy = options.Value.Lockout;
        var window = TimeSpan.FromMinutes(policy.WindowMinutes);
        if (credential.FailedLoginWindowStartedAt is null || now - credential.FailedLoginWindowStartedAt > window)
        {
            credential.FailedLoginWindowStartedAt = now;
            credential.FailedLoginAttempts = 1;
        }
        else
        {
            credential.FailedLoginAttempts += 1;
        }

        // Count failures already in flight when the lock was established, without
        // extending it. Requests whose initial snapshot is locked exit before writing.
        if (credential.FailedLoginAttempts >= policy.MaxFailedAttempts
            && (credential.LockedUntil is null || credential.LockedUntil <= now))
        {
            credential.LockedUntil = now.AddMinutes(policy.DurationMinutes);
        }
        credential.UpdatedAt = now;
        credential.Version += 1;
    }
}
