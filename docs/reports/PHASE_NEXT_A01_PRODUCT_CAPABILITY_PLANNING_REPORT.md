# PHASE-NEXT-A01 — Product Capability Planning Report

## Result

```text
PHASE-NEXT-A01: NEXT PHASE RECOMMENDED
Recommended Next Major Phase: PHASE-REV — Knowledge Revision & Change Safety
Current Product Readiness: Internal Pilot
```

No implementation slice was started. The formal proposal is [PHASE_NEXT_PRODUCT_CAPABILITY_PLAN.md](../planning/PHASE_NEXT_PRODUCT_CAPABILITY_PLAN.md).

## 1. Scope and Evidence Reviewed

This was a Product / Architecture planning task. The review used the current worktree rather than historical prompts alone and covered:

- `AGENTS.md` and the frozen UI, Design, Domain, Database, Application Use Case, API, and Solution Structure specifications;
- the Knowledge Content architecture plan and KC-C01 relationship vocabulary decision;
- AUTH-B01/B02, UI-B03/B04, KC-B01–B07, PHASE-KC verification/R01, KC-C01/C02, and the current Gap Register;
- current backend features, EF migrations/model snapshot, controllers, authentication/bootstrap configuration, and tests;
- current Vue features, routes, KnowledgeDocument editor/detail/list, relationship/search/Unified View surfaces, and package scripts;
- the blocked SEC-04 Production Security Rollout report.

The worktree was already substantially dirty with prior AUTH, UI, KC, migration, test, and documentation work. Baseline `git status`, `git diff --stat`, and `git diff` were reviewed. This task did not reset, clean, revert, format, or modify production code.

## 2. Current Product Baseline

The current product is an authenticated internal knowledge hub, not a Confluence clone, ticketing platform, document-management system, or AI knowledge assistant.

Its defining boundary is:

```text
Structured system knowledge
+ authored typed Markdown documents
+ explicit Evidence / HumanConfirmation / KnowledgeStatus
+ typed relationships
+ lexical discovery and System-centered projection
```

### Authentication / access

- Local Login and OIDC capability coexist through the same application Cookie.
- canonical Current User, active-state checks, and Viewer/Editor/Administrator access are server-enforced.
- author and KnowledgeStatus attribution are server-trusted; the browser cannot select the authoritative actor.
- logout, antiforgery, rate-limit/lockout foundation, and Local/OIDC-aware login UX exist.
- Real Production HTTPS/OIDC/proxy/Data Protection verification remains blocked; Local password change/reset/management and MFA are absent.

### Structured knowledge

- Systems, Business Functions, Database Sources/Objects/Columns, Business Rules, Integrations, Unknown Items, Evidence, HumanConfirmation, Relationships, and KnowledgeStatus are implemented.
- Unknown Items support investigation, findings, Evidence, resolution, proposed/applied knowledge updates, close, and reopen.
- These are knowledge objects and knowledge-gap workflows. They do not record operational incidents or service execution history.

### Knowledge content and KnowledgeDocument

- One `KnowledgeDocument` aggregate supports Requirement, Specification, TestCase, SOP, Troubleshooting, KnowledgeArticle, and DesignNote.
- Users can create, list, filter, open, edit canonical Markdown, preview safely, save explicitly, and receive dirty-state/leave/conflict protection.
- Draft, Published, and Archived lifecycle is separate from Unknown, Inferred, and Confirmed KnowledgeStatus.
- Evidence and HumanConfirmation can support documents without automatic status progression.
- Typed relationships conform to the KC-C01/C02 closed vocabulary and endpoint matrix.
- The current entity stores one title, summary, Markdown body, author/update snapshots, lifecycle/status fields, and one app-managed concurrency version. No revision entity or revision migration exists.

### Discovery and traceability foundation

- Global Search uses a derived SQLite FTS5 projection for KnowledgeDocument title/summary/body, including Chinese body search and archive exclusion.
- System Unified Knowledge View aggregates bounded related structured knowledge, related documents, Evidence, and open Unknown Items.
- KC-C02 makes these traceability statements machine-readable:
  - Requirement → `SpecifiedBy` → Specification;
  - Requirement/Specification → `VerifiedBy` → TestCase;
  - SOP/Troubleshooting/Requirement → approved `AppliesTo` targets;
  - `Documents`, `References`, and same-type `Supersedes` under their approved constraints.
