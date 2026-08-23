using System.Text;
using System.Text.RegularExpressions;

namespace SystemKnowledgeHub.Api.Features.Search.Application;

public static partial class KnowledgeDocumentSearchText
{
    public static string ToIndexText(string? value)
    {
        var plainText = ToPlainText(value);
        var builder = new StringBuilder(plainText.Length * 2);
        foreach (var character in plainText)
        {
            if (IsCjk(character))
            {
                builder.Append(' ').Append(character).Append(' ');
            }
            else
            {
                builder.Append(character);
            }
        }

        return Whitespace().Replace(builder.ToString(), " ").Trim();
    }

    public static string ToPlainText(string? value)
    {
        var plainText = value ?? string.Empty;
        plainText = MarkdownLink().Replace(plainText, "$1");
        plainText = CodeFence().Replace(plainText, string.Empty);
        plainText = Heading().Replace(plainText, string.Empty);
        plainText = BlockQuote().Replace(plainText, string.Empty);
        plainText = InlineFormatting().Replace(plainText, string.Empty);
        return Whitespace().Replace(plainText, " ").Trim();
    }

    public static string BuildQuery(string query)
    {
        var tokens = new List<string>();
        var word = new StringBuilder();
        foreach (var character in query)
        {
            if (IsCjk(character))
            {
                AddWord(tokens, word);
                tokens.Add(character.ToString());
            }
            else if (char.IsLetterOrDigit(character) || character == '_')
            {
                word.Append(character);
            }
            else
            {
                AddWord(tokens, word);
            }
        }
        AddWord(tokens, word);

        return string.Join(" AND ", tokens
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Select(token => $"\"{token.Replace("\"", "\"\"", StringComparison.Ordinal)}\""));
    }

    public static string CreateSnippet(string title, string? summary, string bodyMarkdown, string query)
    {
        var text = string.Join(" ", new[] { summary, ToPlainText(bodyMarkdown) }.Where(value => !string.IsNullOrWhiteSpace(value)));
        if (string.IsNullOrWhiteSpace(text)) return title;

        var match = QueryTokens(query)
            .Select(token => text.IndexOf(token, StringComparison.OrdinalIgnoreCase))
            .Where(index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        if (match < 0) return text.Length <= 180 ? text : text[..180] + "…";

        var start = Math.Max(0, match - 56);
        var length = Math.Min(180, text.Length - start);
        return (start > 0 ? "…" : string.Empty) + text.Substring(start, length) + (start + length < text.Length ? "…" : string.Empty);
    }

    private static IReadOnlyList<string> QueryTokens(string query) =>
        Regex.Matches(query, "[\\p{L}\\p{N}_]+")
            .Select(match => match.Value)
            .Where(token => token.Any(IsCjk) ? true : token.Length > 0)
            .ToArray();

    private static void AddWord(ICollection<string> tokens, StringBuilder word)
    {
        if (word.Length > 0) tokens.Add(word.ToString());
        word.Clear();
    }

    private static bool IsCjk(char character) =>
        character is >= '\u3400' and <= '\u4DBF'
            or >= '\u4E00' and <= '\u9FFF'
            or >= '\uF900' and <= '\uFAFF';

    [GeneratedRegex("!?\\[([^\\]]*)\\]\\([^)]*\\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();

    [GeneratedRegex("```[^\\r\\n]*", RegexOptions.CultureInvariant)]
    private static partial Regex CodeFence();

    [GeneratedRegex("(?m)^\\s{0,3}#{1,6}\\s*", RegexOptions.CultureInvariant)]
    private static partial Regex Heading();

    [GeneratedRegex("(?m)^\\s*>\\s?", RegexOptions.CultureInvariant)]
    private static partial Regex BlockQuote();

    [GeneratedRegex("[`*_~]", RegexOptions.CultureInvariant)]
    private static partial Regex InlineFormatting();

    [GeneratedRegex("\\s+", RegexOptions.CultureInvariant)]
    private static partial Regex Whitespace();
}
