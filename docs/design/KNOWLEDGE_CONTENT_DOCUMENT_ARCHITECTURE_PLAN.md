# KNOWLEDGE-CONTENT-A01 — Knowledge Content / Document Capability Architecture Plan

> Product: 系统知识中心 / System Knowledge Hub
>
> Deliverable type: Architecture and product planning only
>
> Status: Proposed — requires Architecture Gate approval before implementation
>
> Planning baseline date: 2026-08-22

## 1. Executive Decision

System Knowledge Hub should add a controlled long-form knowledge capability, but it should not become a general-purpose Confluence clone. The target product model is:

```text
Structured Knowledge
+ KnowledgeDocument
+ explicit KnowledgeRelation
+ Evidence / HumanConfirmation
+ Unified Knowledge View
```

The recommended domain name is **`KnowledgeDocument`**. The user-facing navigation label is **“知识内容”**. A single document aggregate supports controlled `DocumentType` values such as Requirement, Specification, TestCase, SOP, Troubleshooting, KnowledgeArticle, and DesignNote. The target architecture is a staged form of **Option C: generic document core with typed extensions only when a proven query or invariant requires them**. Content MVP implements the core only; it does not create generic metadata, EAV, dynamic schemas, or one table/Feature per document type.

Explicit answers:

| Question | Decision |
|---|---|
| Add Confluence-like knowledge content capability? | **YES**, limited to authored long-form knowledge, typed templates, search, relations, evidence, status, and normal access control. **NO** to spaces, page trees, comments, real-time collaboration, plugins, and a Confluence clone in the first phases. |
| Separate Features for Requirement, Specification, SOP, Troubleshooting, and TestCase? | **NO**. They are controlled document types in one `KnowledgeDocuments` Feature. A typed extension is added later only if a type gains non-document invariants or query needs. |
| Retain existing structured knowledge? | **YES**. Systems, Business Functions, Database Objects/Columns, Business Rules, Integrations, Relationships, Evidence, HumanConfirmation, and UnknownItems remain canonical structured knowledge. |
| Add a Unified Knowledge View? | **YES**, as read projections on existing object detail pages, not as a new generic aggregate or physical `SystemFullKnowledge` model. |
| Require version history in Content MVP? | **NO**. Content MVP uses the existing opaque concurrency pattern, explicit save, dirty-state warning, and archive semantics. Revision history is the next capability after MVP validation, before broad authoring rollout. |
| Canonical content storage | **Markdown-first** (`body_markdown` as canonical text). Rendered HTML and search chunks are derived, never domain truth. |
| Vue editor | **Milkdown with Vue 3**, initially a deliberately restricted CommonMark/GFM-oriented feature set. It is a Markdown editor, not a generic page builder. |

This plan does not change SEC01–SEC03, does not continue blocked SEC04 deployment work, and does not modify authentication or authorization. The current Viewer / Editor / Administrator access model is sufficient for the first document capability.

## 2. Current Capability Inventory

The inventory below is based on the current repository, current migrations and contracts, and the approved verification reports. “Relate” means participation in the canonical `knowledge_relations` model; UnknownItem target links are called out separately.

| Capability | Current implementation | Structured / Unstructured | Create / edit / search / relate | Evidence support | KnowledgeStatus | Long-form suitability | Main limitation |
|---|---|---|---|---|---|---|---|
| Dashboard | Page-oriented aggregate query and Vue dashboard | Structured projection | Read only; no direct entity relation | Indirect counts | Aggregates existing statuses | Poor | Does not contain or author knowledge content. |
| System | Concrete EF entity, list/detail, inline overview edit | Structured | Yes / yes / yes / yes | Yes | Yes | Poor | Short fields and summaries cannot hold architecture, requirements, or operational guidance. |
| Business Function | Concrete entity, list/detail, inline overview edit | Structured | Yes / yes / yes / yes | Yes | Yes | Poor to partial | Workflow/description fields are not a durable long-form specification. |
| Database Source | Concrete child of System | Structured | Limited create/read; not a first-class global route or search type | Via supported subjects only where exposed | Not a primary status target | Poor | Metadata container, not authored content. |
| Database Object | Concrete entity and page-oriented detail | Structured | Yes / yes / yes / yes | Yes | Yes | Poor | Excellent catalog object; unsuitable for manuals or narrative design. |
| Database Column | Concrete entity, drawer editing | Structured | Yes / yes / yes / yes | Yes | Yes | Poor | Captures field semantics and values, not long-form explanations. |
| Business Rule | Concrete entity with condition/result/input data | Structured | Yes / yes / yes / yes | Yes | Yes | Partial | Represents an explicit rule but not a full policy/specification document or extensive rationale. |
| Integration | Concrete entity with parties, flow, endpoint, fields | Structured | Yes / yes / yes / yes | Yes | Yes | Partial | Strong technical metadata; weak for protocols, examples, runbooks, and narrative design. |
| Knowledge Relationship | Directed, typed edge with controlled endpoint policy and version | Structured | Create/detail/update; not global-search content | Yes, as a relationship subject | Yes | Not applicable | Closed endpoint/type matrix; no document endpoint. |
| Evidence | Evidence entity with subject, source locator, provider snapshot, detail drawer | Semi-structured | Create/update/detail; not globally searchable | It is the evidence model | No automatic status change | Poor as authored content | `ExistingDocument` records why knowledge is believed; it does not store an internal document body. |
| HumanConfirmation | `EvidenceType.HumanConfirmation`, canonical current-user snapshot | Semi-structured | Create/detail and existing correction path | Native | Never auto-advances status | Poor | Confirmation fact, not a document or editorial workflow. |
| UnknownItem | Explicit investigation, findings, resolution, proposed/applied knowledge updates, activity | Structured workflow | Yes / workflow edits / yes / target links, not canonical knowledge edges | Yes | Own workflow status, not KnowledgeStatus | Partial for investigation only | Correct for unresolved knowledge; not an incident system, SOP library, or durable article store. |
| User / KnowledgeRole | Canonical user persistence and knowledge identity; security adds access level/login identity | Structured administration | Admin management; user list search only | No | No | Not applicable | Identity and access context, not knowledge content. KnowledgeRole is not permission. |
| Global Search | One endpoint; grouped SQLite `LIKE` queries over 7 object types | Structured projection | Search only | No | Displays status where applicable | Poor for bodies | No KnowledgeDocument type, no full-text index, no heading-aware snippets, no content-type/lifecycle fields. |
| Application shell | Left navigation, global search, main content, context rail, single drawer/dialog host | UI architecture | Navigation and authoring shell | Indirect | Indirect | Partial | No knowledge-content route. A drawer is too narrow for long-form editing. |

Current frontend facts relevant to this plan:

- Vue 3, strict TypeScript, Vue Router, Pinia, Element Plus, and the shared native-fetch API client are established.
- No rich-text or Markdown editor package is currently installed.
- The left navigation currently has Dashboard, Systems, Business Functions, Database, Unknown Items, and Administrator-only User Management.
- The global search decoder has a closed seven-value `SearchObjectType` union.
- `actorStore` already exposes canonical current user, `canEdit`, and `isAdministrator`; no second user/access store is needed.
- Long-form editing should use a dedicated main-content page. It should not add another Drawer manager or stack drawers.

## 3. Current System Limitations

The system can accurately describe *what objects exist*, *how they relate*, *what is unknown*, and *why a fact is believed*. It cannot yet store a durable, readable explanation of *how a system works*, *what was required*, *how to test it*, or *how to operate and troubleshoot it*.

