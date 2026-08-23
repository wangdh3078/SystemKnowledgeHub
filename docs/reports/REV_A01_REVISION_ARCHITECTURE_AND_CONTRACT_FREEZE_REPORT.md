# REV-A01 — Revision Architecture and Contract Freeze Report

## Result

```text
REV-A01: APPROVED WITH CHANGES
Architecture questions left to implementation: NONE
REV-B01 authorized now: NO — awaiting Human Product / Architecture acceptance
```

The normative contract is [REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md](../design/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md).

No production code, frontend, database, migration, test, frozen specification, or Golden asset was changed by REV-A01.

## 1. Scope and Baseline

REV-A01 was a read-only Product / Architecture freeze over the already selected PHASE-REV. It reviewed:

- `AGENTS.md`, the next-capability plan/report, Knowledge Content architecture, and KC-C01 decision;
- KC-B01–B07, UI-B04, PHASE-KC-R01, KC-C02, AUTH-B01/B02 final reports;
- current KnowledgeDocument entity/configuration/service/query/controller/contracts;
- Current User and access policies;
- lifecycle, KnowledgeStatus, Evidence/HumanConfirmation, relationships, FTS, Unified View, DbContext, migrations/model snapshot;
- Vue KnowledgeDocument contracts/API/detail/editor, safe Markdown, dirty/conflict behavior, overlay architecture, and focused tests.

At task start, `git status`, `git diff --stat`, and `git diff` recorded a heavily dirty worktree containing pre-existing AUTH, UI, KC, migration, test, report, and documentation-reorganization work. REV-A01 preserved all of it and performed no reset, clean, revert, broad formatting, migration generation, or implementation.

## 2. Real Code Inventory

| Concern | Verified current implementation | Freeze impact |
| --- | --- | --- |
| Current content | One `KnowledgeDocument` row holds Title/Summary/Body and one `Version`. | Add child snapshots; head remains canonical. |
| Limits/normalization | 300 title, 2,000 summary, 1,000,000 Markdown; trim/null/LF normalization. | Snapshot and semantic-change rules reuse these exact boundaries. |
| Create | Current User-backed Draft/Unknown create, transaction with FTS. | Add revision 1 in the same transaction. |
| Save | One content PUT updates head, author/time, version, FTS transactionally. | Extend this endpoint with optional `changeSummary`; no second save endpoint. |
| No-op | No equality check; a no-op currently rotates token/history metadata. | REV-B01 must stop no-op writes completely. |
| Archived | UI hides edit, relation, Evidence/status actions; service content PUT itself does not reject Archived. | Backend Archived rejection is mandatory, not optional cleanup. |
| Published | Save retains Published but does not update `PublishedAt`. | New revision becomes latest published and advances publication time. |
| Concurrency | Opaque token encodes app-managed document `Version`. Lifecycle/status also advance it. | Revision number is a separate content sequence; no second token. |
| KnowledgeStatus | Explicit server-trusted actor; no automatic status on content/Evidence. | Preserve behavior; derive warning only. |
| HumanConfirmation | Server Current User/KnowledgeRole snapshot; `ConfirmedAt` is a factual input. | Capture current revision number; do not infer coverage from time. |
| FTS | Derived current-content FTS5; archive excluded from results. | Reindex only committed current head; no history search. |
| Frontend | Detail route, safe renderer, Milkdown edit, dirty guard, Ctrl/Cmd+S, 409 retention, single overlay. | Add in-route Main Content history/compare; reuse safe renderer and guards. |

## 3. PHASE-NEXT Defaults Accepted

The freeze retains the prior recommendation that:

- `KnowledgeDocument` is the current mutable head;
- revisions are immutable child snapshots;
- create/save/restore create revisions atomically;
- Published save remains immediately Published;
- restore is Draft-only and creates a new head;
- Evidence/KnowledgeStatus remain document-level;
- diff is derived and line-oriented;
- revision history has Viewer read and Editor/Administrator write boundaries;
- migration is additive and current FTS/Unified View remain current-head projections;
- Spaces, attachments, collaboration, Incident, AI/RAG, branching, and generic audit/version frameworks remain excluded.

## 4. Approved Changes and Clarifications

| Change | Why it is required |
| --- | --- |
| `CurrentRevisionNumber` on document | `Version` also changes for lifecycle/status and cannot identify content head. |
| nullable `LatestPublishedRevisionNumber` | Publication must identify a revision without mutating the immutable child. |
| MigrationBaseline author null and capture time | `UpdatedBy/UpdatedAt` also reflect non-content writes and cannot truthfully reconstruct history. |
| existing Draft/Archived latest-published pointer null | Old publication content cannot be proven; Archived backend writes were not rejected. |
| HumanConfirmation revision snapshot and required request expectation | Prevents timestamp races and stale-page confirmation while keeping Evidence document-level. |
| four-state confirmation coverage projection | Distinguishes no confirmation, known current, changed, and unknowable legacy confirmation. |
| frontend bounded Myers diff; no compare API | Immutable snapshots are already readable; no stored/generic/server diff is needed. |
| backend Archived save rejection | Frozen product semantics cannot rely only on hidden frontend buttons. |
| `PublishedAt` advances on Published content save | It must describe when the latest published revision became published. |
| dedicated restore reason | Mandatory recovery rationale is not the same as optional normal change summary. |

