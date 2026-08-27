# DELETE-B03

## Result

`DELETE-B03 PASS`

The eight frozen soft-delete roots now behave as absent from current product projections. Explicit historical read paths expose only a closed, minimal tombstone identity, and KnowledgeDocument recovery has an application-owned FTS rebuild command without adding a product restore API.

## Worktree Baseline

- Branch: `main`
- Starting commit: `270a4f3c67df4f855b983ffacdec402489721893`
- Starting worktree: clean and aligned with `origin/main`
- Scope gate: DELETE-B03 only; no DELETE-B04 UI, recycle bin, restore API, hard delete, purge, retention, or new soft-delete root.

## Normative Authority

The implementation was checked against:

- `AGENTS.md`
- `docs/DOCUMENT_INDEX.md`
- `docs/design/DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md`
- `docs/reports/DELETE_B01_SOFT_DELETE_PERSISTENCE_AND_OWNERSHIP_FOUNDATION_VERIFICATION_REPORT.md`
- `docs/reports/DELETE_B02_CORE_DELETE_USE_CASES_AND_DEPENDENCY_GUARDS_VERIFICATION_REPORT.md`
- the applicable frozen domain, application use-case, API-contract, database-model, solution-structure, design-baseline, and UI-inventory sections
- the DELETE-B03 task definition

No frozen source was changed.

## Repository DB Safety Baseline

The repository database was never opened or connected to during this task. Baseline was collected by filesystem metadata and hashing only:

| File | Present | Length | LastWriteTimeUtc | SHA-256 |
| --- | ---: | ---: | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | Yes | 897024 | `2026-08-27T15:46:01.9864232Z` | `7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483` |
| `system-knowledge-hub.db-wal` | No | — | — | — |
| `system-knowledge-hub.db-shm` | No | — | — | — |

## Scope

The change closes current-projection gaps in Dashboard, unified knowledge, relation-derived views, Trace, Coverage, Impact, Supersedes, and relationship reads; adds controlled historical identity to Evidence, HumanConfirmation, revision, Closed UnknownItem, and Applied KnowledgeUpdate reads; audits raw SQL and mutation references; and adds a CLI-only KnowledgeDocument FTS rebuild boundary.

The frozen roots remain exactly: System, DatabaseSource, BusinessFunction, DatabaseObject, DatabaseColumn, BusinessRule, Integration, and KnowledgeDocument.

### Current Projection Matrix

| Surface | Deleted Root | Deleted Child/Target | Result |
| --- | --- | --- | --- |
| List | Excluded by global filters | Nested deleted rows excluded | PASS; totals and paging use filtered sets |
| Detail | `404 not_found` | Required deleted parent does not provide an alternate path | PASS |
| Selector | Not offered | Stale submitted IDs remain invalid | PASS |
| Structured Search | Excluded | Counts and paging exclude deleted results | PASS |
| KnowledgeDocument FTS | Excluded by canonical active-document join | Stale FTS row cannot surface the document | PASS |
| Dashboard | Excluded from current metrics | Deleted target cannot satisfy status/relation metrics | PASS |
| Unified View | Deleted System returns 404 | Deleted children and invalid relationship rows excluded | PASS |
| Trace | Deleted document returns 404 | Edge/path stops at deleted endpoint | PASS |
| Coverage | Cannot be a current root | Deleted Specification/TestCase does not satisfy coverage | PASS |
| Impact | Deleted document returns 404 | Direct/derived path stops; counts use filtered candidates | PASS |
| Supersedes | Cannot be a current root | Deleted lineage endpoint excluded; no name rebinding | PASS |
| Relationship projection | Deleted endpoint is absent | Edge excluded rather than reported as corrupt | PASS |
| Mutation reference | Filtered root cannot be mutated | Deleted body reference is rejected by existing active resolvers | PASS |

## Current Detail Filtering

All normal detail reads for the eight roots remain on EF-filtered query roots and return absence for deleted records. DatabaseSource management/read context is reached through its active System/database context rather than through a new include-deleted route. No `includeDeleted`, deleted-detail DTO, or administrator deleted mode was introduced.

## Current List Filtering

Main and nested lists use active query roots. Systems, BusinessFunctions, DatabaseSources, DatabaseObjects, DatabaseColumns, BusinessRules, Integrations, and KnowledgeDocuments exclude deleted rows before count, sort, and paging. Nested System functions, DatabaseSource objects, DatabaseObject columns, and unified knowledge sections inherit the same boundary.

