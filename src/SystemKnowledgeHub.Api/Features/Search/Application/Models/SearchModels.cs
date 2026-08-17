namespace SystemKnowledgeHub.Api.Features.Search.Application.Models;

public sealed record SearchKnowledgeQuery(
    string? QueryText,
    string? Types,
    int? LimitPerGroup);

public sealed record SearchNavigation(
    string RouteObjectType,
    long RouteObjectId,
    string? OpenDrawer,
    long? DrawerObjectId);

public sealed record SearchResultItem(
    long Id,
    string SystemContext,
    string Title,
    string ShortDescription,
    string? KnowledgeStatus,
    string? UnknownItemStatus,
    SearchNavigation Navigation);

public sealed record SearchResultGroup(
    string ObjectType,
    string Label,
    IReadOnlyList<SearchResultItem> Items);

public sealed record SearchKnowledgeResponse(
    string Query,
    IReadOnlyList<SearchResultGroup> Groups,
    int Total);

public sealed record SearchKnowledgeQueryResult(
    SearchKnowledgeResponse? Response,
    IReadOnlyDictionary<string, string[]>? FieldErrors);
