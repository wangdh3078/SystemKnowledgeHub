# REL-EVIDENCE-A01 — KnowledgeRelation Evidence Dependency and Removal Semantics

Status: **Frozen Design Amendment — APPROVED**
Date: 2026-09-05
Decision: **OPTION A — Dependency Blocker**
Scope: architecture/contract only; implementation follows separately in STABILITY-R01-R01.

## Authority and amendment boundary

This is the explicit user-approved resolution of `RELATION-EVIDENCE-CONTRACT-GAP` recorded in [STABILITY-R01](../reports/STABILITY_R01_CONCURRENCY_STALE_DETAIL_HISTORICAL_INTEGRITY_VERIFICATION_REPORT.md). It supplements the following sources only for Evidence-dependent KnowledgeRelation removal; their other rules remain authoritative:

- [DELETE-A01](DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md): explicit physical relation removal, active endpoint dependencies, eight soft-delete roots, historical preservation, dependency error envelope, and authoritative SQLite writes.
- [KC-C01](KC_C01_RELATIONSHIP_VOCABULARY_ARCHITECTURE_DECISION.md): typed vocabulary, endpoint matrix and direction; no vocabulary or KnowledgeStatus amendment.
- [MVP API Contract](../specifications/System_Knowledge_Hub_MVP_API_Contract.md), C23/C24/C25/Q16, and [Application Use Case Model](../specifications/System_Knowledge_Hub_MVP_Application_Use_Case_Model.md), §4.7 and KnowledgeStatus gates.
- [HumanConfirmation amendment](HUMAN_CONFIRMATION_API_AMENDMENT_REVIEW.md) and [U04 verification](../reports/U04_HUMAN_CONFIRMATION_CURRENT_USER_SNAPSHOT_VERIFICATION_REPORT.md): C25 trusted identity/role hydration and historical provider snapshots supersede the original client-supplied confirmer input. Existing authentication and authorization remain authoritative.
- [VS08 Relationship verification](../reports/VS08_Relationship_Verification_Report.md) and [VS06 Evidence verification](../reports/VS06_Evidence_Verification_Report.md): canonical relation Subjects, ordinary Evidence and HumanConfirmation, explicit status progression.

Earlier frozen documents and STABILITY-R01's historical PARTIAL PASS are not rewritten. Any earlier instruction to remove an endpoint's relation first is now conditional on this blocker: a protected relation cannot be removed to bypass DELETE-A01's active-relation dependency.

## Frozen removal rule

KnowledgeRelation retains **physical + explicit removal** through the existing `DELETE /api/relationships/{id}` use case. For a current relation, the sole Evidence-dependency predicate is existence of at least one canonical Evidence row satisfying:

```text
SubjectType = KnowledgeRelation AND SubjectId = relation.Id
```

This is an existence check over all matching rows. Do not filter by EvidenceType, SubjectDetailKey, confidence, source accessibility, provider/role validity, confirmation completeness, current visibility, or KnowledgeStatus. Do not use a cached detail/list count, Portal projection, status-progression eligibility predicate, or endpoint Evidence as the authority. Same numeric SubjectId with another SubjectType does not protect this relation.

| Current relation | Frozen outcome |
| --- | --- |
| No matching Evidence | Existing explicit physical remove remains allowed, subject to existing authorization and validation. |
| Any ordinary Evidence | Remove blocked. |
| Any `EvidenceType = HumanConfirmation` | Remove blocked by the same predicate. |
| Both ordinary Evidence and HumanConfirmation | Remove blocked. |

HumanConfirmation is a canonical Evidence row, not a second entity or dependency store. Even a legacy confirmation lacking modern provider references remains a blocker when its Subject matches.

Blocked removal must leave the entire Relation and all Evidence unchanged, including IDs, endpoints, description, version, timestamps, KnowledgeStatus and its audit fields, subject bindings, payloads and provider snapshots. No delete, cascade, rebind, copied Subject, orphan marking, automatic Supersede, or replacement relation is permitted.

## Rationale

1. Evidence and HumanConfirmation are historical knowledge grounds.
2. A created Evidence Subject cannot be arbitrarily rebound; C24 only corrects permitted content/provider fields.
3. There is no current Evidence delete/rebind workflow.
4. Relation removal physically erases its canonical identity.
5. Erasing an Evidence Subject relation produces an unresolvable historical orphan.
6. Cascading deletion of Evidence would destroy historical facts.
7. A relation tombstone would change the current relation lifecycle model.
8. An Evidence Subject snapshot would duplicate relation facts and require new schema.
9. Therefore v1 uses the smallest compatible protection: historical grounds exist, so direct relation removal is denied.

## HTTP error contract

Reuse `ApiErrorResponse` and DELETE-A01's frozen dependency mapping: **HTTP 422 / `business_rule_violation`**. The repository does not freeze `dependency_conflict` for this operation; `409 invalid_state` belongs to other state actions. Do not add either code or a second envelope here.

The removal endpoint keeps its Editor authorization policy, existing ID validation, missing-relation 404, and successful **200 `{}`** response. Existing endpoint-target validation policies elsewhere remain unchanged. At the inspected baseline, `RelationshipService.Delete` validates the ID and relation existence but does not independently revalidate source/target; this amendment does not invent a new removal precondition. Existing save atomicity is strengthened by the authoritative transaction below.

Freeze this dependency response, retaining the existing dependency-message format with a relation-specific explanation:

