using Microsoft.EntityFrameworkCore;
using System.Text;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;

public sealed class KnowledgeDocumentService(
    KnowledgeHubDbContext dbContext,
    KnowledgeDocumentQueries queries,
    ConcurrencyTokenCodec concurrencyTokenCodec,
    KnowledgeDocumentSearchIndex searchIndex)
{
    public const int TitleMaximumLength = 300;
    public const int SummaryMaximumLength = 2_000;
    public const int BodyMarkdownMaximumLength = 1_000_000;
    public const int ChangeSummaryMaximumLength = 500;
    public const int RestoreReasonMinimumLength = 5;
    public const int RestoreReasonMaximumLength = 500;

    public async Task<KnowledgeDocumentWriteResult> Create(
        CreateKnowledgeDocumentCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateContent(request.DocumentType, request.Title, request.Summary, request.BodyMarkdown, out var documentType, out var title, out var summary, out var bodyMarkdown);
        if (errors.Count > 0) return new KnowledgeDocumentWriteResult(null, errors, KnowledgeDocumentWriteFailure.Validation);

        var timestamp = DateTimeOffset.UtcNow;
        var document = new KnowledgeDocument
        {
            DocumentType = documentType,
            Title = title,
            Summary = summary,
            BodyMarkdown = bodyMarkdown,
            LifecycleStatus = DocumentLifecycleStatus.Draft,
            KnowledgeStatus = KnowledgeStatus.Unknown,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = request.Author.DisplayName,
            KnowledgeStatusChangedByRole = "创建人",
            CreatedByUserId = request.Author.UserId,
            CreatedByDisplayName = request.Author.DisplayName,
            UpdatedByUserId = request.Author.UserId,
            UpdatedByDisplayName = request.Author.DisplayName,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            CurrentRevisionNumber = 1,
            LatestPublishedRevisionNumber = null,
            Version = 1,
        };
        dbContext.KnowledgeDocuments.Add(document);
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
        await dbContext.SaveChangesAsync(cancellationToken);
        dbContext.KnowledgeDocumentRevisions.Add(CreateRevision(
            document,
            1,
            request.Author,
            timestamp,
            RevisionOrigin.Created,
            null));
        await dbContext.SaveChangesAsync(cancellationToken);
        await searchIndex.Upsert(document, cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return new KnowledgeDocumentWriteResult(await queries.ToDetail(document, cancellationToken), null, KnowledgeDocumentWriteFailure.None);
    }

    public async Task<KnowledgeDocumentWriteResult> UpdateContent(
        UpdateKnowledgeDocumentContentCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateContent(null, request.Title, request.Summary, request.BodyMarkdown, out _, out var title, out var summary, out var bodyMarkdown);
        var changeSummary = NormalizeOptional(request.ChangeSummary);
        if (changeSummary?.Length > ChangeSummaryMaximumLength)
        {
            errors["changeSummary"] = [$"修订说明不能超过 {ChangeSummaryMaximumLength} 个字符。"];
        }
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0) return new KnowledgeDocumentWriteResult(null, errors, KnowledgeDocumentWriteFailure.Validation);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var document = await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(item => item.Id == request.KnowledgeDocumentId, cancellationToken);
        if (document is null) return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.NotFound);
        if (document.Version != expectedVersion) return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.Conflict);
        if (document.LifecycleStatus == DocumentLifecycleStatus.Archived)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.InvalidState);
        }
        if (string.Equals(document.Title, title, StringComparison.Ordinal)
            && string.Equals(document.Summary, summary, StringComparison.Ordinal)
            && string.Equals(document.BodyMarkdown, bodyMarkdown, StringComparison.Ordinal))
        {
            return new KnowledgeDocumentWriteResult(
                await queries.ToDetail(document, cancellationToken),
                null,
                KnowledgeDocumentWriteFailure.None);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var nextRevisionNumber = checked(document.CurrentRevisionNumber + 1);
        document.Title = title;
        document.Summary = summary;
        document.BodyMarkdown = bodyMarkdown;
        document.UpdatedByUserId = request.Author.UserId;
        document.UpdatedByDisplayName = request.Author.DisplayName;
        document.UpdatedAt = timestamp;
        document.CurrentRevisionNumber = nextRevisionNumber;
        if (document.LifecycleStatus == DocumentLifecycleStatus.Published)
        {
            document.LatestPublishedRevisionNumber = nextRevisionNumber;
            document.PublishedAt = timestamp;
        }
        document.Version = expectedVersion + 1;
        dbContext.KnowledgeDocumentRevisions.Add(CreateRevision(
            document,
            nextRevisionNumber,
            request.Author,
            timestamp,
            RevisionOrigin.ContentSave,
            changeSummary));
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await searchIndex.Upsert(document, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.Conflict);
        }
        return new KnowledgeDocumentWriteResult(await queries.ToDetail(document, cancellationToken), null, KnowledgeDocumentWriteFailure.None);
    }

    public async Task<KnowledgeDocumentWriteResult> UpdateLifecycle(
        UpdateKnowledgeDocumentLifecycleCommand request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (!Enum.TryParse<DocumentLifecycleStatus>(request.TargetLifecycleStatus, false, out var target)
            || target.ToString() != request.TargetLifecycleStatus)
        {
            errors["targetLifecycleStatus"] = ["文档生命周期状态无效。"];
        }
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
        {
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        }
        if (errors.Count > 0) return new KnowledgeDocumentWriteResult(null, errors, KnowledgeDocumentWriteFailure.Validation);

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var document = await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(item => item.Id == request.KnowledgeDocumentId, cancellationToken);
        if (document is null) return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.NotFound);
        if (document.Version != expectedVersion) return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.Conflict);
        if (!IsAllowedLifecycleTransition(document.LifecycleStatus, target))
        {
            return new KnowledgeDocumentWriteResult(null, new Dictionary<string, string[]>
            {
                ["targetLifecycleStatus"] = [$"不允许从 {document.LifecycleStatus} 转换到 {target}。"],
            }, KnowledgeDocumentWriteFailure.Validation);
        }
        if (target == DocumentLifecycleStatus.Published)
        {
            if (string.IsNullOrWhiteSpace(document.Title)) errors["title"] = ["发布前标题不能为空。"];
            if (string.IsNullOrWhiteSpace(document.BodyMarkdown)) errors["bodyMarkdown"] = ["发布前正文不能为空。"];
            if (errors.Count > 0) return new KnowledgeDocumentWriteResult(null, errors, KnowledgeDocumentWriteFailure.Validation);
        }

        var timestamp = DateTimeOffset.UtcNow;
        document.LifecycleStatus = target;
        if (target == DocumentLifecycleStatus.Published)
        {
            document.LatestPublishedRevisionNumber = document.CurrentRevisionNumber;
            document.PublishedAt = timestamp;
        }
        document.ArchivedAt = target == DocumentLifecycleStatus.Archived ? timestamp : null;
        document.UpdatedByUserId = request.Author.UserId;
        document.UpdatedByDisplayName = request.Author.DisplayName;
        document.UpdatedAt = timestamp;
        document.Version = expectedVersion + 1;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.Conflict);
        }
        await transaction.CommitAsync(cancellationToken);
        return new KnowledgeDocumentWriteResult(await queries.ToDetail(document, cancellationToken), null, KnowledgeDocumentWriteFailure.None);
    }

    public async Task<KnowledgeDocumentWriteResult> RestoreRevision(
        RestoreKnowledgeDocumentRevisionCommand request,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        var reason = NormalizeOptional(request.Reason);
        var reasonLength = reason is null ? 0 : reason.EnumerateRunes().Count();
        if (reason?.Contains('\0') == true)
        {
            errors["reason"] = ["恢复原因不能包含 NUL 字符。"];
        }
        else if (reasonLength < RestoreReasonMinimumLength)
        {
            errors["reason"] = [$"恢复原因至少需要 {RestoreReasonMinimumLength} 个字符。"];
        }
        else if (reasonLength > RestoreReasonMaximumLength)
        {
            errors["reason"] = [$"恢复原因不能超过 {RestoreReasonMaximumLength} 个字符。"];
        }
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion))
        {
            errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        }
        if (errors.Count > 0)
        {
            return new KnowledgeDocumentWriteResult(null, errors, KnowledgeDocumentWriteFailure.Validation);
        }

        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);

        var document = await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(
            item => item.Id == request.KnowledgeDocumentId,
            cancellationToken);
        if (document is null)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.NotFound);
        }

        var source = await dbContext.KnowledgeDocumentRevisions.AsNoTracking().SingleOrDefaultAsync(
            item => item.KnowledgeDocumentId == request.KnowledgeDocumentId
                && item.RevisionNumber == request.SourceRevisionNumber,
            cancellationToken);
        if (source is null)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.NotFound);
        }
        if (document.Version != expectedVersion)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.Conflict);
        }
        if (document.LifecycleStatus != DocumentLifecycleStatus.Draft)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.InvalidState);
        }
        if (source.RevisionNumber >= document.CurrentRevisionNumber
            || (string.Equals(document.Title, source.Title, StringComparison.Ordinal)
                && string.Equals(document.Summary, source.Summary, StringComparison.Ordinal)
                && string.Equals(document.BodyMarkdown, source.BodyMarkdown, StringComparison.Ordinal)))
        {
            return new KnowledgeDocumentWriteResult(
                null,
                null,
                KnowledgeDocumentWriteFailure.BusinessRuleViolation);
        }

        var timestamp = DateTimeOffset.UtcNow;
        var nextRevisionNumber = checked(document.CurrentRevisionNumber + 1);
        document.Title = source.Title;
        document.Summary = source.Summary;
        document.BodyMarkdown = source.BodyMarkdown;
        document.UpdatedByUserId = request.Author.UserId;
        document.UpdatedByDisplayName = request.Author.DisplayName;
        document.UpdatedAt = timestamp;
        document.CurrentRevisionNumber = nextRevisionNumber;
        document.Version = expectedVersion + 1;
        dbContext.KnowledgeDocumentRevisions.Add(CreateRevision(
            document,
            nextRevisionNumber,
            request.Author,
            timestamp,
            RevisionOrigin.Restore,
            null,
            reason,
            source.RevisionNumber));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            await searchIndex.Upsert(document, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.Conflict);
        }

        return new KnowledgeDocumentWriteResult(
            await queries.ToDetail(document, cancellationToken),
            null,
            KnowledgeDocumentWriteFailure.None);
    }

    private static KnowledgeDocumentRevision CreateRevision(
        KnowledgeDocument document,
        long revisionNumber,
        KnowledgeDocumentAuthor author,
        DateTimeOffset timestamp,
        RevisionOrigin origin,
        string? changeSummary,
        string? restoreReason = null,
        long? restoredFromRevisionNumber = null) => new()
    {
        KnowledgeDocumentId = document.Id,
        RevisionNumber = revisionNumber,
        Title = document.Title,
        Summary = document.Summary,
        BodyMarkdown = document.BodyMarkdown,
        AuthorUserId = author.UserId,
        AuthorDisplayNameSnapshot = author.DisplayName,
        CreatedAt = timestamp,
        LifecycleContext = document.LifecycleStatus,
        ChangeSummary = changeSummary,
        RestoreReason = restoreReason,
        RestoredFromRevisionNumber = restoredFromRevisionNumber,
        RevisionOrigin = origin,
    };

    private static bool IsAllowedLifecycleTransition(DocumentLifecycleStatus current, DocumentLifecycleStatus target) =>
        (current, target) switch
        {
            (DocumentLifecycleStatus.Draft, DocumentLifecycleStatus.Published) => true,
            (DocumentLifecycleStatus.Published, DocumentLifecycleStatus.Draft) => true,
            (DocumentLifecycleStatus.Published, DocumentLifecycleStatus.Archived) => true,
            (DocumentLifecycleStatus.Archived, DocumentLifecycleStatus.Draft) => true,
            _ => false,
        };

    private static Dictionary<string, string[]> ValidateContent(
        string? documentTypeValue,
        string titleValue,
        string? summaryValue,
        string? bodyValue,
        out DocumentType documentType,
        out string title,
        out string? summary,
        out string bodyMarkdown)
    {
        var errors = new Dictionary<string, string[]>();
        documentType = default;
        title = titleValue.Trim();
        summary = NormalizeOptional(summaryValue);
        bodyMarkdown = NormalizeBody(bodyValue);
        if (documentTypeValue is not null && (!Enum.TryParse<DocumentType>(documentTypeValue, false, out documentType) || documentType.ToString() != documentTypeValue)) errors["documentType"] = ["文档类型无效。"];
        if (string.IsNullOrWhiteSpace(title)) errors["title"] = ["标题不能为空。"];
        else if (title.Length > TitleMaximumLength) errors["title"] = [$"标题不能超过 {TitleMaximumLength} 个字符。"];
        if (summary?.Length > SummaryMaximumLength) errors["summary"] = [$"摘要不能超过 {SummaryMaximumLength} 个字符。"];
        if (bodyMarkdown.Length > BodyMarkdownMaximumLength) errors["bodyMarkdown"] = [$"Markdown 正文不能超过 {BodyMarkdownMaximumLength} 个字符。"];
        return errors;
    }

    private static string? NormalizeOptional(string? value)
    {
        var normalized = value?.Trim();
        return string.IsNullOrEmpty(normalized) ? null : normalized;
    }

    private static string NormalizeBody(string? value) => (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal).Replace("\r", "\n", StringComparison.Ordinal);
}
