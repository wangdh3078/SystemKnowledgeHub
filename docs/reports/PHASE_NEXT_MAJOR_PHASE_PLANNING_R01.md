# PHASE-NEXT-PLANNING-R01

## Result

```text
PHASE-NEXT-PLANNING-R01 COMPLETE
Recommended Next Major Phase: PHASE-TRACE — Traceability Experience
Current Product Readiness: Internal Pilot
```

This is a planning and architecture-sequencing decision only. It does not authorize or start PHASE-TRACE, change a frozen specification, implement code, add a migration, or reopen PHASE-REV.

## Current Product Baseline

System Knowledge Hub is an authenticated internal knowledge system with two complementary kinds of truth:

```text
Structured system knowledge
+ authored typed Markdown knowledge
+ explicit typed relationships
+ Evidence / HumanConfirmation / KnowledgeStatus
+ immutable KnowledgeDocument revisions
+ current-head lexical discovery
```

The frozen MVP was verified complete before Knowledge Content and Revision were added. The current repository contains one .NET 8 ASP.NET Core API project, one Vue 3 strict-TypeScript frontend, one xUnit integration-test project, EF Core SQLite persistence, 18 incremental migrations, 45 backend test source files, and 35 frontend spec files.

The current formal boundary is still `Internal Pilot`. The application is not a general document-management system, project tracker, ITSM platform, Confluence clone, multi-tenant workspace, or AI knowledge assistant.

## Completed Major Capabilities

### Platform / Shell

- Light desktop application shell with left navigation, top-level global search, Main Content, object-level Context Rail, and a single Drawer/Dialog coordinator.
- Formal routes for dashboard, Systems, Business Functions, Database Objects, Unknown Items, KnowledgeDocument list/detail, Business Rule detail, Integration detail, and Administrator-only User Management.
- Global Create supports the implemented structured objects and KnowledgeDocument types without introducing separate create routes.
- Central native-fetch API client, typed feature API boundaries, Vue Router, Pinia for shared actor/overlay state, and Element Plus selective bootstrap.

### Authentication / Authorization / Users

- Local Login and configurable OIDC share the application cookie and canonical Current User model.
- Viewer, Editor, and Administrator access is enforced by backend policy; frontend gating is supplementary.
- Antiforgery, logout, active-user/login-identity checks, local-login lockout/rate-limit foundation, and fail-closed production OIDC/Data Protection configuration checks exist.
- User, KnowledgeRole, LoginIdentity, AccessLevel, active-state, and Administrator-safety management are implemented.
- Real production HTTPS/OIDC/proxy/key-persistence rollout remains unverified and is not implied by these capabilities.

### Structured Knowledge Model

- System, System technology/lifecycle, Business Function/process, Database Source/Object/Column/known values, Business Rule, Integration/contract fields.
- Evidence, HumanConfirmation, explicit KnowledgeStatus progression, and first-class directed KnowledgeRelation.
- Unknown Item investigation, Findings, Evidence, Resolution, Proposed/Applied Knowledge Updates, conclusion confirmation, close, reopen, and immutable activity facts.
- Dashboard, Global Search, Knowledge Target search, and System Unified Knowledge View projections.

### Knowledge Content

- One `KnowledgeDocument` core with controlled `Requirement`, `Specification`, `TestCase`, `Sop`, `Troubleshooting`, `KnowledgeArticle`, and `DesignNote` types.
- Create, list/filter, current read, raw Markdown edit, safe preview/read, page-level explicit Save, semantic no-op, optimistic concurrency, Draft/Published/Archived lifecycle, publish/return/archive behavior, dirty-state protection, and Global Create templates.
- CodeMirror raw Markdown authoring, bounded long-document viewport, Markdown tasks/tables, controlled color syntax, Mermaid strict rendering, scoped syntax highlighting, code copy feedback, and XSS-safe read boundaries.
- Document Evidence, HumanConfirmation revision snapshots, explicit KnowledgeStatus progression, typed relationships, FTS5 current-head search, and System Unified View inclusion.

### Revision Safety — Completed / Frozen Baseline

