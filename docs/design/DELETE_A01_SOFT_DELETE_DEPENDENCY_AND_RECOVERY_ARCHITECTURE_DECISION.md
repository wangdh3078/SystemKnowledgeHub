# DELETE-A01 — Soft Delete, Dependency and Recovery Architecture Decision

> Product: 系统知识中心 / System Knowledge Hub
>
> Capability: Soft Delete
>
> Decision date: 2026-08-27
>
> Implementation status: Not implemented; this document does not start DELETE-B01

## Decision Status

```text
DELETE-A01 APPROVED
Blocking human decisions: NONE
DELETE-B01 implementation status: NOT STARTED
```

This document freezes the first-release soft-delete state, scope, ownership, authorization, dependency, filtering, historical-read, uniqueness, manual-recovery, migration, UX, and delivery sequence. It authorizes no source, test, route, schema, migration, runtime-database, or UI change by itself.

The starting worktree was clean on `main`. The required predecessor checkpoints are present:

- `42dfe54 fix: align product consistency flows`
- `3b40b12 fix: unify datetime display formatting`

The later `32f9989` startup correction is also committed. No concurrent repository-changing task was found.

## Context

The current product persists structured knowledge, KnowledgeDocument current heads and immutable revisions, relationships, Evidence/HumanConfirmation, explicit KnowledgeStatus, trace and impact projections, authentication, and Viewer/Editor/Administrator authorization. Core knowledge objects intentionally had no delete operation in the frozen MVP. Soft Delete is a new post-MVP horizontal capability and therefore needs one contract across canonical writes and every current read projection before implementation starts.

The repository has no existing `IsDeleted`, `DeletedAt`, or `DeletedBy` field, no delete use case for the candidate roots, no recovery surface, and no generic audit/event table. Most pre-document entities only keep a client-compatible creator name/role snapshot. Only `KnowledgeDocument` currently has an authoritative `CreatedByUserId`. These facts make ownership migration and historical-reference behavior part of the foundation rather than a UI-only change.

## Product Requirements

The following product decisions are frozen:

- normal lists, selectors, searches, FTS results, Dashboard, Unified View, Trace, Impact, coverage, and current Supersedes projections exclude deleted objects;
- a deleted endpoint does not satisfy current relationships, coverage, or impact paths;
- active domain dependencies block deletion; deletion never cascades to children or removes relationships automatically;
- historical/audit facts remain, and an allowed deleted reference renders the original name with strikethrough, an explicit `已删除` label, and no navigation;
- the application has no restore API, restore button, recycle bin, deleted-object page, or admin recovery page;
- Administrator may delete every supported type; Editor may delete only an object whose authoritative creator ID equals the current User ID; Viewer may not delete;
- soft-deleted business names may be reused by a different new ID;
- deletion records deletion state, server UTC time, and authoritative current User identity;
- manual database recovery is exceptional, DB-authorized, and never auto-renames, merges, replaces, or rebinds objects;
- Archived and Soft Deleted are independent axes.

## Goals

- Define one unambiguous deletion-state invariant for the real SQLite/EF Core model.
- Identify every first-release deletable root and every blocking or historical dependency.
- Make backend ownership enforcement possible without trusting client actor fields.
- Guarantee consistent exclusion from all current product projections.
- Preserve immutable/historical facts through a narrow tombstone boundary.
- Permit active-only name reuse while making invalid manual recovery fail at the database constraint.
- Sequence implementation into small, verifiable slices without introducing a universal delete framework.

## Non-goals

- Implementing entities, migrations, query filters, endpoints, tests, buttons, dialogs, CSS, or runtime behavior in DELETE-A01.
- Hard deletion of core knowledge objects.
- Application recovery, recycle bin, retention scheduling, purge, legal hold, or bulk delete.
- Generic Repository/UnitOfWork, a universal `DeleteService`, reflection-based target mutation, event bus, or audit framework.
- Changing KnowledgeStatus, System lifecycle, document lifecycle, revision pointers, or publication state during delete/recovery.
- Modifying the repository database.

## Current Repository Inventory

The inventory below is based on the domain classes, `KnowledgeHubDbContext`, EF configurations, current ModelSnapshot/migrations, controllers, application queries/services, tests, and Vue feature/routes. The repository database was not opened or modified.

| Area | Current repository fact | DELETE consequence |
| --- | --- | --- |
| Persistence | One EF Core `KnowledgeHubDbContext`, SQLite, feature-local configurations, app-managed integer `Version` on editable resources | Keep direct DbContext and explicit per-feature use cases; add no persistence abstraction. |
| Authorization | Fallback Viewer; writes require Editor; Administrator is the highest single `AccessLevel`; `ICurrentUserContext` resolves canonical active User | Delete endpoints require Editor policy and perform a second creator-or-admin check using canonical IDs. |
| Creator data | System, BusinessFunction, DatabaseSource, DatabaseObject, BusinessRule, Integration keep `CreatedByName/Role`; DatabaseColumn has no creator; KnowledgeDocument has non-null `CreatedByUserId` and snapshot | Seven legacy root tables need nullable canonical creator IDs; DatabaseColumn also needs a creator display snapshot. Legacy unknown ownership stays unknown. |
| Concurrency | All proposed roots except DatabaseSource have `Version`; JSON `concurrencyToken` is the sole API token contract | Add `Version` to DatabaseSource. Every delete requires the current token and increments version once. |
| Relationships | `KnowledgeRelation` uses controlled polymorphic source/target IDs and is physically removed by its explicit current endpoint | A live relation touching a target blocks target deletion; relation removal remains physical and explicit. |
| Evidence/HC | Evidence is a historical fact; HumanConfirmation is `EvidenceType.HumanConfirmation`; subject IDs are polymorphic | Preserve rows, do not block soft delete, and resolve deleted subjects through tombstones in historical reads. |
| Revision | `KnowledgeDocumentRevision` is immutable, FK `RESTRICT`, unique by document/revision number | Preserve every revision; revisions do not block document soft delete and delete creates no revision. |
| FTS | Standalone `knowledge_documents_fts`; application upserts inside document transactions; raw search joins canonical documents | Remove the row atomically on delete and add canonical `is_deleted = 0` defense-in-depth to raw SQL. Recovery requires a controlled rebuild/upsert. |
| Trace/Impact | Derived direct-DbContext projections over current documents and relations | Global root filters plus endpoint-aware relation filtering must exclude deleted roots/intermediates/targets. |
| Owned children | Technology tags, process steps, and contract fields use explicit replace-set physical removal; known values have an explicit guarded physical remove operation | Retain those concrete semantics and require the user to clear active owned rows before deleting the parent. |
| Current soft delete | No deletion-state fields, query filters, root delete endpoints, or recovery UI | Database and application changes are required in later slices. |

## Candidate Entity Matrix

