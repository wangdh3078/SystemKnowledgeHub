# REV-A01 — Revision Architecture and Contract Decision

> Product: 系统知识中心 / System Knowledge Hub
>
> Phase: PHASE-REV — Knowledge Revision & Change Safety
>
> Decision status: **APPROVED WITH CHANGES**
>
> Decision date: 2026-08-23
>
> Implementation status: Not implemented; Human Product / Architecture Gate required before REV-B01

## 1. Decision Summary

System Knowledge Hub will add immutable content revisions as children of the existing `KnowledgeDocument` aggregate.

```text
KnowledgeDocument
  = current mutable canonical head

KnowledgeDocumentRevision
  = immutable historical child snapshot
  ≠ second Document aggregate
```

This decision freezes the complete Product, Domain, Data, API, Security, UX, migration, and verification contract required before REV-B01. It does not implement the contract and does not authorize production changes by itself.

The approved model has these defining properties:

- revision `1` is created atomically with every new document;
- each existing document receives exactly one truthful `MigrationBaseline` revision `1`;
- every successful semantic content change updates the head and creates one next revision atomically;
- a semantic no-op changes nothing, including document `Version`;
- revision numbers are contiguous, never reused, and independent of the document concurrency `Version`;
- Published content saves remain Published and immediately become the latest published revision;
- restore is Draft-only and restores by creating a new head revision;
- Evidence, HumanConfirmation, KnowledgeStatus, and Relationships remain document-level;
- new KnowledgeDocument HumanConfirmations capture the current revision number to derive change coverage without timestamp guesses;
- comparison is a bounded deterministic frontend text diff over two immutable snapshot responses;
- historical revisions cannot be edited, deleted, independently related, independently evidenced, or independently authorized.

## 2. Authority and Context

This decision follows:

- `AGENTS.md` and the frozen MVP specifications;
- `PHASE_NEXT_PRODUCT_CAPABILITY_PLAN.md` and its recommendation of revision/change safety;
- `PHASE_NEXT_A01_PRODUCT_CAPABILITY_PLANNING_REPORT.md`;
- the approved Knowledge Content architecture and KC-C01/C02 relationship contract;
- KC-B01–B07, UI-B04, PHASE-KC-R01, AUTH-B01/B02 verification evidence;
- the real current entity, service, API, EF Core model, FTS projection, Current User, Evidence/HumanConfirmation, KnowledgeStatus, and Vue detail/editor implementation.

No frozen MVP document is rewritten by this post-MVP decision. Where the earlier Knowledge Content architecture deferred revisions, this document is the focused amendment that now freezes them.

## 3. Current Implementation Inventory

| Area | Current fact | Revision consequence |
| --- | --- | --- |
| Current head | `KnowledgeDocument` stores one Title, Summary, `BodyMarkdown`, lifecycle/status metadata, authorship snapshots, timestamps, and app-managed `Version`. | Keep this row as the only mutable current truth. |
| Content validation | Title is trimmed, required, max 300; Summary is trimmed/null-normalized, max 2,000; body normalizes CRLF/CR to LF, max 1,000,000 characters. | Revision snapshots use the same canonical values and limits. |
| Content save | `PUT /api/knowledge-documents/{id}/content` replaces the head, increments `Version`, updates Current User attribution, and updates FTS in a transaction. | Extend this one save path; do not create a second save API. |
| No-op | Current service increments `Version`, `UpdatedAt`, attribution, and FTS even if normalized content is unchanged. | REV-B01 must add semantic no-op detection. |
| Archived write | Vue hides editing when Archived, but the current backend content service has no lifecycle rejection. | Archived read-only becomes a backend invariant in REV-B01. |
| Published edit | Published documents can be edited and remain Published; current `PublishedAt` is not advanced by content save. | Keep immediate publication, create a revision, advance the latest-published pointer and `PublishedAt`. |
| Lifecycle | Draft → Published; Published → Draft/Archived; Archived → Draft. Lifecycle changes increment document `Version` but do not represent content changes. | Lifecycle and revision number must not be inferred from each other. |
| KnowledgeStatus | Status transitions increment the same document `Version` and update `UpdatedAt`, but are explicit and independent. | `Version` and `UpdatedAt` cannot reconstruct historical content revisions. |
| Evidence | Evidence supports `KnowledgeDocument`; HumanConfirmation stores server-resolved canonical User/KnowledgeRole snapshots but accepts a factual `ConfirmedAt`. | Do not use timestamps to infer confirmation coverage; capture revision number for new document confirmations. |
| Search | SQLite FTS5 stores derived current title/summary/body and excludes Archived documents from results. | Keep indexing committed current head only; no historical FTS. |
| Vue | Detail route has safe Markdown view/preview, Milkdown edit, dirty guard, explicit save, stale-409 handling, and one overlay manager. | History/preview/compare live in the existing detail Main Content and reuse the renderer/guards. |

