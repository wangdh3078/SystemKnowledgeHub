# VS14 — Dashboard + Product Entry Verification Report

状态：**VS14 PASS**  
范围：Q01 `GetDashboard`、RP-01 总览、`/` 正式产品入口与冻结 Sidebar 导航收尾。

## 实现范围

- 新增唯一 Dashboard Query：`GET /api/dashboard?systemId?`。
- 返回冻结的 `scope`、`knowledgeOverview`、`knowledgeProgress`、`needsAttention` 与 `recentActivity` 组合模型。
- 统计只读取 canonical `systems`、`business_functions`、`database_objects`、`database_columns`、`business_rules`、`integrations` 与 `unknown_items`；DatabaseSource 不计入 KnowledgeStatus。
- KnowledgeStatus 仅统计 System、BusinessFunction、DatabaseObject、DatabaseColumn、BusinessRule 与 Integration；开放待确认事项单独按非 `Closed` 的 UnknownItemStatus 计算。
- RP-01 实现紧凑知识总览、单一 CSS 分段进展条、需要关注和最近整理；没有图表、KPI 框架、Dashboard 实体或写入副作用。
- `/` 重定向到 `/dashboard`；品牌与 Sidebar“总览”均指向正式 Dashboard。`/foundation` 保留为未导航的 Bootstrap 诊断页，不再作为产品入口或激活“总览”。
- 顶栏继续复用唯一 Global Search Overlay 与全局“新增”流程；未创建 Dashboard 专用搜索或新增流程。

## Dashboard 数据与导航

- 知识总览数字进入现有系统、业务功能、数据库对象或待确认事项页面；业务规则和集成关系继续通过既有 Global Search 入口探索，未新增冻结 Inventory 之外的列表页。
- 最近整理按 canonical `updated_at` 的降序显示最多 4 条，并进入既有 System / Business Function / Database Object / Column Drawer / Business Rule / Integration / Unknown Item 页面。字段先使用现有 Column Detail API 解析所属对象，再进入 Database Object Detail + Column Drawer。
- 需要关注按 Golden 的固定顺序返回有实际计数的高优先级待确认事项、存在未知知识的系统、缺少业务说明的表、未知字段、等待确认的推断知识和未关联数据的业务功能。
- SQLite Provider 不支持将 `DateTimeOffset` 直接翻译为 SQL `ORDER BY`；Q01 仍在数据库完成计数，只对“最近整理”所需的四字段轻量投影在服务端排序并截取 4 条。未增加 Schema、Migration 或通用查询基础设施。

## API、Schema 与主要文件

- API：`GET /api/dashboard`（Q01）；可选 `systemId` 不存在返回 404，非法 ID 返回 400。
- Schema / Migration：无变化；Dashboard 没有独立领域实体或表。
- Backend：`Features/Dashboard/Application/DashboardQueries.cs`、`Models/DashboardModels.cs`、`Api/DashboardController.cs`，以及 `Program.cs` 的 DI 注册。
- Frontend：`features/dashboard/` 的 typed contract、API、local composable、RP-01 页面与局部样式；Router / Navigation / Sidebar 收口到 Dashboard。
- Tests：`DashboardApiTests.cs`。

## Focused Verification

1. `dotnet build SystemKnowledgeHub.sln --no-restore` — 通过，0 warning / 0 error。
2. `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter FullyQualifiedName~DashboardApiTests` — 2 passed。
   - 真实 SQLite summary 与 KnowledgeStatus / UnknownItemStatus 隔离。
   - Recent Activity 的 4 条上限、排序与开放高优先级事项计数。
3. `npm run type-check` — 通过。
4. `npm run build` — 通过；仅保留既有 Vite 单 chunk 大小提示，不构成失败。

## Runtime Verification

本地开发服务器运行期间完成一次真实闭环：

1. 访问 `/`，确认最终 URL 为 `/dashboard`，并显示当前 SQLite 的真实知识总览、进展、关注项和最近整理。
2. 从总览进入系统列表，并确认现有系统页面可达。
3. 从最近整理进入 `MES.TABLE_EQP` 的既有数据库对象详情。
4. 从最近整理进入现有待确认事项详情。
5. 顶栏 Global Search 搜索 `STATE_FLAG`，确认复用 VS-13 分组结果与字段 Drawer 导航。
6. Sidebar“数据库”进入既有数据库对象列表；品牌入口返回 `/dashboard`。
7. 顶栏“新增”打开既有全局对象选择 Overlay，未创建 Dashboard 专用流程。

## Golden UI Review

- 对照：`product-design/final-ui/RP-01_Dashboard.png`、Final Application Shell 与 Final UI Inventory。
- 保持浅色 Desktop Shell、高信息密度、单一知识进展条、任务型关注列表和可扫描最近整理。
- 正式实现使用“系统知识中心 / System Knowledge Hub”和简体中文 UI；技术标识保持英文原样。
- 不显示对象级 Context Rail，不复制 Detail 内容，不引入 BI 图表或新的视觉方向。

## Specification Deviation

无阻塞性偏差。SQLite 的 `DateTimeOffset ORDER BY` Provider 限制使用最小服务端排序处理，未改变冻结 API、Schema 或领域模型。

## Process Cleanup

- 已停止本轮启动的 ASP.NET Core（listener PID 23188 / launcher PID 17028）与 Vite（listener PID 3980 / launcher PID 2156）。
- 已关闭浏览器自动化标签。
- 已确认 5090 与 5173 无 LISTENING listener。
- 已删除 `.runtime-vs14` 临时运行日志目录。

## Deferred

- System Technology Edit
- System Lifecycle Edit
- AI Search
- Semantic Search
- Embedding
- Vector Search
- RAG
- Final MVP Verification
- Architecture Review

