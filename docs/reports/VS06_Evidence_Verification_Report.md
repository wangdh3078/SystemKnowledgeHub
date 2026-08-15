# VS-06 Evidence Verification Report

## Result

`VS06 PASS`

## Implemented Scope

- C23 `AddEvidence`
- C24 `UpdateEvidence`
- C25 `AddHumanConfirmation`
- Q16 `GetEvidenceDetail`
- Business Function Detail 中的“添加证据”、Evidence 列表、Evidence Detail 和人工确认真实入口
- 单 Drawer 替换、局部 loading/error/edit/conflict 状态

## Schema / Migration

- 新增 canonical `evidence` 表，Migration：`20260815000747_AddEvidence`。
- 使用冻结的 `SubjectType + SubjectId + optional SubjectDetailKey`，未新增 Knowledge Object Registry 或通用 FK 框架。
- PersonSnapshot 按冻结模型展平保存；`source_locator_json` 必须是 JSON Object。
- 使用 app-managed integer `version`，对外仍是 opaque `concurrencyToken`。
- 已通过 EF Core `has-pending-model-changes`：当前模型与 Migration 一致。

## Evidence Types

Schema 与 API 使用冻结稳定英文值：

`CodeReference`、`Sql`、`DatabaseSample`、`DatabaseComment`、`Api`、`MqMessage`、`ExistingDocument`、`HumanConfirmation`。

Human Confirmation 是 EvidenceType，不是审批或人员中心。

## Subject Validation

- Application 层通过显式 `EvidenceSubjectResolver` 校验已落地的 `System`、`DatabaseSource`、`BusinessFunction`、`DatabaseObject`、`DatabaseColumn`。
- 未落地 SubjectType 不伪造对象，在对应实体 Slice 实现前返回受控的不支持结果。
- C24 的 Request 不包含 EvidenceType/Subject/SubjectDetailKey；更新只允许纠正来源、说明、可信度和 ProviderSnapshot。

## Canonical API

- `POST /api/evidence`
- `GET /api/evidence/{id}`
- `PUT /api/evidence/{id}`
- `POST /api/evidence/human-confirmations`

未增加备选路由、Evidence Delete/Rebind 或状态接口。

## Main Files

- `src/SystemKnowledgeHub.Api/Features/Evidence/`：Domain、Application、Persistence、API 及 Contract。
- `src/SystemKnowledgeHub.Web/src/features/evidence/`：typed API、Add/Detail/Human Confirmation Drawer 及局部样式。
- `src/SystemKnowledgeHub.Web/src/features/business-functions/pages/BusinessFunctionDetailView.vue`：真实 Subject 入口与保存后刷新。
- `src/SystemKnowledgeHub.Web/src/layouts/DrawerHost.vue`：复用单 Drawer Host，替换内容后恢复顶部滚动位置。

## Focused Tests and Static Verification

- 新增 3 个高价值 SQLite/HTTP focused tests：普通 Evidence 写入与 Subject 状态不变、可变字段/来源定位纠正与 stale token 409、HumanConfirmation 完整快照与状态不变。
- `dotnet build SystemKnowledgeHub.sln --no-restore`：通过，0 warning / 0 error。
- `dotnet test ... --filter FullyQualifiedName~EvidenceApiTests`：3/3 通过。
- 回归检查 `BusinessFunctionsApiTests`：4/4 通过。
- `npm run type-check`：通过。
- `npm run build`：通过；仅保留已有 Vite chunk-size 警告。
- 变更范围 ESLint：通过。未为简单 Drawer 表单新增低价值前端测试。

## Runtime Verification

在真实 SQLite 上完成一次聚焦链路：

`Business Function Detail → Add Evidence → Evidence Detail → Update Evidence → Add Human Confirmation → Evidence Detail`

- 普通 Evidence 保存后详情可立即读取，列表计数刷新。
- 纠正后来源标题/定位和支持说明更新，Subject 保持不变。
- Human Confirmation 保存完整确认人快照，可进入详情。
- 全流程中 Business Function KnowledgeStatus 保持 `Inferred`，未被 Evidence 或 Human Confirmation 自动修改。
- Browser console error：0。

## Golden UI Review

- 已严格对照 DR-08、DR-09、DR-10，并在 `1440 × 900` 与 Golden `1671 × 941` 视口检查。
- Context Rail 在窄桌面被单 Drawer 替换；Drawer 不堆叠。
- 同视口组合对照：`artifacts/VS06/qa-comparison-golden-vs-implementation.png`。
- `design-qa.md` 最终状态：`passed`。

## Specification Deviation

无。由于 Windows 当前保留默认验证端口段，本轮仅通过 `VITE_API_PROXY_TARGET` 在受控的 5190/8390 端口执行运行验证；默认 5090 代理与冻结 API 路由未改变。

## Process Cleanup

- Codex 启动的 ASP.NET Core、Vite 及其子进程已关闭。
- In-app Browser 验证标签已终止。
- 5190、8390 及默认 5090、5173 均无监听进程。
- 临时运行日志与 PID 文件已清理，未留下 watch/test/server 进程。

## Deferred

- C22/C26 KnowledgeStatus Change 与状态推进门槛。
- Relationship、UnknownItem/Finding/Resolution/KnowledgeUpdate 流程。
- 其他 Knowledge Object 页面的 Evidence 入口；本 Slice 仅选择 Business Function Detail 完成真实闭环。
- 运行时外部 Source Accessibility Probe、Evidence Delete/Rebind 和通用附件中心。
