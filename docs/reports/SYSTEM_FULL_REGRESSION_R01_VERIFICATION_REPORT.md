# SYSTEM-FULL-REGRESSION-R01 — Full System Regression Verification Report

## Result

**SYSTEM-FULL-REGRESSION-R01 PASS.** The current `main` product baseline remains coherent across authentication, authorization, knowledge authoring, search, revision, Trace/Impact, user administration, soft delete, historical reads, and shared UI/runtime boundaries. One real Integration authoring regression was found, corrected with a narrowly scoped frontend change, and reverified. No unresolved Blocker, High, or Medium product-baseline gap was found.

`PHASE-ATTACHMENTS READY: YES`

This readiness means the current repository is a stable technical baseline for starting the Attachment architecture/design phase. It is not approval to start ATTACH-A01 in this task, and it is not a real Production deployment approval.

## Scope and authority

The regression used the root `AGENTS.md`, `docs/DOCUMENT_INDEX.md`, the applicable frozen MVP UI/design/domain/database/application/API/solution specifications, the current phase and vertical-slice final reports, DELETE-A01 and DELETE-B01–B04/DELETE-VERIFY, the PHASE-REV gap register, PHASE-TRACE final evidence, authentication/authorization/security implementation and tests, and the Production deployment guide/configuration report.

Frozen specifications, Golden UI assets, task definitions, schema, migrations, package manifests, and the repository-owned runtime database were not changed. The task did not implement Attachment, DB Discovery, HC-UX-R01, Export, Governance, AI/RAG, multi-database persistence, Production infrastructure, or another business capability.

## Baseline

- Branch: `main`.
- Starting HEAD: `e9458147ad5be8107daa8427cd368e41ace97e64`.
- Starting worktree: clean and synchronized with `origin/main`.
- Remote: `https://github.com/wangdh3078/SystemKnowledgeHub.git`.
- Runtime: isolated Development API on task-owned port 5112 and Vite on task-owned port 5196.
- Persistence: fresh task-owned SQLite database, task-owned Data Protection directory, and disposable local administrator.
- Browser: authenticated in-app browser session at 1440×900 and 1280×720 profiles.
- The repository database baseline was captured before any runtime verification.

## Build and automated verification

