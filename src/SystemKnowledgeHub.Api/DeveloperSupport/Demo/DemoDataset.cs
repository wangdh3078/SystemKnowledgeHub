using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Features.Users.Application;
using SystemKnowledgeHub.Api.Features.Users.Application.Models;
using SystemKnowledgeHub.Api.Features.Users.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Application;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Application;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Application;
using SystemKnowledgeHub.Api.Features.Integrations.Application;
using SystemKnowledgeHub.Api.Features.Integrations.Application.Models;
using SystemKnowledgeHub.Api.Features.BusinessRules.Application;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application;
using SystemKnowledgeHub.Api.Features.Portal.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application;

namespace SystemKnowledgeHub.Api.DeveloperSupport.Demo;

public sealed record DemoManifest(int DatasetVersion, DateTimeOffset InitializedAt, string DatabasePath,
    long SentinelUserId, DateTimeOffset SentinelCreatedAt, Dictionary<string, long> ImportantEntityIds,
    string[] RecommendedRoutes, string[] SampleSearchTerms);

public static class DemoDatasetCommand
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    public static async Task<int> RunAsync(IServiceProvider services, DemoRuntime runtime, string[] args)
    {
        try
        {
            if (args.Length > 2 || (args.Length == 2 && args[1] != "--check-admin")) throw new InvalidOperationException("用法：seed-demo-data [--check-admin]");
            Directory.CreateDirectory(runtime.Root);
            Directory.CreateDirectory(Path.Combine(runtime.Root, "runtime"));
            await using var lease = new FileStream(Path.Combine(runtime.Root, "runtime/seed.lock"), FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
            await using var scope = services.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
            var marker = Path.Combine(runtime.Root, "demo-dataset.json");
            if (args.Length == 2)
            {
                if (!File.Exists(marker) || !File.Exists(runtime.DatabasePath)) throw new InvalidOperationException("Demo 数据尚未初始化。");
                var exists = await scope.ServiceProvider.GetRequiredService<UsableAdministratorResolver>().HasAnyAsync();
                Console.WriteLine(exists ? "Demo 已有可用管理员，保留现有账号。" : "Demo 尚无可用管理员，请运行安全 bootstrap。");
                return exists ? 0 : 3;
            }
            if (File.Exists(marker))
            {
                if (!File.Exists(runtime.DatabasePath)) throw new InvalidOperationException("Demo marker 与数据库不对应，拒绝重新写入。");
                var manifest = JsonSerializer.Deserialize<DemoManifest>(await File.ReadAllTextAsync(marker), Json) ?? throw new InvalidOperationException("Demo marker 无效。");
                if (manifest.DatasetVersion != 1 || !string.Equals(manifest.DatabasePath, runtime.DatabasePath, StringComparison.OrdinalIgnoreCase)
                    || !await db.Users.AnyAsync(user => user.Id == manifest.SentinelUserId && user.CreatedAt == manifest.SentinelCreatedAt))
                    throw new InvalidOperationException("Demo marker / canonical sentinel 不匹配；保留数据，请人工检查或显式 reset-demo。");
                Console.WriteLine("Demo 数据已经初始化；保留全部人工修改。");
                return 0;
            }
            await File.WriteAllTextAsync(Path.Combine(runtime.Root, "runtime/demo-owner.json"), JsonSerializer.Serialize(new { purpose = "SystemKnowledgeHub.PersistentDemo", root = runtime.Root }, Json));
            await db.Database.MigrateAsync();
            if (await db.Systems.AnyAsync() || await db.Users.AnyAsync() || await db.KnowledgeDocuments.AnyAsync())
                throw new InvalidOperationException("无完成 marker，但数据库已有数据。拒绝覆盖或追加，请显式 reset-demo。");
            var dataset = new DemoDataset(scope.ServiceProvider);
            await dataset.Seed();
            await dataset.Validate();
            var sentinel = await db.Users.SingleAsync(user => user.Id == dataset.Ids["seedUser"]);
            var result = new DemoManifest(1, DateTimeOffset.UtcNow, runtime.DatabasePath, sentinel.Id, sentinel.CreatedAt, dataset.Ids,
                ["/dashboard", $"/systems/{dataset.Ids["MES"]}", $"/database/{dataset.Ids["LOT"]}", $"/knowledge-documents/{dataset.Ids["SOP"]}", "/analysis", "/portal-management", "/portal"],
                ["Lot Track", "Track In", "STATE_FLAG", "MES.LOT", "Equipment", "Hold", "EAP"]);
            var pending = marker + ".pending";
            await File.WriteAllTextAsync(pending, JsonSerializer.Serialize(result, Json));
            File.Move(pending, marker);
            Console.WriteLine($"Demo 数据初始化成功：{runtime.DatabasePath}");
            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine($"Demo 初始化拒绝/失败：{exception.Message}（持久数据保留；不会自动重置）");
            return 1;
        }
    }
}

