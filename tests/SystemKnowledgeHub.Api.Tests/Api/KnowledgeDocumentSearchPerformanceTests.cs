using System.Diagnostics;
using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit.Abstractions;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class KnowledgeDocumentSearchPerformanceTests(
    BootstrapWebApplicationFactory factory,
    ITestOutputHelper output) : IClassFixture<BootstrapWebApplicationFactory>
{
    [Fact]
    public async Task Fts_query_returns_a_bounded_document_group_for_one_thousand_representative_documents()
    {
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var timestamp = DateTimeOffset.UtcNow;
            var user = await dbContext.Users.OrderBy(item => item.Id).FirstAsync();
            var body = string.Concat(Enumerable.Repeat("检查 Oracle 数据库监听服务是否正常运行；ORA-12541 需要确认 Listener。", 45));
            dbContext.KnowledgeDocuments.AddRange(Enumerable.Range(0, 1_000).Select(index => new KnowledgeDocument
            {
                DocumentType = DocumentType.Sop,
                Title = $"Performance SOP {index}",
                BodyMarkdown = body,
                LifecycleStatus = DocumentLifecycleStatus.Published,
                KnowledgeStatus = (index % 3) switch { 0 => KnowledgeStatus.Unknown, 1 => KnowledgeStatus.Inferred, _ => KnowledgeStatus.Confirmed },
                KnowledgeStatusChangedAt = timestamp,
                KnowledgeStatusChangedByName = user.DisplayName,
                KnowledgeStatusChangedByRole = "性能测试",
                CreatedByUserId = user.Id,
                CreatedByDisplayName = user.DisplayName,
                UpdatedByUserId = user.Id,
                UpdatedByDisplayName = user.DisplayName,
                CreatedAt = timestamp,
                UpdatedAt = timestamp,
                Version = 1,
            }));
            await dbContext.SaveChangesAsync();
            await scope.ServiceProvider.GetRequiredService<KnowledgeDocumentSearchIndex>().Rebuild(CancellationToken.None);
        }

        using var client = factory.CreateAuthenticatedClient();
        var stopwatch = Stopwatch.StartNew();
        using var response = await client.GetAsync("/api/search?q=%E7%9B%91%E5%90%AC&types=KnowledgeDocument&limitPerGroup=5");
        stopwatch.Stop();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        output.WriteLine($"documents=1000; approximateBodyChars=1620; query=监听; elapsedMs={stopwatch.ElapsedMilliseconds}");
    }
}
