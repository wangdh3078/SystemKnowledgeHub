using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;

public sealed class KnowledgeDocumentService(
    KnowledgeHubDbContext dbContext,
    KnowledgeDocumentQueries queries,
    ConcurrencyTokenCodec concurrencyTokenCodec)
{
    public const int TitleMaximumLength = 300;
    public const int SummaryMaximumLength = 2_000;
    public const int BodyMarkdownMaximumLength = 1_000_000;

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
            Version = 1,
        };
        dbContext.KnowledgeDocuments.Add(document);
        await dbContext.SaveChangesAsync(cancellationToken);
        return new KnowledgeDocumentWriteResult(queries.ToDetail(document), null, KnowledgeDocumentWriteFailure.None);
    }

    public async Task<KnowledgeDocumentWriteResult> UpdateContent(
        UpdateKnowledgeDocumentContentCommand request,
        CancellationToken cancellationToken)
    {
        var errors = ValidateContent(null, request.Title, request.Summary, request.BodyMarkdown, out _, out var title, out var summary, out var bodyMarkdown);
        if (!concurrencyTokenCodec.TryDecode(request.ConcurrencyToken, out var expectedVersion)) errors["concurrencyToken"] = ["并发标记无效，请重新加载后重试。"];
        if (errors.Count > 0) return new KnowledgeDocumentWriteResult(null, errors, KnowledgeDocumentWriteFailure.Validation);

        var document = await dbContext.KnowledgeDocuments.SingleOrDefaultAsync(item => item.Id == request.KnowledgeDocumentId, cancellationToken);
        if (document is null) return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.NotFound);
        if (document.Version != expectedVersion) return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.Conflict);

        document.Title = title;
        document.Summary = summary;
        document.BodyMarkdown = bodyMarkdown;
        document.UpdatedByUserId = request.Author.UserId;
        document.UpdatedByDisplayName = request.Author.DisplayName;
        document.UpdatedAt = DateTimeOffset.UtcNow;
        document.Version = expectedVersion + 1;
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            return new KnowledgeDocumentWriteResult(null, null, KnowledgeDocumentWriteFailure.Conflict);
        }
        return new KnowledgeDocumentWriteResult(queries.ToDetail(document), null, KnowledgeDocumentWriteFailure.None);
    }

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
