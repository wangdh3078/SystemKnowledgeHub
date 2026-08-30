using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Tests.TestSupport;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class CanonicalDatabaseDiscoverySnapshotTests
{
    private static readonly DatabaseDiscoveryLimits Limits = new(128, 25_000, 250_000, 250_000, 10_000, 128 * 1024 * 1024);

    [Fact]
    public void Canonical_serialization_reorders_every_collection_recalculates_counts_and_hashes_deterministically()
    {
        var service = new CanonicalSnapshotService();
        var connection = Context("secret-one");
        var request = new DatabaseDiscoveryRequest(["APP_OWNER"], Limits);
        var source = CanonicalSnapshotFixtures.Create(connection, request, 1);
        var shuffled = source with
        {
            Schemas = source.Schemas.Reverse().ToArray(),
            Objects = source.Objects.Reverse().ToArray(),
            Columns = source.Columns.Reverse().ToArray(),
            PrimaryKeys = source.PrimaryKeys.Reverse().ToArray(),
            Capabilities =
            [
                new("ZCapability", DatabaseDiscoveryCapabilityState.Unavailable, "Fake"),
                new("ACapability", DatabaseDiscoveryCapabilityState.Supported, null),
            ],
            Counts = new(999, 999, 999, 999, 999, 999, 999, 999, 999),
        };
        var ordered = source with { Capabilities = shuffled.Capabilities.Reverse().ToArray() };

        var first = service.Prepare(shuffled, connection, Limits);
        var second = service.Prepare(ordered, connection, Limits);

        Assert.True(first.Succeeded);
        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(64, first.ContentSha256!.Length);
        Assert.Equal(2, first.Snapshot!.Counts.Objects);
        Assert.Equal(4, first.Snapshot.Counts.Columns);
        Assert.DoesNotContain("secret-one", first.CanonicalJson, StringComparison.Ordinal);
    }

    [Fact]
    public void Scope_fingerprint_ignores_secret_rotation_but_changes_for_principal_target_and_scope()
    {
        var service = new CanonicalSnapshotService();
        var request = new DatabaseDiscoveryRequest(["APP_OWNER"], Limits);
        var firstConnection = Context("first-secret");
        var samePrincipal = Context("rotated-secret");
        var differentPrincipal = Context("rotated-secret", username: "OTHER_READER");
        var snapshot = CanonicalSnapshotFixtures.Create(firstConnection, request, 1);

        var first = service.Prepare(snapshot, firstConnection, Limits);
        var rotated = service.Prepare(snapshot, samePrincipal, Limits);
        var changedPrincipal = service.Prepare(snapshot, differentPrincipal, Limits);
        var changedTarget = service.Prepare(snapshot with
        {
            DatabaseInfo = snapshot.DatabaseInfo with { TargetFingerprint = "different-target" },
        }, samePrincipal, Limits);

        Assert.Equal(first.ScopeFingerprint, rotated.ScopeFingerprint);
        Assert.NotEqual(first.ScopeFingerprint, changedPrincipal.ScopeFingerprint);
        Assert.NotEqual(first.ScopeFingerprint, changedTarget.ScopeFingerprint);
        Assert.DoesNotContain("first-secret", first.ScopeFingerprint!, StringComparison.Ordinal);
    }

    [Fact]
    public void Canonical_validation_rejects_unresolved_references_out_of_scope_schema_and_limits()
    {
        var service = new CanonicalSnapshotService();
        var connection = Context("secret");
        var request = new DatabaseDiscoveryRequest(["APP_OWNER"], Limits);
        var snapshot = CanonicalSnapshotFixtures.Create(connection, request, 1);
        var brokenForeignKey = snapshot with
        {
            ForeignKeys = snapshot.ForeignKeys.Select(item => item with
            {
                ReferencedObjectLogicalIdentity = "missing-object",
                ReferencedColumnLogicalIdentities = ["missing-column"],
            }).ToArray(),
        };
        var outOfScope = snapshot with
        {
            Schemas = [new("OTHER_OWNER", CanonicalSnapshotFixtures.Key("Schema", "OTHER_OWNER"))],
            DiscoveryScope = snapshot.DiscoveryScope with
            {
                IncludedSchemaLogicalIdentities = [CanonicalSnapshotFixtures.Key("Schema", "OTHER_OWNER")],
            },
        };
        var tinyLimits = Limits with { MaximumObjects = 1 };

        Assert.Equal("UnresolvedForeignKeyReference", service.Prepare(brokenForeignKey, connection, Limits).ErrorCode);
        Assert.Equal("MetadataQueryFailed", service.Prepare(outOfScope, connection, Limits).ErrorCode);
        Assert.Equal("LimitExceeded", service.Prepare(snapshot, connection, tinyLimits).ErrorCode);
    }

    [Fact]
    public void Canonical_validation_accepts_exact_fk_reference_closure_and_rejects_cross_object_stubs()
    {
        var service = new CanonicalSnapshotService();
        var connection = Context("secret");
        var request = new DatabaseDiscoveryRequest(["APP_OWNER"], Limits);
        var snapshot = CanonicalSnapshotFixtures.Create(connection, request, 1);
        var externalSchema = CanonicalSnapshotFixtures.Key("Schema", "REFERENCE_OWNER");
        var externalObject = CanonicalSnapshotFixtures.Key("Object", "REFERENCE_OWNER", "CUSTOMER_TYPES");
        var externalColumn = CanonicalSnapshotFixtures.Key("Column", externalObject, "ID");
        var foreignKey = snapshot.ForeignKeys.Single() with
        {
            ReferencedObjectLogicalIdentity = externalObject,
            ReferencedColumnLogicalIdentities = [externalColumn],
        };
        var closure = new CanonicalForeignKeyReferenceStub(
            externalSchema, "REFERENCE_OWNER", externalObject, "CUSTOMER_TYPES",
            externalColumn, "ID", true);
        var valid = snapshot with { ForeignKeys = [foreignKey], ForeignKeyReferenceClosure = [closure] };

        var prepared = service.Prepare(valid, connection, Limits);
        Assert.True(prepared.Succeeded);
        Assert.Equal(1, prepared.Snapshot!.Counts.ForeignKeyReferenceStubs);

        var otherObject = CanonicalSnapshotFixtures.Key("Object", "REFERENCE_OWNER", "OTHER_TABLE");
        var mismatched = valid with
        {
            ForeignKeyReferenceClosure =
            [
                closure with
                {
                    ObjectLogicalIdentity = otherObject,
                    ObjectName = "OTHER_TABLE",
                    ColumnLogicalIdentity = CanonicalSnapshotFixtures.Key("Column", otherObject, "ID"),
                },
            ],
        };
        Assert.Equal("UnresolvedForeignKeyReference", service.Prepare(mismatched, connection, Limits).ErrorCode);
    }

    [Fact]
    public void Diff_is_provider_neutral_durable_for_added_changed_missing_and_derives_unchanged()
    {
        var canonical = new CanonicalSnapshotService();
        var service = new DatabaseDiscoveryDiffService(canonical);
        var connection = Context("secret");
        var request = new DatabaseDiscoveryRequest(["APP_OWNER"], Limits);
        var baseline = CanonicalSnapshotFixtures.Create(connection, request, 1);
        var changed = CanonicalSnapshotFixtures.Create(connection, request, 2);
        var first = service.Compare(null, baseline);
        var next = service.Compare(baseline, changed);

        Assert.True(first.Succeeded);
        Assert.Null(first.Entries.SingleOrDefault(item => item.State == DatabaseDiscoveryDifferenceState.MissingFromSource));
        Assert.Equal(13, first.Counts.Added);
        Assert.Equal(0, first.Counts.Unchanged);
        Assert.Equal(1, next.Counts.Changed);
        Assert.Equal(DatabaseDiscoveryEntityKind.Column, Assert.Single(next.Entries).EntityKind);
        Assert.Equal(12, next.Counts.Unchanged);

        var orderObject = baseline.Objects.Single(item => item.Name == "ORDERS");
        var withoutOrders = baseline with
        {
            Objects = baseline.Objects.Where(item => item.LogicalIdentity != orderObject.LogicalIdentity).ToArray(),
            Columns = baseline.Columns.Where(item => item.ParentObjectLogicalIdentity != orderObject.LogicalIdentity).ToArray(),
            PrimaryKeys = baseline.PrimaryKeys.Where(item => item.ParentObjectLogicalIdentity != orderObject.LogicalIdentity).ToArray(),
            ForeignKeys = [],
            Indexes = [],
        };
        var missing = service.Compare(baseline, withoutOrders);
        Assert.True(missing.Counts.MissingFromSource > 0);
        Assert.DoesNotContain(missing.Entries, item => item.State == DatabaseDiscoveryDifferenceState.Added);
    }

    private static DatabaseDiscoveryConnectionContext Context(string password, string username = "METADATA_READER") => new(
        1, 1, 1, DatabaseProviderType.Oracle, "db.example.test", 1521, null, "APP_PDB",
        username, password, ["APP_OWNER"]);
}
