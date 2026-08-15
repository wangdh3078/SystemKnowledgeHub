# VS-10 — Business Rule Verification Report

## 结论

**VS10 PASS**

## 实现范围

- 实现 `C15 CreateBusinessRule`、`Q13 GetBusinessRuleDetail`、`C16 UpdateBusinessRule` 与具体 `C32c ApplyBusinessRuleUpdate`。
- 提供 RP-10 业务规则详情、DR-05 规则预览、DR-12 编辑 Drawer，以及全局“新增知识对象 → 业务规则”的最小创建流程。
- 复用 Relationship、Evidence、KnowledgeStatus 与 UnknownItem 能力；未增加 Business Rule List Route、菜单入口或 Integration Feature。

## Schema / Migration

- 新增 migration：`20260815080341_AddBusinessRules`。
- 只新增 canonical `business_rules` 表及冻结要求的 System FK、`(system_id, rule_name)` 唯一约束、状态/版本字段和索引。
- BusinessFunction 与 BusinessRule 只通过 `KnowledgeRelation / AppliesRule` 关联；没有增加 BusinessFunction FK。

## API

- `POST /api/business-rules`
- `GET /api/business-rules/{id}`
- `PUT /api/business-rules/{id}`
- `POST /api/unknown-items/{id}/knowledge-updates/{updateId}/apply-business-rule`

未增加第二套路由或通用知识更新端点。

## 主要文件

- Backend：`Features/BusinessRules/`、`Features/UnknownItems/Application/KnowledgeResolutionService.cs`、相关 Resolver/Query 集成。
- Frontend：`src/features/business-rules/`、全局 Router/Drawer/Dialog Host，以及 UnknownItem 规则更新预览与 Apply 状态。
- Tests：`tests/SystemKnowledgeHub.Api.Tests/Api/BusinessRulesApiTests.cs`。

## Focused Tests

- `BusinessRulesApiTests`：3/3 通过。
  - 最小创建、Q13 读取与同 System 名称唯一性。
  - 编辑规则自身字段且保留 KnowledgeStatus、Relationship、Evidence。
  - C32c preview 不匹配时整体回滚，匹配时原子更新并记录 Applied。

## Build / Type Check

- `dotnet build --no-restore`：通过，0 warning / 0 error。
- `npm run type-check`：通过。
- `npm run build`：通过；仅有既有的单 chunk 超过 500 kB 提示，不阻塞本 Slice。
- `npm run lint`：通过。

## Runtime Verification

已完成一次真实 `Browser → Vue → API → EF Core → SQLite` 验证：

1. 全局新增入口创建 BusinessRule，初始保持“未知”，并进入 RP-10。
2. 从 Business Function 创建 `AppliesRule`，详情摘要可见并能打开 DR-05；Rule Detail 可反向进入 Business Function。
3. 为 Rule 添加 Evidence 后状态不自动改变；再通过显式操作推进 `Unknown → Inferred`。
4. DR-12 更新 Condition、Result 与明确的 InputData JSON 数组；Relationship、Evidence 与状态保持不变。
5. 创建 `BusinessRule → UsesField → MES.TABLE_EQP.STATE_FLAG`，进入现有 Column Drawer，并添加/回读字段级 Evidence。
6. 创建 Rule 级 UnknownItem，完成 `Start → Finding → Evidence → Resolution Preview → C32c Apply → ConfirmConclusion → Close`。
7. 重新打开 Rule Detail，SQLite 中已显示 Applied 后的 Result，KnowledgeStatus 仍为 `Inferred`，证明 Apply 没有隐式推进状态。

## Golden UI Review

- 对照唯一 Golden：RP-10、DR-05、DR-12、OV-04/OV-05。
- 保持简体中文、现有 Application Shell、Main Content + Rule-level Context Rail + 单 Drawer、高信息密度表格、Evidence 与 Knowledge Progression 表达。
- 未创建 Rule List，也未增加导航项或新视觉体系。

## Specification Deviation

- 无阻塞性偏差。
- 收尾时将 Q10 已冻结但此前硬编码为空的字段 Evidence projection 与现有 C23/Q16 接通，以完成冻结的 `BusinessRule → Database Field → Evidence` 路径；未改变 Q10 contract、Schema 或 Golden 布局。

## Process Cleanup

- ASP.NET Core、Vite 及其子进程均已停止。
- 验证端口 `5090`、`5173` 已确认释放。
- 未留下 watch/test server 或临时验证进程。

## Deferred

- Business Rule List、Delete、Integration Feature、跨系统集成和未在本 Slice 指定的业务规则能力继续延期。
- 不开始 VS-11。
