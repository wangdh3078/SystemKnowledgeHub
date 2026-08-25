# TRACE-A01 — Traceability Architecture and Contract Decision

> Product: 系统知识中心 / System Knowledge Hub
>
> Phase: PHASE-TRACE — Traceability Experience
>
> Decision date: 2026-08-25
>
> Implementation status: Not implemented; this document does not start TRACE-B01

## Decision Status

```text
TRACE-A01 APPROVED
Blocking human decisions: NONE
TRACE-B01 implementation status: NOT STARTED
```

This decision freezes the first Traceability product, read-projection, API, UI-placement, security, performance, and verification contract. It authorizes no code, test, route, schema, migration, package, or repository-database change by itself.

## Context

PHASE-REV is closed and its final verification passed. The current repository already has canonical `KnowledgeDocument` current heads, immutable revisions, explicit typed `KnowledgeRelation` rows, Evidence, HumanConfirmation, independent KnowledgeStatus and document lifecycle, current-head FTS, Viewer/Editor/Administrator access, and a bounded System Unified Knowledge View projection.

KC-C01 and its implemented KC-C02 correction make these traceability facts machine-readable:

```text
Requirement   --SpecifiedBy--> Specification
Requirement   --VerifiedBy-->  TestCase
Specification --VerifiedBy-->  TestCase
```

TRACE therefore does not need a new relationship system. It needs a truthful, bounded read experience over facts that users already author through the existing relationship path.

The repository application database currently contains four Draft/Unknown `KnowledgeArticle` documents, one direct `Supersedes` relation between two of them, three `Documents → System` relations, and one `References → System` relation. It contains no Requirement, Specification, TestCase, `SpecifiedBy`, or `VerifiedBy` data. The focused relationship API test does construct and validate a Requirement → Specification → TestCase chain. This is enough to prove technical vocabulary support, but it is not a representative pilot corpus or product-usage measurement.

## Problem Statement

The application can author and list individual relations, but it cannot directly answer:

- which Specifications define a Requirement;
- which TestCases define verification for a Requirement or Specification;
- where a current knowledge chain is structurally incomplete;
- what trust signals support each document and relation;
- which explicitly scoped or documented Systems and BusinessFunctions are relevant to a Requirement or Specification.

Following relations one drawer or page at a time does not give a stable coverage view. Persisting a new graph or coverage record would duplicate truth and create drift. Frontend-only traversal would create N+1 requests, inconsistent authorization, and client-owned business semantics.

## Product Goal

Provide an authenticated, read-only, derived Traceability experience that makes the current explicit Requirement → Specification → TestCase chain, its structural gaps, its trust context, and a small set of bounded impact-context paths visible without adding another source of truth.

The first release optimizes for a compact trace tree, explicit missing-link indicators, and navigation to existing details. It is not a graph explorer, test-execution system, workflow engine, or automatic inference feature.

## Primary User Workflows

### Primary workflow

As an analyst or domain expert viewing a Requirement, I can see:

1. which active Specifications define it;
2. which active TestCases directly define how it is verified;
3. which active TestCases define verification through each Specification;
4. whether a Specification or Test Definition link is missing;
5. lifecycle and trust context for each displayed document and relation;
6. links to the existing document or relationship detail.

### Secondary workflow

As a developer, tester, or reviewer viewing a Specification, I can see upstream Requirements and downstream TestCase definitions. When viewing a TestCase, I can see the Requirements and Specifications it is defined to verify, including the Requirement immediately upstream of an incoming Specification where available.

### Impact workflow

As an analyst viewing a Requirement, Specification, or TestCase, I can see the bounded structured context expressed by approved `AppliesTo` and `Documents` paths, with the exact path meaning shown. This context tells me where review may be needed; it does not claim runtime dependency, change propagation, or blast-radius certainty.

### Pilot validation boundary

The formal product model and relationship contract justify these workflows, but current repository data does not validate their frequency or scale. TRACE-B01/B02 may proceed against purposeful isolated fixtures after this decision is accepted. Before PHASE-TRACE product acceptance, a Product owner must validate at least one real domain example containing one Requirement, one Specification, one TestCase, and one System or BusinessFunction context. This is a final product-acceptance gate, not a blocking architecture choice.

## Existing Foundations

| Foundation | Current repository evidence | TRACE consequence |
|---|---|---|
| KnowledgeDocument | Seven controlled types, current mutable head, lifecycle, KnowledgeStatus, current revision number | Trace nodes use current heads only. |
| KnowledgeRelation | One directed row, closed fifteen-value vocabulary, exact-edge uniqueness | Trace edges reference canonical relation IDs; no inverse rows. |
| Endpoint policy | Server validates endpoint type, DocumentType, direction, same-type Supersedes, and self-relations | No new wire value or endpoint policy change is needed. |
| Evidence | Document and KnowledgeRelation subjects with indexed subject lookup | Return small counts/signals; details remain in existing Evidence/relationship UX. |
| HumanConfirmation | Document revision snapshot plus relationship-level confirmation Evidence | Show confirmation context without calculating a new status. |
| KnowledgeStatus | Explicit Unknown → Inferred → Confirmed progression with Evidence gates | Coverage never depends on status; status remains trust context. |
| Revision | Immutable history, compare, restore-as-new, current revision confirmation coverage | Historical revisions never become trace nodes. |
| System Unified View | Concrete bounded direct-DbContext read projection | Reuse the projection style, not its DTO or query as a generic framework. |
| Search / FTS | Current non-Archived document heads only | TRACE does not search historical revisions or replace FTS. |
| Authorization | Fallback Viewer read; Editor/Administrator writes | Trace reads use Viewer; relationship writes remain unchanged. |
| Frontend | Existing KnowledgeDocument detail, relation/evidence/status sections, router links, single overlay manager | Add a read section in the current detail Main Content; no new route or reader. |
| Persistence indexes | Source and target relation indexes, exact unique edge index, Evidence subject index | Expected first queries have usable leading columns; B01 must measure query plans. |

## Frozen Baseline

TRACE must preserve all of the following:

