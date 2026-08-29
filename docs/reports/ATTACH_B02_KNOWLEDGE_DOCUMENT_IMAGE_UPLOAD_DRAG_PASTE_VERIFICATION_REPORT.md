# ATTACH-B02 — KnowledgeDocument Image Upload / Drag / Paste Verification Report

## Result

```text
ATTACH-B02 PASS
ATTACH-B03 READY: YES
```

Corrective updates completed on 2026-08-29. The first real Windows finding showed that `照片.jpg1.jpg` succeeded through File Picker but failed through Explorer Drag and that real clipboard image paste also failed; Picker, Drag and Paste now share one filename/MIME normalization, lightweight validation and upload-queue path. A later reported `PNG 文件头无效` was not binary corruption: the exact file named `微信截图_20260201110642.png` begins with JPEG/JFIF bytes `FF D8 FF E0`, ends with `FF D9`, and has no PNG signature. Its 415 rejection is correct. The backend stream boundary was still hardened to remove request-body rewind/manual multipart reparse, reuse the antiforgery-cached bounded `IFormFile`, and fail closed if `IFormFile.Length` differs from staging `SizeBytes`.

Focused automated gates and post-fix isolated File Picker/synthetic binary Clipboard checks pass. The requester subsequently confirmed that the corrected Windows Explorer Drag and real screenshot `Ctrl+V` scenarios also pass. The required human evidence is therefore complete and `ATTACH-B02-GAP-001` is closed.

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
- Backend extension, declared MIME, signature and size validation remains unchanged and authoritative. A JPEG binary mislabeled `.png` is not treated as a valid PNG merely because Windows can open it.

No design delta was required.

## Binary / Stream Investigation

The exact reported file and two known-valid controls were inspected before upload and after task-owned storage:

| Input | Original first bytes | Length | SHA-256 | Result |
| --- | --- | ---: | --- | --- |
| Valid PNG control | `89 50 4E 47 0D 0A 1A 0A` | 1,153,469 | `6EC4075693CF3E58B1B7FB301BD96572516CBC8956937CAAC0AF0B6EBBFBE849` | 201; stored bytes identical |
| Reported `微信截图_20260201110642.png` | `FF D8 FF E0 00 10 4A 46 49 46` | 229,832 | `C49F3E3566C02BECAAC77650E211E3D2117DECAD31E2DB5FF1A486FD822218B4` | Correct 415 as mislabeled JPEG |
| Same reported bytes with `.jpg` filename | `FF D8 FF E0 00 10 4A 46 49 46` | 229,832 | `C49F3E3566C02BECAAC77650E211E3D2117DECAD31E2DB5FF1A486FD822218B4` | 201; stored bytes identical |
| Clipboard PNG control | `89 50 4E 47 0D 0A 1A 0A` | 68 | `431CED6916A2A21A156E38701AFE55BBD7F88969FBBFC56D7FE099D47F265460` | 201; stored bytes identical |

The valid PNG and correctly named JPEG were each uploaded before and after the backend hardening. Both object pairs have the same length, first 24 bytes and SHA-256 as their originals. The clipboard PNG was also byte-identical. Task-owned staging residue was `0`.

The controller no longer reads raw `Request.Body` with a second `MultipartReader`. `FormOptions` supplies the bounded parse used by antiforgery; the controller obtains exactly one `file` from cached `Request.ReadFormAsync`, opens its `IFormFile` stream once, and staging verifies the written length against `IFormFile.Length`. Signature validation remains unchanged.

## File Picker

- The existing Markdown toolbar now exposes an accessible `插入图片` button and hidden multiple file input.
- Create-dialog editors without a persisted document ID keep the button disabled with the explanation `创建草稿后可插入图片`.
- Selected images upload sequentially through the B01 multipart endpoint; token insertion occurs only after a valid typed 201 response.
- The button is disabled while a batch is active, duplicate starts are rejected, the source editor remains usable, and semantic Save is blocked while an upload is pending.
- Multi-image successes remain inserted in selection order. A failed item contributes no token and does not relabel earlier successes as failures.
- The common filename parser uses the final suffix (`lastIndexOf('.')`), so `照片.jpg1.jpg` is evaluated as `.jpg`, not `.jpg1`.
- Post-fix isolated browser File Picker uploaded a real 1,153,469-byte PNG and the exact reported 229,832 JPEG bytes under the correct `.jpg` filename. Stored lengths, first 24 bytes and SHA-256 match each source.

