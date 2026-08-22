using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class LoginIdentityPersistenceTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory _factory;

    public LoginIdentityPersistenceTests(BootstrapWebApplicationFactory factory) => _factory = factory;

    [Fact]
    public async Task Provider_and_subject_are_unique_and_user_reference_is_restricted()
    {
        await using var scope = _factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var timestamp = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"SEC-01 Mapping User {Guid.NewGuid():N}",
            IsActive = true,
            AccessLevel = AccessLevel.Viewer,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        };
        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync();
        dbContext.LoginIdentities.Add(new LoginIdentity
        {
            UserId = user.Id,
            Provider = "TestOidc",
            Subject = $"exact-subject-{Guid.NewGuid():N}",
            IsActive = true,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        });
        await dbContext.SaveChangesAsync();

        var storedIdentity = await dbContext.LoginIdentities.SingleAsync(item => item.UserId == user.Id);
        dbContext.LoginIdentities.Add(new LoginIdentity
        {
            UserId = user.Id,
            Provider = storedIdentity.Provider,
            Subject = storedIdentity.Subject,
            IsActive = true,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            Version = 1,
        });
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
        dbContext.ChangeTracker.Clear();

        dbContext.Users.Remove(await dbContext.Users.SingleAsync(item => item.Id == user.Id));
        await Assert.ThrowsAsync<DbUpdateException>(() => dbContext.SaveChangesAsync());
    }
}