- `KnowledgeDocument` is the mutable canonical current head; revisions are immutable historical children.
- Raw Markdown is canonical content; rendered Markdown, snippets, trace trees, counts, and matrices are derived.
- Revision History, Compare, Restore-as-new, semantic Save, Published-save confirmation, FTS synchronization, and HumanConfirmation revision snapshots are unchanged.
- `DocumentLifecycleStatus` and `KnowledgeStatus` remain separate axes.
- Evidence and HumanConfirmation never change status automatically.
- `KnowledgeRelation` keeps one canonical directed row with server-enforced endpoint and DocumentType validation.
- Coverage, missing-link, impact, lineage, and trust summaries are read projections only.
- Viewer reads; Editor/Administrator relationship authoring remains the only write path.
- The existing detail route, shared API client, feature boundaries, and single Drawer/Dialog coordinator remain authoritative.

TRACE expressly forbids revision graph nodes, persisted coverage, inverse relation rows, inferred relations, automatic status changes, a generic KnowledgeObject layer, and unrestricted graph traversal.

## Terminology

| Term | Contract meaning |
|---|---|
| Trace node | A current-head Requirement, Specification, or TestCase KnowledgeDocument included by lifecycle rules. |
| Trace edge | One canonical `SpecifiedBy` or `VerifiedBy` KnowledgeRelation row. |
| Structural coverage | Whether the required eligible explicit edge/path exists. |
| Trust context | Independent KnowledgeStatus, Evidence, HumanConfirmation, and revision-confirmation signals. |
| Missing link | A derived absence under the frozen active-lifecycle rules; not a validation error. |
| Test Definition | A TestCase that defines how knowledge is verified. It is not an execution result. |
| Impact context | A bounded, path-explained structured context that may need review; not inferred runtime impact. |
| Lineage | Direct `Supersedes` relations shown separately from structural trace. |
| Active document | A Draft or Published KnowledgeDocument. |
| Archived document | A stable read-only document excluded from current structural coverage but available in direct lineage context. |

## Canonical Truth vs Derived Projection

### Canonical write truth

Only the following existing persisted facts are authoritative:

```text
KnowledgeRelation rows
+ KnowledgeDocument current head
+ DocumentLifecycleStatus
+ KnowledgeStatus
+ Evidence / HumanConfirmation
+ current revision pointers
```

Relationship creation, correction, deletion, Evidence authoring, HumanConfirmation, status transitions, lifecycle transitions, and content saves continue through their existing explicit use cases.

### Derived read truth

TRACE may calculate:

```text
Trace Tree
Structural Coverage
Missing Link
Impact Context Path
Trust Indicator
Count / Summary
Direct Supersedes Lineage
Truncation / Cycle Warning
```

These values are recalculated from canonical current data. They are never editable and are never written back.

### Persistence decision

```text
No Trace entity
No Coverage entity
No TraceEdge entity
No graph database
No materialized graph
No coverage table
No background projector
No new write model
```

## Relationship Semantics

The active wire vocabulary is exactly the current fifteen values:

```text
Calls Reads Writes UsesField AppliesRule PublishesVia ConsumesVia
UsesIntegration DependsOn Documents References AppliesTo SpecifiedBy
VerifiedBy Supersedes
```

TRACE structural traversal uses only `SpecifiedBy` and `VerifiedBy`. Impact context uses the approved finite combinations of `AppliesTo`, `Documents`, `SpecifiedBy`, and `VerifiedBy` defined later. `References` is a citation and does not imply coverage or impact. `Supersedes` is direct lineage and does not imply lifecycle change. The structured MVP relation types are not arbitrary trace edges.

No new RelationType is required. Any future scenario that cannot be expressed must receive its own vocabulary decision before enum, CHECK, API, or frontend changes.

## Requirement → Specification Semantics

The sole canonical row is:

```text
Requirement --SpecifiedBy--> Specification
```

- The Requirement page displays the outgoing meaning `由规格说明定义`.
- The Specification page displays the incoming side of the same row as `定义需求`.
- No inverse `Specifies` value or row is created.
- A direct Requirement → TestCase edge does not replace the need for a Specification.
- Multiple Specifications are legal and each is shown as a distinct branch.
- Exact duplicate rows remain prohibited by the current unique index and Add Relationship conflict behavior.

## Requirement / Specification → TestCase Semantics

The canonical rows are:

```text
Requirement   --VerifiedBy--> TestCase
Specification --VerifiedBy--> TestCase
```

Requirement direct TestCases and Specification-nested TestCases are separate meaningful branches. If the same TestCase is reached both directly and through a Specification, it appears in both path contexts; this is not an exact duplicate edge and must not be silently collapsed into one meaning.

On a TestCase page, incoming `VerifiedBy` rows are displayed as upstream verification definitions. No inverse row or new wire value is saved.

## VerifiedBy Boundary

`VerifiedBy` means only:

> The target TestCase defines how the source Requirement or Specification is verified.

It never means:

```text
test executed
test passed
test failed
verified successfully
TestRun
TestResult
approval
release gate
```

Formal Simplified Chinese UI wording is `测试定义` and `由测试用例定义验证方式`. Missing copy is `缺少测试定义`. The first release must not use `未验证`, `验证失败`, `测试未通过`, or similar execution-result language.

## Coverage Definition

TRACE adopts Option C:

```text
Structural Coverage
+ independent Trust / Confirmation Context
```

An eligible explicit edge counts structurally regardless of whether its KnowledgeStatus is Unknown, Inferred, or Confirmed and regardless of Evidence count. Trust is displayed separately and never changes `covered` to `not covered`.

An edge is eligible for current structural coverage when:

1. it is a valid canonical `SpecifiedBy` or `VerifiedBy` row;
2. its source and target resolve to the required DocumentTypes;
3. the source and target are active (Draft or Published) under the calculation being performed.

Draft links count structurally and are visibly labeled Draft. Published links have no hidden priority and still use the current head. An Archived target does not satisfy current coverage. Archived lineage remains visible only under the explicit lineage rule.

Coverage booleans and counts use all eligible rows, not merely the display-limited first nodes. Result truncation therefore cannot manufacture a false gap.

## Trust / KnowledgeStatus Semantics

