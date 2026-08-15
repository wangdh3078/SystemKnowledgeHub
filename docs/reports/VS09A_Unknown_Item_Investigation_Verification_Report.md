# VS-09A — Unknown Item Investigation Verification Report

## 结论

**VS09A PASS**

## 实现范围

- 实现 Q11 `GetUnknownItemsList`、Q12 `GetUnknownItemDetail`。
- 实现 C27 创建、C27a 更新非 Primary Targets、C28 开始调查、C29 添加 Finding、C30 添加调查 Evidence、C31 保存 Resolution Draft。
- 实现 RP-08、RP-09 及必要的 Finding / Evidence / Resolution authoring state。
- Business Function Detail 增加创建待确认事项入口，并显示真实功能级开放事项。
- 未实现 Apply KnowledgeUpdate、ConfirmConclusion、Close、Reopen 或任何通用 Patch/Workflow Engine。

## Schema / Migration

- 新增 `AddUnknownItemInvestigation` Migration。
- 增量落地 `unknown_items`、`unknown_item_targets`、`findings`、`resolutions`、`knowledge_updates`、`unknown_item_activities`。
- Evidence 继续使用现有 canonical `evidence` 表，以受控 SubjectType 关联 UnknownItem / Finding / Resolution。
- `unknown_items.version` 使用既定 app-managed integer concurrency strategy；API token 保持 opaque string。
- `knowledge_updates` 保持 Proposed Draft；数据库约束要求 Applied 记录必须具有应用人姓名、角色与应用时间，为后续 VS-09B 保留冻结 Schema 一致性。

## API

- `GET /api/unknown-items`
- `GET /api/unknown-items/{id}`
- `POST /api/unknown-items`
- `PUT /api/unknown-items/{id}/related-targets`
- `POST /api/unknown-items/{id}/start-investigation`
- `POST /api/unknown-items/{id}/findings`
- `POST /api/unknown-items/{id}/evidence`
- `PUT /api/unknown-items/{id}/resolution`

没有增加第二套路由。

## 主要文件

- `src/SystemKnowledgeHub.Api/Features/UnknownItems/`
- `src/SystemKnowledgeHub.Web/src/features/unknown-items/`
- `tests/SystemKnowledgeHub.Api.Tests/Api/UnknownItemsApiTests.cs`

## Focused Tests

- `UnknownItemsApiTests`：3/3 通过。
  - 创建后可由列表/详情读取，并原子保存 Primary Target 与 Created Activity。
  - Start / Finding 校验状态和 stale token，并持久化人员快照与 Activity。
  - Investigation Evidence 限定当前调查 Subject；Resolution 保持 Draft，且目标 KnowledgeStatus 不变。
- 受影响 `BusinessFunctionsApiTests`：4/4 通过。

## Build / Type Check

- `dotnet build SystemKnowledgeHub.sln --no-restore`：通过，0 warning / 0 error。
- `npm run type-check`：通过。
- `npm run build`：通过；仅保留已有 Vite chunk-size 提示，不影响本 Slice。

## Runtime Verification

实际通过 Browser → Vue → Frozen API → Application → EF Core → SQLite 验证：

1. 从待确认事项列表创建 `UNK-001`，状态为 `Open / 待处理`。
2. 进入详情并显式开始调查，状态变为 `Investigating / 调查中`，出现 StatusChanged Activity。
3. 添加 Finding，详情和 Activity 同步刷新。
4. 以该 Finding 为 Subject 添加 CodeReference Evidence；证据数量更新，KnowledgeStatus 保持不变。
5. 保存 Resolution Draft；状态仍为 Investigating，未生成或应用 KnowledgeUpdate。
6. 最终浏览器 console error/warning 检查为空。

## Golden UI Review

- 使用 RP-08、RP-09、WF-02、DR-08、WF-04 的 Golden Reference 复核。
- 保持简体中文、现有 Application Shell、Main Content + Item-level Context Rail + 单 Drawer/Dialog。
- Context Rail 只显示 Related Objects、Knowledge Impact、Open Gaps 与 Evidence 计数，不复制 Finding / Evidence / Resolution 详情。
- 后续 Apply / Confirm / Close 仅以 disabled/说明状态出现，没有提前实现 VS-09B。

## Specification Deviation

无阻塞性偏差。KnowledgeRelation 未作为 UnknownItem Target，因为冻结 API 的 KnowledgeTargetRef 集合不包含该类型；以 Frozen Contract 为准。

## Process Cleanup

- 已关闭本次启动的 ASP.NET Core、Vite 及其 Node 子进程。
- 5090 与 5173 均无 LISTENING 进程；5088 是 Windows 保留端口，不属于本次服务进程。
- 已删除本次临时 runtime log；未触碰冻结规格。

## Deferred

- C32a～C32e concrete Apply KnowledgeUpdate。
- C33 ConfirmConclusion。
- C34 CloseUnknownItem。
- C35 ReopenUnknownItem。
- Business Rule / Integration Target 的运行时创建与预览，待相应 Feature Slice 落地后接入现有受控 resolver。