- Immutable revision snapshot on create and each semantic content save.
- Contiguous per-document revision numbers independent of the entity concurrency `Version`.
- Canonical current head plus latest-published pointer.
- Backend-paged History, immutable historical preview, deterministic line comparison, and bounded compare limits.
- Draft-only Restore-as-new with reason, lineage, authorization, concurrency rejection, and atomic FTS synchronization.
- Published-save confirmation, current-revision HumanConfirmation snapshot, backend-authoritative confirmation coverage, and no automatic KnowledgeStatus change.
- PHASE-REV is CLOSED with `PHASE-REV-VERIFY FINAL RESULT: PASS`. Revision History, Compare, Restore, raw-source storage, semantic Save, revision FTS synchronization, and HC revision snapshots must not be replanned as a new phase.

## Frozen Architecture / Contracts

The next phase must preserve these areas unless a separately approved architecture decision explicitly authorizes a change:

- `KnowledgeDocument` remains the canonical mutable current head; revisions remain immutable child snapshots.
- Raw Markdown is canonical source. Rendered HTML, Mermaid SVG, syntax-highlight output, diffs, search text, and future retrieval chunks are derived.
- One semantic create/save creates one revision; a semantic no-op creates none.
- Restore copies an old snapshot into a new head revision and never edits/deletes history.
- Published-save safety and Draft/Published/Archived lifecycle remain independent of KnowledgeStatus.
- HumanConfirmation records the explicit current revision snapshot; Evidence/confirmation never auto-transition status.
- FTS indexes only current, non-archived discoverable content; historical revisions do not enter global search.
- Viewer reads and Editor/Administrator writes remain the authoritative access boundary.
- Relationships remain explicit, directed, single-row facts under the closed 15-value vocabulary and server-enforced endpoint/DocumentType matrix.
- Coverage, trace trees, impact paths, search indexes, and Unified Views must be derived read projections, never competing write truth.
- No generic KnowledgeObject, graph engine, repository framework, CQRS/event bus, dynamic forms, second overlay manager, or automatic inferred relationship/status write.

## Remaining Specification Scope

The frozen MVP itself has no unimplemented mandatory business slice. Remaining work belongs to post-MVP decisions with different maturity levels:

- **Formally enabled foundation, experience not implemented:** Requirement → Specification → TestCase traceability and structured-target applicability/impact projections.
- **Partially designed future capability:** Attachments have high-level storage/retention/security rules but no approved domain/API/storage contract.
- **Explicitly deferred and not sufficiently designed:** Spaces/page tree, comments/collaboration, AI/RAG, advanced governance, import/export, Incident/Problem operations, and advanced search/discovery.
- **Partially implemented horizontal gate:** Production security foundations exist, but deployment, recovery, observability, database operations, and real-environment verification remain incomplete.
- **Technical debt:** five open Low REV gaps remain recorded; they are not a product phase.

## Implementation Coverage Matrix

