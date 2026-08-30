using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Providers.Oracle;

internal sealed class OracleDiscoveryProvider(
    IOracleDiscoveryCatalogReader catalogReader,
    TimeProvider timeProvider) : IDatabaseDiscoveryProvider
{
    public DatabaseProviderType ProviderType => DatabaseProviderType.Oracle;

    public async Task<DatabaseProviderCapabilities> DetectCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken)
    {
        var result = await catalogReader.ReadCapabilitiesAsync(connection, cancellationToken);
        OracleDiscoveryRules.ValidateTarget(connection, result.Target);
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
            throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
        }

        var catalog = await catalogReader.ReadCatalogAsync(connection, request, cancellationToken);
        OracleDiscoveryRules.ValidateTarget(connection, catalog.Target);
        return BuildSnapshot(catalog, request, capabilities.Capabilities);
    }

    private CanonicalDatabaseDiscoverySnapshot BuildSnapshot(
        OracleCatalogSnapshot catalog,
        DatabaseDiscoveryRequest request,
        IReadOnlyList<CanonicalCapability> capabilities)
    {
        ValidateBounds(catalog, request.Limits);

        var requestedSchemas = request.IncludedSchemas.ToHashSet(StringComparer.Ordinal);
        if (!catalog.VisibleSchemas.ToHashSet(StringComparer.Ordinal).SetEquals(requestedSchemas)
            || catalog.VisibleSchemas.Count != requestedSchemas.Count)
        {
            throw Failure("InsufficientPrivilege", "Oracle 账号缺少必要的目录元数据权限。");
        }

        var schemas = request.IncludedSchemas
            .Select(name => new CanonicalSchema(name, OracleIdentity.Schema(name)))
            .ToArray();
        var schemaIds = schemas.ToDictionary(item => item.Name, item => item.LogicalIdentity, StringComparer.Ordinal);

        var comments = UniqueDictionary(
            catalog.ObjectComments.Where(item => requestedSchemas.Contains(item.Owner)),
            item => (item.Owner, item.ObjectName, item.ObjectType),
            "对象注释");

        var objects = new List<CanonicalDatabaseObject>(catalog.Tables.Count + catalog.Views.Count);
        var objectIds = new Dictionary<(string Owner, string Name), string>();
        foreach (var row in catalog.Tables.Select(item => (item.Owner, item.Name, Type: DatabaseDiscoveryObjectType.Table))
                     .Concat(catalog.Views.Select(item => (item.Owner, item.Name, Type: DatabaseDiscoveryObjectType.View))))
        {
            RequireSchema(row.Owner, requestedSchemas);
            var id = OracleIdentity.DatabaseObject(row.Owner, row.Name);
            if (!objectIds.TryAdd((row.Owner, row.Name), id))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            comments.TryGetValue((row.Owner, row.Name, row.Type == DatabaseDiscoveryObjectType.Table ? "TABLE" : "VIEW"), out var comment);
            objects.Add(new CanonicalDatabaseObject(
                schemaIds[row.Owner], row.Owner, row.Name, row.Type, comment?.Comment, id, null));
        }

        var columnComments = UniqueDictionary(
            catalog.ColumnComments.Where(item => requestedSchemas.Contains(item.Owner)),
            item => (item.Owner, item.ObjectName, item.ColumnName),
            "列注释");
        var columns = new List<CanonicalColumn>(catalog.Columns.Count);
        var columnIds = new Dictionary<(string Owner, string ObjectName, string ColumnName), string>();
        foreach (var row in catalog.Columns)
        {
            if (!objectIds.TryGetValue((row.Owner, row.ObjectName), out var objectId))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            var id = OracleIdentity.Column(objectId, row.Name);
            if (!columnIds.TryAdd((row.Owner, row.ObjectName, row.Name), id))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            columnComments.TryGetValue((row.Owner, row.ObjectName, row.Name), out var comment);
            columns.Add(new CanonicalColumn(
                objectId,
                row.Name,
                row.SourceOrdinal,
                OracleNativeTypeMapper.Map(row),
                row.Nullable == "Y",
                row.DefaultExpression,
                false,
                comment?.Comment,
                id));
        }
        if (objects.Any(item => !columns.Any(column => column.ParentObjectLogicalIdentity == item.LogicalIdentity)))
            throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");

        var constraintRows = UniqueDictionary(
            catalog.Constraints,
            item => (item.Owner, item.Name),
            "约束");
        var constraintColumns = catalog.ConstraintColumns
            .GroupBy(item => (item.Owner, item.ConstraintName))
            .ToDictionary(
                group => group.Key,
                group => OrderedUnique(group, item => item.Position, "约束列"),
                EqualityComparer<(string Owner, string ConstraintName)>.Default);

        var primaryKeys = new List<CanonicalPrimaryKey>();
        var foreignKeys = new List<CanonicalForeignKey>();
        var uniqueConstraints = new List<CanonicalUniqueConstraint>();
        var closure = new Dictionary<string, CanonicalForeignKeyReferenceStub>(StringComparer.Ordinal);
        var primaryColumnIds = new HashSet<string>(StringComparer.Ordinal);
        var constraintLogicalIds = new Dictionary<(string Owner, string Name), string>();

        foreach (var row in catalog.Constraints.Where(item => requestedSchemas.Contains(item.Owner)))
        {
            if (!objectIds.TryGetValue((row.Owner, row.ObjectName), out var parentId))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            if (!constraintColumns.TryGetValue((row.Owner, row.Name), out var orderedColumns))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            if (orderedColumns.Any(item => !string.Equals(item.ObjectName, row.ObjectName, StringComparison.Ordinal)))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            var ownedColumnIds = ResolveColumns(row.Owner, row.ObjectName, orderedColumns, columnIds);
            switch (row.ConstraintType)
            {
                case "P":
                {
                    var logicalId = OracleIdentity.Constraint("PK", parentId, row.Name);
                    primaryKeys.Add(new CanonicalPrimaryKey(row.Name, parentId, ownedColumnIds, logicalId));
                    constraintLogicalIds[(row.Owner, row.Name)] = logicalId;
                    foreach (var id in ownedColumnIds) primaryColumnIds.Add(id);
                    break;
                }
                case "U":
                {
                    var logicalId = OracleIdentity.Constraint("UQ", parentId, row.Name);
                    uniqueConstraints.Add(new CanonicalUniqueConstraint(row.Name, parentId, ownedColumnIds, logicalId));
                    constraintLogicalIds[(row.Owner, row.Name)] = logicalId;
                    break;
                }
                case "R":
                {
                    if (row.ReferencedOwner is null || row.ReferencedConstraintName is null
                        || !constraintRows.TryGetValue((row.ReferencedOwner, row.ReferencedConstraintName), out var referenced)
                        || referenced.ConstraintType is not ("P" or "U")
                        || !constraintColumns.TryGetValue((row.ReferencedOwner, row.ReferencedConstraintName), out var referencedColumns)
                        || referencedColumns.Any(item => !string.Equals(item.ObjectName, referenced.ObjectName, StringComparison.Ordinal)))
                        throw Failure("UnresolvedForeignKeyReference", "无法完整解析 Oracle 外键引用。");
                    var referencedObjectId = OracleIdentity.DatabaseObject(referenced.Owner, referenced.ObjectName);
                    IReadOnlyList<string> referencedColumnIds;
                    if (objectIds.ContainsKey((referenced.Owner, referenced.ObjectName)))
                    {
                        referencedColumnIds = ResolveColumns(
                            referenced.Owner, referenced.ObjectName, referencedColumns, columnIds);
                    }
                    else
                    {
                        referencedColumnIds = referencedColumns.Select(column =>
                        {
                            var id = OracleIdentity.Column(referencedObjectId, column.ColumnName);
                            var stub = new CanonicalForeignKeyReferenceStub(
                                OracleIdentity.Schema(referenced.Owner),
                                referenced.Owner,
                                referencedObjectId,
                                referenced.ObjectName,
                                id,
                                column.ColumnName,
                                true);
                            if (closure.TryGetValue(id, out var existing) && existing != stub)
                                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
                            closure[id] = stub;
                            return id;
                        }).ToArray();
                    }
                    if (ownedColumnIds.Count != referencedColumnIds.Count)
                        throw Failure("UnresolvedForeignKeyReference", "无法完整解析 Oracle 外键引用。");
                    foreignKeys.Add(new CanonicalForeignKey(
                        row.Name,
                        parentId,
                        ownedColumnIds,
                        referencedObjectId,
                        referencedColumnIds,
                        null,
                        OracleDiscoveryRules.MapDeleteRule(row.DeleteRule),
                        OracleIdentity.Constraint("FK", parentId, row.Name)));
                    break;
                }
                default:
                    throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            }
        }

        columns = columns.Select(item => item with
        {
            IsPrimaryKey = primaryColumnIds.Contains(item.LogicalIdentity),
        }).ToList();

        var indexColumns = catalog.IndexColumns
            .GroupBy(item => (item.Owner, item.Name))
            .ToDictionary(
                group => group.Key,
                group => OrderedUnique(group, item => item.Position, "索引列"));
        var indexExpressions = UniqueDictionary(
            catalog.IndexExpressions,
            item => (item.Owner, item.Name, item.Position),
            "索引表达式");
        var indexKeys = catalog.Indexes.Select(item => (item.Owner, item.Name)).ToHashSet();
        if (indexColumns.Keys.Any(key => !indexKeys.Contains(key))
            || indexExpressions.Keys.Any(key => !indexKeys.Contains((key.Owner, key.Name))))
            throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
        var backingConstraints = catalog.Constraints
            .Where(item => item.ConstraintType is "P" or "U" && item.IndexOwner is not null && item.IndexName is not null)
            .ToDictionary(
                item => (item.IndexOwner!, item.IndexName!),
                item => constraintLogicalIds.GetValueOrDefault((item.Owner, item.Name)));
        var indexes = new List<CanonicalIndex>();
        foreach (var row in catalog.Indexes)
        {
            if (!objectIds.TryGetValue((row.TableOwner, row.TableName), out var parentId)
                || !indexColumns.TryGetValue((row.Owner, row.Name), out var parts))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            var keyParts = parts.Select(part =>
            {
                indexExpressions.TryGetValue((row.Owner, row.Name, part.Position), out var expression);
                if (expression is not null)
                    return new CanonicalIndexKeyPart(part.Position, null, expression.Expression, OracleDiscoveryRules.MapSort(part.Descending));
                if (!columnIds.TryGetValue((row.TableOwner, row.TableName, part.ColumnName), out var columnId))
                    throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
                return new CanonicalIndexKeyPart(part.Position, columnId, null, OracleDiscoveryRules.MapSort(part.Descending));
            }).ToArray();
            if (row.IndexType.Contains("FUNCTION-BASED", StringComparison.Ordinal)
                && keyParts.All(item => item.NativeExpression is null))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            backingConstraints.TryGetValue((row.Owner, row.Name), out var backingConstraint);
            indexes.Add(new CanonicalIndex(
                row.Name,
                parentId,
                row.IndexType,
                row.Uniqueness == "UNIQUE",
                keyParts,
                [],
                null,
                backingConstraint,
                OracleIdentity.Index(parentId, row.Name)));
        }

        var sequenceType = OracleNativeTypeMapper.SequenceType;
        var sequences = catalog.Sequences.Select(row =>
        {
            RequireSchema(row.Owner, requestedSchemas);
            if (row.CacheSize is > int.MaxValue)
                throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");
            return new CanonicalSequence(
                schemaIds[row.Owner],
                row.Name,
                sequenceType,
                row.IncrementValue,
                row.MinimumValue,
                row.MaximumValue,
                checked((int?)row.CacheSize),
                OracleDiscoveryRules.MapFlag(row.CycleFlag),
                OracleDiscoveryRules.MapFlag(row.OrderFlag),
                null,
                OracleIdentity.Sequence(schemaIds[row.Owner], row.Name));
        }).ToArray();

        var targetFingerprint = Hash(
            "Oracle19", catalog.Target.ServiceName, catalog.Target.ContainerName ?? string.Empty,
            catalog.Target.DatabaseName ?? string.Empty);
        var visibilityFingerprint = Hash(
            connectionPrincipal: catalog.ConnectedPrincipal,
            components: request.IncludedSchemas.Order(StringComparer.Ordinal).ToArray());
        return new CanonicalDatabaseDiscoverySnapshot(
            CanonicalSnapshotService.CurrentFormatVersion,
            timeProvider.GetUtcNow(),
            DatabaseProviderType.Oracle,
            catalog.Target.ProviderVersion,
            new CanonicalDatabaseInfo(
                "Oracle",
                catalog.Target.ServerVersion,
                catalog.Target.ServiceName,
                catalog.Target.ContainerName,
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
                    ["oracleCatalogMapping"] = "19c-core-v1",
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
        string owner,
        string objectName,
        IReadOnlyList<OracleConstraintColumnRow> rows,
        IReadOnlyDictionary<(string Owner, string ObjectName, string ColumnName), string> columnIds)
    {
        var result = new string[rows.Count];
        for (var index = 0; index < rows.Count; index++)
        {
            if (!columnIds.TryGetValue((owner, objectName, rows[index].ColumnName), out var id))
                throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
            result[index] = id;
        }
        return result;
    }

    private static Dictionary<TKey, T> UniqueDictionary<T, TKey>(
        IEnumerable<T> source,
        Func<T, TKey> keySelector,
        string label) where TKey : notnull
    {
        var result = new Dictionary<TKey, T>();
        foreach (var item in source)
            if (!result.TryAdd(keySelector(item), item))
                throw Failure("MetadataQueryFailed", $"Oracle {label}目录包含重复记录。");
        return result;
    }

    private static IReadOnlyList<T> OrderedUnique<T>(
        IEnumerable<T> source,
        Func<T, int> position,
        string label)
    {
        var result = source.OrderBy(position).ToArray();
        if (result.Length > 4096)
            throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");
        if (result.Length == 0 || result.Any(item => position(item) <= 0)
            || result.Select(position).Distinct().Count() != result.Length)
            throw Failure("MetadataQueryFailed", $"Oracle {label}目录不完整。");
        return result;
    }

    private static void RequireSchema(string owner, IReadOnlySet<string> requestedSchemas)
    {
        if (!requestedSchemas.Contains(owner))
            throw Failure("MetadataQueryFailed", "读取 Oracle 目录元数据失败。");
    }

    private static void ValidateBounds(OracleCatalogSnapshot catalog, DatabaseDiscoveryLimits limits)
    {
        var objectCount = catalog.Tables.Count + catalog.Views.Count;
        var structuralCount = catalog.Constraints.Count(item => catalog.VisibleSchemas.Contains(item.Owner, StringComparer.Ordinal))
            + catalog.Indexes.Count;
        if (catalog.VisibleSchemas.Count > limits.MaximumSchemas
            || objectCount > limits.MaximumObjects
            || catalog.Columns.Count > limits.MaximumColumns
            || catalog.ConstraintColumns.Count > limits.MaximumColumns
            || catalog.IndexColumns.Count > limits.MaximumColumns
            || catalog.IndexExpressions.Count > limits.MaximumColumns
            || catalog.ObjectComments.Count > limits.MaximumObjects
            || catalog.ColumnComments.Count > limits.MaximumColumns
            || structuralCount > limits.MaximumConstraintsAndIndexes
            || catalog.Sequences.Count > limits.MaximumSequences)
            throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");

        IEnumerable<string?> text = catalog.Tables.Select(item => item.Owner).Concat(catalog.Tables.Select(item => item.Name))
            .Concat(catalog.Views.Select(item => item.Owner)).Concat(catalog.Views.Select(item => item.Name))
            .Concat(catalog.Columns.SelectMany(item => new[] { item.Owner, item.ObjectName, item.Name, item.DataType, item.DataTypeOwner }))
            .Concat(catalog.Constraints.SelectMany(item => new[] { item.Owner, item.Name, item.ObjectName, item.ReferencedOwner, item.ReferencedConstraintName }))
            .Concat(catalog.ConstraintColumns.SelectMany(item => new[] { item.Owner, item.ConstraintName, item.ObjectName, item.ColumnName }))
            .Concat(catalog.Indexes.SelectMany(item => new[] { item.Owner, item.Name, item.TableOwner, item.TableName, item.IndexType }))
            .Concat(catalog.IndexColumns.SelectMany(item => new[] { item.Owner, item.Name, item.TableOwner, item.TableName, item.ColumnName }))
            .Concat(catalog.IndexExpressions.SelectMany(item => new[] { item.Owner, item.Name, item.TableOwner, item.TableName }))
            .Concat(catalog.ObjectComments.SelectMany(item => new[] { item.Owner, item.ObjectName, item.ObjectType }))
            .Concat(catalog.ColumnComments.SelectMany(item => new[] { item.Owner, item.ObjectName, item.ColumnName }))
            .Concat(catalog.Sequences.SelectMany(item => new[] { item.Owner, item.Name }));
        if (text.Any(value => value is { Length: > 512 } || value?.Any(character => character == '\0') == true)
            || catalog.Columns.Any(item => TooLong(item.DefaultExpression))
            || catalog.ObjectComments.Any(item => TooLong(item.Comment))
            || catalog.ColumnComments.Any(item => TooLong(item.Comment))
            || catalog.IndexExpressions.Any(item => TooLong(item.Expression))
            || catalog.Sequences.Any(item => item.MinimumValue.Length > 256
                || item.MaximumValue.Length > 256 || item.IncrementValue.Length > 256))
            throw Failure("LimitExceeded", "发现结果超过配置的安全限制。");
    }

    private static bool TooLong(string? value) => value is { Length: > 32768 } || value?.Contains('\0') == true;

    private static string Hash(params string[] components) => Hash(string.Empty, components);

    private static string Hash(string connectionPrincipal, params string[] components)
    {
        var payload = OracleIdentity.Key([connectionPrincipal, .. components]);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload)));
    }

    private static DatabaseDiscoveryProviderException Failure(string code, string summary) => new(code, summary);
}

