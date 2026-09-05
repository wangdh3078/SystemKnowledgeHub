# REL-EVIDENCE-A01 — Relation Evidence Dependency Decision Report

Date: 2026-09-05. **REL-EVIDENCE-A01 PASS — architecture/contract amendment only.**

## Baseline and evidence

Branch: `main`. Starting HEAD: `c2d66b4dbfd33f2898c06d89fb74a0bb579edde6`, `fix: harden concurrency and stale detail safety`. `git status --short`, `git diff` and `git log -6 --oneline` confirm the requested baseline and existing user changes. No reset, clean, revert, branch switch or history rewrite was used.

Existing user work is preserved and excluded from this commit: the DBDISC-FINAL-R01 index row and untracked `docs/reports/DBDISC_FINAL_R01_VERIFICATION_REPORT.md`.

Read AGENTS.md, DOCUMENT_INDEX.md, relevant PROJECT_FILE_MAP documentation entries, STABILITY-R01, DELETE-A01, KC-C01, VS08, VS06, frozen Evidence C23/C24/C25/Q16 and use-case §4.7, the HumanConfirmation amendment/U04 verification, and current Relation/Evidence code. The [approved decision](../design/REL_EVIDENCE_A01_RELATION_EVIDENCE_DEPENDENCY_AND_REMOVAL_DECISION.md) records authority and normative requirements.

Static implementation findings:

- `RelationshipService.Delete` physically removes the loaded relation without an Evidence check or explicit immediate transaction; `RelationshipsController.Delete` uses Editor authorization and returns 200 `{}`, 400 validation, or 404 missing. Enforcement is still pending.
- `EvidenceSubjectResolver.ResolveRelationship` needs the canonical relation and resolved endpoints. Removing a referenced relation loses that Subject context.
- C23/C25 already acquire `SqliteImmediateTransaction` before authoritative Subject resolution and insertion. HumanConfirmation inserts an Evidence row; C24 does not rebind Subjects.
- DELETE-A01's Error Contract and `SoftDeleteApiResponses.Dependencies` use **422 `business_rule_violation`**, not a newly invented dependency code. The amendment reuses `ApiErrorResponse`, the dependency-message format and null detail fields, with no new count DTO.

## Compatibility validation

| Authority / concern | Result and reasoning |
| --- | --- |
| DELETE-A01 | PASS: relation removal stays explicit/physical, now guarded for Evidence; eight-root tombstones and Evidence non-blocking root semantics unchanged. Endpoint relation dependencies remain. |
| KC-C01 | PASS: no enum, direction, typed endpoint matrix or vocabulary change. |
| Evidence C23/C24/Q16 | PASS: Subject identity is retained; no rebind/delete/snapshot or read-contract change. |
| HumanConfirmation C25 | PASS: one canonical Evidence predicate includes confirmations; trusted identity/role and provider snapshots unchanged. |
| KnowledgeStatus | PASS: dependency existence is independent of progression eligibility/status; no automatic advancement, downgrade or Supersede. |
| Concurrency | PASS — design: immediate authoritative check+remove serializes with C23/C25; both commit orders and rollback requirements are explicit future gates. No runtime race test is claimed here. |
| Scope/schema | PASS: documentation only; no product code, database, migration or runtime change. |
| Documentation validation | PASS: decision/report indexed and mapped; relative document links resolved; `git diff --check` passed. |

## Gap closure and limits

`RELATION-EVIDENCE-CONTRACT-GAP` is **CLOSED by design** through OPTION A. STABILITY-R01's original PARTIAL PASS / then-OPEN gap record remains unchanged as historical evidence. This report is its subsequent decision/closure record. The existing runtime removal risk is not claimed fixed: STABILITY-R01-R01 must implement and pass the decision's ten acceptance gates before implementation closure.

No builds, tests, API/browser processes or SQLite connections were started: they are not applicable to a documentation-only amendment. No database/WAL/SHM, runtime configuration, schema, source/test files or Golden UI assets were changed. No task runtime resources or ports require cleanup. No STABILITY-R02, PORTAL-B04 or implementation work was started. Existing unrelated gaps are not reclassified.

## Final decision

| Required status | Result |
| --- | --- |
| REL-EVIDENCE-A01 | PASS |
| RELATION PHYSICAL REMOVE | PRESERVED |
| REMOVE WITHOUT EVIDENCE | ALLOWED |
| REMOVE WITH EVIDENCE | BLOCKED |
| REMOVE WITH HUMAN CONFIRMATION | BLOCKED |
| EVIDENCE CASCADE DELETE | FORBIDDEN |
| EVIDENCE REBIND | FORBIDDEN |
| RELATION TOMBSTONE | NOT INTRODUCED |
| EVIDENCE SUBJECT SNAPSHOT | NOT INTRODUCED |
| ATOMIC DEPENDENCY CHECK + REMOVE | REQUIRED |
| MIGRATION REQUIRED | NO |
| RELATION-EVIDENCE-CONTRACT-GAP | CLOSED — design |
| REL-EVIDENCE-A01 | APPROVED |
| STABILITY-R01-R01 READY | YES |

The BLOCKED/ALLOWED values above are frozen contract decisions, not a claim of changed runtime behavior.

## Delivery

Deliver only this decision, this report, and their DOCUMENT_INDEX / PROJECT_FILE_MAP navigation entries in one commit on `main`: `docs: freeze relation evidence removal dependency`. Git delivery is separate from design validation; the final task response records the actual full commit SHA and push result. Stop after delivery; do not automatically implement the correction.
