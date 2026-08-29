using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Attachments.Application;

public sealed partial class AttachmentReferenceService(
    KnowledgeHubDbContext dbContext,
    AttachmentStorage storage)
{
    private const long MaximumJavaScriptSafeInteger = 9_007_199_254_740_991;

    public async Task<AttachmentSelectionResult> ResolveForContentSave(
        long knowledgeDocumentId,
        long currentRevisionNumber,
        string bodyMarkdown,
        IReadOnlyList<long>? requestedFileAttachmentIds,
        CancellationToken cancellationToken)
    {
        var errors = new Dictionary<string, string[]>();
        if (requestedFileAttachmentIds is not null
            && requestedFileAttachmentIds.Any(id => id <= 0 || id > MaximumJavaScriptSafeInteger))
        {
            errors["fileAttachmentIds"] = ["普通附件 ID 必须是 JavaScript 安全范围内的正整数。"];
            return new AttachmentSelectionResult(null, errors, AttachmentSelectionFailure.Validation);
        }

        var imageIds = new HashSet<long>();
        foreach (Match match in MarkdownImageTokenPattern().Matches(bodyMarkdown))
        {
            if (!long.TryParse(match.Groups[1].Value, out var id) || id > MaximumJavaScriptSafeInteger)
            {
                errors["bodyMarkdown"] = ["Markdown 图片附件 ID 超出允许范围。"];
                return new AttachmentSelectionResult(null, errors, AttachmentSelectionFailure.Validation);
            }
            imageIds.Add(id);
        }

        var currentRevisionId = await dbContext.KnowledgeDocumentRevisions.AsNoTracking()
            .Where(revision => revision.KnowledgeDocumentId == knowledgeDocumentId
                && revision.RevisionNumber == currentRevisionNumber)
            .Select(revision => revision.Id)
            .SingleAsync(cancellationToken);
        var currentAttachments = await dbContext.AttachmentReferences.AsNoTracking()
            .Where(reference => reference.KnowledgeDocumentRevisionId == currentRevisionId)
            .Join(
                dbContext.Attachments.AsNoTracking(),
                reference => reference.AttachmentId,
                attachment => attachment.Id,
                (_, attachment) => attachment)
            .ToArrayAsync(cancellationToken);

        var fileIds = requestedFileAttachmentIds is null
            ? currentAttachments.Where(attachment => attachment.Kind == AttachmentKind.File).Select(attachment => attachment.Id).ToHashSet()
            : requestedFileAttachmentIds.ToHashSet();
        var desiredIds = imageIds.Concat(fileIds).ToHashSet();
        var desired = desiredIds.Count == 0
            ? []
            : await dbContext.Attachments.AsNoTracking()
                .Where(attachment => desiredIds.Contains(attachment.Id))
                .ToArrayAsync(cancellationToken);

        if (desired.Length != desiredIds.Count
            || desired.Any(attachment => attachment.KnowledgeDocumentId != knowledgeDocumentId))
        {
            errors["attachments"] = ["一个或多个附件不存在或不属于当前知识文档。"];
        }
        if (desired.Any(attachment => imageIds.Contains(attachment.Id) && attachment.Kind != AttachmentKind.Image))
        {
            errors["bodyMarkdown"] = ["Markdown attachment 图片标记只能引用图片附件。"];
        }
        if (desired.Any(attachment => fileIds.Contains(attachment.Id) && attachment.Kind != AttachmentKind.File))
        {
            errors["fileAttachmentIds"] = ["普通附件列表只能引用 File 类型附件。"];
        }
        if (errors.Count > 0)
        {
            return new AttachmentSelectionResult(null, errors, AttachmentSelectionFailure.ReferenceInvalid);
        }
        if (desired.Any(attachment => attachment.StorageState != AttachmentStorageState.Ready))
        {
            return new AttachmentSelectionResult(
                null,
                new Dictionary<string, string[]> { ["attachments"] = ["一个或多个附件不再处于可引用状态。"] },
                AttachmentSelectionFailure.ReferenceInvalid);
        }

        return new AttachmentSelectionResult(
            new AttachmentSelection(
                currentAttachments.Select(attachment => attachment.Id).Order().ToArray(),
                desired.OrderBy(attachment => attachment.Id).ToArray()),
            null,
            AttachmentSelectionFailure.None);
    }

    public async Task<RestoreAttachmentSelectionResult> ResolveForRestore(
        long knowledgeDocumentId,
        long currentRevisionNumber,
        long sourceRevisionId,
        CancellationToken cancellationToken)
    {
        var currentIds = await GetAttachmentIds(
            knowledgeDocumentId,
            currentRevisionNumber,
            cancellationToken);
        var sourceAttachments = await dbContext.AttachmentReferences.AsNoTracking()
            .Where(reference => reference.KnowledgeDocumentRevisionId == sourceRevisionId
                && reference.KnowledgeDocumentId == knowledgeDocumentId)
            .Join(
                dbContext.Attachments.AsNoTracking(),
                reference => reference.AttachmentId,
                attachment => attachment.Id,
                (_, attachment) => attachment)
            .OrderBy(attachment => attachment.Id)
            .ToArrayAsync(cancellationToken);
        if (sourceAttachments.Any(attachment => attachment.StorageState != AttachmentStorageState.Ready))
        {
            return new RestoreAttachmentSelectionResult(null, true);
        }
        foreach (var attachment in sourceAttachments)
        {
            if (!await storage.Verify(attachment, cancellationToken))
            {
                return new RestoreAttachmentSelectionResult(null, true);
            }
        }
        return new RestoreAttachmentSelectionResult(
            new AttachmentSelection(currentIds, sourceAttachments),
            false);
    }

    public void AddSnapshot(
        long knowledgeDocumentId,
        long knowledgeDocumentRevisionId,
        IEnumerable<Attachment> attachments)
    {
        dbContext.AttachmentReferences.AddRange(attachments.Select(attachment => new AttachmentReference
        {
            KnowledgeDocumentId = knowledgeDocumentId,
            KnowledgeDocumentRevisionId = knowledgeDocumentRevisionId,
            AttachmentId = attachment.Id,
        }));
    }

    private async Task<long[]> GetAttachmentIds(
        long knowledgeDocumentId,
        long revisionNumber,
        CancellationToken cancellationToken)
    {
        return await dbContext.KnowledgeDocumentRevisions.AsNoTracking()
            .Where(revision => revision.KnowledgeDocumentId == knowledgeDocumentId
                && revision.RevisionNumber == revisionNumber)
            .Join(
                dbContext.AttachmentReferences.AsNoTracking(),
                revision => revision.Id,
                reference => reference.KnowledgeDocumentRevisionId,
                (_, reference) => reference.AttachmentId)
            .OrderBy(id => id)
            .ToArrayAsync(cancellationToken);
    }

    [GeneratedRegex(@"!\[[^\]\r\n]*\]\(attachment:([1-9][0-9]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownImageTokenPattern();
}

public sealed record AttachmentSelection(
    IReadOnlyList<long> CurrentAttachmentIds,
    IReadOnlyList<Attachment> DesiredAttachments)
{
    public bool HasSameSet => CurrentAttachmentIds.SequenceEqual(DesiredAttachments.Select(attachment => attachment.Id));
}

public sealed record AttachmentSelectionResult(
    AttachmentSelection? Selection,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    AttachmentSelectionFailure Failure);

public sealed record RestoreAttachmentSelectionResult(
    AttachmentSelection? Selection,
    bool Unavailable);

public enum AttachmentSelectionFailure
{
    None,
    Validation,
    ReferenceInvalid,
}
