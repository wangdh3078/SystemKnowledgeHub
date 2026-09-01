using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.SqlServer;

internal sealed class SqlServerDiscoveryProvider(
    ISqlServerDiscoveryCatalogReader catalogReader,
    TimeProvider timeProvider) : IDatabaseDiscoveryProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.SqlServer;

    public async Task<DatabaseProviderCapabilities> DetectCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        var result = await catalogReader.ReadCapabilitiesAsync(connection, cancellationToken);
        SqlServerDiscoveryRules.ValidateTarget(connection, result.Target);
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
            throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
        }

        var catalog = await catalogReader.ReadCatalogAsync(connection, request, cancellationToken);
        SqlServerDiscoveryRules.ValidateTarget(connection, catalog.Target);
        return BuildSnapshot(connection, catalog, request, capabilities.Capabilities);
    }

    private CanonicalDatabaseDiscoverySnapshot BuildSnapshot(
        DatabaseDiscoveryConnectionContext connection,
        SqlServerCatalogSnapshot catalog,
        DatabaseDiscoveryRequest request,
        IReadOnlyList<CanonicalCapability> capabilities)
    {
        ValidateBounds(catalog, request.Limits);
        if (catalog.VisibleSchemas.Count != request.IncludedSchemas.Count
            || catalog.VisibleSchemas.Distinct(StringComparer.Ordinal).Count() != catalog.VisibleSchemas.Count)
        {
            throw Failure("UnsupportedIdentifierCollision", "IncludedSchemas 在目标数据库排序规则下存在歧义。");
        }

        var requestedSchemas = catalog.VisibleSchemas.ToHashSet(StringComparer.Ordinal);
        var schemas = catalog.VisibleSchemas
            .Select(name => new CanonicalSchema(name, SqlServerIdentity.Schema(name)))
            .ToArray();
        var schemaIds = schemas.ToDictionary(item => item.Name, item => item.LogicalIdentity, StringComparer.Ordinal);

        var objects = new List<CanonicalDatabaseObject>(catalog.Objects.Count);
        var objectIds = new Dictionary<(string SchemaName, string Name), string>();
        foreach (var row in catalog.Objects)
        {
            RequireSchema(row.SchemaName, requestedSchemas);
            var id = SqlServerIdentity.DatabaseObject(row.SchemaName, row.Name);
            if (!objectIds.TryAdd((row.SchemaName, row.Name), id))
                throw Failure("UnsupportedIdentifierCollision", "SQL Server 对象标识存在歧义。");
            objects.Add(new CanonicalDatabaseObject(
                schemaIds[row.SchemaName], row.SchemaName, row.Name, row.ObjectType,
                row.Comment, id, null));
        }

        var columns = new List<CanonicalColumn>(catalog.Columns.Count);
        var columnIds = new Dictionary<(string SchemaName, string ObjectName, string Name), string>();
        foreach (var row in catalog.Columns)
        {
            if (!objectIds.TryGetValue((row.SchemaName, row.ObjectName), out var objectId))
                throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
            var id = SqlServerIdentity.Column(objectId, row.Name);
            if (!columnIds.TryAdd((row.SchemaName, row.ObjectName, row.Name), id))
                throw Failure("UnsupportedIdentifierCollision", "SQL Server 字段标识存在歧义。");
            columns.Add(new CanonicalColumn(
                objectId,
                row.Name,
                row.SourceOrdinal,
                SqlServerNativeTypeMapper.Map(row),
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
                throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
            var first = rows[0];
            if (rows.Any(item => item.ConstraintType != first.ConstraintType))
                throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
            var ownedColumns = ResolveColumns(key.SchemaName, key.ObjectName, rows, columnIds);

            switch (first.ConstraintType)
            {
                case "PK":
                {
                    var logicalId = SqlServerIdentity.Constraint("PK", parentId, key.Name);
                    primaryKeys.Add(new CanonicalPrimaryKey(key.Name, parentId, ownedColumns, logicalId));
                    constraintLogicalIds[key] = logicalId;
                    foreach (var id in ownedColumns) primaryColumnIds.Add(id);
                    break;
                }
                case "UQ":
                {
                    var logicalId = SqlServerIdentity.Constraint("UQ", parentId, key.Name);
                    uniqueConstraints.Add(new CanonicalUniqueConstraint(key.Name, parentId, ownedColumns, logicalId));
                    constraintLogicalIds[key] = logicalId;
                    break;
                }
                case "FK":
                {
                    if (rows.Any(item => item.ReferencedSchemaName is null
                            || item.ReferencedObjectName is null
                            || item.ReferencedColumnName is null
                            || !string.Equals(item.ReferencedSchemaName, first.ReferencedSchemaName, StringComparison.Ordinal)
                            || !string.Equals(item.ReferencedObjectName, first.ReferencedObjectName, StringComparison.Ordinal)))
                    {
                        throw Failure("UnresolvedForeignKeyReference", "无法完整解析 SQL Server 外键引用。");
                    }

                    var referencedSchema = first.ReferencedSchemaName!;
                    var referencedObject = first.ReferencedObjectName!;
                    var referencedObjectId = SqlServerIdentity.DatabaseObject(referencedSchema, referencedObject);
                    IReadOnlyList<string> referencedColumns;
                    if (objectIds.ContainsKey((referencedSchema, referencedObject)))
                    {
                        referencedColumns = rows.Select(item =>
                        {
                            if (!columnIds.TryGetValue(
                                    (referencedSchema, referencedObject, item.ReferencedColumnName!), out var id))
                                throw Failure("UnresolvedForeignKeyReference", "无法完整解析 SQL Server 外键引用。");
                            return id;
                        }).ToArray();
                    }
                    else
                    {
                        referencedColumns = rows.Select(item =>
                        {
                            var columnName = item.ReferencedColumnName!;
                            var id = SqlServerIdentity.Column(referencedObjectId, columnName);
                            var stub = new CanonicalForeignKeyReferenceStub(
                                SqlServerIdentity.Schema(referencedSchema),
                                referencedSchema,
                                referencedObjectId,
                                referencedObject,
                                id,
                                columnName,
                                true);
                            if (closure.TryGetValue(id, out var existing) && existing != stub)
                                throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
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
                        SqlServerDiscoveryRules.MapReferentialAction(first.UpdateAction),
                        SqlServerDiscoveryRules.MapReferentialAction(first.DeleteAction),
                        SqlServerIdentity.Constraint("FK", parentId, key.Name)));
                    break;
                }
                default:
                    throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
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
            var parts = group.OrderBy(item => item.Position).ToArray();
            if (parts.Length is < 1 or > 4096
                || parts.Select(item => item.Position).Distinct().Count() != parts.Length)
                throw Failure("MetadataQueryFailed", "SQL Server 索引列目录不完整。");
            var first = parts[0];
            if (first.IndexType is not (1 or 2)
                || parts.Any(item => item.IndexType != first.IndexType
                    || item.IndexTypeDescription != first.IndexTypeDescription
                    || item.IsUnique != first.IsUnique
                    || item.NativePredicate != first.NativePredicate
                    || item.BackingConstraintName != first.BackingConstraintName
                    || item.IsHypothetical))
            {
                throw Failure(
                    "UnsupportedIndexFamily",
                    "发现了当前 Core 无法完整表达的 SQL Server 索引类型。");
            }
            if (!objectIds.TryGetValue((first.SchemaName, first.ObjectName), out var parentId))
                throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");

            var keyRows = parts.Where(item => item.KeyOrdinal > 0).OrderBy(item => item.KeyOrdinal).ToArray();
            if (keyRows.Length == 0
                || keyRows.Select(item => item.KeyOrdinal).Distinct().Count() != keyRows.Length
                || keyRows.Where((item, index) => item.KeyOrdinal != index + 1).Any())
                throw Failure("MetadataQueryFailed", "SQL Server 索引键目录不完整。");
            var keyParts = keyRows.Select(part =>
            {
                if (part.IsIncluded
                    || !columnIds.TryGetValue((part.SchemaName, part.ObjectName, part.ColumnName), out var columnId))
                    throw Failure("MetadataQueryFailed", "SQL Server 索引键目录不完整。");
                return new CanonicalIndexKeyPart(
                    part.KeyOrdinal,
                    columnId,
                    null,
                    part.IsDescending
                        ? DatabaseDiscoverySortDirection.Descending
                        : DatabaseDiscoverySortDirection.Ascending);
            }).ToArray();

            var nonKeyParts = parts.Where(item => item.KeyOrdinal == 0).Select(part =>
            {
                if (!columnIds.TryGetValue((part.SchemaName, part.ObjectName, part.ColumnName), out var columnId))
                    throw Failure("MetadataQueryFailed", "SQL Server 索引非键列目录不完整。");
                var role = part.IsIncluded
                    ? DatabaseDiscoveryNonKeyPartRole.Included
                    : part.PartitionOrdinal > 0
                        ? DatabaseDiscoveryNonKeyPartRole.Partitioning
                        : throw Failure("UnsupportedIndexFamily", "SQL Server 索引列角色无法完整表达。");
                return new CanonicalIndexNonKeyPart(part.Position, columnId, role);
            }).ToArray();

            string? backingConstraintId = null;
            if (first.BackingConstraintName is not null
                && !constraintLogicalIds.TryGetValue(
                    (first.SchemaName, first.ObjectName, first.BackingConstraintName), out backingConstraintId))
            {
                throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
            }

            indexes.Add(new CanonicalIndex(
                first.Name,
                parentId,
                first.IndexTypeDescription,
                first.IsUnique,
                keyParts,
                nonKeyParts,
                first.NativePredicate,
                backingConstraintId,
                SqlServerIdentity.Index(parentId, first.Name)));
        }

        var sequences = catalog.Sequences.Select(row =>
        {
            RequireSchema(row.SchemaName, requestedSchemas);
            return new CanonicalSequence(
                schemaIds[row.SchemaName],
                row.Name,
                SqlServerNativeTypeMapper.MapSequence(row),
                row.IncrementValue,
                row.MinimumValue,
                row.MaximumValue,
                row.CacheSize,
                row.IsCyclic,
                null,
                row.StartValue,
                SqlServerIdentity.Sequence(schemaIds[row.SchemaName], row.Name));
        }).ToArray();

        var targetFingerprint = Hash(
            "SqlServer2022",
            connection.Host,
            connection.Port.ToString(CultureInfo.InvariantCulture),
            catalog.Target.DatabaseName);
        var visibilityFingerprint = Hash(
            [
                catalog.ConnectedPrincipal,
                catalog.Target.DatabaseCollation,
                .. catalog.VisibleSchemas.Order(StringComparer.Ordinal),
            ]);
        return new CanonicalDatabaseDiscoverySnapshot(
            CanonicalSnapshotService.CurrentFormatVersion,
            timeProvider.GetUtcNow(),
            DatabaseProviderType.SqlServer,
            catalog.Target.ProviderVersion,
            new CanonicalDatabaseInfo(
                DatabaseProviderType.SqlServer.ToString(),
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
                    ["sqlServerCatalogMapping"] = "2022-core-v1",
                    ["databaseCollation"] = catalog.Target.DatabaseCollation,
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
        IReadOnlyList<SqlServerConstraintColumnRow> rows,
        IReadOnlyDictionary<(string SchemaName, string ObjectName, string Name), string> columnIds) =>
        rows.Select(row =>
        {
            if (!columnIds.TryGetValue((schemaName, objectName, row.ColumnName), out var id))
                throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
            return id;
        }).ToArray();

    private static IReadOnlyList<T> OrderedUnique<T>(
        IEnumerable<T> source,
        Func<T, int> position,
        string label)
    {
        var result = source.OrderBy(position).ToArray();
        if (result.Length is < 1 or > 4096
            || result.Select(position).Distinct().Count() != result.Length
            || result.Where((item, index) => position(item) != index + 1).Any())
            throw Failure("MetadataQueryFailed", $"SQL Server {label}目录不完整。");
        return result;
    }

    private static void RequireSchema(string schemaName, IReadOnlySet<string> requestedSchemas)
    {
        if (!requestedSchemas.Contains(schemaName))
            throw Failure("MetadataQueryFailed", "读取 SQL Server 目录元数据失败。");
    }

    private static void ValidateBounds(SqlServerCatalogSnapshot catalog, DatabaseDiscoveryLimits limits)
    {
        var constraintCount = catalog.Constraints
            .Select(item => (item.SchemaName, item.ObjectName, item.Name)).Distinct().Count();
        var indexCount = catalog.IndexParts
            .Select(item => (item.SchemaName, item.ObjectName, item.Name)).Distinct().Count();
        if (catalog.VisibleSchemas.Count > limits.MaximumSchemas
            || catalog.Objects.Count > limits.MaximumObjects
            || catalog.Columns.Count > limits.MaximumColumns
            || catalog.Constraints.Count > limits.MaximumColumns
            || catalog.IndexParts.Count > limits.MaximumColumns
            || constraintCount + indexCount > limits.MaximumConstraintsAndIndexes
            || catalog.Sequences.Count > limits.MaximumSequences)
            throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");

        IEnumerable<string?> names = catalog.Objects.SelectMany(item => new[] { item.SchemaName, item.Name })
            .Concat(catalog.Columns.SelectMany(item => new[]
            {
                item.SchemaName, item.ObjectName, item.Name, item.TypeName, item.TypeNamespace, item.BaseTypeName,
            }))
            .Concat(catalog.Constraints.SelectMany(item => new[]
            {
                item.SchemaName, item.ObjectName, item.Name, item.ColumnName,
                item.ReferencedSchemaName, item.ReferencedObjectName, item.ReferencedColumnName,
            }))
            .Concat(catalog.IndexParts.SelectMany(item => new[]
            {
                item.SchemaName, item.ObjectName, item.Name, item.IndexTypeDescription,
                item.ColumnName, item.BackingConstraintName,
            }))
            .Concat(catalog.Sequences.SelectMany(item => new[]
            {
                item.SchemaName, item.Name, item.TypeName, item.TypeNamespace, item.BaseTypeName,
            }));
        if (names.Any(value => value is { Length: > 512 } || value?.Any(char.IsControl) == true)
            || catalog.Objects.Any(item => TooLong(item.Comment))
            || catalog.Columns.Any(item => TooLong(item.DefaultExpression) || TooLong(item.Comment))
            || catalog.IndexParts.Any(item => TooLong(item.NativePredicate))
            || catalog.Sequences.Any(item => item.StartValue.Length > 256
                || item.IncrementValue.Length > 256
                || item.MinimumValue.Length > 256
                || item.MaximumValue.Length > 256))
            throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");
    }

    private static bool TooLong(string? value) =>
        value is { Length: > 32768 } || value?.Contains('\0') == true;

    private static string Hash(params string[] components)
    {
        var payload = SqlServerIdentity.Key(components);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static DatabaseDiscoveryProviderException Failure(string code, string summary) =>
        new(code, summary);
}

internal static class SqlServerDiscoveryRules
{
    public const int SupportedMajorVersion = 16;

    public static void ValidateTarget(
        DatabaseDiscoveryConnectionContext connection,
        SqlServerTargetContext target)
    {
        if (target.ServerMajorVersion != SupportedMajorVersion)
            throw new DatabaseDiscoveryProviderException(
                "UnsupportedDatabaseVersion", "仅支持 SQL Server 2022（major 16）。");
        if (string.IsNullOrWhiteSpace(connection.DatabaseName)
            || string.IsNullOrWhiteSpace(target.DatabaseName))
            throw new DatabaseDiscoveryProviderException(
                "ConnectionFailed", "连接到的 SQL Server Database 与配置目标不一致。");
    }

    public static string? MapReferentialAction(string? value) => value switch
    {
        null => null,
        "NO_ACTION" => "NO ACTION",
        "CASCADE" => "CASCADE",
        "SET_NULL" => "SET NULL",
        "SET_DEFAULT" => "SET DEFAULT",
        _ => throw new DatabaseDiscoveryProviderException(
            "MetadataQueryFailed", "读取 SQL Server 目录元数据失败。"),
    };
}

internal static class SqlServerIdentity
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

internal static class SqlServerNativeTypeMapper
{
    public static CanonicalNativeDataType Map(SqlServerColumnRow row)
    {
        if (row.IsAssemblyType)
            throw new DatabaseDiscoveryProviderException(
                "UnsupportedNativeType", "发现了当前 Core 无法完整表达的 SQL Server CLR 类型。");
        return Build(
            row.TypeName,
            row.TypeNamespace,
            row.IsUserDefined,
            row.BaseTypeName,
            row.MaximumLength,
            row.NumericPrecision,
            row.NumericScale);
    }

    public static CanonicalNativeDataType MapSequence(SqlServerSequenceRow row)
    {
        if (row.IsAssemblyType)
            throw new DatabaseDiscoveryProviderException(
                "UnsupportedNativeType", "发现了当前 Core 无法完整表达的 SQL Server CLR 类型。");
        return Build(
            row.TypeName,
            row.TypeNamespace,
            row.IsUserDefined,
            row.BaseTypeName,
            -1,
            row.NumericPrecision,
            row.NumericScale,
            sequence: true);
    }

    private static CanonicalNativeDataType Build(
        string typeName,
        string typeNamespace,
        bool isUserDefined,
        string baseTypeName,
        int maximumLength,
        int precision,
        int scale,
        bool sequence = false)
    {
        var length = sequence
            ? new CanonicalLengthMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null, null)
            : MapLength(baseTypeName, maximumLength);
        var numeric = IsNumeric(baseTypeName);
        return new CanonicalNativeDataType(
            DatabaseDiscoveryNativeTypeOrigin.CatalogDeclared,
            typeName,
            typeNamespace,
            Declaration(typeName, typeNamespace, isUserDefined, baseTypeName, maximumLength, precision, scale),
            length,
            baseTypeName is "char" or "varchar" or "nchar" or "nvarchar" or "text" or "ntext"
                ? "CHARACTERS"
                : null,
            numeric
                ? new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, precision)
                : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null),
            numeric
                ? new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, scale)
                : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null));
    }

    private static string Declaration(
        string typeName,
        string typeNamespace,
        bool isUserDefined,
        string baseTypeName,
        int maximumLength,
        int precision,
        int scale)
    {
        if (isUserDefined)
            return $"[{typeNamespace.Replace("]", "]]", StringComparison.Ordinal)}].[{typeName.Replace("]", "]]", StringComparison.Ordinal)}]";
        return baseTypeName switch
        {
            "char" or "varchar" or "binary" or "varbinary" =>
                $"{baseTypeName}({(maximumLength == -1 ? "max" : maximumLength.ToString(CultureInfo.InvariantCulture))})",
            "nchar" or "nvarchar" =>
                $"{baseTypeName}({(maximumLength == -1 ? "max" : (maximumLength / 2).ToString(CultureInfo.InvariantCulture))})",
            "decimal" or "numeric" => $"{baseTypeName}({precision.ToString(CultureInfo.InvariantCulture)},{scale.ToString(CultureInfo.InvariantCulture)})",
            "datetime2" or "datetimeoffset" or "time" => $"{baseTypeName}({scale.ToString(CultureInfo.InvariantCulture)})",
            "float" => $"float({precision.ToString(CultureInfo.InvariantCulture)})",
            _ => baseTypeName,
        };
    }

    private static CanonicalLengthMeasure MapLength(string baseTypeName, int maximumLength) =>
        baseTypeName switch
        {
            "varchar" or "varbinary" when maximumLength == -1 =>
                new(DatabaseDiscoveryMeasureKind.Unbounded, null, null),
            "nvarchar" when maximumLength == -1 =>
                new(DatabaseDiscoveryMeasureKind.Unbounded, null, null),
            "text" or "ntext" or "image" or "xml" =>
                new(DatabaseDiscoveryMeasureKind.Unbounded, null, null),
            "nchar" or "nvarchar" =>
                new(DatabaseDiscoveryMeasureKind.Exact, maximumLength / 2, DatabaseDiscoveryLengthUnit.Characters),
            "char" or "varchar" or "binary" or "varbinary" =>
                new(DatabaseDiscoveryMeasureKind.Exact, maximumLength, DatabaseDiscoveryLengthUnit.Bytes),
            _ => new(DatabaseDiscoveryMeasureKind.NotApplicable, null, null),
        };

    private static bool IsNumeric(string value) => value is
        "tinyint" or "smallint" or "int" or "bigint" or "decimal" or "numeric"
        or "money" or "smallmoney" or "float" or "real" or "bit";
}