internal static class OracleDiscoveryRules
{
    public static void ValidateTarget(DatabaseDiscoveryConnectionContext connection, OracleTargetContext target)
    {
        var first = new string(target.ServerVersion.TakeWhile(char.IsDigit).ToArray());
        if (!int.TryParse(first, NumberStyles.None, CultureInfo.InvariantCulture, out var major) || major != 19)
            throw new DatabaseDiscoveryProviderException(
                "UnsupportedDatabaseVersion", "仅支持 Oracle Database 19c。");
        if (!string.Equals(connection.ServiceName, target.ServiceName, StringComparison.OrdinalIgnoreCase))
            throw new DatabaseDiscoveryProviderException(
                "ConnectionFailed", "连接到的 Oracle Service 与配置目标不一致。");
        if (string.Equals(target.ContainerName, "CDB$ROOT", StringComparison.OrdinalIgnoreCase))
            throw new DatabaseDiscoveryProviderException(
                "ConnectionFailed", "Oracle 连接不能使用 CDB Root。");
    }

    public static string? MapDeleteRule(string? value) => value switch
    {
        null => null,
        "NO ACTION" => "NO ACTION",
        "CASCADE" => "CASCADE",
        "SET NULL" => "SET NULL",
        _ => throw new DatabaseDiscoveryProviderException("MetadataQueryFailed", "读取 Oracle 目录元数据失败。"),
    };

