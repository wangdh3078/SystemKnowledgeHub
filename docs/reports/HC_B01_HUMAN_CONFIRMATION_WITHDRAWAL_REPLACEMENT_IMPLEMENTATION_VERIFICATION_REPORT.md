# HC-B01 — HumanConfirmation Withdrawal / Replacement Implementation Verification

- Date: 2026-09-07
- Authoritative baseline: `51b50064d05466c3ba376cc79fa3e722b5d843df` (`docs: freeze human confirmation correction lifecycle`)
- Branch: `main`
- Authority: HC-A01 decision/report, C23/C24/C25/Q16, U04, REV/TRACE/PORTAL-B04, DELETE-A01, REL-EVIDENCE-A01, STABILITY-R01/R01-R01/R02 and repository AGENTS.md.
- Overall: **HC-B01 PASS / COMPLETE** — final acceptance closed by HC-B01-CLOSEOUT-R01 on 2026-09-08 using the existing implementation evidence and current AGENTS verification policy.
- Delivery: **HC-B01 implementation DELIVERED TO MAIN**, commit `dcb5fa675f1930de1758627294f6ed8514effab7` (`调整代码`). This closeout does not rewrite that implementation commit.

## Implementation

HumanConfirmation facts can no longer enter C24 correction. After authorization and parsable input, a missing row returns 404; an existing Active or Withdrawn HC returns 422 `invalid_state` before ordinary correction validation, current Subject lookup, or stale-token evaluation. The ordinary seven Evidence correction paths remain covered.

The new `POST /api/evidence/human-confirmations/{id}/withdraw` accepts only reason and token. Local extension-field validation rejects client audit/actor fields with the existing 400 `validation_error` shape. Editor/Administrator can withdraw another confirmer's HC; Viewer and missing CSRF fail closed. Inside `SqliteImmediateTransaction`, the service reloads row/type/version/state and canonical active User, then writes server UTC/display-name audit, normalized reason, UpdatedAt and a single Version increment. It does not require a current Subject, so historical HC whose Subject was soft deleted remains withdrawable. The narrow response never includes the canonical withdrawal actor ID.

C25 optionally sets `ReplacesHumanConfirmationId` on a new row. Its existing write transaction validates a withdrawn HC reference, identical Subject and normalized detail key, and exact non-null current document revision where applicable. An existing direct replacement blocks any later replacement, even after that replacement is withdrawn. The filtered unique index is the final barrier; only its specific SQLite uniqueness failure maps to 409. A → B → C is supported; forks and editing an old link are not. Original confirmation/provider/revision/Subject facts remain intact.

`EffectiveEvidence.Predicate` is a small EF expression: ordinary Evidence or HC with null WithdrawnAt. There is no global Evidence filter, second lifecycle model, new permission, automatic status transition, rollback of completed investigation work, cascade removal, rebind or automatic replacement.

## Current versus historical consumers

| Consumer | Implemented boundary |
| --- | --- |
| KnowledgeDocument detail | Coverage derives only from Active HC, including older snapshot fallback and legacy-null handling. KnowledgeStatus stays unchanged. |
| Traceability | Root/node/relationship trust loads only effective Evidence. |
| Portal TrustSummary / RelatedKnowledge / Traceability | Shared effective evidence load; anonymous read and Admin Preview keep the existing safe projection, without actor/provider/withdrawal/link audit fields. |
| System knowledge view | Current Evidence count and section use effective rows. |
| BusinessFunction / Database | Related relationship/rule and column counts use effective rows; historical summaries retain withdrawn rows with `isWithdrawn`. |
| BusinessRule / Integration / Relationship / UnknownItem | Historical summaries remain visible; frontend trust/support counts exclude their withdrawn marker. UnknownItem current count and context rail use effective rows. |
| KnowledgeStatus / KnowledgeResolution | Current progression and conclusion/Apply support predicates use effective Evidence. Withdrawal does not rewrite already completed statuses, activities or applied knowledge. |
| Relationship removal / KnownValue removal | Existing dependency checks continue to include **all historical Evidence**, including withdrawn HC. |

Q16 and Evidence list add a nullable lifecycle object. Ordinary Evidence receives null. HC shows Active/Withdrawn, audit display fields and direct forward/inverse link IDs. List inverse resolution is one batch query, not a per-row lookup; detail uses one direct indexed inverse lookup. No recursive chain traversal is introduced.

## Frontend

