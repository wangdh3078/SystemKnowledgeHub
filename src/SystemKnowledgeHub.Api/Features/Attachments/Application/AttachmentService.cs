using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.Attachments.Application.Models;
using SystemKnowledgeHub.Api.Features.Attachments.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;

namespace SystemKnowledgeHub.Api.Features.Attachments.Application;

public sealed class AttachmentService(
    KnowledgeHubDbContext dbContext,
    AttachmentOptions options,
    AttachmentFilePolicy filePolicy,
    AttachmentStorage storage,
    AttachmentPreviewService previewService,
    ConcurrencyTokenCodec concurrencyTokenCodec,
    ILogger<AttachmentService> logger)
{
    public async Task<AttachmentUploadResult> Upload(
        long knowledgeDocumentId,
        string? originalFileName,
        string? declaredContentType,
        long expectedSizeBytes,
        Stream content,
        KnowledgeDocumentAuthor actor,
        CancellationToken cancellationToken)
    {
        AttachmentUploadDescriptor descriptor;
        try
        {
            descriptor = filePolicy.ValidateRequest(originalFileName, declaredContentType);
        }
        catch (AttachmentTypeRejectedException exception)
        {
            return Rejected(exception.Message);
        }

        var owner = await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(document => document.Id == knowledgeDocumentId)
            .Select(document => new { document.LifecycleStatus })
            .SingleOrDefaultAsync(cancellationToken);
        if (owner is null)
        {
            return new AttachmentUploadResult(null, null, AttachmentFailure.NotFound);
        }
        if (owner.LifecycleStatus == DocumentLifecycleStatus.Archived)
        {
            return new AttachmentUploadResult(null, null, AttachmentFailure.InvalidState);
        }
        if (await dbContext.Attachments.CountAsync(
            attachment => attachment.KnowledgeDocumentId == knowledgeDocumentId,
            cancellationToken) >= options.MaxStoredAttachmentsPerDocument)
        {
            return new AttachmentUploadResult(null, null, AttachmentFailure.InvalidState);
        }

        StagedAttachment? staged = null;
        string? storageKey = null;
        try
        {
            var maximumBytes = descriptor.Kind == AttachmentKind.Image
                ? options.MaxImageBytes
                : options.MaxFileBytes;
            if (expectedSizeBytes > maximumBytes)
            {
                return new AttachmentUploadResult(null, null, AttachmentFailure.PayloadTooLarge);
            }
            staged = await storage.Stage(content, maximumBytes, cancellationToken);
            if (staged.SizeBytes != expectedSizeBytes)
            {
                logger.LogWarning(
                    "Attachment multipart length mismatch: form file declared {ExpectedSizeBytes} bytes but staging received {StagedSizeBytes} bytes.",
                    expectedSizeBytes,
                    staged.SizeBytes);
                return new AttachmentUploadResult(
                    null,
                    new Dictionary<string, string[]>
                    {
                        ["file"] = ["附件内容长度与 multipart 文件长度不一致。"],
                    },
                    AttachmentFailure.Validation);
            }
            await filePolicy.ValidateContent(staged, descriptor, cancellationToken);
            storageKey = storage.Commit(staged);

            await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken);
            var document = await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(
                item => item.Id == knowledgeDocumentId,
                cancellationToken);
            if (document is null)
            {
                storage.DeleteCommitted(storageKey);
                return new AttachmentUploadResult(null, null, AttachmentFailure.NotFound);
            }
            if (document.LifecycleStatus == DocumentLifecycleStatus.Archived)
            {
                storage.DeleteCommitted(storageKey);
                return new AttachmentUploadResult(null, null, AttachmentFailure.InvalidState);
            }
            if (await dbContext.Attachments.CountAsync(
                attachment => attachment.KnowledgeDocumentId == knowledgeDocumentId,
                cancellationToken) >= options.MaxStoredAttachmentsPerDocument)
            {
                storage.DeleteCommitted(storageKey);
                return new AttachmentUploadResult(null, null, AttachmentFailure.InvalidState);
            }

            var attachment = new Attachment
            {
                KnowledgeDocumentId = knowledgeDocumentId,
                OriginalFileName = descriptor.OriginalFileName,
                Extension = descriptor.Extension,
                Kind = descriptor.Kind,
                ContentType = descriptor.ContentType,
                SizeBytes = staged.SizeBytes,
                StorageKey = storageKey,
                Sha256 = staged.Sha256,
                StorageState = AttachmentStorageState.Ready,
                CreatedByUserId = actor.UserId,
                CreatedByDisplayNameSnapshot = actor.DisplayName,
                CreatedAt = DateTimeOffset.UtcNow,
                Version = 1,
            };
            dbContext.Attachments.Add(attachment);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            storageKey = null;
            return new AttachmentUploadResult(ToMetadata(attachment, false), null, AttachmentFailure.None);
        }
        catch (AttachmentPayloadTooLargeException)
        {
            return new AttachmentUploadResult(null, null, AttachmentFailure.PayloadTooLarge);
        }
        catch (AttachmentEmptyPayloadException)
        {
            return new AttachmentUploadResult(
                null,
                new Dictionary<string, string[]> { ["file"] = ["附件内容不能为空。"] },
                AttachmentFailure.Validation);
        }
        catch (AttachmentTypeRejectedException exception)
        {
            return Rejected(exception.Message);
        }
        catch (AttachmentStorageUnavailableException)
        {
            return new AttachmentUploadResult(null, null, AttachmentFailure.StorageUnavailable);
        }
        catch (DbUpdateException)
        {
            return new AttachmentUploadResult(null, null, AttachmentFailure.Conflict);
        }
        finally
        {
            if (staged is not null) storage.DeleteStaging(staged);
            if (storageKey is not null)
            {
                try { storage.DeleteCommitted(storageKey); }
                catch (AttachmentStorageUnavailableException exception)
                {
                    logger.LogError(
                        exception,
                        "Attachment database-write compensation could not remove the committed object.");
                }
            }
        }
    }

    public async Task<AttachmentContentResult> GetCurrentContent(
        long knowledgeDocumentId,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await FindCurrent(knowledgeDocumentId, attachmentId, cancellationToken);
        if (attachment is null) return MissingContent();
        if (AttachmentFilePolicy.GetPreviewMode(attachment) != PreviewMode.Image)
        {
            return new AttachmentContentResult(null, AttachmentFailure.PreviewNotSupported);
        }
        return await OpenVerified(attachment, cancellationToken);
    }

    public async Task<AttachmentContentResult> GetHistoricalContent(
        long knowledgeDocumentId,
        long revisionNumber,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await FindHistorical(knowledgeDocumentId, revisionNumber, attachmentId, cancellationToken);
        if (attachment is null) return MissingContent();
        if (AttachmentFilePolicy.GetPreviewMode(attachment) != PreviewMode.Image)
        {
            return new AttachmentContentResult(null, AttachmentFailure.PreviewNotSupported);
        }
        return await OpenVerified(attachment, cancellationToken);
    }

    public async Task<AttachmentContentResult> DownloadCurrent(
        long knowledgeDocumentId,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await FindCurrent(knowledgeDocumentId, attachmentId, cancellationToken);
        return attachment is null ? MissingContent() : await OpenVerified(attachment, cancellationToken);
    }

    public async Task<AttachmentContentResult> DownloadHistorical(
        long knowledgeDocumentId,
        long revisionNumber,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await FindHistorical(knowledgeDocumentId, revisionNumber, attachmentId, cancellationToken);
        return attachment is null ? MissingContent() : await OpenVerified(attachment, cancellationToken);
    }

    public async Task<AttachmentPreviewResult> PreviewCurrent(
        long knowledgeDocumentId,
        long attachmentId,
        string? sheet,
        CancellationToken cancellationToken)
    {
        var attachment = await FindCurrent(knowledgeDocumentId, attachmentId, cancellationToken);
        return attachment is null
            ? new AttachmentPreviewResult(null, null, AttachmentFailure.NotFound)
            : await previewService.Create(attachment, ToMetadata(attachment, true), sheet, cancellationToken);
    }

    public async Task<AttachmentPreviewResult> PreviewHistorical(
        long knowledgeDocumentId,
        long revisionNumber,
        long attachmentId,
        string? sheet,
        CancellationToken cancellationToken)
    {
        var attachment = await FindHistorical(knowledgeDocumentId, revisionNumber, attachmentId, cancellationToken);
        return attachment is null
            ? new AttachmentPreviewResult(null, null, AttachmentFailure.NotFound)
            : await previewService.Create(attachment, ToMetadata(attachment, true), sheet, cancellationToken);
    }

    public async Task<AdministratorAttachmentResult> GetAdministratorMetadata(
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == attachmentId, cancellationToken);
        if (attachment is null) return new AdministratorAttachmentResult(null, AttachmentFailure.NotFound);
        var referenceCount = await dbContext.AttachmentReferences.CountAsync(
            reference => reference.AttachmentId == attachmentId,
            cancellationToken);
        return new AdministratorAttachmentResult(
            ToMetadata(attachment, referenceCount > 0, referenceCount),
            AttachmentFailure.None);
    }

    public async Task<AdministratorAttachmentIntegrityResult> CheckAdministratorIntegrity(
        long attachmentId,
        CancellationToken cancellationToken)
    {
        var attachment = await dbContext.Attachments.AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == attachmentId, cancellationToken);
        if (attachment is null)
        {
            return new AdministratorAttachmentIntegrityResult(null, AttachmentFailure.NotFound);
        }
        var inspection = await storage.InspectIntegrity(attachment, cancellationToken);
        return new AdministratorAttachmentIntegrityResult(
            new AdministratorAttachmentIntegrityResponse(
                attachment.Id,
                inspection.Status.ToString(),
                attachment.SizeBytes,
                inspection.ActualSizeBytes,
                Convert.ToHexString(attachment.Sha256).ToLowerInvariant(),
                inspection.ActualSha256,
                DateTimeOffset.UtcNow),
            AttachmentFailure.None);
    }

    public async Task<AttachmentFailure> DeleteOrphan(
        long attachmentId,
        string concurrencyToken,
        CancellationToken cancellationToken)
    {
        if (!concurrencyTokenCodec.TryDecode(concurrencyToken, out var expectedVersion))
        {
            return AttachmentFailure.Validation;
        }

        string storageKey;
        long deletePendingVersion;
        await using (var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken))
        {
            var attachment = await dbContext.Attachments.SingleOrDefaultAsync(
                item => item.Id == attachmentId,
                cancellationToken);
            if (attachment is null) return AttachmentFailure.NotFound;
            if (attachment.Version != expectedVersion) return AttachmentFailure.Conflict;
            if (await dbContext.AttachmentReferences.AnyAsync(
                reference => reference.AttachmentId == attachmentId,
                cancellationToken))
            {
                return AttachmentFailure.Referenced;
            }
            storageKey = attachment.StorageKey;
            if (attachment.StorageState == AttachmentStorageState.Ready)
            {
                attachment.StorageState = AttachmentStorageState.DeletePending;
                attachment.Version++;
                await dbContext.SaveChangesAsync(cancellationToken);
            }
            deletePendingVersion = attachment.Version;
            await transaction.CommitAsync(cancellationToken);
        }

        try
        {
            storage.DeleteCommitted(storageKey);
        }
        catch (AttachmentStorageUnavailableException)
        {
            return AttachmentFailure.StorageUnavailable;
        }

        await using (var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, cancellationToken))
        {
            var attachment = await dbContext.Attachments.SingleOrDefaultAsync(
                item => item.Id == attachmentId,
                cancellationToken);
            if (attachment is null) return AttachmentFailure.None;
            if (attachment.StorageState != AttachmentStorageState.DeletePending
                || attachment.Version != deletePendingVersion)
            {
                return AttachmentFailure.Conflict;
            }
            if (await dbContext.AttachmentReferences.AnyAsync(
                reference => reference.AttachmentId == attachmentId,
                cancellationToken))
            {
                return AttachmentFailure.Referenced;
            }
            dbContext.Attachments.Remove(attachment);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        return AttachmentFailure.None;
    }

    public AttachmentMetadataResponse ToMetadata(
        Attachment attachment,
        bool canDownload,
        int? referenceCount = null)
    {
        var mode = AttachmentFilePolicy.GetPreviewMode(attachment);
        return new AttachmentMetadataResponse(
            attachment.Id,
            attachment.Kind.ToString(),
            attachment.OriginalFileName,
            attachment.Extension,
            attachment.ContentType,
            attachment.SizeBytes,
            Convert.ToHexString(attachment.Sha256).ToLowerInvariant(),
            mode.ToString(),
            mode != PreviewMode.None,
            canDownload,
            concurrencyTokenCodec.Encode(attachment.Version),
            referenceCount);
    }

    private async Task<Attachment?> FindCurrent(
        long knowledgeDocumentId,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        return await dbContext.KnowledgeDocuments.AsNoTracking()
            .Where(document => document.Id == knowledgeDocumentId)
            .Join(
                dbContext.KnowledgeDocumentRevisions.AsNoTracking(),
                document => new { DocumentId = document.Id, Revision = document.CurrentRevisionNumber },
                revision => new { DocumentId = revision.KnowledgeDocumentId, Revision = revision.RevisionNumber },
                (_, revision) => revision)
            .Join(
                dbContext.AttachmentReferences.AsNoTracking().Where(reference => reference.AttachmentId == attachmentId),
                revision => revision.Id,
                reference => reference.KnowledgeDocumentRevisionId,
                (_, reference) => reference)
            .Join(
                dbContext.Attachments.AsNoTracking(),
                reference => new { Id = reference.AttachmentId, DocumentId = reference.KnowledgeDocumentId },
                attachment => new { Id = attachment.Id, DocumentId = attachment.KnowledgeDocumentId },
                (_, attachment) => attachment)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<Attachment?> FindHistorical(
        long knowledgeDocumentId,
        long revisionNumber,
        long attachmentId,
        CancellationToken cancellationToken)
    {
        return await dbContext.KnowledgeDocuments.IgnoreQueryFilters().AsNoTracking()
            .Where(document => document.Id == knowledgeDocumentId)
            .Join(
                dbContext.KnowledgeDocumentRevisions.AsNoTracking().Where(revision => revision.RevisionNumber == revisionNumber),
                document => document.Id,
                revision => revision.KnowledgeDocumentId,
                (_, revision) => revision)
            .Join(
                dbContext.AttachmentReferences.AsNoTracking().Where(reference => reference.AttachmentId == attachmentId),
                revision => revision.Id,
                reference => reference.KnowledgeDocumentRevisionId,
                (_, reference) => reference)
            .Join(
                dbContext.Attachments.AsNoTracking(),
                reference => new { Id = reference.AttachmentId, DocumentId = reference.KnowledgeDocumentId },
                attachment => new { Id = attachment.Id, DocumentId = attachment.KnowledgeDocumentId },
                (_, attachment) => attachment)
            .SingleOrDefaultAsync(cancellationToken);
    }

    private async Task<AttachmentContentResult> OpenVerified(
        Attachment attachment,
        CancellationToken cancellationToken)
    {
        if (attachment.StorageState != AttachmentStorageState.Ready
            || !await storage.Verify(attachment, cancellationToken))
        {
            return new AttachmentContentResult(null, AttachmentFailure.StorageUnavailable);
        }
        try
        {
            return new AttachmentContentResult(
                new AttachmentContent(
                    storage.OpenRead(attachment.StorageKey),
                    attachment.ContentType,
                    attachment.OriginalFileName,
                    attachment.SizeBytes),
                AttachmentFailure.None);
        }
        catch (AttachmentStorageUnavailableException)
        {
            return new AttachmentContentResult(null, AttachmentFailure.StorageUnavailable);
        }
    }

    private static AttachmentContentResult MissingContent() =>
        new(null, AttachmentFailure.NotFound);

    private static AttachmentUploadResult Rejected(string message) =>
        new(
            null,
            new Dictionary<string, string[]> { ["file"] = [message] },
            AttachmentFailure.UnsupportedMediaType);
}
