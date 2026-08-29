# ATTACH-B01 — Attachment Metadata + File Storage + Secure Download + Preview Foundation Verification Report

## Result

```text
ATTACH-B01 PASS
ATTACH-B02 READY: YES
ATTACH PREVIEW SLICE READY: YES
```

Completed on 2026-08-29. No unresolved Blocker or High gap was found. This report covers the backend foundation only; it does not claim ATTACH-B02/B03/B03-PREVIEW frontend UX or a real Production deployment.

2026-08-29 corrective verification: a reported real-browser `PNG 文件头无效` response was investigated without weakening the frozen file policy. The submitted file named `微信截图_20260201110642.png` starts with `FF D8 FF` and is a valid JPEG/JFIF binary, not a PNG; its rejection remains correct. The multipart boundary was nevertheless hardened by removing application-level request-body buffering/reset and manual multipart parsing. Antiforgery and the upload controller now reuse ASP.NET Core's one bounded form parse and `IFormFile`, and staging rejects any mismatch between `IFormFile.Length` and the bytes actually written. Real PNG/JPEG multipart regression proves identical prefixes, lengths and SHA-256 through antiforgery, form parsing and staging.

## Source and baseline

The implementation follows the repository `AGENTS.md`, the frozen ATTACH-A01 architecture, the approved ATTACH-A02 preview amendment, current KnowledgeDocument/Revision/authentication/authorization behavior, and the Production deployment boundary. Frozen sources and Golden UI assets were not edited.

Repository persistence baseline before verification:

| Item | Baseline | Final |
| --- | --- | --- |
| Repository SQLite size | 897,024 bytes | 897,024 bytes |
| Last write UTC | `2026-08-28T14:04:07.3581128Z` | unchanged |
| SHA-256 | `D3E04257042DD7E93FE3D11AFE2A1C75B9B3CAB8FCDCBA1D39D739E7E975BE5C` | unchanged |
| `-wal` / `-shm` | absent / absent | absent / absent |

All migration, API, runtime and integrity work used task-owned temporary SQLite/storage/key paths.

## Preview Requirement Amendment

`docs/design/ATTACH_A02_ATTACHMENT_PREVIEW_CAPABILITY_AMENDMENT.md` is the formal approved delta required by the task. It changes only ordinary-file presentation, the preview delivery boundary, preview limits and the downstream task sequence. It preserves the ATTACH-A01 metadata/reference/storage/revision/authorization/soft-delete/physical-delete/Markdown-image decisions.

The frozen sequence is:

```text
ATTACH-B01
ATTACH-B02
ATTACH-B03
ATTACH-B03-PREVIEW
ATTACH-B04
ATTACH-VERIFY
```

## Implemented foundation

### Metadata and migration

Migration `20260829012501_AddAttachmentFoundation` adds:

- `attachments` with document ownership, original display name, closed extension/kind/canonical MIME, size, opaque storage key, 32-byte SHA-256, `Ready|DeletePending`, canonical creator snapshot, timestamp and optimistic Version;
- `attachment_references` with exact document/revision/attachment ownership;
- composite alternate keys and composite ownership FKs;
- unique revision+attachment and storage-key constraints;
- documented checks and indexes;
- `RESTRICT` only, with no cascade and no fabricated reference rows for existing revisions.

`KnowledgeHubDbContext` rejects application updates/deletes of an existing `AttachmentReference`; new reference rows are written only as part of a new semantic revision snapshot.

### Storage and upload

