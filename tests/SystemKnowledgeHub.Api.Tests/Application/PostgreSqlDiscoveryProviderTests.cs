using System.Reflection;
using Microsoft.Extensions.Options;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.PostgreSql;

namespace SystemKnowledgeHub.Api.Tests.Application;

public sealed class PostgreSqlDiscoveryProviderTests
{
    private static readonly DatabaseDiscoveryLimits Limits = new(
        128, 25_000, 250_000, 250_000, 10_000, 128 * 1024 * 1024);
    private static readonly DateTimeOffset CapturedAt = new(2026, 8, 30, 9, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Npgsql_catalog_reader_uses_connection_and_catalog_command_timeouts_distinctly()
    {
        var reader = new NpgsqlPostgreSqlDiscoveryCatalogReader(Options.Create(new DatabaseDiscoveryOptions
        {
            ConnectionTimeoutSeconds = 19,
            CatalogCommandTimeoutSeconds = 83,
        }));

        Assert.Equal(19, reader.ConfiguredConnectionTimeoutSeconds);
        Assert.Equal(83, reader.ConfiguredCatalogCommandTimeoutSeconds);
    }

    [Fact]
    public async Task PostgreSql_core_catalog_maps_complete_provider_neutral_snapshot()
    {
        var catalog = CompleteCatalog();
        var provider = Provider(catalog);
        var connection = Connection("POSTGRESQL_SECRET_CANARY");
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
        Assert.Equal(CapturedAt, snapshot.CapturedAt);
        Assert.Equal(DatabaseProviderType.PostgreSql, snapshot.ProviderType);
        Assert.Equal("PostgreSql", snapshot.DatabaseInfo.Provider);
        Assert.Equal("knowledge_hub", snapshot.DatabaseInfo.CurrentDatabaseOrService);
        Assert.Equal(5, snapshot.Objects.Count);
        Assert.Equal(18, snapshot.Columns.Count);
        Assert.Equal(3, snapshot.PrimaryKeys.Count);
        Assert.Equal(3, snapshot.ForeignKeys.Count);
        Assert.Equal(2, snapshot.UniqueConstraints.Count);
        Assert.Equal(6, snapshot.Indexes.Count);
        Assert.Single(snapshot.Sequences);
        var reference = Assert.Single(snapshot.ForeignKeyReferenceClosure);
        Assert.Equal("dbdisc_ref", reference.SchemaName);
        Assert.Equal("external_parent", reference.ObjectName);
        Assert.Equal("id", reference.ColumnName);
        Assert.True(reference.ReferenceOnly);
        Assert.DoesNotContain(snapshot.Schemas, item => item.Name == "dbdisc_ref");
        var externalForeignKey = snapshot.ForeignKeys.Single(item => item.Name == "fk_child_external");
        Assert.Equal("NO ACTION", externalForeignKey.UpdateRule);
        Assert.Equal("SET NULL", externalForeignKey.DeleteRule);

        var quotedObject = snapshot.Objects.Single(item => item.Name == "CaseSensitiveTable");
        var quotedColumn = snapshot.Columns.Single(item => item.Name == "MiXeDColumn");
        Assert.Equal("CaseSensitiveTable", quotedObject.Name);
        Assert.Equal(quotedObject.LogicalIdentity, quotedColumn.ParentObjectLogicalIdentity);
        Assert.NotEqual(
            snapshot.Objects.Single(item => item.Name == "departments").LogicalIdentity,
            quotedObject.LogicalIdentity);

        Assert.Equal(DatabaseDiscoveryObjectType.View,
            snapshot.Objects.Single(item => item.Name == "v_departments").ObjectType);
        Assert.Equal("Parent table", snapshot.Objects.Single(item => item.Name == "parent").DatabaseComment);
        AssertType(snapshot, "parent_key", "int4", "integer");
        AssertType(snapshot, "id", "int8", "bigint");
        AssertType(snapshot, "amount", "numeric", "numeric(12,2)");
        AssertType(snapshot, "description", "text", "text");
        AssertType(snapshot, "is_active", "bool", "boolean");
        AssertType(snapshot, "created_date", "date", "date");
        AssertType(snapshot, "created_at", "timestamp", "timestamp without time zone");
        AssertType(snapshot, "created_at_tz", "timestamptz", "timestamp with time zone");
        AssertType(snapshot, "entity_id", "uuid", "uuid");
        var code = snapshot.Columns.Single(item => item.Name == "code");
        Assert.Equal(DatabaseDiscoveryMeasureKind.Exact, code.NativeDataType.Length.Kind);
        Assert.Equal(100, code.NativeDataType.Length.Value);
        Assert.Equal(DatabaseDiscoveryLengthUnit.Characters, code.NativeDataType.Length.Unit);
        Assert.Equal("'unknown'::character varying", code.DefaultExpression);
        Assert.Equal("Parent code", code.DatabaseComment);
        Assert.True(snapshot.Columns.Single(item => item.Name == "parent_key").IsPrimaryKey);
        var amount = snapshot.Columns.Single(item => item.Name == "amount");
        Assert.Equal(new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, 12), amount.NativeDataType.NumericPrecision);
        Assert.Equal(new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, 2), amount.NativeDataType.NumericScale);
        Assert.Equal(DatabaseDiscoveryMeasureKind.Unbounded,
            snapshot.Columns.Single(item => item.Name == "description").NativeDataType.Length.Kind);

