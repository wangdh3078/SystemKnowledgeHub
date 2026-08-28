# ATTACH-A01 — Attachment / Image / Revision / Permission / Storage Architecture Decision

Status: **Frozen Design Decision — Approved**

Product: **系统知识中心 / System Knowledge Hub**

Date: **2026-08-28**

Scope: **Post-MVP KnowledgeDocument attachment capability architecture only**

## 1. Result

```text
ATTACH-A01 APPROVED
Blocking human decisions: NONE
ATTACH-B01 READY: YES
```

This decision freezes the smallest coherent attachment capability for the current Internal Pilot baseline. It does not implement a database migration, backend endpoint, filesystem write, frontend upload control, administrator page, package, or deployment change.

ATTACH-A01 is the later capability-specific authority for attachments. It extends the frozen MVP without editing the frozen MVP sources. It does not approve a real Production deployment: the existing `SEC-04` provider, HTTPS/proxy, persistent-key, backup, and operational evidence gate remains open.

## 2. Authority and inspected baseline

The decision is based on the current clean `main` baseline and these authorities:

- frozen MVP UI inventory, design baseline, domain model, database model, application use-case model, API contract, and solution structure;
- `KNOWLEDGE_CONTENT_DOCUMENT_ARCHITECTURE_PLAN.md`, including the deferred-attachment constraints;
- `KNOWLEDGE_DOCUMENT_MARKDOWN_SOURCE_EDITOR_DECISION.md` and `KNOWLEDGE_DOCUMENT_MARKDOWN_EXTENSION_CONTRACT.md`;
- `REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md` and the implemented immutable revision flow;
- `DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md` and its implemented deleted-owner historical-read boundary;
- `SEC_A01_SECURITY_ACCESS_CONTROL_DESIGN_REVIEW.md`, current Cookie/antiforgery middleware, and the Viewer/Editor/Administrator policies;
- `PRODUCTION_DEPLOYMENT_GUIDE.md` and current fail-closed persistent-path rules;
- `SYSTEM_FULL_REGRESSION_R01_VERIFICATION_REPORT.md`, which records `PHASE-ATTACHMENTS READY: YES` and no unresolved Blocker, High, or Medium baseline gap.

Current implementation facts that constrain this design:

- `KnowledgeDocument` is the mutable current head and has an opaque Version-based `concurrencyToken`.
- `KnowledgeDocumentRevision` is an immutable full Title/Summary/BodyMarkdown snapshot. Create, semantic content save, and restore allocate revisions inside a short SQLite write transaction.
- Lifecycle-only, KnowledgeStatus, Evidence/HumanConfirmation, and Relationship changes do not create content revisions.
- Archived content cannot be normally saved; restore requires Draft.
- KnowledgeDocument soft delete creates no revision, hides current detail, preserves revision list/detail, and permits only the controlled historical tombstone boundary.
- Evidence is a provenance/trust fact and is explicitly not a generic attachment model.
- The backend uses ASP.NET Core Controllers, direct `KnowledgeHubDbContext`, EF Core, SQLite, Cookie authentication, fallback Viewer authorization, explicit Editor/Administrator policies, and antiforgery on API writes.
- The frontend persists raw Markdown source, renders through the shared HTML-disabled safe renderer, and compares revisions as escaped raw source.
- The application does not expose a static-file middleware boundary. Protected binary content therefore remains behind an API authorization check.

## 3. Goals

ATTACH-A01 freezes:

1. raster-image upload through editor file selection, drag/drop, and clipboard paste;
2. controlled ordinary-file upload, listing, download, current-reference removal, and historical display;
3. a minimal `Attachment` plus immutable `AttachmentReference` domain model;
4. deterministic compatibility with semantic save, revision detail, compare, restore, and soft-deleted KnowledgeDocument history;
5. filesystem binary storage plus SQLite metadata/reference storage;
6. filename, extension, content-type, size, authorization, integrity, path, and response-header boundaries;
7. Viewer/Editor/Administrator behavior without a new ACL or permission model;
8. orphan detection and Administrator-only physical deletion;
9. failure, retry, concurrency, backup, restore, and missing-storage behavior;
10. executable ATTACH-B01, B02, B03, B04, and ATTACH-VERIFY slice contracts.

## 4. Explicit non-goals

This phase does not add:

- cloud/object storage, a storage-provider plug-in framework, CDN, public/signed URLs, or direct browser-to-storage upload;
- file bodies, base64, data URIs, rendered HTML, or editor-library state in SQLite/Markdown;
- malware scanning, content-disarm/reconstruction, quarantine workflow, DLP, OCR, thumbnail generation, image crop/compress/edit, or Office/PDF preview;
- attachment-content full-text search, archive extraction, ZIP browsing, or server execution of uploaded content;
- cross-document attachment reuse, folder/media library, rename, tags, description, versioning independent of KnowledgeDocument revisions, comments, or per-file ACL;
- SHA-256 deduplication, shared physical objects, chunked/resumable upload, range download, background jobs, or automatic orphan cleanup;
- attachment-to-Evidence conversion, automatic Evidence/HumanConfirmation, automatic KnowledgeStatus change, or automatic Relationship creation;
- revision deletion/retention purge, attachment restore, recycle bin, bulk physical delete, or product-level deleted-attachment browser;
- legacy Office binary formats, macro-enabled Office files, SVG, HTML, executable/script formats, 7z/RAR, or an administrator-configurable file-type whitelist;
- a generic file controller/service/repository, CQRS/MediatR, event bus, or second authorization model;
- edits to frozen specifications, Golden UI assets, or existing frozen task definitions.

## 5. Decision summary

| Area | Frozen decision |
| --- | --- |
| Binary/metadata split | File bodies on one configured filesystem root; metadata and references in SQLite. No blob/base64 body. |
| Ownership | Every Attachment belongs to exactly one KnowledgeDocument and cannot be rebound or shared across documents. |
| Historical binding | Every AttachmentReference belongs to exactly one immutable KnowledgeDocumentRevision. |
| Current state | The current attachment set is the reference set of `KnowledgeDocument.CurrentRevisionNumber`; no second mutable current-reference table/state exists. |
| Semantic change | Adding/removing an ordinary attachment or changing an inline-image token is a semantic content change and creates one revision. |
| Restore | Restore copies the selected revision's content and complete attachment-reference set into one new revision. |
| Markdown | Inline images persist as `![alt](attachment:<id>)`; the controlled renderer resolves the token in current or historical context. |
| Ordinary files | Persisted as revision references outside Markdown and returned in current/revision detail DTOs. |
| Whitelist | Safe raster images plus a closed ordinary-file list; SVG, active web content, executable, macro-enabled, and legacy Office formats are denied. |
| Integrity | SHA-256 is computed while streaming upload and stored as integrity metadata; it is not a deduplication key. |
| Download | Same-origin authenticated API only, with owner/revision reference checks, safe disposition, `nosniff`, and no static/public storage path. |
| Removal | Removing from the current document creates a revision without the reference; it never deletes historical references or the file. |
| Physical delete | Administrator only, only when the attachment has zero references across all revisions. No automatic cleanup. |
| Authorization | Read/download inherits document/revision authorization; Editor writes any currently editable document; Administrator alone manages global metadata and physical orphan deletion. |
| Production | Storage root must be explicitly configured as an absolute persistent path outside the deployment directory. SEC-04 remains independent. |

