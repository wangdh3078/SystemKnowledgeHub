# ATTACH-A02 — Attachment Preview Capability Amendment

Status: **Frozen Design Amendment — Approved**

Effective task: **ATTACH-B01**

Amends: `ATTACH_A01_ATTACHMENT_IMAGE_REVISION_PERMISSION_STORAGE_ARCHITECTURE_DECISION.md`

## 1. Decision and Readiness

The confirmed product requirement adds a protected, read-only preview path for a bounded set of ordinary attachment types. This amendment supersedes only the ATTACH-A01 statement that PDF/Office preview is a non-goal and the affected delivery/task-sequencing clauses. All ATTACH-A01 decisions for attachment metadata, immutable revision references, authorization, storage, soft delete, physical deletion, and the Markdown image token remain frozen.

```text
ATTACH-B01 READY: YES
```

No unresolved product or architecture decision blocks implementation.

## 2. Product Presentation Policy

The server is authoritative for preview capability. Clients must render the three product states from returned metadata rather than infer capability from a filename:

```text
Image
→ Markdown inline rendering through attachment:<id>

Previewable ordinary file
→ metadata + Preview + Download

Download-only ordinary file
→ metadata + Download
```

Images are not ordinary-file previews. They continue to use the authenticated image content route defined by ATTACH-A01.

## 3. Frozen Preview Matrix

| Approved extension/kind | Canonical content type | `previewMode` | Preview delivery |
| --- | --- | --- | --- |
| PNG/JPEG/GIF/WEBP image | Canonical image MIME | `Image` | Existing protected image content route |
| PDF | `application/pdf` | `Pdf` | Protected inline PDF bytes |
| TXT/LOG/SQL | `text/plain` | `Text` | Bounded JSON text contract |
| JSON | `application/json` | `Text` | Bounded JSON text contract; content is data, never executed |
| XML | `application/xml` | `Text` | Bounded JSON text contract; content is data, never executed |
| Markdown | `text/markdown` | `Markdown` | Bounded JSON text contract; B01 does not render HTML |
| CSV | `text/csv` | `Csv` | Bounded structured rows in JSON |
| XLSX | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | `Spreadsheet` | Bounded workbook/sheet metadata and structured cells in JSON |
| DOCX/PPTX/ZIP | ATTACH-A01 canonical type | `None` | No preview; protected download remains available |

The frozen enum is:

```text
Image | Pdf | Text | Markdown | Csv | Spreadsheet | None
```

`canPreview` is derived as `previewMode != None`; `canDownload` is true only when the attachment is reachable through the exact requested current or historical revision context. Upload responses for unreferenced attachments therefore return `canDownload: false`.

## 4. Metadata Contract

Current-detail and revision-detail attachment projections contain at least:

- `attachmentId`
- `kind`
- `originalFileName`
- `extension`
- `contentType`
- `sizeBytes`
- lowercase hexadecimal `sha256`
- `previewMode`
- `canPreview`
- `canDownload`

Neither ordinary nor administrator APIs return `StorageKey`, a physical path, or a storage-root value.

## 5. Secure Route Boundary

All routes are authenticated, same-origin API routes. They locate bytes only from an attachment ID plus an exact document/revision authorization context.

Current revision:

```http
GET /api/knowledge-documents/{documentId}/attachments/{attachmentId}/content
GET /api/knowledge-documents/{documentId}/attachments/{attachmentId}/download
GET /api/knowledge-documents/{documentId}/attachments/{attachmentId}/preview
```

Historical revision:

```http
GET /api/knowledge-documents/{documentId}/revisions/{revisionNumber}/attachments/{attachmentId}/content
GET /api/knowledge-documents/{documentId}/revisions/{revisionNumber}/attachments/{attachmentId}/download
GET /api/knowledge-documents/{documentId}/revisions/{revisionNumber}/attachments/{attachmentId}/preview
```

Current routes require an active owner and a reference in its current revision. Historical routes require the approved historical owner boundary and a reference in that exact revision, including for a soft-deleted owner. Knowing an attachment ID alone is insufficient. Unreferenced uploads are not downloadable or previewable.

All byte responses set `X-Content-Type-Options: nosniff` and `Cache-Control: private, no-store`. Download uses safe `attachment` disposition. Image and PDF delivery use safe `inline` disposition with the canonical MIME. A request to `/preview` for `Image` or `None` returns `422 preview_not_supported`; clients use `/content` for `Image`.

## 6. Structured Preview Contracts

### 6.1 PDF

The preview response streams the original, validated PDF with canonical `application/pdf` and safe inline disposition. No conversion, public URL, filesystem path, or second stored representation is created.

### 6.2 Text and Markdown

The response is JSON containing metadata, `mode`, `text`, `truncated`, `returnedBytes`, and `maximumBytes`. Accepted text-family uploads are strict UTF-8 without NUL. Invalid encoding is rejected at upload; a later integrity/read failure is fail-closed as `attachment_unavailable`.

Text, JSON, XML, SQL, and LOG are returned only as JSON string data. Markdown is also returned as source text; a later UI must reuse the project's existing safe Markdown renderer. The backend never converts uploaded text or Markdown into trusted HTML.

### 6.3 CSV

