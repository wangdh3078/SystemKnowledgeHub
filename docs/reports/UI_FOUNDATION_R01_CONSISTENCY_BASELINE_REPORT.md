# UI-FOUNDATION-R01

## Result

`UI-FOUNDATION-R01 PASS`

## Worktree Baseline

- Branch: `main`.
- Starting HEAD: `bdbb773 docs: plan next major phase`.
- `origin/main`: synchronized at task start.
- Initial staged/unstaged/untracked status: clean.
- Initial `git diff`, `git diff --stat`, and `git diff --check`: empty/PASS.
- Git ownership was handled with command-scoped `safe.directory`; no global Git configuration was changed.

## Authority

Read authority included `AGENTS.md`, both frozen MVP UI specifications, the final MVP and next-phase planning reports, R06 acceptance, PHASE-REV delta/gap material, current editor/revision decisions, and the real frontend. No frozen specification, Golden asset, or TRACE contract was modified.

## Existing UI Inventory

Static scan counted Vue files using each Element Plus control (not raw instance totals): button 52, input 32, input-number 6, select 23, option 19, table 5, table-column 10, pagination 3, dialog 1 host, form/form-item 22 each, tag 5, tooltip 1, alert 14, switch 1, checkbox 2, date-picker 1. `el-empty`, skeleton, tabs, dropdown, and popover are not currently used and receive no invented implementation rule. Drawers are centralized in `DrawerHost` although feature files do not directly declare `el-drawer`.

Reviewed areas include Dashboard, Systems list/detail, Business Functions list/detail, Database Objects list/detail, Unknown Items list/detail, KnowledgeDocument list/detail/create/history/compare/restore, Business Rule/Integration details and drawers, Global Search/Create, User Management, Shell/Context Rail/Dialog/Drawer hosts, shared feedback/status components, global styles, bootstrap, routes, stores, API boundaries, and feature CSS.

| Component/pattern | Usage areas | Current variants | Audit decision |
| --- | --- | --- | --- |
| Button | headers, sections, forms, tables, overlays, editor | primary/default/danger/text/small/icon-only | A: formalize semantics and icon accessibility; C: editor compact |
| Input/Select | filters, dialog/drawer/full-page forms, search/editor | default/large search, full-width form, controlled filter | A: placeholder/role rules; C: global search large |
| Table | list pages and related-object sections | ordinary/dense/comfortable | A: real selection state and explicit title link; C: density variants |
| Dialog/Drawer | single hosts plus teleported feature content | create/confirm/search/restore/read/edit | A: one coordinator and stable action/scroll rules; no new manager |
| Status/Tag | KnowledgeStatus, lifecycle, users, workflow, priority | domain-specific mappings | A: text + stable mapping; C: dimensions remain distinct |
| Feedback | shared Loading/Empty/Error plus contextual conflicts | initial/refresh/empty/error/conflict | B: continue shared states; C: contextual conflict recovery |

## Button Audit

- A — Must Standardize: one dominant primary action per group; neutral cancel; danger only for destructive actions; default page actions versus small dense actions.
- A corrected: selected global search/current-user/user-management icon-only closes now use shared target/focus treatment, Element Plus tooltip, and matching accessible names.
- B — Should Standardize: remaining legacy custom glyph close controls should adopt the same tooltip pattern when their feature is next modified; they retain visible close glyphs and existing behavior.
- C — Intentional: R06 editor toolbar remains compact and icon-only; progression actions remain small/plain.

## Input Audit

Editable, search/filter, textarea, read-only facts, and CodeMirror are distinct roles. Native default Element Plus sizing remains the ordinary baseline; the global search input intentionally uses `large`. No input wrapper or control-internal rewrite was added.

## Select Audit

Form selects fill form columns; filter selects retain controlled widths. Optional filters are clearable and use dimension-specific `…：全部` placeholders. Stable Chinese labels map to unchanged backend values. Database/Schema browsing remains navigation, not a select-filter rewrite.

## Form Audit