## 6. Domain boundary

### 6.1 Attachment is not Evidence

An Attachment is a document-owned content artifact. Evidence answers why a knowledge assertion should be believed and retains the existing source locator, provider snapshot, HumanConfirmation, and KnowledgeStatus rules.

Therefore:

- uploading or referencing a file does not create Evidence;
- a filename, checksum, or document attachment does not automatically satisfy an Evidence gate;
- Evidence may continue to contain an external/canonical source reference under its existing contract, but ATTACH-A01 adds no Evidence-to-Attachment FK;
- external HTTP(S) links remain Markdown links and are not downloaded or ingested as Attachments.

### 6.2 Attachment aggregate

`Attachment` is metadata for one stored immutable binary object.

| Property | SQLite shape | Rule |
| --- | --- | --- |
| `Id` | INTEGER PK | Safe positive integer; API-visible. |
| `KnowledgeDocumentId` | INTEGER required FK | Owning `knowledge_documents.id`, `RESTRICT`; immutable. |
| `OriginalFileName` | TEXT required, max 255 Unicode scalar values | Normalized display/download name only; never a path. |
| `Extension` | TEXT required, max 16 | Lowercase closed value with leading dot, for example `.png`; immutable. |
| `Kind` | TEXT required | `Image` or `File`; enforced by CHECK. |
| `ContentType` | TEXT required, max 127 | Server-selected canonical MIME value, not the raw browser header. |
| `SizeBytes` | INTEGER required | Greater than zero and within the configured kind-specific limit. |
| `StorageKey` | TEXT required, max 96 | Server-generated opaque relative key; unique; never client supplied or API-public. |
| `Sha256` | BLOB required, exactly 32 bytes | Hash of the accepted file body; lowercase hex only at API/report boundaries. |
| `StorageState` | TEXT required | `Ready` or `DeletePending`; enforced by CHECK. Upload success exposes only `Ready`. |
| `CreatedByUserId` | INTEGER required FK | Canonical current User, `RESTRICT`. |
| `CreatedByDisplayNameSnapshot` | TEXT required | Stable upload-time display snapshot. |
| `CreatedAt` | TEXT/instant required | Server UTC. |
| `Version` | INTEGER required | Starts at 1; opaque admin concurrency token; CHECK `>= 1`. |

The following are deliberately derived, not persisted: `IsImage`, current-reference count, historical-reference count, total reference count, orphan state, effective storage health, and display/download URL.

Attachment metadata and file body are immutable after successful upload. There is no rename/update use case. `StorageState` and `Version` exist only to make Administrator physical deletion recoverable and race-safe.

### 6.3 AttachmentReference snapshot

`AttachmentReference` is an immutable membership row in one KnowledgeDocument revision snapshot.

| Property | SQLite shape | Rule |
| --- | --- | --- |
| `Id` | INTEGER PK | Internal identity. |
| `KnowledgeDocumentId` | INTEGER required | Explicit aggregate owner used in composite integrity constraints. |
| `KnowledgeDocumentRevisionId` | INTEGER required FK | Immutable revision row, `RESTRICT`. |
| `AttachmentId` | INTEGER required FK | Owned Attachment, `RESTRICT`. |

There is no `IsCurrent`, `Version`, `UpdatedAt`, mutable ordering, display name, URL, or copied hash on the reference row. The Attachment metadata is immutable, and the revision already supplies author/time.

One revision may reference an Attachment only once even when an inline token occurs multiple times. Images and ordinary files are distinguished by `Attachment.Kind`:

- `Image` must occur at least once as an exact inline-image token in that revision's BodyMarkdown;
- `File` must be present in the revision's explicit ordinary-file ID set and cannot be used by the inline-image scheme.

Reference sets are unordered. Current/revision ordinary-file lists use stable `Attachment.CreatedAt ASC, Attachment.Id ASC` display order; a visual reorder is not a semantic change in this phase.

### 6.4 Database constraints and indexes

The additive migration must create:

- `attachments` and `attachment_references`; existing revisions receive no invented reference rows;
- unique `attachments.storage_key`;
- index `(knowledge_document_id, created_at, id)` for document/admin queries;
- unique `(id, knowledge_document_id)` alternate keys on Attachment and KnowledgeDocumentRevision where needed for composite FKs;
- composite FK `(attachment_id, knowledge_document_id)` to Attachment ownership;
- composite FK `(knowledge_document_revision_id, knowledge_document_id)` to revision ownership;
- unique `(knowledge_document_revision_id, attachment_id)`;
- index `(attachment_id, knowledge_document_revision_id)` for reference/orphan checks;
- CHECK constraints for safe positive IDs/counts, `Kind`, `StorageState`, nonblank bounded metadata, 32-byte hash, and `Version`.

All FKs use `RESTRICT`. There is no cascade delete. Revision and reference rows have no UPDATE/DELETE use case. The application enforces the file-extension/MIME matrix and image-token/reference correspondence because those rules cannot be expressed responsibly as SQLite row-local CHECK constraints.

## 7. Canonical current and historical model

### 7.1 One source of attachment truth

```text
KnowledgeDocument.CurrentRevisionNumber
    → KnowledgeDocumentRevision
        → AttachmentReference[]
            → Attachment metadata
                → filesystem StorageKey
```

The current attachment set is the current revision's set. A historical attachment set is the addressed historical revision's set. No mutable document-level reference copy is maintained.

This avoids these failure classes:

- head and revision attachments disagreeing after a failed dual update;
- removing a current row accidentally destroying history;
- restore copying Markdown but not its files;
- a deleted head hiding the only remaining attachment ownership fact.

### 7.2 Upload is not attach

Upload creates a `Ready` Attachment owned by an active, editable KnowledgeDocument but creates no AttachmentReference and no revision. This is necessary because editor drag/paste/file selection must complete before a stable Markdown token or ordinary-file selection can be saved.

Until a successful semantic save references it, the Attachment is an orphan. Losing the HTTP response or abandoning an edit does not mutate document content; it may leave an administrator-visible orphan.

### 7.3 Semantic save

The existing command remains the sole atomic document-content write:

```text
PUT /api/knowledge-documents/{id}/content
```

It adds one field:

```json
{
  "title": "...",
  "summary": null,
  "bodyMarkdown": "...",
  "fileAttachmentIds": [310, 311],
  "changeSummary": "补充部署截图和回滚清单",
  "concurrencyToken": "opaque"
}
```

Rules:

1. `fileAttachmentIds` is required as a full desired unordered set once the attachment contract is enabled; absent is accepted only for backward-compatible clients and means “retain the current File set”, never “silently remove all”.
2. Duplicate, non-positive, or JavaScript-unsafe IDs return `400 validation_error`.
3. The server parses exact inline-image IDs from canonical BodyMarkdown; it does not accept a second client-supplied image-ID list.
4. Every candidate Attachment must be `Ready`, owned by the route KnowledgeDocument, have the expected Kind, and pass the configured count. Missing/wrong-owner/wrong-kind/not-ready values return bounded `422 reference_invalid` without disclosing another document.
5. Semantic equality includes normalized Title, Summary, BodyMarkdown, and the complete unordered Attachment ID set. A reference-set change creates a revision even when text is unchanged.
6. A full semantic no-op returns the existing `200` detail and changes no revision number, Version, actor/time, PublishedAt, FTS, or reference rows.
7. A semantic change uses one SQLite immediate transaction to validate the active document and token, allocate N+1, update the head, insert the immutable revision, insert the complete reference snapshot, update Version/published pointer/FTS, and commit.
8. Archived remains `409 invalid_state`. A dirty Published save keeps the existing explicit frontend confirmation and immediately advances the published revision pointer.

