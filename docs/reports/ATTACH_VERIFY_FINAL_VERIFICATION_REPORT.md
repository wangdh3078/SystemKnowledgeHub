# ATTACH-VERIFY Attachment End-to-End Final Verification Report

## Result

```text
ATTACH-VERIFY PASS
PHASE-ATTACHMENTS COMPLETE
NEXT ROADMAP PHASE READY: YES
```

The attachment phase passed its final cross-slice acceptance gate. ATTACH-B01 through ATTACH-B04 and ATTACH-A02 remain integrated without a new Blocker or High issue. The final gate combined source/contract audit, the approved deterministic backend suite, focused frontend coverage, an isolated real-browser master flow, task-owned SQLite/object-store reconciliation, and repository persistent-data comparison.

No frozen design source was modified and no new attachment capability was implemented.

## Scope and Authority

Authority reviewed for this gate:

- repository `AGENTS.md` and `docs/DOCUMENT_INDEX.md`;
- frozen `ATTACH-A01` architecture decision and `ATTACH-A02` preview amendment;
- final ATTACH-B01, B02, B03, B03-PREVIEW, and B04 verification reports;
- current Attachment, KnowledgeDocument, Revision, authorization, restore, soft-delete, preview, and administrator-governance implementation and tests on `main`.

The gate validates the delivered attachment phase only. It does not add new formats, DOCX/PPTX preview, OCR, cloud storage, automatic orphan cleanup, bulk deletion, recycle-bin behavior, attachment restore, retention, annotation, sharing, or media-library behavior.

## Cross-slice Audit

| Slice | Final evidence | Result |
| --- | --- | --- |
| ATTACH-B01 | Filesystem binary storage, SQLite metadata/reference rows, exact current/history routes, signature and byte-integrity tests, staging cleanup, SHA-256 reconciliation | PASS |
| ATTACH-B02 | Picker/paste authoring reverified in the isolated browser; Windows Explorer drag retains the requester-confirmed real B02 evidence and passed the unchanged focused drag normalization/upload regressions | PASS |
| ATTACH-B03 | Ten-file ordinary multi-upload, desired attachment set, current attachment area, reference-only removal, history, restore, and secure download contexts | PASS |
| ATTACH-B03-PREVIEW | Protected PDF/text/Markdown/CSV/XLSX preview matrix, download-only ZIP, exact history context, inert rendering, and limit behavior | PASS |
| ATTACH-B04 | Administrator list/search/filter/statistics/detail, reference classes, integrity, zero-reference delete, referenced-delete guard, and real DeletePending retry | PASS |

## Architecture Invariants

The final source and runtime audit confirmed:

```text
Binary -> filesystem
Metadata / Reference -> SQLite
Upload != Attach
Current attachment truth -> CurrentRevision -> AttachmentReference[]
Historical Revision -> immutable AttachmentReference snapshot
Remove Reference != Physical Delete
Physical Delete -> Administrator + zero references only
```

- Uploading and then cancelling edit left a Ready zero-reference orphan. Cancelling did not delete the binary.
- Removing image/PDF references created a new revision; the old revision and binary remained readable.
- Restoring revision 2 created revision 4 and reused the existing attachment metadata and binary objects.
- Every Attachment foreign key remains `DeleteBehavior.Restrict`; no cascade, force-delete, bulk-delete, or automatic orphan cleanup exists.
- No public/static attachment directory, bare attachment-content route, or second attachment ACL was found.
- Current/history reads use document plus exact revision context. Root soft delete did not enable a current navigation or attachment-restore bypass.

## Image Flow

The isolated browser used a Draft KnowledgeDocument and verified:

- File Picker uploaded a valid 68-byte PNG, inserted `![picker-real](attachment:1)`, rendered the edit preview, saved it, and rendered it in current detail.
- Clipboard paste wrote a real 695-byte JPEG clipboard item with no filename, uploaded it as normalized `clipboard.jpeg`, inserted `![截图](attachment:2)`, and rendered it before and after save.
- Ordinary text Ctrl+V remained native Markdown text paste and did not enter the image-upload path.
- SVG was rejected by the frontend image whitelist and was not uploaded.
- Invalid PDF signature and oversize ordinary-file failures did not alter the Markdown or desired attachment set.
- Historical image routes were exact, for example `/api/knowledge-documents/1/revisions/2/attachments/1/content`.
- Stored SHA-256 values matched the fixture binaries: PNG `431CED6916A2A21A156E38701AFE55BBD7F88969FBBFC56D7FE099D47F265460`; JPEG `5518D9ADEE1D3DB049900AE8F8829E9A57E0F4DC7E0E27D7386D3AB405BC0CE7`.
- The final store audit found no length/hash mismatch, confirming that browser multipart, antiforgery handling, staging, and committed objects preserved the accepted binary bytes.

