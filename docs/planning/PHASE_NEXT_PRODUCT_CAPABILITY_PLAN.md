# PHASE-NEXT-A01 — Next Product Capability Plan

## 1. Plan Status

```text
Decision Status: NEXT PHASE RECOMMENDED
Current Product Readiness: Internal Pilot
Recommended Next Major Phase: PHASE-REV — Knowledge Revision & Change Safety
Alternative Next Phase: PHASE-TRACE — Traceability Experience
```

This plan is the proposed Product / Architecture basis for the next implementation phase. It does not authorize an implementation slice. Human Product / Architecture approval is required before `REV-A01` starts.

## 2. Decision Summary

The next major product phase should add immutable revision history and change-safety UX to `KnowledgeDocument`.

The current product can create, edit, publish, archive, search, evidence, confirm, and relate long-form knowledge. A Published document may be edited, but the system retains only the latest title, summary, and Markdown body. Users cannot reliably answer:

- what changed after publication;
- who made each content change and when;
- which content was current when a document was published or confirmed;
- how to compare two states;
- how to recover an earlier state without destroying later history.

This is now a data-trust and Team Production constraint, not merely visual polish. Revision history was also explicitly identified in the approved Knowledge Content architecture as the first capability after Content MVP validation and before broad authoring rollout.

The recommended phase remains deliberately narrow:

```text
KnowledgeDocument current state
  + immutable content revisions
  + revision list/detail/diff
  + restore by creating a new head revision
  + published/change-trust indicators
```

It does not include Spaces, page trees, attachments, comments, notifications, Incident Management, semantic search, AI/RAG, or a general audit/event framework.

## 3. Current Product Boundary

System Knowledge Hub is currently an authenticated internal knowledge system in which structured system knowledge and authored Markdown documents coexist.

| User capability | Implemented boundary | Important current limit |
| --- | --- | --- |
| Authentication and access | Local Login and OIDC capability share the application Cookie; Current User is server-trusted; Viewer, Editor, and Administrator are enforced; logout is implemented. | Real Production deployment/security rehearsal remains unapproved; Local password lifecycle beyond bootstrap/login is not implemented. |
| Structured knowledge | Systems, Business Functions, Database Objects/Columns, Business Rules, Integrations, Unknown Items, Evidence, HumanConfirmation, Relationships, and explicit KnowledgeStatus progression. | This is knowledge modeling, not an operational Incident or ticketing system. |
| Knowledge content | Requirement, Specification, TestCase, SOP, Troubleshooting, KnowledgeArticle, and DesignNote in one `KnowledgeDocument` aggregate. | Types do not have separate workflow engines or dynamic schemas. |
| Document authoring | Create/list/detail, Markdown edit and safe preview, dirty protection, explicit save, optimistic concurrency, Draft/Published/Archived lifecycle, Evidence, HumanConfirmation, KnowledgeStatus, and typed relationships. | Only the current content state is retained; there is no history, comparison, or restore. |
| Discovery | Global Search with SQLite FTS5 body search and Chinese text support; archived content exclusion; System Unified Knowledge View. | Search is lexical and the Unified View is currently System-centered; no attachment or semantic search. |
| Traceability foundation | Machine-readable `SpecifiedBy`, `VerifiedBy`, `AppliesTo`, `Documents`, `References`, and `Supersedes` contracts with a closed endpoint matrix. | The graph is available through relationships, but there is no coverage projection, traceability tree, or matrix. |

The product must not claim support for revision history, binary attachments, Spaces/page trees, comments/mentions/reviews, Incident records, import/export, semantic retrieval, AI/RAG, or Team Production operations.

## 4. Architecture Boundaries

The next phase must preserve:

```text
Domain truth
≠ Search index
≠ Read projection
≠ UI organization
```