## 4. Changes from the PHASE-NEXT Default

The recommended phase is approved with these necessary changes and clarifications:

1. Add `CurrentRevisionNumber` to `KnowledgeDocument`; do not calculate the head by `Version` or repeated `MAX(...)` queries.
2. Add nullable `LatestPublishedRevisionNumber` as the minimum reliable publication pointer.
3. Migration baselines have no historical author. Their record time is the migration capture time, not a guessed content-modification time.
4. Add a nullable revision-number snapshot to KnowledgeDocument HumanConfirmation Evidence. Existing confirmations remain unknown rather than being falsely assigned to revision `1`.
5. Require `subjectRevisionNumber` on new KnowledgeDocument HumanConfirmation requests so the server rejects confirmation of a stale UI head.
6. Compute diff in the frontend from two immutable snapshots; do not add a compare API.
7. Promote Archived read-only behavior from frontend guidance to backend content/restore enforcement.
8. Define `PublishedAt` as the time the latest published revision became published; a successful Published content save advances it.
9. Store Restore reason separately from optional normal `ChangeSummary`.

These changes close integrity gaps in the default plan without widening PHASE-REV into review, audit, or workflow infrastructure.

## 5. Canonical Model and Invariants

### 5.1 KnowledgeDocument head

`KnowledgeDocument` remains the current mutable canonical aggregate. Add:

| Property | Nullability | Meaning |
| --- | --- | --- |
| `CurrentRevisionNumber` | Required, initially `1` | The immutable revision whose Title/Summary/Body equal the current head. |
| `LatestPublishedRevisionNumber` | Nullable | The latest revision that became published and can still be identified truthfully. Null means never published or legacy publication history is unknown. |

`CurrentRevisionNumber` is not a concurrency mechanism. The existing opaque `concurrencyToken` backed by document `Version` remains the only write-concurrency contract.

Document `Version` may advance without a content revision because lifecycle and KnowledgeStatus changes are separate writes. Therefore:

```text
document.Version ≠ document.CurrentRevisionNumber
```

### 5.2 KnowledgeDocumentRevision child

The new immutable child has the exact minimum properties below.

| Property | Physical column | Nullability / limit | Meaning |
| --- | --- | --- | --- |
| `Id` | `id` | Required safe positive integer | Internal revision row identity; may be returned but is not used in routes. |
| `KnowledgeDocumentId` | `knowledge_document_id` | Required FK → `knowledge_documents.id`, RESTRICT | Owning document. |
| `RevisionNumber` | `revision_number` | Required, `> 0` | Contiguous per-document number. |
| `Title` | `title` | Required, max 300 | Canonical title snapshot. |
| `Summary` | `summary` | Nullable, max 2,000 | Canonical summary snapshot. |
| `BodyMarkdown` | `body_markdown` | Required, application max 1,000,000 | Canonical Markdown snapshot. |
| `AuthorUserId` | `author_user_id` | Nullable FK → `users.id`, RESTRICT | Server-resolved actor; null only for `MigrationBaseline`. |
| `AuthorDisplayNameSnapshot` | `author_display_name_snapshot` | Nullable | Stable actor display snapshot; null only for `MigrationBaseline`. |
| `CreatedAt` | `created_at` | Required server/database UTC | Time the revision record was created; baseline uses capture time. |
| `LifecycleContext` | `lifecycle_context` | Required Draft/Published/Archived | Document lifecycle at snapshot creation; not an independent lifecycle. |
| `ChangeSummary` | `change_summary` | Nullable, trimmed, max 500 | Optional normal create/save explanation; blank normalizes to null. |
| `RestoreReason` | `restore_reason` | Nullable generally; required 5–500 after trim for Restore | Dedicated reason for restore. |
| `RestoredFromRevisionNumber` | `restored_from_revision_number` | Nullable; required for Restore, positive and lower than new number | Source historical revision. |
| `RevisionOrigin` | `revision_origin` | Required controlled text | `Created`, `ContentSave`, `Restore`, or `MigrationBaseline`. |