| Entity | User-created | Authoritative creator now | Current lifecycle | Current unique rules | Material dependencies | Proposed treatment |
| --- | ---: | --- | --- | --- | --- | --- |
| System | Yes | No; name/role snapshot only | `SystemLifecycle` | global `Name` NOCASE | tags, functions, sources, rules, integration endpoints, open UnknownItems, controlled references | Soft-delete root |
| DatabaseSource | Yes | No; name/role snapshot only | None; no KnowledgeStatus | `(SystemId, Name)` and one primary/source | objects, integrations, controlled references | Soft-delete root; add Version |
| BusinessFunction | Yes | No; name/role snapshot only | RewriteStatus + KnowledgeStatus | `(SystemId, Name)` | process steps and controlled references | Soft-delete root |
| DatabaseObject | Yes | No; name/role snapshot only | KnowledgeStatus | `(SourceId, SchemaName, ObjectName)` | columns, database integrations, controlled references | Soft-delete root |
| DatabaseColumn | Yes | None | KnowledgeStatus | `(ObjectId, ColumnName)` and `(ObjectId, Ordinal)` | known values and controlled references | **Soft-delete root added by DELETE-A01** |
| BusinessRule | Yes | No; name/role snapshot only | KnowledgeStatus | `(SystemId, Name)` | controlled references | Soft-delete root |
| Integration | Yes | No; name/role snapshot only | KnowledgeStatus | `(Type, Name, SourceParty, TargetParty)` | contract fields and controlled references | Soft-delete root |
| KnowledgeDocument | Yes | Yes; canonical User ID + snapshot | Draft/Published/Archived + KnowledgeStatus | no title/business-key uniqueness | active relations; revisions and Evidence are historical | Soft-delete root |
| SystemTechnologyTag | Via System edit | Parent-owned | None | `(SystemId, Technology)` | owning System | Not independently soft deleted; existing physical replace/remove; blocks System until cleared |
| BusinessProcessStep | Via Function edit | Parent-owned | None | `(FunctionId, StepOrder)` | owning Function | Not independently soft deleted; existing physical replace/remove; blocks Function until cleared |
| IntegrationContractField | Via Integration edit | Parent-owned | None | field name and ordinal within Integration | owning Integration | Not independently soft deleted; existing physical replace/remove; blocks Integration until cleared |
| ColumnKnownValue | Via Column edit | No | None | `(ColumnId, ValueText)` | Evidence/detail-key and active UnknownItem references checked by current remove use case | Keep guarded explicit physical remove; blocks Column until removed |
| KnowledgeRelation | Yes | Snapshot only | KnowledgeStatus | exact typed directed edge | Evidence and endpoint references | Keep explicit physical `移除关系`; not generic soft delete |
| KnowledgeDocumentRevision | Generated by document content operations | Canonical author ID when known | Immutable history | `(DocumentId, RevisionNumber)` | owning document and author | Never delete; historical, non-blocking |
| Evidence / HumanConfirmation | Yes | Canonical provider ID when available + snapshot | Historical fact | no business uniqueness | subject reference | Preserve; non-blocking |
| UnknownItem and children | Yes / workflow-generated | Snapshot only | Open/Investigating/ConclusionConfirmed/Closed | global ItemCode and child invariants | System and controlled targets | Keep Close/Reopen workflow; active references may block roots, closed/applied facts are historical |
| User / LoginIdentity / KnowledgeRole | Administrative | N/A | Active/Inactive | current identity/account rules | authentication and audit FKs | Keep deactivation/current management; not soft deleted |

## Soft-delete Scope

The first release has eight roots:

```text
System
DatabaseSource
BusinessFunction
DatabaseObject
DatabaseColumn
BusinessRule
Integration
KnowledgeDocument
```

`DatabaseColumn` is added to the seven proposed candidates because it is a first-class route/detail, Evidence/KnowledgeStatus/relationship target, has its own concurrency token, and is an active child of DatabaseObject. If it were only a blocker with no delete capability, users could never satisfy the prerequisite for deleting any populated DatabaseObject.

### Delete scope matrix

| Entity | Soft delete | Delete action | Ownership check | Dependency check | Active-name reuse | Notes |
| --- | :---: | :---: | :---: | :---: | :---: | --- |
| System | Yes | Yes | Yes | Yes | Yes | Lifecycle remains unchanged. |
| DatabaseSource | Yes | Yes | Yes | Yes | Yes | Add Version/current token surface. |
| BusinessFunction | Yes | Yes | Yes | Yes | Yes | Process steps must first be cleared explicitly. |
| DatabaseObject | Yes | Yes | Yes | Yes | Yes | Columns must first be independently deleted. |
| DatabaseColumn | Yes | Yes | Yes | Yes | Yes | Known values must first be explicitly removed. |
| BusinessRule | Yes | Yes | Yes | Yes | Yes | No child aggregate rows. |
| Integration | Yes | Yes | Yes | Yes | Yes | Contract fields must first be cleared explicitly. |
| KnowledgeDocument | Yes | Yes | Yes | Yes | Already allowed | No revision created; revisions retained. |

## Explicitly Excluded Entities

- `KnowledgeRelation`: keep the existing explicit physical remove operation. An active relation blocks deletion of either endpoint and is never silently cleaned up.
- `KnowledgeDocumentRevision`: immutable and never deleted or soft deleted.
- `Evidence`, including HumanConfirmation: retained historical/audit facts; not generic delete roots.
- `UnknownItem`: keep Close/Reopen; no delete capability in this release.
- `User`, `LoginIdentity`, `LocalLoginCredential`, `KnowledgeRole`, and assignments: retain Active/Inactive and administrative lifecycle.
- Owned tags/steps/contract fields and known values: retain current explicit physical correction semantics.
- Findings, Resolution, KnowledgeUpdate, UnknownItemActivity, and UnknownItemTarget: retain workflow/history semantics.

## Archived vs Deleted

Archived is a normal business lifecycle state. Deleted is an independent administrative removal state.

```text
LifecycleStatus = Archived, IsDeleted = false  // current retained document, current detail allowed
LifecycleStatus = Archived, IsDeleted = true   // administratively removed, normal detail is 404
```

Soft delete never changes `LifecycleStatus`, `ArchivedAt`, System `Lifecycle`, RewriteStatus, UnknownItem status, or User active state. Manual recovery never maps Archived to Draft or changes another lifecycle.

## Deletion State Model

Each of the eight roots gets explicit fields:

| .NET property | SQLite column | Type / nullability | Semantics |
| --- | --- | --- | --- |
| `IsDeleted` | `is_deleted` | INTEGER NOT NULL, default `0` | Sole authoritative current deletion state. |
| `DeletedAt` | `deleted_at` | TEXT NULL | Server UTC deletion time for the most recent delete. |
| `DeletedByUserId` | `deleted_by_user_id` | INTEGER NULL | Canonical application User; FK `users.id`, `RESTRICT`. |
| `DeletedByDisplayName` | `deleted_by_display_name` | TEXT NULL | Stable deletion-time display snapshot. |

Invariant:

- when `IsDeleted = 1`, all three deletion-audit values are non-null and the display snapshot is nonblank;
- when `IsDeleted = 0`, all three may be null (never deleted) or all three may remain populated (manually recovered);
- mixed partial audit triples are invalid;
- `IsDeleted`, not nullability of the audit values, decides current visibility;
- delete changes only these fields and increments `Version`; ordinary domain data and `UpdatedAt` remain unchanged.

