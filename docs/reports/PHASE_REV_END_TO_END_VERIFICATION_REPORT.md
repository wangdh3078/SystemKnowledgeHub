# PHASE-REV Revision & Change Safety End-to-End Verification Report

## Result

**PHASE-REV-VERIFY PASS WITH FOLLOW-UPS**

REV-B01～B04 的核心 revision safety 成立：canonical head 与 immutable history 分离，revision 连续且旧行不可变，`Version` 与 `RevisionNumber` 独立，current/latest-published pointers、semantic save/no-op、Draft/Published/Archived、Restore-as-new、FTS current-only、trusted actor 与并发拒绝均通过静态、自动化和隔离运行时验证。

独立审计确认 **5 个 Medium、6 个 Low** 缺口；没有 Blocker/High，也没有发现历史被覆盖、partial write、错误 pointer、权限扩大或 repository database 污染。Medium 缺口集中于 Unicode validation / error details、HumanConfirmation 前端跨 Slice 刷新与一项旧 migration test fixture，不破坏核心 revision integrity，因此按冻结分级规则为 `PASS WITH FOLLOW-UPS`，不是纯 `PASS`。

## Worktree Baseline

- 分支：`main`，开始时 `main...origin/main [ahead 1]`。
- 基线提交：`ff868a587d8ad63b2118c56cd5a0f4f6a351c8ce`（`ff868a5 ✨ feat: 恢复修订与已发布变更安全`）。
- 开始时 `git status --short`、`git diff`、`git diff --check` 均无产品代码差异。
- 本 Gate 没有修改 production code、schema、migration、Golden UI 或 frozen specification；仅生成本报告与 Gap Register。

## Normative Authority

按 `AGENTS.md` 顺序复核 frozen MVP UI / design / domain / database / use-case / API / solution structure，并完整复核：

- `docs/design/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md`
- `docs/reports/REV_B01_IMMUTABLE_REVISION_FOUNDATION_VERIFICATION_REPORT.md`
- `docs/reports/REV_B02_REVISION_HISTORY_READ_UX_VERIFICATION_REPORT.md`
- `docs/reports/REV_B03_REVISION_COMPARE_UX_VERIFICATION_REPORT.md`
- `docs/reports/REV_B04_RESTORE_AND_PUBLISHED_CHANGE_SAFETY_VERIFICATION_REPORT.md`
- `PHASE-REV-VERIFY — Revision & Change Safety End-to-End Verification`

实现证据优先于既有 Slice 自证报告。发现的实现/报告或 frozen contract 漂移均记录于 `PHASE_REV_GAP_REGISTER.md`，未为取得 PASS 而修改权威文档。

## Architecture Inventory

| Area | Current implementation | Audit result |
|---|---|---|
| Canonical head | `knowledge_documents` / `KnowledgeDocument` | PASS |
| Immutable child | `knowledge_document_revisions` / `KnowledgeDocumentRevision` | PASS |
| Write use cases | Create、semantic Content Save、Restore 三个 revision insert site | PASS |
| Reads | current detail、DB-paged history list、immutable revision detail | PASS |
| Compare | frontend-only deterministic Myers；无 backend compare endpoint | PASS |
| Restore | explicit `POST .../revisions/{revisionNumber}/restore` | PASS |
| Concurrency | Document `Version` 的单一 opaque token | PASS |
| Search | current Document head rowid 的 FTS5 index | PASS |
| Evidence / Relations | target canonical `KnowledgeDocument`；HumanConfirmation 另存 revision snapshot | PASS |
| Actor / time | `ICurrentUserContext` 与 server clock | PASS |

没有 Revision Repository、generic rollback、CQRS framework、revision edit/delete、revision-level ACL/Status/Relationship/Evidence target 或第二套 overlay manager。

## Frozen Invariant Audit

| Invariant | Evidence | Result |
|---|---|---|
| Canonical head 可变、历史 child 不可变 | 生产代码只 `Add` Revision，无 update/delete；旧行 hash 复核 | PASS |
| Revision number 正整数且每 Document 连续 | unique `(document, revision)` + runtime `1,2,3,4,5` | PASS |
| Current pointer 指向 head snapshot | final head 与 Revision 5 的 Title/Summary/Body 相同 | PASS |
| Latest-published pointer 表示最后真实发布 snapshot | R3 publish → pointer 3；R5 re-publish → pointer 5；Archive 保留 5 | PASS |
| Version 与 RevisionNumber 独立 | final `Version=11`、`CurrentRevisionNumber=5` | PASS |
| Lifecycle/status action 不制造 revision | publish、return draft、re-publish、archive 均保持 revision count | PASS |
| KnowledgeStatus 独立 | Evidence/HC/save/restore/lifecycle 未自动推进或回退 | PASS |
| Trusted audit | revision/restore/HC actor 和时间来自 server | PASS |

