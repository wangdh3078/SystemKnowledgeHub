using System.Data;
using System.Globalization;
using Oracle.ManagedDataAccess.Client;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.Oracle;

internal sealed class OracleConnectionTester(IOracleConnectionProbe probe) : IDatabaseConnectionTester
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.Oracle;

    public async Task<DatabaseConnectionTestResult> TestConnectionAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await probe.ProbeAsync(connection, cancellationToken);
            if (!TryGetMajor(result.ServerVersion, out var major) || major != 19)
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.UnsupportedDatabaseVersion,
                    "仅支持 Oracle Database 19c。");
            }
            if (string.IsNullOrWhiteSpace(result.ServiceName)
                || !string.Equals(result.ServiceName, connection.ServiceName, StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.ConnectionFailed,
                    "连接到的 Oracle Service 与配置目标不一致。");
            }
            if (string.Equals(result.ContainerName, "CDB$ROOT", StringComparison.OrdinalIgnoreCase))
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.ConnectionFailed,
                    "Oracle 连接必须指向单一 Service/PDB 或 non-CDB，不能使用 CDB Root。");
            }
            var missingSchemas = connection.IncludedSchemas
                .Except(result.VisibleSchemas, StringComparer.Ordinal)
                .ToArray();
            if (missingSchemas.Length > 0 || !result.HasRequiredCatalogVisibility)
            {
                return DatabaseConnectionTestResult.Fail(
                    DatabaseConnectionFailure.InsufficientPrivilege,
                    "当前 Oracle 账号无法验证全部 IncludedSchemas 或必要目录视图。");
            }

            return DatabaseConnectionTestResult.Success(
                "Oracle 19c 连接、目标上下文与基础目录可见性验证成功。",
                result.ServerVersion,
                result.ServiceName,
                result.ContainerName);
        }
        catch (OracleProbeException exception)
        {
            return DatabaseConnectionTestResult.Fail(exception.Failure, SafeSummary(exception.Failure), exception.VendorCode);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            return DatabaseConnectionTestResult.Fail(DatabaseConnectionFailure.Cancelled, "Oracle 连接测试已取消。");
        }
        catch (OperationCanceledException)
        {
            return DatabaseConnectionTestResult.Fail(DatabaseConnectionFailure.Timeout, "Oracle 连接测试超时。");
        }
        catch
        {
            return DatabaseConnectionTestResult.Fail(DatabaseConnectionFailure.ConnectionFailed, "Oracle 连接测试失败。");
        }
    }

    private static bool TryGetMajor(string value, out int major)
    {
        var first = new string(value.TakeWhile(character => char.IsDigit(character)).ToArray());
        return int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out major);
    }

    private static string SafeSummary(DatabaseConnectionFailure failure) => failure switch
    {
        DatabaseConnectionFailure.AuthenticationFailed => "Oracle 用户名或密码验证失败。",
        DatabaseConnectionFailure.InsufficientPrivilege => "Oracle 账号缺少必要的基础目录可见性。",
        DatabaseConnectionFailure.Timeout => "Oracle 连接测试超时。",
        DatabaseConnectionFailure.Cancelled => "Oracle 连接测试已取消。",
        _ => "无法建立 Oracle 连接。",
    };
}

internal interface IOracleConnectionProbe
{
    Task<OracleConnectionProbeResult> ProbeAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);
}

internal sealed record OracleConnectionProbeResult(
    string ServerVersion,
    string ServiceName,
    string? ContainerName,
    IReadOnlySet<string> VisibleSchemas,
    bool HasRequiredCatalogVisibility);

internal sealed class OracleProbeException(
    DatabaseConnectionFailure failure,
    string? vendorCode = null) : Exception
{
    public DatabaseConnectionFailure Failure { get; } = failure;
    public string? VendorCode { get; } = vendorCode;
}

internal sealed class OracleManagedConnectionProbe : IOracleConnectionProbe
{
    private const int TimeoutSeconds = 15;
    private static readonly string[] RequiredCatalogProbes =
    [
        "SELECT 1 FROM ALL_TABLES WHERE 1 = 0",
        "SELECT 1 FROM ALL_VIEWS WHERE 1 = 0",
        "SELECT 1 FROM ALL_TAB_COLUMNS WHERE 1 = 0",
        "SELECT 1 FROM ALL_TAB_COMMENTS WHERE 1 = 0",
        "SELECT 1 FROM ALL_COL_COMMENTS WHERE 1 = 0",
        "SELECT 1 FROM ALL_CONSTRAINTS WHERE 1 = 0",
        "SELECT 1 FROM ALL_CONS_COLUMNS WHERE 1 = 0",
        "SELECT 1 FROM ALL_INDEXES WHERE 1 = 0",
        "SELECT 1 FROM ALL_IND_COLUMNS WHERE 1 = 0",
        "SELECT 1 FROM ALL_IND_EXPRESSIONS WHERE 1 = 0",
        "SELECT 1 FROM ALL_SEQUENCES WHERE 1 = 0",
    ];

