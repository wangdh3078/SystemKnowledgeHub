# ATTACH-B04-FIX-R01 UI and Navigation Verification Report

## Result

```text
ATTACH-B04-FIX-R01 PASS
ATTACH-VERIFY READY: YES
```

The corrective frontend slice is complete. Attachment Administration now presents storage and reference status consistently in Chinese, uses the established danger-action styling for permanent deletion and retry, and closes the shared Drawer through its real `closed` lifecycle before routing to the current KnowledgeDocument.

No backend contract, attachment domain behavior, deletion protocol, preview/download boundary, revision behavior, database schema, or frozen design source changed.

## Storage Label Fix

- The storage filter now displays `全部存储状态`, `可用`, and `等待删除重试`.
- Filter values remain the unchanged API values `""`, `Ready`, and `DeletePending`.
- List, statistics, detail badge, metadata, integrity result, retry error copy, and filter options share one frontend presentation mapping.
- Storage-health labels are `文件缺失`, `长度不一致`, `校验不一致`, and `文件不可用` for the unchanged `Missing`, `LengthMismatch`, `Corrupt`, and `Unavailable` wire values.
- The list no longer repeats a Chinese status tag followed by `Ready` or `DeletePending`.

## Reference / Storage Presentation

Reference tags now use:

- `孤立附件`
- `当前引用`
- `仅历史引用`

Their secondary text is derived from the exact response counts:

- orphan: `0 个引用`;
- current: `N 个当前修订引用`, with a historical count only when present;
- historical-only: `N 个历史修订引用`.

Real-browser fixtures demonstrated all three states. The current fixture displayed `1 个当前修订引用 · 1 个历史修订引用`; the historical-only fixture displayed `1 个历史修订引用`; both orphan fixtures displayed `0 个引用`. Domain classification and reference counts were not changed.

## Permanent Delete Style

- Ready orphan deletion and DeletePending retry both use the existing Element Plus `type="danger"`, `plain`, Delete icon, standard height, font size, radius, and border treatment.
- Real computed styling was `el-button el-button--danger is-plain`, approximately 32px high, 13px font, 7px radius, with one SVG Delete icon.
- Referenced attachments continue to expose no permanent-delete action.
- The irreversible confirmation remains in place and states that metadata and physical content are deleted, the action is unrecoverable, and only zero-reference attachments are eligible.

## Drawer Navigation Root Cause

`AdministratorAttachmentDetailDrawer` previously rendered the owner action as a direct `<router-link>`. The route changed while `DrawerHost` was still completing its close animation and before `handleClosed()` released the shared scroll/focus preservation snapshot. This allowed the destination page to mount while the old Drawer lifecycle still owned overlay and focus cleanup.

## Navigation Fix

The owner route is captured before closing. The Overlay Store now exposes a minimal `closeDrawerAfterClosed()` promise, and `DrawerHost.handleClosed()` resolves pending close continuations only after it releases scroll preservation. The detail action then calls `router.push(target)`.

```text
capture target route
→ request shared Drawer close
→ DrawerHost closed
→ release scroll/focus preservation
→ resolve close boundary
→ router.push(target)
```

No timeout, reload, `window.location`, Router bypass, or scroll-preservation disablement was introduced. The detail contains no other Router navigation that leaves the page; preview replaces the single shared overlay and download remains an exact protected content link.

## Files Changed

- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/attachmentAdministrationPresentation.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/attachmentAdministrationPresentation.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/pages/AdministratorAttachmentsView.vue`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/components/AdministratorAttachmentDetailDrawer.vue`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/components/AdministratorAttachmentDetailDrawer.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/attachment-administration.css`
- `src/SystemKnowledgeHub.Web/src/app/stores/overlays.ts`
- `src/SystemKnowledgeHub.Web/src/app/stores/overlays.spec.ts`
- `src/SystemKnowledgeHub.Web/src/layouts/DrawerHost.vue`
- `docs/reports/ATTACH_B04_FIX_R01_UI_NAVIGATION_VERIFICATION_REPORT.md`
- `docs/DOCUMENT_INDEX.md`

## Frontend Verification

```text
npm run type-check
PASS

