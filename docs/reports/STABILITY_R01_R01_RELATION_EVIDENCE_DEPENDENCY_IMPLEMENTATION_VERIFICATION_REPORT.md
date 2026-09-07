# STABILITY-R01-R01 — KnowledgeRelation Evidence Dependency Enforcement

Date: 2026-09-07. **STABILITY-R01-R01 PASS**.

## Baseline and authority

Branch `main`; starting HEAD `5369649a97bef06608398e923edb08a2a7fa140e` (`docs: freeze relation evidence removal dependency`). Status/history checks confirm the requested baseline. The existing user DBDISC-FINAL-R01 index entry and untracked `DBDISC_FINAL_R01_VERIFICATION_REPORT.md` remain untouched and excluded from this task's commit.

Read AGENTS.md and DOCUMENT_INDEX.md; the [REL-EVIDENCE-A01 frozen decision](../design/REL_EVIDENCE_A01_RELATION_EVIDENCE_DEPENDENCY_AND_REMOVAL_DECISION.md), [decision report](REL_EVIDENCE_A01_RELATION_EVIDENCE_DEPENDENCY_DECISION_REPORT.md), [STABILITY-R01 historical report](STABILITY_R01_CONCURRENCY_STALE_DETAIL_HISTORICAL_INTEGRITY_VERIFICATION_REPORT.md), DELETE-A01, KC-C01, Evidence C23/C24/C25/Q16 contracts, and the current RelationshipService/RelationshipsController, EvidenceSubjectResolver, EvidenceService and SqliteImmediateTransaction.

This report closes the later implementation gate. Prior reports retain their original design-only / PARTIAL PASS conclusions as historical records.

## Implementation

`RelationshipService.Delete` now acquires the existing `SqliteImmediateTransaction` before loading the relation and checking canonical Evidence. The predicate is exactly `SubjectType == KnowledgeRelation && SubjectId == relation.Id`, using `AnyAsync`. It has no EvidenceType, detail-key, confidence, provider-validity, locator or KnowledgeStatus restriction. Ordinary Evidence and HumanConfirmation are protected identically.

A dependency returns the existing `RelationshipFailure.BusinessRuleViolation` without mutation. `RelationshipsController.Delete` maps it to the frozen **422 `business_rule_violation`** envelope, message `无法删除，仍存在依赖项：该关系已有知识依据或人工确认，不能直接移除。`, and null `fieldErrors` / `details`. No count, payload, provider identity or new error DTO is exposed. Editor policy, ID validation, missing 404, and successful physical removal with 200 `{}` remain.

For an unreferenced relation, physical removal and SaveChanges occur inside that same transaction, followed by explicit commit. No production caller supplies an outer transaction to this remove path. C23/C25 already use the same immediate SQLite write boundary for current Subject resolution and Evidence insert; they are unchanged. The helper, resolvers, relation vocabulary, KnowledgeStatus, soft-delete roots, schema and migrations are unchanged.

No cascade, rebind, relation tombstone, Subject snapshot, automatic Supersede, application mutex or silent concurrency-failure fallback was added.

## Focused tests and actual concurrency

New `RelationEvidenceRemovalApiTests` contains **18 cases**:

- Twelve Viewer/Editor/Administrator × no Evidence/ordinary/HC/both cases. Viewer always receives 403; Editor and Administrator can remove unreferenced relations and receive the exact sanitized 422 when referenced. Full serialized Relation/Evidence state comparison proves blocked removal preserves IDs, endpoints, content, versions, timestamps, provider snapshots and KnowledgeStatus. Existing Evidence detail and relation detail remain readable.
- Four real concurrent HTTP cases: Delete vs C23 and Delete vs C25, each with both commit orders. A task-owned file SQLite database, non-pooled independent connections, and separate authenticated clients are used. A SaveChanges interceptor pauses the first writer while its database transaction is active; a connection interceptor observes a different physical connection entering the competing request. The competing request remains pending while the first writer is held, then both requests are drained and checked.
- When append commits first, append returns 201, delete returns 422 and both rows remain; relation KnowledgeStatus stays Unknown and Evidence detail returns 200. When delete commits first, delete returns 200, append returns `422 reference_invalid`, and neither a relation nor matching Evidence remains. Both C23 and C25 are exercised with their real HTTP validation and trusted-user paths.
- A canonical-predicate case proves a different SubjectType with the same numeric ID does not block; its Subject exists. Legacy HumanConfirmation with incomplete provider information, low confidence and a detail key still blocks. All Relation/Evidence rows remain unchanged on the blocked attempt.
- Missing relation 404, invalid ID 400, and cancellation before removal SaveChanges leave no partial persisted mutation. The service cancellation case verifies rollback through a fresh context.

The tests assert zero canonical relation-subject orphan rows using a database anti-existence query, not only response codes. Existing Relationship, Evidence, deleted-subject historical detail, KnowledgeDocument Evidence/status, KnowledgeStatus and CoreSoftDelete tests form the regression gate. Soft-delete history remains preserved and Evidence/HC alone still do not block roots.

## Commands and results

