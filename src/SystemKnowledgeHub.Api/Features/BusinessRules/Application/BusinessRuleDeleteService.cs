using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.BusinessRules.Application;

public sealed class BusinessRuleDeleteService(KnowledgeHubDbContext dbContext, ConcurrencyTokenCodec tokenCodec)
{
    public async Task<SoftDeleteResult> DeleteBusinessRule(long id, string? token, SoftDeleteActor actor, CancellationToken cancellationToken)
    {
        var errors = Validate(id, token, out var expectedVersion);
        if (errors.Count > 0) return new(SoftDeleteFailure.Validation, errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var rule = await dbContext.BusinessRules.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (rule is null) return new(SoftDeleteFailure.NotFound);
        if (!SoftDeleteAuthorization.CanDelete(actor, rule.CreatedByUserId)) return new(SoftDeleteFailure.Forbidden);
        if (rule.Version != expectedVersion) return new(SoftDeleteFailure.Conflict);
        var blockers = new List<DeleteDependencyBlocker>(3);
        await Add(blockers, "knowledgeRelations", "知识关系", dbContext.KnowledgeRelations.CountAsync(item => (item.SourceType == KnowledgeTargetType.BusinessRule && item.SourceId == id) || (item.TargetType == KnowledgeTargetType.BusinessRule && item.TargetId == id), cancellationToken));
        await Add(blockers, "unknownItems", "未关闭待确认事项", dbContext.UnknownItemTargets.CountAsync(item => item.TargetType == KnowledgeTargetType.BusinessRule && item.TargetId == id && item.UnknownItem.Status != UnknownItemStatus.Closed, cancellationToken));
        await Add(blockers, "proposedKnowledgeUpdates", "待应用知识更新", dbContext.KnowledgeUpdates.CountAsync(item => item.TargetType == KnowledgeTargetType.BusinessRule && item.TargetId == id && item.Status == KnowledgeUpdateStatus.Proposed, cancellationToken));
        if (blockers.Count > 0) return new(SoftDeleteFailure.Dependencies, Blockers: blockers);
        rule.IsDeleted = true; rule.DeletedAt = DateTimeOffset.UtcNow; rule.DeletedByUserId = actor.UserId; rule.DeletedByDisplayName = actor.DisplayName; rule.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(SoftDeleteFailure.Conflict); }
        return new(SoftDeleteFailure.None);
    }
    private Dictionary<string, string[]> Validate(long id, string? token, out long version) { var errors = new Dictionary<string, string[]>(); if (!ApiIdParser.IsSafePositive(id)) errors["id"] = ["业务规则 ID 必须是 JavaScript 安全范围内的正整数。"]; if (!tokenCodec.TryDecode(token, out version)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; return errors; }
    private static async Task Add(List<DeleteDependencyBlocker> blockers, string type, string name, Task<int> countTask) { var count = await countTask; if (count > 0) blockers.Add(new(type, name, count)); }
}
