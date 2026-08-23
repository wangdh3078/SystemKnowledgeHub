# KC-C01 — Relationship Vocabulary Architecture Decision

> Product: 系统知识中心 / System Knowledge Hub
> Deliverable: Architecture Decision / Amendment
> Decision status: **APPROVED WITH CHANGES**
> Decision date: 2026-08-23
> Implementation status: Not implemented; Architecture Gate required

## 1. Decision Summary

System Knowledge Hub keeps the existing single directed-edge `knowledge_relations` model. It does not store inverse rows and does not add a generic relationship framework.

For KnowledgeDocument relationships, the approved active wire vocabulary is:

- `Documents`
- `References`
- `AppliesTo`
- `SpecifiedBy`
- `VerifiedBy`
- `Supersedes`

The following current wire values are removed from the active vocabulary:

- `RelatedTo`
- `Implements`
- `Resolves`

They must first be disabled for new writes, then removed from enum/CHECK/API/frontend surfaces after a data preflight. They must not remain as hidden but accepted alternatives.

`DerivedFrom` remains deferred. It may be proposed only when `UnknownItem` becomes an approved Relationship endpoint. It is not authorized by this decision.

The existing structured-knowledge vocabulary (`Calls`, `Reads`, `Writes`, `UsesField`, `AppliesRule`, `PublishesVia`, `ConsumesVia`, `UsesIntegration`, `DependsOn`) is unchanged.

## 2. Context and Authority

The frozen MVP Domain and Database Models prohibit a universal `RelatedTo` relation and require a closed endpoint matrix. The approved Knowledge Content architecture plan repeats that boundary and defines the first document vocabulary as `Documents`, `AppliesTo`, `SpecifiedBy`, `VerifiedBy`, `References`, and `Supersedes`, with conditional future `DerivedFrom`.

KC-B04 instead added `Implements`, `Resolves`, and `RelatedTo`, and allowed every one of the nine document relation types from any KnowledgeDocument to System, BusinessFunction, DatabaseObject, BusinessRule, Integration, or KnowledgeDocument. This is both a vocabulary drift and an endpoint-semantics drift.

Long-term Unified View, search, traceability, impact analysis, and AI/RAG readiness depend on meaningful typed edges. Convenience is not sufficient justification for weak or misleading graph facts.

## 3. Design Principles

The relationship vocabulary must remain:

1. **Explicit:** a user can explain the relation without reading free-form notes.
2. **Directed:** every wire value has one canonical source and target direction.
3. **Non-overlapping by intent:** citation, description, applicability, traceability, and replacement are distinct facts.
4. **Validatable:** the backend can reject illegal source type, DocumentType, target type, target DocumentType, and direction.
5. **Closed but extensible by decision:** new values require an Architecture Decision, schema/contract impact analysis, and focused tests.
6. **Not universal:** absence of a suitable relation remains valid. The user records an UnknownItem when the relationship meaning is not known.

No `Other`, `UnknownRelation`, or replacement universal relation is introduced. No new relation-description/note field is introduced.

## 4. Normative Vocabulary

### 4.1 Retain

| Wire value | Canonical direction | Normative meaning | Recommended outgoing label | Recommended incoming label |
| --- | --- | --- | --- | --- |
| `Documents` | KnowledgeDocument → structured target | The source document substantially describes or documents the target. It is stronger than a citation and does not mean applicability, implementation, verification, or dependency. | 说明 | 由文档说明 |
| `References` | KnowledgeDocument → structured target or KnowledgeDocument | The source deliberately cites or points to the target. It makes no claim of ownership, implementation, applicability, verification, dependency, or replacement. | 引用 | 被引用 |

`Documents` never targets another KnowledgeDocument. A document-to-document citation uses `References`; a document replacement uses `Supersedes`; requirement traceability uses `SpecifiedBy` or `VerifiedBy`.

### 4.2 Restrict