- No coverage view, missing-link analysis, impact navigation, or matrix exists.

## 3. Implemented Capabilities and Original Plan Completion

| Original capability intent | Current status | Evidence-based conclusion |
| --- | --- | --- |
| Typed long-form knowledge | Complete for the approved MVP boundary | Seven controlled document types share one concrete feature; no dynamic schema or duplicated aggregates. |
| Markdown-first authoring | Complete | Milkdown edit/preview, canonical Markdown, safe renderer, explicit save, and dirty/conflict UX passed verification. |
| Lifecycle independent of knowledge confidence | Complete | Draft/Published/Archived and Unknown/Inferred/Confirmed remain separate; Evidence/confirmation do not auto-transition. |
| Document relationships | Complete after correction | KC-C02 closed Gap 003 and retained exactly the approved 15 relationship values with endpoint restrictions. |
| Document Evidence and HumanConfirmation | Complete | Principal-backed confirmation and explicit status flow passed the R01 runtime chain. |
| KnowledgeDocument full-text discovery | Complete for current lexical scope | FTS title/summary/body, Chinese query handling, archive exclusion, and bounded 1,000-document test exist. |
| Unified Knowledge View | Complete for System scope | System detail has a bounded derived projection; no duplicate aggregate or write model was added. |
| Trusted identity and access | Complete for Internal Pilot | Local/OIDC capability, Current User, access levels, antiforgery, and logout exist; Production deployment gate remains separate. |
| Revision history after MVP validation | Not started | The approved architecture explicitly deferred this until after Content MVP and before broad authoring. |
| Attachments, Spaces/tree, comments, AI/RAG | Intentionally deferred | No current implementation was found and the architecture excluded them from first phases. |

## 4. Missing Capabilities

### Material product gaps

- no immutable KnowledgeDocument revision snapshots;
- no author/time history per content change, comparison, restore, or explicit latest-published revision semantics;
- no traceability tree, coverage gaps, impact navigation, or matrix over the now-typed relationship graph;
- no binary attachments or safe storage/download/retention model;
- no owner/reviewer/review-date/staleness governance;
- no import/export or migration workflow;
- no Incident/Problem operational record model.

### Experience enhancements rather than immediate blockers

- Spaces, folders, page trees, personal/team navigation;
- comments, mentions, watch lists, notifications, and social collaboration;
- richer search filters/ranking and relationship-aware discovery;
- broader Unified Views beyond System;
- editor conveniences beyond current safe Markdown behavior.

### Team Production constraints

Product-level:

- Published and Confirmed content can change without an inspectable historical chain. This materially weakens accountability, recovery, and review at broad authoring scale.

Engineering-level:

- SEC-04 is still `BLOCKED` for a real HTTPS/OIDC/reverse-proxy/Data Protection deployment loop;
- no approved deployment topology/configuration/rollback runbook was found;
- no proven Production database backup/restore rehearsal or recovery objectives were found;
- no bounded health/readiness, operational logging/retention, alert ownership, or production observability gate was found;
- SQLite production topology, capacity, file ownership, migration maintenance, and single/multi-instance assumptions are not approved;
- if Local Login is used broadly, self password change/recovery and credential administration remain product/security gaps.

## 5. Candidate Evaluation

`High` is desirable for value/pain/integrity/production/leverage and costly for dependency/complexity/risk. Ratings are qualitative, not pseudo-precise scores. There is no formal usage telemetry or pilot-interview record in the repository; Current Pain is inferred from the real supported workflow and verification findings.

| Candidate | User Value | Current Pain | Arch. Dependency | Data Integrity | Pilot → Prod. | Complexity | Risk | Future Leverage |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| A. Revision History / Versioning | High | High | Medium | High | High | Medium | Medium | High |
| B. Knowledge Organization | Medium | Low | High | Low | Low | High | Medium | Medium |
| C. Attachments | High | Medium | High | High | Medium | High | High | High |
| D. Collaboration | Medium | Low | High | Low | Low | High | Medium | Medium |
| E. Traceability UX | High | Medium | Low | Medium | Medium | Medium | Low | High |
| F. SOP / Problem Handling | Medium | Low | High | Medium | Low | High | High | Medium |
| G. Knowledge Governance | High | Medium | Medium | High | High | Medium | Medium | High |
| H. Search Evolution | Medium | Medium | Medium | Low | Medium | Medium | Medium | High |
| I. AI / RAG | Medium | Low | High | High | Low | High | High | High |
| J. Import / Export | Medium | Low | High | High | Medium | High | High | Medium |
| K. Production Readiness | Medium | High | High | High | High | High | High | High |

