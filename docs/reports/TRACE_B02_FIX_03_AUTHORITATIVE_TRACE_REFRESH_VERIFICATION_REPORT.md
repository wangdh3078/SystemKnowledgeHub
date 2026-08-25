# TRACE-B02-FIX-03

## Result

TRACE-B02-FIX-03 PASS. Relationship removal now uses the same feature-local authoritative refresh orchestration as relationship addition. The current page refreshes both the relationship list and the backend-derived Traceability projection without a browser, route, or full-page reload.

## Problem Statement

TRACE-B02-REVERIFY-R02 found a Medium product regression: after removing `Specification S --VerifiedBy--> TestCase T`, the canonical relationship list removed T immediately, but Traceability continued to show T until a hard reload. The backend write and TRACE-B01 projection were correct; the detail-page remove integration failed to request the current projection.

## Historical Context

```text
TRACE-B02 initial verification: FAIL
TRACE-B02-UI-FIX-02: PASS
TRACE-B02-REVERIFY-R01: FAIL
TRACE-B02-REVERIFY-R02: FAIL — stale Trace after relationship removal
TRACE-B02-FIX-03: PASS — known regression corrected
```

This fix does not promote the historical final TRACE-B02 result. A focused TRACE-B02-REVERIFY-R03 is still required.

## Worktree Baseline

PASS. The accepted baseline was clean on branch `main` at `5b4796d` (`docs: record trace b02 r02 regression`). `git status`, the short status, recent log, diff stat, full diff, and `git diff --check` showed no uncheckpointed or unrelated changes before implementation.

## Normative Authority

Reviewed `AGENTS.md`, TRACE-A01, the system UI interaction baseline, KC-C01 relationship vocabulary, REV-A01, TRACE-B01, the historical TRACE-B02 report, UI-FIX-02, R01, R02, and the live detail, traceability, relationship mutation, evidence, status, lifecycle, restore, request-sequence, and drawer code paths. Frozen trace, coverage, trust, lifecycle, revision, direction, vocabulary, and authorization semantics remain unchanged.

## Root Cause

Add and remove used different refresh paths. `AddRelationshipDrawer` already dispatched the feature's existing `relationship:changed` event after a successful canonical write. `KnowledgeDocumentDetailView.handleRelationshipChanged()` responds by reloading the relationship list and invoking the exposed Traceability refresh, which performs the authoritative API read.

The remove callback bypassed that coordinator. After `deleteRelationship(item.id)` it called only `loadRelations()`, so the relationship list became current while Traceability retained its prior successful response.

## Fix Summary

After a successful delete, the detail page now dispatches the existing `relationship:changed` event. Add and remove therefore share one authoritative refresh contract. The fix is a one-line feature-local orchestration change; it introduces no store, event-bus framework, generic refresh abstraction, backend change, route, schema, migration, or local trace calculation.

## Files Changed

- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/TraceabilitySection.spec.ts`
- `docs/reports/TRACE_B02_FIX_03_AUTHORITATIVE_TRACE_REFRESH_VERIFICATION_REPORT.md`
- `docs/DOCUMENT_INDEX.md`

## Refresh Ownership

The detail page owns relationship-mutation refresh orchestration through the already established `relationship:changed` event and `handleRelationshipChanged()` callback. `RelationshipSection` data is reloaded from its typed relationship API, while `TraceabilitySection.refresh()` independently reloads the authoritative trace endpoint.

## Add Relationship Refresh

PASS. Existing add behavior continues to dispatch the shared event. Component and browser verification showed the relationship list and Traceability both add TestCase T, and `缺少测试定义` disappears without hard reload.

## Remove Relationship Refresh

PASS. The focused component test proves a successful delete dispatches the shared refresh, invokes exactly one relationship reload and one trace refresh, and removes T from the relationship surface. The isolated browser reproduced the R02 sequence and confirmed both the relationship list and Traceability removed T and the Specification displayed `缺少测试定义` without hard reload.

## Re-add Relationship Refresh

PASS. In the same component and browser sessions, re-adding `S --VerifiedBy--> T` restored T in both surfaces and removed the missing-test state without hard reload.

## Requirement Coverage Refresh

PASS. With `Requirement R --SpecifiedBy--> Specification S --VerifiedBy--> TestCase T`, removing S-to-T and navigating normally to R showed global `测试定义覆盖：缺少测试定义` with T absent. Re-adding the relation restored the established coverage projection. No route reload or browser hard reload was used.

## Specification Coverage Refresh

PASS. The live Specification moved from covered to missing and back to covered during remove/re-add. The component-level authoritative refresh test independently covers the same covered → missing → covered sequence.

## TestCase Impact

PASS. The affected Traceability suite continues to cover Requirement, Specification, and TestCase root response rendering. The fix changes only mutation-trigger orchestration and does not alter TestCase semantics, nodes, relation direction, or coverage computation.

## Authoritative Backend Reload

PASS. `TraceabilitySection.refresh()` calls `GET /api/knowledge-documents/{id}/traceability` through the typed traceability API. The fix invokes this existing reload path after mutation; it does not splice or mutate the displayed trace tree.

## No Local Coverage Calculation

PASS. The frontend does not set missing flags or recalculate coverage. `MissingTestDefinition`, branch contents, trust values, and counts continue to come from the decoded backend response.

## Refresh Error Handling

PASS. A focused component test starts from successful covered data, rejects the next authoritative refresh, and verifies that the old TestCase is cleared and the existing `可追溯性加载失败` ErrorState is shown. The successful relationship write is not rolled back and stale trace is not represented as current.

## Async Race Protection

PASS. A focused same-root race test starts refresh A, then B, completes B first and A last, and verifies that B's newer missing-test result remains. The existing AbortController plus monotonically increasing request-sequence guard remains intact.

## Duplicate Request Check

PASS. One logical relationship event produces one relationship-list reload and one Traceability refresh in the focused detail test. The remove path no longer performs its former direct relationship reload in addition to the coordinator, so the fix does not add a duplicate relationship request or request storm.

## Relationship Drawer Integration

PASS. From Traceability, the isolated browser opened one drawer host for S-to-T. Source, target, relationship label, unknown trust, evidence `0`, and human-confirmation `0` were correct; closing the drawer removed the overlay and left Traceability usable.

## Evidence / HC Refresh Regression

PASS. No evidence or HumanConfirmation orchestration was changed. The complete affected detail suite, including existing evidence and HumanConfirmation refresh assertions, passed. Browser trust remained independent from structural coverage: documents and relations stayed `未知` with evidence `0` and human confirmations `0` while structural coverage changed correctly.

## Content Save Refresh Regression

PASS. The browser edited, previewed, and saved Markdown after the relationship remove/re-add sequence. The structural S/T trace remained present after save. Existing detail refresh tests also remained green.

## UI-FIX-02 Regression

PASS. After edit/save/read, `.knowledge-document-saved` count was `0`, `.knowledge-document-body > h2` count was `0`, and Markdown-owned `概述` and `正文` headings rendered. Content remained ordered before Traceability, which remained ordered before Relationships.

## R06 Regression

PASS. Edit mode exposed the raw Markdown textbox and toolbar, Source/Preview switching rendered the unsaved preview, and page-level Save completed. The editor region remained bounded (`460px` observed) and its `.cm-scroller` retained `overflow-y: auto`.

## Backend Build

PASS — `dotnet build SystemKnowledgeHub.sln --no-restore -c Release`: 0 warnings, 0 errors.

## Traceability API Tests

PASS — `TraceabilityApiTests`: 12/12 passed.

## Frontend Type Check

PASS — `npm run type-check`.

## Frontend Build

PASS — `npm run build`; 3400 modules transformed. The existing large-chunk advisory remains non-blocking.

## Affected Vitest

PASS — `KnowledgeDocumentDetailView.spec.ts`, `TraceabilitySection.spec.ts`, and `traceabilityContracts.spec.ts`: 3 files / 40 tests passed. The relationship contract suite also passed: 1 file / 3 tests.

New focused coverage includes remove/re-add orchestration, authoritative covered/missing replacement, refresh failure, and late-response race protection.

## ESLint

PASS — scoped ESLint over the three modified frontend files completed with 0 errors.

## Browser Runtime Master Flow

PASS. An isolated runtime used temporary SQLite, isolated Data Protection keys, a disposable local Administrator, task-owned API/Vite processes, and one task-owned browser tab. The formal UI created `TRACE FIX03 R`, `TRACE FIX03 S`, and `TRACE FIX03 T`, then authored `R --SpecifiedBy--> S` and `S --VerifiedBy--> T`.

Initial R showed S and T with both coverage categories established while trust remained unknown. Removing S-to-T from the formal UI immediately removed T from Relationships and Traceability and displayed `缺少测试定义` on S. Normal navigation to R showed the corresponding global missing-test state. Re-adding through the formal UI immediately restored T and removed the missing state. No F5, Ctrl+R, route reload, `window.location.reload()`, or `router.go(0)` was used.

The optional mixed direct-TestCase fixture was not added; Requirement global missing behavior and the required remove/re-add master flow were directly verified, while coverage remains backend-derived and its mixed semantics are retained by TRACE-B01 tests.

## Browser Console

PASS. After add/delete/re-add, drawer, and edit/preview/save, the browser log contained no application warning, error, or unhandled rejection.

## Responsive Smoke

PASS at 1280x720. Root client and scroll widths were both `1280`; there was no horizontal overflow. Edit and Publish actions were in viewport and did not overlap. Traceability remained rendered at a non-zero `1024.67 × 577.03` CSS-pixel area with established coverage content. The temporary viewport override was reset.

## Temporary SQLite Integrity

PASS. After closing the task-owned browser and stopping the exact API/Vite PIDs (`8064`, `2772`, `20580`), ports `5100` and `5190` had no listeners. The task-owned Microsoft.Data.Sqlite verifier returned:

```text
wal_checkpoint=0,70,70
integrity_check=ok
foreign_key_violations=0
```

The exact temporary root `C:\tmp\skh-trace-b02-fix-03` was then deleted, including the database/WAL/SHM, Data Protection keys, logs, verifier, and disposable administrator data. This task-only cleanup is not recoverable; no user process or data was removed.

## Repository DB Protection

PASS. `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` remained byte-for-byte fingerprint-identical before, during, and after isolated runtime verification. Repository WAL and SHM were absent at every probe.

| Fingerprint | Before | After | Result |
| --- | --- | --- | --- |
| Length | `724992` | `724992` | unchanged |
| LastWriteTimeUtc | `2026-08-25T11:46:34.6467938Z` | `2026-08-25T11:46:34.6467938Z` | unchanged |
| SHA-256 | `5008CF8E966B84070336A0ACFEE1E72CAC691134CE608295AAC50D411DD73E11` | `5008CF8E966B84070336A0ACFEE1E72CAC691134CE608295AAC50D411DD73E11` | unchanged |

No repository database, WAL/SHM, runtime artifact, backend source, schema, migration, API, route, relationship type, or frozen specification changed.

## New Gap Check

PASS. The known Medium stale-refresh regression is closed by this fix, and no new Blocker, High, or Medium gap was found. Existing deferred REV gaps are unchanged; the PHASE-REV gap register was not modified.

## TRACE-B02 Reverify Readiness

`TRACE-B02-REVERIFY-R03 READY: YES`.

The historical state remains `TRACE-B02 FINAL RESULT: FAIL` until R03 independently completes the focused final reverification.

## Final Result

TRACE-B02-FIX-03 PASS.
