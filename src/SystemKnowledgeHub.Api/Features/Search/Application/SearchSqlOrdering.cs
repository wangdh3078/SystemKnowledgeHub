using Microsoft.Data.Sqlite;

namespace SystemKnowledgeHub.Api.Features.Search.Application;

// Connection-local SQLite operations preserve the existing .NET Unicode ordering.
// SQLite evaluates them before LIMIT; no matching rows are materialized to rank them.
public static class SearchSqlOrdering
{
    public const string Collation = "SEARCH_ORDINAL_IGNORE_CASE";

    public static void Register(SqliteConnection connection)
    {
        connection.CreateCollation(Collation, StringComparer.OrdinalIgnoreCase.Compare);
        connection.CreateFunction<string, string, string, int>("search_rank", Rank, isDeterministic: true);
    }

    public static int Rank(string title, string systemContext, string query)
    {
        if (title.Equals(query, StringComparison.OrdinalIgnoreCase)) return 0;
        if (title.StartsWith(query, StringComparison.OrdinalIgnoreCase)) return 1;
        if (systemContext.Equals(query, StringComparison.OrdinalIgnoreCase)) return 2;
        return 3;
    }
}
