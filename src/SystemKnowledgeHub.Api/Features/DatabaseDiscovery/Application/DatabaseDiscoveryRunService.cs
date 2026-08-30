using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public sealed class DatabaseDiscoveryRunService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec tokenCodec,
    CanonicalSnapshotService canonical,
    DatabaseDiscoveryDiffService diffService)
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public async Task<DatabaseDiscoveryOperationResult<DatabaseDiscoveryRunResponse>> Trigger(
        long profileId,
        string? concurrencyToken,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(profileId)) errors["profileId"] = ["连接配置必须是有效 ID。"];
        if (!tokenCodec.TryDecode(concurrencyToken, out var expectedVersion))
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0) return Validation<DatabaseDiscoveryRunResponse>(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var profile = await dbContext.DatabaseConnectionProfiles
            .Include(item => item.Secret)
            .SingleOrDefaultAsync(item => item.Id == profileId, cancellationToken);
        if (profile is null) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.NotFound);
        if (profile.Version != expectedVersion) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.ConcurrencyConflict);
        if (!profile.IsEnabled) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.Disabled);
        if (!await dbContext.DatabaseSources.AnyAsync(item => item.Id == profile.DatabaseSourceId, cancellationToken))
            return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.ReferenceInvalid);
        if (profile.Secret?.ProtectedPayload is null) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.SecretMissing);
        if (await HasActiveRun(profile.Id, cancellationToken))
            return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.DiscoveryAlreadyRunning);

        var now = DateTimeOffset.UtcNow;
        var run = new DatabaseDiscoveryRun
        {
            ProfileId = profile.Id,
            ProfileConfigurationRevision = profile.ConfigurationRevision,
            SecretVersion = profile.Secret.Version,
            QueuedAt = now,
            Status = DatabaseDiscoveryRunStatus.Queued,
            ProviderType = profile.ProviderType,
            RequestedIncludedSchemasJson = profile.IncludedSchemasJson,
            RequestedProviderSpecificOptionsJson = profile.ProviderSpecificOptionsJson,
            RequestedByUserId = actor.Creator.UserId,
            RequestedByDisplayName = actor.Creator.DisplayName,
            Version = 1,
        };
        dbContext.DatabaseDiscoveryRuns.Add(run);
        dbContext.DatabaseConnectionAuditEvents.Add(new DatabaseConnectionAuditEvent
        {
            ProfileId = profile.Id,
            Action = DatabaseConnectionAuditAction.DiscoveryRunTriggered,
            Outcome = DatabaseConnectionAuditOutcome.Succeeded,
            ActorUserId = actor.Creator.UserId,
            ActorDisplayName = actor.Creator.DisplayName,
            OccurredAt = now,
        });
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.DiscoveryAlreadyRunning);
        }

        return Success(await ToResponse(run, profile, cancellationToken));
    }

    public async Task<DatabaseDiscoveryOperationResult<DatabaseDiscoveryRunResponse>> Cancel(
        long runId,
        string? concurrencyToken,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(runId)) errors["runId"] = ["发现运行必须是有效 ID。"];
        if (!tokenCodec.TryDecode(concurrencyToken, out var expectedVersion))
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0) return Validation<DatabaseDiscoveryRunResponse>(errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var run = await dbContext.DatabaseDiscoveryRuns
            .Include(item => item.Profile)
            .SingleOrDefaultAsync(item => item.Id == runId, cancellationToken);
        if (run is null) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.NotFound);
        if (run.Version != expectedVersion) return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.ConcurrencyConflict);
        if (run.Status is DatabaseDiscoveryRunStatus.Succeeded or DatabaseDiscoveryRunStatus.Failed or DatabaseDiscoveryRunStatus.Cancelled)
            return Failure<DatabaseDiscoveryRunResponse>(DatabaseDiscoveryFailure.TerminalRun);

        var now = DateTimeOffset.UtcNow;
        run.CancellationRequestedAt = now;
        run.CancellationRequestedByUserId = actor.Creator.UserId;
        run.Version++;
        if (run.Status == DatabaseDiscoveryRunStatus.Queued)
        {
            run.Status = DatabaseDiscoveryRunStatus.Cancelled;
            run.CompletedAt = now;
            run.ErrorCode = "Cancelled";
            run.ErrorSummary = "发现运行已取消。";
        }
        dbContext.DatabaseConnectionAuditEvents.Add(new DatabaseConnectionAuditEvent
        {
            ProfileId = run.ProfileId,
            Action = DatabaseConnectionAuditAction.DiscoveryRunCancellationRequested,
            Outcome = DatabaseConnectionAuditOutcome.Succeeded,
            ErrorCode = run.Status == DatabaseDiscoveryRunStatus.Cancelled ? "Cancelled" : null,
            ActorUserId = actor.Creator.UserId,
            ActorDisplayName = actor.Creator.DisplayName,
            OccurredAt = now,
        });
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return Success(await ToResponse(run, run.Profile, cancellationToken));
    }

    public async Task<DatabaseDiscoveryRunPageResponse> List(
        long? profileId,
        int? page,
        int? pageSize,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        var normalizedPage = Math.Clamp(page ?? 1, 1, 1_000_000);
        var normalizedPageSize = Math.Clamp(pageSize ?? 20, 1, 100);
        var query = AccessibleRuns(includeHistory);
        if (profileId is not null) query = query.Where(item => item.Run.ProfileId == profileId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(item => item.Run.Id)
            .Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
            .ToArrayAsync(cancellationToken);
        var responses = new List<DatabaseDiscoveryRunResponse>(rows.Length);
        foreach (var row in rows) responses.Add(await ToResponse(row.Run, row.Profile, cancellationToken));
        return new(responses, normalizedPage, normalizedPageSize, total);
    }

    public async Task<DatabaseDiscoveryRunResponse?> GetRun(long id, bool includeHistory, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return null;
        var row = await AccessibleRuns(includeHistory).SingleOrDefaultAsync(item => item.Run.Id == id, cancellationToken);
        return row is null ? null : await ToResponse(row.Run, row.Profile, cancellationToken);
    }

    public async Task<DatabaseDiscoverySnapshotResponse?> GetSnapshot(long id, bool includeHistory, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return null;
        var accessibleRunIds = AccessibleRuns(includeHistory).Select(item => item.Run.Id);
        var snapshot = await dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && accessibleRunIds.Contains(item.RunId), cancellationToken);
        if (snapshot is null) return null;
        using var document = JsonDocument.Parse(snapshot.CanonicalContentJson);
        return new(
            snapshot.Id, snapshot.RunId, snapshot.ProfileId, snapshot.CapturedAt,
            snapshot.FormatVersion, snapshot.IdentityAlgorithmVersion, snapshot.ScopeGenerationId,
            snapshot.ScopeFingerprint, snapshot.Completeness, snapshot.ContentSha256,
            DeserializeCounts(snapshot.CountsJson)!, document.RootElement.Clone());
    }

    public async Task<DatabaseDiscoveryDifferenceResponse?> GetDifference(long id, bool includeHistory, CancellationToken cancellationToken)
    {
        if (!ApiIdParser.IsSafePositive(id)) return null;
        var accessibleSnapshotIds = dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .Where(snapshot => AccessibleRuns(includeHistory).Select(item => item.Run.Id).Contains(snapshot.RunId))
            .Select(snapshot => snapshot.Id);
        var difference = await dbContext.DatabaseDiscoveryDifferences.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id && accessibleSnapshotIds.Contains(item.TargetSnapshotId), cancellationToken);
        return difference is null ? null : new(
            difference.Id, difference.ProfileId, difference.BaseSnapshotId, difference.TargetSnapshotId,
            difference.ScopeGenerationId, difference.AlgorithmVersion, difference.CreatedAt,
            JsonSerializer.Deserialize<DatabaseDiscoveryDifferenceCounts>(difference.SummaryCountsJson, JsonOptions)!,
            difference.ContentSha256);
    }

    public async Task<DatabaseDiscoveryDifferenceEntryPageResponse?> GetDifferenceEntries(
        long id,
        DatabaseDiscoveryDifferenceState state,
        int? page,
        int? pageSize,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        var difference = await GetDifference(id, includeHistory, cancellationToken);
        if (difference is null) return null;
        var normalizedPage = Math.Clamp(page ?? 1, 1, 1_000_000);
        var normalizedPageSize = Math.Clamp(pageSize ?? 50, 1, 200);
        if (state != DatabaseDiscoveryDifferenceState.Unchanged)
        {
            var query = dbContext.DatabaseDiscoveryDifferenceEntries.AsNoTracking()
                .Where(item => item.DifferenceId == id && item.State == state);
            var total = await query.CountAsync(cancellationToken);
            var rows = await query.OrderBy(item => item.EntityKind).ThenBy(item => item.LogicalIdentity)
                .Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
                .ToArrayAsync(cancellationToken);
            return new(rows.Select(ToResponse).ToArray(), normalizedPage, normalizedPageSize, total);
        }

        if (difference.BaseSnapshotId is null)
            return new([], normalizedPage, normalizedPageSize, 0);
        var snapshots = await dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .Where(item => item.Id == difference.BaseSnapshotId || item.Id == difference.TargetSnapshotId)
            .ToArrayAsync(cancellationToken);
        var baseline = canonical.Deserialize(snapshots.Single(item => item.Id == difference.BaseSnapshotId).CanonicalContentJson);
        var target = canonical.Deserialize(snapshots.Single(item => item.Id == difference.TargetSnapshotId).CanonicalContentJson);
        var unchanged = diffService.DeriveUnchanged(baseline, target);
        var items = unchanged.Skip((normalizedPage - 1) * normalizedPageSize).Take(normalizedPageSize)
            .Select(item => new DatabaseDiscoveryDifferenceEntryResponse(
                null, item.EntityKind, item.LogicalIdentity, item.ParentLogicalIdentity, item.DisplayName,
                DatabaseDiscoveryDifferenceState.Unchanged, Parse(item.ContentJson), Parse(item.ContentJson)))
            .ToArray();
        return new(items, normalizedPage, normalizedPageSize, unchanged.Count);
    }

    public Task<bool> HasActiveRun(long profileId, CancellationToken cancellationToken) =>
        dbContext.DatabaseDiscoveryRuns.AnyAsync(item => item.ProfileId == profileId
            && (item.Status == DatabaseDiscoveryRunStatus.Queued || item.Status == DatabaseDiscoveryRunStatus.Running), cancellationToken);

    private IQueryable<AccessibleRunRow> AccessibleRuns(bool includeHistory)
    {
        var sources = includeHistory ? dbContext.DatabaseSources.IgnoreQueryFilters() : dbContext.DatabaseSources;
        return from run in dbContext.DatabaseDiscoveryRuns.AsNoTracking()
               join profile in dbContext.DatabaseConnectionProfiles.AsNoTracking() on run.ProfileId equals profile.Id
               join source in sources.AsNoTracking() on profile.DatabaseSourceId equals source.Id
               where includeHistory || profile.IsEnabled
               select new AccessibleRunRow { Run = run, Profile = profile };
    }

    private async Task<DatabaseDiscoveryRunResponse> ToResponse(
        DatabaseDiscoveryRun run,
        DatabaseConnectionProfile profile,
        CancellationToken cancellationToken)
    {
        var snapshot = await dbContext.DatabaseDiscoverySnapshots.AsNoTracking()
            .Where(item => item.RunId == run.Id)
            .Select(item => new { item.Id, item.CountsJson })
            .SingleOrDefaultAsync(cancellationToken);
        var differenceId = snapshot is null ? null : await dbContext.DatabaseDiscoveryDifferences.AsNoTracking()
            .Where(item => item.TargetSnapshotId == snapshot.Id)
            .Select(item => (long?)item.Id)
            .SingleOrDefaultAsync(cancellationToken);
        return new(
            run.Id, run.ProfileId, profile.DatabaseSourceId, profile.Name, run.ProviderType, run.Status,
            run.BaseSnapshotId, run.ScopeGenerationId,
            snapshot?.Id, differenceId, run.QueuedAt, run.StartedAt, run.CompletedAt,
            run.CancellationRequestedAt, run.ProviderVersion, run.ScopeFingerprint,
            DeserializeCapabilities(run.CapabilitySnapshotJson), DeserializeCounts(run.ObjectCountsJson ?? snapshot?.CountsJson),
            run.ErrorCode, run.ErrorSummary, tokenCodec.Encode(run.Version));
    }

    private static DatabaseDiscoveryDifferenceEntryResponse ToResponse(DatabaseDiscoveryDifferenceEntry item) => new(
        item.Id, item.EntityKind, item.LogicalIdentity, item.ParentLogicalIdentity, item.DisplayName, item.State,
        Parse(item.BeforeJson), Parse(item.AfterJson));

    private static JsonElement? Parse(string? json)
    {
        if (json is null) return null;
        using var document = JsonDocument.Parse(json);
        return document.RootElement.Clone();
    }

    private static IReadOnlyList<CanonicalCapability> DeserializeCapabilities(string? json) =>
        json is null ? [] : JsonSerializer.Deserialize<CanonicalCapability[]>(json, JsonOptions) ?? [];

    private static CanonicalSnapshotCounts? DeserializeCounts(string? json) =>
        json is null ? null : JsonSerializer.Deserialize<CanonicalSnapshotCounts>(json, JsonOptions);

    private static DatabaseDiscoveryOperationResult<T> Success<T>(T response) => new(response, null, DatabaseDiscoveryFailure.None);
    private static DatabaseDiscoveryOperationResult<T> Failure<T>(DatabaseDiscoveryFailure failure) => new(default, null, failure);
    private static DatabaseDiscoveryOperationResult<T> Validation<T>(IReadOnlyDictionary<string, string[]> errors) =>
        new(default, errors, DatabaseDiscoveryFailure.Validation);

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed class AccessibleRunRow
    {
        public required DatabaseDiscoveryRun Run { get; init; }
        public required DatabaseConnectionProfile Profile { get; init; }
    }
}