## Schema / Migration

- Migration 实数：**18**；latest：`20260823092808_AddImmutableKnowledgeDocumentRevisions`。
- `dotnet ef migrations list` 与 `dotnet ef migrations has-pending-model-changes`：migration 链完整、**无 pending model changes**。
- Revision schema 包含 unique `(knowledge_document_id, revision_number)`、positive revision、origin/lifecycle/actor/restore metadata checks 与两个 restrictive FK。
- Revision 没有独立 `Version`、KnowledgeStatus、Evidence/Relationship target 或 ACL column。
- REV-B02～B04 没有新增 schema；MigrationBaseline projection 仍 truthful。

Repository `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` 以 immutable/read-only SQLite 与 EF `Mode=ReadOnly` 打开：`PRAGMA integrity_check=ok`、`foreign_key_check=0`、migration history 18/latest 正确；current/published pointer、FTS 与 MigrationBaseline 均无 violation。审计前后 length=`507904`、mtime=`2026-08-23T09:50:37.0173536Z`、SHA-256=`FCA1B1B7B3CBDC44E16EDA09E296C6C7E0DAD2AEBC8E975B8EFC2026135251FB` 完全不变；未运行 repository DB migration，文件不参与隔离 runtime。

## Revision Creation / Save / No-op

- Browser 建立 `PHASE-REV 总验收文档`：Create 同事务生成 head + Revision 1 + FTS，`Origin=Created`、Draft/Unknown、current=1。
- semantic save 修改 Summary 为 `CONFIRMED-R2-PHASE`，生成 Revision 2，`Origin=ContentSave`，current=2。
- 随后只加入 leading/trailing whitespace 的 canonical-equal save 返回成功但 revision count、pointer 与 token 均不变化，history 仍为 2。
- 后续 Published save 生成 R3、Draft save 生成 R4；所有 real content change 恰好生成一个 revision。

## Concurrency

隔离 API 使用第二个 disposable Document 做交叉流探针：

1. R1 Create → R2 Content Save。
2. 以 R2 token Restore R1 → R3。
3. 再用 R2 stale token Content Save → **409 `conflict`**，无新 revision。
4. 以 R3 token Content Save → R4。
5. 再用 R3 stale token Restore → **409 `conflict`**，无新 revision。

最终 history total=4 且 revisions=`1,2,3,4`。Content Save、Restore、Lifecycle、KnowledgeStatus 共用 Document `Version` token；未发现第二并发机制或 partial write。

## Lifecycle / Published Change Safety

- R2 Draft/Confirmed → Publish：没有新 revision，latest-published=2，KnowledgeStatus 保持 Confirmed。
- Published Edit 持续显示冻结警告：`保存后新内容立即成为已发布内容并生成新修订。`
- dirty Save 首次打开确认并 Cancel：没有请求提交、dirty content 保留、history 仍为 2。
- 同一 dirty state 执行 Ctrl+S，再确认 `确认保存并立即发布`：只生成 R3，R3 `LifecycleContext=Published`，head 仍 Published，latest-published=3。
- Return Draft：不生成 revision；Draft save 生成 R4，latest-published 保留 3。
- Restore 得到 R5 后显式 re-publish：不生成 revision，latest-published=5；Archive：不生成 revision，published pointer/time 保留。
- final API archived-save probe 使用 current token 返回 **409 `invalid_state`**，body 明确 `已归档文档不允许修改内容。`；R5 未被修改。

## History

- history list newest-first、DB pagination、list DTO 不含 BodyMarkdown，revision detail 不暴露 concurrency token。
- R4 时 UI 正确标记 `当前版本`=4、`最近发布`=3；R5 re-publish 后同一 R5 同时显示 current/latest-published。
- preview R2 显示 immutable author snapshot、created time、Draft lifecycle context 与当时 Summary/Body。
- Restore history entry显示 `历史恢复`、`从修订 2 恢复` 与 normalized reason；后续 revision 未删除。
- static query audit 未发现 User join/N+1 或 load-all history。

## Compare

