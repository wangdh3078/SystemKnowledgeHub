# DELETE-VERIFY — Final Soft Delete Chain Verification Report

## Result

**DELETE-VERIFY PASS.** The approved DELETE-A01 contract is implemented coherently across DELETE-B01 through DELETE-B04. The final acceptance audit found no integration regression or new DELETE-scope gap. All required backend and frontend gates passed except for the already-recorded, unrelated frontend baseline deviations listed below.

## Verification scope and authority

This final gate reviewed the root `AGENTS.md`, `docs/DOCUMENT_INDEX.md`, DELETE-A01, the frozen MVP UI/design/domain/database/application/API/solution specifications, and the DELETE-B01/B02/B03/B04 task and verification reports. DELETE-A01 is the later approved capability-specific authority for the eight-root soft-delete scope; earlier frozen MVP text that excludes core delete is not edited.

DELETE-VERIFY did not reimplement B01-B04. No frozen source, Golden UI asset, task definition, or application code was changed in this final gate; only this verification report and the document index are added.

## Cross-slice integration audit

| Slice | Contract carried forward | Final audit result |
| --- | --- | --- |
| DELETE-B01 | Soft-delete fields, canonical creator ownership, version/concurrency token, active-only filters/indexes, FTS foundation, migration/integrity | PASS; B01 report and current solution build/tests remain green |
| DELETE-B02 | Eight concrete use cases, authoritative ownership, bounded dependency guards, atomic SQLite write boundary, audit metadata, race/concurrency semantics | PASS; current full backend suite is 175/175 |
| DELETE-B03 | Current projections/selectors/search/FTS exclusion, historical read boundary, revision/Evidence/relationship filtering, tombstone source data | PASS; B03 report evidence remains valid and no DELETE code changed after B04 |
| DELETE-B04 | Capability-driven delete UX, shared confirmation/error handling, safe navigation, historical tombstones, accessibility/responsive/overlay behavior | PASS; B04 isolated browser/runtime evidence is preserved and re-audited below |

## Eight approved root matrix

All eight root delete routes are explicit, target-specific use cases. Each resolves the canonical current user, applies `SoftDeleteAuthorization.CanDelete`, validates an opaque version token, checks only the root's bounded active blockers, writes the soft-delete audit/version atomically, and leaves lifecycle/status/content/owned history unchanged.

| Root | Endpoint | Ownership/capability | Active projection and FTS | Historical rule | Result |
| --- | --- | --- | --- | --- | --- |
| System | `DELETE /api/systems/{id}` | Administrator or Editor owner | Hidden from current lists/details/selectors/derived views; active dependencies block | Retained references render tombstones | PASS |
| BusinessFunction | `DELETE /api/business-functions/{id}` | Administrator or Editor owner | Same active-only boundary | Historical references retained | PASS |
| DatabaseSource | `DELETE /api/systems/{systemId}/database-sources/{id}` | Administrator or Editor owner | Source and browse context exclude deleted source | Historical snapshots retain identity | PASS |
| DatabaseObject | `DELETE /api/database-objects/{id}` | Administrator or Editor owner | Hidden from current object/selector/search projections | Historical references retained | PASS |
| DatabaseColumn | `DELETE /api/database-columns/{id}` | Administrator or Editor owner | Hidden from current column/detail projections | Historical references retained | PASS |
| BusinessRule | `DELETE /api/business-rules/{id}` | Administrator or Editor owner | Hidden from current rule/feature projections | Historical references retained | PASS |
| Integration | `DELETE /api/integrations/{id}` | Administrator or Editor owner | Hidden from current integration/system projections | Historical references retained | PASS |
| KnowledgeDocument | `DELETE /api/knowledge-documents/{id}` | Administrator or Editor owner | Hidden from current list/detail/search and FTS | Revisions and historical references remain readable through the approved boundary | PASS |

The only other `HttpDelete` is the explicit `RelationshipsController` correction endpoint. It physically removes a relationship row as the A01-approved correction path; it is not a root-delete, cascade, or bulk-delete capability.

