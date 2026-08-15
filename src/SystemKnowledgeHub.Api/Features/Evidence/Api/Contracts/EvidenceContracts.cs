using System.Text.Json;

namespace SystemKnowledgeHub.Api.Features.Evidence.Api.Contracts;

public sealed record EvidenceTargetRequest(string? Type, long Id);

public sealed record PersonSnapshotRequest(
    string? DisplayName,
    string? RoleOrIdentity,
    DateTimeOffset? OccurredAt,
    string? Team,
    string? ExternalUserKey,
    string? Source,
    string? Note);

public sealed record EvidenceActorRequest(string? DisplayName, string? Role);

public sealed record AddEvidenceRequest(
    string? EvidenceType,
    EvidenceTargetRequest? Subject,
    string? SubjectDetailKey,
    string? SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string? SupportReason,
    string? Confidence,
    PersonSnapshotRequest? Provider);

public sealed record UpdateEvidenceRequest(
    string? SourceTitle,
    string? SourceReference,
    JsonElement? SourceLocator,
    string? Summary,
    string? SupportReason,
    string? Confidence,
    PersonSnapshotRequest? Provider,
    EvidenceActorRequest? Actor,
    string? ConcurrencyToken);

public sealed record AddHumanConfirmationRequest(
    EvidenceTargetRequest? Subject,
    string? SubjectDetailKey,
    string? ConfirmationStatement,
    string? SupportReason,
    string? SourceNote,
    PersonSnapshotRequest? Confirmer);
