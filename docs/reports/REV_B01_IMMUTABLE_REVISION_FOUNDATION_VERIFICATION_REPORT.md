# REV-B01 — Immutable Revision Foundation Verification Report

**Result: REV-B01 PASS**

**Verification date:** 2026-08-23  
**Scope:** Immutable `KnowledgeDocumentRevision` foundation only

## 1. Normative basis and worktree safety

Implementation was checked against `AGENTS.md`, the frozen REV-A01 architecture/contract decision and freeze report, the next-capability plan, the knowledge-content architecture plan, and the referenced KC/AUTH verification reports.

The repository was already materially dirty before REV-B01. The initial `git status`, `git diff --stat`, and `git diff` baseline was recorded. Existing modifications, renames, untracked deliverables, and unrelated feature work were preserved; no reset, clean, revert, broad formatting, or unrelated refactor was performed.

## 2. Actual SQLite preflight — PASS

The repository database `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` was opened read-only with SQLite `query_only=ON` before generating the migration.

| Check | Observed result |
|---|---|
| Integrity / foreign keys | `integrity_check=ok`; no `foreign_key_check` rows |
| Knowledge documents | 1 row; lifecycle distribution: Published=1 |
| IDs / versions | no invalid or non-positive IDs; Version min/max both 2 |
| Canonical content limits | no invalid Title, Summary, or Body rows |
| Author references | no orphan author FK |
| Evidence | 9 total; 4 HumanConfirmation; 0 KnowledgeDocument HumanConfirmation |
| Relationships | 1 row retained |
| FTS | 1 document / 1 FTS row; no missing, extra, or derived-content mismatch |
| Existing revision schema | revision table, document pointers, and evidence snapshot column were absent |

No partial or conflicting revision schema, orphan data, invalid limits, or untruthful backfill condition was found. The hard data-review gate therefore passed.

## 3. Schema, domain model, and invariants — PASS

Added `KnowledgeDocumentRevision` with the frozen fields and the controlled origins `Created`, `ContentSave`, `Restore`, and `MigrationBaseline`. B01 writes only Created, ContentSave, and MigrationBaseline; Restore remains schema/domain support only.

The additive model contains:

- `knowledge_documents.current_revision_number`, required and initialized to 1;
- nullable `knowledge_documents.latest_published_revision_number`;
- nullable `evidence.knowledge_document_revision_number_snapshot`;
- insert-only `knowledge_document_revisions` persistence with no update/delete/CRUD API.

EF/SQLite constraints enforce a RESTRICT document FK, nullable RESTRICT author-user FK, unique `(knowledge_document_id, revision_number)`, positive revision numbers, controlled origin/lifecycle values, exact actor-null rules for MigrationBaseline, required trusted actor fields for other origins, and mutually consistent Restore fields. Snapshot lengths match the current document limits. `Version` remains the sole opaque concurrency source and is not equated with `CurrentRevisionNumber`.

## 4. Migration and deterministic baseline — PASS

Migration `20260823092808_AddImmutableKnowledgeDocumentRevisions` is additive and contains migration-time preflight/postflight guards.

For every existing document, Up creates exactly one Revision 1 with exact current Title/Summary/Body, current lifecycle context, `MigrationBaseline`, null actor/change/restore fields, and one captured UTC migration time. It sets CurrentRevisionNumber=1; only Published documents receive LatestPublishedRevisionNumber=1. Existing HumanConfirmation snapshot values remain null.

The migration was applied to the actual repository database. Postflight proved:

- latest applied migration is `20260823092808_AddImmutableKnowledgeDocumentRevisions`;
- 1 document and exactly 1 baseline revision;
- zero content snapshot or pointer mismatch;
- the existing document Version remained 2;
- 9 Evidence rows, 4 HumanConfirmation rows, and zero non-null revision snapshots were preserved;
- 1 Relationship and the Published lifecycle/KnowledgeStatus were preserved;
- FTS still had exactly one matching current-head row;
- the unique revision index and both RESTRICT FKs were present;
- `integrity_check=ok` and `foreign_key_check` was empty.

Down supports the tested baseline-only rollback. It explicitly refuses operational rollback once real Created/ContentSave/Restore history, revision numbers above 1, non-null evidence snapshots, count/pointer divergence, or head/baseline divergence exists.