The response is JSON containing metadata, `mode: Csv`, `rows` as arrays of strings, the applied limits, `truncated`, and explicit truncation reasons. CSV parsing supports quoted values and escaped quotes. Values that resemble formulas or markup remain inert strings. CSV is never emitted as HTML and no formula is evaluated.

### 6.4 XLSX

The response is JSON containing metadata, `mode: Spreadsheet`, bounded sheet metadata, selected sheet, rows as arrays of display strings, applied limits, and truncation indicators. An optional `sheet` query selects an exact workbook sheet name; otherwise the first visible sheet (or first sheet) is selected.

Only macro-free `.xlsx` packages accepted by the ATTACH-A01 package validator are eligible. Parsing uses bounded ZIP/XML reads. Formula expressions are never returned or executed; a formula cell may expose only its cached stored result, or an empty value when no cached result exists. External links, macros, embedded active content, calculation engines, Office automation, and workbook editing are not loaded.

DOCX, PPTX, and ZIP are not opened by the preview service.

## 7. Central Preview and Storage Limits

Limits are centrally configured under `Attachments` and validated at startup. The frozen defaults are:

| Setting | Default |
| --- | ---: |
| `StorageRoot` | Development: `App_Data/attachments`; Production: required absolute persistent path outside deployment; attachment test fixtures: task-owned absolute temp path |
| `MaxImageBytes` | 10 MiB |
| `MaxFileBytes` | 50 MiB |
| `MaxStoredAttachmentsPerDocument` | 100 |
| `PreviewTextMaxBytes` | 256 KiB |
| `PreviewCsvMaxRows` | 200 |
| `PreviewCsvMaxColumns` | 50 |
| `PreviewCsvMaxCharacters` | 256 Ki characters |
| `PreviewSpreadsheetMaxWorkbookBytes` | 10 MiB |
| `PreviewSpreadsheetMaxSheets` | 20 |
| `PreviewSpreadsheetMaxRows` | 200 |
| `PreviewSpreadsheetMaxColumns` | 50 |
| `PreviewSpreadsheetMaxSharedStringCharacters` | 1 MiB characters |

Configured values must remain positive and must not exceed implementation safety ceilings. Exceeding an upload limit rejects upload. Exceeding a representational row/column/character limit returns bounded data with truncation metadata. A workbook exceeding the preview workbook/package safety limit remains downloadable but returns `422 preview_limit_exceeded`.

## 8. Failure Semantics

- `400 validation_error`: malformed IDs/query or multipart shape.
- `401/403`: authentication/role boundary.
- `404 not_found`: owner, revision, attachment, or exact reference context is absent; no cross-document existence disclosure.
- `409 conflict` / `invalid_state`: concurrency or owner/storage state conflict.
- `413 payload_too_large`: configured upload size exceeded.
- `415 unsupported_media_type`: extension, MIME, signature, package, or UTF-8 validation failed.
- `422 preview_not_supported`: approved attachment is download-only or is an image handled by `/content`.
- `422 preview_limit_exceeded`: a safe preview cannot be produced within configured workbook/package limits.
- `503 attachment_unavailable`: referenced metadata exists but stored content is missing, corrupt, or cannot be read safely.

Error bodies never disclose storage keys or physical paths.

## 9. Persistence and Lifecycle Invariants

Preview creates no database state and never changes attachment/revision semantics. The current set remains derived only through:

```text
KnowledgeDocument.CurrentRevisionNumber
→ KnowledgeDocumentRevision
→ AttachmentReference[]
→ Attachment
```

Upload remains distinct from attach. A semantic content save writes the complete attachment reference snapshot into the new immutable revision. Restore copies the source revision's exact reference set after verifying every referenced attachment is `Ready` and physically readable. Soft delete never deletes binary data. Administrator physical deletion remains zero-reference-only and race-safe.

## 10. Implementation Dependency Decision

B01 may implement the XLSX foundation with the .NET base class library's bounded `ZipArchive` and hardened streaming `XmlReader`. This is sufficient for the frozen simple read-only contract, introduces no Office editing framework or new third-party parser dependency, and avoids formula execution. A future dependency change requires a separate documented review.

## 11. Frozen Task Sequence

The unique attachment sequence is:

```text
ATTACH-B01          Metadata + Storage + Secure Download + Preview Foundation
ATTACH-B02          KnowledgeDocument image upload / drag / paste
ATTACH-B03          Ordinary attachment upload / download / attachment area
ATTACH-B03-PREVIEW  PDF/text/CSV/XLSX read-only Preview UX
ATTACH-B04          Administrator Attachment Management
ATTACH-VERIFY       Full attachment verification
```

This repository does not use the ambiguous `ATTACH-B03.5` identifier.

## 12. Preserved ATTACH-A01 Decisions

This amendment does not alter:

- the `Attachment` or `AttachmentReference` model;
- immutable revision binding or semantic save/restore rules;
- filesystem/SQLite storage split or opaque storage keys;
- authorization, canonical-user, Viewer/Editor/Administrator, or antiforgery boundaries;
- soft delete and historical-read behavior;
- zero-reference-only physical deletion;
- `![alt](attachment:<id>)` image syntax;
- the prohibition on DOCX/PPTX conversion, ZIP browsing, online Office editing, OCR, FTS, cloud storage, deduplication, chunk upload, or automatic orphan cleanup.
