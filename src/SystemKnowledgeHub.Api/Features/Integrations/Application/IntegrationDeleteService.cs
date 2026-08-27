using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Integrations.Application;

public sealed class IntegrationDeleteService(KnowledgeHubDbContext dbContext, ConcurrencyTokenCodec tokenCodec)
{
    public async Task<SoftDeleteResult> DeleteIntegration(long id, string? token, SoftDeleteActor actor, CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id)) errors["id"] = ["集成关系 ID 必须是 JavaScript 安全范围内的正整数。"]; 
        if (!tokenCodec.TryDecode(token, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        if (errors.Count > 0) return new(SoftDeleteFailure.Validation, errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.Integrations.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (item is null) return new(SoftDeleteFailure.NotFound);
        if (!SoftDeleteAuthorization.CanDelete(actor, item.CreatedByUserId)) return new(SoftDeleteFailure.Forbidden);
        if (item.Version != expectedVersion) return new(SoftDeleteFailure.Conflict);
        var blockers = new List<DeleteDependencyBlocker>(4);
        await Add(blockers, "contractFields", "集成契约字段", dbContext.IntegrationContractFields.CountAsync(entity => entity.IntegrationId == id, cancellationToken));
        await Add(blockers, "knowledgeRelations", "知识关系", dbContext.KnowledgeRelations.CountAsync(entity => (entity.SourceType == KnowledgeTargetType.Integration && entity.SourceId == id) || (entity.TargetType == KnowledgeTargetType.Integration && entity.TargetId == id), cancellationToken));
        await Add(blockers, "unknownItems", "未关闭待确认事项", dbContext.UnknownItemTargets.CountAsync(entity => entity.TargetType == KnowledgeTargetType.Integration && entity.TargetId == id && entity.UnknownItem.Status != UnknownItemStatus.Closed, cancellationToken));
        await Add(blockers, "proposedKnowledgeUpdates", "待应用知识更新", dbContext.KnowledgeUpdates.CountAsync(entity => entity.TargetType == KnowledgeTargetType.Integration && entity.TargetId == id && entity.Status == KnowledgeUpdateStatus.Proposed, cancellationToken));
        if (blockers.Count > 0) return new(SoftDeleteFailure.Dependencies, Blockers: blockers);
        item.IsDeleted = true; item.DeletedAt = DateTimeOffset.UtcNow; item.DeletedByUserId = actor.UserId; item.DeletedByDisplayName = actor.DisplayName; item.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(SoftDeleteFailure.Conflict); }
        return new(SoftDeleteFailure.None);
    }
    private static async Task Add(List<DeleteDependencyBlocker> blockers, string type, string name, Task<int> countTask) { var count = await countTask; if (count > 0) blockers.Add(new(type, name, count)); }
}
