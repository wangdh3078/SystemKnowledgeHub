using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Api.Contracts;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;
using static SystemKnowledgeHub.Api.Tests.TestSupport.AnalysisWorkspaceTestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class AnalysisWorkspaceConcurrencyTests
{
    [Theory]
    [InlineData("root-create")]
    [InlineData("ancestor-move")]
    [InlineData("reorder-create")]
    [InlineData("reorder-remove")]
    [InlineData("delete-empty-create-child")]
    [InlineData("same-document")]
    public async Task Independent_requests_serialize_and_reject_stale_organization_snapshots(string scenario)
    {
        using var factory = new AnalysisRaceFactory();
        using var firstClient = factory.CreateAuthenticatedClient();
        using var secondClient = factory.CreateAuthenticatedClient();
        HttpRequestMessage firstRequest;
        HttpRequestMessage secondRequest;
        var firstStatus = HttpStatusCode.OK;
        if (scenario == "root-create")
        {
            var tree = await Tree(firstClient);
            firstRequest = Request(HttpMethod.Post, "/api/analysis/folders", new CreateAnalysisFolderRequest(null, "first", tree.TreeConcurrencyToken));
            secondRequest = Request(HttpMethod.Post, "/api/analysis/folders", new CreateAnalysisFolderRequest(null, "second", tree.TreeConcurrencyToken));
            firstStatus = HttpStatusCode.Created;
        }
        else
        {
            var a = (await Folder(firstClient, "A")).Node!;
            var b = (await Folder(firstClient, "B")).Node!;
            var child = (await Folder(firstClient, "child", a.Id)).Node!;
            var document = await Document(firstClient);
            var documentId = document.GetProperty("id").GetInt64();
            var placement = scenario == "reorder-remove" ? (await Placement(firstClient, documentId)).Node : null;
            var tree = await Tree(firstClient);
            var reverse = tree.Items.Where(node => node.ParentId is null).Reverse()
                .Select(node => new AnalysisChildOrderItem(node.Id, node.ConcurrencyToken)).ToArray();
            (firstRequest, secondRequest) = scenario switch
            {
                "ancestor-move" => (
                    Request(HttpMethod.Post, $"/api/analysis/nodes/{a.Id}/move", new MoveAnalysisNodeRequest(b.Id, 0, a.ConcurrencyToken, tree.TreeConcurrencyToken)),
                    Request(HttpMethod.Put, $"/api/analysis/folders/{child.Id}/title", new RenameAnalysisFolderRequest("stale child", child.ConcurrencyToken, tree.TreeConcurrencyToken))),
                "reorder-create" => (
                    Request(HttpMethod.Put, "/api/analysis/children/order", new ReorderAnalysisChildrenRequest(null, reverse, tree.TreeConcurrencyToken)),
                    Request(HttpMethod.Post, "/api/analysis/folders", new CreateAnalysisFolderRequest(null, "stale insert", tree.TreeConcurrencyToken))),
                "reorder-remove" => (
                    Request(HttpMethod.Put, "/api/analysis/children/order", new ReorderAnalysisChildrenRequest(null, reverse, tree.TreeConcurrencyToken)),
                    Request(HttpMethod.Delete, $"/api/analysis/document-placements/{placement!.Id}", new RemoveAnalysisNodeRequest(placement.ConcurrencyToken, tree.TreeConcurrencyToken))),
                "delete-empty-create-child" => (
                    Request(HttpMethod.Delete, $"/api/analysis/folders/{b.Id}", new RemoveAnalysisNodeRequest(b.ConcurrencyToken, tree.TreeConcurrencyToken)),
                    Request(HttpMethod.Post, "/api/analysis/folders", new CreateAnalysisFolderRequest(b.Id, "stale child", tree.TreeConcurrencyToken))),
                "same-document" => (
                    Request(HttpMethod.Post, "/api/analysis/document-placements", new AddAnalysisPlacementRequest(null, documentId, tree.TreeConcurrencyToken)),
                    Request(HttpMethod.Post, "/api/analysis/document-placements", new AddAnalysisPlacementRequest(a.Id, documentId, tree.TreeConcurrencyToken))),
                _ => throw new InvalidOperationException(scenario),
            };
            if (scenario == "same-document") firstStatus = HttpStatusCode.Created;
        }
        using (firstRequest)
        using (secondRequest)
        {
            var results = await Race(factory, () => firstClient.SendAsync(firstRequest), () => secondClient.SendAsync(secondRequest));
            using var first = results.First;
            using var second = results.Second;
            await Mutation(first, firstStatus);
            await Code(second, HttpStatusCode.Conflict, "conflict");
        }
        var final = await Tree(firstClient);
        foreach (var siblings in final.Items.GroupBy(node => node.ParentId))
            Assert.Equal(Enumerable.Range(0, siblings.Count()), siblings.Select(node => node.SortOrder).Order());
        Assert.DoesNotContain(final.Items, node => node.Title.StartsWith("stale", StringComparison.Ordinal));
        if (scenario == "root-create") Assert.Single(final.Items);
        if (scenario == "same-document") Assert.Single(final.Items.Where(node => node.NodeType == "Document"));
        using var scope = factory.Services.CreateScope();
        Assert.Equal(0, await Scalar(scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>(), "SELECT count(*) FROM pragma_foreign_key_check"));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Canonical_delete_and_placement_create_have_both_valid_serial_orders(bool deleteFirst)
    {
        using var factory = new AnalysisRaceFactory();
        using var firstClient = factory.CreateAuthenticatedClient();
        using var secondClient = factory.CreateAuthenticatedClient();
        var document = await Document(firstClient, "deleted title canary");
        var id = document.GetProperty("id").GetInt64();
        var tree = await Tree(firstClient);
        using var deleteRequest = Request(HttpMethod.Delete, $"/api/knowledge-documents/{id}",
            new { concurrencyToken = document.GetProperty("concurrencyToken").GetString() });
        using var placementRequest = Request(HttpMethod.Post, "/api/analysis/document-placements",
            new AddAnalysisPlacementRequest(null, id, tree.TreeConcurrencyToken));
        var results = await Race(factory,
            () => firstClient.SendAsync(deleteFirst ? deleteRequest : placementRequest),
            () => secondClient.SendAsync(deleteFirst ? placementRequest : deleteRequest));
        using var first = results.First;
        using var second = results.Second;
        Assert.Equal(deleteFirst ? HttpStatusCode.NoContent : HttpStatusCode.Created, first.StatusCode);
        if (deleteFirst) await Code(second, HttpStatusCode.UnprocessableEntity, "reference_invalid");
        else Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        var final = await Tree(firstClient);
        if (deleteFirst) Assert.Empty(final.Items);
        else
        {
            var node = Assert.Single(final.Items);
            Assert.Equal("Unavailable", node.Availability);
            Assert.Equal("文档不可用", node.Title);
            Assert.Null(node.DocumentType);
        }
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.True((await db.KnowledgeDocuments.IgnoreQueryFilters().SingleAsync(row => row.Id == id)).IsDeleted);
        Assert.Equal(1, await db.KnowledgeDocumentRevisions.CountAsync(row => row.KnowledgeDocumentId == id));
        Assert.Equal(0, await Scalar(db, "SELECT count(*) FROM pragma_foreign_key_check"));
    }

    private static async Task<(HttpResponseMessage First, HttpResponseMessage Second)> Race(AnalysisRaceFactory factory,
        Func<Task<HttpResponseMessage>> firstAction, Func<Task<HttpResponseMessage>> secondAction)
    {
        factory.Gate.Armed = true;
        var first = Task.Run(firstAction);
        Task<HttpResponseMessage>? second = null;
        try
        {
            await factory.Gate.Held.Task.WaitAsync(TimeSpan.FromSeconds(20));
            second = Task.Run(secondAction);
            await factory.Gate.ContenderOpened.Task.WaitAsync(TimeSpan.FromSeconds(20));
            Assert.True(factory.Gate.HasWriteTransaction);
        }
        finally { factory.Gate.Release.TrySetResult(); }
        var firstResponse = await first;
        var secondResponse = await second!;
        factory.Gate.Armed = false;
        return (firstResponse, secondResponse);
    }
}
