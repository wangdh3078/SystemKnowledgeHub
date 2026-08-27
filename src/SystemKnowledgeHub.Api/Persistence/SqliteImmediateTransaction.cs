using System.Data;
using System.Data.Common;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;

namespace SystemKnowledgeHub.Api.Persistence;

/// <summary>
/// Acquires SQLite's write reservation before authoritative reads so a dependent
/// mutation and a root deletion cannot both validate against stale active state.
/// </summary>
public sealed class SqliteImmediateTransaction : IAsyncDisposable
{
    private readonly KnowledgeHubDbContext dbContext;
    private readonly SqliteTransaction? providerTransaction;
    private readonly IDbContextTransaction? efTransaction;
    private readonly bool closeConnection;
    private readonly bool ownsTransaction;

    private SqliteImmediateTransaction(
        KnowledgeHubDbContext dbContext,
        SqliteTransaction? providerTransaction,
        IDbContextTransaction? efTransaction,
        bool closeConnection,
        bool ownsTransaction)
    {
        this.dbContext = dbContext;
        this.providerTransaction = providerTransaction;
        this.efTransaction = efTransaction;
        this.closeConnection = closeConnection;
        this.ownsTransaction = ownsTransaction;
    }

    public static async Task<SqliteImmediateTransaction> BeginAsync(
        KnowledgeHubDbContext dbContext,
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.CurrentTransaction is not null)
        {
            return new SqliteImmediateTransaction(dbContext, null, null, false, false);
        }

        var connection = dbContext.Database.GetDbConnection();
        if (connection is not SqliteConnection sqliteConnection)
        {
            throw new InvalidOperationException("This transaction boundary requires the configured SQLite provider.");
        }

        var closeConnection = connection.State != ConnectionState.Open;
        if (closeConnection)
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }

        try
        {
            var providerTransaction = sqliteConnection.BeginTransaction(
                IsolationLevel.Serializable,
                deferred: false);
            var efTransaction = await dbContext.Database.UseTransactionAsync(
                providerTransaction,
                cancellationToken)
                ?? throw new InvalidOperationException("EF Core could not enlist in the SQLite transaction.");
            return new SqliteImmediateTransaction(
                dbContext,
                providerTransaction,
                efTransaction,
                closeConnection,
                true);
        }
        catch
        {
            if (closeConnection)
            {
                await dbContext.Database.CloseConnectionAsync();
            }
            throw;
        }
    }

    public Task CommitAsync(CancellationToken cancellationToken) =>
        ownsTransaction
            ? efTransaction!.CommitAsync(cancellationToken)
            : Task.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        if (!ownsTransaction)
        {
            return;
        }

        await efTransaction!.DisposeAsync();
        await providerTransaction!.DisposeAsync();
        if (closeConnection)
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }
}
