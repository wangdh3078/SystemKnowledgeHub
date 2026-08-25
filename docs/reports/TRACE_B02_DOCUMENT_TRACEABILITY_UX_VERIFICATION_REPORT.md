# TRACE-B02 — Document Traceability UX Verification Report

## Result

TRACE-B02 PASS — superseded by TRACE-B02-REVERIFY-R01.

## Worktree Baseline

The task started from a clean `main` worktree at `2bb84a6` (`feat: add derived trace read foundation`). Existing user-owned development processes and the repository SQLite database were not changed.

## Normative Authority

The implementation follows `docs/design/TRACE_A01_TRACEABILITY_ARCHITECTURE_AND_CONTRACT_DECISION.md`, the frozen MVP UI/design sources, the UI component baseline, and TRACE-B01's established `GET /api/knowledge-documents/{id}/traceability` read contract.

## Implementation Summary

Added a feature-scoped, read-only traceability section and reusable trace document node. The detail page renders it between the document body and existing relationship area for Requirement, Specification, and TestCase roots. It uses the existing typed endpoint/decoder only, existing detail navigation, and the established relationship read drawer.

## Files Changed

- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/TraceDocumentNode.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/TraceabilitySection.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/TraceabilitySection.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts`

## UI Foundation Compliance

The section uses semantic section/list structure, existing status badges, design tokens, buttons with visible focus treatment, and ordinary empty/error/loading states. It introduces no global component framework or alternate data grid.

## TRACE-A01 Compliance

No graph traversal, relationship mutation, new endpoint, or raw relationship authoring was added. The UI consumes the bounded, discriminated backend projection and displays its warnings verbatim as bounded-read context.

## UI Placement

The section is immediately after rendered document content and before `关联对象`. It is available in normal detail mode only, preserving history mode and the existing drawer behavior.

## Requirement UX

Requirement roots show specification branches, each branch's tests, direct test definitions separately, structural coverage, and missing specification/test coverage.

## Specification UX

Specification roots show a neutral upstream Requirement state and linked TestCase coverage, including a controlled missing-test state.

## TestCase UX

TestCase roots show validation targets by Requirement/Specification context and preserve immediate upstream Requirement context from the server projection.

## Structural Coverage Presentation

Coverage is presented as a compact summary with explicit `已覆盖`/`缺少` language rather than inferred solely from relationship counts.

## Missing-Link Presentation

Empty specification, test, direct-test, and upstream contexts are explicit neutral states. They do not imply a broken request or write affordance.

## Trust Presentation

Evidence and human-confirmation counts are rendered independently from structural coverage in every document node and relationship context.

## Lifecycle Presentation

Nodes show the existing lifecycle label and status badge. Archived roots receive an archival notice and do not render a misleading structural trace section.

## Confirmation Coverage Presentation

Human-confirmation counts are visible beside evidence counts; the two concepts remain separate in text and layout.

## Relationship Context

Relationship cards display relationship type, relationship status, and evidence/human-confirmation counts supplied by the projection.

## Relationship Drawer Integration

Relationship inspection opens the existing read drawer through the existing overlay store; no new drawer, route, or authoring UI was created.

## Supersedes Lineage

Incoming and outgoing Supersedes lineage is rendered as a bounded compact group with the projection's truncation notice when applicable.

## Loading

The section uses the existing loading state while its authoritative projection is requested.

## Empty States

Document-type-specific neutral empty states distinguish absent valid coverage from endpoint failure.

## Error / Retry

Endpoint errors use the existing error state and retry the same authoritative request.

## Invalid Reference

The `reference_invalid` 422 condition has a controlled Chinese message and retry path.

## Cycle

Server cycle warnings are surfaced as bounded-read notices; the UI neither loops nor follows graph edges itself.

## Truncation

Projection truncation is surfaced as a warning, preserving the server as the authority for traversal bounds.

## Navigation

Document titles use existing named detail-route navigation. Root changes clear old detail data before the next authoritative load, avoiding stale trace content.

## Refresh / Authoritative Reload

The section exposes one refresh operation. The detail page invokes it after relationship, evidence, human-confirmation, document-status, content-save, lifecycle, restore, and current-reference refresh events.

## Async Race Protection

An AbortController plus monotonically increasing request sequence prevents a late root-A response from replacing newer root-B traceability data.

## Accessibility

Document and relationship actions are keyboard-operable buttons; status labels supplement color; list/group headings provide programmatic reading structure; compact layout remains usable without a graph canvas.

## Responsive / Golden Review

An isolated in-app browser smoke check at 1920×1080, 1440×900, 1366×768, and 1280×720 observed no root horizontal overflow. The Requirement missing-coverage layout, body-to-trace-to-relationship placement, and Chinese labels rendered correctly.

## R06 Regression

The trace section stays outside the R06 editor path. Content save keeps the authoritative trace section mounted and refreshes it; no editor behavior was changed.

## Revision Safety Smoke

History mode does not render the live trace section, avoiding an uncontracted historical projection. Restore events refresh the current authoritative trace view.

## Backend Regression

`dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~TraceabilityApiTests"` passed: 12/12.