| Capability | Specification State | Implementation State | Verification State | Dependencies | Notes |
|---|---|---|---|---|---|
| MVP shell/navigation/Golden interaction model | Frozen MVP | Implemented | MVP FINAL PASS plus later UI gates | None | Current shell must be extended, not redesigned. |
| Structured knowledge catalog | Frozen MVP | Implemented | VS01–VS15 and MVP FINAL PASS | SQLite/API/Vue baseline | Concrete features; no generic object abstraction. |
| Unknown Item investigation/resolution | Frozen MVP | Implemented | VS09A/VS09B PASS | Structured targets, Evidence | Not Incident/Problem management. |
| Evidence / HumanConfirmation / KnowledgeStatus | Frozen plus security amendments | Implemented | VS06/VS07, U04, SEC fixes, KC/REV PASS | Current User, target resolver | Status remains explicit. |
| Authentication/access/user administration | Approved post-MVP security design | Implemented for Internal Pilot | AUTH/SEC01–03/U01–04 PASS; SEC04 BLOCKED | Real deployment environment | Production rollout not approved. |
| KnowledgeDocument core and Markdown UX | Approved post-MVP architecture | Implemented | KC-B01–B07, UI-KC-R06, PHASE-KC gates | Existing shell/security | Seven controlled types, one aggregate. |
| Relationship vocabulary and endpoint matrix | Approved KC-C01 decision | Implemented | KC-C02 PASS | KnowledgeDocument types, target resolver | Includes machine-readable traceability edges. |
| Revision/change safety | Approved REV-A01 decision | Implemented / Frozen | REV-B01–B04, REV-FIX-01, Delta PASS | KnowledgeDocument, Current User, FTS | PHASE-REV CLOSED. |
| Global lexical search | Frozen MVP plus KC extension | Implemented | VS13 and KC-B06/REV regression PASS | LIKE for structured data, FTS5 for documents | Current-head only; no semantic or graph ranking. |
| System Unified Knowledge View | Approved KC architecture | Implemented | KC-B07 PASS | System and relationship projections | System-centered and bounded. |
| Traceability experience | Candidate backed by relationship decision | Foundation only | Relationship positive/negative tests; no trace UX verification | Typed relationships, revisions, status/evidence | No coverage/tree/matrix/impact product surface yet. |
| Attachments | Future rules only | Not implemented | None | Storage/security/retention/backup decisions | Partially designed, not ready for implementation. |
| Spaces/page tree | Explicitly out of MVP | Not implemented | None | Ownership/RBAC/search/relation scope decisions | Not sufficiently specified. |
| Search/Knowledge Discovery enhancement | Future candidate only | Not implemented beyond current FTS/Unified View | None as a phase | Trace and attachment semantics | Not sufficiently specified for detailed planning. |
| AI/RAG | Explicitly deferred | Not implemented | None | Permissions, citations, chunks, evaluation, jobs, attachment policy | Not ready. |
| Production Engineering | Separate horizontal gate | Partial code foundation; no delivery system | SEC04 BLOCKED | Real OIDC/proxy/HTTPS/keys/deployment ownership | Required before Team Production, not a product-feature substitute. |

## Existing Deferred Gaps

The authoritative row status and closure evidence in the Gap Register give the following result:

| Gap | Status | Planning treatment |
|---|---|---|
| REV-GAP-005 | CLOSED — UI-KC-FIX-01 | No action; tooltip registration closure is preserved. |
| REV-GAP-006 | OPEN / Deferred | Restore dialog accessible name; address when the overlay/dialog accessibility surface is naturally changed or in accessibility hardening. |
| REV-GAP-007 | OPEN / Deferred | Nested History `main`; address when that landmark is naturally changed or in accessibility hardening. |
| REV-GAP-008 | OPEN / Deferred | Published confirmation overlay coordination; address if a future phase changes modal coordination. |
| REV-GAP-009 | OPEN / Deferred | Missing direct Version rollback assertion; small verification-debt correction, not product scope. |
| REV-GAP-011 | OPEN / Deferred | Default parallel backend-suite stall; test-infrastructure/CI concern, not product scope. |

The Gap Register boundary sentence still says all six Low gaps are deferred, while its table and explicit `REV-GAP-005` closure section correctly record that one is CLOSED. This internal prose-count drift does not change the authoritative status above and is not modified by this planning task.

None of the five open Low gaps is a reason to manufacture a Major Product Phase or reopen PHASE-REV.

## Major Phase Candidates

### PHASE-TRACE — Traceability Experience

Repository basis is real but not yet a complete phase design: the approved relationship decision defines machine-readable `Requirement SpecifiedBy Specification`, `Requirement/Specification VerifiedBy TestCase`, `AppliesTo`, `Documents`, `References`, and `Supersedes` semantics; the backend enforces them; Revision gives stable current/history boundaries; Evidence and Relationship KnowledgeStatus can express trust context.

The missing capability is a user-facing, read-only derived experience: coverage gaps, trace tree, bounded impact navigation, and possibly a filtered matrix. This is the strongest ready candidate, but it must begin with an architecture/contract decision rather than code.

### Attachments

High user value and explicit future guidance exist, but the repository has no Attachment entity, metadata schema, storage abstraction, upload/download API, authorization contract, retention policy, or backup-consistency model. The capability is partially designed and not implementation-ready.

### Search Enhancement / Knowledge Discovery

The current FTS and System Unified View are implemented. Relationship-aware filters/ranking, attachment search, cross-object impact search, synonyms, semantic retrieval, and broader Unified Views are not a single approved contract. This candidate should follow the data semantics it intends to discover. It is not sufficiently specified for detailed planning now.

### Spaces / Knowledge Organization

Spaces and page trees are explicitly excluded from the MVP and Knowledge Content first phases. No tenant/team/project ownership, inheritance, ACL, search-scope, relationship-scope, or migration contract exists. This is a later product-layer candidate, not ready.

