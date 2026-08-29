using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public interface IDatabaseConnectionSecretStore
{
    string Protect(long profileId, string plaintext);
    DatabaseConnectionSecretResolution Resolve(long profileId, DatabaseConnectionSecret? secret);
}

public sealed record DatabaseConnectionSecretResolution(
    string? Plaintext,
    DatabaseConnectionSecretFailure Failure);

public enum DatabaseConnectionSecretFailure
{
    None,
    Missing,
    Unavailable,
}

public sealed class DataProtectionDatabaseConnectionSecretStore(IDataProtectionProvider provider)
    : IDatabaseConnectionSecretStore
{
    private const int SupportedPayloadFormatVersion = 1;

    public string Protect(long profileId, string plaintext) =>
        CreateProtector(profileId).Protect(plaintext);

    public DatabaseConnectionSecretResolution Resolve(long profileId, DatabaseConnectionSecret? secret)
    {
        if (secret?.ProtectedPayload is null)
        {
            return new(null, DatabaseConnectionSecretFailure.Missing);
        }
        if (secret.PayloadFormatVersion != SupportedPayloadFormatVersion)
        {
            return new(null, DatabaseConnectionSecretFailure.Unavailable);
        }

        try
        {
            return new(CreateProtector(profileId).Unprotect(secret.ProtectedPayload), DatabaseConnectionSecretFailure.None);
        }
        catch (CryptographicException)
        {
            return new(null, DatabaseConnectionSecretFailure.Unavailable);
        }
        catch (FormatException)
        {
            return new(null, DatabaseConnectionSecretFailure.Unavailable);
        }
    }

    private IDataProtector CreateProtector(long profileId) => provider.CreateProtector(
        $"SystemKnowledgeHub.DatabaseDiscovery.ConnectionSecret/v1/{profileId}");
}