Real SQLite migration tests passed for zero/existing pre-REV databases, exact baseline content/count/pointers, existing HumanConfirmation null snapshots, constraints/indexes/FKs, valid and invalid origin/actor/Restore combinations, unique revision numbers, data preservation, safe baseline Down, and refusal to drop real history.

## 5. Create, content save, and semantic no-op — PASS

Create uses one transaction for Draft/Unknown head creation, Revision 1 (`Created`), current pointer initialization, trusted current-user snapshot, server time, and current-head FTS. A forced revision-insert failure test proved that Document, Revision, and FTS roll back together.

Content save reuses the frozen normalization rules: Title trim/required/max 300/ordinal; Summary trim/blank-to-null/max 2000/ordinal; Body CRLF/CR-to-LF/max 1,000,000/ordinal; optional ChangeSummary trim/blank-to-null/max 500.

A semantic change validates the opaque concurrency token, rejects Archived state, advances the current revision, updates the head with trusted actor/time, inserts exactly one ContentSave revision, advances Version, updates current FTS, and commits atomically. Title-only, summary-only, and body-only changes were each verified.

When all three normalized content fields are unchanged, the API returns the current detail without changing Revision count, Version, current/published pointers, actor/time, PublishedAt, KnowledgeStatus, or FTS. ChangeSummary-only input is ignored for history creation. A stale token leaves no partial head/revision write.

## 6. Published, Draft, Archived, and trusted authority — PASS

- Draft→Published creates no content revision, points LatestPublishedRevisionNumber at the current revision, and uses server transition time.
- Published content save remains Published, creates the next revision, moves the published pointer to it, and assigns the same trusted save time to PublishedAt/revision/head update.
- Published semantic no-op changes neither PublishedAt nor pointer/version/revision/time.
- Published→Draft retains the last published pointer/time; Draft saves advance only the current pointer; republish points at the current revision.
- Published→Archived retains the published pointer/time and creates no revision.
- Archived content save returns `409 invalid_state`; revision, head Version, and FTS remain unchanged.
- KnowledgeStatus never advances automatically.

All author identity/display snapshots come from `ICurrentUserContext`; all persistence times are server generated. Browser-supplied actor/time data cannot author a revision.

## 7. HumanConfirmation revision capture and coverage — PASS

`POST /api/evidence/human-confirmations` now accepts `subjectRevisionNumber`. It is required for KnowledgeDocument, forbidden for other subject types, and compared with the current document revision inside the confirmation transaction. A mismatch returns `409 conflict` and creates no Evidence. A match stores the current revision in `KnowledgeDocumentRevisionNumberSnapshot` while retaining the trusted Current User and factual ConfirmedAt semantics.

Ordinary Evidence always stores a null revision snapshot. Existing/legacy HumanConfirmation remains null rather than being guessed as Revision 1.

KnowledgeDocument detail now projects `currentRevisionNumber`, `latestPublishedRevisionNumber`, and `{ state, lastConfirmedRevisionNumber }`. Tests covered all frozen states:

- `NoConfirmation`;
- `LegacyConfirmationUnknown`;
- `CurrentRevisionConfirmed`;
- `ChangedSinceConfirmation`.

A snapshot greater than the current revision is detected as a data-integrity violation instead of being silently mapped. Coverage is derived read state only and never changes KnowledgeStatus.

## 8. FTS and existing-feature regressions — PASS

FTS continues to index only committed current KnowledgeDocument heads. Create and semantic save update it in the same transaction; no-op and rejected/rolled-back writes do not touch it. Revision history is not indexed.

Relationships remain bound to KnowledgeDocument rather than revisions; Supersedes remains a relationship vocabulary concept. Unified View continues to return the current document only. No lifecycle, status, evidence, relationship, or unified-view operation manufactures a content revision.

Affected backend regression selection passed 40/40 tests across KnowledgeDocuments, Evidence, KnowledgeStatus, Relationships, Search/FTS capability and performance, Unified View, authorization-sensitive write paths, revision API behavior, and revision/search migration paths.

## 9. Frontend compatibility — PASS