## Frontend Type Check

`npm run type-check` passed.

## Frontend Build

`npm run build` passed. Vite reported only its existing large-chunk advisory; it was not a build failure.

## Affected Vitest

`npx vitest run src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts src/features/knowledge-documents/components/TraceabilitySection.spec.ts src/features/knowledge-documents/api/traceabilityContracts.spec.ts` passed: 3 files, 36 tests. The new component cases cover all three root discriminants, missing states, warnings, invalid references/retry, navigation/drawer integration, refresh, and race protection.

## ESLint

Scoped ESLint completed with no errors. The sole `vue/one-component-per-file` warning is pre-existing in `KnowledgeDocumentDetailView.spec.ts`'s Element Plus test double; the new exposed traceability test double is locally suppressed.

## Browser Runtime Scenario

Used a disposable SQLite database, a disposable local administrator, and task-owned API/Vite processes. A real Requirement was created and opened through the UI. Its live detail page showed the section after rendered body and before relationships, explicit missing Specification/Test coverage, independent zero trust counts, and neutral empty relationship states.

## Browser Console

The isolated browser session produced no console warnings or errors.

## Repository DB Protection

FAIL gate: the repository database was never intentionally opened for write by the TRACE-B02 implementation or its isolated runtime, but the final read-only fingerprint did not match the task-start baseline. Baseline: 724,992 bytes, `2026-08-24T15:38:12.9720638Z`, SHA-256 `854EEDAF15B04F5AFD549769D6045689E3C0240C3B41AE2571EA1711F5085CA6`. Final: 724,992 bytes, `2026-08-25T11:46:34.6467938Z`, SHA-256 `5008CF8E966B84070336A0ACFEE1E72CAC691134CE608295AAC50D411DD73E11`. An existing user-owned API process held a lock before verification; its activity cannot safely be distinguished from an accidental write without altering user data. The database was not reverted or otherwise modified.

## Existing REV Low Gaps

No existing PHASE-REV low-gap behavior was broadened or changed. The section remains read-only and does not alter revision/editor contracts.

## New Gap Check

No new product gap was identified. Browser smoke covered the deployed composition; discriminated structural variants and exceptional responses are covered by focused component tests against typed endpoint fixtures.

## Cleanup

Task-owned API and Vite processes were stopped, verification ports 5098/5188 were released (only transient `TIME_WAIT` records remained), the in-app browser tab/viewport override was closed/reset, and the disposable runtime directory was removed.

## TRACE-B03 Readiness

TRACE-B03 can build on a stable, read-only detail-page projection surface, reusable trace node, typed contract boundary, authoritative refresh seam, and focused interaction tests.

