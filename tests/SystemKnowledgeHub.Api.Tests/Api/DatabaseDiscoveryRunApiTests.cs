using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class DatabaseDiscoveryRunApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Durable_worker_completes_first_and_changed_snapshots_with_sanitized_viewer_reads()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await CreateProfile(factory, administrator);
        const string canary = "DBDISC_B02_CANARY_SUCCESS_SECRET";
        profile = await SetSecret(administrator, profile, canary);

        var first = await Trigger(administrator, profile);
        first = await WaitForTerminal(administrator, first.Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, first.Status);
        Assert.Null(first.BaseSnapshotId);
        Assert.NotNull(first.SnapshotId);
        Assert.NotNull(first.DifferenceId);
        Assert.Equal(13, first.ObjectCounts!.Schemas + first.ObjectCounts.Objects + first.ObjectCounts.Columns
            + first.ObjectCounts.PrimaryKeys + first.ObjectCounts.ForeignKeys + first.ObjectCounts.UniqueConstraints
            + first.ObjectCounts.Indexes + first.ObjectCounts.Sequences);

        var viewerId = await CreateUser(factory, AccessLevel.Viewer);
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);
        using var viewerRun = await viewer.GetAsync($"/api/database-discovery/runs/{first.Id}");
        Assert.Equal(HttpStatusCode.OK, viewerRun.StatusCode);
        var viewerRunJson = await viewerRun.Content.ReadAsStringAsync();
        Assert.DoesNotContain(canary, viewerRunJson, StringComparison.Ordinal);
        Assert.DoesNotContain(profile.Host, viewerRunJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(profile.Username, viewerRunJson, StringComparison.Ordinal);
        Assert.DoesNotContain("secretVersion", viewerRunJson, StringComparison.OrdinalIgnoreCase);
        using var viewerTrigger = await viewer.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/discovery-runs",
            new { concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.Forbidden, viewerTrigger.StatusCode);
        using var viewerCancel = await viewer.PostAsJsonAsync(
            $"/api/database-discovery/runs/{first.Id}/cancel",
            new { concurrencyToken = first.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.Forbidden, viewerCancel.StatusCode);

        using var snapshotResponse = await viewer.GetAsync($"/api/database-discovery/snapshots/{first.SnapshotId}");
        Assert.Equal(HttpStatusCode.OK, snapshotResponse.StatusCode);
        var snapshotJson = await snapshotResponse.Content.ReadAsStringAsync();
        Assert.DoesNotContain(canary, snapshotJson, StringComparison.Ordinal);
        using var firstDifference = await viewer.GetAsync($"/api/database-discovery/differences/{first.DifferenceId}");
        Assert.Equal(HttpStatusCode.OK, firstDifference.StatusCode);
        var firstDiff = await firstDifference.Content.ReadFromJsonAsync<DatabaseDiscoveryDifferenceResponse>(JsonOptions);
        Assert.Equal(13, firstDiff!.SummaryCounts.Added);
        Assert.Equal(0, firstDiff.SummaryCounts.Unchanged);

        profile = await GetProfile(administrator, profile.Id);
        var second = await Trigger(administrator, profile);
        second = await WaitForTerminal(administrator, second.Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, second.Status);
        Assert.Equal(first.SnapshotId, second.BaseSnapshotId);
        Assert.Equal(first.ScopeGenerationId, second.ScopeGenerationId);
        using var secondDifference = await viewer.GetAsync($"/api/database-discovery/differences/{second.DifferenceId}");
        var secondDiff = await secondDifference.Content.ReadFromJsonAsync<DatabaseDiscoveryDifferenceResponse>(JsonOptions);
        Assert.Equal(1, secondDiff!.SummaryCounts.Changed);
        Assert.Equal(12, secondDiff.SummaryCounts.Unchanged);
        using var changedEntries = await viewer.GetAsync($"/api/database-discovery/differences/{second.DifferenceId}/entries?state=Changed");
        var changed = await changedEntries.Content.ReadFromJsonAsync<DatabaseDiscoveryDifferenceEntryPageResponse>(JsonOptions);
        Assert.Equal(DatabaseDiscoveryEntityKind.Column, Assert.Single(changed!.Items).EntityKind);
        using var unchangedEntries = await viewer.GetAsync($"/api/database-discovery/differences/{second.DifferenceId}/entries?state=Unchanged&pageSize=200");
        var unchanged = await unchangedEntries.Content.ReadFromJsonAsync<DatabaseDiscoveryDifferenceEntryPageResponse>(JsonOptions);
        Assert.Equal(12, unchanged!.Total);

        profile = await GetProfile(administrator, profile.Id);
        using var immutableTarget = await administrator.PutAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}", UpdateRequest(profile));
        Assert.Equal(HttpStatusCode.Conflict, immutableTarget.StatusCode);
        Assert.Contains("DiscoveryTargetImmutable", await immutableTarget.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(2, await db.DatabaseDiscoverySnapshots.CountAsync());
        Assert.Equal(2, await db.DatabaseDiscoveryDifferences.CountAsync());
        var storedFields = await db.DatabaseDiscoveryRuns.Select(item => new[]
        {
            item.ErrorCode ?? "", item.ErrorSummary ?? "", item.SafeErrorMetadataJson ?? "",
            item.CapabilitySnapshotJson ?? "", item.ObjectCountsJson ?? "",
        }).ToArrayAsync();
        var ordinary = string.Join('|', storedFields.SelectMany(item => item));
        Assert.DoesNotContain(canary, ordinary, StringComparison.Ordinal);
        var immutable = await db.DatabaseDiscoverySnapshots.SingleAsync(item => item.Id == first.SnapshotId);
        immutable.ContentSha256 = new string('0', 64);
        await Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Compatible_baseline_persists_missing_entries_and_derives_unchanged_entries()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        factory.DiscoveryProvider.SnapshotFactory = (connection, request, call) =>
        {
            var snapshot = CanonicalSnapshotFixtures.Create(connection, request, version: 1);
            return call == 1 ? snapshot : snapshot with { Sequences = [] };
        };
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "missing-secret");

        var first = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        profile = await GetProfile(administrator, profile.Id);
        var second = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);

        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, second.Status);
        Assert.Equal(first.SnapshotId, second.BaseSnapshotId);
        using var differenceResponse = await administrator.GetAsync($"/api/database-discovery/differences/{second.DifferenceId}");
        var difference = await differenceResponse.Content.ReadFromJsonAsync<DatabaseDiscoveryDifferenceResponse>(JsonOptions);
        Assert.Equal(1, difference!.SummaryCounts.MissingFromSource);
        Assert.Equal(12, difference.SummaryCounts.Unchanged);
        using var missingResponse = await administrator.GetAsync(
            $"/api/database-discovery/differences/{second.DifferenceId}/entries?state=MissingFromSource");
        var missing = await missingResponse.Content.ReadFromJsonAsync<DatabaseDiscoveryDifferenceEntryPageResponse>(JsonOptions);
        var entry = Assert.Single(missing!.Items);
        Assert.Equal(DatabaseDiscoveryEntityKind.Sequence, entry.EntityKind);
        Assert.NotNull(entry.Before);
        Assert.Null(entry.After);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(1, await db.DatabaseDiscoveryDifferenceEntries.CountAsync(item =>
            item.DifferenceId == second.DifferenceId
            && item.State == DatabaseDiscoveryDifferenceState.MissingFromSource));
        Assert.Equal(0, await db.DatabaseDiscoveryDifferenceEntries.CountAsync(item =>
            item.DifferenceId == second.DifferenceId
            && item.State == DatabaseDiscoveryDifferenceState.Unchanged));
    }

    [Fact]
    public async Task Incompatible_scope_starts_a_new_generation_without_missing_entries()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        factory.DiscoveryProvider.SnapshotFactory = (connection, request, call) =>
            CanonicalSnapshotFixtures.Create(connection, request, 1, targetFingerprint: $"target-{call}");
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "scope-secret");

        var first = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        profile = await GetProfile(administrator, profile.Id);
        var second = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);

        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, first.Status);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, second.Status);
        Assert.Null(second.BaseSnapshotId);
        Assert.NotEqual(first.ScopeGenerationId, second.ScopeGenerationId);
        using var response = await administrator.GetAsync($"/api/database-discovery/differences/{second.DifferenceId}");
        var difference = await response.Content.ReadFromJsonAsync<DatabaseDiscoveryDifferenceResponse>(JsonOptions);
        Assert.Equal(13, difference!.SummaryCounts.Added);
        Assert.Equal(0, difference.SummaryCounts.MissingFromSource);
    }

    [Fact]
    public async Task Provider_failure_and_timeout_persist_safe_terminal_runs_without_snapshots_or_secret_leakage()
    {
        const string canary = "DBDISC_B02_CANARY_PROVIDER_EXCEPTION";
        using (var factory = new DatabaseDiscoveryWebApplicationFactory())
        {
            factory.DiscoveryProvider.Handler = (connection, _, _) =>
                throw new InvalidOperationException($"raw={connection.Password};{canary};(DESCRIPTION=secret);SELECT password");
            using var administrator = factory.CreateAuthenticatedClient();
            var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), canary);
            var run = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
            Assert.Equal(DatabaseDiscoveryRunStatus.Failed, run.Status);
            Assert.Equal("MetadataQueryFailed", run.ErrorCode);
            Assert.Null(run.SnapshotId);
            var api = JsonSerializer.Serialize(run, JsonOptions);
            Assert.DoesNotContain(canary, api, StringComparison.Ordinal);
            Assert.DoesNotContain("DESCRIPTION", api, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(canary, string.Join('|', factory.LogSink.Entries), StringComparison.Ordinal);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.Equal(0, await db.DatabaseDiscoverySnapshots.CountAsync());
            Assert.Equal(0, await db.DatabaseDiscoveryDifferences.CountAsync());
            var stored = await db.DatabaseDiscoveryRuns.SingleAsync();
            Assert.DoesNotContain(canary, string.Join('|', stored.ErrorCode, stored.ErrorSummary, stored.SafeErrorMetadataJson), StringComparison.Ordinal);
        }

        using (var factory = new DatabaseDiscoveryWebApplicationFactory())
        {
            factory.DiscoveryProvider.Handler = (_, _, _) =>
                throw new DatabaseDiscoveryProviderException(
                    "AuthenticationFailed", "Oracle 用户名或密码验证失败。", "ORA-01017");
            using var administrator = factory.CreateAuthenticatedClient();
            var profile = await SetSecret(
                administrator, await CreateProfile(factory, administrator), "oracle-provider-secret");
            var run = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
            Assert.Equal(DatabaseDiscoveryRunStatus.Failed, run.Status);
            Assert.Equal("AuthenticationFailed", run.ErrorCode);
            Assert.Equal("Oracle 用户名或密码验证失败。", run.ErrorSummary);
            Assert.Null(run.SnapshotId);
            Assert.Null(run.DifferenceId);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var stored = await db.DatabaseDiscoveryRuns.SingleAsync();
            Assert.Equal("{\"vendorCode\":\"ORA-01017\"}", stored.SafeErrorMetadataJson);
            Assert.Equal(0, await db.DatabaseDiscoverySnapshots.CountAsync());
            Assert.Equal(0, await db.DatabaseDiscoveryDifferences.CountAsync());
        }

        using (var factory = new DatabaseDiscoveryWebApplicationFactory { WorkerOverallTimeoutSeconds = 1 })
        {
            factory.DiscoveryProvider.Handler = async (_, _, cancellationToken) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                throw new InvalidOperationException("unreachable");
            };
            using var administrator = factory.CreateAuthenticatedClient();
            var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "timeout-secret");
            var run = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id, TimeSpan.FromSeconds(6));
            Assert.Equal(DatabaseDiscoveryRunStatus.Failed, run.Status);
            Assert.Equal("Timeout", run.ErrorCode);
            Assert.Null(run.SnapshotId);
        }

        using (var factory = new DatabaseDiscoveryWebApplicationFactory())
        {
            factory.DiscoveryProvider.SnapshotFactory = (connection, request, _) =>
                CanonicalSnapshotFixtures.Create(connection, request) with { FormatVersion = 999 };
            using var administrator = factory.CreateAuthenticatedClient();
            var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "invalid-canonical-secret");
            var run = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
            Assert.Equal(DatabaseDiscoveryRunStatus.Failed, run.Status);
            Assert.Equal("MetadataQueryFailed", run.ErrorCode);
            Assert.Null(run.SnapshotId);
            await using var scope = factory.Services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.Equal(0, await db.DatabaseDiscoverySnapshots.CountAsync());
            Assert.Equal(0, await db.DatabaseDiscoveryDifferences.CountAsync());
        }
    }

    [Fact]
    public async Task Overall_timeout_keeps_lease_alive_until_non_cooperative_provider_returns()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory
        {
            WorkerOverallTimeoutSeconds = 1,
            WorkerLeaseDurationSeconds = 2,
            WorkerHeartbeatIntervalSeconds = 1,
        };
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        factory.DiscoveryProvider.Handler = async (_, _, cancellationToken) =>
        {
            entered.TrySetResult();
            await release.Task;
            cancellationToken.ThrowIfCancellationRequested();
            throw new InvalidOperationException("unreachable");
        };

        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(
            administrator, await CreateProfile(factory, administrator), "timeout-lease-secret");
        var trigger = await Trigger(administrator, profile);
        await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        DateTimeOffset? firstHeartbeat;
        DateTimeOffset? secondHeartbeat;
        DateTimeOffset? secondExpiry;
        DatabaseDiscoveryRunStatus secondStatus;
        try
        {
            await Task.Delay(1600);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
                firstHeartbeat = (await db.DatabaseDiscoveryRuns.AsNoTracking()
                    .SingleAsync(item => item.Id == trigger.Id)).LeaseHeartbeatAt;
            }

            await Task.Delay(1300);
            await using (var scope = factory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
                var stored = await db.DatabaseDiscoveryRuns.AsNoTracking()
                    .SingleAsync(item => item.Id == trigger.Id);
                secondHeartbeat = stored.LeaseHeartbeatAt;
                secondExpiry = stored.LeaseExpiresAt;
                secondStatus = stored.Status;
            }
        }
        finally
        {
            release.TrySetResult();
        }

        Assert.NotNull(firstHeartbeat);
        Assert.True(secondHeartbeat > firstHeartbeat);
        Assert.True(secondExpiry > DateTimeOffset.UtcNow);
        Assert.Equal(DatabaseDiscoveryRunStatus.Running, secondStatus);

        var terminal = await WaitForTerminal(administrator, trigger.Id, TimeSpan.FromSeconds(6));
        Assert.Equal(DatabaseDiscoveryRunStatus.Failed, terminal.Status);
        Assert.Equal("Timeout", terminal.ErrorCode);
        Assert.Null(terminal.SnapshotId);
        Assert.Null(terminal.DifferenceId);
    }

    [Fact]
    public async Task Queued_and_running_cancel_are_durable_and_running_heartbeat_blocks_profile_mutation()
    {
        using (var queuedFactory = new DatabaseDiscoveryWebApplicationFactory { WorkerPollIntervalMilliseconds = 60_000 })
        {
            using var administrator = queuedFactory.CreateAuthenticatedClient();
            await Task.Delay(150);
            var profile = await SetSecret(administrator, await CreateProfile(queuedFactory, administrator), "queued-secret");
            var queued = await Trigger(administrator, profile);
            using var cancelResponse = await administrator.PostAsJsonAsync(
                $"/api/database-discovery/runs/{queued.Id}/cancel", new { concurrencyToken = queued.ConcurrencyToken });
            Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
            var cancelled = await cancelResponse.Content.ReadFromJsonAsync<DatabaseDiscoveryRunResponse>(JsonOptions);
            Assert.Equal(DatabaseDiscoveryRunStatus.Cancelled, cancelled!.Status);
            Assert.Equal(0, queuedFactory.DiscoveryProvider.CallCount);
        }

        using (var runningFactory = new DatabaseDiscoveryWebApplicationFactory())
        {
            runningFactory.DiscoveryProvider.GateCalls = true;
            using var administrator = runningFactory.CreateAuthenticatedClient();
            var profile = await SetSecret(administrator, await CreateProfile(runningFactory, administrator), "running-secret");
            var trigger = await Trigger(administrator, profile);
            var call = await runningFactory.DiscoveryProvider.WaitForCall().WaitAsync(TimeSpan.FromSeconds(5));
            var running = await GetRun(administrator, trigger.Id);
            Assert.Equal(DatabaseDiscoveryRunStatus.Running, running.Status);
            DateTimeOffset? firstHeartbeat;
            long firstVersion;
            await using (var scope = runningFactory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
                var stored = await db.DatabaseDiscoveryRuns.AsNoTracking().SingleAsync(item => item.Id == running.Id);
                firstHeartbeat = stored.LeaseHeartbeatAt;
                firstVersion = stored.Version;
            }
            await Task.Delay(1200);
            await using (var scope = runningFactory.Services.CreateAsyncScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
                var stored = await db.DatabaseDiscoveryRuns.AsNoTracking().SingleAsync(item => item.Id == running.Id);
                Assert.True(stored.LeaseHeartbeatAt > firstHeartbeat);
                Assert.True(stored.Version > firstVersion);
            }

            profile = await GetProfile(administrator, profile.Id);
            using var update = await administrator.PutAsJsonAsync(
                $"/api/admin/database-connection-profiles/{profile.Id}", UpdateRequest(profile));
            Assert.Equal(HttpStatusCode.Conflict, update.StatusCode);
            Assert.Contains("DiscoveryAlreadyRunning", await update.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            using var test = await administrator.PostAsJsonAsync(
                $"/api/admin/database-connection-profiles/{profile.Id}/test-connection",
                new { concurrencyToken = profile.ConcurrencyToken });
            Assert.Equal(HttpStatusCode.Conflict, test.StatusCode);
            using var replaceSecret = await administrator.PutAsJsonAsync(
                $"/api/admin/database-connection-profiles/{profile.Id}/secret",
                new { password = "replacement-must-not-apply", concurrencyToken = profile.ConcurrencyToken });
            Assert.Equal(HttpStatusCode.Conflict, replaceSecret.StatusCode);
            Assert.Contains("DiscoveryAlreadyRunning", await replaceSecret.Content.ReadAsStringAsync(), StringComparison.Ordinal);
            using var disable = await administrator.PutAsJsonAsync(
                $"/api/admin/database-connection-profiles/{profile.Id}/enabled-state",
                new { isEnabled = false, concurrencyToken = profile.ConcurrencyToken });
            Assert.Equal(HttpStatusCode.Conflict, disable.StatusCode);

            running = await GetRun(administrator, running.Id);
            using var cancel = await administrator.PostAsJsonAsync(
                $"/api/database-discovery/runs/{running.Id}/cancel", new { concurrencyToken = running.ConcurrencyToken });
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
            var terminal = await WaitForTerminal(administrator, running.Id, TimeSpan.FromSeconds(6));
            Assert.Equal(DatabaseDiscoveryRunStatus.Cancelled, terminal.Status);
            Assert.Null(terminal.SnapshotId);
            _ = call;
        }
    }

    [Fact]
    public async Task Expired_lease_recovery_is_token_safe_and_respects_pending_cancel()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory { WorkerPollIntervalMilliseconds = 60_000 };
        using var administrator = factory.CreateAuthenticatedClient();
        await Task.Delay(150);
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "recovery-secret");
        var queued = await Trigger(administrator, profile);
        ClaimedDatabaseDiscoveryRun claim;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            claim = (await scope.ServiceProvider.GetRequiredService<DatabaseDiscoveryRunProcessor>()
                .ClaimNext("recovery-owner", CancellationToken.None))!;
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var run = await db.DatabaseDiscoveryRuns.SingleAsync(item => item.Id == queued.Id);
            run.LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseDiscoveryRunProcessor>()
                .RecoverExpiredRuns(CancellationToken.None);
        }
        var failed = await GetRun(administrator, queued.Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Failed, failed.Status);
        Assert.Equal("RunInterrupted", failed.ErrorCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseDiscoveryTerminalWriter>()
                .Fail(claim, "MetadataQueryFailed", "读取数据库结构元数据失败。", CancellationToken.None);
        }
        Assert.Equal("RunInterrupted", (await GetRun(administrator, queued.Id)).ErrorCode);

        profile = await GetProfile(administrator, profile.Id);
        var queuedCancel = await Trigger(administrator, profile);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            _ = await scope.ServiceProvider.GetRequiredService<DatabaseDiscoveryRunProcessor>()
                .ClaimNext("recovery-owner-2", CancellationToken.None);
        }
        var running = await GetRun(administrator, queuedCancel.Id);
        using (var cancel = await administrator.PostAsJsonAsync(
            $"/api/database-discovery/runs/{running.Id}/cancel", new { concurrencyToken = running.ConcurrencyToken }))
            Assert.Equal(HttpStatusCode.OK, cancel.StatusCode);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var run = await db.DatabaseDiscoveryRuns.SingleAsync(item => item.Id == running.Id);
            run.LeaseExpiresAt = DateTimeOffset.UtcNow.AddSeconds(-1);
            await db.SaveChangesAsync();
        }
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<DatabaseDiscoveryRunProcessor>()
                .RecoverExpiredRuns(CancellationToken.None);
        }
        var cancelled = await GetRun(administrator, running.Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Cancelled, cancelled.Status);
        Assert.Equal("Cancelled", cancelled.ErrorCode);
        Assert.NotEqual(claim.LeaseToken, cancelled.ConcurrencyToken);
    }

    private static async Task<DatabaseDiscoveryRunResponse> Trigger(HttpClient client, DatabaseConnectionProfileResponse profile)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/discovery-runs",
            new { concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<DatabaseDiscoveryRunResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Discovery trigger response was empty.");
    }

    private static async Task<DatabaseDiscoveryRunResponse> WaitForTerminal(
        HttpClient client,
        long runId,
        TimeSpan? timeout = null)
    {
        var deadline = DateTimeOffset.UtcNow + (timeout ?? TimeSpan.FromSeconds(8));
        while (DateTimeOffset.UtcNow < deadline)
        {
            var run = await GetRun(client, runId);
            if (run.Status is DatabaseDiscoveryRunStatus.Succeeded or DatabaseDiscoveryRunStatus.Failed or DatabaseDiscoveryRunStatus.Cancelled)
                return run;
            await Task.Delay(40);
        }
        throw new TimeoutException($"Discovery Run {runId} did not become terminal.");
    }

    private static async Task<DatabaseDiscoveryRunResponse> GetRun(HttpClient client, long runId)
    {
        using var response = await client.GetAsync($"/api/database-discovery/runs/{runId}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<DatabaseDiscoveryRunResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Discovery Run response was empty.");
    }

    private static object ProfileRequest(long sourceId) => new
    {
        databaseSourceId = sourceId,
        name = $"B02-{Guid.NewGuid():N}",
        providerType = "Oracle",
        host = "db.example.test",
        port = 1521,
        databaseName = (string?)null,
        serviceName = "APP_PDB",
        authenticationMode = "UsernamePassword",
        username = "METADATA_READER",
        providerSpecificOptions = new { version = 1 },
        includedSchemas = new[] { "APP_OWNER" },
        isEnabled = true,
    };

    private static object UpdateRequest(DatabaseConnectionProfileResponse profile) => new
    {
        name = profile.Name,
        providerType = profile.ProviderType.ToString(),
        host = "changed.example.test",
        port = profile.Port,
        databaseName = profile.DatabaseName,
        serviceName = profile.ServiceName,
        authenticationMode = profile.AuthenticationMode.ToString(),
        username = profile.Username,
        providerSpecificOptions = new { version = 1 },
        includedSchemas = profile.IncludedSchemas,
        concurrencyToken = profile.ConcurrencyToken,
    };

    private static async Task<DatabaseConnectionProfileResponse> CreateProfile(
        DatabaseDiscoveryWebApplicationFactory factory,
        HttpClient client)
    {
        var sourceId = await CreateSource(factory);
        using var response = await client.PostAsJsonAsync(
            "/api/admin/database-connection-profiles", ProfileRequest(sourceId));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadProfile(response);
    }

    private static async Task<DatabaseConnectionProfileResponse> SetSecret(
        HttpClient client,
        DatabaseConnectionProfileResponse profile,
        string password)
    {
        using var response = await client.PostAsJsonAsync(
            $"/api/admin/database-connection-profiles/{profile.Id}/secret",
            new { password, concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadProfile(response);
    }

    private static async Task<DatabaseConnectionProfileResponse> GetProfile(HttpClient client, long id)
    {
        using var response = await client.GetAsync($"/api/admin/database-connection-profiles/{id}");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadProfile(response);
    }

    private static async Task<DatabaseConnectionProfileResponse> ReadProfile(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<DatabaseConnectionProfileResponse>(JsonOptions)
        ?? throw new InvalidOperationException("Profile response was empty.");

    private static async Task<long> CreateSource(DatabaseDiscoveryWebApplicationFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var systemId = await db.Systems.Select(item => item.Id).FirstAsync();
        var user = await db.Users.FirstAsync(item => item.AccessLevel == AccessLevel.Administrator);
        var now = DateTimeOffset.UtcNow;
        var source = new DatabaseSource
        {
            SystemId = systemId,
            Name = $"DBDISC-B02-{Guid.NewGuid():N}",
            Engine = "Oracle",
            CreatedAt = now,
            CreatedByUserId = user.Id,
            CreatedByName = user.DisplayName,
            UpdatedAt = now,
            Version = 1,
        };
        db.DatabaseSources.Add(source);
        await db.SaveChangesAsync();
        return source.Id;
    }

    private static async Task<long> CreateUser(DatabaseDiscoveryWebApplicationFactory factory, AccessLevel accessLevel)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User
        {
            DisplayName = $"DBDISC B02 {accessLevel} {Guid.NewGuid():N}",
            IsActive = true,
            AccessLevel = accessLevel,
            CreatedAt = now,
            UpdatedAt = now,
            Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
