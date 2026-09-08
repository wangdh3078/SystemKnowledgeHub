using System.Data.Common;
using System.Globalization;
using System.Net;
using System.Net.Http.Json;
using System.Net.Http.Headers;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;

namespace SystemKnowledgeHub.Api.Tests.TestSupport;

internal static class AnalysisWorkspaceTestSupport
{
    public static async Task<long> ProtectedDocument(BootstrapWebApplicationFactory factory, HttpClient client)
    {
        var document = await Document(client, "Protected analysis knowledge");
        var id = document.GetProperty("id").GetInt64();
        using var multipart = new MultipartFormDataContent();
        using var bytes = new ByteArrayContent("analysis attachment canary"u8.ToArray());
        bytes.Headers.ContentType = new MediaTypeHeaderValue("text/plain");
        multipart.Add(bytes, "file", "analysis.txt");
        using var upload = await client.PostAsync($"/api/knowledge-documents/{id}/attachments", multipart);
        Assert.Equal(HttpStatusCode.Created, upload.StatusCode);
        var attachment = await upload.Content.ReadFromJsonAsync<JsonElement>();
        using var save = await client.PutAsJsonAsync($"/api/knowledge-documents/{id}/content", new
        {
            title = document.GetProperty("title").GetString(), bodyMarkdown = "protected body",
            fileAttachmentIds = new[] { attachment.GetProperty("attachmentId").GetInt64() },
            concurrencyToken = document.GetProperty("concurrencyToken").GetString(),
        });
        Assert.Equal(HttpStatusCode.OK, save.StatusCode);
        using var confirmation = await client.PostAsJsonAsync("/api/evidence/human-confirmations", new
        {
            subject = new { type = "KnowledgeDocument", id }, subjectRevisionNumber = 2,
            confirmationMethod = "InSystem", confirmedAt = "2026-09-08T01:00:00Z",
            confirmationStatement = "确认分析结论", supportReason = "验证证据", sourceNote = "test fixture",
        });
        Assert.Equal(HttpStatusCode.Created, confirmation.StatusCode);
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var systemId = await db.Systems.Select(system => system.Id).FirstAsync();
        using var relation = await client.PostAsJsonAsync("/api/relationships", new
        { source = new { type = "KnowledgeDocument", id }, relationType = "Documents", target = new { type = "System", id = systemId } });
        Assert.Equal(HttpStatusCode.Created, relation.StatusCode);
        var actor = await db.Users.FirstAsync();
        var now = DateTimeOffset.UtcNow;
        var page = new PortalPage
        {
            Title = "Protected portal composition", PrimaryTargetType = PortalTargetType.KnowledgeDocument, PrimaryTargetId = id,
            CreatedAt = now, UpdatedAt = now, CreatedByUserId = actor.Id, UpdatedByUserId = actor.Id,
            CreatedByDisplayName = actor.DisplayName, UpdatedByDisplayName = actor.DisplayName,
        };
        db.PortalPages.Add(page);
        await db.SaveChangesAsync();
        db.PortalPageNodes.Add(new PortalPageNode
        {
            Title = "Protected portal placement", NodeKind = PortalPageNodeKind.Page, PortalPageId = page.Id,
            CreatedAt = now, UpdatedAt = now, CreatedByUserId = actor.Id, UpdatedByUserId = actor.Id,
            CreatedByDisplayName = actor.DisplayName, UpdatedByDisplayName = actor.DisplayName,
        });
        db.PortalPageSections.Add(new PortalPageSection
        {
            PortalPageId = page.Id, Heading = "正文", SourceKind = PortalPageSectionSourceKind.PrimaryTarget,
            ProjectionKind = PortalPageProjectionKind.KnowledgeDocumentBody,
        });
        await db.SaveChangesAsync();
        return id;
    }

    public static async Task<AnalysisTreeResponse> Tree(HttpClient client) =>
        (await client.GetFromJsonAsync<AnalysisTreeResponse>("/api/analysis/tree"))!;

    public static async Task<AnalysisMutationResponse> Folder(HttpClient client, string title, long? parentId = null)
    {
        using var response = await client.PostAsJsonAsync("/api/analysis/folders", new
        { parentId, title, treeConcurrencyToken = (await Tree(client)).TreeConcurrencyToken });
        return await Mutation(response, HttpStatusCode.Created);
    }

    public static async Task<AnalysisMutationResponse> Placement(HttpClient client, long documentId, long? parentId = null)
    {
        using var response = await client.PostAsJsonAsync("/api/analysis/document-placements", new
        { parentId, knowledgeDocumentId = documentId, treeConcurrencyToken = (await Tree(client)).TreeConcurrencyToken });
        return await Mutation(response, HttpStatusCode.Created);
    }

