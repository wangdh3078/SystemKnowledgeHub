# DBDISC-B04-R01 — Object-group Selection and Apply Correction Verification Report

## Result

```text
DBDISC-B04-R01 PASS
```

This correction closes the remaining B04 reconciliation-selection usability gap and the ordinal-staging defect found by the historical final verification. It does not repeat or replace `DBDISC-VERIFY`; `DBDISC_FINAL_VERIFICATION_REPORT.md` remains an unchanged historical FAIL record because its repository-data-protection gate failed.

## Object-group Reconciliation

- Reconciliation is presented as a bounded object-parent/column-child tree instead of a flat object/column list.
- New provider-neutral read models expose object groups, bounded lazy child pages, total/selectable/selected counts, conflict/unsupported/no-action counts, and the required parent action.
- Object and column search remains server-side and bounded. A column match returns its containing object group without returning every Snapshot column.
- Expanding an object loads only the requested child page. Collapsing, re-expanding, or changing the child page does not discard typed selections.
- Parent selection is computed from server-provided `SelectableCount` and `SelectedCount`: zero is unchecked, a strict partial count is indeterminate, equality is checked, and a zero-selectable parent is disabled.
- Viewer receives the same read projection without selection controls. Editor and Administrator use the existing Editor policy for all plan mutations.

## Whole-object Selection

`POST /api/database-discovery/reconciliation/object-selection` expands one object selection on the server into the existing B04 typed actions. It is a selection convenience only; no bulk/sync-whole-table action was introduced.

The expansion:

- includes every currently applicable object and column action, including unloaded child pages;
- excludes `Unsupported`, `Conflict`, review-only, and `NoAction` rows;
- automatically includes the required `CreateDatabaseObject` or `LinkExistingDatabaseObject` action when selected columns depend on it;
- merges with the current selection by typed action, logical identity, and exact target;
- rejects, rather than truncates, a result over `DatabaseDiscovery:MaximumSyncPlanActions`;
- returns the exact product message `该选择将超过单个同步计划允许的最大操作数，请减少选择范围。`.

The resulting Draft, PreviewHash, confirmation, and Apply payloads continue to contain only the frozen B04 typed actions.

## Mixed Status and Review-only Structures

- A mixed object can surface `NoAction`, selectable updates, and conflict children together. Its parent selects only the applicable updates.
- FK, unique-constraint detail, index, sequence, and unsupported PK detail remain review-only and are counted separately; they never enter ordinary column children or a plan.
- Difference remains distinct from Reconciliation, no rename is inferred, and MissingFromSource remains a binding marker rather than deletion.

## Ordinal Staging Correction

The atomic Apply staging range now starts above:

```text
max(current active ordinals, every selected action's planned final ordinal)
```

This prevents a selected bound-column update from being staged at an ordinal that another selected new column will use as its final ordinal. Overflow validation uses the same maximum.

Focused regression covers one plan containing an object structural update, an existing column ordinal update, and a new column whose final ordinal equals the former staging candidate. Apply succeeds with the requested final ordinals and no duplicate or partial write. `ActiveOrdinalConflict`, `UnsupportedOrdinal`, and `Int32.MaxValue` staging overflow remain fail-closed.

## Preserved B04 Semantics

- Existing typed bindings and actions are unchanged.
- Preview shows the actual object and column actions; it never reports a synthetic “whole table” action.
- PreviewHash is still derived from the normalized typed selections and exact target state.
- Explicit confirmation and the one-short-SQLite-transaction Apply path are unchanged.
- Latest Snapshot, Profile revision, Scope/identity, plan token, target token, binding token, identifier, and ordinal validation remain authoritative.
- Human-owned business descriptions, access modes, business keys, KnowledgeStatus, creator/ownership, deletion state, Known Values, Evidence, Human Confirmation, Unknown Items, relationships, revisions, documents, and attachments remain outside automated structural writes.

## EF Design-time and Migration Safety

`KnowledgeHubDesignTimeDbContextFactory` now fails closed unless `SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH` identifies an absolute task-owned path. This removes the former fallback to repository `App_Data` for design-time commands.

With the variable set to the B04-R01 task path, `dotnet ef migrations has-pending-model-changes --no-build` reported no pending model changes. The task design-time file was not created, and the repository DB/WAL/SHM state remained byte-identical.

## Dependency Security

The already verified test-only overrides remain private to the test project:

- `System.Net.Http` 4.3.4 (`PrivateAssets=all`)
- `System.Text.RegularExpressions` 4.3.1 (`PrivateAssets=all`)

`dotnet list SystemKnowledgeHub.sln package --vulnerable --include-transitive` reports no vulnerable package in either project. Product drivers were not upgraded:

- Microsoft.Data.SqlClient 7.0.2
- Npgsql 10.0.3
- Oracle.ManagedDataAccess.Core 23.26.300

## Verification

### Backend

| Check | Result |
| --- | --- |
| Release solution build | PASS — 0 warnings, 0 errors |
| B04-R01 focused Sync API regression | PASS — 14/14 |
| Focused migration regression | PASS — 3/3 |
| Deterministic Oracle/PostgreSQL/SQL Server provider regression | PASS — 71/71 |
| Approved serial full backend gate | PASS — 413/413, 0 failed, 0 skipped |
| EF pending-model gate on task-owned path | PASS — no pending changes |
| Dependency vulnerability scan | PASS — no vulnerable package |

