# DELETE-B04 — Delete UX + Historical Tombstones Verification Report

## Result

**PASS**. DELETE-B04 exposes the eight approved root delete actions only when the authoritative read contract returns `canDelete=true`, uses one shared destructive confirmation/error experience, and renders deleted historical targets as explicit non-navigable tombstones. No recovery, recycle-bin, hard-delete, bulk-delete, cascade-delete, or automatic dependency cleanup surface was added.

## Worktree Baseline

- Branch: `main`
- Starting HEAD: `807cf564154686daa3fa2c0954bb1cefcca92f53`
- DELETE-B01, DELETE-B02, and DELETE-B03 were already committed and pushed.
- The repository database was protected by filesystem metadata/hash comparison only.
- A concurrent `AGENTS.md` working-tree change is unrelated to DELETE-B04 and remains unstaged.

## Normative Authority

This slice follows the frozen MVP UI, design, domain, database, application-use-case, API-contract, and solution-structure specifications; `DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md`; the verified DELETE-B01/B02/B03 implementation state; and the DELETE-B04 task specification. Frozen sources and Golden UI assets were not modified.

## Scope

Implemented only:

- authoritative `canDelete` projection for the eight approved roots;
- typed frontend delete requests using the existing B02 endpoints and opaque concurrency tokens;
- delete actions, confirmation, structured error handling, and post-delete navigation/refresh;
- one shared historical-target tombstone renderer;
- deleted-target behavior for Evidence/HumanConfirmation, KnowledgeDocument revision history, Closed UnknownItem context, and Applied KnowledgeUpdate context;
- focused contract, component, integration, full regression, and isolated browser/runtime verification.

## Delete Action Matrix

| Root | UX surface | Successful result | Result |
|---|---|---|---|
| System | System detail header | Return to Systems list | PASS |
| BusinessFunction | Business Function detail header | Return to Business Functions list | PASS |
| DatabaseSource | Database Objects source context | Refresh database browse context | PASS |
| DatabaseObject | Database Object detail header | Return to Database Objects list | PASS |
| DatabaseColumn | Column detail drawer | Close/refresh owning database detail | PASS |
| BusinessRule | Business Rule detail header | Return to Business Functions context | PASS |
| Integration | Integration detail header | Return to Systems context | PASS |
| KnowledgeDocument | Knowledge Document detail header | Return to Knowledge Documents list | PASS |

## Authorization / Capability UX

The backend resolves the current canonical user and applies the frozen `SoftDeleteAuthorization` rule to each root's canonical `CreatedByUserId`. The frontend consumes the projected boolean; it does not reconstruct ownership from display-name snapshots or roles.

## Administrator UX

Administrators receive `canDelete=true` for active roots. Browser verification showed the KnowledgeDocument delete action and the System dependency-block flow.

## Editor Own UX

An Editor receives `canDelete=true` only when their canonical user ID equals `CreatedByUserId`. Browser and API integration verification passed.

## Editor Other UX

An Editor viewing another user's root receives `canDelete=false`; the action is absent rather than disabled. Browser and API integration verification passed.

## Viewer UX

Viewer reads remain available, but delete actions are absent. Browser and API integration verification passed.

Legacy/null creator ownership is fail-closed for Editors and remains deletable by Administrators; this is covered by the backend integration test.

## Delete Confirmation

All roots use the shared `delete-root` dialog. It names the root, explains that current list/search/detail views will hide it, states that no page recovery exists, provides Cancel and destructive Confirm actions, prevents double submit, and keeps server errors in the same dialog when continued correction is possible.

## Successful Delete Flow

The authorized browser flow created temporary KnowledgeDocument `id=2`, opened its detail, opened the shared confirmation, and clicked `确认删除`. The application returned to `/knowledge-documents`; the current list showed `0` documents. SQLite then verified:

- `is_deleted=1`;
- `deleted_by_display_name=B04 Administrator`;
- `deleted_by_user_id=1`;
- `version=2`;
- FTS rows for document 2: `0`;
- active KnowledgeDocument count: `0`.

## 400 UX

| HTTP | UX |
|---|---|
| 400 | Keep dialog open and show the validation/problem message without inventing success. |
| 403 | Close dialog, show permission feedback, and refresh authoritative state. |
| 404 | Close dialog, show unavailable feedback, and leave the stale detail. |
| 409 | Close dialog, show stale-data feedback, and refresh authoritative state. |
| 422 | Keep dialog open and render structured dependency groups/counts/names. |

The shared component tests cover the 400 fallback and the required callback behavior for 403/404/409/422.

## 403 UX

