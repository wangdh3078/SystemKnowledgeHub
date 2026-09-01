using System.Reflection;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.SqlServer;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class SqlServerDiscoveryProviderTests
{
    private static readonly DatabaseDiscoveryLimits Limits = new(
        128, 25_000, 250_000, 250_000, 10_000, 128 * 1024 * 1024);
    private static readonly DateTimeOffset CapturedAt = new(2026, 9, 1, 1, 0, 0, TimeSpan.Zero);

    [Fact]
    public void SqlClient_catalog_reader_uses_connection_and_catalog_timeouts_distinctly()
    {
        var reader = new SqlClientSqlServerDiscoveryCatalogReader(Options.Create(new DatabaseDiscoveryOptions
        {
            ConnectionTimeoutSeconds = 17,
            CatalogCommandTimeoutSeconds = 91,
        }));

        Assert.Equal(17, reader.ConfiguredConnectionTimeoutSeconds);
        Assert.Equal(91, reader.ConfiguredCatalogCommandTimeoutSeconds);
    }

    [Fact]
    public async Task SQL_Server_core_catalog_maps_complete_provider_neutral_snapshot()
    {
        var catalog = CompleteCatalog();
        var provider = Provider(catalog);
        var connection = Connection("SQLSERVER_SECRET_CANARY");
        var capabilities = await provider.DetectCapabilitiesAsync(connection, CancellationToken.None);
        var prepared = new CanonicalSnapshotService().Prepare(
            await provider.DiscoverAsync(
                connection,
                new DatabaseDiscoveryRequest(["dbdisc_a", "dbdisc_b"], Limits),
                capabilities,
                CancellationToken.None),
            connection,
            Limits);

        Assert.True(prepared.Succeeded, prepared.ErrorSummary);
        var snapshot = prepared.Snapshot!;
        Assert.Equal(DatabaseProviderType.SqlServer, snapshot.ProviderType);
        Assert.Equal("SqlServer", snapshot.DatabaseInfo.Provider);
        Assert.Equal("SKH_DBDISC", snapshot.DatabaseInfo.CurrentDatabaseOrService);
        Assert.Equal(CapturedAt, snapshot.CapturedAt);
        Assert.Equal(5, snapshot.Objects.Count);
        Assert.Equal(17, snapshot.Columns.Count);
        Assert.Equal(3, snapshot.PrimaryKeys.Count);
        Assert.Equal(2, snapshot.ForeignKeys.Count);
        Assert.Single(snapshot.UniqueConstraints);
        Assert.Equal(5, snapshot.Indexes.Count);
        Assert.Single(snapshot.Sequences);
        Assert.Single(snapshot.ForeignKeyReferenceClosure);
        Assert.DoesNotContain(snapshot.Schemas, item => item.Name == "dbdisc_ref");

        Assert.Equal("Customer master", snapshot.Objects.Single(item => item.Name == "customers").DatabaseComment);
        Assert.Equal(DatabaseDiscoveryObjectType.View,
            snapshot.Objects.Single(item => item.Name == "customer_summary").ObjectType);
        Assert.Equal("MiXeDColumn", snapshot.Columns.Single(item => item.Name == "MiXeDColumn").Name);
        Assert.Equal("Stable customer code", snapshot.Columns.Single(item => item.Name == "code").DatabaseComment);
        var customersIdentity = snapshot.Objects.Single(item => item.Name == "customers").LogicalIdentity;
        Assert.True(snapshot.Columns.Single(item => item.Name == "id"
            && item.ParentObjectLogicalIdentity == customersIdentity).IsPrimaryKey);

        AssertType(snapshot, "id", "int", "int");
        AssertType(snapshot, "big_value", "bigint", "bigint");
        AssertType(snapshot, "amount", "decimal", "decimal(12,2)");
        AssertType(snapshot, "code", "varchar", "varchar(100)");
        AssertType(snapshot, "unicode_code", "nvarchar", "nvarchar(100)");
        AssertType(snapshot, "notes", "nvarchar", "nvarchar(max)");
        AssertType(snapshot, "active", "bit", "bit");
        AssertType(snapshot, "business_date", "date", "date");
        AssertType(snapshot, "created_at", "datetime2", "datetime2(7)");
        AssertType(snapshot, "created_with_offset", "datetimeoffset", "datetimeoffset(7)");
        AssertType(snapshot, "external_id", "uniqueidentifier", "uniqueidentifier");
        AssertType(snapshot, "payload", "varbinary", "varbinary(max)");
        var alias = snapshot.Columns.Single(item => item.Name == "alias_code").NativeDataType;
        Assert.Equal("customer_code_type", alias.Name);
        Assert.Equal("dbdisc_a", alias.Namespace);
        Assert.Equal("[dbdisc_a].[customer_code_type]", alias.Declaration);
        Assert.Equal(DatabaseDiscoveryMeasureKind.Exact,
            snapshot.Columns.Single(item => item.Name == "unicode_code").NativeDataType.Length.Kind);
        Assert.Equal(100, snapshot.Columns.Single(item => item.Name == "unicode_code").NativeDataType.Length.Value);

        var include = snapshot.Indexes.Single(item => item.Name == "ix_customers_amount_include");
        Assert.Single(include.KeyParts);
        Assert.Single(include.NonKeyParts);
        Assert.Equal(DatabaseDiscoveryNonKeyPartRole.Included, include.NonKeyParts[0].Role);
        Assert.Equal(DatabaseDiscoverySortDirection.Descending, include.KeyParts[0].SortDirection);
        var filtered = snapshot.Indexes.Single(item => item.Name == "ix_customers_active");
        Assert.Equal("([active]=(1))", filtered.NativePredicate);
        Assert.NotNull(snapshot.Indexes.Single(item => item.Name == "pk_customers").BackingConstraintLogicalIdentity);

        var closure = Assert.Single(snapshot.ForeignKeyReferenceClosure);
        Assert.Equal("dbdisc_ref", closure.SchemaName);
        Assert.Equal("parent_entity", closure.ObjectName);
        Assert.Equal("id", closure.ColumnName);
        Assert.Equal("NO ACTION", snapshot.ForeignKeys.Single(item => item.Name == "fk_orders_parent").UpdateRule);
        Assert.Equal("CASCADE", snapshot.ForeignKeys.Single(item => item.Name == "fk_orders_parent").DeleteRule);

        var sequence = Assert.Single(snapshot.Sequences);
        Assert.Equal("100", sequence.StartValue);
        Assert.Equal("5", sequence.IncrementValue);
        Assert.Equal("bigint", sequence.NativeDataType.Name);
        Assert.Null(sequence.IsOrdered);
        Assert.DoesNotContain("SQLSERVER_SECRET_CANARY", prepared.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("current_value", prepared.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Latin1_General_100_CS_AS_SC_UTF8",
            snapshot.DiscoveryScope.NormalizationOptions["databaseCollation"]);
        Assert.Equal(10, snapshot.Capabilities.Count);
    }

    [Fact]
    public async Task SQL_Server_mapping_is_deterministic_for_catalog_order_and_secret_rotation()
    {
        var firstCatalog = CompleteCatalog();
        var secondCatalog = firstCatalog with
        {
            Objects = firstCatalog.Objects.Reverse().ToArray(),
            Columns = firstCatalog.Columns.Reverse().ToArray(),
            Constraints = firstCatalog.Constraints.Reverse().ToArray(),
            IndexParts = firstCatalog.IndexParts.Reverse().ToArray(),
        };
        var canonical = new CanonicalSnapshotService();
        var request = new DatabaseDiscoveryRequest(["dbdisc_a", "dbdisc_b"], Limits);

        var firstConnection = Connection("first-secret");
        var firstProvider = Provider(firstCatalog);
        var firstCapabilities = await firstProvider.DetectCapabilitiesAsync(firstConnection, CancellationToken.None);
        var first = canonical.Prepare(
            await firstProvider.DiscoverAsync(firstConnection, request, firstCapabilities, CancellationToken.None),
            firstConnection,
            Limits);

        var secondConnection = Connection("rotated-secret");
        var secondProvider = Provider(secondCatalog);
        var secondCapabilities = await secondProvider.DetectCapabilitiesAsync(secondConnection, CancellationToken.None);
        var second = canonical.Prepare(
            await secondProvider.DiscoverAsync(secondConnection, request, secondCapabilities, CancellationToken.None),
            secondConnection,
            Limits);

        Assert.True(first.Succeeded, first.ErrorSummary);
        Assert.True(second.Succeeded, second.ErrorSummary);
        Assert.Equal(first.CanonicalJson, second.CanonicalJson);
        Assert.Equal(first.ContentSha256, second.ContentSha256);
        Assert.Equal(first.ScopeFingerprint, second.ScopeFingerprint);
    }

    [Fact]
    public async Task SQL_Server_identifier_identity_is_ordinal_and_case_only_change_is_not_rename()
    {
        var canonical = new CanonicalSnapshotService();
        var request = new DatabaseDiscoveryRequest(["dbdisc_a"], Limits);
        var connection = Connection("secret", ["dbdisc_a"]);
        var beforeProvider = Provider(QuotedCatalog("CaseSensitiveTable", "MiXeDColumn"));
        var afterProvider = Provider(QuotedCatalog("casesensitivetable", "mixedcolumn"));
        var before = canonical.Prepare(
            await beforeProvider.DiscoverAsync(
                connection, request,
                await beforeProvider.DetectCapabilitiesAsync(connection, CancellationToken.None),
                CancellationToken.None),
            connection, Limits);
        var after = canonical.Prepare(
            await afterProvider.DiscoverAsync(
                connection, request,
                await afterProvider.DetectCapabilitiesAsync(connection, CancellationToken.None),
                CancellationToken.None),
            connection, Limits);

        var difference = new DatabaseDiscoveryDiffService(canonical).Compare(before.Snapshot!, after.Snapshot!);
        Assert.True(difference.Succeeded, difference.ErrorSummary);
        Assert.Equal(2, difference.Counts.Added);
        Assert.Equal(2, difference.Counts.MissingFromSource);
        Assert.Equal(1, difference.Counts.Unchanged);
    }

    [Fact]
    public async Task SQL_Server_fails_closed_for_specialized_index_unresolved_reference_collision_and_limit()
    {
        var catalog = CompleteCatalog();
        var specialized = catalog with
        {
            IndexParts = catalog.IndexParts.Select((item, index) => index == 0
                ? item with { IndexType = 5, IndexTypeDescription = "CLUSTERED COLUMNSTORE" }
                : item).ToArray(),
        };
        Assert.Equal("UnsupportedIndexFamily", (await DiscoverFailure(specialized)).ErrorCode);

        var unresolved = catalog with
        {
            Constraints = catalog.Constraints.Select(item => item.Name == "fk_orders_parent"
                ? item with { ReferencedColumnName = null }
                : item).ToArray(),
        };
        Assert.Equal("UnresolvedForeignKeyReference", (await DiscoverFailure(unresolved)).ErrorCode);

        var collision = catalog with { VisibleSchemas = ["dbdisc_a", "dbdisc_a"] };
        Assert.Equal("UnsupportedIdentifierCollision", (await DiscoverFailure(collision)).ErrorCode);

        var provider = Provider(catalog);
        var connection = Connection("secret");
        var capabilities = await provider.DetectCapabilitiesAsync(connection, CancellationToken.None);
        var limited = await Assert.ThrowsAsync<DatabaseDiscoveryProviderException>(() => provider.DiscoverAsync(
            connection,
            new DatabaseDiscoveryRequest(["dbdisc_a", "dbdisc_b"], Limits with { MaximumObjects = 1 }),
            capabilities,
            CancellationToken.None));
        Assert.Equal("LimitExceeded", limited.ErrorCode);
    }

    [Theory]
    [InlineData(15)]
    [InlineData(17)]
    public async Task SQL_Server_provider_rejects_non_16_major(int major)
    {
        var catalog = CompleteCatalog();
        var provider = Provider(catalog with
        {
            Target = catalog.Target with
            {
                ServerMajorVersion = major,
                ServerVersion = $"{major}.0.1000.1",
            },
        });

        var exception = await Assert.ThrowsAsync<DatabaseDiscoveryProviderException>(() =>
            provider.DetectCapabilitiesAsync(Connection("secret"), CancellationToken.None));
        Assert.Equal("UnsupportedDatabaseVersion", exception.ErrorCode);
    }

    [Fact]
    public void SQL_Server_query_inventory_is_parameterized_closed_catalog_only_and_sequence_stable()
    {
        var sql = string.Join('\n', SqlServerCatalogSql.ReviewedQueryInventory);
        foreach (var required in new[]
        {
            "sys.schemas", "sys.tables", "sys.views", "sys.columns", "sys.types",
            "sys.default_constraints", "sys.key_constraints", "sys.foreign_keys",
            "sys.foreign_key_columns", "sys.indexes", "sys.index_columns", "sys.sequences",
            "sys.extended_properties",
        }) Assert.Contains(required, sql, StringComparison.OrdinalIgnoreCase);
        foreach (var forbidden in new[]
        {
            " INSERT ", " UPDATE ", " DELETE ", " MERGE ", " CREATE ", " ALTER ",
            " DROP ", " TRUNCATE ", " GRANT ", " REVOKE ", "FOR UPDATE", "SELECT *",
            "current_value", "OBJECT_DEFINITION", "sp_helptext",
        }) Assert.DoesNotContain(forbidden, $" {sql} ", StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbdisc_a", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbdisc_b", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbdisc_ref", sql, StringComparison.OrdinalIgnoreCase);
        Assert.All(
            new[]
            {
                SqlServerCatalogSql.Schemas, SqlServerCatalogSql.Objects, SqlServerCatalogSql.Columns,
                SqlServerCatalogSql.Constraints, SqlServerCatalogSql.IndexParts, SqlServerCatalogSql.Sequences,
            },
            query => Assert.Contains("/*SCHEMA_FILTER*/", query, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(18456, false, "AuthenticationFailed", "MSSQL-18456")]
    [InlineData(229, true, "InsufficientPrivilege", "MSSQL-229")]
    [InlineData(-2, true, "Timeout", null)]
    [InlineData(53, false, "ConnectionFailed", "MSSQL-53")]
    [InlineData(50000, true, "MetadataQueryFailed", "MSSQL-50000")]
    public void SQL_Server_error_mapping_is_normalized_and_vendor_number_is_strict(
        int number,
        bool connected,
        string expectedCode,
        string? vendorCode)
    {
        Assert.Equal(expectedCode, SqlServerDiscoveryErrorMapper.MapCode(number, connected));
        Assert.Equal(vendorCode, SqlServerDiscoveryErrorMapper.AllowlistedVendorCode(number));
        Assert.Null(SqlServerDiscoveryErrorMapper.AllowlistedVendorCode(0));
        Assert.Null(SqlServerDiscoveryErrorMapper.AllowlistedVendorCode(1_000_000));
        Assert.Equal("MSSQL-18456", DatabaseDiscoveryFailureSafety.SafeVendorCode("MSSQL-18456"));
        Assert.Null(DatabaseDiscoveryFailureSafety.SafeVendorCode("MSSQL-18456 password"));
    }

    [Fact]
    public void SQL_Server_sequence_contract_has_no_runtime_value_field()
    {
        Assert.DoesNotContain(
            typeof(SqlServerSequenceRow).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => property.Name.Contains("Current", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Last", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain("current_value", SqlServerCatalogSql.Sequences, StringComparison.OrdinalIgnoreCase);
    }

    private static void AssertType(
        CanonicalDatabaseDiscoverySnapshot snapshot,
        string columnName,
        string typeName,
        string declaration)
    {
        var type = snapshot.Columns.First(item => item.Name == columnName).NativeDataType;
        Assert.Equal(typeName, type.Name);
        Assert.Equal(declaration, type.Declaration);
    }

    private static SqlServerDiscoveryProvider Provider(SqlServerCatalogSnapshot catalog) => new(
        new FakeCatalogReader(catalog, Capabilities(catalog.Target)),
        new FixedTimeProvider(CapturedAt));

    private static async Task<DatabaseDiscoveryProviderException> DiscoverFailure(SqlServerCatalogSnapshot catalog)
    {
        var provider = Provider(catalog);
        var connection = Connection("secret");
        var capabilities = await provider.DetectCapabilitiesAsync(connection, CancellationToken.None);
        return await Assert.ThrowsAsync<DatabaseDiscoveryProviderException>(() => provider.DiscoverAsync(
            connection,
            new DatabaseDiscoveryRequest(["dbdisc_a", "dbdisc_b"], Limits),
            capabilities,
            CancellationToken.None));
    }

    private static DatabaseDiscoveryConnectionContext Connection(
        string password,
        IReadOnlyList<string>? schemas = null) => new(
        1, 1, 1, DatabaseProviderType.SqlServer, "db.example.test", 1433,
        "SKH_DBDISC", null, "metadata_reader", password,
        schemas ?? ["dbdisc_a", "dbdisc_b"]);

    private static SqlServerCapabilityProbe Capabilities(SqlServerTargetContext target) => new(target,
    [
        new("SupportsIdentityColumns", DatabaseDiscoveryCapabilityState.NotSupported, "CoreColumnContractDoesNotProjectIdentity"),
        new("SupportsComputedColumns", DatabaseDiscoveryCapabilityState.NotSupported, "CoreColumnContractDoesNotProjectComputedExpression"),
        new("SupportsInvisibleColumns", DatabaseDiscoveryCapabilityState.NotSupported, "SqlServer2022NotSupported"),
        new("SupportsMaterializedViews", DatabaseDiscoveryCapabilityState.NotApplicable, "SqlServerIndexedViewsOutsideCore"),
        new("SupportsPartitions", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
        new("SupportsSequences", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsSynonyms", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
        new("SupportsTriggers", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
        new("SupportsContainerDatabase", DatabaseDiscoveryCapabilityState.NotApplicable, "SqlServerNotApplicable"),
        new("SupportsFullDdl", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
    ]);

    private static SqlServerCatalogSnapshot CompleteCatalog()
    {
        var target = Target();
        var columns = new List<SqlServerColumnRow>
        {
            Col("customers", "id", 1, "int", 4, 10, 0, nullable: false),
            Col("customers", "big_value", 2, "bigint", 8, 19, 0),
            Col("customers", "amount", 3, "decimal", 9, 12, 2),
            Col("customers", "code", 4, "varchar", 100, 0, 0, comment: "Stable customer code"),
            Col("customers", "unicode_code", 5, "nvarchar", 200, 0, 0),
            Col("customers", "notes", 6, "nvarchar", -1, 0, 0),
            Col("customers", "active", 7, "bit", 1, 1, 0, nullable: false, defaultExpression: "((1))"),
            Col("customers", "business_date", 8, "date", 3, 0, 0),
            Col("customers", "created_at", 9, "datetime2", 8, 0, 7),
            Col("customers", "created_with_offset", 10, "datetimeoffset", 10, 0, 7),
            Col("customers", "external_id", 11, "uniqueidentifier", 16, 0, 0),
            Col("customers", "payload", 12, "varbinary", -1, 0, 0),
            Col("customers", "alias_code", 13, "varchar", 40, 0, 0,
                typeName: "customer_code_type", typeNamespace: "dbdisc_a", isUserDefined: true),
            Col("orders", "id", 1, "int", 4, 10, 0, nullable: false),
            Col("orders", "customer_id", 2, "int", 4, 10, 0, nullable: false),
            Col("CaseSensitiveTable", "MiXeDColumn", 1, "int", 4, 10, 0, nullable: false),
            Col("rename_before", "id", 1, "int", 4, 10, 0, nullable: false, schema: "dbdisc_b"),
        };
        IReadOnlyList<SqlServerConstraintColumnRow> constraints =
        [
            Constraint("customers", "pk_customers", "PK", 1, "id"),
            Constraint("customers", "uq_customers_code", "UQ", 1, "code"),
            Constraint("orders", "pk_orders", "PK", 1, "id"),
            Constraint("orders", "fk_orders_customer", "FK", 1, "customer_id",
                "dbdisc_a", "customers", "id", "NO_ACTION", "NO_ACTION"),
            Constraint("orders", "fk_orders_parent", "FK", 1, "id",
                "dbdisc_ref", "parent_entity", "id", "NO_ACTION", "CASCADE"),
            Constraint("CaseSensitiveTable", "PK_CaseSensitiveTable", "PK", 1, "MiXeDColumn"),
        ];
        IReadOnlyList<SqlServerIndexPartRow> indexes =
        [
            Index("customers", "pk_customers", 1, "CLUSTERED", true, 1, 1, false, 0, false, "id", backing: "pk_customers"),
            Index("customers", "ux_customers_code", 2, "NONCLUSTERED", true, 1, 1, false, 0, false, "code"),
            Index("customers", "ix_customers_active", 2, "NONCLUSTERED", false, 1, 1, false, 0, false, "active", "([active]=(1))"),
            Index("customers", "ix_customers_amount_include", 2, "NONCLUSTERED", false, 1, 1, false, 0, true, "amount"),
            Index("customers", "ix_customers_amount_include", 2, "NONCLUSTERED", false, 2, 0, true, 0, false, "notes"),
            Index("orders", "pk_orders", 1, "CLUSTERED", true, 1, 1, false, 0, false, "id", backing: "pk_orders"),
        ];
        return new SqlServerCatalogSnapshot(
            target,
            "metadata_reader",
            ["dbdisc_a", "dbdisc_b"],
            [
                new("dbdisc_a", "customers", DatabaseDiscoveryObjectType.Table, "Customer master"),
                new("dbdisc_a", "orders", DatabaseDiscoveryObjectType.Table, null),
                new("dbdisc_a", "CaseSensitiveTable", DatabaseDiscoveryObjectType.Table, null),
                new("dbdisc_a", "customer_summary", DatabaseDiscoveryObjectType.View, "Customer view"),
                new("dbdisc_b", "rename_before", DatabaseDiscoveryObjectType.Table, null),
            ],
            columns,
            constraints,
            indexes,
            [new("dbdisc_a", "manual_sequence", "bigint", "sys", false, false, "bigint", 19, 0,
                "100", "5", "100", "999999", 7, false)]);
    }

    private static SqlServerCatalogSnapshot QuotedCatalog(string objectName, string columnName) => new(
        Target(),
        "metadata_reader",
        ["dbdisc_a"],
        [new("dbdisc_a", objectName, DatabaseDiscoveryObjectType.Table, null)],
        [Col(objectName, columnName, 1, "int", 4, 10, 0, nullable: false)],
        [],
        [],
        []);

    private static SqlServerTargetContext Target() => new(
        "16.0.4215.2", 16, "Microsoft.Data.SqlClient/7.0.2.0", "SKH_DBDISC",
        "Latin1_General_100_CS_AS_SC_UTF8", "metadata_reader");

    private static SqlServerColumnRow Col(
        string objectName,
        string name,
        int ordinal,
        string baseType,
        int maximumLength,
        int precision,
        int scale,
        bool nullable = true,
        string? defaultExpression = null,
        string? comment = null,
        string schema = "dbdisc_a",
        string? typeName = null,
        string typeNamespace = "sys",
        bool isUserDefined = false) => new(
        schema, objectName, name, ordinal, typeName ?? baseType, typeNamespace,
        isUserDefined, false, baseType, maximumLength, precision, scale,
        nullable, defaultExpression, comment);

    private static SqlServerConstraintColumnRow Constraint(
        string objectName,
        string name,
        string type,
        int position,
        string column,
        string? referencedSchema = null,
        string? referencedObject = null,
        string? referencedColumn = null,
        string? updateAction = null,
        string? deleteAction = null) => new(
        "dbdisc_a", objectName, name, type, position, column,
        referencedSchema, referencedObject, referencedColumn, updateAction, deleteAction);

    private static SqlServerIndexPartRow Index(
        string objectName,
        string name,
        int type,
        string typeDescription,
        bool unique,
        int position,
        int keyOrdinal,
        bool included,
        int partitionOrdinal,
        bool descending,
        string column,
        string? predicate = null,
        string? backing = null) => new(
        "dbdisc_a", objectName, name, type, typeDescription, unique,
        position, keyOrdinal, included, partitionOrdinal, descending,
        column, predicate, backing, false);

    private sealed class FakeCatalogReader(
        SqlServerCatalogSnapshot catalog,
        SqlServerCapabilityProbe capabilities) : ISqlServerDiscoveryCatalogReader
    {
        public Task<SqlServerCapabilityProbe> ReadCapabilitiesAsync(
            DatabaseDiscoveryConnectionContext connection,
            CancellationToken cancellationToken) => Task.FromResult(capabilities);

        public Task<SqlServerCatalogSnapshot> ReadCatalogAsync(
            DatabaseDiscoveryConnectionContext connection,
            DatabaseDiscoveryRequest request,
            CancellationToken cancellationToken) => Task.FromResult(catalog);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
