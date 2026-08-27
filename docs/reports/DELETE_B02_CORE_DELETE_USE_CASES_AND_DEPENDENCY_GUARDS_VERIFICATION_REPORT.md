# DELETE-B02 — Core Delete Use Cases and Dependency Guards Verification Report

Date: 2026-08-27–2026-08-28

Branch: `main`

Authority: DELETE-B02 task definition and the approved `docs/design/DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md`

## Result

**DELETE-B02 PASS.**

The implementation, API contracts, authorization/ownership rules, dependency guards, concurrency behavior, FTS atomicity, deterministic race coverage, build, complete serial backend regression, frontend regression, isolated runtime smoke, isolated SQLite integrity, repository-database corrective protection gate, and verification cleanup passed.

The first repository-database comparison detected that the ignored database's original WAL had been checkpointed into its main file. The user was informed of the exact physical delta and explicitly authorized using that coherent checkpointed state as the corrective baseline. A new full verification cycle then proved the approved main-file bytes and metadata unchanged with no remaining WAL/SHM. The incident and corrective evidence are retained below rather than hidden.

## Baseline and authority gate

- Start branch/HEAD: `main` at `1c71cfd846cf41f50d7e030c35bde1eb8853f357`, matching `origin/main`.
- Initial tracked working tree: clean.
- Frozen specifications, `docs/DOCUMENT_INDEX.md`, DELETE-A01, the DELETE-B01 verification report, and the DELETE-B02 task definition were reviewed before implementation.
- DELETE-B01 was confirmed as the persistence/ownership prerequisite.
- DELETE-B02's later explicit body-reference rule supersedes the earlier DELETE-B01 test expectation: missing or deleted body-referenced roots return `422 reference_invalid`; a missing or deleted route root remains `404 not_found`.
- No frozen specification or Golden UI asset was modified.

## Implemented scope

Eight explicit delete endpoints were added. No generic CRUD/delete framework, repository abstraction, CQRS layer, restore endpoint, recycle bin, or delete UI was introduced.

| Root | Endpoint | Concrete use case |
| --- | --- | --- |
| System | `DELETE /api/systems/{id}` | `SystemDeleteService` |
| DatabaseSource | `DELETE /api/database-sources/{id}` | `DatabaseKnowledgeDeleteService.DeleteSource` |
| BusinessFunction | `DELETE /api/business-functions/{id}` | `BusinessFunctionDeleteService` |
| DatabaseObject | `DELETE /api/database-objects/{id}` | `DatabaseKnowledgeDeleteService.DeleteObject` |
| DatabaseColumn | `DELETE /api/database-columns/{id}` | `DatabaseKnowledgeDeleteService.DeleteColumn` |
| BusinessRule | `DELETE /api/business-rules/{id}` | `BusinessRuleDeleteService` |
| Integration | `DELETE /api/integrations/{id}` | `IntegrationDeleteService` |
| KnowledgeDocument | `DELETE /api/knowledge-documents/{id}` | `KnowledgeDocumentDeleteService` |

The endpoint request contracts contain the required JSON `concurrencyToken`. Successful deletion returns `204 No Content`.

## Authorization, ownership, and state mutation

| Case | Verified result |
| --- | --- |
| Anonymous | `401` before controller execution |
| Viewer | `403` by Editor policy |
| Editor deleting own active root | Allowed |
| Editor deleting another user's root | `403 forbidden` |
| Editor deleting legacy root with null creator | `403 forbidden` |
| Administrator deleting any active root, including legacy/null creator | Allowed |

The delete mutation changes only `IsDeleted`, `DeletedAt`, `DeletedByUserId`, `DeletedByDisplayName`, and `Version = Version + 1`. Audit time is server UTC and actor identity is resolved from the authenticated canonical user, not the request's display actor. Tests assert that business fields, lifecycle/status, current/published revision pointers, revision rows, and historical rows are unchanged.

## Dependency guard matrix

Each dependency check runs inside the same SQLite immediate transaction as the final mutation. Counts are category summaries, are deterministic, and are bounded to at most eight categories.