Structural trace and trust remain distinct.

Node trust fields:

- document KnowledgeStatus;
- Evidence count;
- HumanConfirmation count;
- current revision number;
- confirmation coverage state: `NoConfirmation`, `LegacyConfirmationUnknown`, `CurrentRevisionConfirmed`, or `ChangedSinceConfirmation`.

Edge trust fields:

- relationship KnowledgeStatus;
- Evidence count;
- HumanConfirmation count.

The projection does not calculate a composite score, weakest-link status, inherited confirmation, or automatic confidence. A Confirmed document connected by an Unknown edge remains exactly that: a Confirmed node and an Unknown edge. `ChangedSinceConfirmation` is a warning beside trust context, not a KnowledgeStatus value.

The first UI always shows lifecycle and document KnowledgeStatus. It shows relationship KnowledgeStatus on each branch and uses compact Evidence/HumanConfirmation counts or indicators. Full Evidence and confirmation details remain in existing detail/drawer surfaces.

## Missing-Link Rules

### Requirement

For an active Requirement:

- `MissingSpecification` when there is no eligible outgoing `SpecifiedBy` edge to an active Specification.
- `hasDirectTestDefinition` when at least one eligible outgoing `VerifiedBy` edge reaches an active TestCase.
- `hasSpecificationTestDefinition` when at least one active Specification branch has at least one eligible outgoing `VerifiedBy` edge to an active TestCase.
- `MissingTestDefinition` when neither direct nor specification-path Test Definition exists.
- each active Specification branch independently receives `MissingTestDefinition` when it has no eligible TestCase.

A Requirement with a direct TestCase but no Specification shows the direct Test Definition and still shows `缺少规格说明`. A Requirement with one tested Specification and one Specification without a TestCase is not globally missing all Test Definitions, but the incomplete Specification branch still shows `缺少测试定义`.

### Specification

For an active Specification, `MissingTestDefinition` means no eligible outgoing `VerifiedBy` edge reaches an active TestCase.

The absence of an upstream Requirement is not a first-release coverage failure because KC-C01 does not define Specification → Requirement as a required creation invariant. The UI may show `暂无上游需求关系` as neutral context, not as a coverage error.

### TestCase

A TestCase has no downstream missing-link calculation in the first release. No incoming Requirement/Specification is neutral `暂无验证对象关系`, not `测试未执行` or `测试无效`.

### Archived root

An Archived root returns `coverageEligibility = ExcludedArchived` and no current missing-link warning. It remains readable with lifecycle and direct lineage context.

## Lifecycle Semantics

| Lifecycle | Included as directly opened root | Counts in current structural coverage | Trace behavior |
|---|---:|---:|---|
| Draft | Yes | Yes | Clearly display `草稿`; use current head. |
| Published | Yes | Yes | Clearly display `已发布`; use current head, not latest-published revision. |
| Archived | Yes | No | Read-only root; suppress current gap claims; excluded as an active child. |

Published receives no ranking or trust advantage. Publication is editorial visibility, not confirmation. Draft is deliberately included because progressive authoring allows useful incomplete knowledge. Archived related edges are excluded from active tree/coverage and summarized as `已归档关联不计入当前覆盖`; their document metadata is not expanded there.

Lifecycle never changes KnowledgeStatus, relationship status, Evidence, or coverage rows because no coverage rows exist.

## Revision Semantics

TRACE always reads:

```text
current KnowledgeDocument head
+ current KnowledgeRelation truth
```

It does not read a graph for revision 1, 2, 3, or any historical snapshot. A document ID is one trace node. `CurrentRevisionNumber` and confirmation-coverage state are trust/display metadata only.

History, Compare, and Restore stay in the existing KnowledgeDocument revision UX. A Trace node may navigate to the existing current document detail, from which the user may explicitly open History. There is no historical trace route, historical edge reconstruction, revision relationship, or revision Evidence.

## Supersedes Semantics

`Supersedes` remains a direct, same-DocumentType, document-level statement:

```text
newer/intended replacement --Supersedes--> older document
```

First-release rules:

- show only direct incoming and outgoing Supersedes relations in a separate `替代关系` lineage area;
- show the other document lifecycle, including Archived;
- mark an active document that has an incoming Supersedes edge as `已有文档声明替代此文档`;
- do not remove an active superseded document from structural coverage solely because of Supersedes;
- do not archive, publish, change status, or edit any document automatically;
- do not recursively expand a superseding chain;
- cap direct lineage display at 20 entries and report truncation;
- detect a direct two-node/self-equivalent cycle defensively and show a warning without changing data.

Longer Supersedes chains and their lifecycle quality belong to later governance. Supersedes never participates in Requirement → Specification → TestCase coverage or impact-context propagation.

## Impact Semantics

Impact means:

> A bounded, explainable structured context that the explicit relation contract indicates may require review when the current document changes.

It is not a causal runtime dependency, transitive blast radius, ownership statement, or guarantee that the target is affected. Each result must show a controlled path kind and human-readable meaning. `AppliesTo` is explicit scope; `Documents` is documented context and must not be relabeled as applicability.

`References` is excluded because citation alone makes no impact claim. `Supersedes` is lineage only. Structured `DependsOn`, `Calls`, `Reads`, and other relations are not followed from a trace document in the first release.

## Allowed Trace Paths

### Structural trace paths

| Root | Canonical path | Result | Maximum relation depth |
|---|---|---|---:|
| Requirement | `Requirement -SpecifiedBy→ Specification` | Defining Specifications | 1 |
| Requirement | `Requirement -VerifiedBy→ TestCase` | Direct Test Definitions | 1 |
| Requirement | `Requirement -SpecifiedBy→ Specification -VerifiedBy→ TestCase` | Specification Test Definitions | 2 |
| Specification | `Specification ←SpecifiedBy- Requirement` | Upstream Requirements | 1 |
| Specification | `Specification -VerifiedBy→ TestCase` | Test Definitions | 1 |
| TestCase | `TestCase ←VerifiedBy- Requirement` | Directly verified Requirements | 1 |
| TestCase | `TestCase ←VerifiedBy- Specification` | Directly verified Specifications | 1 |
| TestCase | `TestCase ←VerifiedBy- Specification ←SpecifiedBy- Requirement` | Requirements upstream of a verified Specification | 2 |