- Development resolves `App_Data/attachments`; Testing fixtures provide unique absolute temporary roots; Production requires an explicit absolute non-root persistent path outside deployment.
- Keys use `objects/<2hex>/<32hex>.bin`; filenames, IDs and MIME values never enter a path.
- Upload remains distinct from attach/revision. A successful upload is a `Ready` zero-reference orphan and projects `canDownload: false`.
- Request authorization and editable active owner are checked before streaming. The owner/state/count are rechecked under an SQLite immediate transaction before metadata insert.
- Multipart bytes are bounded by `FormOptions`, streamed from the parsed `IFormFile` into a same-root staging file, and hashed incrementally with SHA-256. `IFormFile.Length` must equal staging `SizeBytes`; a mismatch fails closed before content validation or metadata insert. Validation precedes atomic move. DB failure compensation removes the committed object; failed/oversized staging writes remove the temporary file.
- Filename NFC/scalar/control/path/device-name checks, extension whitelist, declared MIME contradiction checks, signatures, strict UTF-8/no-NUL, bounded ZIP/OOXML metadata, macro rejection, entry/expanded-size/compression-ratio ceilings and server canonical MIME are enforced.
- The global header-based antiforgery boundary remains authoritative. Antiforgery and the controller reuse ASP.NET Core's cached bounded form parse; the application no longer rewinds `Request.Body` or runs a second `MultipartReader`. The controller requires exactly one `file` `IFormFile` and streams its body once into staging; neither extension nor declared MIME can bypass signature validation.

### Revision binding

`PUT /api/knowledge-documents/{id}/content` now accepts optional `fileAttachmentIds`:

- absent retains the current ordinary-file set;
- present is the complete requested ordinary-file set;
- exact `![alt](attachment:<id>)` tokens authoritatively produce the image set;
- ownership, kind and `Ready` state are validated inside the semantic-save transaction;
- content equality includes the unordered attachment ID set;
- a real change creates Revision N+1 and one complete immutable reference snapshot.

Restore copies the exact source reference set into the new restore revision only after every source attachment is still `Ready` and matches its stored size/SHA-256. A missing/corrupt object returns `409 attachment_unavailable` with no head/revision/reference write.

### Secure current and historical delivery

Current routes resolve only through the active owner, `CurrentRevisionNumber`, exact revision and exact reference. Historical routes resolve through the exact requested revision and remain available under the approved soft-deleted-owner historical boundary. An orphan, another document's ID or an ID from another revision resolves as `404` without cross-context disclosure.

Download is `attachment`; image/PDF delivery is `inline`; every byte response uses canonical MIME, safe ASCII+RFC 5987 filename handling, `nosniff` and `private, no-store`. No route accepts or returns a storage key/path, and no static attachment directory is enabled.

### Administrator physical-delete foundation

The minimal B01 foundation provides Administrator-only metadata/refcount and single-item orphan deletion. It does not implement the B04 list/statistics/UI/filesystem-browser scope. Delete:

1. validates the concurrency token under an immediate transaction;
2. rejects any all-revision reference count above zero;
3. transitions `Ready` to `DeletePending` and increments Version;
4. deletes the exact opaque object;
5. revalidates zero references and deletes metadata;
6. supports retry with the new token after filesystem failure.

Viewer/Editor receive 403. Stale tokens conflict, referenced files return 422, and there is no bulk/force/automatic cleanup.

## Preview Type Matrix

| Type | Mode | B01 behavior | Result |
| --- | --- | --- | --- |
| PNG/JPEG/GIF/WEBP | `Image` | Protected current/historical content route; Markdown token is authoritative | PASS |
| PDF | `Pdf` | Protected original bytes, canonical `application/pdf`, inline; independent download remains | PASS |
| TXT/LOG/SQL/JSON/XML | `Text` | Strict UTF-8 source as bounded JSON string, never HTML/execution | PASS |
| Markdown | `Markdown` | Bounded source string only; no second renderer or trusted HTML | PASS |
| CSV | `Csv` | Bounded quoted-field parser returning string rows and explicit truncation reasons | PASS |
| XLSX | `Spreadsheet` | Bounded workbook/sheet metadata and display strings; exact sheet selection; cached formula result only | PASS |
| DOCX/PPTX/ZIP | `None` | Metadata plus secure download only; preview returns 422 | PASS |