There is no low-level `POST reference` or `DELETE reference` endpoint. “Add” and “Remove” mean changing the desired full set through the semantic content-save command. This prevents reference mutation without history and prevents two successive API calls from manufacturing partial revisions.

### 7.4 Remove current reference

Removing an inline image token or removing an ID from `fileAttachmentIds` creates N+1 without that AttachmentReference. All earlier reference rows remain immutable.

The binary and Attachment metadata are retained. Because revision deletion is outside scope, an Attachment successfully referenced by any revision is not physically deletable in ATTACH-B01–B04 even when it is no longer current. This storage cost is an explicit consequence of reproducible history.

### 7.5 Restore

Restore extends the REV-A01 semantic snapshot:

```text
Historical Revision K content + AttachmentReference set
→ validate every referenced Attachment metadata/storage object
→ copy Title/Summary/Body and the exact Attachment ID set
→ create Revision N+1 with copied reference rows
→ point current head to N+1
→ commit atomically
```

The existing Draft, Editor-or-higher, concurrency-token, reason, actor/time, FTS, and published-pointer rules remain.

Restore equality now compares content plus the complete Attachment set. An older revision with identical text but a different set is restorable; an older revision with identical content and set remains `422 business_rule_violation`.

An Attachment referenced by history cannot have been legitimately physically deleted. If its Ready object is missing/corrupt because of external storage damage, restore returns `409 conflict`, makes no head/revision/reference change, and tells the user that storage recovery by an Administrator/operator is required. Restore never manufactures a blank file or silently drops a reference.

### 7.6 Revision detail and compare

Current detail and revision detail add an `attachmentReferences` projection:

```json
{
  "attachmentReferences": [
    {
      "attachmentId": 310,
      "kind": "Image",
      "originalFileName": "部署拓扑.png",
      "contentType": "image/png",
      "sizeBytes": 482901,
      "sha256": "lowercase-64-hex"
    }
  ]
}
```

`storageKey`, `Version`, and unrelated admin facts are never included in ordinary document/revision DTOs.

The existing frontend raw-source diff remains authoritative for Title/Summary/BodyMarkdown. ATTACH-B03 adds a deterministic set comparison beside it:

- key by `(attachmentId, kind)`;
- show `新增附件`, `移除附件`, and unchanged counts using immutable filename/kind/size/hash metadata;
- do not compute binary, image-pixel, Office, PDF, or same-hash similarity diffs;
- do not treat two different IDs with the same SHA-256 as the same Attachment;
- preserve the existing compare size limits and escaped-text/XSS boundary.

## 8. Markdown reference contract

### 8.1 Frozen syntax

An inline image uses standard image syntax with one controlled internal destination:

```markdown
![部署拓扑](attachment:310)
```

The internal destination is exactly `attachment:<safe-positive-integer>`. Query, fragment, hostname, relative path, filename, storage key, whitespace, sign, decimal variant, or another scheme is invalid. Alt text remains ordinary escaped Markdown author content.

Ordinary files are not encoded as Markdown links in this phase. They are revision reference metadata shown in the document attachment area. Authors may still write normal external links under the existing safe-protocol contract.

### 8.2 Why an internal scheme is selected

| Option | Evaluation | Decision |
| --- | --- | --- |
| Persist API URL | Couples canonical source to current route/version/disposition and makes historical/deleted-owner context ambiguous. | Reject. |
| Persist filesystem path or storage key | Leaks deployment structure and bypasses authorization portability. | Reject. |
| Persist data URI/base64 | Bloats Markdown/SQLite/revisions and bypasses file governance. | Reject. |
| Persist custom `attachment:<id>` | Stable across current/history/export evolution and requires explicit controlled resolution. | Adopt. |

### 8.3 Rendering boundary

The raw source editor preserves the token exactly. The shared safe renderer gains one narrow image-destination extension:

1. parse only an image destination matching the frozen grammar;
2. in saved current/history mode, resolve only when that ID appears as `Kind=Image` in the current/revision detail's authorized reference projection;
3. in the unsaved edit-session preview only, allow the exact just-uploaded Image ID to resolve through an in-memory `Blob` object URL retained by that editor session;
4. produce a context-specific same-origin API content URL for saved state, revoke every transient object URL on replacement/save/discard/unmount, and never persist or log the object URL;
5. render malformed, missing, wrong-kind, or unauthorized tokens as a non-executable Chinese unavailable-image placeholder;
6. retain `html: false`, existing safe link handling, Mermaid strict mode, escaping, and no persisted rendered output.

The upload response deliberately contains no persistent content URL. The editor already owns the selected/dropped/pasted Blob, so unsaved preview needs no temporary server authorization exception. Current saved context resolves to the current content route. Historical context includes the exact revision number. A deleted KnowledgeDocument has no current resolution; its allowed revision view resolves only through the historical route.

## 9. File-type and validation freeze

### 9.1 Allowed raster images

| Extensions | Canonical MIME | Required recognition |
| --- | --- | --- |
| `.png` | `image/png` | PNG signature and structurally valid bounded header. |
| `.jpg`, `.jpeg` | `image/jpeg` | JPEG signature/marker validation. |
| `.gif` | `image/gif` | GIF87a/GIF89a signature. |
| `.webp` | `image/webp` | RIFF + WEBP signature. |

SVG is explicitly prohibited. It is scriptable active content, is not required for the Internal Pilot, and would require a separate sanitizer/rendering contract. BMP, TIFF, ICO, AVIF, HEIC, and all other image types are also denied in this phase.

### 9.2 Allowed ordinary files

| Extensions | Canonical MIME | Required recognition/handling |
| --- | --- | --- |
| `.pdf` | `application/pdf` | PDF signature; always attachment disposition, never inline preview. |
| `.docx` | `application/vnd.openxmlformats-officedocument.wordprocessingml.document` | Bounded OOXML package validation; reject macro-enabled content types. |
| `.xlsx` | `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet` | Bounded OOXML package validation; reject macro-enabled content types. |
| `.pptx` | `application/vnd.openxmlformats-officedocument.presentationml.presentation` | Bounded OOXML package validation; reject macro-enabled content types. |
| `.txt`, `.log`, `.sql` | `text/plain` | Valid UTF-8, no NUL; download only. |
| `.md` | `text/markdown` | Valid UTF-8, no NUL; download only. |
| `.csv` | `text/csv` | Valid UTF-8, no NUL; syntax is not imported/interpreted. |
| `.json` | `application/json` | Valid UTF-8, no NUL; content is not parsed/executed as application configuration. |
| `.xml` | `application/xml` | Valid UTF-8, no NUL; no XML parser/entity resolution. |
| `.zip` | `application/zip` | ZIP structure recognized; retained opaque and never extracted/browsed by the server. |

