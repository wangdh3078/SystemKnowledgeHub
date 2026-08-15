using Microsoft.EntityFrameworkCore;
using SystemKnowledgeHub.Api.Features.BusinessFunctions.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.BusinessFunctions.Persistence;

public static class BusinessFunctionDevelopmentData
{
    public static async Task SeedAsync(
        KnowledgeHubDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.BusinessFunctions.AnyAsync(cancellationToken))
        {
            return;
        }

        var systems = await dbContext.Systems
            .Where(system => system.Id == 12 || system.Id == 13)
            .ToDictionaryAsync(system => system.Id, cancellationToken);
        if (!systems.TryGetValue(12, out var mes) || !systems.TryGetValue(13, out var wms))
        {
            return;
        }

        var timestamp = new DateTimeOffset(2026, 8, 12, 1, 20, 0, TimeSpan.Zero);
        var equipmentStatus = CreateFunction(
            77,
            mes,
            "Equipment Status Query",
            "设备状态查询",
            "Query",
            "查询设备当前状态并计算展示状态",
            "MES 设备监控页、生产看板",
            "EQP_ID",
            "EquipmentStatusDto",
            RewriteStatus.Keep,
            KnowledgeStatus.Inferred,
            timestamp);
        AddSteps(
            equipmentStatus,
            "接收请求",
            "验证设备",
            "查询 MES.TABLE_EQP",
            "查询当前作业",
            "计算展示状态",
            "返回结果");

        var currentJob = CreateFunction(
            78,
            mes,
            "Current Job Query",
            "当前作业查询",
            "ServiceQuery",
            "查询设备当前作业",
            "Equipment Status Query",
            "EQP_ID",
            "CurrentJobDto",
            RewriteStatus.Change,
            KnowledgeStatus.Inferred,
            timestamp.AddDays(-1));
        AddSteps(currentJob, "接收设备标识", "查询当前作业", "返回作业摘要");

        var lotTrackIn = CreateFunction(
            79,
            mes,
            "Lot Track In",
            "批次进站",
            "BusinessOperation",
            "批次进站与设备绑定",
            "生产操作员",
            "LOT_ID、EQP_ID",
            "TrackInResult",
            RewriteStatus.Unknown,
            KnowledgeStatus.Unknown,
            timestamp.AddDays(-2));
        AddSteps(lotTrackIn, "校验批次", "绑定设备", "记录进站结果");

        var workOrderImport = CreateFunction(
            80,
            mes,
            "Work Order Import",
            "工单导入",
            "IntegrationTask",
            "接收 ERP 工单并校验主数据",
            "ERP",
            "WorkOrderMessage",
            "ImportResult",
            RewriteStatus.Change,
            KnowledgeStatus.Confirmed,
            timestamp.AddDays(-3));
        AddSteps(workOrderImport, "接收工单", "校验主数据", "保存工单", "返回导入结果");

        var inventoryAllocation = CreateFunction(
            81,
            wms,
            "Inventory Allocation",
            "库存分配",
            "BusinessOperation",
            "按优先级分配可用库存",
            "出库任务",
            "AllocationRequest",
            "AllocationResult",
            RewriteStatus.Keep,
            KnowledgeStatus.Inferred,
            timestamp.AddDays(-4));
        AddSteps(inventoryAllocation, "读取需求", "计算可用库存", "锁定库存", "返回分配结果");

        var equipmentHistory = CreateFunction(
            82,
            mes,
            "Equipment History Export",
            "设备历史导出",
            "Batch",
            "导出设备状态历史",
            "设备工程师",
            "ExportFilter",
            "CSV File",
            RewriteStatus.Keep,
            KnowledgeStatus.Confirmed,
            timestamp.AddDays(-5));
        AddSteps(equipmentHistory, "读取导出条件", "查询历史记录", "生成 CSV", "返回文件位置");

        dbContext.BusinessFunctions.AddRange(
            equipmentStatus,
            currentJob,
            lotTrackIn,
            workOrderImport,
            inventoryAllocation,
            equipmentHistory);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static BusinessFunction CreateFunction(
        long id,
        KnowledgeSystem system,
        string name,
        string displayName,
        string functionType,
        string purpose,
        string caller,
        string input,
        string output,
        RewriteStatus rewriteStatus,
        KnowledgeStatus knowledgeStatus,
        DateTimeOffset timestamp)
    {
        return new BusinessFunction
        {
            Id = id,
            System = system,
            Name = name,
            DisplayName = displayName,
            FunctionType = functionType,
            Purpose = purpose,
            CallerSummary = caller,
            InputDescription = input,
            OutputDescription = output,
            RewriteStatus = rewriteStatus,
            CreatedAt = timestamp,
            CreatedByName = "王敏",
            CreatedByRole = "知识整理人员",
            UpdatedAt = timestamp,
            KnowledgeStatus = knowledgeStatus,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "王敏",
            KnowledgeStatusChangedByRole = "知识整理人员",
            Version = 1,
        };
    }

    private static void AddSteps(BusinessFunction function, params string[] names)
    {
        for (var index = 0; index < names.Length; index++)
        {
            function.ProcessSteps.Add(new BusinessProcessStep
            {
                BusinessFunction = function,
                StepOrder = index + 1,
                Name = names[index],
            });
        }
    }
}
