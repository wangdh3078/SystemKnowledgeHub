namespace SystemKnowledgeHub.Api.Features.StatusProgression.Application;

public sealed record KnowledgeStatusTargetCommand(string Type, long Id);

public sealed record KnowledgeStatusActorCommand(
    string DisplayName,
    string RoleOrIdentity,
    DateTimeOffset OccurredAt);

public sealed record ChangeKnowledgeStatusCommand(
    KnowledgeStatusTargetCommand? Target,
    string TargetStatus,
    string? Reason,
    KnowledgeStatusActorCommand Actor,
    string ConcurrencyToken);

public sealed record KnowledgeStatusTargetResponse(string Type, long Id);

public sealed record ChangeKnowledgeStatusResponse(
    KnowledgeStatusTargetResponse Target,
    string PreviousStatus,
    string KnowledgeStatus,
    string? Reason,
    DateTimeOffset ChangedAt,
    string ConcurrencyToken);

public enum KnowledgeStatusFailure
{
    None,
    Validation,
    NotFound,
    Unsupported,
    Conflict,
    BusinessRuleViolation,
}

public sealed record ChangeKnowledgeStatusResult(
    ChangeKnowledgeStatusResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    KnowledgeStatusFailure Failure,
    string? Message = null,
    object? Details = null);