    public static async Task<JsonElement> Document(HttpClient client, string title = "Analysis fixture", string documentType = "DesignNote")
    {
        using var response = await client.PostAsJsonAsync("/api/knowledge-documents", new
        { documentType, title, summary = "summary canary", bodyMarkdown = "# Body canary\nmermaid source" });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<JsonElement>();
    }

    public static HttpRequestMessage Request(HttpMethod method, string path, object body) =>
        new(method, path) { Content = JsonContent.Create(body) };

    public static async Task<AnalysisMutationResponse> Mutation(HttpResponseMessage response, HttpStatusCode expected = HttpStatusCode.OK)
    {
        Assert.True(response.StatusCode == expected, $"{response.StatusCode}: {await response.Content.ReadAsStringAsync()}");
        return (await response.Content.ReadFromJsonAsync<AnalysisMutationResponse>())!;
    }

    public static async Task Code(HttpResponseMessage response, HttpStatusCode status, string code)
    {
        Assert.Equal(status, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    public static async Task<long> User(BootstrapWebApplicationFactory factory, AccessLevel access)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = new User { DisplayName = "Analysis actor", AccessLevel = access, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Version = 1 };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    // All data is from a disposable test database. Compare every existing table, including FTS
    // shadows and immutable history, without assuming that an empty table proves preservation.
    public static async Task<Dictionary<string, string>> CanonicalRows(KnowledgeHubDbContext db)
    {
        await db.Database.OpenConnectionAsync();
        var connection = db.Database.GetDbConnection();
        var tables = new List<string>();
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name NOT IN ('analysis_nodes','__EFMigrationsHistory','sqlite_sequence') ORDER BY name";
            await using var reader = await command.ExecuteReaderAsync();
            while (await reader.ReadAsync()) tables.Add(reader.GetString(0));
        }
        var result = new Dictionary<string, string>();
        foreach (var table in tables)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM \"{table.Replace("\"", "\"\"")}\"";
            await using var reader = await command.ExecuteReaderAsync();
            var rows = new List<string>();
            while (await reader.ReadAsync())
                rows.Add(JsonSerializer.Serialize(Enumerable.Range(0, reader.FieldCount).Select(index =>
                    reader.IsDBNull(index) ? null : reader.GetValue(index) is byte[] bytes
                        ? Convert.ToHexString(bytes) : Convert.ToString(reader.GetValue(index), CultureInfo.InvariantCulture)).ToArray()));
            result.Add(table, string.Join('\n', rows.Order(StringComparer.Ordinal)));
        }
        return result;
    }

    public static async Task<long> Scalar(KnowledgeHubDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = sql;
        return Convert.ToInt64(await command.ExecuteScalarAsync(), CultureInfo.InvariantCulture);
    }
}

internal sealed class AnalysisRaceFactory : BootstrapWebApplicationFactory
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), $"analysis-b01-race-{Guid.NewGuid():N}");
    public AnalysisWriteGate Gate { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        base.ConfigureWebHost(builder);
        Directory.CreateDirectory(directory);
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<DbContextOptions<KnowledgeHubDbContext>>();
            services.AddDbContext<KnowledgeHubDbContext>(options => options
                .UseSqlite($"Data Source={Path.Combine(directory, "analysis.db")};Pooling=False;Foreign Keys=True;Default Timeout=15")
                .AddInterceptors(Gate, new AnalysisConnectionObserver(Gate)));
        });
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        if (disposing && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
    }
}

internal sealed class AnalysisWriteGate : SaveChangesInterceptor
{
    public bool Armed { get; set; }
    public bool HasWriteTransaction { get; private set; }
    public DbConnection? Owner { get; private set; }
    public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    public TaskCompletionSource ContenderOpened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private int arrivals;

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (Armed && Interlocked.Increment(ref arrivals) == 1)
        {
            HasWriteTransaction = eventData.Context!.Database.CurrentTransaction is not null;
            Owner = eventData.Context.Database.GetDbConnection();
            Held.TrySetResult();
            await Release.Task.WaitAsync(TimeSpan.FromSeconds(20), cancellationToken);
        }
        return result;
    }
}

internal sealed class AnalysisConnectionObserver(AnalysisWriteGate gate) : DbConnectionInterceptor
{
    public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (gate.Held.Task.IsCompleted && !gate.Release.Task.IsCompleted && !ReferenceEquals(connection, gate.Owner))
            gate.ContenderOpened.TrySetResult();
        return Task.CompletedTask;
    }
}
