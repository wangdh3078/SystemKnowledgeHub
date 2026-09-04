# PORTAL-A01-AMEND-01 TrustSummary Source Compatibility Report

## Result

**PORTAL-A01-AMEND-01 PASS**

The PORTAL-A01 frozen decision now explicitly limits Portal v1 `TrustSummary` to one canonical target selected by `PrimaryTarget` or `ExplicitReference`. `Derived + TrustSummary` is explicitly unsupported and invalid. This closes the PORTAL-B04 contract gap without changing product code, persistence, enums, schema, migrations, canonical trust facts, or TRACE semantics.

## Authority and Baseline

- Baseline: `main` at `771575619e0770d47adf9395ce0ee924b79ec855`, already present on `origin/main`.
- Reviewed PORTAL-A01 and its freeze report, PORTAL-B01/B02/B03 verification reports, TRACE-A01, KC-C01 relationship vocabulary, and the current Evidence, HumanConfirmation, KnowledgeStatus, lifecycle, and revision semantics.
- Existing unrelated `DBDISC_FINAL_R01` report/index worktree changes were preserved and excluded from this amendment.
- PORTAL-A01 remains the original decision document; this task adds a numbered explicit amendment and does not rewrite its historical freeze report.

## Frozen Compatibility

| SourceKind | Target | Decision |
| --- | --- | --- |
| `PrimaryTarget` | `PortalPage.PrimaryTarget` | Allowed and frozen |
| `ExplicitReference` | `PortalPageSection.ReferenceTarget` | Allowed and frozen |
| `Derived` | No valid target recipe | Not supported in Portal v1 |

Each allowed TrustSummary corresponds to exactly one v1 PortalTarget and may return its safe target type/title, independent KnowledgeStatus, direct Evidence count, and direct HumanConfirmation count. A KnowledgeDocument may additionally return the established current-revision confirmation coverage state. Other targets return `confirmationCoverage: null`.

Admin composition must not offer `Derived + TrustSummary`; backend validation returns `400 validation_error`. Portal read and Admin Preview fail closed if persisted invalid data contains that combination.

## No-Aggregation Decision

Portal TrustSummary does not traverse KnowledgeRelation or Trace paths and does not select, combine, deduplicate, total, rank, inherit, or otherwise aggregate trust facts from multiple targets or relations. This preserves the existing rules that KnowledgeStatus, Evidence, HumanConfirmation, relation trust, and revision coverage remain distinct canonical facts or single-subject projections.

RelatedKnowledge and Traceability may expose allowlisted trust signals beside each individual relation or node under their own frozen semantics. They do not produce an aggregate TrustSummary.

## Compatibility Validation

- TRACE-A01 explicitly keeps structural coverage separate from per-node/per-edge trust and forbids composite score, weakest-link status, and inherited confirmation; the amendment is consistent.
- KC-C01 keeps each KnowledgeRelation as one explicit directed semantic fact; TrustSummary performs no relation traversal or inference.
- Evidence and HumanConfirmation remain subject-bound facts and are not copied or mutated.
- REV-A01 confirmation coverage remains the exact four-state projection of one KnowledgeDocument current revision; it is not generalized across heterogeneous targets.
- PORTAL-B01 persistence already stores both enum values independently, so narrowing application compatibility needs no schema or migration change.
- PORTAL-B02/B03 behavior and historical verification reports are unchanged.

## Validation Performed

This was a documentation-only architecture amendment. Product builds, runtime tests, database access, and migration execution were not applicable. Validation comprised authority review, compatibility analysis, final documentation diff review, and `git diff --check`.

## Final Status

```text
PORTAL-A01-AMEND-01 PASS

TRUSTSUMMARY PRIMARY TARGET: FROZEN
TRUSTSUMMARY EXPLICIT REFERENCE: FROZEN
TRUSTSUMMARY DERIVED: NOT SUPPORTED
MULTI-TARGET TRUST AGGREGATION: FORBIDDEN
MIGRATION REQUIRED: NO
PORTAL-B04 BLOCKER: CLOSED

PORTAL-A01-AMEND-01 APPROVED
PORTAL-B04 READY: YES
```
