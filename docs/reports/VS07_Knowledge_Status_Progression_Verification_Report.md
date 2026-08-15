# VS-07 — Knowledge Status Progression Verification Report

## 结论

**VS07 PASS**

## 实现范围

- 实现 C26 `ChangeKnowledgeStatus`，唯一 canonical route 为 `PUT /api/knowledge-status`。
- 对当前已落地的 `System`、`BusinessFunction`、`DatabaseObject`、`DatabaseColumn` 执行受控 Target 解析与状态更新；`DatabaseSource` 明确拒绝，尚未落地的 `BusinessRule`、`Integration` 返回受控 422。
- `Unknown → Inferred` 要求至少一条与同一 Subject 相关且具有有效 `SourceReference` 或 `SourceLocator` 的 Evidence。
- `Inferred → Confirmed` 要求至少一条相关 `HumanConfirmation`，且确认人姓名、角色和时间完整。
- 禁止 `Unknown → Confirmed`；允许显式回退，但必须提供非空 Reason。
- 普通 Evidence 与 HumanConfirmation 的保存均不自动改变 KnowledgeStatus。
- Business Function Detail 接入只读 Knowledge Progression、门槛反馈及显式确认 Dialog；不把状态节点设计为 Tab 或快捷开关。
- C22 属于 `KnowledgeRelation` 专用用例；Relationship 尚未进入当前 Slice，因此未伪造关系实体、路由或状态入口。

## Schema / Migration

- **无 Schema 变化，无 Migration。**
- 继续使用各 canonical entity 已存在的 `knowledge_status`、最近修改快照与 app-managed integer `version`。
- Evidence 继续使用 VS-06 canonical `evidence` 表和受控 `SubjectType + SubjectId + SubjectDetailKey`。

## API

- `PUT /api/knowledge-status`
- 成功响应返回 Target、前后状态、Reason、ChangedAt 和新的 opaque `concurrencyToken`。
- 门槛或非法 progression 返回 `422 business_rule_violation`；stale token 与同状态重复修改返回 `409 conflict`。

## 主要文件

- `src/SystemKnowledgeHub.Api/Features/KnowledgeStatus/`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-status/`
- `src/SystemKnowledgeHub.Web/src/components/data-display/KnowledgeProgression.vue`
- `tests/SystemKnowledgeHub.Api.Tests/Api/KnowledgeStatusApiTests.cs`

## Focused Tests

- `KnowledgeStatusApiTests`：2/2 通过。
  - 证明 Evidence 门槛、普通 Evidence 不自动推进、显式 Inferred、HumanConfirmation 不自动推进、显式 Confirmed 及修改人快照持久化。
  - 证明禁止 Unknown 直达 Confirmed、stale token 409、无原因回退拒绝及带原因回退成功。
- 受影响回归：`EvidenceApiTests` 3/3、`BusinessFunctionsApiTests` 4/4 通过。

## Build / Type Check

- `dotnet build SystemKnowledgeHub.sln --no-restore`：通过，0 warning / 0 error。
- `npm run type-check`：通过。
- `npm run build`：通过；仅保留 Vite 既有 chunk size warning。
- ESLint：通过。

## Runtime Verification

通过真实 `Browser → Vue → Frozen API → EF Core → SQLite` 验证 Business Function `VS05 Runtime Function 1786727085736`：

1. 初始为“未知”，尝试推进时显示 Evidence 门槛，确认按钮禁用。
2. 添加具有来源定位的 Code Reference；页面刷新后状态仍为“未知”。
3. 再次打开确认 Dialog，显式推进为“推断”。
4. 添加带完整确认人快照的 HumanConfirmation；页面刷新后状态仍为“推断”。
5. 再次显式推进为“已确认”。
6. 刷新 Route 后仍为“已确认”，证明 SQLite 持久化成功；浏览器控制台无错误。

## Golden UI Review

- 对照冻结 Business Function Detail 与 Evidence / Human Confirmation Golden 交互语言复核。
- 保留简体中文 Application Shell、Main Content + Function-level Context Rail、单 Dialog/Drawer Host、高信息密度和浅色技术工具视觉。
- Knowledge Progression 是清晰路径，不是 Tab；门槛、知识影响和显式确认操作的视觉层级明确。
- 未生成新视觉方案，未修改 Golden UI。

## Specification Deviation

- **无阻塞性偏差。**
- C22 未实现不是契约偏差：它只适用于尚未实现的 `KnowledgeRelation`，且本 Slice 明确禁止启动 Relationship Feature。

## Process Cleanup

- 已关闭本次启动的 ASP.NET Core、Vite、Node/npm 及其验证子进程。
- API 端口 `5190` 与 Web 端口 `8390` 均已确认释放。
- 浏览器验证 Tab 已关闭，未留下 watch/test server。

## Deferred

- C22 `ChangeRelationKnowledgeStatus` 与 Relationship Feature。
- Business Rule、Integration 的实体落地及其 C26 UI 入口。
- System、Database Object、Database Column 的状态推进 UI 入口；后端 C26 已按冻结受控 Target 支持其已落地实体。
- Unknown Item workflow 与后续 Vertical Slice。

**VS07 PASS**