### Well Supported Today

- System inventory and ownership context.
- Business Function catalog and structured flow summaries.
- Database objects, columns, semantics, known values, and usage relationships.
- Business Rules with explicit conditions/results.
- Integration parties, direction, endpoint metadata, and fields.
- Explicit typed relationships among supported structured targets.
- Evidence and canonical HumanConfirmation snapshots.
- Unknown knowledge investigation and explicit application of conclusions.
- KnowledgeStatus progression with evidence gates and no automatic status changes.
- Canonical users, knowledge identities, OIDC-backed current user, and Viewer/Editor/Administrator access.

### Partially Supported Today

- **Business logic explanation:** BusinessRule and BusinessFunction fields capture concise semantics, but not multi-section rationale, examples, edge cases, decision history, or diagrams-as-code.
- **Integration specification:** endpoints and fields are structured, but payload examples, retry behavior, error handling, and operational notes have no long-form home.
- **Architecture knowledge:** System notes, dependencies, code/repository summaries, and relations provide fragments, not a coherent architecture note.
- **Problem investigation:** UnknownItem is strong while knowledge remains unknown, but it is intentionally not a durable troubleshooting manual or incident/problem management suite.
- **Existing document references:** Evidence can point to an external document, but the product cannot author, search, relate, or govern that document as internal knowledge.

### Not Properly Supported Yet

- Requirements.
- Functional and technical specifications.
- Test cases as durable knowledge (without test execution management).
- SOPs and operational procedures.
- Troubleshooting guides.
- General Knowledge Articles.
- Architecture and design notes.
- Reusable document templates.
- Search across long-form body content.
- A unified system/function view combining structured objects with authored documents.

Trying to place these in existing description, note, Evidence, or UnknownItem fields would blur domain semantics and create unsearchable, unversionable text blobs. A new capability is justified.

## 4. Target Product Model

The target is not “documents beside the knowledge model.” Documents become one explicit kind of knowledge target while structured objects remain first-class.

```text
System / BusinessFunction / DatabaseObject / Column / Rule / Integration
                ↑              ↑
                └── KnowledgeRelation ── KnowledgeDocument
                                             │
                          ┌──────────────────┼─────────────────┐
                          │                  │                 │
                    KnowledgeStatus      Evidence       HumanConfirmation
                          │
                 Draft / Published / Archived
                 (separate lifecycle axis)
```

Product principles:

1. Structured data remains canonical where stable fields, filtering, validation, and direct relationships matter.
2. `KnowledgeDocument` holds narrative knowledge whose natural shape is headings, paragraphs, lists, tables, code, links, and later images.
3. Relationships connect documents to structured objects and to other documents; body text never creates a relationship implicitly.
4. Evidence answers “why believe this document or section?” and HumanConfirmation records who confirmed it.
5. KnowledgeStatus remains epistemic confidence. Document lifecycle remains editorial availability.
6. Unified views are read models over existing entities and relationships, never copied “full knowledge” tables.
7. Search indexes and future AI chunks are derived and rebuildable from canonical Markdown and structured entities.

## 5. Structured Knowledge vs Documents

Use the following boundary test before adding a field or content type:

| Question | Prefer structured entity/field | Prefer KnowledgeDocument |
|---|---|---|
| Must the value be filtered, sorted, validated, or joined reliably? | Yes | Only if a small core metadata field also exists. |
| Does it have a stable cardinality/identifier/FK? | Yes | Reference it through an explicit relationship. |
| Is it primarily narrative, explanatory, procedural, or illustrative? | No | Yes. |
| Does it need headings, lists, tables, code blocks, links, or images? | Rarely | Yes. |
| Does it have an independent business workflow beyond authoring? | A dedicated Feature may be justified. | Keep as a document only while its workflow is editorial. |
| Is it evidence rather than knowledge being asserted? | Use Evidence. | A document may be the evidence source or evidence subject, but does not replace Evidence. |
| Is it still an unresolved question? | Use UnknownItem. | Publish the durable answer later and relate it to the UnknownItem. |

Examples:

- Integration URL, direction, queue, and parties remain structured; retry strategy and payload examples belong in a Specification.
- Column name/type/nullability remain structured; data-quality investigation guidance belongs in Troubleshooting or SOP.
- A BusinessRule condition/result remains structured; its rationale and worked examples may be a related KnowledgeArticle or Specification.
- A missing fact starts as UnknownItem; the confirmed explanation may later become a document without deleting the investigation history.

## 6. KnowledgeDocument Domain Proposal

### Naming analysis

| Candidate | Assessment |
|---|---|
| `KnowledgeDocument` | **Recommended.** Concrete, discoverable, distinct from body content, and clear beside EvidenceType `ExistingDocument`. |
| `KnowledgeContent` | Useful product capability name, but ambiguous as an entity: it could mean the body, the entire knowledge graph, or any existing structured object. |
| `Document` | Too generic in a feature-rich application and conflicts conceptually with external documents used as Evidence. |
| `KnowledgeArticle` | Good controlled type, but too narrow for Requirement, Specification, TestCase, and SOP. |
| `KnowledgePage` | Good UI metaphor, but implies a page-tree/wiki architecture that is explicitly out of scope. |
| `ContentItem` | Abstract and framework-like; weak domain language. |

### Target aggregate

`KnowledgeDocument` is one aggregate with:

- Identity: `Id`.
- Classification: immutable `DocumentType` after first publish (change permitted while Draft only if implementation remains simple).
- Core content: `Title`, optional `Summary`, `BodyMarkdown`.
- Editorial lifecycle: `Draft`, `Published`, `Archived`.
- Knowledge confidence: existing `Unknown`, `Inferred`, `Confirmed` semantics and status metadata.
- Author/update attribution using canonical User references and display snapshots, local to this Feature; no generic audit framework.
- App-managed integer version exposed only as opaque `concurrencyToken`.
- Timestamps: created, updated, optional published, optional archived.

The aggregate does not contain navigation collections to Systems, Evidence, Users, or KnowledgeRoles. Application queries project those through explicit queries, preserving the current direct-DbContext, feature-first style.

### Architecture alternatives

| Criterion | A. Separate Feature per document type | B. Generic Document + ContentType | C. Document core + typed extensions |
|---|---|---|---|
| Domain clarity | High only if every type has truly different invariants; otherwise repetitive | Clear for shared authoring | **High:** one authored-content concept, with explicit typed additions when justified |
| Extensibility | Requires routes/tables/services for every type | Easy to add enum values, but risks generic metadata JSON | **Controlled:** enum/type first; typed table/code only for proven needs |
| Queryability | Strong per type, difficult cross-type list/search | Strong for core, weak for arbitrary metadata | **Strong core and strong typed queries where needed** |
| Validation | Precise but duplicated | Core validation only; dynamic validation tempts a schema engine | **Core invariants plus ordinary type-specific code** |
| Editor reuse | Duplicated wrappers/pages | Excellent | **Excellent** |
| Search | Requires union across stores | Simple | **Simple for core; extension fields can join/index later** |
| Relationships | Many endpoint types | One `KnowledgeDocument` endpoint | **One endpoint; no relationship explosion** |
| Versioning | Repeated per type | One revision model | **One revision core; extension snapshots only if later required** |
| Permissions | Repeated policies | One policy boundary | **One policy boundary** |
| Initial implementation cost | Highest | Lowest | Slightly above B conceptually; **same as B in Content MVP** |
| Migration risk | Many tables and CHECK changes | Low, unless metadata becomes ungoverned | **Low when staged; typed migrations are explicit** |
| Main failure mode | Feature/table proliferation | EAV/dynamic-form drift | Typed extensions added prematurely |

