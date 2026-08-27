using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Features.Search.Application;

public sealed class KnowledgeDocumentSearchIndex(KnowledgeHubDbContext dbContext)
{
    public async Task Upsert(KnowledgeDocument document, CancellationToken cancellationToken)
    {
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"DELETE FROM knowledge_documents_fts WHERE rowid = {document.Id};",
            cancellationToken);
        if (document.IsDeleted)
        {
            return;
        }
        await dbContext.Database.ExecuteSqlInterpolatedAsync(
            $"INSERT INTO knowledge_documents_fts(rowid, title, summary, body_text) VALUES ({document.Id}, {KnowledgeDocumentSearchText.ToIndexText(document.Title)}, {KnowledgeDocumentSearchText.ToIndexText(document.Summary)}, {KnowledgeDocumentSearchText.ToIndexText(document.BodyMarkdown)});",
            cancellationToken);
    }

    public async Task Rebuild(CancellationToken cancellationToken)
    {
        var documents = await dbContext.KnowledgeDocuments
            .AsNoTracking()
            .Where(document => !document.IsDeleted)
            .ToArrayAsync(cancellationToken);
        await dbContext.Database.ExecuteSqlRawAsync("DELETE FROM knowledge_documents_fts;", cancellationToken);
        foreach (var document in documents)
        {
            await Upsert(document, cancellationToken);
        }
    }
}