The matrix yields two different conclusions that must not be conflated:

1. Revision history is the strongest next **product capability**.
2. Production readiness is a mandatory, separately governed **engineering/release workstream**.

## 6. Revision History Analysis

### What happens today

A successful content update replaces title, summary, and `BodyMarkdown`, updates author/time, and increments the opaque concurrency version. It preserves no prior content. Published documents are editable, so concurrency prevents simultaneous lost updates but does not provide history or recovery.

### Required capability

- immutable snapshot on create and every successful explicit content save;
- actor/time and optional change summary;
- revision list and safe historical preview;
- readable title/summary/Markdown diff;
- restore by copying an old snapshot into a new head revision;
- clear latest-published revision semantics and changed-since-confirmation indicator.

### Lifecycle interaction

The recommended small model keeps one current aggregate and no branches. Publishing marks the current revision as latest published. Editing while Published creates and immediately exposes a new published revision, with an explicit UX warning. Draft changes can diverge from the last published revision. Archived content remains read-only until explicitly returned to Draft. Restore is Draft-only.

This preserves the current lifecycle and avoids introducing approval/review workflow accidentally. `REV-A01` must freeze the exact contract.

### Evidence and KnowledgeStatus interaction

Evidence, HumanConfirmation, and KnowledgeStatus should stay document-level initially. Copying or rebinding them per revision would create ambiguous facts and a much larger compliance model. Saving must not auto-change KnowledgeStatus. Instead, a derived warning should show when current content is newer than the most recent confirmation/Confirmed transition; users then make an explicit status decision.

### Production impact

No revision history does not prevent a tightly controlled Internal Pilot, but it does limit Team Production for long-lived Published knowledge. The missing historical chain makes accountability, review, incident recovery, and trusted reuse substantially weaker. It is therefore the next product priority.

## 7. Knowledge Organization Analysis

`KnowledgeDocument` is authored knowledge; Space/PageTree is navigation organization. They are not the same domain.

Current System context already carries part of the organization burden:

- documents can explicitly relate to Systems and structured objects;
- System Unified Knowledge View aggregates object-centered knowledge;
- FTS provides cross-system discovery.

A Page Tree would provide curated browse order, not the same result as a Unified View. However, there is no demonstrated need for Personal or Team Spaces, no team-scoped ACL model, and no evidence that current object-centered navigation is blocking the pilot. Adding a hierarchy now risks competing navigation truths and Confluence-like scope expansion.

Decision: no Space and no Page Tree in the next phase. If later approved, model organization and document placement separately; do not add document body copies or overload the KnowledgeDocument aggregate.

## 8. Traceability Analysis

KC-C02 materially changed the opportunity: Requirement/Specification/TestCase relationships are now machine-readable and constrained. A useful product projection can now show:

```text
Requirement
├─ Specifications
├─ Test Cases
├─ Missing specification coverage
├─ Missing test coverage
└─ Related Systems / impact paths
```

Coverage and matrix cells must be derived from current relationship truth, never stored independently. This candidate has high value, low foundational dependency, and relatively low risk.

It is the Alternative Next Phase if pilot users are already producing enough Requirements/Specifications/TestCases that coverage analysis is their dominant blocked job. It is not the primary recommendation because an expanded traceability experience would encourage broader authoring while the underlying content still lacks change history.

## 9. SOP / Problem Handling Analysis

- SOP and Troubleshooting are durable knowledge: what should be done in a known situation.
- UnknownItem is a knowledge-gap investigation: what is not yet known and how knowledge is corrected.
- Incident/Problem is an operational record: what happened at a specific time, impact, responder actions, recovery, and follow-up.

The current product correctly supports the first two concepts and has no Incident entity. An Incident module would require a separate use-case, retention, ownership, access, operational timeline, and likely integration decision. It should not be smuggled into SOP, Troubleshooting, or UnknownItem.

Decision: defer Incident/Problem handling until a distinct Product / Architecture phase is justified by real operational demand.