| Root | Active blockers, in response order |
| --- | --- |
| System | `technologyTags`, `businessFunctions`, `databaseSources`, `businessRules`, `integrations`, `unknownItems`, `knowledgeRelations`, `proposedKnowledgeUpdates` |
| DatabaseSource | `databaseObjects`, `integrations`, `knowledgeRelations`, `unknownItems`, `proposedKnowledgeUpdates` |
| BusinessFunction | `processSteps`, `knowledgeRelations`, `unknownItems`, `proposedKnowledgeUpdates` |
| DatabaseObject | `databaseColumns`, `integrations`, `knowledgeRelations`, `unknownItems`, `proposedKnowledgeUpdates` |
| DatabaseColumn | `knownValues`, `knowledgeRelations`, `unknownItems`, `proposedKnowledgeUpdates` |
| BusinessRule | `knowledgeRelations`, `unknownItems`, `proposedKnowledgeUpdates` |
| Integration | `contractFields`, `knowledgeRelations`, `unknownItems`, `proposedKnowledgeUpdates` |
| KnowledgeDocument | `knowledgeRelations` |

Active child/embedded rows, active relationships where the root is either endpoint, open UnknownItems, and Proposed KnowledgeUpdates block as specified. Closed UnknownItems, Applied updates, Evidence, HumanConfirmations, and immutable KnowledgeDocument revisions are historical evidence and do not block. Blocked deletion changes neither the root nor dependencies; there is no cascade and no automatic relation removal.

## Error contract matrix

| Condition | HTTP/code |
| --- | --- |
| Invalid route id or malformed/empty concurrency token | `400 validation_failed` |
| Authenticated but unauthorized ownership/access | `403 forbidden` |
| Missing or already-deleted route root | `404 not_found` |
| Stale token for an active root | `409 conflict` |
| Active dependencies | `422 business_rule_violation` with bounded `details.blockers` |
| Missing/deleted body reference after a delete | `422 reference_invalid` |

Post-delete mutation tests cover route-root edits returning `404`, and child creation, relationship creation, Evidence, HumanConfirmation, and KnowledgeStatus body-reference operations returning `422 reference_invalid`. No path resurrected a deleted row.

## Transaction and race design

`SqliteImmediateTransaction` starts `BEGIN IMMEDIATE` through `Microsoft.Data.Sqlite` and enlists EF Core in the transaction. If a composed use case already owns an EF transaction, the helper borrows it without committing or disposing it. This keeps existing UnknownItem/Evidence composition atomic while serializing root delete against mutations that could otherwise create a blocker or resurrect a stale tracked root.

Deterministic real-file SQLite tests use command interceptors and explicit task barriers; they do not use timing sleeps.

| Race | Mutation commits first | Delete commits first |
| --- | --- | --- |
| Relationship add vs root delete | Relationship succeeds; delete returns dependency blocker | Delete succeeds; relationship returns reference-invalid result |
| Child create vs parent delete | Child succeeds; delete returns child blocker | Delete succeeds; body-referenced child create returns the reference-invalid application result |
| Root edit vs root delete | Edit increments Version; delete returns stale-token conflict | Delete succeeds; edit returns not-found and cannot resurrect |

All six interleavings passed. A separate failure-injection test drops the KnowledgeDocument FTS table and proves that FTS removal failure rolls back canonical deletion, audit fields, and Version. The successful path removes the FTS row in the same transaction and creates no revision.

## Query-plan evidence

The focused query-plan test executes representative dependency probes for relationship endpoint lookup, active UnknownItem target lookup, Proposed KnowledgeUpdate target lookup, Integration source lookup, and child lookup. Plans used the B01 indexes rather than table scans for the guarded predicates. This test is included in both the affected and full regression results below.

## Verification evidence

### Build and automated tests

| Check | Result |
| --- | --- |
| Release solution build | PASS, 0 warnings, 0 errors |
| Initial focused regression after transaction integration | 46/46 PASS after correcting nested ambient transaction handling |
| Core delete API focused suite | PASS |
| Deterministic race/atomicity/query-plan suite | 8/8 PASS |
| Final affected backend regression | 48/48 PASS |
| Full backend regression, serial runsettings | 164/164 PASS, 0 failed, 0 skipped, approximately 34 seconds |
| Corrective full backend regression after user-approved database re-baseline | 164/164 PASS, 0 failed, 0 skipped, approximately 33 seconds |
| Frontend `npm run type-check` | PASS |
| Frontend `npm run build` | PASS; existing chunk-size advisory only |
| Final `git diff --check` | PASS; line-ending conversion advisories only |

The first default-parallel full test invocation did not complete within the bounded observation window because of the pre-existing `REV-GAP-011` cross-test concurrency behavior. Only the two task-owned test processes were stopped, and no `testhost`/`vstest` process remained. The complete suite was then run serially and passed 164/164. The temporary runsettings file was removed immediately after the cycle.

### Isolated migration and runtime

An isolated database under `.tmp/delete-b02-runtime` was migrated through `20260827144345_AddSoftDeleteOwnershipFoundation`. A disposable local Administrator was bootstrapped, then the Release API ran on task-owned `http://127.0.0.1:51902` with isolated Data Protection keys.

