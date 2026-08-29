using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public sealed class DatabaseConnectionTestService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec tokenCodec,
    IDatabaseConnectionSecretStore secretStore,
    IEnumerable<IDatabaseConnectionTester> testers)
{
    public async Task<DatabaseConnectionOperationResult<DatabaseConnectionTestResponse>> Test(
        long profileId,
        string? concurrencyToken,
        DatabaseConnectionActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(profileId)) errors["id"] = ["连接配置必须是有效 ID。"];
        if (!tokenCodec.TryDecode(concurrencyToken, out var expectedVersion))
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0)
            return new(null, errors, DatabaseConnectionFailure.Validation);

        TestStartSnapshot start;
        await using (var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken))
        {
            var profile = await dbContext.DatabaseConnectionProfiles
                .Include(item => item.Secret)
                .SingleOrDefaultAsync(item => item.Id == profileId, cancellationToken);
            if (profile is null) return Failure(DatabaseConnectionFailure.NotFound);
            if (profile.Version != expectedVersion) return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
            if (!profile.IsEnabled) return Failure(DatabaseConnectionFailure.Disabled);
            if (!await dbContext.DatabaseSources.AnyAsync(item => item.Id == profile.DatabaseSourceId, cancellationToken))
                return Failure(DatabaseConnectionFailure.ReferenceInvalid);

            var now = DateTimeOffset.UtcNow;
            var attemptId = Guid.NewGuid().ToString("N");
            profile.LatestConnectionTestAttemptId = attemptId;
            profile.LastConnectionTestStartedAt = now;
            profile.UpdatedAt = now;
            profile.Version++;
            dbContext.DatabaseConnectionAuditEvents.Add(Audit(
                profile.Id,
                DatabaseConnectionAuditAction.ConnectionTestStarted,
                DatabaseConnectionAuditOutcome.Succeeded,
                actor,
                now));
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
            }
            catch (DbUpdateConcurrencyException)
            {
                return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
            }

            start = new TestStartSnapshot(
                profile.Id,
                attemptId,
                profile.ConfigurationRevision,
                profile.Secret?.Version ?? 0,
                profile.ProviderType,
                profile.Host,
                profile.Port,
                profile.DatabaseName,
                profile.ServiceName,
                profile.Username,
                JsonSerializer.Deserialize<string[]>(profile.IncludedSchemasJson) ?? [],
                profile.Secret);
        }

        dbContext.ChangeTracker.Clear();
        var testResult = await Execute(start, cancellationToken);
        return await Complete(start, testResult, actor);
    }

    private async Task<DatabaseConnectionTestResult> Execute(
        TestStartSnapshot start,
        CancellationToken cancellationToken)
    {
        var secret = secretStore.Resolve(start.ProfileId, start.Secret);
        if (secret.Failure == DatabaseConnectionSecretFailure.Missing)
            return DatabaseConnectionTestResult.Fail(DatabaseConnectionFailure.SecretMissing, "尚未设置数据库连接密码。");
        if (secret.Failure == DatabaseConnectionSecretFailure.Unavailable)
            return DatabaseConnectionTestResult.Fail(DatabaseConnectionFailure.SecretUnavailable, "数据库连接密码无法解密，请重新设置。");
        var tester = testers.SingleOrDefault(item => item.ProviderType == start.ProviderType);
        if (tester is null)
            return DatabaseConnectionTestResult.Fail(DatabaseConnectionFailure.ProviderUnavailable, "当前 Provider 尚未提供测试连接实现。");

        var connection = new DatabaseDiscoveryConnectionContext(
            start.ProfileId,
            start.ConfigurationRevision,
            start.SecretVersion,
            start.ProviderType,
            start.Host,
            start.Port,
            start.DatabaseName,
            start.ServiceName,
            start.Username,
            secret.Plaintext!,
            start.IncludedSchemas);
        try
        {
            return await tester.TestConnectionAsync(connection, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DatabaseConnectionTestResult.Fail(DatabaseConnectionFailure.Cancelled, "数据库连接测试已取消。");
        }
        catch
        {
            return DatabaseConnectionTestResult.Fail(DatabaseConnectionFailure.ConnectionFailed, "数据库连接测试失败。");
        }
    }

    private async Task<DatabaseConnectionOperationResult<DatabaseConnectionTestResponse>> Complete(
        TestStartSnapshot start,
        DatabaseConnectionTestResult testResult,
        DatabaseConnectionActor actor)
    {
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, CancellationToken.None);
        var profile = await dbContext.DatabaseConnectionProfiles
            .Include(item => item.Secret)
            .SingleOrDefaultAsync(item => item.Id == start.ProfileId, CancellationToken.None);
        if (profile is null) return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        var currentSecretVersion = profile.Secret?.Version ?? 0;
        var superseded = profile.LatestConnectionTestAttemptId != start.AttemptId
            || profile.ConfigurationRevision != start.ConfigurationRevision
            || currentSecretVersion != start.SecretVersion;
        var now = DateTimeOffset.UtcNow;
        if (superseded)
        {
            dbContext.DatabaseConnectionAuditEvents.Add(Audit(
                profile.Id,
                DatabaseConnectionAuditAction.ConnectionTestResult,
                DatabaseConnectionAuditOutcome.Superseded,
                actor,
                now,
                "ConcurrencyConflict"));
            await dbContext.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
            return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        }

        profile.ConnectionStatus = testResult.Succeeded
            ? DatabaseConnectionStatus.Succeeded
            : DatabaseConnectionStatus.Failed;
        profile.LastConnectionTestAt = now;
        profile.LastConnectionTestErrorCode = testResult.Succeeded ? null : testResult.Failure.ToString();
        profile.LastConnectionTestVendorCode = testResult.VendorCode;
        profile.LastConnectionTestSummary = testResult.Summary;
        profile.UpdatedAt = now;
        profile.Version++;
        dbContext.DatabaseConnectionAuditEvents.Add(Audit(
            profile.Id,
            DatabaseConnectionAuditAction.ConnectionTestResult,
            testResult.Succeeded ? DatabaseConnectionAuditOutcome.Succeeded : DatabaseConnectionAuditOutcome.Failed,
            actor,
            now,
            testResult.Succeeded ? null : testResult.Failure.ToString(),
            testResult.VendorCode));
        try
        {
            await dbContext.SaveChangesAsync(CancellationToken.None);
            await transaction.CommitAsync(CancellationToken.None);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Failure(DatabaseConnectionFailure.ConcurrencyConflict);
        }

        var response = new DatabaseConnectionTestResponse(
            profile.Id,
            testResult.Succeeded,
            testResult.Succeeded ? null : testResult.Failure.ToString(),
            testResult.VendorCode,
            testResult.Summary,
            testResult.ProviderVersion,
            testResult.ServiceName,
            testResult.ContainerName,
            tokenCodec.Encode(profile.Version));
        return new(
            response,
            null,
            testResult.Succeeded ? DatabaseConnectionFailure.None : testResult.Failure,
            testResult.VendorCode);
    }

    private static DatabaseConnectionAuditEvent Audit(
        long profileId,
        DatabaseConnectionAuditAction action,
        DatabaseConnectionAuditOutcome outcome,
        DatabaseConnectionActor actor,
        DateTimeOffset occurredAt,
        string? errorCode = null,
        string? vendorCode = null) => new()
    {
        ProfileId = profileId,
        Action = action,
        Outcome = outcome,
        ErrorCode = errorCode,
        VendorCode = vendorCode,
        ActorUserId = actor.Creator.UserId,
        ActorDisplayName = actor.Creator.DisplayName,
        OccurredAt = occurredAt,
    };

    private static DatabaseConnectionOperationResult<DatabaseConnectionTestResponse> Failure(
        DatabaseConnectionFailure failure) => new(null, null, failure);

    private sealed record TestStartSnapshot(
        long ProfileId,
        string AttemptId,
        long ConfigurationRevision,
        long SecretVersion,
        DatabaseProviderType ProviderType,
        string Host,
        int Port,
        string? DatabaseName,
        string? ServiceName,
        string Username,
        IReadOnlyList<string> IncludedSchemas,
        DatabaseConnectionSecret? Secret);
}
