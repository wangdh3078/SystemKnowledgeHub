# ATTACH-B04 Administrator Attachment Management Verification Report

## Result

```text
ATTACH-B04 PASS
ATTACH-VERIFY READY: YES
```

Administrator Attachment Management is implemented and verified. The delivered slice provides Administrator-only metadata inventory, filters and paging, aggregate statistics, exact reference/history inspection, on-demand integrity verification, soft-deleted owner tombstones, and permanent deletion for exact zero-reference orphans. The implementation preserves the frozen attachment storage, authorization, immutable revision-reference, secure-content-route, and `Upload != Attach` boundaries.

No unresolved Blocker or High issue remains in this task.

## Scope

Implemented only the ATTACH-B04 administration slice:

- Administrator attachment navigation and `/admin/attachments` page.
- Metadata list with filename, kind, extension, reference-status, and storage-state filters.
- Pagination and metadata-derived statistics.
- Detail drawer with normalized metadata, full SHA-256, owner status, storage health, exact reference counts, and referencing revision summaries.
- Exact current or historical protected download/preview actions where a valid reference context exists.
- Explicit on-demand binary integrity check.
- Irreversible permanent deletion for attachments with zero `AttachmentReference` rows.
- `DeletePending` storage-failure state and retry path.
- Focused backend/frontend tests, isolated runtime verification, and real-browser verification.

The task does not add bulk deletion, force deletion, reference cascading, orphan cleanup scheduling, cloud storage, thumbnailing, generic media management, or ATTACH-VERIFY work.

## ATTACH-A01 / A02 / B01 / B03 Compliance

- ATTACH-A01 remains unchanged. SQLite continues to own attachment metadata and immutable revision references; the filesystem continues to own binary content.
- `Upload != Attach` remains intact. Orphan means exactly zero `AttachmentReference` rows across every revision, not merely absent from the current head.
- Current and historical references are counted separately. A historical-only attachment remains referenced and cannot be deleted.
- Permanent deletion never cascades through `AttachmentReference`, never deletes a document/revision, and never treats a soft-deleted owner as an orphan.
- ATTACH-A02 preview capability metadata is reused. No new public/static binary route or preview format was added.
- ATTACH-B01 extension/MIME/signature/size validation, SHA-256 persistence, protected content delivery, StorageKey secrecy, and fail-closed storage behavior remain authoritative.
- ATTACH-B03 current/history download and preview routes are reused with exact document/revision context. The administration API does not expose a bare download route.
- Frozen design sources were not modified.

## Administrator Page and Authorization

- The 管理 navigation group contains an 附件管理 entry only for Administrator users.
- The frontend route is marked Administrator-only and the backend controller is protected by the Administrator policy.
- Editor and Viewer users do not receive the navigation item. Direct navigation is rejected and resolves to the existing forbidden experience.
- The page follows the existing page shell, filter, table, pagination, drawer, confirmation, feedback, and responsive conventions.

## List, Filters, Pagination, and Statistics

`GET /api/admin/attachments` provides a typed, paged metadata projection with:

- filename query;
- attachment kind;
- final normalized extension;
- reference status: `Referenced`, `Orphan`, `Current`, or `HistoricalOnly`;
- storage state;
- bounded page and page-size validation.

The list computes reference classification from exact revision references. It joins active and soft-deleted KnowledgeDocument owners, and performs only shallow storage health inspection for the current page. StorageKey and physical paths are never returned.

`GET /api/admin/attachments/statistics` operates on metadata/reference rows and returns:

- total count and bytes;
- image/file counts;
- orphan, referenced, current, historical-only, and deleted-owner counts;
- Ready and DeletePending counts;
- largest and recent attachment summaries;
- recent seven-day count.

Statistics do not scan or hash every binary. Full binary verification is kept behind the explicit integrity action.

## Detail and Reference Semantics

`GET /api/admin/attachments/{attachmentId}` returns:

- normalized filename, extension, media type, kind, size, state, timestamps, creator, and opaque concurrency token;
- persisted full SHA-256;
- current, historical, total, and distinct revision reference counts;
- exact referencing revision summaries;
- owner identity and a tombstone when the owner is soft deleted;
- storage health without StorageKey or filesystem disclosure.

Current and historical actions use only exact authorized KnowledgeDocument routes. Historical-only attachments select a historical revision context. Soft-deleted owners expose no current-document navigation and use only approved historical routes.

## Integrity Check

`POST /api/admin/attachments/{attachmentId}/integrity-check` performs the explicit full filesystem inspection and returns:

- expected and actual byte length;
- expected and actual SHA-256;
- a normalized health result;
- check timestamp.

