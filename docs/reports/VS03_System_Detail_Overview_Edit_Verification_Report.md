# VS-03 — System Detail + Overview Inline Edit Verification Report

## Result

**VS03 PASS**

## 实现范围

- Q05 `GET /api/systems/{id}`：返回冻结的系统概览、知识概况、关联摘要、数据库对象和可用操作。
- C02 `PUT /api/systems/{id}/overview`：只更新冻结允许的概览字段；`Name`、`Lifecycle`、`KnowledgeStatus`、`Technology` 与 `SystemId` 不可由此用例修改。
- RP-03 System Detail：真实 SQLite 数据、加载/空/错误状态、系统级“关系与缺口” Context Rail，以及到 VS-01 Database Object Detail 的导航。
- ES-01 Overview Inline Edit：就地编辑、前端校验、保存、取消、明确只读上下文、409 冲突提示与重新加载。

## 主要文件

- Backend：`Features/Systems/Application/SystemQueries.cs`、`SystemService.cs`、`Models/SystemModels.cs`、`Api/SystemsController.cs`、`Api/Contracts/SystemRequests.cs`。
- Frontend：`features/systems/pages/SystemDetailView.vue`、`components/SystemOverviewSection.vue`、`components/SystemContextRail.vue`、`composables/useSystemDetail.ts`、Systems API contracts/client、router 与 `systems.css`。
- Tests：`tests/SystemKnowledgeHub.Api.Tests/Api/SystemsApiTests.cs`。

## Schema

- **无 Schema 变化，无 Migration。**
- 复用 VS-02 已有 `systems.version`；EF 将该属性标记为 concurrency token，但未建立第二套物理模型。

## API 与并发

- Canonical routes：`GET /api/systems/{id}`、`PUT /api/systems/{id}/overview`，没有第二套路由。
- API token 保持 opaque string；Application 解码当前整数版本并执行显式版本比较与递增。
- stale token 返回 HTTP 409 / `ConcurrencyConflict`；前端不自动重试，保留用户草稿并提供“重新加载”。

## 必要验证

- `dotnet build SystemKnowledgeHub.sln --no-restore`：PASS，0 warning / 0 error。
- focused backend tests：`SystemsApiTests` 4/4 PASS；其中新增 2 个高价值测试覆盖 Q05 real projection，以及 C02 SQLite 持久化 + stale token 409 + 不可编辑字段保持不变。
- `npm run type-check`：PASS。
- `npm run lint`：PASS。
- `npm run build`：PASS；仅保留 Vite 既有的单 chunk > 500 kB 非阻塞提示。
- 未新增前端测试：关键交互已由严格 type-check、真实浏览器闭环与后端集成测试覆盖。

## Runtime 验证

- Browser → Systems List → MES Detail → `MES.TABLE_EQP` → VS-01 Detail → Back：PASS。
- Overview Inline Edit → API → SQLite → 页面刷新后仍为更新值：PASS。
- 浏览器保持旧 token，同时另一请求先完成 C02；浏览器保存返回 409、冲突提示可见、草稿原样保留，再通过明确“重新加载”读取最新值：PASS。
- Browser console error/warning：0。
- Development runtime 数据用于验证后已删除；Codex 启动的 ASP.NET Core、Vite 及 wrapper processes 均已关闭，5090/5173 无监听。

## Golden UI Review

- Golden：`product-design/final-ui/RP-03_System_Detail.png`、`product-design/final-ui/ES-01_Edit_System_Inline.png`。
- 实现截图与组合对照：`artifacts/VS03/`；检查 1672 × 941 Golden 尺寸及 1366、1440、1920 桌面宽度。
- 保持浅色 Application Shell、Main Content + system-level Context Rail、紧凑分隔、技术标识样式、知识状态表达与 Section inline edit。
- 1366/1440 下无 Drawer 打开，Context Rail 保持可见；主内容与编辑区均未被四栏硬塞或截断。
- Golden 示例含尚未实现的 Business Functions、Integrations、Unknown Items；实现按真实 Slice 数据展示明确空状态，没有伪造关系数据。
- Root `design-qa.md`：`final result: passed`。

## Specification Deviation

- **无阻塞性 Specification Deviation。**
- 冻结 Q05 response 不包含 C02 接受的 `mainProjects` / `mainEntryPoints`；本 Slice UI 不展示这两个无法由 Q05 预填的字段，后端仍严格接受 C02 frozen request。该 contract 可见性问题保留到后续规格评审，本 Slice 未私自扩展 Q05。

## Deferred

- Q05 当前返回未实现 Feature 的真实空集合；不提前实现 Business Function、Integration 或 Unknown Item。
- System Technology / Lifecycle / KnowledgeStatus 的编辑继续延期；C02 不承担这些职责。
- 不开始 VS-04。
