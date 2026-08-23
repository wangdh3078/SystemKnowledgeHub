# UI-B02 Layout + Knowledge Status Dialog Verification Report

## Result

UI-B02 PASS

## Scope and guardrails

- Only UI layout, table presentation, and Dialog rendering lifecycle code changed.
- Authentication, authorization, current-user semantics, Domain rules, Evidence schema, Human Confirmation, and Knowledge Status transition rules were not changed.
- The existing `Unknown → Inferred → Confirmed` server validation, evidence requirement, and opaque concurrency token remain in use.

## Issue 1 — 待确认事项页面宽度

### Root cause

`unknown-items.css` assigned both Unknown Item pages `max-width: 1180px` and `margin: 0 auto`, bypassing the normal full-width application content area. The Business Functions list also retained a separate 1500px cap while the standard list pages use the full main-content area.

### Changed files

- `src/SystemKnowledgeHub.Web/src/features/unknown-items/unknown-items.css`
- `src/SystemKnowledgeHub.Web/src/features/business-functions/business-functions.css`

### Before / after

- Before: Unknown Items was centered and capped at 1180px; Business Functions had a distinct 1500px cap.
- After: Unknown Items and Business Functions lists use the same full application content area as User Management. The Business Function detail page keeps its existing detail-specific max width.
- At the 1920px browser verification viewport, the Unknown Items, Business Functions, and User Management page roots each measured 1724px (`x=196..1920`); their header/table content measured `x=226..1890`.
- At compact desktop width, the three compared list pages use the same 24px side padding.

### Browser verification

Verified the three required pages directly: 待确认事项, 业务功能, 用户管理. Their filter/table content had no horizontal overflow.

## Issue 2 — 业务功能列表末列

### Root cause

The final column was an unlabeled 34px `ArrowRight` icon. Its actual meaning was a redundant “enter Business Function detail” affordance; the complete row already opens the detail route. The small column could appear as clipped `> ...` content.

### Changed files

- `src/SystemKnowledgeHub.Web/src/features/business-functions/pages/BusinessFunctionsListView.vue`
- `src/SystemKnowledgeHub.Web/src/features/business-functions/business-functions.css`

### Resolution and browser verification

Removed the redundant arrow column rather than preserving an unlabeled action. The final visible column is now the explicitly headed `更新于` column. At 1920px it measured `x=1782..1890`, fully inside the table; no blank header or clipped trailing action remains.

## Issue 3 — 用户管理操作列

### Root cause

The fixed-right action column was only 140px and had no reserved right-side cell spacing, which left the `编辑` / `停用` controls visually pressed against the table edge.

### Changed files

- `src/SystemKnowledgeHub.Web/src/features/users/pages/UsersManagementView.vue`
- `src/SystemKnowledgeHub.Web/src/features/users/users.css`

### Resolution and browser verification

The explicit `操作` column is now a 164px fixed-right column with a named cell class, 12px internal spacing, and non-wrapping actions. At 1920px the column measured `x=1726..1890`; the two buttons ended at `x=1813` and `x=1885`, respectively. Both `编辑` and `停用` were fully visible, and the users table had no horizontal overflow.

## Issue 4 — 推进为推断空白 Dialog

### Actual root cause and runtime DOM evidence

This was reproduced against `EAP.Test` → field `ID`.

Before the fix, `KnowledgeStatusDialogContent` was mounted inside `ColumnDetailDrawer`. Clicking `推进为推断` replaces the single current overlay from a Drawer to a Dialog. `DrawerHost` therefore destroys the Column Drawer, which destroys the source component and its Teleport. `DialogHost` correctly created the Element Plus Dialog shell and `#dialog-feature-content`, but the target then had zero children.

Runtime DOM/CSS evidence before the fix:

- `#dialog-feature-content.children.length`: `0`
- `.el-dialog__body` computed height: `0px`
- Dialog shell computed height: about `33px`
- No `.knowledge-status-dialog` node existed

This was not a hidden-text, z-index, opacity, height override, or Teleport target race. The target existed; the source was unmounted by the overlay lifecycle. Page-level status dialogs had been less likely to expose this because their source components remain mounted, which explains why the failure could appear environment/path dependent.

### Changed files

- `src/SystemKnowledgeHub.Web/src/layouts/DialogHost.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-status/components/KnowledgeStatusDialogContent.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/components/ColumnDetailDrawer.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/pages/DatabaseObjectDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/business-functions/pages/BusinessFunctionDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/business-rules/pages/BusinessRuleDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/integrations/pages/IntegrationDetailView.vue`

### Resolution

`KnowledgeStatusDialogContent` is now mounted by the persistent `DialogHost` as a direct Element Plus Dialog body child; it no longer uses Teleport. Successful status changes and the explicit conflict recovery action publish `knowledge-status:changed`; active relevant detail pages reload through that event. The prior per-page / per-drawer content mounts were removed, so only one stable renderer exists.

Runtime DOM/CSS evidence after the fix:

- `.knowledge-status-dialog` is a direct `.el-dialog__body` child, is visible, and has computed height about `478.5px`
- `#dialog-feature-content` remains available for the other feature Dialogs; Knowledge Status no longer depends on its creation timing.
- The Dialog contains title, current/target status, requirement, reason input, Cancel, and Confirm controls.

### Browser verification path and cases

Local runtime path verified: Browser → Vite/Vue → DialogHost → Knowledge Status Dialog → ASP.NET Core API → SQLite.

- Case A: Opened `EAP.Test` → `ID` → `推进为推断`; all required content appeared.
- Case B: Cancel closed the Dialog and released the overlay.
- Case C: Reopened after cancel; full content remained visible.
- Case D: Entered a reason and confirmed a real `Unknown → Inferred` API transition. After reload, the field row displayed `推断`.
- Case E: Held a second Dialog open with the old concurrency token, completed the first transition in another tab, then submitted the stale Dialog. The visible Dialog showed `内容已被其他操作修改，请重新加载后重试。` and the explicit `重新加载` action; it did not return to a blank shell.
- Case F: Verified `Open → Cancel → Open → Cancel → Open → Confirm` for `Inferred → Confirmed`. The final `EAP.Test.ID` row displayed `已确认`.

## Additional page regression check

Checked 总览, 系统, 业务功能, 数据库, 待确认事项, and 用户管理 in the running application. The checked list tables did not overflow horizontally; the Database page retained its intentionally separate Database/Schema browsing region.

## Automated verification

- `npm run type-check` — passed.
- `npm run build` — passed. Vite emitted only its existing bundle-size advisory.
- Scoped ESLint over all modified Vue files — passed with no warnings or errors.
