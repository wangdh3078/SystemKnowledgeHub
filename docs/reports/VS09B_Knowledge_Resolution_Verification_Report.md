# VS-09B — Knowledge Resolution Verification Report

## 结论

**VS09B PASS**

## 实现范围

- 实现 C32a `AddColumnKnownValue`、C32b `UpdateDatabaseColumnKnowledge`、C32e `UpdateBusinessFunction` 的具体 Apply 操作。
- 实现 C33 `ConfirmConclusion`、C34 `CloseUnknownItem`、C35 `ReopenUnknownItem`。
- Apply 以明确目标字段执行，并在一个事务中完成目标修改、KnowledgeUpdate Applied 快照、人员快照与 Activity；没有通用 Patch/反射更新器。
- Resolution Draft、Apply、结论确认、关闭、重新打开保持为独立显式操作；Apply 不自动改变 KnowledgeStatus。
- Reopen 回到 `Investigating`，不回滚或删除历史 Applied KnowledgeUpdate、Resolution、Finding、Evidence 与 Activity。
- 实现 RP-09 及 WF-04/WF-05/WF-06 所需的 Proposed/Applied、确认、关闭和重新打开状态。

## Schema / Migration

- **Schema 无变化，未创建 Migration。**
- 复用 VS-09A 已落地的 `resolutions`、`knowledge_updates`、`unknown_item_activities` 以及既有 target/version 字段。

## Apply Transaction Behavior

- 每次具体 Apply 在单一 EF Core SQLite 事务中校验事项、Resolution、KnowledgeUpdate、目标归属、事项 token、目标 token 和 Preview。
- 同一事务更新明确目标知识、将 KnowledgeUpdate 标记为 Applied、保存 before/after 与应用人快照、写 Activity，并递增必要版本。
- 任一步失败均回滚；不会出现“目标已修改但 Update 仍 Proposed”或相反状态。
- 已 Applied 的同一 KnowledgeUpdate 再次提交会被服务端拒绝，不会重复写知识或 Activity。

## Applied Snapshot / Activity

- `knowledge_updates` 保存完整 `AppliedByName / AppliedByRole / AppliedByTeam / AppliedByExternalKey / AppliedBySource / AppliedByNote / AppliedAt` 快照，不依赖人员中心。
- Q12 按冻结 Contract 不扩展 KnowledgeUpdate 字段；页面在 Applied 结果旁显示最近一条 `KnowledgeUpdateApplied` Activity 的应用人名称、角色/身份和时间，并在完整 Activity 时间线中保留全部应用事实。
- ConfirmConclusion、Close 与 Reopen 分别写冻结要求的状态/活动记录；未扩展为通用 Audit 或 Event Framework。

## KnowledgeStatus Side-effect Check

- 本次浏览器和 integration test 的 C32a 请求将 `knowledgeStatusChange` 明确设为 `null`。
- Apply 后 `STATE_FLAG` 仍为 `Inferred / 推断`；保存 Resolution、Apply、ConfirmConclusion、Close 与 Reopen 均未自动推进或回退 KnowledgeStatus。
- 如未来具体 Apply 显式携带 `knowledgeStatusChange`，仍由 VS-07 的 Evidence/HumanConfirmation 门槛校验。

## Concurrency

- Apply 同时要求最新 UnknownItem `concurrencyToken` 与目标 `targetConcurrencyToken`；Confirm、Close、Reopen 使用最新事项 token。
- token 对客户端保持 opaque；服务端继续使用唯一 app-managed integer version 策略。
- stale token 或重复 Apply 返回 409，不自动重放或合并。

## API

- `POST /api/unknown-items/{id}/knowledge-updates/{updateId}/apply-column-known-value`
- `POST /api/unknown-items/{id}/knowledge-updates/{updateId}/apply-column-knowledge`
- `POST /api/unknown-items/{id}/knowledge-updates/{updateId}/apply-business-function`
- `POST /api/unknown-items/{id}/confirm-conclusion`
- `POST /api/unknown-items/{id}/close`
- `POST /api/unknown-items/{id}/reopen`

没有增加第二套路由，也没有增加 generic apply endpoint。

## 主要文件

