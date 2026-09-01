using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application.Models;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class DatabaseDiscoverySyncApiTests
{
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    [Fact]
    public async Task Manual_sync_create_update_missing_reappeared_is_explicit_atomic_and_preserves_human_knowledge()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        factory.DiscoveryProvider.SnapshotFactory = (connection, request, call) =>
        {
            var snapshot = CanonicalSnapshotFixtures.Create(connection, request, call >= 2 ? 2 : 1);
            if (call != 3) return snapshot;
            var orders = snapshot.Objects.Single(x => x.Name == "ORDERS").LogicalIdentity;
            return snapshot with
            {
                Objects = snapshot.Objects.Where(x => x.LogicalIdentity != orders).ToArray(),
                Columns = snapshot.Columns.Where(x => x.ParentObjectLogicalIdentity != orders).ToArray(),
                PrimaryKeys = snapshot.PrimaryKeys.Where(x => x.ParentObjectLogicalIdentity != orders).ToArray(),
                ForeignKeys = snapshot.ForeignKeys.Where(x => x.ParentObjectLogicalIdentity != orders).ToArray(),
                Indexes = snapshot.Indexes.Where(x => x.ParentObjectLogicalIdentity != orders).ToArray(),
            };
        };
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "DBDISC_B04_SECRET_CANARY");
        var first = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, first.Status);

        var viewerId = await CreateUser(factory, AccessLevel.Viewer);
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);
        using var viewerRead = await viewer.GetAsync($"/api/database-discovery/reconciliation?profileId={profile.Id}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, viewerRead.StatusCode);
        var firstReconciliation = await viewerRead.Content.ReadFromJsonAsync<DatabaseDiscoveryReconciliationPageResponse>(JsonOptions);
        Assert.NotNull(firstReconciliation);
        Assert.Contains(firstReconciliation!.Items, x => x.SuggestedAction == DatabaseDiscoverySyncActionType.CreateDatabaseObject);
        Assert.Contains(firstReconciliation.Items, x => x.SuggestedAction == DatabaseDiscoverySyncActionType.CreateDatabaseColumn);
        Assert.Contains(firstReconciliation.Items, x => x.Status == DatabaseDiscoveryReconciliationStatus.Unsupported);
        using var forbidden = await viewer.PostAsJsonAsync("/api/database-discovery/sync-plans", new
        {
            profileId = profile.Id,
            targetSnapshotId = first.SnapshotId,
            actions = new[] { new { actionType = "CreateDatabaseObject", logicalIdentity = "forbidden", targetId = (long?)null } },
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbidden.StatusCode);

        var editorId = await CreateUser(factory, AccessLevel.Editor);
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        var initialActions = firstReconciliation.Items.Where(x => x.SuggestedAction is
                DatabaseDiscoverySyncActionType.CreateDatabaseObject or DatabaseDiscoverySyncActionType.CreateDatabaseColumn)
            .Select(Selection).ToArray();
        var created = await CreatePlan(editor, profile.Id, first.SnapshotId!.Value, initialActions);
        var previewed = await Preview(editor, created);
        Assert.NotNull(previewed.Preview);
        Assert.Equal(2, previewed.Preview!.Counts.CreateObjects);
        Assert.Equal(4, previewed.Preview.Counts.CreateColumns);
        var samePreview = await Preview(editor, previewed);
        Assert.Equal(previewed.Preview.PreviewHash, samePreview.Preview!.PreviewHash);
        var confirmed = await Confirm(editor, samePreview);
        Assert.Equal(DatabaseDiscoverySyncPlanStatus.Ready, confirmed.Status);
        var applied = await Apply(editor, confirmed);
        Assert.Equal(DatabaseDiscoverySyncPlanStatus.Applied, applied.Status);
        Assert.Equal(2, applied.Result!.CreatedObjects);
        Assert.Equal(4, applied.Result.CreatedColumns);
        using (var duplicateApply = await editor.PostAsJsonAsync($"/api/database-discovery/sync-plans/{applied.Id}/apply", new
        {
            previewHash = applied.Preview!.PreviewHash,
            concurrencyToken = applied.ConcurrencyToken,
        }))
        {
            Assert.Equal(HttpStatusCode.Conflict, duplicateApply.StatusCode);
        }

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.Equal(2, await db.DatabaseObjects.CountAsync(x => x.DatabaseSourceId == profile.DatabaseSourceId));
            Assert.Equal(4, await db.DatabaseColumns.CountAsync(x => x.DatabaseObject.DatabaseSourceId == profile.DatabaseSourceId));
            Assert.Equal(2, await db.DatabaseObjectDiscoveryBindings.CountAsync());
            Assert.Equal(4, await db.DatabaseColumnDiscoveryBindings.CountAsync());
            Assert.All(await db.DatabaseObjects.Where(x => x.DatabaseSourceId == profile.DatabaseSourceId).ToArrayAsync(),
                item => Assert.Null(item.BusinessKeyColumnsJson));
            var customers = await db.DatabaseObjects.SingleAsync(x => x.DatabaseSourceId == profile.DatabaseSourceId && x.ObjectName == "CUSTOMERS");
            Assert.Equal("Customer master", customers.DatabaseComment);
            Assert.Equal(KnowledgeStatus.Unknown, customers.KnowledgeStatus);
        }

        profile = await GetProfile(administrator, profile.Id);
        var second = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, second.Status);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var name = await db.DatabaseColumns.SingleAsync(x => x.ColumnName == "NAME");
            var customers = await db.DatabaseObjects.SingleAsync(x => x.DatabaseSourceId == profile.DatabaseSourceId && x.ObjectName == "CUSTOMERS");
            name.BusinessDescription = "人工维护的客户姓名业务定义";
            name.KnownValues.Add(new ColumnKnownValue
            {
                ValueText = "VIP", Meaning = "人工维护的重要客户", SortOrder = 1,
                CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
            });
            name.Version++;
            customers.BusinessDescription = "人工维护的客户主表说明";
            customers.BusinessKeyColumnsJson = "[\"NAME\"]";
            customers.AccessMode = DatabaseAccessMode.Read;
            customers.KnowledgeStatus = KnowledgeStatus.Confirmed;
            customers.DatabaseComment = "等待发现同步的旧技术备注";
            customers.PrimaryKeyColumnsJson = null;
            customers.Version++;
            db.Evidence.AddRange(
                Evidence(EvidenceType.DatabaseComment, name.Id, "人工证据"),
                Evidence(EvidenceType.HumanConfirmation, name.Id, "人工确认"));
            await db.SaveChangesAsync();
        }
        var changed = await Reconcile(editor, profile.Id);
        var update = Assert.Single(changed.Items.Where(x => x.SuggestedAction == DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure));
        var objectUpdate = Assert.Single(changed.Items.Where(x => x.SuggestedAction == DatabaseDiscoverySyncActionType.UpdateDatabaseObjectStructure && x.ObjectName == "CUSTOMERS"));
        var updatePlan = await CreatePlan(editor, profile.Id, second.SnapshotId!.Value, [Selection(objectUpdate), Selection(update)]);
        updatePlan = await Preview(editor, updatePlan);
        updatePlan = await Confirm(editor, updatePlan);
        updatePlan = await Apply(editor, updatePlan);
        Assert.Equal(1, updatePlan.Result!.UpdatedColumns);
        Assert.Equal(1, updatePlan.Result.UpdatedObjects);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var name = await db.DatabaseColumns.SingleAsync(x => x.ColumnName == "NAME");
            var customers = await db.DatabaseObjects.SingleAsync(x => x.DatabaseSourceId == profile.DatabaseSourceId && x.ObjectName == "CUSTOMERS");
            Assert.Equal("VARCHAR2(200 CHAR)", name.DataType);
            Assert.Equal("人工维护的客户姓名业务定义", name.BusinessDescription);
            Assert.Equal("VIP", Assert.Single(await db.ColumnKnownValues.Where(x => x.DatabaseColumnId == name.Id).ToArrayAsync()).ValueText);
            Assert.Equal(2, await db.Evidence.CountAsync(x => x.SubjectType == EvidenceSubjectType.DatabaseColumn && x.SubjectId == name.Id));
            Assert.Equal("人工维护的客户主表说明", customers.BusinessDescription);
            Assert.Equal("[\"NAME\"]", customers.BusinessKeyColumnsJson);
            Assert.Equal(DatabaseAccessMode.Read, customers.AccessMode);
            Assert.Equal(KnowledgeStatus.Confirmed, customers.KnowledgeStatus);
            Assert.Equal("Customer master", customers.DatabaseComment);
            Assert.Equal("[\"ID\"]", customers.PrimaryKeyColumnsJson);
        }

        profile = await GetProfile(administrator, profile.Id);
        var third = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        var missing = await Reconcile(editor, profile.Id);
        var missingActions = missing.Items.Where(x => x.SuggestedAction is
                DatabaseDiscoverySyncActionType.MarkObjectSourceMissing or DatabaseDiscoverySyncActionType.MarkColumnSourceMissing)
            .Select(Selection).ToArray();
        Assert.Equal(3, missingActions.Length);
        var missingPlan = await Apply(editor, await Confirm(editor, await Preview(editor,
            await CreatePlan(editor, profile.Id, third.SnapshotId!.Value, missingActions))));
        Assert.Equal(3, missingPlan.Result!.MarkedMissing);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            Assert.True(await db.DatabaseObjects.AnyAsync(x => x.ObjectName == "ORDERS"));
            Assert.Equal(3, await db.DatabaseObjectDiscoveryBindings.CountAsync(x => x.SourceMissingSinceSnapshotId != null)
                + await db.DatabaseColumnDiscoveryBindings.CountAsync(x => x.SourceMissingSinceSnapshotId != null));
        }

        profile = await GetProfile(administrator, profile.Id);
        var fourth = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        var reappeared = await Reconcile(editor, profile.Id);
        var clearActions = reappeared.Items.Where(x => x.SuggestedAction is
                DatabaseDiscoverySyncActionType.ClearObjectSourceMissing or DatabaseDiscoverySyncActionType.ClearColumnSourceMissing)
            .Select(Selection).ToArray();
        Assert.Equal(3, clearActions.Length);
        var clearPlan = await Apply(editor, await Confirm(editor, await Preview(editor,
            await CreatePlan(editor, profile.Id, fourth.SnapshotId!.Value, clearActions))));
        Assert.Equal(3, clearPlan.Result!.ClearedMissing);
    }

    [Fact]
    public async Task Plan_selection_confirmation_and_latest_snapshot_gates_fail_closed()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "plan-gates-secret");
        var first = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        var reconciliation = await Reconcile(administrator, profile.Id);
        var action = Assert.Single(reconciliation.Items.Where(x =>
            x.SuggestedAction == DatabaseDiscoverySyncActionType.CreateDatabaseObject && x.ObjectName == "CUSTOMERS"));
        var draft = await CreatePlan(administrator, profile.Id, first.SnapshotId!.Value, [Selection(action)]);

        using (var unconfirmed = await administrator.PostAsJsonAsync($"/api/database-discovery/sync-plans/{draft.Id}/apply", new
        {
            previewHash = new string('a', 64), concurrencyToken = draft.ConcurrencyToken,
        }))
            Assert.Equal(HttpStatusCode.Conflict, unconfirmed.StatusCode);

        var previewed = await Preview(administrator, draft);
        var confirmed = await Confirm(administrator, previewed);
        using var update = await administrator.PutAsJsonAsync($"/api/database-discovery/sync-plans/{confirmed.Id}/actions", new
        {
            actions = new[] { Selection(action) }, concurrencyToken = confirmed.ConcurrencyToken,
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        var updated = await ReadPlan(update);
        Assert.Equal(DatabaseDiscoverySyncPlanStatus.Draft, updated.Status);
        Assert.Null(updated.Preview);
        Assert.Null(updated.ConfirmedPreviewHash);

        using (var staleSelection = await administrator.PutAsJsonAsync($"/api/database-discovery/sync-plans/{confirmed.Id}/actions", new
        {
            actions = new[] { Selection(action) }, concurrencyToken = confirmed.ConcurrencyToken,
        }))
            Assert.Equal(HttpStatusCode.Conflict, staleSelection.StatusCode);

        updated = await Confirm(administrator, await Preview(administrator, updated));
        profile = await GetProfile(administrator, profile.Id);
        var second = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, second.Status);
        using (var staleSnapshot = await administrator.PostAsJsonAsync($"/api/database-discovery/sync-plans/{updated.Id}/apply", new
        {
            previewHash = updated.Preview!.PreviewHash, concurrencyToken = updated.ConcurrencyToken,
        }))
            Assert.Equal(HttpStatusCode.Conflict, staleSnapshot.StatusCode);
        var superseded = await administrator.GetFromJsonAsync<DatabaseDiscoverySyncPlanResponse>(
            $"/api/database-discovery/sync-plans/{updated.Id}", JsonOptions);
        Assert.Equal(DatabaseDiscoverySyncPlanStatus.Superseded, superseded!.Status);

        using var oversized = await administrator.PostAsJsonAsync("/api/database-discovery/sync-plans", new
        {
            profileId = profile.Id,
            targetSnapshotId = second.SnapshotId,
            actions = Enumerable.Range(1, 2001).Select(i => new
            {
                actionType = "CreateDatabaseObject", logicalIdentity = $"oversized-{i}", targetId = (long?)null,
            }),
        });
        Assert.Equal(HttpStatusCode.BadRequest, oversized.StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(0, await verify.DatabaseObjectDiscoveryBindings.CountAsync());
        Assert.Equal(0, await verify.DatabaseObjects.CountAsync(x => x.DatabaseSourceId == profile.DatabaseSourceId));
        Assert.Contains(await verify.DatabaseDiscoverySyncAuditEvents.ToArrayAsync(),
            x => x.Action == DatabaseDiscoverySyncAuditAction.PlanSuperseded);
    }

    [Fact]
    public async Task Incompatible_scope_is_out_of_scope_and_identity_version_requires_rebaseline()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "scope-secret");
        var first = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        var initial = await Reconcile(administrator, profile.Id);
        var initialActions = initial.Items.Where(x => x.SuggestedAction is
                DatabaseDiscoverySyncActionType.CreateDatabaseObject or DatabaseDiscoverySyncActionType.CreateDatabaseColumn)
            .Select(Selection).ToArray();
        await Apply(administrator, await Confirm(administrator, await Preview(administrator,
            await CreatePlan(administrator, profile.Id, first.SnapshotId!.Value, initialActions))));

        profile = await UpdateIncludedSchemas(administrator, await GetProfile(administrator, profile.Id), ["OTHER_OWNER"]);
        var changedScope = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, changedScope.Status);
        var outOfScope = await Reconcile(administrator, profile.Id);
        Assert.Contains(outOfScope.Items, x => x.Category == "OutOfScope" && x.BlockCode == "OutOfScope");
        Assert.DoesNotContain(outOfScope.Items, x => x.SuggestedAction is
            DatabaseDiscoverySyncActionType.MarkObjectSourceMissing or DatabaseDiscoverySyncActionType.MarkColumnSourceMissing);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var snapshotId = await db.DatabaseDiscoverySnapshots.OrderByDescending(x => x.Id).Select(x => x.Id).FirstAsync();
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE database_discovery_snapshots SET identity_algorithm_version = 2 WHERE id = {snapshotId}");
        }
        var rebaseline = await Reconcile(administrator, profile.Id);
        Assert.Contains(rebaseline.Items, x => x.Category == "RebaselineRequired" && x.BlockCode == "RebaselineRequired");
        Assert.DoesNotContain(rebaseline.Items, x => x.SuggestedAction is
            DatabaseDiscoverySyncActionType.MarkObjectSourceMissing or DatabaseDiscoverySyncActionType.MarkColumnSourceMissing);
    }

    [Fact]
    public async Task Stale_column_token_cannot_partially_apply()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "column-stale-secret");
        var first = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        var initial = await Reconcile(administrator, profile.Id);
        var initialActions = initial.Items.Where(x => x.SuggestedAction is
                DatabaseDiscoverySyncActionType.CreateDatabaseObject or DatabaseDiscoverySyncActionType.CreateDatabaseColumn)
            .Select(Selection).ToArray();
        await Apply(administrator, await Confirm(administrator, await Preview(administrator,
            await CreatePlan(administrator, profile.Id, first.SnapshotId!.Value, initialActions))));

        profile = await GetProfile(administrator, profile.Id);
        var second = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        var changed = await Reconcile(administrator, profile.Id);
        var update = Assert.Single(changed.Items.Where(x => x.SuggestedAction == DatabaseDiscoverySyncActionType.UpdateDatabaseColumnStructure));
        var ready = await Confirm(administrator, await Preview(administrator,
            await CreatePlan(administrator, profile.Id, second.SnapshotId!.Value, [Selection(update)])));
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var target = await db.DatabaseColumns.SingleAsync(x => x.Id == update.TargetId);
            target.BusinessDescription = "并发人工编辑";
            target.Version++;
            await db.SaveChangesAsync();
        }
        using var response = await administrator.PostAsJsonAsync($"/api/database-discovery/sync-plans/{ready.Id}/apply", new
        {
            previewHash = ready.Preview!.PreviewHash, concurrencyToken = ready.ConcurrencyToken,
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var unchanged = await verify.DatabaseColumns.SingleAsync(x => x.Id == update.TargetId);
        Assert.Equal("VARCHAR2(100 CHAR)", unchanged.DataType);
        Assert.Equal("并发人工编辑", unchanged.BusinessDescription);
        Assert.False(await verify.DatabaseDiscoverySyncApplyResults.AnyAsync(x => x.PlanId == ready.Id));
    }

    [Fact]
    public async Task Active_ordinal_conflict_is_blocked_before_apply()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "ordinal-secret");
        var run = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var snapshotEntity = await db.DatabaseDiscoverySnapshots.SingleAsync(x => x.Id == run.SnapshotId);
            var snapshot = JsonSerializer.Deserialize<CanonicalDatabaseDiscoverySnapshot>(snapshotEntity.CanonicalContentJson, JsonOptions)!;
            var sourceObject = snapshot.Objects.Single(x => x.Name == "CUSTOMERS");
            var sourceColumn = snapshot.Columns.Single(x => x.Name == "NAME");
            var admin = await db.Users.FirstAsync(x => x.AccessLevel == AccessLevel.Administrator);
            var now = DateTimeOffset.UtcNow;
            var parent = new DatabaseObject
            {
                DatabaseSourceId = profile.DatabaseSourceId, SchemaName = sourceObject.SchemaName, ObjectName = sourceObject.Name,
                ObjectType = DatabaseObjectType.Table, DatabaseComment = sourceObject.DatabaseComment,
                TechnicalIdentityAlgorithmVersion = snapshot.IdentityAlgorithmVersion, TechnicalIdentity = sourceObject.LogicalIdentity,
                AccessMode = DatabaseAccessMode.Unknown, CreatedAt = now, CreatedByUserId = admin.Id, CreatedByName = admin.DisplayName,
                UpdatedAt = now, KnowledgeStatus = KnowledgeStatus.Unknown, KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = admin.DisplayName, KnowledgeStatusChangedByRole = "Administrator", Version = 1,
            };
            var boundColumn = Column(3, sourceColumn.Name, sourceColumn.NativeDataType.Declaration, sourceColumn.IsNullable,
                sourceColumn.DefaultExpression, sourceColumn.DatabaseComment, admin.Id, admin.DisplayName, now);
            boundColumn.TechnicalIdentityAlgorithmVersion = snapshot.IdentityAlgorithmVersion;
            boundColumn.TechnicalIdentity = sourceColumn.LogicalIdentity;
            parent.Columns.Add(boundColumn);
            parent.Columns.Add(Column(2, "MANUAL_ONLY", "VARCHAR2(20 CHAR)", true, null, null,
                admin.Id, admin.DisplayName, now));
            db.DatabaseObjects.Add(parent);
            await db.SaveChangesAsync();
            db.DatabaseObjectDiscoveryBindings.Add(new DatabaseObjectDiscoveryBinding
            {
                ProfileId = profile.Id, ScopeGenerationId = snapshotEntity.ScopeGenerationId,
                IdentityAlgorithmVersion = snapshot.IdentityAlgorithmVersion, SchemaLogicalIdentity = sourceObject.SchemaLogicalIdentity,
                LogicalIdentity = sourceObject.LogicalIdentity, DatabaseObjectId = parent.Id,
                FirstAppliedSnapshotId = snapshotEntity.Id, LastAppliedSnapshotId = snapshotEntity.Id,
                CreatedAt = now, UpdatedAt = now, Version = 1,
            });
            db.DatabaseColumnDiscoveryBindings.Add(new DatabaseColumnDiscoveryBinding
            {
                ProfileId = profile.Id, ScopeGenerationId = snapshotEntity.ScopeGenerationId,
                IdentityAlgorithmVersion = snapshot.IdentityAlgorithmVersion, SchemaLogicalIdentity = sourceObject.SchemaLogicalIdentity,
                ParentObjectLogicalIdentity = sourceObject.LogicalIdentity, LogicalIdentity = sourceColumn.LogicalIdentity,
                DatabaseColumnId = boundColumn.Id, FirstAppliedSnapshotId = snapshotEntity.Id, LastAppliedSnapshotId = snapshotEntity.Id,
                CreatedAt = now, UpdatedAt = now, Version = 1,
            });
            await db.SaveChangesAsync();
        }
        var changed = await Reconcile(administrator, profile.Id);
        Assert.Contains(changed.Items, x => x.ChildName == "NAME" && x.BlockCode == "ActiveOrdinalConflict"
            && x.Status == DatabaseDiscoveryReconciliationStatus.Conflict && x.SuggestedAction is null);
    }

    [Fact]
    public async Task Null_source_ordinal_is_an_unsupported_conflict()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        factory.DiscoveryProvider.SnapshotFactory = (connection, request, call) =>
        {
            var snapshot = CanonicalSnapshotFixtures.Create(connection, request, call);
            return snapshot with
            {
                Columns = snapshot.Columns.Select(x => x.Name == "NAME" ? x with { SourceOrdinal = null } : x).ToArray(),
            };
        };
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator,
            await CreateProfile(factory, administrator), "null-ordinal-secret");
        await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        var nullOrdinal = await Reconcile(administrator, profile.Id);
        Assert.Contains(nullOrdinal.Items, x => x.BlockCode == "UnsupportedOrdinal"
            && x.Status == DatabaseDiscoveryReconciliationStatus.Conflict);
    }

    [Theory]
    [InlineData(DatabaseProviderType.PostgreSql)]
    [InlineData(DatabaseProviderType.SqlServer)]
    public async Task Additional_provider_canonical_snapshot_uses_the_same_provider_neutral_sync_pipeline(
        DatabaseProviderType providerType)
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        factory.DiscoveryProvider.ProviderType = providerType;
        factory.DiscoveryProvider.SnapshotFactory = (connection, request, call) =>
        {
            var snapshot = CanonicalSnapshotFixtures.Create(connection, request, call);
            return snapshot with
            {
                ProviderType = providerType,
                ProviderVersion = providerType == DatabaseProviderType.SqlServer
                    ? "FakeSqlServer/1"
                    : "FakePostgreSql/1",
                DatabaseInfo = snapshot.DatabaseInfo with
                {
                    Provider = providerType.ToString(),
                    ServerVersion = providerType == DatabaseProviderType.SqlServer ? "16.0.4215.2" : "18.6",
                    CurrentDatabaseOrService = providerType == DatabaseProviderType.SqlServer
                        ? "SKH_DBDISC"
                        : "knowledge_test",
                    CurrentContainer = null,
                    TargetFingerprint = providerType == DatabaseProviderType.SqlServer
                        ? "fake-sqlserver-target-v1"
                        : "fake-postgresql-target-v1",
                },
            };
        };
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator,
            await CreateProfile(factory, administrator, providerType), $"{providerType}-secret");
        var run = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        Assert.Equal(DatabaseDiscoveryRunStatus.Succeeded, run.Status);
        var reconciliation = await Reconcile(administrator, profile.Id);
        var actions = reconciliation.Items.Where(x => x.SuggestedAction is
                DatabaseDiscoverySyncActionType.CreateDatabaseObject or DatabaseDiscoverySyncActionType.CreateDatabaseColumn)
            .Select(Selection).ToArray();
        var applied = await Apply(administrator, await Confirm(administrator, await Preview(administrator,
            await CreatePlan(administrator, profile.Id, run.SnapshotId!.Value, actions))));
        Assert.Equal(2, applied.Result!.CreatedObjects);
        Assert.Equal(4, applied.Result.CreatedColumns);
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(2, await db.DatabaseObjectDiscoveryBindings.CountAsync());
        Assert.Equal(4, await db.DatabaseColumnDiscoveryBindings.CountAsync());
    }

    [Fact]
    public async Task Apply_rejects_stale_target_token_without_partial_writes()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "stale-secret");
        var run = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        var reconciliation = await Reconcile(administrator, profile.Id);
        var objectAction = Assert.Single(reconciliation.Items.Where(x => x.SuggestedAction == DatabaseDiscoverySyncActionType.CreateDatabaseObject && x.ObjectName == "CUSTOMERS"));
        var plan = await Preview(administrator, await CreatePlan(administrator, profile.Id, run.SnapshotId!.Value, [Selection(objectAction)]));
        plan = await Confirm(administrator, plan);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            db.DatabaseObjects.Add(new DatabaseObject
            {
                DatabaseSourceId = profile.DatabaseSourceId, SchemaName = "APP_OWNER", ObjectName = "CUSTOMERS",
                ObjectType = DatabaseObjectType.Table, AccessMode = DatabaseAccessMode.Unknown,
                CreatedAt = DateTimeOffset.UtcNow, CreatedByName = "concurrent", UpdatedAt = DateTimeOffset.UtcNow,
                KnowledgeStatus = KnowledgeStatus.Unknown, KnowledgeStatusChangedAt = DateTimeOffset.UtcNow,
                KnowledgeStatusChangedByName = "concurrent", KnowledgeStatusChangedByRole = "Editor", Version = 1,
            });
            await db.SaveChangesAsync();
        }
        using var response = await administrator.PostAsJsonAsync($"/api/database-discovery/sync-plans/{plan.Id}/apply", new
        {
            previewHash = plan.Preview!.PreviewHash,
            concurrencyToken = plan.ConcurrencyToken,
        });
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(0, await verify.DatabaseObjectDiscoveryBindings.CountAsync());
        Assert.Equal(1, await verify.DatabaseObjects.CountAsync(x => x.DatabaseSourceId == profile.DatabaseSourceId));
    }

    [Fact]
    public async Task Exact_match_link_is_explicit_and_does_not_overwrite_human_fields()
    {
        using var factory = new DatabaseDiscoveryWebApplicationFactory();
        using var administrator = factory.CreateAuthenticatedClient();
        var profile = await SetSecret(administrator, await CreateProfile(factory, administrator), "link-secret");
        var run = await WaitForTerminal(administrator, (await Trigger(administrator, profile)).Id);
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var admin = await db.Users.FirstAsync(x => x.AccessLevel == AccessLevel.Administrator);
            var now = DateTimeOffset.UtcNow;
            var target = new DatabaseObject
            {
                DatabaseSourceId = profile.DatabaseSourceId, SchemaName = "APP_OWNER", ObjectName = "CUSTOMERS",
                ObjectType = DatabaseObjectType.Table, DatabaseComment = "人工保留的技术备注",
                BusinessDescription = "人工维护的客户主表说明", AccessMode = DatabaseAccessMode.Read,
                CreatedAt = now, CreatedByUserId = admin.Id, CreatedByName = admin.DisplayName,
                UpdatedAt = now, KnowledgeStatus = KnowledgeStatus.Confirmed, KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = admin.DisplayName, KnowledgeStatusChangedByRole = "Administrator", Version = 1,
            };
            target.Columns.Add(Column(1, "ID", "NUMBER(19)", false, null, null, admin.Id, admin.DisplayName, now));
            target.Columns.Add(Column(2, "NAME", "VARCHAR2(100 CHAR)", false, null, "Customer name", admin.Id, admin.DisplayName, now));
            db.DatabaseObjects.Add(target);
            await db.SaveChangesAsync();
        }
        var reconciliation = await Reconcile(administrator, profile.Id);
        var links = reconciliation.Items.Where(x => x.SuggestedAction is
            DatabaseDiscoverySyncActionType.LinkExistingDatabaseObject or DatabaseDiscoverySyncActionType.LinkExistingDatabaseColumn).Select(Selection).ToArray();
        Assert.Equal(3, links.Length);
        var applied = await Apply(administrator, await Confirm(administrator, await Preview(administrator,
            await CreatePlan(administrator, profile.Id, run.SnapshotId!.Value, links))));
        Assert.Equal(1, applied.Result!.LinkedObjects);
        Assert.Equal(2, applied.Result.LinkedColumns);
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verify = verifyScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var linked = await verify.DatabaseObjects.SingleAsync(x => x.DatabaseSourceId == profile.DatabaseSourceId);
        Assert.Equal("人工维护的客户主表说明", linked.BusinessDescription);
        Assert.Equal("人工保留的技术备注", linked.DatabaseComment);
        Assert.Equal(DatabaseAccessMode.Read, linked.AccessMode);
        Assert.Equal(KnowledgeStatus.Confirmed, linked.KnowledgeStatus);
        Assert.Equal(1, await verify.DatabaseObjectDiscoveryBindings.CountAsync());
        Assert.Equal(2, await verify.DatabaseColumnDiscoveryBindings.CountAsync());
    }

    private static DatabaseColumn Column(
        int ordinal, string name, string dataType, bool nullable, string? defaultValue, string? comment,
        long actorId, string actorName, DateTimeOffset now) => new()
    {
        OrdinalPosition = ordinal, ColumnName = name, DataType = dataType, IsNullable = nullable,
        DefaultValue = defaultValue, DatabaseComment = comment, BusinessDescription = $"人工字段说明：{name}",
        CreatedAt = now, CreatedByUserId = actorId, CreatedByDisplayName = actorName, UpdatedAt = now,
        KnowledgeStatus = KnowledgeStatus.Confirmed, KnowledgeStatusChangedAt = now,
        KnowledgeStatusChangedByName = actorName, KnowledgeStatusChangedByRole = "Administrator", Version = 1,
    };

    private static Evidence Evidence(EvidenceType type, long subjectId, string title) => new()
    {
        EvidenceType = type,
        SubjectType = EvidenceSubjectType.DatabaseColumn,
        SubjectId = subjectId,
        SourceTitle = title,
        SourceReference = "manual://dbdisc-b04",
        SupportReason = "B04 人工知识保护回归",
        ProviderName = "人工审查人",
        ProviderRole = "Administrator",
        ProvidedAt = DateTimeOffset.UtcNow,
        CreatedAt = DateTimeOffset.UtcNow,
        UpdatedAt = DateTimeOffset.UtcNow,
        Version = 1,
    };

    private static object Selection(DatabaseDiscoveryReconciliationCandidateResponse item) => new
    {
        actionType = item.SuggestedAction!.Value.ToString(), item.LogicalIdentity, item.TargetId,
    };

    private static async Task<DatabaseDiscoveryReconciliationPageResponse> Reconcile(HttpClient client, long profileId)
    {
        using var response = await client.GetAsync($"/api/database-discovery/reconciliation?profileId={profileId}&pageSize=100");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await response.Content.ReadFromJsonAsync<DatabaseDiscoveryReconciliationPageResponse>(JsonOptions)
            ?? throw new InvalidOperationException("Reconciliation response empty.");
    }

    private static async Task<DatabaseDiscoverySyncPlanResponse> CreatePlan(HttpClient client, long profileId, long snapshotId, object[] actions)
    {
        using var response = await client.PostAsJsonAsync("/api/database-discovery/sync-plans", new { profileId, targetSnapshotId = snapshotId, actions });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return await ReadPlan(response);
    }
    private static async Task<DatabaseDiscoverySyncPlanResponse> Preview(HttpClient client, DatabaseDiscoverySyncPlanResponse plan)
    {
        using var response = await client.PostAsJsonAsync($"/api/database-discovery/sync-plans/{plan.Id}/preview", new { concurrencyToken = plan.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadPlan(response);
    }
    private static async Task<DatabaseDiscoverySyncPlanResponse> Confirm(HttpClient client, DatabaseDiscoverySyncPlanResponse plan)
    {
        using var response = await client.PostAsJsonAsync($"/api/database-discovery/sync-plans/{plan.Id}/confirm", new { previewHash = plan.Preview!.PreviewHash, concurrencyToken = plan.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadPlan(response);
    }
    private static async Task<DatabaseDiscoverySyncPlanResponse> Apply(HttpClient client, DatabaseDiscoverySyncPlanResponse plan)
    {
        using var response = await client.PostAsJsonAsync($"/api/database-discovery/sync-plans/{plan.Id}/apply", new { previewHash = plan.Preview!.PreviewHash, concurrencyToken = plan.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return await ReadPlan(response);
    }
    private static async Task<DatabaseDiscoverySyncPlanResponse> ReadPlan(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<DatabaseDiscoverySyncPlanResponse>(JsonOptions)
        ?? throw new InvalidOperationException("Plan response empty.");

    private static async Task<DatabaseDiscoveryRunResponse> Trigger(HttpClient client, DatabaseConnectionProfileResponse profile)
    {
        using var response = await client.PostAsJsonAsync($"/api/admin/database-connection-profiles/{profile.Id}/discovery-runs", new { concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DatabaseDiscoveryRunResponse>(JsonOptions))!;
    }
    private static async Task<DatabaseDiscoveryRunResponse> WaitForTerminal(HttpClient client, long id)
    {
        var deadline = DateTimeOffset.UtcNow.AddSeconds(8);
        while (DateTimeOffset.UtcNow < deadline)
        {
            var run = await client.GetFromJsonAsync<DatabaseDiscoveryRunResponse>($"/api/database-discovery/runs/{id}", JsonOptions);
            if (run!.Status is DatabaseDiscoveryRunStatus.Succeeded or DatabaseDiscoveryRunStatus.Failed or DatabaseDiscoveryRunStatus.Cancelled) return run;
            await Task.Delay(40);
        }
        throw new TimeoutException();
    }
    private static async Task<DatabaseConnectionProfileResponse> CreateProfile(
        DatabaseDiscoveryWebApplicationFactory factory,
        HttpClient client,
        DatabaseProviderType providerType = DatabaseProviderType.Oracle)
    {
        var sourceEngine = providerType switch
        {
            DatabaseProviderType.PostgreSql => "PostgreSQL",
            DatabaseProviderType.SqlServer => "SQL Server",
            _ => "Oracle",
        };
        var port = providerType switch
        {
            DatabaseProviderType.PostgreSql => 5432,
            DatabaseProviderType.SqlServer => 1433,
            _ => 1521,
        };
        var databaseName = providerType switch
        {
            DatabaseProviderType.PostgreSql => "knowledge_test",
            DatabaseProviderType.SqlServer => "SKH_DBDISC",
            _ => null,
        };
        var sourceId = await CreateSource(factory, sourceEngine);
        using var response = await client.PostAsJsonAsync("/api/admin/database-connection-profiles", new
        {
            databaseSourceId = sourceId, name = $"B04-{Guid.NewGuid():N}", providerType = providerType.ToString(),
            host = "db.example.test", port,
            databaseName, serviceName = providerType == DatabaseProviderType.Oracle ? "APP_PDB" : null,
            authenticationMode = "UsernamePassword", username = "METADATA_READER",
            providerSpecificOptions = new { version = 1 }, includedSchemas = new[] { "APP_OWNER" }, isEnabled = true,
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DatabaseConnectionProfileResponse>(JsonOptions))!;
    }
    private static async Task<DatabaseConnectionProfileResponse> SetSecret(HttpClient client, DatabaseConnectionProfileResponse profile, string password)
    {
        using var response = await client.PostAsJsonAsync($"/api/admin/database-connection-profiles/{profile.Id}/secret", new { password, concurrencyToken = profile.ConcurrencyToken });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DatabaseConnectionProfileResponse>(JsonOptions))!;
    }
    private static async Task<DatabaseConnectionProfileResponse> GetProfile(HttpClient client, long id) =>
        (await client.GetFromJsonAsync<DatabaseConnectionProfileResponse>($"/api/admin/database-connection-profiles/{id}", JsonOptions))!;
    private static async Task<DatabaseConnectionProfileResponse> UpdateIncludedSchemas(
        HttpClient client, DatabaseConnectionProfileResponse profile, string[] includedSchemas)
    {
        using var response = await client.PutAsJsonAsync($"/api/admin/database-connection-profiles/{profile.Id}", new
        {
            profile.Name,
            providerType = profile.ProviderType.ToString(),
            profile.Host,
            profile.Port,
            profile.DatabaseName,
            profile.ServiceName,
            authenticationMode = profile.AuthenticationMode.ToString(),
            profile.Username,
            providerSpecificOptions = new { profile.ProviderSpecificOptions.Version },
            includedSchemas,
            profile.ConcurrencyToken,
        });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<DatabaseConnectionProfileResponse>(JsonOptions))!;
    }
    private static async Task<long> CreateSource(DatabaseDiscoveryWebApplicationFactory factory, string engine = "Oracle")
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var systemId = await db.Systems.Select(x => x.Id).FirstAsync();
        var admin = await db.Users.FirstAsync(x => x.AccessLevel == AccessLevel.Administrator);
        var now = DateTimeOffset.UtcNow;
        var source = new DatabaseSource { SystemId = systemId, Name = $"B04-{Guid.NewGuid():N}", Engine = engine, CreatedAt = now, CreatedByUserId = admin.Id, CreatedByName = admin.DisplayName, UpdatedAt = now, Version = 1 };
        db.DatabaseSources.Add(source); await db.SaveChangesAsync(); return source.Id;
    }
    private static async Task<long> CreateUser(DatabaseDiscoveryWebApplicationFactory factory, AccessLevel accessLevel)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User { DisplayName = $"B04 {accessLevel} {Guid.NewGuid():N}", IsActive = true, AccessLevel = accessLevel, CreatedAt = now, UpdatedAt = now, Version = 1 };
        db.Users.Add(user); await db.SaveChangesAsync(); return user.Id;
    }
    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }
}
