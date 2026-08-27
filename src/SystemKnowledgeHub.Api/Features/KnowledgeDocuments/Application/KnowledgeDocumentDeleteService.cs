using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;

public sealed class KnowledgeDocumentDeleteService(
    KnowledgeHubDbContext dbContext,
    KnowledgeDocumentSearchIndex searchIndex,
    ConcurrencyTokenCodec tokenCodec)
{
    public async Task<SoftDeleteResult> DeleteKnowledgeDocument(
        long id,
        string? token,
        SoftDeleteActor actor,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!ApiIdParser.IsSafePositive(id)) errors["id"] = ["知识文档 ID 必须是 JavaScript 安全范围内的正整数。"]; 
        if (!tokenCodec.TryDecode(token, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"]; 
        if (errors.Count > 0) return new(SoftDeleteFailure.Validation, errors);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        var document = await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (document is null) return new(SoftDeleteFailure.NotFound);
        if (!SoftDeleteAuthorization.CanDelete(actor, document.CreatedByUserId)) return new(SoftDeleteFailure.Forbidden);
        if (document.Version != expectedVersion) return new(SoftDeleteFailure.Conflict);
        var relationCount = await dbContext.KnowledgeRelations.CountAsync(item =>
            (item.SourceType == KnowledgeTargetType.KnowledgeDocument && item.SourceId == id)
            || (item.TargetType == KnowledgeTargetType.KnowledgeDocument && item.TargetId == id), cancellationToken);
        if (relationCount > 0)
        {
            return new(SoftDeleteFailure.Dependencies, Blockers:
            [
                new DeleteDependencyBlocker("knowledgeRelations", "知识关系", relationCount),
            ]);
        }

        document.IsDeleted = true;
        document.DeletedAt = DateTimeOffset.UtcNow;
        document.DeletedByUserId = actor.UserId;
        document.DeletedByDisplayName = actor.DisplayName;
        document.Version = expectedVersion + 1;
        try
        {
            await searchIndex.Upsert(document, cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new(SoftDeleteFailure.Conflict);
        }
        return new(SoftDeleteFailure.None);
    }
}