    public async Task<OracleConnectionProbeResult> ProbeAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(TimeoutSeconds));
        var token = timeout.Token;
        try
        {
            var host = connection.Host.Contains(':', StringComparison.Ordinal)
                ? $"[{connection.Host.Trim('[', ']')}]"
                : connection.Host;
            var builder = new OracleConnectionStringBuilder
            {
                DataSource = $"{host}:{connection.Port}/{connection.ServiceName}",
                UserID = connection.Username,
                Password = connection.Password,
                Pooling = false,
                ConnectionTimeout = TimeoutSeconds,
            };
            await using var oracleConnection = new OracleConnection(builder.ConnectionString);
            await oracleConnection.OpenAsync(token);

            var context = await ReadContext(oracleConnection, token);
            var schemas = await ReadVisibleSchemas(oracleConnection, connection.IncludedSchemas, token);
            foreach (var sql in RequiredCatalogProbes)
            {
                await ExecuteCatalogProbe(oracleConnection, sql, token);
            }

            return new OracleConnectionProbeResult(
                oracleConnection.ServerVersion,
                context.ServiceName,
                context.ContainerName,
                schemas,
                true);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            throw new OracleProbeException(DatabaseConnectionFailure.Timeout);
        }
        catch (OracleException exception) when (cancellationToken.IsCancellationRequested)
        {
            throw new OracleProbeException(
                DatabaseConnectionFailure.Cancelled,
                AllowlistedVendorCode(exception.Number));
        }
        catch (OracleException exception) when (timeout.IsCancellationRequested)
        {
            throw new OracleProbeException(
                DatabaseConnectionFailure.Timeout,
                AllowlistedVendorCode(exception.Number));
        }
        catch (OracleException exception)
        {
            var failure = MapOracleFailure(exception.Number);
            throw new OracleProbeException(failure, AllowlistedVendorCode(exception.Number));
        }
    }

    private static async Task<(string ServiceName, string? ContainerName)> ReadContext(
        OracleConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = TimeoutSeconds;
        command.CommandText = "SELECT SYS_CONTEXT('USERENV','SERVICE_NAME'), SYS_CONTEXT('USERENV','CON_NAME') FROM DUAL";
        await using var reader = await command.ExecuteReaderAsync(CommandBehavior.SingleRow, cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new OracleProbeException(DatabaseConnectionFailure.ConnectionFailed);
        }
        var serviceName = reader.IsDBNull(0) ? string.Empty : reader.GetString(0);
        var containerName = reader.IsDBNull(1) ? null : reader.GetString(1);
        return (serviceName, containerName);
    }

    private static async Task<IReadOnlySet<string>> ReadVisibleSchemas(
        OracleConnection connection,
        IReadOnlyList<string> schemas,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = TimeoutSeconds;
        var parameterNames = new string[schemas.Count];
        for (var index = 0; index < schemas.Count; index++)
        {
            parameterNames[index] = $":schema{index}";
            command.Parameters.Add($"schema{index}", OracleDbType.Varchar2, schemas[index], ParameterDirection.Input);
        }
        command.CommandText = $"SELECT USERNAME FROM ALL_USERS WHERE USERNAME IN ({string.Join(',', parameterNames)})";
        var visible = new HashSet<string>(StringComparer.Ordinal);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            visible.Add(reader.GetString(0));
        }
        return visible;
    }

    private static async Task ExecuteCatalogProbe(
        OracleConnection connection,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.BindByName = true;
        command.CommandTimeout = TimeoutSeconds;
        command.CommandText = sql;
        await command.ExecuteScalarAsync(cancellationToken);
    }

    private static DatabaseConnectionFailure MapOracleFailure(int number) => number switch
    {
        1017 or 1005 => DatabaseConnectionFailure.AuthenticationFailed,
        1031 or 942 => DatabaseConnectionFailure.InsufficientPrivilege,
        1013 => DatabaseConnectionFailure.Cancelled,
        12170 or 12535 => DatabaseConnectionFailure.Timeout,
        _ => DatabaseConnectionFailure.ConnectionFailed,
    };

    private static string? AllowlistedVendorCode(int number) =>
        number is >= 1 and <= 99999
            ? $"ORA-{number.ToString("D5", CultureInfo.InvariantCulture)}"
            : null;
}
