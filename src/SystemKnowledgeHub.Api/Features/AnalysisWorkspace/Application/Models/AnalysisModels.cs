namespace SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application.Models;

public sealed record AnalysisNodeResponse(long Id, long? ParentId, string NodeType, int SortOrder,
    string ConcurrencyToken, string Title, long? KnowledgeDocumentId, string? DocumentType,
    string? LifecycleStatus, string Availability, DateTimeOffset CreatedAt, DateTimeOffset UpdatedAt);
public sealed record AnalysisTreeResponse(IReadOnlyList<AnalysisNodeResponse> Items, string TreeConcurrencyToken);
public sealed record AnalysisMutationResponse(AnalysisNodeResponse? Node, IReadOnlyList<AnalysisNodeResponse> Items, string TreeConcurrencyToken);

// Expected failures of these explicit organization use cases; unexpected storage failures still propagate.
public sealed class AnalysisRequestException(string code, string message,
    IReadOnlyDictionary<string, string[]>? fieldErrors = null) : Exception(message)
{
    public string Code { get; } = code;
    public IReadOnlyDictionary<string, string[]>? FieldErrors { get; } = fieldErrors;
}
