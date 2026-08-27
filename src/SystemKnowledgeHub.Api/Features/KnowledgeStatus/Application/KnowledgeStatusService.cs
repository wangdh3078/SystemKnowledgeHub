using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.StatusProgression.Application;

public sealed class KnowledgeStatusService(
    KnowledgeHubDbContext dbContext,
    KnowledgeStatusPolicy policy,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    public async Task<ChangeKnowledgeStatusResult> ChangeKnowledgeStatus(
        ChangeKnowledgeStatusCommand request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request, out var targetType, out var targetStatus, out var expectedVersion);
        if (errors.Count > 0)
        {
            return new ChangeKnowledgeStatusResult(null, errors, KnowledgeStatusFailure.Validation);
        }

        if (targetType == KnowledgeStatusTargetType.DatabaseSource)
        {
            return Unsupported(request.Target!, "DatabaseSource 第一版不持久化 KnowledgeStatus，不允许变更。");
        }
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var result = targetType switch
        {
            KnowledgeStatusTargetType.System => await ChangeSystem(request, targetStatus, expectedVersion, cancellationToken),
            KnowledgeStatusTargetType.BusinessFunction => await ChangeBusinessFunction(request, targetStatus, expectedVersion, cancellationToken),
            KnowledgeStatusTargetType.DatabaseObject => await ChangeDatabaseObject(request, targetStatus, expectedVersion, cancellationToken),
            KnowledgeStatusTargetType.DatabaseColumn => await ChangeDatabaseColumn(request, targetStatus, expectedVersion, cancellationToken),
            KnowledgeStatusTargetType.BusinessRule => await ChangeBusinessRule(request, targetStatus, expectedVersion, cancellationToken),
            KnowledgeStatusTargetType.Integration => await ChangeIntegration(request, targetStatus, expectedVersion, cancellationToken),
            KnowledgeStatusTargetType.KnowledgeDocument => await ChangeKnowledgeDocument(request, targetStatus, expectedVersion, cancellationToken),
            _ => Unsupported(request.Target!, "当前目标类型不支持知识状态变更。"),
        };
        if (result.Failure == KnowledgeStatusFailure.None)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return result;
    }

    private async Task<ChangeKnowledgeStatusResult> ChangeSystem(
        ChangeKnowledgeStatusCommand request,
        KnowledgeStatus targetStatus,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Systems.SingleOrDefaultAsync(item => item.Id == request.Target!.Id, cancellationToken);
        if (entity is null) return NotFound();
        return await Apply(
            request, targetStatus, expectedVersion, entity.KnowledgeStatus, entity.Version,
            EvidenceSubjectType.System,
            (status, reason, changedAt, name, role, version) =>
            {
                entity.KnowledgeStatus = status;
                entity.KnowledgeStatusReason = reason;
                entity.KnowledgeStatusChangedAt = changedAt;
                entity.KnowledgeStatusChangedByName = name;
                entity.KnowledgeStatusChangedByRole = role;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                entity.Version = version;
            },
            cancellationToken);
    }

    private async Task<ChangeKnowledgeStatusResult> ChangeBusinessFunction(
        ChangeKnowledgeStatusCommand request,
        KnowledgeStatus targetStatus,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.BusinessFunctions.SingleOrDefaultAsync(item => item.Id == request.Target!.Id, cancellationToken);
        if (entity is null) return NotFound();
        return await Apply(
            request, targetStatus, expectedVersion, entity.KnowledgeStatus, entity.Version,
            EvidenceSubjectType.BusinessFunction,
            (status, reason, changedAt, name, role, version) =>
            {
                entity.KnowledgeStatus = status;
                entity.KnowledgeStatusReason = reason;
                entity.KnowledgeStatusChangedAt = changedAt;
                entity.KnowledgeStatusChangedByName = name;
                entity.KnowledgeStatusChangedByRole = role;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                entity.Version = version;
            },
            cancellationToken);
    }

    private async Task<ChangeKnowledgeStatusResult> ChangeDatabaseObject(
        ChangeKnowledgeStatusCommand request,
        KnowledgeStatus targetStatus,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.DatabaseObjects.SingleOrDefaultAsync(item => item.Id == request.Target!.Id, cancellationToken);
        if (entity is null) return NotFound();
        return await Apply(
            request, targetStatus, expectedVersion, entity.KnowledgeStatus, entity.Version,
            EvidenceSubjectType.DatabaseObject,
            (status, reason, changedAt, name, role, version) =>
            {
                entity.KnowledgeStatus = status;
                entity.KnowledgeStatusReason = reason;
                entity.KnowledgeStatusChangedAt = changedAt;
                entity.KnowledgeStatusChangedByName = name;
                entity.KnowledgeStatusChangedByRole = role;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                entity.Version = version;
            },
            cancellationToken);
    }

    private async Task<ChangeKnowledgeStatusResult> ChangeDatabaseColumn(
        ChangeKnowledgeStatusCommand request,
        KnowledgeStatus targetStatus,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.DatabaseColumns.SingleOrDefaultAsync(item => item.Id == request.Target!.Id, cancellationToken);
        if (entity is null) return NotFound();
        return await Apply(
            request, targetStatus, expectedVersion, entity.KnowledgeStatus, entity.Version,
            EvidenceSubjectType.DatabaseColumn,
            (status, reason, changedAt, name, role, version) =>
            {
                entity.KnowledgeStatus = status;
                entity.KnowledgeStatusReason = reason;
                entity.KnowledgeStatusChangedAt = changedAt;
                entity.KnowledgeStatusChangedByName = name;
                entity.KnowledgeStatusChangedByRole = role;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                entity.Version = version;
            },
            cancellationToken);
    }

    private async Task<ChangeKnowledgeStatusResult> ChangeBusinessRule(
        ChangeKnowledgeStatusCommand request,
        KnowledgeStatus targetStatus,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.BusinessRules.SingleOrDefaultAsync(item => item.Id == request.Target!.Id, cancellationToken);
        if (entity is null) return NotFound();
        return await Apply(
            request, targetStatus, expectedVersion, entity.KnowledgeStatus, entity.Version,
            EvidenceSubjectType.BusinessRule,
            (status, reason, changedAt, name, role, version) =>
            {
                entity.KnowledgeStatus = status;
                entity.KnowledgeStatusReason = reason;
                entity.KnowledgeStatusChangedAt = changedAt;
                entity.KnowledgeStatusChangedByName = name;
                entity.KnowledgeStatusChangedByRole = role;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                entity.Version = version;
            },
            cancellationToken);
    }

    private async Task<ChangeKnowledgeStatusResult> ChangeIntegration(
        ChangeKnowledgeStatusCommand request,
        KnowledgeStatus targetStatus,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.Integrations.SingleOrDefaultAsync(item => item.Id == request.Target!.Id, cancellationToken);
        if (entity is null) return NotFound();
        return await Apply(
            request, targetStatus, expectedVersion, entity.KnowledgeStatus, entity.Version,
            EvidenceSubjectType.Integration,
            (status, reason, changedAt, name, role, version) =>
            {
                entity.KnowledgeStatus = status;
                entity.KnowledgeStatusReason = reason;
                entity.KnowledgeStatusChangedAt = changedAt;
                entity.KnowledgeStatusChangedByName = name;
                entity.KnowledgeStatusChangedByRole = role;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                entity.Version = version;
            },
            cancellationToken);
    }

    private async Task<ChangeKnowledgeStatusResult> ChangeKnowledgeDocument(
        ChangeKnowledgeStatusCommand request,
        KnowledgeStatus targetStatus,
        long expectedVersion,
        CancellationToken cancellationToken)
    {
        var entity = await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(item => item.Id == request.Target!.Id, cancellationToken);
        if (entity is null) return NotFound();
        return await Apply(
            request, targetStatus, expectedVersion, entity.KnowledgeStatus, entity.Version,
            EvidenceSubjectType.KnowledgeDocument,
            (status, reason, changedAt, name, role, version) =>
            {
                entity.KnowledgeStatus = status;
                entity.KnowledgeStatusReason = reason;
                entity.KnowledgeStatusChangedAt = changedAt;
                entity.KnowledgeStatusChangedByName = name;
                entity.KnowledgeStatusChangedByRole = role;
                entity.UpdatedAt = DateTimeOffset.UtcNow;
                entity.Version = version;
            },
            cancellationToken);
    }

    private async Task<ChangeKnowledgeStatusResult> Apply(
        ChangeKnowledgeStatusCommand request,
        KnowledgeStatus targetStatus,
        long expectedVersion,
        KnowledgeStatus currentStatus,
        long currentVersion,
        EvidenceSubjectType evidenceSubjectType,
        Action<KnowledgeStatus, string?, DateTimeOffset, string, string, long> apply,
        CancellationToken cancellationToken)
    {
        if (currentVersion != expectedVersion)
        {
            return Conflict("内容已被其他操作修改，请重新加载后重试。");
        }

        var relatedEvidence = await dbContext.Evidence.AsNoTracking()
            .Where(item => item.SubjectType == evidenceSubjectType && item.SubjectId == request.Target!.Id)
            .Select(item => new
            {
                item.EvidenceType,
                item.SourceReference,
                item.SourceLocatorJson,
                item.ProviderName,
                item.ProviderRole,
                item.ProvidedAt,
            })
            .ToArrayAsync(cancellationToken);
        var hasEvidence = relatedEvidence.Any(item =>
            !string.IsNullOrWhiteSpace(item.SourceReference)
            || !string.IsNullOrWhiteSpace(item.SourceLocatorJson));
        var hasHumanConfirmation = relatedEvidence.Any(item =>
            item.EvidenceType == EvidenceType.HumanConfirmation
            && !string.IsNullOrWhiteSpace(item.ProviderName)
            && !string.IsNullOrWhiteSpace(item.ProviderRole)
            && item.ProvidedAt != default);

        var normalizedReason = NormalizeOptional(request.Reason);
        var decision = policy.Validate(currentStatus, targetStatus, normalizedReason, hasEvidence, hasHumanConfirmation);
        if (!decision.IsAllowed)
        {
            return new ChangeKnowledgeStatusResult(
                null,
                null,
                decision.Failure,
                decision.Message,
                new
                {
                    targetType = request.Target!.Type,
                    targetId = request.Target.Id,
                    currentStatus = currentStatus.ToString(),
                    targetStatus = targetStatus.ToString(),
                    missingRequirement = decision.MissingRequirement,
                });
        }

        var actor = request.Actor;
        var nextVersion = expectedVersion + 1;
        apply(
            targetStatus,
            normalizedReason,
            actor.OccurredAt,
            actor.DisplayName.Trim(),
            actor.RoleOrIdentity.Trim(),
            nextVersion);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Conflict("内容已被其他操作修改，请重新加载后重试。");
        }

        return new ChangeKnowledgeStatusResult(
            new ChangeKnowledgeStatusResponse(
                new KnowledgeStatusTargetResponse(request.Target!.Type, request.Target.Id),
                currentStatus.ToString(),
                targetStatus.ToString(),
                normalizedReason,
                actor.OccurredAt,
                concurrencyTokenCodec.Encode(nextVersion)),
            null,
            KnowledgeStatusFailure.None);
    }

    private Dictionary<string, string[]> Validate(
        ChangeKnowledgeStatusCommand request,
        out KnowledgeStatusTargetType targetType,
        out KnowledgeStatus targetStatus,
        out long expectedVersion)
    {
        var errors = new Dictionary<string, string[]>();
        targetType = default;
        targetStatus = default;
        expectedVersion = default;

        if (request.Target is null)
        {
            errors["target"] = ["必须指定知识状态目标。"];
        }
        else
        {
            if (!Enum.TryParse(request.Target.Type, false, out targetType)
                || targetType.ToString() != request.Target.Type)
            {
                errors["target.type"] = ["知识状态目标类型无效。"];
            }
            if (!ApiIdParser.IsSafePositive(request.Target.Id))
            {
                errors["target.id"] = ["目标 ID 必须是 JavaScript 安全范围内的正整数。"];
            }
        }

        if (!Enum.TryParse(request.TargetStatus, false, out targetStatus)
            || targetStatus.ToString() != request.TargetStatus)
        {
            errors["targetStatus"] = ["目标知识状态无效。"];
        }
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out expectedVersion))
        {
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        }

        return errors;
    }

    private static ChangeKnowledgeStatusResult NotFound()
        => new(null, null, KnowledgeStatusFailure.NotFound);

    private static ChangeKnowledgeStatusResult Unsupported(KnowledgeStatusTargetCommand target, string message)
        => new(null, null, KnowledgeStatusFailure.Unsupported, message, new { targetType = target.Type, targetId = target.Id });

    private static ChangeKnowledgeStatusResult Conflict(string message)
        => new(null, null, KnowledgeStatusFailure.Conflict, message);

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private enum KnowledgeStatusTargetType
    {
        System,
        DatabaseSource,
        BusinessFunction,
        DatabaseObject,
        DatabaseColumn,
        BusinessRule,
        Integration,
        KnowledgeDocument,
    }
}