Windows Explorer drag was not repeated through agent desktop control in this final run because that controller could not establish a trusted Chrome URL and stopped before input. This does not reopen B02: the requester previously confirmed the real Windows drag scenario, the B02 final report records that evidence, and the unchanged picker/drag/paste shared normalization and drag-focused regressions all passed in this gate.

No body stored base64, StorageKey, filesystem path, object URL, or public URL.

## Ordinary Attachment Flow

One real multi-select upload accepted ten files in order:

- PDF: `manual.pdf`;
- text family: `safe.txt`, `payload.json`, `payload.xml`, `query.sql`, `events.log`;
- CSV: `table.csv`;
- XLSX: `workbook.xlsx`;
- Markdown: `notes.md`;
- download-only: `download-only.zip`.

All ten entered the pending desired file set, stayed outside Markdown image tokens, were attached only by semantic save, and appeared in current detail with protected download actions. Removing `manual.pdf` affected only the next revision. Revision 2 retained its PDF reference and revision 4 restored it without copying binary or metadata.

Multi-file success, ordering, per-item failure, partial-failure preservation, wrong-kind rejection, owner validation, and pending-upload save behavior passed the focused frontend/backend regressions. The real browser separately exercised signature and size failures without corrupting editor state.

## Preview Matrix

| Type | Final verification | Result |
| --- | --- | --- |
| PDF | Current overlay, historical overlay, inline protected blob, exact historical download fallback | PASS |
| TXT | `<script>` displayed as inert text; no execution | PASS |
| JSON/XML/SQL/LOG | Metadata capability and typed protected text-preview paths; inert family regressions | PASS |
| Markdown | Shared safe Markdown renderer and raw-HTML-disabled regressions; exact protected context | PASS |
| CSV | Table rendered; runtime limit of 3 rows produced an explicit truncation message; `=1+1` remained display text | PASS |
| XLSX | `Data` default sheet, `Archive` sheet switch, cached value `2`, no formula-expression execution or display | PASS |
| ZIP | Presented as download-only with no preview action | PASS |

The preview implementation still has no uploaded-HTML trust, formula execution, Office automation, ZIP browsing/extraction, or client-side capability guessing. Missing/corrupt content remains fail closed and does not remove the download fallback.

## Revision, Compare, and Restore

The real master flow produced:

```text
Revision 1: initial document
Revision 2: PNG + clipboard JPEG + PDF + XLSX + other ordinary files
Revision 3: PNG and PDF removed; clipboard JPEG and XLSX retained
Revision 4: restore of Revision 2
```

- Revision 2 continued to render both historical images and preview/download `manual.pdf` after revision 3 removed them.
- Compare 2 -> 3 showed raw Markdown deletion of `![picker-real](attachment:1)` and attachment-set removals for Image #1 and File #3.
- Restore created a new Head revision, reused the original attachment IDs, and restored PNG, PDF, XLSX, and the complete revision-2 reference set.
- Immutable history did not change. The final database held 35 `AttachmentReference` rows across the exercised snapshots.
- Race, stale concurrency, invalid reference, wrong owner/kind, unavailable restore, and physical-delete/reference race cases passed backend coverage.

## Soft Delete Historical Boundary

After the task-owned owner document was soft deleted:

- current list/detail/edit/upload navigation became unavailable;
- `?view=history` displayed an explicit `已删除` tombstone and four immutable revisions;
- historical images rendered through revision-scoped content routes;
- historical PDF preview and all ordinary-file downloads remained revision scoped;
- no restore action or current owner navigation was exposed;
- binaries and metadata remained present because root soft delete does not physically delete attachments.

## Authorization Matrix

| Role/state | Observed result | Result |
| --- | --- | --- |
| Viewer | Historical image/file preview and download available; no edit, restore, upload, remove, or attachment administration; direct admin route resolved to `/forbidden` | PASS |
| Editor | Created and edited a Draft, uploaded an image, and cancelled to leave an orphan; no admin navigation and direct admin route forbidden | PASS |
| Administrator | Full document behavior plus attachment inventory, integrity, delete, and retry | PASS |
| Archived | Editor-created document was published then archived; no Edit or attachment-upload entry remained | PASS |
| Soft deleted | Current edit/upload unavailable; only approved historical boundary remained | PASS |

Backend authorization, antiforgery, cross-document context, and state checks passed the focused and full backend gates; button visibility is not the sole enforcement evidence.

## Administrator Management

