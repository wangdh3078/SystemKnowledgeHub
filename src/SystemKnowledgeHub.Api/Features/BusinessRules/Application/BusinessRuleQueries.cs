using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessRules.Application.Models;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.BusinessRules.Application;

public sealed class BusinessRuleQueries(KnowledgeHubDbContext dbContext,
    RelationshipTargetResolver targetResolver, ConcurrencyTokenCodec tokenCodec)
{
    public async Task<BusinessRuleDetailResponse?> GetDetail(long id, CancellationToken cancellationToken)
    {
        var rule = await dbContext.BusinessRules.AsNoTracking().Where(item => item.Id == id)
            .Select(item => new
            {
                item.Id, item.Name, item.Description, item.ConditionText, item.ResultText, item.InputDataJson,
                item.KnowledgeStatus, item.Version, SystemId = item.System.Id, SystemName = item.System.Name,
            }).SingleOrDefaultAsync(cancellationToken);
        if (rule is null) return null;

        var relations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(item => item.SourceType == KnowledgeTargetType.BusinessRule && item.SourceId == id
                || item.TargetType == KnowledgeTargetType.BusinessRule && item.TargetId == id)
            .ToArrayAsync(cancellationToken);
        var relatedFunctions = new List<BusinessRuleRelationshipResponse>();
        var relatedFields = new List<BusinessRuleRelationshipResponse>();
        var integrations = new List<BusinessRuleRelationshipResponse>();
        foreach (var relation in relations)
        {
            var ruleIsSource = relation.SourceType == KnowledgeTargetType.BusinessRule && relation.SourceId == id;
            var otherType = ruleIsSource ? relation.TargetType : relation.SourceType;
            var otherId = ruleIsSource ? relation.TargetId : relation.SourceId;
            var other = await targetResolver.Resolve(otherType, otherId, cancellationToken);
            if (other is null) continue;
            var row = new BusinessRuleRelationshipResponse(relation.Id, otherId, other.Title, relation.RelationType.ToString());
            if (!ruleIsSource && relation.RelationType == RelationType.AppliesRule && otherType == KnowledgeTargetType.BusinessFunction)
                relatedFunctions.Add(row);
            else if (ruleIsSource && relation.RelationType == RelationType.UsesField && otherType == KnowledgeTargetType.DatabaseColumn)
                relatedFields.Add(row);
            else if (ruleIsSource && relation.RelationType == RelationType.UsesIntegration && otherType == KnowledgeTargetType.Integration)
                integrations.Add(row);
        }

        var evidenceRows = await dbContext.Evidence.AsNoTracking()
            .Where(item => item.SubjectType == EvidenceSubjectType.BusinessRule && item.SubjectId == id)
            .Select(item => new { item.Id, item.EvidenceType, item.SourceTitle, item.ProvidedAt })
            .ToArrayAsync(cancellationToken);
        var evidence = evidenceRows.OrderByDescending(item => item.ProvidedAt)
            .Select(item => new BusinessRuleEvidenceResponse(item.Id, item.EvidenceType.ToString(), item.SourceTitle))
            .ToArray();
        var unknownItems = await dbContext.UnknownItemTargets.AsNoTracking()
            .Where(item => item.TargetType == KnowledgeTargetType.BusinessRule && item.TargetId == id
                && item.UnknownItem.Status != UnknownItemStatus.Closed)
            .Select(item => new BusinessRuleUnknownItemResponse(item.UnknownItem.Id, item.UnknownItem.Question,
                item.UnknownItem.Status.ToString()))
            .ToArrayAsync(cancellationToken);

        return new BusinessRuleDetailResponse(rule.Id, new(rule.SystemId, rule.SystemName), tokenCodec.Encode(rule.Version),
            new(rule.Name, rule.KnowledgeStatus.ToString()), rule.Description, rule.ConditionText, rule.ResultText,
            BusinessRuleService.DeserializeInputData(rule.InputDataJson), relatedFunctions, relatedFields, integrations,
            evidence, unknownItems, new(relations.Length, unknownItems.Length),
            ["UpdateBusinessRule", "AddKnowledgeRelation", "AddEvidence", "ChangeKnowledgeStatus", "CreateUnknownItem"]);
    }
}
