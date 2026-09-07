using System.Linq.Expressions;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using EvidenceEntity = SystemKnowledgeHub.Api.Features.Evidence.Domain.Evidence;

namespace SystemKnowledgeHub.Api.Features.Evidence.Application;

public static class EffectiveEvidence
{
    public static readonly Expression<Func<EvidenceEntity, bool>> Predicate =
        item => item.EvidenceType != EvidenceType.HumanConfirmation || item.WithdrawnAt == null;
}