- `KnowledgeDocument` remains the single mutable aggregate representing current document state.
- A revision is an immutable historical snapshot owned by a `KnowledgeDocument`; it is not a second document aggregate and has no independent lifecycle.
- Diff output is a derived read result, not stored domain truth.
- Search remains a rebuildable projection over current, discoverable content unless a later, explicit historical-search requirement is approved.
- Page trees or Spaces, if ever approved, must be separate organization/mapping concepts and must not copy document bodies or become `parentId` semantics hidden inside the document core.
- Evidence and KnowledgeStatus remain explicit domain facts. Saving a revision must never automatically advance or regress KnowledgeStatus.
- Current User, author identity, timestamps, and access decisions must be resolved server-side; clients cannot submit authoritative actor snapshots.

## 5. Candidate Evaluation

Ratings are qualitative. For User Value, Current Pain, Data Integrity, Pilot → Production, and Future Leverage, `High` means greater importance. For Architectural Dependency, Complexity, and Risk, `High` means more dependency/cost/risk. No usage telemetry or formal pilot interviews were found, so Current Pain reflects the implemented workflow and documented rollout risk; it must be checked against pilot feedback at the gate.

| Candidate | User Value | Current Pain | Arch. Dependency | Data Integrity | Pilot → Prod. | Complexity | Risk | Future Leverage |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A. Revision History / Versioning | High | High | Medium | High | High | Medium | Medium | High |
| B. Spaces / Page Tree / hierarchy | Medium | Low | High | Low | Low | High | Medium | Medium |
| C. Attachments | High | Medium | High | High | Medium | High | High | High |
| D. Comments / Mentions / Review / Notifications | Medium | Low | High | Low | Low | High | Medium | Medium |
| E. Traceability UX | High | Medium | Low | Medium | Medium | Medium | Low | High |
| F. SOP / Problem / Incident handling | Medium | Low | High | Medium | Low | High | High | Medium |
| G. Knowledge Governance | High | Medium | Medium | High | High | Medium | Medium | High |
| H. Search Evolution | Medium | Medium | Medium | Low | Medium | Medium | Medium | High |
| I. AI / RAG | Medium | Low | High | High | Low | High | High | High |
| J. Import / Export | Medium | Low | High | High | Medium | High | High | Medium |
| K. Production Readiness Engineering | Medium | High | High | High | High | High | High | High |

Candidate K is a mandatory release-engineering workstream, not a substitute for choosing the next product capability. It must have its own ownership and gate.

## 6. Recommended Capability Semantics

### 6.1 Revision creation

- Backfill every existing `KnowledgeDocument` with revision `1`, containing its current title, summary, and canonical Markdown body.
- New documents create revision `1` in the same transaction as document creation.
- Every successful explicit content save creates exactly one next immutable revision in the same transaction as updating the current document row.
- Lifecycle-only, relationship, Evidence, HumanConfirmation, and KnowledgeStatus operations do not create content revisions.
- No-op content saves create neither a document version increment nor a revision.
- Revision numbers are monotonic per document and unique by `(knowledge_document_id, revision_number)`.
- Each revision stores canonical User ID plus immutable display-name snapshot and server timestamp. It may store a short, user-supplied change summary, but never a client-supplied authoritative actor or time.

### 6.2 Published revision semantics

The recommended default preserves the current simple lifecycle rather than introducing branches:

- Draft content saves create Draft-context revisions.
- Publishing marks the current revision as the latest published revision; publishing alone does not duplicate its content snapshot.
- A content save while Published creates a new revision atomically and that new revision becomes the current/latest published revision. The UI must make this consequence explicit before save.
- Moving Published to Draft preserves the latest published revision marker while later Draft revisions may diverge.
- Republishing marks the then-current revision as latest published.
- Archived documents remain read-only; the existing explicit transition back to Draft is required before content changes or restore.

`REV-A01` must freeze this contract before schema or API work. A draft-branch/review workflow is explicitly not part of this phase.

### 6.3 Diff and restore

