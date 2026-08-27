using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Search.Application;

/// <summary>Controlled operator-only FTS recovery entry point; it is intentionally not an HTTP API.</summary>
public static class KnowledgeDocumentSearchMaintenanceCommand
{
    public const string CommandName = "rebuild-knowledge-document-search";

    public static bool IsRequested(string[] args) =>
        args.Length > 0 && string.Equals(args[0], CommandName, StringComparison.Ordinal);

    public static async Task<int> RunAsync(string[] args, IServiceProvider services)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine($"用法：{CommandName}");
            return 1;
        }

        await using var scope = services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var index = scope.ServiceProvider.GetRequiredService<KnowledgeDocumentSearchIndex>();
        await using var transaction = await SqliteImmediateTransaction.BeginAsync(dbContext, CancellationToken.None);
        var activeCount = await dbContext.KnowledgeDocuments.CountAsync();
        await index.Rebuild(CancellationToken.None);
        await transaction.CommitAsync(CancellationToken.None);
        Console.WriteLine($"KnowledgeDocument FTS 已重建：activeDocuments={activeCount}。");
        return 0;
    }
}