Missing, corrupt, or otherwise unavailable content fails closed. The response never contains the StorageKey or a physical path.

## Permanent Deletion Protocol

Deletion is available only when the attachment has exactly zero references. The UI confirmation identifies the target file and size and states that both metadata and physical content will be irreversibly removed.

The backend protocol is:

1. Parse and validate the opaque concurrency token.
2. In an immediate SQLite transaction, reload the attachment, recheck version and the exact zero-reference invariant, transition `Ready` to `DeletePending`, increment the version, and commit.
3. Delete the physical object. A missing object is idempotently treated as already removed.
4. In a second immediate transaction, reload and recheck `DeletePending`, the transition version, and the zero-reference invariant before deleting metadata.

If physical deletion fails, metadata remains `DeletePending`. The response is fail closed, the page reloads the latest state/token, and the user can explicitly retry. New semantic saves cannot attach a `DeletePending` item. The retry path reuses the same guarded protocol; there is no force or cascade option.

## Error and Concurrency UX

- `400` validation failures remain field-aware.
- `401/403` remain authorization failures.
- `404` reports a missing attachment without exposing storage details.
- `409` concurrency/state conflicts reload current data and require a fresh decision.
- `422` reference/state failures explain that deletion or attachment is no longer valid.
- `503/507` storage failures retain `DeletePending`, preserve metadata, and offer an explicit retry after reload.
- List, detail, integrity, deletion, empty, and loading states remain independently recoverable.
- No failure path clears unrelated page state or invents a public content URL.

## Accessibility and Responsive Verification

- Filters have labels and native/Element Plus keyboard interaction.
- Detail, integrity, download/preview, delete, and retry controls have accessible text.
- Status is communicated with text rather than color alone.
- The drawer and irreversible confirmation reuse the existing overlay/focus boundary.
- Real browser viewports were calibrated to exact `1280x720` and `1440x900` inner dimensions.
- At both sizes, the document width matched the viewport width; no page-level horizontal overflow was present.
- The first browser pass exposed unresolved local registration for the radio filter controls. Local Element Plus registration was added, and a fresh browser pass confirmed one real radio group with three controls, functional filtering, and zero new console errors or warnings.

## Automated Verification

Backend:

```text
dotnet build SystemKnowledgeHub.sln -c Release --no-restore
PASS — 0 warnings, 0 errors

dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj \
  -c Release --no-build \
  --settings .tmp/attach-b04-verification/serial.runsettings \
  --filter "FullyQualifiedName~AdministratorAttachmentsApiTests|FullyQualifiedName~AttachmentFoundationApiTests"
PASS — 14 passed, 0 failed, 0 skipped

final AdministratorAttachmentsApiTests authorization assertion rerun
PASS — 5 passed, 0 failed, 0 skipped

dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj \
  -c Release --no-build \
  --settings .tmp/attach-b04-verification/serial.runsettings
PASS — 192 passed, 0 failed, 0 skipped
```

The approved deterministic serial gate used `MaxCpuCount=1`, disabled xUnit test-collection parallelization, and set one maximum parallel thread. An initial runner-settings attempt that did not include the xUnit section stalled under the existing `REV-GAP-011` infrastructure condition; that exact test session was stopped and replaced with the approved deterministic configuration above.

Frontend:

```text
npm run type-check
PASS

npm run build
PASS — existing chunk-size advisory only

npx vitest run \
  src/features/attachment-administration/api/administratorAttachmentContracts.spec.ts \
  src/features/attachment-administration/api/administratorAttachmentsApi.spec.ts \
  src/features/attachment-administration/components/AdministratorAttachmentDetailDrawer.spec.ts \
  src/layouts/AppSidebar.attachments.spec.ts
PASS — 4 files, 9 tests

targeted ESLint for changed frontend files
PASS
```

## Real Browser Verification

The browser used isolated API/web ports `18440` and `18441`, a task-owned SQLite database, task-owned Data Protection keys, and a task-owned attachment StorageRoot.

Verified scenarios:

- Administrator navigation, route access, list, pagination basis, all filters, statistics, loading, and detail drawer.
- Six seeded fixtures covering current, historical-only, orphan, image, ordinary file, and soft-deleted-owner states.
- Filename filtering selected only the intended historical CSV item.
- Historical-only detail reported the exact revision reference; protected CSV preview rendered its content through the exact historical route.
- On-demand integrity reported Ready with matching actual size and SHA-256.
- Soft-deleted owner showed a tombstone, no current owner link, and an exact historical download route.
- Referenced attachments exposed no permanent-delete action.
- Editor and Viewer had no administration navigation and direct access resolved to `/forbidden`.
- With the requester's explicit authorization, a task-owned orphan was permanently deleted through the real confirmation flow. Its row, metadata, and binary disappeared, while referenced items remained.
- A task-owned orphan binary was intentionally locked to produce a real storage deletion failure. The attachment remained `DeletePending`, the UI exposed the retry action, and retry succeeded after releasing the exact task process lock.
- Final fixture state contained four referenced attachments, zero orphans, and zero DeletePending attachments.
- Fresh browser console after the radio registration correction: `0` errors and `0` warnings.

## SQLite and Storage Safety

Repository-owned SQLite baseline and final state were identical:

```text
Path: src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db
Length: 950272 bytes
LastWriteTimeUtc: 2026-08-29T05:58:49.2454191Z
SHA-256: AF0509630E229801735361AF257CEBD1B4C11947D9A98E8E0358E00F676B664D
db-wal: absent before and after
db-shm: absent before and after
```

Repository-owned attachment storage was also identical:

```text
Files: 20 before / 20 after
Bytes: 3835039 before / 3835039 after
Manifest SHA-256: 74751AACDFCF19E5648A8439456E4681038389A56037EA780381796CF39C79FF before / after
```

Therefore:

```text
Repository DB: UNCHANGED
Repository Attachment Storage: UNCHANGED
```

## Cleanup

- Stopped only the isolated ATTACH-B04 API and Vite process tree created for this task.
- Released ports `18440` and `18441`.
- Released the exact task-owned file lock used by the DeletePending scenario.
- Removed `.tmp/attach-b04-runtime` and `.tmp/attach-b04-verification` after evidence capture.
- Task-owned staging residue: `0`.
- Browser tabs created for verification were closed and the browser viewport was reset.

```text
Storage Cleanup: PASS
Runtime Cleanup: PASS
```

## Files Changed

Backend:

- `src/SystemKnowledgeHub.Api/Features/Attachments/Api/AdministratorAttachmentsController.cs`
- `src/SystemKnowledgeHub.Api/Features/Attachments/Application/AdministratorAttachmentQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/Attachments/Application/AttachmentReferenceService.cs`
- `src/SystemKnowledgeHub.Api/Features/Attachments/Application/AttachmentService.cs`
- `src/SystemKnowledgeHub.Api/Features/Attachments/Application/AttachmentStorage.cs`
- `src/SystemKnowledgeHub.Api/Features/Attachments/Application/Models/AdministratorAttachmentModels.cs`
- `src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Api/KnowledgeDocumentsController.cs`
- `src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Application/KnowledgeDocumentService.cs`
- `src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Application/Models/KnowledgeDocumentModels.cs`
- `src/SystemKnowledgeHub.Api/Program.cs`

Frontend:

- `src/SystemKnowledgeHub.Web/src/app/router/navigation.ts`
- `src/SystemKnowledgeHub.Web/src/app/router/routes.ts`
- `src/SystemKnowledgeHub.Web/src/layouts/DrawerHost.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/AppSidebar.attachments.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/api/administratorAttachmentContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/api/administratorAttachmentContracts.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/api/administratorAttachmentsApi.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/api/administratorAttachmentsApi.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/composables/useAdministratorAttachments.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/components/AdministratorAttachmentDetailDrawer.vue`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/components/AdministratorAttachmentDetailDrawer.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/pages/AdministratorAttachmentsView.vue`
- `src/SystemKnowledgeHub.Web/src/features/attachment-administration/attachment-administration.css`

Tests and documentation:

- `tests/SystemKnowledgeHub.Api.Tests/Api/AdministratorAttachmentsApiTests.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Api/AttachmentFoundationApiTests.cs`
- `docs/reports/ATTACH_B04_ADMINISTRATOR_ATTACHMENT_MANAGEMENT_VERIFICATION_REPORT.md`
- `docs/DOCUMENT_INDEX.md`

## Existing and New Gaps

Existing gaps/advisories:

- `REV-GAP-011` remains the known low-severity default parallel backend test-runner stall. The repository-approved deterministic serial full gate passed 192/192 and this task does not alter that gap's scope.
- The existing Vite chunk-size advisory remains informational and unchanged.

New gaps:

- None.

## ATTACH-VERIFY Readiness

All applicable ATTACH-B04 implementation, security, authorization, deletion-race, storage-failure/retry, accessibility, responsive, automated, real-browser, persistent-data-safety, and cleanup gates passed.

```text
ATTACH-VERIFY READY: YES
```

ATTACH-VERIFY was not started.