Permission changes between read and write do not leave a misleading actionable dialog. The dialog closes, permission feedback is shown, and the surface reloads authoritative capability.

## 404 UX

Already-deleted or missing roots close the stale confirmation, show unavailable feedback, and navigate away from the obsolete detail context.

## 409 UX

Stale opaque concurrency tokens close the dialog, show a conflict message, and refresh the root rather than retrying with an invented token.

## 422 Dependency Blocking UX

The dialog remains open and renders structured blocker groups without HTML injection. Browser verification of System `MES` displayed `技术标签 2`, `业务功能 5`, and `数据库来源 1`; no root or dependency was deleted.

## System Delete UX

The System detail owns its action. It passes the current opaque token, navigates to the Systems list after 204, and renders B02 dependency blockers from the 422 response.

## DatabaseSource Delete UX

DatabaseSource deletion is owned by the source context on the Database Objects screen. Success refreshes the browse context; no child cleanup or source-slot inference is performed in the client.

## BusinessFunction Delete UX

The Business Function detail owns the action, sends its current token, and returns to the feature list after success.

## DatabaseObject Delete UX

The Database Object detail owns the action and returns to the database list after success. Column state is not silently changed.

## DatabaseColumn Delete UX

The existing column drawer exposes the action only for `canDelete=true`; success closes/refreshes the owning detail through the existing change notification boundary.

## BusinessRule Delete UX

The Business Rule detail owns the action and returns to its Business Function context after success.

## Integration Delete UX

The Integration detail owns the action and returns to the Systems context after success.

## KnowledgeDocument Delete UX

The KnowledgeDocument detail owns the action. Successful deletion returns to the current list; revision history remains reachable only through the explicit historical entry mode and never exposes restore for a deleted owner.

## Historical Tombstone Component

`HistoricalTargetLabel.vue` is the shared renderer. Active navigable targets use `RouterLink`; deleted targets render plain text with line-through styling plus an explicit `已删除` label. Deleted targets never receive a route.

## Evidence Tombstone

Evidence remains historically readable after subject deletion. The subject uses the shared tombstone and correction/add-HumanConfirmation actions are hidden for the deleted subject.

## HumanConfirmation Tombstone

HumanConfirmation remains part of retained Evidence history and inherits the same deleted-subject tombstone and mutation boundary.

## Revision Tombstone

Revision list/detail content remains readable when the KnowledgeDocument current detail returns 404. The owner is a non-link tombstone and Restore is absent.

## Closed UnknownItem Tombstone

Closed UnknownItem historical context renders deleted System/primary/proposed targets as tombstones. Deleted targets are filtered from editable choices and cannot reopen an apply path.

## Applied KnowledgeUpdate Tombstone

Applied-update historical target context uses the same tombstone rule. A deleted target is retained as history but is non-navigable and non-actionable.

| Historical surface | Read retained | Deleted marker | Link removed | Mutation removed | Result |
|---|---:|---:|---:|---:|---|
| Evidence | Yes | Yes | Yes | Yes | PASS |
| HumanConfirmation | Yes | Yes | Yes | Yes | PASS |
| KnowledgeDocument Revision | Yes | Yes | Yes | Restore hidden | PASS |
| Closed UnknownItem | Yes | Yes | Yes | Apply hidden | PASS |
| Applied KnowledgeUpdate | Yes | Yes | Yes | Apply hidden | PASS |

## Navigation Safety

Current projections continue to exclude deleted roots. Historical deleted targets are plain text, not fake links; current routes are never inferred from a retained historical ID. KnowledgeDocument history can load through `?view=history` even when current detail is 404, and returning from a deleted owner goes to the list.

## Accessibility

The shared dialog has a named region/heading, explicit Cancel and Confirm controls, danger semantics, loading state, and text error output. Tombstones convey deletion through visible text in addition to line-through styling. Icon-only/close controls retain accessible names.

## Responsive

Browser verification passed at 1440×900 and 1280×720. The 520px dialog remained fully within the viewport and action rows wrapped without horizontal loss.

## Overlay / Dialog Safety

Delete uses the existing single `DialogHost` ownership boundary. No second overlay manager, nested modal stack, or page-level unmanaged Teleport was introduced. Cancel closes without an API call; pending confirm cannot be submitted twice.

## Files Changed

- Backend: eight read projections/models, `SoftDeleteCapabilityResolver`, DI registration, and one API integration test.
- Frontend API: shared client 204/delete-body support plus eight typed delete methods/contracts.
- Frontend UX: eight owning surfaces, shared delete dialog, shared historical tombstone, and affected historical-read surfaces.
- Tests: client, capability, dialog, tombstone, Evidence, revision, KnowledgeDocument detail, database contract/detail, and UnknownItem regressions.
- Documentation: this report and `docs/DOCUMENT_INDEX.md`.