## Drag & Drop

- The editor recognizes actual file drags through a case-insensitive `DataTransfer.types` check plus `items` and `files` fallbacks; normal text/Markdown drag behavior is not prevented.
- File drag enter/over/leave/drop state has a non-color-only overlay and status text.
- Multiple dropped images are processed sequentially and inserted in drop order. Unsupported items are reported without corrupting source content.
- Picker and Drag no longer apply different MIME rules: both pass the raw file through the same final-suffix/MIME normalization before the same upload queue. A browser-specific/non-standard JPEG MIME such as `image/jpg` does not block a final `.jpg` file from reaching authoritative backend validation.
- Focused Vitest covers `照片.jpg1.jpg`, a browser MIME variant, file drop, multi-image ordering/partial failure, unsupported input and non-file drag preservation.
- The requester completed the post-fix real Windows Explorer Drag check and confirmed it passes.

## Clipboard Paste

- Clipboard file items enter the same normalization and upload path as Picker and Drag. The normalizer uses `ClipboardItem.type` as a MIME hint when the returned `File.type` is empty.
- An empty or extensionless clipboard filename with approved PNG/JPEG/GIF/WEBP MIME is rebuilt as `截图-YYYYMMDD-HHMMSS.<mime-extension>`; it does not need an original valid filename.
- When any approved image exists, images take deterministic priority over simultaneous clipboard text.
- If no image exists, the handler does not prevent default and ordinary text/Markdown paste remains native CodeMirror behavior.
- Upload failures leave the editor and existing Markdown intact.
- Post-fix isolated browser `Ctrl+V` with a real valid 68-byte PNG clipboard payload produced a 201 and token; its stored bytes and SHA-256 match the clipboard payload. This verifies the application path but is not a substitute for the requested real Windows screenshot paste.
- The requester completed the post-fix real screenshot `Ctrl+V` check and confirmed it passes.

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

The editor maps 413, 415, 401/403, 404, 409, 422, 503/507 and network failures to actionable Chinese messages. Failures do not insert a token, clear existing Markdown, close the editor or scroll to the page top. A real unsupported SVG selection verified unchanged body and `scrollY = 0` before/after. The exact mislabeled `.png` JPEG remains safely rejected with no token and no storage object. Focused tests cover oversize, unsupported type, request failure, paste failure, loading and double-submit.

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

Corrective backend stream boundary and multipart regression:

- `src/SystemKnowledgeHub.Api/Program.cs`
- `src/SystemKnowledgeHub.Api/Features/Attachments/Api/KnowledgeDocumentAttachmentsController.cs`
- `src/SystemKnowledgeHub.Api/Features/Attachments/Application/AttachmentService.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Api/AttachmentFoundationApiTests.cs`
- `tests/SystemKnowledgeHub.Api.Tests/TestSupport/BootstrapWebApplicationFactory.cs`
- `tests/SystemKnowledgeHub.Api.Tests/TestSupport/MultipartUploadCapture.cs`

Documentation:

- `docs/reports/ATTACH_B01_ATTACHMENT_METADATA_FILE_STORAGE_SECURE_DOWNLOAD_VERIFICATION_REPORT.md`
- `docs/reports/ATTACH_B02_KNOWLEDGE_DOCUMENT_IMAGE_UPLOAD_DRAG_PASTE_VERIFICATION_REPORT.md`
- `docs/DOCUMENT_INDEX.md`

## Automated Tests

