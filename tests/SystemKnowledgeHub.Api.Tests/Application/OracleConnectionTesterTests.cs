using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.Oracle;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class OracleConnectionTesterTests
{
    [Fact]
    public void Managed_probe_and_discovery_reader_wire_their_distinct_timeout_semantics()
    {
        var options = Options.Create(new DatabaseDiscoveryOptions
        {
            ConnectionTimeoutSeconds = 19,
            CatalogCommandTimeoutSeconds = 83,
        });

        var testConnectionProbe = new OracleManagedConnectionProbe(options);
        var discoveryCatalogReader = new OracleManagedDiscoveryCatalogReader(options);

        Assert.Equal(19, testConnectionProbe.ConfiguredConnectionTimeoutSeconds);
        Assert.Equal(19, testConnectionProbe.ConfiguredCommandTimeoutSeconds);
        Assert.Equal(19, discoveryCatalogReader.ConfiguredConnectionTimeoutSeconds);
        Assert.Equal(83, discoveryCatalogReader.ConfiguredCatalogCommandTimeoutSeconds);
    }

    [Fact]
    public async Task Oracle_19c_matching_service_non_root_and_visible_schemas_succeeds()
    {
        var tester = Tester(_ => Result());
        var result = await tester.TestConnectionAsync(Context(), CancellationToken.None);
        Assert.True(result.Succeeded);
        Assert.Equal(DatabaseConnectionFailure.None, result.Failure);
        Assert.Equal("19.0.0.0.0", result.ProviderVersion);
        Assert.DoesNotContain("secret-password", Context().ToString(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("11.2.0.4.0")]
    [InlineData("21.3.0.0.0")]
    public async Task Non_19c_versions_are_rejected_without_legacy_fallback(string version)
    {
        var tester = Tester(_ => Result(serverVersion: version));
        var result = await tester.TestConnectionAsync(Context(), CancellationToken.None);
        Assert.False(result.Succeeded);
        Assert.Equal(DatabaseConnectionFailure.UnsupportedDatabaseVersion, result.Failure);
    }

    [Fact]
    public async Task Service_root_and_catalog_visibility_boundaries_are_enforced()
    {
        var wrongService = await Tester(_ => Result(serviceName: "OTHER_PDB"))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.ConnectionFailed, wrongService.Failure);
        var root = await Tester(_ => Result(containerName: "CDB$ROOT"))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.ConnectionFailed, root.Failure);
        var missingSchema = await Tester(_ => Result(visibleSchemas: new HashSet<string>(StringComparer.Ordinal) { "OTHER" }))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.InsufficientPrivilege, missingSchema.Failure);
        var missingCatalog = await Tester(_ => Result(hasRequiredCatalogVisibility: false))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.InsufficientPrivilege, missingCatalog.Failure);
    }

    [Theory]
    [InlineData(DatabaseConnectionFailure.AuthenticationFailed, "ORA-01017")]
    [InlineData(DatabaseConnectionFailure.ConnectionFailed, "ORA-12541")]
    [InlineData(DatabaseConnectionFailure.InsufficientPrivilege, "ORA-01031")]
    [InlineData(DatabaseConnectionFailure.Timeout, "ORA-12170")]
    public async Task Probe_failures_expose_only_normalized_and_allowlisted_values(
        DatabaseConnectionFailure failure,
        string vendorCode)
    {
        var tester = new OracleConnectionTester(new DelegateOracleProbe((_, _) =>
            throw new OracleProbeException(failure, vendorCode)));
        var result = await tester.TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(failure, result.Failure);
        Assert.Equal(vendorCode, result.VendorCode);
        Assert.DoesNotContain("secret-password", result.Summary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Timeout_cancellation_and_unexpected_provider_failure_are_normalized()
    {
        var timeout = await new OracleConnectionTester(new DelegateOracleProbe((_, _) =>
            throw new OperationCanceledException()))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.Timeout, timeout.Failure);

        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var cancelled = await new OracleConnectionTester(new DelegateOracleProbe((_, _) =>
            throw new OperationCanceledException()))
            .TestConnectionAsync(Context(), cancellation.Token);
        Assert.Equal(DatabaseConnectionFailure.Cancelled, cancelled.Failure);

        var unexpected = await new OracleConnectionTester(new DelegateOracleProbe((_, _) =>
            throw new InvalidOperationException("secret-password raw provider exception")))
            .TestConnectionAsync(Context(), CancellationToken.None);
        Assert.Equal(DatabaseConnectionFailure.ConnectionFailed, unexpected.Failure);
        Assert.DoesNotContain("secret-password", unexpected.Summary, StringComparison.Ordinal);
    }

    private static OracleConnectionTester Tester(Func<DatabaseDiscoveryConnectionContext, OracleConnectionProbeResult> result) =>
        new(new DelegateOracleProbe((connection, _) => Task.FromResult(result(connection))));

    private static OracleConnectionProbeResult Result(
        string serverVersion = "19.0.0.0.0",
        string serviceName = "APP_PDB",
        string? containerName = "APP_PDB",
        IReadOnlySet<string>? visibleSchemas = null,
        bool hasRequiredCatalogVisibility = true) => new(
            serverVersion,
            serviceName,
            containerName,
            visibleSchemas ?? new HashSet<string>(StringComparer.Ordinal) { "APP_OWNER" },
            hasRequiredCatalogVisibility);

    private static DatabaseDiscoveryConnectionContext Context() => new(
        1,
        1,
        1,
        DatabaseProviderType.Oracle,
        "db.example.test",
        1521,
        null,
        "APP_PDB",
        "METADATA_READER",
        "secret-password",
        ["APP_OWNER"]);

    private sealed class DelegateOracleProbe(
        Func<DatabaseDiscoveryConnectionContext, CancellationToken, Task<OracleConnectionProbeResult>> handler)
        : IOracleConnectionProbe
    {
        public Task<OracleConnectionProbeResult> ProbeAsync(
            DatabaseDiscoveryConnectionContext connection,
            CancellationToken cancellationToken) => handler(connection, cancellationToken);
    }
}
