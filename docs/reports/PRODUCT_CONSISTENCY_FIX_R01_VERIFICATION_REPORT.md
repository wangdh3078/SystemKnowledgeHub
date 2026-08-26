# PRODUCT-CONSISTENCY-FIX-R01

## Result

```text
PRODUCT-CONSISTENCY-FIX-R01 PASS
```

PCF-001 through PCF-008 are complete. The change preserves the existing product architecture, explicit knowledge-status transition contract, TRACE behavior, and revision behavior. No migration or soft-delete work was introduced.

## Worktree Baseline

- Branch: `main`
- Starting HEAD: `00da4b2`
- `git status --short`: clean
- `git diff --stat`, `git diff`, and `git diff --check`: no starting changes
- The repository database was already open by a user-owned process at baseline. Its main-file metadata was recorded without stopping that process: length `724992`, `LastWriteTimeUtc` `2026-08-26T14:32:28.8616945Z`; WAL/SHM existed and the main file could not be hashed while locked.

## Normative Authority

The implementation and verification used:

- `AGENTS.md`
- `docs/DOCUMENT_INDEX.md`
- `docs/design/SYSTEM_UI_COMPONENT_AND_INTERACTION_BASELINE.md`
- `docs/design/TRACE_A01_TRACEABILITY_ARCHITECTURE_AND_CONTRACT_DECISION.md`
- `docs/design/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md`
- `docs/reports/UI_FOUNDATION_R01_CONSISTENCY_BASELINE_REPORT.md`
- `docs/reports/PHASE_TRACE_FINAL_VERIFICATION_REPORT.md`
- the applicable frozen UI, domain, application, API, database, and solution specifications
- the current `PRODUCT-CONSISTENCY-FIX-R01` task definition and adjacent implementation/tests

Frozen sources and historical verification reports were not modified.

## Issue Matrix

| Issue | Before | Fix / Finding | Verification | Final |
| ----- | ------ | ------------- | ------------ | ----- |
| PCF-001 | Systems list lacked a page create action. | Added header `新增系统`, wired to the canonical `create-system` overlay. | Source test and browser create flow. | PASS |
| PCF-002 | Page create labels mixed `新增`, `新建`, and generic wording. | Unified the five list actions to `新增 + 对象名称`; retained global `新增`. | Source test plus five-page browser audit. | PASS |
| PCF-003 | Unknown Item create action did not have a proven common primary hierarchy. | Reused the existing Element Plus primary action pattern; no new button family. | Browser computed-style comparison across five pages. | PASS |
| PCF-004 | Table/View showed status but lacked object-level Evidence/HC/progression UX. | Exposed the already-supported DatabaseObject canonical subject through object-level evidence, HC, and progression UI; detail actions now include `AddEvidence` and `ChangeKnowledgeStatus`. | API/query tests, component test, and full browser status chain. | PASS |
| PCF-005 | `知识概况` could be mistaken for a KnowledgeDocument aggregate. | Renamed it `结构化知识状态概况` and disclosed the exact included/excluded scope without changing aggregation. | Query inspection plus MES Inferred / related document Confirmed runtime fixture. | PASS |
| PCF-006 | BusinessRule system options survived as a stale first-open list. | Reloads authoritative system options on every transition into the BusinessRule create flow and clears prior options first. | Automated reopen test and no-reload browser scenario. | PASS |
| PCF-007 | HC errors crowded the following field. | Added a local HC error-state form-item bottom margin. | 1280×720 runtime geometry and usability check. | PASS |
| PCF-008 | Dashboard recent activity omitted the year. | Added a shared local minute formatter and used `YYYY-MM-DD HH:mm`. | 2025/2026 unit cases and dashboard runtime. | PASS |

## PCF-001 Systems Create Action

The Systems page now exposes `新增系统` in the page header. It opens the same overlay coordinator route, `{ kind: 'create-system' }`, used by Global Create. Browser verification created `PCF-R01-System` through this path while the top-bar `新增` remained present.

## PCF-002 Create Button Naming

The verified page actions are:

```text
系统             新增系统
业务功能         新增业务功能
数据库对象       新增数据库对象
知识内容         新增知识内容
待确认事项       新增待确认事项
```

All stay in their page-header action slot. Global Create remains `新增` and retains its cross-object meaning.

## PCF-003 Unknown Item Create Button Style

At 1280×720, all five actions resolved to `el-button el-button--primary`, height `32px`, padding `8px 15px`, font size `13px`, radius `7px`, background `rgb(79, 70, 229)`, and white foreground. The existing shared Element Plus hover/focus/disabled behavior remains authoritative; no custom Unknown Item button family was added.

## PCF-004 DatabaseObject KnowledgeStatus Path

```text
DatabaseObject current backend/domain support: canonical existing subject
Evidence: supported
HumanConfirmation: supported as HumanConfirmation Evidence
KnowledgeStatus progression: supported by the canonical status endpoint
```

