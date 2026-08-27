using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application.Models;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.SoftDelete.Application;
using SystemKnowledgeHub.Api.Features.Search.Application;
using SystemKnowledgeHub.Api.Features.StatusProgression.Application;
using SystemKnowledgeHub.Api.Features.Systems.Application;
using SystemKnowledgeHub.Api.Features.Systems.Application.Models;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Persistence.Concurrency;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Tests.Persistence;

public sealed class CoreSoftDeleteRaceTests
{
    private static readonly ConcurrencyTokenCodec TokenCodec = new();
    private static readonly SoftDeleteActor AdminActor = new(1, "Race Administrator", AccessLevel.Administrator);
    private static readonly CanonicalCreator Creator = new(1, "Race Administrator");

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_and_relationship_add_serialize_both_interleavings(bool deleteFirst)
    {
        await InDatabase(async databasePath =>
        {
            var seed = await SeedSystems(databasePath, includeSecondSystem: true);
            var firstSql = deleteFirst ? "UPDATE \"systems\"" : "INSERT INTO \"knowledge_relations\"";
            var gate = new CommandGate(firstSql);
            await using var firstContext = Context(databasePath, gate);
            await using var secondContext = Context(databasePath);
            var deleteService = new SystemDeleteService(deleteFirst ? firstContext : secondContext, TokenCodec);
            var relationshipContext = deleteFirst ? secondContext : firstContext;
            var relationshipService = RelationshipService(relationshipContext);
            var relationshipCommand = new AddRelationshipCommand(
                new("System", seed.SystemId),
                "DependsOn",
                new("System", seed.OtherSystemId),
                null,
                new("Race", "Editor"));

            if (deleteFirst)
            {
                var deleteTask = deleteService.DeleteSystem(seed.SystemId, TokenCodec.Encode(1), AdminActor, CancellationToken.None);
                await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var relationTask = Task.Run(async () => { secondStarted.SetResult(); return await relationshipService.Add(relationshipCommand, CancellationToken.None); });
                await secondStarted.Task;
                gate.Release.SetResult();
                Assert.Equal(SoftDeleteFailure.None, (await deleteTask).Failure);
                Assert.Equal(RelationshipFailure.ReferenceInvalid, (await relationTask).Failure);
            }
            else
            {
                var relationTask = relationshipService.Add(relationshipCommand, CancellationToken.None);
                await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var deleteTask = Task.Run(async () => { secondStarted.SetResult(); return await deleteService.DeleteSystem(seed.SystemId, TokenCodec.Encode(1), AdminActor, CancellationToken.None); });
                await secondStarted.Task;
                gate.Release.SetResult();
                Assert.Equal(RelationshipFailure.None, (await relationTask).Failure);
                var delete = await deleteTask;
                Assert.Equal(SoftDeleteFailure.Dependencies, delete.Failure);
                Assert.Contains(delete.Blockers!, blocker => blocker.DependencyType == "knowledgeRelations");
            }

            await using var verify = Context(databasePath);
            var deleted = await verify.Systems.IgnoreQueryFilters().Where(item => item.Id == seed.SystemId).Select(item => item.IsDeleted).SingleAsync();
            var relationExists = await verify.KnowledgeRelations.AnyAsync(item => item.SourceId == seed.SystemId);
            Assert.NotEqual(deleted, relationExists);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_and_child_create_serialize_both_interleavings(bool deleteFirst)
    {
        await InDatabase(async databasePath =>
        {
            var seed = await SeedSystems(databasePath, includeSecondSystem: false);
            var firstSql = deleteFirst ? "UPDATE \"systems\"" : "INSERT INTO \"database_sources\"";
            var gate = new CommandGate(firstSql);
            await using var firstContext = Context(databasePath, gate);
            await using var secondContext = Context(databasePath);
            var deleteService = new SystemDeleteService(deleteFirst ? firstContext : secondContext, TokenCodec);
            var childContext = deleteFirst ? secondContext : firstContext;
            var childService = new DatabaseKnowledgeService(childContext, TokenCodec);
            var childCommand = new CreateDatabaseSourceCommand(
                seed.SystemId, "race_source", "SQLite", null, null, null, null, null, false,
                new("Race", "Editor"), Creator);

            if (deleteFirst)
            {
                var deleteTask = deleteService.DeleteSystem(seed.SystemId, TokenCodec.Encode(1), AdminActor, CancellationToken.None);
                await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var childTask = Task.Run(async () => { secondStarted.SetResult(); return await childService.CreateDatabaseSource(childCommand, CancellationToken.None); });
                await secondStarted.Task;
                gate.Release.SetResult();
                Assert.Equal(SoftDeleteFailure.None, (await deleteTask).Failure);
                Assert.Equal(CreateDatabaseSourceFailure.SystemNotFound, (await childTask).Failure);
            }
            else
            {
                var childTask = childService.CreateDatabaseSource(childCommand, CancellationToken.None);
                await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var deleteTask = Task.Run(async () => { secondStarted.SetResult(); return await deleteService.DeleteSystem(seed.SystemId, TokenCodec.Encode(1), AdminActor, CancellationToken.None); });
                await secondStarted.Task;
                gate.Release.SetResult();
                Assert.Equal(CreateDatabaseSourceFailure.None, (await childTask).Failure);
                var delete = await deleteTask;
                Assert.Equal(SoftDeleteFailure.Dependencies, delete.Failure);
                Assert.Contains(delete.Blockers!, blocker => blocker.DependencyType == "databaseSources");
            }

            await using var verify = Context(databasePath);
            var deleted = await verify.Systems.IgnoreQueryFilters().Where(item => item.Id == seed.SystemId).Select(item => item.IsDeleted).SingleAsync();
            var childExists = await verify.DatabaseSources.AnyAsync(item => item.SystemId == seed.SystemId);
            Assert.NotEqual(deleted, childExists);
        });
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Delete_and_edit_serialize_without_stale_resurrection(bool deleteFirst)
    {
        await InDatabase(async databasePath =>
        {
            var seed = await SeedSystems(databasePath, includeSecondSystem: false);
            var gate = new CommandGate("UPDATE \"systems\"");
            await using var firstContext = Context(databasePath, gate);
            await using var secondContext = Context(databasePath);
            var deleteService = new SystemDeleteService(deleteFirst ? firstContext : secondContext, TokenCodec);
            var editService = new SystemService(deleteFirst ? secondContext : firstContext, TokenCodec);
            var editCommand = new UpdateSystemOverviewCommand(
                seed.SystemId, "Edited", "Service", "edited", [], new(null, null), [], [], [], null,
                new ActorContext("Race", "Editor"), TokenCodec.Encode(1));

            if (deleteFirst)
            {
                var deleteTask = deleteService.DeleteSystem(seed.SystemId, TokenCodec.Encode(1), AdminActor, CancellationToken.None);
                await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var editTask = Task.Run(async () => { secondStarted.SetResult(); return await editService.UpdateSystemOverview(editCommand, CancellationToken.None); });
                await secondStarted.Task;
                gate.Release.SetResult();
                Assert.Equal(SoftDeleteFailure.None, (await deleteTask).Failure);
                Assert.Equal(UpdateSystemOverviewFailure.NotFound, (await editTask).Failure);
            }
            else
            {
                var editTask = editService.UpdateSystemOverview(editCommand, CancellationToken.None);
                await gate.Reached.Task.WaitAsync(TimeSpan.FromSeconds(5));
                var secondStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var deleteTask = Task.Run(async () => { secondStarted.SetResult(); return await deleteService.DeleteSystem(seed.SystemId, TokenCodec.Encode(1), AdminActor, CancellationToken.None); });
                await secondStarted.Task;
                gate.Release.SetResult();
                Assert.Equal(UpdateSystemOverviewFailure.None, (await editTask).Failure);
                Assert.Equal(SoftDeleteFailure.Conflict, (await deleteTask).Failure);
            }

            await using var verify = Context(databasePath);
            var state = await verify.Systems.IgnoreQueryFilters().Where(item => item.Id == seed.SystemId)
                .Select(item => new { item.IsDeleted, item.DisplayName, item.Version }).SingleAsync();
            if (deleteFirst)
            {
                Assert.True(state.IsDeleted);
                Assert.NotEqual("Edited", state.DisplayName);
                Assert.Equal(2, state.Version);
            }
            else
            {
                Assert.False(state.IsDeleted);
                Assert.Equal("Edited", state.DisplayName);
                Assert.Equal(2, state.Version);
            }
        });
    }

    [Fact]
    public async Task Knowledge_document_delete_rolls_back_canonical_state_when_FTS_removal_fails()
    {
        await InDatabase(async databasePath =>
        {
            await using (var seed = Context(databasePath))
            {
                await seed.Database.MigrateAsync();
                var now = DateTimeOffset.UtcNow;
                seed.Users.Add(new User
                {
                    Id = 1, DisplayName = AdminActor.DisplayName, IsActive = true, AccessLevel = AccessLevel.Administrator,
                    CreatedAt = now, UpdatedAt = now, Version = 1,
                });
                seed.KnowledgeDocuments.Add(new KnowledgeDocument
                {
                    Id = 1, DocumentType = DocumentType.KnowledgeArticle, Title = "Atomic FTS", BodyMarkdown = "atomic",
                    LifecycleStatus = DocumentLifecycleStatus.Draft, KnowledgeStatus = KnowledgeStatus.Unknown,
                    KnowledgeStatusChangedAt = now, KnowledgeStatusChangedByName = AdminActor.DisplayName,
                    KnowledgeStatusChangedByRole = "Administrator", CreatedByUserId = 1,
                    CreatedByDisplayName = AdminActor.DisplayName, UpdatedByUserId = 1,
                    UpdatedByDisplayName = AdminActor.DisplayName, CreatedAt = now, UpdatedAt = now,
                    CurrentRevisionNumber = 1, Version = 1,
                });
                await seed.SaveChangesAsync();
                await seed.Database.ExecuteSqlRawAsync("DROP TABLE knowledge_documents_fts;");
            }

            await using (var deleting = Context(databasePath))
            {
                var service = new KnowledgeDocumentDeleteService(
                    deleting,
                    new KnowledgeDocumentSearchIndex(deleting),
                    TokenCodec);
                await Assert.ThrowsAsync<Microsoft.Data.Sqlite.SqliteException>(() =>
                    service.DeleteKnowledgeDocument(1, TokenCodec.Encode(1), AdminActor, CancellationToken.None));
            }

            await using var verify = Context(databasePath);
            var state = await verify.KnowledgeDocuments.IgnoreQueryFilters().Where(item => item.Id == 1)
                .Select(item => new { item.IsDeleted, item.DeletedAt, item.DeletedByUserId, item.Version }).SingleAsync();
            Assert.False(state.IsDeleted);
            Assert.Null(state.DeletedAt);
            Assert.Null(state.DeletedByUserId);
            Assert.Equal(1, state.Version);
        });
    }

    [Fact]
    public async Task Dependency_queries_use_existing_endpoint_target_and_parent_indexes()
    {
        await InDatabase(async databasePath =>
        {
            await using var db = Context(databasePath);
            await db.Database.MigrateAsync();
            var relationPlan = await Explain(db,
                "SELECT count(*) FROM knowledge_relations WHERE (source_type='System' AND source_id=1) OR (target_type='System' AND target_id=1)");
            Assert.Contains("IX_knowledge_relations_source_type_source_id", relationPlan);
            Assert.Contains("IX_knowledge_relations_target_type_target_id", relationPlan);

            var unknownPlan = await Explain(db,
                "SELECT count(*) FROM unknown_item_targets t JOIN unknown_items u ON u.id=t.unknown_item_id WHERE t.target_type='BusinessRule' AND t.target_id=1 AND u.status<>'Closed'");
            Assert.Contains("IX_unknown_item_targets_target_type_target_id_unknown_item_id", unknownPlan);

            var updatePlan = await Explain(db,
                "SELECT count(*) FROM knowledge_updates WHERE target_type='BusinessRule' AND target_id=1 AND status='Proposed'");
            Assert.Contains("IX_knowledge_updates_target_type_target_id", updatePlan);

            var integrationPlan = await Explain(db,
                "SELECT count(*) FROM integrations WHERE is_deleted=0 AND database_source_id=1");
            Assert.Contains("IX_integrations_database_source_id", integrationPlan);

            var childPlan = await Explain(db,
                "SELECT count(*) FROM database_objects WHERE is_deleted=0 AND database_source_id=1");
            Assert.Contains("IX_database_objects_database_source_id", childPlan);
        });
    }

    private static RelationshipService RelationshipService(KnowledgeHubDbContext context) => new(
        context,
        new RelationshipTargetResolver(context),
        new RelationshipEndpointPolicy(),
        new KnowledgeStatusPolicy(),
        TokenCodec);

    private static KnowledgeHubDbContext Context(string databasePath, IInterceptor? interceptor = null)
    {
        var builder = new DbContextOptionsBuilder<KnowledgeHubDbContext>()
            .UseSqlite($"Data Source={databasePath};Foreign Keys=True;Default Timeout=10;Pooling=False");
        if (interceptor is not null) builder.AddInterceptors(interceptor);
        return new KnowledgeHubDbContext(builder.Options);
    }

    private static async Task<(long SystemId, long OtherSystemId)> SeedSystems(string databasePath, bool includeSecondSystem)
    {
        await using var db = Context(databasePath);
        await db.Database.MigrateAsync();
        var now = DateTimeOffset.UtcNow;
        db.Users.Add(new User
        {
            Id = 1, DisplayName = AdminActor.DisplayName, IsActive = true, AccessLevel = AccessLevel.Administrator,
            CreatedAt = now, UpdatedAt = now, Version = 1,
        });
        var system = System("race_system", now);
        db.Systems.Add(system);
        KnowledgeSystem? other = null;
        if (includeSecondSystem)
        {
            other = System("race_other", now);
            db.Systems.Add(other);
        }
        await db.SaveChangesAsync();
        return (system.Id, other?.Id ?? 0);
    }

    private static async Task<string> Explain(KnowledgeHubDbContext db, string sql)
    {
        await db.Database.OpenConnectionAsync();
        await using var command = db.Database.GetDbConnection().CreateCommand();
        command.CommandText = "EXPLAIN QUERY PLAN " + sql;
        await using var reader = await command.ExecuteReaderAsync();
        var details = new List<string>();
        while (await reader.ReadAsync()) details.Add(reader.GetString(3));
        return string.Join(Environment.NewLine, details);
    }

    private static KnowledgeSystem System(string name, DateTimeOffset now) => new()
    {
        Name = name, DisplayName = name, SystemType = "Service", Lifecycle = SystemLifecycle.Running,
        CreatedAt = now, CreatedByUserId = 1, CreatedByName = AdminActor.DisplayName, UpdatedAt = now,
        KnowledgeStatus = KnowledgeStatus.Unknown, KnowledgeStatusChangedAt = now,
        KnowledgeStatusChangedByName = AdminActor.DisplayName, KnowledgeStatusChangedByRole = "Administrator", Version = 1,
    };

    private static async Task InDatabase(Func<string, Task> test)
    {
        var directory = Path.Combine(Path.GetTempPath(), $"delete-b02-race-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            await test(Path.Combine(directory, "race.db"));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private sealed class CommandGate(string commandFragment) : DbCommandInterceptor
    {
        private int entered;
        public TaskCompletionSource Reached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource Release { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public override async ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            await Pause(command, cancellationToken);
            return result;
        }

        public override async ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            await Pause(command, cancellationToken);
            return result;
        }

        private async Task Pause(DbCommand command, CancellationToken cancellationToken)
        {
            if (!command.CommandText.Contains(commandFragment, StringComparison.Ordinal)
                || Interlocked.Exchange(ref entered, 1) != 0)
            {
                return;
            }
            Reached.TrySetResult();
            await Release.Task.WaitAsync(cancellationToken);
        }
    }
}
