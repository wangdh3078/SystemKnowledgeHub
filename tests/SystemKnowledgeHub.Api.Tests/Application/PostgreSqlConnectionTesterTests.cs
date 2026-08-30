using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.PostgreSql;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class PostgreSqlConnectionTesterTests
{
    [Fact]
    public void Npgsql_probe_uses_typed_target_and_connection_test_timeout()
    {
        var probe = new NpgsqlConnectionProbe(Options.Create(new DatabaseDiscoveryOptions
        {
            ConnectionTimeoutSeconds = 23,
            CatalogCommandTimeoutSeconds = 89,
        }));

        var builder = probe.CreateConnectionStringBuilder(Context());

        Assert.Equal(23, probe.ConfiguredConnectionTimeoutSeconds);
        Assert.Equal(23, probe.ConfiguredCommandTimeoutSeconds);
        Assert.Equal("db.example.test", builder.Host);
        Assert.Equal(55432, builder.Port);
        Assert.Equal("knowledge_hub", builder.Database);
        Assert.Equal("metadata_reader", builder.Username);
        Assert.Equal("secret-password", builder.Password);
        Assert.False(builder.Pooling);
        Assert.False(builder.Enlist);
        Assert.Equal(23, builder.Timeout);
        Assert.Equal(23, builder.CommandTimeout);
        Assert.False(builder.IncludeErrorDetail);
    }

    [Fact]
    public async Task PostgreSql_18_matching_database_and_visible_schemas_succeeds()
    {
        var result = await Tester(_ => Result()).TestConnectionAsync(Context(), CancellationToken.None);

        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseConnectionFailure.None, result.Failure);
        Assert.Equal("18.1", result.ProviderVersion);
        Assert.Equal("knowledge_hub", result.DatabaseName);
        Assert.Null(result.ServiceName);
        Assert.Null(result.ContainerName);
        Assert.DoesNotContain("secret-password", Context().ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(19)]
    public async Task Unsupported_server_major_is_rejected_without_fallback(int major)
    {
        var result = await Tester(_ => Result(serverMajorVersion: major))
            .TestConnectionAsync(Context(), CancellationToken.None);

        Assert.False(result.Succeeded);
        Assert.Equal(DatabaseConnectionFailure.UnsupportedDatabaseVersion, result.Failure);
    }

    [Fact]
    public async Task Database_schema_and_catalog_visibility_boundaries_are_enforced()
    {
        var wrongDatabase = await Tester(_ => Result(databaseName: "other_database"))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.ConnectionFailed, wrongDatabase.Failure);

        var wrongCaseDatabase = await Tester(_ => Result(databaseName: "KNOWLEDGE_HUB"))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.ConnectionFailed, wrongCaseDatabase.Failure);

        var missingSchema = await Tester(_ => Result(
                visibleSchemas: new HashSet<string>(StringComparer.Ordinal) { "dbdisc_a" }))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.InsufficientPrivilege, missingSchema.Failure);

        var wrongCaseSchema = await Tester(_ => Result(
                visibleSchemas: new HashSet<string>(StringComparer.Ordinal) { "dbdisc_a", "DBDISC_B" }))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.InsufficientPrivilege, wrongCaseSchema.Failure);

        var missingCatalog = await Tester(_ => Result(hasRequiredCatalogVisibility: false))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.InsufficientPrivilege, missingCatalog.Failure);
    }

    [Theory]
    [InlineData(DatabaseConnectionFailure.AuthenticationFailed, "28P01", "SQLSTATE-28P01")]
    [InlineData(DatabaseConnectionFailure.ConnectionFailed, "57P03", "SQLSTATE-57P03")]
    [InlineData(DatabaseConnectionFailure.InsufficientPrivilege, "42501", "SQLSTATE-42501")]
    [InlineData(DatabaseConnectionFailure.Timeout, "57014", "SQLSTATE-57014")]
    public async Task Probe_failures_expose_only_normalized_and_allowlisted_values(
        DatabaseConnectionFailure failure,
        string sqlState,
        string vendorCode)
    {
        var tester = new PostgreSqlConnectionTester(new DelegatePostgreSqlProbe((_, _) =>
            throw new PostgreSqlProbeException(failure, sqlState)));

        var result = await tester.TestConnectionAsync(Context(), CancellationToken.None);

        Assert.Equal(failure, result.Failure);
        Assert.Equal(vendorCode, result.VendorCode);
        Assert.DoesNotContain("secret-password", result.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", result.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Unsafe_vendor_value_timeout_cancellation_and_unexpected_failure_are_normalized()
    {
        var unsafeVendorValue = await new PostgreSqlConnectionTester(new DelegatePostgreSqlProbe((_, _) =>
                throw new PostgreSqlProbeException(
                    DatabaseConnectionFailure.ConnectionFailed,
                    "28P01 secret-password SELECT")))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Null(unsafeVendorValue.VendorCode);

        var timeout = await new PostgreSqlConnectionTester(new DelegatePostgreSqlProbe((_, _) =>
                throw new OperationCanceledException()))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.Timeout, timeout.Failure);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await new PostgreSqlConnectionTester(new DelegatePostgreSqlProbe((_, _) =>
                throw new OperationCanceledException()))
            .TestConnectionAsync(Context(), cancellation.Token);
        Assert.Equal(DatabaseConnectionFailure.Cancelled, cancelled.Failure);

        var unexpected = await new PostgreSqlConnectionTester(new DelegatePostgreSqlProbe((_, _) =>
                throw new InvalidOperationException("secret-password raw provider exception SELECT *")))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.ConnectionFailed, unexpected.Failure);
        Assert.DoesNotContain("secret-password", unexpected.Summary, StringComparison.Ordinal);
        Assert.DoesNotContain("SELECT", unexpected.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Missing_database_name_fails_without_invoking_probe()
    {
        var invoked = false;
        var tester = new PostgreSqlConnectionTester(new DelegatePostgreSqlProbe((_, _) =>
        {
            invoked = true;
            return Task.FromResult(Result());
        }));

        var result = await tester.TestConnectionAsync(Context(databaseName: null), CancellationToken.None);

        Assert.False(invoked);
        Assert.Equal(DatabaseConnectionFailure.ConnectionFailed, result.Failure);
    }

    private static PostgreSqlConnectionTester Tester(
        Func<DatabaseDiscoveryConnectionContext, PostgreSqlConnectionProbeResult> result) =>
        new(new DelegatePostgreSqlProbe((connection, _) => Task.FromResult(result(connection))));

    private static PostgreSqlConnectionProbeResult Result(
        string serverVersion = "18.1",
        int serverMajorVersion = 18,
        string databaseName = "knowledge_hub",
        IReadOnlySet<string>? visibleSchemas = null,
        bool hasRequiredCatalogVisibility = true) => new(
            serverVersion,
            serverMajorVersion,
            databaseName,
            visibleSchemas ?? new HashSet<string>(StringComparer.Ordinal) { "dbdisc_a", "dbdisc_b" },
            hasRequiredCatalogVisibility);

    private static DatabaseDiscoveryConnectionContext Context(string? databaseName = "knowledge_hub") => new(
        1,
        1,
        1,
        DatabaseProviderType.PostgreSql,
        "db.example.test",
        55432,
        databaseName,
        null,
        "metadata_reader",
        "secret-password",
        ["dbdisc_a", "dbdisc_b"]);

    private sealed class DelegatePostgreSqlProbe(
        Func<DatabaseDiscoveryConnectionContext, CancellationToken, Task<PostgreSqlConnectionProbeResult>> handler)
        : IPostgreSqlConnectionProbe
    {
        public Task<PostgreSqlConnectionProbeResult> ProbeAsync(
            DatabaseDiscoveryConnectionContext connection,
            CancellationToken cancellationToken) => handler(connection, cancellationToken);
    }
}