No other structural path is authorized.

### Allowed impact-context paths

| Source | Path | Target | Meaning | Max Depth |
|---|---|---|---|---:|
| Requirement | `AppliesTo` | System / BusinessFunction | Explicit requirement scope | 1 |
| Requirement | `Documents` | System / BusinessFunction / DatabaseObject / BusinessRule / Integration | Target substantially documented by Requirement | 1 |
| Requirement | `SpecifiedBy → Documents` | Same structured target set | Context documented by a defining Specification | 2 |
| Specification | `Documents` | System / BusinessFunction / DatabaseObject / BusinessRule / Integration | Target substantially documented by Specification | 1 |
| Specification | `← SpecifiedBy ← Requirement → AppliesTo` | System / BusinessFunction | Scope inherited only as explained upstream Requirement context | 2 |
| Specification | `← SpecifiedBy ← Requirement → Documents` | Structured target set | Context documented by an upstream Requirement | 2 |
| TestCase | `Documents` | System / BusinessFunction / DatabaseObject / BusinessRule / Integration | Target substantially documented by TestCase | 1 |
| TestCase | `← VerifiedBy ← Requirement → AppliesTo` | System / BusinessFunction | Scope of the directly verified Requirement | 2 |
| TestCase | `← VerifiedBy ← Specification → Documents` | Structured target set | Context documented by the directly verified Specification | 2 |

The arrows in inverse paths describe read direction over the same canonical row; no inverse relation is stored. BusinessFunction, DatabaseObject, BusinessRule, and Integration System context may be returned as endpoint metadata from their existing canonical ownership/party fields; that metadata is not an extra graph hop.

The depth-three path `TestCase ← VerifiedBy ← Specification ← SpecifiedBy ← Requirement → AppliesTo` is deliberately excluded. If users prove it necessary, a later decision may add that exact path with measured query and UX evidence; arbitrary traversal remains prohibited.

## Cycle Handling

The enforced DocumentType matrix makes the structural Requirement → Specification → TestCase path acyclic for valid rows, but every server projection still uses a request-scoped visited set keyed by `(KnowledgeTargetType, Id)` and a path-local relation-ID set.

Required behavior:

- never recurse after the fixed maximum depth;
- stop expansion when a node/relation already exists in the current path;
- keep deterministic nodes already collected;
- return `cycleDetected = true` and a controlled `CycleDetected` warning without target body or arbitrary metadata;
- show a non-blocking UI warning and the bounded partial result;
- never delete, reverse, or correct a business relation automatically.

Because Supersedes is direct-only, it receives cycle detection but no recursive chain traversal.

## Traversal and Result Limits

The API exposes no client-controlled depth.

| Limit | Default | Hard maximum | Behavior at limit |
|---|---:|---:|---|
| Structural trace depth | fixed 2 | 2 | No expansion beyond an allowed path. |
| Impact path depth | fixed 2 | 2 | No unrestricted or caller-selected path. |
| Displayed trace nodes per detail response | up to 200 | 200 | Return deterministic partial tree and `isTruncated`. |
| Displayed trace edges per detail response | up to 300 | 300 | Return deterministic partial tree and `isTruncated`. |
| Direct Supersedes lineage entries | up to 20 | 20 | Return total count plus truncated direct entries. |
| Impact page | 1 | JavaScript-safe positive integer | Invalid page returns `400 validation_error`. |
| Impact page size | 20 | 100 | Invalid page size returns `400 validation_error`. |

Coverage booleans and total counts use separate `EXISTS`/aggregate queries and remain accurate when display nodes are truncated. Truncation returns `200` with `isTruncated`, controlled `truncationReasons`, hard-limit metadata, and any applicable pagination metadata. It is not silently presented as complete and never causes a missing-link flag from an unexamined display slice.

There is no `/api/graph`, arbitrary relation list, `depth` parameter, dynamic path expression, or graph query language.

## Deterministic Ordering

All ordering is explicit and ends with a safe integer tie-breaker.

- DocumentType rank: Requirement, Specification, TestCase.
- Structural branch rank: SpecifiedBy before VerifiedBy; outgoing before incoming only where both appear in one group.
- Document order: existing NOCASE title ordering, then document ID.
- Edge tie-breaker: RelationType rank, target/source ID as appropriate, then relationship ID.
- Missing-link codes: `MissingSpecification`, then `MissingTestDefinition`.
- Lineage: outgoing before incoming, other-document title, other-document ID, relationship ID.
- Impact: explicit scope, explicit documented context, then derived contexts; target type order System, BusinessFunction, DatabaseObject, BusinessRule, Integration; title, target ID, path kind.

Database incidental order and frontend insertion order are never contracts. The backend returns final stable order; the frontend does not re-sort by localized label.

## Authorization

| Actor | Trace detail | Impact context | Relationship authoring |
|---|---:|---:|---:|
| Viewer | Allow | Allow | Deny |
| Editor | Allow | Allow | Existing allow |
| Administrator | Allow | Allow | Existing allow |
| Anonymous | Deny (`401`) | Deny (`401`) | Deny |

The existing authenticated fallback policy remains sufficient; TRACE introduces no permission, ACL, role, Space, or tenant model.

The root must pass the same read rule as its existing detail. All nested targets must pass the same visibility rule before any title, ID, status, count, path, or metadata is returned. The current model grants all authenticated Viewers the same object-read scope. If object-specific visibility is introduced later, TRACE must be amended with a bulk visibility predicate before rollout; it must not infer visibility from frontend routes.

If a canonical relation target is missing or invalid under the current policy, the trace request fails closed with `422 reference_invalid` and no missing target title/body. It must not emit the existing generic related-list placeholder `对象已不存在` as trusted trace data.

## Security / Rendering

