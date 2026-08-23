# KC-B01 — KnowledgeDocument Foundation + Persistence Verification Report

## Result

```text
KC-B01 PASS
```

## Architecture Conformance

- Added one `KnowledgeDocument` aggregate in `Features/KnowledgeDocuments`; no separate Feature/table exists for any document type.
- The controlled text-persisted `DocumentType` values are exactly `Requirement`, `Specification`, `TestCase`, `Sop`, `Troubleshooting`, `KnowledgeArticle`, and `DesignNote`.
- Canonical authored content is `body_markdown` Markdown text. No HTML, editor JSON, metadata JSON, typed extension, generic repository, or content-type framework was introduced.
- Structured Knowledge remains unchanged and canonical. This slice does not alter Systems, BusinessFunctions, Database Knowledge, BusinessRules, Integrations, Relationships, Evidence, HumanConfirmation, Search, or existing KnowledgeStatus behavior.

## Implemented

| Area | Implementation |
|---|---|
| Domain | `KnowledgeDocument`, `DocumentType`, and `DocumentLifecycleStatus` (`Draft`, `Published`, `Archived`) |
| Persistence | EF configuration, `DbSet<KnowledgeDocument>`, additive migration, text enum conversions, FK restrictions, version token support |
| Create | `POST /api/knowledge-documents`, Editor minimum, trusted Current User authorship |
| List | Paginated `GET /api/knowledge-documents` with title/summary query, type, lifecycle, KnowledgeStatus, and explicit finite sort options |
| Detail | `GET /api/knowledge-documents/{id}` with canonical Markdown, authorship snapshots, timestamps, axes, and opaque token |
| Content update | `PUT /api/knowledge-documents/{id}/content`, Editor minimum, token validation/conflict behavior, trusted UpdatedBy attribution |
| Lifecycle foundation | Schema and enum are stable; lifecycle API is deliberately deferred to KC-B02 |
| Security | Existing Viewer fallback and Editor policy; current-user resolution; existing antiforgery pipeline |

## Schema

Migration: `20260822060141_AddKnowledgeDocumentFoundation`.

`knowledge_documents` contains the planned core identity, type, content, lifecycle, KnowledgeStatus metadata, created/updated canonical User IDs and display snapshots, timestamps, publish/archive placeholders, and app-managed `version`.

- `document_type` is `TEXT NOT NULL` with the seven-value CHECK constraint.
- `lifecycle_status` is `TEXT NOT NULL` with `Draft`/`Published`/`Archived` CHECK.
- `knowledge_status` retains the existing `Unknown`/`Inferred`/`Confirmed` CHECK.
- Both author FKs reference `users.id` with `ON DELETE RESTRICT`; EF also creates its normal FK-supporting indexes.
- Required planning index: `(document_type, lifecycle_status, updated_at)`.
- No FTS table, author-specific product index, relationship/evidence join table, or delete route was added.

## Initial Semantics

```text
Create
  -> LifecycleStatus: Draft
  -> KnowledgeStatus: Unknown
```

Lifecycle and KnowledgeStatus are independent axes. Content updates never change either value. KC-B01 does not expose a lifecycle operation and does not extend the existing KnowledgeStatus target integration; those are later slices.

Title is trimmed and limited to 300 characters, optional summary is trimmed/null-normalized and limited to 2,000 characters, and Markdown is limited to 1,000,000 characters. Markdown normalization only converts CRLF/CR to LF; it does not parse, reformat, or sanitize Markdown. Raw HTML is not rendered in this slice; a future renderer must apply its own sanitizer.

## Authorship and Concurrency

```text
Authenticated Principal
  -> LoginIdentity
  -> canonical User / ICurrentUserContext
  -> CreatedBy / UpdatedBy User ID and display snapshot
```

Create and content update resolve `ICurrentUserContext` in the API boundary. Request bodies have no actor, User ID, or display-name field. The focused spoof-header regression confirms an `X-Current-User-Id` value cannot override the authenticated author.

The API exposes only the existing opaque `concurrencyToken`. Successful content update returns a new token; malformed tokens return 400 `validation_error`; stale tokens return 409 `conflict`. Raw `Version` is not exposed.

## API Authorization Matrix

