namespace SystemKnowledgeHub.Api.Shared.Api.Contracts;

public sealed record ApiErrorResponse(
    string Code,
    string Message,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    object? Details);