## 10. Attachment Analysis

Attachments are likely more urgent than Spaces or Comments because screenshots, PDFs, logs, SQL files, spreadsheets, Word documents, and configuration samples directly support knowledge and Evidence.

They are not recommended first because the minimum safe capability has coupled decisions:

- Document versus Evidence binding and stable IDs;
- local storage root, authorization, path traversal, safe filename/content disposition;
- MIME/content validation, file-size and quota policy, executable/macro posture, malware scanning stance;
- stable Markdown links and deletion/orphan/retention semantics;
- database plus file backup consistency and restore rehearsal;
- optional text extraction/search without treating derived text as source truth.

A future attachment phase should use separate metadata and one approved local-deployment store first. It must not put base64 in Markdown/SQLite or create a speculative multi-cloud storage framework. Revision history should precede it so document change and attachment-reference changes are accountable.

## 11. Collaboration Analysis

Current identity, access, authorship, optimistic concurrency, and HumanConfirmation are not a comments/review system. No repository evidence demonstrates current demand for mentions, watches, notifications, or real-time collaboration.

Adding them would create notification delivery, unread state, mention parsing, review state, retention, and permission questions. Lightweight owner/reviewer/review-date governance is more likely to deliver value first, and revisions give that future review a stable content basis.

Decision: Comments/Mentions/Watch/Notification are `DEFER NOW`.

## 12. AI/RAG Readiness

```text
AI/RAG = DEFER
```

The product has useful foundations—typed knowledge, KnowledgeStatus, Evidence, explicit relationships, trusted identity, and lexical search—but not enough for trustworthy AI behavior. Gaps include:

- no revision-stable citation/change model;
- no attachment ingestion/extraction policy;
- no permission-filtered retrieval design or evaluation corpus;
- no stale/change governance;
- no traceability coverage experience to validate suggested relationships;
- no defined answer citation, confidence, feedback, or hallucination gate.

AI must not infer facts into Domain truth, create relationships, or change KnowledgeStatus without explicit user actions. Adding a vector store now would be infrastructure-led development.

## 13. Production Readiness

### Classification

**Internal Pilot** remains the correct level after KC-C02. All KC gaps are closed and the authenticated content/evidence/status/search/Unified View chain passed R01/C02 verification. The product is not Team Production or Enterprise Production ready.

### Product capability gate

- revision/change safety for Published documents;
- later governance decision for owner/reviewer/staleness if Team Production scope requires scheduled review;
- Local credential lifecycle if Local Login, rather than OIDC, is a normal production access method.

### Production engineering gate

- real SEC-04 HTTPS/OIDC/reverse-proxy/Data Protection closed loop;
- safe Production configuration and secret injection;
- repeatable deployment, migration maintenance window, smoke checks, rollback ownership;
- approved SQLite production strategy/capacity or separate database decision;
- tested backup/restore and recovery objectives;
- health/readiness, structured logs, retention/privacy, alerts, and operator runbooks;
- verification of HSTS/proxy ownership, body logging, Cookie behavior, rate limits, and recovery in the actual topology.

Enterprise Production would additionally require organization-specific scale, availability, compliance, disaster recovery, audit retention, identity lifecycle, and support evidence. None should be claimed or speculatively built in this phase.

## 14. Recommended Next Major Phase

### PHASE-REV — Knowledge Revision & Change Safety

Why now:

- it closes the largest trust gap in the product's central authoring workflow;
- it follows the approved Knowledge Content architecture sequence;
- it protects Published content before authoring broadens;
- it gives future governance, review, attachments, import, AI citations, and traceability changes a stable historical basis;
- it is bounded enough for small vertical slices and does not require a platform rewrite.

Why not the other candidates now:

- Spaces/tree and collaboration have low demonstrated pain and broad scope;
- attachments are valuable but require unresolved storage/security/backup policy;
- traceability UX is ready but would expand use before change safety is solved;
- Incident is a distinct product module with no proven current demand;
- AI/RAG depends on several missing trust foundations;
- Production Engineering is mandatory but is not a product-capability substitute.

Risk of not doing it:

- users cannot prove or recover Published changes;
- Confirmed content may be materially edited without visible historical context;
- governance/review and future imports/AI cannot cite a stable content state;
- broad Team Production authoring increases the blast radius of accidental or inappropriate edits.

