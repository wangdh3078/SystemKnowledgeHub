using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;

namespace SystemKnowledgeHub.Api.Features.Traceability.Application;

/// <summary>
/// Applies TRACE's fixed-path cycle defense without introducing a generic graph traversal abstraction.
/// </summary>
public sealed class TraceTraversalGuard
{
    private readonly HashSet<(DocumentType Type, long Id)> _visitedNodes = [];

    public bool CycleDetected { get; private set; }

    public bool ObservePath(
        IReadOnlyList<(DocumentType Type, long Id)> nodes,
        IReadOnlyList<long> relationshipIds)
    {
        var pathNodes = new HashSet<(DocumentType Type, long Id)>();
        var pathRelationships = new HashSet<long>();
        foreach (var node in nodes)
        {
            _visitedNodes.Add(node);
            if (!pathNodes.Add(node))
            {
                CycleDetected = true;
                return false;
            }
        }
        foreach (var relationshipId in relationshipIds)
        {
            if (!pathRelationships.Add(relationshipId))
            {
                CycleDetected = true;
                return false;
            }
        }
        return true;
    }
}
