using System.Security.Cryptography;
using System.Text;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Domain;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application.Models;

namespace SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application;

public static class AnalysisLimits
{
    public const int MaxDepth = 10;
    public const int MaxNodes = 2_000;
}

internal static class AnalysisTreeState
{
    // BinaryWriter length-prefixes strings and writes fixed-width integer fields. Null IDs use 0,
    // outside the valid ID domain. Only organization metadata participates; never document content.
    public static string Token(IReadOnlyList<AnalysisNode> nodes)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write("SystemKnowledgeHub.AnalysisTree.v1");
            writer.Write(nodes.Count);
            foreach (var node in nodes.OrderBy(node => node.Id))
            {
                writer.Write(node.Id);
                writer.Write(node.ParentId ?? 0);
                writer.Write(node.NodeType.ToString());
                writer.Write(node.KnowledgeDocumentId ?? 0);
                writer.Write(node.SortOrder);
                writer.Write(node.Version);
            }
        }
        return "a1." + Convert.ToHexString(SHA256.HashData(buffer.ToArray()));
    }

    public static bool IsToken(string? token) => token is { Length: 67 }
        && token.StartsWith("a1.", StringComparison.Ordinal) && token.AsSpan(3).ToArray().All(Uri.IsHexDigit);

    public static void Validate(IReadOnlyList<AnalysisNode> nodes)
    {
        if (nodes.Count > AnalysisLimits.MaxNodes) throw Rule("分析目录最多允许 2000 个节点，不能截断读取。");
        var byId = nodes.ToDictionary(node => node.Id);
        foreach (var node in nodes)
        {
            var depth = 1;
            var parent = node.ParentId;
            var visited = new HashSet<long> { node.Id };
            while (parent is not null)
            {
                if (!byId.TryGetValue(parent.Value, out var ancestor) || ancestor.NodeType != AnalysisNodeType.Folder)
                    throw Rule("父节点必须是存在的分析目录。");
                if (!visited.Add(parent.Value)) throw Rule("目录不能包含循环引用。");
                if (++depth > AnalysisLimits.MaxDepth) throw Rule("分析目录最多允许 10 层节点。");
                parent = ancestor.ParentId;
            }
        }
    }

    public static AnalysisRequestException Rule(string message) => new("business_rule_violation", message);
}
