using System.Data.Common;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class RelationEvidenceRemovalApiTests
{
    private const string BlockMessage = "无法删除，仍存在依赖项：该关系已有知识依据或人工确认，不能直接移除。";

    [Theory]
    [InlineData(AccessLevel.Viewer, 0)]
    [InlineData(AccessLevel.Viewer, 1)]
    [InlineData(AccessLevel.Viewer, 2)]
    [InlineData(AccessLevel.Viewer, 3)]
    [InlineData(AccessLevel.Editor, 0)]
    [InlineData(AccessLevel.Editor, 1)]
    [InlineData(AccessLevel.Editor, 2)]
    [InlineData(AccessLevel.Editor, 3)]
    [InlineData(AccessLevel.Administrator, 0)]
    [InlineData(AccessLevel.Administrator, 1)]
    [InlineData(AccessLevel.Administrator, 2)]
    [InlineData(AccessLevel.Administrator, 3)]
    public async Task Removal_respects_roles_and_preserves_all_protected_history(AccessLevel role, int evidenceKinds)
    {
        using var factory = new RemovalFactory();
        using var admin = factory.CreateAuthenticatedClient();
        var id = await CreateRelation(admin);
        var evidenceIds = new List<long>();
        if ((evidenceKinds & 1) != 0) evidenceIds.Add(await AddAndReadId(admin, id, false));
        if ((evidenceKinds & 2) != 0) evidenceIds.Add(await AddAndReadId(admin, id, true));
        using var client = await ClientForRole(factory, role);
        var before = await Snapshot(factory);
        using var response = await client.DeleteAsync($"/api/relationships/{id}");
        var blocked = role != AccessLevel.Viewer && evidenceKinds != 0;
        Assert.Equal(role == AccessLevel.Viewer ? HttpStatusCode.Forbidden
            : blocked ? HttpStatusCode.UnprocessableEntity : HttpStatusCode.OK, response.StatusCode);
        if (blocked) await AssertBlock(response);
        if (role == AccessLevel.Viewer || blocked) Assert.Equal(before, await Snapshot(factory));
        else
        {
            Assert.Equal("{}", await response.Content.ReadAsStringAsync());
            using var missing = await admin.GetAsync($"/api/relationships/{id}");
            Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        }
        foreach (var evidenceId in evidenceIds)
        {
            using var detail = await client.GetAsync($"/api/evidence/{evidenceId}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
            var body = await detail.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal(id, body.GetProperty("subject").GetProperty("id").GetInt64());
            Assert.Equal("Unknown", body.GetProperty("subjectContext").GetProperty("knowledgeStatus").GetString());
        }
        if (blocked)
        {
            using var detail = await client.GetAsync($"/api/relationships/{id}");
            Assert.Equal(HttpStatusCode.OK, detail.StatusCode);
        }
        await AssertNoOrphans(factory);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task Concurrent_delete_and_append_serialize_in_both_commit_orders(bool human, bool deleteFirst)
    {
        using var factory = new RemovalFactory();
        using var first = factory.CreateAuthenticatedClient();
        using var second = factory.CreateAuthenticatedClient();
        var id = await CreateRelation(first);
        factory.Gate.Armed = true;
        Task<HttpResponseMessage> Delete(HttpClient client) => client.DeleteAsync($"/api/relationships/{id}");
        Task<HttpResponseMessage> Append(HttpClient client) => Add(client, id, human);
        var winner = Task.Run(() => deleteFirst ? Delete(first) : Append(first));
        Task<HttpResponseMessage>? contender = null;
        try
        {
            await factory.Gate.Held.Task.WaitAsync(TimeSpan.FromSeconds(15));
            Assert.True(factory.Gate.HasWriteTransaction);
            contender = Task.Run(() => deleteFirst ? Append(second) : Delete(second));
            // A second physical connection has entered the competing HTTP request while
            // the first request still owns SQLite's write reservation before SaveChanges.
            await factory.Connections.ContenderOpened.Task.WaitAsync(TimeSpan.FromSeconds(15));
            await Task.Delay(200);
            Assert.False(contender.IsCompleted);
        }
        finally
        {
            factory.Gate.Release.TrySetResult();
            // Drain both requests before disposing the test host, including assertion failures.
            await winner.WaitAsync(TimeSpan.FromSeconds(20));
            if (contender is not null) await contender.WaitAsync(TimeSpan.FromSeconds(20));
        }
        using var winningResponse = await winner;
        using var competingResponse = await contender!;
        Assert.Equal(deleteFirst ? HttpStatusCode.OK : HttpStatusCode.Created, winningResponse.StatusCode);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, competingResponse.StatusCode);
        if (deleteFirst)
            Assert.Equal("reference_invalid", (await competingResponse.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        else await AssertBlock(competingResponse);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(!deleteFirst, await db.KnowledgeRelations.AnyAsync(r => r.Id == id));
        Assert.Equal(deleteFirst ? 0 : 1, await db.Evidence.CountAsync(e => e.SubjectType == EvidenceSubjectType.KnowledgeRelation && e.SubjectId == id));
        if (!deleteFirst)
        {
            var evidence = await db.Evidence.SingleAsync(e => e.SubjectType == EvidenceSubjectType.KnowledgeRelation && e.SubjectId == id);
            Assert.Equal(human ? EvidenceType.HumanConfirmation : EvidenceType.Sql, evidence.EvidenceType);
            Assert.Equal(SystemKnowledgeHub.Api.Shared.Domain.KnowledgeStatus.Unknown,
                (await db.KnowledgeRelations.SingleAsync(r => r.Id == id)).KnowledgeStatus);
            using var historical = await first.GetAsync($"/api/evidence/{evidence.Id}");
            Assert.Equal(HttpStatusCode.OK, historical.StatusCode);
        }
        await AssertNoOrphans(factory);
    }

    [Fact]
    public async Task Canonical_predicate_ignores_other_subject_types_but_includes_legacy_confirmation()
    {
        using var factory = new RemovalFactory();
        using var client = factory.CreateAuthenticatedClient();
        var id = await CreateRelation(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            // A different canonical SubjectType with the same numeric ID is not this relation.
            if (!await db.Systems.AnyAsync(s => s.Id == id))
            {
                db.Systems.Add(new()
                {
                    Id = id, Name = "Other subject", DisplayName = "Other subject", SystemType = "Internal",
                    CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
                    KnowledgeStatusChangedAt = DateTimeOffset.UtcNow,
                });
            }
            db.Evidence.Add(new()
            {
                SubjectType = EvidenceSubjectType.System, SubjectId = id, EvidenceType = EvidenceType.Sql,
                SourceTitle = "other subject", SourceReference = "other.sql", SupportReason = "other",
                ProviderName = "test", ProviderRole = "test", ProvidedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        using var removed = await client.DeleteAsync($"/api/relationships/{id}");
        Assert.Equal(HttpStatusCode.OK, removed.StatusCode);
        id = await CreateRelation(client);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            db.Evidence.Add(new()
            {
                SubjectType = EvidenceSubjectType.KnowledgeRelation, SubjectId = id,
                EvidenceType = EvidenceType.HumanConfirmation, SubjectDetailKey = "legacy-region",
                SourceTitle = "legacy", SourceReference = "legacy meeting", SupportReason = "legacy", Confidence = EvidenceConfidence.Low,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            await db.SaveChangesAsync();
        }
        var before = await Snapshot(factory);
        using var blocked = await client.DeleteAsync($"/api/relationships/{id}");
        await AssertBlock(blocked);
        Assert.Equal(before, await Snapshot(factory));
        await AssertNoOrphans(factory);
    }

    [Fact]
    public async Task Missing_invalid_and_cancelled_removal_do_not_write()
    {
        using var factory = new RemovalFactory();
        using var client = factory.CreateAuthenticatedClient();
        using var missing = await client.DeleteAsync("/api/relationships/9999999");
        Assert.Equal(HttpStatusCode.NotFound, missing.StatusCode);
        using var invalid = await client.DeleteAsync("/api/relationships/0");
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        var id = await CreateRelation(client);
        var before = await Snapshot(factory);
        await using var scope = factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<SystemKnowledgeHub.Api.Features.Relationships.Application.RelationshipService>();
        factory.Gate.CancelSave = true;
        await Assert.ThrowsAsync<OperationCanceledException>(() => service.Delete(id, CancellationToken.None));
        Assert.Equal(before, await Snapshot(factory));
    }

    private static async Task AssertBlock(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal("business_rule_violation", body.GetProperty("code").GetString());
        Assert.Equal(BlockMessage, body.GetProperty("message").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("fieldErrors").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("details").ValueKind);
        Assert.Equal(4, body.EnumerateObject().Count());
    }

    private static async Task<long> CreateRelation(HttpClient client)
    {
        using var response = await client.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "BusinessFunction", id = 77 }, relationType = "Reads",
            target = new { type = "DatabaseObject", id = 45 }, description = "protected relation",
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    private static async Task<long> AddAndReadId(HttpClient client, long id, bool human)
    {
        using var response = await Add(client, id, human);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("id").GetInt64();
    }

    private static Task<HttpResponseMessage> Add(HttpClient client, long id, bool human) => human
        ? client.PostAsJsonAsync("/api/evidence/human-confirmations", new
        {
            subject = new { type = "KnowledgeRelation", id }, knowledgeRoleId = (long?)null,
            confirmationMethod = "Meeting", confirmedAt = "2026-08-22T02:30:00Z",
            confirmationStatement = "确认读取关系", supportReason = "业务确认", sourceNote = "test",
        })
        : client.PostAsJsonAsync("/api/evidence", new
        {
            evidenceType = "Sql", subject = new { type = "KnowledgeRelation", id },
            sourceTitle = "private evidence title", sourceReference = "private.sql", supportReason = "private body",
            confidence = "Low", provider = new
            {
                displayName = "private provider", roleOrIdentity = "expert", occurredAt = "2026-08-22T02:30:00Z",
            },
        });

    private static async Task<HttpClient> ClientForRole(RemovalFactory factory, AccessLevel role)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var user = new User
        {
            DisplayName = "Removal test", AccessLevel = role, IsActive = true,
            CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow, Version = 1,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return await factory.CreateAuthenticatedClientAsync(user.Id);
    }

    private static async Task<string> Snapshot(RemovalFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        return JsonSerializer.Serialize(new
        {
            Relations = await db.KnowledgeRelations.AsNoTracking().OrderBy(r => r.Id).ToArrayAsync(),
            Evidence = await db.Evidence.AsNoTracking().OrderBy(e => e.Id).ToArrayAsync(),
        });
    }

    private static async Task AssertNoOrphans(RemovalFactory factory)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.False(await db.Evidence.AnyAsync(e => e.SubjectType == EvidenceSubjectType.KnowledgeRelation
            && !db.KnowledgeRelations.Any(r => r.Id == e.SubjectId)));
    }

    private sealed class RemovalFactory : BootstrapWebApplicationFactory
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"stability-r01-r01-{Guid.NewGuid():N}");
        public WriteGate Gate { get; } = new();
        public ConnectionObserver Connections { get; }
        public RemovalFactory() => Connections = new(Gate);
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);
            Directory.CreateDirectory(directory);
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<DbContextOptions<KnowledgeHubDbContext>>();
                services.AddDbContext<KnowledgeHubDbContext>(options => options
                    .UseSqlite($"Data Source={Path.Combine(directory, "relations.db")};Pooling=False;Default Timeout=10")
                    .AddInterceptors(Gate, Connections));
            });
        }
        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing && Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class WriteGate : SaveChangesInterceptor
    {
        public bool Armed { get; set; }
        public bool CancelSave { get; set; }
        public bool HasWriteTransaction { get; private set; }
        public DbConnection? OwnerConnection { get; private set; }
        public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            if (CancelSave) throw new OperationCanceledException("Injected cancellation before removal save.");
            if (Armed && Interlocked.Increment(ref arrivals) == 1)
            {
                HasWriteTransaction = eventData.Context!.Database.CurrentTransaction is not null;
                OwnerConnection = eventData.Context.Database.GetDbConnection();
                Held.TrySetResult();
                await Release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }
            return result;
        }
    }

    private sealed class ConnectionObserver(WriteGate gate) : DbConnectionInterceptor
    {
        public TaskCompletionSource ContenderOpened { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public override Task ConnectionOpenedAsync(DbConnection connection, ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            if (gate.Held.Task.IsCompleted && !gate.Release.Task.IsCompleted && !ReferenceEquals(connection, gate.OwnerConnection))
                ContenderOpened.TrySetResult();
            return Task.CompletedTask;
        }
    }
}