| Gate | Result |
| --- | --- |
| `npm run type-check` | PASS |
| `npm run build` | PASS — Vite emitted its existing large-chunk advisory only |
| Affected ESLint files | PASS — 0 errors, 0 warnings |
| Affected Prettier check | PASS |
| Corrective focused Vitest | PASS — 9 files, 131 tests; editor suite 16/16 including byte equality for Picker/Drag/Paste |
| Original ATTACH-B02 focused Vitest aggregate | PASS — 12 files, 146 tests |
| Full `npm test` diagnostic | KNOWN BASELINE DEVIATION — 53/54 files and 335/336 tests; only the pre-existing unrelated `AppShell.spec.ts` stale `关系与缺口` assertion failed |
| `dotnet build SystemKnowledgeHub.sln -c Release --no-restore` | PASS — 0 warnings, 0 errors |
| `AttachmentFoundationApiTests` | PASS — 9/9 |
| Real PNG/JPEG multipart regression | PASS — request/form/staging prefix, length, SHA-256 and full byte equality |

Focused frontend coverage includes Picker and Drag `照片.jpg1.jpg`, final-suffix parsing, a Drag `image/jpg` MIME variant, byte-for-byte preservation into the common upload function, empty-name PNG clipboard input, empty-name/File-MIME JPEG input using `ClipboardItem.type`, ordinary text paste, unsupported/413/network errors, pending/double-submit, token insertion/cursor/alt preservation, sequential multi-image partial failure, file/non-file drag, Blob cleanup, exact current/historical routes, unavailable fallback, no path exposure, pending-save guard, restore attachment-set equality and raw compare fixtures.

B01 focused backend coverage remains authoritative for Editor/Viewer upload role bounds, whitelist/signature/size validation, orphan unreadability, wrong owner/kind/reference rejection, semantic image revision creation/removal, exact historical delivery, restore snapshots, corruption failure and soft-delete history. The new regression records:

| Fixture | Request `Content-Length` | `IFormFile.Length` | staging `SizeBytes` | Original = `IFormFile` = staging first 24 bytes | SHA-256 |
| --- | ---: | ---: | ---: | --- | --- |
| Valid PNG | 366 | 68 | 68 | `89 50 4E 47 0D 0A 1A 0A 00 00 00 0D 49 48 44 52 00 00 00 01 00 00 00 01` | `431CED6916A2A21A156E38701AFE55BBD7F88969FBBFC56D7FE099D47F265460` |
| Valid JPEG | 994 | 695 | 695 | `FF D8 FF E0 00 10 4A 46 49 46 00 01 01 01 00 60 00 60 00 00 FF DB 00 43` | `5518D9ADEE1D3DB049900AE8F8829E9A57E0F4DC7E0E27D7386D3AB405BC0CE7` |

The captured `IFormFile` first 24 bytes and atomically moved staging object's first 24 bytes equal each original; full stored byte arrays and SHA-256 also match. A JPEG fixture mislabeled `.png` / `image/png` remains 415.

## Browser Verification

Isolated Release API + Vite runtime used local authentication and task-owned persistence.

| Scenario | Evidence | Result |
| --- | --- | --- |
| File Picker | Post-fix isolated browser uploaded a real valid PNG and the reported JPEG bytes with correct `.jpg`; token insertion and exact stored length/hash passed | PASS |
| Drag | Shared normalization/byte-preservation tests pass; requester confirmed the corrected Windows Explorer Drag scenario | PASS — automated + human |
| Paste | Isolated browser binary clipboard PNG upload/token/storage equality passed; requester confirmed real screenshot `Ctrl+V` | PASS — automated + human |
| Revision | Removed attachment 1 in Revision 5; Revision 4 retained it through exact historical URL; raw Compare showed token diff | PASS |
| Restore | Revision 4 restored as Revision 6; three exact image references returned to current head | PASS |
| Permission | Editor upload passed; Viewer Draft showed images with no Edit/Insert Image; Archived showed no editor; soft-deleted historical read passed | PASS |
| Error | Real SVG rejection plus exact mislabeled `.png` JPEG 415: body usable, no token/object, signature rule preserved | PASS |
| Responsive | 1440×900 and 1280×720 toolbar/body/image measurements | PASS |