Keeping the audit triple after manual recovery preserves the only available deletion fact without introducing a speculative audit/event system. A later delete may replace this “most recent deletion” triple; complete multi-cycle audit history is outside this release.

## Audit Fields

`DeletedAt` is produced by the server as a UTC `DateTimeOffset` (zero offset) and stored with the repository's existing SQLite TEXT convention. Client time is never accepted.

`DeletedByUserId` is the authoritative actor identity. The display-name snapshot keeps history readable after a User rename. User deactivation does not delete the User row, and `RESTRICT` prevents a future physical cleanup from orphaning delete attribution.

The client cannot write any deletion field through a general update contract. Only the explicit delete use case can set them.

## CreatedBy / Ownership Model

Ownership means creator provenance, not last editor, current assignee, name equality, KnowledgeRole, department, or UI actor text.

- KnowledgeDocument keeps its current non-null `CreatedByUserId` and snapshot.
- System, DatabaseSource, BusinessFunction, DatabaseObject, BusinessRule, and Integration add nullable `CreatedByUserId` FK `users.id ON DELETE RESTRICT` while retaining the existing `CreatedByName/Role` snapshots.
- DatabaseColumn adds nullable `CreatedByUserId` plus nullable `CreatedByDisplayName`; new post-migration rows populate both from `ICurrentUserContext`.
- All new rows after the migration must take creator ID/name from the authenticated canonical current User. Request `actor` fields remain presentation/backward-compatibility inputs only and never establish ownership.

No common entity base class is introduced; the repeated fields remain explicit in each mapped entity.

## Authorization Matrix

| Actor | Delete own object | Delete others' object | Restore |
| --- | :---: | :---: | :---: |
| Anonymous / invalid session | Deny | Deny | None |
| Viewer | Deny | Deny | None |
| Editor | Allow when canonical creator ID equals current User ID | Deny | None |
| Administrator | Allow | Allow | None |
| DB operator | N/A product API | N/A product API | Manual database procedure only |

Every delete endpoint has the existing Editor-or-higher policy. The application use case then loads the current canonical User and the target creator ID. Administrator bypasses only the ownership comparison, not existence, concurrency, dependency, integrity, or antiforgery checks.

## Legacy Creator Handling

Existing non-document rows cannot be authoritatively mapped from `CreatedByName` to User. Display names are mutable and need not be unique. Migration therefore does not guess or backfill IDs.

```text
legacy CreatedByUserId = NULL
Editor delete = DENY
Administrator delete = ALLOW, subject to every other guard
```

This is a safe authorization default and requires no Product-owner exception. A future separately approved data-curation process may assign proven creator IDs, but name matching alone is insufficient evidence.

## Delete API Semantics

Each root owns a concrete semantic endpoint, for example:

```text
DELETE /api/systems/{id}
DELETE /api/database-sources/{id}
DELETE /api/business-functions/{id}
DELETE /api/database-objects/{id}
DELETE /api/database-columns/{id}
DELETE /api/business-rules/{id}
DELETE /api/integrations/{id}
DELETE /api/knowledge-documents/{id}
```

The request carries the existing JSON `concurrencyToken`; no `PATCH isDeleted=true`, restore endpoint, second ETag mechanism, or generic `/api/objects/{type}/{id}` route is introduced. Success returns `204 No Content`.

The use case performs: validate route/token; resolve canonical current User; load an active target and creator; enforce role/ownership; validate target-specific dependencies; mark state and audit; increment Version; update FTS where applicable; save and commit atomically.

Deleted or unknown route resources return `404 not_found`. This intentionally does not disclose tombstone state through normal product APIs.

## Optimistic Concurrency

Delete uses the same app-managed integer Version encoded into the current JSON `concurrencyToken`.

- DatabaseSource gains Version and a read/manage projection that returns its token.
- A valid delete increments Version exactly once.
- A stale token against an active object returns `409 conflict`.
- A writer that reloads after deletion sees `404`; a stale writer may receive 404 or 409 depending on which atomic operation won, but never succeeds and never clears `IsDeleted`.
- Relationship creation, Evidence/HC, KnowledgeStatus, lifecycle, publish, revision restore, UnknownItem Apply, and all ordinary edits resolve only active targets inside their transaction.
- Delete and dependency creation must be serialized/revalidated within the same short SQLite write boundary. DELETE-B02 must exercise both interleavings; an active dependency and a deleted target may not coexist because of a race.

No `ETag`, `If-Match`, timestamp comparison, or automatic retry is added.

## Post-delete Mutation Rules

| Mutation form | Deleted target result |
| --- | --- |
| Route-root edit/lifecycle/publish/revision restore/delete | `404 not_found` |
| Body reference, relationship endpoint, Evidence/HC subject, selector target | `422 reference_invalid` or the existing feature-equivalent invalid-reference result |
| Stale token while target is still active but Version changed | `409 conflict` |
| Historical read explicitly allowed below | Tombstone or immutable revision projection only |

No normal mutation can set `IsDeleted = false`. No save operation can resurrect a row.

## Dependency Definition

Dependencies have three classes:

1. **Active domain dependency** — a current child or reference whose continued behavior requires the target. It blocks deletion.
2. **Historical/audit reference** — an immutable or completed fact retained to explain history. It does not normally block and uses tombstone presentation.
3. **Derived projection** — Dashboard, search, Unified View, Trace, Impact, coverage, selectors, and summaries. It never blocks; it recalculates from non-deleted canonical truth.

For workflow references, “active” is state-sensitive:

- UnknownItem reference blocks while the item is not Closed;
- `KnowledgeUpdate.Status = Proposed` blocks;
- Closed UnknownItem context and Applied KnowledgeUpdate snapshots are historical;
- all current KnowledgeRelation rows are active until explicitly removed.

Only non-deleted dependency roots count as active. A previously soft-deleted child does not block its parent.

## Dependency Matrix