There is no revision `Version`, lifecycle status, KnowledgeStatus, Evidence collection, Relationship collection, ACL, or mutable update timestamp.

### 5.3 Database invariants

- Primary key: `id`.
- Unique/order index: `(knowledge_document_id, revision_number)`; this single composite unique index supports newest-first history and the FK leading column, so no duplicate ordering index is added.
- `revision_number > 0`.
- `revision_origin IN ('Created','ContentSave','Restore','MigrationBaseline')`.
- `lifecycle_context IN ('Draft','Published','Archived')`.
- `MigrationBaseline` requires both author fields null; every other origin requires both author fields non-null.
- `Restore` requires nonblank validated `restore_reason` and a positive `restored_from_revision_number < revision_number`; non-Restore rows keep both restore fields null.
- Revision rows are insert-only in application behavior. No UPDATE or DELETE use case/API exists.
- Number allocation, head update, snapshot insertion, pointer changes, and FTS update share one database transaction.

The application enforces `CurrentRevisionNumber` continuity and `LatestPublishedRevisionNumber <= CurrentRevisionNumber`. A cross-table/circular FK for the pointers is not added; focused relational tests prove the invariant.

## 6. Revision Creation Contract

### 6.1 Create

One transaction performs:

```text
insert KnowledgeDocument Draft/Unknown
→ insert Revision 1, origin Created, lifecycleContext Draft
→ set CurrentRevisionNumber = 1
→ LatestPublishedRevisionNumber = null
→ upsert current FTS
→ commit
```

The document and revision share the same canonical content, server Current User ID/name, and server timestamp. Create request does not add `changeSummary` in this phase.

### 6.2 Semantic content save

Before deciding whether a change exists, the server applies the current canonical normalization:

- Title: `Trim()`, ordinal comparison, required, max 300.
- Summary: `Trim()`, empty → null, ordinal comparison, max 2,000.
- Body: CRLF/CR → LF, otherwise exact ordinal comparison, max 1,000,000.

Case, whitespace inside values, Markdown punctuation, and any body byte represented by a different normalized .NET string are meaningful changes.

If at least one normalized field differs, one transaction:

1. checks current opaque token and rejects stale state;
2. rejects Archived lifecycle;
3. increments `CurrentRevisionNumber` by exactly one;
4. updates current Title/Summary/Body and trusted UpdatedBy/time;
5. inserts one `ContentSave` revision with optional normalized `ChangeSummary`;
6. if Published, advances latest-published pointer and `PublishedAt` to the same server timestamp;
7. increments document `Version` once;
8. upserts FTS from the committed current head;
9. commits atomically.

### 6.3 No-op save

If all three normalized content fields equal the current head:

- return `200` with the unchanged current detail;
- do not insert a revision;
- do not change `Version`, `CurrentRevisionNumber`, `LatestPublishedRevisionNumber`, attribution, `UpdatedAt`, `PublishedAt`, KnowledgeStatus, or FTS;
- ignore an optional `changeSummary`; metadata alone cannot manufacture history.

### 6.4 Operations that never create a content revision

- Draft → Published, Published → Draft, Published → Archived, Archived → Draft;
- KnowledgeStatus transition;
- Evidence or HumanConfirmation creation/correction;
- Relationship creation/update/delete;
- Search-index rebuild;
- Unified View reads.

## 7. Published and Lifecycle Semantics

### 7.1 State rules

