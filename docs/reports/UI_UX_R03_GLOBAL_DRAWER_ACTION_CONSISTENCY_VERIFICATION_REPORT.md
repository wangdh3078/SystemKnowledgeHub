# UI-UX-R03 Global Drawer + Action Button Consistency Verification Report

## Result

```text
UI-UX-R03 PASS
```

The shared right-side Drawer now uses overlay semantics without changing the underlying page width. Read-only and authoring close behavior, Drawer headers and widths, page-level primary actions, Evidence/HumanConfirmation actions, and shared focus-visible states are consistent across the applicable frontend surfaces. Focused tests, type checking, lint, production build, and isolated real-browser verification passed with no new browser console warning or error.

No backend, API, persistence, authorization, Evidence, HumanConfirmation, KnowledgeStatus, Attachment-domain, or Database-domain contract was changed.

## Drawer Inventory

The frontend has one native Element Plus Drawer boundary: `layouts/DrawerHost.vue`. Feature content is either rendered directly by the host or teleported into its feature mount point; no feature maintains a second native `<el-drawer>`.

| Feature / kind | Composition | Width | Interaction class | Close policy |
| --- | --- | --- | --- | --- |
| User Management | Teleported feature content | Large | Create / edit | Clean close; dirty close guarded |
| Attachment Administration detail | Teleported feature content | Standard | Read-only | Mask, Esc, or close control |
| Evidence / investigation evidence | DrawerHost content | Large | Create | Clean close; dirty close guarded |
| Evidence detail | DrawerHost content | Standard | Read-only | Mask, Esc, or close control |
| HumanConfirmation | DrawerHost content | Large | Create | Clean close; dirty close guarded |
| Relationship add | DrawerHost content | Large | Create | Clean close; dirty close guarded |
| Relationship detail | DrawerHost content | Standard | Read-only | Mask, Esc, or close control |
| BusinessRule edit / preview | DrawerHost content | Large / Standard | Edit / read-only | Edit is guarded; preview closes directly |
| Integration edit / preview | DrawerHost content | Large / Standard | Edit / read-only | Edit is guarded; preview closes directly |
| Database Column | DrawerHost content | Standard | Read with bounded inline edit | Inline edit and Drawer close use the shared guard |
| Database Object edit | DrawerHost content | Large | Edit | Clean close; dirty close guarded |
| Foundation / fallback read detail | DrawerHost content | Standard | Read-only | Mask, Esc, or close control |

Before this task the host used `modal=false`, `lock-scroll=false`, and `AppShell` applied a Drawer-open right padding that reduced the main content width. Dirty confirmation existed only for the KnowledgeDocument editor and was not a Drawer boundary.

## Drawer Overlay Decision

- `DrawerHost` now uses the Element Plus modal overlay and retains `lock-scroll=false` so the existing scroll-preservation boundary remains authoritative.
- The `app-shell--drawer-open` layout state and all corresponding main-content, top-bar, and right-rail padding adjustments were removed.
- The shared mask is `rgba(23, 32, 51, 0.24)`: the underlying context remains visible while clearly inactive.
- Drawer content owns its vertical scrolling with overscroll containment; the document/body do not gain horizontal overflow.
- Existing Dialog/confirmation layering remains above the Drawer. The dirty confirmation uses the existing Element Plus MessageBox boundary rather than a new overlay framework.

## Outside-click Policy

- Read-only or clean Drawers may close through mask click, Esc, or the accessible close control.
- Any captured `input` or `change` inside the shared Drawer content marks the current Drawer dirty.
- Mask click, Esc, the header close control, and explicit cancel controls all resolve through the same minimal `confirmDrawerDiscard()` boundary.
- The confirmation copy remains the existing product language: `尚有未保存的修改，确认放弃？`, with `继续编辑` and `放弃修改` actions.
- Cancelling the confirmation keeps the Drawer open and preserves the entered value. Successful save and other intentional programmatic navigation close the Drawer without a redundant discard prompt.
- The guard is deliberately conservative for this slice: once an authoring interaction changes a field, the Drawer remains dirty until save/reset/close. No generic form framework or per-feature duplicate dirty implementation was introduced.

