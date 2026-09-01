using System.Data;
using System.Globalization;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.SqlServer;

internal sealed class SqlServerConnectionTester(ISqlServerConnectionProbe probe) : IDatabaseConnectionTester
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.SqlServer;

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(connection.DatabaseName))
        {
            return DatabaseConnectionTestResult.Fail(
                DatabaseConnectionFailure.ConnectionFailed,
                "SQL Server 数据库名称无效。");
        }

        try
        {
            var result = await probe.ProbeAsync(connection, cancellationToken);
            if (result.ServerMajorVersion != SqlServerDiscoveryRules.SupportedMajorVersion)
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.UnsupportedDatabaseVersion,
                    "仅支持 SQL Server 2022（major 16）。");
            }
            if (result.ResolvedSchemas.Count != connection.IncludedSchemas.Count)
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.InsufficientPrivilege,
                    "当前 SQL Server 账号无法验证全部 IncludedSchemas 或必要系统目录。");
            }
            if (result.ResolvedSchemas.Distinct(StringComparer.Ordinal).Count() != result.ResolvedSchemas.Count)
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.ConnectionFailed,
                    "IncludedSchemas 在目标数据库排序规则下存在歧义。");
            }

            return DatabaseConnectionTestResult.Success(
                "SQL Server 2022 连接、数据库上下文与基础目录可见性验证成功。",
                result.ServerVersion,
                result.DatabaseName,
                null,
                null);
        }
        catch (SqlServerProbeException exception)
        {
            return DatabaseConnectionTestResult.Fail(
                exception.Failure,
                SqlServerDiscoveryErrorMapper.ConnectionSummary(exception.Failure),
                exception.VendorCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DatabaseConnectionTestResult.Fail(
                DatabaseConnectionFailure.Cancelled,
                "SQL Server 连接测试已取消。");
        }
        catch (OperationCanceledException)
        {
            return DatabaseConnectionTestResult.Fail(
                DatabaseConnectionFailure.Timeout,
                "SQL Server 连接测试超时。");
        }
        catch
        {
            return DatabaseConnectionTestResult.Fail(
                DatabaseConnectionFailure.ConnectionFailed,
                "SQL Server 连接测试失败。");
        }
    }
}

internal interface ISqlServerConnectionProbe
{
    Task<SqlServerConnectionProbeResult> ProbeAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);
}

internal sealed record SqlServerConnectionProbeResult(
    string ServerVersion,
    int ServerMajorVersion,
    string ProviderVersion,
    string DatabaseName,
    string DatabaseCollation,
    string ConnectedPrincipal,
    IReadOnlyList<string> ResolvedSchemas);

internal sealed class SqlServerProbeException(
    DatabaseConnectionFailure failure,
    int? errorNumber = null) : Exception
{
    public DatabaseConnectionFailure Failure { get; } = failure;
    public string? VendorCode { get; } = errorNumber is null
        ? null
        : SqlServerDiscoveryErrorMapper.AllowlistedVendorCode(errorNumber.Value);
}