## Selector Filtering

The selector inventory was checked across System, BusinessRule, Integration, relationship target search, knowledge target search, database-child creation, UnknownItem, and other typed target loaders. Current option loaders use filtered sets or the existing active closed-type resolver. Cached deleted IDs are still rejected server-side by the corresponding mutation validator.

## Structured Search

Structured global search remains limited to its existing supported types. Its EF queries inherit the eight-root global filters, so deleted results do not contribute rows, per-type counts, or total paging. DatabaseSource was not added as a new global-search type.

## KnowledgeDocument FTS

Both raw FTS result and count queries join `knowledge_documents` and require `d.is_deleted = 0`. Delete continues to remove the FTS row in the delete transaction. Therefore a deliberately stale FTS row is also fail-safe: the canonical deleted document prevents the hit and its count. The active-only rebuild path was verified on the disposable runtime database.

## FTS Recovery Maintenance Boundary

The internal CLI command is:

```text
SystemKnowledgeHub.Api rebuild-knowledge-document-search
```

It opens an immediate SQLite transaction and delegates rebuilding to `KnowledgeDocumentSearchIndex`, preserving the existing normalization and Markdown-to-search-text processing. It indexes active KnowledgeDocuments only and reports the active document count. An operator who manually restores a KnowledgeDocument in the database must then run this application-owned command; operators must not insert raw document bodies directly into the FTS table.

No HTTP maintenance endpoint, product restore API, recycle bin, or deleted-document management surface was added.

## Dashboard Filtering

Dashboard root counts and active-root queries continue to inherit global filters. The correction also restricts UnknownItem summaries to active Systems and requires relation-backed function-data metrics to resolve active DatabaseObject/DatabaseColumn targets. Deleted confirmed/inferred objects and deleted targets no longer contribute to current metrics or navigable current activity.

## Unified Knowledge View

A deleted System remains absent. Child sections use active roots, and relationship-derived entries now pass through the active relationship target resolver. Section counts are formed from the same visible active collections, preventing hidden rows from remaining in totals.

## Trace Filtering

Trace now distinguishes endpoint state with one physical KnowledgeDocument lookup root: a physically missing/wrong-type endpoint remains fail-closed `reference_invalid`, while an existing deleted endpoint is excluded from current edges. Active endpoint joins drive graph traversal, totals, and cycle evaluation. Deleted intermediates terminate paths instead of allowing traversal to later nodes.

## Coverage Recalculation

Specification and test-definition coverage are calculated from active endpoints only. A deleted Specification, TestCase, or direct Requirement test endpoint cannot satisfy coverage; the existing missing-specification/missing-test-definition semantics are recalculated from the remaining active graph.

## Impact Filtering

Impact validates physical reference type separately from current visibility, selects active intermediate documents, and resolves only active typed targets. Deleted direct targets and deleted intermediates are omitted before total/paging calculation. A genuinely missing or wrong-type physical endpoint still returns the existing `reference_invalid` failure.

## Supersedes Filtering

Current lineage uses active endpoints. A deleted superseded/superseding endpoint is excluded and never rebound to a same-name active replacement; identity remains ID-based.

## Relationship Current Projection

Relationship detail and related-item lists validate current endpoints. Deleted endpoints cause current rows to be excluded/absent, while physically missing endpoints retain the existing fail-closed `reference_invalid` result. Related-item projection no longer fabricates an “object no longer exists” current item. DatabaseKnowledge, BusinessFunction, BusinessRule, Dashboard, and unified-view relationship counts apply the same active-endpoint rule.

## Historical Tombstone Contract

`HistoricalTargetResolver` is a read-only, closed eight-type resolver. It projects only:

```text
Id, TargetType, DisplayName, IsDeleted, IsNavigable
```

It does not expose descriptions, document bodies, knowledge-status detail, evidence payload, or root business fields. `IsNavigable` is false for a deleted owner. It is injected only into historical read queries, never into mutation services.

### Historical Read Matrix

| Historical Surface | Deleted Subject | Allowed Data | Navigable |
| --- | --- | --- | ---: |
| Evidence | Retained | Evidence fact plus minimal subject tombstone | No |
| HumanConfirmation | Retained | Confirmation fact plus minimal subject tombstone | No |
| KnowledgeDocument revision list/detail | Retained | Immutable snapshot, revision metadata, author/time, owner tombstone | No |
| Closed UnknownItem | Retained | Closed workflow record and minimal original target identities | No |
| Applied KnowledgeUpdate | Retained | Applied historical snapshot and minimal original target identity | No |

