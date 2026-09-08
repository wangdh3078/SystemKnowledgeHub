using System.Net;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Api.Contracts;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;
using static SystemKnowledgeHub.Api.Tests.TestSupport.AnalysisWorkspaceTestSupport;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class AnalysisWorkspacePersistenceTests
{
    private const string Previous = "20260907133706_AddHumanConfirmationCorrectionLifecycle";
    private const string AnalysisMigration = "20260908130346_AddAnalysisWorkspaceTreeFoundation";

    [Fact]
    public async Task Additive_upgrade_and_disposable_down_preserve_every_existing_table_and_row()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        await ProtectedDocument(factory, client);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var migrator = db.GetService<IMigrator>();
        await migrator.MigrateAsync(Previous);
        var before = await CanonicalRows(db);
        var schemaBefore = await ExistingSchema(db);
        await migrator.MigrateAsync(AnalysisMigration);
        Assert.Equal(before.OrderBy(pair => pair.Key), (await CanonicalRows(db)).OrderBy(pair => pair.Key));
        Assert.Equal(schemaBefore, await ExistingSchema(db));
        Assert.Empty(await db.AnalysisNodes.AsNoTracking().ToArrayAsync());
        Assert.Equal(1, await Scalar(db, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='analysis_nodes'"));
        Assert.Equal(0, await Scalar(db, "SELECT count(*) FROM pragma_foreign_key_check"));
        await migrator.MigrateAsync(Previous);
        Assert.Equal(0, await Scalar(db, "SELECT count(*) FROM sqlite_master WHERE type='table' AND name='analysis_nodes'"));
        Assert.Equal(before.OrderBy(pair => pair.Key), (await CanonicalRows(db)).OrderBy(pair => pair.Key));
        Assert.Equal(schemaBefore, await ExistingSchema(db));
    }

    [Fact]
    public async Task Fresh_migration_has_exact_shape_restrict_fks_unique_orders_and_document_barrier()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var document = await Document(client);
        var documentId = document.GetProperty("id").GetInt64();
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(9, await Scalar(db, "SELECT count(*) FROM pragma_table_info('analysis_nodes')"));
        Assert.Equal(2, await Scalar(db, "SELECT count(*) FROM pragma_foreign_key_list('analysis_nodes') WHERE on_delete='RESTRICT'"));
        Assert.Equal(3, await Scalar(db, "SELECT count(*) FROM pragma_index_list('analysis_nodes') WHERE [unique]=1 AND partial=1"));
        await db.Database.ExecuteSqlRawAsync("""
            INSERT INTO analysis_nodes(id,parent_id,node_type,title,knowledge_document_id,sort_order,created_at,updated_at,version)
            VALUES (80001,NULL,'Folder','Root',NULL,0,'2026-09-08','2026-09-08',1),
                   (80002,80001,'Folder','Child',NULL,0,'2026-09-08','2026-09-08',1);
            """);
        await db.Database.ExecuteSqlInterpolatedAsync($"INSERT INTO analysis_nodes(id,parent_id,node_type,title,knowledge_document_id,sort_order,created_at,updated_at,version) VALUES (80003,NULL,'Document',NULL,{documentId},1,'2026-09-08','2026-09-08',1)");
        var invalid = new[]
        {
            "UPDATE analysis_nodes SET node_type='Other' WHERE id=80001",
            "UPDATE analysis_nodes SET title=NULL WHERE id=80001",
            "UPDATE analysis_nodes SET title=' ' WHERE id=80001",
            "UPDATE analysis_nodes SET title=' untrimmed ' WHERE id=80001",
            "UPDATE analysis_nodes SET title=replace(hex(zeroblob(101)),'0','a') WHERE id=80001",
            "UPDATE analysis_nodes SET title='override' WHERE id=80003",
            $"UPDATE analysis_nodes SET knowledge_document_id={documentId} WHERE id=80002",
            "UPDATE analysis_nodes SET knowledge_document_id=NULL WHERE id=80003",
            "UPDATE analysis_nodes SET id=0 WHERE id=80003",
            "UPDATE analysis_nodes SET id=9007199254740992 WHERE id=80003",
            "UPDATE analysis_nodes SET parent_id=id WHERE id=80002",
            "UPDATE analysis_nodes SET parent_id=999999 WHERE id=80002",
            "UPDATE analysis_nodes SET knowledge_document_id=999999 WHERE id=80003",
            "UPDATE analysis_nodes SET version=0 WHERE id=80002",
            "UPDATE analysis_nodes SET sort_order=-1 WHERE id=80002",
            "UPDATE analysis_nodes SET sort_order=0 WHERE id=80003",
            "UPDATE analysis_nodes SET parent_id=80001,sort_order=0 WHERE id=80003",
            "DELETE FROM analysis_nodes WHERE id=80001",
            $"DELETE FROM knowledge_documents WHERE id={documentId}",
            $"INSERT INTO analysis_nodes(id,node_type,knowledge_document_id,sort_order,created_at,updated_at,version) VALUES (80004,'Document',{documentId},2,'2026-09-08','2026-09-08',1)",
        };
        foreach (var sql in invalid) await Assert.ThrowsAsync<SqliteException>(() => db.Database.ExecuteSqlRawAsync(sql));
        Assert.Equal(3, await db.AnalysisNodes.CountAsync());
        Assert.Equal(0, await Scalar(db, "SELECT count(*) FROM pragma_foreign_key_check"));
    }

    [Fact]
    public async Task Removing_the_only_placement_and_empty_folder_preserves_populated_knowledge_and_portal_state()
    {
        using var factory = new BootstrapWebApplicationFactory();
        using var client = factory.CreateAuthenticatedClient();
        var id = await ProtectedDocument(factory, client);
        var folder = (await Folder(client, "Remove organization only")).Node!;
        Dictionary<string, string> before;
        using (var scope = factory.Services.CreateScope())
            before = await CanonicalRows(scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>());
        var placed = await Placement(client, id, folder.Id);
        using var removeRequest = Request(HttpMethod.Delete, $"/api/analysis/document-placements/{placed.Node!.Id}",
            new RemoveAnalysisNodeRequest(placed.Node.ConcurrencyToken, placed.TreeConcurrencyToken));
        using var remove = await client.SendAsync(removeRequest);
        var removed = await Mutation(remove);
        using var deleteRequest = Request(HttpMethod.Delete, $"/api/analysis/folders/{folder.Id}",
            new RemoveAnalysisNodeRequest(folder.ConcurrencyToken, removed.TreeConcurrencyToken));
        using var delete = await client.SendAsync(deleteRequest);
        Assert.Empty((await Mutation(delete)).Items);
        using var verification = factory.Services.CreateScope();
        var db = verification.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(before.OrderBy(pair => pair.Key), (await CanonicalRows(db)).OrderBy(pair => pair.Key));
        Assert.Equal(2, await db.KnowledgeDocumentRevisions.CountAsync(row => row.KnowledgeDocumentId == id));
        Assert.NotEmpty(await db.AttachmentReferences.ToArrayAsync());
        Assert.NotEmpty(await db.Evidence.ToArrayAsync());
        Assert.NotEmpty(await db.KnowledgeRelations.ToArrayAsync());
        Assert.NotEmpty(await db.PortalPageSections.ToArrayAsync());
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/knowledge-documents/{id}")).StatusCode);
        await Placement(client, id); // Removal releases only the organization uniqueness slot.
    }

    private static async Task<string> ExistingSchema(KnowledgeHubDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "SELECT type,name,sql FROM sqlite_master WHERE tbl_name <> 'analysis_nodes' ORDER BY type,name";
        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<string>();
        while (await reader.ReadAsync()) rows.Add($"{reader.GetString(0)}|{reader.GetString(1)}|{(reader.IsDBNull(2) ? "" : reader.GetString(2))}");
        return string.Join('\n', rows);
    }
}
