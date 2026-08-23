# REV-B04 Restore & Published Change Safety Verification Report

## Result

**REV-B04 PASS**

REV-B04 已完成 Restore business action、Published change safety、confirmation coverage 与 HumanConfirmation revision-context UX，并通过 focused automation、回归检查和隔离的 Browser → API → EF Core → SQLite 运行时验收。未新增或修改 schema / migration。

## Worktree Baseline

- 开始时分支：`main`，与 `origin/main` 同步。
- 基线提交：`7dd8961 ✨ feat: 历史版本管理`。
- `git status` / `git status --short`：clean；没有覆盖 REV-B03 或用户未提交内容。
- 实施范围只覆盖 REV-B04 所需 KnowledgeDocument、Revision History、Evidence/HumanConfirmation、single-dialog host、focused tests 与本报告。

## Architecture Compliance

- 完整复核 REV-A01 freeze、REV-B01/B02/B03 verification、PHASE-NEXT planning 与当前实现；未发现规范冲突。
- Restore 继续使用 feature-first Controller → explicit use-case service → direct `KnowledgeHubDbContext`，没有引入 Repository、CQRS、command bus 或通用 rollback framework。
- 前端继续使用 typed feature API、现有 shared API client 和 single-overlay host；History、Compare 和 Current Content 没有建立第二套 overlay manager。
- Restore 不解析 concurrency token，不将 Revision metadata 当作 authoritative input，不改变既有 lifecycle/status/evidence/relationship 语义。
- REV-B01 schema 已完整支持 Restore；本 Slice **No migration / No schema change**。

## Restore API Contract

- 新增精确路由：`POST /api/knowledge-documents/{id}/revisions/{revisionNumber}/restore`。
- authoritative request 仅包含 `concurrencyToken` 与 `reason`；内容从 route Document 拥有的 immutable Revision snapshot 读取。
- 成功返回扩展后的 current `KnowledgeDocumentDetailResponse`，包含新 token、`currentRevisionNumber`、保留的 latest-published pointer 与 `confirmationCoverage`。
- 额外 legacy actor/time/title 字段不能控制恢复内容或审计；focused integration test 已验证服务端 actor、时间和 snapshot 来源。

## Authorization / Validation

- 复用现有 Editor policy：Viewer → `403 forbidden`；Editor / Administrator 可执行合法 Restore。
- document id 与 revision number 均执行 JavaScript-safe positive integer 校验。
- token 使用现有 opaque codec；invalid token → `400 validation_error`，stale token → `409 conflict`。
- reason trim 后强制 5～500；blank、过短、过长 → `400 validation_error`。
- 按 Document → owned Revision 顺序解析；missing Document、missing Revision、cross-document Revision 均为 `404 not_found`，不泄露其它 Document revision。
- Published / Archived → `409 invalid_state`；current revision 或 semantic-identical historical snapshot → `422 business_rule_violation`。

## Atomic Restore

- 同一 transaction 内完成：校验 current head/source snapshot → 复制 Title/Summary/BodyMarkdown → current revision +1 → 插入 `Origin=Restore` revision → 更新 trusted actor/time、UpdatedAt、Version +1 → 同步 current-head FTS → commit。
- Restore revision 固定 `LifecycleContext=Draft`、`RestoredFromRevisionNumber=K`、normalized `RestoreReason`、`ChangeSummary=null`。
- focused failure test 通过临时 SQLite trigger 强制 revision insert 失败，证明 head、revision pointer、Version、revision rows 与 FTS 全部 rollback，无 partial write。

## Data Preservation

- Restore 保持 `DocumentType`、Draft lifecycle、KnowledgeStatus 及其 audit、Evidence、HumanConfirmation、Relationships、`LatestPublishedRevisionNumber`、`PublishedAt` 和全部历史 revision。
- 运行时从 revision 1 恢复后：current revision 4，revision 1～4 全部存在；revision 4 内容与 revision 1 完全一致。
- 运行时仍为 `KnowledgeStatus=Confirmed`；两条 Evidence 保留，HumanConfirmation 仍覆盖 revision 2；关系数量保持 0；latest published 仍为 3，`PublishedAt` 与 revision 3 创建时间一致。
- runtime head Version 从恢复前 7 精确增加为 8。

## FTS

- Restore 成功只重建 current-head FTS，不索引 historical revisions。
- 运行时搜索 `版本三摘要` 无结果，搜索恢复后的 `版本一摘要` 返回当前 Document。
- SQLite 只读断言确认 FTS 不再包含 revision 2/3 的 `ALPHA-B04` / `BETA-B04` 唯一词。
- invalid/stale/forbidden/failing transaction tests 均未留下 FTS partial update。

