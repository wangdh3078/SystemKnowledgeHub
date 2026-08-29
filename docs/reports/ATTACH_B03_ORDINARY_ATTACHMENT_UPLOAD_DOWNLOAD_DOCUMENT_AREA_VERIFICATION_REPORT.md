# ATTACH-B03 — Ordinary Attachment Upload / Download / Document Area Verification Report

## Result

```text
ATTACH-B03 PASS
ATTACH-B03-PREVIEW READY: YES
```

ATTACH-B03 was completed and verified on 2026-08-29. KnowledgeDocument now has an ordinary-file attachment area for multi-file upload, full desired-set editing, reference-only removal, secure current/historical download, immutable revision history, restore compatibility and attachment-set comparison. The implementation does not add a preview viewer, physical delete, orphan cleanup or Attachment Administration.

No frozen design or Golden UI asset was changed. No production backend code or database contract was changed. The existing ATTACH-B01 upload, validation, secure delivery, semantic-save and exact revision-reference boundaries remain authoritative.

## Scope

Implemented only the B03 ordinary-attachment slice:

- PDF, DOCX, XLSX, PPTX, TXT, LOG, SQL, MD, CSV, JSON, XML and ZIP selection;
- sequential multiple upload with partial-success preservation;
- complete `fileAttachmentIds` desired-set ownership in the edit session;
- current Detail and historical Revision attachment areas;
- exact protected current/revision download URLs;
- explicit reference-only removal and semantic save;
- restore and deterministic attachment-set Compare presentation;
- Viewer, Editor/Administrator, Archived and soft-delete boundaries;
- focused automated and isolated real-browser verification.

Image attachments remain in B02 Markdown `attachment:<id>` rendering and are filtered out of the ordinary attachment area. B03-PREVIEW, file viewers, physical deletion, orphan cleanup, global attachment management/search and cloud storage remain out of scope.

## ATTACH-A01 / A02 / B01 / B02 Compliance

- `Upload != Attach` is preserved. A successful upload creates a Ready orphan; it enters a revision only when the document semantic save submits the complete ordinary-file ID set.
- The client submits one complete `fileAttachmentIds` set. It does not call low-level add/delete reference endpoints and does not derive image references from that set.
- Frontend extension handling is a lightweight UX boundary. The ATTACH-B01 backend continues to enforce extension, declared MIME, signature, size, owner, Kind and StorageState.
- Browser MIME variants are normalized to the frozen canonical MIME for the file's final suffix before multipart submission without changing file bytes. This lets normal browser/Windows MIME labels reach authoritative backend validation; it does not weaken signature or size validation.
- Current and historical downloads use exact B01 document contexts. No StorageKey, filesystem path, internal Version, public static URL or binary content is exposed by the UI.
- Removal changes only the next revision reference set. Metadata and binary remain, and older revisions keep their exact references.
- A02 `previewMode` / `canPreview` metadata is presented without a non-functional Preview button. No viewer was implemented.
- B02 images continue to use Markdown tokens and protected image content routes and are not duplicated in the ordinary file set.

No design delta was required.

## Upload / Multiple Upload

- Edit mode exposes an accessible `添加附件` button and multiple file input with the frozen ordinary-file suffix list.
- Files upload sequentially through the B01 multipart endpoint. A single failure does not roll back earlier successful files or mislabel them as failed.
- The upload response must be `Kind = File`; an image response is rejected from this area with direction to use `插入图片`.
- Upload state announces the current item and batch position. The add/remove controls reject duplicate starts while a batch is pending, while the Markdown editor remains usable.
- Pending ordinary upload is part of the page's save/unload guard, so a half-finished desired set cannot be semantically saved.
- A browser-provided ZIP MIME of `application/x-zip-compressed` was the only real-browser integration finding. The common normalizer now sends the frozen `application/zip` declaration while retaining the original 172 bytes; backend validation and stored SHA-256 passed.

## Desired Attachment Set

- Edit initialization filters the current revision's metadata to `Kind = File` and preserves its ordered Attachment IDs.
- Each successful ordinary upload appends one non-duplicate metadata item to the desired set.
- Confirmed removal deletes only that ID from the desired set.
- Dirty-state comparison includes the ordinary Attachment ID set, so attachment-only edits require semantic save.
- The save request always contains the complete target `fileAttachmentIds`; image IDs are excluded.
- Focused tests cover initialization, add/remove, empty set, complete save payload, image exclusion and duplicate prevention.

## Current Detail

Current Detail renders only current-revision `Kind = File` references. Each item shows normalized filename, extension/type label, human-readable size, preview capability and an exact protected download link. Empty current sets show `暂无附件`. Long names use ellipsis/title fallback without expanding the page width.

The isolated browser saved PDF, XLSX and ZIP as Revision 2, removed PDF in Revision 3, and restored it in Revision 4. The current attachment area matched each head revision.

## Download

Current links use:

```text
/api/knowledge-documents/{documentId}/attachments/{attachmentId}/download
```