### Knowledge Governance / Operational Administration

User administration exists. Owner/reviewer/review-date/staleness governance, local credential self-service, retention, audit policy, and operational administration are separate concerns and have no unified approved product design. Not sufficiently specified for a Major Phase.

### AI / RAG

The repository has useful foundations but no approved AI product contract. AI/RAG remains a dependent future candidate, not an automatic consequence of the product name.

### Production Engineering

This is a required release/readiness workstream. It can become the next engineering program when a real deployment environment and owners exist, but it is not the best next product capability and cannot currently close its own SEC04 gate.

## PHASE-TRACE Readiness

### Business purpose and users

- Purpose: answer which Requirements are specified and test-defined, where coverage is missing, and which Systems/Business Functions are affected by a change.
- Primary users: business/system analysts, domain experts, developers, testers, and reviewers who maintain Requirement/Specification/TestCase knowledge.
- Value: replace manual link-following and spreadsheet coverage tracking with a truthful projection over explicit repository relationships.

### Existing foundations

- Target entities: Requirement, Specification, TestCase KnowledgeDocuments plus System/BusinessFunction and other permitted structured targets.
- Relationship dependency: fully available through the closed vocabulary, canonical direction, endpoint/DocumentType validation, incoming/outgoing reads, and relationship Evidence/KnowledgeStatus.
- Evidence dependency: relationship Evidence and status exist, but the phase must decide whether unconfirmed edges count as coverage or are shown as lower-trust coverage.
- Revision dependency: complete. Trace should use current document heads and current relation truth; historical revisions remain available for explaining content change, not as graph nodes.
- Backend readiness: direct-DbContext page projections, target resolver, endpoint policy, relationship indexes, and System Unified View pattern exist. The current generic related-object query is not itself a bounded trace/coverage API and includes per-relation resolution work.
- Frontend readiness: document detail, System detail, tables, empty states, routing, typed API decoders, and responsive shell exist. No approved trace route or Golden UI extension exists.

### Expected impact

| Layer | Expected impact |
|---|---|
| Domain | Prefer no new write entity; define derived trace/coverage language only. |
| Application | Concrete read queries for trace tree, missing-link indicators, and bounded impact paths. |
| Infrastructure | Query-plan/index review; no graph database or background projector. |
| API | Page-oriented, concrete read contracts; no generic graph traversal endpoint. |
| Database | No coverage table or duplicate edge truth; add an index only if a proven query plan needs it. |
| Frontend | A01-approved trace surface integrated with existing detail/navigation model. |
| Security | Authenticated read policy; existing Editor relationship authoring remains the only write path. |
| Tests | Focused projection/contract/security tests plus one real read-only trace flow. |

### Readiness conclusion

```text
PHASE-TRACE: READY FOR ARCHITECTURE DECISION, NOT READY FOR DIRECT IMPLEMENTATION
```

It has the highest dependency readiness and lowest likely rework of the product candidates because it derives value from existing truth rather than creating a new storage/security subsystem.

## Attachments Readiness

- Domain contract: absent. Evidence explicitly states it is not a generic attachment model.
- Metadata/storage: absent; no blob/file table, storage provider, root, stable attachment identifier, or safe-name contract.
- API: no upload/download/delete/metadata route.
- Authorization: existing document access is reusable context, but attachment-specific read/write, archived-document, and direct-URL rules are undefined.
- Revision relationship: future guidance says an attachment referenced by history cannot be physically removed without retention design; exact snapshot/reference semantics are unresolved.
- Evidence relationship: an attachment may be a source for Evidence, but must not become Evidence automatically.
- Markdown relationship: controlled stable IDs/URLs are proposed; renderer and broken-reference behavior are not frozen.
- Security/operations: MIME/content validation, size/quota, path traversal, download disposition, macro/executable posture, malware stance, orphan cleanup, retention, and database/file backup consistency are unresolved.

```text
Attachments: PARTIALLY DESIGNED / NOT READY
```

## Spaces Readiness

No Space domain meaning is approved. There is no tenant/project/team boundary, ownership model, per-Space RBAC, document/system placement rule, scoped search, cross-Space relationship rule, inherited lifecycle, or migration plan. Existing navigation is object-centered through Systems, relationships, search, and Unified View.