## Final Result

TRACE-B02 PASS — superseded by TRACE-B02-REVERIFY-R01.

## TRACE-B02-REVERIFY-R01

### Result

TRACE-B02-REVERIFY-R01 PASS.

### Scope and original result

This focused reverification made no product source, API, CSS, test, schema, migration, or runtime-configuration change. It preserves the initial TRACE-B02 FAIL evidence above: the original repository-database comparison was not isolated from a pre-existing process. This run establishes a new quiescent baseline and proves the isolated B02 verification path keeps the repository database unchanged.

### Repository DB writer gate

Before taking the new baseline, the only existing `dotnet` processes were the VS Code ProjectSystem BuildHost (PID 29164) and a pre-existing VSTest host (PID 22336). Neither was `SystemKnowledgeHub.Api`; no repository `system-knowledge-hub.db-wal` or `system-knowledge-hub.db-shm` file existed. Neither user-owned process was stopped or reconfigured.

### Isolated runtime

- Runtime database: `C:\\tmp\\skh-trace-b02-reverify-r01\\trace-b02-reverify.db`
- API: PID 29444, `http://127.0.0.1:5098`
- Vite: root PID 30736, `http://127.0.0.1:5188`, explicit proxy `http://127.0.0.1:5098`
- Data Protection: `C:\\tmp\\skh-trace-b02-reverify-r01\\keys`
- Runtime confirmation: `GET /api/auth/options` returned `localLoginEnabled=true`, `oidcLoginEnabled=false`; the temporary DB, WAL/SHM, migrations, seed data, and disposable local Administrator were created under the temporary root only.

### Repository DB protection

| Fingerprint | Before | After | Result |
| --- | --- | --- | --- |
| Length | 724,992 | 724,992 | unchanged |
| LastWriteTimeUtc | 2026-08-25T11:46:34.6467938Z | 2026-08-25T11:46:34.6467938Z | unchanged |
| SHA-256 | 5008CF8E966B84070336A0ACFEE1E72CAC691134CE608295AAC50D411DD73E11 | 5008CF8E966B84070336A0ACFEE1E72CAC691134CE608295AAC50D411DD73E11 | unchanged |
| WAL / SHM | absent | absent | unchanged |

Repository DB Protection: PASS.

### Focused browser smoke

Using the isolated browser tab and disposable administrator, the following passed:

- local login;
- creation/opening of a Requirement with the `可追溯性` section after rendered body and before `关联对象`;
- explicit missing Specification/Test Definition state;
- R06 minimum regression: edit mode shows raw Markdown source and toolbar, page Save succeeds, read mode returns with Traceability still available;
- creation of an isolated Specification and `Requirement --SpecifiedBy--> Specification` through existing relationship authoring;
- refreshed Requirement linked state (`规格说明覆盖：已建立`), branch-level `缺少测试定义`, existing relationship drawer, and navigation to the related Specification;
- Specification upstream Requirement context and missing Test Definition state.

The browser console contained no warning, error, or unhandled rejection.

### Automated reverification

- `npm run type-check`: PASS.
- `npm run build`: PASS (only the pre-existing Vite large-chunk advisory).
- Affected Vitest: PASS, 3 files / 36 tests.
- `TraceabilityApiTests`: PASS, 12/12.

### Cleanup

The task-owned browser tab was closed. Task-owned API/Vite PID tree `29444, 30736, 27616, 10468, 13768, 1320, 35044` was stopped by exact PID; ports 5098/5188 had no listeners. The temporary SQLite/WAL/SHM, Data Protection keys, disposable administrator, and logs were deleted. No user-owned process or repository database companion file was deleted.

### Supersession

```text
TRACE-B02 initial verification: FAIL
Reason: repository DB fingerprint attribution was not isolated.

TRACE-B02-REVERIFY-R01: PASS

Final TRACE-B02 Result: PASS
TRACE-B03 READY: YES
```
