using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Evidence.Application;

/// <summary>将 Evidence Subject 映射为可验证的知识上下文。</summary>
public sealed class EvidenceSubjectResolver(
    KnowledgeHubDbContext dbContext,
    RelationshipTargetResolver relationshipTargetResolver)
{
    /// <summary>解析 Subject 的显示名称与当前知识状态。</summary>
    /// <param name="subjectType">受控 Evidence Subject 类型。</param>
    /// <param name="subjectId">目标对象标识符。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回 Subject 上下文；类型不受支持或目标不存在时返回 null。</returns>
    public async Task<EvidenceSubjectContext?> Resolve(
        EvidenceSubjectType subjectType,
        long subjectId,
        CancellationToken cancellationToken)
    {
        return subjectType switch
        {
            EvidenceSubjectType.System => await dbContext.Systems.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext(item.Name, item.KnowledgeStatus))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.DatabaseSource => await dbContext.DatabaseSources.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext(
                    item.System.Name + " · " + item.Name,
                    KnowledgeStatus.Unknown))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.BusinessFunction => await dbContext.BusinessFunctions.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext(
                    item.System.Name + " · " + item.Name,
                    item.KnowledgeStatus))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.DatabaseObject => await dbContext.DatabaseObjects.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext(
                    item.SchemaName + "." + item.ObjectName,
                    item.KnowledgeStatus))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.DatabaseColumn => await dbContext.DatabaseColumns.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext(
                    item.DatabaseObject.SchemaName + "." + item.DatabaseObject.ObjectName + "." + item.ColumnName,
                    item.KnowledgeStatus))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.BusinessRule => await dbContext.BusinessRules.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext(
                    item.System.Name + " · " + item.Name,
                    item.KnowledgeStatus))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.Integration => await dbContext.Integrations.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext(item.Name, item.KnowledgeStatus))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.KnowledgeRelation => await ResolveRelationship(subjectId, cancellationToken),
            EvidenceSubjectType.UnknownItem => await dbContext.UnknownItems.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext(item.ItemCode + " · " + item.Question, KnowledgeStatus.Unknown))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.Finding => await dbContext.Findings.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext("调查发现 · " + item.Content, KnowledgeStatus.Unknown))
                .SingleOrDefaultAsync(cancellationToken),
            EvidenceSubjectType.Resolution => await dbContext.Resolutions.AsNoTracking()
                .Where(item => item.Id == subjectId)
                .Select(item => new EvidenceSubjectContext("结论草稿 · " + item.Conclusion, KnowledgeStatus.Unknown))
                .SingleOrDefaultAsync(cancellationToken),
            _ => null,
        };
    }

    private async Task<EvidenceSubjectContext?> ResolveRelationship(long id, CancellationToken cancellationToken)
    {
        var relation = await dbContext.KnowledgeRelations.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (relation is null) return null;
        var source = await relationshipTargetResolver.Resolve(relation.SourceType, relation.SourceId, cancellationToken);
        var target = await relationshipTargetResolver.Resolve(relation.TargetType, relation.TargetId, cancellationToken);
        return source is null || target is null
            ? null
            : new EvidenceSubjectContext($"{source.Title} → {relation.RelationType} → {target.Title}", relation.KnowledgeStatus);
    }
}
