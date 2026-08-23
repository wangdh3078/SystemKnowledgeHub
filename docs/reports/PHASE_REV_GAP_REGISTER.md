# PHASE-REV Gap Register

## Boundary

PHASE-REV-VERIFY 的历史结果为 **PASS WITH FOLLOW-UPS**。本清单保留独立阶段审计确认的 5 个 Medium、6 个 Low gap及其原始复现/风险记录；Blocker/High 为 0。`REV-FIX-01` 已于 2026-08-23 关闭全部 5 个 Medium gap，6 个 Low gap仍为明确延期项。

| ID | Severity | Status | Area | Summary |
|---|---|---|---|---|
| REV-GAP-001 | Medium | **CLOSED — REV-FIX-01** | Restore / validation | .NET 与 SQLite 对 supplementary Unicode 长度语义不同，可能把 invalid reason变成 500 |
| REV-GAP-002 | Medium | **CLOSED — REV-FIX-01** | HumanConfirmation API | stale 409 details 缺 frozen `currentRevisionNumber` |
| REV-GAP-003 | Medium | **CLOSED — REV-FIX-01** | HumanConfirmation UX | Knowledge Progression Panel 快捷入口漏传 `subjectRevisionNumber` |
| REV-GAP-004 | Medium | **CLOSED — REV-FIX-01** | Confirmation Coverage UX | HC save 后 detail coverage cache未刷新 |
| REV-GAP-005 | Low | OPEN / Deferred | Editor runtime UX | `ElTooltip` 未注册，toolbar tooltip丢失并产生 Vue warnings |
| REV-GAP-006 | Low | OPEN / Deferred | Accessibility | Restore ancestor dialog没有 accessible name |
| REV-GAP-007 | Low | OPEN / Deferred | Accessibility | Revision History 在 shell `<main>` 内嵌套第二个 `<main>` |
| REV-GAP-008 | Low | OPEN / Deferred | Single overlay | Published save的直接 `ElMessageBox` 缺现有 overlay guard |
| REV-GAP-009 | Low | OPEN / Deferred | Verification evidence | Restore rollback test未直接 assert `Version` rollback |
| REV-GAP-010 | Medium | **CLOSED — REV-FIX-01** | Migration test fixture | pre-revision schema seed误用 current Evidence model，full backend suite留下 1项 deterministic failure |
| REV-GAP-011 | Low | OPEN / Deferred | Test infrastructure | default parallel full backend run因 SQLite/WebApplicationFactory collection并发停滞 |

## REV-FIX-01 Closure Summary

- `REV-GAP-001 CLOSED` — Restore reason 先 trim，再拒绝 NUL，并按 Unicode scalar（`EnumerateRunes()`）执行 5～500 validation；focused boundary tests、serial full backend 123/123 与 real API 3/5 emoji runtime均通过。
- `REV-GAP-002 CLOSED` — stale HumanConfirmation 409 的 frozen details精确包含 `resourceType`、`resourceId`、`currentRevisionNumber`；exact-key/no-write test与 runtime N→N+1 probe均通过。
- `REV-GAP-003 CLOSED` — Knowledge Progression Panel 将 Detail `currentRevisionNumber` 透传至 overlay、Drawer与 API request；跨组件 integration test和 prominent-path Browser runtime均通过。
- `REV-GAP-004 CLOSED` — HumanConfirmation success派发定向事件，Detail重新获取 backend authoritative projection；Evidence仍独立刷新，sequence guard阻止旧 request覆盖，无 frontend coverage重算；三种旧 coverage state、error与 request-count tests及 Browser即时更新均通过。
- `REV-GAP-010 CLOSED` — pre-revision Evidence改用 target-era explicit-column raw SQL seed；preservation、migration history、integrity/FK test通过，serial full backend 123/123。

完整证据见 `docs/reports/REV_FIX_01_MEDIUM_GAPS_CORRECTION_VERIFICATION_REPORT.md`。

