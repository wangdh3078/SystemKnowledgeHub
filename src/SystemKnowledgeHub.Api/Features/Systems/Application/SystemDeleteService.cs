using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Systems.Application;

public sealed class SystemDeleteService(
    KnowledgeHubDbContext dbContext,
    ConcurrencyTokenCodec tokenCodec)
{
    public async Task<SoftDeleteResult> DeleteSystem(
        long systemId,
        string? concurrencyToken,
        SoftDeleteActor actor,
        CancellationToken cancellationToken)
    {
        var errors = Validate(systemId, concurrencyToken, out var expectedVersion);
        if (errors.Count > 0) return new(SoftDeleteFailure.Validation, errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var system = await dbContext.Systems.SingleOrDefaultAsync(item => item.Id == systemId, cancellationToken);
        if (system is null) return new(SoftDeleteFailure.NotFound);
        if (!SoftDeleteAuthorization.CanDelete(actor, system.CreatedByUserId)) return new(SoftDeleteFailure.Forbidden);
        if (system.Version != expectedVersion) return new(SoftDeleteFailure.Conflict);

        var blockers = new List<DeleteDependencyBlocker>(8);
        await Add(blockers, "technologyTags", "技术标签", dbContext.SystemTechnologyTags.CountAsync(item => item.SystemId == systemId, cancellationToken));
        await Add(blockers, "businessFunctions", "业务功能", dbContext.BusinessFunctions.CountAsync(item => item.SystemId == systemId, cancellationToken));
        await Add(blockers, "databaseSources", "数据库来源", dbContext.DatabaseSources.CountAsync(item => item.SystemId == systemId, cancellationToken));
        await Add(blockers, "businessRules", "业务规则", dbContext.BusinessRules.CountAsync(item => item.SystemId == systemId, cancellationToken));
        await Add(blockers, "integrations", "集成关系", dbContext.Integrations.CountAsync(item => item.SourceSystemId == systemId || item.TargetSystemId == systemId, cancellationToken));
        await Add(blockers, "unknownItems", "未关闭待确认事项", dbContext.UnknownItems.CountAsync(item =>
            item.Status != UnknownItemStatus.Closed
            && (item.SystemId == systemId || item.Targets.Any(target => target.TargetType == KnowledgeTargetType.System && target.TargetId == systemId)), cancellationToken));
        await Add(blockers, "knowledgeRelations", "知识关系", dbContext.KnowledgeRelations.CountAsync(item =>
            (item.SourceType == KnowledgeTargetType.System && item.SourceId == systemId)
            || (item.TargetType == KnowledgeTargetType.System && item.TargetId == systemId), cancellationToken));
        await Add(blockers, "proposedKnowledgeUpdates", "待应用知识更新", dbContext.KnowledgeUpdates.CountAsync(item =>
            item.Status == KnowledgeUpdateStatus.Proposed
            && item.TargetType == KnowledgeTargetType.System
            && item.TargetId == systemId, cancellationToken));
        if (blockers.Count > 0) return new(SoftDeleteFailure.Dependencies, Blockers: blockers);

        system.IsDeleted = true;
        system.DeletedAt = DateTimeOffset.UtcNow;
        system.DeletedByUserId = actor.UserId;
        system.DeletedByDisplayName = actor.DisplayName;
        system.Version = expectedVersion + 1;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(SoftDeleteFailure.Conflict);
        }

        return new(SoftDeleteFailure.None);
    }

    private Dictionary<string, string[]> Validate(long id, string? token, out long version)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id)) errors["id"] = ["系统 ID 必须是 JavaScript 安全范围内的正整数。"]; 
        if (!tokenCodec.TryDecode(token, out version)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        return errors;
    }

    private static async Task Add(List<DeleteDependencyBlocker> blockers, string type, string name, Task<int> countTask)
    {
        var count = await countTask;
        if (count > 0) blockers.Add(new(type, name, count));
    }
}
