using System.Data;
using Npgsql;
using NpgsqlTypes;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.PostgreSql;

internal sealed class PostgreSqlConnectionTester(IPostgreSqlConnectionProbe probe) : IDatabaseConnectionTester
{
    private const int SupportedServerMajorVersion = 18;

    public DatabaseProviderType ProviderType => DatabaseProviderType.PostgreSql;

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connection.DatabaseName))
        {
            return DatabaseConnectionTestResult.Fail(
                DatabaseConnectionFailure.ConnectionFailed,
                "PostgreSQL 数据库名称无效。");
        }

        try
        {
            var result = await probe.ProbeAsync(connection, cancellationToken);
            if (result.ServerMajorVersion != SupportedServerMajorVersion)
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.UnsupportedDatabaseVersion,
                    "仅支持 PostgreSQL 18。");
            }
            if (!string.Equals(result.DatabaseName, connection.DatabaseName, StringComparison.Ordinal))
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.ConnectionFailed,
                    "连接到的 PostgreSQL 数据库与配置目标不一致。");
            }

            var missingSchemas = connection.IncludedSchemas
                .Except(result.VisibleSchemas, StringComparer.Ordinal)
                .ToArray();
            if (missingSchemas.Length > 0 || !result.HasRequiredCatalogVisibility)
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.InsufficientPrivilege,
                    "当前 PostgreSQL 账号无法验证全部 IncludedSchemas 或必要系统目录。");
            }

            return DatabaseConnectionTestResult.Success(
                "PostgreSQL 18 连接、数据库上下文与基础目录可见性验证成功。",
                result.ServerVersion,
                result.DatabaseName,
                null,
                null);
        }
        catch (PostgreSqlProbeException exception)
        {
            return DatabaseConnectionTestResult.Fail(
                exception.Failure,
                SafeSummary(exception.Failure),
                exception.VendorCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DatabaseConnectionTestResult.Fail(
                DatabaseConnectionFailure.Cancelled,
                "PostgreSQL 连接测试已取消。");
        }
        catch (OperationCanceledException)
        {
            return DatabaseConnectionTestResult.Fail(
                DatabaseConnectionFailure.Timeout,
                "PostgreSQL 连接测试超时。");
        }
        catch
        {
            return DatabaseConnectionTestResult.Fail(
                DatabaseConnectionFailure.ConnectionFailed,
                "PostgreSQL 连接测试失败。");
        }
    }

    private static string SafeSummary(DatabaseConnectionFailure failure) => failure switch
    {
        DatabaseConnectionFailure.AuthenticationFailed => "PostgreSQL 用户名或密码验证失败。",
        DatabaseConnectionFailure.InsufficientPrivilege => "PostgreSQL 账号缺少必要的基础目录可见性。",
        DatabaseConnectionFailure.Timeout => "PostgreSQL 连接测试超时。",
        DatabaseConnectionFailure.Cancelled => "PostgreSQL 连接测试已取消。",
        _ => "无法建立 PostgreSQL 连接。",
    };
}

internal interface IPostgreSqlConnectionProbe
{
    Task<PostgreSqlConnectionProbeResult> ProbeAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);
}

internal sealed record PostgreSqlConnectionProbeResult(
    string ServerVersion,
    int ServerMajorVersion,
    string DatabaseName,
    IReadOnlySet<string> VisibleSchemas,
    bool HasRequiredCatalogVisibility);

internal sealed class PostgreSqlProbeException : Exception
{
    public PostgreSqlProbeException(
        DatabaseConnectionFailure failure,
        string? sqlState = null)
    {
        Failure = failure;
        VendorCode = NormalizeSqlState(sqlState);
    }

    public DatabaseConnectionFailure Failure { get; }
    public string? VendorCode { get; }

    private static string? NormalizeSqlState(string? sqlState)
    {
        if (sqlState is null
            || sqlState.Length != 5
            || sqlState.Any(character => !char.IsAsciiLetterUpper(character) && !char.IsAsciiDigit(character)))
        {
            return null;
        }

        return $"SQLSTATE-{sqlState}";
    }
}

internal sealed class NpgsqlConnectionProbe(IOptions<DatabaseDiscoveryOptions> options) : IPostgreSqlConnectionProbe
{
    private readonly DatabaseDiscoveryOptions settings = Validate(options.Value);

    private static readonly string[] RequiredCatalogProbes =
    [
        "SELECT 1 FROM pg_catalog.pg_namespace WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_class WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_attribute WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_attrdef WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_type WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_constraint WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_index WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_am WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_sequence WHERE FALSE",
        "SELECT 1 FROM pg_catalog.pg_depend WHERE FALSE",
    ];

