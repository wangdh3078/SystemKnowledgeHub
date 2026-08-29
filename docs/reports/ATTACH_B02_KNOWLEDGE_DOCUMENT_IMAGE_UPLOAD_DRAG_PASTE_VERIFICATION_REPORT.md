# ATTACH-B02 — KnowledgeDocument Image Upload / Drag / Paste Verification Report

## Result

```text
ATTACH-B02 PARTIAL
ATTACH-B03 READY: NO
```

Completed on 2026-08-29. The picker, drag/drop implementation, clipboard paste, protected rendering, revision snapshot, compare, restore, authorization, error and responsive behavior are implemented and their focused automated gates pass. The result remains `PARTIAL` because the required real-browser external-file Drag scenario could not be executed with the available browser control boundary. File drag/drop is covered by focused component tests, but that does not satisfy the task's explicit real-browser Scenario 2 PASS condition.

No frozen source or Golden UI asset was modified. ATTACH-B03 was not started.

## Scope

This slice adds only KnowledgeDocument Markdown image authoring and protected image rendering:

- image selection and sequential multi-image upload;
- real-file drag event handling and drag-over feedback;
- clipboard image/screenshot paste with text-paste preservation;
- frozen `![alt](attachment:<attachmentId>)` insertion;
- unsaved owner-local Blob preview and saved exact protected routes;
- current Detail, historical Revision Detail, raw Markdown Compare and Restore integration;
- pending upload, cancellation, failure, role and lifecycle UX;
- responsive image/toolbar behavior.

It does not add an ordinary attachment area, PDF/CSV/XLSX preview UI, Attachment Administration, media library, thumbnails, OCR, image editing, cloud storage or orphan cleanup.

## ATTACH-A01 / A02 / B01 Compliance

- `Upload != Attach` remains intact. Uploaded images are `Ready` orphans until an existing semantic content save parses their Markdown token and writes the new revision's exact `AttachmentReference` snapshot.
- The client submits no parallel `imageAttachmentIds`; the Markdown body remains authoritative for images.
- The client whitelist mirrors the B01 image set (PNG/JPEG/GIF/WEBP) only as an early UX filter. Backend extension/MIME/signature/size validation remains authoritative; SVG is not accepted.
- No filesystem path, StorageKey, base64 data URI, object URL or public static URL is persisted in Markdown.
- Unsaved preview follows the already frozen ATTACH-A01 boundary: the exact just-uploaded image uses an in-memory Blob URL, which is revoked on component disposal; no current/historical content route was weakened and no draft public route was added.
- Saved current images use the exact current document route. Historical images use exact `documentId + revisionNumber + attachmentId` routes.
- A02 ordinary-file preview scope was not implemented or changed.

No design delta was required.

## File Picker

- The existing Markdown toolbar now exposes an accessible `插入图片` button and hidden multiple file input.
- Create-dialog editors without a persisted document ID keep the button disabled with the explanation `创建草稿后可插入图片`.
- Selected images upload sequentially through the B01 multipart endpoint; token insertion occurs only after a valid typed 201 response.
- The button is disabled while a batch is active, duplicate starts are rejected, the source editor remains usable, and semantic Save is blocked while an upload is pending.
- Multi-image successes remain inserted in selection order. A failed item contributes no token and does not relabel earlier successes as failures.

## Drag & Drop

- The editor intercepts only drag payloads whose `DataTransfer.types` contains `Files`; normal text/Markdown drag behavior is not prevented.
- File drag enter/over/leave/drop state has a non-color-only overlay and status text.
- Multiple dropped images are processed sequentially and inserted in drop order. Unsupported items are reported without corrupting source content.
- Focused Vitest covers a file drop, multi-image ordering/partial failure, unsupported drop input and non-file drag preservation.
- Required real-browser external-file Drag was **not executed**. Browser file-chooser and clipboard primitives do not synthesize an OS file drag, browser page evaluation is read-only, and Windows automation is prohibited from controlling the Codex desktop surface. This is `ATTACH-B02-GAP-001` and blocks overall PASS.

## Clipboard Paste

- Clipboard items are inspected for image/file items. When any approved image exists, images take deterministic priority over simultaneous clipboard text.
- If no image exists, the handler does not prevent default and ordinary text/Markdown paste remains native CodeMirror behavior.
- Screenshot items without a useful filename receive a safe `截图-YYYYMMDD-HHMMSS.<ext>` upload name and default alt `截图`.
- Upload failures leave the editor and existing Markdown intact.

## Markdown Token

The only inserted persistent form is:

```markdown
![alt](attachment:<attachmentId>)
```

File picker/drop alt text derives from the normalized server filename without its extension. Clipboard images use `截图`. Markdown-sensitive `]` and backslash characters are escaped, and users can edit the alt text directly.

## Editor Preview