The real browser verified:

- inventory, filename search, reference radio, extension filter, statistics, and detail drawer;
- full SHA-256, normalized metadata, storage state, owner/lifecycle, and soft-deleted owner tombstone;
- exact current, historical, total, and per-revision reference counts;
- historical-only attachment #14 reported `全部 1 · 当前 0 · 历史 1` and used `/api/knowledge-documents/3/revisions/2/attachments/14/download`;
- the soft-deleted owner's attachment #12 reported `全部 3 · 当前 1 · 历史 2` and selected the approved revision-4 route rather than a current route;
- no drawer text exposed StorageKey or a physical path;
- on-demand integrity for the 695-byte JPEG returned the exact expected size and SHA-256.

The final statistics correctly distinguished 13 referenced attachments as 12 current plus 1 historical-only, with 0 orphan and 0 DeletePending. Therefore `historical-only reference != orphan` held in both list and detail behavior.

## Physical Delete

- Referenced and historical-only details exposed no permanent-delete action. Backend tests returned `422` for current and historical references and rechecked a new-reference race in the deletion transaction.
- The task-owned 695-byte zero-reference orphan was deleted through the real confirmation. Its metadata and binary disappeared; referenced rows and history remained.
- A second task-owned orphan was locked with exclusive filesystem sharing. Delete returned the normalized storage failure, metadata remained `DeletePending`, statistics reported one pending item, and the detail exposed a single retry action.
- After releasing the exact task-owned lock, the confirmed retry removed metadata and binary. Final `DeletePending=0` and orphan count was zero.
- Stale-token behavior passed the backend `409` coverage. There is no force, cascade, or bulk alternative.

## Error Matrix

| Status | Evidence | Result |
| --- | --- | --- |
| 400 | Invalid query/body and empty upload focused API coverage | PASS |
| 401 | Anonymous protected preview/download focused API coverage | PASS |
| 403 | Real Viewer/Editor admin-route denial plus backend policy/antiforgery coverage | PASS |
| 404 | Real soft-deleted current route/list unavailability and missing-context coverage | PASS |
| 409 | Revision, lifecycle, archived upload, stale delete/retry concurrency coverage | PASS |
| 413 | Real 1.1 MB upload against a 1 MiB task limit; readable error, no desired-set mutation | PASS |
| 415 | Real invalid PDF signature and SVG whitelist rejection; MIME/signature mismatch coverage | PASS |
| 422 | Referenced physical delete, unsupported preview, wrong kind/owner/context coverage | PASS |
| 503/507 class | Real locked-object delete produced the normalized `503` DeletePending recovery path; storage-unavailable coverage passed | PASS |

Failures did not report success, create a partial semantic revision, disclose a path/StorageKey, remove historical references, or disable download fallback.

## Security Audit

- No `UseStaticFiles`/public attachment mapping or bare `/api/attachments` content route exists.
- API response contracts do not expose `StorageKey`; runtime pages/drawers contained no filesystem path.
- Current and historical content/download/preview routes require exact document/revision reference authorization.
- Uploaded HTML is never trusted. Text is inert and Markdown uses the existing safe renderer with raw HTML disabled.
- XLSX reads cached values only; no formula evaluation, macro support, Office automation, or ZIP browsing was introduced.
- Image tokens are attachment IDs only. The backend remains authoritative for extension, MIME, signature, size, ownership, kind, state, and reference validity.
- EF foreign keys remain restrictive; deletion cannot cascade into revisions/documents.

## Accessibility and Responsive

- Image picker, ordinary attachment picker, preview/download, sheet selector, filters, integrity, close, delete, and retry controls exposed readable accessible names.
- Upload/error/status, current/history/orphan/DeletePending, and success/failure states used text rather than color alone.
- Preview, drawer, lifecycle, reference-removal, restore, delete, and retry overlays exposed named dialog/region boundaries and working close/cancel controls.
- Real checks at 1440x900 and 1280x720 found document scroll width equal to client width and no page-level horizontal overflow.
- At the smaller viewport, historical images reported `max-width: 100%`, preserved aspect ratio, and did not cause horizontal scroll.
- The final browser console contained `0` errors and `0` warnings.

Cross-slice B02/B03/B03-PREVIEW/B04 reports and the focused component suite retain the detailed toolbar, attachment area, CSV/XLSX table, overlay, long filename, focus, and responsive evidence for the unchanged UI surfaces.

## Automated Verification

Backend:

```text
dotnet build SystemKnowledgeHub.sln -c Release --no-restore
PASS — 0 warnings, 0 errors

focused attachment/admin/revision/restore/authorization/antiforgery/history gate
PASS — 31 passed, 0 failed, 0 skipped

approved deterministic serial full backend gate
PASS — 192 passed, 0 failed, 0 skipped
```

