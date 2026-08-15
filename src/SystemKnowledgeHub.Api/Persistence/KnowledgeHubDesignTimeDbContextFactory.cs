using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace SystemKnowledgeHub.Api.Persistence;

public sealed class KnowledgeHubDesignTimeDbContextFactory
    : IDesignTimeDbContextFactory<KnowledgeHubDbContext>
{
    public KnowledgeHubDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseSqlite("Data Source=App_Data/system-knowledge-hub.db")
            .Options;

        return new KnowledgeHubDbContext(options);
    }
}
