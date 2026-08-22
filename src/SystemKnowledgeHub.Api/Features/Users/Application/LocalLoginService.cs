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

        var credential = await dbContext.LocalLoginCredentials
            .Include(item => item.User)
            .SingleOrDefaultAsync(item => item.NormalizedUsername == normalizedUsername, cancellationToken);
        if (credential is null)
        {
            passwords.VerifyDummy(password);
            return new LocalLoginResult(LocalLoginFailure.InvalidCredentials, null);
        }

        var verification = passwords.Verify(credential, credential.PasswordHash, password);
        var now = DateTimeOffset.UtcNow;
        if (credential.LockedUntil is DateTimeOffset lockedUntil && lockedUntil > now)
        {
            return new LocalLoginResult(LocalLoginFailure.InvalidCredentials, null);
        }

        if (verification is PasswordVerificationResult.Failed
            || !credential.IsActive
            || credential.User is null
            || !credential.User.IsActive)
        {
            if (verification is PasswordVerificationResult.Failed && credential.IsActive && credential.User?.IsActive == true)
            {
                RecordFailure(credential, now);
                await dbContext.SaveChangesAsync(cancellationToken);
            }
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

        if (credential.FailedLoginAttempts >= policy.MaxFailedAttempts)
        {
            credential.LockedUntil = now.AddMinutes(policy.DurationMinutes);
        }
        credential.UpdatedAt = now;
        credential.Version += 1;
    }
}
