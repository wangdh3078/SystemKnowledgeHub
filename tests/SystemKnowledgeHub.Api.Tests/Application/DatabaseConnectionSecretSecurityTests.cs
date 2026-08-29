using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class DatabaseConnectionSecretSecurityTests
{
    [Fact]
    public async Task Task_owned_database_and_key_ring_survive_restart_while_wrong_ring_purpose_and_corruption_fail_closed()
    {
        using var task = new TaskOwnedDirectory();
        var databasePath = Path.Combine(task.Path, "dbdisc-b01.db");
        var keyPath = Path.Combine(task.Path, "keys");
        var wrongKeyPath = Path.Combine(task.Path, "wrong-keys");
        Directory.CreateDirectory(keyPath);
        Directory.CreateDirectory(wrongKeyPath);
        const string canary = "DBDISC_CANARY_SECRET_RESTART_7f3a";
        long profileId;

        await using (var first = CreateContext(databasePath))
        {
            await first.Database.MigrateAsync();
            var now = DateTimeOffset.UtcNow;
            var user = new User
            {
                DisplayName = "DBDISC Restart User",
                AccessLevel = AccessLevel.Administrator,
                IsActive = true,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1,
            };
            first.Users.Add(user);
            await first.SaveChangesAsync();
            var system = new KnowledgeSystem
            {
                Name = "DBDISC-RESTART",
                DisplayName = "DBDISC Restart",
                SystemType = "Test",
                Lifecycle = SystemLifecycle.Running,
                CreatedAt = now,
                CreatedByUserId = user.Id,
                CreatedByName = user.DisplayName,
                UpdatedAt = now,
                KnowledgeStatus = KnowledgeStatus.Unknown,
                KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = user.DisplayName,
                KnowledgeStatusChangedByRole = "测试",
                Version = 1,
            };
            first.Systems.Add(system);
            await first.SaveChangesAsync();
            var source = new DatabaseSource
            {
                SystemId = system.Id,
                Name = "DBDISC Restart Source",
                Engine = "Oracle",
                CreatedAt = now,
                CreatedByUserId = user.Id,
                CreatedByName = user.DisplayName,
                UpdatedAt = now,
                Version = 1,
            };
            first.DatabaseSources.Add(source);
            await first.SaveChangesAsync();
            var profile = new DatabaseConnectionProfile
            {
                DatabaseSourceId = source.Id,
                Name = "DBDISC Restart Profile",
                ProviderType = DatabaseProviderType.Oracle,
                Host = "restart.example.test",
                Port = 1521,
                ServiceName = "APP_PDB",
                AuthenticationMode = DatabaseAuthenticationMode.UsernamePassword,
                Username = "METADATA_READER",
                IncludedSchemasJson = "[\"APP_OWNER\"]",
                ProviderSpecificOptionsJson = "{\"version\":1}",
                ConnectionStatus = DatabaseConnectionStatus.Unknown,
                ConfigurationRevision = 1,
                CreatedByUserId = user.Id,
                CreatedByDisplayName = user.DisplayName,
                CreatedAt = now,
                UpdatedAt = now,
                Version = 1,
            };
            first.DatabaseConnectionProfiles.Add(profile);
            await first.SaveChangesAsync();
            profileId = profile.Id;
            var store = Store(keyPath);
            first.DatabaseConnectionSecrets.Add(new DatabaseConnectionSecret
            {
                ProfileId = profile.Id,
                ProtectedPayload = store.Protect(profile.Id, canary),
                PayloadFormatVersion = 1,
                UpdatedAt = now,
                Version = 1,
            });
            await first.SaveChangesAsync();
        }

        await using (var restarted = CreateContext(databasePath))
        {
            var secret = await restarted.DatabaseConnectionSecrets.AsNoTracking().SingleAsync();
            Assert.NotEqual(canary, secret.ProtectedPayload);
            Assert.DoesNotContain(canary, secret.ProtectedPayload!, StringComparison.Ordinal);

            var sameRing = Store(keyPath).Resolve(profileId, secret);
            Assert.Equal(DatabaseConnectionSecretFailure.None, sameRing.Failure);
            Assert.Equal(canary, sameRing.Plaintext);
            Assert.Equal(DatabaseConnectionSecretFailure.Unavailable, Store(wrongKeyPath).Resolve(profileId, secret).Failure);
            Assert.Equal(DatabaseConnectionSecretFailure.Unavailable, Store(keyPath).Resolve(profileId + 1, secret).Failure);
            Assert.Equal(DatabaseConnectionSecretFailure.Unavailable, Store(keyPath).Resolve(profileId, new DatabaseConnectionSecret
            {
                ProfileId = profileId,
                ProtectedPayload = secret.ProtectedPayload + "corrupt",
                PayloadFormatVersion = 1,
                Version = 1,
            }).Failure);
            Assert.Equal(DatabaseConnectionSecretFailure.Unavailable, Store(keyPath).Resolve(profileId, new DatabaseConnectionSecret
            {
                ProfileId = profileId,
                ProtectedPayload = secret.ProtectedPayload,
                PayloadFormatVersion = 2,
                Version = 1,
            }).Failure);
        }

        var databaseBytes = await File.ReadAllBytesAsync(databasePath);
        Assert.False(ContainsUtf8(databaseBytes, canary));
        Assert.False(File.Exists(databasePath + "-wal"));
        Assert.False(File.Exists(databasePath + "-shm"));
    }

    private static KnowledgeHubDbContext CreateContext(string path) => new(
        new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseSqlite($"Data Source={path};Foreign Keys=True;Default Timeout=5;Pooling=False")
            .Options);

    private static DataProtectionDatabaseConnectionSecretStore Store(string keyPath)
    {
        var provider = DataProtectionProvider.Create(
            new DirectoryInfo(keyPath),
            builder => builder.SetApplicationName("SystemKnowledgeHub.DBDISC.B01.Tests"));
        return new DataProtectionDatabaseConnectionSecretStore(provider);
    }

    private static bool ContainsUtf8(byte[] haystack, string needle)
    {
        var bytes = System.Text.Encoding.UTF8.GetBytes(needle);
        return haystack.AsSpan().IndexOf(bytes) >= 0;
    }

    private sealed class TaskOwnedDirectory : IDisposable
    {
        public TaskOwnedDirectory()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "SystemKnowledgeHub.DBDISC.B01", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }
        public string Path { get; }
        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