## Authorization, `canDelete`, and ownership

| Actor/state | Expected behavior | Result |
| --- | --- | --- |
| Anonymous | No authenticated canonical actor; denied | PASS |
| Viewer | Read-only; `canDelete=false`, delete action absent | PASS |
| Editor owning the root | `canDelete=true`; delete allowed | PASS |
| Editor owning another user's root | `canDelete=false`; action absent and write returns 403 | PASS |
| Editor with legacy/null/unknown creator | Fail-closed (`canDelete=false`, 403) | PASS |
| Administrator | Supported roots allowed regardless of creator, including legacy owner | PASS |
| Renamed/deactivated creator or deleter snapshot | Canonical IDs/audit snapshots remain FK-valid and readable; display names are not ownership authority | PASS |

The authoritative read contracts project `canDelete`; the frontend does not infer ownership from display names, request actors, or role labels.

## Error and concurrency contract

| Status | Contract | Result |
| --- | --- | --- |
| 400 | Invalid route/body/token shape is rejected without mutation | PASS |
| 403 | Missing permission/ownership is denied without mutation | PASS |
| 404 | Missing or already-deleted route root is not found; stale detail is not treated as success | PASS |
| 409 | Stale opaque concurrency token returns conflict; no invented retry or resurrection | PASS |
| 422 | Active dependency/reference blocker returns bounded structured groups/counts/names; storage unchanged | PASS |
| 204 | Authorized unblocked delete succeeds; version increments and audit is written | PASS |

B02 race coverage proves delete-vs-edit, delete-vs-relation-add, delete-vs-status-progression, and delete-vs-UnknownItem-Apply interleavings cannot leave an active dependency attached to a deleted target. Evidence, HumanConfirmation, revisions, and completed workflow history alone are historical and do not block.

## Current projection and FTS

All current list/detail/selector/search/Dashboard/Unified View/Trace/Impact/Supersedes and current mutation paths resolve active truth. A successful KnowledgeDocument delete in the isolated B04 runtime produced `is_deleted=1`, removed the document's FTS row, and reduced the active current-document projection to zero. No active deleted root remained discoverable through current UI navigation or search.

## Historical reads and tombstones

| Historical surface | Read retained | Marker/name retained | Link removed | Mutation/recovery action removed | Result |
| --- | ---: | ---: | ---: | ---: | --- |
| Evidence | Yes | Yes | Yes | Add/correct actions hidden for deleted subject | PASS |
| HumanConfirmation | Yes | Yes | Yes | Subject mutation boundary retained | PASS |
| KnowledgeDocument revisions | Yes, through explicit historical entry | Yes | Yes | Restore absent for deleted owner | PASS |
| Closed UnknownItem context | Yes | Yes | Yes | Reopen/apply path hidden for deleted target | PASS |
| Applied KnowledgeUpdate context | Yes | Yes | Yes | Apply path hidden for deleted target | PASS |

`HistoricalTargetLabel.vue` is the shared boundary: deleted targets keep their original name, visible line-through styling, and explicit `已删除`, render as plain text, and never receive a route. Historical reads do not expose a full deleted current detail.

## Scope boundary audit

Static route/service/UI inventory found no DELETE-scope capability for:

- root Restore/recycle-bin/deleted-list recovery;
- hard delete, purge, retention cleanup, or automatic dependency cleanup;
- cascade delete, bulk delete, generic delete framework, or speculative CRUD layer;
- delete-triggered lifecycle/status/content/revision mutation.

The existing KnowledgeDocument revision restore feature belongs to the separately approved PHASE-REV contract. DELETE-VERIFY added no restore surface; B04 verifies Restore is unavailable for a deleted owner. Relationship row removal remains the explicitly approved physical correction path and is not cascade behavior.

## Build and test verification

Verification-only processes were run one-shot and cleaned after each cycle.

