# DBDISC-B04 — Manual Sync Planning + Apply Verification Report

## Result

```text
DBDISC-B04 PASS
```

DBDISC-B04 establishes a provider-neutral, human-confirmed path from the latest compatible complete Discovery Snapshot to Database Knowledge. Discovery completion still never mutates Knowledge automatically, and no SQL Server provider, automatic synchronization, generic patch engine, or B04-external capability was added.

## Reconciliation and Binding Model

- Reconciliation is derived from the latest complete Snapshot, compatible Scope Generation/identity version, typed bindings, and current Hub state; it is not a replay of the latest Difference.
- `DatabaseObjectDiscoveryBinding` and `DatabaseColumnDiscoveryBinding` use real FKs and database-level uniqueness in both directions.
- The external identity unique key is exactly `ProfileId + ScopeGenerationId + IdentityAlgorithmVersion + LogicalIdentity`; Hub targets are independently unique.
- Reconciliation exposes bounded server-side paging, category/search filters, current target state, suggested typed action, support status, and safe blocker code.
- Scope-incompatible bindings are `OutOfScope`; identity-version incompatibility is `RebaselineRequired`; neither is mass-marked Missing.
- Unsupported PK detail, FK, unique constraint, index, and sequence structures remain review-only. PK ordered column names alone map to `PrimaryKeyColumnsJson` when an object is synchronized.

## Supported Typed Actions

The API and persisted plan payload use the frozen explicit action names:

```text
CreateDatabaseObject
LinkExistingDatabaseObject
CreateDatabaseColumn
LinkExistingDatabaseColumn
UpdateDatabaseObjectStructure
UpdateDatabaseColumnStructure
MarkObjectSourceMissing
MarkColumnSourceMissing
ClearObjectSourceMissing
ClearColumnSourceMissing
```

No generic entity patch or reflection-based updater exists. Exact-link candidates require an active, unique, structurally compatible target without another binding. Identifier ambiguity, case collision, deleted targets, parent mismatch, or an existing incompatible binding blocks the action.

## Plan, Preview, Confirmation, and Apply

- Plans persist `Draft`, `Ready`, `Applied`, and `Superseded` state plus selection format/version, target/base Snapshot, Difference, Profile revision, Scope, identity algorithm, actor, confirmation, result, and optimistic version.
- Selection creation/update is capped by validated `DatabaseDiscovery:MaximumSyncPlanActions` with a default of 2000.
- Preview is deterministic and hashes the target Snapshot/content hash, Profile revision, Scope/identity version, normalized typed selections, exact targets, target/binding tokens, and structural before/after payload.
- Changing selections clears preview and confirmation and returns the plan to `Draft`.
- Confirmation records the canonical actor, timestamp, and exact PreviewHash. Apply requires `ConfirmedPreviewHash == PreviewHash`.
- Apply uses one short SQLite immediate transaction. It revalidates Plan version, latest successful Snapshot, Profile revision, Scope/identity version, all targets/bindings, structural preconditions, identifier constraints, and the freshly rebuilt preview.
- Any stale/collision/write failure produces zero partial structure writes. A newer successful Snapshot supersedes the plan.
- Repeated Apply is rejected; there is no durable `Applying` state and no background/automatic apply path.

## Database Knowledge Mapping and Protection

Created/updated structure is restricted to externally owned technical fields:

- Object: Schema, name/identity, type, database comment, and ordered PK field-name array.
- Column: name/identity, positive ordinal, canonical native declaration, nullability, default, and database comment.
- Created objects/columns use the applying actor and start at KnowledgeStatus `Unknown`.
- Collision-safe ordinal updates stage selected bound columns at guaranteed-unused positive ordinals before final values; null/overflow/unbound-active collisions are rejected.
- Changed identity names are not treated as rename; they are blocked as `RenameNotSupported`.
- Missing only sets `SourceMissingSinceSnapshotId` on a typed binding. It never deletes, soft-deletes, archives, changes KnowledgeStatus, or removes relations/evidence. Reappearance requires an explicit clear action.

Regression proves the synchronization path does not modify BusinessDescription, AccessMode, BusinessKeyColumnsJson, KnowledgeStatus, creator/owner, deletion state, Known Values, Evidence, HumanConfirmation, Unknown Items, Relationships, revisions, documents, or attachments.

## Migration Safety

Migration `20260831170031_AddManualDiscoverySyncFoundation`:

- adds provider-neutral typed bindings, plans, results, and safe audit persistence;
- adds DatabaseObject/DatabaseColumn database comments and versioned technical identities;
- deterministically backfills legacy rows as `legacy:object:v1:<id>` and `legacy:column:v1:<id>` before unique indexes are created;
- preserves existing business fields and rows;
- creates both-direction binding unique indexes and complete restrictive FKs;
- adds no Oracle/PostgreSQL/SQL Server-specific table;
- upgrades from the prior current migration, passes `foreign_key_check`, and rolls back in the focused migration test.

`dotnet ef migrations has-pending-model-changes` reported no pending model changes.

## Authorization, Concurrency, and Audit

- Viewer can read reconciliation, plans, previews, and results but cannot create/update/preview-confirm/apply.
- Editor and Administrator can manage B04 plans and apply them.
- Editor still cannot manage Connection Profiles/Secrets, test a connection, trigger Discovery, or cancel a Run; existing Administrator policies remain unchanged.
- Backend authorization and antiforgery remain authoritative.
- Plan/target/binding concurrency is independently checked with opaque tokens; stale requests return conflict and never use last-write-wins or automatic retry.
- Structured audit covers plan create, selection change, preview, confirmation, apply, and supersede with actor/Plan/Profile/Snapshot/count/timestamp-safe metadata.
- Passwords, Secrets, connection strings, raw provider errors, complete external rows, PasswordHash, and protected payloads are absent from Sync persistence, responses, logs, and audits.