Evidence detail hides HC correction, provides a reason-required withdrawal dialog, preserves input on conflict, disables blind resubmission and offers explicit reload. It shows plain wrapped audit text and direct record links. Current document revision is re-read before reconfirmation; a changed or legacy revision produces an explicit independent confirmation with null link. Deleted Subjects cannot be reconfirmed. The existing C25 form and current-user identity are reused.

Document historical cards now expose “查看记录”. Evidence/confirmation change events refresh current document coverage and current projections. Historical labels mark withdrawn confirmations. Scoped Prettier formatting of affected legacy compact files is included; no unrelated package or framework changes were made.

## Migration and preservation

One additive migration: `20260907133706_AddHumanConfirmationCorrectionLifecycle`.

It adds only five nullable Evidence columns, withdrawal-user RESTRICT FK/index, self RESTRICT FK, filtered unique replacement index and five CHECK constraints (all-or-none audit, nonblank name, normalized bounded reason, non-HC-null lifecycle fields, non-self link). Existing rows are not backfilled or modified. Existing HC remains Active through null lifecycle fields.

`HumanConfirmationLifecycleMigrationTests` exercises both fresh migration and generated old-schema upgrade using SQLite memory databases. It compares every old column for every seeded row, IDs/Version/provider/Subject/revision snapshots, old indexes, foreign keys and CHECK names; validates new null fields, invalid audit shapes/reasons/self-links, RESTRICT references and uniqueness; and executes Down/Up on disposable data. This does **not** assert that production Down can safely discard withdrawal history.

## Backend verification

Final focused run: **PASS — 263/263 tests, 0 failed, 0 skipped** (Release; 1 minute 47 seconds).

The selected scope includes HumanConfirmation, Evidence, CurrentUser, AccessControl, KnowledgeDocument, Traceability, Portal, KnowledgeStatus, Relationship, DatabaseKnowledge, UnknownItem, KnowledgeResolution, soft-delete/current historical boundaries, LocalAuthentication and DatabaseDiscoverySync. Collection parallelism is disabled with the repository-approved `xUnit.ParallelizeTestCollections=false` gate (REV-GAP-011). The known unrelated `DatabaseDiscoverySyncMigrationTests` exact-table assertion remains excluded and REV-GAP-012 remains OPEN / DEFERRED.

Coverage includes:

- Active/Withdrawn C24 rejection priority, seven ordinary C24 types, safe ID/token/reason limits, extra-field rejection, Editor/Admin/Viewer/CSRF, inactive canonical actor and deleted Subject withdrawal.
- Complete original-fact comparisons, immutable old link, normalized historical detail keys, replacement validation, same/new/legacy document revisions, lifetime uniqueness and A → B → C.
- Real separate SQLite connection/request races: withdraw/withdraw, replacement/replacement, withdrawal-first replacement, and both document-save/C25 commit orders. First write is held by a test interceptor while the contender opens a different connection; no application static lock is introduced. Replacement-before-withdrawal is also verified against the Active old row, then withdrawal succeeds.
- Mixed ordinary + Active + Withdrawn projection checks in System/BusinessFunction/Database and Trace, Portal Related/Trace and TrustSummary; history stays readable.
- Investigation confirmation rejects withdrawn-only support, accepts new active support, and later withdrawal leaves completed conclusion and applied KnownValue intact.
- Withdrawn-only Relation and KnownValue historical dependencies continue to block removal.

The old deleted-subject historical test now expects the explicitly approved `WithdrawHumanConfirmation` action for HC and still no action for ordinary Evidence. The preceding broad run identified only that obsolete assertion; focused reverification passed 42/42. Portal-focused verification passed 51/51 before the final additional Related mixed-evidence assertion.

## Frontend and tool verification

- Affected feature Vitest: **341/341**, 46 files, 0 skipped.
- Lifecycle drawer tests: withdrawal controls, Viewer hiding, deleted Subject behavior, original facts/audit and narrow submission; existing C25 tests now also verify same-revision linkage and link clearing after revision reload.
- Type check: PASS.
- ESLint: PASS.
- Vite production build: PASS (existing large-chunk advisory only).
- Scoped Prettier: PASS for all affected frontend files.
- Supplemental full Vitest: **609 passed / 2 failed**. Both failures are unchanged pre-existing assertions in `productConsistencySurface.spec.ts` (formatted closing tag) and `PortalLayout.spec.ts` (existing Search header). Their production sources and tests have no task diff. They are recorded in the existing gap register's unrelated baselines; this report does not claim a fully passing whole-repository frontend suite.
- Final `dotnet build SystemKnowledgeHub.sln -c Release --disable-build-servers`: PASS, 0 warnings / 0 errors.
- Final EF `has-pending-model-changes`: PASS, no model changes, explicit absolute task-owned `%TEMP%/hc-b01-ef/model.db` path supplied through `SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH`.
- NuGet vulnerable/transitive scan: PASS, neither project has vulnerable packages in the configured source.
- `git diff --check`: PASS after removing an extra controller EOF blank line.

