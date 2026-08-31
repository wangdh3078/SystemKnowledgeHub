using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Domain;
using SystemKnowledgeHub.Api.Features.Systems.Domain;
using SystemKnowledgeHub.Api.Persistence;
using SystemKnowledgeHub.Api.Shared.Domain;

namespace SystemKnowledgeHub.Api.Features.DatabaseKnowledge.Persistence;

public static class DatabaseKnowledgeDevelopmentData
{
    public static async Task InitializeAsync(
        IServiceProvider serviceProvider,
        CancellationToken cancellationToken = default)
    {
        await using var scope = serviceProvider.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<KnowledgeHubDbContext>();
        await dbContext.Database.MigrateAsync(cancellationToken);
        await SeedAsync(dbContext, cancellationToken);
    }

    public static async Task SeedAsync(
        KnowledgeHubDbContext dbContext,
        CancellationToken cancellationToken = default)
    {
        if (await dbContext.DatabaseObjects.AnyAsync(cancellationToken))
        {
            return;
        }

        var timestamp = new DateTimeOffset(2026, 8, 12, 1, 20, 0, TimeSpan.Zero);
        var system = new KnowledgeSystem
        {
            Id = 12,
            Name = "MES",
            DisplayName = "制造执行系统",
            SystemType = "Manufacturing Execution System",
            Lifecycle = SystemLifecycle.Legacy,
            Purpose = "管理设备与生产执行状态",
            MainUsersJson = JsonSerializer.Serialize(new[] { "生产操作员", "设备工程师", "MES 支持团队" }),
            RepositoryName = "mes-legacy",
            RepositoryUrl = "https://git.example/mes-legacy",
            DeploymentJson = JsonSerializer.Serialize(new[]
            {
                new { environment = "Production", description = "MES-APP-01 / MES-APP-02" },
            }),
            Notes = "2009 年上线，核心设备状态模块仍在持续维护",
            CreatedAt = timestamp,
            CreatedByName = "王敏",
            CreatedByRole = "知识整理人员",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Inferred,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "王敏",
            KnowledgeStatusChangedByRole = "知识整理人员",
            Version = 1,
        };

        AddTechnology(system, ".NET Framework 4.8", "Oracle");

        var source = new DatabaseSource
        {
            Id = 9,
            System = system,
            Name = "MES 生产库",
            Engine = "Oracle",
            Environment = "Production",
            ServiceName = "MESPROD",
            Description = "MES 主业务数据库",
            IsPrimary = true,
            CreatedAt = timestamp,
            CreatedByName = "王敏",
            CreatedByRole = "知识整理人员",
            UpdatedAt = timestamp,
        };

        var databaseObject = new DatabaseObject
        {
            Id = 45,
            DatabaseSource = source,
            SchemaName = "MES",
            ObjectName = "TABLE_EQP",
            ObjectType = DatabaseObjectType.Table,
            TechnicalIdentityAlgorithmVersion = 1,
            TechnicalIdentity = "seed:object:v1:45",
            BusinessDescription = "设备当前状态与运行标识主表",
            EstimatedRows = 2_400_000,
            AccessMode = DatabaseAccessMode.ReadWrite,
            PrimaryKeyColumnsJson = "[\"EQP_ID\"]",
            BusinessKeyColumnsJson = "[\"EQP_ID\"]",
            CreatedAt = timestamp,
            CreatedByName = "王敏",
            CreatedByRole = "知识整理人员",
            UpdatedAt = timestamp,
            KnowledgeStatus = KnowledgeStatus.Inferred,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "王敏",
            KnowledgeStatusChangedByRole = "知识整理人员",
            Version = 1,
        };

        var columns = new[]
        {
            CreateColumn(121, 1, "EQP_ID", "VARCHAR2(20)", false, "设备唯一标识", KnowledgeStatus.Confirmed, timestamp),
            CreateColumn(122, 2, "STATUS", "VARCHAR2(10)", true, "设备当前状态", KnowledgeStatus.Inferred, timestamp),
            CreateColumn(123, 3, "STATE_FLAG", "VARCHAR2(2)", true, "设备运行状态标志", KnowledgeStatus.Inferred, timestamp),
            CreateColumn(124, 4, "AREA_ID", "VARCHAR2(20)", false, "设备所属区域标识", KnowledgeStatus.Confirmed, timestamp),
            CreateColumn(125, 5, "JOB_ID", "VARCHAR2(30)", true, "当前作业标识", KnowledgeStatus.Inferred, timestamp),
            CreateColumn(126, 6, "FOUP_ID", "VARCHAR2(30)", true, "关联 FOUP 标识", KnowledgeStatus.Unknown, timestamp),
            CreateColumn(127, 7, "UPDATED_BY", "VARCHAR2(30)", false, "最后更新人员", KnowledgeStatus.Confirmed, timestamp),
            CreateColumn(128, 8, "LAST_UPDATED_AT", "TIMESTAMP", false, "最后来源更新时间", KnowledgeStatus.Confirmed, timestamp),
        };

        foreach (var column in columns)
        {
            column.DatabaseObject = databaseObject;
        }

        columns[2].KnownValues.Add(new ColumnKnownValue
        {
            Id = 701,
            ValueText = "10",
            Meaning = "运行中",
            SortOrder = 10,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        });
        columns[2].KnownValues.Add(new ColumnKnownValue
        {
            Id = 702,
            ValueText = "20",
            Meaning = "待机",
            SortOrder = 20,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        });
        columns[2].KnownValues.Add(new ColumnKnownValue
        {
            Id = 703,
            ValueText = "30",
            Meaning = "Unknown / Offline",
            SortOrder = 30,
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
        });

        dbContext.Systems.Add(system);
        dbContext.Systems.AddRange(
            CreateSystem(13, "WMS", "仓储管理系统", "核心业务系统", "物料与仓储作业管理", SystemLifecycle.Running, KnowledgeStatus.Unknown, timestamp.AddDays(-1), "Java", "Oracle"),
            CreateSystem(14, "APS", "高级计划排程", "计划系统", "生产计划与排程", SystemLifecycle.Maintaining, KnowledgeStatus.Inferred, timestamp.AddDays(-2), "Java", "PostgreSQL"),
            CreateSystem(15, "Equipment Gateway", "设备接入网关", "集成系统", "设备协议与消息转换", SystemLifecycle.Running, KnowledgeStatus.Confirmed, timestamp.AddDays(-3), "C#", "RabbitMQ"),
            CreateSystem(16, "ERP", "企业资源计划", "外部系统", "工单与物料主数据", SystemLifecycle.Running, KnowledgeStatus.Confirmed, timestamp.AddDays(-4), "SAP"),
            CreateSystem(17, "Data Warehouse", "数据仓库", "分析系统", "历史数据汇聚与报表", SystemLifecycle.Maintaining, KnowledgeStatus.Inferred, timestamp.AddDays(-5), "SQL Server"));
        dbContext.DatabaseColumns.AddRange(columns);
        await dbContext.SaveChangesAsync(cancellationToken);
    }