- Render titles, controlled labels, statuses, and counts through normal Vue text bindings.
- Return no body Markdown, rendered HTML, Mermaid SVG, relationship description, or Markdown-derived snippet in the first trace responses.
- Do not use `v-html` for Traceability.
- Do not execute arbitrary HTML or treat rendered Markdown/Mermaid as trace data.
- IDs remain JavaScript-safe positive integers; enums remain exact English wire values with Chinese labels in the frontend.
- Use the existing API error contract, cookie authentication, antiforgery on existing writes, and shared API client.
- A read projection never accepts actor, lifecycle, status, relationship, Evidence, or revision mutation fields.

## API Projection Contract

TRACE uses two concrete, page-oriented read endpoints. It does not add a generic graph controller.

### 1. KnowledgeDocument Traceability Detail

```text
GET /api/knowledge-documents/{id}/traceability
```

Purpose: populate the `可追溯性` section for one Requirement, Specification, or TestCase current detail.

Input:

- route `id`: JavaScript-safe positive KnowledgeDocument ID;
- no depth, relation-type, arbitrary target-type, or dynamic path parameters.

Output shape:

```json
{
  "root": {
    "id": 101,
    "documentType": "Requirement",
    "title": "设备状态查询需求",
    "lifecycleStatus": "Draft",
    "knowledgeStatus": "Inferred",
    "currentRevisionNumber": 3,
    "evidenceCount": 2,
    "humanConfirmationCount": 1,
    "confirmationCoverage": {
      "state": "ChangedSinceConfirmation",
      "lastConfirmedRevisionNumber": 2
    }
  },
  "coverage": {
    "eligibility": "Active",
    "hasSpecification": true,
    "hasDirectTestDefinition": false,
    "hasAnyTestDefinition": true,
    "missingLinkCodes": []
  },
  "specifications": [
    {
      "relationship": {
        "id": 501,
        "relationType": "SpecifiedBy",
        "direction": "Outgoing",
        "knowledgeStatus": "Confirmed",
        "evidenceCount": 1,
        "humanConfirmationCount": 1
      },
      "document": { "id": 102, "documentType": "Specification", "title": "设备状态查询规格", "lifecycleStatus": "Published", "knowledgeStatus": "Confirmed" },
      "coverage": { "hasTestDefinition": true, "missingLinkCodes": [] },
      "testCases": []
    }
  ],
  "directTestCases": [],
  "upstreamRequirements": [],
  "lineage": {
    "incoming": [],
    "outgoing": [],
    "total": 0,
    "isTruncated": false
  },
  "cycleDetected": false,
  "isTruncated": false,
  "truncationReasons": [],
  "limits": { "maxDepth": 2, "maxNodes": 200, "maxEdges": 300 }
}
```

The actual response is a discriminated page model by `root.documentType`:

- Requirement uses `specifications` and `directTestCases`;
- Specification uses `upstreamRequirements` and `testCases`;
- TestCase uses `directRequirements` and `upstreamSpecifications`, whose entries may contain immediate upstream Requirements.

It is not a generic `nodes + edges` response and does not require the client to reconstruct graph semantics.

Pagination: none. This one-root page is protected by fixed node/edge/lineage limits and explicit truncation metadata. Structural totals and coverage remain complete.

Authorization: Viewer fallback policy.

Errors:

| Condition | HTTP / code |
|---|---|
| invalid/unsafe ID | `400 validation_error` |
| missing document | `404 not_found` |
| non-Requirement/Specification/TestCase root | `422 business_rule_violation` |
| invalid/missing relation endpoint in selected trace paths | `422 reference_invalid` |
| anonymous/session/identity/access failure | existing `401`/`403` authorization contract |

### 2. KnowledgeDocument Impact Context

```text
GET /api/knowledge-documents/{id}/traceability/impact?page=1&pageSize=20
```

Purpose: populate TRACE-B03's bounded `影响上下文` section using only the approved paths table.

Input: safe route ID plus page/default 1 and pageSize/default 20/max 100. No depth, relation-type, target-type, or path expression is accepted.

Output shape:

```json
{
  "items": [
    {
      "pathKind": "DirectAppliesTo",
      "meaning": "ExplicitRequirementScope",
      "target": {
        "type": "System",
        "id": 12,
        "title": "MES",
        "systemContext": [{ "id": 12, "name": "MES" }]
      },
      "path": [
        { "relationshipId": 601, "relationType": "AppliesTo", "direction": "Outgoing" }
      ]
    }
  ],
  "page": 1,
  "pageSize": 20,
  "total": 1,
  "maxDepth": 2
}
```

`pathKind` and `meaning` are closed response enums defined from the allowed path table; they are not a dynamic graph language. Results with the same target but different meanings/paths remain separately explainable. Exact duplicate paths are distinct-counted by canonical relation IDs and endpoint path key.

Authorization and root/type errors match the Traceability Detail endpoint. Invalid page/pageSize returns `400 validation_error`. A missing/invalid selected endpoint returns `422 reference_invalid` without target metadata.

### Rejected first-release API surfaces

- separate Requirement Summary and Specification Summary endpoints;
- global coverage/matrix endpoint;
- System Trace route or generic System impact graph;
- frontend loops over `/api/relationships` and `/api/relationships/{id}`;
- compare/history-aware trace endpoints;
- arbitrary graph query, filter language, or traversal API.

The detail response already contains the smallest Requirement/Specification/TestCase coverage summary needed by TRACE-B02. Cross-document coverage browsing has no approved first-release UI and remains deferred.

## Database / Query Strategy

```text
Database change required: NO
Migration required: NO
New relationship type required: NO
```

Use a concrete `Traceability` feature with direct `KnowledgeHubDbContext` read projections. Do not add a domain entity, persistence configuration, repository, graph abstraction, or generic resolver framework.

Expected query plan:

1. load the root current document projection;
2. bulk-load all eligible `SpecifiedBy`/`VerifiedBy` relation rows for the fixed root-specific path set using source/target indexes;
3. bulk-load referenced KnowledgeDocuments by ID;
4. bulk-aggregate Evidence/HumanConfirmation counts by `(subject_type, subject_id)` for selected documents and relations;
5. bulk-load direct Supersedes lineage;
6. calculate coverage with database `EXISTS`/counts independent of display caps.

Impact uses fixed branch queries for allowed document and structured target types. A fixed small number of target-type projections or a clear SQL/LINQ union is acceptable; a per-row `ResolveTargetAsync` loop is not.

