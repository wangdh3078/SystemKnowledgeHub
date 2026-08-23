using Microsoft.Data.Sqlite;
using Xunit.Abstractions;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class SqliteFtsCapabilityTests(ITestOutputHelper output)
{
    [Fact]
    public async Task Runtime_supports_fts5_unicode61_for_cjk_character_tokens_and_reports_trigram_availability()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        output.WriteLine($"SQLite version: {await Scalar<string>(connection, "SELECT sqlite_version();")}");
        var compileOptions = await ReadStrings(connection, "PRAGMA compile_options;");
        Assert.Contains(compileOptions, option => option.Contains("ENABLE_FTS5", StringComparison.Ordinal));

        await Execute(connection, "CREATE VIRTUAL TABLE document_fts USING fts5(content, tokenize='unicode61');");
        await Execute(connection, "INSERT INTO document_fts(content) VALUES ('检 查 Oracle 数 据 库 监 听 服 务 正 常 运 行 ORA 12541');");

        Assert.Equal(1L, await CountMatches(connection, "\"数\" AND \"据\" AND \"库\""));
        Assert.Equal(1L, await CountMatches(connection, "\"监\" AND \"听\""));
        Assert.Equal(1L, await CountMatches(connection, "\"Oracle\" AND \"12541\""));

        try
        {
            await Execute(connection, "CREATE VIRTUAL TABLE trigram_fts USING fts5(content, tokenize='trigram');");
            output.WriteLine("FTS5 trigram tokenizer: available");
        }
        catch (SqliteException exception)
        {
            output.WriteLine($"FTS5 trigram tokenizer: unavailable ({exception.SqliteErrorCode})");
        }
    }

    private static async Task<long> CountMatches(SqliteConnection connection, string query)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT count(*) FROM document_fts WHERE document_fts MATCH $query;";
        command.Parameters.AddWithValue("$query", query);
        return (long)(await command.ExecuteScalarAsync() ?? 0L);
    }

    private static async Task<T> Scalar<T>(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        return (T)(await command.ExecuteScalarAsync() ?? throw new InvalidOperationException("Expected a scalar value."));
    }

    private static async Task<IReadOnlyList<string>> ReadStrings(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await using var reader = await command.ExecuteReaderAsync();
        var values = new List<string>();
        while (await reader.ReadAsync()) values.Add(reader.GetString(0));
        return values;
    }

    private static async Task Execute(SqliteConnection connection, string sql)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync();
    }
}
