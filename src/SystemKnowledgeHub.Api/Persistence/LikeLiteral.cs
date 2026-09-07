namespace SystemKnowledgeHub.Api.Persistence;

public static class LikeLiteral
{
    public const string EscapeCharacter = "\\";

    public static string Contains(string text) => "%" + text
        .Replace("\\", "\\\\", StringComparison.Ordinal)
        .Replace("%", "\\%", StringComparison.Ordinal)
        .Replace("_", "\\_", StringComparison.Ordinal) + "%";
}