## REV-GAP-001 — Restore Unicode length mismatch

- **Severity:** Medium
- **Area:** Backend / Restore validation / Error contract
- **Reproduction:** 使用 reason=`😀😀😀`。`.NET string.Length=6`，通过 service 的 5～500 check；仓库实际 SQLite `length(trim(value))=3`，触发 `ck_knowledge_document_revisions_restore`。
- **Expected:** invalid reason返回 `400 validation_error`，且不进入数据库异常路径。
- **Actual:** SQLite constraint拒绝；Restore只捕获 `DbUpdateConcurrencyException`，`DbUpdateException` 可上升为 500。transaction仍 rollback。
- **Risk:** 边界 Unicode 输入得到错误 status/error shape；没有 data corruption。
- **Recommended fix slice:** 用 SQLite-compatible Unicode scalar count统一 service/API validation并明确 NUL policy；新增 supplementary Unicode、NUL、trimmed 4/5/500/501 boundary integration tests。
- **Closure (REV-FIX-01):** **CLOSED**。采用 trim → NUL rejection → Unicode scalar count；invalid路径在 persistence前返回 400。Focused tests覆盖 3/5 emoji、trimmed 4/5、500/501、NUL、whitespace及 head/Version/revision/FTS no-write；real API验证 3 emoji=400、5 emoji=200、无 500。

## REV-GAP-002 — HumanConfirmation stale conflict details drift

- **Severity:** Medium
- **Area:** API contract / conflict recovery
- **Reproduction:** current revision前进后，以旧 `subjectRevisionNumber` 创建 KnowledgeDocument HumanConfirmation。
- **Expected:** `409 conflict` details包含 `resourceType`、`resourceId`、`currentRevisionNumber`。
- **Actual:** `EvidenceController` 只返回前两项；service拒绝与 no-write正确。
- **Risk:** frozen wire contract漂移，client无法从 error details直接识别最新 revision；核心 persistence安全。
- **Recommended fix slice:** 增加 `currentRevisionNumber` details并把现有 test从只断言 409/count扩展到 exact error contract。
- **Closure (REV-FIX-01):** **CLOSED**。Application result携带 server current revision，Controller frozen 409 details只有并完整包含三个要求字段。Exact contract test与 runtime均证明 Evidence、HumanConfirmation、head token/current revision、history和 KnowledgeStatus无变化。

## REV-GAP-003 — Progression Panel omits subject revision

- **Severity:** Medium
- **Area:** Frontend / HumanConfirmation cross-Slice integration
- **Reproduction:** KnowledgeDocument 为 Inferred 且无 HC，点击 Knowledge Progression Panel 的 prominent `添加人工确认`。
- **Expected:** Drawer payload包含 Detail传入的 current `subjectRevisionNumber`，请求绑定当前 revision。
- **Actual:** panel接收 prop但 `addHumanConfirmation()` 未放入 payload；request省略 required revision number，backend返回 400。Evidence section的独立入口正常。
- **Risk:** 主要引导路径不可完成 HC，用户必须找到替代入口；没有错误 snapshot写入。
- **Recommended fix slice:** 透传 prop到 overlay payload；增加 Detail → Panel → Drawer → API payload integration test。
- **Closure (REV-FIX-01):** **CLOSED**。Panel prominent action条件式透传 `subjectRevisionNumber`，集成测试覆盖 Detail revision 7 → Panel → overlay → Drawer → API；Browser runtime从 prominent path显示并成功保存 revision 1 HumanConfirmation。

## REV-GAP-004 — Confirmation coverage remains stale after HC save