| Endpoint | Viewer | Editor | Administrator |
|---|---:|---:|---:|
| `GET /api/knowledge-documents` | Allow | Allow | Allow |
| `GET /api/knowledge-documents/{id}` | Allow | Allow | Allow |
| `POST /api/knowledge-documents` | 403 | Allow | Allow |
| `PUT /api/knowledge-documents/{id}/content` | 403 | Allow | Allow |
| Lifecycle endpoint | Not implemented | Not implemented | Not implemented |

Unsafe endpoints have no antiforgery exemption and run through the existing SEC-03 antiforgery middleware.

## Migration Preservation

`KnowledgeDocumentMigrationTests` starts from `20260822025403_AddOidcAuthenticationFoundation`, creates a User, LoginIdentity, two Systems, a KnowledgeRelation, and a HumanConfirmation Evidence record, then upgrades to the KC-B01 migration. It verifies all corresponding IDs/references remain readable and that the new document table is empty after upgrade. The migration only creates `knowledge_documents` and its indexes; no existing table rebuild is generated.

## Runtime Verification

The `KnowledgeDocumentsApiTests` integration host performs the required real request path:

```text
HTTP client
  -> ASP.NET Core TestServer/controller
  -> KnowledgeDocument service/query
  -> EF Core
  -> SQLite
```

It creates Draft documents for all seven types, lists/filter/sorts, loads detail, updates Markdown, asserts LF normalization, verifies authorship and token rotation, and reads the persisted result. The same host verifies Viewer denial, Editor and Administrator writes, antiforgery-backed unsafe endpoints, Current User spoof resistance, constraints, and lifecycle enum text persistence.

## Tests and Build

| Command | Result |
|---|---|
| `dotnet build SystemKnowledgeHub.sln --no-restore` | Passed: 0 warnings, 0 errors |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~KnowledgeDocumentsApiTests|FullyQualifiedName~KnowledgeDocumentMigrationTests"` | Passed: 4; failed: 0; skipped: 0 |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~KnowledgeDocumentsApiTests|FullyQualifiedName~KnowledgeDocumentMigrationTests|FullyQualifiedName~CurrentUserApiTests|FullyQualifiedName~AccessControlApiTests|FullyQualifiedName~UsersApiTests|FullyQualifiedName~EvidenceApiTests|FullyQualifiedName~AntiforgeryApiTests|FullyQualifiedName~KnowledgeStatusApiTests|FullyQualifiedName~RelationshipsApiTests|FullyQualifiedName~HumanConfirmationSnapshotMigrationTests|FullyQualifiedName~OidcAuthenticationFoundationMigrationTests"` | Passed: 27; failed: 0; skipped: 0 |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build` | Passed: 79; failed: 0; skipped: 0 |

No frontend contracts were necessary for this backend foundation, so no frontend type-check/build was run. No verification server or watcher was started; all test hosts were one-shot and exited.

## Deviations

There are no architecture deviations.

- KC-B01 intentionally provides lifecycle persistence foundation without a lifecycle API. This is explicitly allowed by the slice instruction and leaves publish/archive UI/workflow to KC-B02.
- The list response additionally includes `createdAt` to support the approved `createdAt:desc` sort while retaining the required list fields.

## Explicitly Not Implemented

This slice does not implement an editor, frontend document pages, navigation, router changes, sidebar, typed frontend API plumbing, templates, lifecycle UI/API, relationships, Evidence/HumanConfirmation subject integration, KnowledgeStatus target integration, Search/FTS, Unified View, revision history, attachments, tags, spaces, comments, autosave, AI/RAG, or any document-type-specific workflow.

## Pre-existing Worktree and Scope Review

The worktree was already substantially modified/untracked before KC-B01, including prior security, XML documentation, Evidence, frontend, migration, and report work. KC-B01 changed only the new `KnowledgeDocuments` Feature, minimal `KnowledgeHubDbContext`/DI registration, its generated migration and model snapshot, focused tests, and this report. It did not revert, format, or overwrite unrelated work. `git diff --check` completed with no whitespace errors; Git continues to emit the pre-existing inaccessible global-ignore warning.

## Final Scope Decision

KC-B01 is complete. No KC-B02 or later work was started.
