using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Evidence.Domain;
using SystemKnowledgeHub.Api.Features.Integrations.Domain;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Domain;
using SystemKnowledgeHub.Api.Features.Relationships.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Features.UnknownItems.Domain;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Api;

public sealed class CoreSoftDeleteApiTests : IClassFixture<BootstrapWebApplicationFactory>
{
    private readonly BootstrapWebApplicationFactory factory;
    private readonly HttpClient admin;

    public CoreSoftDeleteApiTests(BootstrapWebApplicationFactory factory)
    {
        this.factory = factory;
        admin = factory.CreateAuthenticatedClient();
    }

    [Fact]
    public async Task Eight_concrete_endpoints_soft_delete_in_dependency_order_with_audit_and_no_cascade()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var currentUser = await Read(admin, "/api/current-user");
        var system = await CreateSystem(admin, $"DELETE_B02_ROOT_{suffix}");
        var systemId = Id(system);
        var source = await Created(admin, "/api/database-sources", new
        {
            systemId, name = $"source_{suffix}", engine = "SQLite", environment = "Test",
            databaseName = $"db_{suffix}", isPrimary = false, actor = Actor(),
        });
        var databaseObject = await Created(admin, "/api/database-objects", new
        {
            databaseSourceId = Id(source), schemaName = "main", objectName = $"object_{suffix}", objectType = "Table",
            estimatedRows = 7L, accessMode = "Read", primaryKeyColumns = Array.Empty<string>(),
            businessKeyColumns = Array.Empty<string>(), businessDescription = "must remain", actor = Actor(),
        });
        var column = await Created(admin, $"/api/database-objects/{Id(databaseObject)}/columns", new
        {
            ordinalPosition = 1, columnName = $"column_{suffix}", dataType = "TEXT", nullable = false,
            businessDescription = "must remain", actor = Actor(), concurrencyToken = Token(databaseObject),
        });
        var function = await Created(admin, "/api/business-functions", new
        {
            systemId, name = $"function_{suffix}", functionType = "Query", rewriteStatus = "Unknown", actor = Actor(),
        });
        var rule = await Created(admin, "/api/business-rules", new
        {
            systemId, name = $"rule_{suffix}", description = "must remain", inputData = Array.Empty<object>(), actor = Actor(),
        });
        var integration = await Created(admin, "/api/integrations", new
        {
            name = $"integration_{suffix}", integrationType = "RabbitMq",
            sourceParty = new { systemId = (long?)systemId, displayName = "Source" },
            targetParty = new { systemId = (long?)null, displayName = "External" },
            flowDirection = "OneWay", endpoint = new { exchange = "b02", topic = $"topic.{suffix}", queue = (string?)null },
            actor = Actor(),
        });
        var document = await Created(admin, "/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle", title = $"DELETE-B02 {suffix}", summary = "must remain", bodyMarkdown = $"fts_{suffix}",
        });

        var objectTokenAfterColumn = column.GetProperty("parentConcurrencyToken").GetString()!;
        var columnPayload = column.GetProperty("column");
        await Deleted(admin, $"/api/database-columns/{Id(columnPayload)}", Token(columnPayload));
        await Deleted(admin, $"/api/database-objects/{Id(databaseObject)}", objectTokenAfterColumn);
        await Deleted(admin, $"/api/database-sources/{Id(source)}", Token(source));
        await Deleted(admin, $"/api/business-functions/{Id(function)}", Token(function));
        await Deleted(admin, $"/api/business-rules/{Id(rule)}", Token(rule));
        await Deleted(admin, $"/api/integrations/{Id(integration)}", Token(integration));
        await Deleted(admin, $"/api/knowledge-documents/{Id(document)}", Token(document));
        await Deleted(admin, $"/api/systems/{systemId}", Token(system));

        foreach (var route in new[]
        {
            $"/api/systems/{systemId}", $"/api/business-functions/{Id(function)}",
            $"/api/database-objects/{Id(databaseObject)}", $"/api/database-columns/{Id(columnPayload)}",
            $"/api/business-rules/{Id(rule)}", $"/api/integrations/{Id(integration)}",
            $"/api/knowledge-documents/{Id(document)}",
        })
        {
            using var detail = await admin.GetAsync(route);
            Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        }

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var expectedUserId = currentUser.GetProperty("id").GetInt64();
        var expectedName = currentUser.GetProperty("displayName").GetString();
        var rows = new[]
        {
            await db.Systems.IgnoreQueryFilters().Where(x => x.Id == systemId).Select(x => new DeleteState(x.IsDeleted, x.DeletedAt, x.DeletedByUserId, x.DeletedByDisplayName, x.Version)).SingleAsync(),
            await db.DatabaseSources.IgnoreQueryFilters().Where(x => x.Id == Id(source)).Select(x => new DeleteState(x.IsDeleted, x.DeletedAt, x.DeletedByUserId, x.DeletedByDisplayName, x.Version)).SingleAsync(),
            await db.BusinessFunctions.IgnoreQueryFilters().Where(x => x.Id == Id(function)).Select(x => new DeleteState(x.IsDeleted, x.DeletedAt, x.DeletedByUserId, x.DeletedByDisplayName, x.Version)).SingleAsync(),
            await db.DatabaseObjects.IgnoreQueryFilters().Where(x => x.Id == Id(databaseObject)).Select(x => new DeleteState(x.IsDeleted, x.DeletedAt, x.DeletedByUserId, x.DeletedByDisplayName, x.Version)).SingleAsync(),
            await db.DatabaseColumns.IgnoreQueryFilters().Where(x => x.Id == Id(columnPayload)).Select(x => new DeleteState(x.IsDeleted, x.DeletedAt, x.DeletedByUserId, x.DeletedByDisplayName, x.Version)).SingleAsync(),
            await db.BusinessRules.IgnoreQueryFilters().Where(x => x.Id == Id(rule)).Select(x => new DeleteState(x.IsDeleted, x.DeletedAt, x.DeletedByUserId, x.DeletedByDisplayName, x.Version)).SingleAsync(),
            await db.Integrations.IgnoreQueryFilters().Where(x => x.Id == Id(integration)).Select(x => new DeleteState(x.IsDeleted, x.DeletedAt, x.DeletedByUserId, x.DeletedByDisplayName, x.Version)).SingleAsync(),
            await db.KnowledgeDocuments.IgnoreQueryFilters().Where(x => x.Id == Id(document)).Select(x => new DeleteState(x.IsDeleted, x.DeletedAt, x.DeletedByUserId, x.DeletedByDisplayName, x.Version)).SingleAsync(),
        };
        Assert.Equal(new long[] { 2, 2, 2, 3, 2, 2, 2, 2 }, rows.Select(row => row.Version));
        Assert.All(rows, row =>
        {
            Assert.True(row.IsDeleted);
            Assert.NotNull(row.DeletedAt);
            Assert.Equal(TimeSpan.Zero, row.DeletedAt!.Value.Offset);
            Assert.Equal(expectedUserId, row.DeletedByUserId);
            Assert.Equal(expectedName, row.DeletedByDisplayName);
        });
        Assert.Equal("must remain", await db.DatabaseColumns.IgnoreQueryFilters().Where(x => x.Id == Id(columnPayload)).Select(x => x.BusinessDescription).SingleAsync());
        Assert.Equal("must remain", await db.BusinessRules.IgnoreQueryFilters().Where(x => x.Id == Id(rule)).Select(x => x.Description).SingleAsync());
        Assert.Equal(SystemLifecycle.Running, await db.Systems.IgnoreQueryFilters().Where(x => x.Id == systemId).Select(x => x.Lifecycle).SingleAsync());
        Assert.Equal(KnowledgeStatus.Unknown, await db.BusinessFunctions.IgnoreQueryFilters().Where(x => x.Id == Id(function)).Select(x => x.KnowledgeStatus).SingleAsync());
        var documentState = await db.KnowledgeDocuments.IgnoreQueryFilters().Where(x => x.Id == Id(document))
            .Select(x => new { x.LifecycleStatus, x.KnowledgeStatus, x.CurrentRevisionNumber, x.LatestPublishedRevisionNumber, x.PublishedAt, x.ArchivedAt }).SingleAsync();
        Assert.Equal(DocumentLifecycleStatus.Draft, documentState.LifecycleStatus);
        Assert.Equal(KnowledgeStatus.Unknown, documentState.KnowledgeStatus);
        Assert.Equal(1, documentState.CurrentRevisionNumber);
        Assert.Null(documentState.LatestPublishedRevisionNumber);
        Assert.Null(documentState.PublishedAt);
        Assert.Null(documentState.ArchivedAt);
        Assert.Equal(1, await db.KnowledgeDocumentRevisions.CountAsync(x => x.KnowledgeDocumentId == Id(document)));
        Assert.Equal(0, await db.Database.SqlQuery<long>($"SELECT count(*) AS Value FROM knowledge_documents_fts WHERE rowid={Id(document)}").SingleAsync());
    }

    [Fact]
    public async Task Authorization_matrix_enforces_editor_ownership_legacy_denial_and_admin_override()
    {
        var editorId = await CreateUser(AccessLevel.Editor, "DELETE B02 Editor");
        var viewerId = await CreateUser(AccessLevel.Viewer, "DELETE B02 Viewer");
        using var editor = await factory.CreateAuthenticatedClientAsync(editorId);
        using var viewer = await factory.CreateAuthenticatedClientAsync(viewerId);
        using var anonymous = factory.CreateClient();
        using var withoutAntiforgery = factory.CreateAuthenticatedClientWithoutAntiforgery();
        var own = await CreateSystem(editor, $"EDITOR_OWN_{Guid.NewGuid():N}");
        var other = await CreateSystem(admin, $"EDITOR_OTHER_{Guid.NewGuid():N}");
        var legacy = await CreateSystem(admin, $"LEGACY_{Guid.NewGuid():N}");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            await db.Database.ExecuteSqlInterpolatedAsync($"UPDATE systems SET created_by_user_id = NULL WHERE id = {Id(legacy)}");
        }

        Assert.Equal(HttpStatusCode.Forbidden, (await Delete(editor, $"/api/systems/{Id(other)}", Token(other))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Delete(editor, $"/api/systems/{Id(legacy)}", Token(legacy))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Delete(viewer, $"/api/systems/{Id(other)}", Token(other))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Delete(anonymous, $"/api/systems/{Id(other)}", Token(other))).StatusCode);
        using (var csrf = await Delete(withoutAntiforgery, $"/api/systems/{Id(other)}", Token(other)))
        {
            Assert.Equal(HttpStatusCode.Forbidden, csrf.StatusCode);
            Assert.Equal("antiforgery_failed", (await csrf.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        await Deleted(editor, $"/api/systems/{Id(own)}", Token(own));
        await Deleted(admin, $"/api/systems/{Id(legacy)}", Token(legacy));
    }

    [Fact]
    public async Task Delete_contract_distinguishes_validation_conflict_dependencies_and_deleted_not_found()
    {
        var source = await CreateSystem(admin, $"DELETE_CONTRACT_{Guid.NewGuid():N}");
        using (var invalid = await Delete(admin, $"/api/systems/{Id(source)}", "invalid"))
        {
            Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
            Assert.Equal("validation_error", (await invalid.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }

        using var update = await admin.PutAsJsonAsync($"/api/systems/{Id(source)}/overview", new
        {
            displayName = "Changed", systemType = "Service", purpose = (string?)null,
            mainUsers = Array.Empty<string>(), repository = new { name = (string?)null, url = (string?)null },
            deployment = Array.Empty<object>(), mainProjects = Array.Empty<string>(), mainEntryPoints = Array.Empty<string>(),
            notes = (string?)null, actor = Actor(), concurrencyToken = Token(source),
        });
        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        using (var stale = await Delete(admin, $"/api/systems/{Id(source)}", Token(source)))
        {
            Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
            Assert.Equal("conflict", (await stale.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
        }
        var fresh = await update.Content.ReadFromJsonAsync<JsonElement>();
        var function = await Created(admin, "/api/business-functions", new
        {
            systemId = Id(source), name = $"BLOCKER_{Guid.NewGuid():N}", functionType = "Query", rewriteStatus = "Unknown", actor = Actor(),
        });
        using (var blocked = await Delete(admin, $"/api/systems/{Id(source)}", fresh.GetProperty("concurrencyToken").GetString()!))
        {
            Assert.Equal(HttpStatusCode.UnprocessableEntity, blocked.StatusCode);
            var error = await blocked.Content.ReadFromJsonAsync<JsonElement>();
            Assert.Equal("business_rule_violation", error.GetProperty("code").GetString());
            var blockers = error.GetProperty("details").GetProperty("blockers").EnumerateArray().ToArray();
            Assert.InRange(blockers.Length, 1, 8);
            Assert.Contains(blockers, x => x.GetProperty("dependencyType").GetString() == "businessFunctions" && x.GetProperty("count").GetInt32() == 1);
        }
        await Deleted(admin, $"/api/business-functions/{Id(function)}", Token(function));
        await Deleted(admin, $"/api/systems/{Id(source)}", fresh.GetProperty("concurrencyToken").GetString()!);
        using var second = await Delete(admin, $"/api/systems/{Id(source)}", fresh.GetProperty("concurrencyToken").GetString()!);
        Assert.Equal(HttpStatusCode.NotFound, second.StatusCode);
    }

    [Fact]
    public async Task System_with_only_technology_tags_soft_deletes_and_preserves_tombstone_associations_and_current_projection()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var currentUser = await Read(admin, "/api/current-user");
        var system = await CreateSystem(admin, $"TAG_ONLY_{suffix}");
        var systemId = Id(system);
        await AddTechnologyTags(systemId, "Vue", ".NET", "SQLite", "Element Plus");

        await Deleted(admin, $"/api/systems/{systemId}", Token(system));

        using var detail = await admin.GetAsync($"/api/systems/{systemId}");
        Assert.Equal(HttpStatusCode.NotFound, detail.StatusCode);
        using var listResponse = await admin.GetAsync($"/api/systems?search=TAG_ONLY_{suffix}&page=1&pageSize=20");
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<JsonElement>();
        Assert.Equal(0, list.GetProperty("total").GetInt32());
        Assert.DoesNotContain(list.GetProperty("items").EnumerateArray(), item => item.GetProperty("id").GetInt64() == systemId);

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var tombstone = await db.Systems.IgnoreQueryFilters().Where(item => item.Id == systemId)
            .Select(item => new DeleteState(item.IsDeleted, item.DeletedAt, item.DeletedByUserId, item.DeletedByDisplayName, item.Version))
            .SingleAsync();
        Assert.True(tombstone.IsDeleted);
        Assert.NotNull(tombstone.DeletedAt);
        Assert.Equal(TimeSpan.Zero, tombstone.DeletedAt!.Value.Offset);
        Assert.Equal(currentUser.GetProperty("id").GetInt64(), tombstone.DeletedByUserId);
        Assert.Equal(currentUser.GetProperty("displayName").GetString(), tombstone.DeletedByDisplayName);
        Assert.Equal(2, tombstone.Version);
        Assert.Equal(4, await db.SystemTechnologyTags.CountAsync(item => item.SystemId == systemId));
    }

    [Fact]
    public async Task System_with_technology_tags_and_business_function_reports_only_business_function()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var system = await CreateSystem(admin, $"TAG_FUNCTION_{suffix}");
        var systemId = Id(system);
        await AddTechnologyTags(systemId, "Vue", "SQLite");
        await Created(admin, "/api/business-functions", new
        {
            systemId, name = $"fn_{suffix}", functionType = "Query", rewriteStatus = "Unknown", actor = Actor(),
        });

        await AssertBlockers($"/api/systems/{systemId}", Token(system), "businessFunctions");
    }

    [Fact]
    public async Task System_with_technology_tags_database_source_and_knowledge_relation_reports_only_true_dependencies()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var system = await CreateSystem(admin, $"TAG_MULTI_{suffix}");
        var systemId = Id(system);
        await AddTechnologyTags(systemId, "Vue", "SQLite");
        await Created(admin, "/api/database-sources", new
        {
            systemId, name = $"source_{suffix}", engine = "SQLite", isPrimary = false, actor = Actor(),
        });
        var document = await Created(admin, "/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle", title = $"Classification {suffix}", bodyMarkdown = "classification",
        });
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.KnowledgeRelations.Add(new KnowledgeRelation
            {
                SourceType = KnowledgeTargetType.System,
                SourceId = systemId,
                TargetType = KnowledgeTargetType.KnowledgeDocument,
                TargetId = Id(document),
                RelationType = RelationType.References,
                CreatedAt = now,
                CreatedByName = "test",
                UpdatedAt = now,
                KnowledgeStatus = KnowledgeStatus.Unknown,
                KnowledgeStatusChangedAt = now,
                KnowledgeStatusChangedByName = "test",
                KnowledgeStatusChangedByRole = "test",
                Version = 1,
            });
            await db.SaveChangesAsync();
        }

        await AssertBlockers($"/api/systems/{systemId}", Token(system), "databaseSources", "knowledgeRelations");
    }

    [Fact]
    public async Task Every_root_reports_its_complete_bounded_active_dependency_categories_without_cascade()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var system = await CreateSystem(admin, $"BLOCKER_ROOT_{suffix}");
        var systemId = Id(system);
        var function = await Created(admin, "/api/business-functions", new
        {
            systemId, name = $"fn_{suffix}", functionType = "Query", rewriteStatus = "Unknown", actor = Actor(),
        });
        var source = await Created(admin, "/api/database-sources", new
        {
            systemId, name = $"source_{suffix}", engine = "SQLite", isPrimary = false, actor = Actor(),
        });
        var databaseObject = await Created(admin, "/api/database-objects", new
        {
            databaseSourceId = Id(source), schemaName = "main", objectName = $"object_{suffix}", objectType = "Table",
            accessMode = "Read", primaryKeyColumns = Array.Empty<string>(), businessKeyColumns = Array.Empty<string>(), actor = Actor(),
        });
        var columnCreate = await Created(admin, $"/api/database-objects/{Id(databaseObject)}/columns", new
        {
            ordinalPosition = 1, columnName = $"column_{suffix}", dataType = "TEXT", nullable = false,
            actor = Actor(), concurrencyToken = Token(databaseObject),
        });
        var column = columnCreate.GetProperty("column");
        var rule = await Created(admin, "/api/business-rules", new
        {
            systemId, name = $"rule_{suffix}", description = "blocker", inputData = Array.Empty<object>(), actor = Actor(),
        });
        var integration = await Created(admin, "/api/integrations", new
        {
            name = $"integration_{suffix}", integrationType = "RabbitMq",
            sourceParty = new { systemId = (long?)systemId, displayName = "Source" },
            targetParty = new { systemId = (long?)null, displayName = "External" },
            flowDirection = "OneWay", endpoint = new { exchange = "b02", topic = $"blocker.{suffix}", queue = (string?)null }, actor = Actor(),
        });
        var document = await Created(admin, "/api/knowledge-documents", new
        {
            documentType = "KnowledgeArticle", title = $"Blocker document {suffix}", bodyMarkdown = "blocker",
        });

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var now = DateTimeOffset.UtcNow;
            db.SystemTechnologyTags.Add(new SystemTechnologyTag { SystemId = systemId, Technology = $"tech_{suffix}" });
            db.BusinessProcessSteps.Add(new BusinessProcessStep { BusinessFunctionId = Id(function), StepOrder = 1, Name = "step" });
            db.ColumnKnownValues.Add(new ColumnKnownValue
            {
                DatabaseColumnId = Id(column), ValueText = "A", Meaning = "Known", SortOrder = 1, CreatedAt = now, UpdatedAt = now,
            });
            db.IntegrationContractFields.Add(new IntegrationContractField
            {
                IntegrationId = Id(integration), Ordinal = 1, FieldName = "payload", IsRequired = true,
            });
            await db.Database.ExecuteSqlInterpolatedAsync(
                $"UPDATE integrations SET database_source_id = {Id(source)}, database_object_id = {Id(databaseObject)} WHERE id = {Id(integration)}");

            var controlledTargets = new[]
            {
                (KnowledgeTargetType.System, systemId),
                (KnowledgeTargetType.DatabaseSource, Id(source)),
                (KnowledgeTargetType.BusinessFunction, Id(function)),
                (KnowledgeTargetType.DatabaseObject, Id(databaseObject)),
                (KnowledgeTargetType.DatabaseColumn, Id(column)),
                (KnowledgeTargetType.BusinessRule, Id(rule)),
                (KnowledgeTargetType.Integration, Id(integration)),
            };
            foreach (var (type, id) in controlledTargets)
            {
                db.KnowledgeRelations.Add(new KnowledgeRelation
                {
                    SourceType = type, SourceId = id, TargetType = KnowledgeTargetType.KnowledgeDocument, TargetId = Id(document),
                    RelationType = RelationType.References, CreatedAt = now, CreatedByName = "test", UpdatedAt = now,
                    KnowledgeStatus = KnowledgeStatus.Unknown, KnowledgeStatusChangedAt = now,
                    KnowledgeStatusChangedByName = "test", KnowledgeStatusChangedByRole = "test", Version = 1,
                });
                var unknown = new UnknownItem
                {
                    ItemCode = $"B02-{type}-{suffix}", SystemId = systemId, Question = $"question {type}",
                    Priority = UnknownItemPriority.Medium, Status = UnknownItemStatus.Open,
                    CreatedAt = now, CreatedByName = "test", UpdatedAt = now, Version = 1,
                };
                unknown.Targets.Add(new UnknownItemTarget
                {
                    TargetType = type, TargetId = id, IsPrimary = true, DisplaySnapshot = type.ToString(),
                });
                unknown.KnowledgeUpdates.Add(new KnowledgeUpdate
                {
                    TargetType = type, TargetId = id, ChangeSummary = "pending", BeforeJson = "{}", AfterJson = "{}",
                    Status = KnowledgeUpdateStatus.Proposed, CreatedAt = now, UpdatedAt = now,
                });
                db.UnknownItems.Add(unknown);
            }
            await db.SaveChangesAsync();
        }

        await AssertBlockers($"/api/systems/{systemId}", Token(system),
            "businessFunctions", "databaseSources", "businessRules", "integrations", "unknownItems", "knowledgeRelations", "proposedKnowledgeUpdates");
        await AssertBlockers($"/api/database-sources/{Id(source)}", Token(source),
            "databaseObjects", "integrations", "knowledgeRelations", "unknownItems", "proposedKnowledgeUpdates");
        await AssertBlockers($"/api/business-functions/{Id(function)}", Token(function),
            "processSteps", "knowledgeRelations", "unknownItems", "proposedKnowledgeUpdates");
        await AssertBlockers($"/api/database-objects/{Id(databaseObject)}", columnCreate.GetProperty("parentConcurrencyToken").GetString()!,
            "databaseColumns", "integrations", "knowledgeRelations", "unknownItems", "proposedKnowledgeUpdates");
        await AssertBlockers($"/api/database-columns/{Id(column)}", Token(column),
            "knownValues", "knowledgeRelations", "unknownItems", "proposedKnowledgeUpdates");
        await AssertBlockers($"/api/business-rules/{Id(rule)}", Token(rule),
            "knowledgeRelations", "unknownItems", "proposedKnowledgeUpdates");
        await AssertBlockers($"/api/integrations/{Id(integration)}", Token(integration),
            "contractFields", "knowledgeRelations", "unknownItems", "proposedKnowledgeUpdates");
        await AssertBlockers($"/api/knowledge-documents/{Id(document)}", Token(document), "knowledgeRelations");

        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(7, await verifyDb.KnowledgeRelations.CountAsync(item => item.TargetId == Id(document)));
        Assert.False(await verifyDb.Systems.IgnoreQueryFilters().Where(item => item.Id == systemId).Select(item => item.IsDeleted).SingleAsync());
    }

    [Fact]
    public async Task Closed_unknown_applied_update_and_evidence_history_do_not_block_or_change_on_delete()
    {
        var system = await CreateSystem(admin, $"HISTORY_SYSTEM_{Guid.NewGuid():N}");
        var rule = await Created(admin, "/api/business-rules", new
        {
            systemId = Id(system), name = $"HISTORY_RULE_{Guid.NewGuid():N}", description = "history", inputData = Array.Empty<object>(), actor = Actor(),
        });
        long unknownId;
        long updateId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var now = DateTimeOffset.UtcNow;
            var unknown = new UnknownItem
            {
                ItemCode = $"B02-HISTORY-{Guid.NewGuid():N}", SystemId = Id(system), Question = "closed",
                Priority = UnknownItemPriority.Low, Status = UnknownItemStatus.Closed, ClosedAt = now,
                CreatedAt = now, CreatedByName = "historian", UpdatedAt = now, Version = 1,
            };
            unknown.Targets.Add(new UnknownItemTarget
            {
                TargetType = KnowledgeTargetType.BusinessRule, TargetId = Id(rule), IsPrimary = true, DisplaySnapshot = "rule",
            });
            var update = new KnowledgeUpdate
            {
                TargetType = KnowledgeTargetType.BusinessRule, TargetId = Id(rule), ChangeSummary = "applied",
                BeforeJson = "{}", AfterJson = "{}", Status = KnowledgeUpdateStatus.Applied,
                AppliedByName = "historian", AppliedByRole = "Editor", AppliedAt = now, CreatedAt = now, UpdatedAt = now,
            };
            unknown.KnowledgeUpdates.Add(update);
            db.UnknownItems.Add(unknown);
            foreach (var type in new[] { EvidenceType.CodeReference, EvidenceType.HumanConfirmation })
            {
                db.Evidence.Add(new Evidence
                {
                    EvidenceType = type, SubjectType = EvidenceSubjectType.BusinessRule, SubjectId = Id(rule),
                    SourceTitle = type.ToString(), SourceReference = "history", SupportReason = "preserved",
                    ProviderName = "historian", ProviderRole = "Editor", ProvidedAt = now,
                    CreatedAt = now, UpdatedAt = now, Version = 1,
                });
            }
            await db.SaveChangesAsync();
            unknownId = unknown.Id;
            updateId = update.Id;
        }

        await Deleted(admin, $"/api/business-rules/{Id(rule)}", Token(rule));
        await using var verifyScope = factory.Services.CreateAsyncScope();
        var verifyDb = verifyScope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        Assert.Equal(2, await verifyDb.Evidence.CountAsync(item => item.SubjectType == EvidenceSubjectType.BusinessRule && item.SubjectId == Id(rule)));
        Assert.Equal(UnknownItemStatus.Closed, await verifyDb.UnknownItems.Where(item => item.Id == unknownId).Select(item => item.Status).SingleAsync());
        Assert.Equal(KnowledgeUpdateStatus.Applied, await verifyDb.KnowledgeUpdates.Where(item => item.Id == updateId).Select(item => item.Status).SingleAsync());
    }

    [Fact]
    public async Task Deleted_targets_reject_route_mutations_and_body_references_without_resurrection()
    {
        var deleted = await CreateSystem(admin, $"POST_DELETE_{Guid.NewGuid():N}");
        var active = await CreateSystem(admin, $"POST_DELETE_ACTIVE_{Guid.NewGuid():N}");
        await Deleted(admin, $"/api/systems/{Id(deleted)}", Token(deleted));

        using var edit = await admin.PutAsJsonAsync($"/api/systems/{Id(deleted)}/overview", new
        {
            displayName = "resurrect", systemType = "Service", purpose = (string?)null,
            mainUsers = Array.Empty<string>(), repository = new { name = (string?)null, url = (string?)null },
            deployment = Array.Empty<object>(), mainProjects = Array.Empty<string>(), mainEntryPoints = Array.Empty<string>(),
            notes = (string?)null, actor = Actor(), concurrencyToken = Token(deleted),
        });
        Assert.Equal(HttpStatusCode.NotFound, edit.StatusCode);

        using var child = await admin.PostAsJsonAsync("/api/database-sources", new
        {
            systemId = Id(deleted), name = $"blocked_{Guid.NewGuid():N}", engine = "SQLite", isPrimary = false, actor = Actor(),
        });
        await AssertError(child, HttpStatusCode.UnprocessableEntity, "reference_invalid");

        using var evidence = await admin.PostAsJsonAsync("/api/evidence", new
        {
            evidenceType = "CodeReference", subject = new { type = "System", id = Id(deleted) },
            sourceTitle = "deleted target", sourceReference = "test", supportReason = "must reject",
            provider = new { displayName = "Tester", roleOrIdentity = "Editor", occurredAt = "2026-08-27T00:00:00Z" },
        });
        await AssertError(evidence, HttpStatusCode.UnprocessableEntity, "reference_invalid");

        using var confirmation = await admin.PostAsJsonAsync("/api/evidence/human-confirmations", new
        {
            subject = new { type = "System", id = Id(deleted) }, confirmationMethod = "InSystem",
            confirmedAt = "2026-08-27T00:00:00Z", confirmationStatement = "must reject",
            supportReason = "deleted target",
        });
        await AssertError(confirmation, HttpStatusCode.UnprocessableEntity, "reference_invalid");

        using var status = await admin.PutAsJsonAsync("/api/knowledge-status", new
        {
            target = new { type = "System", id = Id(deleted) }, targetStatus = "Inferred",
            actor = new { displayName = "forged", roleOrIdentity = "Editor", occurredAt = "2026-08-27T00:00:00Z" },
            concurrencyToken = Token(deleted),
        });
        await AssertError(status, HttpStatusCode.UnprocessableEntity, "reference_invalid");

        using var relation = await admin.PostAsJsonAsync("/api/relationships", new
        {
            source = new { type = "System", id = Id(deleted) }, relationType = "DependsOn",
            target = new { type = "System", id = Id(active) }, actor = Actor(),
        });
        await AssertError(relation, HttpStatusCode.UnprocessableEntity, "reference_invalid");

        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var state = await db.Systems.IgnoreQueryFilters().Where(item => item.Id == Id(deleted))
            .Select(item => new { item.IsDeleted, item.DisplayName, item.Version }).SingleAsync();
        Assert.True(state.IsDeleted);
        Assert.NotEqual("resurrect", state.DisplayName);
        Assert.Equal(2, state.Version);
    }

    private async Task<long> CreateUser(AccessLevel accessLevel, string displayName)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        var now = DateTimeOffset.UtcNow;
        var user = new User { DisplayName = displayName, IsActive = true, AccessLevel = accessLevel, CreatedAt = now, UpdatedAt = now, Version = 1 };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    private static async Task<HttpResponseMessage> Delete(HttpClient client, string uri, string token)
    {
        var request = new HttpRequestMessage(HttpMethod.Delete, uri) { Content = JsonContent.Create(new { concurrencyToken = token }) };
        return await client.SendAsync(request);
    }

    private static async Task Deleted(HttpClient client, string uri, string token)
    {
        using var response = await Delete(client, uri, token);
        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
    }

    private async Task AssertBlockers(string uri, string token, params string[] expectedTypes)
    {
        using var response = await Delete(admin, uri, token);
        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        var error = await response.Content.ReadFromJsonAsync<JsonElement>();
        var blockers = error.GetProperty("details").GetProperty("blockers").EnumerateArray().ToArray();
        Assert.InRange(blockers.Length, 1, 8);
        Assert.Equal(expectedTypes, blockers.Select(item => item.GetProperty("dependencyType").GetString()).ToArray());
        Assert.DoesNotContain(blockers, item => item.GetProperty("dependencyType").GetString() == "technologyTags");
        Assert.DoesNotContain(blockers, item => item.GetProperty("displayName").GetString() == "技术标签");
        Assert.All(blockers, item => Assert.True(item.GetProperty("count").GetInt32() > 0));
    }

    private async Task AddTechnologyTags(long systemId, params string[] technologies)
    {
        await using var scope = factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        db.SystemTechnologyTags.AddRange(technologies.Select(technology => new SystemTechnologyTag
        {
            SystemId = systemId,
            Technology = technology,
        }));
        await db.SaveChangesAsync();
    }

    private static async Task AssertError(HttpResponseMessage response, HttpStatusCode statusCode, string code)
    {
        Assert.Equal(statusCode, response.StatusCode);
        Assert.Equal(code, (await response.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("code").GetString());
    }

    private static async Task<JsonElement> CreateSystem(HttpClient client, string name) => await Created(client, "/api/systems", new
    {
        name, displayName = name, systemType = "Service", lifecycle = "Running", purpose = "DELETE-B02", actor = Actor(),
    });

    private static async Task<JsonElement> Created(HttpClient client, string uri, object payload)
    {
        using var response = await client.PostAsJsonAsync(uri, payload);
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<JsonElement>()).Clone();
    }

    private static async Task<JsonElement> Read(HttpClient client, string uri) =>
        (await client.GetFromJsonAsync<JsonElement>(uri)).Clone();

    private static long Id(JsonElement value) => value.GetProperty("id").GetInt64();
    private static string Token(JsonElement value) => value.GetProperty("concurrencyToken").GetString()!;
    private static object Actor() => new { displayName = "Request Actor", role = "Editor" };
    private sealed record DeleteState(bool IsDeleted, DateTimeOffset? DeletedAt, long? DeletedByUserId, string? DeletedByDisplayName, long Version);
}
