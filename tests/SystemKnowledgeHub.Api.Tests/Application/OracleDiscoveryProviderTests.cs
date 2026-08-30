using System.Reflection;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.Oracle;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class OracleDiscoveryProviderTests
{
    private static readonly DatabaseDiscoveryLimits Limits = new(128, 25_000, 250_000, 250_000, 10_000, 128 * 1024 * 1024);
    private static readonly DateTimeOffset CapturedAt = new(2026, 8, 30, 8, 0, 0, TimeSpan.Zero);

    [Fact]
    public async Task Oracle_core_catalog_maps_complete_provider_neutral_snapshot_and_capabilities()
    {
        var catalog = CompleteCatalog();
        var reader = new FakeOracleCatalogReader(catalog, Capabilities(catalog.Target));
        var provider = new OracleDiscoveryProvider(reader, new FixedTimeProvider(CapturedAt));
        var connection = Connection("ORACLE_SECRET_CANARY");
        var capabilities = await provider.DetectCapabilitiesAsync(connection, CancellationToken.None);

        var snapshot = await provider.DiscoverAsync(
            connection, new DatabaseDiscoveryRequest(["APP_OWNER"], Limits), capabilities, CancellationToken.None);
        var prepared = new CanonicalSnapshotService().Prepare(snapshot, connection, Limits);

        Assert.True(prepared.Succeeded, prepared.ErrorSummary);
        Assert.Equal(CapturedAt, prepared.Snapshot!.CapturedAt);
        Assert.Equal(3, prepared.Snapshot.Objects.Count);
        Assert.Equal(6, prepared.Snapshot.Columns.Count);
        Assert.Equal(2, prepared.Snapshot.PrimaryKeys.Count);
        Assert.Single(prepared.Snapshot.ForeignKeys);
        Assert.Single(prepared.Snapshot.UniqueConstraints);
        Assert.Equal(3, prepared.Snapshot.Indexes.Count);
        Assert.Single(prepared.Snapshot.Sequences);
        Assert.Single(prepared.Snapshot.ForeignKeyReferenceClosure);
        Assert.Equal("CaseSensitive", prepared.Snapshot.Columns.Single(item => item.Name == "CaseSensitive").Name);
        Assert.Null(prepared.Snapshot.Columns.Single(item => item.Name == "CaseSensitive").SourceOrdinal);
        Assert.Equal("VARCHAR2(100 CHAR)", prepared.Snapshot.Columns.Single(item => item.Name == "NAME").NativeDataType.Declaration);
        Assert.Equal(-2, prepared.Snapshot.Columns.Single(item => item.Name == "AMOUNT").NativeDataType.NumericScale.Value);
        Assert.Equal("UPPER(\"NAME\")", prepared.Snapshot.Indexes.Single(item => item.Name == "IX_CUSTOMERS_UPPER").KeyParts.Single().NativeExpression);
        Assert.Null(prepared.Snapshot.Sequences.Single().StartValue);
        Assert.Equal(DatabaseDiscoveryNativeTypeOrigin.ProviderImplicit, prepared.Snapshot.Sequences.Single().NativeDataType.Origin);
        Assert.Equal(9, prepared.Snapshot.Capabilities.Count);
        Assert.DoesNotContain("ORACLE_SECRET_CANARY", prepared.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("LAST_NUMBER", prepared.CanonicalJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Oracle_mapping_is_deterministic_for_catalog_row_order_and_preserves_constraint_order()
    {
        var firstCatalog = CompleteCatalog();
        var shuffled = firstCatalog with
        {
            Tables = firstCatalog.Tables.Reverse().ToArray(),
            Views = firstCatalog.Views.Reverse().ToArray(),
            Columns = firstCatalog.Columns.Reverse().ToArray(),
            Constraints = firstCatalog.Constraints.Reverse().ToArray(),
            ConstraintColumns = firstCatalog.ConstraintColumns.Reverse().ToArray(),
            Indexes = firstCatalog.Indexes.Reverse().ToArray(),
            IndexColumns = firstCatalog.IndexColumns.Reverse().ToArray(),
            IndexExpressions = firstCatalog.IndexExpressions.Reverse().ToArray(),
        };
        var connection = Connection("first-secret");
        var request = new DatabaseDiscoveryRequest(["APP_OWNER"], Limits);
        var canonical = new CanonicalSnapshotService();

        var firstProvider = new OracleDiscoveryProvider(
            new FakeOracleCatalogReader(firstCatalog, Capabilities(firstCatalog.Target)), new FixedTimeProvider(CapturedAt));
        var firstCapabilities = await firstProvider.DetectCapabilitiesAsync(connection, CancellationToken.None);
        var first = canonical.Prepare(
            await firstProvider.DiscoverAsync(connection, request, firstCapabilities, CancellationToken.None), connection, Limits);

        var secondProvider = new OracleDiscoveryProvider(
            new FakeOracleCatalogReader(shuffled, Capabilities(shuffled.Target)), new FixedTimeProvider(CapturedAt));
        var secondCapabilities = await secondProvider.DetectCapabilitiesAsync(connection, CancellationToken.None);
        var second = canonical.Prepare(
            await secondProvider.DiscoverAsync(connection, request, secondCapabilities, CancellationToken.None), connection, Limits);

        Assert.True(first.Succeeded);
        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal(first.ContentSha256, second.ContentSha256);
        var primaryKey = first.Snapshot!.PrimaryKeys.Single(item => item.Name == "PK_CUSTOMERS");
        Assert.EndsWith("2:ID", primaryKey.ColumnLogicalIdentities.Single(), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("18.0.0.0.0")]
    [InlineData("21.0.0.0.0")]
    [InlineData("not-a-version")]
    public async Task Oracle_provider_rejects_every_non_19_major_before_snapshot(string version)
    {
        var catalog = CompleteCatalog() with { Target = CompleteCatalog().Target with { ServerVersion = version } };
        var provider = new OracleDiscoveryProvider(
            new FakeOracleCatalogReader(catalog, Capabilities(catalog.Target)), new FixedTimeProvider(CapturedAt));

        var exception = await Assert.ThrowsAsync<DatabaseDiscoveryProviderException>(() =>
            provider.DiscoverAsync(
                Connection("secret"), new DatabaseDiscoveryRequest(["APP_OWNER"], Limits),
                new DatabaseProviderCapabilities([]), CancellationToken.None));

        Assert.Equal("UnsupportedDatabaseVersion", exception.ErrorCode);
        Assert.Equal("仅支持 Oracle Database 19c。", exception.SafeSummary);
    }

    [Fact]
    public async Task Oracle_provider_rejects_service_mismatch_and_cdb_root()
    {
        var catalog = CompleteCatalog();
        var mismatch = catalog with { Target = catalog.Target with { ServiceName = "OTHER_PDB" } };
        var root = catalog with { Target = catalog.Target with { ContainerName = "CDB$ROOT" } };

        var mismatchException = await DiscoverFailure(mismatch);
        var rootException = await DiscoverFailure(root);

        Assert.Equal("ConnectionFailed", mismatchException.ErrorCode);
        Assert.Equal("ConnectionFailed", rootException.ErrorCode);
        Assert.DoesNotContain("db.example.test", mismatchException.SafeSummary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Oracle_provider_fails_closed_for_unresolved_fk_limit_and_missing_schema_visibility()
    {
        var catalog = CompleteCatalog();
        var unresolved = catalog with
        {
            Constraints = catalog.Constraints.Where(item => item.Owner != "REF_OWNER").ToArray(),
            ConstraintColumns = catalog.ConstraintColumns.Where(item => item.Owner != "REF_OWNER").ToArray(),
        };
        var unresolvedException = await DiscoverFailure(unresolved);
        Assert.Equal("UnresolvedForeignKeyReference", unresolvedException.ErrorCode);

        var provider = Provider(catalog);
        var limitException = await Assert.ThrowsAsync<DatabaseDiscoveryProviderException>(() =>
            provider.DiscoverAsync(
                Connection("secret"),
                new DatabaseDiscoveryRequest(["APP_OWNER"], Limits with { MaximumObjects = 1 }),
                new DatabaseProviderCapabilities([]), CancellationToken.None));
        Assert.Equal("LimitExceeded", limitException.ErrorCode);

        var invisible = catalog with { VisibleSchemas = [] };
        var invisibleException = await DiscoverFailure(invisible);
        Assert.Equal("InsufficientPrivilege", invisibleException.ErrorCode);
    }

    [Fact]
    public void Oracle_query_inventory_is_closed_catalog_only_and_covers_every_required_group()
    {
        var sql = string.Join('\n', OracleCatalogSql.ReviewedQueryInventory);
        foreach (var required in new[]
        {
            "ALL_USERS", "ALL_TABLES", "ALL_VIEWS", "ALL_TAB_COLUMNS", "ALL_TAB_COMMENTS",
            "ALL_COL_COMMENTS", "ALL_CONSTRAINTS", "ALL_CONS_COLUMNS", "ALL_INDEXES",
            "ALL_IND_COLUMNS", "ALL_IND_EXPRESSIONS", "ALL_SEQUENCES",
        }) Assert.Contains(required, sql, StringComparison.Ordinal);

        foreach (var forbidden in new[]
        {
            "DBA_", "SYS.", "SYSTEM.", "DBMS_METADATA", "ALTER SESSION", "FOR UPDATE",
            " INSERT ", " UPDATE ", " DELETE ", " MERGE ", " CREATE ", " DROP ", "SELECT *",
        }) Assert.DoesNotContain(forbidden, $" {sql} ", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("LAST_NUMBER", OracleCatalogSql.Sequences, StringComparison.OrdinalIgnoreCase);
        Assert.All(
            OracleCatalogSql.ReviewedQueryInventory.Where(item => item.Contains("{0}", StringComparison.Ordinal)),
            query => Assert.Contains("{0}", query, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(1017, false, "AuthenticationFailed", "ORA-01017")]
    [InlineData(1031, true, "InsufficientPrivilege", "ORA-01031")]
    [InlineData(12170, false, "Timeout", "ORA-12170")]
    [InlineData(600, true, "MetadataQueryFailed", "ORA-00600")]
    public void Oracle_error_mapping_exposes_only_normalized_code_and_allowlisted_vendor_code(
        int number,
        bool connected,
        string code,
        string vendorCode)
    {
        Assert.Equal(vendorCode, OracleDiscoveryErrorMapper.AllowlistedVendorCode(number));
        Assert.Null(OracleDiscoveryErrorMapper.AllowlistedVendorCode(0));
        Assert.Null(OracleDiscoveryErrorMapper.AllowlistedVendorCode(100000));
        Assert.Equal(code, OracleDiscoveryErrorMapper.MapCode(number, connected));
    }

    [Fact]
    public void Oracle_sequence_contract_has_no_volatile_last_number_field()
    {
        Assert.DoesNotContain(typeof(OracleSequenceRow).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => property.Name.Contains("Last", StringComparison.OrdinalIgnoreCase));
    }

    private static OracleDiscoveryProvider Provider(OracleCatalogSnapshot catalog) => new(
        new FakeOracleCatalogReader(catalog, Capabilities(catalog.Target)), new FixedTimeProvider(CapturedAt));

    private static async Task<DatabaseDiscoveryProviderException> DiscoverFailure(OracleCatalogSnapshot catalog) =>
        await Assert.ThrowsAsync<DatabaseDiscoveryProviderException>(() => Provider(catalog).DiscoverAsync(
            Connection("secret"), new DatabaseDiscoveryRequest(["APP_OWNER"], Limits),
            new DatabaseProviderCapabilities([]), CancellationToken.None));

    private static DatabaseDiscoveryConnectionContext Connection(string password) => new(
        1, 1, 1, DatabaseProviderType.Oracle, "db.example.test", 1521, null, "APP_PDB",
        "METADATA_READER", password, ["APP_OWNER"]);

    private static OracleCapabilityProbe Capabilities(OracleTargetContext target) => new(target,
    [
        new("SupportsIdentityColumns", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsInvisibleColumns", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsMaterializedViews", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsPartitions", DatabaseDiscoveryCapabilityState.Unavailable, "ORA-01031"),
        new("SupportsSequences", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsSynonyms", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsTriggers", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsContainerDatabase", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsFullDdl", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
    ]);

    private static OracleCatalogSnapshot CompleteCatalog()
    {
        var target = new OracleTargetContext("19.22.0.0.0", "Oracle.ManagedDataAccess.Core/test", "APP_PDB", "APP_PDB", "APPDB");
        return new OracleCatalogSnapshot(
            target,
            "METADATA_READER",
            ["APP_OWNER"],
            [new("APP_OWNER", "CUSTOMERS"), new("APP_OWNER", "ORDERS")],
            [new("APP_OWNER", "V_CUSTOMERS")],
            [
                new("APP_OWNER", "CUSTOMERS", "ID", 1, "NUMBER", null, 22, 0, null, 19, 0, "N", null),
                new("APP_OWNER", "CUSTOMERS", "NAME", 2, "VARCHAR2", null, 400, 100, "C", null, null, "N", "'anonymous'"),
                new("APP_OWNER", "CUSTOMERS", "CaseSensitive", null, "APP_TYPE", "TYPE_OWNER", null, null, null, null, null, "Y", null),
                new("APP_OWNER", "ORDERS", "ID", 1, "NUMBER", null, 22, 0, null, 19, 0, "N", null),
                new("APP_OWNER", "ORDERS", "AMOUNT", 2, "NUMBER", null, 22, 0, null, 12, -2, "N", null),
                new("APP_OWNER", "V_CUSTOMERS", "ID", 1, "NUMBER", null, 22, 0, null, 19, 0, "Y", null),
            ],
            [
                new("APP_OWNER", "CUSTOMERS", "TABLE", "客户主数据"),
                new("APP_OWNER", "ORDERS", "TABLE", null),
                new("APP_OWNER", "V_CUSTOMERS", "VIEW", "客户视图"),
            ],
            [new("APP_OWNER", "CUSTOMERS", "NAME", "客户名称")],
            [
                new("APP_OWNER", "PK_CUSTOMERS", "P", "CUSTOMERS", null, null, null, "APP_OWNER", "PK_CUSTOMERS"),
                new("APP_OWNER", "UQ_CUSTOMERS_NAME", "U", "CUSTOMERS", null, null, null, "APP_OWNER", "UQ_CUSTOMERS_NAME"),
                new("APP_OWNER", "PK_ORDERS", "P", "ORDERS", null, null, null, "APP_OWNER", "PK_ORDERS"),
                new("APP_OWNER", "FK_ORDERS_TYPE", "R", "ORDERS", "REF_OWNER", "PK_TYPES", "NO ACTION", null, null),
                new("REF_OWNER", "PK_TYPES", "P", "TYPES", null, null, null, null, null),
            ],
            [
                new("APP_OWNER", "PK_CUSTOMERS", "CUSTOMERS", "ID", 1),
                new("APP_OWNER", "UQ_CUSTOMERS_NAME", "CUSTOMERS", "NAME", 1),
                new("APP_OWNER", "PK_ORDERS", "ORDERS", "ID", 1),
                new("APP_OWNER", "FK_ORDERS_TYPE", "ORDERS", "AMOUNT", 1),
                new("REF_OWNER", "PK_TYPES", "TYPES", "ID", 1),
            ],
            [
                new("APP_OWNER", "PK_CUSTOMERS", "APP_OWNER", "CUSTOMERS", "NORMAL", "UNIQUE"),
                new("APP_OWNER", "PK_ORDERS", "APP_OWNER", "ORDERS", "NORMAL", "UNIQUE"),
                new("APP_OWNER", "IX_CUSTOMERS_UPPER", "APP_OWNER", "CUSTOMERS", "FUNCTION-BASED NORMAL", "NONUNIQUE"),
            ],
            [
                new("APP_OWNER", "PK_CUSTOMERS", "APP_OWNER", "CUSTOMERS", "ID", 1, "ASC"),
                new("APP_OWNER", "PK_ORDERS", "APP_OWNER", "ORDERS", "ID", 1, "ASC"),
                new("APP_OWNER", "IX_CUSTOMERS_UPPER", "APP_OWNER", "CUSTOMERS", "SYS_NC00001$", 1, "ASC"),
            ],
            [new("APP_OWNER", "IX_CUSTOMERS_UPPER", "APP_OWNER", "CUSTOMERS", "UPPER(\"NAME\")", 1)],
            [new("APP_OWNER", "ORDER_SEQ", "1", "9999999999999999999999999999", "1", "N", "N", 20)]);
    }

    private sealed class FakeOracleCatalogReader(
        OracleCatalogSnapshot catalog,
        OracleCapabilityProbe capabilities) : IOracleDiscoveryCatalogReader
    {
        public Task<OracleCapabilityProbe> ReadCapabilitiesAsync(
            DatabaseDiscoveryConnectionContext connection,
            CancellationToken cancellationToken) => Task.FromResult(capabilities);

        public Task<OracleCatalogSnapshot> ReadCatalogAsync(
            DatabaseDiscoveryConnectionContext connection,
            DatabaseDiscoveryRequest request,
            CancellationToken cancellationToken) => Task.FromResult(catalog);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