Top-label authoring forms are already consistent across create/edit overlays. Dialog, Drawer, full-page, and compact filter forms intentionally use different layouts. Validation remains inline through Element Plus; no generic form framework was introduced.

## Table Audit

- A corrected: Systems and Business Functions no longer paint the first row as selected without selection state.
- A corrected: entity title/name cells on Systems, Business Functions, Database Objects, Unknown Items, KnowledgeDocuments, and Users expose explicit focusable navigation/action buttons while retaining frozen row-click convenience.
- A corrected: ordinary, dense database, and comfortable user table semantics are declared centrally.
- Existing overflow tooltips remain on long purpose/technology/user fields; action columns retain protected width.
- C: database browser remains dense; users remain comfortable because roles wrap; related-object tables remain feature-scoped.

## Dialog Audit

`DialogHost` remains the single overlay coordinator with bounded widths and feature content. Existing cancel-primary ordering is retained. Knowledge create/editor and restore workflow semantics are frozen and unchanged. No modal service or stacked Dialog system was added.

Browser audit found that the KnowledgeDocument create Dialog's existing viewport rule used a descendant selector even though Element Plus attaches the custom class directly to `.el-dialog`. At 1280×720 the rule therefore did not apply (`top=108`, `bottom=908`). UI-FOUNDATION-R01 corrected only that selector, restored the 4vh/92vh bounded viewport, made the Dialog body the internal scroll owner, and kept the footer reachable; editor structure and persistence behavior were not changed.

The same 1280×720 audit found that revision Restore content could exceed the generic host Dialog (`bottom=913`). A restore-specific host class now applies the same 4vh/92vh boundary and internal body scrolling. The resulting Dialog measured `top=29`, `bottom=691`, `height=662`; its footer became reachable at `bottom=675` after scrolling the Dialog body. The restore close control also has a 32×32 target, matching Tooltip, and accessible name.

## Drawer Audit

`DrawerHost` remains the sole Drawer coordinator, one layer at a time, using the established width and feature-owned body/footer. User edit close accessibility was normalized. No second scroll/overlay manager was introduced.

## Tag / Status Audit

KnowledgeStatus continues through `KnowledgeStatusBadge`. Lifecycle, priority, Unknown Item workflow status, rewrite status, update status, and user active state retain separate text-bearing mappings. UI Foundation does not equate Published with Confirmed or Draft with Unknown.

## Tooltip Audit

Element Plus tooltip registration remains present. R06 toolbar behavior is unchanged. Shared `.skh-icon-action` defines a 32px target and keyboard focus; affected overlay closes now pair Tooltip content with accessible intent. Ordinary text buttons do not receive redundant tooltips.

## Typography Audit

Existing font stack is retained. Page title/supporting text, table headers, metadata, form/helper text, and technical mono roles are documented. UI Foundation semantic title size is 27px. No new font dependency was introduced.

## Spacing / Layout Audit

The existing 4/8/12/16/20/24/32 scale is retained. Shared page padding, section/filter gap, title, table, and pagination tokens reduce repeated values without replacing feature-specific calculations. Main Content/Context Rail/Shell widths are unchanged.

## Filter / Search Audit

Primary list filter bars now share `.skh-filter-bar` alignment while preserving feature-owned columns. Keyword debounce and immediate select change behavior remain. Clear restores canonical query state. Global Search keyboard navigation and recent/no-result flows are frozen and unchanged.

## Loading / Empty / Error Audit

Existing `LoadingState`, `EmptyState`, and `ErrorState` are reused; no competing components were added. Initial, empty/filtered empty, refresh failure, forbidden, and concurrency conflict are documented as distinct states. Page-specific copy remains contextual.

## Accessibility Audit

Explicit table title/name buttons provide keyboard navigation and visible focus. Affected icon-only closes have tooltip + accessible name. Existing roles, `aria-busy`, form labels, Dialog semantics, and text-bearing statuses remain. This task does not claim closure of deferred low gaps without separate evidence.

## Responsive Audit

