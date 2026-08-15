# VS-08 Relationship Verification Report

## Result

**VS08 PASS**

## Implemented Use Cases

- Q03 `SearchKnowledgeTargets`（仅 `purpose=RelationTarget`）。
- Q15 `GetRelationshipDetail`。
- C20 `AddKnowledgeRelation`。
- C21 `UpdateKnowledgeRelationDescription`。
- C22 `ChangeRelationKnowledgeStatus`。
- Business Function Detail 作为本 Slice 唯一创建入口；创建后打开 Relationship Detail Drawer，并刷新真实关联数据摘要。

## RelationTypes Actually Exercised

- `Reads`：合法 `BusinessFunction → DatabaseObject/DatabaseColumn` 创建、详情和完整状态闭环。
- `Writes`：精确重复关系保护。
- `UsesField`：非法 `BusinessFunction → DatabaseObject` 端点组合拒绝。
- `Calls`：同 System `BusinessFunction → BusinessFunction` 合法，跨 System 拒绝。

其余冻结 RelationType 已进入封闭枚举与端点矩阵，但未为每一种重复建立测试或演示数据。

## Endpoint Validation

- Source/Target 使用封闭 `KnowledgeTargetType`；应用层验证存在性、不同端点、RelationType 端点矩阵和 System Context。
- `Calls` 仅允许同 System 的 `BusinessFunction → BusinessFunction`。
- `Reads`、`Writes`、`UsesField`、`AppliesRule` 对已落地对象执行共享 System Context 校验。
- 未建立 Knowledge Object Registry、Generic Relation Repository 或 Graph Engine。

## Schema / Migration

- 新增 canonical `knowledge_relations` 表。
- Migration：`20260815032629_AddKnowledgeRelations`。
- 包含冻结的 Source/Target type+id、RelationType、Description、KnowledgeStatus 快照、创建字段、整数 `version`、查询索引及精确唯一约束。
- SQLite 多态端点继续由 Application/Persistence Boundary 校验，不增加物理通用 FK。
- Evidence 既有 Subject type+id 结构直接支持 `KnowledgeRelation`，无需修改 Evidence Schema。
- `dotnet ef migrations has-pending-model-changes`：无待处理模型差异。

## API

- `GET /api/knowledge-targets`
- `POST /api/relationships`
- `GET /api/relationships/{id}`
- `PUT /api/relationships/{id}/description`
- `PUT /api/relationships/{id}/knowledge-status`

未增加第二套路由、删除路由或通用 Command Endpoint。

## Target Search

- 根据已知 Source、RelationType、TargetType、System Context 和关键词返回明确目标预览。
- 结果包含技术名称、对象类型、简短说明、知识状态和 System Context。
- 当前只解析已经落地的 System、DatabaseSource、BusinessFunction、DatabaseObject、DatabaseColumn；尚未实现实体的数据类型返回真实空结果。

## Duplicate Protection

- Application 在写入前返回明确 `409 DUPLICATE_RELATIONSHIP`，并附现有 Relationship ID。
- SQLite 唯一索引同时保护 `SourceType + SourceId + TargetType + TargetId + RelationType`。
- 不自动合并、不静默重复创建。

## Relationship KnowledgeStatus

- 新建关系保持 `Unknown`，保存 Relationship/Evidence 均不自动推进。
- `Unknown → Inferred` 要求至少一条可定位且直接绑定当前 Relationship 的 Evidence。
- `Inferred → Confirmed` 要求至少一条带完整人员快照的 `HumanConfirmation` Evidence。
- 禁止跳级；显式回退沿用 VS-07 Reason 规则；写入使用 opaque `concurrencyToken`。

## Evidence Integration

- `EvidenceSubjectResolver` 增加受控 `KnowledgeRelation` Subject 支持。
- Relationship Detail 展示真实 Evidence；可复用现有 Add Evidence、Evidence Detail 与 Add Human Confirmation Drawer。
- Evidence 仍只是知识依据，不执行状态变更。
- Unknown Item 尚未实现，Relationship Detail 显示真实 0/空状态。

## Focused Tests

新增 **3** 个 `RelationshipsApiTests` 高价值测试；与受影响回归合并运行共 **10/10 PASS**：

- Relationship：3。
- Evidence：3。
- BusinessFunctions：4。

覆盖合法创建/读取/说明更新、非法矩阵、精确去重、跨 System Calls 拒绝，以及 Evidence/HumanConfirmation 状态门槛。

## Build / Type Check

- `dotnet build SystemKnowledgeHub.sln`：PASS，0 warning / 0 error。
- `npm run type-check`：PASS。
- `npm run build`：PASS（仅保留既有 Vite chunk size warning，不影响产物）。
- Relationship/Business Function/DrawerHost focused ESLint：PASS。

## Runtime Verification

通过真实浏览器完成一次完整链路：

`Business Function Detail → Add Relationship → Reads → MES.TABLE_EQP → Save → Relationship Detail → Update Description → Add CodeReference Evidence → 显式 Unknown → Inferred → Add HumanConfirmation → 显式 Inferred → Confirmed → Refresh`。

刷新后确认：Relationship 保持“已确认”，说明和 2 条 Evidence 已持久化；Business Function 关联数据摘要、中文关系类型和证据计数同步更新。完整链路经过 Vue、冻结 API、Application、EF Core 与 SQLite。

## Golden UI Review

- 对照 DR-06 与 DR-07：Source / Relation / Target、System Context、目标预览、关系说明、Knowledge Progression、Evidence、创建/修改上下文均清晰。
- 复用 Application Shell、Business Function Main Content、单 Drawer、Evidence Drawer 和统一 KnowledgeProgression。
- Drawer 打开时窄桌面隐藏 Context Rail；Main Content 保持关系摘要，不复制 Drawer 详情。
- 正式产品名与操作文案为简体中文，技术标识保持原文。
- 未重新设计页面或引入第二套视觉体系。

## Specification Deviation

无阻塞或功能性 Specification Deviation。

尚未落地的 BusinessRule、Integration 和 UnknownItem 只显示真实空结果/空状态，不伪造数据，也不扩大本 Slice。

## Process Cleanup

- 浏览器验证会话已关闭。
- 本次启动的 ASP.NET Core、Vite 及其子进程已全部终止。
- 验证端口 `5090`、`5173` 已确认释放。

## Deferred

- BusinessRule、Integration 端点的真实搜索与关系数据，待对应实体 Slice。
- UnknownItem 关联摘要，待 UnknownItem Slice。
- Relationship Delete、Rebind、Bulk Edit、Graph Visualization、Automatic Discovery 均不属于 MVP VS-08。
- 未开始 Finding、Resolution、KnowledgeUpdate 或 VS-09。
