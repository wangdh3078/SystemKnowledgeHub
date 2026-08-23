namespace SystemKnowledgeHub.Api.Features.Systems.Application.Models;

public sealed record SystemKnowledgeViewResponse(
    long SystemId,
    SystemKnowledgeOverviewResponse Overview,
    IReadOnlyList<SystemKnowledgeItemResponse> BusinessFunctions,
    IReadOnlyList<SystemKnowledgeItemResponse> DatabaseObjects,
    IReadOnlyList<SystemKnowledgeItemResponse> BusinessRules,
    IReadOnlyList<SystemKnowledgeIntegrationResponse> Integrations,
    IReadOnlyList<SystemKnowledgeDocumentResponse> Documents,
    IReadOnlyList<SystemKnowledgeRelationshipResponse> Relationships,
    IReadOnlyList<SystemKnowledgeEvidenceResponse> Evidence,
    IReadOnlyList<SystemKnowledgeUnknownItemResponse> UnknownItems);

public sealed record SystemKnowledgeOverviewResponse(
    int BusinessFunctionCount,
    int DatabaseObjectCount,
    int BusinessRuleCount,
    int IntegrationCount,
    int DocumentCount,
    int EvidenceCount,
    int OpenUnknownItemCount);

public sealed record SystemKnowledgeItemResponse(
    long Id,
    string Title,
    string? Description,
    string KnowledgeStatus);

public sealed record SystemKnowledgeIntegrationResponse(
    long Id,
    string Name,
    string IntegrationType,
    string Direction,
    string RelatedParty,
    string KnowledgeStatus);

public sealed record SystemKnowledgeDocumentResponse(
    long Id,
    string DocumentType,
    string Title,
    string LifecycleStatus,
    string KnowledgeStatus,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<string> RelationTypes);

public sealed record SystemKnowledgeRelationshipResponse(
    long Id,
    string Direction,
    string RelationType,
    string RelatedType,
    long RelatedId,
    string KnowledgeStatus);

public sealed record SystemKnowledgeEvidenceResponse(
    long Id,
    string EvidenceType,
    string SourceTitle,
    string? Summary,
    DateTimeOffset ProvidedAt);

public sealed record SystemKnowledgeUnknownItemResponse(
    long Id,
    string ItemCode,
    string Question,
    string Priority,
    string Status,
    DateTimeOffset UpdatedAt);
