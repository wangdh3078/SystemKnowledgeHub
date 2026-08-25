# System UI Component and Interaction Baseline

## Status

`APPROVED — UI-FOUNDATION-R01 PASS` (2026-08-25).

This non-frozen design baseline governs new System Knowledge Hub UI work after UI-FOUNDATION-R01. Frozen specifications, Golden assets, and phase-specific architecture decisions remain higher authority.

## Scope

This document standardizes the existing Vue 3 + Element Plus product by semantic role. It covers established pages, Main Content, Context Rail, the single Dialog/Drawer coordinator, forms, lists, tables, state presentation, feedback, accessibility, and narrow desktop behavior. It does not redesign the product or change domain, API, persistence, authorization, relation, revision, or editor behavior.

## Principles

- Consistency follows semantic role, not component name alone.
- Preserve intentional density and workflow differences.
- One action group has at most one dominant primary action.
- Prefer explicit, discoverable navigation and feedback.
- Use shared tokens and CSS conventions before extracting components.
- Formal UI copy is Simplified Chinese; API enum values remain unchanged.

## Element Plus Boundary

Use native Element Plus controls, the selective registration in `bootstrapApp.ts`, shared tokens, and semantic CSS. Do not create one-to-one `SkhButton`, `SkhInput`, `SkhSelect`, `SkhTable`, `SkhDialog`, or similar wrappers. Do not replace Element Plus or add a second grid/overlay/form framework.

## Design Tokens

`src/styles/tokens.css` owns the product palette, status colors, spacing scale, radii, shell dimensions, and UI Foundation semantic layout tokens. Feature CSS may use specific values when content requires them, but repeated page padding, title size, section/filter gaps, table heights, and pagination height use the shared tokens. Compatibility aliases exist only to make older feature styles resolve to the current canonical tokens; new code uses the canonical names.

## Typography

| Role | Baseline |
| --- | --- |
| Page title | `--skh-page-title-size`, strong ink, 1.2 line height |
| Supporting text | 13px muted, 1.55 line height |
| Section title | existing `h2` baseline; feature size may increase only for true major sections |
| Panel title | 13–15px semibold |
| Body | 13–14px regular text |
| Secondary / metadata | 10–12px muted |
| Table header | 10–11px semibold/technical density |
| Form label | Element Plus label semantics; visible label required |
| Helper text | 11–12px muted; not a validation substitute |
| Code / locator | `--font-mono` through `.technical-text` |

## Spacing

Use the established `--space-1/2/3/4/5/6/8` scale. Page edges use the semantic page padding tokens. Ordinary action groups use 8–12px gaps. Section separation normally uses 20–24px. Feature-specific editor, diagram, table-column, or calculation values need not be tokenized when they are not repeated semantics.

## Page Layout

Use `.skh-page` for ordinary full Main Content pages. Preserve the frozen Application Shell, Main Content, Context Rail, and one-at-a-time Drawer layout. At 1440px and below page inline padding reduces to 24px. Do not force mobile-first restructuring into the Internal Pilot desktop product.

## Page Header

Use `.skh-page-header` for the title/supporting text/action row and `.skh-page-header__actions` for actions. Put the page's primary creation or save action in the header action group where the established page pattern does so. Breadcrumbs/eyebrows are optional and must represent real hierarchy; do not add them mechanically.

## Page Sections

Sections must have a clear heading, optional supporting description/count, and an action group aligned with that heading. Avoid nested cards when a divider or spacing already communicates hierarchy. Context Rail sections may remain denser than Main Content.

## Buttons

- Primary: create, save, publish, confirm, or the single main workflow transition.
- Default: cancel, close, return, reset, and secondary actions.
- Danger: delete, deactivate, or an actually destructive operation. Cancel is never danger.
- Text/link: lightweight navigation or secondary table/section actions, not hidden critical actions.
- `small`: compact toolbar, dense table actions, progression controls. Ordinary page actions use default size.
- Dialog footer order is secondary/cancel first, primary or destructive confirmation last.

## Icon-only Actions