The backend resolver, Evidence subject vocabulary, HC flow, authorization, and knowledge-status service already supported `DatabaseObject`. The missing product path was primarily UI exposure; runtime also found that the detail query omitted the two existing actions, so `AddEvidence` and `ChangeKnowledgeStatus` were added to `DatabaseObjectActions` and asserted in API/application tests.

The detail page now has:

- an object-level `证据与人工确认` section;
- `添加证据` and `添加人工确认` actions using the shared overlays;
- shared `KnowledgeStatusProgressionPanel` behavior;
- explicit copy separating Table/View evidence from field evidence.

Strict-order runtime proof on `PCF.R01_EXACT`:

```text
DatabaseObject Unknown
→ add CodeReference Evidence
→ still Unknown
→ explicit progression
→ Inferred
→ add HumanConfirmation Evidence
→ still Inferred
→ explicit progression
→ Confirmed

STATUS_CODE column
→ remained Unknown throughout
```

This satisfies object/column status independence. No domain expansion or migration was required.

## PCF-005 System Knowledge Overview Semantics

Actual aggregation source: the existing system overview query in `SystemQueries`.

Actual included entity types:

```text
System
BusinessFunction
DatabaseObject
DatabaseColumn
Integration
```

Actual excluded entity types:

```text
KnowledgeDocument
BusinessRule
Evidence
UnknownItem
```

KnowledgeDocument status does not change System status, and System status does not change a related KnowledgeDocument. Because the existing query is a structured-knowledge aggregate rather than a document aggregate, the calculation was retained and the title became `结构化知识状态概况`. Its scope copy states that it counts the system itself, business functions, database objects, columns, and integrations, and excludes related knowledge documents and business rules.

Runtime fixture:

```text
MES System = Inferred
PCF-R01 已确认知识内容 = Confirmed
KnowledgeDocument → explains → MES relation exists
```

The System header remained `推断`; the Unified Knowledge View displayed the related document's own `已确认` badge; the overview scope explicitly excluded that document.

## PCF-006 BusinessRule System Option Refresh

`CreateBusinessRuleFlow` now watches the active overlay kind. Every open of `create-business-rule` clears the previous options, reloads active systems from the authoritative API, and resets the selection through normal component/dialog lifecycle.

The automated test observed A on first open, A+B on reopen, two API calls, and no retained old selection. Browser verification opened the flow, closed it, created `PCF-R01-Reloaded` without a page reload, and found that system in the next BusinessRule option list while the default selection returned to `APS`.

## PCF-007 HumanConfirmation Validation Spacing

The change is scoped to `.human-confirmation-drawer .evidence-form-section--confirmation .el-form-item.is-error`; it does not override Element Plus forms globally or change validation rules.

At 1280×720, both invalid items had computed `margin-bottom: 24px`; each error message had a measured `10px` gap before the next form item. The drawer body used `overflow-y: auto`, the next labels/textareas remained readable, and the Save button remained reachable at the viewport bottom.

## PCF-008 Dashboard Recent Date Format

Dashboard recent activity now uses shared `formatLocalDateTimeToMinute` output:

```text
YYYY-MM-DD HH:mm
```

The formatter does not alter API values or stored timestamps. Tests cover 2025 and 2026 values plus invalid-input preservation. Runtime displayed values such as `2026-08-12 09:20`.

## Files Changed

Backend:

- DatabaseObject detail action exposure
- focused API/application assertions

Frontend:

- Systems, DatabaseObjects, KnowledgeDocuments list create actions
- BusinessRule create option reload
- Dashboard shared minute formatting
- DatabaseObject object-level evidence/HC/progression UI
- System overview scope copy
- HC local validation spacing
- focused Vitest coverage

Documentation:

- this verification report
- `docs/DOCUMENT_INDEX.md`

No migration, runtime database, key, log, build artifact, or soft-delete file is part of the task diff.

## UI Foundation Compliance

PASS. Existing page-header structure, Element Plus primary buttons, shared overlay coordinator, Evidence/HC overlays, progression panel, and status tags were reused. No new component family, page-header family, or database-specific status system was introduced.

## TRACE Regression

PASS. Requirement/Specification/TestCase trace rendering, coverage/trust separation, impact context, and relationship add/delete/re-add refresh code were not changed. `PHASE_TRACE_FINAL_VERIFICATION_REPORT.md` was not modified and Product Acceptance remains pending human review.

## Revision Regression

PASS. KnowledgeDocument revision/history/compare/restore, semantic save, published save, HC revision snapshot, and FTS behavior were not modified. The full backend serial suite passed.

## Backend Build

`dotnet build SystemKnowledgeHub.sln --no-restore -c Release`: PASS — 0 warnings, 0 errors.

## Backend Focused Tests

Database knowledge API and application query selection: PASS — 11/11.

The focused coverage asserts DatabaseObject detail, Evidence/HC eligibility exposure, and knowledge-status action availability.

## Full Backend Regression