| Target | Dependency | Active blocks? | Historical blocks? | How the user resolves |
| --- | --- | :---: | :---: | --- |
| System | SystemTechnologyTag | Yes | N/A | Clear Technology through existing System edit. |
| System | BusinessFunction | Yes | No when function is deleted | Delete each function after its blockers are resolved. |
| System | DatabaseSource (and therefore its object tree) | Yes | No when source is deleted | Resolve/delete database descendants, then delete source. |
| System | BusinessRule | Yes | No when rule is deleted | Delete rules explicitly. |
| System | Integration source/target endpoint | Yes | No when integration is deleted | Edit endpoint away where valid or delete Integration. |
| System | UnknownItem `SystemId` or target | Yes while not Closed | No when Closed | Close after completing workflow or retarget where contract permits. |
| System | KnowledgeRelation endpoint | Yes | No row after explicit removal | Remove relation explicitly. |
| BusinessFunction | BusinessProcessStep | Yes | N/A | Replace process-step collection without those rows. |
| BusinessFunction | Relation / non-Closed UnknownItem target / Proposed KnowledgeUpdate | Yes | Completed records: No | Remove relation; complete/retarget workflow. |
| DatabaseSource | DatabaseObject | Yes | No when object is deleted | Delete objects after columns and references are resolved. |
| DatabaseSource | Integration `DatabaseSourceId` | Yes | No when integration is deleted | Edit/remove dependency or delete Integration. |
| DatabaseSource | Relation / non-Closed UnknownItem target / Proposed KnowledgeUpdate | Yes | Completed records: No | Remove relation; complete/retarget workflow. |
| DatabaseObject | DatabaseColumn | Yes | No when column is deleted | Delete columns after known values/references are resolved. |
| DatabaseObject | Integration `DatabaseObjectId` | Yes | No when integration is deleted | Edit/remove dependency or delete Integration. |
| DatabaseObject | Relation / non-Closed UnknownItem target / Proposed KnowledgeUpdate | Yes | Completed records: No | Remove relation; complete/retarget workflow. |
| DatabaseColumn | ColumnKnownValue | Yes | N/A | Use existing guarded known-value removal. |
| DatabaseColumn | Relation / non-Closed UnknownItem target / Proposed KnowledgeUpdate | Yes | Completed records: No | Remove relation; complete/retarget workflow. |
| BusinessRule | Relation / non-Closed UnknownItem target / Proposed KnowledgeUpdate | Yes | Completed records: No | Remove relation; complete/retarget workflow. |
| Integration | IntegrationContractField | Yes | N/A | Replace contract field collection without those rows. |
| Integration | Relation / non-Closed UnknownItem target / Proposed KnowledgeUpdate | Yes | Completed records: No | Remove relation; complete/retarget workflow. |
| KnowledgeDocument | Any KnowledgeRelation endpoint, including SpecifiedBy/VerifiedBy/Supersedes | Yes | No row after explicit removal | Remove relation explicitly. |
| Any root | Evidence / HumanConfirmation | No | No | Preserved; no action needed. |
| KnowledgeDocument | KnowledgeDocumentRevision | No | No | Preserved; no action needed. |
| Any root | Dashboard/search/selector/Trace/Impact/Unified View row | No | No | Recomputed automatically; no cleanup action. |

Dependency checks return counts, not unbounded entity lists. They must use endpoint and status indexes already present and add only measured indexes in B01/B02.

## System Dependency Rules

System deletion is denied while any of these are active: Technology tags, BusinessFunctions, DatabaseSources, BusinessRules, Integrations that name the System as source or target, non-Closed UnknownItems in the System context, KnowledgeRelation endpoints, non-Closed UnknownItem targets, or Proposed KnowledgeUpdates.

DatabaseObjects are reached through their DatabaseSource ownership; the source cannot be deleted until its active objects are gone. This prevents an active indirect object tree beneath a deleted System without needing a cascade.

Evidence/HumanConfirmation and completed UnknownItem/KnowledgeUpdate facts are historical and do not block. The System row remains as the stable ID and tombstone name for those facts.

## Relationship Dependency Rules

Every current `KnowledgeRelation` touching a candidate root is an active dependency, including `SpecifiedBy`, `VerifiedBy`, `Supersedes`, `Documents`, `References`, and all structured types. The user must execute the existing relationship remove use case first.

Soft delete never removes, rewrites, redirects, or rebinds a relation. Creating a new same-name object with a new ID does not affect a historical old ID. If legacy/manual data contains a relation to a deleted endpoint, current projections exclude that edge and historical contexts use a tombstone.

## Historical / Audit Preservation

- Evidence and HumanConfirmation rows remain unchanged.
- KnowledgeDocument revisions remain unchanged and retain author snapshots/FKs.
- Closed UnknownItem context, Applied KnowledgeUpdate before/after snapshots, Findings, Resolution, and Activity remain unchanged.
- Deletion does not generate a KnowledgeDocument revision because it is aggregate administrative metadata, not semantic content.
- No historical row becomes a current dependency merely because it contains the old ID.

## Tombstone Projection

Allowed historical contexts resolve only:

```text
Id
TargetType
DisplayName
IsDeleted = true
IsNavigable = false
```

DisplayName is derived from the retained canonical name/title fields (`System.Name`, `SchemaName.ObjectName`, `Title`, and equivalent type-specific rules). A small closed-type historical resolver may use `IgnoreQueryFilters`, but it must project only these fields and must not become a generic mutable KnowledgeObject resolver.

UI contract:

```text
~~原名称~~  [已删除]
```

The explicit label is required in addition to strikethrough. The text is not a link, has no click/keyboard navigation, and never opens current Detail.

## Deleted Detail Read Boundary

Normal list/detail/Trace/Impact/Unified View endpoints treat a deleted root as absent and return 404 where a route resource is required. No full deleted object response is available.

Two narrow historical exceptions preserve existing facts:

- Evidence/HC and completed workflow/audit reads may include the tombstone projection above.
- Existing KnowledgeDocument revision list/detail reads remain available to authenticated Viewer-or-higher when directly addressed or reached from an allowed historical context. They use a controlled owning-document lookup, return immutable revision snapshots plus tombstone owner identity, and do not expose a mutable current deleted-document detail. Revision restore remains 404/denied.

This preserves revisions rather than hiding them because the current head was deleted, without creating a deleted-object browser.

## List Filtering

All normal root lists use the default non-deleted query. Required-parent invariants also prevent active children under deleted parents. Pagination totals, empty states, counts, and sort sets are computed after deletion filtering.

## Selector Filtering

System options, relationship target search, Knowledge Target search, BusinessRule System options, Integration endpoint/database options, Global Create dependent selectors, and every other authoring lookup exclude deleted targets. A submitted deleted ID is rejected server-side even when stale UI options still show it.

## Global Search Filtering

Structured LIKE search for Systems, BusinessFunctions, DatabaseObjects, DatabaseColumns, BusinessRules, and Integrations uses default global filters. UnknownItem remains governed by its workflow but any displayed current target/system metadata must not turn a deleted object into a navigation target.

## Query Filtering Matrix

| Surface | Deleted objects | Required implementation change |
| --- | --- | --- |
| Normal lists and pagination totals | Hidden | EF global filters on all eight roots; calculate totals after filtering. |
| Normal detail routes | 404 | Default filtered root lookup; no fallback to a full deleted DTO. |
| Global/feature create selectors | Hidden | Filter options and reject stale submitted IDs server-side. |
| Structured Global Search | Hidden | Default filters on each typed DbSet and parent navigation. |
| KnowledgeDocument FTS | Hidden | Atomic FTS row removal plus raw canonical `d.is_deleted = 0`. |
| Dashboard | Excluded | Active-only counts, attention items, relation summaries, and recent activity. |
| System Unified Knowledge View | Excluded | Active-only section queries/counts; deleted System root is 404. |
| Trace / coverage | Excluded | Drop deleted root/child/intermediate/edge endpoint and recompute missing links. |
| Impact | Excluded | Drop paths with a deleted root, intermediate, or structured target. |
| Current Supersedes lineage | Excluded | Exclude the deleted other endpoint from current lineage. |
| Ordinary mutations | Unavailable | Route root 404; body reference invalid; never resurrect. |
| Historical Evidence/HC/workflow | Tombstone | Minimal name/type/ID/deleted projection, explicit label, no navigation. |
| Immutable revision list/detail | Preserved historical read | Controlled deleted-owner lookup; no current mutable detail or restore. |