internal sealed class SqlClientConnectionProbe(IOptions<DatabaseDiscoveryOptions> options)
    : ISqlServerConnectionProbe
{
    private readonly DatabaseDiscoveryOptions settings = Validate(options.Value);

    internal int ConfiguredConnectionTimeoutSeconds => settings.ConnectionTimeoutSeconds;
    internal int ConfiguredCommandTimeoutSeconds => settings.ConnectionTimeoutSeconds;

    public async Task<SqlServerConnectionProbeResult> ProbeAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(settings.ConnectionTimeoutSeconds));
        var token = timeout.Token;
        var connected = false;

        try
        {
            await using var database = SqlServerConnectionFactory.Create(
                connection,
                settings.ConnectionTimeoutSeconds,
                settings.SqlServerTrustServerCertificate);
            await database.OpenAsync(token);
            connected = true;

            var target = await ReadTarget(database, token);
            var schemas = await SqlServerCatalogSql.ReadResolvedSchemas(
                database,
                connection.IncludedSchemas,
                settings.ConnectionTimeoutSeconds,
                token);
            foreach (var sql in SqlServerCatalogSql.RequiredCatalogProbes)
            {
                await using var command = database.CreateCommand();
                command.CommandTimeout = settings.ConnectionTimeoutSeconds;
                command.CommandText = sql;
                await command.ExecuteScalarAsync(token);
            }

            return target with { ResolvedSchemas = schemas };
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new SqlServerProbeException(DatabaseConnectionFailure.Timeout);
        }
        catch (SqlException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new SqlServerProbeException(DatabaseConnectionFailure.Cancelled, exception.Number);
        }
        catch (SqlException exception) when (timeout.IsCancellationRequested)
        {
            throw new SqlServerProbeException(DatabaseConnectionFailure.Timeout, exception.Number);
        }
        catch (SqlException exception)
        {
            throw new SqlServerProbeException(
                SqlServerDiscoveryErrorMapper.MapConnectionFailure(exception.Number, connected),
                exception.Number);
        }
        catch (TimeoutException)
        {
            throw new SqlServerProbeException(DatabaseConnectionFailure.Timeout);
        }
    }

    internal SqlConnectionStringBuilder CreateConnectionStringBuilder(
        DatabaseDiscoveryConnectionContext connection) =>
        SqlServerConnectionFactory.CreateBuilder(
            connection,
            settings.ConnectionTimeoutSeconds,
            settings.SqlServerTrustServerCertificate);

    private async Task<SqlServerConnectionProbeResult> ReadTarget(
        SqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandTimeout = settings.ConnectionTimeoutSeconds;
        command.CommandText = SqlServerCatalogSql.TargetContext;
        await using var reader = await command.ExecuteReaderAsync(
            CommandBehavior.SingleRow,
            cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
            throw new SqlServerProbeException(DatabaseConnectionFailure.ConnectionFailed);

        var serverVersion = RequiredText(reader, 0);
        var major = checked(Convert.ToInt32(reader.GetValue(1), CultureInfo.InvariantCulture));
        return new SqlServerConnectionProbeResult(
            serverVersion,
            major,
            $"Microsoft.Data.SqlClient/{typeof(SqlConnection).Assembly.GetName().Version}",
            RequiredText(reader, 2),
            RequiredText(reader, 3),
            RequiredText(reader, 4),
            []);
    }

    private static string RequiredText(SqlDataReader reader, int ordinal) =>
        reader.IsDBNull(ordinal)
            ? throw new SqlServerProbeException(DatabaseConnectionFailure.ConnectionFailed)
            : Convert.ToString(reader.GetValue(ordinal), CultureInfo.InvariantCulture)
                ?? throw new SqlServerProbeException(DatabaseConnectionFailure.ConnectionFailed);

    private static DatabaseDiscoveryOptions Validate(DatabaseDiscoveryOptions value)
    {
        value.Validate();
        return value;
    }
}

internal static class SqlServerConnectionFactory
{
    public static SqlConnection Create(
        DatabaseDiscoveryConnectionContext connection,
        int connectionTimeoutSeconds,
        bool trustServerCertificate) =>
        new(CreateBuilder(connection, connectionTimeoutSeconds, trustServerCertificate).ConnectionString);

    public static SqlConnectionStringBuilder CreateBuilder(
        DatabaseDiscoveryConnectionContext connection,
        int connectionTimeoutSeconds,
        bool trustServerCertificate) => new()
    {
        DataSource = $"{connection.Host},{connection.Port.ToString(CultureInfo.InvariantCulture)}",
        InitialCatalog = connection.DatabaseName,
        UserID = connection.Username,
        Password = connection.Password,
        IntegratedSecurity = false,
        Pooling = false,
        Enlist = false,
        PersistSecurityInfo = false,
        MultipleActiveResultSets = false,
        ConnectTimeout = connectionTimeoutSeconds,
        Encrypt = SqlConnectionEncryptOption.Mandatory,
        TrustServerCertificate = trustServerCertificate,
        ApplicationName = "SystemKnowledgeHub.DatabaseDiscovery",
    };
}