- Users can list revisions, inspect a historical snapshot, and compare any two revisions; the default comparison is current versus previous.
- Comparison covers title, summary, and Markdown body using a readable line-oriented derived diff. A semantic Markdown diff framework is not required.
- Restoring never overwrites or deletes history. It copies the selected historical snapshot into current state and creates a new head revision with `restoredFromRevisionNumber` metadata.
- Restore requires Editor access, a current opaque concurrency token, and a required restore reason.
- Restore is allowed only while Draft. Published or Archived content must first use the existing explicit lifecycle transition to Draft.
- The UI must preview the candidate result and clearly state that restore creates a new revision.

### 6.4 Evidence, KnowledgeStatus, and confirmation

- Evidence, HumanConfirmation, and KnowledgeStatus remain document-level in this phase; they are not copied into every revision and are not silently rebound.
- A content save must not automatically change KnowledgeStatus, consistent with the canonical explicit progression rule.
- The read model should derive and show `内容在最近一次确认后已修改` when the current content revision is newer than the latest applicable HumanConfirmation/Confirmed transition.
- The warning is not a new status and does not block saving. Users retain the explicit decision to regress/reconfirm knowledge status.
- Revision-scoped Evidence or revision-scoped confirmation is deferred until a real compliance/use case proves it necessary.

## 7. Proposed Data, API, Security, and UX Boundary

Exact names and contracts are frozen only by `REV-A01`; the intended minimum is:

### Data

- Add one `knowledge_document_revisions` table with document FK, per-document revision number, title/summary/body snapshot, author ID/name snapshot, server creation time, lifecycle context, optional change summary, and optional restored-from revision number.
- Add the minimum current/latest-published revision pointer or number required to make publication semantics unambiguous.
- Do not add generic audit tables, event sourcing, blobs, tags, trees, ACLs, or a second canonical body format.

### API

- Page-oriented revision list and revision detail reads under the concrete KnowledgeDocument route.
- A concrete compare read or a small client-derived comparison over revision snapshots, selected during `REV-A01` based on payload limits.
- One explicit restore business action with current document concurrency token and restore reason.
- Existing content update remains the single normal authoring command and atomically records the revision.
- No generic version CRUD, generic command endpoint, or client-parsed concurrency token.

### Access and identity

| Operation | Viewer | Editor | Administrator |
| --- | ---: | ---: | ---: |
| List/view/compare revisions | Allow | Allow | Allow |
| Normal content save and revision creation | Deny | Allow | Allow |
| Restore historical revision | Deny | Allow | Allow |

All writes use canonical Current User and server time. Historical display-name snapshots remain stable if a User is renamed or deactivated.

### User-visible experience

- KnowledgeDocument detail shows a clear revision count and “修订历史” entry.
- History shows revision number, author, time, lifecycle context, change summary, and restore origin where applicable.
- A comparison view presents added/removed/changed title, summary, and Markdown lines without exposing unsafe rendered HTML.
- Published editing displays that saving immediately creates a new published revision.
- Restore offers preview, requires a reason, respects dirty-state protection, and uses the existing single-overlay rule; no stacked drawer is introduced.
- A changed-since-confirmation indicator appears near KnowledgeStatus without masquerading as a new status.

## 8. Recommended Slice Breakdown

### REV-A01 — Revision Architecture and Contract Freeze

**Goal:** Freeze revision, publication, confirmation-warning, API, migration, and UI behavior before implementation.

**Scope:** Resolve the proposed defaults in §6–7; review existing document rows and maximum payload; define contracts, schema, permission matrix, Golden UX extension, and verification cases.

**Non-goals:** Production code, migration generation, attachments, review workflow, generic audit.

**Dependencies:** This plan, approved Knowledge Content architecture, current KnowledgeDocument/lifecycle/status contracts, KC-C02 relationship contract.

**Verification gate:** Human Product / Architecture approval; no unresolved material conflict with frozen specifications; approved additive migration and rollback approach.

### REV-B01 — Immutable Revision Foundation

