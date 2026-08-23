using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Api;

namespace SystemKnowledgeHub.Api.Features.Evidence.Application;

/// <summary>读取 Evidence 详情及其 Provider Snapshot、Subject 上下文和可用操作投影。</summary>
public sealed class EvidenceQueries(
    KnowledgeHubDbContext dbContext,
    EvidenceSubjectResolver subjectResolver,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    /// <summary>读取一个现有知识对象的 Evidence 摘要，不会改变其 KnowledgeStatus。</summary>
    /// <param name="subjectType">受控 Evidence Subject 类型。</param>
    /// <param name="subjectId">Subject 的 JavaScript 安全正整数标识符。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回该 Subject 的 Evidence 集合，或字段/Subject 失败分类。</returns>
    public async Task<EvidenceListQueryResult> GetEvidenceList(
        string? subjectType,
        long subjectId,
        CancellationToken cancellationToken)
    {
        var fieldErrors = new Dictionary<string, string[]>();
        if (!Enum.TryParse<EvidenceSubjectType>(subjectType, out var parsedSubjectType)
            || !Enum.IsDefined(parsedSubjectType))
        {
            fieldErrors["subjectType"] = ["证据关联对象类型无效。"];
        }

        if (!ApiIdParser.IsSafePositive(subjectId))
        {
            fieldErrors["subjectId"] = ["证据关联对象 ID 必须是 JavaScript 安全范围内的正整数。"];
        }

        if (fieldErrors.Count > 0)
        {
            return new EvidenceListQueryResult(null, fieldErrors, EvidenceFailure.Validation);
        }

        if (await subjectResolver.Resolve(parsedSubjectType, subjectId, cancellationToken) is null)
        {
            return new EvidenceListQueryResult(null, null, EvidenceFailure.SubjectNotFound);
        }

        var items = await dbContext.Evidence.AsNoTracking()
            .Where(item => item.SubjectType == parsedSubjectType && item.SubjectId == subjectId)
            .OrderByDescending(item => item.Id)
            .Select(item => new
            {
                item.Id,
                item.EvidenceType,
                item.KnowledgeDocumentRevisionNumberSnapshot,
                item.SourceTitle,
                item.SourceReference,
                item.SourceLocatorJson,
                item.Summary,
                item.SupportReason,
                item.ProviderName,
                item.ProviderRole,
                item.ProvidedAt,
                item.ProviderTeam,
                item.ProviderExternalKey,
                item.ProviderSource,
                item.ProviderNote,
            })
            .ToListAsync(cancellationToken);

        return new EvidenceListQueryResult(
            new EvidenceListResponse(items.Select(item => new EvidenceListItemResponse(
                item.Id,
                item.EvidenceType.ToString(),
                item.KnowledgeDocumentRevisionNumberSnapshot,
                item.SourceTitle,
                item.SourceReference,
                item.SourceLocatorJson is null ? null : JsonSerializer.Deserialize<JsonElement>(item.SourceLocatorJson),
                item.Summary,
                item.SupportReason,
                new PersonSnapshotResponse(
                    item.ProviderName,
                    item.ProviderRole,
                    item.ProvidedAt,
                    item.ProviderTeam,
                    item.ProviderExternalKey,
                    item.ProviderSource,
                    item.ProviderNote)))
                .ToList()),
            null,
            EvidenceFailure.None);
    }

    /// <summary>按安全 Evidence ID 读取可展示详情。</summary>
    /// <param name="evidenceId">待读取的 Evidence 标识符。</param>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>异步完成后返回详情，或以结果分类表示 Evidence/Subject 不存在。</returns>
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
                item.KnowledgeDocumentRevisionNumberSnapshot,
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