## FTS Strategy

KnowledgeDocument soft delete performs both defenses:

1. delete the matching `knowledge_documents_fts` row inside the same transaction that marks the canonical row deleted;
2. add `d.is_deleted = 0` to both raw FTS result and count SQL.

`KnowledgeDocumentSearchIndex.Rebuild` indexes active documents only. If the FTS mutation fails, the canonical deletion transaction rolls back. If stale/legacy FTS content exists, the canonical join predicate still prevents a user-visible hit.

## Dashboard Filtering

Dashboard root counts, KnowledgeStatus totals, needs-attention counts, recent activity, missing-description counts, and relation-based summaries exclude all deleted roots and any relation path with a deleted endpoint. Historical audit counts may include deleted rows only after a separately named and designed metric; no current metric has that designation.

## Unified Knowledge View Filtering

System Unified Knowledge View returns 404 for a deleted System and excludes deleted BusinessFunctions, DatabaseObjects, BusinessRules, Integrations, and KnowledgeDocuments. Its counts and five-item sections use the same active truth. Evidence remains historical but is not surfaced through a deleted current System view.

## Trace Filtering

- Deleted root: current Trace endpoint returns 404.
- Deleted Specification/TestCase child or intermediate: excluded from tree and coverage; it cannot satisfy `hasSpecification` or test-definition coverage.
- Deleted relation endpoint in legacy/manual data: edge excluded rather than treated as current `reference_invalid` solely because the endpoint is deleted.
- Historical trace/audit context: tombstone only.

Example: if the only `Requirement --SpecifiedBy--> Specification` endpoint becomes deleted through legacy/manual inconsistency, current Requirement coverage reports `MissingSpecification = true`.

## Impact Filtering

Deleted roots, structured targets, and intermediate Requirement/Specification/TestCase documents are excluded. A path cannot continue through a deleted node. Target metadata loading must drop candidates whose canonical target is deleted, and path validation must distinguish “deleted/excluded” from malformed active data.

## Supersedes Filtering

Current direct incoming/outgoing Supersedes lineage excludes a deleted other endpoint. An allowed historical view may show its tombstone, but deleted lineage does not participate in current Trace and never causes same-name rebinding.

## KnowledgeStatus Independence

Soft delete and manual recovery never change KnowledgeStatus, reason, changed time, actor snapshot, Evidence gate, or HumanConfirmation coverage. A deleted Confirmed object remains `Confirmed + IsDeleted` in storage and is absent from current status projections.

## Lifecycle Independence

Soft delete never changes System Lifecycle, document Draft/Published/Archived, RewriteStatus, UnknownItem workflow, or User active state. Recovery preserves the stored lifecycle exactly.

## Revision Preservation

KnowledgeDocument soft delete:

- creates no revision;
- does not change `CurrentRevisionNumber`, `LatestPublishedRevisionNumber`, `PublishedAt`, `ArchivedAt`, revision lineage, or revision contents;
- keeps revision list/detail available only through the historical boundary;
- denies content save, lifecycle change, publish, KnowledgeStatus mutation, and revision restore while deleted.

## Evidence Preservation

Evidence remains a historical fact and normally does not block deletion. Direct Evidence detail may resolve a deleted subject through the minimal tombstone. It must not call the normal full subject Detail or expose deleted mutable fields.

## HumanConfirmation Preservation

HumanConfirmation remains an Evidence row, including any KnowledgeDocument revision-number snapshot. It is not deleted, does not block, does not change KnowledgeStatus, and uses the same tombstone rule for a deleted subject.

## Name Reuse

Identity is always the numeric ID. A soft-deleted business name may be reused by a new row with a new ID. Old references continue to point to the old ID and are never rebound by string equality.

```text
soft delete old ID 10 / name X
create new ID 27 / name X  => allowed
historical references to ID 10 => remain ID 10
manual restore ID 10 while ID 27 active => database uniqueness failure until operator resolves it
```

KnowledgeDocument titles already have no database uniqueness rule, so the deletion feature does not add one.

## Unique Constraint Inventory

| Entity | Business key | Current DB implementation | Soft-delete conflict | Required active-only strategy | Restore conflict |
| --- | --- | --- | --- | --- | --- |
| System | `Name` NOCASE | unique EF index | Deleted name still blocks reuse | unique partial index `WHERE is_deleted = 0` | Blocks restore if active same name exists |
| DatabaseSource | `(SystemId, Name)` NOCASE | unique EF index | Deleted source still blocks reuse | filtered unique active rows | Blocks conflicting restore |
| DatabaseSource | one primary per System | partial unique `WHERE is_primary = 1` | Deleted primary still occupies slot | change filter to `is_primary = 1 AND is_deleted = 0` | Blocks restoring a second active primary |
| BusinessFunction | `(SystemId, Name)` NOCASE | unique EF index | Deleted function blocks reuse | filtered unique active rows | Blocks conflicting restore |
| DatabaseObject | `(SourceId, SchemaName, ObjectName)` NOCASE | unique EF index | Deleted object blocks reuse | filtered unique active rows | Blocks conflicting restore |
| DatabaseColumn | `(ObjectId, ColumnName)` NOCASE | unique EF index | Deleted column blocks reuse | filtered unique active rows | Blocks conflicting restore |
| DatabaseColumn | `(ObjectId, OrdinalPosition)` | unique EF index | Deleted column occupies ordinal | filtered unique active rows | Blocks conflicting restore |
| BusinessRule | `(SystemId, Name)` NOCASE | unique EF index | Deleted rule blocks reuse | filtered unique active rows | Blocks conflicting restore |
| Integration | `(Type, Name, SourceParty, TargetParty)` NOCASE fields | unique EF index | Deleted integration blocks reuse | filtered unique active rows | Blocks conflicting restore |
| KnowledgeDocument | None | no title unique constraint | None | no new uniqueness | No title conflict generated by this capability |

Owned-child and excluded-entity unique indexes remain unchanged because those rows retain explicit physical correction/removal or immutable semantics.

## Active-only Uniqueness Strategy

SQLite partial unique indexes are the selected mechanism. The repository already uses filtered unique indexes for DatabaseSource primary selection and nullable User identity fields, so provider and project conventions are proven.

The current root uniqueness rules are EF-created indexes, not inline table `UNIQUE(column)` constraints. They can be dropped and recreated with `HasFilter("is_deleted = 0")`; uniqueness migration does not itself require table rebuild. Table rebuild may still be generated for added check constraints/FKs and must be reviewed separately.

Application duplicate checks must use default active filters. Database constraints remain authoritative under concurrency.

## Recovery Conflict Semantics

Manual restore never auto-renames, merges, replaces the newer object, changes another row, or rebinds references. The partial unique constraint deliberately rejects `IsDeleted = 0` when an active conflicting business key exists. The operator must stop, choose an explicit data correction outside the recovery statement, and revalidate.

Required-parent invariants also apply: a DatabaseColumn cannot be restored under a deleted DatabaseObject; a DatabaseObject cannot be restored under a deleted DatabaseSource; a source/function/rule cannot be restored under a deleted System.

## Manual Database Recovery Contract

Manual recovery is an exceptional offline/controlled database operation, not a product workflow.

