# STABILITY-R01 — Concurrency / Stale Detail / Historical Integrity

## Result

**STABILITY-R01 PARTIAL PASS**

**RELATION-EVIDENCE-CONTRACT-GAP OPEN — item #5 BLOCKED pending design direction.**

Items #1, #2, #3, #4, and #6 are implemented and pass the affected verification gate. This is not an all-PASS claim for the six-item task or for the complete repository test suite. STABILITY-R02 is not declared ready and was not started. PORTAL-B04 was not changed.

## Baseline and authority

- Date: 2026-09-05. Branch: `main`; starting HEAD and fetched `origin/main`: `0ba5eb63f0ff4f2d28148297e8e5b35c0a0bddbe`.
- Reviewed `AGENTS.md`, `docs/DOCUMENT_INDEX.md`, current implementation, status, diff, and six-commit history. No reset, clean, revert, rebase, or branch change.
- Existing user work preserved and excluded from this task's commit: the DBDISC-FINAL-R01 row in `DOCUMENT_INDEX.md` and untracked `DBDISC_FINAL_R01_VERIFICATION_REPORT.md`.
- Authentication authority: SEC-A01, AUTH-A01 coexistence/password-policy amendment, AUTH-USER-A01 lifecycle decision, and current AUTH-B01/lifecycle verification evidence. Canonical identity, authorization, hashing, lockout, and method-scoped session revocation remain unchanged.
- Evidence/HumanConfirmation authority: frozen MVP API/use-case contracts (C23/C24/C25/Q16), VS06/VS08 evidence, DELETE-A01 and DELETE-B03 historical-read evidence. HumanConfirmation identity snapshots were not redesigned.
- Relation authority: KC-C01 vocabulary decision, original Evidence subject contract, and DELETE-A01's explicit physical removal and historical-reference rules.
- Discovery authority: DBDISC-A01 B04 manual-sync freeze and current B04/R01 verification reports. No Snapshot, PreviewHash, Confirm/Apply truth, field ownership, binding schema, or migration change.

## Verification matrix

| Required item | Result | Evidence |
| --- | --- | --- |
| AUTH CONCURRENT FAILED LOGIN | PASS | Independent HTTP requests/connections synchronize their initial credential reads; single failure and concurrent failures from counts 0 and threshold−1 persist exact counts and safe generic 401s. |
| PENDING CONFIRMATION STALE RESPONSE | PASS | Deferred requests ignore cancellation deliberately; real Vue Router test proves URL/selection B, visible detail B, cleared A overlay, and mutation request subject B after A completes late. |
| ENTITY DETAIL STALE ACTION SAFETY | PASS | Five-feature load/mutation tests cover loading, B failure, rapid A/B/A, stale success/error/finally, live route mismatch, late mutation completion, and unmount. Database object/column regressions also pass. |
| EVIDENCE DELETED SUBJECT MUTATION | PASS | Current-subject correction succeeds for Editor/Admin; Viewer remains 403. After real System soft delete, direct correction/add rejects with the existing invalid-reference contract; complete Evidence serialization is unchanged; historical GET remains 200. |
| RELATION EVIDENCE LIFECYCLE | CONTRACT GAP | #5 BLOCKED; no lifecycle option was invented or implemented. |
| DISCOVERY SYNC SAVE CONCURRENCY | PASS | Real Version updates between precheck and SaveChanges on actions/preview/confirm/supersede return exact sanitized 409; plan fields, audit count and apply-result state prove no partial write. |
| BACKEND REGRESSION | PASS — affected scope | 78 passed, 0 failed, 0 skipped with the approved serial gate. Supplemental unrelated migration failure is disclosed separately below. |
| FRONTEND REGRESSION | PASS — affected scope | 8 files, 49 tests passed; type-check, whole-project lint, and production build passed. |
| REPOSITORY DATA PROTECTION | PASS | Filesystem-only existence, size, UTC timestamp and SHA-256 checks match the baseline; no repository SQLite connection was opened. |
| CLEANUP | PASS | Test hosts and one-shot commands exited; no API/Vite/watch server or verification port was created. The initial failed fixture's task-owned directory and TRX output directory were removed. |

## Final implementation

### #1 Authentication