The post-fix clean `localhost` browser session reported 0 console errors. Its clipboard payload was delivered through the browser clipboard and real `Ctrl+V`; the requester then supplied the separate real-Windows confirmation required for Explorer Drag and screenshot Paste.

## SQLite / Storage Safety

The original isolated B02 verification left the repository database unchanged at 897,024 bytes with SHA-256 `D3E04257042DD7E93FE3D11AFE2A1C75B9B3CAB8FCDCBA1D39D739E7E975BE5C` and no repository attachment root. Subsequent requester/manual runs intentionally used the repository runtime. At the start of this binary investigation that pre-existing state was:

| Item | Binary-cycle baseline |
| --- | --- |
| SQLite size | 950,272 bytes |
| Last write UTC | `2026-08-29T03:37:50.6488056Z` |
| SHA-256 | `E3FD7E684B98F91E8549B2E44F2BE1B059B8EA8862DBE0D3C4FD8B9EFBF5A784` |
| repository `-wal` / `-shm` | absent / absent |
| repository attachment root | 5 files, 499,404 bytes; newest write `2026-08-29T03:32:22.6966620Z` |

All agent reproduction/browser writes in this cycle used the exact task-owned root `skh-attach-binary-20260829`, not repository persistence. A pre-existing user-started API process remained active during the corrective cycle and the requester subsequently completed human verification. Final repository state after that confirmation was SQLite size 950,272 bytes, last write `2026-08-29T05:58:49.2454191Z`, SHA-256 `AF0509630E229801735361AF257CEBD1B4C11947D9A98E8E0358E00F676B664D`, no WAL/SHM, and 20 attachment files totaling 3,835,039 bytes with newest write `2026-08-29T05:53:22.8514401Z`. These changes are therefore reported as `Repository DB: CHANGED` for the wall-clock cycle, while the agent's isolated runtime configuration did not target or mutate that database. The user-owned process, database and attachment objects were preserved without reset or deletion.

## Cleanup

- Agent-started API, both task-owned Vite processes and two orphaned focused-test hosts were stopped; isolated ports 5297 and 5273 were released.
- The agent-created browser tab was closed.
- The exact task-owned SQLite, WAL/SHM, Data Protection keys, storage objects/staging directories, logs and image fixture copy under `skh-attach-binary-20260829` were removed; the root no longer exists.
- Task-owned staging residue was `0` before cleanup.
- The pre-existing user-started API/Vite processes and concurrently changing repository SQLite/storage were not stopped, deleted or reset.

## Existing Gaps

- The full Vitest suite retains the repeatedly documented, unrelated `AppShell.spec.ts` stale `关系与缺口` expectation. All 146 focused ATTACH-B02 tests pass.
- Existing `REV-GAP-011` remains unchanged. The corrective backend change is confined to multipart form consumption and staging-length verification; Release build, the byte-integrity regression and all 9 attachment foundation tests passed, so the known-slow broad serial backend gate was not repeated.
- The ATTACH-A01 Internal Pilot malware-scanning limitation and SEC-04 real-Production deployment boundary remain unchanged.

## Closed Corrective Gap

### ATTACH-B02-GAP-001 — Corrected real Windows Drag / Paste human verification

- Final status: CLOSED on 2026-08-29 by requester human confirmation.
- Product implementation evidence: the three entry points share one normalization/validation/upload path; byte-preservation assertions pass for Picker/Drag/Paste; the real multipart PNG/JPEG regression and 9/9 attachment suite pass; isolated File Picker and browser-clipboard bytes reach storage unchanged.
- Human evidence: the requester confirmed that post-fix Windows Explorer Drag and real screenshot `Ctrl+V` both pass.
- Security clarification: the mislabeled `微信截图_20260201110642.png` is not a valid PNG fixture and correctly continues to fail unless renamed `.jpg` or re-exported as a real PNG.

No new product-code Blocker or High defect was found.

## ATTACH-B03 Readiness

All applicable implementation, automated, isolated-browser and requester human-verification gates are complete. ATTACH-B03 was not started in this task.

```text
ATTACH-B03 READY: YES
```