- `dotnet build SystemKnowledgeHub.sln --no-restore -c Release`: **PASS**, 0 warnings, 0 errors.
- `dotnet test SystemKnowledgeHub.sln --no-build -c Release --settings .tmp/delete-verify-serial.runsettings`: **PASS**, 175/175, 0 skipped. The temporary runsettings serialized xUnit collections and was deleted immediately after the run.
- `npm run type-check`: **PASS**.
- `npm run build`: **PASS**; the existing Vite chunk-size advisory remains.
- Affected DELETE-B04 Vitest set: **51/51 PASS** (inherited from the verified B04 gate; no affected source changed after that gate).
- Full `npm run test`: **303/304**. The sole failure remains the unrelated pre-existing `AppShell.spec.ts` expectation for `关系与缺口`; it is outside DELETE and unchanged by B01-B04.
- ESLint over B04 changed frontend files: **0 errors**, one existing test-file structure warning. Full-project lint still has the unrelated `CreateIntegrationDialog.vue` unused `props` error.
- `git diff --check`: **PASS** for this report/index change.

## Runtime verification and cleanup

The B04 authenticated isolated browser master flow remains the DELETE behavior evidence. A later non-DELETE production-startup configuration commit was audited separately; the current source was started in Development against a fresh task-owned SQLite database and returned `GET /api/auth/options` = 200 (`localLoginEnabled=true`, `oidcLoginEnabled=false`) and anonymous `GET /api/current-user` = 401. The current runtime exited cleanly after this smoke check. The B04 flow covered login, administrator/editor/viewer capability states, editor ownership denial, System dependency blocking with real 422 groups, successful KnowledgeDocument deletion, current-list disappearance, historical `?view=history` revision read, non-navigable tombstones, navigation safety, accessibility, 1440×900 and 1280×720 responsive layouts, and zero captured browser console errors.

The B04 runtime used only task-owned ports 5193/5194, an isolated local administrator, isolated Data Protection keys, and a temporary SQLite database. Both services and the browser tab were stopped, the exact temporary directory was removed, and current checks show no agent-owned listener on 5193 or 5194. The pre-existing system listener on 5088 was not touched.

Temporary database integrity after the successful delete was `PRAGMA integrity_check=ok`, `foreign_key_check` returned zero rows, and WAL checkpoint reported `busy=0`, `log=0`, `checkpointed=0`.

## Repository database status

The checked-in repository database was never opened, migrated, seeded, or connected to. Before/after filesystem protection evidence is identical:

- Path: `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db`
- Length: `897024` bytes
- LastWriteTimeUtc: `2026-08-27T15:46:01.9864232Z`
- SHA-256: `7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483`
- Repository `-wal` / `-shm`: absent

## Existing gaps

These are unchanged baseline items, not DELETE-VERIFY regressions:

1. `REV-GAP-011` remains OPEN/Deferred Low: default parallel backend test collections can stall; the approved serial runsettings gate passed 175/175.
2. The unrelated full Vitest `AppShell.spec.ts` stale `关系与缺口` expectation remains (303/304).
3. Full-project ESLint retains the unrelated `CreateIntegrationDialog.vue` unused `props` error.
4. One changed test file retains the pre-existing `vue/one-component-per-file` warning; CSS notices are outside this repository's configured CSS lint scope.
5. Vite reports its existing production chunk-size advisory.

None of these gaps changes DELETE authorization, persistence, dependency, projection, FTS, historical, or UX behavior.

## New gaps

**None found.** No DELETE-B01/B02/B03/B04 integration regression, omission, blocker, High, or Medium gap was identified. No new recovery, cascade, hard-delete, bulk-delete, or database-safety issue was introduced.

## Final readiness

`DELETE-VERIFY READY: YES`

All eight roots, ownership/capability projection, 400/403/404/409/422/204 contracts, dependency atomicity, current projection and FTS exclusion, historical tombstones, scope boundaries, SQLite integrity, repository database protection, build/test gates, runtime evidence, and verification cleanup are accepted.

## Final result

**DELETE-VERIFY PASS**
