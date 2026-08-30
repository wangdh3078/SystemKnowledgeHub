using System.Text.Json;
using System.Text.Json.Serialization;
using SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseDiscovery.Application;

public sealed record DatabaseDiscoveryDifferenceCounts(int Added, int Changed, int MissingFromSource, int Unchanged);

public sealed record PreparedDatabaseDiscoveryDifference(
    IReadOnlyList<DatabaseDiscoveryDifferenceEntry> Entries,
    IReadOnlyList<DerivedDatabaseDiscoveryDifferenceEntry> UnchangedEntries,
    DatabaseDiscoveryDifferenceCounts Counts,
    string SummaryCountsJson,
    string ContentSha256,
    string? ErrorCode,
    string? ErrorSummary)
{
    public bool Succeeded => ErrorCode is null;
}

public sealed record DerivedDatabaseDiscoveryDifferenceEntry(
    DatabaseDiscoveryEntityKind EntityKind,
    string LogicalIdentity,
    string? ParentLogicalIdentity,
    string DisplayName,
    string ContentJson);

public sealed class DatabaseDiscoveryDiffService(CanonicalSnapshotService canonical)
{
    private const int MaximumEntryPayloadLength = 65536;
    private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

    public PreparedDatabaseDiscoveryDifference Compare(
        CanonicalDatabaseDiscoverySnapshot? baseline,
        CanonicalDatabaseDiscoverySnapshot target)
    {
        var baseItems = baseline is null
            ? new Dictionary<string, CanonicalDifferenceItem>(StringComparer.Ordinal)
            : Enumerate(baseline).ToDictionary(ItemKey, StringComparer.Ordinal);
        var targetItems = Enumerate(target).ToDictionary(ItemKey, StringComparer.Ordinal);
        var entries = new List<DatabaseDiscoveryDifferenceEntry>();
        var unchanged = new List<DerivedDatabaseDiscoveryDifferenceEntry>();

        foreach (var key in baseItems.Keys.Union(targetItems.Keys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            var hasBase = baseItems.TryGetValue(key, out var before);
            var hasTarget = targetItems.TryGetValue(key, out var after);
            if (!hasBase)
            {
                if (after!.ContentJson.Length > MaximumEntryPayloadLength) return LimitFailure();
                entries.Add(ToDurable(after, DatabaseDiscoveryDifferenceState.Added, null, after.ContentJson));
            }
            else if (!hasTarget)
            {
                if (before!.ContentJson.Length > MaximumEntryPayloadLength) return LimitFailure();
                entries.Add(ToDurable(before, DatabaseDiscoveryDifferenceState.MissingFromSource, before.ContentJson, null));
            }
            else if (!string.Equals(before!.ContentJson, after!.ContentJson, StringComparison.Ordinal))
            {
                if (before.ContentJson.Length > MaximumEntryPayloadLength || after.ContentJson.Length > MaximumEntryPayloadLength)
                    return LimitFailure();
                entries.Add(ToDurable(after, DatabaseDiscoveryDifferenceState.Changed, before.ContentJson, after.ContentJson));
            }
            else
            {
                unchanged.Add(new(after.EntityKind, after.LogicalIdentity, after.ParentLogicalIdentity, after.DisplayName, after.ContentJson));
            }
        }

        var orderedEntries = entries
            .OrderBy(item => item.EntityKind)
            .ThenBy(item => item.LogicalIdentity, StringComparer.Ordinal)
            .ToArray();
        var orderedUnchanged = unchanged
            .OrderBy(item => item.EntityKind)
            .ThenBy(item => item.LogicalIdentity, StringComparer.Ordinal)
            .ToArray();
        var counts = new DatabaseDiscoveryDifferenceCounts(
            orderedEntries.Count(item => item.State == DatabaseDiscoveryDifferenceState.Added),
            orderedEntries.Count(item => item.State == DatabaseDiscoveryDifferenceState.Changed),
            orderedEntries.Count(item => item.State == DatabaseDiscoveryDifferenceState.MissingFromSource),
            orderedUnchanged.Length);
        var summary = JsonSerializer.Serialize(counts, JsonOptions);
        var hashPayload = JsonSerializer.Serialize(new
        {
            AlgorithmVersion = 1,
            Counts = counts,
            Entries = orderedEntries.Select(item => new
            {
                item.EntityKind,
                item.LogicalIdentity,
                item.ParentLogicalIdentity,
                item.DisplayName,
                item.State,
                item.BeforeJson,
                item.AfterJson,
            }),
        }, JsonOptions);
        return new(orderedEntries, orderedUnchanged, counts, summary, canonical.HashUtf8(hashPayload), null, null);
    }

    public IReadOnlyList<DerivedDatabaseDiscoveryDifferenceEntry> DeriveUnchanged(
        CanonicalDatabaseDiscoverySnapshot baseline,
        CanonicalDatabaseDiscoverySnapshot target) =>
        Compare(baseline, target).UnchangedEntries;

    private IEnumerable<CanonicalDifferenceItem> Enumerate(CanonicalDatabaseDiscoverySnapshot snapshot)
    {
        foreach (var item in snapshot.Schemas)
            yield return Item(DatabaseDiscoveryEntityKind.Schema, item.LogicalIdentity, null, item.Name, item);
        foreach (var item in snapshot.Objects)
            yield return Item(DatabaseDiscoveryEntityKind.DatabaseObject, item.LogicalIdentity, item.SchemaLogicalIdentity, $"{item.SchemaName}.{item.Name}", item);
        foreach (var item in snapshot.Columns)
            yield return Item(DatabaseDiscoveryEntityKind.Column, item.LogicalIdentity, item.ParentObjectLogicalIdentity, item.Name, item);
        foreach (var item in snapshot.PrimaryKeys)
            yield return Item(DatabaseDiscoveryEntityKind.PrimaryKey, item.LogicalIdentity, item.ParentObjectLogicalIdentity, item.Name, item);
        foreach (var item in snapshot.ForeignKeys)
            yield return Item(DatabaseDiscoveryEntityKind.ForeignKey, item.LogicalIdentity, item.ParentObjectLogicalIdentity, item.Name, item);
        foreach (var item in snapshot.UniqueConstraints)
            yield return Item(DatabaseDiscoveryEntityKind.UniqueConstraint, item.LogicalIdentity, item.ParentObjectLogicalIdentity, item.Name, item);
        foreach (var item in snapshot.Indexes)
            yield return Item(DatabaseDiscoveryEntityKind.Index, item.LogicalIdentity, item.ParentObjectLogicalIdentity, item.Name, item);
        foreach (var item in snapshot.Sequences)
            yield return Item(DatabaseDiscoveryEntityKind.Sequence, item.LogicalIdentity, item.SchemaLogicalIdentity, item.Name, item);
    }

    private CanonicalDifferenceItem Item<T>(
        DatabaseDiscoveryEntityKind kind,
        string identity,
        string? parentIdentity,
        string displayName,
        T value) => new(kind, identity, parentIdentity, displayName, canonical.SerializeEntity(value));

    private static string ItemKey(CanonicalDifferenceItem item) => $"{(int)item.EntityKind:D2}:{item.LogicalIdentity}";

    private static DatabaseDiscoveryDifferenceEntry ToDurable(
        CanonicalDifferenceItem item,
        DatabaseDiscoveryDifferenceState state,
        string? before,
        string? after) => new()
        {
            EntityKind = item.EntityKind,
            LogicalIdentity = item.LogicalIdentity,
            ParentLogicalIdentity = item.ParentLogicalIdentity,
            DisplayName = item.DisplayName,
            State = state,
            BeforeJson = before,
            AfterJson = after,
        };

    private static PreparedDatabaseDiscoveryDifference LimitFailure() => new(
        [], [], new(0, 0, 0, 0), string.Empty, string.Empty,
        "LimitExceeded", "差异条目超过允许的持久化大小限制。");

    private static JsonSerializerOptions CreateJsonOptions()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        options.Converters.Add(new JsonStringEnumConverter());
        return options;
    }

    private sealed record CanonicalDifferenceItem(
        DatabaseDiscoveryEntityKind EntityKind,
        string LogicalIdentity,
        string? ParentLogicalIdentity,
        string DisplayName,
        string ContentJson);
}
