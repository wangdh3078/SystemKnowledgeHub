using System.Net.Sockets;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

internal enum DiscoveryFailureStage
{
    WorkerLoop, LoadWork, CapabilityDetection, MetadataDiscovery, CanonicalPreparation,
    DifferenceCalculation, SnapshotPersistence, Finalize, LeaseMonitor, ConnectionTest,
}

internal enum DiscoveryDiagnosticCategory
{
    KnownProviderFailure, PersistenceConcurrency, PersistenceConstraint,
    PersistenceFailure, EnvironmentFailure, UnexpectedProgramFailure, ValidationFailure,
}

internal static class DatabaseDiscoveryDiagnostics
{
    // Never pass the exception object, Message, Data, StackTrace or connection context to ILogger.
    public static void Log(ILogger logger, Exception? exception, DiscoveryFailureStage stage,
        string publicErrorCode, long? runId = null, long? profileId = null,
        DatabaseProviderType? providerType = null, string? vendorCode = null, bool knownProvider = false)
    {
        var cause = exception is DatabaseDiscoveryProviderException { InnerException: not null } provider
            ? provider.InnerException! : exception;
        var category = cause switch
        {
            null when publicErrorCode == "ConcurrencyConflict" => DiscoveryDiagnosticCategory.PersistenceConcurrency,
            null => DiscoveryDiagnosticCategory.ValidationFailure,
            DbUpdateConcurrencyException => DiscoveryDiagnosticCategory.PersistenceConcurrency,
            DbUpdateException update when update.InnerException is SqliteException { SqliteErrorCode: 19 }
                => DiscoveryDiagnosticCategory.PersistenceConstraint,
            SqliteException { SqliteErrorCode: 19 } => DiscoveryDiagnosticCategory.PersistenceConstraint,
            DbUpdateException => DiscoveryDiagnosticCategory.PersistenceFailure,
            IOException or SocketException => DiscoveryDiagnosticCategory.EnvironmentFailure,
            _ when knownProvider || cause is DatabaseDiscoveryProviderException
                || cause is Oracle.ManagedDataAccess.Client.OracleException
                || cause is Npgsql.NpgsqlException || cause is Microsoft.Data.SqlClient.SqlException
                || cause is TimeoutException or OperationCanceledException
                => DiscoveryDiagnosticCategory.KnownProviderFailure,
            _ => DiscoveryDiagnosticCategory.UnexpectedProgramFailure,
        };
        logger.Log(stage == DiscoveryFailureStage.WorkerLoop ? LogLevel.Error : LogLevel.Warning,
            "Database Discovery failure RunId={RunId} ProfileId={ProfileId} ProviderType={ProviderType} Stage={Stage} PublicErrorCode={PublicErrorCode} DiagnosticCategory={DiagnosticCategory} ExceptionType={ExceptionType} HResult={HResult} VendorCode={VendorCode}",
            runId, profileId, providerType, stage, DatabaseDiscoveryFailureSafety.SafeCode(publicErrorCode),
            category, cause?.GetType().FullName, cause?.HResult,
            DatabaseDiscoveryFailureSafety.SafeVendorCode(vendorCode ?? (exception as DatabaseDiscoveryProviderException)?.VendorCode));
    }
}