| Operation | Content revision | Current pointer | Latest-published pointer | `PublishedAt` |
| --- | --- | --- | --- | --- |
| Create Draft | Revision 1 | `1` | null | null |
| Draft content save | next revision | next | unchanged | unchanged |
| Draft → Published | none | unchanged | current | server transition time |
| Published content save | next revision | next | next | same server save time |
| Published → Draft | none | unchanged | retained | retained |
| Draft republish | none | unchanged | current | server transition time |
| Published → Archived | none | unchanged | retained | retained |
| Archived → Draft | none | unchanged | retained/unknown | retained |

`ArchivedAt` keeps the existing behavior: set when archiving and cleared when returning to Draft.

### 7.2 Published save UX

Entering edit mode on a Published document shows this persistent warning near the save action:

> 保存后新内容立即成为已发布内容并生成新修订。

Each dirty Published save also requires an explicit confirmation using the same wording. This does not introduce Approval, Pending Review, a draft branch, or a review queue.

### 7.3 Archived backend invariant

Archived normal content save and restore return `409 invalid_state`. Users must first execute the existing explicit Archived → Draft lifecycle operation. The frontend hiding actions remains usability support, not the security/business rule.

## 8. Restore Contract

### 8.1 Preconditions

- document and requested historical revision exist;
- revision belongs to the route document;
- requested revision number is lower than `CurrentRevisionNumber`;
- current document lifecycle is Draft;
- current user has Editor or Administrator access;
- opaque document `concurrencyToken` is valid and current;
- reason after trim is 5–500 characters;
- source snapshot content differs semantically from current head.

Restoring the current revision or an older revision whose normalized content equals the current head returns `422 business_rule_violation`; it does not create a false revision.

### 8.2 Atomic behavior

```text
Historical Revision K
→ server loads immutable snapshot
→ copy Title/Summary/Body into current KnowledgeDocument
→ create Revision N+1, origin Restore
→ RestoredFromRevisionNumber = K
→ RestoreReason = required normalized reason
→ CurrentRevisionNumber = N+1
→ update trusted actor/time, document Version, and current FTS
→ commit
```

Restore does not:

- edit or delete revision K or any newer revision;
- alter `DocumentType`;
- change lifecycle from Draft;
- change KnowledgeStatus, status reason/actor/time, Evidence, HumanConfirmation, or Relationships;
- change `LatestPublishedRevisionNumber` or `PublishedAt`;
- publish the restored content.

The response is the extended current `KnowledgeDocumentDetailResponse` with the new opaque concurrency token.

## 9. Evidence, HumanConfirmation, and KnowledgeStatus

### 9.1 Document-level ownership

Evidence and HumanConfirmation continue to bind to `KnowledgeDocument`, never `KnowledgeDocumentRevision`. Relationships and `Supersedes` also continue to bind documents. A revision does not have independent KnowledgeStatus.

Content saves and restores never automatically advance or regress KnowledgeStatus.

### 9.2 Stable confirmation coverage capture

Add nullable `KnowledgeDocumentRevisionNumberSnapshot` to Evidence persistence. It is used only when:

```text
EvidenceType = HumanConfirmation
AND SubjectType = KnowledgeDocument
```

For every new KnowledgeDocument HumanConfirmation after REV-B01:

1. client submits `subjectRevisionNumber`, the safe positive current revision it is displaying;
2. server begins the existing HumanConfirmation transaction and resolves trusted Current User;
3. server reads the document `CurrentRevisionNumber`;
4. mismatch returns `409 conflict` and writes no Evidence;
5. match is stored as `KnowledgeDocumentRevisionNumberSnapshot` on the new Evidence row.

The submitted number is an optimistic content-context expectation, not actor/time authority. `ConfirmedAt` remains the factual confirmation time but is never used to order content revisions.

For non-KnowledgeDocument subjects, `subjectRevisionNumber` must be absent; if provided it returns `400 validation_error`. Ordinary Evidence never sets the snapshot.

Existing HumanConfirmation rows are migrated with null revision snapshot. They are not assigned revision `1`, because the current content may have changed after the historical confirmation.

### 9.3 Exact derived algorithm

For a KnowledgeDocument detail query:

```text
knownConfirmedRevision = MAX(
  Evidence.KnowledgeDocumentRevisionNumberSnapshot
  WHERE EvidenceType = HumanConfirmation
    AND SubjectType = KnowledgeDocument
    AND SubjectId = document.Id
    AND snapshot IS NOT NULL
)
```

Return:

| State | Rule | UI behavior |
| --- | --- | --- |
| `NoConfirmation` | No HumanConfirmation exists for the document. | No change-warning; ordinary “尚无人工确认” context may remain. |
| `LegacyConfirmationUnknown` | HumanConfirmation exists but every snapshot is null. | Neutral text: `迁移前人工确认无法确定覆盖的修订。` |
| `CurrentRevisionConfirmed` | known number equals current revision. | No change-warning; may show `人工确认覆盖当前修订 N`. |
| `ChangedSinceConfirmation` | known number is lower than current revision. | Show `内容在最近一次确认后已修改` near KnowledgeStatus, not as a status badge. |

The impossible case `known number > current revision` is treated as a data-integrity failure and must be caught by migration/application tests, not silently relabeled.

This read projection does not change status validation. A user may explicitly keep or regress/reconfirm KnowledgeStatus according to existing rules.

## 10. Diff Contract

### 10.1 Execution decision

There is no backend compare endpoint. The frontend fetches two immutable revision detail responses and computes a deterministic local diff.

Reasons:

- both snapshots are already Viewer-readable under the same document authorization;
- immutable inputs make the result deterministic;
- it avoids a stored diff, generic diff service, and fourth API contract;
- no server-side data reduction is gained because historical detail/preview already returns the bodies.

### 10.2 Comparison behavior

- Title and Summary: field-level old/new comparison, with null Summary displayed as empty.
- BodyMarkdown: deterministic Myers line-oriented diff over canonical LF text.
- Line tokens: `equal`, `delete`, `insert`; replacement is adjacent delete/insert.
- Default selection: previous revision → current revision.
- Users may select any two revisions of the same document; the UI normalizes display order from older → newer.
- Chinese text, fenced code, and Markdown tables are ordinary text lines.
- Diff output renders only through escaped Vue text bindings; it never uses `v-html` or renders Markdown.

### 10.3 Limits and oversized UX

The current per-body limit remains 1,000,000 characters. Inline compare proceeds only when both conditions pass:

```text
combined Title + Summary + Body length <= 2,005,000 .NET/JavaScript string units
combined body line count <= 10,000
```

The client checks limits before running the algorithm. If exceeded:

- do not compute a partial or misleading diff;
- show `这两个修订内容过大，无法在页面内比较。请分别查看修订内容。`;
- keep each historical revision independently previewable;
- do not add export, server fallback, background job, or semantic diff in PHASE-REV.

## 11. Frozen API Contract

All routes inherit the existing authenticated fallback policy. IDs and revision numbers exposed to JavaScript must be safe positive integers.

### 11.1 Extend current document detail

All existing `KnowledgeDocumentDetailResponse` fields remain. Add:

```json
{
  "currentRevisionNumber": 4,
  "latestPublishedRevisionNumber": 3,
  "confirmationCoverage": {
    "state": "ChangedSinceConfirmation",
    "lastConfirmedRevisionNumber": 2
  }
}
```

`latestPublishedRevisionNumber` and `lastConfirmedRevisionNumber` are nullable. Revision count shown in UX is `currentRevisionNumber` because numbers are contiguous and revisions cannot be deleted.

### 11.2 Normal content save

```text
PUT /api/knowledge-documents/{id}/content
```

Request keeps existing fields and adds only:

```json
{
  "title": "...",
  "summary": null,
  "bodyMarkdown": "...",
  "changeSummary": "补充回滚步骤",
  "concurrencyToken": "opaque"
}
```

`changeSummary` is optional, trimmed, blank → null, max 500. It is ignored on semantic no-op. No actor/time/revision number is accepted.

### 11.3 Revision list

```text
GET /api/knowledge-documents/{id}/revisions?page=1&pageSize=20
```

- default page `1`, page size `20`, maximum `100`;
- validation outside range returns `400 validation_error`;
- order is `revisionNumber DESC`;
- missing document returns `404 not_found`;
- list DTO never returns Title, Summary, or BodyMarkdown.

