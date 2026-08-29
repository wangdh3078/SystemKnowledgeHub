using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;

public sealed class DatabaseKnowledgeDeleteService(KnowledgeHubDbContext dbContext, ConcurrencyTokenCodec tokenCodec)
{
    public async Task<SoftDeleteResult> DeleteDatabaseSource(long id, string? token, SoftDeleteActor actor, CancellationToken cancellationToken)
    {
        var errors = Validate(id, token, "数据库来源", out var expectedVersion);
        if (errors.Count > 0) return new(SoftDeleteFailure.Validation, errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var source = await dbContext.DatabaseSources.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (source is null) return new(SoftDeleteFailure.NotFound);
        if (!SoftDeleteAuthorization.CanDelete(actor, source.CreatedByUserId)) return new(SoftDeleteFailure.Forbidden);
        if (source.Version != expectedVersion) return new(SoftDeleteFailure.Conflict);
        var blockers = new List<DeleteDependencyBlocker>(6);
        await Add(blockers, "databaseObjects", "数据库对象", dbContext.DatabaseObjects.CountAsync(item => item.DatabaseSourceId == id, cancellationToken));
        await Add(blockers, "integrations", "集成关系", dbContext.Integrations.CountAsync(item => item.DatabaseSourceId == id, cancellationToken));
        await Add(blockers, "enabledDatabaseConnectionProfiles", "已启用数据库连接配置", dbContext.DatabaseConnectionProfiles.CountAsync(item => item.DatabaseSourceId == id && item.IsEnabled, cancellationToken));
        await AddControlled(blockers, KnowledgeTargetType.DatabaseSource, id, cancellationToken);
        if (blockers.Count > 0) return new(SoftDeleteFailure.Dependencies, Blockers: blockers);
        source.IsDeleted = true; source.DeletedAt = DateTimeOffset.UtcNow; source.DeletedByUserId = actor.UserId; source.DeletedByDisplayName = actor.DisplayName; source.Version = expectedVersion + 1;
        return await Save(transaction, cancellationToken);
    }

    public async Task<SoftDeleteResult> DeleteDatabaseObject(long id, string? token, SoftDeleteActor actor, CancellationToken cancellationToken)
    {
        var errors = Validate(id, token, "数据库对象", out var expectedVersion);
        if (errors.Count > 0) return new(SoftDeleteFailure.Validation, errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.DatabaseObjects.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (item is null) return new(SoftDeleteFailure.NotFound);
        if (!SoftDeleteAuthorization.CanDelete(actor, item.CreatedByUserId)) return new(SoftDeleteFailure.Forbidden);
        if (item.Version != expectedVersion) return new(SoftDeleteFailure.Conflict);
        var blockers = new List<DeleteDependencyBlocker>(5);
        await Add(blockers, "databaseColumns", "数据库字段", dbContext.DatabaseColumns.CountAsync(entity => entity.DatabaseObjectId == id, cancellationToken));
        await Add(blockers, "integrations", "集成关系", dbContext.Integrations.CountAsync(entity => entity.DatabaseObjectId == id, cancellationToken));
        await AddControlled(blockers, KnowledgeTargetType.DatabaseObject, id, cancellationToken);
        if (blockers.Count > 0) return new(SoftDeleteFailure.Dependencies, Blockers: blockers);
        item.IsDeleted = true; item.DeletedAt = DateTimeOffset.UtcNow; item.DeletedByUserId = actor.UserId; item.DeletedByDisplayName = actor.DisplayName; item.Version = expectedVersion + 1;
        return await Save(transaction, cancellationToken);
    }

    public async Task<SoftDeleteResult> DeleteDatabaseColumn(long id, string? token, SoftDeleteActor actor, CancellationToken cancellationToken)
    {
        var errors = Validate(id, token, "数据库字段", out var expectedVersion);
        if (errors.Count > 0) return new(SoftDeleteFailure.Validation, errors);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var item = await dbContext.DatabaseColumns.SingleOrDefaultAsync(entity => entity.Id == id, cancellationToken);
        if (item is null) return new(SoftDeleteFailure.NotFound);
        if (!SoftDeleteAuthorization.CanDelete(actor, item.CreatedByUserId)) return new(SoftDeleteFailure.Forbidden);
        if (item.Version != expectedVersion) return new(SoftDeleteFailure.Conflict);
        var blockers = new List<DeleteDependencyBlocker>(4);
        await Add(blockers, "knownValues", "字段已知值", dbContext.ColumnKnownValues.CountAsync(entity => entity.DatabaseColumnId == id, cancellationToken));
        await AddControlled(blockers, KnowledgeTargetType.DatabaseColumn, id, cancellationToken);
        if (blockers.Count > 0) return new(SoftDeleteFailure.Dependencies, Blockers: blockers);
        item.IsDeleted = true; item.DeletedAt = DateTimeOffset.UtcNow; item.DeletedByUserId = actor.UserId; item.DeletedByDisplayName = actor.DisplayName; item.Version = expectedVersion + 1;
        return await Save(transaction, cancellationToken);
    }

    private async Task AddControlled(List<DeleteDependencyBlocker> blockers, KnowledgeTargetType type, long id, CancellationToken cancellationToken)
    {
        await Add(blockers, "knowledgeRelations", "知识关系", dbContext.KnowledgeRelations.CountAsync(item => (item.SourceType == type && item.SourceId == id) || (item.TargetType == type && item.TargetId == id), cancellationToken));
        await Add(blockers, "unknownItems", "未关闭待确认事项", dbContext.UnknownItemTargets.CountAsync(item => item.TargetType == type && item.TargetId == id && item.UnknownItem.Status != UnknownItemStatus.Closed, cancellationToken));
        await Add(blockers, "proposedKnowledgeUpdates", "待应用知识更新", dbContext.KnowledgeUpdates.CountAsync(item => item.TargetType == type && item.TargetId == id && item.Status == KnowledgeUpdateStatus.Proposed, cancellationToken));
    }

    private async Task<SoftDeleteResult> Save(SqliteImmediateTransaction transaction, CancellationToken cancellationToken)
    {
        try { await dbContext.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return new(SoftDeleteFailure.None); }
        catch (DbUpdateConcurrencyException) { return new(SoftDeleteFailure.Conflict); }
    }

    private Dictionary<string, string[]> Validate(long id, string? token, string displayName, out long version)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id)) errors["id"] = [$"{displayName} ID 必须是 JavaScript 安全范围内的正整数。"]; 
        if (!tokenCodec.TryDecode(token, out version)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        return errors;
    }
    private static async Task Add(List<DeleteDependencyBlocker> blockers, string type, string name, Task<int> countTask) { var count = await countTask; if (count > 0) blockers.Add(new(type, name, count)); }
}