Current useful indexes:

- unique `(source_type, source_id, target_type, target_id, relation_type)`;
- `(source_type, source_id, relation_type)`;
- `(target_type, target_id, relation_type)`;
- `(relation_type, knowledge_status)`;
- Evidence `(subject_type, subject_id, subject_detail_key)`;
- KnowledgeDocument primary key and `(document_type, lifecycle_status, updated_at)`.

TRACE-A01 adds no index. TRACE-B01 must capture `EXPLAIN QUERY PLAN` for root trace, incoming Specification/TestCase traversal, Evidence aggregation, Supersedes, and the largest allowed impact path using representative fixture sizes. Only a measured scan/hotspot may justify one minimal additive index and focused migration proposal.

## UI Placement Decision

### Options considered

| Option | Fit | Decision |
|---|---|---|
| A — KnowledgeDocument Detail | Matches primary object workflow, existing route/navigation, Main Content width, and current relations/Evidence/status context | **Chosen primary entry point** |
| B — System Unified Knowledge View | Useful as later secondary discovery, but cannot cover cross-system or not-yet-related root documents and would overgrow System Detail | Secondary/deferred only |
| C — New Trace route | Requires route/navigation/UI Inventory/Golden amendment and encourages a broad graph/matrix product before need is proven | Rejected for first release |

### Frozen placement

The primary entry is a `可追溯性` Main Content section inside the existing `/knowledge-documents/{id}` detail for Requirement, Specification, and TestCase only. It appears after the rendered body and before the generic `关联对象` section.

The section is hidden for other DocumentTypes. It does not create a route, navigation item, Context Rail, Drawer, second document reader, or second relationship authoring surface.

Secondary navigation comes from existing document list/search/System Unified View/relationship links into the existing KnowledgeDocument detail. A later compact System Unified View trace summary may be proposed only after B02 pilot evidence; it is not part of the frozen first release.

TRACE-B02 needs a focused UI/Golden acceptance for the new section's density and supported widths, but no Final UI Inventory route amendment is required because no route/overlay type is added.

## Trace Tree UX Contract

- Section title: `可追溯性`.
- Requirement summary uses `规格说明`, `直接测试定义`, and per-Specification `测试定义` groups.
- Specification uses `上游需求` and `测试定义` groups.
- TestCase uses `验证对象` grouped by Requirement and Specification, with immediate Requirement context under Specification when present.
- Node displays Title, DocumentType, lifecycle badge, KnowledgeStatus badge, and compact confirmation/Evidence context.
- Edge displays contextual relation label, relationship KnowledgeStatus, and compact Evidence/HumanConfirmation context.
- Missing indicators use exact text `缺少规格说明` and `缺少测试定义`.
- Draft is always visibly `草稿`; Archived is never silently mixed into active branches.
- Clicking a document title navigates to the existing KnowledgeDocument detail.
- Clicking relationship context may open the existing Relationship Detail Drawer; it does not create a new trace drawer.
- The existing generic `添加关联` action remains the authoring path. Missing indicators do not create, infer, repair, or drag an edge.
- Loading, empty, error, truncation, invalid-reference, and cycle states are explicit.
- The section uses compact nested lists/disclosure, not a node canvas or graph visualization.

## Coverage / Matrix Decision

Required in first release:

- per-root structural coverage summary;
- per-Specification missing Test Definition indicator;
- exact direct-vs-via-Specification path display;
- read-only counts and gaps.

Deferred:

- global coverage table;
- Requirement × Specification or Specification × TestCase matrix;
- editable matrix cells;
- System-wide coverage dashboard;
- cross-document coverage route.

No evidence demonstrates that a matrix is required to complete the primary workflow. A matrix is therefore not part of TRACE-B03. If pilot users later need batch scanning, a separate focused amendment must define filters, pagination, semantics, and placement. It remains read-only and can never become a second Relationship write path.

## Accessibility Boundary

- Use semantic headings and nested lists; do not apply ARIA `tree` unless full tree keyboard behavior is implemented.
- Document navigation uses real links/buttons with accessible names and visible focus.
- Disclosure controls, if used, expose `aria-expanded` and preserve keyboard operation.
- Lifecycle, status, missing, trust, cycle, and truncation meaning is conveyed by text/icon plus accessible label, never color alone.
- Loading and result updates use a restrained live region; errors remain readable and retryable.
- Empty states distinguish no relationship from a missing required link.
- No nested `<main>` landmark is introduced.
- At 1920px the section coexists with the shell; at 1440/1366px it remains Main Content and the existing overlay rules continue to hide/replace Context Rail when a Drawer opens.
- Node title text, relation labels, and status names meet existing contrast and zoom behavior; horizontal scrolling is not required for the Trace Tree.

## Performance Boundary

- Fixed paths and maximum depth only.
- No client N+1 requests.
- No backend per-node database loop where a bulk query is possible.
- No body/Markdown/snippet payload.
- Coverage aggregates remain independent of display truncation.
- Impact is paged and deterministic.
- Use `AsNoTracking` and direct projections.
- Measure actual SQLite query plans and response size in TRACE-B01.
- Focused performance fixtures include fan-out at default page size and at hard caps, plus archived and Supersedes rows.
- No load test, cache, background job, read replica, graph database, or external search service is authorized by A01.

An ordinary trace detail should complete through a small fixed number of bulk queries. B01 must record observed SQL/query plans rather than freezing a speculative millisecond SLA without deployment hardware or a real corpus.

## Architecture Alternatives

| Alternative | Truth duplication | Complexity | Performance/authorization | Testability/rework | Decision |
|---|---|---|---|---|---|
| 1. Derived page-oriented relational projections | None | Small, concrete | Bulk queries and normal policy boundary | Deterministic, matches repository conventions | **Choose** |
| 2. Generic graph service | Risks generic semantics and unrestricted paths | High | Harder to bound and authorize | Broad framework before a second use case | Reject |
| 3. Persisted coverage/read model | Duplicates relation/lifecycle truth and needs synchronization | High | Fast reads only after new write/job failure modes | Migration/rebuild/drift risk | Reject |
| 4. Frontend-only traversal | No persisted duplicate, but business semantics move to client | Medium | Client/backend N+1 and leakage risk | Inconsistent clients and brittle tests | Reject |