These are why the formal result is `APPROVED WITH CHANGES` rather than plain `APPROVED`.

## 5. Frozen Domain Contract

- Revision numbers are contiguous `1..N`, unique per document, and never reused/deleted.
- `CurrentRevisionNumber` points to the snapshot equal to current Title/Summary/Body.
- Document `Version` remains the only concurrency source and is not equal to revision number.
- `LatestPublishedRevisionNumber` is nullable and may point to an older revision while current lifecycle is Draft.
- Revision origins are exactly `Created`, `ContentSave`, `Restore`, `MigrationBaseline`.
- Revision lifecycle context is capture metadata, not independent state.
- Baseline has no author; Created/ContentSave/Restore require server canonical User ID/name snapshot.
- Change summary is optional, trim/blank-null, max 500.
- Restore reason is separate, required after trim, length 5–500.
- Revisions have no independent lifecycle/status/evidence/relationship/permission/version.

## 6. Frozen Write Semantics

### Create

Document and revision 1 are committed together with identical canonical content, Current User snapshot, Draft context, origin Created, current number 1, no published pointer, and current FTS.

### Save

Normalized Title/Summary/Body equality determines change. Any changed field creates exactly one next revision and updates head in one transaction. Published save also advances latest-published pointer and `PublishedAt`. Archived save is `409 invalid_state`.

### No-op

Returns unchanged detail with no revision, token/version/time/actor/pointer/FTS change. Change-summary-only cannot create a revision.

### Lifecycle and adjacent facts

Lifecycle, status, Evidence/HumanConfirmation, and relationships create no content revision. Draft publish points latest-published at current; return to Draft retains it; republish points it at current; archive retains it.

### Restore

Editor/Administrator only, Draft only, current token, required reason, historical source lower than current, and content must differ. Server copies source content and creates a new Restore revision with lineage. It preserves lifecycle/status/Evidence/confirmation/relationships/latest-published.

## 7. Frozen Confirmation Algorithm

New KnowledgeDocument HumanConfirmation requests carry the displayed `subjectRevisionNumber`. The server transaction compares it with current head and stores it on Evidence; mismatch is `409 conflict`.

The detail read computes the maximum known captured confirmation revision and returns one of:

- `NoConfirmation`;
- `LegacyConfirmationUnknown`;
- `CurrentRevisionConfirmed`;
- `ChangedSinceConfirmation`.

The warning `内容在最近一次确认后已修改` appears only for the last state. Existing confirmation rows stay null/unknown. Neither `ConfirmedAt`, Evidence `ProvidedAt`, document `UpdatedAt`, nor KnowledgeStatus time is used to order content coverage.

## 8. Frozen Diff Decision

- No compare API and no persisted diff.
- Frontend fetches two immutable revision detail responses.
- Title/Summary use old/new field comparison; body uses deterministic Myers line diff.
- Output is escaped text, not rendered Markdown/HTML.
- Default previous → current; selectable two-revision comparison.
- Combined content limit: 2,005,000 string units; combined body-line limit: 10,000.
- Over limit: no partial diff; independently preview each revision and show the frozen oversized message.

## 9. Frozen API Inventory

| API | Contract |
| --- | --- |
| existing `PUT /api/knowledge-documents/{id}/content` | Only normal save; add optional max-500 `changeSummary`; no actor/time/revision input. |
| `GET /api/knowledge-documents/{id}/revisions` | newest-first, page 1/pageSize 20 defaults, max 100, no body in list. |
| `GET /api/knowledge-documents/{id}/revisions/{revisionNumber}` | immutable snapshot detail; no concurrency token. |
| `POST /api/knowledge-documents/{id}/revisions/{revisionNumber}/restore` | body exactly `concurrencyToken + reason`; returns current document detail. |
| existing document detail | add current/latest-published revision numbers and confirmation coverage object. |
| existing HumanConfirmation POST | add KnowledgeDocument-only required `subjectRevisionNumber`; stale revision 409. |

Restore failure families are frozen as 400 validation, 404 not found, 403 forbidden, 409 conflict, 409 invalid_state, and 422 business_rule_violation according to the exact condition table in the decision.

## 10. Frozen Data and Migration Contract

Add:

- `knowledge_document_revisions` with exact snapshot/origin/lineage fields and one unique `(knowledge_document_id, revision_number)` index;
- `knowledge_documents.current_revision_number` default 1;
- nullable `knowledge_documents.latest_published_revision_number`;
- nullable `evidence.knowledge_document_revision_number_snapshot`.

For D existing documents, migration must create exactly D MigrationBaseline rows and prove exact Title/Summary/Body equality. Baseline actor is null; `CreatedAt` is database migration capture time. Current Published rows receive latest pointer 1; Draft/Archived receive null. Existing HumanConfirmations stay null.

The migration must preflight data/FTS and postflight row counts, equality, constraints, indexes, author FKs, Evidence, relationships, lifecycle/status, `Version`, and FTS behavior.

Destructive Down is allowed only before real history exists and after preflight. Once any Created/ContentSave/Restore or revision number >1 exists, operational rollback retains schema or rolls forward; history cannot be silently dropped.

## 11. Frozen UX Contract

- Detail header shows `修订历史（N）`.
- History/preview/compare use an in-route Main Content mode, not a permanent body section or new drawer manager.
- History shows revision, author, capture/change time, lifecycle context, summaries/reasons, origin/lineage, current and latest-published markers.
- MigrationBaseline explicitly shows unknown historical author and capture time.
- Historical preview reuses safe Markdown renderer and never loads Milkdown.
- Published edit/save shows and confirms immediate-publish warning.
- Restore is Draft-only, previewed, reasoned, confirmed, concurrency-protected, and returns to current detail.
- Existing dirty guard and single-overlay rule remain authoritative.
- Changed-since-confirmation is explanatory text beside status, never a new badge/status.

## 12. Verification Cases Frozen for Later Slices

### REV-B01

Migration baseline/count/equality, create→rev1, all semantic-change shapes, no-op, stale token, atomicity, actor/time, HumanConfirmation current-revision capture/stale rejection, Published pointer/time, Archived rejection, constraints, FTS and existing feature regression.

### REV-B02

Viewer history reads, newest-first pagination/max 100, list no body, safe Markdown, baseline labels, stable author snapshot after rename/deactivation, current/latest-published markers, dirty guard and responsive Main Content mode.

### REV-B03

Title/Summary/Body, Chinese/code/table/blank lines, identical input, deterministic order, both limits, oversized UX, and XSS-safe escaped output.

### REV-B04

Draft restore, current/identical reject, Published/Archived reject, stale 409, Viewer deny, lineage/reason/actor/time, atomic FTS, unchanged lifecycle/status/Evidence/confirmation/relationships/published pointer, HumanConfirmation capture/stale behavior, four coverage states, Published warning/pointer/time.

### PHASE-REV-VERIFY

Focused full Browser → API → EF Core → SQLite chain plus relevant builds/tests, no open Blocker/High, cleanup of every verification process/port, and a separate readiness decision. It cannot substitute for Production Engineering/SEC-04 evidence.

## 13. Explicit Non-goals

No Spaces/Page Tree, attachments, comments/mentions/notifications, review/approval, Incident/Problem/TestRun, historical FTS/OCR, semantic/vector search, AI/RAG, branch/merge/CRDT/autosave, AST diff, revision deletion, revision-level status/evidence/relationship/ACL, event sourcing, or generic audit/version/event framework is approved.

## 14. Conflicts and Unresolved Issues

### Conflicts

No blocking conflict was found with the frozen MVP or approved Knowledge Content/relationship architecture.

The one real implementation mismatch—Archived content is protected in Vue but not in the backend content service—is explicitly corrected by the new contract. It is not silently treated as already implemented.

### Unresolved issues

No core Product/Domain/Data/API/UX item is left for implementation to decide. REV-B01 must still execute the required target-database preflight and report actual row counts; that is verification evidence, not an open architecture choice.

## 15. REV-B01 Authorization Decision

```text
Architecture package: APPROVED WITH CHANGES
Human Product / Architecture acceptance: REQUIRED
REV-B01 may start before that acceptance: NO
```

After the human gate accepts this exact contract, REV-B01 is authorized only for the immutable revision foundation/migration/create/save/no-op/Published/Archived/FTS behavior assigned to it. It must not implement REV-B02 read UX, REV-B03 diff, REV-B04 restore/confirmation UX, or any non-goal, and it must stop after its own verification report.

## 16. Planning Verification

- Required decision created: `docs/design/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md`.
- Required report created: `docs/reports/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_FREEZE_REPORT.md`.
- Only these REV-A01 documentation files are task changes.
- No build/test/runtime was required for a documentation-only architecture freeze.
- Task-document trailing-whitespace scan: PASS.
- `git diff --check`: PASS (exit `0`; Git emitted only pre-existing line-ending conversion warnings from the dirty worktree).

REV-A01 stops here and waits for the Human Product / Architecture Gate. REV-B01 was not started.