## Restore UX

- Restore action 只位于 Revision History → selected historical preview。
- 可见性为 Editor/Admin + Draft + historical revision；Viewer/current revision 隐藏，Published/Archived 显示只读“请先将文档返回草稿后再恢复历史内容”提示；Compare 无 Restore。
- single dialog 显示 source revision、origin、author snapshot、server time、historical title/summary、current revision 与冻结的 no-history-deletion 文案。
- reason 为 required 5～500，带字符计数和 validation；invalid 时 confirm disabled。
- submit 有 loading/disabled/double-submit protection；成功关闭 dialog、清理 reason、采用 response detail、退出 History/Compare、返回 Current Content，并显示“已从修订 1 恢复，并创建修订 4”。
- History 正确显示 revision 4“历史恢复 / 从修订 1 恢复 / 原因”，1→4 Compare 正常并报告内容一致。

## Error / Conflict UX

- `409 conflict` 显示冻结冲突文案，不自动 retry；用户显式 reload 后刷新 current detail/token，同时保留 selected historical revision 与 reason，仍需再次确认。
- `409 invalid_state` 与 `422 business_rule_violation` 刷新 current/history pointers，不自动 ReturnToDraft，也不归类为 network error。
- validation、forbidden、not-found、server/network failure 复用 ApiError UX；失败时保留 historical preview 与 reason，不显示假成功。
- component tests 覆盖 conflict reload/reconfirm、invalid-state/422 refresh、network context retention 和 single-overlay behavior。

## Published Save Warning

- Published Edit Mode 在 Save 附近持续显示：“保存后新内容立即成为已发布内容并生成新修订。”
- 每次 dirty Published save 均使用同一 explicit confirmation；Cancel 不请求 content update、保持 Edit Mode/dirty content/可用 Save。
- Confirm 只提交一次，复用现有 content update endpoint；运行时产生 revision 3，仍为 Published，latest published 移至 3，`PublishedAt` 等于 revision 3 save time，KnowledgeStatus 保持 Confirmed。
- Draft edit 无 Published warning；clean state 不提交。

## Keyboard Save Confirmation

- 点击 Save、Ctrl+S 与 Cmd+S 全部进入同一个 `requestSave` confirmation path。
- 浏览器运行时真实执行 Ctrl+S：confirmation 出现；第一次 Cancel 后 revision count 仍为 2 且 dirty summary 保留；第二次 Confirm 后只生成 revision 3。
- frontend tests 分别覆盖 click、Ctrl+S、Cmd+S、Cancel、single-submit 和 clean-state no-op。

## Confirmation Coverage UX

- UI 直接消费 REV-B01 detail projection，不在 frontend 重算规则。
- `NoConfirmation`：不显示 changed warning。
- `LegacyConfirmationUnknown`：精确显示“迁移前人工确认无法确定覆盖的修订。”
- `CurrentRevisionConfirmed`：显示“人工确认覆盖当前修订 N”。
- `ChangedSinceConfirmation`：在 KnowledgeStatus 附近精确显示“内容在最近一次确认后已修改”，不是新的 status badge，不阻止 save/restore，也不自动改变 KnowledgeStatus。
- 运行时 revision 2 人工确认后显示 current coverage；published save 到 revision 3 及 restore 到 revision 4 后均显示 ChangedSinceConfirmation。

## HumanConfirmation Revision Context

- KnowledgeDocument HumanConfirmation drawer 明确显示“本次人工确认将覆盖当前显示的修订 N”，只发送 current detail 的 `currentRevisionNumber`，不接受用户手工 revision。
- 运行时在 revision 2 创建 HumanConfirmation，SQLite snapshot 精确为 2。
- stale `409 conflict` 保留 confirmation facts，要求用户显式 reload；刷新 revision 后禁止 auto-retry，用户必须再次 Save。
- focused tests 覆盖 current revision request 与 stale preserve/reload/reconfirm。

## REV-B01/B02/B03 Regression

- backend focused selection 覆盖 revision foundation/create/save/no-op、revision reads/history、Evidence/status、KnowledgeDocument search/FTS、global search、authorization 与 Restore，共 **42/42 PASS**。
- frontend affected regression 覆盖 Detail/Editor、History、Restore、Compare/Myers diff、contracts/markdown/edit state、HumanConfirmation/Evidence，共 **12 files / 51 tests PASS**。
- Compare XSS-safe rendering、current/latest markers、MigrationBaseline display、dirty guard 与 no Restore in Compare 保持通过。