Semantic page padding reduces at 1440px. Feature-owned grid layouts and established media queries remain. Database dense table, user action reachability, bounded overlays, Context Rail behavior, and editor internal scrolling are preserved. Browser evidence is recorded after runtime verification.

## Intentional Differences

- R06 editor compact toolbar and bounded source/preview.
- Global Search large input and keyboard-oriented overlay.
- Database embedded browser dense table and split pagination.
- User table comfortable multiline rows and fixed-right action column.
- Dialog/Drawer/full-page/filter form layouts.
- Domain-specific status dimensions.

## Baseline Decisions

Consistency is semantic. Native Element Plus remains the control layer. Shared CSS owns repeated layout/interaction patterns; features own business layout and column definitions. Row click may remain as frozen convenience only when a visible focusable name/title action exists.

## Design Tokens Added or Reused

Reused existing palette, spacing, radius, shell, and status tokens. Added semantic `--skh-page-*`, title, section/filter gap, table height, and pagination height tokens plus compatibility aliases for older feature CSS that referenced obsolete token names.

## Shared Components Added / Reused

Added Vue components: none. Reused `LoadingState`, `EmptyState`, `ErrorState`, `KnowledgeStatusBadge`, `DialogHost`, `DrawerHost`, and native Element Plus controls. Added shared semantic stylesheet `ui-foundation.css`.

## Page-level Corrections

Dashboard header; Systems, Business Functions, Database Objects, Unknown Items, KnowledgeDocuments, and Users list surfaces; Global Search close; current-user panel close; User edit/role-management close. No detail business semantics changed.

KnowledgeDocument create Dialog received the selector-only bounded viewport correction described under Dialog Audit.

## Deferred Issues

- B: remaining legacy minified feature CSS/templates should adopt canonical token names and the icon tooltip pattern only when those features are next modified; a formatting/rewrite was not justified here.
- D: navigation architecture, backend contracts, TRACE, Attachments, Discovery, Governance, mobile-first redesign, generic table/form frameworks.

## Existing REV Low Gap Interaction

No deferred REV low gap is claimed closed. R06 tooltip/bootstrap and editor accessibility remain intact. UI Foundation naturally improves affected icon/table focus behavior without changing the Gap Register.

## Type Check

PASS — `npm run type-check` completed with exit code 0 in both the initial and final cycles.

## Frontend Build

PASS — `npm run build` completed with exit code 0. Vite transformed 3,392 modules and produced the production bundle. The existing chunk-size advisory remains non-blocking and is not a UI Foundation regression.

## Affected Vitest

PASS — 5 focused files / 40 tests passed: AppTopBar, KnowledgeDocument detail, editor, Markdown rendering, and System Unified Knowledge View.

## ESLint

PASS — scoped ESLint over every modified TypeScript/Vue source completed with exit code 0 in both implementation cycles.

## Browser Smoke

PASS — isolated API/Vite runtime and disposable Administrator were used; no repository database or production-like credentials were reused.

- At 1280×720, Dashboard, Systems, Business Functions, Database Objects, Unknown Items, KnowledgeDocuments, and Users rendered without root horizontal overflow. Systems and Business Functions used 44px headers/52px rows, Database Objects used the intentional dense 36px/46px variant, and Users used the intentional comfortable 48px/62px variant.
- Systems, Business Functions, Database Objects, Unknown Items, KnowledgeDocuments, and Users exposed explicit name/title action buttons where rows existed. The former fake first-row highlight was absent. Filters remained aligned and user actions remained reachable.
- At 1180×720, Systems, Business Functions, Database Objects, and Users retained reachable filters/actions without root overflow; the fixed-right Users action column ended inside the viewport.
- Global Search opened with a 32×32 named close action, returned 11 grouped `MES` results across the existing entity categories, and introduced no overflow. Global Create retained six enabled creation types and the established disabled context entries.
- The current-user close, restore close, and user-drawer close showed matching visible Tooltips; tested controls had 32px minimum targets and accessible names.
- The User Management Drawer measured 440px wide, owned its internal vertical scroll, and exposed its footer at `bottom=696` after body scrolling.
- KnowledgeDocument R1/R2 History, historical preview, Compare, and Restore entry surfaces all rendered without root overflow. Compare showed the real appended line between immutable revisions. Restore confirmation used bounded internal scrolling and kept its footer reachable.
- Browser console result: no warnings or errors.

