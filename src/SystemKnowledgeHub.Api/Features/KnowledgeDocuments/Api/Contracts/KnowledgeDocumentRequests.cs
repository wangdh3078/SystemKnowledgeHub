namespace SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Api.Contracts;

public sealed record CreateKnowledgeDocumentRequest(
    string? DocumentType,
    string? Title,
    string? Summary,
    string? BodyMarkdown);

public sealed record UpdateKnowledgeDocumentContentRequest(
    string? Title,
    string? Summary,
    string? BodyMarkdown,
    string? ChangeSummary,
    string? ConcurrencyToken);

public sealed record UpdateKnowledgeDocumentLifecycleRequest(
    string? TargetLifecycleStatus,
    string? ConcurrencyToken);

public sealed record RestoreKnowledgeDocumentRevisionRequest(
    string? ConcurrencyToken,
    string? Reason);