## Build / Tests / Lint

- `dotnet build SystemKnowledgeHub.sln --no-restore` — PASS，0 warnings，0 errors。
- Restore backend focused integration — 4/4 PASS；相关 backend regression selection — 42/42 PASS。
- Restore/frontend focused run — 31/31 PASS；扩大 affected regression — 51/51 PASS。
- `npm run type-check` — PASS。
- `npm run build` — PASS；只有既有 Vite chunk-size advisory。
- REV-B04 modified-scope ESLint — PASS。
- `git diff --check` — PASS（仅 Windows CRLF conversion notice，无 whitespace error）。
- 未修改 task 外的 repo-wide lint baseline；既有 `CreateIntegrationDialog.vue` unused props 与 `unknownItemContracts.ts` empty interface 不在本 Slice 处理。

## Browser → API → EF Core → SQLite Runtime

- 使用独立临时 SQLite、独立 Data Protection keys、disposable Local Administrator、API `5124` 与 Vite `5191`。
- 完整链路通过：Create revision 1 → Save revision 2 → Evidence → explicit Inferred → HumanConfirmation(revision 2) → explicit Confirmed → Publish → Published edit warning → Ctrl+S Cancel → Ctrl+S Confirm → revision 3 Published → Return Draft → preview revision 1 → Restore with reason → revision 4 Restore。
- UI 验证 current content=revision 1、Draft/Confirmed、ChangedSinceConfirmation、Evidence/HumanConfirmation 保留、History restore metadata、1→4 Compare 与 no history deletion。
- SQLite 只读断言的 16 项 invariant 全部为 `true`，包括 trusted actor、restore lineage、exact pointer/time preservation、Version +1、FTS 和四条 revision。
- 未观察到业务 API 或 application exception；development logs 中有 unchanged `KnowledgeDocumentEditor` 的既有 `el-tooltip` warning，以及 viewport override 触发的 Vite `ResizeObserver` warning。两者不影响本 Slice 行为或 production build，未越权修改无关文件。

## Network Smoke

- Restore 保持同一 SPA detail URL，成功后直接采用 response detail 并执行必要的 history/detail refresh；无 full browser reload、automatic retry 或 unrelated API storm。
- runtime 只产生 revision 4；未产生 user-triggered revision 5，证明 Restore 无 duplicate submit。
- Published confirm 只产生 revision 3；Cancel 没有产生 revision，Confirm 没有双提交。
- conflict/invalid-state/reload 路径的 unit tests 明确验证无自动 retry。

## Responsive / Accessibility

- 低频逐一检查精确视口：1920×1080、1714×892、1366×768、1024×768；每次变更间隔 1.2 秒，结束后 reset viewport。
- Detail、History、historical preview、Restore Dialog、reason textarea、warning 与 action buttons 均可见且可操作；1024×768 dialog 未丢失任何关键控件。
- reason/confirmation fields 有明确 label，warning 与 error/loading 使用文字而非只靠颜色，button 文案描述业务动作，confirmation 支持 keyboard path。

## Cleanup

- 关闭本次 browser tab；browser session tab list 为空。
- 只向本次 Vite PTY 与 API PTY 发送 Ctrl+C；未按进程名、父进程或 process tree 批量终止。
- 停止前精确确认 listener：API PID 22560 / port 5124，Vite PID 20564 / port 5191；停止后 listener count=0。
- 临时 SQLite、disposable user（随临时 DB）、Data Protection key 与临时目录已删除。
- repository `App_Data` git status 为空；未触碰 pre-existing process 或开发数据。

## Explicitly Not Implemented

- 未实现 Approval/Pending Review/reviewer、branch/merge/CRDT、revision edit/delete、revision-scoped Evidence/Status/Relationships、historical FTS、attachment/comments/notifications、AI/RAG 或 generic rollback/workflow framework。
- 未从 Compare 提供 Restore，未自动改变 Lifecycle 或 KnowledgeStatus，未实现跨 Document restore。

## PHASE-REV-VERIFY Readiness

REV-B04 是 PHASE-REV 最后一个开发 Slice，所有 PASS gates 已满足并已清理验证资源。当前可以进入：

`PHASE-REV-VERIFY — Revision & Change Safety End-to-End Verification`

本任务不开始 REV-B05 或任何新 Feature Slice，等待人工 Verification Gate。