**Goal:** Persist a complete, trustworthy content history from creation and explicit saves.

**Scope:** Additive revision schema; deterministic existing-row backfill; atomic create/save revision capture; server actor snapshots; per-document numbering; no-op and concurrency behavior.

**Non-goals:** Revision UI, diff, restore, historical search, generic audit.

**Dependencies:** REV-A01.

**Verification gate:** Migration up/backfill/constraints/down behavior on real SQLite; 1–3 focused integration tests proving atomic capture, stale-token rejection, and preserved existing content; backend build.

### REV-B02 — Revision History Read UX

**Goal:** Let all authenticated users understand who changed a document and inspect prior content.

**Scope:** Typed list/detail reads; revision history entry and list; historical safe Markdown preview; lifecycle and restore-origin metadata.

**Non-goals:** Diff, restore, editing a historical row, comments.

**Dependencies:** REV-B01.

**Verification gate:** Backend projection check, frontend type-check/build, focused browser path from current document to a historical snapshot, Golden UX review.

### REV-B03 — Revision Compare UX

**Goal:** Make “what changed?” answerable without external tools.

**Scope:** Select two revisions, default previous-to-current comparison, title/summary/body line diff, large-document limits and safe rendering.

**Non-goals:** Semantic AST diff, merge, branching, side-by-side rich editor, historical full-text search.

**Dependencies:** REV-B02.

**Verification gate:** Focused deterministic diff tests for meaningful changes and size limits; frontend type-check/build; browser comparison at desktop target widths.

### REV-B04 — Restore and Published Change Safety

**Goal:** Recover earlier content without destroying history and expose lifecycle/trust consequences.

**Scope:** Draft-only restore by new head revision; restore reason; preview/confirmation; published-save warning; latest-published marker; changed-since-confirmation read indicator.

**Non-goals:** Approval workflow, automatic KnowledgeStatus transition, revision-scoped Evidence, scheduled review.

**Dependencies:** REV-B01–B03 and existing lifecycle/status/Evidence behavior.

**Verification gate:** Real SQLite tests for restore lineage, authorization and stale concurrency; explicit proof that lifecycle/Evidence/KnowledgeStatus do not auto-change; focused authenticated browser restore and published-edit flow.

### PHASE-REV-VERIFY — End-to-End Gate

**Goal:** Decide whether revision capability is complete and whether the product may be considered for Team Production after separate engineering gates.

**Scope:** Existing-row migration, create/edit/publish/draft/archive, history, compare, restore, Current User attribution, access levels, status/Evidence separation, FTS current-content behavior, System Unified View regression, process cleanup.

**Non-goals:** Declaring deployment/security/operations PASS without a real environment.

**Dependencies:** REV-B01–B04 and the separate Production Engineering evidence available at that time.

**Verification gate:** Focused build/tests plus one Browser → API → EF Core → SQLite chain; zero open Blocker/High revision gap; all verification-only processes stopped; formal readiness decision.

## 9. Migration and Rollback Plan

- Migration is additive: create revision storage, add only the minimal pointer/number fields, and backfill one initial snapshot per existing document.
- Before migration, preflight duplicate/invalid document IDs, current version values, title/body limits, author references, and row counts.
- Backfill must be deterministic and idempotence must be verified at migration level; each existing document gets exactly one revision `1` matching its current content.
- Index only `(knowledge_document_id, revision_number)` and the minimum list ordering path proven by the read contract.
- Revision creation and current document update must share one transaction. FTS continues to index only committed current content.
- For this SQLite single-application deployment, use a maintenance window rather than inventing dual-write compatibility infrastructure.
- Take and validate a database backup before Production migration. A rollback before new writes may remove the additive objects. After new revisions exist, application rollback should retain the revision table; dropping it is destructive and requires an explicit data-loss decision.
- Migration verification must prove all existing KnowledgeDocuments, relationships, Evidence subjects, author FKs/snapshots, indexes, content, version tokens, and FTS behavior are preserved.

