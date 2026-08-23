# KC-C01 — Relationship Vocabulary Decision Report

## Result

```text
APPROVED WITH CHANGES
```

This is an architecture-only result. No production code, migration, database, API contract, or frontend relation mapping was modified.

`KC-GAP-003` is now `DECIDED / IMPLEMENTATION REQUIRED`; it is not closed.

## Current State

The current implementation has 18 `RelationType` enum/wire values: nine frozen structured-knowledge values and nine KC-B04 document values.

For a KnowledgeDocument source, the backend currently treats all nine document values identically and permits each to target System, BusinessFunction, DatabaseObject, BusinessRule, Integration, or KnowledgeDocument. The frontend duplicates the same broad matrix, shows all nine choices, and defaults a KnowledgeDocument source to `Documents`.

This means current validation proves that a value is in the enum and that the source is a KnowledgeDocument, but it does not prove the intended DocumentType, target DocumentType, or relation meaning.

Persistence includes every current value in `ck_knowledge_relations_relation_type`. The KC-B04 migration also added `KnowledgeDocument` to both endpoint CHECK constraints.

## Approved Architecture

The approved Knowledge Content plan defines:

- `Documents`: KnowledgeDocument → structured target;
- `AppliesTo`: KnowledgeDocument → structured target with applicability semantics;
- `SpecifiedBy`: Requirement → Specification;
- `VerifiedBy`: Requirement/Specification → TestCase;
- `References`: KnowledgeDocument → document or structured target;
- `Supersedes`: KnowledgeDocument → KnowledgeDocument;
- conditional future `DerivedFrom`: KnowledgeDocument → UnknownItem only if UnknownItem relationship endpoints enter scope;
- no first-phase `RelatedTo`.

The frozen MVP Domain and Database Models are stricter: they explicitly prohibit a universal `RelatedTo` and require a closed RelationType endpoint matrix.

## Drift

| RelationType | Current implementation | Approved plan | Drift? | Decision needed/result |
| --- | --- | --- | --- | --- |
| `Documents` | Any KnowledgeDocument → six broad targets, including KnowledgeDocument | KnowledgeDocument → structured target | Yes | Retain; exclude KnowledgeDocument target and define “source substantially describes target.” |
| `References` | Any KnowledgeDocument → six broad targets | KnowledgeDocument → document or structured target | Partial | Retain; define citation-only semantics and stronger-relation precedence. |
| `AppliesTo` | Any KnowledgeDocument → six broad targets | Applicability relation for relevant document types and structured targets | Yes | Restrict by DocumentType and structured target. |
| `Implements` | Any KnowledgeDocument → six broad targets | Not approved | Yes | Disable then remove. |
| `SpecifiedBy` | Any KnowledgeDocument → six broad targets | Requirement → Specification | Yes | Restrict to exact source/target DocumentTypes. |
| `VerifiedBy` | Any KnowledgeDocument → six broad targets | Requirement/Specification → TestCase | Yes | Restrict to exact source/target DocumentTypes. |
| `Resolves` | Any KnowledgeDocument → six broad targets | Not approved; no Incident entity | Yes | Disable then remove. |
| `RelatedTo` | Any KnowledgeDocument → six broad targets | Explicitly prohibited | Yes | Disable then remove; Option A. |
| `Supersedes` | Any KnowledgeDocument → six broad targets | KnowledgeDocument → KnowledgeDocument | Yes | Restrict to different documents of the same DocumentType. |
| `DerivedFrom` | Not implemented | Conditional on UnknownItem endpoint implementation | No | Keep deferred. |

The primary problem is not only the three extra names. The six approved names also lack their approved endpoint/DocumentType semantics in current code.

## Relation Inventory

### Inventory method

The inventory inspected the current working-tree implementation rather than inferring from reports:

- backend enum/entity, endpoint policy, resolver, query/service validation, controller contracts, and EF configuration;
- KC-B04 migration and model snapshot;
- frontend union, decoder, Chinese label map, add drawer, detail/list presentation;
- relationship API tests, KnowledgeDocument detail tests, and Unified System View tests;
- B04 and phase runtime reports;
- the repository application database through a read-only SQLite connection.

“HTTP coverage” below means an existing integration test exercises the real Controller/application/EF path. “Browser” refers to an explicitly recorded browser runtime verification, not component-test simulation.

### All actual RelationType values