Observed HTTP flow:

- local login: `204`;
- current user: `200`, canonical access level `Administrator`;
- System create: `201`;
- System delete with fresh token: `204`;
- detail after delete: `404`;
- repeated delete: `404 not_found`.

The complete role/ownership matrix was exercised through authenticated real-SQLite API tests; the standalone smoke intentionally used only the disposable Administrator credential.

After server shutdown, an application-native `Microsoft.Data.Sqlite` verifier reported:

```text
PRAGMA wal_checkpoint(TRUNCATE) => 0
PRAGMA integrity_check => ok
pragma_foreign_key_check rows => 0
runtime deleted System rows with canonical administrator audit => 1
```

The runtime API process and its remaining `dotnet run` parent were stopped by exact PID, port `51902` was confirmed released, and the isolated database, WAL/SHM, Data Protection keys, verifier, and build artifacts under `.tmp/delete-b02-runtime` were removed. No task-owned API, `testhost`, or helper process was intentionally left running.

## Repository database protection incident and corrective PASS

Initial baseline for the ignored repository runtime database:

```text
Path: src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db
Length: 724992
LastWriteTimeUtc: 2026-08-26T14:32:28.8616945Z
SHA-256: A122F8D208338A8445DCF19CE27601634B98A0B35AC1575FDBB6ADF7A3422DA9
WAL present: true
SHM present: true
```

Final observation:

```text
Length: 897024
LastWriteTimeUtc: 2026-08-27T15:46:01.9864232Z
SHA-256: 7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483
WAL present: false
SHM present: false
```

This was consistent with the original WAL being checkpointed into the main file, but the task did not capture enough page-level evidence to reconstruct the exact original physical file. A read-only search found no repository or user-profile copy with the same database name, and elevated read-only Volume Shadow Copy enumeration was unavailable. The database is ignored by Git, so Git could not restore it. No overwrite/reset was attempted because that could destroy user runtime data.

The user then explicitly authorized adopting the coherent checkpointed state as the corrective baseline. Corrective baseline and final state were identical:

```text
Length: 897024
LastWriteTimeUtc: 2026-08-27T15:46:01.9864232Z
SHA-256: 7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483
WAL present: false
SHM present: false
```

An application-native read-only inspection returned `integrity_check=ok`, zero foreign-key violations, and 19 applied migrations while preserving the main-file length, timestamp, and SHA-256. That inspection created only a zero-byte WAL and a 32768-byte SHM sidecar. After the user explicitly authorized their deletion, the exact two sidecars were size-checked and removed; the main file still matched the corrective baseline. Release build and another complete 164-test serial regression then ran against test-owned databases. The final main-file metadata/hash remained identical and no WAL/SHM remained.

Therefore the corrective `repository DB unchanged` gate is **PASS** under the explicit user-approved baseline.

## Files and architecture review

- Controllers contain eight explicit routes and map the shared result/error convention.
- Root-specific dependency queries remain in concrete feature delete services.
- Existing mutation services were changed only where required to share SQLite serialization and enforce deleted body-reference behavior.
- Shared additions are limited to soft-delete result/authorization types, consistent API response mapping, and SQLite immediate transaction ownership.
- New API and persistence tests own endpoint, blocker, historical, atomicity, query-plan, and race evidence.
- No frontend source file changed; no delete UI, restore API, or recycle-bin behavior was added.
- No migration/schema change was required in DELETE-B02.

## Final gate and DELETE-B03 readiness

```text
8/8 DELETE endpoints PASS
Viewer deny PASS
Editor own PASS
Editor other deny PASS
legacy unknown Editor deny PASS
Administrator PASS
concurrency PASS
audit PASS
Version increment PASS
all required blocker categories PASS
historical non-blocking PASS
no cascade PASS
no automatic relation removal PASS
KnowledgeDocument FTS atomic remove PASS
delete-vs-relation race PASS
delete-vs-child race PASS
delete-vs-edit race PASS
post-delete mutations protected PASS
error contracts PASS
backend build PASS
focused tests PASS
affected regression PASS
full backend regression PASS
query-plan gate PASS
SQLite integrity PASS
repository DB unchanged PASS (user-approved corrective baseline)
no delete UI PASS
no restore API PASS
no recycle bin PASS
```

**DELETE-B03 READY: YES.**

All DELETE-B02 PASS-gate items are satisfied. The initial checkpoint incident remains documented as verification history; it is not an open DELETE-B03 blocker after the explicit re-baseline authorization and passing corrective cycle.