The serial settings used `MaxCpuCount=1`, disabled xUnit collection parallelization, and set one maximum parallel thread, consistent with the approved `REV-GAP-011` workaround.

Frontend:

```text
npm run type-check
PASS

npm run build
PASS — existing Vite chunk-size advisory only

focused attachment/admin/editor/renderer/revision Vitest
PASS — 16 files, 171 tests

npm run lint
PASS
```

Diagnostic full frontend Vitest remained at the known unrelated baseline: 60 files passed, 1 `AppShell.spec.ts` file failed; 380 tests passed, 1 stale navigation-text assertion failed, with its existing Element Plus test-stub warnings. Attachment-focused tests all passed and no production runtime issue was observed.

## Browser Master Flow

The isolated runtime used:

```text
API:  http://127.0.0.1:18540
Web:  http://127.0.0.1:18541
SQLite: task-owned
Attachment StorageRoot: task-owned
Data Protection keys: task-owned
```

Completed flow:

```text
Admin login
-> create Draft
-> Picker PNG
-> paste JPEG clipboard image
-> upload PDF/TXT/CSV/XLSX/ZIP/MD/JSON/XML/SQL/LOG
-> edit preview and semantic save
-> current detail and protected previews/download contexts
-> remove PNG/PDF and save revision 3
-> revision 2 historical assets
-> raw Markdown and attachment-set compare
-> restore revision 2 as revision 4
-> soft delete owner
-> historical tombstone, image, preview, and download
-> Viewer/Editor/Administrator/Archived checks
-> admin inventory, filters, statistics, detail, and integrity
-> referenced/historical-only delete blocked
-> orphan permanent delete
-> real DeletePending and retry
```

Console result: `0 new errors`, `0 new warnings`.

## SQLite and Storage Integrity

Final task-owned runtime reconciliation:

```text
PRAGMA integrity_check: ok
PRAGMA foreign_key_check: 0 rows
Attachments: 13
AttachmentReferences: 35
Ready: 13
DeletePending: 0
Orphans: 0
Object files: 13
Staging files: 0
Metadata length mismatches: 0
Missing objects: 0
SHA-256 mismatches: 0
```

Metadata count, object count, byte lengths, and hashes were mutually consistent.

## Repository Persistent Data Safety

Repository SQLite before and after:

```text
Path: src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db
Length: 950272 bytes
LastWriteTimeUtc: 2026-08-29T05:58:49.2454191Z
SHA-256: AF0509630E229801735361AF257CEBD1B4C11947D9A98E8E0358E00F676B664D
db-wal: absent
db-shm: absent
```

Repository attachment storage before and after:

```text
Files: 20
Bytes: 3835039
Manifest SHA-256: 0B0A0612BD65772C3B4768046269B693D0F49ACDFE558CD7C53EA5300733A173
```

```text
Repository DB: UNCHANGED
Repository Attachment Storage: UNCHANGED
```

## Cleanup

- Closed the agent-created browser tab and reset the temporary viewport override.
- Stopped only the task-owned API/Vite process trees.
- Released ports 18540 and 18541.
- Released the exact task-owned exclusive file lock.
- Removed the task SQLite, Attachment StorageRoot, Data Protection keys, fixtures, logs, helper, and serial runsettings under `.tmp/attach-verify`.
- Confirmed final task staging residue was zero before removal.
- Confirmed no task-owned lock, runtime, browser tab, or listener remained.

```text
Storage Cleanup: PASS
Runtime Cleanup: PASS
```

## Files Changed

- `docs/reports/ATTACH_VERIFY_FINAL_VERIFICATION_REPORT.md`
- `docs/DOCUMENT_INDEX.md`

No implementation, migration, frozen design, package, or runtime-data file changed.

## Existing and New Gaps

Existing, unchanged:

- `REV-GAP-011` deterministic serial test-runner infrastructure condition;
- ATTACH-A01 Internal Pilot malware-scanning limitation;
- SEC-04 Production deployment boundary;
- existing Vite chunk-size advisory;
- existing unrelated full-frontend `AppShell.spec.ts` stale navigation assertion/test-stub warning baseline.

New gaps: none.

No unresolved Blocker or High issue remains in the attachment phase.

## Final Readiness

```text
ATTACH-VERIFY PASS
PHASE-ATTACHMENTS COMPLETE: YES
NEXT ROADMAP PHASE READY: YES
```

ATTACH-VERIFY is complete. This report does not start the next Major Phase.