1. Back up the SQLite database and ensure exclusive/controlled maintenance access.
2. Identify the exact row by object type and numeric ID; never use display name as identity.
3. Inspect current `is_deleted`, delete audit, Version, required parent state, and current same-key active rows.
4. Inspect active dependencies and confirm that restoring the object will not produce an invalid active graph.
5. Resolve uniqueness conflicts manually; do not rename/merge/replace as an implicit part of recovery.
6. In one transaction, set only `is_deleted = 0` and increment `version = version + 1`. Keep `deleted_at`, `deleted_by_user_id`, and `deleted_by_display_name` unchanged as the most recent deletion audit. Do not alter lifecycle, KnowledgeStatus, content, revision pointers, publication fields, creator, or IDs.
7. For KnowledgeDocument, execute the controlled active-document FTS rebuild/upsert described below.
8. Run `PRAGMA foreign_key_check`, `PRAGMA integrity_check`, and type-specific uniqueness/dependency queries before commit/return to service.
9. Verify list/detail, selector, search, Dashboard, Unified View, Trace, and Impact projections appropriate to the restored type.

No application guarantee protects an operator who bypasses this runbook.

## Recovery Matrix

| Object type | Manual restore fields | Uniqueness validation | Dependency / parent validation | Derived data rebuild | Special notes |
| --- | --- | --- | --- | --- | --- |
| System | `is_deleted=0`, `version+1`; retain delete audit | Active global Name | No invalid active child/reference state; current lifecycle retained | Query-time projections only | Do not change Retired/other lifecycle. |
| DatabaseSource | Same | Active `(SystemId, Name)` and one active primary | Owning System must be active; integration/object rules valid | Query-time projections only | Retain `IsPrimary`; resolve primary conflict manually. |
| BusinessFunction | Same | Active `(SystemId, Name)` | Owning System active; workflow/reference rules valid | Query-time projections only | Process steps remain exactly as stored. |
| DatabaseObject | Same | Active `(SourceId, Schema, ObjectName)` | DatabaseSource and its System active; integration/reference rules valid | Query-time projections only | Columns remain independently deleted/active as stored. |
| DatabaseColumn | Same | Active name and ordinal within Object | DatabaseObject/source/System active; reference rules valid | Query-time projections only | Known values remain exactly as stored. |
| BusinessRule | Same | Active `(SystemId, Name)` | Owning System active; reference rules valid | Query-time projections only | KnowledgeStatus unchanged. |
| Integration | Same | Active composite type/name/parties | At least one required System active; database references active and type-valid | Query-time projections only | No endpoint or party auto-rewrite. |
| KnowledgeDocument | Same | No title uniqueness; still validate ID and state | Historical/current relation consistency reviewed | Controlled FTS rebuild/upsert required | Preserve lifecycle, content, revisions, publication/status pointers. |

## FTS / Derived Data Recovery

KnowledgeDocument recovery must run an application-owned offline FTS rebuild or single-document upsert using the same Unicode/Markdown normalization as `KnowledgeDocumentSearchIndex`. Direct ad-hoc insertion of raw body text is not accepted. DELETE-B03 must provide/document the controlled maintenance invocation without exposing a product recovery API.

Other current projections are query-time derived and require no stored rebuild. After recovery, they must be verified against current active relationships and required parents.

## SQLite Migration Strategy

Migration work is additive for columns but replacement-based for active-only indexes:

- add deletion columns with `is_deleted = 0` for every existing row;
- add nullable creator IDs without invented backfill; add DatabaseColumn creator snapshot; add DatabaseSource Version default `1`;
- add User FKs with `RESTRICT` and deletion audit consistency checks;
- drop nine affected unique indexes and recreate them as SQLite partial unique indexes;
- retain collations and exact composite key order;
- update FTS migration/rebuild SQL to index active documents and clean any deleted entries;
- update the EF ModelSnapshot in implementation;
- review generated SQLite rebuild operations, copy/default behavior, foreign keys, indexes, and FTS virtual table survival rather than assuming SQL Server-style `ALTER TABLE` support;
- run row-count/content preflight/postflight, `foreign_key_check`, `integrity_check`, and repository-database protection checks on isolated copies/temporary databases.

The checked-in repository database must never be migrated by DELETE-A01 or by migration tests.

## Migration Groups

```text
Database Change Required: YES
Migration Required: YES
```

| Group | Tables / projection | Planned change |
| --- | --- | --- |
| 1. Creator/concurrency foundation | systems, database_sources, business_functions, database_objects, database_columns, business_rules, integrations | Nullable canonical creator IDs; DatabaseColumn snapshot; DatabaseSource Version |
| 2. Deletion state/audit | eight soft-delete root tables, users FK target | IsDeleted, DeletedAt, DeletedByUserId, DeletedByDisplayName, checks/FKs |
| 3. Active uniqueness/indexes | systems, database_sources, business_functions, database_objects, database_columns, business_rules, integrations | Replace nine unique indexes; add only justified active/dependency query indexes |
| 4. FTS/current projection cleanup | knowledge_documents_fts and rebuild SQL | Remove deleted entries, active-only rebuild, raw-query defense |
| 5. Model synchronization | ModelSnapshot | Exact final EF model; no runtime data artifact |

Existing rows initialize `IsDeleted = false`; delete audit is NULL. Existing KnowledgeDocument creator IDs remain authoritative. Other existing creator IDs remain NULL. Therefore:

```text
CreatedBy backfill required: NO unsafe backfill
Legacy unknown provenance retained: YES
```

## Query Filter Architecture

Use EF Core global query filters for the eight roots, declared explicitly in each entity configuration. This is the default current-product safety net for lists, details, navigation options, validators, Dashboard, Unified View, Trace, and Impact.

Controlled exceptions:

- tombstone projection;
- immutable KnowledgeDocument revision list/detail owner resolution;
- dependency/maintenance diagnostics that explicitly need deleted rows;
- migration and focused tests.

Every `IgnoreQueryFilters` call must be locally visible, type-specific, read-only unless it is the explicit delete use case, and covered by a historical-boundary test. No broad repository helper or request flag may disable filters. Raw SQL, especially FTS, must contain its own active predicate because EF filters do not apply.

No `ISoftDeletable` marker/base type is selected. Eight explicit configurations are small, make scope reviewable, and avoid accidentally enrolling historical entities.

## Query / Index Performance Boundary

Global `is_deleted = 0` predicates affect hundreds of current typed DbContext references across feature queries/services. B01/B03 must verify representative `EXPLAIN QUERY PLAN` output for:

- name/business-key duplicate lookups;
- paged Systems/Functions/DatabaseObjects/KnowledgeDocuments lists;
- DatabaseColumn parent/ordinal lookups;
- relation source/target blocker counts;
- Integration source/target/database dependency counts;
- non-Closed UnknownItem and Proposed KnowledgeUpdate blockers;
- Dashboard aggregate/recent activity;
- FTS canonical join;
- Trace and largest allowed Impact paths.

Prefer partial active indexes that match real predicates. Do not add a blanket standalone `is_deleted` index to every table without a measured plan benefit.

## Delete Confirmation UX Contract