## Drawer Header / Width

- Shared `skh-drawer-header` spacing, divider, title/context placement, and top-right close action were applied across User, Attachment, Evidence, HumanConfirmation, Relationship, BusinessRule, Integration, Database Column, and Database Object Drawers.
- All close controls have feature-appropriate accessible names and inherit the shared focus-visible treatment.
- Width tokens are centralized as `--drawer-width-standard` and `--drawer-width-large`:
  - desktop: 500 px / 600 px;
  - at or below 1440 px: 460 px / 560 px, with viewport caps.
- The host selects the size by Drawer kind; features no longer need temporary page-width compensation.
- Closing restores the trigger focus with `preventScroll`, including after Element Plus completes mask-close autofocus, and preserves the captured page scroll position.

## Primary Action Style

`skh-page-primary-action` is the shared page-level action class. It provides one solid primary treatment with a 36 px height, 7 px radius, 16 px horizontal padding, and the shared Plus icon scale/gap.

Applied surfaces include:

- App top-bar global add entry;
- User Management;
- Unknown Items / 待确认事项;
- Knowledge Content;
- Systems;
- Business Functions;
- Database Objects;
- Dashboard empty-state primary creation entry.

The change only normalizes presentation; existing role and state capability checks remain unchanged.

## Evidence / HumanConfirmation Action Style

- `skh-section-action` owns the shared compact section-action dimensions and icon/text spacing.
- `skh-evidence-action` is the stronger solid primary action.
- `skh-human-confirmation-action` is the secondary outline action.
- Paired actions render Evidence before HumanConfirmation and align in the section action area.
- The shared classes are applied across Business Function, Database Object, Database Column, BusinessRule, Integration, KnowledgeDocument, Relationship/Evidence detail, and the common KnowledgeStatus progression surface used by System and the other knowledge objects.

## Focus-visible Fix

Shared Element Plus overrides now keep primary, outline/plain, text/link, and danger button text readable in default, hover, active, focus-visible, and disabled states.

- Focus-visible adds a distinct outline with offset; it does not rely on color alone.
- Primary focus retains white text on the darker primary background.
- Outline/HumanConfirmation focus retains dark primary text on a light background.
- Disabled primary text remains readable while the control remains visibly unavailable.
- Danger plain focus remains red and is not overwritten by the generic plain-primary focus rule.

## Files Changed

### Shared interaction and layout

- `src/SystemKnowledgeHub.Web/src/layouts/DrawerHost.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/drawerDirtyState.ts`
- `src/SystemKnowledgeHub.Web/src/app/stores/overlays.ts`
- `src/SystemKnowledgeHub.Web/src/layouts/AppShell.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/AppTopBar.vue`

### Shared styles

- `src/SystemKnowledgeHub.Web/src/styles/tokens.css`
- `src/SystemKnowledgeHub.Web/src/styles/app.css`
- `src/SystemKnowledgeHub.Web/src/styles/ui-foundation.css`
- `src/SystemKnowledgeHub.Web/src/styles/element-plus-overrides.css`

### Feature surfaces

- User Management and Attachment Administration Drawer components/pages;
- Evidence, HumanConfirmation, Relationship, BusinessRule, Integration, Database Column, and Database Object Drawer components;
- Dashboard, Systems, Business Functions, Database Objects, Knowledge Documents, Unknown Items, and User Management page actions;
- Business Function, BusinessRule, Integration, Database Object, KnowledgeDocument, and KnowledgeStatus evidence/confirmation surfaces.

### Tests and documentation

- `src/SystemKnowledgeHub.Web/src/layouts/DrawerHost.spec.ts`
- `src/SystemKnowledgeHub.Web/src/layouts/drawerDirtyState.spec.ts`
- `src/SystemKnowledgeHub.Web/src/styles/uiConsistency.spec.ts`
- affected Overlay, AppShell, and HumanConfirmation focused tests
- this report and `docs/DOCUMENT_INDEX.md`

## Focused Tests