## Evidence Historical Read

Existing Evidence remains list/detail readable after its subject is deleted. The response adds the minimal subject tombstone and makes current subject context nullable. Actions are empty for a deleted subject, so the historical read does not reopen a mutation path.

## HumanConfirmation Historical Read

HumanConfirmation remains attached to the retained Evidence read and uses the same subject tombstone boundary. Existing confirmation facts are not deleted or hidden because their subject was soft-deleted.

## KnowledgeDocument Revision Historical Read

Revision list/detail performs a minimal unfiltered owner-pointer lookup, then returns immutable revision content and an owner tombstone. Normal current document detail/search remains absent. Revision restore still uses the active filtered document lookup, so restoring a revision to a deleted document remains denied/404.

## Closed Workflow Historical Read

Closed UnknownItems may resolve deleted System and typed targets through the historical resolver. Non-Closed items remain current-only. When a historical target is deleted, the detail is explanatory but non-navigable and exposes no actions.

## Applied KnowledgeUpdate Historical Read

Applied KnowledgeUpdates in a Closed workflow retain the original target ID/type/display name through a tombstone. Resolution is by original ID, so a newly created same-name object cannot receive the old history. Active/apply mutations continue to use the current active resolver.

## IgnoreQueryFilters Audit

All production occurrences are locally explicit, read-only, and minimal. There are 12 occurrences:

| File / Method | Target type | Reason | Projection fields | Test evidence |
| --- | --- | --- | --- | --- |
| `HistoricalTargetResolver.ResolveAsync` / System branch | System | Historical identity | Id, Name, IsDeleted | historical boundary focused tests |
| same / DatabaseSource branch | DatabaseSource | Historical identity | Id, Name, IsDeleted | closed resolver type inventory + build/regression |
| same / BusinessFunction branch | BusinessFunction | Historical identity | Id, Name, IsDeleted | closed resolver type inventory + build/regression |
| same / DatabaseObject branch | DatabaseObject | Historical identity | Id, Name, IsDeleted | closed resolver type inventory + build/regression |
| same / DatabaseColumn branch | DatabaseColumn | Historical identity | Id, Name, IsDeleted | closed resolver type inventory + build/regression |
| same / BusinessRule branch | BusinessRule | Historical identity | Id, Name, IsDeleted | closed resolver type inventory + build/regression |
| same / Integration branch | Integration | Historical identity | Id, Name, IsDeleted | closed resolver type inventory + build/regression |
| same / KnowledgeDocument branch | KnowledgeDocument | Historical identity | Id, Title, IsDeleted | Evidence/HC and revision focused tests |
| `KnowledgeDocumentQueries.GetRevisionListAsync` | KnowledgeDocument | Locate historical owner and current/latest revision pointer | Id, Title, IsDeleted, CurrentRevisionNumber, LatestRevisionNumber | deleted revision list/detail focused test |
| `KnowledgeDocumentQueries.GetRevisionDetailAsync` | KnowledgeDocument | Locate historical owner and current/latest revision pointer | Id, Title, IsDeleted, CurrentRevisionNumber, LatestRevisionNumber | deleted revision list/detail focused test |
| `TraceabilityQueries.LoadReferencesAsync` | KnowledgeDocument | Distinguish deleted endpoint from physical missing/wrong type | Id, DocumentType, IsDeleted | trace/relationship legacy-deleted regression |
| `ImpactQueries.LoadPhysicalDocumentsAsync` | KnowledgeDocument | Distinguish deleted endpoint from physical missing/wrong type | Id, DocumentType, IsDeleted | impact legacy-deleted regression |

No general include-deleted repository or caller-controlled deleted mode exists.

## Raw SQL Audit

Production raw SQL was searched using `FromSql`, `ExecuteSql`, `ExecuteSqlRaw`, `SqlQuery`, `CreateCommand`, `CommandText`, SQL keywords, and `knowledge_documents_fts`. Migration SQL is historical schema evolution and is not a current product projection.