Approved deterministic serial gate: PASS — 144/144, 0 failed, 0 skipped. The task-owned temporary runsettings disabled xUnit collection parallelism and was deleted immediately afterward. `REV-GAP-011` remains OPEN / Deferred.

## Frontend Type Check

`npm run type-check`: PASS.

## Frontend Build

`npm run build`: PASS — 3,406 modules transformed. The existing chunk-size advisory remains and was not expanded into this task.

## Affected Vitest

PASS — 8 files, 24/24 tests.

Coverage includes page create labels/canonical routes, Global Create retention, BusinessRule reopen/reload, HC drawer behavior, Dashboard formatting, System overview/Unified Knowledge View, and DatabaseObject Evidence/HC/progression composition.

## ESLint

Task-modified TypeScript/Vue source: PASS — 0 errors.

The repository-wide `npm run lint` baseline still reports two unrelated pre-existing errors and one pre-existing warning in `CreateIntegrationDialog.vue`, `unknownItemContracts.ts`, and `KnowledgeDocumentDetailView.spec.ts`. None is in a task-modified file, and this task did not expand that baseline.

## Browser Runtime Scenario

PASS. The browser-control workflow validated the real UI against a task-owned API, Vite server, temporary SQLite database, isolated Data Protection keys, and disposable Administrator.

Verified:

- Systems page canonical create flow and retained Global Create;
- BusinessRule authoritative option reload without browser reload;
- five list labels and common primary hierarchy;
- DatabaseObject object-level Evidence/HC/progression and column independence;
- MES Inferred versus related KnowledgeDocument Confirmed semantics;
- HC validation spacing and scroll/footer access;
- Dashboard four-digit year date format.

## Responsive Smoke

PASS at 1280×720 and 1440×900.

- 1280×720 Unknown Items: document/body/main `scrollWidth == clientWidth`.
- 1280×720 DatabaseObject detail: document/body/main `scrollWidth == clientWidth`; progression actions usable.
- 1280×720 HC drawer: scroll container active, errors separated, Save reachable.
- 1440×900 DatabaseObject and System details: document/body/main `scrollWidth == clientWidth`.
- Page actions, filter/list surfaces, dates, and detail content remained reachable.

## Browser Console

PASS — final controlled tab query returned 0 application warnings and 0 errors; no unhandled rejection was observed.

## Temporary SQLite Integrity

After stopping the task API:

```text
PRAGMA wal_checkpoint(TRUNCATE) = (0, 0, 0)
PRAGMA integrity_check = ok
PRAGMA foreign_key_check = 0 rows
```

The final strict-order database confirmed DatabaseObject `Confirmed`, its column `Unknown`, and exactly one `CodeReference` plus one `HumanConfirmation` Evidence record. The preceding semantic-fixture cycle independently confirmed MES `Inferred`, the related document `Confirmed`, and exactly one document-to-MES relationship before its own integrity check and cleanup.

## Repository DB Protection

PASS. All migrations, seed data, authentication, fixture creation, relations, Evidence/HC, and status transitions used the task-owned temporary database.

Repository main DB evidence:

| Probe | Baseline | Final | Result |
| --- | --- | --- | --- |
| Length | `724992` | `724992` | unchanged |
| LastWriteTimeUtc | `2026-08-26T14:32:28.8616945Z` | `2026-08-26T14:32:28.8616945Z` | unchanged |
| Final SHA-256 | locked by pre-existing user process | `A122F8D208338A8445DCF19CE27601634B98A0B35AC1575FDBB6ADF7A3422DA9` | final fingerprint established; baseline metadata identical |
| WAL / SHM | present under pre-existing user process | absent after that user-owned process exited | no task connection or cleanup targeted repository App_Data |

The agent did not stop the user-owned repository process, did not point a connection string at repository App_Data, and did not delete its WAL/SHM. The exact main-file length and nanosecond-resolution modification time remained unchanged across the task.

## New Gap Check

- New Blocker: none.
- New High: none.
- New Medium: none.
- The repository-wide lint baseline items and `REV-GAP-011` remain pre-existing deferred work; neither was caused or expanded by this task.

## Deferred / Separate Decisions

- No new product-consistency issue requiring a separate architecture decision was discovered.
- Existing unrelated lint baseline cleanup remains outside PCF-001 through PCF-008.
- `REV-GAP-011` remains OPEN / Deferred under the already-approved serial gate.

## Soft Delete Boundary

```text
Soft Delete = NOT STARTED
DELETE-A01 = NEXT SEPARATE ARCHITECTURE TASK
```

No `IsDeleted`, deletion metadata, query filters, delete/recovery UI, unique-index change, or migration was added.

## Final Result

```text
PRODUCT-CONSISTENCY-FIX-R01 PASS

PCF-001 PASS
PCF-002 PASS
PCF-003 PASS
PCF-004 PASS
PCF-005 PASS
PCF-006 PASS
PCF-007 PASS
PCF-008 PASS

Product Readiness: Internal Pilot
PHASE-TRACE Product Acceptance: PENDING HUMAN REVIEW
```