| Check | Result |
| --- | --- |
| DrawerHost / dirty guard / focus / modal / width | PASS |
| Overlay store and scroll preservation | PASS |
| AppShell and HumanConfirmation regression | PASS |
| Shared action/focus/danger class regression | PASS |
| Focused Vitest aggregate | PASS — 7 files, 23 tests |
| `npm run type-check` | PASS |
| `npm run lint` | PASS — 0 issues |
| `npm run build` | PASS |

The production build continues to emit the existing non-failing Vite chunk-size advisory. UI-UX-R03 does not change bundle architecture.

## Browser Verification

Runtime isolation:

- task-owned SQLite database;
- task-owned Data Protection directory;
- task-owned Attachment StorageRoot;
- isolated API/Vite ports `18561` / `18562`;
- task-local Administrator account and task-local attachment fixture.

### 1440 × 900

- User Edit: main width remained `1244.39 px`; Large Drawer width was `599.99 px`; body/document width remained 1440 px.
- Database Object Evidence/HumanConfirmation: the main column remained `888.39 px` with the existing right rail; Large Drawer width was `599.99 px`.
- Attachment Detail: main width remained `1244.39 px`; Standard Drawer width was `500.00 px`.
- Mask opacity measured `rgba(23, 32, 51, 0.24)` and Drawer body overflow was `auto`.
- Dirty User Edit mask click and Esc both opened the discard confirmation. `继续编辑` retained `R03 未保存验证`; the isolated record was then saved so no unconfirmed discard action was required.
- Read-only Drawer mask/Esc close, clean Evidence Esc close, and trigger-focus restoration passed.
- Page-level Knowledge Content and Database Object add actions measured the same 36 px primary style.
- Database Object Evidence and HumanConfirmation actions had the expected solid/outline hierarchy and readable keyboard focus.

### 1280 × 720

- Attachment Detail: main width remained `1084.13 px`; responsive Standard Drawer width was `460.00 px`; Drawer body scrolled independently.
- User Edit: main width remained `1084.13 px`; responsive Large Drawer width was `560.00 px`; body/document width remained 1280 px.
- Clean mask close restored focus to the `编辑` trigger; Tab, Shift+Tab, and Esc kept focus within the Drawer and returned it on close.
- User, Unknown Item, Knowledge Content, and Database Object primary actions used the same class, dimensions, padding, radius, color, and Plus icon scale.
- Database Object, KnowledgeDocument, and BusinessFunction surfaces used the shared Evidence/HumanConfirmation classes and readable colors; paired DatabaseObject/KnowledgeDocument actions retained Evidence before HumanConfirmation.
- No page-level horizontal overflow was observed.

Attachment Detail was verified against a real task-local TXT upload attached to a task-local KnowledgeDocument revision. This exercised the actual Attachment Administration Drawer without touching repository-owned storage.

Final clean browser session:

```text
new console errors   = 0
new console warnings = 0
```

## SQLite / Runtime Safety

- The browser/runtime used only the task-owned `.tmp/ui-ux-r03-20260829-2027` tree.
- Repository SQLite/WAL/SHM was never configured for the verification runtime.
- A pre-existing user-owned process held the repository SQLite sidecar state open at baseline, so an initial exclusive byte hash could not be acquired. The task did not target or stop that process. Final repository database metadata still matched the baseline exactly (`950272` bytes; `2026-08-29T11:50:30.0126773Z`), final SHA-256 was `0ED137C0A673F8E19CE83CFD55866443C498DD6620F52582383AA624732EF3C9`, and no WAL/SHM sidecar remained.
- Task Attachment StorageRoot staging residue was `0` before cleanup. API/Vite listeners on `18561`/`18562` were stopped, browser tabs and viewport override were released, and the entire task-owned runtime tree was removed (`EXISTS_AFTER=False`).

## Existing / New Gaps

- Existing: the non-failing Vite chunk-size advisory remains outside this focused UI consistency slice.
- Existing: UI-UX-R02's documented HumanConfirmation contract decision remains unchanged and outside UI-UX-R03.
- New Blocker / High gaps: none.
- New in-scope product gaps: none.

## Final Result

All applicable UI-UX-R03 PASS conditions are satisfied:

```text
UI-UX-R03 PASS
```

No DB Discovery, Attachment-domain expansion, or other follow-on feature was started.