    internal int ConfiguredConnectionTimeoutSeconds => settings.ConnectionTimeoutSeconds;
    internal int ConfiguredCommandTimeoutSeconds => settings.ConnectionTimeoutSeconds;

    public async Task<PostgreSqlConnectionProbeResult> ProbeAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.ConnectionTimeoutSeconds));
        var token = timeout.Token;

        try
        {
            var builder = CreateConnectionStringBuilder(connection);
            await using var databaseConnection = new NpgsqlConnection(builder.ConnectionString);
            await databaseConnection.OpenAsync(token);

            var databaseName = await ReadCurrentDatabase(databaseConnection, token);
            var schemas = await ReadVisibleSchemas(databaseConnection, connection.IncludedSchemas, token);
            foreach (var sql in RequiredCatalogProbes)
            {
                await ExecuteCatalogProbe(databaseConnection, sql, token);
            }

            return new PostgreSqlConnectionProbeResult(
                databaseConnection.ServerVersion,
                databaseConnection.PostgreSqlVersion.Major,
                databaseName,
                schemas,
                true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new PostgreSqlProbeException(DatabaseConnectionFailure.Timeout);
        }
        catch (PostgresException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new PostgreSqlProbeException(DatabaseConnectionFailure.Cancelled, exception.SqlState);
        }
        catch (PostgresException exception) when (timeout.IsCancellationRequested)
        {
            throw new PostgreSqlProbeException(DatabaseConnectionFailure.Timeout, exception.SqlState);
        }
        catch (PostgresException exception)
        {
            throw new PostgreSqlProbeException(MapPostgreSqlFailure(exception.SqlState), exception.SqlState);
        }
        catch (NpgsqlException) when (cancellationToken.IsCancellationRequested)
        {
            throw new PostgreSqlProbeException(DatabaseConnectionFailure.Cancelled);
        }
        catch (NpgsqlException) when (timeout.IsCancellationRequested)
        {
            throw new PostgreSqlProbeException(DatabaseConnectionFailure.Timeout);
        }
        catch (NpgsqlException exception)
        {
            throw new PostgreSqlProbeException(
                exception.InnerException is TimeoutException
                    ? DatabaseConnectionFailure.Timeout
                    : DatabaseConnectionFailure.ConnectionFailed);
        }
        catch (TimeoutException)
        {
            throw new PostgreSqlProbeException(DatabaseConnectionFailure.Timeout);
        }
    }

    internal NpgsqlConnectionStringBuilder CreateConnectionStringBuilder(
        DatabaseDiscoveryConnectionContext connection) => new()
    {
        Host = connection.Host,
        Port = connection.Port,
        Database = connection.DatabaseName,
        Username = connection.Username,
        Password = connection.Password,
        Pooling = false,
        Enlist = false,
        Timeout = settings.ConnectionTimeoutSeconds,
        CommandTimeout = settings.ConnectionTimeoutSeconds,
        IncludeErrorDetail = false,
    };

    private async Task<string> ReadCurrentDatabase(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = settings.ConnectionTimeoutSeconds;
        command.CommandText = "SELECT pg_catalog.current_database()";
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result as string ?? throw new PostgreSqlProbeException(DatabaseConnectionFailure.ConnectionFailed);
    }

    private async Task<IReadOnlySet<string>> ReadVisibleSchemas(
        NpgsqlConnection connection,
        IReadOnlyList<string> schemas,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = settings.ConnectionTimeoutSeconds;
        command.CommandText = """
            SELECT namespace.nspname
            FROM pg_catalog.pg_namespace AS namespace
            WHERE namespace.nspname = ANY (@schemas)
              AND pg_catalog.has_schema_privilege(CURRENT_USER, namespace.oid, 'USAGE')
            """;
        command.Parameters.AddWithValue(
            "schemas",
            NpgsqlDbType.Array | NpgsqlDbType.Text,
            schemas.ToArray());

        var visible = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            visible.Add(reader.GetString(0));
        }
        return visible;
    }

    private async Task ExecuteCatalogProbe(
        NpgsqlConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = settings.ConnectionTimeoutSeconds;
        command.CommandText = sql;
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static DatabaseConnectionFailure MapPostgreSqlFailure(string sqlState) => sqlState switch
    {
        "28000" or "28P01" => DatabaseConnectionFailure.AuthenticationFailed,
        "42501" => DatabaseConnectionFailure.InsufficientPrivilege,
        "57014" => DatabaseConnectionFailure.Timeout,
        _ => DatabaseConnectionFailure.ConnectionFailed,
    };

    private static DatabaseDiscoveryOptions Validate(DatabaseDiscoveryOptions value)
    {
        value.Validate();
        return value;
    }
}