    private static KnowledgeSystem CreateSystem(
        long id,
        string name,
        string displayName,
        string systemType,
        string purpose,
        SystemLifecycle lifecycle,
        KnowledgeStatus knowledgeStatus,
        DateTimeOffset timestamp,
        params string[] technologies)
    {
        var system = new KnowledgeSystem
        {
            Id = id,
            Name = name,
            DisplayName = displayName,
            SystemType = systemType,
            Purpose = purpose,
            Lifecycle = lifecycle,
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

        AddTechnology(system, technologies);
        return system;
    }

    private static void AddTechnology(KnowledgeSystem system, params string[] technologies)
    {
        foreach (var technology in technologies)
        {
            system.TechnologyTags.Add(new SystemTechnologyTag
            {
                System = system,
                Technology = technology,
            });
        }
    }

    private static DatabaseColumn CreateColumn(
        long id,
        int ordinalPosition,
        string name,
        string dataType,
        bool nullable,
        string? businessDescription,
        KnowledgeStatus knowledgeStatus,
        DateTimeOffset timestamp)
    {
        return new DatabaseColumn
        {
            Id = id,
            OrdinalPosition = ordinalPosition,
            ColumnName = name,
            DataType = dataType,
            IsNullable = nullable,
            BusinessDescription = businessDescription,
            DatabaseComment = name == "STATE_FLAG" ? "Equipment state flag" : null,
            TechnicalIdentityAlgorithmVersion = 1,
            TechnicalIdentity = $"seed:column:v1:{id}",
            CreatedAt = timestamp,
            UpdatedAt = timestamp,
            KnowledgeStatus = knowledgeStatus,
            KnowledgeStatusChangedAt = timestamp,
            KnowledgeStatusChangedByName = "王敏",
            KnowledgeStatusChangedByRole = "知识整理人员",
            Version = 1,
        };
    }
}