## R06 Regression Smoke

PASS — a 520-line Markdown document with a fenced TypeScript block was created through the real UI against the isolated database.

- Before the create fix, the Dialog measured `top=108`, `bottom=908`, `height=800`; after the selector correction it measured `top=29`, `bottom=691`, `height=662`, with the footer visible and no root overflow.
- Create source scroller: `clientHeight=325`, `scrollHeight=10370`, internally scrollable; toolbar and create action remained available.
- Detail edit source scroller: `clientHeight=425`, `scrollHeight=10370`, `overflow-y=auto`; the editor toolbar remained visible and page Save stayed at `top=44`, `bottom=76`.
- Preview: `clientHeight=425`, `scrollHeight=4492`, `overflow-y=auto`, with `预览未保存内容` visible and no root horizontal overflow.
- A real page-level Save created revision 2 and returned to read mode; R06 storage, toolbar, source/preview, history, compare, and restore semantics were not changed.

## Repository DB Protection

PASS — before and after runtime verification the repository database remained length `724992`, mtime UTC `2026-08-24T15:38:12.9720638Z`, SHA-256 `854EEDAF15B04F5AFD549769D6045689E3C0240C3B41AE2571EA1711F5085CA6`.

The isolated database/key directory was removed after its task-owned API process released the SQLite handles. Ports 5096 and 5181 were confirmed released, the API/Vite wrappers and child processes started for verification were stopped, and the in-app browser tab/viewport override were closed/reset.

## New Gap Check

PASS — final static, runtime, console, responsive, and diff review found no new Blocker, High, or Medium gap. Existing deferred REV Low gaps remain explicit and unchanged. The build's existing chunk-size advisory remains a non-blocking performance follow-up, not a newly introduced gap.

## Files Changed

- `docs/DOCUMENT_INDEX.md`
- `docs/design/SYSTEM_UI_COMPONENT_AND_INTERACTION_BASELINE.md`
- `docs/reports/UI_FOUNDATION_R01_CONSISTENCY_BASELINE_REPORT.md`
- `src/SystemKnowledgeHub.Web/src/app/bootstrap/bootstrapApp.ts`
- `src/SystemKnowledgeHub.Web/src/styles/tokens.css`
- `src/SystemKnowledgeHub.Web/src/styles/ui-foundation.css`
- `src/SystemKnowledgeHub.Web/src/layouts/AppTopBar.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/DialogHost.vue`
- `src/SystemKnowledgeHub.Web/src/features/dashboard/pages/DashboardView.vue`
- `src/SystemKnowledgeHub.Web/src/features/systems/pages/SystemsListView.vue`
- `src/SystemKnowledgeHub.Web/src/features/systems/systems.css`
- `src/SystemKnowledgeHub.Web/src/features/business-functions/pages/BusinessFunctionsListView.vue`
- `src/SystemKnowledgeHub.Web/src/features/business-functions/business-functions.css`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/pages/DatabaseObjectsListView.vue`
- `src/SystemKnowledgeHub.Web/src/features/unknown-items/pages/UnknownItemsListView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentsListView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentRestoreDialogContent.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/knowledge-documents.css`
- `src/SystemKnowledgeHub.Web/src/features/search/components/GlobalSearchOverlay.vue`
- `src/SystemKnowledgeHub.Web/src/features/users/components/KnowledgeRoleManagementDialog.vue`
- `src/SystemKnowledgeHub.Web/src/features/users/components/UserManagementDrawer.vue`
- `src/SystemKnowledgeHub.Web/src/features/users/pages/UsersManagementView.vue`

## Final Result

`UI-FOUNDATION-R01 PASS`