The backend is authoritative for `previewMode`, `canPreview` and context-specific `canDownload`; clients do not need to guess from an extension.

## Secure Preview Boundary

Current and historical `/preview` routes reuse the same exact reference authorization context as download. PDF returns bytes only. Text/Markdown/CSV/XLSX return JSON and set `nosniff`/`private, no-store`. `Image` directs the client to `/content`; `None` and Image-on-preview return `422 preview_not_supported`. A supported XLSX that exceeds the preview workbook ceiling returns `422 preview_limit_exceeded` and remains downloadable. Missing/corrupt referenced content fails closed as `503 attachment_unavailable`.

No Preview response emits user-controlled HTML, executes JSON/XML/SQL/LOG, opens ZIP/DOCX/PPTX content, loads an external relationship, starts Office/COM, or exposes a formula expression.

## PDF

Authenticated focused and isolated runtime checks verified canonical `application/pdf`, safe inline disposition, independent attachment download, `nosniff`, private no-store cache control, no path disclosure, anonymous denial and wrong-context 404.

## Text-family

Strict UTF-8 without NUL is required at upload. Text/Markdown source is returned inside JSON only. The 256 KiB default is byte-bounded without splitting a UTF-8 scalar; response metadata reports returned/maximum bytes and truncation. Invalid UTF-8 was rejected with 415. JSON/XML/SQL/LOG remain inert text by policy.

## CSV

The bounded parser handles commas, quoted fields, escaped quotes and line breaks, returns arrays of strings, caps rows/columns/characters and reports `Rows`, `Columns` and/or `Characters`. Markup/formula-looking content remains a JSON string. A focused case returned `<script>alert(1)</script>` as inert data and demonstrated both row and column truncation.

## XLSX

The implementation uses .NET BCL `ZipArchive` plus hardened streaming `XmlReader`; no new package or Office editing framework was introduced. Package recognition rejects macro-enabled content and invalid/mismatched OOXML. Preview bounds workbook bytes, sheet metadata, rows, columns, XML characters, shared-string/display characters and package structure. It reads only internal workbook/worksheet/shared-string parts, ignores formula expressions and returns only a cached stored result when present. Focused and isolated runtime cases verified sheet selection, shared strings, cached value `5`, absence of formula text and download-only fallback above the preview ceiling.

## No-preview Download-only Types

Focused DOCX, PPTX and ZIP cases projected `previewMode: None` and `canPreview: false`, returned 422 from `/preview`, and retained authenticated canonical download. There is no server conversion, archive browse, extraction, HTML generation or online editing path.

## Preview Limits

All settings live under `Attachments`; invalid, non-positive or safety-ceiling-breaking values fail startup. Defaults:

| Limit | Default |
| --- | ---: |
| Image upload | 10 MiB |
| Ordinary file upload | 50 MiB |
| Stored attachment metadata rows/document | 100 |
| Text preview | 256 KiB |
| CSV | 200 rows / 50 columns / 256 Ki characters |
| XLSX workbook preview | 10 MiB |
| XLSX | 20 sheets / 200 rows / 50 columns / 1 MiB shared/display characters |

Production configuration, least-privilege storage, proxy/temp capacity and coordinated SQLite+object backup/restore guidance is synchronized in `docs/PRODUCTION_DEPLOYMENT_GUIDE.md`.

## Automated verification