```json
{
  "code": "business_rule_violation",
  "message": "无法删除，仍存在依赖项：该关系已有知识依据或人工确认，不能直接移除。",
  "fieldErrors": null,
  "details": null
}
```

No dependency count or new DTO is introduced. The existing soft-delete blocker DTO supports counts, but this operation only needs existence; its contract intentionally returns none. Never include Evidence body, title/source, provider/user identity, SQL, stack traces, or unbounded dependency details. Authorization remains before any dependency disclosure.

## Atomic dependency check and remove

The subsequent implementation must acquire an authoritative database write transaction **before** loading the current relation and checking Evidence, retain it through physical removal and SaveChanges, then commit. Reuse `SqliteImmediateTransaction` (SQLite immediate write reservation), or an explicitly approved equivalent. An outer transaction is acceptable only if it already provides that authoritative write guarantee; a deferred transaction must not be mistaken for an immediate reservation.

C23 and C25 must continue resolving the current relation Subject and inserting Evidence within the same authoritative write boundary. Current `EvidenceService.Add` and `AddHumanConfirmation` already use `SqliteImmediateTransaction`. The remove path must serialize with those writes across separate requests/connections, not merely within one process.

| Winning committed operation | Required subsequent outcome |
| --- | --- |
| Add Evidence or HumanConfirmation commits first | Remove observes the canonical row and returns the frozen 422 dependency response; relation and Evidence survive. |
| Remove commits first | Add re-resolves the now-missing Subject and rejects under existing `422 reference_invalid`; no Evidence is inserted. |
| Operation fails/cancels before commit | Its writes roll back; no success response or partial persisted state. |

A contention failure cannot justify continuing from a stale precheck. No check outside the transaction followed by later deletion; no static lock, Semaphore, application-wide mutex, ignored database failure, or best-effort orphan cleanup. SQLite itself must enforce serialization. This task does not create new busy/retry/error infrastructure.

## Historical reads and scope

Retaining the Evidence-dependent relation retains the Subject identity required by `EvidenceSubjectResolver.ResolveRelationship`. Existing relation detail, Evidence detail/current/history and HumanConfirmation reads keep their contracts; no `HistoricalTargetResolver` relation tombstone or Evidence Subject snapshot is needed. DELETE-A01 continues to block endpoint soft deletion while a live relation references it, preserving the endpoint context used by the resolver.

The blocker applies only to physical **KnowledgeRelation remove**. System, DatabaseSource, BusinessFunction, DatabaseObject, DatabaseColumn, BusinessRule, Integration and KnowledgeDocument retain DELETE-A01's soft-delete/tombstone/history rules: Evidence/HC alone do not block their soft deletion. A protected relation may continue to block an endpoint through the existing relation-dependency rule; that is not a new direct Evidence blocker on roots.

This prospective invariant does not repair already orphaned legacy data and is not a claim that databases have been scanned. No backfill or data cleanup is authorized.

## Correction and schema boundaries

A relation with Evidence is protected by historical grounds and cannot be directly physically removed in v1, including when a user considers the relation erroneous. Future relation withdrawal, supersession, replacement/history requires a separate explicit design. Do not misuse the existing `Supersedes` vocabulary or status downgrade to evade the blocker.

**MIGRATION REQUIRED: NO.** No table, column, FK, entity, enum or schema changes to KnowledgeRelation, Evidence, HumanConfirmation, KnowledgeStatus, RelationType or EvidenceSubjectType. No soft delete, tombstone, relation history table, Evidence Subject snapshot or generic historical relation store. Existing C24 corrections, C25 identity snapshots, and explicit C22/C26 KnowledgeStatus gates remain unchanged; recording Evidence/HC does not advance status.

Implementation is limited to the existing remove use case/controller dependency result, authoritative transaction and focused tests. No new product workflow, generic framework, schema or historical resolver is required.

## Required implementation acceptance

These are future gates, not tests executed by this design task:

| # | Required proof |
| --- | --- |
| 1 | Relation without Evidence removes successfully via the existing endpoint and returns 200 `{}`. |
| 2 | Ordinary Evidence blocks removal with the exact 422 envelope. |
| 3 | HumanConfirmation alone blocks removal, including legacy/partial provider snapshots. |
| 4 | Both types block; no EvidenceType/SubjectDetailKey/status/visibility filtering bypasses the canonical predicate. |
| 5 | Blocked remove leaves full Relation/Evidence state, versions and KnowledgeStatus unchanged; no information leakage. |
| 6 | Concurrent remove/Add Evidence on independent SQLite connections exercises both commit orders; no orphan. |
| 7 | Concurrent remove/Add HumanConfirmation exercises both commit orders; no orphan. |
| 8 | Existing Relationship detail/read, authorization, invalid-ID and missing-relation behavior remains intact; matching IDs of other SubjectTypes are not blockers. |
| 9 | Existing eight-root historical soft-delete behavior remains intact; live relations still block endpoint deletion. |
| 10 | No cascade, rebind, copied Subject, schema change or migration; failure/cancellation leaves no partial write. |

Tests must use isolated task-owned SQLite/runtime state and the approved repository verification gate. A UI-disabled button or sequential mocked test alone cannot prove database race safety.

## Decision status

**RELATION-EVIDENCE-CONTRACT-GAP: CLOSED (design decision).** Runtime enforcement remains pending STABILITY-R01-R01 implementation and verification. This does not upgrade STABILITY-R01's historical result or authorize automatic implementation.

**REL-EVIDENCE-A01 APPROVED**
**STABILITY-R01-R01 READY: YES**
