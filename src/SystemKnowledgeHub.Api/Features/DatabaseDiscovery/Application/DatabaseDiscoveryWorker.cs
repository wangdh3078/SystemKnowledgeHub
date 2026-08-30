using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public sealed class DatabaseDiscoveryWorker(
    IServiceScopeFactory scopeFactory,
    DatabaseDiscoveryWorkerReadiness readiness,
    IOptions<DatabaseDiscoveryOptions> options,
    ILogger<DatabaseDiscoveryWorker> logger) : BackgroundService
{
    private readonly DatabaseDiscoveryOptions settings = Validate(options.Value);
    private readonly string ownerId = $"{Environment.MachineName}:{Environment.ProcessId}:{Guid.NewGuid():N}";

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await readiness.WaitAsync(stoppingToken);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await using var scope = scopeFactory.CreateAsyncScope();
                var processor = scope.ServiceProvider.GetRequiredService<DatabaseDiscoveryRunProcessor>();
                await processor.RecoverExpiredRuns(stoppingToken);
                var claim = await processor.ClaimNext(ownerId, stoppingToken);
                if (claim is null)
                {
                    await Task.Delay(settings.QueuePollIntervalMilliseconds, stoppingToken);
                    continue;
                }
                await processor.Process(claim, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch
            {
                logger.LogError("Database Discovery worker loop failed; queued SQLite rows remain authoritative.");
                await Task.Delay(settings.QueuePollIntervalMilliseconds, stoppingToken);
            }
        }
    }

    private static DatabaseDiscoveryOptions Validate(DatabaseDiscoveryOptions options)
    {
        options.Validate();
        return options;
    }
}

public sealed class DatabaseDiscoveryWorkerReadiness
{
    private readonly TaskCompletionSource ready;

    public DatabaseDiscoveryWorkerReadiness() : this(true)
    {
    }

    public DatabaseDiscoveryWorkerReadiness(bool initiallyReady)
    {
        ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (initiallyReady) ready.TrySetResult();
    }

    public Task WaitAsync(CancellationToken cancellationToken) => ready.Task.WaitAsync(cancellationToken);

    public void SignalReady() => ready.TrySetResult();

}

public sealed record ClaimedDatabaseDiscoveryRun(long RunId, string LeaseToken);