```text
Spaces: NOT READY — Insufficient repository evidence for detailed planning.
```

Spaces are not necessary for the current MVP and would introduce a competing navigation and authorization truth.

## AI / RAG Readiness

Available foundations:

- canonical raw Markdown and stable current-head semantics;
- immutable revisions and current-only FTS;
- explicit typed relationships, Evidence, HumanConfirmation, and KnowledgeStatus;
- authenticated Viewer/Editor/Administrator boundary.

Missing foundations/design:

- approved AI user scenarios and evaluation corpus;
- chunking/version invalidation and current-vs-history indexing policy;
- permission-aware retrieval and leakage tests;
- citation/provenance contract tied to document revision and Evidence;
- attachment extraction policy;
- embedding model/store, background job/rebuild, deletion/retention, and operational ownership;
- answer confidence, feedback, hallucination, and canonical-write approval boundary.

```text
AI / RAG: NOT READY — Insufficient repository evidence for detailed planning.
```

AI output must never become canonical knowledge, a relationship, Evidence, or KnowledgeStatus transition without an explicit user operation.

## Production Engineering Readiness

Implemented foundations include fail-closed production OIDC/Data Protection configuration checks, secure-cookie policy, antiforgery, Local Login rate limiting/lockout, console logging, access policies, HTTPS redirection outside Development, and operator-controlled administrator bootstrap.

Material gaps remain:

- no real OIDC provider/callback, HTTPS host, reverse-proxy topology, or trusted forwarded-header configuration;
- no verified persistent/protected Data Protection key store or restart/redeploy cookie continuity;
- no deployment artifact, CI/CD definition, release ownership, migration maintenance procedure, rollback runbook, or smoke automation;
- no health/readiness endpoint, structured operational/security logging pipeline, retention/privacy rules, alerts, monitoring, or error-reporting ownership;
- no approved SQLite production topology/capacity/concurrency strategy, backup/restore objectives, or restore rehearsal;
- HSTS/CSP/security-header ownership and actual proxy behavior remain unverified;
- no relevant load/concurrency/performance baseline or complete accessibility-hardening gate.

```text
Production Engineering: REQUIRED BEFORE TEAM PRODUCTION, CURRENTLY ENVIRONMENT-BLOCKED
```

Starting it without the real deployment topology would repeat SEC04's blocker and risk speculative infrastructure. It should resume as a parallel readiness workstream as soon as the required environment and owners exist, and it must pass before any Team Production claim.

## Dependency Graph

```text
Completed foundations
MVP structured knowledge + Security/Internal Pilot
        + KnowledgeDocument/KC
        + typed Relationship/Evidence/Status
        + immutable Revision/current-head FTS
                         |
                         v
Immediate product candidate
PHASE-TRACE — derived traceability/coverage/impact experience
          |                         \
          v                          v
Dependent candidates          Separate storage branch
Search/Discovery              Attachment A01 storage/security decision
          |                          |
          +------------+-------------+
                       v
Later candidates
Governance / permission-aware retrieval / AI-RAG

Parallel release gate across all stages:
Production Engineering -> required before Team Production
```

Spaces remain outside this dependency path until a separate product need and ownership/RBAC decision exist.

## Candidate Scoring

Scores are 1–5. Higher `Implementation complexity` and `Rework risk` mean more cost/risk; the other higher scores are favorable. Scores are decision aids, not a mechanical total.

| Candidate | MVP business value | Dependency readiness | Architecture readiness | User-visible value | Risk reduction | Implementation complexity | Rework risk | Unlocks future phases |
|---|---:|---:|---:|---:|---:|---:|---:|---:|
| PHASE-TRACE | 5 | 5 | 4 | 5 | 3 | 3 | 2 | 5 |
| Attachments | 4 | 3 | 2 | 5 | 3 | 4 | 4 | 4 |
| Search / Discovery Enhancement | 3 | 3 | 2 | 4 | 2 | 3 | 4 | 4 |
| Spaces / Organization | 2 | 1 | 1 | 3 | 1 | 5 | 5 | 3 |
| Governance / Operational Administration | 3 | 2 | 1 | 3 | 4 | 4 | 4 | 4 |
| AI / RAG | 2 | 2 | 1 | 4 | 2 | 5 | 5 | 5 |
| Production Engineering | 4 | 2 | 3 | 1 | 5 | 5 | 3 | 5 |