Icon-only controls need a minimum 32×32 target, visible hover/focus/disabled states, an accessible name, and a tooltip with the same intent. Use `.skh-icon-action` for the target/focus convention and native `el-tooltip` for hover/focus disclosure. The R06 editor toolbar keeps its frozen compact sizes and existing tooltip/ARIA behavior.

## Inputs

Use native default-sized Element Plus controls unless a frozen compact/large surface requires otherwise. Search inputs identify their search scope in placeholder or accessible label. Editable, read-only, disabled, textarea, and code/Markdown editor roles remain visually distinct. Do not render ordinary read-only facts as disabled inputs.

## Selects

- Form select: fills its form column; required choices use stable Chinese labels and unchanged wire values.
- Optional filter select: controlled width, clearable, placeholder names the dimension and all-state (for example `知识状态：全部`).
- Required form placeholder: `请选择…`; optional filter: `<维度>：全部`; an actual explicit all option may be used when the model needs a non-null sentinel.

## Forms

Dialog, Drawer, and full-page forms may differ in layout but each uses visible labels, consistent section spacing, inline validation, and a stable action footer. Top labels remain the established authoring default. Required indicators and error text come from Element Plus form semantics. Help text explains consequences; it does not duplicate the label.

## Filter Bars

Use `.skh-filter-bar` for shared alignment/gap while the feature owns its grid columns. Keyword changes use the established short debounce; discrete select changes query immediately. Optional filters are clearable. `清除筛选` restores the canonical default query and page 1. Database/Schema browsing remains a separate navigation control, not a filter masquerading as Context Rail.

## Tables

Use `.skh-data-table` for the ordinary compact table baseline. Use `--dense` for embedded technical browsing and `--comfortable` only for rows with legitimate multiline content. Tables require a visible header, hover state, stable action columns, overflow handling, loading/empty/error separation, and reachable actions. Do not paint the first row as selected without actual selection state.

Entity tables expose a visible title/name navigation control using `.skh-table-link`; row click may remain as the frozen convenience behavior but is not the only navigation affordance. Nested actions stop row navigation. Title/name is normally one line; descriptions and long technical lists use ellipsis plus Element Plus overflow tooltip where the full value matters. IDs/locators remain recognizable and copyable. Action columns remain right-aligned/fixed only when needed, reserve enough width, and do not collapse.

## Pagination

Use `.skh-pagination` with backend page size/total contracts. It displays the visible range and total when available and aligns the range with the pager. `.skh-pagination--split` is reserved for the intentionally compact database browser workspace. Pagination resets to page 1 after query/filter changes.

## Dialogs

The existing `DialogHost` remains authoritative. Dialogs use viewport-bounded widths, internal body scrolling when content is long, a visible accessible title, stable close behavior, and footer order `取消 | 主要动作`. Dirty authoring uses the existing discard guard. Do not introduce a modal service or stacked overlay system.

## Drawers

The existing `DrawerHost` remains authoritative and opens one Drawer at a time. A Drawer has one header, one body scroll owner, and one footer action area. Save/cancel stays reachable without nested random scrolling. The established 500px desktop width may vary only for a documented content need.

## Tags and Status

Every status has text; color is supplementary. KnowledgeStatus uses the existing explicit `Unknown/Inferred/Confirmed` mapping through `KnowledgeStatusBadge`. Lifecycle, user active state, relationship/evidence state, priority, and workflow status are separate dimensions and must not reuse labels/colors in a way that implies equivalence. Use a limited semantic palette: primary, success, warning, danger, info/neutral.

## Tooltips

Use tooltips for icon-only controls, important truncated text, and non-obvious compact actions. Do not add them to every ordinary text button. Tooltip text and accessible name express the same intent. Preserve the selective Element Plus tooltip bootstrap and the R06 editor tooltips.

## Loading

Initial content load uses `LoadingState`; keep the content surface stable for refresh/paging with `aria-busy` rather than blanking or flickering. The R06 editor has its own established lazy/loading behavior and is not normalized into table loading.

