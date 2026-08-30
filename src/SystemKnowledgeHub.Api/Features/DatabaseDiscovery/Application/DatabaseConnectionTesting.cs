using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public interface IDatabaseConnectionTester
{
    DatabaseProviderType ProviderType { get; }

    Task<DatabaseConnectionTestResult> TestConnectionAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);
}

public sealed class DatabaseDiscoveryConnectionContext
{
    internal DatabaseDiscoveryConnectionContext(
        long profileId,
        long configurationRevision,
        long secretVersion,
        DatabaseProviderType providerType,
        string host,
        int port,
        string? databaseName,
        string? serviceName,
        string username,
        string password,
        IReadOnlyList<string> includedSchemas)
    {
        ProfileId = profileId;
        ConfigurationRevision = configurationRevision;
        SecretVersion = secretVersion;
        ProviderType = providerType;
        Host = host;
        Port = port;
        DatabaseName = databaseName;
        ServiceName = serviceName;
        Username = username;
        Password = password;
        IncludedSchemas = includedSchemas;
    }

    public long ProfileId { get; }
    public long ConfigurationRevision { get; }
    public long SecretVersion { get; }
    public DatabaseProviderType ProviderType { get; }
    public string Host { get; }
    public int Port { get; }
    public string? DatabaseName { get; }
    public string? ServiceName { get; }
    public string Username { get; }
    public IReadOnlyList<string> IncludedSchemas { get; }
    internal string Password { get; }

    public override string ToString() => $"DatabaseDiscoveryConnectionContext(ProfileId={ProfileId}, ProviderType={ProviderType})";
}

public sealed record DatabaseConnectionTestResult(
    bool Succeeded,
    DatabaseConnectionFailure Failure,
    string Summary,
    string? VendorCode = null,
    string? ProviderVersion = null,
    string? DatabaseName = null,
    string? ServiceName = null,
    string? ContainerName = null)
{
    public static DatabaseConnectionTestResult Success(
        string summary,
        string providerVersion,
        string? databaseName,
        string? serviceName,
        string? containerName) =>
        new(
            true,
            DatabaseConnectionFailure.None,
            summary,
            null,
            providerVersion,
            databaseName,
            serviceName,
            containerName);

    public static DatabaseConnectionTestResult Fail(
        DatabaseConnectionFailure failure,
        string summary,
        string? vendorCode = null) =>
        new(false, failure, summary, vendorCode);
}
