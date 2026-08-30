using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.PostgreSql;

internal sealed class PostgreSqlDiscoveryProvider(
    IPostgreSqlDiscoveryCatalogReader catalogReader,
    TimeProvider timeProvider) : IDatabaseDiscoveryProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.PostgreSql;

    public async Task<DatabaseProviderCapabilities> DetectCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        var result = await catalogReader.ReadCapabilitiesAsync(connection, cancellationToken);
        PostgreSqlDiscoveryRules.ValidateTarget(connection, result.Target);
        return new DatabaseProviderCapabilities(result.Capabilities);
    }

    public async Task<CanonicalDatabaseDiscoverySnapshot> DiscoverAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        DatabaseProviderCapabilities capabilities,
        CancellationToken cancellationToken)
    {
        if (request.IncludedSchemas.Count is < 1
            || request.IncludedSchemas.Count > request.Limits.MaximumSchemas
            || !request.IncludedSchemas.SequenceEqual(connection.IncludedSchemas, StringComparer.Ordinal))
        {
            throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
        }

        var catalog = await catalogReader.ReadCatalogAsync(connection, request, cancellationToken);
        PostgreSqlDiscoveryRules.ValidateTarget(connection, catalog.Target);
        return BuildSnapshot(connection, catalog, request, capabilities.Capabilities);
    }

    private CanonicalDatabaseDiscoverySnapshot BuildSnapshot(
        DatabaseDiscoveryConnectionContext connection,
        PostgreSqlCatalogSnapshot catalog,
        DatabaseDiscoveryRequest request,
        IReadOnlyList<CanonicalCapability> capabilities)
    {
        ValidateBounds(catalog, request.Limits);

        var requestedSchemas = request.IncludedSchemas.ToHashSet(StringComparer.Ordinal);
        if (!catalog.VisibleSchemas.ToHashSet(StringComparer.Ordinal).SetEquals(requestedSchemas)
            || catalog.VisibleSchemas.Count != requestedSchemas.Count)
        {
            throw Failure("InsufficientPrivilege", "PostgreSQL 账号缺少必要的目录元数据权限。");
        }

        var schemas = request.IncludedSchemas
            .Select(name => new CanonicalSchema(name, PostgreSqlIdentity.Schema(name)))
            .ToArray();
        var schemaIds = schemas.ToDictionary(item => item.Name, item => item.LogicalIdentity, StringComparer.Ordinal);

        var objects = new List<CanonicalDatabaseObject>(catalog.Objects.Count);
        var objectIds = new Dictionary<(string SchemaName, string Name), string>();
        foreach (var row in catalog.Objects)
        {
            RequireSchema(row.SchemaName, requestedSchemas);
            var id = PostgreSqlIdentity.DatabaseObject(row.SchemaName, row.Name);
            if (!objectIds.TryAdd((row.SchemaName, row.Name), id))
                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            objects.Add(new CanonicalDatabaseObject(
                schemaIds[row.SchemaName], row.SchemaName, row.Name, row.ObjectType,
                row.Comment, id, null));
        }

        var columns = new List<CanonicalColumn>(catalog.Columns.Count);
        var columnIds = new Dictionary<(string SchemaName, string ObjectName, string Name), string>();
        foreach (var row in catalog.Columns)
        {
            if (!objectIds.TryGetValue((row.SchemaName, row.ObjectName), out var objectId))
                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            var id = PostgreSqlIdentity.Column(objectId, row.Name);
            if (!columnIds.TryAdd((row.SchemaName, row.ObjectName, row.Name), id))
                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            columns.Add(new CanonicalColumn(
                objectId,
                row.Name,
                row.SourceOrdinal,
                PostgreSqlNativeTypeMapper.Map(row),
                row.IsNullable,
                row.DefaultExpression,
                false,
                row.Comment,
                id));
        }

        var groupedConstraints = catalog.Constraints
            .GroupBy(item => (item.SchemaName, item.ObjectName, item.Name))
            .ToDictionary(
                group => group.Key,
                group => OrderedUnique(group, item => item.Position, "约束列"));
        var primaryKeys = new List<CanonicalPrimaryKey>();
        var foreignKeys = new List<CanonicalForeignKey>();
        var uniqueConstraints = new List<CanonicalUniqueConstraint>();
        var primaryColumnIds = new HashSet<string>(StringComparer.Ordinal);
        var constraintLogicalIds = new Dictionary<(string SchemaName, string ObjectName, string Name), string>();
        var closure = new Dictionary<string, CanonicalForeignKeyReferenceStub>(StringComparer.Ordinal);

        foreach (var (key, rows) in groupedConstraints)
        {
            if (!objectIds.TryGetValue((key.SchemaName, key.ObjectName), out var parentId))
                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            var first = rows[0];
            if (rows.Any(item => item.ConstraintType != first.ConstraintType))
                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            var ownedColumns = ResolveColumns(key.SchemaName, key.ObjectName, rows, columnIds);
            switch (first.ConstraintType)
            {
                case "p":
                {
                    var logicalId = PostgreSqlIdentity.Constraint("PK", parentId, key.Name);
                    primaryKeys.Add(new CanonicalPrimaryKey(key.Name, parentId, ownedColumns, logicalId));
                    constraintLogicalIds[key] = logicalId;
                    foreach (var id in ownedColumns) primaryColumnIds.Add(id);
                    break;
                }
                case "u":
                {
                    var logicalId = PostgreSqlIdentity.Constraint("UQ", parentId, key.Name);
                    uniqueConstraints.Add(new CanonicalUniqueConstraint(key.Name, parentId, ownedColumns, logicalId));
                    constraintLogicalIds[key] = logicalId;
                    break;
                }
                case "f":
                {
                    if (rows.Any(item => item.ReferencedSchemaName is null
                            || item.ReferencedObjectName is null
                            || item.ReferencedColumnName is null
                            || !string.Equals(item.ReferencedSchemaName, first.ReferencedSchemaName, StringComparison.Ordinal)
                            || !string.Equals(item.ReferencedObjectName, first.ReferencedObjectName, StringComparison.Ordinal)))
                    {
                        throw Failure("UnresolvedForeignKeyReference", "无法完整解析 PostgreSQL 外键引用。");
                    }

                    var referencedSchema = first.ReferencedSchemaName!;
                    var referencedObject = first.ReferencedObjectName!;
                    var referencedObjectId = PostgreSqlIdentity.DatabaseObject(referencedSchema, referencedObject);
                    IReadOnlyList<string> referencedColumns;
                    if (objectIds.ContainsKey((referencedSchema, referencedObject)))
                    {
                        referencedColumns = rows.Select(item =>
                        {
                            if (!columnIds.TryGetValue(
                                    (referencedSchema, referencedObject, item.ReferencedColumnName!), out var id))
                                throw Failure("UnresolvedForeignKeyReference", "无法完整解析 PostgreSQL 外键引用。");
                            return id;
                        }).ToArray();
                    }
                    else
                    {
                        referencedColumns = rows.Select(item =>
                        {
                            var columnName = item.ReferencedColumnName!;
                            var id = PostgreSqlIdentity.Column(referencedObjectId, columnName);
                            var stub = new CanonicalForeignKeyReferenceStub(
                                PostgreSqlIdentity.Schema(referencedSchema),
                                referencedSchema,
                                referencedObjectId,
                                referencedObject,
                                id,
                                columnName,
                                true);
                            if (closure.TryGetValue(id, out var existing) && existing != stub)
                                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
                            closure[id] = stub;
                            return id;
                        }).ToArray();
                    }

                    foreignKeys.Add(new CanonicalForeignKey(
                        key.Name,
                        parentId,
                        ownedColumns,
                        referencedObjectId,
                        referencedColumns,
                        PostgreSqlDiscoveryRules.MapReferentialAction(first.UpdateAction),
                        PostgreSqlDiscoveryRules.MapReferentialAction(first.DeleteAction),
                        PostgreSqlIdentity.Constraint("FK", parentId, key.Name)));
                    break;
                }
                default:
                    throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            }
        }

        columns = columns.Select(item => item with
        {
            IsPrimaryKey = primaryColumnIds.Contains(item.LogicalIdentity),
        }).ToList();

        var indexes = new List<CanonicalIndex>();
        foreach (var group in catalog.IndexParts.GroupBy(
                     item => (item.SchemaName, item.ObjectName, item.Name)))
        {
            var parts = OrderedUnique(group, item => item.Position, "索引列");
            var first = parts[0];
            if (!objectIds.TryGetValue((first.SchemaName, first.ObjectName), out var parentId)
                || first.KeyPartCount is < 1 || first.KeyPartCount > parts.Count
                || parts.Any(item => item.IndexKind != first.IndexKind
                    || item.IsUnique != first.IsUnique
                    || item.KeyPartCount != first.KeyPartCount
                    || item.NativePredicate != first.NativePredicate
                    || item.BackingConstraintName != first.BackingConstraintName
                    || !item.IsValid || !item.IsReady))
            {
                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            }

            var keyParts = parts.Take(first.KeyPartCount).Select(part =>
            {
                if (part.ColumnName is not null)
                {
                    if (part.NativeExpression is not null
                        || !columnIds.TryGetValue((part.SchemaName, part.ObjectName, part.ColumnName), out var columnId))
                        throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
                    return new CanonicalIndexKeyPart(
                        part.Position,
                        columnId,
                        null,
                        PostgreSqlDiscoveryRules.MapSort(first.IndexKind, part.IsDescending));
                }
                if (string.IsNullOrWhiteSpace(part.NativeExpression))
                    throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
                return new CanonicalIndexKeyPart(
                    part.Position,
                    null,
                    part.NativeExpression,
                    PostgreSqlDiscoveryRules.MapSort(first.IndexKind, part.IsDescending));
            }).ToArray();

            var nonKeyParts = parts.Skip(first.KeyPartCount).Select(part =>
            {
                if (part.ColumnName is null || part.NativeExpression is not null
                    || !columnIds.TryGetValue((part.SchemaName, part.ObjectName, part.ColumnName), out var columnId))
                    throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
                return new CanonicalIndexNonKeyPart(
                    part.Position, columnId, DatabaseDiscoveryNonKeyPartRole.Included);
            }).ToArray();

            string? backingConstraintId = null;
            if (first.BackingConstraintName is not null
                && !constraintLogicalIds.TryGetValue(
                    (first.SchemaName, first.ObjectName, first.BackingConstraintName), out backingConstraintId))
            {
                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            }

            indexes.Add(new CanonicalIndex(
                first.Name,
                parentId,
                first.IndexKind,
                first.IsUnique,
                keyParts,
                nonKeyParts,
                first.NativePredicate,
                backingConstraintId,
                PostgreSqlIdentity.Index(parentId, first.Name)));
        }

        var sequences = catalog.Sequences.Select(row =>
        {
            RequireSchema(row.SchemaName, requestedSchemas);
            if (row.CacheSize > int.MaxValue)
                throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");
            return new CanonicalSequence(
                schemaIds[row.SchemaName],
                row.Name,
                PostgreSqlNativeTypeMapper.MapSequence(row),
                row.IncrementValue,
                row.MinimumValue,
                row.MaximumValue,
                checked((int)row.CacheSize),
                row.IsCyclic,
                null,
                row.StartValue,
                PostgreSqlIdentity.Sequence(schemaIds[row.SchemaName], row.Name));
        }).ToArray();

        var targetFingerprint = Hash(
            "PostgreSql18",
            connection.Host,
            connection.Port.ToString(CultureInfo.InvariantCulture),
            catalog.Target.DatabaseName);
        var visibilityFingerprint = Hash(
            connectionPrincipal: catalog.ConnectedPrincipal,
            components: request.IncludedSchemas.Order(StringComparer.Ordinal).ToArray());
        return new CanonicalDatabaseDiscoverySnapshot(
            CanonicalSnapshotService.CurrentFormatVersion,
            timeProvider.GetUtcNow(),
            DatabaseProviderType.PostgreSql,
            catalog.Target.ProviderVersion,
            new CanonicalDatabaseInfo(
                DatabaseProviderType.PostgreSql.ToString(),
                catalog.Target.ServerVersion,
                catalog.Target.DatabaseName,
                null,
                targetFingerprint),
            new CanonicalDiscoveryScope(
                1,
                1,
                schemas.Select(item => item.LogicalIdentity).ToArray(),
                [DatabaseDiscoveryObjectType.Table, DatabaseDiscoveryObjectType.View],
                1,
                "Ordinal",
                new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["postgresqlCatalogMapping"] = "18-core-v1",
                },
                visibilityFingerprint),
            CanonicalSnapshotService.CurrentIdentityAlgorithmVersion,
            DatabaseDiscoveryCompleteness.Complete,
            capabilities,
            schemas,
            objects,
            columns,
            primaryKeys,
            foreignKeys,
            uniqueConstraints,
            indexes,
            sequences,
            closure.Values.ToArray(),
            new CanonicalSnapshotCounts(0, 0, 0, 0, 0, 0, 0, 0, 0));
    }

    private static IReadOnlyList<string> ResolveColumns(
        string schemaName,
        string objectName,
        IReadOnlyList<PostgreSqlConstraintColumnRow> rows,
        IReadOnlyDictionary<(string SchemaName, string ObjectName, string Name), string> columnIds)
    {
        return rows.Select(row =>
        {
            if (!columnIds.TryGetValue((schemaName, objectName, row.ColumnName), out var id))
                throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
            return id;
        }).ToArray();
    }

    private static IReadOnlyList<T> OrderedUnique<T>(
        IEnumerable<T> source,
        Func<T, int> position,
        string label)
    {
        var result = source.OrderBy(position).ToArray();
        if (result.Length is < 1 or > 4096
            || result.Select(position).Where(value => value > 0).Distinct().Count() != result.Length
            || result.Select(position).Where(value => value > 0).Order().Where((value, index) => value != index + 1).Any())
        {
            throw Failure("MetadataQueryFailed", $"PostgreSQL {label}目录不完整。");
        }
        return result;
    }

    private static void RequireSchema(string schemaName, IReadOnlySet<string> requestedSchemas)
    {
        if (!requestedSchemas.Contains(schemaName))
            throw Failure("MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。");
    }

    private static void ValidateBounds(PostgreSqlCatalogSnapshot catalog, DatabaseDiscoveryLimits limits)
    {
        var constraintCount = catalog.Constraints
            .Select(item => (item.SchemaName, item.ObjectName, item.Name))
            .Distinct().Count();
        var indexCount = catalog.IndexParts
            .Select(item => (item.SchemaName, item.ObjectName, item.Name))
            .Distinct().Count();
        if (catalog.VisibleSchemas.Count > limits.MaximumSchemas
            || catalog.Objects.Count > limits.MaximumObjects
            || catalog.Columns.Count > limits.MaximumColumns
            || catalog.Constraints.Count > limits.MaximumColumns
            || catalog.IndexParts.Count > limits.MaximumColumns
            || constraintCount + indexCount > limits.MaximumConstraintsAndIndexes
            || catalog.Sequences.Count > limits.MaximumSequences)
        {
            throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");
        }

        IEnumerable<string?> names = catalog.Objects.SelectMany(item => new[] { item.SchemaName, item.Name })
            .Concat(catalog.Columns.SelectMany(item => new[]
            {
                item.SchemaName, item.ObjectName, item.Name, item.TypeName, item.TypeNamespace, item.BaseTypeName,
                item.BaseTypeNamespace,
            }))
            .Concat(catalog.Constraints.SelectMany(item => new[]
            {
                item.SchemaName, item.ObjectName, item.Name, item.ColumnName,
                item.ReferencedSchemaName, item.ReferencedObjectName, item.ReferencedColumnName,
            }))
            .Concat(catalog.IndexParts.SelectMany(item => new[]
            {
                item.SchemaName, item.ObjectName, item.Name, item.IndexKind,
                item.ColumnName, item.BackingConstraintName,
            }))
            .Concat(catalog.Sequences.SelectMany(item => new[]
            {
                item.SchemaName, item.Name, item.TypeName, item.TypeNamespace,
            }));
        if (names.Any(value => value is { Length: > 512 } || value?.Any(char.IsControl) == true)
            || catalog.Objects.Any(item => TooLong(item.Comment))
            || catalog.Columns.Any(item => item.Declaration.Length > 2048
                || TooLong(item.DefaultExpression) || TooLong(item.Comment))
            || catalog.IndexParts.Any(item => TooLong(item.NativeExpression) || TooLong(item.NativePredicate))
            || catalog.Sequences.Any(item => item.Declaration.Length > 2048
                || item.StartValue.Length > 256 || item.IncrementValue.Length > 256
                || item.MinimumValue.Length > 256 || item.MaximumValue.Length > 256))
        {
            throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");
        }
    }

    private static bool TooLong(string? value) =>
        value is { Length: > 32768 } || value?.Contains('\0') == true;

    private static string Hash(params string[] components) => Hash(string.Empty, components);

    private static string Hash(string connectionPrincipal, params string[] components)
    {
        var payload = PostgreSqlIdentity.Key([connectionPrincipal, .. components]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static DatabaseDiscoveryProviderException Failure(string code, string summary) => new(code, summary);
}

internal static class PostgreSqlDiscoveryRules
{
    public const int SupportedMajorVersion = 18;

    public static void ValidateTarget(
        DatabaseDiscoveryConnectionContext connection,
        PostgreSqlTargetContext target)
    {
        if (target.ServerMajorVersion != SupportedMajorVersion)
            throw new DatabaseDiscoveryProviderException(
                "UnsupportedDatabaseVersion", "仅支持 PostgreSQL 18。");
        if (connection.DatabaseName is null
            || !string.Equals(connection.DatabaseName, target.DatabaseName, StringComparison.Ordinal))
        {
            throw new DatabaseDiscoveryProviderException(
                "ConnectionFailed", "连接到的 PostgreSQL Database 与配置目标不一致。");
        }
    }

    public static string? MapReferentialAction(string? value) => value switch
    {
        null => null,
        "a" => "NO ACTION",
        "r" => "RESTRICT",
        "c" => "CASCADE",
        "n" => "SET NULL",
        "d" => "SET DEFAULT",
        _ => throw new DatabaseDiscoveryProviderException(
            "MetadataQueryFailed", "读取 PostgreSQL 目录元数据失败。"),
    };

    public static DatabaseDiscoverySortDirection MapSort(string indexKind, bool isDescending) =>
        string.Equals(indexKind, "btree", StringComparison.Ordinal)
            ? isDescending
                ? DatabaseDiscoverySortDirection.Descending
                : DatabaseDiscoverySortDirection.Ascending
            : DatabaseDiscoverySortDirection.Unspecified;
}

internal static class PostgreSqlIdentity
{
    public static string Schema(string name) => Key(["Schema", name]);
    public static string DatabaseObject(string schemaName, string name) => Key(["Object", schemaName, name]);
    public static string Column(string objectIdentity, string name) => Key(["Column", objectIdentity, name]);
    public static string Constraint(string kind, string objectIdentity, string name) =>
        Key(["Constraint", kind, objectIdentity, name]);
    public static string Index(string objectIdentity, string name) => Key(["Index", objectIdentity, name]);
    public static string Sequence(string schemaIdentity, string name) => Key(["Sequence", schemaIdentity, name]);
    public static string Key(IEnumerable<string> components) => string.Concat(components.Select(
        component => $"{component.Length.ToString(CultureInfo.InvariantCulture)}:{component}"));
}

internal static class PostgreSqlNativeTypeMapper
{
    public static CanonicalNativeDataType Map(PostgreSqlColumnRow row)
    {
        var baseType = row.BaseTypeName;
        var length = MapLength(baseType, row.CharacterLength, row.IsUnboundedLength);
        var numeric = IsNumeric(baseType);
        var precision = numeric
            ? row.NumericPrecision is null
                ? new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Unknown, null)
                : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, row.NumericPrecision)
            : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null);
        var scale = numeric
            ? row.NumericScale is null
                ? new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Unknown, null)
                : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, row.NumericScale)
            : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null);
        return new CanonicalNativeDataType(
            DatabaseDiscoveryNativeTypeOrigin.CatalogDeclared,
            row.TypeName,
            row.TypeNamespace,
            row.Declaration,
            length,
            baseType is "varchar" or "bpchar" or "text" ? "CHARACTERS" : null,
            precision,
            scale);
    }

    public static CanonicalNativeDataType MapSequence(PostgreSqlSequenceRow row)
    {
        var numericPrecision = row.TypeName switch
        {
            "int2" => 16,
            "int4" => 32,
            "int8" => 64,
            _ => (int?)null,
        };
        return new CanonicalNativeDataType(
            DatabaseDiscoveryNativeTypeOrigin.CatalogDeclared,
            row.TypeName,
            row.TypeNamespace,
            row.Declaration,
            new CanonicalLengthMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null, null),
            null,
            numericPrecision is null
                ? new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Unknown, null)
                : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, numericPrecision),
            new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, 0));
    }

    private static CanonicalLengthMeasure MapLength(
        string baseType,
        long? characterLength,
        bool isUnbounded)
    {
        if (baseType is "varchar" or "bpchar")
        {
            if (characterLength is not null)
                return new CanonicalLengthMeasure(
                    DatabaseDiscoveryMeasureKind.Exact,
                    characterLength,
                    DatabaseDiscoveryLengthUnit.Characters);
            return isUnbounded
                ? new CanonicalLengthMeasure(DatabaseDiscoveryMeasureKind.Unbounded, null, null)
                : new CanonicalLengthMeasure(DatabaseDiscoveryMeasureKind.Unknown, null, null);
        }
        if (baseType == "text")
            return new CanonicalLengthMeasure(DatabaseDiscoveryMeasureKind.Unbounded, null, null);
        if (baseType == "bytea")
            return new CanonicalLengthMeasure(DatabaseDiscoveryMeasureKind.Unbounded, null, null);
        return new CanonicalLengthMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null, null);
    }

    private static bool IsNumeric(string value) =>
        value is "int2" or "int4" or "int8" or "numeric" or "float4" or "float8";
}
