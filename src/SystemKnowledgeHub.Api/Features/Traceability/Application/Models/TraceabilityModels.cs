using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.Traceability.Application.Models;

public enum TraceabilityQueryFailure
{
    None,
    NotFound,
    UnsupportedDocumentType,
    ReferenceInvalid,
}

public enum TraceCoverageEligibility
{
    Active,
    ExcludedArchived,
}

public enum TraceMissingLinkCode
{
    MissingSpecification,
    MissingTestDefinition,
}

public enum TraceDirection
{
    Outgoing,
    Incoming,
}

public enum TraceTruncationReason
{
    MaxNodes,
    MaxEdges,
}

public enum TraceConfirmationCoverageState
{
    NoConfirmation,
    LegacyConfirmationUnknown,
    CurrentRevisionConfirmed,
    ChangedSinceConfirmation,
}

public sealed record TraceConfirmationCoverageResponse(
    TraceConfirmationCoverageState State,
    long? LastConfirmedRevisionNumber);

public sealed record TraceDocumentResponse(
    long Id,
    DocumentType DocumentType,
    string Title,
    DocumentLifecycleStatus LifecycleStatus,
    KnowledgeStatus KnowledgeStatus,
    long CurrentRevisionNumber,
    int EvidenceCount,
    int HumanConfirmationCount,
    TraceConfirmationCoverageResponse ConfirmationCoverage);

public sealed record TraceRelationshipResponse(
    long Id,
    RelationType RelationType,
    TraceDirection Direction,
    KnowledgeStatus KnowledgeStatus,
    int EvidenceCount,
    int HumanConfirmationCount);

public sealed record TraceDocumentRelationResponse(
    TraceRelationshipResponse Relationship,
    TraceDocumentResponse Document);

public sealed record TraceSpecificationBranchCoverageResponse(
    bool HasTestDefinition,
    IReadOnlyList<TraceMissingLinkCode> MissingLinkCodes);

public sealed record TraceSpecificationBranchResponse(
    TraceRelationshipResponse Relationship,
    TraceDocumentResponse Document,
    TraceSpecificationBranchCoverageResponse Coverage,
    IReadOnlyList<TraceDocumentRelationResponse> TestCases);

public sealed record TraceUpstreamSpecificationResponse(
    TraceRelationshipResponse Relationship,
    TraceDocumentResponse Document,
    IReadOnlyList<TraceDocumentRelationResponse> UpstreamRequirements);

public sealed record TraceRequirementCoverageResponse(
    TraceCoverageEligibility Eligibility,
    bool HasSpecification,
    bool HasDirectTestDefinition,
    bool HasSpecificationTestDefinition,
    bool HasAnyTestDefinition,
    IReadOnlyList<TraceMissingLinkCode> MissingLinkCodes);

public sealed record TraceSpecificationCoverageResponse(
    TraceCoverageEligibility Eligibility,
    bool HasTestDefinition,
    IReadOnlyList<TraceMissingLinkCode> MissingLinkCodes);

public sealed record TraceTestCaseCoverageResponse(
    TraceCoverageEligibility Eligibility,
    IReadOnlyList<TraceMissingLinkCode> MissingLinkCodes);

public sealed record TraceLineageResponse(
    IReadOnlyList<TraceDocumentRelationResponse> Incoming,
    IReadOnlyList<TraceDocumentRelationResponse> Outgoing,
    int Total,
    bool IsTruncated);

public sealed record TraceLimitsResponse(
    int MaxDepth,
    int MaxNodes,
    int MaxEdges,
    int MaxLineageEntries);

public interface ITraceabilityResponse
{
    TraceDocumentResponse Root { get; }
    TraceLineageResponse Lineage { get; }
    bool CycleDetected { get; }
    bool IsTruncated { get; }
    IReadOnlyList<TraceTruncationReason> TruncationReasons { get; }
    TraceLimitsResponse Limits { get; }
}

public sealed record RequirementTraceabilityResponse(
    TraceDocumentResponse Root,
    TraceRequirementCoverageResponse Coverage,
    IReadOnlyList<TraceSpecificationBranchResponse> Specifications,
    IReadOnlyList<TraceDocumentRelationResponse> DirectTestCases,
    IReadOnlyList<TraceDocumentRelationResponse> UpstreamRequirements,
    TraceLineageResponse Lineage,
    bool CycleDetected,
    bool IsTruncated,
    IReadOnlyList<TraceTruncationReason> TruncationReasons,
    TraceLimitsResponse Limits) : ITraceabilityResponse;

public sealed record SpecificationTraceabilityResponse(
    TraceDocumentResponse Root,
    TraceSpecificationCoverageResponse Coverage,
    IReadOnlyList<TraceDocumentRelationResponse> UpstreamRequirements,
    IReadOnlyList<TraceDocumentRelationResponse> TestCases,
    TraceLineageResponse Lineage,
    bool CycleDetected,
    bool IsTruncated,
    IReadOnlyList<TraceTruncationReason> TruncationReasons,
    TraceLimitsResponse Limits) : ITraceabilityResponse;

public sealed record TestCaseTraceabilityResponse(
    TraceDocumentResponse Root,
    TraceTestCaseCoverageResponse Coverage,
    IReadOnlyList<TraceDocumentRelationResponse> DirectRequirements,
    IReadOnlyList<TraceUpstreamSpecificationResponse> UpstreamSpecifications,
    TraceLineageResponse Lineage,
    bool CycleDetected,
    bool IsTruncated,
    IReadOnlyList<TraceTruncationReason> TruncationReasons,
    TraceLimitsResponse Limits) : ITraceabilityResponse;

public sealed record TraceabilityQueryResult(
    ITraceabilityResponse? Response,
    TraceabilityQueryFailure Failure);
