using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application;
using SystemKnowledgeHub.Api.Features.KnowledgeDocuments.Application.Models;
using SystemKnowledgeHub.Api.Features.Evidence.Application;
using SystemKnowledgeHub.Api.Features.Evidence.Application.Models;
using SystemKnowledgeHub.Api.Features.Relationships.Application;
using SystemKnowledgeHub.Api.Features.Relationships.Application.Models;
using SystemKnowledgeHub.Api.Features.StatusProgression.Application;
using SystemKnowledgeHub.Api.Features.AnalysisWorkspace.Application;
using SystemKnowledgeHub.Api.Features.Attachments.Application;
using SystemKnowledgeHub.Api.Features.Users.Domain;

namespace SystemKnowledgeHub.Api.DeveloperSupport.Demo;

internal sealed partial class DemoDataset
{
    private KnowledgeDocumentAuthor Author => new(Ids["seedUser"], ActorName);
    private PersonSnapshotCommand Person => new(ActorName, "演示领域维护人", DateTimeOffset.UtcNow, "演示团队", null, "Demo", "仅用于人工检查");
    private async Task<KnowledgeDocumentDetailResponse> Document(string key, string type, string title, string body)
    {
        var result = await Service<KnowledgeDocumentService>().Create(new(type, "演示 · " + title, "Demo / Manual Acceptance：非真实生产知识", body, Author), Ct);
        var document = Need(result.Response, result); Ids[key] = document.Id; return document;
    }
    private async Task<KnowledgeDocumentDetailResponse> SaveDocument(long id, string body, IReadOnlyList<long>? files = null)
    {
        var current = await Service<KnowledgeDocumentQueries>().GetDetail(id, Ct) ?? throw new InvalidOperationException("Demo 文档缺失");
        var result = await Service<KnowledgeDocumentService>().UpdateContent(new(id, current.Title, current.Summary, body, "演示 · 补充操作校验", current.ConcurrencyToken, files ?? [], Author), Ct);
        return Need(result.Response, result);
    }
    private async Task Lifecycle(long id, string status)
    {
        var current = await Db.KnowledgeDocuments.SingleAsync(item => item.Id == id);
        var result = await Service<KnowledgeDocumentService>().UpdateLifecycle(new(id, status, Token(current.Version), Author), Ct); Need(result.Response, result);
    }
    private async Task<long> Evidence(string type, string subject, long id)
    {
        var result = await Service<EvidenceService>().AddEvidence(new(type, new(subject, id), null, "演示 · " + type + " / Lot Track In",
            type == "Sql" ? "SELECT LOT_ID, STATE_FLAG FROM MES.LOT WHERE LOT_ID = :lotId" : "demo://acceptance/lot-track-in",
            JsonSerializer.SerializeToElement(new { sample = "演示样本；不执行 SQL，不访问远程资源" }), "演示 · EAP 回报与 MES.LOT 状态样本", "用于人工检查来源、关联与可信状态", "High", Person), Ct);
        var response = (AddEvidenceResponse)Need(result.Response, result); return response.Id;
    }
    private async Task<long> Confirmation(string subject, long id, long? revision = null, long? replaces = null)
    {
        var result = await Service<EvidenceService>().AddHumanConfirmation(new(Ids["seedUser"], new(subject, id), revision, null, null, "InSystem", DateTimeOffset.UtcNow,
            "演示确认：已检查 Lot Track In 示例及预期字段含义。", "为人工验收展示当前确认覆盖。", "非真实领域验收", replaces), Ct);
        return ((AddEvidenceResponse)Need(result.Response, result)).Id;
    }
    private async Task Withdraw(long id)
    {
        var item = await Db.Evidence.SingleAsync(item => item.Id == id);
        var result = await Service<EvidenceService>().WithdrawHumanConfirmation(new(id, Ids["seedUser"], "演示 · 原确认范围需要纠正", Token(item.Version)), Ct); Need(result.Response, result);
    }
    private async Task<long> Relation(string source, long sourceId, string type, string target, long targetId)
    {
        var result = await Service<RelationshipService>().Add(new(new(source, sourceId), type, new(target, targetId), "演示 · 明确有向知识关联", new(ActorName, "演示")), Ct);
        return ((AddRelationshipResponse)Need(result.Response, result)).Id;
    }
    private async Task Status(string type, long id, string status)
    {
        long version = type switch
        {
            "System" => (await Db.Systems.SingleAsync(item => item.Id == id)).Version,
            "KnowledgeDocument" => (await Db.KnowledgeDocuments.SingleAsync(item => item.Id == id)).Version,
            _ => throw new InvalidOperationException("Demo status target unsupported")
        };
        var result = await Service<KnowledgeStatusService>().ChangeKnowledgeStatus(new(new(type, id), status, "演示 · 根据当前证据推进", new(ActorName, "演示领域维护人", DateTimeOffset.UtcNow), Token(version)), Ct); Need(result.Response, result);
    }
    private async Task<long> Folder(string key, string title, long? parent)
    {
        var analysis = Service<AnalysisWorkspaceService>(); var tree = await analysis.GetTree(Ct);
        var result = await analysis.CreateFolder(new(parent, title, tree.TreeConcurrencyToken), Ct); Ids[key] = result.Node!.Id; return result.Node.Id;
    }
    private async Task Placement(string key, long docId, long? parent)
    {
        var analysis = Service<AnalysisWorkspaceService>(); var tree = await analysis.GetTree(Ct);
        var result = await analysis.AddPlacement(new(parent, docId, tree.TreeConcurrencyToken), Ct); Ids[key] = result.Node!.Id;
    }
    private async Task SeedKnowledge()
    {
        foreach (var (key, type, title) in new[]{("REQ","Requirement","Lot Track In 业务要求"),("SPEC","Specification","Lot Track In 接口规格"),("TEST","TestCase","Lot Track In 正常流程测试"),
            ("SOP","Sop","Lot Track In 操作说明"),("OLD","Troubleshooting","旧 Track In 失败排查"),("ARTICLE","KnowledgeArticle","MES Lot 状态说明"),
            ("NOTE","DesignNote","Lot Track In 设计分析"),("REQ_GAP","Requirement","Lot Release 新业务要求（缺规格）"),("REQ_NO_TEST","Requirement","Hold Lot 业务要求（缺测试）"),
            ("SPEC_NO_TEST","Specification","Hold Lot 接口规格"),("HOLD_NOTE","DesignNote","Hold Lot 分析"),("LOT_NOTE","DesignNote","MES.LOT 数据库分析"),("API_NOTE","DesignNote","MES → EAP 接口分析")})
            await Document(key, type, title, "# 演示 · " + title + "\n\n围绕 Lot Track In、Equipment、Hold、EAP 与 STATE_FLAG 的人工检查内容。\n\n所有编号与接口均为演示。\n\n- 输入：DEMO-LOT-001\n- 预期：等待状态转为 Running；异常进入人工调查。");
        // Revision 1 is confirmed; later canonical saves deliberately demonstrate changed coverage.
        Ids["changedConfirmation"] = await Confirmation("KnowledgeDocument", Ids["SOP"], 1);
        var attachments = Service<AttachmentService>();
        foreach (var (key, name, mime, bytes) in new[] { ("png", "demo-lot-flow.png", "image/png", DemoAssets.Png()), ("pdf", "demo-sop.pdf", "application/pdf", DemoAssets.Pdf()) })
        {
            using var stream = new MemoryStream(bytes);
            var result = await attachments.Upload(Ids["SOP"], name, mime, bytes.Length, stream, Author, Ct); Ids[key] = Need(result.Response, result).AttachmentId;
        }
        await SaveDocument(Ids["SOP"], SopBody + "\n\n## Revision 2\n增加 Equipment Available 校验。", [Ids["pdf"]]);
        await SaveDocument(Ids["SOP"], SopBody + "\n\n## Revision 3\n增加 Hold Lot 拒绝逻辑和人工恢复步骤。", [Ids["pdf"]]);
        await SaveDocument(Ids["NOTE"], "# 演示 · Lot Track In 设计分析\n\n## Revision 2\n明确 EAP 延迟回报、幂等请求和超时补偿边界。\n\n```mermaid\nflowchart LR\nLot --> MES\nMES --> EAP\nEAP --> Equipment\n```\n");
        foreach (var key in new[] { "REQ", "SPEC", "TEST", "SOP", "ARTICLE", "OLD", "REQ_NO_TEST", "SPEC_NO_TEST" }) await Lifecycle(Ids[key], "Published");
        await Lifecycle(Ids["OLD"], "Archived");
        Ids["traceRelation"] = await Relation("KnowledgeDocument", Ids["REQ"], "SpecifiedBy", "KnowledgeDocument", Ids["SPEC"]);
        Ids["verifiedRelation"] = await Relation("KnowledgeDocument", Ids["REQ"], "VerifiedBy", "KnowledgeDocument", Ids["TEST"]);
        await Relation("KnowledgeDocument", Ids["SPEC"], "VerifiedBy", "KnowledgeDocument", Ids["TEST"]);
        await Relation("KnowledgeDocument", Ids["REQ_NO_TEST"], "SpecifiedBy", "KnowledgeDocument", Ids["SPEC_NO_TEST"]);
        await Relation("KnowledgeDocument", Ids["REQ"], "AppliesTo", "BusinessFunction", Ids["TrackIn"]);
        await Relation("KnowledgeDocument", Ids["SOP"], "References", "KnowledgeDocument", Ids["REQ"]);
        await Relation("KnowledgeDocument", Ids["NOTE"], "References", "KnowledgeDocument", Ids["SPEC"]);
        foreach (var key in new[] { "REQ", "SPEC", "TEST", "SOP", "ARTICLE", "NOTE", "REQ_GAP", "REQ_NO_TEST", "SPEC_NO_TEST", "HOLD_NOTE", "LOT_NOTE", "API_NOTE" }) await Relation("KnowledgeDocument", Ids[key], "Documents", "System", Ids["MES"]);
        await Relation("KnowledgeDocument", Ids["ARTICLE"], "Documents", "DatabaseObject", Ids["LOT"]);
        await Relation("KnowledgeDocument", Ids["LOT_NOTE"], "Documents", "DatabaseObject", Ids["LOT"]);
        await Relation("KnowledgeDocument", Ids["SOP"], "AppliesTo", "DatabaseObject", Ids["LOT"]);
        await Relation("KnowledgeDocument", Ids["API_NOTE"], "Documents", "Integration", Ids["MES_EAP"]);
        foreach (var (type, target, key) in new[] { ("Reads", "DatabaseObject", "LOT"), ("Writes", "DatabaseObject", "LOT"), ("UsesField", "DatabaseColumn", "LOT.STATE_FLAG"), ("AppliesRule", "BusinessRule", "rule0"), ("AppliesRule", "BusinessRule", "rule1"), ("UsesIntegration", "Integration", "MES_EAP"), ("Calls", "BusinessFunction", "Hold") })
            await Relation("BusinessFunction", Ids["TrackIn"], type, target, Ids[key]);
        await Relation("System", Ids["MES"], "DependsOn", "System", Ids["EAP"]);
        await Relation("System", Ids["MES"], "PublishesVia", "Integration", Ids["MES_EAP"]);
        foreach (var (type, key) in new[] { ("System", "MES"), ("System", "EAP"), ("BusinessFunction", "TrackIn"), ("DatabaseObject", "LOT"), ("DatabaseColumn", "LOT.STATE_FLAG"), ("BusinessRule", "rule0"), ("Integration", "MES_EAP"), ("KnowledgeDocument", "SOP"), ("KnowledgeDocument", "REQ"), ("KnowledgeDocument", "ARTICLE"), ("KnowledgeRelation", "traceRelation") })
            await Evidence("ExistingDocument", type, Ids[key]);
        foreach (var type in new[] { "CodeReference", "Sql", "DatabaseSample", "DatabaseComment", "Api", "MqMessage" }) await Evidence(type, "DatabaseObject", Ids["LOT"]);
        Ids["activeConfirmation"] = await Confirmation("System", Ids["MES"]);
        Ids["withdrawnConfirmation"] = await Confirmation("KnowledgeDocument", Ids["ARTICLE"], 1); await Withdraw(Ids["withdrawnConfirmation"]);
        var old = await Confirmation("KnowledgeDocument", Ids["REQ"], 1); await Withdraw(old);
        Ids["replacementConfirmation"] = await Confirmation("KnowledgeDocument", Ids["REQ"], 1, old);
        await Status("System", Ids["MES"], "Inferred"); await Status("System", Ids["MES"], "Confirmed"); await Status("System", Ids["EAP"], "Inferred");
        await Status("KnowledgeDocument", Ids["REQ"], "Inferred"); await Status("KnowledgeDocument", Ids["REQ"], "Confirmed"); await Status("KnowledgeDocument", Ids["ARTICLE"], "Inferred");
        var relation = await Db.KnowledgeRelations.SingleAsync(item => item.Id == Ids["traceRelation"]);
        var relationStatus = await Service<RelationshipService>().ChangeStatus(new(relation.Id, "Inferred", "演示 · 有依据的规格关联", new(ActorName, "演示", DateTimeOffset.UtcNow), Token(relation.Version)), Ct); Need(relationStatus.Response, relationStatus);
        await Confirmation("KnowledgeRelation", relation.Id);
        relationStatus = await Service<RelationshipService>().ChangeStatus(new(relation.Id, "Confirmed", "演示 · 已确认规格关联", new(ActorName, "演示", DateTimeOffset.UtcNow), Token(relation.Version)), Ct); Need(relationStatus.Response, relationStatus);
        await Evidence("ExistingDocument", "KnowledgeRelation", Ids["verifiedRelation"]);
        var inferredRelation = await Db.KnowledgeRelations.SingleAsync(item => item.Id == Ids["verifiedRelation"]);
        relationStatus = await Service<RelationshipService>().ChangeStatus(new(inferredRelation.Id, "Inferred", "演示 · 待人工确认的测试关联", new(ActorName, "演示", DateTimeOffset.UtcNow), Token(inferredRelation.Version)), Ct); Need(relationStatus.Response, relationStatus);
        await Folder("analysisMES", "MES 分析", null); await Folder("analysisOverview", "系统概览", Ids["analysisMES"]); await Folder("analysisFlow", "业务流程", Ids["analysisMES"]);
        await Folder("analysisDb", "数据库分析", Ids["analysisMES"]); await Folder("analysisApi", "接口", Ids["analysisMES"]); await Folder("analysisHistory", "历史分析", null);
        await Placement("analysisRequirement", Ids["REQ"], Ids["analysisFlow"]); await Placement("analysisArticle", Ids["ARTICLE"], Ids["analysisOverview"]); await Placement("analysisSop", Ids["SOP"], Ids["analysisFlow"]);
        foreach (var (key, parent) in new[] { ("NOTE", "analysisFlow"), ("HOLD_NOTE", "analysisFlow"), ("LOT_NOTE", "analysisDb"), ("API_NOTE", "analysisApi"), ("OLD", "analysisHistory") }) await Placement("node" + key, Ids[key], Ids[parent]);
        var analysis = Service<AnalysisWorkspaceService>(); var snapshot = await analysis.GetTree(Ct);
        var created = await analysis.CreateDocument(new(Ids["analysisFlow"], snapshot.TreeConcurrencyToken, "DesignNote", "演示 · 原子创建的 Lot Track In 分析", "从 Analysis 创建", "# 演示 · 原子创建\n\n文档与 placement 同时创建。"), Author, Ct); Ids["atomicAnalysisNode"] = created.Node!.Id;
        var deleted = await Document("deleted", "DesignNote", "已删除文档占位", "演示 · 此正文不应通过 current tree 暴露。");
        await Placement("unavailableNode", deleted.Id, Ids["analysisHistory"]);
        var deletion = await Service<KnowledgeDocumentDeleteService>().DeleteKnowledgeDocument(deleted.Id, deleted.ConcurrencyToken, new(Ids["seedUser"], ActorName, AccessLevel.Editor), Ct);
        if (deletion.Failure != SystemKnowledgeHub.Api.Features.SoftDelete.Application.SoftDeleteFailure.None) throw Failed(deletion);
    }
    private string SopBody => """
# 演示 · Lot Track In 操作说明

本说明用于系统知识中心的长期人工检查，**不是生产作业指导书**。所有批次、Equipment 和接口均为演示。

## 操作前检查

- 核对 MES.LOT 与 PRODUCT_ID。
- 识别 STATE_FLAG：10 Waiting、20 Running、30 Hold、40 Complete。
- 检查 Equipment 通信状态和 EAP 回报时间。

1. 输入 DEMO-LOT-001。
2. 核对 Product Route。
3. 确认设备可用后发出 Track In 请求。
4. 等待 EAP 回报，核对 MES 状态。

- [x] 准备演示批次
- [x] 验证设备
- [ ] 用户自行检查异常分支

> Hold Lot 必须先调查原因，不能直接重试 Track In。

| 字段 | 含义 | 示例 |
| --- | --- | --- |
| LOT_ID | 批次编号 | DEMO-LOT-001 |
| STATE_FLAG | 状态编码 | 20 |
| EQUIPMENT_ID | 设备标识 | DEMO-EQP-01 |

```sql
-- 仅为文本示例，不执行，不连接数据库。
SELECT LOT_ID, STATE_FLAG FROM MES.LOT WHERE LOT_ID = :lotId;
```

```mermaid
flowchart LR
    Lot --> MES
    MES --> EAP
    EAP --> Equipment
```

[返回知识内容](/knowledge-documents)

## 异常调查与恢复

如果状态迟迟未更新，先检查 EAP 回报延迟，再核对请求幂等标识。用户可以在演示待确认事项中查看 Finding、Evidence、Resolution 和已应用的 Knowledge Update。记录调查结论时，必须区分样本事实与推断。

恢复前再次检查 Hold 状态，避免把等待处理的批次当作正常执行批次。请在相关知识中查看 Requirement、Specification 和 TestCase 的有向关联，以及与当前修订不同步的人工确认覆盖。

## 附件与修订

下图和 PDF 均为无敏感信息的小型演示资源。用户可检查 Markdown 图片、附件预览、下载、历史修订引用及匿名 Portal 的 page-scoped 附件。
""" + $"\n\n![演示流程图片](attachment:{Ids["png"]})\n";
}