- 无 compare API、无 persisted diff；frontend 分别读取 immutable snapshots。
- Browser 2→4 显示 old→new Summary change，Title/Body 未变化；2→5 精确显示 `两个修订的标题、摘要和正文内容一致。`
- deterministic Myers、plain-text escaping、safe Markdown 与 XSS regression 通过 affected tests。
- exact combined-content/line limit 允许，one-over 阻断且不产生 partial diff；5,000×5,000 行最坏边界探针约 **241 ms**，未冻结 Browser。

## Restore

- 仅 current Draft 可 Restore；source 必须属于同一 Document、是 historical 且与 current 非 semantic-identical。
- Browser 从 R4 恢复 R2，生成 R5：`Origin=Restore`、`RestoredFromRevisionNumber=2`、reason=`总验收恢复修订二确认基线`、`LifecycleContext=Draft`。
- R5 head 与 R2 snapshot 的 Title/Summary/Body 一致；R1～R4 全部保留。
- Restore 未修改 DocumentType、Confirmed status/audit、两条 Evidence、HumanConfirmation snapshot、Relationships、published pointer/time。
- invalid id/revision、cross-document、current/identical source、Published/Archived、Viewer 与 stale token 由 focused/full integration tests覆盖；rollback transaction 对 head/revision/FTS 无 partial write。
- 既有 rollback test 未直接 assert `Version` rollback，作为 Low verification gap 记录，不把报告措辞扩大为“该断言已经直接存在”。

## HumanConfirmation Revision Context

- Browser 在 current Revision 2 打开 Drawer，明确显示：`本次人工确认将覆盖当前显示的修订 2。`
- SQLite final Evidence row：`HumanConfirmation` 的 `knowledge_document_revision_number_snapshot=2`；provider identity 来自 current user。
- stale HC transaction no-write 行为正确；但 frozen 409 details 缺 `currentRevisionNumber`，见 `REV-GAP-002`。
- Evidence 区域入口 runtime 成功；Knowledge Progression Panel 的同名快捷入口漏传 revision number，见 `REV-GAP-003`。

## Confirmation Coverage

- backend projection 四态（NoConfirmation / LegacyConfirmationUnknown / CurrentRevisionConfirmed / ChangedSinceConfirmation）与 integrity guard 通过 focused tests/static audit。
- runtime R2 HC 后显式 Confirmed，UI 显示 current coverage；R3 save、R4 save、R5 restore 后显示 `内容在最近一次确认后已修改`，KnowledgeStatus 仍 Confirmed。
- HC save 后 Detail 仅重载 Evidence、不立刻重载 cached coverage；若用户不继续 status transition/reload，提示可能陈旧，见 `REV-GAP-004`。

## KnowledgeStatus Independence

- Create、Evidence、HumanConfirmation、content save、publish/draft/archive、restore 均未自动改变 KnowledgeStatus。
- runtime 只有显式 `Unknown → Inferred → Confirmed` actions 改变 status。
- Restore R2 内容没有把 Confirmed 回退；coverage 和 KnowledgeStatus 保持两个不同概念。

## Trusted Actor

- Create/save/restore revision actor、status actor/time 与 HumanConfirmation identity 全由 authenticated `Current User` / server time 生成。
- Restore request authoritative fields只有 opaque token 与 reason；client 不能提交 snapshot content、author 或 time。
- history 使用 immutable display-name snapshot，不依赖当前 User 名称。

## FTS Current-only

- FTS 只按 canonical Document head rowid 删除/重建，不索引 revision table。
- Restore 后 Browser 搜索 `DRAFT-R4-PHASE` 无结果，搜索 `CONFIRMED-R2-PHASE` 返回当前 Document。
- final read-only SQLite row包含 restored R2 unique term，排除 R3/R4 unique term；historical snapshots仍可经 History 获取但不污染全局搜索。

## Relationships / Evidence / Unified View Regression

- master Document final Evidence=2：CodeReference 1 + HumanConfirmation 1；HC snapshot=2；Restore/Archive 后数量与内容保持。
- master Document Relationship count 从 0 到 final 0；focused relationship tests 与 static target audit确认 Restore 不写 Relation。
- Unified View 继续查询 current `KnowledgeDocuments`，不读取 revision table；相关 full backend regression覆盖。
- Evidence/Relationship target仍是 canonical `KnowledgeDocument`，未引入 revision-scoped knowledge semantics。

## Authorization Matrix