Supported detail/manage surfaces expose a danger action `删除` only when the type is deletable. Basic confirmation copy:

```text
确认删除“{原名称}”？
删除后将从列表、搜索及当前知识视图中隐藏。
系统不提供页面恢复功能。
```

Do not say `永久删除`. DatabaseSource uses its nearest current management surface because it has no standalone detail route; DatabaseColumn uses the existing detail drawer. B04 may refine compact placement, not semantics.

## Dependency Blocking UX Contract

HTTP 422 blocker details render:

```text
无法删除，仍存在依赖项
```

Then show bounded category/count rows, for example `业务功能 3`, `集成关系 2`, `知识关系 4`. At most eight categories are returned, with no raw unbounded object list. A category may include one safe existing navigation target when a suitable current list/filter exists; otherwise instruct the user to resolve the category in its existing surface and retry.

## No Recycle Bin Decision

First release has no recycle bin, deleted-object list, restore action, restore route, deleted search mode, admin recovery page, or bulk recovery. This is a frozen product boundary, not deferred B04 polish.

## Security

- Backend canonical current User, not frontend button visibility or request actor, is the security boundary.
- Deleted normal details return 404 to avoid state disclosure.
- Tombstones expose only ID/type/original display name/deleted state and only in allowed authenticated historical contexts.
- Administrator ownership bypass does not bypass dependency or concurrency checks.
- Antiforgery applies to DELETE as to every authenticated cookie write.
- Deleted rows retain creator/deleter User FKs with `RESTRICT`; deactivation does not corrupt history.
- Logs/errors must not include deleted document body, Evidence payloads, or unbounded blocker data.

## Error Contract

Reuse the existing envelope and codes:

| Condition | HTTP / code | Details |
| --- | --- | --- |
| Invalid ID/token shape | 400 `validation_error` | field errors |
| Unauthenticated | 401 existing auth code | existing auth status |
| Viewer or Editor deleting another/unknown-owner object | 403 `forbidden` | resource type/ID; no creator identity disclosure |
| Missing/already deleted route target | 404 `not_found` | resource type/ID |
| Stale active Version | 409 `conflict` | resource type/ID |
| Active dependencies | 422 `business_rule_violation` | bounded `blockers[]` with `dependencyType`, Chinese `displayName`, `count`, optional safe navigation |
| Deleted/invalid body reference | 422 `reference_invalid` | current feature's reference detail |

Do not add `soft_delete_error`, `delete_error`, or a second error envelope.

## Atomicity

One short SQLite write transaction must cover authoritative reload, ownership/concurrency checks, active dependency validation, deletion state/audit, Version increment, and KnowledgeDocument FTS removal. Any failure rolls back every part.

All dependency-creating mutations must resolve active endpoints in their own write transaction. DELETE-B02 must prove delete-vs-edit, delete-vs-relation-add, delete-vs-status-progression, and delete-vs-UnknownItem-Apply interleavings. There is no automatic retry that could replay a user delete after state changed.

## Architecture Alternatives

| Alternative | Evaluation | Decision |
| --- | --- | --- |
| A. Per-entity fields/use cases + shared conventions | Keeps ownership/dependency rules explicit and fits feature-first direct DbContext | Adopt |
| B. EF global filters + controlled historical `IgnoreQueryFilters` | Strong default for the many current LINQ surfaces; requires raw-SQL and exception audits | Adopt with A |
| C. Generic base/interface/framework | Reduces a few repeated properties but obscures exact scope and invites generic deletion/dependency logic | Reject |
| D. Hard delete | Breaks revisions, Evidence/HC, workflow history, tombstones, and name/ID continuity | Reject |
| E. Recycle bin/application restore | Contradicts frozen first-release recovery boundary and expands authorization/UI/workflows | Reject |

## Chosen Architecture

```text
Explicit fields on eight concrete roots
+ explicit per-root delete use cases
+ canonical current-user ownership helper
+ entity-specific dependency validators
+ explicit EF global query filters
+ narrowly whitelisted historical tombstone/revision reads
+ SQLite partial active unique indexes
+ manual DB-only recovery
```

A small authorization helper may compare AccessLevel/current User ID/creator ID and return a typed result. It must not load arbitrary entities or perform delete. Dependency logic remains feature-specific.

## Rejected Alternatives

- No automatic cascade soft delete or child/relationship cleanup.
- No generic soft-delete base/interface, repository, controller, service, reflection dispatcher, or universal DTO.
- No explicit filter-only architecture; 266 access sites make omission risk unacceptable.
- No global filter escape supplied by request/query parameter.
- No hard delete, archive substitution, recovery API, recycle bin, automatic rename/merge, or name-based rebinding.
- No deletion revision for KnowledgeDocument.

## DELETE-B01 Contract

**Soft Delete Persistence + Ownership Foundation**

- Add creator/concurrency/deletion fields, FKs/checks, global filters, active-only unique indexes, and ModelSnapshot migration.
- Migrate existing rows to active with null audit and unknown legacy creator IDs.
- Make all new root creation paths capture canonical current User IDs; stop treating request actor as ownership authority.
- Add DatabaseSource Version/token foundation.
- Add migration, constraint, ownership, query-filter escape, name-reuse, restore-conflict, and SQLite integrity tests.
- Do not add delete endpoints or UI yet.

## DELETE-B02 Contract

**Core Delete Use Cases + Dependency Guards**

- Add eight concrete DELETE endpoints/use cases.
- Implement Administrator/Editor-own/Viewer authorization and legacy unknown-owner denial.
- Implement entity-specific bounded blocker summaries and short atomic SQLite transactions.
- Keep relationship and owned-child physical correction paths explicit.
- Prove no cascade, no stale-write resurrection, dependency races, audit values, unchanged lifecycle/status/content, and 404/403/409/422 contracts.

## DELETE-B03 Contract

**Current Projection and FTS Exclusion**

- Audit every list/detail/selector/search/Dashboard/Unified View/Trace/Impact/Supersedes/current mutation path.
- Add FTS atomic remove, raw canonical predicate, active-only rebuild, and offline maintenance invocation/documentation.
- Add historical tombstone/revision/Evidence read boundaries and endpoint-aware relation filtering.
- Capture representative SQLite query plans; add only measured indexes.
- Do not add delete UI or recovery UI.

## DELETE-B04 Contract

**Delete UX + Historical Tombstones**

- Add ownership-aware danger actions to supported detail/manage surfaces.
- Add confirmation copy, backend-authoritative error handling, bounded dependency dialog, and refresh/navigation behavior.
- Render original name + strikethrough + `已删除`, non-clickable, in approved historical contexts.
- Preserve the single overlay coordinator and existing Simplified Chinese/accessibility baseline.
- Do not add recycle bin, restore, deleted list/search, or full deleted detail.

## DELETE-VERIFY Strategy

### Authorization

- Anonymous and Viewer denied; Editor own allowed; Editor other/legacy unknown denied; Administrator any supported root allowed.
- Renamed/deactivated creator or deleter snapshots remain readable and FK-valid.

### Dependency and atomicity

- No blocker succeeds; every active child/relation/workflow blocker denies with bounded details.
- Evidence/HC/revisions/completed workflow history alone do not block.
- Delete never cascades and races cannot leave an active dependency attached to a deleted target.