## 10. Alternative Next Phase

`PHASE-TRACE — Traceability Experience` is the single alternative.

Choose it instead only if pilot evidence shows that teams are already authoring a meaningful Requirement/Specification/TestCase corpus and their dominant blocked job is coverage and impact analysis, while document change frequency remains low and Published content is still tightly controlled.

Its minimum scope would be derived, read-only projections:

- Requirement → Specification → TestCase tree;
- missing specification and missing test coverage indicators based on the approved relationship matrix;
- related System navigation and impact paths;
- a bounded traceability matrix with explicit filters.

Coverage is never stored as a second truth; it is derived from typed relationships. Even if selected first, revision history remains required before broad authoring or Team Production.

## 11. Analysis of Other Capabilities

### Knowledge organization

System context, typed relationships, search, and the System Unified Knowledge View already provide meaningful object-centered discovery. There is no verified need for Personal Space or Team Space, and no current team-scoped authorization model to support them. A page tree would curate navigation, while Unified View derives related knowledge; they are not identical, but building both now would add competing browse models without demonstrated pain. `DEFER NOW`.

### SOP and problem handling

SOP and Troubleshooting documents answer “遇到这种情况应该怎么处理？” Unknown Items manage gaps in what is known. An Incident/Problem record answers “某天实际发生了什么、谁处理、何时恢复？” and needs event time, impact, responders, actions, recovery, and operational history. It must be a separate future module if approved; it must not be hidden inside SOP, Troubleshooting, or UnknownItem. `DEFER NOW` pending real operational use cases.

### Attachments

Screenshots, PDFs, logs, SQL files, spreadsheets, and configuration samples are more likely to unlock real knowledge capture than Spaces or Comments. They are nevertheless not the safest immediate phase: storage authorization, safe filenames, MIME/content validation, size/quotas, download headers, orphan/retention rules, stable Markdown links, malware posture, and backup/restore must be decided together. A future attachment phase should use separate metadata and stable IDs, bind explicitly to Document or Evidence, store no base64 in Markdown/database, and begin with one approved local-deployment storage strategy rather than a speculative multi-provider framework.

### Collaboration and governance

No verified demand currently justifies comments, mentions, watch lists, or notification infrastructure. Lightweight owner/reviewer/review-date governance is more likely to matter before social collaboration, but it benefits from stable revisions so a review can identify the content reviewed. Governance is a likely subsequent phase, not part of PHASE-REV.

### Search and AI/RAG

Existing FTS and System-centered discovery are adequate for the pilot baseline. Relationship-aware filters and attachment text search should follow the corresponding data capabilities. Semantic/vector search is not a prerequisite now.

```text
AI/RAG = DEFER
```

Revision stability, attachment extraction policy, permission-filtered retrieval, traceability, stale/change metadata, evaluation datasets, and citation behavior are not mature enough for trustworthy AI answers. AI drafting or relationship suggestion must not be used to bypass explicit Evidence, confirmation, relationship creation, or KnowledgeStatus decisions.

## 12. Production Readiness Is a Separate Gate

Current classification remains **Internal Pilot** after KC-C02. PHASE-REV addresses the primary product-level change-safety gap, but it cannot by itself approve Team Production.

Separate Production Engineering work must close at least:

- resume SEC-04 in a real HTTPS/reverse-proxy deployment, including explicitly trusted forwarded headers, Cookie/OIDC callback behavior, persistent protected Data Protection keys, and restart/redeploy verification;
- define deployment artifact/configuration ownership, secret injection, migration procedure, rollback, and release smoke checks;
- define SQLite production topology/capacity and single-writer/concurrency assumptions, or approve another database strategy through a separate architecture decision;
- establish tested database and attachment-aware backup/restore with recovery objectives and a restore rehearsal;
- add bounded health/readiness checks, structured operational/security logging, retention/privacy rules, alert ownership, and sufficient observability to diagnose login, API, migration, and storage failures;
- verify Production HTTPS, HSTS ownership, proxy/body logging safety, rate-limit behavior for the actual topology, and local/OIDC recovery procedures;
- decide whether Local Login will be used for normal Team Production users; if yes, plan self password change/recovery and credential administration before broad rollout.

