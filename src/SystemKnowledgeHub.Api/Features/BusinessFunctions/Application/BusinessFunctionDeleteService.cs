using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Application;

public sealed class BusinessFunctionDeleteService(KnowledgeHubDbContext dbContext, ConcurrencyTokenCodec tokenCodec)
{
    public async Task<SoftDeleteResult> DeleteBusinessFunction(long id, string? token, SoftDeleteActor actor, CancellationToken cancellationToken)
    {
        var errors = Validate(id, token, out var expectedVersion);
        if (errors.Count > 0) return new(SoftDeleteFailure.Validation, errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var function = await dbContext.BusinessFunctions.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (function is null) return new(SoftDeleteFailure.NotFound);
        if (!SoftDeleteAuthorization.CanDelete(actor, function.CreatedByUserId)) return new(SoftDeleteFailure.Forbidden);
        if (function.Version != expectedVersion) return new(SoftDeleteFailure.Conflict);

        var blockers = new List<DeleteDependencyBlocker>(4);
        await Add(blockers, "processSteps", "业务流程步骤", dbContext.BusinessProcessSteps.CountAsync(item => item.BusinessFunctionId == id, cancellationToken));
        await Add(blockers, "knowledgeRelations", "知识关系", RelationCount(KnowledgeTargetType.BusinessFunction, id, cancellationToken));
        await Add(blockers, "unknownItems", "未关闭待确认事项", UnknownCount(KnowledgeTargetType.BusinessFunction, id, cancellationToken));
        await Add(blockers, "proposedKnowledgeUpdates", "待应用知识更新", ProposedCount(KnowledgeTargetType.BusinessFunction, id, cancellationToken));
        if (blockers.Count > 0) return new(SoftDeleteFailure.Dependencies, Blockers: blockers);

        function.IsDeleted = true;
        function.DeletedAt = DateTimeOffset.UtcNow;
        function.DeletedByUserId = actor.UserId;
        function.DeletedByDisplayName = actor.DisplayName;
        function.Version = expectedVersion + 1;
        try { await dbContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); }
        catch (DbUpdateConcurrencyException) { return new(SoftDeleteFailure.Conflict); }
        return new(SoftDeleteFailure.None);
    }

    private Dictionary<string, string[]> Validate(long id, string? token, out long version)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id)) errors["id"] = ["业务功能 ID 必须是 JavaScript 安全范围内的正整数。"]; 
        if (!tokenCodec.TryDecode(token, out version)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        return errors;
    }
    private Task<int> RelationCount(KnowledgeTargetType type, long id, CancellationToken ct) => dbContext.KnowledgeRelations.CountAsync(item => (item.SourceType == type && item.SourceId == id) || (item.TargetType == type && item.TargetId == id), ct);
    private Task<int> UnknownCount(KnowledgeTargetType type, long id, CancellationToken ct) => dbContext.UnknownItemTargets.CountAsync(item => item.TargetType == type && item.TargetId == id && item.UnknownItem.Status != UnknownItemStatus.Closed, ct);
    private Task<int> ProposedCount(KnowledgeTargetType type, long id, CancellationToken ct) => dbContext.KnowledgeUpdates.CountAsync(item => item.TargetType == type && item.TargetId == id && item.Status == KnowledgeUpdateStatus.Proposed, ct);
    private static async Task Add(List<DeleteDependencyBlocker> blockers, string type, string name, Task<int> countTask) { var count = await countTask; if (count > 0) blockers.Add(new(type, name, count)); }
}