    public static DatabaseDiscoverySortDirection MapSort(string value) => value switch
    {
        "ASC" => DatabaseDiscoverySortDirection.Ascending,
        "DESC" => DatabaseDiscoverySortDirection.Descending,
        _ => DatabaseDiscoverySortDirection.Unspecified,
    };

    public static bool MapFlag(string value) => value switch
    {
        "Y" => true,
        "N" => false,
        _ => throw new DatabaseDiscoveryProviderException("MetadataQueryFailed", "读取 Oracle 目录元数据失败."),
    };
}

internal static class OracleIdentity
{
    public static string Schema(string name) => Key(["Schema", name]);
    public static string DatabaseObject(string owner, string name) => Key(["Object", owner, name]);
    public static string Column(string objectIdentity, string name) => Key(["Column", objectIdentity, name]);
    public static string Constraint(string kind, string objectIdentity, string name) => Key(["Constraint", kind, objectIdentity, name]);
    public static string Index(string objectIdentity, string name) => Key(["Index", objectIdentity, name]);
    public static string Sequence(string schemaIdentity, string name) => Key(["Sequence", schemaIdentity, name]);
    public static string Key(IEnumerable<string> components) =>
        string.Concat(components.Select(component => $"{component.Length.ToString(CultureInfo.InvariantCulture)}:{component}"));
}