## Backend Build

`dotnet build SystemKnowledgeHub.sln --no-restore -c Release`: **PASS**, 0 warnings, 0 errors.

## Backend Tests

- `DeleteCapabilityProjectionApiTests`: **1/1 PASS**; covers Administrator, Editor owner, Editor other, legacy unknown owner, and Viewer across all eight root read contracts.
- Deterministic full suite with xUnit collection parallelism disabled: **168/168 PASS**, 0 skipped, 33 seconds.
- Verification-only testhost processes exited and no test ports were opened.

## Frontend Type Check

`npm run type-check`: **PASS**.

## Frontend Build

`npm run build`: **PASS**. The existing Vite chunk-size advisory remains unchanged.

## Affected Vitest

Ten affected files covering the shared client, database contracts/detail, delete dialog/error handling, shared tombstone, Evidence, KnowledgeDocument contracts/detail/revisions, and UnknownItem passed **51/51**. The complete suite passed **303/304**; its sole failure is the pre-existing unrelated `AppShell.spec.ts` `关系与缺口` navigation-copy expectation.

## ESLint

All changed frontend source/test files: **0 errors**. ESLint reports the pre-existing multi-component warning in `KnowledgeDocumentDetailView.spec.ts`; CSS files are ignored because this repository has no matching CSS ESLint configuration. Full-project lint still has the pre-existing unrelated `CreateIntegrationDialog.vue` unused `props` error.

## Browser Runtime

Runtime used only task-owned ports `5193/5194`, an isolated local Administrator, isolated Data Protection keys, and a temporary SQLite database under `.tmp`. Login, create, successful delete, dependency-block, historical-read, role, responsive, and navigation flows passed. The browser tab was closed, both services were stopped, LISTENING sockets were absent, and the exact temporary runtime directory was removed.

## Browser Role Matrix

| Actor/state | Delete action | Result |
|---|---:|---|
| Administrator | Visible | PASS |
| Editor owner | Visible | PASS |
| Editor other | Hidden | PASS |
| Viewer | Hidden | PASS |
| Legacy/unknown owner | Editor false, Administrator true in API integration | PASS |

## Browser Dependency Block Flow

Deleting System `MES` produced the expected 422 dependency presentation with real group names/counts. The dialog stayed open and storage remained unchanged.

## Browser Historical Flow

A soft-deleted KnowledgeDocument remained revision-readable at `?view=history`; both owner occurrences showed explicit line-through tombstones, link count was zero, Restore count was zero, and historical revision content remained readable.

## Browser Console

Captured browser runtime console errors: **0**. The successful delete flow produced no application error state.

## SQLite Integrity

Final temporary database checks after service shutdown:

- `PRAGMA wal_checkpoint(TRUNCATE)`: busy 0;
- `PRAGMA integrity_check`: `ok`;
- `PRAGMA foreign_key_check`: 0 rows;
- deleted document removed from FTS and absent from current projection.

## Repository DB Protection

The repository database was never opened or connected to. Start/end filesystem evidence is identical:

- length: `897024`;
- last write UTC: `2026-08-27T15:46:01.9864232Z`;
- SHA-256: `7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483`;
- repository WAL/SHM: absent before and after.

## Existing Baseline Deviations

- Full Vitest: unchanged unrelated `AppShell.spec.ts` stale navigation-copy expectation; B04 result is 303/304.
- Full ESLint: unchanged unrelated `CreateIntegrationDialog.vue` unused `props` error.
- Changed-file ESLint: one pre-existing test-file structure warning and ignored CSS notices, with 0 errors.
- Vite production build: existing chunk-size advisory.
- `REV-GAP-011`: default parallel backend collection execution remains Deferred; the approved deterministic collection-serial gate passed 168/168.

## New Gap Check

No new DELETE-B04 Blocker, High, or Medium gap was found. The implementation does not broaden recovery, cascading, hard-delete, bulk-delete, blocker precomputation, or generic CRUD/framework scope.

## DELETE-VERIFY Readiness

`DELETE-VERIFY READY: YES`

All eight UI surfaces, authorization states, confirmation/error behaviors, successful delete, dependency block flow, historical tombstones, navigation safety, accessibility, responsive/overlay checks, backend gates, frontend gates, browser runtime, SQLite integrity, cleanup, and repository database protection passed.

## Final Result

**DELETE-B04 PASS**