| File / method | Root type | Current / Historical | Deleted predicate | Test evidence / result |
| --- | --- | --- | --- | --- |
| `SearchQueries.SearchKnowledgeDocumentsAsync` result command | KnowledgeDocument | Current | Canonical join plus `d.is_deleted = 0` | create-hit/delete-no-hit runtime and search tests; PASS |
| `SearchQueries.SearchKnowledgeDocumentsAsync` count command | KnowledgeDocument | Current | Canonical join plus `d.is_deleted = 0` | post-delete total `0`; PASS |
| `KnowledgeDocumentSearchIndex.UpsertAsync` | KnowledgeDocument | Current index maintenance | Caller resolves active document; delete/insert is parameterized | lifecycle and full regression; PASS |
| `KnowledgeDocumentSearchIndex.DeleteAsync` | KnowledgeDocument | Current index maintenance | Removes by canonical document ID | delete/FTS focused and runtime; PASS |
| `KnowledgeDocumentSearchIndex.RebuildAsync` | KnowledgeDocument | Current index maintenance | Source query is active documents only | CLI rebuild produced active count `0` and FTS rows `0`; PASS |
| `DbContextConfiguration` connection initialization | None | Infrastructure | PRAGMA/connection setup only | Not a root projection |
| `BootstrapController` diagnostic command | None | Infrastructure | FK diagnostic only | Not a root projection |

No current-product raw root query lacking an active-row predicate was found.

## Mutation Reference Audit

| Mutation family | Root/reference boundary | Existing guard / B03 result |
| --- | --- | --- |
| System edit/lifecycle/delete | System ID | Filtered root query; deleted root is absent |
| BusinessFunction edit and child lifecycle | Function and owning System | Filtered root/parent queries; deleted reference rejected |
| DatabaseSource/Object/Column mutations | Source, object, column, owning hierarchy | Filtered parent/root queries; child creation cannot target deleted parent |
| BusinessRule edit | Rule and System context | Filtered queries/active System validation |
| Integration edit | Integration plus System/DatabaseSource/Object references | Closed active target validation |
| KnowledgeDocument edit/publish/archive/revision restore/delete | KnowledgeDocument ID | Filtered current root; revision historical reads do not change restore guard |
| Relationship create/update/status/delete | Typed source/target | `RelationshipTargetResolver` with transactional revalidation |
| Evidence add/update | Typed subject | `EvidenceSubjectResolver` active-only validation |
| HumanConfirmation add/update | Evidence subject | Active subject validation and transactional revalidation |
| KnowledgeStatus progression | Typed subject | Active target resolver |
| UnknownItem create/update/investigation/resolution/reopen | System and typed targets | Active closed-type target resolution |
| UnknownItem Apply / KnowledgeUpdate Apply | Original target | Active target resolution plus transactional revalidation |

Historical resolution is not registered in any write use case. The existing B02 delete-vs-reference and post-delete protections remain intact.

## Query Plan Verification

Representative plans were executed only against task-owned databases through the backend test suite:

- `KnowledgeDocumentSearchPerformanceTests.Fts_query_returns_a_bounded_document_group_for_one_thousand_representative_documents`
- `TraceabilityApiTests.Representative_SQLite_query_plans_use_existing_indexes`
- `ImpactApiTests.Representative_SQLite_query_plans_use_existing_relation_indexes`

The paged/list, structured-search, Dashboard/unified, and tombstone paths use bounded PK/FK/global-filter queries verified by the affected/full regression. No measured regression justified adding blanket `is_deleted` indexes; no index was added.

## Files Changed

Backend:

- added `Features/SoftDelete/Application/HistoricalTargetResolver.cs`
- added `Features/Search/Application/KnowledgeDocumentSearchMaintenanceCommand.cs`
- updated Program registration/CLI dispatch
- updated Evidence, KnowledgeDocument revision, UnknownItem, Dashboard, System unified-view, Relationship, DatabaseKnowledge, BusinessFunction, BusinessRule, Trace, and Impact query/model/controller boundaries

Frontend strict boundary:

- updated Evidence and KnowledgeDocument revision contracts/tests
- updated the Evidence drawer only enough to tolerate nullable current context
- updated UnknownItem contract types for tombstone fields

Tests and documentation:

- added `CurrentProjectionHistoricalBoundaryApiTests.cs`
- added this report and updated `docs/DOCUMENT_INDEX.md`

No delete UI or historical deleted-state visual treatment was added.

## Backend Build

`dotnet build SystemKnowledgeHub.sln --no-restore -c Release`: PASS, 0 warnings, 0 errors.

## Focused Tests

`CurrentProjectionHistoricalBoundaryApiTests`: 3/3 PASS.

The facts cover KnowledgeDocument current exclusion/FTS/historical Evidence-HC/revision/restore denial/rebuild, Closed UnknownItem and Applied KnowledgeUpdate tombstones with same-name replacement defense, and legacy deleted relation endpoints across Trace/Impact/relationship projections.

