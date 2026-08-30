using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class UsableAdministratorResolverTests
{
    [Fact]
    public async Task Resolver_counts_only_the_enabled_oidc_provider()
    {
        using var factory = new AuthenticationOptionsWebApplicationFactory(false, true);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<UsableAdministratorResolver>();

        Assert.True(await resolver.HasAnyAsync());
        var identity = await db.LoginIdentities.SingleAsync();
        identity.Provider = "UnapprovedProvider";
        await db.SaveChangesAsync();
        Assert.False(await resolver.HasAnyAsync());
    }

    [Fact]
    public async Task Resolver_counts_active_local_credentials_even_when_locked_or_forced_to_change_password()
    {
        using var factory = new AuthenticationOptionsWebApplicationFactory(true, false);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var resolver = scope.ServiceProvider.GetRequiredService<UsableAdministratorResolver>();
        var passwords = scope.ServiceProvider.GetRequiredService<LocalPasswordService>();

        Assert.False(await resolver.HasAnyAsync());
        var userId = await db.Users.Select(user => user.Id).SingleAsync();
        var timestamp = DateTimeOffset.UtcNow;
        var credential = new LocalLoginCredential
        {
            UserId = userId,
            Username = "recoverable-local-admin",
            NormalizedUsername = "RECOVERABLE-LOCAL-ADMIN",
            IsActive = true,
            MustChangePassword = true,
            FailedLoginAttempts = 5,
            FailedLoginWindowStartedAt = timestamp,
            LockedUntil = timestamp.AddHours(1),
            SessionVersion = 1,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            LastPasswordChangedAt = timestamp,
            Version = 1,
        };
        credential.PasswordHash = passwords.Hash(credential, "recoverable local admin password");
        db.LocalLoginCredentials.Add(credential);
        await db.SaveChangesAsync();

        Assert.True(await resolver.HasAnyAsync());
        credential.IsActive = false;
        await db.SaveChangesAsync();
        Assert.False(await resolver.HasAnyAsync());
    }
}