internal static class OracleNativeTypeMapper
{
    public static readonly CanonicalNativeDataType SequenceType = new(
        DatabaseDiscoveryNativeTypeOrigin.ProviderImplicit,
        "NUMBER",
        null,
        "NUMBER",
        new CanonicalLengthMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null, null),
        null,
        new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Unknown, null),
        new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Unknown, null));

    public static CanonicalNativeDataType Map(OracleColumnRow row)
    {
        var character = row.DataType is "CHAR" or "VARCHAR2" or "NCHAR" or "NVARCHAR2";
        var raw = row.DataType is "RAW" or "LONG RAW";
        var lob = row.DataType is "CLOB" or "NCLOB" or "BLOB" or "BFILE" or "LONG";
        CanonicalLengthMeasure length;
        string? semantics = null;
        if (character)
        {
            var useCharacters = row.CharacterUsed == "C"
                || (row.CharacterUsed is null && row.DataType is "NCHAR" or "NVARCHAR2");
            var value = useCharacters ? row.CharacterLength : row.DataLength;
            if (value is null) length = new(DatabaseDiscoveryMeasureKind.Unknown, null, null);
            else length = new(DatabaseDiscoveryMeasureKind.Exact, value,
                useCharacters ? DatabaseDiscoveryLengthUnit.Characters : DatabaseDiscoveryLengthUnit.Bytes);
            semantics = useCharacters ? "CHAR" : "BYTE";
        }
        else if (raw)
        {
            length = row.DataLength is null
                ? new(DatabaseDiscoveryMeasureKind.Unknown, null, null)
                : new(DatabaseDiscoveryMeasureKind.Exact, row.DataLength, DatabaseDiscoveryLengthUnit.Bytes);
        }
        else if (lob)
        {
            length = new(DatabaseDiscoveryMeasureKind.Unbounded, null, null);
        }
        else
        {
            length = new(DatabaseDiscoveryMeasureKind.NotApplicable, null, null);
        }

        var numeric = row.DataType is "NUMBER" or "FLOAT" or "BINARY_FLOAT" or "BINARY_DOUBLE";
        var precision = numeric
            ? row.NumericPrecision is null
                ? new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Unknown, null)
                : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, row.NumericPrecision)
            : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null);
        var scale = row.DataType == "NUMBER"
            ? row.NumericScale is null
                ? new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Unknown, null)
                : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.Exact, row.NumericScale)
            : new CanonicalNumericMeasure(DatabaseDiscoveryMeasureKind.NotApplicable, null);

        return new CanonicalNativeDataType(
            DatabaseDiscoveryNativeTypeOrigin.CatalogDeclared,
            row.DataType,
            row.DataTypeOwner,
            Declaration(row, character, raw),
            length,
            semantics,
            precision,
            scale);
    }

    private static string Declaration(OracleColumnRow row, bool character, bool raw)
    {
        if (row.DataTypeOwner is not null) return $"{row.DataTypeOwner}.{row.DataType}";
        if (character)
        {
            var useCharacters = row.CharacterUsed == "C"
                || (row.CharacterUsed is null && row.DataType is "NCHAR" or "NVARCHAR2");
            var length = useCharacters ? row.CharacterLength : row.DataLength;
            return length is null ? row.DataType : $"{row.DataType}({length.Value.ToString(CultureInfo.InvariantCulture)} {(useCharacters ? "CHAR" : "BYTE")})";
        }
        if (raw && row.DataLength is not null)
            return $"{row.DataType}({row.DataLength.Value.ToString(CultureInfo.InvariantCulture)})";
        if (row.DataType == "NUMBER")
        {
            if (row.NumericPrecision is null) return "NUMBER";
            return row.NumericScale is null
                ? $"NUMBER({row.NumericPrecision.Value.ToString(CultureInfo.InvariantCulture)})"
                : $"NUMBER({row.NumericPrecision.Value.ToString(CultureInfo.InvariantCulture)},{row.NumericScale.Value.ToString(CultureInfo.InvariantCulture)})";
        }
        if (row.DataType == "FLOAT" && row.NumericPrecision is not null)
            return $"FLOAT({row.NumericPrecision.Value.ToString(CultureInfo.InvariantCulture)})";
        return row.DataType;
    }
}