        var expression = snapshot.Indexes.Single(item => item.Name == "ix_child_description_lower");
        Assert.Equal("lower(description)", Assert.Single(expression.KeyParts).NativeExpression);
        var partialInclude = snapshot.Indexes.Single(item => item.Name == "ix_child_active_include");
        Assert.Single(partialInclude.KeyParts);
        var included = Assert.Single(partialInclude.NonKeyParts);
        Assert.Equal(DatabaseDiscoveryNonKeyPartRole.Included, included.Role);
        Assert.Equal("(is_active = true)", partialInclude.NativePredicate);
        Assert.NotNull(snapshot.Indexes.Single(item => item.Name == "parent_pkey").BackingConstraintLogicalIdentity);

        var sequence = Assert.Single(snapshot.Sequences);
        Assert.Equal("1", sequence.StartValue);
        Assert.Equal("1", sequence.IncrementValue);
        Assert.Equal("int8", sequence.NativeDataType.Name);
        Assert.Null(sequence.IsOrdered);
        Assert.DoesNotContain("POSTGRESQL_SECRET_CANARY", prepared.CanonicalJson, StringComparison.Ordinal);
        Assert.DoesNotContain("last_value", prepared.CanonicalJson, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(9, snapshot.Capabilities.Count);
        Assert.Equal(DatabaseDiscoveryCapabilityState.NotApplicable,
            snapshot.Capabilities.Single(item => item.Name == "SupportsContainerDatabase").State);
        Assert.Equal(DatabaseDiscoveryCapabilityState.NotSupported,
            snapshot.Capabilities.Single(item => item.Name == "SupportsInvisibleColumns").State);
    }