| Wire value | Canonical direction | Allowed semantic | Recommended outgoing label | Recommended incoming label |
| --- | --- | --- | --- | --- |
| `AppliesTo` | Requirement/Sop/Troubleshooting → permitted structured target | The source's scope or procedure applies to the target. It does not mean the source merely mentions the target. | 适用于 | 适用文档 |
| `SpecifiedBy` | Requirement → Specification | The requirement is elaborated or defined by the target specification. | 由规格说明定义 | 定义需求 |
| `VerifiedBy` | Requirement/Specification → TestCase | The target TestCase defines how the source requirement/specification is verified. It does not record a test run or result. | 由测试用例验证 | 验证需求/规格 |
| `Supersedes` | KnowledgeDocument → KnowledgeDocument | The source is the intended replacement for the older target document. Both documents must have the same DocumentType in the first implementation. It never archives the target automatically. | 替代旧文档 | 被新文档替代 |

These restrictions are server invariants, not frontend guidance only.

### 4.3 Remove from the active vocabulary

| Wire value | Decision | Reason | Valid replacement when applicable |
| --- | --- | --- | --- |
| `RelatedTo` | Remove; immediately disable new writes | It is prohibited by frozen models and the approved plan, overlaps every useful relation, and degrades graph/search/RAG semantics. | Choose a precise active type; otherwise create no edge and record an UnknownItem if the meaning is unknown. |
| `Implements` | Remove; immediately disable new writes | A KnowledgeDocument is authored knowledge, not an implementation entity. A Specification does not implement a Requirement, and a document does not implement a System, rule, integration, or database object. | `Documents`, `References`, or `SpecifiedBy`, only after semantic review. |
| `Resolves` | Remove; immediately disable new writes | There is no Incident or Problem relationship endpoint. Troubleshooting does not resolve a System, Integration, or DatabaseObject; it applies to the affected target. | `AppliesTo` for an affected structured target, or a future relation to an approved Incident/Problem entity. |

No automatic mapping is safe. Endpoint types alone cannot determine the intended meaning.

### 4.4 Defer

| Candidate | Status | Reconsideration gate |
| --- | --- | --- |
| `DerivedFrom` | Deferred, not an active or accepted wire value | A separate decision adds `UnknownItem` to Relationship endpoints and defines KnowledgeDocument → UnknownItem semantics. |
| `Implements` concept | Deferred after removal | A real implementation artifact/entity exists and the product needs traceability from that entity to Requirement/Specification. |
| `Resolves` concept | Deferred after removal | A real Incident/Problem entity exists with durable identity and lifecycle. |
| Free-form relation explanation | Future only | A proven use case cannot be represented by current optional Relationship description without adding new semantics. |

## 5. Canonical Allowed Matrix

### 5.1 General document relations

| Source | Relation | Target | Allowed | Constraint |
| --- | --- | --- | --- | --- |
| Any KnowledgeDocument | `Documents` | System / BusinessFunction / DatabaseObject / BusinessRule / Integration | Yes | Source substantially describes target. KnowledgeDocument, DatabaseSource, and DatabaseColumn are not targets in this slice. |
| Any KnowledgeDocument | `References` | System / BusinessFunction / DatabaseObject / BusinessRule / Integration / KnowledgeDocument | Yes | Citation/reference only; use a stronger legal relation when that fact is intended. |
| Any KnowledgeDocument | `Supersedes` | KnowledgeDocument | Restricted | Source and target differ and have the same DocumentType. No automatic lifecycle change. |
| Any KnowledgeDocument | `RelatedTo` / `Implements` / `Resolves` | Any target | No | Rejected for new writes. |
| Structured knowledge object | Any document-specific relation | KnowledgeDocument | No canonical outgoing edge | The single stored edge uses the canonical document direction. UI may offer an inverse authoring action but submits one canonical row. |

### 5.2 DocumentType-specific relations

General `Documents`, `References`, and same-type `Supersedes` rules remain available where their exact meaning applies. The following table defines additional type-specific traceability and applicability.

