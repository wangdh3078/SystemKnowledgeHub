using System.Text.Json;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;

namespace SystemKnowledgeHub.Api.Features.Integrations.Application.Models;

public sealed record IntegrationActor(string DisplayName, string? Role);
public sealed record IntegrationParty(long? SystemId, string DisplayName);
public sealed record IntegrationEndpoint(
    string? Url, string? Method, string? Exchange, string? Topic, string? Queue, string? FilePath);
public sealed record IntegrationOverviewCommand(
    string Name, string IntegrationType, IntegrationParty? SourceParty, IntegrationParty? TargetParty,
    string FlowDirection, string? Purpose, JsonElement? Endpoint, long? DatabaseSourceId, long? DatabaseObjectId);
public sealed record CreateIntegrationCommand(IntegrationOverviewCommand Overview, IntegrationActor Actor, CanonicalCreator Creator);
public sealed record UpdateIntegrationCommand(long IntegrationId, IntegrationOverviewCommand Overview, IntegrationActor Actor, string ConcurrencyToken);
public sealed record IntegrationContractFieldCommand(int Order, string FieldName, string? DataType, bool Required, string? Description, string? SampleValue);
public sealed record ReplaceIntegrationContractFieldsCommand(long IntegrationId, IReadOnlyList<IntegrationContractFieldCommand>? Fields, IntegrationActor Actor, string ConcurrencyToken);
public sealed record IntegrationPartyResponse(long? SystemId, string DisplayName);
public sealed record IntegrationEndpointResponse(string? Url, string? Method, string? Exchange, string? Topic, string? Queue, string? FilePath);
public sealed record IntegrationWriteResponse(long Id, string Name, string IntegrationType, string KnowledgeStatus, string ConcurrencyToken);
public sealed record IntegrationContractFieldsResponse(long Id, IReadOnlyList<IntegrationContractFieldResponse> Fields, string ConcurrencyToken);
public sealed record IntegrationContractFieldResponse(int Order, string FieldName, string? DataType, bool Required, string? Description, string? SampleValue);
public sealed record IntegrationHeaderResponse(string Name, string IntegrationType, string KnowledgeStatus);
public sealed record IntegrationRelationshipResponse(long RelationshipId, long Id, string Name, string RelationType);
public sealed record IntegrationEvidenceResponse(long Id, string EvidenceType, string SourceTitle);
public sealed record IntegrationUnknownItemResponse(long Id, string Question, string Status);
public sealed record IntegrationContextRailResponse(IReadOnlyList<string> ParticipantSystems, int RelatedFunctionCount, int RelatedDataCount, int OpenUnknownCount, IReadOnlyList<string> ContractGaps);
public sealed record IntegrationDetailResponse(long Id, string ConcurrencyToken, IntegrationHeaderResponse Header,
    IntegrationPartyResponse SourceParty, IntegrationPartyResponse TargetParty, string FlowDirection, string? Purpose,
    IntegrationEndpointResponse Endpoint, long? DatabaseSourceId, long? DatabaseObjectId,
    IReadOnlyList<IntegrationContractFieldResponse> ContractFields,
    IReadOnlyList<IntegrationRelationshipResponse> RelatedFunctions, IReadOnlyList<IntegrationRelationshipResponse> RelatedData,
    IReadOnlyList<IntegrationEvidenceResponse> Evidence, IReadOnlyList<IntegrationUnknownItemResponse> UnknownItems,
    IntegrationContextRailResponse ContextRail, IReadOnlyList<string> AvailableActions);
public enum IntegrationFailure { None, Validation, NotFound, ReferenceInvalid, Duplicate, Conflict }
public sealed record IntegrationCommandResult(object? Response, IReadOnlyDictionary<string, string[]>? FieldErrors, IntegrationFailure Failure, string? Message = null);