The frontend received only the required typed compatibility fields: current/published revision pointers, confirmation coverage, HumanConfirmation subject revision, Evidence revision snapshot, and optional ChangeSummary request support. The current revision is passed into the existing HumanConfirmation authoring path. Existing Open→Edit→Save→Reload behavior remains operational.

No History, Compare, Diff, Restore, or historical-preview UI was introduced.

## 10. Automated verification — PASS

| Gate | Result |
|---|---|
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors |
| REV-B01 migration tests | PASS — 2/2 |
| REV-B01 API tests | PASS — 4/4 |
| affected backend regression selection | PASS — 40/40 |
| `npm run type-check` | PASS |
| impacted KnowledgeDocument/Evidence Vitest | PASS — combined impacted files passed; detail view re-run independently passed 6/6 after one transient worker-start timeout |
| `npm run build` | PASS — Vite production build completed; only the existing non-blocking chunk-size advisory was emitted |
| scoped ESLint | PASS — no warnings/errors |
| `git diff --check` | PASS — exit 0; only existing CRLF conversion notices |

No new testing framework, generic fixture layer, or broad redundant test suite was added.

## 11. Runtime Browser → API → EF Core → SQLite chain — PASS

An isolated temporary SQLite database, disposable Local Administrator, isolated Data Protection key directory, API on 127.0.0.1:5109, and Vite on 127.0.0.1:5176 were used. The repository database and real users were not used for runtime authoring.

The real browser logged in and created KnowledgeDocument 1 through the UI. The detail page rendered the Draft/Unknown document, body, evidence/status areas, and later reloaded the final current content with no browser console errors. API/EF/SQLite verification then proved:

| Runtime step | Verified result |
|---|---|
| Create | head CurrentRevision=1; exactly Revision 1, Created, Draft, trusted administrator actor |
| Edit/save | CurrentRevision=2; Revision 2 ContentSave; opaque Version/token changed; reload returned the saved title/body |
| Semantic no-op | revision remained 2; concurrency token and UpdatedAt/Version remained unchanged |
| Publish | lifecycle Published; LatestPublishedRevision=2 |
| Published edit/save | Revision 3; lifecycle remained Published; pointer=3; PublishedAt advanced |
| Current confirmation | HTTP 201; stored snapshot=3; coverage CurrentRevisionConfirmed; KnowledgeStatus Unknown |
| Edit after confirmation | Revision 4; coverage ChangedSinceConfirmation; last confirmed=3; KnowledgeStatus Unknown |
| Stale confirmation | HTTP 409 `conflict`; no new confirmation |
| Ordinary Evidence | HTTP 201; revision snapshot null |
| Archive | lifecycle Archived; current/published pointers both 4; KnowledgeStatus Unknown |
| Archived content save | HTTP 409 `invalid_state`; revision remained 4 |

Final read-only SQLite inspection reported `integrity_check=ok`, zero FK violations, four immutable revisions, exact head/latest-revision equality, exact current-head FTS equality, snapshots `3,null`, head Version 6, Archived lifecycle, and Unknown KnowledgeStatus. API responses and persisted rows agreed.

## 12. Diff scope and cleanup — PASS

REV-B01 changes are confined to KnowledgeDocument revision domain/configuration, the additive EF migration/model snapshot, current document and Evidence fields, KnowledgeDocument create/save/lifecycle/query contracts, HumanConfirmation capture/query contracts, minimal frontend typing/propagation, and focused API/migration/frontend tests. Pre-existing unrelated dirty-worktree changes were not edited for cleanup or formatting.

The agent-created browser tab was closed and the automation session had zero tabs. Only the task-owned API and Vite sessions were stopped. Ports 5090, 5109, and 5176 had no remaining listeners. The validated task temp directory was removed together with its runtime SQLite database, disposable account data, logs/state, and Data Protection keys; a final check confirmed the directory no longer existed.

## 13. Explicitly not implemented

REV-B01 does not implement History UI, revision list/detail UX, compare/Myers diff, Restore API/UI/use case, historical preview, historical FTS, Attachments, Spaces, Comments, Approval, Incident, AI/RAG, branch/merge/CRDT, or a generic audit/version framework.

REV-B02 was not started. The implementation stops here for the human Verification Gate.
