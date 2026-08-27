using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;

public sealed class KnowledgeDocumentQueries(
    KnowledgeHubDbContext dbContext,
    HistoricalTargetResolver historicalTargetResolver,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;

    public async Task<KnowledgeDocumentListQueryResult> GetList(
        KnowledgeDocumentListQuery request,
        CancellationToken cancellationToken)
    {
        var errors = Validate(request, out var documentType, out var lifecycleStatus, out var knowledgeStatus, out var sort);
        if (errors.Count > 0)
        {
            return new KnowledgeDocumentListQueryResult(null, errors);
        }

        var page = request.Page ?? 1;
        var pageSize = request.PageSize ?? DefaultPageSize;
        var query = dbContext.KnowledgeDocuments.AsNoTracking();
        var search = NormalizeOptional(request.Query);
        if (search is not null)
        {
            var pattern = $"%{search}%";
            query = query.Where(item => EF.Functions.Like(item.Title, pattern)
                || (item.Summary != null && EF.Functions.Like(item.Summary, pattern)));
        }
        if (documentType.HasValue) query = query.Where(item => item.DocumentType == documentType.Value);
        if (lifecycleStatus.HasValue)
        {
            query = query.Where(item => item.LifecycleStatus == lifecycleStatus.Value);
        }
        else
        {
            query = query.Where(item => item.LifecycleStatus != DocumentLifecycleStatus.Archived);
        }
        if (knowledgeStatus.HasValue) query = query.Where(item => item.KnowledgeStatus == knowledgeStatus.Value);

        var rows = await query
            .Select(item => new KnowledgeDocumentListItemResponse(
                item.Id,
                item.DocumentType.ToString(),
                item.Title,
                item.Summary,
                item.LifecycleStatus.ToString(),
                item.KnowledgeStatus.ToString(),
                item.CreatedByDisplayName,
                item.UpdatedByDisplayName,
                item.CreatedAt,
                item.UpdatedAt))
            .ToArrayAsync(cancellationToken);
        var items = ApplySort(rows, sort)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToArray();

        return new KnowledgeDocumentListQueryResult(
            new KnowledgeDocumentsListResponse(items, page, pageSize, rows.Length),
            null);
    }

    public async Task<KnowledgeDocumentDetailResponse?> GetDetail(long id, CancellationToken cancellationToken)
    {
        var item = await dbContext.KnowledgeDocuments.AsNoTracking()
            .SingleOrDefaultAsync(document => document.Id == id, cancellationToken);
        return item is null ? null : await ToDetail(item, cancellationToken);
    }

    public async Task<KnowledgeDocumentRevisionListQueryResult> GetRevisions(
        long knowledgeDocumentId,
        int? requestedPage,
        int? requestedPageSize,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (requestedPage is < 1)
        {
            errors["page"] = ["页码必须从 1 开始。"];
        }
        if (requestedPageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"];
        }
        if (errors.Count > 0)
        {
            return new KnowledgeDocumentRevisionListQueryResult(null, errors, false);
        }

        var owner = await historicalTargetResolver.Resolve(
            KnowledgeTargetType.KnowledgeDocument,
            knowledgeDocumentId,
            cancellationToken);
        if (owner is null)
        {
            return new KnowledgeDocumentRevisionListQueryResult(null, null, false);
        }

        var document = await dbContext.KnowledgeDocuments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == knowledgeDocumentId)
            .Select(item => new { item.CurrentRevisionNumber, item.LatestPublishedRevisionNumber })
            .SingleAsync(cancellationToken);

        var page = requestedPage ?? 1;
        var pageSize = requestedPageSize ?? DefaultPageSize;
        var revisions = dbContext.KnowledgeDocumentRevisions.AsNoTracking()
            .Where(item => item.KnowledgeDocumentId == knowledgeDocumentId);
        var total = await revisions.CountAsync(cancellationToken);
        var offset = ((long)page - 1) * pageSize;
        var items = offset > int.MaxValue
            ? []
            : await revisions
                .OrderByDescending(item => item.RevisionNumber)
                .Skip((int)offset)
                .Take(pageSize)
                .Select(item => new KnowledgeDocumentRevisionListItemResponse(
                    item.Id,
                    item.RevisionNumber,
                    item.RevisionOrigin.ToString(),
                    item.LifecycleContext.ToString(),
                    item.AuthorUserId,
                    item.AuthorDisplayNameSnapshot,
                    item.CreatedAt,
                    item.ChangeSummary,
                    item.RestoreReason,
                    item.RestoredFromRevisionNumber,
                    item.RevisionNumber == document.CurrentRevisionNumber,
                    document.LatestPublishedRevisionNumber != null
                        && item.RevisionNumber == document.LatestPublishedRevisionNumber))
                .ToArrayAsync(cancellationToken);

        return new KnowledgeDocumentRevisionListQueryResult(
            new KnowledgeDocumentRevisionListResponse(owner, items, page, pageSize, total),
            null,
            true);
    }

    public async Task<KnowledgeDocumentRevisionDetailResponse?> GetRevisionDetail(
        long knowledgeDocumentId,
        long revisionNumber,
        CancellationToken cancellationToken)
    {
        var owner = await historicalTargetResolver.Resolve(
            KnowledgeTargetType.KnowledgeDocument,
            knowledgeDocumentId,
            cancellationToken);
        if (owner is null) return null;

        var document = await dbContext.KnowledgeDocuments.IgnoreQueryFilters().AsNoTracking()
            .Where(item => item.Id == knowledgeDocumentId)
            .Select(item => new
            {
                item.CurrentRevisionNumber,
                item.LatestPublishedRevisionNumber,
            })
            .SingleOrDefaultAsync(cancellationToken);
        if (document is null) return null;
        return await dbContext.KnowledgeDocumentRevisions.AsNoTracking()
            .Where(item => item.KnowledgeDocumentId == knowledgeDocumentId
                && item.RevisionNumber == revisionNumber)
            .Select(item => new KnowledgeDocumentRevisionDetailResponse(
                owner,
                item.Id,
                item.KnowledgeDocumentId,
                item.RevisionNumber,
                item.RevisionOrigin.ToString(),
                item.LifecycleContext.ToString(),
                item.AuthorUserId,
                item.AuthorDisplayNameSnapshot,
                item.CreatedAt,
                item.ChangeSummary,
                item.RestoreReason,
                item.RestoredFromRevisionNumber,
                item.RevisionNumber == document.CurrentRevisionNumber,
                document.LatestPublishedRevisionNumber != null
                    && item.RevisionNumber == document.LatestPublishedRevisionNumber,
                item.Title,
                item.Summary,
                item.BodyMarkdown))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<KnowledgeDocumentDetailResponse> ToDetail(
        KnowledgeDocument item,
        CancellationToken cancellationToken)
    {
        var confirmation = await dbContext.Evidence.AsNoTracking()
            .Where(evidence => evidence.EvidenceType == EvidenceType.HumanConfirmation
                && evidence.SubjectType == EvidenceSubjectType.KnowledgeDocument
                && evidence.SubjectId == item.Id)
            .GroupBy(_ => 1)
            .Select(group => new
            {
                Count = group.Count(),
                LastConfirmedRevisionNumber = group.Max(evidence => evidence.KnowledgeDocumentRevisionNumberSnapshot),
            })
            .SingleOrDefaultAsync(cancellationToken);

        var coverage = confirmation switch
        {
            null => new KnowledgeDocumentConfirmationCoverageResponse("NoConfirmation", null),
            { LastConfirmedRevisionNumber: null } => new KnowledgeDocumentConfirmationCoverageResponse("LegacyConfirmationUnknown", null),
            { LastConfirmedRevisionNumber: var revision } when revision == item.CurrentRevisionNumber =>
                new KnowledgeDocumentConfirmationCoverageResponse("CurrentRevisionConfirmed", revision),
            { LastConfirmedRevisionNumber: var revision } when revision < item.CurrentRevisionNumber =>
                new KnowledgeDocumentConfirmationCoverageResponse("ChangedSinceConfirmation", revision),
            _ => throw new InvalidOperationException(
                $"KnowledgeDocument {item.Id} has a HumanConfirmation snapshot newer than current revision {item.CurrentRevisionNumber}."),
        };

        return new KnowledgeDocumentDetailResponse(
            item.Id,
            item.DocumentType.ToString(),
            item.Title,
            item.Summary,
            item.BodyMarkdown,
            item.LifecycleStatus.ToString(),
            item.KnowledgeStatus.ToString(),
            item.CurrentRevisionNumber,
            item.LatestPublishedRevisionNumber,
            coverage,
            item.CreatedByUserId,
            item.CreatedByDisplayName,
            item.UpdatedByUserId,
            item.UpdatedByDisplayName,
            item.CreatedAt,
            item.UpdatedAt,
            item.PublishedAt,
            item.ArchivedAt,
            concurrencyTokenCodec.Encode(item.Version));
    }

    private static Dictionary<string, string[]> Validate(
        KnowledgeDocumentListQuery request,
        out DocumentType? documentType,
        out DocumentLifecycleStatus? lifecycleStatus,
        out KnowledgeStatus? knowledgeStatus,
        out KnowledgeDocumentSort sort)
    {
        var errors = new Dictionary<string, string[]>();
        documentType = null;
        lifecycleStatus = null;
        knowledgeStatus = null;
        sort = KnowledgeDocumentSort.UpdatedAtDescending;
        if (request.Page is < 1) errors["page"] = ["页码必须从 1 开始。"];
        if (request.PageSize is < 1 or > MaximumPageSize) errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"];
        if (request.DocumentType is not null && !TryParseEnum(request.DocumentType, out documentType)) errors["documentType"] = ["文档类型筛选值无效。"];
        if (request.LifecycleStatus is not null && !TryParseEnum(request.LifecycleStatus, out lifecycleStatus)) errors["lifecycleStatus"] = ["文档生命周期筛选值无效。"];
        if (request.KnowledgeStatus is not null && !TryParseEnum(request.KnowledgeStatus, out knowledgeStatus)) errors["knowledgeStatus"] = ["知识状态筛选值无效。"];
        if (request.Sort is not null && !TryParseSort(request.Sort, out sort)) errors["sort"] = ["排序值无效。"];
        return errors;
    }

    private static bool TryParseEnum<TEnum>(string value, out TEnum? result) where TEnum : struct, Enum
    {
        if (Enum.TryParse<TEnum>(value, false, out var parsed) && parsed.ToString() == value)
        {
            result = parsed;
            return true;
        }
        result = null;
        return false;
    }

    private static bool TryParseSort(string value, out KnowledgeDocumentSort sort)
    {
        sort = value switch
        {
            "updatedAt:asc" => KnowledgeDocumentSort.UpdatedAtAscending,
            "updatedAt:desc" => KnowledgeDocumentSort.UpdatedAtDescending,
            "title:asc" => KnowledgeDocumentSort.TitleAscending,
            "createdAt:desc" => KnowledgeDocumentSort.CreatedAtDescending,
            _ => KnowledgeDocumentSort.Invalid,
        };
        return sort != KnowledgeDocumentSort.Invalid;
    }

    private static IEnumerable<KnowledgeDocumentListItemResponse> ApplySort(
        IReadOnlyList<KnowledgeDocumentListItemResponse> rows,
        KnowledgeDocumentSort sort) => sort switch
    {
        KnowledgeDocumentSort.UpdatedAtAscending => rows.OrderBy(item => item.UpdatedAt).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
        KnowledgeDocumentSort.TitleAscending => rows.OrderBy(item => item.Title, StringComparer.OrdinalIgnoreCase).ThenByDescending(item => item.UpdatedAt),
        KnowledgeDocumentSort.CreatedAtDescending => rows.OrderByDescending(item => item.CreatedAt).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
        _ => rows.OrderByDescending(item => item.UpdatedAt).ThenBy(item => item.Title, StringComparer.OrdinalIgnoreCase),
    };

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private enum KnowledgeDocumentSort { Invalid, UpdatedAtAscending, UpdatedAtDescending, TitleAscending, CreatedAtDescending }
}