`LocalLoginService` verifies the password against an untracked snapshot, then uses the existing `SqliteImmediateTransaction` for a short authoritative credential/User re-read and write. A changed password hash, username, or SessionVersion rejects the old verification; active-state checks remain fail closed. Wrong-password writes recompute the failure window, count and lock from the current row. Two failures already in flight count twice (0 → 2; 4 → 6 with threshold 5); establishing the lock does not allow the second failure to extend it. Requests that initially observe a locked account do not write. Correct login rechecks the current lock before clearing counters.

No static semaphore, application lock, ignored concurrency exception, schema change, SessionVersion bookkeeping increment, or reduced credential protection was introduced. Existing successful-login Version/rehash semantics and generic authentication responses remain intact. Password lifecycle and access-control regressions pass.

### #2/#3 Detail selection and action identity

System, BusinessFunction, BusinessRule, Integration and UnknownItem retain concrete feature composables. Each load clears the old detail immediately, enters loading, cancels the old request, and requires both the current generation and selected ID before applying success, error, or finally state. Unmount invalidates the request. Mutation methods validate the loaded ID against the live route, capture their subject before awaiting, and cannot refresh an old subject after selection changes.

Page action guards and rendering use the current route identity. Delete-dialog execution repeats that identity check. Route guards close subject-bound overlays through the existing dirty-drawer confirmation behavior before navigation. Pending-item confirmation/prompt, resolution, draft-target and Apply continuations retain their initiating subject and reject stale continuations; route changes clear the prior draft fields.

The same demonstrated request issue was corrected in DatabaseObject detail (including invalid-column fallback), DatabaseColumn drawer, System unified knowledge view, and DatabaseObject Evidence loading. KnowledgeDocument's main detail path already has a request sequence/ID guard and clears the old head on ID changes; its architecture was not refactored. No generic detail framework or new dependency was added.

### #4 Evidence mutations

C24 now acquires the existing immediate transaction before fetching Evidence, resolves the current Subject through the query-filtered resolver, and rejects a missing/deleted Subject before changing any Evidence field. This closes the check/write race with root deletion. C23/C25 already resolve their current Subject within that transaction; investigation Evidence is restricted to its extant workflow Subject. No Evidence delete/rebind endpoint exists. Historical Q16/list tombstone readers remain unchanged.

The existing `EvidenceFailure.SubjectNotFound` mapping returns `422 reference_invalid`; Viewer authorization remains the earlier 403 boundary. Subject correction does not rebind Evidence, rewrite snapshots, or advance KnowledgeStatus.

### #6 Discovery save conflict handling

Actions, Preview, Confirm and supersede use a small shared save function that catches only `DbUpdateConcurrencyException`. EF's SaveChanges transaction rolls back the plan and its audit event together. Apply's explicit transaction and concurrency handling remain; its supersede branches now honor the same save result. The prior redundant second save after supersede was removed.

The existing Apply identifier-collision mapping is restricted to SQLite UNIQUE (2067) failures on Object/Column/typed binding entries. Other DbUpdateExceptions are not mislabeled as stale/concurrent state; a focused negative test proves propagation without any persisted preview/audit change. Creation has no existing-plan optimistic token; read-only object-selection expansion does not save a Plan. Existing prechecks, hash-bound confirmation, all-or-nothing Apply, protected manual fields, ordinal handling and authorization regressions pass.

## RELATION-EVIDENCE-CONTRACT-GAP

- **Status:** OPEN / BLOCKED, design owner required.
- DELETE-A01 inventory explicitly says relation removal remains physical and explicit; its KnowledgeRelation row says to retain physical `移除关系`, not introduce generic soft delete.
- DELETE-A01 preserves Evidence/HC as historical facts and provides tombstones for the eight soft-delete roots. It does not freeze a retained identity model for a physically removed KnowledgeRelation.
- C24 does not allow changing the Subject; the frozen contract has no Evidence delete/rebind API. KC-C01 is a vocabulary/matrix decision and explicitly leaves lifecycle/Evidence behavior unchanged.
- The sources therefore do not uniquely choose A (block remove when Evidence exists), B (relation tombstone), or C (Evidence historical Subject snapshot). Existing Evidence subject resolution still requires the relation row.
- No relation mutation, schema, cascade, orphan cleanup, copied Subject or semantic relation change was made. The known unreadable-history risk after relation removal remains unresolved.
- **Required next input:** an explicit lifecycle/dependency contract decision. Do not start STABILITY-R02 or implement one of the options implicitly.

## Commands and test evidence