| Capability | Viewer | Editor | Administrator | Result |
|---|---|---|---|---|
| Read current/revisions/compare | Allow | Allow | Allow | PASS |
| Edit content | Deny | Allow | Allow | PASS |
| Restore | Deny | Allow | Allow | PASS |
| Lifecycle / KnowledgeStatus | existing policy | Allow | Allow | PASS |
| HumanConfirmation | existing contract | existing contract | existing contract | PASS |

Compare 没有 server write capability；route/policy audit与 full tests未发现权限扩大或 IDOR。

## API Contract Drift Audit

- PASS：无 compare API；无 Revision PUT/PATCH/DELETE；revision list/detail、content save、lifecycle/status 与 Restore route/request shape未漂移；client actor/time不是 authoritative。
- Medium drift：stale HumanConfirmation 409 details 缺 frozen `currentRevisionNumber`（`REV-GAP-002`）。
- Medium validation drift：supplementary Unicode restore reason可能由 service 放行、SQLite check拒绝并变成 500（`REV-GAP-001`）；transaction仍 rollback。

## Database Invariants

隔离 SQLite final master Document：

| Check | Final value |
|---|---|
| `PRAGMA integrity_check` | `ok` |
| `PRAGMA foreign_key_check` | 0 rows |
| Lifecycle / KnowledgeStatus | Archived / Confirmed |
| Version / current revision | 11 / 5 |
| latest published | 5 |
| revisions | contiguous 1～5 |
| origins | Created, ContentSave, ContentSave, ContentSave, Restore |
| Evidence / HC snapshot | 2 / revision 2 |
| Relationships | 0 |
| FTS | current restored R2 only |
| migration history | 18 / expected latest |

## Revision Immutability

使用所有 revision columns 的 canonical JSON SHA-256，在 Restore/republish/archive 前后比较 R1～R4：

| Revision | Final hash | Pre-Restore unchanged |
|---|---|---|
| 1 | `8eb32c336b083b2671dbebbc0740410136fdc3788d1604c43dd66d6f41ac8865` | yes |
| 2 | `b126e0c6110b38fde77b7abe41b75054471ad2acbbb294d29b51e28edb18bbbe` | yes |
| 3 | `5b7f692a1d7df631e17b2a232183ef30315c6df4cd8aa416f28deede1ab0ba92` | yes |
| 4 | `e95928bf4d25dffd88cf1d070a130c3ec3e566fda90fe28054ca5eb9b171276f` | yes |
| 5 | `a74e325b5c11f66b0c3da38433b74ebf60f778a740c308565819b13409894de5` | new Restore row |

Title、Summary、BodyMarkdown、Origin、LifecycleContext、author snapshot、CreatedAt 与 restore metadata 均包含在 hash 中；后续 lifecycle actions未改变旧 row。

## Browser Master Scenario

使用 in-app Browser + isolated runtime 完成：

`login → R1 create → R2 save → canonical no-op → Evidence → Inferred → HC@R2 → Confirmed → Publish → Published dirty Save/Cancel → Ctrl+S/Confirm → R3 Published → Return Draft → R4 save → History markers → R2 preview → Compare 2→4 → Restore R2 as R5 → Compare 2→5 identical → FTS current-only → re-publish R5 → Archive → archived save 409`

所有关键 UI 文案、revision count、markers、restore lineage 与 final state均与 SQLite/API 交叉验证。未发生 duplicate submit；master final current revision恰为 5。

## Responsive / Accessibility

- 低频依次验证精确视口 1920×1080、1714×892、1366×768、1024×768，每次等待至少 1.2 秒，结束后 reset override。
- History、current/latest markers、Compare selectors/cards/diff 与返回 action在四个宽度均保留且可操作；1024 使用窄屏 history layout，无关键 action 丢失。
- keyboard Ctrl+S published confirmation、labels、文字状态/错误与 focusable controls通过 smoke。
- Low gaps：Restore outer dialog landmark无 accessible name（`REV-GAP-006`）；History 在 shell `<main>` 内嵌套第二个 `<main>`（`REV-GAP-007`）。

## Network / Performance Smoke

