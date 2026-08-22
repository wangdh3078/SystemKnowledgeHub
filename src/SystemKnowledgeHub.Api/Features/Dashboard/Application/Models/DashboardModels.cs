using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;

namespace SystemKnowledgeHub.Api.Features.Dashboard.Application.Models;

public sealed record DashboardQuery(long? SystemId);

public sealed record DashboardScopeResponse(long? SystemId, string? SystemName);

public sealed record DashboardKnowledgeOverviewResponse(
    int Systems,
    int BusinessFunctions,
    int DatabaseObjects,
    int Columns,
    int Integrations,
    int BusinessRules,
    int UnknownItems);

public sealed record DashboardKnowledgeProgressResponse(
    int Confirmed,
    int Inferred,
    int Unknown,
    int OpenUnknownItems);

public sealed record DashboardNeedsAttentionResponse(
    string Kind,
    int Count,
    string Label);

public sealed record DashboardRecentActivityResponse(
    string ObjectType,
    long ObjectId,
    string Title,
    DateTimeOffset UpdatedAt);

public sealed record DashboardUnknownItemStatusRow(
    UnknownItemStatus Status,
    UnknownItemPriority Priority);

public sealed record DashboardResponse(
    DashboardScopeResponse Scope,
    DashboardKnowledgeOverviewResponse KnowledgeOverview,
    DashboardKnowledgeProgressResponse KnowledgeProgress,
    IReadOnlyList<DashboardNeedsAttentionResponse> NeedsAttention,
    IReadOnlyList<DashboardRecentActivityResponse> RecentActivity);

public enum DashboardQueryFailure
{
    None,
    SystemNotFound,
}

public sealed record DashboardQueryResult(
    DashboardResponse? Response,
    DashboardQueryFailure Failure);