- Saved attachment IDs and current-session uploaded IDs are passed as an explicit image authorization context to the shared renderer.
- A just-uploaded orphan renders from its local Blob URL only in the current component instance.
- Saving/unmounting disposes the editor preview context and revokes Blob URLs; cancellation never physically deletes the uploaded orphan.
- Malformed, unapproved-context or unavailable attachment tokens become inert accessible placeholders rather than breaking the page.

## Current Detail

Current Detail passes the exact current document ID and current revision attachment metadata into the Markdown renderer. Authorized images resolve only to:

```text
/api/knowledge-documents/{documentId}/attachments/{attachmentId}/content
```

No response metadata field that could reveal StorageKey/path is used by the renderer.

## Revision History

Revision Detail passes the selected immutable revision number and that revision's exact attachment metadata. Images resolve only to:

```text
/api/knowledge-documents/{documentId}/revisions/{revisionNumber}/attachments/{attachmentId}/content
```

The isolated browser verified that an image removed from the current body remained visible in Revision 4, and that a soft-deleted owner still showed the approved Revision 6 images through exact historical routes while current detail returned 404.

## Compare

Compare remains a raw Markdown line diff. The isolated browser compared Revision 4 to Revision 5 and displayed literal added/removed `![...](attachment:<id>)` lines; no visual image diff or attachment fetch was introduced.

## Restore

- Restore continues to submit only the source revision, reason and current concurrency token.
- The restore dialog now treats the attachment ID set as part of snapshot equality, so text-equal revisions with different attachment references are not incorrectly declared identical.
- The isolated browser restored Revision 4 into new head Revision 6 and verified the restored current image set and protected current routes without copying binary or creating attachment metadata.

## Authorization and State

- Viewer: can read current and historical images but has no Edit or Insert Image control on an otherwise editable Draft.
- Editor: can upload and insert images into a Draft through the B01 Editor endpoint.
- Archived: no Edit or Insert Image control; the lifecycle remains the authoritative state boundary.
- Soft deleted: current detail is unavailable; approved historical revision image reads continue with an `已删除` tombstone and exact revision routes.
- Administrator follows existing Editor capability. No Attachment Administration UI was added.

## Error UX

The editor maps 413, 415, 401/403, 404, 409, 422, 503/507 and network failures to actionable Chinese messages. Failures do not insert a token, clear existing Markdown, close the editor or scroll to the page top. A real unsupported SVG selection verified unchanged body and `scrollY = 0` before/after. Focused tests cover oversize, unsupported type, request failure, paste failure, loading and double-submit.

No error text exposes a path or StorageKey.

## Accessibility / Responsive

- Insert Image has an accessible name and keyboard-operable file input path; drag/drop is not the only input method.
- Upload progress uses `role=status`; errors use an alert; success/failure is not color-only.
- The drag overlay is descriptive and non-interactive.
- Protected images have alt text and unavailable images expose a readable fallback.
- At 1440×900: body `scrollWidth == clientWidth == 1440`; toolbar `scrollWidth == clientWidth == 1167`; preview image max-width was `100%`.
- At 1280×720: body `scrollWidth == clientWidth == 1280`; toolbar `scrollWidth == clientWidth == 1023`; a 987 px image rendered at 974 px with `max-width: 100%` and retained aspect ratio.

## Files Changed

Frontend implementation:

- `src/SystemKnowledgeHub.Web/src/api/client/apiClient.ts`
- `src/SystemKnowledgeHub.Web/src/api/contracts/errors.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/attachmentContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/knowledgeDocumentAttachmentsApi.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/knowledgeDocumentContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/editor/KnowledgeDocumentEditor.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/markdown/renderMarkdown.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/markdown/KnowledgeDocumentMarkdown.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/markdown/knowledge-markdown-theme.css`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentRevisionHistory.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentRestoreDialogContent.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/knowledge-documents.css`

Focused tests and fixtures were updated beside these features, including the typed client/contracts, editor upload/drop/paste, renderer, Detail, Revision History, Compare, Restore and shared KnowledgeDocument fixtures.

Documentation:

- `docs/reports/ATTACH_B02_KNOWLEDGE_DOCUMENT_IMAGE_UPLOAD_DRAG_PASTE_VERIFICATION_REPORT.md`
- `docs/DOCUMENT_INDEX.md`

An unrelated concurrent `AGENTS.md` working-tree change was preserved and is not part of ATTACH-B02 delivery.

## Automated Tests

| Gate | Result |
| --- | --- |
| `npm run type-check` | PASS |
| `npm run build` | PASS — Vite emitted its existing large-chunk advisory only |
| Affected ESLint files | PASS — 0 errors, 0 warnings |
| Focused Vitest | PASS — 12 files, 143 tests |
| Full `npm test` diagnostic | KNOWN BASELINE DEVIATION — 53/54 files and 335/336 tests; only the pre-existing unrelated `AppShell.spec.ts` stale `关系与缺口` assertion failed |
| `dotnet build SystemKnowledgeHub.sln -c Release --no-restore` | PASS — 0 warnings, 0 errors |
| `AttachmentFoundationApiTests` | PASS — 8/8 |

Focused frontend coverage includes valid picker upload, unsupported/413/network errors, pending/double-submit, token insertion/cursor/alt preservation, sequential multi-image partial failure, file/non-file drag, clipboard image priority and ordinary text paste, Blob cleanup, exact current/historical routes, unavailable fallback, no path exposure, pending-save guard, restore attachment-set equality and raw compare fixtures.

B01 focused backend coverage remains authoritative for Editor/Viewer upload role bounds, whitelist/signature/size validation, orphan unreadability, wrong owner/kind/reference rejection, semantic image revision creation/removal, exact historical delivery, restore snapshots, corruption failure and soft-delete history.

## Browser Verification

Isolated Release API + Vite runtime used local authentication and task-owned persistence.

| Scenario | Evidence | Result |
| --- | --- | --- |
| File Picker | PNG upload → attachment 1 token → Blob preview → save → exact current route | PASS |
| Drag | External OS-file drag could not be produced by the allowed browser surface; focused component drag tests pass | NOT RUN — blocks PASS |
| Paste | Binary PNG clipboard item → `![截图](attachment:2)`; ordinary Markdown paste remained native; save/detail showed both | PASS |
| Revision | Removed attachment 1 in Revision 5; Revision 4 retained it through exact historical URL; raw Compare showed token diff | PASS |
| Restore | Revision 4 restored as Revision 6; three exact image references returned to current head | PASS |
| Permission | Editor upload passed; Viewer Draft showed images with no Edit/Insert Image; Archived showed no editor; soft-deleted historical read passed | PASS |
| Error | Real SVG selection: readable rejection, body unchanged, no token, no scroll movement | PASS |
| Responsive | 1440×900 and 1280×720 toolbar/body/image measurements | PASS |

The final clean `localhost` browser session reported 0 console errors. An earlier discarded setup tab recorded one antiforgery error after the API environment was deliberately restarted under the same `127.0.0.1` cookie origin; verification was repeated from a clean origin/session and the product scenarios above emitted 0 errors.

## SQLite / Storage Safety

Repository persistence baseline and final state:

| Item | Baseline | Final |
| --- | --- | --- |
| SQLite size | 897,024 bytes | 897,024 bytes |
| Last write UTC | `2026-08-28T14:04:07.3581128Z` | unchanged |
| SHA-256 | `D3E04257042DD7E93FE3D11AFE2A1C75B9B3CAB8FCDCBA1D39D739E7E975BE5C` | unchanged |
| repository `-wal` / `-shm` | absent / absent | absent / absent |
| repository attachment root | absent | absent |

Before cleanup, the task-owned runtime reported `integrity_check=ok`, 0 foreign-key violations, 4 attachment rows, 11 immutable reference rows, 4 storage objects and 0 staging files. The data set intentionally included one unsaved orphan to verify `Upload != Attach`.

## Cleanup

- Agent-started API and Vite processes were stopped.
- Isolated ports 5197 and 5173 were released.
- Agent-created browser tabs were closed and the temporary viewport override was reset.
- The exact task-owned SQLite, WAL/SHM, Data Protection keys, storage objects/staging directories and image fixtures under `skh-attach-b02-20260829T1045` were removed; the root no longer exists.
- Repository SQLite/storage remained unchanged.

## Existing Gaps

- The full Vitest suite retains the repeatedly documented, unrelated `AppShell.spec.ts` stale `关系与缺口` expectation. All 143 focused ATTACH-B02 tests pass.
- Existing `REV-GAP-011` remains unchanged; no broad backend gate was required because B02 made no backend change and the focused attachment foundation gate passed.
- The ATTACH-A01 Internal Pilot malware-scanning limitation and SEC-04 real-Production deployment boundary remain unchanged.

## New Gaps

### ATTACH-B02-GAP-001 — Required real-browser external-file Drag not executed

- Severity for task completion: Blocker.
- Product implementation evidence: focused drag/drop tests pass, including file detection, non-file preservation, ordering, rejection and partial failure.
- Missing evidence: an actual OS image dragged into the real browser editor, followed by upload, token insertion, save and protected rendering.
- Closure: run the required manual or automation-capable isolated browser Scenario 2 with a task-owned PNG and record 0 new console errors.

No new product-code Blocker or High defect was found.

## ATTACH-B03 Readiness

Implementation prerequisites for ATTACH-B03 are present, but the task's explicit PASS rule requires every applicable real-browser scenario. Until `ATTACH-B02-GAP-001` is closed:

```text
ATTACH-B03 READY: NO
```

