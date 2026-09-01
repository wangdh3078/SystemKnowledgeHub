using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.SqlServer;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class SqlServerConnectionTesterTests
{
    [Fact]
    public void SqlClient_probe_uses_typed_fixed_connection_security_and_configured_timeout()
    {
        var probe = new SqlClientConnectionProbe(Options.Create(new DatabaseDiscoveryOptions
        {
            ConnectionTimeoutSeconds = 19,
            CatalogCommandTimeoutSeconds = 83,
        }));

        var builder = probe.CreateConnectionStringBuilder(Connection("SQLSERVER_SECRET_CANARY"));

        Assert.Equal(19, probe.ConfiguredConnectionTimeoutSeconds);
        Assert.Equal(19, probe.ConfiguredCommandTimeoutSeconds);
        Assert.Equal("db.example.test,1433", builder.DataSource);
        Assert.Equal("SKH_DBDISC", builder.InitialCatalog);
        Assert.Equal("metadata_reader", builder.UserID);
        Assert.Equal("SQLSERVER_SECRET_CANARY", builder.Password);
        Assert.Equal(SqlConnectionEncryptOption.Mandatory, builder.Encrypt);
        Assert.False(builder.TrustServerCertificate);
        Assert.False(builder.Pooling);
        Assert.False(builder.PersistSecurityInfo);
        Assert.DoesNotContain("SQLSERVER_SECRET_CANARY", Connection("SQLSERVER_SECRET_CANARY").ToString());
    }

    [Fact]
    public async Task SQL_Server_2022_matching_context_and_schema_visibility_succeeds()
    {
        var result = await Tester(Result()).TestConnectionAsync(Connection("secret"), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseConnectionFailure.None, result.Failure);
        Assert.Equal("16.0.4215.2", result.ProviderVersion);
        Assert.Equal("SKH_DBDISC", result.DatabaseName);
        Assert.Null(result.ServiceName);
        Assert.DoesNotContain("metadata_reader", result.Summary, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    public async Task SQL_Server_non_16_major_is_rejected(int major)
    {
        var result = await Tester(Result() with
        {
            ServerMajorVersion = major,
            ServerVersion = $"{major}.0.1000.1",
        }).TestConnectionAsync(Connection("secret"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(DatabaseConnectionFailure.UnsupportedDatabaseVersion, result.Failure);
        Assert.Equal("仅支持 SQL Server 2022（major 16）。", result.Summary);
    }

    [Fact]
    public async Task SQL_Server_missing_or_ambiguous_schema_visibility_fails_closed()
    {
        var missing = await Tester(Result() with { ResolvedSchemas = ["dbdisc_a"] })
            .TestConnectionAsync(Connection("secret"), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.InsufficientPrivilege, missing.Failure);

        var ambiguous = await Tester(Result() with { ResolvedSchemas = ["dbdisc_a", "dbdisc_a"] })
            .TestConnectionAsync(Connection("secret"), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.ConnectionFailed, ambiguous.Failure);
        Assert.DoesNotContain("secret", ambiguous.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData(18456, false, DatabaseConnectionFailure.AuthenticationFailed, "MSSQL-18456")]
    [InlineData(229, true, DatabaseConnectionFailure.InsufficientPrivilege, "MSSQL-229")]
    [InlineData(53, false, DatabaseConnectionFailure.ConnectionFailed, "MSSQL-53")]
    public async Task SQL_Server_failures_are_normalized_without_raw_message(
        int number,
        bool connected,
        DatabaseConnectionFailure expected,
        string vendorCode)
    {
        var tester = new SqlServerConnectionTester(new DelegateProbe((_, _) =>
            throw new SqlServerProbeException(
                SqlServerDiscoveryErrorMapper.MapConnectionFailure(number, connected), number)));

        var result = await tester.TestConnectionAsync(Connection("secret-canary"), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(expected, result.Failure);
        Assert.Equal(vendorCode, result.VendorCode);
        Assert.DoesNotContain("secret-canary", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SQL_Server_timeout_and_cancellation_are_distinct_and_safe()
    {
        var timeout = await new SqlServerConnectionTester(new DelegateProbe((_, _) =>
                throw new OperationCanceledException("SQLSERVER_TIMEOUT_SECRET_CANARY")))
            .TestConnectionAsync(Connection("secret-canary"), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.Timeout, timeout.Failure);
        Assert.DoesNotContain("CANARY", timeout.Summary, StringComparison.Ordinal);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await new SqlServerConnectionTester(new DelegateProbe((_, token) =>
                throw new OperationCanceledException("SQLSERVER_CANCEL_SECRET_CANARY", token)))
            .TestConnectionAsync(Connection("secret-canary"), cancellation.Token);
        Assert.Equal(DatabaseConnectionFailure.Cancelled, cancelled.Failure);
        Assert.DoesNotContain("CANARY", cancelled.Summary, StringComparison.Ordinal);
    }

    private static SqlServerConnectionTester Tester(SqlServerConnectionProbeResult result) =>
        new(new DelegateProbe((_, _) => Task.FromResult(result)));

    private static SqlServerConnectionProbeResult Result() => new(
        "16.0.4215.2",
        16,
        "16.0.4215.2",
        "SKH_DBDISC",
        "Latin1_General_100_CS_AS_SC_UTF8",
        "metadata_reader",
        ["dbdisc_a", "dbdisc_b"]);

    private static DatabaseDiscoveryConnectionContext Connection(string password) => new(
        1,
        1,
        1,
        DatabaseProviderType.SqlServer,
        "db.example.test",
        1433,
        "SKH_DBDISC",
        null,
        "metadata_reader",
        password,
        ["dbdisc_a", "dbdisc_b"]);

    private sealed class DelegateProbe(
        Func<DatabaseDiscoveryConnectionContext, CancellationToken, Task<SqlServerConnectionProbeResult>> handler)
        : ISqlServerConnectionProbe
    {
        public Task<SqlServerConnectionProbeResult> ProbeAsync(
            DatabaseDiscoveryConnectionContext connection,
            CancellationToken cancellationToken) => handler(connection, cancellationToken);
    }
}