## Chosen Architecture

```text
Existing canonical KnowledgeRelation rows
               +
KnowledgeDocument current heads / lifecycle
               +
Evidence / HumanConfirmation / KnowledgeStatus
               ↓
Concrete Traceability Application read queries
               ↓
Bounded page-oriented API projection DTOs
               ↓
Read-only Vue section in existing KnowledgeDocument Detail
```

The backend owns path semantics, coverage calculation, lifecycle filtering, limits, ordering, trust aggregation, authorization, and invalid-reference handling. The frontend owns Chinese labels, compact presentation, navigation, and accessible interaction. Neither layer writes derived trace truth.

## Rejected Alternatives

- `GenericGraphController`, graph query language, GraphQL-like traversal, or `depth=999`.
- generic KnowledgeObject service or universal target repository.
- graph database, materialized graph, event-driven projector, cache, or background coverage rebuild.
- `trace`, `coverage`, or `trace_edge` persistence.
- frontend loops over relation detail endpoints.
- rendered Markdown, Mermaid, FTS, or AI output as trace truth.
- inferred/automatic relations, automatic KnowledgeStatus, or automatic lifecycle changes.
- independent Trace route, graph explorer, canvas, drag-edge creation, or editable matrix.

## TRACE-B01 Contract

Name: `TRACE-B01 — Derived Trace Read Foundation`.

In scope:

- create the concrete backend `Traceability` read feature, contracts, queries, and endpoints defined here;
- implement structural trace detail, coverage/missing rules, lifecycle filtering, trust aggregation, direct Supersedes lineage, limits, ordering, cycle defense, and authorization;
- implement typed frontend API contracts/decoders only as needed for B02 handoff;
- use isolated SQLite fixtures containing direct, indirect, missing, Draft, Published, Archived, trust, duplicate-conflict, cycle-defense, and fan-out scenarios;
- capture query plans and propose a minimal index only if measured evidence requires it.

Out of scope: Vue product surface, impact UI, matrix, new write behavior, vocabulary/schema change by default, and repository-database mutation.

Verification boundary: focused real-SQLite API/application tests, authorization matrix, deterministic contract/order/limit tests, query-plan evidence, build, and no write-model/schema drift.

## TRACE-B02 Contract

Name: `TRACE-B02 — Document Traceability UX`.

In scope:

- add the approved `可追溯性` section to existing Requirement/Specification/TestCase detail;
- implement compact Trace Tree, structural summary, missing states, trust indicators, lineage, loading/empty/error/cycle/truncation states, and existing-detail navigation;
- reuse existing Relationship Detail Drawer for relation inspection;
- complete focused UI/Golden acceptance at 1920, 1440, and 1366 widths.

Out of scope: new route, graph canvas, relationship editing inside the tree, impact context, matrix, System dashboard, or second reader/drawer.

Verification boundary: strict TypeScript, build, focused decoder/component tests for all discriminated root types and states, accessibility checks, and one authenticated Requirement → Specification → TestCase browser read path.

## TRACE-B03 Contract

Name adjusted to `TRACE-B03 — Bounded Impact Context`.

In scope:

- implement the exact paged impact endpoint and allowed path kinds defined here;
- add a compact read-only `影响上下文` subsection on the same KnowledgeDocument detail;
- show explicit path meaning so `AppliesTo` and `Documents` are never conflated;
- verify path bounds, target metadata, ordering, pagination, invalid references, and responsive behavior.

Out of scope: global coverage/matrix page, new route, arbitrary traversal, structured-relation blast radius, search ranking, or write action.

TRACE-B03 remains a separate sub-phase because polymorphic structured-target projection and user-facing impact wording have different performance/semantic risks from the document-only trace tree. A matrix is removed from B03 and deferred.

## PHASE-TRACE Verification Strategy

### Backend gate

- canonical directions and inverse display for all allowed trace paths;
- Requirement → Specification, Requirement → TestCase, Specification → TestCase, and the two-level chain;
- direct and via-Specification TestCase shown as distinct path meanings;
- Requirement and Specification missing-link rules;
- exact duplicate Add Relationship conflict and distinct path counting;
- Draft/Published inclusion, Archived exclusion, Archived-root behavior;
- current-head and confirmation-coverage projection; no revision nodes;
- document/relationship KnowledgeStatus and Evidence/HumanConfirmation aggregation without composite trust;
- direct Supersedes display, no recursion, no lifecycle side effect;
- allowed impact paths only; References/Supersedes/structured arbitrary traversal excluded;
- cycle defense, fixed depth, node/edge caps, truncation, pagination, and total/count correctness;
- deterministic ordering independent of insertion order;
- Viewer/Editor/Administrator read, anonymous deny, existing relationship write policy unchanged;
- invalid/missing endpoint fails closed without target metadata;
- bulk-query/query-plan evidence and no per-node resolver loop;
- no schema/write/canonical-data change.

### Frontend gate

- all three root DocumentTypes;
- trace groups, direct/indirect paths, missing states, Draft/Published labels, archived/lineage context;
- document and relationship trust signals without coverage conflation;
- existing detail and relationship-drawer navigation;
- loading, empty, error, cycle, invalid-reference, and truncation states;
- text-only safe rendering and no body/snippet HTML;
- keyboard/focus, accessible names, non-color status, zoom, 1920/1440/1366 response;
- no new route, second drawer, matrix edit, or authoring path.

### Runtime gate

Use an isolated database, not the repository application database:

```text
Login as Editor
→ create/use Requirement
→ create/use Specification
→ create/use TestCase
→ link Requirement SpecifiedBy Specification
→ link Specification VerifiedBy TestCase
→ optionally link Requirement VerifiedBy TestCase
→ open Requirement detail Traceability
→ verify Requirement → Specification → TestCase
→ remove the Specification → TestCase relation through existing authoring
→ verify 缺少测试定义 appears on that Specification branch
→ restore the relation through existing authoring
→ verify the gap disappears
→ navigate to Specification and TestCase details
→ verify Viewer can read the same projection but cannot author relations
→ verify current KnowledgeStatus/lifecycle/Evidence do not change automatically
```

