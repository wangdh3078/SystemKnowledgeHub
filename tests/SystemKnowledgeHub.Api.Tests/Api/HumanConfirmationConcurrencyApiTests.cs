using System.Net.Http.Json;
using System.Data.Common;
using System.Net;
using Microsoft.AspNetCore.Hosting;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Tests.TestSupport;
using static SystemKnowledgeHub.Api.Tests.Api.HumanConfirmationLifecycleApiTests;

namespace SystemKnowledgeHub.Api.Tests.Api;
public sealed class HumanConfirmationConcurrencyApiTests
{
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Independent_requests_have_one_withdrawal_or_replacement_winner(bool replacement)
    {
        using var f = new LifecycleFactory(); using var a = f.CreateAuthenticatedClient(); using var b = f.CreateAuthenticatedClient();
        var old = await Add(a); var id = old.GetProperty("id").GetInt64();
        if (replacement) { using var w = await Withdraw(a, old, "withdraw before replacement"); Assert.Equal(HttpStatusCode.OK,w.StatusCode); }
        f.Gate.Armed = true;
        Task<HttpResponseMessage> Start(HttpClient c) => Task.Run(() => replacement
            ? c.PostAsJsonAsync("/api/evidence/human-confirmations", Request("BusinessFunction",77,id))
            : Withdraw(c,old,"concurrent withdrawal"));
        var first = Start(a);
        await f.Gate.Held.Task.WaitAsync(TimeSpan.FromSeconds(15));
        var second = Start(b);
        try { await f.Connections.ContenderOpened.Task.WaitAsync(TimeSpan.FromSeconds(15)); Assert.True(f.Gate.HasWriteTransaction); }
        finally { f.Gate.Release.TrySetResult(); }
        using var winner=await first;using var loser=await second;
        Assert.Equal(replacement?HttpStatusCode.Created:HttpStatusCode.OK,winner.StatusCode);
        await Code(loser,409,"conflict");
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(replacement?1:0,await db.Evidence.CountAsync(e=>e.ReplacesHumanConfirmationId==id));
        Assert.Equal(2,(await db.Evidence.FindAsync(id))!.Version);
    }

    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Withdraw_and_replacement_observe_authoritative_commit_order(bool withdrawFirst)
    {
        using var f=new LifecycleFactory();using var a=f.CreateAuthenticatedClient();using var b=f.CreateAuthenticatedClient();
        var old=await Add(a);var id=old.GetProperty("id").GetInt64();
        if (!withdrawFirst)
        {
            // Hold an independent immediate transaction before either operation; replacement sees Active first.
            using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await using var tx=await SqliteImmediateTransaction.BeginAsync(db,CancellationToken.None);
            var attempt=Task.Run(()=>a.PostAsJsonAsync("/api/evidence/human-confirmations",Request("BusinessFunction",77,id)));
            await tx.CommitAsync(CancellationToken.None);using var rejected=await attempt;await Code(rejected,422,"invalid_state");
            using var withdrawn=await Withdraw(b,old,"later withdrawal");Assert.Equal(HttpStatusCode.OK,withdrawn.StatusCode);
        }
        else
        {
            f.Gate.Armed=true;var withdraw=Task.Run(()=>Withdraw(a,old,"first withdrawal"));
            await f.Gate.Held.Task.WaitAsync(TimeSpan.FromSeconds(15));
            var replace=Task.Run(()=>b.PostAsJsonAsync("/api/evidence/human-confirmations",Request("BusinessFunction",77,id)));
            try {await f.Connections.ContenderOpened.Task.WaitAsync(TimeSpan.FromSeconds(15));} finally {f.Gate.Release.TrySetResult();}
            using var withdrawn=await withdraw;using var replacement=await replace;
            Assert.Equal(HttpStatusCode.OK,withdrawn.StatusCode);Assert.Equal(HttpStatusCode.Created,replacement.StatusCode);
        }
    }
    [Theory]
    [InlineData(false)] [InlineData(true)]
    public async Task Document_save_and_replacement_preserve_revision_context(bool saveFirst)
    {
        using var f=new LifecycleFactory();using var a=f.CreateAuthenticatedClient();using var b=f.CreateAuthenticatedClient();
        using var create=await a.PostAsJsonAsync("/api/knowledge-documents",new {documentType="Requirement",title="race",bodyMarkdown="one"});
        Assert.Equal(HttpStatusCode.Created,create.StatusCode);var doc=await create.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();var id=doc.GetProperty("id").GetInt64();
        var old=await Add(a,"KnowledgeDocument",id,revision:1);using var withdrawn=await Withdraw(a,old,"replace");Assert.Equal(HttpStatusCode.OK,withdrawn.StatusCode);
        Task<HttpResponseMessage> Save()=>Task.Run(()=>a.PutAsJsonAsync($"/api/knowledge-documents/{id}/content",new {title="race",bodyMarkdown="two",concurrencyToken=doc.GetProperty("concurrencyToken").GetString()}));
        Task<HttpResponseMessage> Replace()=>Task.Run(()=>b.PostAsJsonAsync("/api/evidence/human-confirmations",Request("KnowledgeDocument",id,old.GetProperty("id").GetInt64(),revision:1)));
        f.Gate.Armed=true;var first=saveFirst?Save():Replace();await f.Gate.Held.Task.WaitAsync(TimeSpan.FromSeconds(15));var second=saveFirst?Replace():Save();
        try{await f.Connections.ContenderOpened.Task.WaitAsync(TimeSpan.FromSeconds(15));}finally{f.Gate.Release.TrySetResult();}
        using var firstResult=await first;using var secondResult=await second;
        Assert.Equal(saveFirst?HttpStatusCode.OK:HttpStatusCode.Created,firstResult.StatusCode);
        Assert.Equal(saveFirst?HttpStatusCode.Conflict:HttpStatusCode.OK,secondResult.StatusCode);
        using var scope=f.Services.CreateScope();var db=scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();var oldId=old.GetProperty("id").GetInt64();
        var replacements=await db.Evidence.Where(e=>e.ReplacesHumanConfirmationId==oldId).ToArrayAsync();Assert.Equal(saveFirst?0:1,replacements.Length);Assert.All(replacements,e=>Assert.Equal(1,e.KnowledgeDocumentRevisionNumberSnapshot));
        Assert.Equal(2,(await db.KnowledgeDocuments.FindAsync(id))!.CurrentRevisionNumber);
    }

    private sealed class LifecycleFactory : BootstrapWebApplicationFactory
    {
        private readonly string directory = Path.Combine(Path.GetTempPath(), $"hc-b01-concurrency-{Guid.NewGuid():N}");
        public WriteGate Gate { get; } = new();
        public ConnectionObserver Connections { get; }
        public LifecycleFactory() => Connections = new(Gate);
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
        public bool HasWriteTransaction { get; private set; }
        public DbConnection? OwnerConnection { get; private set; }
        public TaskCompletionSource Held { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int arrivals;
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
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