| Wire value | Current Chinese label | Current allowed source → target | Actual HTTP/browser use found | Test coverage found | In KC-B04 CHECK | Known repository DB rows / data risk |
| --- | --- | --- | --- | --- | ---: | --- |
| `Calls` | 调用 | BusinessFunction → BusinessFunction | HTTP rejection of cross-System call | Negative endpoint rule | Yes | 0; external DB unknown |
| `Reads` | 读取 | BusinessFunction → DatabaseObject/DatabaseColumn | HTTP create/search/detail/update/status path | Strong focused coverage | Yes | 0; external DB unknown |
| `Writes` | 写入 | BusinessFunction → DatabaseObject/DatabaseColumn | HTTP create and duplicate | Positive + duplicate | Yes | 0; external DB unknown |
| `UsesField` | 使用字段 | BusinessFunction/BusinessRule → DatabaseColumn | HTTP valid status path and illegal DatabaseObject target | Positive + negative | Yes | 0; external DB unknown |
| `AppliesRule` | 应用规则 | BusinessFunction → BusinessRule | HTTP use in BusinessRules API test | Focused integration use | Yes | 0; external DB unknown |
| `PublishesVia` | 通过集成发布 | System/BusinessFunction → Integration | No exact focused use found | No exact wire-value test found | Yes | 0; external DB unknown |
| `ConsumesVia` | 通过集成消费 | System/BusinessFunction → Integration | No exact focused use found | No exact wire-value test found | Yes | 0; external DB unknown |
| `UsesIntegration` | 使用集成 | BusinessFunction/BusinessRule → Integration | HTTP use in Integrations API test | Focused integration use | Yes | 0; external DB unknown |
| `DependsOn` | 依赖 | System/BusinessFunction/Integration → System/DatabaseSource/DatabaseObject | No exact focused use found | No exact wire-value test found | Yes | 0; external DB unknown |
| `Documents` | 文档说明 | KnowledgeDocument → System/BusinessFunction/DatabaseObject/BusinessRule/Integration/KnowledgeDocument | HTTP create; KC-B04 browser default; one persisted app row | Positive API; Unified View uses `Documents`, including one direct-seeded reverse row that policy would reject | Yes | **1** valid KnowledgeArticle → System row; preserve |
| `References` | 引用 | KnowledgeDocument → same six targets | HTTP create; Viewer forbidden-write case | Positive API + access boundary | Yes | 0; external DB preflight required |
| `AppliesTo` | 适用于 | KnowledgeDocument → same six targets | HTTP create; R01 browser SOP → System; frontend detail test | Positive API, viewer, detail UI, Unified View | Yes | 0 in repository DB; external DB preflight required |
| `Implements` | 实现 | KnowledgeDocument → same six targets | HTTP test creates Document → BusinessRule | Positive API only | Yes | 0; safe known-db removal, external DB preflight required |
| `SpecifiedBy` | 规格说明 | KnowledgeDocument → same six targets | No exact HTTP/browser use found | Enum/decoder presence only | Yes | 0; external DB preflight required |
| `VerifiedBy` | 验证 | KnowledgeDocument → same six targets | No exact HTTP/browser use found | Enum/decoder presence only | Yes | 0; external DB preflight required |
| `Resolves` | 解决 | KnowledgeDocument → same six targets | HTTP test creates Document → Integration | Positive API only | Yes | 0; safe known-db removal, external DB preflight required |
| `RelatedTo` | 相关 | KnowledgeDocument → same six targets | HTTP Document → Document | Positive create/incoming/duplicate/self/delete | Yes | 0; safe known-db removal, external DB preflight required |
| `Supersedes` | 替代 | KnowledgeDocument → same six targets | No exact HTTP/browser use found | Enum/decoder presence only | Yes | 0; external DB preflight required |

The current frontend has no focused test for relation-picker option filtering or the default relation selection. `KnowledgeDocumentDetailView.spec.ts` covers displaying/navigating one `AppliesTo` relation and Viewer action hiding. `SystemUnifiedKnowledgeView.spec.ts` displays `AppliesTo` and `Documents` strings but does not validate vocabulary legality.

## Semantic Analysis

### Documents

Decision: retain with exact direction.

```text
Source: KnowledgeDocument
Relation: Documents
Target: System | BusinessFunction | DatabaseObject | BusinessRule | Integration
Meaning: the source document substantially describes/documents the target
```

`KnowledgeArticle → Documents → System` is clear. `Documents` is not a document-to-document relation and does not mean “the target owns the document.” On an incoming target view, it is read as “由文档说明.”

### References

Decision: retain.

```text
Source KnowledgeDocument → References → target object/document
```

It records a deliberate citation or pointer only. It does not assert ownership, implementation, dependency, applicability, verification, or replacement. If one of those stronger facts is intended and legal, that stronger relation must be used.

### AppliesTo

Decision: retain and restrict.

It is the correct relation for:

- Requirement → System/BusinessFunction;
- Sop → System/BusinessFunction/DatabaseObject/Integration;
- Troubleshooting → System/DatabaseObject/Integration.

It is more useful than `RelatedTo` because applicability can support scoped navigation and impact analysis.

### SpecifiedBy

Decision: retain with the approved passive direction:

```text
Requirement → SpecifiedBy → Specification
```

This is natural when read from the Requirement. The Specification page displays the incoming side as “定义需求.” A second `Specifies` row/value is not stored or added.

### VerifiedBy

Decision: retain and restrict:

```text
Requirement | Specification → VerifiedBy → TestCase
```

It represents verification definition/coverage, not a TestRun result. The TestCase page displays incoming edges as “验证需求/规格.”

## RelatedTo Decision

| Option | Assessment |
| --- | --- |
| A — Remove | Best graph quality; conforms to frozen models and approved plan; users can leave the edge absent or record an UnknownItem. |
| B — Keep but restrict to document ↔ document | Still overlaps `References` and `Supersedes`; creates low-value edges and encourages postponing semantic classification. |
| C — Keep generic | Current behavior; directly conflicts with frozen/approved architecture and creates the highest search/traceability/RAG noise. |

**Selected: Option A — Remove.**

No human product choice is required because both frozen models and the approved plan already resolve the boundary. Retaining `RelatedTo` would require an explicit amendment that contradicts those sources and lacks supporting product evidence.

## Implements Decision

**Remove from the active document vocabulary.**

A Requirement is specified by a Specification; a Specification document does not implement the Requirement. A KnowledgeDocument also does not implement a System, BusinessRule, Integration, or DatabaseObject. The current product has no SoftwareComponent, ImplementationArtifact, CodeModule, Deployment, or comparable implementation entity.

Possible reclassification is case-specific:

- substantial description of a structured target → `Documents`;
- citation → `References`;
- Requirement elaborated by Specification → `SpecifiedBy` in canonical direction.

The `Implements` concept can return only with a real implementation endpoint and a new architecture decision.

## Resolves Decision

**Remove from the active document vocabulary.**

Troubleshooting does not resolve a System, Integration, or DatabaseObject. It applies to an affected target. There is no Incident or Problem endpoint in the approved product scope.

Use `Troubleshooting → AppliesTo → affected target`. Defer `Resolves` until an approved Incident/Problem entity exists.

## Supersedes Decision

**Retain but restrict to different KnowledgeDocuments of the same DocumentType.**

`Supersedes` is useful before revision history because it can express that one independent document replaces another. It does not represent an edit revision and does not archive either document automatically. Same-type restriction prevents it becoming another vague relation.

## Final Vocabulary Proposal

### Retain

- `Documents`
- `References`
- all nine pre-KC-B04 structured vocabulary values unchanged

### Restrict

- `AppliesTo`
- `SpecifiedBy`
- `VerifiedBy`
- `Supersedes`

### Remove

- `RelatedTo`
- `Implements`
- `Resolves`

Removal uses an immediate disable-new-writes step followed by data review, CHECK tightening, and contract cleanup.

### Defer

- `DerivedFrom` until UnknownItem becomes an approved Relationship endpoint
- `Implements` concept until a real implementation entity exists
- `Resolves` concept until a real Incident/Problem entity exists

## Allowed Relation Matrix

### General layer

| Source | Relation | System | BusinessFunction | DatabaseObject | BusinessRule | Integration | KnowledgeDocument |
| --- | --- | ---: | ---: | ---: | ---: | ---: | ---: |
| Any KnowledgeDocument | `Documents` | Yes | Yes | Yes | Yes | Yes | No |
| Any KnowledgeDocument | `References` | Yes | Yes | Yes | Yes | Yes | Yes |
| Any KnowledgeDocument | `Supersedes` | No | No | No | No | No | Same DocumentType only |
| Any KnowledgeDocument | `RelatedTo` / `Implements` / `Resolves` | No | No | No | No | No | No |

### Document-specific layer

| DocumentType | Relation | Allowed target | Notes |
| --- | --- | --- | --- |
| Requirement | `AppliesTo` | System / BusinessFunction | Scope |
| Requirement | `SpecifiedBy` | Specification document | Traceability |
| Requirement | `VerifiedBy` | TestCase document | Coverage |
| Specification | `VerifiedBy` | TestCase document | Coverage |
| Specification | `Documents` | DatabaseObject / Integration; other general structured targets when semantically accurate | Specification → Requirement is incoming `SpecifiedBy` |
| TestCase | No additional outgoing type | Incoming `VerifiedBy` from Requirement/Specification | No inverse row |
| Sop | `AppliesTo` | System / BusinessFunction / DatabaseObject / Integration | Procedure scope |
| Troubleshooting | `AppliesTo` | System / DatabaseObject / Integration | Not `Resolves` |
| KnowledgeArticle | `Documents` recommended | System / BusinessFunction; general `Documents` remains available when accurate | Explanatory content |
| DesignNote | `Documents` recommended | System / Integration; general `Documents` remains available when accurate | Structured-target rationale |
| DesignNote | `References` | Specification document | Document reference |