## Provider Neutrality

Reconciliation, binding, plan, preview, confirmation, and Apply contain no Oracle/PostgreSQL conditional path. They consume only Canonical metadata and Provider-produced versioned logical identities.

The deterministic PostgreSQL fake-provider test executes the same B04 pipeline, while the Oracle and PostgreSQL Discovery regression group passed without provider-specific Sync persistence.

## UI and Browser Verification

The Database Discovery area now has five route-driven surfaces: connection configuration, runs, snapshots, difference review, and manual synchronization.

The Manual Sync page provides Profile/category/search/paging, current Hub state, suggested action, support/block reason, selectable applicable rows, Draft plan history, deterministic before/after preview, protected-field explanation, explicit confirmation, Apply result counts, and direct Database Knowledge navigation. Unsupported/conflict rows are not selectable; Viewer is read-only.

A real Vue/API browser smoke used task-owned SQLite, Data Protection keys, attachments, Serilog logs, and isolated ports 5414/5415. It verified:

```text
task-owned PostgreSQL canonical Snapshot
→ 3 reconciliation candidates
→ Draft plan
→ deterministic PreviewHash
→ explicit checkbox confirmation
→ Ready
→ confirmation dialog
→ atomic Applied
→ 1 DatabaseObject + 2 DatabaseColumns
→ reconciliation becomes NoAction
→ Database Knowledge list/detail read
```

The final Database Knowledge detail showed `public.b04_customer`, PK `customer_id`, `bigint`/`character varying(120)`, correct nullability, two fields, KnowledgeStatus `Unknown`, no business description, and zero evidence. The result page reported created objects `1`, created columns `2`, and zero link/update/missing actions.

The verified browser tab was deliberately retained as requested. Its task API/Vite processes and ports were stopped; the tab itself was not closed.

## Verification

### Backend

| Check | Result |
| --- | --- |
| Release solution build | PASS — 0 warnings, 0 errors |
| B04 focused API + migration tests | PASS — 10/10 |
| B01/B02/B03 + Oracle/PostgreSQL regression | PASS — 86/86 |
| Approved serial full backend gate | PASS — 384/384 |
| Worker/provider and Database Knowledge regression within full gate | PASS |
| EF pending-model gate | PASS — no pending model changes |

### Frontend

| Check | Result |
| --- | --- |
| B04 view/navigation focused tests | PASS — 2 files / 16 tests |
| Full frontend suite | PASS — 85 files / 505 tests |
| `npm run type-check` | PASS |
| `npm run build` | PASS; existing chunk-size advisory only |
| `npm run lint` | PASS |
| B04 affected-file Prettier check | PASS |

## Repository Data Protection and Cleanup

The repository-owned database baseline and final state are byte-for-byte identical:

| File | Exists | Size | mtime UTC | SHA-256 |
| --- | --- | ---: | --- | --- |
| `system-knowledge-hub.db` | yes | 1052672 | 2026-08-31T16:22:52.0160774Z | `0A3F52A89BDE6C656445F95A3341CB9041D343394759B92467798750B68C89A8` |
| `system-knowledge-hub.db-wal` | no | — | — | — |
| `system-knowledge-hub.db-shm` | no | — | — | — |

All tests and browser/runtime verification used task-owned persistent paths. During migration-file regeneration, one initial EF design-time remove command inspected the repository migration history because the existing design-time factory selected its default path; immediate and final size/mtime/hash checks prove that it performed no repository write, migration, checkpoint, or WAL/SHM creation. The design-time gate was then rerun against task-owned SQLite.

Task-owned database/WAL/SHM, Data Protection keys, attachments, Serilog logs, disposable credential, seed project, serial runsettings, diagnostic dumps, API/Vite processes, and isolated ports were cleaned. No user process or browser was stopped.

```text
REPOSITORY DATA PROTECTION: PASS
CLEANUP: PASS
```

## Existing / New Gaps

- DBDISC-GAP-004 remains: Snapshot/Reconciliation meaning is bounded by configured scope and source-principal metadata visibility.
- REV-GAP-011 remains: the repository-approved serial backend gate is used for the known parallel test infrastructure issue.
- FK, unique constraint, index, sequence, and provider constraint names/details remain Snapshot/Difference review-only by approved B04 scope.
- No new Blocker or High gap remains.

## Final Status

```text
DBDISC-B04 PASS

RECONCILIATION: PASS
DISCOVERY BINDINGS: PASS
TECHNICAL IDENTITY MIGRATION: PASS

CREATE OBJECT/COLUMN: PASS
LINK EXISTING: PASS
STRUCTURAL UPDATE: PASS
SOURCE MISSING: PASS

HUMAN KNOWLEDGE PROTECTION: PASS
PREVIEW HASH: PASS
EXPLICIT CONFIRMATION: PASS
ATOMIC APPLY: PASS

CONCURRENCY / STALE PLAN: PASS
LATEST SNAPSHOT REVALIDATION: PASS
IDENTIFIER / ORDINAL COLLISION: PASS

AUTHORIZATION: PASS
PROVIDER-NEUTRAL SYNC: PASS
MIGRATION SAFETY: PASS
REPOSITORY DATA PROTECTION: PASS

DBDISC-B04 COMPLETE
DBDISC-SQLSERVER-B01 READY: YES
```