## Affected Regression

Focused filters covering soft-delete ownership, Trace, Impact, Evidence, revision reads, UnknownItems, Relationships, Dashboard, and System unified view: 46/46 PASS.

## Full Backend Regression

The approved deterministic serial gate used a task-owned temporary runsettings file with one test worker:

```text
dotnet test --no-build --settings .tmp/delete-b03-serial.runsettings
```

Result: 167/167 PASS, 0 skipped. The runsettings file was deleted immediately afterward. REV-GAP-011 remains Deferred; the default parallel attempt reproduced that known runner hang and was safely interrupted through its exact task-owned terminal session before the deterministic gate was run.

## Frontend Boundary

- `npm run type-check`: PASS
- `npm run build`: PASS; existing chunk-size advisory only
- ESLint on all changed frontend source/test files: PASS, 0 errors
- affected Vitest contracts: 6/6 PASS
- no delete button, confirmation dialog, dependency blocker UI, deleted badge, recycle bin, restore UI, or management surface added

An additional non-gating full `npm test` run passed 292/293 tests. The sole failure is the pre-existing unrelated `AppShell.spec` navigation-copy expectation for `关系与缺口`; DELETE-B03 did not modify AppShell/navigation. It is recorded as a baseline deviation, not concealed as a full frontend PASS and not treated as a DELETE-B03 contract failure.

## Runtime API Verification

Runtime verification used a task-owned temporary SQLite database, isolated local authentication/bootstrap state, and port `51903`:

```text
Login                           204
Create KnowledgeDocument       id=2
Search before delete total     1
DELETE                         204
Current detail after delete    404
Search after delete total      0
Revision owner IsDeleted       true
Revision count                 1
FTS maintenance command        exit 0, activeDocuments=0
```

This proves the real create/search/delete/current-absence/historical-revision/FTS-rebuild path without opening the repository database. The runtime server was stopped and port `51903` was confirmed released.

## Temporary SQLite Integrity

On the task-owned runtime database only:

```text
PRAGMA integrity_check       ok
PRAGMA foreign_key_check     0 rows
active KnowledgeDocuments    0
knowledge_documents_fts rows 0
```

The temporary database, WAL/SHM if any, runtime directory, DataProtection/runtime state, runsettings, and logs were removed. No task-owned API or testhost process remained.

## Repository DB Protection

The final filesystem-only check exactly matched the baseline:

| File | Final result |
| --- | --- |
| Main DB length | 897024 — unchanged |
| Main DB LastWriteTimeUtc | `2026-08-27T15:46:01.9864232Z` — unchanged |
| Main DB SHA-256 | `7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483` — unchanged |
| WAL | Not present — unchanged |
| SHM | Not present — unchanged |

Repository `App_Data`: UNCHANGED. No checkpoint, rebaseline, sidecar cleanup, migration, PRAGMA, or read-only SQLite connection touched it.

## New Gap Check

No new DELETE-B03 Blocker/High/Medium gap was found. REV-GAP-011 remains Deferred and unchanged. The unrelated AppShell Vitest expectation is an existing frontend baseline deviation outside this task's changed surface.

## DELETE-B04 Readiness

`DELETE-B04 READY: YES`

DELETE-B04 was not started.

## Final Result

```text
DELETE-B03 COMPLETE

Result:                         DELETE-B03 PASS
Details / Lists / Selectors:    PASS
Structured Search:              PASS
FTS / Stale Defense / Rebuild:  PASS
FTS Recovery Maintenance:       PASS
Dashboard / Unified View:       PASS
Trace / Coverage:               PASS
Impact / Supersedes:            PASS
Relationship Projection:        PASS
Evidence / HC Historical:       PASS
Revision Historical:            PASS
Closed Workflow / Applied:      PASS
IgnoreQueryFilters Audit:       PASS
Raw SQL Audit:                  PASS
Mutation Reference Audit:       PASS
Backend Build:                  PASS
Focused Tests:                  3/3 PASS
Affected Regression:            46/46 PASS
Full Backend Tests:             167/167 PASS
Frontend affected gate:         PASS
Query Plans:                    PASS
Runtime:                        PASS
SQLite integrity_check:         ok
SQLite foreign_key_check:       0 rows
Repository App_Data:            UNCHANGED
New Gaps:                       NONE
Delete UI:                      NOT STARTED
Restore API:                    NONE
Recycle Bin:                    NONE
DELETE-B04 READY:               YES
```
