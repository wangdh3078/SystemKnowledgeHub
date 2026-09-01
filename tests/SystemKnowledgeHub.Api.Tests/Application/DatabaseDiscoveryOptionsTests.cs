using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class DatabaseDiscoveryOptionsTests
{
    [Fact]
    public void Defaults_are_valid_and_keep_connection_catalog_and_worker_boundaries_distinct()
    {
        var options = new DatabaseDiscoveryOptions();

        options.Validate();

        Assert.Equal(15, options.ConnectionTimeoutSeconds);
        Assert.Equal(60, options.CatalogCommandTimeoutSeconds);
        Assert.Equal(900, options.OverallTimeoutSeconds);
        Assert.Equal(2_000, options.QueuePollIntervalMilliseconds);
        Assert.Equal(30, options.LeaseDurationSeconds);
        Assert.Equal(5, options.HeartbeatIntervalSeconds);
        Assert.Equal(2_000, options.MaximumSyncPlanActions);
        Assert.False(options.SqlServerTrustServerCertificate);
        Assert.Equal(new DatabaseDiscoveryLimits(
            128,
            25_000,
            250_000,
            250_000,
            10_000,
            128 * 1024 * 1024), options.Limits);
    }

    [Theory]
    [InlineData("ConnectionTimeoutSeconds", 0, "DatabaseDiscovery:ConnectionTimeoutSeconds")]
    [InlineData("CatalogCommandTimeoutSeconds", 3601, "DatabaseDiscovery:CatalogCommandTimeoutSeconds")]
    [InlineData("OverallTimeoutSeconds", 86401, "DatabaseDiscovery:OverallTimeoutSeconds")]
    [InlineData("MaximumIncludedSchemas", 1025, "DatabaseDiscovery:MaximumIncludedSchemas")]
    [InlineData("MaximumObjects", 0, "DatabaseDiscovery:MaximumObjects")]
    [InlineData("MaximumCanonicalSnapshotBytes", 1023, "DatabaseDiscovery:MaximumCanonicalSnapshotBytes")]
    [InlineData("LeaseDurationSeconds", 1, "DatabaseDiscovery:LeaseDurationSeconds")]
    [InlineData("HeartbeatIntervalSeconds", 30, "DatabaseDiscovery:HeartbeatIntervalSeconds")]
    [InlineData("QueuePollIntervalMilliseconds", 24, "DatabaseDiscovery:QueuePollIntervalMilliseconds")]
    [InlineData("QueuePollIntervalMilliseconds", 5000, "queue polling and heartbeat intervals")]
    [InlineData("MaximumSyncPlanActions", 10001, "DatabaseDiscovery:MaximumSyncPlanActions")]
    public void Invalid_values_fail_closed_with_the_actionable_configuration_key(
        string key,
        int value,
        string expectedDiagnostic)
    {
        var options = new DatabaseDiscoveryOptions();
        Set(options, key, value);

        var exception = Assert.Throws<InvalidOperationException>(options.Validate);

        Assert.Contains(expectedDiagnostic, exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Supported_custom_timeouts_validate_without_collapsing_their_semantics()
    {
        var options = new DatabaseDiscoveryOptions
        {
            ConnectionTimeoutSeconds = 19,
            CatalogCommandTimeoutSeconds = 83,
            OverallTimeoutSeconds = 901,
        };

        options.Validate();

        Assert.Equal(19, options.ConnectionTimeoutSeconds);
        Assert.Equal(83, options.CatalogCommandTimeoutSeconds);
        Assert.Equal(901, options.OverallTimeoutSeconds);
    }

    [Fact]
    public void Application_configuration_exposes_the_effective_worker_and_provider_defaults()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateClient();

        var options = factory.Services.GetRequiredService<IOptions<DatabaseDiscoveryOptions>>().Value;

        Assert.Equal(15, options.ConnectionTimeoutSeconds);
        Assert.Equal(60, options.CatalogCommandTimeoutSeconds);
        Assert.Equal(900, options.OverallTimeoutSeconds);
        Assert.Equal(2_000, options.QueuePollIntervalMilliseconds);
        Assert.Equal(30, options.LeaseDurationSeconds);
        Assert.Equal(5, options.HeartbeatIntervalSeconds);
        Assert.False(options.SqlServerTrustServerCertificate);
    }

    private static void Set(DatabaseDiscoveryOptions options, string key, int value)
    {
        switch (key)
        {
            case "ConnectionTimeoutSeconds": options.ConnectionTimeoutSeconds = value; break;
            case "CatalogCommandTimeoutSeconds": options.CatalogCommandTimeoutSeconds = value; break;
            case "OverallTimeoutSeconds": options.OverallTimeoutSeconds = value; break;
            case "MaximumIncludedSchemas": options.MaximumIncludedSchemas = value; break;
            case "MaximumObjects": options.MaximumObjects = value; break;
            case "MaximumCanonicalSnapshotBytes": options.MaximumCanonicalSnapshotBytes = value; break;
            case "LeaseDurationSeconds": options.LeaseDurationSeconds = value; break;
            case "HeartbeatIntervalSeconds": options.HeartbeatIntervalSeconds = value; break;
            case "QueuePollIntervalMilliseconds": options.QueuePollIntervalMilliseconds = value; break;
            case "MaximumSyncPlanActions": options.MaximumSyncPlanActions = value; break;
            default: throw new ArgumentOutOfRangeException(nameof(key), key, null);
        }
    }
}
