using SystemKnowledgeHub.Api.Features.Relationships.Domain;

namespace SystemKnowledgeHub.Api.Features.Traceability.Application.Models;

public enum ImpactQueryFailure
{
    None,
    NotFound,
    UnsupportedDocumentType,
    ReferenceInvalid,
}

public enum ImpactPathKind
{
    DirectAppliesTo,
    DirectDocuments,
    ViaSpecificationDocuments,
    ViaRequirementAppliesTo,
    ViaRequirementDocuments,
    ViaVerifiedRequirementAppliesTo,
    ViaVerifiedSpecificationDocuments,
}

public enum ImpactMeaning
{
    ExplicitRequirementScope,
    DocumentedByRequirement,
    DocumentedBySpecification,
    DocumentedByTestCase,
    UpstreamRequirementScope,
    UpstreamRequirementDocumentedContext,
    VerifiedRequirementScope,
    VerifiedSpecificationDocumentedContext,
}

public enum ImpactTargetType
{
    System,
    BusinessFunction,
    DatabaseObject,
    BusinessRule,
    Integration,
}

public sealed record ImpactPathSegmentResponse(
    long RelationshipId,
    RelationType RelationType,
    TraceDirection Direction);

public sealed record ImpactSystemContextResponse(long Id, string Name);

public sealed record ImpactTargetResponse(
    ImpactTargetType Type,
    long Id,
    string Title,
    IReadOnlyList<ImpactSystemContextResponse> SystemContext);

public sealed record ImpactItemResponse(
    ImpactPathKind PathKind,
    ImpactMeaning Meaning,
    ImpactTargetResponse Target,
    IReadOnlyList<ImpactPathSegmentResponse> Path);

public sealed record ImpactResponse(
    IReadOnlyList<ImpactItemResponse> Items,
    long Page,
    int PageSize,
    int Total,
    int MaxDepth);

public sealed record ImpactQueryResult(
    ImpactResponse? Response,
    ImpactQueryFailure Failure);