public sealed class DatabaseDiscoveryRunProcessor(
    KnowledgeHubDbContext dbContext,
    IDatabaseConnectionSecretStore secretStore,
    IEnumerable<IDatabaseDiscoveryProvider> providers,
    CanonicalSnapshotService canonical,
    DatabaseDiscoveryDiffService diffService,
    IOptions<DatabaseDiscoveryOptions> options,
    IServiceScopeFactory scopeFactory,
    ILogger<DatabaseDiscoveryRunProcessor> logger)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();
    private readonly DatabaseDiscoveryOptions settings = Validate(options.Value);

    public async Task RecoverExpiredRuns(CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var expired = await dbContext.DatabaseDiscoveryRuns
            .FromSqlInterpolated($"""
                SELECT *
                FROM database_discovery_runs
                WHERE status = 'Running'
                  AND lease_expires_at IS NOT NULL
                  AND julianday(lease_expires_at) <= julianday({now})
                ORDER BY id
                """)
            .ToArrayAsync(cancellationToken);
        foreach (var run in expired)
        {
            var cancelled = run.CancellationRequestedAt is not null;
            run.Status = cancelled ? DatabaseDiscoveryRunStatus.Cancelled : DatabaseDiscoveryRunStatus.Failed;
            run.CompletedAt = now;
            run.ErrorCode = cancelled ? "Cancelled" : "RunInterrupted";
            run.ErrorSummary = cancelled ? "发现运行已取消。" : "发现运行因执行实例中断而失败，请重新触发。";
            ClearLease(run);
            run.Version++;
            AddResultAudit(run, cancelled ? "Cancelled" : "RunInterrupted", now);
        }
        if (expired.Length > 0) await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ClaimedDatabaseDiscoveryRun?> ClaimNext(string ownerId, CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var run = await dbContext.DatabaseDiscoveryRuns
            .Where(item => item.Status == DatabaseDiscoveryRunStatus.Queued)
            .OrderBy(item => item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (run is null)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        if (run.CancellationRequestedAt is not null)
        {
            run.Status = DatabaseDiscoveryRunStatus.Cancelled;
            run.CompletedAt = now;
            run.ErrorCode = "Cancelled";
            run.ErrorSummary = "发现运行已取消。";
            run.Version++;
            AddResultAudit(run, "Cancelled", now);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return null;
        }

        var leaseToken = Guid.NewGuid().ToString("N");
        run.Status = DatabaseDiscoveryRunStatus.Running;
        run.StartedAt = now;
        run.LeaseOwnerId = ownerId;
        run.LeaseToken = leaseToken;
        run.LeaseHeartbeatAt = now;
        run.LeaseExpiresAt = now.AddSeconds(settings.LeaseDurationSeconds);
        run.Version++;
        var profile = await dbContext.DatabaseConnectionProfiles.SingleAsync(item => item.Id == run.ProfileId, cancellationToken);
        profile.LastDiscoveryAt = now;
        profile.UpdatedAt = now;
        profile.Version++;
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new(run.Id, leaseToken);
    }

    public async Task Process(ClaimedDatabaseDiscoveryRun claim, CancellationToken stoppingToken)
    {
        var work = await LoadWork(claim, stoppingToken);
        if (work.FailureCode is not null)
        {
            await Fail(claim, work.FailureCode, work.FailureSummary!, stoppingToken);
            return;
        }

        var connection = work.Connection!;
        var matchingProviders = providers.Where(item => item.ProviderType == connection.ProviderType).Take(2).ToArray();
        if (matchingProviders.Length != 1)
        {
            await Fail(claim, "ProviderUnavailable", "当前 Provider 尚未提供发现实现。", stoppingToken);
            return;
        }
        var provider = matchingProviders[0];

        using var operation = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        operation.CancelAfter(TimeSpan.FromSeconds(settings.OverallTimeoutSeconds));
        using var monitorStop = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        var monitor = MonitorLease(claim, operation, monitorStop.Token);
        try
        {
            var capabilities = await provider.DetectCapabilitiesAsync(connection, operation.Token);
            var request = new DatabaseDiscoveryRequest(work.IncludedSchemas!, settings.Limits);
            var providerSnapshot = await provider.DiscoverAsync(connection, request, capabilities, operation.Token);
            var snapshot = providerSnapshot with { Capabilities = capabilities.Capabilities };
            var prepared = canonical.Prepare(snapshot, connection, settings.Limits);
            if (!prepared.Succeeded)
            {
                await Fail(claim, prepared.ErrorCode!, prepared.ErrorSummary!, stoppingToken);
                return;
            }

            var baselineRow = await dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
                .Where(item => item.ProfileId == work.ProfileId && item.ScopeFingerprint == prepared.ScopeFingerprint)
                .OrderByDescending(item => item.Id)
                .Select(item => new { item.Id, item.CanonicalContentJson })
                .FirstOrDefaultAsync(stoppingToken);
            var baseline = baselineRow is null ? null : canonical.Deserialize(baselineRow.CanonicalContentJson);
            var difference = diffService.Compare(baseline, prepared.Snapshot!);
            if (!difference.Succeeded)
            {
                await Fail(claim, difference.ErrorCode!, difference.ErrorSummary!, stoppingToken);
                return;
            }

            try
            {
                var finalized = await FinalizeSucceeded(
                    claim, work, prepared, baselineRow?.Id, difference, stoppingToken);
                if (!finalized)
                {
                    logger.LogWarning("Database Discovery Run {RunId} could not finalize against its current lease and baseline.", claim.RunId);
                    var cancellationRequested = await IsCancellationRequested(claim, CancellationToken.None);
                    await FailInNewScope(
                        claim,
                        cancellationRequested ? "Cancelled" : "ConcurrencyConflict",
                        cancellationRequested ? "发现运行已取消。" : "连接配置、租约或兼容基线已变化。",
                        CancellationToken.None);
                }
            }
            catch
            {
                await FailInNewScope(claim, "SnapshotPersistenceFailed", "发现快照持久化失败。", stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            if (stoppingToken.IsCancellationRequested) return;
            var cancellationRequested = await IsCancellationRequested(claim, CancellationToken.None);
            await FailInNewScope(
                claim,
                cancellationRequested ? "Cancelled" : "Timeout",
                cancellationRequested ? "发现运行已取消。" : "发现运行超时。",
                CancellationToken.None);
        }
        catch (DatabaseDiscoveryProviderException exception)
        {
            await FailInNewScope(
                claim,
                exception.ErrorCode,
                exception.SafeSummary,
                stoppingToken,
                exception.VendorCode);
        }
        catch
        {
            await FailInNewScope(claim, "MetadataQueryFailed", "读取数据库结构元数据失败。", stoppingToken);
        }
        finally
        {
            monitorStop.Cancel();
            try { await monitor; } catch (OperationCanceledException) { }
        }
    }

    private async Task<DiscoveryWork> LoadWork(ClaimedDatabaseDiscoveryRun claim, CancellationToken cancellationToken)
    {
        var run = await dbContext.DatabaseDiscoveryRuns.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == claim.RunId
                && item.Status == DatabaseDiscoveryRunStatus.Running
                && item.LeaseToken == claim.LeaseToken, cancellationToken);
        if (run is null) return DiscoveryWork.Fail("RunInterrupted", "发现运行的执行租约已失效。");
        var profile = await dbContext.DatabaseConnectionProfiles.AsNoTracking()
            .Include(item => item.Secret)
            .SingleOrDefaultAsync(item => item.Id == run.ProfileId, cancellationToken);
        if (profile is null || !profile.IsEnabled
            || !await dbContext.DatabaseSources.AnyAsync(item => item.Id == profile.DatabaseSourceId, cancellationToken))
            return DiscoveryWork.Fail("ConcurrencyConflict", "连接配置或数据库来源已变化。");
        if (profile.ConfigurationRevision != run.ProfileConfigurationRevision
            || profile.Secret?.Version != run.SecretVersion)
            return DiscoveryWork.Fail("ConcurrencyConflict", "连接配置或密码版本已变化。");
        var resolved = secretStore.Resolve(profile.Id, profile.Secret);
        if (resolved.Failure == DatabaseConnectionSecretFailure.Missing)
            return DiscoveryWork.Fail("SecretMissing", "尚未设置数据库连接密码。");
        if (resolved.Failure == DatabaseConnectionSecretFailure.Unavailable)
            return DiscoveryWork.Fail("SecretUnavailable", "数据库连接密码无法解密，请重新设置。");
        var schemas = JsonSerializer.Deserialize<string[]>(run.RequestedIncludedSchemasJson, JsonOptions) ?? [];
        var connection = new DatabaseDiscoveryConnectionContext(
            profile.Id, profile.ConfigurationRevision, profile.Secret.Version, profile.ProviderType,
            profile.Host, profile.Port, profile.DatabaseName, profile.ServiceName, profile.Username,
            resolved.Plaintext!, schemas);
        return new(run.ProfileId, run.RequestedByUserId, run.RequestedByDisplayName, connection, schemas, null, null);
    }

    private async Task MonitorLease(
        ClaimedDatabaseDiscoveryRun claim,
        CancellationTokenSource operation,
        CancellationToken stop)
    {
        while (!stop.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(settings.HeartbeatIntervalSeconds), stop);
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var now = DateTimeOffset.UtcNow;
            var state = await db.DatabaseDiscoveryRuns.AsNoTracking()
                .Where(item => item.Id == claim.RunId && item.Status == DatabaseDiscoveryRunStatus.Running
                    && item.LeaseToken == claim.LeaseToken)
                .Select(item => new { item.CancellationRequestedAt })
                .SingleOrDefaultAsync(stop);
            if (state is null || state.CancellationRequestedAt is not null)
            {
                operation.Cancel();
                return;
            }
            var updated = await db.DatabaseDiscoveryRuns
                .Where(item => item.Id == claim.RunId && item.Status == DatabaseDiscoveryRunStatus.Running
                    && item.LeaseToken == claim.LeaseToken)
                .ExecuteUpdateAsync(setters => setters
                    .SetProperty(item => item.LeaseHeartbeatAt, now)
                    .SetProperty(item => item.LeaseExpiresAt, now.AddSeconds(settings.LeaseDurationSeconds))
                    .SetProperty(item => item.Version, item => item.Version + 1), stop);
            if (updated != 1)
            {
                operation.Cancel();
                return;
            }
        }
    }

    private async Task<bool> FinalizeSucceeded(
        ClaimedDatabaseDiscoveryRun claim,
        DiscoveryWork work,
        CanonicalSnapshotPreparation prepared,
        long? expectedBaseSnapshotId,
        PreparedDatabaseDiscoveryDifference preparedDifference,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var run = await dbContext.DatabaseDiscoveryRuns.Include(item => item.Profile).ThenInclude(item => item.Secret)
            .SingleOrDefaultAsync(item => item.Id == claim.RunId, cancellationToken);
        if (run is null || run.Status != DatabaseDiscoveryRunStatus.Running
            || run.LeaseToken != claim.LeaseToken || run.LeaseExpiresAt <= now
            || run.CancellationRequestedAt is not null
            || run.Profile.ConfigurationRevision != run.ProfileConfigurationRevision
            || run.Profile.Secret?.Version != run.SecretVersion)
            return false;
        var actualBaseSnapshotId = await dbContext.DatabaseDiscoverySnapshots
            .Where(item => item.ProfileId == run.ProfileId && item.ScopeFingerprint == prepared.ScopeFingerprint)
            .OrderByDescending(item => item.Id).Select(item => (long?)item.Id)
            .FirstOrDefaultAsync(cancellationToken);
        if (actualBaseSnapshotId != expectedBaseSnapshotId) return false;

        var generation = await dbContext.DatabaseDiscoveryScopeGenerations
            .SingleOrDefaultAsync(item => item.ProfileId == run.ProfileId
                && item.ScopeFingerprint == prepared.ScopeFingerprint, cancellationToken);
        if (generation is null)
        {
            generation = new DatabaseDiscoveryScopeGeneration
            {
                ProfileId = run.ProfileId,
                ScopeFingerprint = prepared.ScopeFingerprint!,
                CreatedAt = now,
            };
            dbContext.DatabaseDiscoveryScopeGenerations.Add(generation);
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        var snapshot = new DatabaseDiscoverySnapshot
        {
            RunId = run.Id,
            ProfileId = run.ProfileId,
            CapturedAt = prepared.Snapshot!.CapturedAt,
            FormatVersion = prepared.Snapshot.FormatVersion,
            IdentityAlgorithmVersion = prepared.Snapshot.IdentityAlgorithmVersion,
            ScopeGenerationId = generation.Id,
            ScopeFingerprint = prepared.ScopeFingerprint!,
            Completeness = DatabaseDiscoveryCompleteness.Complete,
            CanonicalContentJson = prepared.CanonicalJson!,
            ContentSha256 = prepared.ContentSha256!,
            CountsJson = prepared.CountsJson!,
        };
        dbContext.DatabaseDiscoverySnapshots.Add(snapshot);
        await dbContext.SaveChangesAsync(cancellationToken);

        var difference = new DatabaseDiscoveryDifference
        {
            ProfileId = run.ProfileId,
            BaseSnapshotId = expectedBaseSnapshotId,
            TargetSnapshotId = snapshot.Id,
            ScopeGenerationId = generation.Id,
            AlgorithmVersion = 1,
            CreatedAt = now,
            SummaryCountsJson = preparedDifference.SummaryCountsJson,
            ContentSha256 = preparedDifference.ContentSha256,
        };
        foreach (var entry in preparedDifference.Entries) difference.Entries.Add(entry);
        dbContext.DatabaseDiscoveryDifferences.Add(difference);

        run.BaseSnapshotId = expectedBaseSnapshotId;
        run.ScopeGenerationId = generation.Id;
        run.Status = DatabaseDiscoveryRunStatus.Succeeded;
        run.CompletedAt = now;
        run.ProviderVersion = prepared.Snapshot.ProviderVersion;
        run.ScopeFingerprint = prepared.ScopeFingerprint;
        run.CapabilitySnapshotJson = JsonSerializer.Serialize(prepared.Snapshot.Capabilities, JsonOptions);
        run.ObjectCountsJson = prepared.CountsJson;
        run.ErrorCode = null;
        run.ErrorSummary = null;
        run.SafeErrorMetadataJson = null;
        ClearLease(run);
        run.Version++;
        run.Profile.LastDiscoveryAt = now;
        run.Profile.LastSuccessfulDiscoveryAt = now;
        run.Profile.UpdatedAt = now;
        run.Profile.Version++;
        AddResultAudit(run, null, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return true;
    }

    private async Task Fail(
        ClaimedDatabaseDiscoveryRun claim,
        string errorCode,
        string summary,
        CancellationToken cancellationToken)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var run = await dbContext.DatabaseDiscoveryRuns.Include(item => item.Profile)
            .SingleOrDefaultAsync(item => item.Id == claim.RunId, cancellationToken);
        if (run is null || run.Status != DatabaseDiscoveryRunStatus.Running
            || run.LeaseToken != claim.LeaseToken || run.LeaseExpiresAt <= now)
            return;
        var cancelled = run.CancellationRequestedAt is not null || errorCode == "Cancelled";
        var safeErrorCode = DatabaseDiscoveryFailureSafety.SafeCode(errorCode);
        run.Status = cancelled ? DatabaseDiscoveryRunStatus.Cancelled : DatabaseDiscoveryRunStatus.Failed;
        run.CompletedAt = now;
        run.ErrorCode = cancelled ? "Cancelled" : safeErrorCode;
        run.ErrorSummary = cancelled
            ? "发现运行已取消。"
            : DatabaseDiscoveryFailureSafety.SummaryFor(safeErrorCode);
        run.SafeErrorMetadataJson = null;
        ClearLease(run);
        run.Version++;
        run.Profile.LastDiscoveryAt = now;
        run.Profile.UpdatedAt = now;
        run.Profile.Version++;
        AddResultAudit(run, run.ErrorCode, now);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogWarning("Database Discovery Run {RunId} ended with {ErrorCode}.", run.Id, run.ErrorCode);
    }

    private async Task FailInNewScope(
        ClaimedDatabaseDiscoveryRun claim,
        string errorCode,
        string summary,
        CancellationToken cancellationToken,
        string? vendorCode = null)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var terminal = scope.ServiceProvider.GetRequiredService<DatabaseDiscoveryTerminalWriter>();
        await terminal.Fail(claim, errorCode, summary, cancellationToken, vendorCode);
    }

    private async Task<bool> IsCancellationRequested(ClaimedDatabaseDiscoveryRun claim, CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        return await db.DatabaseDiscoveryRuns.AsNoTracking().AnyAsync(item => item.Id == claim.RunId
            && item.LeaseToken == claim.LeaseToken && item.CancellationRequestedAt != null, cancellationToken);
    }

    private void AddResultAudit(DatabaseDiscoveryRun run, string? errorCode, DateTimeOffset now) =>
        dbContext.DatabaseConnectionAuditEvents.Add(new DatabaseConnectionAuditEvent
        {
            ProfileId = run.ProfileId,
            Action = DatabaseConnectionAuditAction.DiscoveryRunResult,
            Outcome = errorCode is null ? DatabaseConnectionAuditOutcome.Succeeded : DatabaseConnectionAuditOutcome.Failed,
            ErrorCode = errorCode,
            ActorUserId = run.RequestedByUserId,
            ActorDisplayName = run.RequestedByDisplayName,
            OccurredAt = now,
        });

    private static void ClearLease(DatabaseDiscoveryRun run)
    {
        run.LeaseOwnerId = null;
        run.LeaseToken = null;
        run.LeaseHeartbeatAt = null;
        run.LeaseExpiresAt = null;
    }

    private static DatabaseDiscoveryOptions Validate(DatabaseDiscoveryOptions options)
    {
        options.Validate();
        return options;
    }

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record DiscoveryWork(
        long ProfileId,
        long RequestedByUserId,
        string RequestedByDisplayName,
        DatabaseDiscoveryConnectionContext? Connection,
        IReadOnlyList<string>? IncludedSchemas,
        string? FailureCode,
        string? FailureSummary)
    {
        public static DiscoveryWork Fail(string code, string summary) => new(0, 0, string.Empty, null, null, code, summary);
    }
}

internal static class DatabaseDiscoveryFailureSafety
{
    public static string SafeCode(string value) => value switch
    {
        "ConnectionFailed" or "AuthenticationFailed" or "InsufficientPrivilege"
            or "UnsupportedDatabaseVersion" or "MetadataQueryFailed" or "Timeout"
            or "Cancelled" or "ProviderUnavailable" or "SnapshotPersistenceFailed"
            or "SecretMissing" or "SecretUnavailable" or "LimitExceeded"
            or "BaselineIncompatible" or "UnresolvedForeignKeyReference"
            or "ConcurrencyConflict" or "RunInterrupted" => value,
        _ => "MetadataQueryFailed",
    };

    public static string SummaryFor(string safeCode) => safeCode switch
    {
        "ConnectionFailed" => "无法连接到数据库。",
        "AuthenticationFailed" => "数据库用户名或密码验证失败。",
        "InsufficientPrivilege" => "数据库账号缺少发现所需的目录元数据权限。",
        "UnsupportedDatabaseVersion" => "当前数据库版本不受支持。",
        "Timeout" => "发现运行超时。",
        "Cancelled" => "发现运行已取消。",
        "ProviderUnavailable" => "当前 Provider 尚未提供发现实现。",
        "SnapshotPersistenceFailed" => "发现快照持久化失败。",
        "SecretMissing" => "尚未设置数据库连接密码。",
        "SecretUnavailable" => "数据库连接密码无法解密，请重新设置。",
        "LimitExceeded" => "发现结果超过配置的安全限制。",
        "BaselineIncompatible" => "连接配置或兼容基线已变化。",
        "UnresolvedForeignKeyReference" => "无法完整解析外键引用。",
        "ConcurrencyConflict" => "连接配置、租约或兼容基线已变化。",
        "RunInterrupted" => "发现运行因执行实例中断而失败，请重新触发。",
        _ => "读取数据库结构元数据失败。",
    };

    public static string? SafeVendorCode(string? value)
    {
        if (value is { Length: 9 }
            && value[3] == '-'
            && value.AsSpan(0, 3).IndexOfAnyExceptInRange('A', 'Z') < 0
            && value.AsSpan(4).IndexOfAnyExceptInRange('0', '9') < 0)
            return value;
        if (value is { Length: 14 }
            && value.StartsWith("SQLSTATE-", StringComparison.Ordinal)
            && value.AsSpan(9).IndexOfAnyExcept("ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789") < 0)
            return value;
        return null;
    }
}

public sealed class DatabaseDiscoveryTerminalWriter(
    KnowledgeHubDbContext dbContext,
    ILogger<DatabaseDiscoveryTerminalWriter> logger)
{
    public async Task Fail(
        ClaimedDatabaseDiscoveryRun claim,
        string errorCode,
        string summary,
        CancellationToken cancellationToken,
        string? vendorCode = null)
    {
        var now = DateTimeOffset.UtcNow;
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var run = await dbContext.DatabaseDiscoveryRuns.Include(item => item.Profile)
            .SingleOrDefaultAsync(item => item.Id == claim.RunId, cancellationToken);
        if (run is null || run.Status != DatabaseDiscoveryRunStatus.Running
            || run.LeaseToken != claim.LeaseToken || run.LeaseExpiresAt <= now)
            return;
        var cancelled = run.CancellationRequestedAt is not null || errorCode == "Cancelled";
        var safeErrorCode = DatabaseDiscoveryFailureSafety.SafeCode(errorCode);
        run.Status = cancelled ? DatabaseDiscoveryRunStatus.Cancelled : DatabaseDiscoveryRunStatus.Failed;
        run.CompletedAt = now;
        run.ErrorCode = cancelled ? "Cancelled" : safeErrorCode;
        run.ErrorSummary = cancelled
            ? "发现运行已取消。"
            : DatabaseDiscoveryFailureSafety.SummaryFor(safeErrorCode);
        var safeVendorCode = DatabaseDiscoveryFailureSafety.SafeVendorCode(vendorCode);
        run.SafeErrorMetadataJson = cancelled || safeVendorCode is null
            ? null
            : JsonSerializer.Serialize(new { vendorCode = safeVendorCode });
        run.LeaseOwnerId = null;
        run.LeaseToken = null;
        run.LeaseHeartbeatAt = null;
        run.LeaseExpiresAt = null;
        run.Version++;
        run.Profile.LastDiscoveryAt = now;
        run.Profile.UpdatedAt = now;
        run.Profile.Version++;
        dbContext.DatabaseConnectionAuditEvents.Add(new DatabaseConnectionAuditEvent
        {
            ProfileId = run.ProfileId,
            Action = DatabaseConnectionAuditAction.DiscoveryRunResult,
            Outcome = DatabaseConnectionAuditOutcome.Failed,
            ErrorCode = run.ErrorCode,
            ActorUserId = run.RequestedByUserId,
            ActorDisplayName = run.RequestedByDisplayName,
            OccurredAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        logger.LogWarning("Database Discovery Run {RunId} ended with {ErrorCode}.", run.Id, run.ErrorCode);
    }

}