npm run build
PASS — existing Vite chunk-size advisory only

focused Vitest: presentation, contracts, API client, detail Drawer,
Overlay Store, scroll preservation, and Administrator navigation
PASS — 7 files, 21 tests

targeted ESLint
PASS

targeted Prettier check
PASS
```

Focused regressions cover Chinese label/wire-value separation, Ready and DeletePending labels, Orphan/Referenced/HistoricalOnly count text, Ready orphan delete visibility and danger style, referenced delete suppression, DeletePending retry style, and the exact `close → DrawerHost closed → router.push` ordering.

No backend source changed, so no unrelated backend suite was run.

## Browser Verification

The real in-app browser used:

- API `http://127.0.0.1:18550`;
- web `http://127.0.0.1:18551`;
- task-owned SQLite database;
- task-owned Data Protection directory;
- task-owned Attachment StorageRoot;
- a task-owned local Administrator and task-owned fixtures only.

Verified:

- The storage dropdown displayed only `全部存储状态`, `可用`, and `等待删除重试`.
- Current, historical-only, orphan, Ready, and DeletePending fixtures all rendered with the expected Chinese status and exact count wording, without duplicated English storage enums.
- A task-owned file lock produced the real `503 attachment_storage_unavailable` deletion outcome and `DeletePending` state; the filter then returned exactly that one row.
- Ready orphan delete and DeletePending retry used the same plain danger treatment and accessible names. The destructive confirmation content remained intact.
- `附件管理 → current-reference.txt → 查看当前文档` was executed twice. Both runs closed the Drawer before navigation, loaded `/knowledge-documents/1`, left zero visible Drawers and zero visible overlays, preserved `pointer-events: auto`, and allowed subsequent clicks and navigation.
- The destination main scroll container changed from the top to `scrollTop ≈ 307.7`, then opened and cancelled edit successfully, proving the page was neither frozen nor click-blocked.
- Exact internal viewports `1440×900` and `1280×720` were calibrated. At both sizes the document scroll width equaled the viewport width; the filter, table, action footer, and Drawer remained within their controlled regions with no page-level horizontal overflow.
- Browser console: `0` errors and `0` warnings.

```text
Browser Verification: PASS
```

## Persistent Data and Cleanup

The corrective runtime never used the repository connection string or repository Attachment StorageRoot. All four fixtures and the intentional DeletePending failure were contained in the task runtime.

An already-running, non-task API process (started at 18:49) held the repository SQLite WAL before browser verification. During the task it independently checkpointed the existing WAL into the main database before the task-owned API started at 19:12; the repository main file timestamp therefore changed and WAL/SHM disappeared. The task did not start, stop, browse, or configure that external process. This is an external-runtime filesystem observation, not evidence that this corrective runtime touched repository data.

Cleanup completed:

- released the exact task-owned file lock;
- reset the temporary browser viewport and closed the verification tab;
- stopped only the API/Vite processes started for this task;
- released ports 18550 and 18551;
- removed the task SQLite database, Data Protection keys, Attachment StorageRoot, fixture files, and runtime directory;
- verification-process residue: `0`;
- task runtime/storage residue: `0`.

```text
Task-owned Storage Cleanup: PASS
Runtime Cleanup: PASS
```

## Existing / New Gaps

Existing advisory:

- The existing Vite chunk-size warning remains informational and unchanged.

New product gaps:

- None.

The concurrent repository-WAL checkpoint is recorded above as an external runtime observation. It did not affect the isolated correction result and does not represent a new attachment product defect.

## ATTACH-VERIFY Readiness

All applicable corrective requirements and automated/browser gates passed, with no unresolved Blocker or High issue.

```text
ATTACH-VERIFY READY: YES
```

This task did not start or reopen ATTACH-VERIFY.
