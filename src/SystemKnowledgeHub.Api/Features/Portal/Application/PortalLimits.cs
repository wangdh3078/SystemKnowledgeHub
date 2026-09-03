namespace SystemKnowledgeHub.Api.Features.Portal.Application;

public static class PortalLimits
{
    public const int MaximumTreeDepth = 10;
    public const int MaximumEffectiveTreeNodes = 2_000;
    public const int MaximumSectionsPerPage = 30;
    public const int MaximumKnowledgeDocumentBodySectionsPerPage = 5;
    public const int MaximumRelatedResultsPerGroup = 20;
    public const int MaximumTraceNodes = 200;
    public const int MaximumTraceEdges = 300;
    public const int TraceDepth = 2;
    public const int DefaultSearchPageSize = 20;
    public const int MaximumSearchPageSize = 100;
    public const int MaximumDatabaseColumnsPerObject = 500;
}