    [Fact]
    public async Task PostgreSql_mapping_is_deterministic_for_row_order_and_secret_rotation()
    {
        var firstCatalog = CompleteCatalog();
        var shuffled = firstCatalog with
        {
            Objects = firstCatalog.Objects.Reverse().ToArray(),
            Columns = firstCatalog.Columns.Reverse().ToArray(),
            Constraints = firstCatalog.Constraints.Reverse().ToArray(),
            IndexParts = firstCatalog.IndexParts.Reverse().ToArray(),
            Sequences = firstCatalog.Sequences.Reverse().ToArray(),
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
        var secondProvider = Provider(shuffled);
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
        Assert.Equal(
            ["parent_id", "department_id"],
            first.Snapshot!.UniqueConstraints.Single(item => item.Name == "uq_child_parent_department")
                .ColumnLogicalIdentities.Select(id => first.Snapshot.Columns.Single(column => column.LogicalIdentity == id).Name));
    }

    [Fact]
    public async Task PostgreSql_quoted_case_is_ordinal_and_case_only_rename_is_missing_plus_added()
    {
        var canonical = new CanonicalSnapshotService();
        var connection = Connection("secret", ["dbdisc_a"]);
        var request = new DatabaseDiscoveryRequest(["dbdisc_a"], Limits);

        var beforeProvider = Provider(QuotedCatalog("CaseSensitiveTable", "MiXeDColumn"));
        var beforeCapabilities = await beforeProvider.DetectCapabilitiesAsync(connection, CancellationToken.None);
        var before = canonical.Prepare(
            await beforeProvider.DiscoverAsync(connection, request, beforeCapabilities, CancellationToken.None),
            connection,
            Limits);

        var afterProvider = Provider(QuotedCatalog("casesensitivetable", "mixedcolumn"));
        var afterCapabilities = await afterProvider.DetectCapabilitiesAsync(connection, CancellationToken.None);
        var after = canonical.Prepare(
            await afterProvider.DiscoverAsync(connection, request, afterCapabilities, CancellationToken.None),
            connection,
            Limits);

        Assert.True(before.Succeeded, before.ErrorSummary);
        Assert.True(after.Succeeded, after.ErrorSummary);
        Assert.NotEqual(before.Snapshot!.Objects.Single().LogicalIdentity, after.Snapshot!.Objects.Single().LogicalIdentity);
        var difference = new DatabaseDiscoveryDiffService(canonical).Compare(before.Snapshot, after.Snapshot);
        Assert.True(difference.Succeeded, difference.ErrorSummary);
        Assert.Equal(2, difference.Counts.Added);
        Assert.Equal(0, difference.Counts.Changed);
        Assert.Equal(2, difference.Counts.MissingFromSource);
        Assert.Equal(1, difference.Counts.Unchanged);
    }

    [Theory]
    [InlineData(17)]
    [InlineData(19)]
    public async Task PostgreSql_provider_rejects_non_18_major_before_snapshot(int major)
    {
        var catalog = CompleteCatalog();
        var target = catalog.Target with { ServerMajorVersion = major, ServerVersion = $"{major}.1" };
        var provider = Provider(catalog with { Target = target });

        var exception = await Assert.ThrowsAsync<DatabaseDiscoveryProviderException>(() =>
            provider.DetectCapabilitiesAsync(Connection("secret"), CancellationToken.None));

        Assert.Equal("UnsupportedDatabaseVersion", exception.ErrorCode);
        Assert.Equal("仅支持 PostgreSQL 18。", exception.SafeSummary);
    }

    [Fact]
    public async Task PostgreSql_provider_fails_closed_for_visibility_limits_and_unresolved_fk()
    {
        var catalog = CompleteCatalog();
        var invisible = catalog with { VisibleSchemas = ["dbdisc_a"] };
        Assert.Equal("InsufficientPrivilege", (await DiscoverFailure(invisible)).ErrorCode);

        var provider = Provider(catalog);
        var capabilities = await provider.DetectCapabilitiesAsync(Connection("secret"), CancellationToken.None);
        var limit = await Assert.ThrowsAsync<DatabaseDiscoveryProviderException>(() => provider.DiscoverAsync(
            Connection("secret"),
            new DatabaseDiscoveryRequest(["dbdisc_a", "dbdisc_b"], Limits with { MaximumObjects = 1 }),
            capabilities,
            CancellationToken.None));
        Assert.Equal("LimitExceeded", limit.ErrorCode);

        var unresolvedRows = catalog.Constraints.Select(item =>
            item.ConstraintType == "f" && item.ReferencedSchemaName == "dbdisc_ref"
                ? item with { ReferencedColumnName = null }
                : item).ToArray();
        Assert.Equal("UnresolvedForeignKeyReference",
            (await DiscoverFailure(catalog with { Constraints = unresolvedRows })).ErrorCode);

        var duplicate = catalog with { Objects = [.. catalog.Objects, catalog.Objects[0]] };
        Assert.Equal("MetadataQueryFailed", (await DiscoverFailure(duplicate)).ErrorCode);
    }

    [Fact]
    public void PostgreSql_query_inventory_is_closed_parameterized_catalog_only_and_sequence_stable()
    {
        var sql = string.Join('\n', PostgreSqlCatalogSql.ReviewedQueryInventory);
        foreach (var required in new[]
        {
            "pg_catalog.pg_namespace", "pg_catalog.pg_class", "pg_catalog.pg_attribute",
            "pg_catalog.pg_type", "pg_catalog.pg_constraint", "pg_catalog.pg_index",
            "pg_catalog.pg_sequence",
        }) Assert.Contains(required, sql, StringComparison.Ordinal);

        foreach (var forbidden in new[]
        {
            " INSERT ", " UPDATE ", " DELETE ", " MERGE ", " CREATE ", " ALTER ",
            " DROP ", " TRUNCATE ", " GRANT ", " REVOKE ", " LOCK ", "FOR UPDATE",
            "SELECT *", "pg_get_viewdef", "pg_get_constraintdef", "pg_get_ruledef",
            "pg_get_triggerdef", "last_value", "is_called",
        }) Assert.DoesNotContain(forbidden, $" {sql} ", StringComparison.OrdinalIgnoreCase);

        Assert.All(
            PostgreSqlCatalogSql.ReviewedQueryInventory.Where(query => query.Contains("@schemas", StringComparison.Ordinal)),
            query => Assert.Contains("@schemas", query, StringComparison.Ordinal));
        Assert.DoesNotContain("dbdisc_a", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbdisc_b", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("dbdisc_ref", sql, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            PostgreSqlCatalogSql.ReviewedQueryInventory,
            query => query.Contains("pg_get_indexdef(index_rel.oid, 0", StringComparison.OrdinalIgnoreCase));
    }

    [Theory]
    [InlineData("28P01", false, "AuthenticationFailed", "SQLSTATE-28P01")]
    [InlineData("42501", true, "InsufficientPrivilege", "SQLSTATE-42501")]
    [InlineData("57014", true, "Timeout", "SQLSTATE-57014")]
    [InlineData("57P03", false, "ConnectionFailed", "SQLSTATE-57P03")]
    public void PostgreSql_error_mapping_exposes_only_normalized_code_and_allowlisted_sqlstate(
        string sqlState,
        bool connected,
        string code,
        string vendorCode)
    {
        Assert.Equal(code, PostgreSqlDiscoveryErrorMapper.MapCode(sqlState, connected));
        Assert.Equal(vendorCode, PostgreSqlDiscoveryErrorMapper.AllowlistedVendorCode(sqlState));
        Assert.Null(PostgreSqlDiscoveryErrorMapper.AllowlistedVendorCode("28P01 secret SELECT"));
        Assert.Null(PostgreSqlDiscoveryErrorMapper.AllowlistedVendorCode("abcde"));
    }

    [Fact]
    public void Discovery_failure_safety_accepts_normalized_sqlstate_and_rejects_raw_provider_values()
    {
        Assert.Equal("SQLSTATE-28P01", DatabaseDiscoveryFailureSafety.SafeVendorCode("SQLSTATE-28P01"));
        Assert.Equal("ORA-01017", DatabaseDiscoveryFailureSafety.SafeVendorCode("ORA-01017"));
        Assert.Null(DatabaseDiscoveryFailureSafety.SafeVendorCode("SQLSTATE-28P01 secret-password"));
        Assert.Null(DatabaseDiscoveryFailureSafety.SafeVendorCode("28P01"));
        Assert.Null(DatabaseDiscoveryFailureSafety.SafeVendorCode("SELECT"));
        Assert.Equal(
            "数据库用户名或密码验证失败。",
            DatabaseDiscoveryFailureSafety.SummaryFor("AuthenticationFailed"));
        Assert.Equal(
            "读取数据库结构元数据失败。",
            DatabaseDiscoveryFailureSafety.SummaryFor("POSTGRESQL_SECRET_CANARY"));
    }

    [Fact]
    public void PostgreSql_sequence_contract_has_no_runtime_value_field()
    {
        Assert.DoesNotContain(
            typeof(PostgreSqlSequenceRow).GetProperties(BindingFlags.Instance | BindingFlags.Public),
            property => property.Name.Contains("Last", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Current", StringComparison.OrdinalIgnoreCase)
                || property.Name.Contains("Called", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertType(
        CanonicalDatabaseDiscoverySnapshot snapshot,
        string columnName,
        string typeName,
        string declaration)
    {
        var column = snapshot.Columns.Single(item => item.Name == columnName);
        Assert.Equal(typeName, column.NativeDataType.Name);
        Assert.Equal("pg_catalog", column.NativeDataType.Namespace);
        Assert.Equal(declaration, column.NativeDataType.Declaration);
    }

    private static PostgreSqlDiscoveryProvider Provider(PostgreSqlCatalogSnapshot catalog) => new(
        new FakePostgreSqlCatalogReader(catalog, Capabilities(catalog.Target)),
        new FixedTimeProvider(CapturedAt));

    private static async Task<DatabaseDiscoveryProviderException> DiscoverFailure(PostgreSqlCatalogSnapshot catalog)
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
        1,
        1,
        1,
        DatabaseProviderType.PostgreSql,
        "db.example.test",
        5432,
        "knowledge_hub",
        null,
        "metadata_reader",
        password,
        schemas ?? ["dbdisc_a", "dbdisc_b"]);

    private static PostgreSqlCapabilityProbe Capabilities(PostgreSqlTargetContext target) => new(target,
    [
        new("SupportsIdentityColumns", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsInvisibleColumns", DatabaseDiscoveryCapabilityState.NotSupported, "PostgreSql18NotSupported"),
        new("SupportsMaterializedViews", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsPartitions", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsSequences", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsSynonyms", DatabaseDiscoveryCapabilityState.NotApplicable, "PostgreSqlNotApplicable"),
        new("SupportsTriggers", DatabaseDiscoveryCapabilityState.Supported, null),
        new("SupportsContainerDatabase", DatabaseDiscoveryCapabilityState.NotApplicable, "PostgreSqlNotApplicable"),
        new("SupportsFullDdl", DatabaseDiscoveryCapabilityState.NotSupported, "CoreScopeExcluded"),
    ]);

    private static PostgreSqlCatalogSnapshot QuotedCatalog(string objectName, string columnName)
    {
        var target = Target();
        return new PostgreSqlCatalogSnapshot(
            target,
            "metadata_reader",
            ["dbdisc_a"],
            [new("dbdisc_a", objectName, DatabaseDiscoveryObjectType.Table, null)],
            [Column("dbdisc_a", objectName, columnName, 1, "uuid", "uuid")],
            [],
            [],
            []);
    }

    private static PostgreSqlCatalogSnapshot CompleteCatalog()
    {
        var target = Target();
        return new PostgreSqlCatalogSnapshot(
            target,
            "metadata_reader",
            ["dbdisc_a", "dbdisc_b"],
            [
                new("dbdisc_a", "parent", DatabaseDiscoveryObjectType.Table, "Parent table"),
                new("dbdisc_a", "child", DatabaseDiscoveryObjectType.Table, "Child table"),
                new("dbdisc_b", "departments", DatabaseDiscoveryObjectType.Table, "Departments"),
                new("dbdisc_b", "v_departments", DatabaseDiscoveryObjectType.View, "Department view"),
                new("dbdisc_b", "CaseSensitiveTable", DatabaseDiscoveryObjectType.Table, null),
            ],
            [
                Column("dbdisc_a", "parent", "parent_key", 1, "int4", "integer", nullable: false),
                Column("dbdisc_a", "parent", "code", 2, "varchar", "character varying(100)", nullable: false,
                    characterLength: 100, defaultExpression: "'unknown'::character varying", comment: "Parent code"),
                Column("dbdisc_a", "child", "id", 1, "int8", "bigint", nullable: false),
                Column("dbdisc_a", "child", "parent_id", 2, "int4", "integer", nullable: false),
                Column("dbdisc_a", "child", "department_id", 3, "int4", "integer", nullable: false),
                Column("dbdisc_a", "child", "external_id", 4, "int4", "integer", nullable: false),
                Column("dbdisc_a", "child", "amount", 5, "numeric", "numeric(12,2)", nullable: false,
                    numericPrecision: 12, numericScale: 2),
                Column("dbdisc_a", "child", "description", 6, "text", "text", unboundedLength: true),
                Column("dbdisc_a", "child", "is_active", 7, "bool", "boolean", nullable: false,
                    defaultExpression: "true"),
                Column("dbdisc_a", "child", "created_date", 8, "date", "date"),
                Column("dbdisc_a", "child", "created_at", 9, "timestamp", "timestamp without time zone"),
                Column("dbdisc_a", "child", "created_at_tz", 10, "timestamptz", "timestamp with time zone"),
                Column("dbdisc_a", "child", "entity_id", 11, "uuid", "uuid"),
                Column("dbdisc_a", "child", "spare", 12, "int4", "integer"),
                Column("dbdisc_b", "departments", "department_key", 1, "int4", "integer", nullable: false),
                Column("dbdisc_b", "departments", "name", 2, "varchar", "character varying(80)", characterLength: 80),
                Column("dbdisc_b", "v_departments", "department_key", 1, "int4", "integer"),
                Column("dbdisc_b", "CaseSensitiveTable", "MiXeDColumn", 1, "uuid", "uuid"),
            ],
            [
                Constraint("dbdisc_a", "parent", "parent_pkey", "p", 1, "parent_key"),
                Constraint("dbdisc_a", "parent", "uq_parent_code", "u", 1, "code"),
                Constraint("dbdisc_a", "child", "child_pkey", "p", 1, "id"),
                Constraint("dbdisc_a", "child", "fk_child_parent", "f", 1, "parent_id",
                    "dbdisc_a", "parent", "parent_key", "a", "c"),
                Constraint("dbdisc_a", "child", "fk_child_department", "f", 1, "department_id",
                    "dbdisc_b", "departments", "department_key", "a", "r"),
                Constraint("dbdisc_a", "child", "fk_child_external", "f", 1, "external_id",
                    "dbdisc_ref", "external_parent", "id", "a", "n"),
                Constraint("dbdisc_a", "child", "uq_child_parent_department", "u", 1, "parent_id"),
                Constraint("dbdisc_a", "child", "uq_child_parent_department", "u", 2, "department_id"),
                Constraint("dbdisc_b", "departments", "departments_pkey", "p", 1, "department_key"),
            ],
            [
                Index("dbdisc_a", "parent", "parent_pkey", true, 1, 1, "parent_key", backing: "parent_pkey"),
                Index("dbdisc_a", "parent", "uq_parent_code", true, 1, 1, "code", backing: "uq_parent_code"),
                Index("dbdisc_a", "child", "ix_child_parent", false, 1, 1, "parent_id"),
                Index("dbdisc_a", "child", "ix_child_description_lower", false, 1, 1, null,
                    expression: "lower(description)"),
                Index("dbdisc_a", "child", "ix_child_active_include", false, 1, 1, "id",
                    predicate: "(is_active = true)"),
                Index("dbdisc_a", "child", "ix_child_active_include", false, 1, 2, "description",
                    predicate: "(is_active = true)"),
                Index("dbdisc_b", "departments", "departments_pkey", true, 1, 1, "department_key",
                    backing: "departments_pkey"),
            ],
            [
                new("dbdisc_a", "child_seq", "int8", "pg_catalog", "bigint", "1", "1", "1",
                    "9223372036854775807", 20, false),
            ]);
    }

    private static PostgreSqlTargetContext Target() => new(
        "18.1",
        18,
        "Npgsql/test",
        "knowledge_hub");

    private static PostgreSqlColumnRow Column(
        string schema,
        string objectName,
        string name,
        int ordinal,
        string typeName,
        string declaration,
        bool nullable = true,
        string? defaultExpression = null,
        string? comment = null,
        long? characterLength = null,
        bool unboundedLength = false,
        int? numericPrecision = null,
        int? numericScale = null) => new(
        schema,
        objectName,
        name,
        ordinal,
        typeName,
        "pg_catalog",
        declaration,
        nullable,
        defaultExpression,
        comment,
        typeName,
        "pg_catalog",
        characterLength,
        unboundedLength,
        numericPrecision,
        numericScale);

    private static PostgreSqlConstraintColumnRow Constraint(
        string schema,
        string objectName,
        string name,
        string type,
        int position,
        string columnName,
        string? referencedSchema = null,
        string? referencedObject = null,
        string? referencedColumn = null,
        string? updateAction = null,
        string? deleteAction = null) => new(
        schema,
        objectName,
        name,
        type,
        position,
        columnName,
        referencedSchema,
        referencedObject,
        referencedColumn,
        updateAction,
        deleteAction);

    private static PostgreSqlIndexPartRow Index(
        string schema,
        string objectName,
        string name,
        bool unique,
        int keyPartCount,
        int position,
        string? columnName,
        string? expression = null,
        string? predicate = null,
        string? backing = null) => new(
        schema,
        objectName,
        name,
        "btree",
        unique,
        keyPartCount,
        position,
        columnName,
        expression,
        false,
        predicate,
        backing,
        true,
        true);

    private sealed class FakePostgreSqlCatalogReader(
        PostgreSqlCatalogSnapshot catalog,
        PostgreSqlCapabilityProbe capabilities) : IPostgreSqlDiscoveryCatalogReader
    {
        public Task<PostgreSqlCapabilityProbe> ReadCapabilitiesAsync(
            DatabaseDiscoveryConnectionContext connection,
            CancellationToken cancellationToken) => Task.FromResult(capabilities);

        public Task<PostgreSqlCatalogSnapshot> ReadCatalogAsync(
            DatabaseDiscoveryConnectionContext connection,
            DatabaseDiscoveryRequest request,
            CancellationToken cancellationToken) => Task.FromResult(catalog);
    }

    private sealed class FixedTimeProvider(DateTimeOffset value) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => value;
    }
}