Legacy `.doc/.xls/.ppt`, macro-enabled `.docm/.xlsm/.pptm`, HTML, CSS, JavaScript, shell/PowerShell/batch, executable/library/installer, shortcut, disk-image, RAR/7z, and any unlisted extension are denied.

### 9.3 MIME/extension/content decision

All three signals are evaluated:

- filename extension must be in the hard-coded server whitelist;
- a specific declared browser MIME that contradicts the selected type is rejected;
- empty or generic `application/octet-stream` does not authorize a file but may proceed to server recognition;
- binary signatures/package markers or UTF-8/no-NUL text checks must match the extension;
- the server stores and returns only the canonical MIME from the table;
- changing the whitelist requires an explicit reviewed code/design amendment, not an administrator configuration toggle.

This is type recognition, not proof that the content is benign.

OOXML validation reads only bounded package metadata and refuses encrypted packages, macro-enabled content types, more than 10,000 entries, more than 500 MiB declared total uncompressed bytes, or a declared compression ratio above 100:1. Plain `.zip` remains opaque: central-directory recognition is allowed, but the application never extracts entries or treats their names as filesystem paths. These are engineering safety ceilings and require an explicit amendment if real pilot files demonstrate a legitimate need to change them.

### 9.4 Filename rules

The server decodes the multipart filename and normalizes it to Unicode NFC. It rejects:

- empty/whitespace names, `.`/`..`, more than 255 Unicode scalar values, or a name without the approved terminal extension;
- NUL, CR/LF, control characters, `/`, `\`, colon, alternate-data-stream syntax, or any path/root component;
- Windows reserved device names and names ending in a dot/space.

The server does not “fix” a path by trusting `Path.GetFileName`; path-like input is rejected. Duplicate display filenames are allowed because identity is Attachment ID. Response headers use a sanitized ASCII fallback plus RFC 5987 `filename*`; raw user input never enters a header or storage path.

### 9.5 Configurable engineering defaults

These are first-release engineering defaults, not immutable Product quotas:

| Setting | Default | Meaning |
| --- | ---: | --- |
| `Attachments:MaxImageBytes` | 10 MiB | Maximum streamed image body. |
| `Attachments:MaxFileBytes` | 50 MiB | Maximum streamed ordinary-file body. |
| `Attachments:MaxStoredAttachmentsPerDocument` | 100 | Maximum physical Attachment metadata rows owned by one document, including orphans and historical-only files. |

Values must be positive, bounded to safe .NET/request-limit ranges, and aligned with the reverse proxy/Kestrel multipart limit. A deployment may lower or deliberately raise them through reviewed configuration. The application does not guess a global filesystem quota: B04 reports metadata capacity, while volume sizing, free-space alerting, and hard quota remain deployment-owned.

## 10. Filesystem storage decision

### 10.1 Configuration

One concrete filesystem implementation is selected. There is no local-versus-Production provider switch or speculative object-storage interface.

`Attachments:StorageRoot` rules:

- Development default: `App_Data/attachments`, resolved against the API Content Root;
- Testing: every fixture that exercises attachment code must supply a task/test-owned absolute temporary root and delete it after the run;
- Production: required, absolute, persistent, outside repository/content-root/build/publish/deployment directories, not a URI or network assumption, and validated fail-closed at startup;
- the process identity receives only the required create/read/delete permissions on this root; users and the web server never receive a separately served static mapping;
- the root must be on durable deployment-owned storage with approved ACLs, backup, capacity monitoring, and encryption at rest.

The Production guide must be extended in ATTACH-B01 with the new setting and coordinated backup requirement. No real machine path or credential is committed.

### 10.2 Opaque storage key

The server generates a cryptographically random lowercase 32-hex object ID before persistence:

```text
staging/<32hex>.tmp
objects/<first-two-hex>/<32hex>.bin
```

The persisted `StorageKey` is only the `objects/...` relative value. It contains no Attachment ID, document ID, original filename, extension, MIME, user value, drive letter, or `..` segment.

On every read/delete, the implementation:

1. accepts a key only from trusted metadata;
2. validates the exact generated-key grammar;
3. combines it with the configured root;
4. resolves and verifies the result remains under the managed root;
5. refuses managed subdirectories containing reparse-point/symlink redirection.

Client input can never become a path. The original extension is metadata only; stored bodies always use `.bin`.

### 10.3 Upload protocol

Upload uses streamed multipart processing; it does not buffer the complete body in memory.

```text
authorize Editor + resolve active/editable KnowledgeDocument
→ validate filename/declared type and configured document count
→ stream to same-root staging file with hard byte limit while computing SHA-256
→ validate signature/package/text and select canonical metadata
→ atomically move staging file to a new final object key
→ begin SQLite immediate transaction
→ re-resolve active/editable owner and count
→ insert Ready Attachment with canonical current User/time
→ commit
→ return 201
```

Revalidating the owner after the potentially slow stream prevents upload from succeeding against a document concurrently archived/deleted or made otherwise invalid. A failure before final move removes staging. A DB/authorization/capacity failure after final move attempts immediate compensation deletion. If the process crashes between file move and metadata commit, the possible residue is an untracked storage object, never a user-visible Attachment or document reference; it is logged/reconciled operationally and is distinct from a metadata orphan.

The API returns success only after both final object and metadata commit exist. Upload never opens a document-content transaction for the duration of network streaming.

### 10.4 SHA-256 and deduplication

SHA-256 is computed over the exact accepted bytes during the single upload stream. The API/admin projection renders the 32 bytes as lowercase 64-character hexadecimal.

The checksum supports integrity display, backup verification, and Administrator on-demand validation. It is not unique and is not used to share storage. Two uploads with identical content produce two Attachment IDs, two StorageKeys, and two bodies. This keeps ownership, deletion, race, and backup behavior explicit for the first release.

Normal download validates metadata state, object existence, and recorded length before streaming. It does not rehash every download. B04 on-demand integrity verification computes SHA-256 and compares it to metadata without changing canonical content.

## 11. Security and download boundary

### 11.1 API-only delivery

No upload directory is mapped with static-file middleware. No filesystem path, storage key, public URL, or presigned URL appears in ordinary DTOs or Markdown.

An authorized controller opens the validated object and streams it with:

- server canonical `Content-Type`;
- `Content-Disposition: inline` only for an allowed `Image` content endpoint;
- `Content-Disposition: attachment` for every download endpoint, including PDF, Office, text, XML, JSON, ZIP, and optional image download;
- sanitized ASCII filename fallback and encoded `filename*`;
- `X-Content-Type-Options: nosniff`;
- `Cache-Control: private, no-store`;
- no server-side rendering, execution, archive extraction, MIME reflection, or path disclosure.

Range requests, public caching, content transformation, thumbnail URLs, and anonymous access are not implemented.

### 11.2 Authorization before storage disclosure

Every read resolves authorization and an exact reference before opening storage:

- current route: active/non-deleted KnowledgeDocument plus AttachmentReference in its current revision;
- historical route: allowed immutable revision read plus AttachmentReference in that exact revision;
- image content route: referenced Attachment must also be `Kind=Image`;
- download route: Attachment may be `Image` or `File` but must be referenced in the addressed context;
- an Attachment ID alone never grants access.

Unknown, wrong-owner, wrong-revision, wrong-kind, unreferenced, and inaccessible IDs return the bounded existing `404 not_found`/`422 reference_invalid` behavior appropriate to route versus write input. Authorization happens before reporting storage existence, size mismatch, or hash information.

### 11.3 Upload/write security

- Upload and content save require the existing Editor policy and canonical Active User resolution.
- Upload/content-save/admin-delete requests use the current Cookie antiforgery middleware; frontend visibility is not an enforcement boundary.
- Client actor, timestamp, uploader ID, SHA-256, MIME, size, StorageKey, document owner, revision number, and storage state are never trusted.
- Upload accepts only active, non-Archived, non-deleted KnowledgeDocuments that the current application authorizes the Editor/Administrator to edit. The current model has global Editor authoring; ATTACH-A01 does not invent creator-only editing.
- Binding uses the document concurrency token and a short SQLite immediate transaction. A stale token never retries automatically.
- Logs contain correlation/Attachment/document IDs and bounded failure categories, not file bodies, Markdown bodies, credentials, storage roots, or raw unsafe filenames.

### 11.4 Malware limitation

This repository has no approved malware-scanning platform. ATTACH-B01 therefore does not pretend to scan:

- the allowlist, signature checks, forced download, `nosniff`, byte limits, API authorization, and “never execute/extract” rules reduce exposure;
- raster images still exercise browser decoders, and allowed PDF/Office/ZIP files can still carry malicious content for a user's local application;
- the UI must state that downloaded files are unscanned and should be opened only when the source is trusted;
- broader-than-Internal-Pilot rollout requires a separate security/deployment decision for malware control or an explicit risk acceptance. It is not silently included in ATTACH-B01.

This accepted limitation does not weaken the existing authentication/authorization boundary and does not close `SEC-04`.

## 12. Authorization matrix

All capabilities require an authenticated, mapped, Active User. The existing fallback policy is Viewer.

| Capability | Viewer | Editor | Administrator |
| --- | :---: | :---: | :---: |
| Read current referenced attachment metadata/list | Allow | Allow | Allow |
| Render/download current referenced content | Allow | Allow | Allow |
| Read/render/download attachment in an allowed historical revision, including deleted owner tombstone context | Allow | Allow | Allow |
| Upload to an active editable KnowledgeDocument | Deny | Allow | Allow |
| Add/remove references through semantic content save | Deny | Allow | Allow |
| Restore attachment set with revision restore | Deny | Allow | Allow |
| Read global Attachment administration list/statistics/detail/storage health | Deny | Deny | Allow |
| Run on-demand checksum verification | Deny | Deny | Allow |
| Physically delete an orphan | Deny | Deny | Allow |
| Physically delete any referenced Attachment | Deny | Deny | Deny |

Attachment access inherits the owning current/historical KnowledgeDocument boundary. `CreatedByUserId` is audit provenance, not an independent ACL and not a right to physical deletion. There is no per-document/per-attachment sharing, deny, ownership hierarchy, department scope, or new role.

## 13. Soft delete and historical reproducibility

KnowledgeDocument soft delete:

- does not alter Attachment metadata, file bodies, reference rows, hashes, revision pointers, or revision content;
- does not create an attachment or content revision;
- makes current attachment list/content/download return 404 with the current document;
- preserves authenticated revision detail and exact historical attachment read through the existing controlled deleted-owner tombstone boundary;
- denies upload, content/reference save, and revision restore while deleted;
- keeps an unreferenced uploaded Attachment visible only to Administrator management, where it may satisfy orphan deletion rules.

An AttachmentReference is a historical fact and is never silently removed. Consequently, a stored revision remains reproducible as long as the deployment preserves the filesystem object. External/manual file loss is a storage incident, not permission to rewrite the revision.

ATTACH-A01 adds no attachment restore/recycle-bin operation. If an Administrator physically deletes a legitimate orphan, both object and metadata are removed through the protocol below and cannot be recovered by the application.

## 14. Orphan and physical-delete contract

### 14.1 Definitions

- **Attachment orphan:** a `Ready` Attachment metadata row with zero AttachmentReference rows across all revisions.
- **Historical-only Attachment:** referenced by one or more revisions but not by the current revision. It is not an orphan.
- **Untracked storage residue:** an object key on disk with no Attachment row, normally possible only after crash/failed compensation. It is not exposed as an Attachment and is handled by operational reconciliation.
- **Missing storage object:** an Attachment row whose validated key has no file or whose length/hash check fails. It is a storage-health failure, not an orphan.

There is no age-based or background auto-delete. Newly uploaded unsaved objects are intentionally orphans and remain so until a semantic save or explicit Administrator action.

### 14.2 Administrator deletion protocol

The delete request carries the Attachment's opaque admin `concurrencyToken`.

```text
BEGIN IMMEDIATE
→ load metadata and verify token + Ready
→ assert COUNT(all AttachmentReference) = 0
→ set DeletePending and increment Version
→ COMMIT
→ delete final file (missing is idempotently considered removed)
→ delete DeletePending metadata in a second short conditional transaction
→ return 204
```

Reference creation accepts only `Ready`. SQLite write serialization gives two valid race outcomes:

- reference save wins, then delete sees a reference and returns `422 business_rule_violation`;
- delete marks `DeletePending` first, then reference save returns `422 reference_invalid` and creates no revision.

If filesystem deletion fails, metadata remains `DeletePending`; it cannot be referenced or downloaded and the Administrator may retry. If the process crashes after file deletion but before row deletion, retry observes the missing file and completes metadata deletion. Unexpected DB failure never reclassifies a referenced Attachment as deletable.

No bulk delete, force flag, retention override, cascade, or “delete file but keep historical placeholder” exists.

## 15. Failure, consistency, and retry behavior

| Condition | Behavior |
| --- | --- |
| Upload exceeds limit | Stop streaming, remove staging, return `413`; no metadata/reference/revision. |
| Unsupported/mismatched type | Remove staging, return `415`; no metadata/reference/revision. |
| Invalid filename/request | Return `400 validation_error`; no final object. |
| Disk full/capacity failure | Remove staging/compensate final object, return `507`; no success response. |
| Owner archived/deleted during upload | Revalidation fails with bounded `409`/`404`; compensate final object. |
| DB insert fails after final move | Roll back metadata, compensate file; log untracked residue if compensation itself fails. |
| Duplicate/retried upload | Each completed request creates a distinct Attachment; no idempotency/dedup claim. UI disables accidental double submit. |
| Stale content token | `409 conflict`; uploaded files remain orphans and may be reused in a later valid save if still Ready. |
| Invalid/wrong-owner/not-ready reference | `422 reference_invalid`; no head/revision/reference change. |
| Metadata exists but object missing/short | After authorization, return `503 attachment_storage_unavailable`; do not emit partial bytes. |
| Hash mismatch found by admin check | Report `Corrupt`; do not rewrite hash/file or silently remove reference. Operator restores coordinated backup. |
| Unexpected read/write error | Safe existing error envelope/correlation log; no path/body disclosure and no swallowed failure. |

Application HTTP status freeze:

| HTTP | Use |
| --- | --- |
| `200` | Metadata/detail/list/save/no-op/integrity result/download stream start. |
| `201` | Upload metadata committed and final object Ready. |
| `204` | Administrator orphan physical delete completed. |
| `400` | Invalid scalar/list/token shape or filename. |
| `401` / `403` | Existing unauthenticated/role authorization behavior. |
| `404` | Route resource/reference not exposed in the addressed current/historical/admin context. |
| `409` | Stale concurrency, invalid lifecycle, DeletePending/storage restore conflict. |
| `413` | Configured request/file size exceeded. |
| `415` | Extension/MIME/content recognition denied. |
| `422` | Wrong-owner/kind/state reference, referenced delete, semantic business rule. |
| `503` | Authorized referenced metadata exists but storage cannot serve valid bytes. |
| `507` | Insufficient configured filesystem capacity/disk write. |

## 16. Frozen API shape

No generic `/api/files` or bare `/api/attachments/{id}/content` route exists.

| Method | Route | Policy | Semantics |
| --- | --- | --- | --- |
| `POST` | `/api/knowledge-documents/{id}/attachments` | Editor | Stream one image/file upload owned by active editable document; return `201` Ready metadata. |
| `PUT` | `/api/knowledge-documents/{id}/content` | Editor | Existing semantic save extended with optional-backward-compatible/full desired `fileAttachmentIds`; server derives image IDs; creates at most one revision. |
| `GET` | `/api/knowledge-documents/{id}/attachments/{attachmentId}/content` | Viewer fallback | Stream current-revision Image inline only. |
| `GET` | `/api/knowledge-documents/{id}/attachments/{attachmentId}/download` | Viewer fallback | Download a current-revision referenced Image/File. |
| `GET` | `/api/knowledge-documents/{id}/revisions/{revisionNumber}/attachments/{attachmentId}/content` | Viewer fallback | Stream exact historical Image inline. |
| `GET` | `/api/knowledge-documents/{id}/revisions/{revisionNumber}/attachments/{attachmentId}/download` | Viewer fallback | Download exact historical referenced Image/File. |
| `GET` | `/api/admin/attachments` | Administrator | Paged/filterable global metadata/reference/owner-state list. |
| `GET` | `/api/admin/attachments/statistics` | Administrator | Bounded aggregate counts/bytes. |
| `GET` | `/api/admin/attachments/{id}` | Administrator | Metadata, opaque token, full hash/key, bounded references, and shallow storage health. |
| `POST` | `/api/admin/attachments/{id}/integrity-check` | Administrator | On-demand length/SHA-256 computation; no canonical mutation. |
| `DELETE` | `/api/admin/attachments/{id}` | Administrator | JSON body with opaque concurrency token; orphan-only physical delete. |

Current KnowledgeDocument detail and revision detail carry `attachmentReferences`; no separate current list endpoint is needed. Upload response may expose the new orphan to the uploading Editor so the current edit session can insert/select it, but it does not make the object downloadable until a successful reference save.

The API never accepts actor/time/hash/size/content type/storage key/storage state/revision number. Existing direct JSON success and error-envelope conventions remain; new stable failure details stay bounded and use the codes in §15 rather than a second envelope.

## 17. Administrator management scope

ATTACH-B04 adds one Administrator navigation/page using the existing application shell, table, status, dialog, empty/loading/error, and accessibility baselines.

### 17.1 List and filters

Columns:

- Attachment ID, original filename, Kind/canonical type, size;
- owning document ID/title or deleted tombstone;
- uploader snapshot and CreatedAt;
- current-reference flag, total revision-reference count, orphan/historical-only state;
- shallow storage health (`Ready`, `Missing`, `LengthMismatch`, `DeletePending`);
- abbreviated SHA-256; full value and StorageKey only in Administrator detail.

Filters remain bounded: name, Kind/extension, uploader, owning document ID, owner deleted state, orphan/historical-only, StorageState/health, and CreatedAt range. Default sort is newest upload then ID. Pagination uses the existing safe integer/default 20/max 100 convention.

### 17.2 Statistics

The statistics endpoint returns:

- total metadata count and recorded bytes;
- Image/File counts and bytes;
- orphan counts and bytes;
- current-referenced, historical-only, and deleted-owner counts;
- Ready/DeletePending counts;
- largest files and recent upload counts within fixed bounded windows.

It does not recursively hash or scan every filesystem object per page request. Shallow existence/length health belongs to detail/bounded queries; full SHA-256 is on demand. Deployment filesystem free space and quota alerts remain operational metrics.

### 17.3 Actions

- View metadata/reference summary and storage health.
- Run one Attachment integrity check.
- Physically delete only an orphan with confirmation, opaque token, and clear irreversible warning.
- Retry a DeletePending deletion.

No administrator action renames, moves, previews, edits metadata, changes ownership, rebinds, force-deletes a reference, restores, bulk-deletes, or browses arbitrary filesystem objects.

## 18. Backup, restore, and operational boundary

SQLite and the attachment root form one logical backup unit even though they are different media.

An approved backup must:

1. quiesce attachment/document writes or stop the application;
2. capture SQLite through an approved SQLite backup/checkpoint procedure, including WAL correctness rather than copying only a live `.db` blindly;
3. capture the complete managed attachment root, including objects and any recoverable DeletePending state;
4. capture required deployment configuration and the existing persistent Data Protection keys under their own secret/key controls;
5. record application/schema version, backup timestamp, object count, and recorded byte/hash manifest;
6. resume writes only after both database and filesystem capture succeed.

Restore must place database and attachment root from the same logical backup set, apply least-privilege ACLs, validate SQLite integrity/FKs, validate every Ready metadata key remains under the root, compare existence/length and preferably full SHA-256, then perform authenticated current/historical download checks before reopening writes.

Restoring only SQLite or only the filesystem is an incomplete recovery. Application code does not auto-delete unmatched data or rewrite revision references after a mismatch. Operators investigate from backup manifests and bounded diagnostics.

## 19. Alternatives and rejected designs

| Alternative | Reason rejected |
| --- | --- |
| SQLite BLOB/base64 Markdown | Inflates DB/revisions, harms streaming/backup, and mixes content with file bodies. |
| Mutable current references plus revision copies | Creates two canonical sets and dual-write drift. Current derives from current revision instead. |
| Attachment shared across documents | Expands authorization, retention, ownership, and deletion races; no proven need. |
| Revision stores only URL/filename | Cannot guarantee owner integrity, secure delivery, or historical membership. |
| Bare attachment-ID download | Lacks current versus exact-revision authorization context, especially for deleted owners. |
| Physical delete on current remove | Breaks immutable historical rendering/restore. |
| Soft-delete every Attachment | Adds recycle-bin/restore semantics without solving missing binary or referenced-history safety. |
| SHA-256 dedup/shared object | Requires reference-counted physical lifecycle and cross-document security not required now. |
| Antivirus “stub” | Creates false assurance. The limitation is explicit until a real platform is approved. |
| Configurable extension list | Lets deployment configuration silently weaken the security contract. |
| Generic storage/provider/controller framework | No second storage implementation or generic file domain is approved. |

## 20. Implementation slices

### ATTACH-B01 — Attachment Foundation, Storage, API, and Revision Contract

**Goal:** implement the complete backend persistence/storage/security/revision foundation without end-user image/file UX or Administrator page.

In scope:

- `Attachment`/`AttachmentReference` domain, EF configurations, DbSets, additive SQLite migration, ModelSnapshot, constraints, and indexes;
- `Attachments` options, Development/Testing/Production path validation, concrete filesystem storage, streaming validation, checksum, compensation, and safe streaming response;
- document-scoped upload/current/historical content/download routes;
- extend content save, semantic no-op, revision snapshot insertion, restore, current detail, revision detail, soft-deleted historical reads, and opaque concurrency as frozen above;
- typed backend contracts and focused relational/API/storage tests using task-owned temporary SQLite, Data Protection, and attachment roots;
- update `PRODUCTION_DEPLOYMENT_GUIDE.md` and create the B01 verification report/index entry.

Out of scope:

- drag/drop/paste/file picker UI, ordinary attachment panel, revision attachment compare UI, Administrator management UI/endpoints, malware integration, object storage, deduplication, and B02+ work.

Acceptance:

- migration preserves all existing document/revision/content/FTS rows and creates zero invented references;
- upload allow/deny/size/filename/signature/hash/count matrix passes; no repository database/storage is used;
- current and historical authorization, safe headers, wrong-owner/revision/kind cases, deleted-owner history, and storage-missing behavior pass;
- content-only, image-token, file-set-only, combined, no-op, stale, Published, Archived, restore, and rollback transactions satisfy exact revision/reference invariants;
- delete-vs-reference and upload-vs-delete/archive races have deterministic focused tests;
- Production invalid/relative/deployment-local root fails closed; Testing uses temporary roots;
- no file/static path leaks, no generic file framework, and no B02+ UI.

Dependencies: accepted ATTACH-A01, current `main`, REV-A01 implementation, DELETE-A01 implementation, current security/Production configuration baseline.

Deliverable: one task-specific commit and `ATTACH_B01_ATTACHMENT_FOUNDATION_STORAGE_API_REVISION_VERIFICATION_REPORT.md`. Stop after B01.

### ATTACH-B02 — Inline Image Authoring and Rendering UX

**Goal:** make the frozen Image contract usable in the raw Markdown editor and safe current/historical renderer.

In scope:

- image file picker, drag/drop, and clipboard image paste in the KnowledgeDocument editor;
- upload progress/disabled state, typed 400/413/415/507 errors, retry without duplicate automatic submission, and orphan-aware cancellation wording;
- insert exact `![alt](attachment:<id>)` only after `201`; preserve caret/selection, dirty guard, and the sole page-level Save action;
- controlled renderer mapping for current preview/read and historical preview, unavailable placeholder, loading/error/alt accessibility;
- Published-save confirmation and revision reference behavior through B01; responsive/fullscreen editor verification.

Out of scope:

- image crop/compress/resize/gallery/thumbnail service, external-image ingestion, SVG, arbitrary media embeds, ordinary file panel, admin page, or B03+.

Acceptance:

- picker/drag/paste use the same upload path and whitelist;
- failed/aborted upload inserts no token; successful upload inserts one stable token and only Save creates the revision;
- preview/read/history load only authorized exact-context API URLs and never persist generated URLs/HTML/base64;
- malformed/wrong-kind/missing token is inert and accessible; XSS/raw HTML/protocol regression remains closed;
- focused frontend type-check/build/tests and one authenticated Browser → API → filesystem → SQLite image path pass using isolated state.

Dependencies: ATTACH-B01 PASS.

Deliverable: one task-specific commit and B02 verification report. Stop after B02.

### ATTACH-B03 — Ordinary File UX, Historical Display, and Compare

**Goal:** make approved ordinary attachments usable without turning them into Markdown blobs or a media library.

In scope:

- ordinary file picker/upload and current edit attachment panel;
- full desired set maintained with document edit state and persisted only by the single content Save;
- current read list, safe filename/type/size/hash metadata, download action, removal confirmation explaining historical retention;
- revision-detail attachment list and deterministic added/removed compare alongside existing escaped raw-source diff;
- dirty guard, stale token preservation, Published confirmation, Archived/deleted states, typed errors, and responsive/accessibility behavior.

Out of scope:

- inline preview for PDF/Office/text/ZIP, filename/content editing, attachment search in global search, binary diff, order semantics, bulk operations, or Administrator management.

Acceptance:

- every allowed/denied ordinary type and download header is covered;
- adding/removing/combined content edits create exactly one semantic revision; no-op creates none;
- current removal retains historical download/restore and never performs physical delete;
- compare uses ID/kind set semantics and does not merge same-hash IDs;
- Viewer/Editor/Administrator and deleted-owner historical behavior match the matrix;
- focused frontend/backend gates and one isolated real browser file workflow pass.

Dependencies: ATTACH-B01 PASS; B02 renderer/token foundation PASS.

Deliverable: one task-specific commit and B03 verification report. Stop after B03.

### ATTACH-B04 — Administrator Attachment Management

**Goal:** expose bounded operational visibility and safe orphan cleanup to Administrator only.

In scope:

- admin list/filter/page/detail/statistics contracts and UI;
- current/historical/orphan/deleted-owner projections, shallow storage health, full metadata/key/hash detail;
- on-demand integrity check;
- orphan-only physical delete/DeletePending retry with concurrency, reference revalidation, antiforgery, confirmation, and failure recovery;
- bounded storage-residue diagnostics in verification/operations documentation, without arbitrary file browsing or auto-cleanup.

Out of scope:

- Editor orphan deletion, bulk/forced/reference deletion, metadata edit, restore/rebind, quota manager, filesystem browser, alerting platform, malware service, or new roles.

Acceptance:

- Viewer/Editor receive 403 for every admin route; Administrator cannot bypass reference/concurrency/storage rules;
- totals/filter/page values agree with SQLite metadata/reference truth, including deleted owners;
- integrity check detects missing/length/hash mismatch without canonical mutation;
- delete success, stale token, referenced conflict, both race orders, filesystem failure, retry, and crash-equivalent DeletePending recovery pass;
- no historical reference/file is removed and no unrelated persistent state is touched;
- admin UX follows the existing shell/table/dialog/accessibility/responsive baseline.

Dependencies: ATTACH-B01–B03 PASS.

Deliverable: one task-specific commit and B04 verification report. Stop after B04.

### ATTACH-VERIFY — Final Attachment Phase Verification

**Goal:** prove the four slices compose without regressing security, revision, deletion, search, Trace, or protected persistence.

Required gates:

- serial/focused backend tests under the repository's approved test-infrastructure workaround plus all affected relational/API/storage tests;
- frontend type-check, build, relevant unit tests, and lint when affected;
- authenticated Browser → API → EF Core → temporary SQLite + temporary filesystem coverage for image picker/drag/paste, ordinary file add/remove/download, Published save, history/compare/restore, deleted-owner history, admin statistics/integrity/orphan delete, and role denial;
- whitelist/filename/MIME/signature/size/CSRF/path-traversal/header/XSS/security cases;
- upload/save/delete race and injected filesystem/DB failure cases with no partial authoritative state;
- SQLite integrity/FK checks, metadata-to-file existence/length/hash reconciliation, repository DB/WAL/SHM/hash baseline preservation, and no generated runtime artifact in Git;
- cleanup of only task-started processes, ports, temporary database, keys, storage roots, staging/final objects, logs, and scripts;
- final phase report and index synchronization.

ATTACH-VERIFY may not claim a real Production deployment, malware safety, backup success in an unprovided real environment, object-storage compatibility, or out-of-scope file types.

## 21. Verification gates by invariant

### Persistence and migration

- zero-attachment migration and existing-document/revision migration;
- exact before/after counts/content/hash/pointers/FTS for existing rows;
- DB CHECK/unique/composite FK/RESTRICT behavior;
- no AttachmentReference without same-document Attachment and revision;
- existing revisions start with empty reference sets.

### Revision behavior

- create Revision 1 empty reference set;
- image token/file set/content-only/combined semantic changes;
- reference-set no-op and duplicate-ID validation;
- immutable earlier sets, current derivation, published pointer/time, archived rejection;
- restore-as-new copies exact set and missing storage rolls back;
- deleted owner preserves exact historical read and denies current mutation.

### Storage and security

- every whitelist row and representative denial;
- Unicode filename/header injection/reserved name/path traversal/reparse boundary;
- size termination, staging cleanup, SHA-256, no dedup, response loss/retry;
- current/historical/wrong-document/wrong-revision/wrong-kind/unreferenced role matrix;
- `nosniff`, disposition, private no-store, no static/public path;
- absent/short/corrupt file behavior and safe logs.

### Orphan and concurrency

- upload-only orphan; attached and historical-only are not orphan;
- content save versus admin delete in both serialized orders;
- delete token/reference checks, DeletePending file failure and retry;
- crash-equivalent file-first upload residue is not metadata-visible;
- no automatic/bulk/force delete.

## 22. Risks and accepted trade-offs

| Risk/trade-off | Control/decision |
| --- | --- |
| Immutable revisions retain used files indefinitely | Required for reproducibility; no purge until a separate retention architecture exists. |
| Filesystem and SQLite lack one distributed transaction | File-first upload compensation, DeletePending delete state, immediate reference transactions, reconciliation, and coordinated backup. |
| Unscanned allowed files can be malicious | Narrow whitelist, recognition, forced download, never execute/extract, warning, explicit Internal Pilot limitation. |
| Upload before Save creates orphans | Explicit orphan model, per-document count, admin visibility/manual deletion; no autosave/history corruption. |
| Custom Markdown scheme is nonstandard outside the app | Canonical stable ID is portable through an explicit future export resolver and avoids persisting deployment URLs. |
| No dedup increases capacity | Simpler ownership/deletion/integrity; B04 measures actual usage before a new decision. |
| Historical image API calls add authorization/storage reads | Exact revision security is preferred over public/bare IDs; current size limits are bounded. |
| Production filesystem topology is not supplied | Fail closed on an explicit absolute persistent root; SEC-04 and deployment evidence remain open. |

## 23. Compatibility with frozen/current architecture

| Area | Compatibility result |
| --- | --- |
| Frozen MVP attachment exclusions | ATTACH-A01 is the explicit post-MVP capability authority; frozen sources remain unchanged. |
| KnowledgeDocument Markdown | Raw Markdown remains canonical; only one controlled image destination is added. |
| REV-A01 | Full snapshots, contiguous numbers, no-op, Published/Archived, compare, and restore-as-new remain; attachment membership joins the semantic snapshot. |
| DELETE-A01 | Current deleted owner remains hidden, immutable revision reads remain allowed, no delete revision/restore route is added. |
| Evidence/HumanConfirmation/KnowledgeStatus | Independent and unchanged; Attachment is not Evidence. |
| Viewer/Editor/Administrator | Reused without ACL/role changes; Administrator-only scope is limited to global metadata/integrity/orphan deletion. |
| Current User | Canonical User ID/display snapshot/server UTC remain authoritative. |
| API/error/concurrency | Existing semantic controller, direct response/envelope, JSON opaque token, 401/403/409 conventions retained. |
| Feature-first/direct DbContext | Concrete Attachment feature services/configurations/use cases; no generic repository/file framework. |
| SQLite | Metadata/references only; binaries never stored as BLOB/base64. |
| UI baseline | Main document/editor/history surfaces and one admin page reuse the existing shell, single-overlay, responsive, and accessibility rules. |
| Search/Trace/Unified View | Current canonical document behavior unchanged; file names/bodies are not indexed and attachments add no trace/evidence edges. |
| Production safety | New root follows existing fail-closed persistent path pattern; real Production still requires SEC-04 and coordinated backup evidence. |

## 24. Open questions and gaps

Blocking human decisions: **NONE**.

Non-blocking deployment/implementation confirmations:

1. Deployment owners must choose the real persistent StorageRoot, reviewed numeric limits, filesystem capacity/alerts, ACLs, encryption at rest, and coordinated backup/restore procedure before real rollout.
2. Broader rollout beyond the Internal Pilot must choose an approved malware-control approach or record explicit security risk acceptance; no placeholder scanner is permitted.
3. Any future legacy Office, SVG, additional image/archive, object-storage, CDN, cross-document reuse, retention purge, or automatic cleanup request requires a separate architecture amendment.
4. ATTACH-B01 must measure the two proposed composite/index query paths with `EXPLAIN QUERY PLAN` and add no speculative index beyond demonstrated owner/history/orphan/admin queries.

These are not ATTACH-B01 blockers and do not create a new Product-baseline gap. Existing `SEC-04`, Product acceptance, and Low accessibility gaps remain owned by their current records and are not duplicated here.

## 25. Static verification evidence

ATTACH-A01 is architecture/documentation only:

- inspected the current domain, EF configuration/DbContext, revision service/query/controller contracts, soft-delete historical boundary, access policies, antiforgery, app settings, Production path validation, Markdown decisions, adjacent design decisions, reports, and document index;
- reconciled the deferred frozen MVP attachment statements with this later explicitly approved capability without editing a frozen source;
- did not create a migration, entity, endpoint, package, UI component, runtime process, temporary server, database, filesystem attachment root, or verification port;
- did not open, migrate, seed, checkpoint, or write the repository SQLite database;
- changed only this decision document and `docs/DOCUMENT_INDEX.md`.

No verification-only process/resource was created, so runtime cleanup is not applicable.

## 26. Final gate

```text
ATTACH-A01 APPROVED