PHASE-TRACE is recommended because readiness, immediate user-visible value, architecture fit, and low rework align. Production Engineering has the highest risk-reduction role but is a parallel release gate with missing external prerequisites. Attachments has broad value but would force unresolved storage/security/retention decisions. Other candidates depend on semantics or infrastructure that do not yet exist.

## Recommended Next Major Phase

```text
Recommended Next Major Phase: PHASE-TRACE — Traceability Experience
```

The phase should deliver a truthful, bounded, read-only experience over current explicit relationship truth. Its central path is:

```text
Requirement
  -> Specification(s)
  -> TestCase(s)
  -> missing specification/test-definition indicators
  -> related System/BusinessFunction impact context
```

`VerifiedBy` means a TestCase defines how knowledge is verified; it is not a TestRun, pass/fail result, or execution record. PHASE-TRACE must preserve that distinction.

## Why This Phase Now

- PHASE-REV removed the prior change-safety prerequisite and provides stable current/history boundaries.
- KC-C01/C02 already made trace relationships machine-readable, directed, and server-validatable.
- Evidence and Relationship KnowledgeStatus provide trust context without a new graph/coverage write model.
- System Unified View proves the repository's small page-oriented derived-projection pattern.
- The capability exposes value from facts users already author rather than demanding a storage service, new identity boundary, or external platform.
- It improves Requirement/Specification/TestCase usefulness before advanced discovery or AI relies on those links.
- It can remain read-only and bounded, making it the lowest-rework next product phase.

Repository evidence does not contain formal pilot telemetry or a measured Requirement corpus. TRACE-A01 must validate the priority with Product owners and sample real workflows before implementation; this uncertainty is not sufficient to prefer a less ready candidate.

## Why Other Candidates Later

- **Attachments:** revision prerequisite is complete, but storage, security, retention, historical reference, and backup decisions are not.
- **Search/Discovery:** should consume trace and attachment semantics rather than invent relationship-aware ranking before those surfaces exist.
- **Spaces:** no demonstrated MVP need or ownership/RBAC model; likely to create competing browse truth.
- **Governance/Admin:** no agreed owner/reviewer/staleness workflow; should build on revisions and observed pilot use.
- **AI/RAG:** lacks permission-aware retrieval, citation, evaluation, ingestion, and operational boundaries.
- **Production Engineering:** mandatory for rollout, but currently blocked on an actual environment and is not a substitute for selecting the next product capability.
- **Deferred Low gaps:** technical debt and accessibility/test-infrastructure corrections, not a Major Product Phase.

## Recommended Future Phase Sequence

```text
1. PHASE-TRACE — Traceability Experience
2. PHASE-ATTACHMENTS — Controlled Files and Images (Tentative; requires approved storage/security/retention decision)
3. PHASE-DISCOVERY — Relationship- and Attachment-aware Knowledge Discovery (Tentative)
4. PHASE-GOVERNANCE — Ownership, Review, and Staleness (Tentative; insufficient current design)
5. PHASE-AI-RAG — Permission-aware Cited Retrieval (Tentative; only after explicit product/evaluation approval)

Parallel release gate: Production Engineering resumes when a real deployment environment is available and must pass before Team Production.
```

Spaces, collaboration, Incident/Problem operations, and import/export are omitted from the recommended sequence because repository evidence is insufficient to establish priority or contracts.

## Recommended Phase In Scope

- Freeze the business meaning of trace coverage, gaps, impact, and trust indicators.
- Derived Requirement → Specification → TestCase tree over current KnowledgeRelations.
- Missing specification and missing test-definition indicators.
- Display incoming/outgoing canonical relation semantics without duplicate inverse rows.
- Show relevant document lifecycle, KnowledgeStatus, relation KnowledgeStatus, and Evidence context without computing new truth.
- Bounded navigation from Requirement/Specification/TestCase to related System/BusinessFunction context.
- A bounded, filtered coverage/matrix projection only if TRACE-A01 proves it materially improves the chosen user workflow.
- Concrete page-oriented read contracts, pagination/limits, cycle/path limits, and safe empty/error states.
- Viewer-readable surfaces; relation creation/correction remains the existing Editor operation.
- Focused query, contract, authorization, frontend, accessibility, and one Browser → API → SQLite read-flow verification.