## Browser verification

Task-owned Development runtime used `dotnet run --project src/SystemKnowledgeHub.Api -c Release --no-build --no-launch-profile`, API 5191 / Vite 5192, with separate absolute SQLite, Data Protection, Attachment StorageRoot and Serilog paths under `%TEMP%/hc-b01-browser`. No generated executable was directly launched as a shortcut.

Actual UI checks completed:

1. Published Requirement “HC-B01 MES Lot Track In”, revision 1: create confirmation; detail shows original confirmer/time/facts, no correction action, and withdrawal action.
2. Withdraw with “原确认结论存在业务口径错误”: original facts remain, history is marked, current Evidence/HC counts drop 1 → 0, coverage returns to NoConfirmation, KnowledgeStatus stays Unknown.
3. A second already-open detail submits the old token: conflict is shown, reason remains, submit is disabled pending explicit reload; no automatic retry.
4. Reconfirm revision 1: separate new Active record and direct links in both directions; both historical cards remain. The already replaced old row cannot start another replacement.
5. Withdraw the replacement with an 880-character reason: audit wraps; original confirmation facts remain.
6. Advance document to revision 2, reconfirm from withdrawn revision 1: explicit independent-confirmation message and new revision 2 row with null replacement link, also verified in the isolated database.
7. Delete a separate task document with HC, then withdraw the already-open HC: succeeds, detail shows deleted identity and no reconfirmation action.
8. Viewer on an Active HC: effective status/facts visible, zero withdrawal buttons. Task actor role was restored afterwards.
9. Portal current trust shows 1 then 0 after withdrawal/reload; no history/actor audit is displayed. Anonymous and Admin Preview JSON contain none of the prohibited lifecycle/provider fields.

Historical browser evidence (retained; not a current required matrix): Chrome actual viewports **1366×768**, **1440×900**, **1920×1080** were visually checked; document overflow checks were false. Long audit text wraps and close/submit buttons remain reachable. **960×540** (the CSS layout space equivalent to 1920×1080 at 200%) also passed drawer/dialog reflow checks. Native browser 200% zoom itself **was not completed or verified**: shortcuts did not change zoom; opening browser appearance settings was rejected by browser URL security policy. No workaround to that policy was attempted. User assistance to set native zoom was requested. On continuation, the Chrome verification tab was no longer present, so no native zoom result could be recovered. Equivalent viewport coverage is not evidence that native zoom was verified. The then-requested native-zoom gate is superseded by the closeout policy below; it is no longer a required check.

## DBSAFE, cleanup, delivery

Repository database was inspected only by existence/size/mtime/SHA-256; never SQLite/EF opened, copied, seeded or migrated.

- `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db`: 1,355,776 bytes; mtime ns `1788534775749975700`; SHA-256 `12aca3ad05b199d3c8591c17d38fa1f145924df317956132b7d0dc2dfc6a328a`.
- Repository WAL and SHM absent before/after.
- Final recorded baseline comparison: identical existence, size, nanosecond mtime and SHA-256; WAL/SHM absent.
- Runtime fixture FK check: zero violations; five HC records, four withdrawn and one direct replacement; both revision-2 records have null replacement links.
- Cleanup: PASS. Agent-owned API/Vite and test processes stopped; no listeners on 5191/5192. Removed the isolated browser database, keys, attachments, credentials, logs, test results, baseline file, canceled concurrency database directory and 60 positively identified canceled-run fixture directories. No user process or repository runtime data was removed.
- During original HC-B01 verification, unrelated DBDISC_FINAL_R01 work and its index row were preserved outside the HC-B01 scope. They were subsequently included in the user-provided delivery commit; this closeout does not change or reclassify them.
- HC-B01 implementation: **DELIVERED TO MAIN** in `dcb5fa675f1930de1758627294f6ed8514effab7`. The original verification ended without a task commit while the former zoom gate was pending; this historical delivery hold no longer describes the current state.