- **Severity:** Medium
- **Area:** Frontend / confirmation coverage
- **Reproduction:** 在 `NoConfirmation` 或 `ChangedSinceConfirmation` Detail保存 HC后停留页面，不执行额外 status transition/reload。
- **Expected:** Evidence list与 `confirmationCoverage` 一起刷新，立即显示 current coverage。
- **Actual:** Drawer只派发 `evidence:changed`；Detail handler只 `loadEvidence()`，coverage来自 cached `data.confirmationCoverage`。
- **Risk:** 用户看到已保存 HC但 coverage警告陈旧；backend projection/data正确。
- **Recommended fix slice:** 成功事件触发 authoritative detail refresh（或返回/合并完整 projection），增加跨组件 state integration test，避免 frontend重算 coverage。
- **Closure (REV-FIX-01):** **CLOSED**。HC success后执行一次必要 Evidence refresh与一次 authoritative Detail refresh；Detail request sequence只采用最新 server response。Tests覆盖 NoConfirmation、ChangedSinceConfirmation、LegacyConfirmationUnknown、save error、out-of-order response及 exact request count；Browser无需手工 reload即显示 current coverage。

## REV-GAP-005 — KnowledgeDocumentEditor tooltip registration

- **Severity:** Low
- **Area:** Frontend runtime / editor UX
- **Reproduction:** 进入 KnowledgeDocument Edit；Browser/Vite重复记录 `Failed to resolve component: el-tooltip`。
- **Expected:** 10个 toolbar tooltip使用注册的 Element Plus component/style且无 console warning。
- **Actual:** editor模板使用 `<el-tooltip>`，selective bootstrap未注册 `ElTooltip`；unit test局部 stub掩盖 production问题。按钮仍可操作，hover help丢失。
- **Risk:** 辅助提示和运行时日志质量降低；不影响 content persistence。
- **Recommended fix slice:** 在现有 selective bootstrap注册 component/style，并增加 production-like mount smoke而非只依赖 stub。

## REV-GAP-006 — Restore dialog landmark is unnamed

- **Severity:** Low
- **Area:** Accessibility / dialog host
- **Reproduction:** 打开 Restore；accessibility snapshot显示 unnamed outer `dialog`，内部 `region "恢复修订 N"`有名称。
- **Expected:** ancestor `role=dialog`自身由可见标题命名。
- **Actual:** `DialogHost` 的 `el-dialog`没有 title/header binding；content section的 `aria-labelledby`不能命名祖先 dialog。
- **Risk:** screen reader用户难以辨识 modal purpose。
- **Recommended fix slice:** 让 overlay metadata向 DialogHost提供 title/`aria-labelledby`，并增加 dialog landmark accessible-name test。

## REV-GAP-007 — Nested main landmark in Revision History

- **Severity:** Low
- **Area:** Accessibility / landmarks
- **Reproduction:** 进入 History；route已在 `AppContentArea <main>` 内，History preview再次渲染 `<main>`。
- **Expected:** 页面只有一个 main landmark；preview使用 named `section/region`。
- **Actual:** nested/multiple main landmarks。
- **Risk:** screen reader landmark navigation层级无效；视觉行为正常。
- **Recommended fix slice:** 把 preview `<main>` 改为 accessible named section/region并加 landmark smoke test。

## REV-GAP-008 — Published confirm can bypass single-overlay guard

- **Severity:** Low
- **Area:** Frontend / overlay coordination
- **Reproduction:** static path：dirty Published edit保持 mounted，先打开 Global Search/hosted dialog，再从非-input focus触发 Ctrl+S。
- **Expected:** 新 confirmation替换/关闭当前 overlay或被 guard阻止，任何时刻单 modal。
- **Actual:** window save handler只检查 `canSave`，直接调用 `ElMessageBox.confirm`，不检查 overlay store；存在 modal stacking风险。未在本 Gate runtime强制复现。
- **Risk:** edge UX/focus trap冲突；请求仍需用户确认，核心 write安全。
- **Recommended fix slice:** 把 Published confirm纳入 single-overlay coordinator或在 save handler加 current-overlay guard；增加 Global Search + Ctrl+S integration test。