### Visibility

- Lists, details, selectors, structured search, FTS, Dashboard, Unified View, Trace, Impact, Supersedes, status mutations, Evidence/HC additions, relationships, publish/restore, and UnknownItem Apply all enforce active truth.

### Name/identity/recovery

- active duplicate denied; delete old then create same name succeeds with new ID; old references retain old ID; manual restore conflicts until operator resolves the active duplicate.
- Recovery keeps lifecycle/status/content/revisions and delete audit, increments Version, restores FTS through controlled maintenance, and passes SQLite integrity checks.

### Historical presentation

- Tombstone keeps original name, visible strikethrough and `已删除`, is not navigable, and leaks no full deleted detail.
- Revisions/Evidence/HC remain readable only through the frozen historical boundary.

### Repository gates

- focused backend/API/persistence tests, affected frontend tests, `dotnet build`, frontend type-check/build, one authenticated browser master flow, SQLite query plans/integrity, and repository database hash/metadata protection;
- stop every verification-only process and release every agent-used port after each cycle.

## Risks

| Risk | Mitigation |
| --- | --- |
| An omitted query leaks deleted data | Global filters, raw-SQL audit, mutation/selector tests, whitelisted filter bypass. |
| Creator names are mistaken for ownership | Nullable canonical creator IDs; legacy Editor denial; no name backfill. |
| Global filters hide data needed for history | Narrow type-specific tombstone and revision exceptions with tests. |
| Deleting a parent creates orphaned active behavior | Complete entity-specific blocker matrix and race tests; no cascade. |
| Partial index migration loses collation/key semantics | Compare ModelSnapshot/generated SQL; duplicate preflight and isolated SQLite migration tests. |
| FTS and canonical state drift | Same transaction removal plus canonical raw predicate and rebuild tests. |
| Manual recovery violates uniqueness/parents | Database constraints and mandatory operator runbook/integrity verification. |
| Generic abstraction hides domain differences | Concrete use cases/configurations/validators; no generic delete engine. |

## Open Questions

Blocking human decisions: **NONE**.

Non-blocking implementation validation items:

1. DELETE-B01/B03 must use `EXPLAIN QUERY PLAN` to determine whether any additional partial active index is warranted beyond the changed unique/list indexes.
2. DELETE-B03 must choose the smallest operational packaging for the offline FTS rebuild (not an HTTP/product recovery endpoint).
3. DELETE-B04 must place DatabaseSource delete in its nearest existing management context because no standalone DatabaseSource detail route currently exists.
4. Final Chinese blocker category labels and safe navigation targets may be refined without changing blocker semantics or response bounds.

## Compatibility with Frozen Architecture

| Frozen/current area | Compatibility result |
| --- | --- |
| Feature-first direct DbContext | Retained; concrete configurations/use cases/queries only. |
| Frozen MVP “no core delete/soft-delete framework” | This explicitly approved post-MVP capability supersedes the earlier no-delete scope only for the eight listed roots; no frozen specification is edited and no framework is introduced. |
| Physical delete reference checks | Retained for owned correction rows/KnowledgeRelation; new soft delete distinguishes historical references from active blockers. |
| Viewer/Editor/Administrator | Retained and extended with frozen creator ownership; no new role/permission system. |
| Current User security | Reused as sole actor authority; request actor cannot establish ownership/deleter. |
| Integer Version/JSON token | Reused; DatabaseSource is brought into the existing recommended scope. |
| KnowledgeStatus | Independent and unchanged. |
| Document lifecycle and REV-A01 | Archived remains independent; delete creates no revision and preserves every pointer/snapshot. |
| Evidence/HumanConfirmation | Historical facts preserved; no automatic status change. |
| Relationship vocabulary/removal | No wire values change; active rows block and remain explicitly removed. |
| TRACE-A01 | Still derived from canonical current truth; deleted nodes/paths are excluded. |
| SQLite/FTS | EF Core remains primary, focused SQL remains limited to measured/FTS needs. |
| UI baseline | Existing routes/overlays/Simplified Chinese/accessibility retained; no recycle-bin route. |

The earlier frozen database/application/API documents explicitly excluded soft delete from MVP. That is a scope difference, not an irreconcilable semantic conflict: DELETE-A01 is the later capability-specific authority and leaves the frozen source files untouched.

## Static Verification Evidence

DELETE-A01 used static inspection only, as required:

- reviewed domain entities, DbContext, EF configurations, full ModelSnapshot and FTS migration;
- inventoried current unique indexes, physical FKs, polymorphic relation/Evidence/UnknownItem/KnowledgeUpdate references, controllers, access policies, concurrency codec, query services, tests, and Vue feature surfaces;
- reviewed the frozen domain/database/application/API/solution/UI sources and adjacent SEC-A01, REV-A01, TRACE-A01 and implementation verification documents;
- did not start API, Vite, Browser, test/watch process, or temporary server;
- did not open/migrate/seed/write the checked-in SQLite database;
- changed only this design decision and `docs/DOCUMENT_INDEX.md`.

No verification-only process or port was created, so cleanup is not applicable.

## Final Decision

```text
DELETE-A01 APPROVED

Soft-delete Root Scope:
System, DatabaseSource, BusinessFunction, DatabaseObject, DatabaseColumn,
BusinessRule, Integration, KnowledgeDocument

Child Entity Strategy:
DatabaseColumn is independently soft deletable; tags, process steps, contract
fields, and known values retain explicit physical correction/removal and block
their parent until cleared

Excluded Entities:
KnowledgeRelation generic soft delete, KnowledgeDocumentRevision, Evidence,
HumanConfirmation, UnknownItem, User, and workflow/audit children

Deletion State:
IsDeleted + DeletedAt UTC + DeletedByUserId + DeletedByDisplayName

Authorization:
Administrator any supported root; Editor authoritative own only; Viewer deny;
legacy unknown creator Editor deny; no application restore permission

Dependencies:
Active blocks; historical facts do not normally block; no cascade or automatic
relationship cleanup

Current Projections:
Deleted rows hidden/excluded from lists, details, search/FTS, Dashboard,
selectors, Unified View, Trace, Impact, Supersedes and ordinary mutations

Historical Reference:
Original name + strikethrough + explicit 已删除; not navigable; minimal tombstone

Name Reuse:
Allowed through active-only SQLite partial unique indexes; new ID; no rebinding

Recovery:
Manual DB operator only; keep deletion audit, increment Version, validate
uniqueness/parents/dependencies, rebuild KnowledgeDocument FTS, verify integrity

Revision/Evidence/HumanConfirmation:
Preserved; no delete revision; normally non-blocking

Database Change Required: YES
Migration Required: YES
CreatedBy Backfill: NO unsafe/invented backfill; legacy remains unknown
FTS Change Required: YES
Blocking Human Decisions: NONE

Next permitted task after human acceptance:
DELETE-B01 — Soft Delete Persistence + Ownership Foundation
```

Stop after this decision. Do not start DELETE-B01, DELETE-B02, DELETE-B03, DELETE-B04, or DELETE-VERIFY automatically.