## 15. Alternative

### PHASE-TRACE — Traceability Experience

Select this alternative only when pilot evidence demonstrates that Requirement/Specification/TestCase coverage is the dominant current blocker and content change volume is still low/controlled.

Its first scope should be read-only derived projections: traceability tree, missing specification/test indicators, System impact navigation, and bounded matrix. It must not add a coverage table or weaken the relationship contract. Revision history remains a subsequent Team Production prerequisite.

## 16. DEFER NOW

- Spaces, folders, Page Tree, Personal/Team Space.
- Attachments during PHASE-REV.
- Comments, mentions, reviews, watches, notifications, real-time editing.
- Incident/Problem/TestRun/operational execution history.
- Full governance workflow, expiry jobs, and automatic stale-state mutation.
- attachment search/OCR, semantic/vector search, AI/RAG.
- bulk/Confluence import and PDF/HTML export frameworks.
- branching/merge/CRDT/autosave/semantic diff/revision deletion.
- generic audit/event/versioning/storage/workflow frameworks.

## 17. Recommended Slice Breakdown

| Slice | Goal | Main scope | Gate |
| --- | --- | --- | --- |
| REV-A01 | Freeze semantics and contracts | publication/revision/status rules, schema/API/UX/migration/security | Human Product / Architecture approval |
| REV-B01 | Immutable history foundation | additive schema, baseline backfill, atomic create/save snapshots, trusted actor | SQLite migration + focused integration + build |
| REV-B02 | History read UX | list/detail, metadata, safe historical preview | typed frontend/build + focused browser + Golden review |
| REV-B03 | Compare UX | previous/current and selected revision line diff | deterministic diff checks + responsive browser review |
| REV-B04 | Restore and change safety | Draft-only restore-as-new, reason, published warning, changed-since-confirmation | authorization/concurrency/status separation + real runtime chain |
| PHASE-REV-VERIFY | End-to-end acceptance | migration through browser/API/EF/SQLite and regression | zero Blocker/High, cleanup, formal readiness decision |

Each slice has detailed Scope, Non-goals, Dependencies, and Verification Gate in the formal plan. No slice was executed by this task.

## 18. Risks

| Risk | Planning response |
| --- | --- |
| Second source of document truth | Revisions are immutable child snapshots; current KnowledgeDocument remains head. |
| Published editing semantics become a hidden workflow change | Freeze the immediate-published revision behavior and expose explicit UX before implementation. |
| Confirmation becomes misleading after edit | Derived changed-since-confirmation warning; no automatic KnowledgeStatus transition. |
| Restore destroys later work | Restore creates a new revision and requires Draft, preview, reason, and current token. |
| SQLite size grows | Measure real payloads; avoid premature compression; index only proven queries. |
| Existing content receives invented history | Backfill a clearly labeled migration baseline only. |
| Adjacent capabilities enter the phase | Enforce DEFER list and separate Product / Architecture gates. |
| Feature PASS is mistaken for Production PASS | Keep SEC-04/deployment/backup/observability evidence independent. |

## 19. Human Decisions Required

The recommendation is unique, but implementation remains gated. Product / Architecture owners must approve:

1. PHASE-REV as next phase and PHASE-TRACE only as the evidence-triggered alternative.
2. The no-branch publication rule: a Published save creates a new immediately Published revision.
3. Draft-only restore-as-new with a required reason.
4. Document-level Evidence/KnowledgeStatus plus derived changed-since-confirmation warning.
5. Revision-1 migration baseline for current documents.
6. Independent ownership and scheduling of the Team Production engineering gate.

## 20. Planning Verification

- Required plan created: `docs/planning/PHASE_NEXT_PRODUCT_CAPABILITY_PLAN.md`.
- Required report created: `docs/reports/PHASE_NEXT_A01_PRODUCT_CAPABILITY_PLANNING_REPORT.md`.
- No production code, migration, API, frontend, test, or frozen specification was changed by PHASE-NEXT-A01.
- No build/test was required because this task changed planning/report documentation only.
- Task-document trailing-whitespace scan: PASS.
- `git diff --check`: PASS (exit `0`; Git emitted only pre-existing line-ending conversion warnings from the dirty worktree).

The task stops here and awaits the Human Product / Architecture Gate. It does not start `REV-A01` or any implementation slice.