The runtime gate also adds a direct Requirement `AppliesTo` System or BusinessFunction path for TRACE-B03. Every agent-started server/browser process is stopped and ports released immediately after verification.

### Product acceptance gate

Before PHASE-TRACE final PASS, a Product owner validates one real domain chain and confirms that the detail-first Trace Tree answers the primary workflow without requiring a global matrix. Lack of this real sample prevents a product-acceptance PASS but does not invalidate B01/B02 technical verification.

## Risks

| Risk | Decision response |
|---|---|
| No real trace corpus or telemetry | Require one real domain chain before final Product PASS; keep first release detail-first and small. |
| Coverage and trust are conflated | Freeze Option C and independent response fields/copy. |
| `VerifiedBy` implies executed/passed testing | Freeze Test Definition wording and explicitly exclude TestRun/results. |
| Archived targets create false coverage | Exclude from current coverage; preserve direct lineage separately. |
| Supersedes silently changes currentness | Keep lifecycle independent and direct-only marker. |
| Fan-out or malformed cycles overload queries/UI | Fixed paths/depth, visited sets, caps, deterministic truncation. |
| Existing generic relation reads cause N+1 | New bulk page projection; no per-row resolver loop. |
| Coverage display becomes second write path | Read-only section; authoring stays in existing Add Relationship Drawer. |
| Impact is interpreted as blast radius | Controlled path kinds and explicit `scope` versus `documented context` labels. |
| New route expands frozen UI | Use existing KnowledgeDocument detail; matrix/global route deferred. |
| Hidden/missing target leaks metadata | Reuse visibility checks and fail closed on invalid selected endpoints. |
| Premature index/schema work | Measure query plans in B01; no A01 migration. |

## Deferred Scope

- global coverage table or matrix;
- compact System Unified View trace summary pending B02 pilot evidence;
- additional impact paths, including depth-three TestCase → Specification → Requirement → System;
- recursive Supersedes lineage and lifecycle governance;
- object-specific ACL/Space-aware trace filtering, if such a permission model is later approved;
- coverage trend/history, governance, review dates, owners, and staleness;
- trace-aware search ranking or discovery filters;
- attachments, cited attachment context, semantic retrieval, and AI/RAG.

Every deferred capability requires a focused product/architecture decision where it changes semantics, UI placement, authorization, persistence, or API shape.

## Out of Scope

- TRACE-B01/B02/B03 implementation in this task;
- any source, test, route, CSS, migration, package, database, seed, or runtime change;
- historical revision graph, historical relation reconstruction, persisted diff, or rendered Markdown truth;
- TestPlan, TestRun, TestResult, pass/fail, release gate, approval, or test execution;
- inferred, AI-proposed, or automatically repaired relationships;
- automatic KnowledgeStatus or lifecycle transition;
- graph database, generic graph service, arbitrary traversal, node canvas, or infinite map;
- persisted coverage, editable matrix, cell-created relationship, or drag-edge authoring;
- new relationship types or endpoint types;
- Spaces/page tree, comments, notifications, collaboration, Incident/Problem, import/export;
- production deployment, observability, backup/recovery, or unrelated REV Low-gap cleanup.

## Compatibility with Frozen Architecture

| Frozen area | Compatibility result |
|---|---|
| KnowledgeDocument current head | TRACE reads it directly; no competing document state. |
| Raw Markdown canonical storage | TRACE returns no body/rendered content and changes no storage. |
| Revision History/Compare/Restore | No revision node/API change; navigation remains existing UX. |
| Semantic/Published Save | No save contract or pointer change. |
| HumanConfirmation revision snapshot | Reused as trust context only. |
| Current-head FTS | Unchanged; TRACE is not a search index. |
| KnowledgeStatus progression | Independent display; no automatic transition. |
| Relationship direction/vocabulary | Uses current canonical rows and fifteen-value closed enum. |
| Endpoint/type validation | Assumed and revalidated; invalid selected rows fail closed. |
| Authorization | Existing Viewer fallback and Editor writes retained. |
| UI shell/routes/overlays | Existing detail Main Content and single overlay coordinator retained. |
| Persistence architecture | Direct DbContext projections; no repository/graph/read-model persistence. |

No conflict with the frozen MVP specifications, KC-C01/C02, REV-A01, or current implemented contracts was found.

## Open Questions

Blocking questions: **NONE**.

Non-blocking validation items owned by future sub-phases:

1. TRACE-B02 freezes final Chinese helper copy, compact spacing, and accepted Golden screenshots without changing the semantics above.
2. TRACE-B01 measures whether any minimal additive index is justified; the default remains none.
3. The Product acceptance gate supplies one real Requirement/Specification/TestCase/System-or-BusinessFunction example and confirms whether a later batch matrix has proven value.
4. A System Unified View trace summary remains deferred unless B02 pilot use demonstrates a clear secondary workflow.

These items do not change the chosen write truth, path semantics, API direction, primary UI placement, or lifecycle/coverage contract.

## Final Decision

```text
TRACE-A01 APPROVED

Chosen Architecture:
Derived page-oriented relational projections over canonical current truth

Primary TRACE Entry Point:
Existing KnowledgeDocument Detail Main Content

Coverage Semantics:
Structural Coverage separated from Trust / Confirmation Context

Lifecycle Semantics:
Draft and Published participate using current heads; Archived is excluded from
current coverage and appears only as an explicitly labeled root/lineage context

Revision Semantics:
Current head + current relationship truth; no historical revision graph

Impact Boundary:
Only the frozen AppliesTo/Documents paths with SpecifiedBy/VerifiedBy at max depth 2

Database Change Required: NO
New Relationship Types Required: NO
Blocking Human Decisions: NONE

Next permitted task after human acceptance of this decision:
TRACE-B01 — Derived Trace Read Foundation
```

Stop after this decision. Do not start TRACE-B01, TRACE-B02, TRACE-B03, or PHASE-TRACE-VERIFY automatically.