DatabaseSource and DatabaseColumn are intentionally not added as document targets in this correction. A later proven use case can propose them without using a universal relation.

## Existing Data Impact

The repository contains one application database:

`src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db`

Read-only findings on 2026-08-23:

- KC-B04 migration `20260822124136_AddKnowledgeDocumentRelationships` is applied.
- `knowledge_relations` contains one row total.
- The row is `KnowledgeArticle #1 → Documents → System #12 (MES)` and remains valid.
- `RelatedTo`, `Implements`, and `Resolves` each have zero rows.
- No repository-retained temporary/test database was found.

Risk is low for the known database but not assumed zero for other environments. The implementation must inventory every target database before tightening its CHECK constraint.

No existing relation was modified or deleted during KC-C01.

## Migration Impact

A migration is required because the current SQLite CHECK explicitly permits all three removed values. Endpoint/DocumentType rules remain application validation, while legal wire values remain database-constrained.

Safe rollout:

1. disable new writes for removed values;
2. inventory affected rows;
3. explicitly reclassify/remove only after semantic review;
4. update CHECK constraint;
5. remove enum and contract members.

There is no safe automatic mapping. SQLite table-rebuild behavior and retention of indexes/constraints must be verified in the follow-up slice.

## API Impact

The route and JSON shapes remain unchanged. Required corrections are:

- reject removed wire values;
- validate source DocumentType and target DocumentType;
- validate the narrowed target-type matrix;
- continue returning a single directed relation with incoming/outgoing read projection;
- retain existing duplicate/self checks and KnowledgeStatus behavior.

During a staged rollout, historical removed values may remain readable until data cleanup. They must not remain creatable. The final contract union excludes them.

## Frontend Impact

Required follow-up changes:

- remove `RelatedTo`, `Implements`, and `Resolves` from union/labels/options after compatibility cleanup;
- stop showing every document relation against every target type;
- filter by source DocumentType, relation, target type, and target DocumentType;
- begin with no relation selected;
- show recommended relations first and other legal relations second;
- use contextual incoming/outgoing labels;
- add focused tests for option filtering, no-default behavior, and canonical inverse authoring.

`RelatedTo` must not appear under an “其他关系” section and must never be the default.

## Architecture Amendment Requirement

The approved architecture plan does not need to be changed to keep the extra vocabulary. The opposite applies: implementation must return to its approved boundary.

`KC_C01_RELATIONSHIP_VOCABULARY_ARCHITECTURE_DECISION.md` is a normative clarification that adds strict semantics and rollout guidance. It does not retroactively amend the plan to allow `RelatedTo`, `Implements`, or `Resolves`.

The repository has no existing ADR directory or ADR naming convention, so KC-C01 did not invent one and did not create a separate ADR file.

## Recommended Implementation Slice

```text
KC-C02 — Relationship Vocabulary Contract Correction
```

Scope:

1. disable/remove the three rejected values safely;
2. implement the decision matrix with DocumentType-aware validation;
3. update migration/CHECK, API/frontend contracts, labels, and picker behavior;
4. correct broad tests and add focused positive/negative coverage;
5. perform one Browser → API → SQLite runtime verification;
6. generate a verification report and stop.

It must not add `DerivedFrom`, UnknownItem relationship endpoints, relation notes, revision history, or adjacent capabilities.

## KC-GAP-003 Status

```text
DECIDED / IMPLEMENTATION REQUIRED
```

Closure criteria still outstanding:

- production enum/policy and CHECK conform;
- removed values are no longer writable or in final contracts;
- retained values enforce their exact matrix;
- frontend selection/default behavior conforms;
- affected databases pass compatibility preflight;
- focused migration/API/frontend/runtime verification passes.

Until those are complete, KC-GAP-003 remains open and further vocabulary expansion remains blocked.

## Scope and Verification Record

- Read all task-mandated architecture/verification/gap documents and inspected real implementation surfaces.
- Recorded the pre-existing dirty worktree before editing.
- Queried the application database read-only; made no data changes.
- Created only the two KC-C01 architecture/report files.
- Did not run `dotnet build`, `dotnet test`, `npm`, or runtime servers because this task is documentation-only.
- Final whitespace and production-diff checks are recorded after document generation.
