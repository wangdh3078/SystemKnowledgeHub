namespace SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Api.Contracts;

public sealed record CreateAnalysisFolderRequest(long? ParentId, string? Title, string? TreeConcurrencyToken);
public sealed record RenameAnalysisFolderRequest(string? Title, string? NodeConcurrencyToken, string? TreeConcurrencyToken);
public sealed record AddAnalysisPlacementRequest(long? ParentId, long KnowledgeDocumentId, string? TreeConcurrencyToken);
public sealed record RemoveAnalysisNodeRequest(string? NodeConcurrencyToken, string? TreeConcurrencyToken);
public sealed record MoveAnalysisNodeRequest(long? TargetParentId, int TargetPosition, string? NodeConcurrencyToken, string? TreeConcurrencyToken);
public sealed record AnalysisChildOrderItem(long Id, string? NodeConcurrencyToken);
public sealed record ReorderAnalysisChildrenRequest(long? ParentId, IReadOnlyList<AnalysisChildOrderItem>? Items, string? TreeConcurrencyToken);
public sealed record CreateAnalysisDocumentRequest(long? ParentId, string? TreeConcurrencyToken,
    string? DocumentType, string? Title, string? Summary, string? BodyMarkdown);