Response:

```json
{
  "items": [
    {
      "id": 904,
      "revisionNumber": 4,
      "revisionOrigin": "Restore",
      "lifecycleContext": "Draft",
      "authorUserId": 18,
      "authorDisplayName": "王敏",
      "createdAt": "2026-08-23T05:00:00Z",
      "changeSummary": null,
      "restoreReason": "恢复被误删的处理步骤",
      "restoredFromRevisionNumber": 2,
      "isCurrent": true,
      "isLatestPublished": false
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 4
}
```

Baseline author fields are null.

### 11.4 Revision detail

```text
GET /api/knowledge-documents/{id}/revisions/{revisionNumber}
```

Response contains every list-item field plus:

```json
{
  "knowledgeDocumentId": 222,
  "title": "...",
  "summary": null,
  "bodyMarkdown": "..."
}
```

No concurrency token or edit action is returned. Missing document or revision returns `404 not_found`; invalid/safe-range values return `400 validation_error`.

### 11.5 Restore

```text
POST /api/knowledge-documents/{id}/revisions/{revisionNumber}/restore
```

Request contains exactly:

```json
{
  "concurrencyToken": "opaque",
  "reason": "恢复被误删的处理步骤"
}
```

It never accepts actor, time, title, summary, body, lifecycle, status, or source revision metadata.

Failures:

| Condition | HTTP / code |
| --- | --- |
| malformed ID/revision/token/reason | `400 validation_error` |
| document or owned revision missing | `404 not_found` |
| Viewer | `403 forbidden` |
| stale document token | `409 conflict` |
| Published/Archived document | `409 invalid_state` |
| current revision or content-identical source | `422 business_rule_violation` |

Success returns `200` with the extended current document detail.

### 11.6 HumanConfirmation amendment

`POST /api/evidence/human-confirmations` adds optional wire field `subjectRevisionNumber`. It is required for `Subject.Type = KnowledgeDocument` after REV-B01, forbidden for other subjects, and never replaces the existing server-trusted Current User or factual `ConfirmedAt`.

A stale KnowledgeDocument revision returns `409 conflict` with `details.resourceType = "KnowledgeDocument"`, `resourceId`, and `currentRevisionNumber`. The response/list/detail Evidence projections add nullable `knowledgeDocumentRevisionNumberSnapshot` so the UI can show which revision a new confirmation covered.

## 12. Data Migration and Compatibility Freeze

### 12.1 Expected additive schema work

- create `knowledge_document_revisions`;
- add `current_revision_number INTEGER NOT NULL DEFAULT 1` to `knowledge_documents`;
- add nullable `latest_published_revision_number` to `knowledge_documents`;
- add nullable `knowledge_document_revision_number_snapshot` to `evidence`;
- update EF model snapshot and `DbSet` only as required.

No existing body/status/lifecycle/relationship row is rewritten. Avoid an existing-table rebuild unless generated SQLite SQL proves it is required; do not add speculative CHECK constraints to the existing document/evidence tables that force a rebuild.

### 12.2 Preflight

Before migration, record and verify:

- document row count and lifecycle distribution;
- all document IDs/versions are positive and author FKs resolve;
- Title/Summary/Body and required fields satisfy current limits/constraints;
- no target revision table/columns already exist;
- KnowledgeDocument HumanConfirmation count; every existing row is designated legacy-unknown;
- FTS row/content consistency baseline sufficient to prove no regression.

### 12.3 Deterministic baseline

For every existing document, insert exactly one revision:

| Field | Baseline value |
| --- | --- |
| RevisionNumber | `1` |
| Title/Summary/Body | exact current stored values |
| Origin | `MigrationBaseline` |
| LifecycleContext | exact current lifecycle |
| AuthorUserId / display | null / null; historical content author is unknown |
| CreatedAt | one database-generated UTC migration capture time; not `UpdatedAt` |
| ChangeSummary / restore fields | null |

`CurrentRevisionNumber` becomes `1`.

`LatestPublishedRevisionNumber` backfill:

- current lifecycle Published → `1`, because the migration-time current content is published;
- Draft or Archived → null, because the pre-revision model cannot prove which snapshot was last published, especially because backend Archived content writes were not previously rejected;
- retain existing `PublishedAt`; a non-null `PublishedAt` with null pointer is displayed as legacy publication history unknown.

Do not infer a baseline author from `UpdatedBy*` or time from `UpdatedAt`: lifecycle and KnowledgeStatus operations also update those fields. Do not assign existing HumanConfirmations to revision `1`.

### 12.4 Postflight

- revision row count equals document row count;
- each document has exactly one revision `1` and no other revision;
- every baseline Title/Summary/Body exactly equals its current head;
- every current revision number is `1`;
- published pointer follows the rule above and never exceeds current;
- existing Evidence/HumanConfirmation, relationships, author FKs, lifecycle/status, document `Version`, FTS results, and indexes remain readable and unchanged;
- new uniqueness/check/FK constraints accept valid rows and reject invalid rows.

### 12.5 Rollback

Before any post-migration create/save/restore, a tested Down path may remove the additive revision objects after proving every row is a single MigrationBaseline and all current numbers equal `1`.

After any real revision exists, destructive Down is prohibited operationally. Roll back the application while retaining the additive history schema, or roll forward. Dropping revision history requires an explicit human data-loss decision plus verified backup/export; it must never happen silently.

## 13. Authorization and Security

| Capability | Viewer | Editor | Administrator |
| --- | ---: | ---: | ---: |
| Current document/history list/history detail/compare | Allow | Allow | Allow |
| Create/content save/lifecycle/status | Deny | Allow | Allow |
| Restore | Deny | Allow | Allow |
| HumanConfirmation | Deny | Allow | Allow |

- Historical reads first resolve the owning document and apply the same read authorization; no independent revision ACL exists.
- Every write uses `Authenticated Principal → ICurrentUserContext → canonical User → snapshot` and server UTC.
- Deactivated or renamed Users do not rewrite historical author snapshots. FK RESTRICT preserves referenced User rows.
- Client actor/time/historical content is rejected or ignored by contract, never trusted.
- Existing antiforgery and Cookie behavior applies to content save, restore, lifecycle, status, and HumanConfirmation.
- Revision IDs/numbers and response fields are validated as JavaScript-safe integers.

## 14. UX Freeze

### 14.1 History mode

- KnowledgeDocument detail header adds `修订历史（N）`, where `N = currentRevisionNumber`.
- Selecting it enters a history mode inside the existing detail route Main Content and replaces the normal body/editor surface. It does not permanently expand the document body and does not create a second route page or drawer manager.
- Entering history while editing uses the existing dirty-discard guard.
- History list shows number, actor, time, lifecycle context, change summary, restore reason/origin, restored-from number, and current/latest-published markers.
- `MigrationBaseline` displays `迁移基线`, author `历史作者未知`, and `捕获于 <CreatedAt>` rather than claiming a modification time.
- Historical preview uses the existing safe HTML-disabled Markdown renderer and never loads Milkdown.

### 14.2 Compare

- Default is previous → current; users may choose any two revisions.
- Compare occupies the Main Content width and follows §10 limits/escaping.
- It does not use a drawer, rich editor, or rendered Markdown diff.

### 14.3 Restore

- Restore action is visible only to Editor/Administrator and only while the current document is Draft.
- User previews the selected historical snapshot, enters a required 5–500-character reason, and confirms that a new revision will be created.
- Restore uses the current detail token, never a revision token.
- Success reloads current detail, exits historical preview to current content, and shows the new revision number.
- Dirty current edits must be discarded explicitly before restore/history navigation.
- Existing single-overlay rule remains; no stacked drawer is introduced.

### 14.4 Confirmation coverage

- `ChangedSinceConfirmation` displays the exact warning `内容在最近一次确认后已修改` adjacent to KnowledgeStatus explanatory content, not inside the KnowledgeStatus badge.
- `LegacyConfirmationUnknown` displays neutral migration wording.
- The indicator never blocks save or changes status.

## 15. Frozen Verification Gates

### REV-B01 — Foundation