| Source DocumentType | Relation | Allowed target | Product use |
| --- | --- | --- | --- |
| `Requirement` | `AppliesTo` | System / BusinessFunction | Requirement → System / BusinessFunction scope |
| `Requirement` | `SpecifiedBy` | KnowledgeDocument with type `Specification` | Requirement → Specification traceability |
| `Requirement` | `VerifiedBy` | KnowledgeDocument with type `TestCase` | Requirement → TestCase coverage |
| `Specification` | `VerifiedBy` | KnowledgeDocument with type `TestCase` | Specification → TestCase coverage |
| `Specification` | `Documents` | System / BusinessFunction / DatabaseObject / BusinessRule / Integration | Specification describes the structured target |
| `TestCase` | none additional | Incoming `VerifiedBy` from Requirement/Specification | TestCase → Requirement/Specification is represented by the same incoming edge, not a duplicate inverse row |
| `Sop` | `AppliesTo` | System / BusinessFunction / DatabaseObject / Integration | Procedure applicability |
| `Troubleshooting` | `AppliesTo` | System / DatabaseObject / Integration | Diagnostic guide applicability; never `Resolves` the target |
| `KnowledgeArticle` | `Documents` | System / BusinessFunction (recommended); other permitted structured targets only when it truly documents them | General explanatory knowledge |
| `DesignNote` | `Documents` | System / Integration (recommended); other permitted structured targets when it truly documents them | Design rationale for structured targets |
| `DesignNote` | `References` | KnowledgeDocument with type `Specification` | DesignNote → Specification reference |

This layered policy avoids hundreds of hard-coded combinations:

- General relations define the broad but precise citation/description/replacement semantics.
- Document-specific relations define applicability and traceability.
- Restricted relations require both endpoint types and DocumentType checks.

## 6. Required Product Scenarios

| Required scenario | Canonical stored edge |
| --- | --- |
| Requirement → Specification | Requirement `SpecifiedBy` Specification |
| Specification → Requirement | Display the incoming side of the same `SpecifiedBy` row as “定义需求” |
| Requirement → TestCase | Requirement `VerifiedBy` TestCase |
| TestCase → Requirement | Display the incoming side of the same `VerifiedBy` row as “验证需求” |
| Specification → TestCase | Specification `VerifiedBy` TestCase |
| TestCase → Specification | Display the incoming side of the same `VerifiedBy` row as “验证规格” |
| Requirement → System / BusinessFunction | Requirement `AppliesTo` target |
| Specification → DatabaseObject / Integration | Specification `Documents` target; use `References` only for a citation |
| SOP → System / BusinessFunction / DatabaseObject / Integration | Sop `AppliesTo` target |
| Troubleshooting → System / Integration / DatabaseObject | Troubleshooting `AppliesTo` target |
| KnowledgeArticle → System / BusinessFunction | KnowledgeArticle `Documents` target |
| DesignNote → Specification | DesignNote `References` Specification |
| DesignNote → System / Integration | DesignNote `Documents` target |

## 7. Direction and Inverse Semantics

Persistence remains one directed row:

```text
source_type + source_id + relation_type + target_type + target_id
```

The system must not save a second inverse row for `SpecifiedBy`, `VerifiedBy`, `Documents`, or any other relation.

Read projections continue to report incoming/outgoing direction. UI copy is contextual: the same `Requirement SpecifiedBy Specification` row is shown as “由规格说明定义” on the Requirement and “定义需求” on the Specification.

An authoring UI opened from the inverse side may search for the canonical source and submit the single canonical direction. It must not invent an inverse wire value such as `Specifies` merely for display.

## 8. UI Decision

The relation picker must:

1. Start with no selected RelationType. `RelatedTo`, `Documents`, or any other value must not be a default.
2. Filter legal relation choices by source DocumentType and authoring direction.
3. Filter target types and target documents again after relation selection.
4. Put recommended relations for the current DocumentType first, while showing all other legal relations in a clearly separated section.
5. Use contextual outgoing/incoming Chinese labels; wire values remain English.
6. Explain the difference between `Documents`, `References`, and `AppliesTo` with short helper text.
7. Never show removed values as selectable, even if a historical row must temporarily remain readable during migration.

Frontend filtering is usability support. Backend endpoint and DocumentType validation remains authoritative.

## 9. Existing Data Compatibility

A read-only inventory of the repository application database on 2026-08-23 found:

| Relation | Count | Shape |
| --- | ---: | --- |
| `Documents` | 1 | KnowledgeDocument (`KnowledgeArticle`) → System |
| `RelatedTo` | 0 | — |
| `Implements` | 0 | — |
| `Resolves` | 0 | — |
| All other RelationTypes | 0 | — |

The existing `Documents` row conforms to this decision and needs no data change.

This repository database does not prove that every deployed or copied database has zero removed values. Every implementation rollout must run a read-only preflight grouped by relation type and endpoint shape.

If removed or newly invalid rows exist:

- freeze new writes first;
- export the affected IDs and endpoint/title context for review;
- explicitly reclassify each row only when its meaning is known;
- otherwise preserve it until a human decides whether to remove the edge;
- never bulk-map `RelatedTo`, `Implements`, or `Resolves` based only on endpoint types.

## 10. Migration and Contract Decision

A follow-up implementation slice is required.

Recommended safe order:

1. Add backend rejection for new `RelatedTo`, `Implements`, and `Resolves` document edges; hide them in the frontend.
2. Run read-only data inventory against every target database.
3. Reclassify or explicitly remove incompatible rows under human review if any exist.
4. Tighten backend endpoint policy with DocumentType-aware rules.
5. Update the SQLite `relation_type` CHECK to remove the three values.
6. Remove the three enum values, API/frontend unions, decoders, labels, and picker options.
7. Replace broad target lists with the matrix in this decision.
8. Add focused API/migration/frontend tests and one runtime path.

The HTTP route and JSON field shapes do not need redesign. This is a controlled enum/validation correction. Clients that submit removed wire values will receive the existing validation/reference-invalid error family; the implementation report must freeze the exact status/error code it uses.

Existing read responses remain compatible for retained values. No concurrency, KnowledgeStatus, Evidence, lifecycle, or authorization behavior changes.

## 11. Architecture Plan Impact

`docs/design/KNOWLEDGE_CONTENT_DOCUMENT_ARCHITECTURE_PLAN.md` does **not** require a vocabulary amendment. Its “no generic `RelatedTo`” boundary and minimal vocabulary remain authoritative.

This decision is a normative clarification of that plan: it supplies stricter DocumentType/endpoint semantics, contextual labels, data compatibility, and rollout order. The plan must not be changed to retroactively approve `RelatedTo`, `Implements`, or `Resolves`.

## 12. Future Vocabulary Governance

Every new RelationType requires a focused Architecture Decision containing:

- business scenario and why active types cannot express it;
- canonical source, target, and direction;
- allowed endpoint and DocumentType matrix;
- outgoing and incoming Chinese labels;
- overlap analysis against every active type;
- backend validation rule;
- wire/API/frontend compatibility;
- existing-data and CHECK-migration plan;
- focused positive and negative tests;
- Unified View, search, traceability, impact-analysis, and AI/RAG consequences.

A new enum member must never be added first and documented later. Runtime-configurable relation taxonomies remain out of scope.

## 13. Consequences

### Positive

- Requirement → Specification → TestCase coverage remains machine-understandable.
- SOP and Troubleshooting applicability is distinguishable from citation.
- Documents and References retain useful general cases without a universal edge.
- Search, Unified View, impact analysis, and future retrieval receive less semantic noise.
- The existing single-edge persistence and API shape remain small.

### Trade-offs

- Authors sometimes cannot create an edge until they know its meaning.
- UI authoring from an inverse side needs contextual wording and canonical-direction submission.
- The correction requires a CHECK migration and contract cleanup even though the known database has no removed-value rows.

These trade-offs are accepted because graph truth quality is a core product property.

## 14. Gate and Follow-up

**KC-GAP-003 status:** `DECIDED / IMPLEMENTATION REQUIRED`.

It is not closed until the code, CHECK constraint, API validation, frontend picker/labels, affected tests, and runtime behavior conform to this decision.

Recommended next slice:

```text
KC-C02 — Relationship Vocabulary Contract Correction
```

That slice must be limited to the correction described here, produce its own verification report, and stop. This document does not authorize production-code changes by itself.