- `src/SystemKnowledgeHub.Api/Features/UnknownItems/Application/KnowledgeResolutionService.cs`
- `src/SystemKnowledgeHub.Api/Features/UnknownItems/Application/Models/UnknownItemModels.cs`
- `src/SystemKnowledgeHub.Api/Features/UnknownItems/Api/Contracts/UnknownItemContracts.cs`
- `src/SystemKnowledgeHub.Api/Features/UnknownItems/Api/UnknownItemsController.cs`
- `src/SystemKnowledgeHub.Web/src/features/unknown-items/`
- `tests/SystemKnowledgeHub.Api.Tests/Api/KnowledgeResolutionApiTests.cs`

## Focused Tests

- `KnowledgeResolutionApiTests`：3/3 通过。
  - C32a 原子写入字段 Known Value，并依次完成 C33/C34/C35；重复 Apply 被拒绝，Reopen 后 Applied Update 与正式知识保持。
  - Preview 与当前目标不一致时整个 Apply 失败，目标与 KnowledgeUpdate 均无部分提交。
  - 非法 Confirm/Close 状态顺序被拒绝。
- 同时回归受影响的 `UnknownItemsApiTests`：3/3 通过；合计 6/6。

## Build / Type Check

- `dotnet build SystemKnowledgeHub.sln`：通过，0 warning / 0 error。
- `npm run type-check`：通过。
- `npm run build`：通过；仅有既存 Vite chunk-size 提示，不影响本 Slice。

## Runtime Verification

实际通过 Browser → Vue → Frozen API → Application → EF Core → SQLite 验证：

1. 从 `MES.TABLE_EQP.STATE_FLAG` Drawer 创建高优先级待确认事项。
2. 显式开始调查，添加一条 Finding 和一条 DatabaseSample Evidence。
3. 保存 `STATE_FLAG=90` 的 Resolution 与 `AddColumnKnownValue` Proposed Preview；此时正式知识未改变。
4. 在明确确认 Dialog 中 Apply；页面显示 Applied，Activity 记录执行人，结论仍为“调查中”。
5. 单独确认结论，再单独关闭事项。
6. 填写原因重新打开；事项回到“调查中”，历史 Applied Update、Finding、Evidence、Resolution 与全部 Activity 均保留。
7. 返回 Column Drawer 后 Known Values 数量由 3 变为 4，证明正式知识已写入 SQLite，KnowledgeStatus 仍为“推断”。
8. 最终复核重新读取 `UNK-002`，Applied 区域显示“王敏（知识更新执行人）”、应用时间与不回滚提示；数据来自真实 Q12 Activity，而非 Vue 临时状态。

**Reopen 是否 rollback Applied Knowledge：No。**

最终浏览器 error/warning 日志检查为空。

## Golden UI Review

- 使用 RP-09、WF-04、WF-05、WF-06 Golden Reference 复核。
- 保持简体中文、既有 Application Shell、Main Content + Item-level Context Rail + 单 Drawer/Dialog。
- Proposed 与 Applied 明确区分；Apply 前展示 before/after Preview，并使用显式确认 Dialog。
- Resolution、KnowledgeUpdate、事项状态与 KnowledgeStatus 的概念和操作保持分离。
- 关闭状态只读；重新打开后继续调查，同时明确提示历史 Applied 更新不会回滚。

## Specification Deviation

无阻塞性偏差。

C32c `UpdateBusinessRule`、C32d `UpdateIntegration` 按任务要求延后：当前 BusinessRule / Integration target feature 尚未真实落地，未用硬编码、伪造目标或 generic applier 绕过边界。C32a/C32b/C32e 已完整实现。

## Process Cleanup

- 已关闭本次启动的 ASP.NET Core 与 Vite 验证进程。
- 5090 与 5173 已确认无 LISTENING 进程。
- 已删除本次临时 runtime log，未触碰冻结规格或运行时 SQLite 数据。

## Deferred

- C32c `UpdateBusinessRule`：等待 BusinessRule Feature 提供 canonical entity、version 与具体更新用例。
- C32d `UpdateIntegration`：等待 Integration Feature 提供 canonical entity、version 与具体更新用例。
- 不计划 GenericKnowledgeUpdateApplier、自动回滚、Undo Engine 或 KnowledgeStatus 自动推进。