## Recommended Phase Out of Scope

- Revision architecture, history, compare, restore, or current-head storage changes.
- Persisted coverage rows, generic graph database/service/query language, arbitrary traversal, or inferred/automatic edges.
- Test execution, pass/fail, TestRun/TestPlan, requirement approval, release gating, or workflow automation.
- Automatic relationship creation, automatic Evidence, automatic KnowledgeStatus transition, or AI suggestions.
- New relationship wire values unless a separate vocabulary decision proves the need.
- Editing relationships inside a matrix as a second write path.
- Attachments, Spaces/page trees, comments/mentions/notifications, tags, import/export, Incident/Problem management.
- Search engine replacement, embeddings/vector search, AI/RAG.
- Production Engineering, deployment, backup/restore, observability, or unrelated Low-gap cleanup.
- Refactoring working KnowledgeDocument/Relationship/Evidence code into generic frameworks.

## Proposed Sub-phase Structure

| Sub-phase | Objective | Major deliverables | Dependency | Verification boundary |
|---|---|---|---|---|
| TRACE-A01 | Architecture and contract decision | User scenarios, coverage/trust semantics, lifecycle/revision boundary, UI location, API projections, path limits, security, no-storage decision | Human Product/Architecture review of this plan | APPROVED decision; no unresolved material conflict; no code/migration |
| TRACE-B01 | Derived trace read foundation | Concrete Requirement/Specification/TestCase and gap projections; bounded queries; page-oriented API; no coverage table | TRACE-A01 | Focused SQLite/API tests for direction, missing links, trust/lifecycle filters, cycles/limits, Viewer access |
| TRACE-B02 | Document traceability UX | A01-approved document-level trace tree/gaps, contextual navigation, loading/empty/error/accessibility behavior | TRACE-B01 | Type-check/build, focused component/contract tests, real Requirement → Specification → TestCase browser path |
| TRACE-B03 | Bounded impact and coverage view | A01-approved System/BusinessFunction context and optional filtered matrix; no arbitrary graph explorer | TRACE-B01–B02 | Query-plan/limit checks, responsive Golden review, focused navigation/runtime verification |
| PHASE-TRACE-VERIFY | End-to-end phase decision | Static/automated/runtime audit of truth, direction, coverage, permissions, performance bounds, and frozen-baseline regression | TRACE-B01–B03 | Zero Blocker/High; no competing truth; cleanup complete; formal phase result |

No sub-phase is started by this report.

## Required Architecture Decisions Before Implementation

1. Which real pilot workflow and sample corpus justifies PHASE-TRACE, and who owns acceptance?
2. Does “coverage” count every explicit edge, only non-Unknown relationship status, or all edges with trust displayed separately?
3. How do Draft, Published, Archived, superseded, and missing/inaccessible documents participate in current trace projections?
4. Are trace projections always current-head/current-relation truth, and how is historical revision context linked without creating revision graph nodes?
5. What exact missing-link rules apply to Requirement and Specification, and how are direct Requirement → TestCase links shown alongside Requirement → Specification → TestCase?
6. What System/BusinessFunction impact paths are valid under `AppliesTo`, `Documents`, and the existing structured relation vocabulary?
7. What traversal depth, cycle handling, row/page limits, and deterministic ordering prevent graph/path explosion?
8. Where does the UI live: existing KnowledgeDocument detail, System Unified View, or one new approved route? A Golden/UI inventory extension is required before implementation.
9. What concrete API responses serve each page without a generic graph endpoint or N+1 target resolution?
10. Are existing relationship indexes sufficient; if not, what measured query justifies a minimal additive index?
11. How are relationship Evidence and KnowledgeStatus displayed without implying verification execution or automatically changing coverage truth?
12. What accessibility, authorization, XSS/text escaping, and performance limits constitute the phase gate?

## Risks