Backend affected gate:

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests -c Release --no-restore
  --settings <task-owned serial runsettings>
  --filter 'FullyQualifiedName~LocalLogin|FullyQualifiedName~LocalPassword|FullyQualifiedName~LocalCredential|FullyQualifiedName~Evidence|FullyQualifiedName~DatabaseDiscoverySyncApiTests|FullyQualifiedName~CoreSoftDelete|FullyQualifiedName~AccessControl'
```

Result: **78/78 PASS**. The runsettings uses `MaxCpuCount=1`, `ParallelizeTestCollections=false`, `MaxParallelThreads=1`, as already approved for `REV-GAP-011`. Real TestServer HTTP/EF/SQLite tests provide the focused runtime verification; no direct executable or shared development server was started.

Frontend affected gate:

```text
npm run type-check
npm run lint
npm test -- src/app/detailRequestSafety.spec.ts src/features/unknown-items/pages src/features/database-knowledge/composables src/features/database-knowledge/pages/DatabaseObjectDetailView.spec.ts src/features/database-discovery/components/SyncPlanDialog.spec.ts src/features/evidence/components/EvidenceDetailDrawer.spec.ts
npm run build
```

Result: **49/49 PASS**, type-check/lint/build PASS. Vite retains the existing large-chunk advisory. This is component/composable/router testing, not a claim of a real Production browser deployment.

`dotnet build SystemKnowledgeHub.sln -c Release --no-restore` passed with zero warnings/errors. No repository CI format gate is configured; unrelated existing formatting was preserved instead of reformatting the solution. Final `git diff --check`: PASS.

### Supplemental failed check and verification limitations

A broader exploratory backend filter also selected the unchanged `DatabaseDiscoverySyncMigrationTests`. Its exact B04 table-delta assertion fails after `MigrateAsync()` applies latest, because the already-approved Portal foundation adds three tables. This is **FAIL**, not a migration PASS or a skipped-success claim. No schema or migration is changed by STABILITY-R01; this supplemental check is outside its required affected behavior gate. Recorded as **REV-GAP-012 OPEN / Deferred** in the existing `PHASE_REV_GAP_REGISTER.md`; no unrelated migration-test correction was made. A full backend suite is not claimed all passing.

One initial new-login fixture run hit a test-host log-file disposal IOException; subsequent isolated login and complete affected serial runs passed. The task-owned leftover directory was removed during final cleanup. No runtime safety or logging policy was weakened to pass verification.

## Repository data protection

The repository database was never opened by SQLite/EF, migrated, seeded, checkpointed, copied into a live verification runtime, or otherwise mutated. Only filesystem metadata and SHA-256 were read.

| File | Before/after presence | Bytes | LastWriteTimeUtc | SHA-256 |
| --- | --- | ---: | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | Present / Present | 1355776 | `2026-09-04T15:12:55.7499757Z` | `12ACA3AD05B199D3C8591C17D38FA1F145924DF317956132B7D0DC2DFC6A328A` |
| `system-knowledge-hub.db-wal` | Absent / Absent | — | — | — |
| `system-knowledge-hub.db-shm` | Absent / Absent | — | — | — |

The two `.vs/.../CopilotIndices/17.14.1686.8233` databases were also filesystem-baselined: `SemanticSymbols.db` (2977792 bytes, SHA-256 `705DC0F8B8EC6BA168495618385FD8C401E7A9AEFD3F56F6A9CA275D354B57F6`) and `CodeChunks.db` (7045120 bytes, SHA-256 `D5102EADEA9726D4E4473F8CF3F76AF879FC47DC9D6F932644AC9E261FD10F82`); size, timestamp and hash stayed unchanged.

All test factories use isolated memory/task-owned SQLite, ephemeral/task-owned keys, attachments and logs. The concurrent-login test uses a unique task-owned file database with independent non-pooled connections. No production configuration was changed.

## Documentation and delivery

This report owns the STABILITY-R01 corrective behavior and verification record. `DOCUMENT_INDEX.md` links it; `PHASE_REV_GAP_REGISTER.md` records the supplemental test issue. No frozen document or Golden UI asset was changed. Task-only staging excludes both initial user document changes.

Implementation/verification status is separate from Git delivery. Planned delivery: one commit on `main`, `fix: harden concurrency and stale detail safety`, then ordinary push to the configured GitHub remote; the final task response records the actual SHA and push result. A successful push does not close #5 or REV-GAP-012.