Backend focused coverage includes object-group projection, bounded child paging/search, server selectable/selected counts, Viewer read/Editor selection authorization, unloaded-child whole-object expansion, required parent dependency, unsupported/conflict exclusion, action-limit rejection without truncation, ordinal collision correction, and overflow/no-partial-write behavior.

### Frontend

| Check | Result |
| --- | --- |
| Grouped reconciliation focused tests | PASS — 1 file / 9 tests |
| Database Discovery regression | PASS — 9 files / 60 tests |
| Full frontend suite | PASS — 85 files / 513 tests |
| `npm run type-check` | PASS |
| `npm run build` | PASS; existing chunk-size advisory only |
| `npm run lint` | PASS |
| Affected-file Prettier check | PASS |

Frontend coverage includes group rendering, disclosure/lazy load, unchecked/checked/indeterminate/disabled parents, whole-object and required-parent selection, deselection after parent selection, mixed states, collapse and paging retention, unloaded children, exact limit feedback, Viewer read-only, and Editor/Administrator preview-confirm paths.

## Task-owned Browser Smoke

The real Vue/API path used two task-owned SQLite verification datasets, task-owned Data Protection keys, Attachment StorageRoot, Serilog logs, and isolated ports 11321/11322. The full Apply dataset contained:

- Object A: unbound object with three creatable columns;
- Object B: bound object with a NoAction field, an update field, and a new field;
- Object C: a conflict child; review-only exclusion was additionally isolated in the second mixed-status dataset.

The browser verified:

```text
expand Object A
→ collapse without losing state
→ select Object A parent
→ 1 CreateDatabaseObject + 3 CreateDatabaseColumn typed actions
→ select only applicable Object B actions
→ deselect one B child
→ parent becomes indeterminate
→ deterministic Preview
→ explicit Confirm
→ atomic Apply
→ Database Knowledge shows the synchronized objects/columns
```

The second browser dataset then placed an invalid-ordinal conflict field and a review-only unique constraint under Object C. The parent rendered `冲突 1 · 仅审查 1`; expansion showed `UnsupportedOrdinal` and `ReviewOnlyStructure` as separate, non-selectable child reasons.

No raw provider connection was attempted. The browser tab was retained; only the task-owned API/Vite processes and ports were stopped.

The first Applied-result render also exposed a missing global `ElResult` registration. The component and its Element Plus CSS were registered in the existing bootstrap list; the real runtime then rendered the success result without a new unresolved-component warning.

## Repository Data Protection

The new B04-R01 baseline was captured by OS-level metadata/hash operations only. The repository SQLite files were never opened, including read-only access.

| File | Exists | Size | mtime UTC | SHA-256 |
| --- | --- | ---: | --- | --- |
| `system-knowledge-hub.db` | yes | 1220608 | 2026-09-01T10:56:28.3847954Z | `C9578E48B0D733A244C343D6BE423D3E8D0A6BF7642780338C5F4F502A49F6BB` |
| `system-knowledge-hub.db-wal` | yes | 0 | 2026-09-01T12:55:34.9604817Z | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| `system-knowledge-hub.db-shm` | yes | 32768 | 2026-09-01T12:56:49.8856028Z | `FD4C9FDA9CD3F9AE7C962B0DDF37232294D55580E1AA165AA06129B8549389EB` |

The final existence, size, mtime, and SHA-256 values are identical. No WAL/SHM file was created, removed, checkpointed, or modified by this task.

Task-owned database/WAL/SHM, Data Protection keys, attachments, logs, seed project, runsettings, ports, and processes were cleaned. No pre-existing user process was stopped, and the browser/Codex application was not closed.

## Existing / New Gaps

- DBDISC-GAP-004 remains: Snapshot/Reconciliation meaning is bounded by configured scope and source-principal metadata visibility.
- REV-GAP-011 remains: the repository-approved serial backend gate is used for the known parallel test infrastructure issue.
- FK, unique-constraint detail, index, sequence, and unsupported PK detail remain review-only by frozen B04 scope.
- The historical `DBDISC-VERIFY FAIL` remains unchanged and requires the separately requested DBDISC-VERIFY-R01.
- No new Blocker or High gap remains.

## Final Status

```text
DBDISC-B04-R01 PASS

OBJECT GROUP UX: PASS
EXPAND / COLLAPSE: PASS
WHOLE-OBJECT SELECTION: PASS
TRI-STATE SELECTION: PASS
UNLOADED CHILD SELECTION: PASS
ACTION LIMIT SAFETY: PASS

TYPED ACTION EXPANSION: PASS
PREVIEW SEMANTICS: PASS
ATOMIC APPLY: PASS
ORDINAL STAGING CORRECTION: PASS

AUTHORIZATION: PASS
HUMAN KNOWLEDGE PROTECTION: PASS
PROVIDER NEUTRALITY: PASS
DEPENDENCY SECURITY: PASS

BACKEND REGRESSION: PASS
FRONTEND REGRESSION: PASS
REPOSITORY DATA PROTECTION: PASS
CLEANUP: PASS

DBDISC-B04-R01 COMPLETE
DBDISC-VERIFY-R01 READY: YES
```
