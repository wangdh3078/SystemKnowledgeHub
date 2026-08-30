using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class DatabaseDiscoveryRunConcurrencyTests
{
    [Fact]
    public async Task Concurrent_triggers_serialize_to_one_active_run_and_database_rejects_a_second_active_row()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"dbdisc-b02-concurrency-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        var databasePath = Path.Combine(directory, "discovery.db");
        try
        {
            var profileId = await Seed(databasePath);
            var token = new ConcurrencyTokenCodec().Encode(1);
            var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            async Task<DatabaseDiscoveryFailure> Trigger()
            {
                await gate.Task;
                await using var db = Context(databasePath);
                var canonical = new CanonicalSnapshotService();
                var service = new DatabaseDiscoveryRunService(
                    db, new ConcurrencyTokenCodec(), canonical, new DatabaseDiscoveryDiffService(canonical));
                return (await service.Trigger(
                    profileId, token,
                    new DatabaseConnectionActor(new CanonicalCreator(1, "Concurrency Administrator")),
                    CancellationToken.None)).Failure;
            }

            var first = Task.Run(Trigger);
            var second = Task.Run(Trigger);
            gate.SetResult();
            var results = await Task.WhenAll(first, second);
            Assert.Equal(1, results.Count(item => item == DatabaseDiscoveryFailure.None));
            Assert.Equal(1, results.Count(item => item == DatabaseDiscoveryFailure.DiscoveryAlreadyRunning));

            await using var verify = Context(databasePath);
            Assert.Equal(1, await verify.DatabaseDiscoveryRuns.CountAsync(item =>
                item.Status == DatabaseDiscoveryRunStatus.Queued || item.Status == DatabaseDiscoveryRunStatus.Running));
            var existing = await verify.DatabaseDiscoveryRuns.SingleAsync();
            verify.DatabaseDiscoveryRuns.Add(new DatabaseDiscoveryRun
            {
                ProfileId = profileId,
                ProfileConfigurationRevision = 1,
                SecretVersion = 1,
                QueuedAt = DateTimeOffset.UtcNow,
                Status = DatabaseDiscoveryRunStatus.Running,
                LeaseOwnerId = "other",
                LeaseToken = Guid.NewGuid().ToString("N"),
                LeaseHeartbeatAt = DateTimeOffset.UtcNow,
                LeaseExpiresAt = DateTimeOffset.UtcNow.AddMinutes(1),
                ProviderType = DatabaseProviderType.Oracle,
                RequestedIncludedSchemasJson = "[\"APP_OWNER\"]",
                RequestedProviderSpecificOptionsJson = "{\"version\":1}",
                RequestedByUserId = 1,
                RequestedByDisplayName = "Concurrency Administrator",
                Version = 1,
            });
            var exception = await Assert.ThrowsAsync<DbUpdateException>(() => verify.SaveChangesAsync());
            Assert.Contains("UNIQUE", exception.InnerException?.Message ?? exception.Message, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(DatabaseDiscoveryRunStatus.Queued, existing.Status);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static KnowledgeHubDbContext Context(string path) => new(
        new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseSqlite($"Data Source={path};Foreign Keys=True;Default Timeout=10;Pooling=False")
            .Options);

    private static async Task<long> Seed(string path)
    {
        await using var db = Context(path);
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        db.Users.Add(new User
        {
            Id = 1,
            DisplayName = "Concurrency Administrator",
            IsActive = true,
            AccessLevel = AccessLevel.Administrator,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        });
        var system = new KnowledgeSystem
        {
            Name = "dbdisc_concurrency",
            DisplayName = "DBDISC concurrency",
            SystemType = "Service",
            Lifecycle = SystemLifecycle.Running,
            CreatedAt = now,
            CreatedByUserId = 1,
            CreatedByName = "Concurrency Administrator",
            UpdatedAt = now,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = now,
            KnowledgeStatusChangedByName = "Concurrency Administrator",
            KnowledgeStatusChangedByRole = "Administrator",
            Version = 1,
        };
        db.Systems.Add(system);
        await db.SaveChangesAsync();
        var source = new DatabaseSource
        {
            SystemId = system.Id,
            Name = "DBDISC concurrency source",
            Engine = "Oracle",
            CreatedAt = now,
            CreatedByUserId = 1,
            CreatedByName = "Concurrency Administrator",
            UpdatedAt = now,
            Version = 1,
        };
        db.DatabaseSources.Add(source);
        await db.SaveChangesAsync();
        var profile = new DatabaseConnectionProfile
        {
            DatabaseSourceId = source.Id,
            Name = "DBDISC concurrency profile",
            ProviderType = DatabaseProviderType.Oracle,
            Host = "db.example.test",
            Port = 1521,
            ServiceName = "APP_PDB",
            AuthenticationMode = DatabaseAuthenticationMode.UsernamePassword,
            Username = "METADATA_READER",
            ProviderSpecificOptionsJson = "{\"version\":1}",
            IncludedSchemasJson = "[\"APP_OWNER\"]",
            IsEnabled = true,
            ConnectionStatus = DatabaseConnectionStatus.Unknown,
            ConfigurationRevision = 1,
            CreatedByUserId = 1,
            CreatedByDisplayName = "Concurrency Administrator",
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.DatabaseConnectionProfiles.Add(profile);
        await db.SaveChangesAsync();
        db.DatabaseConnectionSecrets.Add(new DatabaseConnectionSecret
        {
            ProfileId = profile.Id,
            ProtectedPayload = "test-ciphertext",
            PayloadFormatVersion = 1,
            UpdatedAt = now,
            Version = 1,
        });
        await db.SaveChangesAsync();
        return profile.Id;
    }
}