| Check | Actual result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln -c Release` | PASS, 0 warnings / 0 errors |
| Focused backend gate below | PASS, 55 passed / 0 failed / 0 skipped, reported duration 1 m 22 s |
| `dotnet ef migrations has-pending-model-changes --project src/SystemKnowledgeHub.Api --configuration Release --no-build` | PASS: no changes since the last migration |
| `git diff --check` and staged diff check | PASS |

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests -c Release --no-restore
  --settings <task-owned serial runsettings>
  --filter 'FullyQualifiedName~RelationEvidenceRemovalApiTests|FullyQualifiedName~RelationshipsApiTests|FullyQualifiedName~EvidenceApiTests|FullyQualifiedName~EvidenceDeletedSubjectApiTests|FullyQualifiedName~KnowledgeDocumentEvidenceStatusApiTests|FullyQualifiedName~KnowledgeStatusApiTests|FullyQualifiedName~CoreSoftDelete'
```

The runsettings follow the existing REV-GAP-011 approved serial gate: MaxCpuCount=1, ParallelizeTestCollections=false, MaxParallelThreads=1. Concurrency inside each race test still uses separate simultaneous requests/connections. Tests build the final updated test source. No frontend change occurred, so frontend checks were not repeated.

Initial execution details: the sandbox build could not read the user's NuGet.Config; the authorized normal-access retry passed. The first new-test run had 17 passes and one invalid legacy fixture missing the database-required source locator; the fixture was corrected without changing constraints, and the final 55-case gate passed. An initial EF invocation used `-c Release`, which EF interprets as a context name; the corrected `--configuration Release` invocation passed. EF emitted existing required-navigation/global-query-filter warnings; it reported no model delta. These initial errors are not hidden or counted as passing checks.

This is a focused implementation gate, not a full backend-suite or Production-deployment claim. Existing unrelated `REV-GAP-012` (historical B04 exact-table migration assertion after Portal additions) remains OPEN / Deferred and was not selected or changed.

## DBSAFE and cleanup

The repository SQLite database was never opened by EF/SQLite, migrated, seeded, checkpointed, or copied into a verification runtime. Only filesystem metadata and SHA-256 were read.

| Protected file | Before / after |
| --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | Present; 1355776 bytes; LastWriteTimeUtc `2026-09-04T15:12:55.7499757Z`; SHA-256 `12ACA3AD05B199D3C8591C17D38FA1F145924DF317956132B7D0DC2DFC6A328A`; all unchanged |
| `system-knowledge-hub.db-wal` | Absent / absent |
| `system-knowledge-hub.db-shm` | Absent / absent |

Bootstrap test hosts provide explicit task-owned guard database, ephemeral keys, attachment and log paths before application startup. New race tests override only the EF connection with unique absolute `%TEMP%/stability-r01-r01-<guid>/relations.db`, Pooling=False. The EF model check explicitly sets `SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH` to the absolute task-owned `%TEMP%/stability-r01-r01-ef/model-check.db`; no file/directory was created by the model-only check. The environment override was removed afterward.

Test hosts/requests were disposed; task-owned database directories and factory keys/attachments/logs were cleaned. No API/Vite/watch server was launched. The testhost and its listener exited. Four task-created SDK 8 build workers were identified by PID, command line and creation time and stopped; two needed a PID-specific taskkill after Stop-Process failed. Pre-existing SDK 10/user processes were preserved. Temporary runsettings and filesystem-baseline files were removed before delivery.

## Final status

| Required item | Status |
| --- | --- |
| STABILITY-R01-R01 | PASS |
| REMOVE WITHOUT EVIDENCE | PASS — physical remove allowed |
| REMOVE WITH EVIDENCE BLOCKED | PASS |
| REMOVE WITH HUMAN CONFIRMATION BLOCKED | PASS |
| ATOMIC CHECK + REMOVE | PASS |
| DELETE VS ADD EVIDENCE CONCURRENCY | PASS — both commit orders |
| DELETE VS ADD HUMAN CONFIRMATION CONCURRENCY | PASS — both commit orders |
| NO ORPHAN EVIDENCE | PASS — tested canonical relation-subject invariant |
| NO CASCADE DELETE | PASS |
| NO EVIDENCE REBIND | PASS |
| NO NEW MIGRATION | PASS |
| REGRESSION | PASS — focused scope |
| DBSAFE | PASS |
| CLEANUP | PASS |

**RELATION-EVIDENCE-CONTRACT-GAP: CLOSED — DESIGN + IMPLEMENTATION**

**STABILITY-R01: COMPLETE** — combines the original verified #1/#2/#3/#4/#6 with this verified #5. This does not reclassify unrelated gaps or claim those unchanged features were all retested here.

**STABILITY-R02 READY: YES** — readiness only; not started. No PORTAL-VERIFY or Analysis Workspace work was performed.

## Documentation and Git delivery

This report and DOCUMENT_INDEX / PROJECT_FILE_MAP entries accompany the two production-file changes and focused test file. Frozen documents remain unchanged. Final task-only staged diff is reviewed; user changes remain unstaged.

Delivery commit message: `fix: protect relation evidence history on removal`, on `main`, followed by `git push origin main`. The final task response records the actual full SHA and push result separately from verification status. Stop after delivery.
