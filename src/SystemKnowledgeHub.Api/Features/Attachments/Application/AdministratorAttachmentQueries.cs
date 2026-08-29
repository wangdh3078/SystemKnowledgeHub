using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Attachments.Application.Models;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.Attachments.Application;

public sealed class AdministratorAttachmentQueries(
    KnowledgeHubDbContext dbContext,
    AttachmentStorage storage,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    private const int DefaultPageSize = 20;
    private const int MaximumPageSize = 100;
    private const int MaximumReferenceSummaries = 100;
    private const int StatisticsItemLimit = 5;
    private const int RecentWindowDays = 7;

    public async Task<AdministratorAttachmentListResult> GetList(
        AdministratorAttachmentListQuery request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateList(request, out var kind, out var storageState, out var page, out var pageSize);
        if (errors.Count > 0) return new AdministratorAttachmentListResult(null, errors);

        var query = BuildRows();
        var search = request.Query?.Trim();
        if (!string.IsNullOrEmpty(search))
        {
            query = query.Where(row => row.OriginalFileName.Contains(search));
        }
        if (kind.HasValue)
        {
            query = query.Where(row => row.Kind == kind.Value);
        }
        var extension = request.Extension?.Trim().ToLowerInvariant();
        if (!string.IsNullOrEmpty(extension))
        {
            query = query.Where(row => row.Extension == extension);
        }
        if (storageState.HasValue)
        {
            query = query.Where(row => row.StorageState == storageState.Value);
        }
        query = request.ReferenceStatus switch
        {
            "Referenced" => query.Where(row => row.ReferenceCount > 0),
            "Orphan" => query.Where(row => row.ReferenceCount == 0),
            "Current" => query.Where(row => row.CurrentReferenceCount > 0),
            "HistoricalOnly" => query.Where(row => row.ReferenceCount > 0 && row.CurrentReferenceCount == 0),
            _ => query,
        };

        var total = await query.LongCountAsync(cancellationToken);
        var offset = checked((page - 1) * pageSize);
        var rows = await query
            .OrderByDescending(row => row.AttachmentId)
            .Skip(offset)
            .Take(pageSize)
            .ToArrayAsync(cancellationToken);

        var items = rows.Select(ToListItem).ToArray();
        return new AdministratorAttachmentListResult(
            new AdministratorAttachmentListResponse(items, page, pageSize, total),
            null);
    }

    public async Task<AdministratorAttachmentDetailResult> GetDetail(
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var row = await BuildRows().SingleOrDefaultAsync(
            item => item.AttachmentId == attachmentId,
            cancellationToken);
        if (row is null)
        {
            return new AdministratorAttachmentDetailResult(null, AttachmentFailure.NotFound);
        }

        var references = await (
            from reference in dbContext.AttachmentReferences.AsNoTracking()
            join revision in dbContext.KnowledgeDocumentRevisions.AsNoTracking()
                on reference.KnowledgeDocumentRevisionId equals revision.Id
            where reference.AttachmentId == attachmentId
            orderby revision.RevisionNumber descending
            select new AdministratorAttachmentReferenceResponse(
                revision.RevisionNumber,
                revision.RevisionNumber == row.OwnerCurrentRevisionNumber,
                revision.CreatedAt))
            .Take(MaximumReferenceSummaries + 1)
            .ToArrayAsync(cancellationToken);
        var referencesTruncated = references.Length > MaximumReferenceSummaries;
        if (referencesTruncated) references = references[..MaximumReferenceSummaries];

        var attachment = ToAttachment(row);
        var mode = AttachmentFilePolicy.GetPreviewMode(attachment);
        var health = storage.InspectShallow(attachment);
        return new AdministratorAttachmentDetailResult(
            new AdministratorAttachmentDetailResponse(
                attachment.Id,
                attachment.OriginalFileName,
                attachment.Extension,
                attachment.Kind.ToString(),
                attachment.ContentType,
                attachment.SizeBytes,
                Convert.ToHexString(attachment.Sha256).ToLowerInvariant(),
                attachment.CreatedAt,
                attachment.CreatedByUserId,
                attachment.CreatedByDisplayNameSnapshot,
                attachment.StorageState.ToString(),
                health.Status.ToString(),
                mode.ToString(),
                mode != PreviewMode.None,
                concurrencyTokenCodec.Encode(attachment.Version),
                ToOwner(row),
                row.ReferenceCount,
                row.CurrentReferenceCount,
                row.ReferenceCount - row.CurrentReferenceCount,
                GetReferenceStatus(row.ReferenceCount, row.CurrentReferenceCount),
                references,
                referencesTruncated),
            AttachmentFailure.None);
    }

    public async Task<AdministratorAttachmentStatisticsResponse> GetStatistics(
        CancellationToken cancellationToken)
    {
        var attachments = dbContext.Attachments.AsNoTracking();
        var rows = BuildRows();
        var totalCount = await attachments.LongCountAsync(cancellationToken);
        var totalSizeBytes = await SumBytes(attachments, cancellationToken);
        var images = attachments.Where(item => item.Kind == AttachmentKind.Image);
        var files = attachments.Where(item => item.Kind == AttachmentKind.File);
        var orphans = rows.Where(item => item.ReferenceCount == 0);
        var imageCount = await images.LongCountAsync(cancellationToken);
        var imageSizeBytes = await SumBytes(images, cancellationToken);
        var fileCount = await files.LongCountAsync(cancellationToken);
        var fileSizeBytes = await SumBytes(files, cancellationToken);
        var orphanCount = await orphans.LongCountAsync(cancellationToken);
        var orphanSizeBytes = await orphans.SumAsync(
            item => (long?)item.SizeBytes,
            cancellationToken) ?? 0;
        var referencedCount = totalCount - orphanCount;
        var currentReferencedCount = await rows.LongCountAsync(
            item => item.CurrentReferenceCount > 0,
            cancellationToken);
        var historicalOnlyCount = await rows.LongCountAsync(
            item => item.ReferenceCount > 0 && item.CurrentReferenceCount == 0,
            cancellationToken);
        var deletedOwnerCount = await rows.LongCountAsync(item => item.OwnerIsDeleted, cancellationToken);
        var readyCount = await attachments.LongCountAsync(
            item => item.StorageState == AttachmentStorageState.Ready,
            cancellationToken);
        var deletePendingCount = totalCount - readyCount;
        var recentSince = DateTimeOffset.UtcNow.AddDays(-RecentWindowDays);
        var createdAtValues = await attachments
            .Select(item => item.CreatedAt)
            .ToArrayAsync(cancellationToken);
        var recentUploadCount = createdAtValues.LongCount(item => item >= recentSince);
        var largestRows = await attachments
            .OrderByDescending(item => item.SizeBytes)
            .ThenByDescending(item => item.Id)
            .Take(StatisticsItemLimit)
            .Select(item => new { item.Id, item.OriginalFileName, item.Kind, item.SizeBytes, item.CreatedAt })
            .ToArrayAsync(cancellationToken);
        var recentRows = await attachments
            .OrderByDescending(item => item.Id)
            .Take(StatisticsItemLimit)
            .Select(item => new { item.Id, item.OriginalFileName, item.Kind, item.SizeBytes, item.CreatedAt })
            .ToArrayAsync(cancellationToken);
        var largest = largestRows.Select(item => new AdministratorAttachmentStatisticItemResponse(
            item.Id,
            item.OriginalFileName,
            item.Kind.ToString(),
            item.SizeBytes,
            item.CreatedAt)).ToArray();
        var recent = recentRows.Select(item => new AdministratorAttachmentStatisticItemResponse(
            item.Id,
            item.OriginalFileName,
            item.Kind.ToString(),
            item.SizeBytes,
            item.CreatedAt)).ToArray();

        return new AdministratorAttachmentStatisticsResponse(
            totalCount,
            totalSizeBytes,
            imageCount,
            imageSizeBytes,
            fileCount,
            fileSizeBytes,
            orphanCount,
            orphanSizeBytes,
            referencedCount,
            currentReferencedCount,
            historicalOnlyCount,
            deletedOwnerCount,
            readyCount,
            deletePendingCount,
            RecentWindowDays,
            recentUploadCount,
            largest,
            recent);
    }

    private IQueryable<AdministratorAttachmentDatabaseRow> BuildRows() =>
        from attachment in dbContext.Attachments.AsNoTracking()
        join owner in dbContext.KnowledgeDocuments.IgnoreQueryFilters().AsNoTracking()
            on attachment.KnowledgeDocumentId equals owner.Id
        select new AdministratorAttachmentDatabaseRow
        {
            AttachmentId = attachment.Id,
            KnowledgeDocumentId = attachment.KnowledgeDocumentId,
            OriginalFileName = attachment.OriginalFileName,
            Extension = attachment.Extension,
            Kind = attachment.Kind,
            ContentType = attachment.ContentType,
            SizeBytes = attachment.SizeBytes,
            StorageKey = attachment.StorageKey,
            Sha256 = attachment.Sha256,
            StorageState = attachment.StorageState,
            CreatedByUserId = attachment.CreatedByUserId,
            CreatedByDisplayName = attachment.CreatedByDisplayNameSnapshot,
            CreatedAt = attachment.CreatedAt,
            Version = attachment.Version,
            OwnerId = owner.Id,
            OwnerTitle = owner.Title,
            OwnerLifecycleStatus = owner.LifecycleStatus,
            OwnerIsDeleted = owner.IsDeleted,
            OwnerCurrentRevisionNumber = owner.CurrentRevisionNumber,
            ReferenceCount = dbContext.AttachmentReferences.Count(reference => reference.AttachmentId == attachment.Id),
            CurrentReferenceCount = (
                from reference in dbContext.AttachmentReferences
                join revision in dbContext.KnowledgeDocumentRevisions
                    on reference.KnowledgeDocumentRevisionId equals revision.Id
                where reference.AttachmentId == attachment.Id
                    && revision.RevisionNumber == owner.CurrentRevisionNumber
                select reference.Id).Count(),
        };

    private AdministratorAttachmentListItemResponse ToListItem(AdministratorAttachmentDatabaseRow row)
    {
        var attachment = ToAttachment(row);
        var mode = AttachmentFilePolicy.GetPreviewMode(attachment);
        return new AdministratorAttachmentListItemResponse(
            attachment.Id,
            attachment.OriginalFileName,
            attachment.Extension,
            attachment.Kind.ToString(),
            attachment.ContentType,
            attachment.SizeBytes,
            attachment.CreatedByDisplayNameSnapshot,
            attachment.CreatedAt,
            ToOwner(row),
            row.ReferenceCount,
            row.CurrentReferenceCount,
            row.ReferenceCount - row.CurrentReferenceCount,
            GetReferenceStatus(row.ReferenceCount, row.CurrentReferenceCount),
            attachment.StorageState.ToString(),
            storage.InspectShallow(attachment).Status.ToString(),
            mode.ToString(),
            mode != PreviewMode.None,
            Convert.ToHexString(attachment.Sha256).ToLowerInvariant());
    }

    private static AdministratorAttachmentOwnerResponse ToOwner(AdministratorAttachmentDatabaseRow row) =>
        new(row.OwnerId, row.OwnerTitle, row.OwnerLifecycleStatus.ToString(), row.OwnerIsDeleted);

    private static Attachment ToAttachment(AdministratorAttachmentDatabaseRow row) => new()
    {
        Id = row.AttachmentId,
        KnowledgeDocumentId = row.KnowledgeDocumentId,
        OriginalFileName = row.OriginalFileName,
        Extension = row.Extension,
        Kind = row.Kind,
        ContentType = row.ContentType,
        SizeBytes = row.SizeBytes,
        StorageKey = row.StorageKey,
        Sha256 = row.Sha256,
        StorageState = row.StorageState,
        CreatedByUserId = row.CreatedByUserId,
        CreatedByDisplayNameSnapshot = row.CreatedByDisplayName,
        CreatedAt = row.CreatedAt,
        Version = row.Version,
    };

    private static string GetReferenceStatus(int referenceCount, int currentReferenceCount) =>
        referenceCount == 0
            ? "Orphan"
            : currentReferenceCount > 0
                ? "Referenced"
                : "HistoricalOnly";

    private static async Task<long> SumBytes(
        IQueryable<Attachment> query,
        CancellationToken cancellationToken) =>
        await query.SumAsync(item => (long?)item.SizeBytes, cancellationToken) ?? 0;

    private static Dictionary<string, string[]> ValidateList(
        AdministratorAttachmentListQuery request,
        out AttachmentKind? kind,
        out AttachmentStorageState? storageState,
        out int page,
        out int pageSize)
    {
        var errors = new Dictionary<string, string[]>();
        kind = null;
        storageState = null;
        page = request.Page ?? 1;
        pageSize = request.PageSize ?? DefaultPageSize;
        var search = request.Query?.Trim();
        if (search?.Length > 255) errors["query"] = ["文件名搜索最多 255 个字符。"];
        if (!string.IsNullOrWhiteSpace(request.Kind))
        {
            if (!Enum.TryParse<AttachmentKind>(request.Kind, ignoreCase: false, out var parsedKind))
            {
                errors["kind"] = ["附件 Kind 必须为 Image 或 File。"];
            }
            else kind = parsedKind;
        }
        var extension = request.Extension?.Trim();
        if (!string.IsNullOrEmpty(extension)
            && (extension.Length is < 2 or > 16
                || extension[0] != '.'
                || extension.Skip(1).Any(character => !char.IsAsciiLetterOrDigit(character))))
        {
            errors["extension"] = ["扩展名必须是以点开头的安全字母或数字后缀。"];
        }
        if (!string.IsNullOrWhiteSpace(request.ReferenceStatus)
            && request.ReferenceStatus is not ("Referenced" or "Orphan" or "Current" or "HistoricalOnly"))
        {
            errors["referenceStatus"] = ["引用状态无效。"];
        }
        if (!string.IsNullOrWhiteSpace(request.StorageState))
        {
            if (!Enum.TryParse<AttachmentStorageState>(request.StorageState, ignoreCase: false, out var parsedState))
            {
                errors["storageState"] = ["存储状态必须为 Ready 或 DeletePending。"];
            }
            else storageState = parsedState;
        }
        if (page < 1 || (long)(page - 1) * pageSize > int.MaxValue)
        {
            errors["page"] = ["页码必须是有效的正整数。"];
        }
        if (pageSize is < 1 or > MaximumPageSize)
        {
            errors["pageSize"] = ["每页数量必须在 1 到 100 之间。"];
        }
        return errors;
    }

    private sealed class AdministratorAttachmentDatabaseRow
    {
        public long AttachmentId { get; init; }
        public long KnowledgeDocumentId { get; init; }
        public string OriginalFileName { get; init; } = string.Empty;
        public string Extension { get; init; } = string.Empty;
        public AttachmentKind Kind { get; init; }
        public string ContentType { get; init; } = string.Empty;
        public long SizeBytes { get; init; }
        public string StorageKey { get; init; } = string.Empty;
        public byte[] Sha256 { get; init; } = [];
        public AttachmentStorageState StorageState { get; init; }
        public long CreatedByUserId { get; init; }
        public string CreatedByDisplayName { get; init; } = string.Empty;
        public DateTimeOffset CreatedAt { get; init; }
        public long Version { get; init; }
        public long OwnerId { get; init; }
        public string OwnerTitle { get; init; } = string.Empty;
        public DocumentLifecycleStatus OwnerLifecycleStatus { get; init; }
        public bool OwnerIsDeleted { get; init; }
        public long OwnerCurrentRevisionNumber { get; init; }
        public int ReferenceCount { get; init; }
        public int CurrentReferenceCount { get; init; }
    }
}