internal interface IOracleDiscoveryCatalogReader
{
    Task<OracleCapabilityProbe> ReadCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);

    Task<OracleCatalogSnapshot> ReadCatalogAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        CancellationToken cancellationToken);
}

internal sealed record OracleTargetContext(
    string ServerVersion,
    string ProviderVersion,
    string ServiceName,
    string? ContainerName,
    string? DatabaseName);

internal sealed record OracleCapabilityProbe(
    OracleTargetContext Target,
    IReadOnlyList<CanonicalCapability> Capabilities);

internal sealed record OracleCatalogSnapshot(
    OracleTargetContext Target,
    string ConnectedPrincipal,
    IReadOnlyList<string> VisibleSchemas,
    IReadOnlyList<OracleObjectRow> Tables,
    IReadOnlyList<OracleObjectRow> Views,
    IReadOnlyList<OracleColumnRow> Columns,
    IReadOnlyList<OracleObjectCommentRow> ObjectComments,
    IReadOnlyList<OracleColumnCommentRow> ColumnComments,
    IReadOnlyList<OracleConstraintRow> Constraints,
    IReadOnlyList<OracleConstraintColumnRow> ConstraintColumns,
    IReadOnlyList<OracleIndexRow> Indexes,
    IReadOnlyList<OracleIndexColumnRow> IndexColumns,
    IReadOnlyList<OracleIndexExpressionRow> IndexExpressions,
    IReadOnlyList<OracleSequenceRow> Sequences);