- master scenario 未出现业务 API 5xx、network failure、full reload、automatic retry 或 API storm；预期 stale/archived probes为受控 409。
- 缺 antiforgery header 的 probe被正确拒绝；取得 token/cookie 后 login/write成功。
- API warning log无 application exception。Browser dev log有 5 次 `el-tooltip` unresolved warning（`REV-GAP-005`）；viewport override期间 Vite dev client另记录既有 `ResizeObserver loop` warning，但 UI/requests均未失败。
- History 为 DB pagination且 list不加载 Body；query projection无 N+1；Compare bounded；5,000×5,000 line probe约 241 ms；Restore transaction正常。
- default parallel backend full run会同时启动大量 SQLite/WebApplicationFactory collections并停滞；serial collection run 22s完成，见 `REV-GAP-011`。

## Build / Tests / Lint

- `dotnet build SystemKnowledgeHub.sln --no-restore` — **PASS，0 warnings / 0 errors（3.86s）**。
- full backend test project（`xUnit.ParallelizeTestCollections=false`）— **120 passed / 1 failed / 0 skipped，total 121，22s**；唯一 deterministic failure为旧 migration preservation test在 pre-revision schema 上用 current Evidence model seed，引用尚不存在的 `knowledge_document_revision_number_snapshot`。这是 test fixture/model-time mismatch，不是 current schema/repository corruption，但该 legacy preservation assertion尚未被证明，见 `REV-GAP-010`。
- default parallel full run在 15.27min仍无汇总；精确停止本次 testhost后，用 2min blame-hang确认 21个 collections同时未完成，未定位单一 hung test。serial full run稳定结束，见 `REV-GAP-011`。
- `npm run type-check` — PASS。
- `npm run build` — PASS，2133 modules；仅既有 chunk-size advisory。
- affected Vitest — **16 files / 65 tests PASS**。
- full Vitest — **31 files：30 passed / 1 known-baseline failed；99 tests：98 passed / 1 failed**。唯一失败为已多次记录的 `AppShell.spec.ts` 陈旧“关系与缺口”断言，不是 PHASE-REV regression。
- modified-scope ESLint — PASS。
- full ESLint — 仍只有两个 frozen baseline：`CreateIntegrationDialog.vue` unused props、`unknownItemContracts.ts` empty interface；无扩散。
- `git diff --check` — PASS。

## Gap Register

详见 `docs/reports/PHASE_REV_GAP_REGISTER.md`：

- Medium：REV-GAP-001～004、REV-GAP-010。
- Low：REV-GAP-005～009、REV-GAP-011。
- Blocker/High：0。

这些 gap 不允许将结果写为纯 PASS；也没有一个 gap 造成 immutable history、pointer、atomicity、authorization 或 FTS corruption。

## Cleanup

- Browser viewport override已 reset；本次 agent-created tab已关闭。
- task-owned listeners：API `127.0.0.1:5126` PID 27072 (`SystemKnowledgeHub.Api`)；Vite `127.0.0.1:5193` PID 23276 (`node`)。
- PTY Ctrl+C 后 listener未立即释放；经端口与 process name精确复核后，只对 PID **27072、23276** 执行 exact `Stop-Process`。未按 name、parent/process tree 或 wildcard终止，未触碰 Codex；随后两端口 listener=0。
- 已删除精确临时目录 `skh-phase-rev-verify-5fd18690e99344f79595563a422ea1d1`，包含 isolated SQLite/WAL/SHM、disposable Local account data 与 DP keys；`ExistsAfter=false`。
- parallel-test hang diagnostic 的精确 `TestResults/a6a5e9ce-d2d4-4c2f-94ca-c401996b80a6`（约 533 MB dump + sequence）经路径验证后删除；未删除其它 TestResults，final testhost count=0。
- repository `App_Data` 未迁移、未写入、未删除；未运行 `git clean/reset/gc/prune`。

## Product Readiness

产品边界仍严格为：**Internal Pilot**。

不得声明 Production Ready / Team Production Approved。SEC-04、HTTPS/real OIDC/reverse proxy、production Data Protection、backup/restore、SQLite production strategy、observability 与 deployment/rollback 仍属于独立 Production Engineering Gate。

## Recommended Next Phase

不要自动开始 PHASE-TRACE 或其它 feature。先由人工批准一个 narrow PHASE-REV follow-up slice，优先处理 REV-GAP-001～004、REV-GAP-010，并补相应 contract/integration tests；随后重跑 affected gates 与 PHASE-REV delta verification。

Medium gaps关闭并取得纯 PASS 后，再由人工在 `PHASE-TRACE — Requirement Traceability Experience`、Production Engineering Gate 或其它批准 major phase中选择。当前任务到报告与提交为止立即停止。