## Required status

| Gate | Result |
| --- | --- |
| HC C24 IMMUTABILITY | PASS |
| WITHDRAW API | PASS |
| WITHDRAW AUTHORIZATION | PASS |
| WITHDRAW AUDIT | PASS |
| DELETED SUBJECT WITHDRAW | PASS |
| WITHDRAW CONCURRENCY | PASS |
| REPLACEMENT CREATE | PASS |
| REPLACEMENT VALIDATION | PASS |
| REPLACEMENT UNIQUE | PASS |
| REPLACEMENT CONCURRENCY | PASS |
| REPLACEMENT CHAIN | PASS |
| DOCUMENT REVISION REPLACEMENT | PASS |
| ACTIVE HC COUNT | PASS |
| EFFECTIVE EVIDENCE COUNT | PASS |
| REVISION COVERAGE | PASS |
| TRACE TRUST | PASS |
| PORTAL TRUST | PASS |
| KNOWLEDGE STATUS SUPPORT | PASS |
| INVESTIGATION SUPPORT | PASS |
| HISTORICAL READ | PASS |
| PORTAL PRIVACY | PASS |
| RELATION HISTORICAL DEPENDENCY | PASS |
| KNOWNVALUE HISTORICAL DEPENDENCY | PASS |
| ORDINARY EVIDENCE C24 REGRESSION | PASS |
| ADDITIVE MIGRATION | PASS |
| EXISTING ROW PRESERVATION | PASS |
| NO PENDING MODEL CHANGES | PASS |
| BACKEND REGRESSION | PASS |
| FRONTEND REGRESSION | PASS |
| BROWSER E2E | PASS |
| DEFAULT DESKTOP BROWSER UI CHECK | PASS — existing browser UI evidence |
| BROWSER ZOOM | NOT APPLICABLE — prohibited by AGENTS verification policy |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |

REV-GAP-012: **OPEN / DEFERRED**.

ORIGINAL STABILITY FINDING #10: **CLOSED — DESIGN + IMPLEMENTATION**.

ORIGINAL STABILITY FINDINGS #1–#10: **CLOSED** (prior R01/R01-R01/R02 evidence plus HC-A01 and HC-B01).

HC-B01: **PASS / COMPLETE**.

STABILITY HARDENING: **COMPLETE**.

PORTAL-VERIFY: **READY AFTER REV-GAP-012 DISPOSITION**. Portal B04 and UI R01/R01-R01 are complete; no PORTAL-VERIFY was run.

No HC-B02, REV-GAP-012 fix or Analysis Workspace was started.

## HC-B01-CLOSEOUT-R01 — final acceptance follow-up

2026-09-08; authoritative main `dcb5fa675f1930de1758627294f6ed8514effab7`. **HC-B01-CLOSEOUT-R01 PASS**.

**Native Browser Zoom: NOT APPLICABLE. AGENTS VERIFICATION POLICY: APPLIED.** Current root [AGENTS.md](../../AGENTS.md), sections 8–9, explicitly prohibits browser zoom validation and excludes it from required verification and PASS/FAIL gates, including 125%, 150%, 175%, 200% and every other percentage. Under this user-authorized closeout, that repository-wide policy supersedes the old task's native-zoom gate. No browser zoom, equivalent-viewport substitute or responsive workaround is required or requested. The original incomplete attempt remains recorded above; no native 200% result or responsive-matrix certification is claimed.

The existing 263/263 backend and 341/341 affected frontend PASS evidence, type-check, ESLint, Vite/Release builds, Prettier, EF no-pending-model-changes, NuGet scan, Browser E2E, DBSAFE and cleanup results are preserved. These are historical results corresponding to the delivered HC-B01 implementation, not newly executed checks. The supplemental whole-frontend failures also remain historical facts. HC-B01 closure is independent of **REV-GAP-012 OPEN / DEFERRED**.

This closeout changes documentation/status only. Verification is document consistency review and `git diff --check`; no product code, migration or tests changed, and no test suite, browser, runtime or database operation was run. DOCUMENT_INDEX is synchronized; PROJECT_FILE_MAP's two stale future-implementation descriptions are clarified. HC-B01 and stability hardening are COMPLETE. REV-GAP-012, PORTAL-VERIFY and Analysis Workspace were not started.