| Gate | Final result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore -c Release -p:UseSharedCompilation=false -nodeReuse:false` | **PASS** — 0 warnings, 0 errors |
| Serial full backend `dotnet test` with temporary runsettings | **PASS** — 175/175, 0 failed, 0 skipped |
| `npm run type-check` | **PASS** |
| `npm run build` | **PASS** — 3,413 modules; only the existing chunk-size advisory |
| `npm run lint` after the regression fix | **PASS** — 0 errors; one existing `vue/one-component-per-file` warning in `KnowledgeDocumentDetailView.spec.ts` |
| Full `npm run test` | **KNOWN BASELINE DEVIATION** — 47/48 files and 303/304 tests passed; the sole failure is the unchanged stale `AppShell.spec.ts` expectation for `关系与缺口` |

The full Vitest failure reproduces the same recorded baseline assertion and does not exercise the Integration components changed in this task. Test-harness-only unresolved Element Plus warnings remain attached to that shallow `AppShell.spec.ts` mount; the clean real browser recheck emitted no warning or error after the fix.

`REV-GAP-011` remains the reason the approved serial backend gate was used. The temporary serial runsettings file was deleted during cleanup.

## Authentication

| Check | Evidence and result |
| --- | --- |
| Authentication options | Runtime `GET /api/auth/options` returned 200 with local login enabled and OIDC disabled, matching the isolated Development configuration |
| Anonymous access | Runtime `GET /api/current-user` and `GET /api/dashboard` returned 401 |
| Invalid credentials | Runtime local login returned 401 without session establishment |
| Valid login/current user | Disposable administrator login returned 204; current-user read returned the canonical user and Administrator access level |
| Antiforgery/session state | A stale pre-login antiforgery token was rejected with 403 `antiforgery_failed`; refreshing the token allowed writes without weakening the boundary |
| Logout and invalidated/deactivated sessions | Covered by the current full backend authentication/access-control suite; no test-only authentication bypass was enabled |

The runtime used only the currently supported Development authentication mode. No Production authentication default was weakened.

## Authorization

The current backend suite covers anonymous, Viewer, Editor, and Administrator page/API boundaries; Editor owner versus Editor other-owner behavior; legacy/null ownership fail-closed behavior; deactivated users/sessions; and administrator-only user management. Frontend capability projection remains convenience/UI behavior, while backend authorization is authoritative.

The browser master flow used an Administrator and verified visible authoring, delete, and user-management actions. It created, edited, and disabled an isolated user profile. Editor/Viewer write denial and user-management denial are attributed to the full backend/access-control regression rather than to frontend button hiding.

## Feature matrix

| Area | Current verification result |
| --- | --- |
| Dashboard | Loaded authenticated statistics, navigation, recent/current projections, and excluded the deleted KnowledgeDocument from the MES unified current view |
| System | Six seeded systems listed; MES detail, lifecycle/technology, BusinessFunction, database, Integration, and context navigation loaded; create/edit/delete/dependency/authorization remain covered by the full suite |
| BusinessFunction | MES `Equipment Status Query` detail rendered ordered process steps, system context, related rule/integration/data/evidence/unknown surfaces; command/delete/authorization coverage passed in the full suite |
| DatabaseSource/Object/Column | Source browser, MES Oracle source, `MES.TABLE_EQP`, eight columns, current metadata, known values, Evidence/Unknown sections, and `STATE_FLAG` Column Drawer loaded; selector/current/deleted filtering passed |
| BusinessRule | Created and opened an isolated rule; detail showed system ownership, definition/input/relationship/evidence/unknown empty states and explicit knowledge progression |
| Integration | Created and opened an isolated RabbitMQ Integration linked to MES; detail showed source/target, one-way flow, Topic, contract/relationship/evidence surfaces; edit flow also loaded |
| KnowledgeDocument | List/create/edit/detail/Markdown, lifecycle, revisions, compare, restore, search, relations, Evidence, HumanConfirmation, KnowledgeStatus, soft delete, FTS exclusion, and historical revision boundary passed |
| Revision | Content saves generated revisions, compare showed line changes, restore generated a new head, old revision detail remained readable, and deleted-owner Restore was absent |
| Evidence/HumanConfirmation | Ordinary Evidence and HumanConfirmation were created and read; state did not auto-progress; confirmation coverage changed after a later revision; deleted-target tombstones preserved identity and removed mutations |
| KnowledgeStatus | Unknown → Inferred → Confirmed required explicit operations and evidence/confirmation prerequisites; soft delete preserved historical status |
| KnowledgeRelation | Relationship add/read/correction delete passed; the correction delete remained physical as explicitly approved and was not treated as root hard delete |
| Search | FTS body token, multi-type `STATE_FLAG` grouping, canonical Column navigation, empty result/recovery state, and deleted-document exclusion passed |
| Trace/Impact | Requirement current Trace/Impact rendered derived coverage/trust and bounded empty states; Published→Archived changed the current coverage boundary; full backend tests retain allowed/forbidden traversal and deleted-root filtering |
| UnknownItem | Browser exercised Pending → Investigating → Resolved → Closed with Finding, Evidence, Resolution, explicit confirmation, activity, and read-only close semantics; full tests retain Apply/deleted-target historical boundaries |
| Global Create | Canonical chooser exposed System, BusinessFunction, Database Knowledge, BusinessRule, Integration, and Knowledge Content; UnknownItem/Evidence correctly required object context; form validation and post-create navigation passed |
| User Management | Administrator list, required-name validation, create, edit, active→inactive, retained profile, and separate LoginIdentity/AccessLevel boundary rendered correctly |
| Soft Delete | Eight-root contract, `canDelete`, role/owner matrix, dependencies, concurrency, projections, FTS, tombstones, and prohibited cascade/bulk/hard-delete/restore boundaries passed |

## Cross-feature scenarios

### Scenario A — Composed knowledge establishment

The isolated seed supplied System → BusinessFunction → Database knowledge. The browser then created BusinessRule, Integration, KnowledgeDocument, KnowledgeRelation, Evidence, HumanConfirmation, and an UnknownItem investigation. Canonical navigation and cross-object context remained usable; Evidence/HumanConfirmation remained distinct from explicit KnowledgeStatus changes.

### Scenario B — KnowledgeDocument lifecycle and soft delete

1. Created a draft KnowledgeArticle with Markdown heading, list, and blockquote.
2. Added a `Documents` relationship and ordinary Evidence; status remained Unknown.
3. Explicitly progressed Unknown → Inferred, added HumanConfirmation against revision 1, then explicitly progressed to Confirmed.
4. Edited semantic content to revision 2 and verified changed-since-confirmation coverage.
5. Searched a body-only FTS token and navigated to the document.
6. Compared revisions 1→2 and restored revision 1, creating a new head while retaining history.
7. Exercised stale-token 409 and active-dependency 422 without partial mutation.
8. Removed the relationship through the approved correction delete, created the final content revision, and soft-deleted the root with 204.
9. Current detail returned 404; list, search, FTS, Dashboard/unified current projections returned no deleted document.
10. Six historical revisions remained readable; Evidence/HumanConfirmation preserved the original name with `已删除`, were not navigable, and exposed no mutation action. Deleted-owner revision Restore returned 404 and was absent from the history UI.

### Scenario C — Permission matrix

The full access-control/backend suite remains the authoritative Administrator/Editor/Viewer matrix, including Editor-owned and Editor-other-owned objects. Runtime probes separately proved anonymous 401, fail-closed antiforgery 403, and Administrator user-management/write behavior. No browser-only permission check is claimed as backend proof.

### Scenario D — Dependency and delete

An active KnowledgeRelation blocked KnowledgeDocument deletion with structured 422 and no current/history mutation. A stale token returned 409. After explicit relationship correction and a fresh token, the root delete returned 204 and disappeared from current projections. The full suite retains the equivalent dependency/concurrency contract for all eight approved roots.

### Scenario E — Deleted historical target

The isolated KnowledgeDocument chain verified Evidence, HumanConfirmation, and revision tombstones directly. Closed UnknownItem and applied KnowledgeUpdate deleted-target behavior remains covered by the 175-test current suite and DELETE-VERIFY final evidence: original names are retained, `已删除` is visible, links are removed, and reopen/apply/restore paths are unavailable.

## Error paths

| Status | Runtime/automated evidence | Result |
| --- | --- | --- |
| 400 | Empty/invalid delete token and validation failures | Rejected without mutation |
| 401 | Anonymous current-user/dashboard and invalid login | Rejected without data disclosure/write |
| 403 | Stale antiforgery token at runtime; role/owner denial in full suite | Fail-closed; frontend visibility is not the authority |
| 404 | Missing and soft-deleted current detail; deleted-owner restore | Correct not-found boundary |
| 409 | Stale KnowledgeDocument delete token | Conflict without mutation or invented retry |
| 422 | Active relationship dependency | Structured blocker; owner and projections unchanged |
| 204 | Authorized unblocked soft delete and valid login operation | Correct success semantics |

No UI path fabricated success after API failure. Recovery/refresh behavior kept current server state authoritative.

## Browser and responsive verification

- Real navigation covered login, Dashboard, Systems, BusinessFunction, Database Object/Column Drawer, Knowledge Content, revision history/compare/restore, Search, UnknownItem, BusinessRule, Integration, Global Create, and User Management.
- 1440×900 and 1280×720 browser profiles were inspected. At the narrower profile the in-app browser content canvas reported 1231×692 after browser chrome; document/body widths matched with no horizontal overflow.
- The 440px Column Drawer remained usable at the narrower profile while the main table/detail content retained its own vertical scroll. No action footer overlap, trapped overlay, duplicate modal, or unclosable surface was observed.
- Empty states, validation text, alerts, toast/message feedback, tombstones, tables, long technical identifiers, current-user controls, dialogs, and drawers remained readable.
- Status remained text-backed rather than color-only; exercised form controls and icon closes were keyboard/role-addressable through the accessibility tree.
- Known accessibility gaps `REV-GAP-006` and `REV-GAP-007` reproduced unchanged.
- After the Integration correction, a new clean browser tab rendered real `单向`/`双向` radio controls in both create and edit surfaces, successfully selected `双向`, and captured zero warning/error console entries.

## Fixes made during regression

### R01-FIX-001 — Integration flow-direction controls

**Finding:** The real Integration create dialog emitted unresolved-component warnings for `el-radio-group` and `el-radio-button`; the visible text was present but the intended radio semantics/interaction were missing. This elevated the previously recorded `CreateIntegrationDialog.vue` baseline lint issue into a real current-feature regression.

**Minimal correction:**

- locally imported `ElRadioGroup` and `ElRadioButton` in Integration create and edit components;
- changed the Element Plus radio value binding from deprecated `label`-as-value to `value`;
- removed the unused `props` binding while preserving the typed `systems` prop exposed to the template.

No API, domain, route, schema, migration, package, lifecycle, authorization, or layout contract changed.

**Reverification:** Type check, production build, and full lint passed; the full Vitest result remained exactly the same known 303/304 baseline; fresh browser create/edit controls were semantic radios, bidirectional selection worked, and the console was clean.

## SQLite and persistent-data safety

The task-owned runtime database was checked after all browser/API writes and after the runtime processes were stopped:

- `PRAGMA integrity_check = ok`.
- `PRAGMA foreign_key_check` returned zero rows.
- Final isolated counts included 2 users, 6 systems, 6 BusinessFunctions, 1 DatabaseSource, 1 DatabaseObject, 8 columns, 1 BusinessRule, 1 Integration, 2 KnowledgeDocuments, 7 revisions, 1 UnknownItem, 3 Evidence rows, and 0 relationships after the deliberate correction delete.

The checked-in repository database remained byte-for-byte and timestamp identical to the captured baseline:

- Path: `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db`.
- Length: `897024` bytes.
- LastWriteTimeUtc: `2026-08-27T15:46:01.9864232Z`.
- SHA-256: `7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483`.
- Repository `-wal` / `-shm`: absent before and after.

The repository database was never opened, migrated, seeded, checkpointed, or used for verification.

## Existing gaps and external gates

| Item | Severity/status | Regression assessment |
| --- | --- | --- |
| `REV-GAP-006` | Low, OPEN/Deferred | Restore ancestor dialog landmark remains unnamed; reproduced unchanged |
| `REV-GAP-007` | Low, OPEN/Deferred | Revision History retains nested `<main>`; reproduced unchanged |
| `REV-GAP-008` | Low, OPEN/Deferred | Published-save direct message-box overlay guard; unchanged |
| `REV-GAP-009` | Low, OPEN/Deferred | Restore rollback Version assertion evidence; unchanged |
| `REV-GAP-011` | Low, OPEN/Deferred | Default parallel backend suite can stall; approved serial gate passed 175/175 |
| `AppShell.spec.ts` | Known baseline test deviation | Unchanged stale `关系与缺口` assertion; 303/304 full Vitest |
| Vite chunk-size advisory | Informational | Unchanged; production build passes |
| PHASE-TRACE real-domain acceptance | External human/Product gate | Technical Trace/Impact regression passes; phase closure still requires authentic Product/domain acceptance |
| `SEC-04` real Production evidence | External deployment/security gate | Repository-supported fail-closed configuration remains verified, but real OIDC/HTTPS/proxy/key/operations topology remains `NOT VERIFIED AS REAL PRODUCTION DEPLOYMENT` |

The PHASE-TRACE human acceptance and SEC-04 Production topology evidence are not code regressions and do not block starting Attachment design/development on the current technical baseline. They remain mandatory before their respective Product/Production approvals and are not closed or duplicated by this report.

## New gaps

| ID | Severity | Status | Finding |
| --- | --- | --- | --- |
| `SYSTEM-R01-GAP-001` | Low | OPEN / Deferred | The shared headerless `DrawerHost` exposes an unnamed dialog landmark; observed on the Column Detail and Integration Edit drawers. Visible headings and named close controls exist, but the ancestor drawer lacks an accessible name. Address in a focused overlay accessibility hardening slice by deriving a stable title/`aria-labelledby` from overlay metadata. |

No new Blocker, High, or Medium gap was found.

## Cleanup

- Closed all task browser tabs and reset the temporary viewport override.
- Stopped only the API/Vite/dotnet/node processes started for this regression; ports 5112 and 5196 have no listener.
- Removed the validated task-owned `.tmp/system-full-regression-r01` directory, including the disposable database, WAL/SHM, Data Protection keys, logs, serial runsettings, and SQLite checker build artifacts.
- No pre-existing development process or listener was terminated.

## Final readiness

The current mainline is a stable technical baseline for the next major feature: core flows compose correctly, backend authorization remains authoritative, soft delete does not leak into current projections, historical reads retain approved tombstones, search/FTS and Trace/Impact remain bounded to current truth, SQLite integrity and repository data protection passed, and the only newly recorded gap is Low accessibility hardening.

`PHASE-ATTACHMENTS READY: YES`

## Final result

**SYSTEM-FULL-REGRESSION-R01 PASS**
