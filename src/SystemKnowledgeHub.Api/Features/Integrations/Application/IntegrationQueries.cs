using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.Integrations.Application;

public sealed class IntegrationQueries(KnowledgeHubDbContext dbContext, RelationshipTargetResolver targetResolver, ConcurrencyTokenCodec tokenCodec)
{
    public async Task<IntegrationDetailResponse?> GetDetail(long id, CancellationToken cancellationToken)
    {
        var integration = await dbContext.Integrations.AsNoTracking().Include(item => item.SourceSystem).Include(item => item.TargetSystem)
            .Include(item => item.ContractFields).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (integration is null) return null;
        var relations = await dbContext.KnowledgeRelations.AsNoTracking()
            .Where(item => item.SourceType == KnowledgeTargetType.Integration && item.SourceId == id || item.TargetType == KnowledgeTargetType.Integration && item.TargetId == id)
            .ToArrayAsync(cancellationToken);
        var functions = new List<IntegrationRelationshipResponse>(); var data = new List<IntegrationRelationshipResponse>();
        foreach (var relation in relations)
        {
            var outgoing = relation.SourceType == KnowledgeTargetType.Integration && relation.SourceId == id;
            var otherType = outgoing ? relation.TargetType : relation.SourceType;
            var otherId = outgoing ? relation.TargetId : relation.SourceId;
            var other = await targetResolver.Resolve(otherType, otherId, cancellationToken);
            if (other is null) continue;
            var row = new IntegrationRelationshipResponse(relation.Id, otherId, other.Title, relation.RelationType.ToString());
            if (otherType == KnowledgeTargetType.BusinessFunction) functions.Add(row);
            if (otherType is KnowledgeTargetType.DatabaseSource or KnowledgeTargetType.DatabaseObject or KnowledgeTargetType.DatabaseColumn) data.Add(row);
        }
        var evidenceRows = await dbContext.Evidence.AsNoTracking().Where(item => item.SubjectType == EvidenceSubjectType.Integration && item.SubjectId == id)
            .Select(item => new { item.Id, item.EvidenceType, item.SourceTitle, item.ProvidedAt }).ToArrayAsync(cancellationToken);
        var evidence = evidenceRows.OrderByDescending(item => item.ProvidedAt)
            .Select(item => new IntegrationEvidenceResponse(item.Id, item.EvidenceType.ToString(), item.SourceTitle)).ToArray();
        var unknownItems = await dbContext.UnknownItemTargets.AsNoTracking().Where(item => item.TargetType == KnowledgeTargetType.Integration && item.TargetId == id && item.UnknownItem.Status != UnknownItemStatus.Closed)
            .Select(item => new IntegrationUnknownItemResponse(item.UnknownItem.Id, item.UnknownItem.Question, item.UnknownItem.Status.ToString())).ToArrayAsync(cancellationToken);
        var participants = new[] { integration.SourceSystem?.Name, integration.TargetSystem?.Name }.Where(item => !string.IsNullOrWhiteSpace(item)).Distinct(StringComparer.OrdinalIgnoreCase).Cast<string>().ToArray();
        var gaps = new List<string>();
        if (!integration.ContractFields.Any()) gaps.Add("尚未补充消息 / 数据契约字段");
        if (!evidence.Any()) gaps.Add("尚未记录证据");
        return new IntegrationDetailResponse(integration.Id, tokenCodec.Encode(integration.Version), new(integration.Name, integration.IntegrationType.ToString(), integration.KnowledgeStatus.ToString()),
            new(integration.SourceSystemId, integration.SourcePartyName), new(integration.TargetSystemId, integration.TargetPartyName), integration.FlowDirection.ToString(), integration.Purpose,
            IntegrationEndpointParser.Deserialize(integration.EndpointJson), integration.DatabaseSourceId, integration.DatabaseObjectId,
            integration.ContractFields.OrderBy(item => item.Ordinal).Select(item => new IntegrationContractFieldResponse(item.Ordinal, item.FieldName, item.DataType, item.IsRequired, item.Description, item.SampleValue)).ToArray(),
            functions, data, evidence, unknownItems, new(participants, functions.Count, data.Count, unknownItems.Length, gaps),
            ["UpdateIntegration", "ReplaceIntegrationContractFields", "AddKnowledgeRelation", "AddEvidence", "ChangeKnowledgeStatus", "CreateUnknownItem"]);
    }
}