internal sealed record OracleObjectRow(string Owner, string Name);
internal sealed record OracleObjectCommentRow(string Owner, string ObjectName, string ObjectType, string? Comment);
internal sealed record OracleColumnCommentRow(string Owner, string ObjectName, string ColumnName, string? Comment);
internal sealed record OracleColumnRow(
    string Owner,
    string ObjectName,
    string Name,
    int? SourceOrdinal,
    string DataType,
    string? DataTypeOwner,
    long? DataLength,
    long? CharacterLength,
    string? CharacterUsed,
    int? NumericPrecision,
    int? NumericScale,
    string Nullable,
    string? DefaultExpression);
internal sealed record OracleConstraintRow(
    string Owner,
    string Name,
    string ConstraintType,
    string ObjectName,
    string? ReferencedOwner,
    string? ReferencedConstraintName,
    string? DeleteRule,
    string? IndexOwner,
    string? IndexName);
internal sealed record OracleConstraintColumnRow(
    string Owner,
    string ConstraintName,
    string ObjectName,
    string ColumnName,
    int Position);
internal sealed record OracleIndexRow(
    string Owner,
    string Name,
    string TableOwner,
    string TableName,
    string IndexType,
    string Uniqueness);
internal sealed record OracleIndexColumnRow(
    string Owner,
    string Name,
    string TableOwner,
    string TableName,
    string ColumnName,
    int Position,
    string Descending);
internal sealed record OracleIndexExpressionRow(
    string Owner,
    string Name,
    string TableOwner,
    string TableName,
    string Expression,
    int Position);
internal sealed record OracleSequenceRow(
    string Owner,
    string Name,
    string MinimumValue,
    string MaximumValue,
    string IncrementValue,
    string CycleFlag,
    string OrderFlag,
    long? CacheSize);
