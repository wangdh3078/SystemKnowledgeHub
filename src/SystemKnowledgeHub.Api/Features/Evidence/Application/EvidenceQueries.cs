using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.Evidence.Application;

public sealed class EvidenceQueries(
    KnowledgeHubDbContext dbContext,
    EvidenceSubjectResolver subjectResolver,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    public async Task<EvidenceDetailQueryResult> GetEvidenceDetail(
        long evidenceId,
        CancellationToken cancellationToken)
    {
        var item = await dbContext.Evidence.AsNoTracking()
            .SingleOrDefaultAsync(evidence => evidence.Id == evidenceId, cancellationToken);
        if (item is null)
        {
            return new EvidenceDetailQueryResult(null, EvidenceFailure.NotFound);
        }

        var subjectContext = await subjectResolver.Resolve(item.SubjectType, item.SubjectId, cancellationToken);
        if (subjectContext is null)
        {
            return new EvidenceDetailQueryResult(null, EvidenceFailure.SubjectNotFound);
        }

        JsonElement? sourceLocator = item.SourceLocatorJson is null
            ? null
            : JsonSerializer.Deserialize<JsonElement>(item.SourceLocatorJson);

        return new EvidenceDetailQueryResult(
            new EvidenceDetailResponse(
                item.Id,
                concurrencyTokenCodec.Encode(item.Version),
                item.EvidenceType.ToString(),
                new EvidenceTargetResponse(item.SubjectType.ToString(), item.SubjectId),
                item.SubjectDetailKey,
                item.SourceTitle,
                item.SourceReference,
                sourceLocator,
                item.Summary,
                item.SupportReason,
                item.Confidence?.ToString(),
                new PersonSnapshotResponse(
                    item.ProviderName,
                    item.ProviderRole,
                    item.ProvidedAt,
                    item.ProviderTeam,
                    item.ProviderExternalKey,
                    item.ProviderSource,
                    item.ProviderNote),
                new EvidenceSubjectContextResponse(
                    subjectContext.Title,
                    subjectContext.KnowledgeStatus.ToString()),
                item.SubjectType == EvidenceSubjectType.KnowledgeRelation
                    ? new[] { "UpdateEvidence", "ChangeRelationKnowledgeStatus" }
                    : (item.SubjectType is EvidenceSubjectType.System
                    or EvidenceSubjectType.BusinessFunction
                    or EvidenceSubjectType.DatabaseObject
                    or EvidenceSubjectType.DatabaseColumn)
                    ? new[] { "UpdateEvidence", "ChangeKnowledgeStatus" }
                    : new[] { "UpdateEvidence" }),
            EvidenceFailure.None);
    }
}