| Risk | Planning response |
|---|---|
| Sparse real Requirement/Specification/TestCase corpus makes the feature low-value | TRACE-A01 must validate real pilot workflows and seed only purposeful test fixtures. |
| `VerifiedBy` is misrepresented as passed testing | Freeze “test definition, not execution result” language and exclude TestRun/pass-fail. |
| Coverage becomes a second stored truth | Derive it from canonical current relationships; no coverage table or editable matrix cells. |
| Unknown/unconfirmed edges are treated as equal trust | Freeze counting/display semantics and expose relation status/Evidence context. |
| Archived/superseded/current-head semantics produce misleading gaps | Freeze lifecycle and `Supersedes` treatment in TRACE-A01. |
| Graph cycles or fan-out cause slow queries/UI overload | Use approved paths, bounded depth, pagination, limits, deterministic order, and query-plan checks. |
| Generic graph abstractions expand architecture | Use concrete trace queries and response models only. |
| A new route conflicts with frozen UI inventory | Require an explicit UI/Golden amendment before adding a route. |
| Trace code mutates relationship truth | Make the phase read-only; all writes stay in existing relationship authoring. |
| Product PASS is confused with Production readiness | Keep Internal Pilot and the independent Production Engineering gate explicit. |

## Product Readiness Boundary

Completion of PHASE-TRACE would improve the value and inspectability of typed knowledge relationships. It would not by itself approve production deployment, attachments, organization-wide permissions, compliance workflows, semantic retrieval, or AI.

Until the independent Production Engineering gate passes in a real deployment environment, the product remains:

```text
Internal Pilot
```

## Files Inspected

The review read/scanned actual content rather than relying on filenames alone.

### Normative baseline

- `AGENTS.md`
- all seven frozen files under `docs/specifications/`, including UI Inventory, Design Baseline, Domain, Database, Application Use Case, API Contract, and Solution Structure
- `docs/design/KNOWLEDGE_CONTENT_DOCUMENT_ARCHITECTURE_PLAN.md`
- `docs/design/KC_C01_RELATIONSHIP_VOCABULARY_ARCHITECTURE_DECISION.md`
- `docs/design/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md`
- relevant authentication/security, HumanConfirmation, Markdown source/extension/theme, and User/Person decisions under `docs/design/`

### Accepted phase evidence

- `docs/reports/FINAL_MVP_VERIFICATION_REPORT.md`
- KC-B01–KC-B07, KC-C01/C02, PHASE-KC reports and gap register
- REV-B01–REV-B04, PHASE-REV end-to-end report, Gap Register, REV-FIX-01, UI-KC-B05-R06, and final Delta Report
- AUTH-B01/B02, SEC-01–SEC-04, U01–U04, VS01–VS15, search, Unified View, UI acceptance, and prior planning reports
- the linked `docs/planning/PHASE_NEXT_PRODUCT_CAPABILITY_PLAN.md`

### Real implementation

- solution/project/package manifests and all feature inventories under `src/` and `tests/`
- all API Controller route attributes and feature Application service/query files
- `KnowledgeHubDbContext`, 18 migrations, KnowledgeDocument/Revision entities and persistence configuration
- relationship endpoint policy, resolver/query/service, vocabulary migration, and positive/negative relationship tests
- Search queries/FTS helpers, System Unified View query, Current User/access/security bootstrap
- Vue routes/navigation, shell/overlay/actor/API client, KnowledgeDocument editor/read/history/compare/restore, Evidence/HC, relationships, search, and System Unified View surfaces
- backend test inventory and all frontend `.spec.ts` inventory
- absence scans for Attachment, Space, trace projection, embedding/RAG, health/readiness, deployment, and CI/CD implementations

No API, Vite, Browser, migration, build, or test process was required or started for this read-only planning task.

## Final Recommendation

```text
PHASE-NEXT-PLANNING-R01 COMPLETE

Recommended Next Major Phase: PHASE-TRACE — Traceability Experience

Recommended Sequence:
1. PHASE-TRACE — Traceability Experience
2. PHASE-ATTACHMENTS — Controlled Files and Images (Tentative)
3. PHASE-DISCOVERY — Relationship- and Attachment-aware Knowledge Discovery (Tentative)
4. PHASE-GOVERNANCE — Ownership, Review, and Staleness (Tentative)
5. PHASE-AI-RAG — Permission-aware Cited Retrieval (Tentative)

Parallel release gate: Production Engineering before Team Production.
```

Wait for Human Product / Architecture review. Do not start TRACE-A01 or any implementation phase automatically.