## Empty States

Use `EmptyState` with specific title/description. Distinguish an empty dataset from a filtered empty result in copy. A create action may accompany an empty dataset where the actor can edit. Do not use the same `暂无数据` sentence for permissions, error, and no matches.

## Error States

Initial load failure uses `ErrorState` with retry. Refresh failure keeps existing content and shows contextual inline feedback. Forbidden uses the established dedicated route/state. Field validation stays inline; concurrency conflicts keep their existing contextual reload/retry action.

## Feedback

Normal successful actions use lightweight `ElMessage`. Copy feedback remains local to the copied control/card. Validation is inline. Destructive confirmation uses a Dialog/MessageBox. Network and revision conflicts provide contextual recovery and never claim success before server confirmation.

## Destructive Actions

Delete, relationship removal, user deactivation, and archive have distinct domain wording. Use danger presentation only for destructive intent and confirm only when the consequence warrants it. Archive is not renamed to delete. Cancel remains neutral.

## Accessibility

All interactive controls are keyboard reachable and have visible focus. Inputs/forms have labels or explicit accessible labels. Icon-only actions have tooltip + accessible name. Dialogs have accessible titles. Status is never color-only. Explicit title/name buttons provide keyboard table navigation; row click remains convenience. New work should preserve contrast and `aria-busy`, `role=status`, or `role=alert` semantics where applicable.

## Responsive Behavior

The product remains desktop-first. At supported narrow desktop/tablet-like widths, filters may wrap or reflow, action groups remain reachable, tables stay within their content region with controlled overflow, Dialogs remain viewport bounded, and Context Rail follows the frozen 1440/1366 collapse rule. Do not compress Navigation + Main + Rail + Drawer into unusable columns.

## Shared Component Extraction Rules

Extract a component only when the same semantic role, layout, and behavior has at least three meaningful uses, reduces duplication, and does not hide business behavior. Prefer slots/simple props. Do not create schema-driven tables/forms, a column DSL, JSON page configuration, or business-aware FilterBar. UI-FOUNDATION-R01 adds no new Vue shared component because existing native controls plus semantic CSS are sufficient.

## Intentional Exceptions

- R06 editor toolbar: compact icon-only controls and bounded internal source/preview scrolling.
- Database browser table: dense row/header treatment and split pagination within the embedded Source/Schema workspace.
- User table: comfortable row height for multiple KnowledgeRole tags and a fixed reachable action column.
- Dialog/Drawer/full-page/filter forms: distinct layouts appropriate to their surfaces.
- KnowledgeStatus, lifecycle, workflow status, priority, and active state: separate semantic mappings.

## Frozen Existing UX

Do not regress the Application Shell, Main Content/Context Rail responsibilities, single Overlay coordinator, Golden list/detail navigation, relationship direction/semantics, revision History/Compare/Restore behavior, published-save confirmation, HumanConfirmation coverage/conflict UX, or the R06 raw Markdown editor, toolbar, source/preview boundaries, Mermaid/highlighting, copy feedback, and page-level Save.

## Usage Examples

```vue
<main class="feature-page skh-page">
  <header class="feature-page__header skh-page-header">
    <div><h1>页面标题</h1><p>支持说明。</p></div>
    <div class="skh-page-header__actions"><el-button type="primary">新增</el-button></div>
  </header>
  <section class="feature-filter skh-filter-bar" aria-label="页面筛选">…</section>
  <el-table class="feature-table skh-data-table">…</el-table>
  <footer class="feature-pagination skh-pagination">…</footer>
</main>
```

```vue
<el-tooltip content="关闭详情" placement="bottom">
  <button class="skh-icon-action" type="button" aria-label="关闭详情">…</button>
</el-tooltip>
```

## Future Phase Requirements

TRACE-B01/B02/B03, Attachments, Discovery, and Governance UI must use this baseline by default. A deviation needs a phase-specific design/architecture decision explaining the semantic reason. Future work must not independently select button hierarchy, control size, table actions, Dialog layout, status color, or a new shared component family.
