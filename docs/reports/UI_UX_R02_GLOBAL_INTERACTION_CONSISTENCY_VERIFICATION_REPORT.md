# UI-UX-R02 — Global Interaction Consistency + Human Confirmation UX

## Result

```text
UI-UX-R02 PARTIAL
```

Nine interaction-consistency items are implemented and verified. The requested HumanConfirmation relaxation is only partially applicable: the canonical-user and helper-copy improvements are complete, but making `SupportReason` optional when Evidence exists and replacing factual `ConfirmedAt` with a server-generated event time conflict with the frozen C25, database, API, and revision contracts. Those conflicting semantic changes were stopped; no frozen source was modified.

## Worktree Baseline

- Branch: `main`
- Starting HEAD: `13d859a`
- Starting worktree: clean
- No frozen specification or frozen task definition was modified.

## Normative Authority

The implementation and verification used:

- `AGENTS.md`
- `docs/DOCUMENT_INDEX.md`
- `docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md`
- `docs/specifications/System_Knowledge_Hub_MVP_Design_Baseline.md`
- `docs/specifications/System_Knowledge_Hub_MVP_Application_Use_Case_Model.md`
- `docs/specifications/System_Knowledge_Hub_MVP_API_Contract.md`
- `docs/specifications/System_Knowledge_Hub_MVP_Database_Model.md`
- `docs/design/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md`
- the current `UI-UX-R02` task definition, implementation, and focused tests

## Issues Addressed

| # | Area | Final implementation | Result |
| --- | --- | --- | --- |
| 1 | Global Create | Removed the context-dependent Evidence and UnknownItem disabled cards. The six existing independent create flows remain. | PASS |
| 2 | Current-user popover | Added outside-click dismissal while retaining trigger toggle, close action, and `Esc`; unrelated dialogs/drawers are not closed. | PASS |
| 3 | Database breadcrumb | Database list, System detail, and database-source-filtered list are real buttons; the current DatabaseObject remains text. | PASS |
| 4 | Dashboard metrics | Metrics with real destinations remain buttons. Integration and BusinessRule, which have no independent list page, are static and no longer fall back to Global Search. | PASS |
| 5 | Global Search | Click and `Ctrl + K` both focus the search input after the Element Plus `opened` lifecycle; close restores the connected invoker. | PASS |
| 6 | Close position | Evidence, Relationship, HumanConfirmation, KnowledgeStatus, and User Edit use one visible, accessible close control at the header top-right. | PASS |
| 7 | HumanConfirmation | Canonical actor/help copy and non-duplicative basis guidance improved; frozen required/factual fields remain contract-compatible. The requested conditional relaxation was not applied. | PARTIAL — frozen conflict |
| 8 | LoginIdentity | Title is `登录身份映射（OIDC / SSO）`; helper copy separates LoginIdentity from knowledge/permission roles and explains configured Provider plus stable Subject/sub. | PASS |
| 9 | Overlay scroll | Shared hosts capture the application reading position and restore it on the current main container, including when overlay rendering replaces that DOM node. No global `window.scrollTo` workaround is used. | PASS |
| 10 | KnowledgeStatus note | Removed the forward-progression `修改说明` input and local state. Forward requests submit `reason: null`; the canonical reason field remains available for rollback, where the frozen contract requires it. | PASS |

## HumanConfirmation Decision

### Final required fields

- `ConfirmationStatement`: required; it is the actual confirmation conclusion.
- `SupportReason`: required by frozen C25 and database validation. The UI now calls it `本次确认依据` and tells users not to copy existing Evidence text.
- `ConfirmationMethod`: retained because the current contract exposes multiple real methods; the normal in-product value is preselected.
- `ConfirmedAt`: retained as a factual confirmation time and prefilled with the form-open local time. It is editable only when the fact occurred at another time.
- Knowledge role selection is conditionally required only when the current user has multiple active knowledge roles; a sole role is resolved automatically, and no role does not block confirmation.

### Automatic information

- Confirmer identity and historical person snapshot come from the server-trusted Current User.
- A sole knowledge role is resolved automatically by the existing server boundary.
- Confirmation method and local factual time receive meaningful defaults; users do not re-enter canonical actor data.
- KnowledgeDocument revision coverage continues to use the current revision snapshot.

### Optional fields

- `SourceNote` / `来源说明（可选）` remains optional.
- Knowledge-role input is absent unless a real choice between multiple active roles is required.

### Contract compatibility and frozen conflict

The frozen application use case requires non-empty `SupportReason`; the frozen database column is non-null; the API contract accepts client factual confirmation time; and REV-A01 explicitly preserves factual `ConfirmedAt`. Therefore:

- `SupportReason` was not made conditionally optional when Evidence exists.
- `ConfirmedAt` was not replaced with an operation timestamp generated by the server.
- Backend, frontend validation, KnowledgeStatus gates, and revision coverage remain aligned.
- Saving HumanConfirmation still creates Evidence and never advances KnowledgeStatus automatically.

This conflict is the reason the overall result is `PARTIAL`. Resolving it requires a separately approved amendment to the frozen C25/database/API/revision contract; it is not safe UI-only work.

## LoginIdentity UX Decision

The capability remains because it maps an external stable login identity to a System Knowledge Hub user. It is not a KnowledgeRole and is not an authorization role. The final UI:

- uses `登录身份映射（OIDC / SSO）` as the human-facing title;
- keeps `LoginIdentity` as the supporting technical name;
- tells administrators to use a configured identity-provider identifier;
- tells administrators to copy the stable Subject/sub supplied by that provider;
- explicitly rejects guessing a mapping from name or email.

No OIDC wire contract or automatic mapping behavior changed.

## KnowledgeStatus Note Decision

The model stores only the latest status-change metadata/reason and explicitly does not define a complete status history, activity stream, or audit table. There is no formal history read boundary where a forward-progression note could be reliably viewed.

The unusable forward `修改说明（可选）` input was therefore removed. The existing request/domain reason remains intact because explicit rollback requires a non-empty reason under the frozen contract. No new audit framework, table, route, or page was introduced.

## Files Changed

Frontend implementation:

- overlay store and Global Search focus lifecycle
- `AppTopBar` popover dismissal
- Global Create chooser
- Dashboard metric semantics and styling
- DatabaseObject breadcrumb behavior and styling
- HumanConfirmation copy and Evidence-family close layout
- KnowledgeStatus progression dialog and styling
- LoginIdentity mapping copy
- shared `DialogHost` / `DrawerHost` scroll preservation
- shared application search cursor styling

Focused frontend tests:

- `AppTopBar.spec.ts`
- `CreateKnowledgeObjectChooser.spec.ts`
- `DatabaseObjectDetailView.spec.ts`
- `AddHumanConfirmationDrawer.spec.ts`
- `DashboardView.spec.ts`
- `KnowledgeStatusDialogContent.spec.ts`
- `useGlobalSearch.focus.spec.ts`
- `LoginIdentityManagementPanel.copy.spec.ts`
- `overlayScrollPreservation.spec.ts`

Documentation:

- this verification report
- `docs/DOCUMENT_INDEX.md`

No backend source, migration, schema, frozen source, production configuration, or runtime database belongs to the task diff.

## Frontend Verification

- Focused Vitest: PASS — 9 files, 20/20 tests.
- `npm run type-check`: PASS.
- `npm run build`: PASS — 3,414 modules transformed.
- Vite emitted the existing non-failing chunk-size advisory; no package or bundling change was introduced for it.

## Backend Verification

Not applicable. The frozen HumanConfirmation and KnowledgeStatus contracts were deliberately preserved, and no backend source or contract changed. Per the risk-based repository gate, no unrelated backend build/test suite was run for this frontend-only correction.

## Browser Verification

PASS for all non-conflicting acceptance paths at both requested sizes: 1440×900 and 1280×720.

Verified in the real application:

- Global Create shows exactly the six supported independent types and no Evidence/UnknownItem cards.
- Current-user popover closes by outside click, trigger toggle, close action, and `Esc`.
- Database breadcrumb destinations resolve to `/database-objects`, `/systems/12`, and `/database-objects?systemId=12&databaseSourceId=9`; the current object is plain text.
- Dashboard Integration and BusinessRule metrics are static when no real list destination exists; valid metrics retain navigation.
- Global Search input receives focus from both top-bar click and `Ctrl + K`; `Esc` restores the top-bar invoker.
- Evidence, Relationship, HumanConfirmation, KnowledgeStatus, and User Edit close controls are visible at the right side of their headers and have accessible names.
- HumanConfirmation displays canonical current-user information, factual-time guidance, required conclusion/basis, optional source note, and the unchanged explicit KnowledgeStatus boundary.
- LoginIdentity explanatory copy distinguishes login mapping, configured Provider, stable Subject/sub, and knowledge/permission roles.
- KnowledgeStatus progression contains no `修改说明` input.
- No horizontal clipping or new overlay stacking issue was observed at either requested size.
- Final controlled browser console check: 0 warnings and 0 errors.

Long-page scroll proof:

```text
1440×900: before 130.1282 → opened 130.1282 → closed 130.1282
1280×720: before 303.2051 → opened 303.2051 → closed 303.2051
```

The same shared host boundary was spot-checked with Evidence, Relationship, and KnowledgeStatus overlays; focus trap, `Esc`, and keyboard navigation remained active.

## Persistent Data Safety and Cleanup

Browser/runtime verification used a task-owned temporary SQLite database, temporary Data Protection directory, and disposable local administrator. Temporary Evidence and KnowledgeDocument records existed only in that database.

The task-owned API process was stopped, port `5090` was released, the browser tab was closed, the viewport override was reset, and `.tmp/ui-ux-r02` was deleted. A pre-existing user-owned Vite process already listening on `5173` was reused by the browser and deliberately left running.

Repository database before and after verification:

```text
Length:           897024
LastWriteTimeUtc: 2026-08-28T14:04:07.3581128Z
SHA256:           D3E04257042DD7E93FE3D11AFE2A1C75B9B3CAB8FCDCBA1D39D739E7E975BE5C
WAL:              absent
SHM:              absent
```

The repository database remained unchanged.

## Existing Gaps

- The existing Vite chunk-size advisory remains outside this task.
- No existing product Gap ID was found that owns the HumanConfirmation frozen-contract conflict.

## New Gaps

None. The HumanConfirmation limitation is a direct conflict between this task's desired semantics and frozen authoritative sources, not a newly discovered implementation defect. It is recorded above and requires an approved contract amendment before implementation.

## Final Result

```text
PARTIAL
```

All safe, in-scope interaction corrections passed focused automated and real-browser verification. The two conflicting HumanConfirmation semantic changes were not implemented, the current security/status/revision boundaries remain fail-closed and compatible, and no ATTACH-B01 or adjacent feature work was started.