Historical links use:

```text
/api/knowledge-documents/{documentId}/revisions/{revisionNumber}/attachments/{attachmentId}/download
```

The isolated browser exercised PDF, XLSX and ZIP current downloads and the removed PDF's historical download. Filename/type presentation remained correct, and a Viewer retained download visibility without edit controls. Task-owned stored objects were byte-for-byte identical by length and SHA-256 to the source fixtures.

## Revision / Restore

- Revision Detail passes the exact immutable revision number to the attachment area; a later head removal does not change old links or metadata.
- Revision Compare retains raw Markdown source diff and adds a separate deterministic Attachment ID + Kind set summary (`added`, `removed`, `unchanged`). It performs no hash merge or binary diff; different IDs with the same hash remain different references.
- Restore remains backend snapshot restoration. Restoring Revision 2 created Revision 4 with the PDF/XLSX/ZIP reference set again, without copying binary or creating new Attachment metadata.
- B01 focused backend coverage continues to prove current removal/historical retention, exact historical authorization, soft-deleted historical reads, full reference snapshot restore and missing/corrupt fail-closed behavior.

## Preview Capability Presentation

The component consumes backend `previewMode` and `canPreview` metadata:

- PDF: `支持PDF预览（预览功能将在下一阶段提供）`;
- TXT/LOG/SQL/JSON/XML: Text capability;
- MD: Markdown capability;
- CSV: CSV capability;
- XLSX: Spreadsheet capability;
- DOCX/PPTX/ZIP: `仅支持下载`.

No Preview button or viewer route was added, so the UI contains no dead interaction. Full viewing remains owned by ATTACH-B03-PREVIEW.

## Authorization / Lifecycle

- Viewer: current Detail showed all three downloadable files and Revision History remained available; no Edit, Add or Remove control was present.
- Editor/Administrator: upload and reference removal are available only while the existing KnowledgeDocument state permits edit.
- Archived: current files remained readable/downloadable, with no Edit, Add or Remove control. The document was returned to Draft within the disposable runtime before the Viewer check.
- Soft deleted: current access remains unavailable; approved historical revision reads and downloads continue through exact revision context, as covered by B01 backend and focused Revision UI tests.
- Backend authorization remains final. No global Attachment Administration UI was introduced.

## Error UX

The attachment area maps 413, 415, 401/403, 404, 409, 422, 503/507 and network failures to readable Chinese messages. A failed item does not enter the desired set, remove existing files, clear Markdown, close the editor or navigate to the page top.

The isolated browser uploaded a real invalid-signature `.pdf`. Backend returned the B01 signature rejection, the invalid item was absent, the three valid attachments and body remained intact, Save stayed clean/disabled, and no storage object or staging residue was created.

No error message exposes StorageKey or a physical path.

## Accessibility / Responsive

- Add Attachment is keyboard-operable and has an accessible name; download and remove controls include the filename in their accessible names.
- Upload/progress uses `role=status` and `aria-live`; batch errors use `role=alert`; success/failure does not rely only on color.
- Removal uses the existing Element Plus confirmation overlay and explicitly says that history and the file remain.
- At requested 1440×900, the effective content viewport was 1384×865; body and attachment area had equal client/scroll widths and every item remained within its container.
- At requested 1280×720, the effective content viewport was 1231×692; body width was 1231 and the attachment area width was 976 with no item overflow.
- Long filename identity and action groups wrap/ellipsis safely at constrained widths.

## Files Changed

Frontend implementation:

- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/knowledgeDocumentAttachmentsApi.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/knowledgeDocumentContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentRevisionHistory.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/RevisionCompareView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/compare/revisionCompare.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/editor/documentEditState.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/knowledge-documents.css`

Focused tests were added or updated beside the typed attachment client, attachment area, detail page, revision history, revision compare and edit-state modules.

Documentation:

- `docs/reports/ATTACH_B03_ORDINARY_ATTACHMENT_UPLOAD_DOWNLOAD_DOCUMENT_AREA_VERIFICATION_REPORT.md`
- `docs/DOCUMENT_INDEX.md`

No backend production file, migration or frozen document changed.

## Automated Verification

| Gate                                                          | Result                                                     |
| ------------------------------------------------------------- | ---------------------------------------------------------- |
| `npm run type-check`                                          | PASS                                                       |
| `npm run build`                                               | PASS — Vite emitted its existing large-chunk advisory only |
| Affected ESLint                                               | PASS — 0 errors                                            |
| Affected Prettier check                                       | PASS                                                       |
| Focused Vitest                                                | PASS — 8 files, 63 tests                                   |
| `dotnet build SystemKnowledgeHub.sln -c Release --no-restore` | PASS — 0 warnings, 0 errors                                |
| `AttachmentFoundationApiTests`                                | PASS — 9/9                                                 |

Focused frontend coverage includes single/multiple upload, partial failure and 413, unsupported final suffix, pending/double submit, duplicate ID prevention, byte-preserving browser MIME normalization, complete desired-set save, image exclusion, reference-only removal, empty/current/historical rendering, exact secure routes, Viewer/Archived/soft-delete presentation, preview capability, restore-aware attachment set comparison and same-hash/different-ID semantics.

Because production backend code and contracts were unchanged, the existing B01 Release build and 9 focused attachment/revision API tests were the proportionate backend gate. The known slow broad backend gate was not repeated.

## Browser Verification

An isolated Release API and Vite runtime used task-owned SQLite, Attachment StorageRoot, Data Protection keys and ports 5313/5324.

| Scenario              | Evidence                                                                                                                                | Result |
| --------------------- | --------------------------------------------------------------------------------------------------------------------------------------- | ------ |
| A — Upload + Save     | PDF and XLSX succeeded; ZIP initially exposed a browser MIME variant, succeeded after common normalization; Revision 2 showed all three | PASS   |
| B — Download          | PDF native download plus XLSX/ZIP protected downloads completed; historical PDF download also completed                                 | PASS   |
| C — Remove            | PDF reference removed and saved as Revision 3; current omitted it while Revision 2 retained it and its exact route                      | PASS   |
| D — Restore           | Revision 2 restored as Revision 4; PDF returned and storage still contained only the original three objects                             | PASS   |
| E — Viewer / Archived | Archived had no edit controls; Viewer showed/downloaded files with no Edit/Add/Remove and no management navigation                      | PASS   |
| F — Error             | Invalid-signature PDF rejected; body and three existing files remained, no invalid object/token/reference was added                     | PASS   |
| Responsive            | Requested 1440×900 and 1280×720 had no horizontal attachment overflow                                                                   | PASS   |

Revision Compare 3→4 displayed PDF as added and XLSX/ZIP as unchanged beside the unchanged raw Markdown comparison. The browser console reported 0 new errors.

## SQLite / Storage Safety

Repository persistence was baselined before verification and compared after cleanup:

| Item                                   | Before                                                             | After                          |
| -------------------------------------- | ------------------------------------------------------------------ | ------------------------------ |
| SQLite length                          | 950,272 bytes                                                      | 950,272 bytes                  |
| SQLite last write UTC                  | `2026-08-29T05:58:49.2454191Z`                                     | `2026-08-29T05:58:49.2454191Z` |
| SQLite SHA-256                         | `AF0509630E229801735361AF257CEBD1B4C11947D9A98E8E0358E00F676B664D` | same                           |
| repository WAL / SHM                   | absent / absent                                                    | absent / absent                |
| repository attachment storage          | 20 files, 3,835,039 bytes                                          | same                           |
| newest repository attachment write UTC | `2026-08-29T05:53:22.8514401Z`                                     | same                           |

Agent verification therefore left the repository database and attachment storage `UNCHANGED`.

The task-owned successful objects were:

| Fixture           | Length | SHA-256                                                            | Stored match |
| ----------------- | -----: | ------------------------------------------------------------------ | ------------ |
| `MES接口规范.pdf` |     64 | `0D0923B2B84448A8B5661F8445918A33811C5FB1B6DFBB3F05E53C7FB219595B` | exact        |
| `Equipment.xlsx`  |    562 | `6EEBB7E34C915FF0E0552B06A058BBBD06A6A879BB63E47A15A1A0F066622F12` | exact        |
| `Source.zip`      |    172 | `63C8693A7B0E7F36A63C3CD331CB8911D2EAACD3FF82AEDAF7B05B29F1AF8C5B` | exact        |

The invalid PDF had no stored object. Staging residue was `0` before cleanup.

## Cleanup

- The agent-created browser tab was closed and its temporary viewport override was reset.
- The exact task-owned API/Vite process tree was stopped; ports 5313 and 5324 have no listeners.
- Task-owned SQLite/WAL/SHM, Data Protection keys, fixtures, storage objects and staging data under `.tmp/attach-b03-runtime-20260829-1435` were deleted; the root no longer exists.
- The first graceful-stop attempt released both ports but left the API holding SQLite. The process was then identified by its exact 14:36 start time and repository Release binary path, stopped, and the remaining database files were removed.
- No pre-existing/user-started process or repository persistence was stopped, reset or deleted.

## Existing / New Gaps

- Existing ATTACH-A01 Internal Pilot malware-scanning limitation and SEC-04 real-Production deployment boundary remain unchanged.
- Existing `REV-GAP-011` and the unrelated full-frontend `AppShell.spec.ts` stale expectation remain unchanged; all focused B03 gates pass.
- No new Blocker or High issue was found.
- No new B03 product gap remains. Full content viewers are intentionally deferred to ATTACH-B03-PREVIEW by the frozen A02 sequence.

## ATTACH-B03-PREVIEW Readiness

All applicable implementation, focused backend/frontend, isolated-browser, persistent-data and cleanup gates pass. ATTACH-B03-PREVIEW was not started.

```text
ATTACH-B03-PREVIEW READY: YES
```
