using System.Text.Json;

namespace SystemKnowledgeHub.Api.Features.Integrations.Api.Contracts;

public sealed record DeleteIntegrationRequest(string? ConcurrencyToken);

public sealed record IntegrationPartyRequest(long? SystemId, string? DisplayName);
public sealed record IntegrationActorRequest(string? DisplayName, string? Role);
public sealed record IntegrationOverviewRequest(string? Name, string? IntegrationType, IntegrationPartyRequest? SourceParty,
    IntegrationPartyRequest? TargetParty, string? FlowDirection, string? Purpose, JsonElement? Endpoint,
    long? DatabaseSourceId, long? DatabaseObjectId);
public sealed record CreateIntegrationRequest(string? Name, string? IntegrationType, IntegrationPartyRequest? SourceParty,
    IntegrationPartyRequest? TargetParty, string? FlowDirection, string? Purpose, JsonElement? Endpoint,
    long? DatabaseSourceId, long? DatabaseObjectId, IntegrationActorRequest? Actor);
public sealed record UpdateIntegrationOverviewRequest(string? Name, string? IntegrationType, IntegrationPartyRequest? SourceParty,
    IntegrationPartyRequest? TargetParty, string? FlowDirection, string? Purpose, JsonElement? Endpoint,
    long? DatabaseSourceId, long? DatabaseObjectId, IntegrationActorRequest? Actor, string? ConcurrencyToken);
public sealed record IntegrationContractFieldRequest(int Order, string? FieldName, string? DataType, bool Required, string? Description, string? SampleValue);
public sealed record ReplaceIntegrationContractFieldsRequest(IReadOnlyList<IntegrationContractFieldRequest>? Fields, IntegrationActorRequest? Actor, string? ConcurrencyToken);