## REV-GAP-009 — Restore rollback Version assertion absent

- **Severity:** Low
- **Area:** Verification evidence
- **Reproduction:** 查看 `KnowledgeDocumentRestoreApiTests` forced revision-insert failure test。
- **Expected:** 报告若声明 Version rollback被直接测试，应有 before/after `head.Version` exact assertion。
- **Actual:** test直接断言 pointer/title/body/revision count/FTS；同 transaction行为强烈支持 Version rollback，但没有单独 Version assertion。B04报告措辞比直接断言更强。
- **Risk:** evidence precision不足，不代表已发现 implementation rollback bug。
- **Recommended fix slice:** 增加一条 Version exact assertion，或在历史报告后续勘误中收敛措辞；不需要新测试框架。

## REV-GAP-010 — Legacy migration preservation fixture uses current model too early

- **Severity:** Medium
- **Area:** Backend test / migration verification
- **Reproduction:** 单独运行 `Persistence.KnowledgeDocumentMigrationTests.Migration_from_pre_knowledge_document_latest_preserves_existing_security_evidence_system_and_relationship_rows`。Fixture只迁移到 `20260822025403_AddOidcAuthenticationFoundation`，随后用 current EF Evidence model seed。
- **Expected:** 用 target-era columns在 pre-revision schema建立 Evidence，再 migrate latest并证明 security/evidence/system/relation rows完整保留。
- **Actual:** current model INSERT引用后续 migration才添加的 `knowledge_document_revision_number_snapshot`，SQLite Error 1；serial full suite为 120 passed / 1 failed。current schema、repository DB与其它120项测试正常。
- **Risk:** legacy upgrade preservation assertion未被有效执行；当前失败不是 production migration/data corruption证据，但 full backend gate不能标为全 PASS。
- **Recommended fix slice:** 在该 fixture用只包含目标 migration当时 columns的 raw SQL（或 target-era model）seed，再 migrate latest并保留现有 preservation assertions；isolated test与serial/full suite都必须通过。
- **Closure (REV-FIX-01):** **CLOSED**。Fixture仍只先迁移至 `20260822025403_AddOidcAuthenticationFoundation`，Evidence用 target-era raw SQL seed；latest migration后 security/Evidence/System/Relationship、snapshot null、完整 migration history、`integrity_check=ok`与 FK=0全部通过。Focused migration 1/1、serial full backend 123/123。

## REV-GAP-011 — Parallel backend suite stalls

- **Severity:** Low
- **Area:** Test infrastructure / deterministic verification
- **Reproduction:** default parallel `dotnet test` 运行 15.27min仍无汇总；2min blame-hang在19项通过后显示21个 collections同时 `Completed=False`。同一二进制关闭 collection parallelism后 22s完成121项。
- **Expected:** documented full backend command在受控 Windows环境稳定结束并给出可靠汇总。
- **Actual:** 大量 SQLite/WebApplicationFactory collections并发导致资源饥饿/停滞，需 exact testhost cleanup；没有定位单一 product test deadlock。
- **Risk:** CI/agent误判 hang、浪费资源或需要不安全 cleanup；serial结果仍可判定功能测试。
- **Recommended fix slice:** 在现有 xUnit configuration中有界或禁用 collection parallelism，保持 test logic不变；验证 default documented command可稳定完成，不引入新测试框架。

## Existing Unrelated Baselines

以下已在先前报告记录，不计为本 Phase 新 REV gap：

- full Vitest 的 `AppShell.spec.ts` 陈旧 `关系与缺口` expectation。
- full ESLint 的 `CreateIntegrationDialog.vue` unused props。
- full ESLint 的 `unknownItemContracts.ts` empty interface。
- Vite dev viewport override期间的 `ResizeObserver loop` warning；四个 required viewport仍完成且无功能失败。

处理这些 baseline需单独批准，PHASE-REV Verification不顺手修改。