internal sealed partial class DemoDataset(IServiceProvider services)
{
    public Dictionary<string, long> Ids { get; } = new(StringComparer.Ordinal);
    private KnowledgeHubDbContext Db => services.GetRequiredService<KnowledgeHubDbContext>();
    private T Service<T>() where T : notnull => services.GetRequiredService<T>();
    private static readonly CancellationToken Ct = CancellationToken.None;
    private const string ActorName = "演示 · 知识整理员";
    private CanonicalCreator Creator => new(Ids["seedUser"], ActorName);
    private static Exception Failed(object result) => new InvalidOperationException(JsonSerializer.Serialize(result, DemoDatasetCommand.Json));
    private static T Need<T>(T? response, object result) where T : class => response ?? throw Failed(result);
    private string Token(long version) => Service<SystemKnowledgeHub.Api.Persistence.Concurrency.ConcurrencyTokenCodec>().Encode(version);
    public async Task Seed()
    {
        foreach (var (key, name, level) in new[] { ("seedUser", ActorName, AccessLevel.Editor), ("viewer", "演示 · Viewer", AccessLevel.Viewer), ("editor", "演示 · Editor", AccessLevel.Editor) })
        {
            var result = await Service<UserService>().CreateUser(new("DEMO-" + key, name, null, "演示知识团队", "演示领域维护人", level, [], new("none", null, null, null, null), new(ActorName, "Demo 初始化")), Ct);
            Ids[key] = Need(result.Response, result).Id;
        }
        foreach (var (key, lifecycle) in new[] { ("MES", "Running"), ("EAP", "Maintaining"), ("OCS", "InDevelopment") })
        {
            var result = await Service<SystemService>().CreateSystem(new("DEMO_" + key, "演示 · " + key, "制造系统", lifecycle, "仅供人工检查的 " + key + " 演示系统", new(ActorName, "演示"), Creator), Ct);
            Ids[key] = Need(result.Response, result).Id;
        }
        var functions = new[] { ("TrackIn", "Lot Track In", "MES"), ("TrackOut", "Lot Track Out", "MES"), ("Hold", "Hold Lot", "MES"), ("Release", "Lot Release", "MES"), ("Communication", "Equipment Communication", "EAP"), ("Recipe", "Recipe Download", "EAP"), ("Dispatch", "Job Dispatch", "OCS") };
        foreach (var (key, title, system) in functions)
        {
            var result = await Service<BusinessFunctionService>().CreateBusinessFunction(new(Ids[system], "DEMO_" + key, "演示 · " + title, "业务流程", title + " 演示行为", "Unknown", new(ActorName, "演示"), Creator), Ct);
            Ids[key] = Need(result.Response, result).Id;
        }
        var database = Service<DatabaseKnowledgeService>();
        foreach (var system in new[] { "MES", "EAP" })
        {
            var result = await database.CreateDatabaseSource(new(Ids[system], system + "DB", "Oracle", "Demo", null, null, system + "DB", "演示 · 仅知识元数据，无真实连接", true, new(ActorName, "演示"), Creator), Ct);
            Ids[system + "DB"] = Need(result.Response, result).Id;
        }
        foreach (var (name, system) in new[] { ("LOT", "MES"), ("EQUIPMENT", "MES"), ("JOB", "MES"), ("PRODUCT", "MES"), ("RECIPE", "EAP"), ("EVENT", "EAP") })
        {
            var result = await database.RegisterDatabaseObject(new(Ids[system + "DB"], system, name, "Table", name == "LOT" ? 12000 : 300, "ReadWrite", [name + "_ID"], [], "演示 · " + system + "." + name + " 元数据，估计行数为示意", new(ActorName, "演示"), Creator), Ct);
            var objectId = Need(result.Response, result).Id; Ids[name] = objectId;
            var columns = name == "LOT" ? new[] { "LOT_ID", "PRODUCT_ID", "STATE_FLAG", "EQUIPMENT_ID", "CREATED_AT" } : new[] { name + "_ID", "NAME", "STATE_FLAG", "UPDATED_AT", "DESCRIPTION" };
            for (var index = 0; index < columns.Length; index++)
            {
                var entity = await Db.DatabaseObjects.SingleAsync(item => item.Id == objectId);
                var column = await database.RegisterDatabaseColumn(new(objectId, index + 1, columns[index], columns[index].EndsWith("_AT") ? "TIMESTAMP" : "VARCHAR2(100)", index > 0, null, "演示字段", "演示 · " + columns[index], new(ActorName, "演示"), Token(entity.Version), Creator), Ct);
                Ids[name + "." + columns[index]] = Need(column.Response, column).Column.Id;
            }
            var current = await Db.DatabaseObjects.SingleAsync(item => item.Id == objectId);
            var updated = await database.UpdateDatabaseObjectKnowledge(new(objectId, current.BusinessDescription, current.EstimatedRows, "ReadWrite", [columns[0]], new(ActorName, "演示"), Token(current.Version)), Ct);
            Need(updated.Response, updated);
        }
        foreach (var name in new[] { "LOT", "EQUIPMENT", "JOB" })
            foreach (var (value, meaning) in new[] { ("10", "Waiting"), ("20", "Running"), ("30", "Hold"), ("40", "Complete") })
            {
                var column = await Db.DatabaseColumns.SingleAsync(item => item.Id == Ids[name + ".STATE_FLAG"]);
                var result = await database.AddColumnKnownValue(new(column.Id, value, "演示 · " + meaning, int.Parse(value), new(ActorName, "演示"), Token(column.Version)), Ct); Need(result.Response, result);
            }
        var ruleNames = new[] { "Lot Track In 前置条件", "Hold Lot 禁止 Track In", "Equipment 必须 Available", "Product Route 必须存在", "EAP 回报幂等校验" };
        for (var i = 0; i < ruleNames.Length; i++)
        {
            var result = await Service<BusinessRuleService>().Create(new(Ids["MES"], "演示 · " + ruleNames[i], "演示业务约束：" + ruleNames[i], "Lot 与 Equipment 状态满足条件", "允许继续，否则返回演示错误", [new("LOT_ID", "演示批次"), new("STATE_FLAG", "演示状态")], new(ActorName, "演示"), Creator), Ct);
            Ids["rule" + i] = Need(result.Response, result).Id;
        }
        foreach (var (key, source, target, title) in new[] { ("MES_EAP", "MES", "EAP", "Lot Track In"), ("EAP_OCS", "EAP", "OCS", "Equipment Command"), ("MES_OCS", "MES", "OCS", "Job Dispatch"), ("EAP_MES", "EAP", "MES", "Equipment 回报") })
        {
            var result = await Service<IntegrationService>().Create(new(new("演示 · " + source + " → " + target + " " + title, "HttpApi", new(Ids[source], "演示 · " + source), new(Ids[target], "演示 · " + target), "OneWay", "演示接口，无真实远程调用", JsonSerializer.SerializeToElement(new { url = "https://demo.invalid/api/track-in", method = "POST" }), null, null), new(ActorName, "演示"), Creator), Ct);
            var integration = (IntegrationWriteResponse)Need(result.Response, result); Ids[key] = integration.Id;
            var contract = await Service<IntegrationService>().ReplaceContractFields(new(integration.Id, [new(1, "lotId", "string", true, "演示批次", "DEMO-LOT-001"), new(2, "equipmentId", "string", true, "演示设备", "DEMO-EQP-01")], new(ActorName, "演示"), integration.ConcurrencyToken), Ct); Need(contract.Response, contract);
        }
        await SeedKnowledge();
        await SeedUnknownItems();
        await SeedPortal();
    }
    public async Task Validate()
    {
        var connection = Db.Database.GetDbConnection(); await Db.Database.OpenConnectionAsync();
        await using (var command = connection.CreateCommand()) { command.CommandText = "PRAGMA foreign_key_check"; await using var reader = await command.ExecuteReaderAsync(); if (await reader.ReadAsync()) throw new InvalidOperationException("Demo 外键检查失败。"); }
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "SELECT COUNT(*) FROM knowledge_documents_fts WHERE rowid = $id AND knowledge_documents_fts MATCH 'Track'";
            var parameter = command.CreateParameter(); parameter.ParameterName = "$id"; parameter.Value = Ids["SOP"]; command.Parameters.Add(parameter);
            if (Convert.ToInt64(await command.ExecuteScalarAsync()) != 1) throw new InvalidOperationException("Demo 主 SOP 的 FTS 索引缺失。");
        }
        var tree = await Service<AnalysisWorkspaceService>().GetTree(Ct);
        var portal = await Service<PortalQueries>().GetTreeAsync(Ct);
        foreach (var id in await Db.KnowledgeRelations.Select(item => item.Id).ToListAsync())
            if ((await Service<RelationshipQueries>().GetRelationshipDetail(id, Ct)).Response is null) throw new InvalidOperationException("Demo 关系端点无法解析。");
        if (tree.Items.Count < 15 || portal.Response is not { Total: > 0 } || !await Db.Systems.AnyAsync(item => item.Id == Ids["MES"]) || !await Db.DatabaseObjects.AnyAsync(item => item.Id == Ids["LOT"]) || !await Db.KnowledgeDocumentRevisions.AnyAsync(item => item.RevisionNumber >= 3)) throw new InvalidOperationException("Demo 关键数据不完整。");
        Console.WriteLine($"Demo summary：系统 {await Db.Systems.CountAsync()}；文档 {await Db.KnowledgeDocuments.CountAsync()}；关系 {await Db.KnowledgeRelations.CountAsync()}（端点已解析）；Analysis {tree.Items.Count}；FK=0；主 SOP FTS 有效；Portal 已发布树可读。");
    }
}
