using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application;
using SystemKnowledgeHub.Api.Features.UnknownItems.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Application;
using SystemKnowledgeHub.Api.Features.Portal.Application.Models;
using SystemKnowledgeHub.Api.Features.Portal.Domain;

namespace SystemKnowledgeHub.Api.DeveloperSupport.Demo;
internal sealed partial class DemoDataset
{
    private async Task SeedUnknownItems()
    {
        var workflow = Service<UnknownItemService>(); var resolution = Service<KnowledgeResolutionService>();
        var questions = new[] { "Product Route 校验来源待确认", "Equipment Available 回报正在调查", "Hold Lot 样本已记录 Finding", "STATE_FLAG 更新策略待确认结论", "Lot Track In 后 STATE_FLAG 未更新", "EAP 延迟回报处理已经完成" };
        for (var i = 0; i < questions.Length; i++)
        {
            var result = await workflow.CreateUnknownItem(new(Ids["MES"], "演示 · " + questions[i], "演示调查链：EAP 回报、MES.LOT 与操作说明", i % 2 == 0 ? "High" : "Medium", new("BusinessFunction", Ids["TrackIn"]), [new("DatabaseObject", Ids["LOT"])], Person), Ct);
            var created = (CreateUnknownItemResponse)Need(result.Response, result); var id = created.Id; Ids["unknown" + i] = id;
            async Task<string> CurrentToken() => Token((await Db.UnknownItems.SingleAsync(item => item.Id == id)).Version);
            if (i == 0) continue;
            var started = await workflow.StartInvestigation(new(id, Person, await CurrentToken()), Ct); Need(started.Response, started);
            if (i == 1) continue;
            var finding = await workflow.AddFinding(new(id, "演示发现：EAP 回报延迟，MES STATE_FLAG 尚未及时更新。应补充 SOP / DesignNote 的等待与重试边界。", Person, await CurrentToken()), Ct); Need(finding.Response, finding);
            var evidence = await workflow.AddEvidenceToInvestigation(new(id, new("DatabaseSample", new("UnknownItem", id), null, "演示 · 延迟回报日志样本", "demo://eap/logs/track-in", JsonSerializer.SerializeToElement(new { sample = "T+00 request; T+05 EAP acknowledgement" }), "演示日志文本，无外部连接", "说明状态写入依赖回报时间", "Medium", Person), await CurrentToken()), Ct); Need(evidence.Response, evidence);
            if (i == 2) continue;
            var function = await Db.BusinessFunctions.SingleAsync(item => item.Id == Ids["TrackIn"]);
            var before = JsonSerializer.SerializeToElement(new { name = function.Name, displayName = function.DisplayName, functionType = function.FunctionType, purpose = function.Purpose, caller = function.CallerSummary, input = function.InputDescription, output = function.OutputDescription, rewriteStatus = function.RewriteStatus.ToString() });
            var purpose = "演示 · 等待 EAP 回报后确认 STATE_FLAG；参照主 SOP 和 DesignNote，第 " + i + " 个调查结论。";
            var after = JsonSerializer.SerializeToElement(new { name = function.Name, displayName = function.DisplayName, functionType = function.FunctionType, purpose, caller = function.CallerSummary, input = function.InputDescription, output = function.OutputDescription, rewriteStatus = function.RewriteStatus.ToString() });
            var saved = await workflow.SaveResolutionDraft(new(id, "演示结论：回报延迟导致状态暂未更新；补充 SOP / DesignNote 调查说明，并明确业务功能的等待边界。", [new(null, new("BusinessFunction", function.Id), null, "UpdateBusinessFunction", "补充回报等待与幂等处理", before, after, null, null)], Person, await CurrentToken()), Ct);
            var draft = (SaveResolutionDraftResponse)Need(saved.Response, saved);
            if (i == 3) continue;
            var applied = await resolution.ApplyBusinessFunction(new(id, draft.KnowledgeUpdates[0].Id, function.Id, new(function.Name, function.DisplayName, function.FunctionType, purpose, function.CallerSummary, function.InputDescription, function.OutputDescription, function.RewriteStatus.ToString()), null, Person, await CurrentToken(), Token(function.Version)), Ct); Need(applied.Response, applied);
            var confirmed = await resolution.ConfirmConclusion(new(id, Person, await CurrentToken()), Ct); Need(confirmed.Response, confirmed);
            if (i == 5) { var closed = await resolution.CloseUnknownItem(new(id, "演示 · 结论已落实到知识", Person, await CurrentToken()), Ct); Need(closed.Response, closed); }
        }
    }
    private async Task SeedPortal()
    {
        var portal = Service<AdminPortalService>(); var actor = new PortalCommandActor(Ids["seedUser"], ActorName);
        async Task<long> Node(string title, long? parent, long? page, bool publish)
        {
            var order = await Db.PortalPageNodes.CountAsync(item => item.ParentId == parent);
            var created = await portal.CreateNodeAsync(new("演示 · " + title, page is null ? PortalPageNodeKind.Folder : PortalPageNodeKind.Page, parent, page, order), actor, Ct);
            var node = Need(created.Response, created);
            if (publish) { var result = await portal.PublishNodeAsync(node.NodeId, new(node.ConcurrencyToken), actor, Ct); Need(result.Response, result); }
            return node.NodeId;
        }
        async Task<long> Page(string key, string title, PortalTargetType target, long id, AdminPortalSectionRequest[] sections, bool publish)
        {
            var created = await portal.CreatePageAsync(new("演示 · " + title, new(target, id)), actor, Ct); var page = Need(created.Response, created);
            var updated = await portal.UpdatePageAsync(page.Id, new(page.Title, new(target, id), sections, page.ConcurrencyToken), actor, Ct); page = Need(updated.Response, updated);
            if (publish) { var published = await portal.PublishPageAsync(page.Id, new(page.ConcurrencyToken), actor, Ct); page = Need(published.Response, published); }
            Ids[key] = page.Id; return page.Id;
        }
        static AdminPortalSectionRequest Section(string heading, PortalPageProjectionKind projection, int order, PortalPageSectionSourceKind source = PortalPageSectionSourceKind.PrimaryTarget, AdminPortalTargetReferenceRequest? reference = null) => new(null, heading, source, reference, projection, order);
        var root = await Node("系统知识中心", null, null, true); var mes = await Node("MES", root, null, true); var database = await Node("数据库", mes, null, true);
        var system = await Page("portalSystem", "MES 系统概览", PortalTargetType.System, Ids["MES"], [Section("概览", PortalPageProjectionKind.Summary, 0), Section("结构化信息", PortalPageProjectionKind.StructuredOverview, 1), Section("当前可信依据", PortalPageProjectionKind.TrustSummary, 2), Section("相关知识", PortalPageProjectionKind.RelatedKnowledge, 3, PortalPageSectionSourceKind.Derived)], true);
        await Node("MES 系统概览", mes, system, true);
        var requirement = await Page("portalRequirement", "Lot Track In", PortalTargetType.KnowledgeDocument, Ids["REQ"], [Section("业务要求", PortalPageProjectionKind.KnowledgeDocumentBody, 0), Section("追溯与覆盖", PortalPageProjectionKind.Traceability, 1, PortalPageSectionSourceKind.Derived), Section("可信依据", PortalPageProjectionKind.TrustSummary, 2)], true); await Node("Lot Track In", mes, requirement, true);
        var lot = await Page("portalLot", "MES.LOT", PortalTargetType.DatabaseObject, Ids["LOT"], [Section("数据对象", PortalPageProjectionKind.StructuredOverview, 0), Section("字段与状态", PortalPageProjectionKind.DatabaseStructure, 1), Section("相关知识", PortalPageProjectionKind.RelatedKnowledge, 2, PortalPageSectionSourceKind.Derived)], true); await Node("MES.LOT", database, lot, true);
        var integration = await Page("portalIntegration", "MES → EAP 接口", PortalTargetType.Integration, Ids["MES_EAP"], [Section("接口契约", PortalPageProjectionKind.StructuredOverview, 0), Section("摘要", PortalPageProjectionKind.Summary, 1)], true); await Node("接口", mes, integration, true);
        var sop = await Page("portalSop", "Lot Track In 操作说明", PortalTargetType.KnowledgeDocument, Ids["SOP"], [Section("操作正文", PortalPageProjectionKind.KnowledgeDocumentBody, 0), Section("附件", PortalPageProjectionKind.AttachmentList, 1), Section("原修订确认覆盖", PortalPageProjectionKind.TrustSummary, 2), Section("已撤销确认的当前支持集合", PortalPageProjectionKind.TrustSummary, 3, PortalPageSectionSourceKind.ExplicitReference, new(PortalTargetType.KnowledgeDocument, Ids["ARTICLE"]))], true); await Node("操作说明", mes, sop, true);
        var draft = await Page("portalDraft", "尚未发布的设计分析", PortalTargetType.KnowledgeDocument, Ids["NOTE"], [Section("草稿分析", PortalPageProjectionKind.KnowledgeDocumentBody, 0)], false); await Node("未发布设计分析", mes, draft, false);
        await Node("未发布的 SOP 导航位置", mes, sop, false);
    }
}