| Gate | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln -c Release --no-restore` | PASS — 0 warnings, 0 errors |
| Focused `FullyQualifiedName~Attachment|FullyQualifiedName~StartupConfigurationProcessTests` | PASS — 19/19, 0 failed, 0 skipped |
| Approved serial full backend with `xUnit.ParallelizeTestCollections=false` | PASS — 186/186, 0 failed, 0 skipped, 1 min 25 s |
| Corrective `AttachmentFoundationApiTests` | PASS — 9/9, including real PNG/JPEG multipart byte round trip |

Focused coverage includes schema/check/FK/index/no-cascade/ownership/duplicate/immutability, upload image/file/empty/extension/MIME/signature/UTF-8/filename/role/archived/SHA/staging cleanup, exact semantic snapshots, image token/kind, current/history/soft-delete/restore, secure headers/path non-disclosure, bounded PDF/text/CSV/XLSX, macro/invalid/oversized XLSX, DOCX/PPTX/ZIP download-only behavior, admin role/reference/stale token/DeletePending retry and storage removal.

The corrective multipart regression additionally captures the request `Content-Length`, cached `IFormFile.Length`, first 24 `IFormFile` bytes, staging `SizeBytes`, staging first 24 bytes and SHA-256. The valid PNG case recorded `366 / 68 / 68` bytes and SHA-256 `431CED6916A2A21A156E38701AFE55BBD7F88969FBBFC56D7FE099D47F265460`; the valid JPEG case recorded `994 / 695 / 695` bytes and SHA-256 `5518D9ADEE1D3DB049900AE8F8829E9A57E0F4DC7E0E27D7386D3AB405BC0CE7`. Both stored byte arrays equal their originals. The same JPEG fixture deliberately mislabeled `.png` / `image/png` remains 415 with `PNG 文件头无效。`.

The approved serial full gate was used because existing low `REV-GAP-011` records that the default parallel SQLite/WebApplicationFactory collections can stall.

## Isolated Runtime Smoke

An actual Release API process ran in Development on a task-owned loopback port with:

- task-owned SQLite, Data Protection keys and attachment root;
- a disposable local Administrator created through password stdin;
- real antiforgery, login Cookie and authenticated HTTP requests;
- PDF, TXT, ZIP and XLSX multipart uploads followed by one semantic save;
- PDF inline preview and download;
- safe TXT JSON preview;
- XLSX sheet preview with cached result and no formula expression;
- ZIP preview denial and successful download;
- anonymous download denial.

Final runtime evidence:

```json
{"result":"PASS","login":"PASS","pdfPreview":"PASS","pdfDownload":"PASS","textPreview":"PASS","xlsxPreview":"PASS","downloadOnly":"PASS","anonymousDenied":"PASS","sqliteIntegrity":"ok","foreignKeyViolations":0,"attachments":4,"attachmentReferences":4,"storageObjects":4,"stagingResidue":0}
```

The runtime API was stopped. The exact task-owned database, keys, logs, upload fixtures and attachment root were removed. No runtime process, port, database, object or staging file was intentionally left behind.

## Integrity and cleanup

- Runtime SQLite: `integrity_check=ok`; foreign-key violations `0`.
- Runtime metadata/object reconciliation: 4 attachments, 4 references, 4 objects, 0 staging files.
- Test factories: unique absolute temporary roots removed on disposal.
- Repository SQLite/WAL/SHM/hash baseline: unchanged.
- Temporary serial runsettings, checker project and runtime script are verification-only and removed before delivery.

## Existing and new gaps

- Existing low `REV-GAP-011` remains open; the approved serial backend gate passed 186/186.
- Existing SEC-04 real-Production deployment boundary remains blocked until a selected environment proves TLS/proxy, durable paths, secrets, monitoring and coordinated backup/restore. This does not block the local ATTACH-B01 implementation result.
- The frozen Internal Pilot limitation remains: uploads are recognized and tightly bounded but are not antivirus-scanned. Wider rollout requires the ATTACH-A01 malware-control decision/risk acceptance.
- No new Blocker, High or Medium gap was found.

## ATTACH-B03-PREVIEW Readiness

The future preview UI has authenticated current/historical routes, authoritative metadata modes, bounded typed JSON for text/CSV/XLSX, safe PDF inline delivery, explicit limit/unsupported/unavailable errors, and download fallback. It does not need a public URL, storage path, renderer duplication, Office automation or client-side capability guessing.

```text
ATTACH-B02 READY: YES
ATTACH PREVIEW SLICE READY: YES
```

ATTACH-B02 was not started.