These are release gates, not reasons to add a generic audit platform, distributed cache, background-job framework, or enterprise observability stack speculatively.

## 13. DEFER NOW

- Spaces, Personal Space, Team Space, folders, page tree, and Confluence-like home/navigation.
- Attachments in PHASE-REV; retain as a high-value later phase after storage/security/backup decisions.
- Comments, mentions, reviews, watch lists, real-time co-editing, and notification delivery.
- Incident/Problem records, TestRun, execution history, on-call, ticketing, or service-management workflow.
- Owner/reviewer/expiration/staleness workflow beyond the changed-since-confirmation indicator.
- Historical full-text search, relationship-aware ranking, attachment OCR/extraction, semantic/vector search.
- AI/RAG, summarization, Q&A, inference, relationship auto-creation, or automatic KnowledgeStatus changes.
- Bulk import, Confluence migration, PDF/HTML publishing, and general export framework.
- Revision branching, merge, approvals, CRDT, autosave, semantic AST diff, revision deletion, and retention policies that discard history.
- Generic audit/event framework, event sourcing, generic version service, repository/CQRS frameworks, or a second Document aggregate.

## 14. Scope-Control Rules

1. Only one implementation slice may be active after its predecessor gate passes.
2. Every discovered adjacent need goes to a phase backlog; it does not enter the current slice without a new Product / Architecture decision.
3. Every slice must state user-visible behavior, Domain truth, API contract, migration impact, permissions, non-goals, and verification before code starts.
4. Search projections, Unified Views, coverage views, and future organization views remain derived; they never become competing write models.
5. No infrastructure is generalized until two approved current use cases prove reuse.
6. Failed or skipped applicable verification blocks the next slice. Verification-only processes must be stopped and ports released.
7. Team Production readiness is decided by the combined Product gate and separate Production Engineering gate, never inferred from a successful feature build.

## 15. Risks and Mitigations

| Risk | Mitigation |
| --- | --- |
| Revision rows become a second document truth | Current `KnowledgeDocument` remains head; revisions are immutable snapshots and restore creates a new head. |
| Published edits surprise readers | Explicit Published-save warning, latest-published marker, visible revision metadata, and architecture freeze before implementation. |
| Confirmed content changes without status regression | Preserve explicit KnowledgeStatus rule and show derived changed-since-confirmation warning. |
| Large Markdown history increases SQLite size | Measure real document sizes in REV-A01; retain text snapshots initially; no premature compression/blob framework. |
| Migration silently misstates existing history | Label backfilled revision as migration baseline, prove exact snapshot equality and row counts. |
| Restore destroys later work | Draft-only restore, preview, required reason, concurrency token, and new revision rather than overwrite. |
| Phase expands into workflow/governance | Enforce DEFER list and slice non-goals; require a new gate for approval/review semantics. |
| Product work is mistaken for Production approval | Maintain independent SEC-04/deployment/backup/observability gates and readiness classification. |

## 16. Human Product / Architecture Gate

Before `REV-A01`, approve or reject this package as a whole, especially these recommended defaults:

1. PHASE-REV is the next product phase; PHASE-TRACE is conditional alternative only.
2. Published content saves remain immediately Published but create a visible new published revision.
3. Restore is Draft-only and creates a new head revision with a required reason.
4. Evidence and KnowledgeStatus remain document-level; changed-since-confirmation is a derived warning.
5. Existing documents receive a clearly identified revision-1 migration baseline.
6. Production Engineering remains a separately owned gate and is not bundled into revision implementation.

No implementation slice starts until this gate is recorded.