Domain:
One immutable binary Attachment owned by one KnowledgeDocument
+ one immutable AttachmentReference per referenced revision

Current Truth:
Derived from the AttachmentReference set of CurrentRevisionNumber

Storage:
Filesystem body + SQLite metadata/reference; opaque random key; SHA-256;
Production absolute persistent StorageRoot outside deployment

History:
Current removal creates a new revision without the reference;
historical references/files remain reproducible; restore copies the exact set

Authorization:
Viewer current/historical read/download; Editor upload and semantic reference
save on editable documents; Administrator global management and orphan-only
physical deletion

Security:
Closed whitelist, streamed limits, server MIME recognition, API-only download,
antiforgery writes, no SVG/static path/public URL, explicit no-malware-scan risk

Physical Delete:
Zero references across all revisions + admin token + DeletePending protocol;
no cascade, force, auto-cleanup, or attachment restore

Database Change Required: YES (ATTACH-B01)
Migration Required: YES (ATTACH-B01)
Production Guide Change Required: YES (ATTACH-B01)
Blocking Human Decisions: NONE
ATTACH-B01 READY: YES
```

The next permitted task is exactly **ATTACH-B01 — Attachment Foundation, Storage, API, and Revision Contract**. Stop after B01 and its verification report; do not begin B02, B03, B04, or ATTACH-VERIFY automatically.