- additive schema and exact baseline row/count/content equality;
- zero-document and existing-document migration cases;
- create → revision 1 and atomic FTS;
- normalized semantic save → next revision;
- title-only, summary-only, and body-only change;
- no-op keeps revision number, `Version`, attribution/time, PublishedAt, and FTS unchanged;
- stale token 409 and failed transaction leaves neither partial head nor orphan revision;
- trusted actor/time and user snapshot stability;
- KnowledgeDocument HumanConfirmation captures the displayed current revision, rejects a stale number, and never trusts client actor/time;
- Published save pointer/time behavior and Archived backend rejection;
- revision/check/unique/FK constraints;
- existing Evidence/relationship/status/search regressions.

### REV-B02 — Read UX

- Viewer list/detail access and Editor parity;
- newest-first pagination, default 20/max 100, no body in list;
- safe historical Markdown and baseline labels;
- renamed/deactivated User leaves revision display snapshot stable;
- current/latest-published markers for Draft/Published/Archived and legacy-unknown pointer;
- dirty guard and Main Content history mode at supported widths.

### REV-B03 — Diff

- title, summary, body changes;
- Chinese, code fences, tables, blank/trailing lines, identical input;
- deterministic older → newer output;
- combined character and line limits with oversized UX;
- XSS payloads remain escaped text;
- no backend compare endpoint or persisted diff.

### REV-B04 — Restore and trust integration

- Draft restore-as-new with lineage and required trimmed 5–500 reason;
- current/identical source rejected without history;
- Published/Archived `409 invalid_state`;
- Viewer forbidden, stale token `409 conflict`;
- trusted actor/time, atomic FTS, and new token;
- KnowledgeStatus, Evidence, HumanConfirmation, Relationships, lifecycle, and latest-published pointer unchanged;
- HumanConfirmation current revision capture, stale revision conflict, and legacy null preservation;
- all four confirmation-coverage states;
- Published save warning, pointer, and `PublishedAt` semantics.

### PHASE-REV-VERIFY

One focused authenticated Browser → API → EF Core → SQLite path must cover create, publish, Published edit, history, compare, return to Draft, restore, HumanConfirmation coverage, status independence, search current-content behavior, and Unified View current-head behavior. Applicable build/test gates must pass; all verification processes must be stopped and ports released.

## 16. Explicit Non-goals

PHASE-REV does not implement:

- Spaces, folders, Page Tree, Personal/Team Space;
- attachments, OCR, binary storage, attachment search;
- comments, mentions, notifications, watch lists, co-editing;
- review, approval, Pending Review, ownership/expiry workflow;
- Incident, Problem, TestRun, or operational execution history;
- historical FTS, semantic/vector search, AI/RAG;
- branches, merge, CRDT, autosave, Markdown AST diff;
- revision edit/delete/retention deletion;
- revision-scoped lifecycle, KnowledgeStatus, Evidence, Relationship, or ACL;
- generic version/audit/event framework, event sourcing, command bus, or background diff jobs.

## 17. Consequences and Risks

### Benefits

- Published changes become attributable, inspectable, comparable, and recoverable.
- Restore preserves every later revision instead of overwriting history.
- Confirmation coverage is based on immutable revision order rather than race-prone timestamps.
- Current search and Unified Views stay simple and continue to use only the canonical head.
- Future governance, attachments, traceability, import, and citation features gain a stable historical base.

### Accepted trade-offs

- Full snapshots increase SQLite size; current 1,000,000-character cap and real-size measurement are sufficient before considering compression.
- Existing historical author/publication/confirmation facts cannot be reconstructed and are shown as unknown.
- Published editing remains immediate publication; teams needing approval require a separate future phase.
- Frontend comparison has explicit limits rather than an unbounded/server fallback.

## 18. Gate Decision

```text
REV-A01 Decision: APPROVED WITH CHANGES
Blocking architecture questions: NONE
REV-B01 implementation authorization: NOT YET
```

REV-B01 may start only after a human Product / Architecture owner accepts this frozen package, including the Archived backend correction, migration-baseline unknowns, confirmation revision capture, client-side diff decision, and PublishedAt/latest-published semantics.

After that approval, start exactly REV-B01, produce its verification report, and stop. Do not begin REV-B02 automatically.