**Recommendation: C, staged.** Content MVP physically implements only the common document table. It must not add a `metadata_json`, key/value metadata table, dynamic schema, or typed extension tables. A future typed extension is approved only when a document type needs at least one server-enforced invariant or commonly filtered field that cannot responsibly stay in Markdown.

## 7. Content Types

Use a code enum persisted as text with a database CHECK, following current conventions. Content types are not database-managed taxonomy rows in the first phase.

| Wire value | Chinese label | Purpose | Content MVP |
|---|---|---|---|
| `Requirement` | 需求 | Business/user/system need and acceptance intent | Yes |
| `Specification` | 规格说明 | Functional or technical design, protocol, constraints | Yes |
| `TestCase` | 测试用例 | Repeatable verification intent and expected result, without execution management | Yes |
| `Sop` | 标准作业程序 | Repeatable operational procedure | Yes |
| `Troubleshooting` | 故障排查 | Symptoms, diagnosis, resolution, prevention | Yes |
| `KnowledgeArticle` | 知识文章 | General explanatory or reference content | Yes; default generic type |
| `DesignNote` | 设计说明 | Architecture decision context and design notes | Yes |

Do not add `Other` in Content MVP. `KnowledgeArticle` is the explicit general-purpose type. Adding a new type is a small code/schema decision, not an administrator-created runtime value.

Types influence:

- Label and icon.
- Initial Markdown template.
- Allowed/recommended relationship labels.
- List/search filters.
- Optional type-specific validation implemented in ordinary code later.

Types do not create different storage formats, routers, generic forms, permission schemes, or workflow engines.

## 8. Requirement

Requirement is a document type, not a Jira issue model.

Content MVP template:

```markdown
# 背景

## 目标

## 范围

## 需求说明

## 验收标准

## 非目标
```

Core metadata remains only type, title, summary, lifecycle, KnowledgeStatus, authorship, timestamps, and concurrency. Priority, assignee, sprint, estimates, work-item state, dependency scheduling, and approval chains are not added.

Relationship guidance:

- Requirement `AppliesTo` System or BusinessFunction.
- Requirement `SpecifiedBy` Specification.
- Requirement `VerifiedBy` TestCase.
- Requirement may `References` another document or structured object.

If later users need server-side queries such as “all approved high-priority requirements,” that is evidence for a small typed `RequirementDetails` extension. It is not evidence for a dynamic metadata engine.

## 9. Specification

Specification stores functional or technical design that is too extensive for structured summary fields.

Suggested template:

```markdown
# Context

## Goals and Non-goals

## Proposed Behavior

## Data / API Contract

## Error and Edge Cases

## Operational Considerations

## Verification
```

Use explicit relationships to the System, BusinessFunction, Integration, DatabaseObject, BusinessRule, or Requirement being specified. Do not copy structured endpoint/column/rule metadata into the document as a second source of truth. The document may explain or link to it.

Specifications do not introduce a generic review/approval workflow in Content MVP. Publication means editorial visibility, not regulatory approval.

## 10. Test Case

TestCase is durable verification knowledge, not a test-management product.

Suggested template:

```markdown
# Objective

## Preconditions

## Test Data

## Steps

1. ...

## Expected Result

## Notes / Evidence Guidance
```

Content MVP supports authoring, search, relation, evidence, HumanConfirmation, lifecycle, and KnowledgeStatus. It explicitly does not support:

- test runs or execution history;
- pass/fail dashboards;
- environment matrices;
- step-level structured results;
- defect linkage workflows;
- automation orchestration;
- test suites/plans as a hierarchy.

If execution management becomes a confirmed product need, it should be designed as a separate future Feature that references KnowledgeDocument TestCases. The document itself remains the durable test definition.

## 11. SOP

SOP captures repeatable actions, boundaries, prerequisites, verification, and rollback/escalation guidance.

Suggested template:

```markdown
# Purpose

## Preconditions

## Safety / Access Notes

## Procedure

1. ...

## Verification

## Rollback or Escalation
```

Use ordinary Markdown ordered lists, code blocks, tables, links, and supported callout syntax. Do not model each step as a database row in Content MVP. A structured step engine would imply execution, branching, approvals, assignments, and run history—an out-of-scope workflow product.

SOPs normally `ApplyTo` a System, BusinessFunction, DatabaseObject, or Integration. Evidence can support the whole SOP or a stable heading identified by `subjectDetailKey`.

## 12. Troubleshooting

Troubleshooting is a reusable diagnostic guide, not an incident ticket.

Suggested template:

```markdown
# Symptoms

## Impact

## Likely Causes

## Diagnosis

## Resolution

## Verification

## Prevention
```

Error codes, log fragments, and commands remain searchable body content in Content MVP. Do not add arbitrary metadata fields for them yet. If operational use proves that exact error-code filtering is essential, add a specific typed extension or controlled field later.

UnknownItem remains the correct place for an unresolved knowledge question or investigation. After resolution, an Editor may create or update a Troubleshooting document and relate it to the affected structured objects and the originating UnknownItem. Content MVP does not introduce Incident or Problem entities. A future `ProblemRecord` document type should be added only after evidence shows that a durable post-incident narrative is distinct from UnknownItem and Troubleshooting.

## 13. Knowledge Article

KnowledgeArticle is the default explanatory content type for concepts, FAQs, reference notes, onboarding material, data interpretation, and other durable knowledge that does not fit the more specific types.

Suggested template:

```markdown
# Summary

## Context

## Details

## Examples

## Related Knowledge
```

It must not become an escape hatch that duplicates structured fields. The authoring UI should encourage an explicit relationship to relevant Systems, BusinessFunctions, Database objects, Rules, or Integrations, while allowing truly cross-system articles.

DesignNote is a sibling controlled type for architecture/design rationale. It does not replace ADR files in the source repository; repository ADRs can be referenced as Evidence or linked from a DesignNote.

## 14. Content Lifecycle

Document lifecycle is an editorial axis:

```text
Draft ── Publish ──> Published ── Archive ──> Archived
  ^                       │
  └──── ReturnToDraft <───┘
```

Recommended semantics:

| Lifecycle | Read behavior | Edit behavior | Search/list behavior |
|---|---|---|---|
| `Draft` | Viewer can read in the initial simple access model; clearly labeled as draft | Editor/Administrator can edit | Included with visible Draft label and filter |
| `Published` | Normal readable content | Editor/Administrator can edit; changes do not imply confirmation | Default list/search result |
| `Archived` | Read-only by default; retained at stable ID | Restore action returns it to Draft | Excluded from default lists/search unless explicitly included |

There is no physical delete endpoint. Archive is the removal mechanism. No approval queue, reviewer assignment, scheduled publishing, or multi-stage workflow is introduced.

Publication rules in Content MVP should be intentionally small: non-empty title, supported type, and non-empty meaningful body. Publication does not require KnowledgeStatus `Confirmed`, Evidence, or HumanConfirmation. Those are knowledge-confidence concerns.

## 15. KnowledgeStatus Integration

Every KnowledgeDocument participates in the canonical progression:

```text
Unknown → Inferred → Confirmed
```

Lifecycle and KnowledgeStatus are independent:

| Example | Lifecycle | KnowledgeStatus | Meaning |
|---|---|---|---|
| Early design draft | Draft | Unknown | Authored but not yet supported. |
| Published working hypothesis | Published | Inferred | Visible and evidence-supported, not human-confirmed. |
| Confirmed runbook | Published | Confirmed | Visible and explicitly confirmed. |
| Superseded confirmed spec | Archived | Confirmed | No longer current, but historically confirmed. |

The existing KnowledgeStatus use case should be extended with `KnowledgeDocument` as a supported target. It retains existing evidence gates, explicit user action, current actor attribution, and opaque concurrency. Create, edit, publish, archive, Evidence save, relationship save, and HumanConfirmation save must never automatically change KnowledgeStatus.

Content lifecycle transitions must also use the document concurrency token; they do not introduce a second token.

## 16. Evidence / HumanConfirmation Integration

Reuse the existing Evidence feature and C25 HumanConfirmation path.

Required extension:

- Add `KnowledgeDocument` to the permitted Evidence subject types and subject resolver.
- Allow object-level Evidence with `subjectDetailKey = null`.
- Allow section-level Evidence using a stable heading anchor in `subjectDetailKey`, for example `Heading:rollback-and-escalation`. Section targeting is optional UX in Content MVP; the contract must not encode editor node IDs.
- HumanConfirmation for a document uses the current principal-backed Current User, canonical User/KnowledgeRole resolution, immutable provider snapshot, and existing error behavior.
- Adding Evidence or HumanConfirmation does not advance document KnowledgeStatus.

An internal KnowledgeDocument may also support a structured object as evidence. Do not duplicate it as a new `DocumentEvidence` entity. The preferred staged approach is:

1. Content MVP: use an explicit KnowledgeRelation for navigation/context and an Evidence record for the actual claim, using current `ExistingDocument` locator semantics where adequate.
2. Only if a canonical internal document FK is required for integrity/querying, add one nullable `source_knowledge_document_id` to Evidence in a later focused amendment. Do not hide a document ID in an external-key convention.

Historical Evidence snapshots remain historical facts. Editing or archiving a document does not rewrite Evidence or HumanConfirmation records.

## 17. Relationship Architecture

Reuse `knowledge_relations`; do not create `document_relationships` or a parallel graph framework.

### Endpoint extension

Add `KnowledgeDocument` as a `KnowledgeTargetType` for relationship purposes. Add `UnknownItem` only to the relationship endpoint resolver if the first implementation slice includes “investigation produced this document.” Because the same enum is also referenced by UnknownItem targets/updates, each use case must retain an explicit allowed-type list; adding an enum value must not make KnowledgeDocument or UnknownItem valid in unrelated update operations.

### Minimal new relationship vocabulary

| RelationType | Canonical direction | Allowed use |
|---|---|---|
| `Documents` | KnowledgeDocument → structured target | General document explains the target. In target UI, inverse label is “文档说明”. |
| `AppliesTo` | KnowledgeDocument → structured target | Procedure/troubleshooting/requirement applies to the target. |
| `SpecifiedBy` | Requirement → Specification | Requirement is elaborated by a specification. |
| `VerifiedBy` | Requirement or Specification → TestCase | Verification definition for the source document. |
| `References` | KnowledgeDocument → document or structured target | Deliberate weak citation/reference when a stronger relation is not correct. |
| `Supersedes` | KnowledgeDocument → KnowledgeDocument | New document supersedes an older document; does not archive automatically. |
| `DerivedFrom` | KnowledgeDocument → UnknownItem | Durable content resulted from an investigation; include only when UnknownItem endpoints are implemented. |

Do not add `RelatedTo` in the first phase. It encourages meaningless edges. Do not infer relationships from Markdown links, titles, tags, AI output, or search clicks.

Relationship rules remain in the existing explicit endpoint policy. They should validate allowed source document types where the distinction matters (`SpecifiedBy`, `VerifiedBy`, `DerivedFrom`) and reject self-relations and duplicate edges. `Supersedes` should reject self-reference; cycle detection beyond that is not required in Content MVP because archive/currentness remains explicit.

### Graph behavior

Existing relationship graph projections may include KnowledgeDocument nodes after the basic content slice, but the initial UI should prefer grouped related-content lists. Rendering hundreds of document nodes would reduce graph value. The graph is a view of explicit relations, never a separately persisted graph.

## 18. Unified Knowledge View

Unified Knowledge View is a product view, not an aggregate, entity, table, generic repository, or replacement router.

The first implementation should extend existing System and BusinessFunction detail projections with related documents grouped by `DocumentType`, using canonical `knowledge_relations`. Existing structured sections remain unchanged and authoritative.

Example System Unified Knowledge View:

```text
┌──────────────────────────────── System: MES ────────────────────────────────┐
│ owner · lifecycle · KnowledgeStatus · actions                              │
├──────────────────────── Main Content ───────────────────┬─ Context Rail ───┤
│ Knowledge summary                                      │ Related / Gaps    │
│ Business Functions                                     │ Evidence summary  │
│ Database Objects                                       │ Open Unknowns     │
│ Integrations                                           │ Document types    │
│ Business Rules                                         │                   │
│ Knowledge Content                                      │                   │
│  Requirements (3)  Specifications (2)  SOPs (4)       │                   │
│  Troubleshooting (5)  Articles (6)                     │                   │
└─────────────────────────────────────────────────────────┴───────────────────┘
```

No `SystemFullKnowledge` snapshot is persisted. The query is page-oriented and target-specific, consistent with the current architecture. Begin with System; add BusinessFunction only after the System projection and UX are verified. Other object detail pages can receive a compact “相关知识内容” section later.

## 19. Editor Architecture

### Editor comparison

| Option | Strengths | Risks in this repository | Decision |
|---|---|---|---|
| Tiptap | Mature ProseMirror ecosystem, Vue 3 integration, extensible headless UI, tables/code/link extensions | Official Markdown parse/serialize package is currently marked **Beta**; custom extensions require strict round-trip work; easy to drift into a page-builder scope | Strong fallback, not first recommendation for Markdown canonical MVP |
| Lexical | Lean, accessible, modular, strong editor-state model | React-first ecosystem and UI examples; more integration work for Vue; Markdown and toolbars are plugins rather than the core product concern | Reject for current Vue stack |
| Editor.js | Simple block editor and clean block JSON output | Canonical format becomes tool-specific block JSON; Markdown/export and rich table/code behavior depend on plugins; plugin key changes affect stored data | Reject because storage decision is Markdown-first |
| Milkdown | Markdown is the primary model; Vue 3 integration; CommonMark/GFM, tables, clipboard, outline, and WYSIWYG flow; ProseMirror foundation | Smaller ecosystem than Tiptap; still requires a restricted schema, bundle review, and round-trip tests | **Recommend** |
| Plain Markdown textarea | Minimal dependency, perfect fidelity and portability | Poor experience for non-technical authors, tables and links are error-prone, no WYSIWYG | Keep as an emergency/debug fallback only, not main UX |

Official evidence considered:

- Tiptap documents Markdown parsing/serialization and `getMarkdown()`, but labels the Markdown API Beta: [Tiptap Markdown Editor API](https://tiptap.dev/docs/editor/markdown/api/editor).
- Milkdown documents first-class Vue 3 support and retrieving Markdown directly from the editor: [Milkdown Vue integration](https://milkdown.dev/docs/recipes/vue).
- Milkdown provides a GFM preset including tables: [Milkdown GFM preset](https://milkdown.dev/docs/api/preset-gfm).
- Editor.js persists editor-specific block JSON whose block data depends on configured tools: [Editor.js saving data](https://editorjs.io/saving-data/).
- Lexical describes itself as a lean framework where rich text and Markdown are supplied through plugins: [Lexical](https://lexical.dev/).

### Recommended editor boundary

Create one local `KnowledgeDocumentEditor.vue` inside the `knowledge-documents` Feature. It owns Milkdown setup and emits/accepts a Markdown string. Pages and composables never manipulate Milkdown editor state directly.

Allowed initial feature set:

- headings H1–H4;
- paragraphs, bold, italic;
- ordered/unordered lists;
- links;
- blockquotes/callouts using a documented Markdown convention;
- inline code and fenced code blocks;
- GFM tables;
- horizontal rule.

Deferred:

- image upload and attachment nodes;
- mentions, comments, tasks, embeds, diagrams, formulas;
- arbitrary HTML;
- custom widgets;
- collaborative cursors;
- slash-command ecosystem beyond a small fixed menu.

Before adoption, run a time-boxed editor spike that proves Markdown round-trip for every allowed construct, Chinese IME behavior, paste from browser/Office, code blocks, tables, read-only rendering, keyboard accessibility, and bundle impact. Failure of the spike should select a simpler Markdown editor/textarea, not change the canonical storage to opaque editor JSON without a new Architecture Gate.

## 20. Content Storage Format

### Decision: Markdown-first

Canonical storage is normalized Markdown text in `body_markdown`.

Reasons:

- portable and readable outside the editor;
- straightforward diff/revision snapshots;
- good for code blocks, tables, links, and technical content;
- easy to index and chunk by headings;
- avoids persisting editor-library implementation detail;
- supports future export and AI retrieval without HTML cleanup;
- simpler schema and migrations than JSON blocks.

Normalization rules should be narrow:

- UTF-8 application strings;
- normalize line endings to `\n` at the API boundary;
- trim trailing whitespace where it does not affect fenced code;
- ensure stable serialization through the editor;
- reject or sanitize raw HTML in rendering;
- impose a documented maximum body size based on actual operational needs, not an arbitrary tiny text limit.

Rendered HTML is derived in the frontend or by a narrowly scoped renderer. It must be sanitized and never written back as canonical content. Search text, heading outline, snippets, and future AI chunks are also derived and rebuildable.

Rich JSON is not selected because it couples persistence to an editor schema and complicates diff/export/AI consumption. HTML is not selected because it is difficult to sanitize, normalize, diff, and migrate. Hybrid dual-write is not selected because two canonical representations will drift.

## 21. Search Architecture

### Current state

The current global search performs bounded `LIKE` queries over seven structured types and merges grouped results in application code. There is no FTS index, no document type, and no body-aware snippet contract.

### Recommended staged search

1. Extend the existing `/api/search` contract with `KnowledgeDocument` as another search object type; do not create a second global search overlay or search framework.
2. Use SQLite FTS5 for `KnowledgeDocument.Title`, `Summary`, and `BodyMarkdown` because long-form body search is the first concrete requirement that justifies full text.
3. Keep existing structured-object `LIKE` queries initially. The search service merges their groups with the document FTS group.
4. Treat the FTS virtual table as derived/rebuildable. Canonical content remains in `knowledge_documents`.
5. Synchronize the index in the same document write transaction using focused application code or minimal triggers selected during implementation. Do not introduce background indexing infrastructure.

Suggested additive search item fields:

- `contentType` nullable for non-document results;
- `lifecycleStatus` nullable;
- `updatedAt` nullable initially for compatibility;
- existing `title`, `shortDescription` (document snippet), `systemContext`, `knowledgeStatus`, and `navigation` remain.

Document search behavior:

- default excludes Archived;
- matches title, summary, headings, and body;
- returns an escaped text snippet, never rendered arbitrary HTML;
- filters by `DocumentType`, lifecycle, KnowledgeStatus, and optionally related System;
- navigation goes to `knowledge-document-detail` at stable ID;
- current access model means all authenticated users can read all non-archived documents; no permission-based index filtering is needed.

Do not add Elasticsearch/OpenSearch, a generic indexing bus, tag ranking, semantic/vector search, or saved searches in Content MVP.

## 22. AI / Retrieval Considerations

The design should remain retrieval-friendly without implementing AI.

Canonical Markdown enables deterministic derived chunks:

```text
documentId
documentType
title
headingPath
sectionOrdinal
sectionMarkdown / plainText
contentHash
relatedTarget references
knowledgeStatus
lifecycleStatus
updatedAt
```

Future chunking should split by headings first, then by bounded size with overlap only when required. Tables and code blocks should remain intact where practical. `contentHash` allows selective re-indexing. Relations provide high-value metadata for system/function-scoped retrieval.

AI outputs must not directly mutate documents, relationships, Evidence, or KnowledgeStatus. A future assistant may propose a draft, relationship, or UnknownItem; an Editor must explicitly review and save it. Vector embeddings, model selection, prompt pipelines, RAG APIs, and AI-generated answers are explicitly outside this plan’s implementation scope.

## 23. API Proposal

Use explicit use-case routes under the existing `/api` base path. No `/v1`, generic command endpoint, JSON Patch, or CRUD framework.

### Document routes

| Method | Route | Use case |
|---|---|---|
| `GET` | `/api/knowledge-documents` | Paged list with query/type/lifecycle/KnowledgeStatus/relatedSystem filters and explicit sort. |
| `GET` | `/api/knowledge-documents/{id}` | Page-oriented detail including core content, authorship, status, available actions, related knowledge summaries, Evidence/HumanConfirmation summaries, and concurrency token. |
| `POST` | `/api/knowledge-documents` | Create minimum valid Draft with type, title, optional summary/body, and optional explicit initial relationships. |
| `PUT` | `/api/knowledge-documents/{id}/content` | Update title, summary, and Markdown body with opaque concurrency token. |
| `PUT` | `/api/knowledge-documents/{id}/lifecycle` | Explicit Publish, ReturnToDraft, Archive, or Restore action with token. |

No physical DELETE route is proposed.

### Reused routes

- Extend existing KnowledgeStatus route/use case to accept `KnowledgeDocument`.
- Extend existing Evidence subject contract/resolver to accept `KnowledgeDocument`.
- Extend existing HumanConfirmation endpoint through the Evidence subject contract; no document-specific confirmation route.
- Extend existing relationship create/detail/search contracts and endpoint policy; no document relationship API.
- Extend `/api/search` with the document result group.

### Contract shape principles

- `DocumentType`, lifecycle, KnowledgeStatus, relation type, and action values remain English wire codes; frontend supplies Chinese labels.
- IDs remain JavaScript-safe integers.
- `concurrencyToken` remains opaque and is never parsed by Vue.
- Direct success JSON and the existing Error Contract remain.
- Expected errors: `validation_error`, `not_found`, `reference_invalid`, `invalid_state`, `conflict`, `forbidden` using current conventions.
- A stale edit/lifecycle/status token returns `409`; UI reloads and preserves the unsaved local Markdown for manual comparison. It must not silently retry or overwrite.

## 24. Persistence Proposal

### Content MVP table

Conceptual `knowledge_documents` columns:

| Column | Concept | Notes |
|---|---|---|
| `id` | integer PK | Existing safe integer pattern. |
| `document_type` | TEXT required | Controlled enum CHECK. |
| `title` | TEXT required | Normalized, length bounded. |
| `summary` | TEXT null | List/search summary; not duplicated body. |
| `body_markdown` | TEXT required | Canonical content; Draft may contain an initially generated template. |
| `lifecycle_status` | TEXT required | Draft / Published / Archived CHECK. |
| `knowledge_status` | TEXT required | Existing Unknown / Inferred / Confirmed CHECK. |
| status attribution/time fields | existing pattern | Same semantics as current KnowledgeStatus entities. |
| `created_by_user_id` | INTEGER required FK | Canonical User, RESTRICT; users have no physical-delete path. |
| `created_by_display_name` | TEXT required | Local historical display snapshot, not a generic identity framework. |
| `updated_by_user_id` | INTEGER required FK | Canonical User, RESTRICT. |
| `updated_by_display_name` | TEXT required | Historical display snapshot for current revision. |
| `created_at`, `updated_at` | TEXT/instant required | Existing UTC conventions. |
| `published_at`, `archived_at` | nullable instant | Lifecycle facts. |
| `version` | INTEGER required | Existing app-managed concurrency strategy. |

Recommended indexes:

- `(document_type, lifecycle_status, updated_at)` for list/filter.
- `(knowledge_status, lifecycle_status)` only if query-plan evidence supports it during implementation.
- author/update-user indexes only if “My Content” is implemented later.
- FTS5 virtual table for title/summary/body.

Do not add tags, spaces, parent page, slug, arbitrary metadata JSON, permissions ACL, template ID, approval state, attachment blobs, or revision rows in Content MVP.

### Future revision table

`knowledge_document_revisions` is a later additive table with at least:

- `id`, `knowledge_document_id`, `revision_no`;
- title/summary/body Markdown snapshot;
- lifecycle/KnowledgeStatus snapshot only if required for an accurate history view;
- `changed_by_user_id`, `changed_by_display_name`;
- `changed_at`, optional `change_summary`;
- unique `(knowledge_document_id, revision_no)` and index by document/date.

The current document row remains the current state. Revisions are immutable. Do not store diffs as the only historical source.

## 25. Frontend Information Architecture

### Navigation and routes

Add one primary navigation item:

```text
知识内容 → /knowledge-documents
```

Add two routes:

- `/knowledge-documents` — list/filter/search/create entry.
- `/knowledge-documents/:id` — read view and explicit in-page Edit mode.

Do not add separate Requirement/SOP/TestCase navigation items, a page tree, “Spaces,” a second dashboard, or separate create/edit route pages. A lightweight type filter provides category navigation.

### View versus edit

Default is readable document view. Editor/Administrator sees “编辑”; selecting it switches the same route to a full-width editing state. While editing, the Context Rail is hidden/replaced to maximize width, consistent with the responsive shell. The single Drawer manager remains available for Evidence/Relationship detail but must not contain the document editor.

Create flow:

1. Small dialog asks for DocumentType and Title.
2. Apply a hardcoded type template in the frontend.
3. POST creates a Draft.
4. Navigate to detail in edit state.

Templates are typed constants inside the Feature during Content MVP. They are UX defaults, not validation schemas or database-managed content.

### Metadata and content hierarchy

Document header shows type, lifecycle, KnowledgeStatus, title, summary, related Systems, author, and updated time. Main body renders Markdown. Context Rail shows relationship groups, Evidence/HumanConfirmation counts, and UnknownItem links where supported.

### Knowledge Home, dashboard, page tree, My Content

- **Knowledge Home:** the `/knowledge-documents` list is the first content home; do not build an additional landing page.
- **Dashboard:** no new complex widgets in the first slice. A later small “recently updated documents” block may be added only after list/detail usage exists.
- **Page tree:** not in the initial architecture. Hierarchy causes ordering, moves, cycles, permissions inheritance, and duplicate navigation problems.
- **My Content:** defer until documents have real authorship data and users demonstrate the need. It should be a list filter, not a separate workspace.
- **Tags:** defer. Existing System technology tags have different semantics and must not be reused as a generic document taxonomy.

## 26. Text Wireframes

### Knowledge Content List

```text
┌─ 知识内容 ────────────────────────────────────────────────────────────────┐
│ 将结构化知识与需求、规格、SOP、排障和文章连接起来     [新建知识内容]      │
├──────────────────────────────────────────────────────────────────────────┤
│ [搜索标题与正文...] [类型: 全部] [生命周期: 已发布] [知识状态: 全部]     │
│ [关联系统: 全部]                                      排序: 最近更新     │
├──────────────┬────────────────────────┬────────┬────────┬───────────────┤
│ 类型         │ 标题 / 摘要            │ 生命周期│ 知识状态│ 关联系统/更新 │
├──────────────┼────────────────────────┼────────┼────────┼───────────────┤
│ SOP          │ MES 夜间批处理恢复     │ 已发布  │ 已确认  │ MES · 2h       │
│ 排障指南     │ STATE_FLAG 异常排查    │ 草稿    │ 推断    │ WMS · 1d       │
└──────────────┴────────────────────────┴────────┴────────┴───────────────┘
```

### Knowledge Document View

```text
┌─ SOP · 已发布 · 已确认 ─────────────────────────────── [编辑] [更多] ─┐
│ MES 夜间批处理恢复                                                    │
│ 当批处理停滞时的诊断、恢复与验证步骤                                 │
│ MES · 作者 王工 · 更新于 2026-08-22                                  │
├────────────────────────── Main ──────────────────┬─ Context Rail ─────┤
│ Purpose                                          │ AppliesTo          │
│ Preconditions                                    │ - MES              │
│ Procedure                                        │ Evidence 3         │
│ 1. ...                                           │ Confirmation 1     │
│ Verification                                     │ Unknown Items 0    │
│ Rollback or Escalation                           │ Related docs 2     │
└──────────────────────────────────────────────────┴────────────────────┘
```

### Knowledge Document Edit

```text
┌─ 编辑 SOP ─────────────────────────────────────────────────────────────┐
│ [标题................................................]  [取消] [保存] │
│ [摘要...............................................................] │
│ H1 H2 H3 | B I | List | Link | Code | Quote | Table                  │
├───────────────────────────────────────────────────────────────────────┤
│ Markdown-backed WYSIWYG editor                                        │
│                                                                       │
│ ## Procedure                                                          │
│ 1. ...                                                                │
│                                                                       │
├───────────────────────────────────────────────────────────────────────┤
│ 未保存更改 · lifecycle/status/relationships are separate actions      │
└───────────────────────────────────────────────────────────────────────┘
```

### Unified System Knowledge View

```text
┌─ MES System ───────────────────────────────────────────────────────────┐
│ Overview | status | owner                                             │
├───────────────────────────────────────────────┬────────────────────────┤
│ Structured Knowledge                          │ Context Rail           │
│ - Business Functions                          │ gaps / evidence         │
│ - Database Objects / Columns                   │ relationship summary   │
│ - Rules / Integrations                         │                        │
│                                               │                        │
│ Knowledge Content                             │                        │
│ - Requirements / Specifications               │                        │
│ - SOP / Troubleshooting / Test Cases           │                        │
│ - Articles / Design Notes                      │                        │
└───────────────────────────────────────────────┴────────────────────────┘
```

## 27. Permissions

Reuse the SEC02 matrix and current backend policies:

| Action | Viewer | Editor | Administrator |
|---|---:|---:|---:|
| List/search/read document | Yes | Yes | Yes |
| Create/edit content | No | Yes | Yes |
| Publish/return/archive/restore | No | Yes | Yes |
| Change KnowledgeStatus | No | Yes, subject to evidence gates | Yes, same rules |
| Add relation/evidence/HumanConfirmation | No | Yes | Yes |
| Manage users/login identities/access levels | No | No | Yes, existing admin scope only |

Frontend gating uses `actorStore.canEdit`; backend policies remain authoritative. Do not create document ACLs, per-space permissions, ownership-based security, “publisher” role, claims, or a second permission identity. KnowledgeRole remains a HumanConfirmation knowledge identity, not a permission.

SEC04’s blocked external deployment verification does not block architecture planning or local implementation after the Architecture Gate. It remains a deployment prerequisite for production rollout, not a reason to weaken the approved security design.

## 28. Versioning

### First-phase decision

Content MVP does **not** include revision history. This is a deliberate scope decision, not a claim that version history is unnecessary.

MVP safeguards:

- opaque concurrency token on content and lifecycle writes;
- `409 conflict` with understandable UI;
- keep unsaved local content visible after conflict;
- explicit Save only;
- dirty-state marker and route/browser-leave confirmation;
- archive rather than delete;
- author/update attribution.

### Next capability after MVP

Add immutable revisions before broad author adoption or before introducing autosave. A revision is created on each successful explicit content save, not every keystroke. Users can list revisions, inspect a read-only snapshot, compare text at a basic line level, and restore by creating a new revision/current state. Restore never deletes history.

Do not implement branching, merge, collaborative CRDT history, legal records management, or Git semantics.

### Autosave and draft recovery

No server autosave in Content MVP. Autosave complicates concurrency, revision noise, and failure messaging. First provide explicit save and unsaved-change protection. A later local draft recovery can use browser storage keyed by current user and document ID with timestamp/content hash, but it must be clearly local, expire safely, and never silently overwrite server content. Server draft recovery should be designed together with revision history, not before it.

## 29. Attachments

Attachments are deferred from Content MVP, but the content representation must not block them.

Future rules:

- Store attachment metadata separately; do not store base64 in Markdown or SQLite text.
- Use an approved file storage abstraction chosen for the actual deployment environment; do not invent local/prod dual storage in the document Feature.
- Markdown refers to stable attachment IDs/URLs through a controlled renderer.
- Validate filename, content type, size, and authorization on download; never trust browser MIME.
- Images are attachments with preview metadata, not inline data URIs.
- Attachment lifecycle follows the document; an attachment referenced by a historical revision cannot be physically removed without an explicit retention design.
- External links are links, not ingested attachments.

Do not implement a generic media library, antivirus framework, object-storage framework, or attachment deduplication in the first document slice.

## 30. Templates

Content MVP templates are hardcoded TypeScript constants in `features/knowledge-documents`, selected by `DocumentType`. They initialize Markdown once at creation and are not linked afterward.

Advantages:

- zero new table or admin UI;
- versioned with application code;
- predictable and testable;
- avoids template permissions, publication, inheritance, and dynamic validation.

The backend accepts ordinary Markdown and does not enforce headings as a dynamic schema. Minimal type-specific validation may require a non-empty body at publish time only.

Backend-managed templates should be considered later only if administrators genuinely need organization-specific variants. Even then, use a small `KnowledgeDocumentTemplate` entity with controlled type and Markdown body—not a dynamic form/schema engine.

## 31. MVP

### Content MVP scope

The smallest useful MVP must deliver a real end-to-end document capability:

1. `KnowledgeDocument` persistence with the seven controlled types.
2. Draft/Published/Archived lifecycle with archive instead of delete.
3. Canonical Markdown body and restricted Milkdown Vue editor after the spike passes.
4. List with basic search/filter/sort and document detail read view.
5. Create and content edit with opaque concurrency and 409 recovery.
6. Viewer read and Editor/Administrator authoring via current security policies.
7. Explicit relations to existing structured knowledge using the existing relationship model.
8. Evidence and HumanConfirmation subject support.
9. Explicit KnowledgeStatus support with no automatic transition.
10. Global search result group for document body/title/summary, preferably backed by SQLite FTS5.
11. One focused System detail integration showing related documents.
12. Risk-based API, migration, frontend, and one Browser → API → SQLite runtime verification.

### Explicit MVP exclusions

- revisions/history and restore;
- attachments/images upload;
- comments, mentions, notifications;
- autosave/server drafts;
- page tree/spaces;
- tags and custom fields;
- per-document permissions;
- approval workflows;
- real-time collaboration;
- AI, embeddings, semantic search;
- Incident, TestRun, or project-management entities;
- separate document-type Features.

## 32. Implementation Slices

Implementation must begin only after this plan passes Architecture Gate. Each slice produces its own verification report and stops at its gate.

| Slice | Goal | Backend | Frontend | High-risk verification | Dependencies |
|---|---|---|---|---|---|
| **KC-B01 — Document Foundation** | Persist and expose core Draft documents | Entity/config/migration, type/lifecycle enums, create/list/detail/content update, concurrency, Viewer/Editor policies | Typed API/composable, no editor yet | Migration preserves all current data; create/read/update; stale token 409; access matrix | Architecture Gate |
| **KC-B02 — Document Read/List UX** | Make content discoverable and readable | Page projections and filters | Nav item, list, detail renderer, create dialog, template constants, archive/publish UI | Viewer/Editor behavior; lifecycle filters; XSS-safe rendering; no drawer stacking | B01 |
| **KC-B03 — Markdown Editor** | Usable long-form authoring | Body normalization/limits only | Milkdown spike, restricted editor wrapper, explicit save, dirty guard, conflict recovery | Round-trip matrix, Chinese IME, paste, tables/code, accessibility, bundle/build | B02; spike can block/replace editor only |
| **KC-B04 — Relationships** | Connect documents to structured knowledge | Extend target enum/resolver/policy/queries and CHECK migration | Relation picker and grouped related content | Allowed matrix, duplicates, invalid refs, inverse display; no inferred edges | B01–B03 |
| **KC-B05 — Evidence / Status** | Make documents part of epistemic model | Evidence subject resolver, HC subject path, KnowledgeStatus service | Existing drawers/panels reused | Evidence gates, HC snapshot, no auto status/lifecycle change, legacy regression | B04 |
| **KC-B06 — Search** | Search long-form content globally | FTS5 derived index, extend existing search group/contract | Extend existing overlay/decoders/navigation | body matches/snippets, archived exclusion, index sync/rebuild, structured regression | B01–B03 |
| **KC-B07 — Unified System View** | Show documents with structured System knowledge | Extend System page projection | Add grouped Knowledge Content section | one real System with related docs; context rail behavior at 1920/1440 | B04–B06 |
| **KC-C01 — Revision History** | Safe broader authoring | Immutable revisions and restore-as-new-state | History list/view/basic compare/restore | sequential revision numbers, restore, concurrency, historical author snapshot | Content MVP Verification Gate |
| **KC-C02 — Attachments** | Controlled file/image support | Storage decision and attachment contracts | Upload/reference/render | authorization, MIME/size, orphan/retention behavior | Deployment storage decision + C01 |

`KC-B01` is the **one recommended next task** after Architecture Gate approval. Do not start B02 or later automatically.

## 33. Risks

| Risk | Consequence | Mitigation / gate |
|---|---|---|
| Generic document becomes dumping ground | Structured fields are duplicated and stale | Boundary rules, explicit relationships, content-type guidance, review Unified View usage. |
| Option C becomes dynamic metadata | EAV, runtime validation, generic forms | No metadata JSON/table in MVP; typed extension requires a separate decision backed by query/invariant evidence. |
| Markdown editor round-trip loss | Content corruption | B03 spike and construct matrix; restricted schema; canonical storage cannot change silently. |
| Markdown XSS | Security compromise | Disallow/sanitize raw HTML, safe link protocols, security tests around renderer. |
| Large-body `LIKE` search | Slow queries and poor snippets | Use dedicated SQLite FTS5 for documents; keep index derived and bounded. |
| FTS index drift | Missing/stale search results | Same-transaction synchronization or minimal triggers plus deterministic rebuild verification. |
| Relationship vocabulary explosion | Unusable graph and meaningless edges | Six/seven explicit new types, endpoint matrix, no generic `RelatedTo`. |
| Adding KnowledgeDocument to shared enums broadens unrelated inputs | Invalid UnknownItem updates/targets | Purpose-specific allowlists and focused regression tests on all enum consumers. |
| Lifecycle confused with confidence | Publishing falsely implies confirmation | Two separate badges/actions; no automatic coupling. |
| No MVP revisions | Accidental loss despite concurrency | Explicit save, dirty guard, conflict preservation; C01 immediately follows MVP before broad rollout. |
| Authorship follows mutable user profile | Historical attribution becomes unclear | Canonical User FK plus local display snapshot; no dynamic join-only display. |
| Archived documents disappear from evidence/history | Broken historical context | Stable IDs, no delete, archive remains readable, Evidence unchanged. |
| Editor dependency bloats bundle | Slow app and complex maintenance | Route-level lazy loading, restricted packages/features, measure B03 build output. |
| Unified view becomes a new aggregate/framework | Architectural duplication | Target-specific page projection; no physical full-knowledge model or generic repository. |
| Attachments introduced too early | Storage/security/retention scope expansion | Defer to C02 with deployment storage decision. |
| Users expect Jira/ITSM/test management | Scope explosion | Clear type semantics and exclusions; new workflow entities require separate frozen design. |
| SEC04 remains blocked | Production deployment cannot be security-verified | Keep planning/implementation separate; production rollout still waits for real provider/proxy/HTTPS/key resources. |

## 34. Explicitly Not Implemented

This architecture plan does not implement or authorize:

- any production code, database migration, API, router, UI, editor dependency, or package installation;
- Authentication, Authorization redesign, RBAC/Permission redesign, Claims changes, Login/Logout, OIDC changes, SSO changes, or SEC04 continuation;
- a second Current User, User, KnowledgeRole, identity, or permission model;
- a Person, Department, Team, JobTitle, Organization Tree, or generic audit framework;
- a Confluence clone, Spaces, page trees, page ownership hierarchy, comments, reactions, mentions, notifications, watches, collaborative editing, plugins, macros, or generic embeds;
- Jira/project management, backlog, sprint, assignee, estimation, approval, or workflow engines;
- TestRun/TestSuite/TestPlan execution management;
- Incident/Problem/ITSM ticketing or on-call workflow;
- generic dynamic forms, low-code schema, Metadata UI Engine, EAV, arbitrary metadata JSON, runtime content-type designer, or template designer;
- generic Repository, UnitOfWork, CQRS/MediatR, mapper framework, command bus, query bus, event bus, or architecture layer restructuring;
- a second Evidence, HumanConfirmation, Relationship, Search, Drawer, or KnowledgeStatus framework;
- automatic relationships, automatic Evidence, automatic KnowledgeStatus changes, or AI-authored canonical content;
- AI/RAG, embeddings, vector database, semantic search, AI answer generation, or model integrations;
- attachments, revision history, autosave, tag taxonomy, or per-document ACL in Content MVP;
- physical document deletion;
- changes to frozen MVP specifications or approved security designs;
- XML documentation rollout B04+ or unrelated code comments;
- any follow-on slice before its predecessor verification gate passes.

## 35. Architecture Decisions

| Decision | Recommendation | Reason |
|---|---|---|
| Generic model | `KnowledgeDocument` core with controlled `DocumentType` | One authored-content concept without a generic `KnowledgeObject` framework. |
| Separate types | Types are enum values; no separate Feature/table per Requirement/Spec/SOP/TestCase | Shared lifecycle/editor/search/relations dominate; workflows are not yet distinct. |
| Target architecture option | Staged Option C: core now, typed extensions only on proven need | Preserves queryability without EAV or premature tables. |
| Structured knowledge | Retain and keep canonical | Existing objects provide validated, filterable, relational truth that documents cannot replace. |
| Unified view | Add target-specific read projections on existing detail routes | Combines knowledge without copying it into a new aggregate. |
| Entity name | `KnowledgeDocument`; UI “知识内容” | Concrete domain language and clear product navigation. |
| Editor | Milkdown Vue 3 with restricted CommonMark/GFM feature set | Direct Markdown model, Vue support, technical-content features, no page builder. |
| Storage | Markdown-first; derived sanitized HTML/outline/search chunks | Portable, diffable, searchable, AI-ready, editor-independent. |
| Lifecycle | Draft / Published / Archived, no delete | Small editorial model and durable references. |
| KnowledgeStatus | Reuse Unknown → Inferred → Confirmed independently | Confidence is not publication state; existing gates remain authoritative. |
| Versioning | Not in Content MVP; immutable revisions as immediate post-MVP capability | Keeps first slice small while acknowledging long-form safety needs. |
| Autosave | No server autosave before revisions | Avoids concurrency and revision-noise complexity. |
| Attachments | Deferred; separate metadata/storage, stable IDs, never base64 body | Storage/security/retention need an explicit later design. |
| Templates | Hardcoded typed Markdown defaults in frontend for MVP | Useful authoring guidance without schema/admin/framework scope. |
| Search | Extend current global search; document-only SQLite FTS5 initially | Long body justifies FTS while external search infrastructure does not. |
| Relationships | Reuse `knowledge_relations`; minimal explicit new vocabulary | Keeps one graph and preserves meaningful typed edges. |
| Evidence/HC | Add KnowledgeDocument as existing subject type | Reuses the established epistemic model and canonical user snapshot. |
| Permissions | Viewer read; Editor and Administrator author; existing backend policy authoritative | Matches SEC02/SEC03; no second permission model. |
| UI routes | One list route and one detail/edit route; no editor drawer | Long-form needs main width and should preserve the single-drawer architecture. |
| Taxonomy/spaces/tree | No tags, spaces, or page tree in MVP | Type filters and relations meet current navigation needs with far less complexity. |
| AI readiness | Heading-based derived chunks later; no AI implementation | Markdown and explicit relations preserve a clean future retrieval boundary. |

## 36. Recommended Next Task

After this document passes the Architecture Gate, start exactly one task:

```text
KC-B01 — KnowledgeDocument Foundation + Persistence
```

KC-B01 should freeze the core `KnowledgeDocument` contract, implement only the table/migration and create/list/detail/content-update endpoints with existing security and concurrency semantics, add focused relational/API tests, generate its verification report, clean all verification processes, and stop.

Do not install an editor, add relationships/evidence/search, modify Unified Views, start revision history, or begin KC-B02+ during KC-B01.

---

**Architecture Gate status:** Awaiting human review. No implementation is authorized by this plan alone.
