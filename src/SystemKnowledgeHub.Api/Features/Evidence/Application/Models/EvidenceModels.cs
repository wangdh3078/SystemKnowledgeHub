using System.Text.Json;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Evidence.Application.Models;

public sealed record EvidenceTargetCommand(string Type, long Id);

public sealed record PersonSnapshotCommand(
    string DisplayName,
    string RoleOrIdentity,
    DateTimeOffset OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);

public sealed record EvidenceActorCommand(string DisplayName, string? Role);

public sealed record AddEvidenceCommand(
    string EvidenceType,
    EvidenceTargetCommand? Subject,
    string? SubjectDetailKey,
    string SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string SupportReason,
    string? Confidence,
    PersonSnapshotCommand? Provider);

public sealed record UpdateEvidenceCommand(
    long EvidenceId,
    string SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string SupportReason,
    string? Confidence,
    PersonSnapshotCommand? Provider,
    EvidenceActorCommand? Actor,
    string ConcurrencyToken);

public sealed record AddHumanConfirmationCommand(
    EvidenceTargetCommand? Subject,
    string? SubjectDetailKey,
    string ConfirmationStatement,
    string SupportReason,
    string? SourceNote,
    PersonSnapshotCommand? Confirmer);

public sealed record EvidenceTargetResponse(string Type, long Id);

public sealed record PersonSnapshotResponse(
    string DisplayName,
    string RoleOrIdentity,
    DateTimeOffset OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);

public sealed record EvidenceSubjectContextResponse(string Title, string KnowledgeStatus);

public sealed record EvidenceDetailResponse(
    long Id,
    string ConcurrencyToken,
    string EvidenceType,
    EvidenceTargetResponse Subject,
    string? SubjectDetailKey,
    string SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string SupportReason,
    string? Confidence,
    PersonSnapshotResponse Provider,
    EvidenceSubjectContextResponse SubjectContext,
    IReadOnlyList<string> AvailableActions);

public sealed record AddEvidenceResponse(
    long Id,
    string EvidenceType,
    EvidenceTargetResponse Subject,
    string? SubjectDetailKey,
    string SourceTitle,
    string SubjectKnowledgeStatus,
    bool KnowledgeStatusChanged,
    string ConcurrencyToken);

public sealed record EvidenceSubjectContext(string Title, KnowledgeStatus KnowledgeStatus);

public enum EvidenceFailure
{
    None,
    Validation,
    NotFound,
    SubjectNotFound,
    Conflict,
}

public sealed record EvidenceCommandResult(
    object? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors,
    EvidenceFailure Failure);

public sealed record EvidenceDetailQueryResult(
    EvidenceDetailResponse? Response,
    EvidenceFailure Failure);
