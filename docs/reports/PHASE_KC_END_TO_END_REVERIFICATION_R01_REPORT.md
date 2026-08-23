# PHASE-KC-VERIFY-R01 — Knowledge Content End-to-End Re-Verification Report

## Result

```text
PHASE-KC-VERIFY-R01 PASS WITH FOLLOW-UPS
```

KC-GAP-001 and KC-GAP-002 are closed. No new BLOCKER or High issue was found. KC-GAP-003 remains an independent Medium architecture-conformance follow-up and was not changed in this phase.

## Baseline and Scope

`git status`, `git diff --stat`, and `git diff` were reviewed before verification. The worktree already contained substantial, unrelated and uncommitted AUTH, KC, UI, documentation, migration, test, and untracked feature work. R01 did not reset, clean, revert, format, or overwrite it.

R01 changed only this report and the status summary in `PHASE_KC_GAP_REGISTER.md`. No production code, API, database schema, migration, authentication behavior, authorization behavior, KnowledgeDocument behavior, or business logic was changed.

## Blocking Gaps Recheck

| Gap | R01 result | Evidence |
| --- | --- | --- |
| KC-GAP-001 — frontend build gate | **CLOSED** | `npm run type-check` and `npm run build` both passed after KC-FIX-01. |
| KC-GAP-002 — request-supplied KnowledgeStatus actor | **CLOSED** | The focused integration suite sent the legacy `FORGED ADMIN` JSON actor and asserted persisted audit identity `SEC-01 Test Principal` / `Administrator` with a server, non-forged time. The isolated runtime accepted the unknown legacy actor property but returned a server time (`2026-08-23...`), not the forged `2099-01-01` value. |
| KC-GAP-003 — relationship vocabulary | **OPEN / Medium** | Still an approved-plan conformance decision only; R01 observed no corruption, authorization bypass, or operational blocker. No fix was attempted. |

## Build and Focused Regression

| Command | Result |
| --- | --- |
| `npm run type-check` | PASS |
| `npm run build` | PASS; Vite emitted only its chunk-size advisory. |
| Targeted Vitest: SecurityGate, authentication API, AppTopBar/logout, Global Create, editor, dirty state, Milkdown round-trip, renderer security, detail, search, Unified View | PASS — 11 files, 31 tests. |
| Scoped `npx eslint --quiet` over the verified frontend surfaces | PASS — no errors. |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors. |
| Focused backend tests: LocalLogin, CurrentUser, AccessControl, KnowledgeDocuments, KnowledgeStatus, Evidence/Status, Relationships, Search/FTS, SystemKnowledgeView | PASS — 35 passed, 0 failed, 0 skipped. |

The automated backend path is real Controller → `ICurrentUserContext` → application service → EF Core/SQLite, rather than a mocked actor boundary.

## Authenticated Browser Chain

An isolated local-only runtime was created with a temporary Administrator and a separate SQLite database. The browser completed:

```text
Login → Dashboard → Global Create → SOP Draft/Unknown
→ Open Detail → Edit title/summary/body
→ Heading / Bold / List / Quote / Code Block / Table toolbar commands
→ Preview → Save → Reload
```

The reloaded detail preserved the updated title, summary, Markdown heading, blockquote/code and table. The created document was `Oracle 数据库连接异常处理 SOP`; the title intentionally did not contain “监听”, while its Markdown body did.

Dirty-state behavior was also exercised: editing made the save state `未保存`; Cancel opened `放弃编辑`, and confirming `放弃修改` returned to the saved document rather than silently discarding changes. The focused AppTopBar and detail tests continue to cover unsaved logout protection and concurrency; R01 did not repeat a two-tab conflict run.

## Trusted Actor Runtime

The runtime identity path was:

```text
Local authenticated Principal → Current User → canonical User
→ server actor snapshot → KnowledgeStatus transition
```

- Local login returned `204`; the dashboard and Current User UI displayed canonical `Phase 验证管理员` with `Administrator` access.
- A direct authenticated `PUT /api/knowledge-status` included the removed legacy `actor` object with `FORGED ADMIN`, `Administrator`, and `2099-01-01T00:00:00Z`. The transition returned `200`, and its result time was server-generated `2026-08-23...`, not the submitted future time.
- The integration regression additionally verifies the persisted audit identity and role are principal-derived, and `AccessControlApiTests` verifies a Viewer cannot gain transition authority by payload forgery.
- HumanConfirmation in the browser explicitly displayed “确认人身份由服务端根据 Current User 生成” and recorded `Phase 验证管理员`; it did not accept a client-selected identity.

## Evidence, Status, Lifecycle and Relationship Chain

The same SOP followed this real browser path:

```text
Draft + Unknown
→ ordinary Evidence (still Unknown)
→ explicit Unknown → Inferred
→ HumanConfirmation (still Inferred)
→ explicit Inferred → Confirmed
→ Publish → Archive (still Confirmed)
```

The ordinary Evidence save showed “知识状态保持未知”; the HumanConfirmation save showed that it remained Inferred until the separate explicit confirmation action. Publishing and archiving preserved Confirmed, proving lifecycle and KnowledgeStatus remain independent.

An explicit `AppliesTo → System MES` relationship was created. The relation itself remained Unknown and did not alter the document’s Confirmed status. MES Unified Knowledge View then showed exactly one related KnowledgeDocument and kept System Evidence separate from document evidence.

## Search and Unified View Regression

- Global search for `监听` found the SOP from `检查 Oracle 数据库监听服务。` in body Markdown before archive.
- After archive, the same default search returned no matching object, confirming archive exclusion.
- The MES Unified Knowledge View displayed the document once under Knowledge Content, labelled `AppliesTo`; no duplicate document or mutation behavior was observed.
- The focused Search/FTS and SystemKnowledgeView test coverage passed, including their route and projection checks.

## Security and Network Boundary

- `/api/current-user` during application initialization behaved as the expected authenticated-session request.
- Local login reached Dashboard; no 500/502 response was treated as a login success.
- No duplicate-request storm or failed API chain was observed on document detail, search, or Unified View during the focused browser path.
- The local Vite runtime emitted non-blocking `el-tooltip` component-resolution warnings while loading the editor. Toolbar controls remained visible, labelled, interactive and saved correctly. This is a UI follow-up observation, not a KC BLOCKER/High and was not changed in R01.

## Product Readiness

**Recommended readiness: Internal Pilot.**

The build gates are green; trusted actor attribution, principal-backed HumanConfirmation, authenticated authoring, Markdown save/reload, Evidence/Status separation, FTS body search, archive exclusion, and Unified View projection all passed. This is not a recommendation for Team Production or Enterprise Production: KC-GAP-003 still requires an explicit relationship-vocabulary decision, and normal pilot operations should continue to monitor UI/runtime warnings.

## Recommended Next Step

Hold a focused KC-C relationship-vocabulary decision for KC-GAP-003 (remove/reject the extra vocabulary or explicitly amend the architecture plan with semantics and migration compatibility). Do not begin a new Knowledge Content, UI, or AUTH slice automatically.

## Cleanup

The isolated runtime used only `artifacts/phase-kc-r01-runtime-20260823`, temporary cookie jars, Data Protection keys, logs and SQLite data. These processes and files are cleaned up at the end of R01; no temporary verification data is retained.
