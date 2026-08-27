# DELETE-B01

## Result

DELETE-B01 PASS

The soft-delete persistence and ownership foundation is complete for all eight approved roots. This slice adds no delete/restore endpoint, delete/restore UI, recycle bin, automatic cascade, or recovery workflow.

## Worktree Baseline

- Branch: `main`
- Starting commit: `ffc710a60435bb922a515c6cf708802b7535fd68`
- Starting worktree: clean and aligned with `origin/main`
- Repository database baseline: 724,992 bytes; `2026-08-26T14:32:28.8616945Z`; SHA-256 `A122F8D208338A8445DCF19CE27601634B98A0B35AC1575FDBB6ADF7A3422DA9`; WAL present; SHM present.

## Normative Authority

Implementation follows the approved `docs/design/DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md`, the DELETE-B01 task definition, `docs/DOCUMENT_INDEX.md`, and the applicable frozen domain/database/application/API/solution/UI specifications. No material conflict was found and no frozen source, Golden UI asset, or frozen task definition was changed.

## Scope

The slice contains only the persistence/read/create foundations needed by later delete slices: explicit root state/audit fields, canonical creator ownership, DatabaseSource concurrency, active-row filters and uniqueness, required active-parent create validation, KnowledgeDocument FTS defenses, migration/snapshot, focused tests, and the minimal DatabaseSource token decoder update.

## Root Entity Matrix

| Root | IsDeleted | Delete audit | CreatedByUserId | Query filter | Active-only unique | Version |
|---|---:|---:|---:|---:|---:|---:|
| System | Yes | Yes | Nullable, new | Yes | Yes | Existing |
| DatabaseSource | Yes | Yes | Nullable, new | Yes | Yes | New, required/default 1 |
| BusinessFunction | Yes | Yes | Nullable, new | Yes | Yes | Existing |
| DatabaseObject | Yes | Yes | Nullable, new | Yes | Yes | Existing |
| DatabaseColumn | Yes | Yes | Nullable, new | Yes | Yes | Existing |
| BusinessRule | Yes | Yes | Nullable, new | Yes | Yes | Existing |
| Integration | Yes | Yes | Nullable, new | Yes | Yes | Existing |
| KnowledgeDocument | Yes | Yes | Existing required ID | Yes | Not applicable | Existing |

## Deletion State Mapping

Every root explicitly maps `is_deleted` as required with default `0`, plus nullable `deleted_at`, `deleted_by_user_id`, and `deleted_by_display_name`. No shared base class, marker interface, shadow property, convention-based soft-delete abstraction, or speculative cascade mechanism was introduced.

## Deletion Audit Constraints

Each root table has an explicit named check constraint. Active rows permit either an entirely null deletion audit or the complete historical audit triple. Deleted rows require the complete triple. `deleted_by_display_name IS NOT NULL` and nonblank trimmed content are explicit so SQLite's null-valued CHECK semantics cannot admit a partial triple. Tests prove malformed state, partial audit, blank/missing audit, and invalid deleter FK are rejected; a complete historical triple on an active row is accepted.

## Creator Ownership Foundation

| Root | Existing creator ID before B01 | After B01 | Legacy backfill | New create capture |
|---|---|---|---|---|
| System | None | Nullable FK to User, RESTRICT | None; remains unknown | Canonical Current User |
| DatabaseSource | None | Nullable FK to User, RESTRICT | None; remains unknown | Canonical Current User |
| BusinessFunction | None | Nullable FK to User, RESTRICT | None; remains unknown | Canonical Current User |
| DatabaseObject | None | Nullable FK to User, RESTRICT | None; remains unknown | Canonical Current User |
| DatabaseColumn | None | Nullable FK plus nullable display snapshot | None; both remain null | Canonical Current User + snapshot |
| BusinessRule | None | Nullable FK to User, RESTRICT | None; remains unknown | Canonical Current User |
| Integration | None | Nullable FK to User, RESTRICT | None; remains unknown | Canonical Current User |
| KnowledgeDocument | Required canonical ID and snapshot | Unchanged | None | Existing canonical Current User path |

Legacy name/role snapshots are historical attribution only and are not treated as ownership authority. Controllers resolve `ICurrentUserContext` before root creation; request actor data remains compatible input but cannot select or spoof the owner.

## Legacy Creator Handling

Upgrade verification seeds all eight roots in the pre-B01 schema. The seven newly introduced creator IDs remain `NULL`; KnowledgeDocument's existing creator ID remains unchanged. There is no heuristic name-to-user backfill.

## DatabaseColumn Creator

DatabaseColumn now has nullable `created_by_user_id` and nullable `created_by_display_name`. Legacy rows retain both as null. New column registration captures the canonical current User ID and current display-name snapshot.

## DatabaseSource Version / Concurrency

DatabaseSource now maps a required concurrency-token `version` with default/initial value `1` and `version >= 1` check. Create responses and all nearest DatabaseSource read projections expose an opaque encoded `concurrencyToken`, including browse context, object list rows, and object detail context. Backend and strict frontend decoders agree on this shape.

## Global Query Filters

| Entity | Global filter | Controlled bypass locations |
|---|---|---|
| System | `!IsDeleted` | Migration test line 136 |
| DatabaseSource | `!IsDeleted` | Migration test line 137 |
| BusinessFunction | `!IsDeleted` | Migration test line 138 |
| DatabaseObject | `!IsDeleted` | Migration test line 139 |
| DatabaseColumn | `!IsDeleted` | Migration test line 140 |
| BusinessRule | `!IsDeleted` | Migration test line 141 |
| Integration | `!IsDeleted` | Migration test line 142 |
| KnowledgeDocument | `!IsDeleted` | Migration test line 143 |

Normal EF reads therefore hide deleted roots. Existing create-time parent lookups automatically require active parents: System for DatabaseSource/BusinessFunction/BusinessRule, DatabaseSource for DatabaseObject, DatabaseObject for DatabaseColumn, and filtered root references for Integration.

## IgnoreQueryFilters Audit

Production source contains zero `IgnoreQueryFilters()` uses. The only eight uses are the one-per-root assertions in `SoftDeleteOwnershipFoundationMigrationTests`, where the bypass proves the controlled persistence fixture still exists while normal DbSets hide it.

## Unique Constraint Migration

| Entity | Business key | Old index | New active-only index | NOCASE preserved | Restore conflict verified |
|---|---|---|---:|---:|---:|
| System | `name` | `IX_systems_name` | Same name, `WHERE is_deleted = 0` | Yes | Yes |
| DatabaseSource | `system_id,name` | `IX_database_sources_system_id_name` | Same name, `WHERE is_deleted = 0` | Yes | Covered by metadata/root rules |
| DatabaseSource primary | `system_id` when primary | `IX_database_sources_system_id` | Same name, `WHERE is_primary = 1 AND is_deleted = 0` | N/A | Covered by metadata/root rules |
| BusinessFunction | `system_id,name` | `IX_business_functions_system_id_name` | Same name, `WHERE is_deleted = 0` | Yes | Covered by metadata/root rules |
| DatabaseObject | `database_source_id,schema_name,object_name` | `IX_database_objects_database_source_id_schema_name_object_name` | Same name, `WHERE is_deleted = 0` | Yes | Covered by metadata/root rules |
| DatabaseColumn name | `database_object_id,column_name` | `IX_database_columns_database_object_id_column_name` | Same name, `WHERE is_deleted = 0` | Yes | Covered by metadata/root rules |
| DatabaseColumn ordinal | `database_object_id,ordinal_position` | `IX_database_columns_database_object_id_ordinal_position` | Same name, `WHERE is_deleted = 0` | N/A | Covered by metadata/root rules |
| BusinessRule | `system_id,name` | `IX_business_rules_system_id_name` | Same name, `WHERE is_deleted = 0` | Yes | Covered by metadata/root rules |
| Integration | `integration_type,name,source_party_name,target_party_name` | `IX_integrations_integration_type_name_source_party_name_target_party_name` | Same name, `WHERE is_deleted = 0` | Yes | Covered by metadata/root rules |

All nine approved rules preserve column order and index names. No blanket `is_deleted` performance index was added.

## Active-only Name Reuse

Real SQLite verification soft-deletes a System with a valid audit triple and successfully inserts another active System with the same NOCASE name. Both physical rows remain.

## Restore Conflict

The same fixture attempts to return the old System to `is_deleted=0` while the replacement remains active. The preserved partial unique index rejects the operation, proving the later recovery slice must resolve the business-key conflict explicitly.

## Required Parent Validation

API tests create legal deleted System, DatabaseSource, and DatabaseObject fixtures and verify their normal child-create paths return not-found rather than attaching new active children. All validation is performed through ordinary filtered DbSets; no production bypass exists.

## KnowledgeDocument FTS Foundation

`KnowledgeDocumentSearchIndex.Upsert` removes an existing row and does not reinsert a deleted document. Rebuild enumerates active documents only. Both raw result and raw count queries join canonical `knowledge_documents` and explicitly require `d.is_deleted = 0`. A stale FTS row is intentionally retained in a real SQLite API test and never appears in search; rebuild subsequently removes it.

## Migration

Formal EF migration: `20260827144345_AddSoftDeleteOwnershipFoundation` / logical name `AddSoftDeleteOwnershipFoundation`. SQLite-controlled table rebuilds preserve existing business columns, timestamps, statuses, versions, child references, document revisions, Evidence, and FTS content while installing fields, constraints, FKs, and partial unique indexes.

## Migration Upgrade Verification

```text
Fresh migrate: PASS
Upgrade migrate: PASS
Existing rows preserved: PASS
ModelSnapshot: PASS
Unique indexes: PASS
FK: PASS
Check constraints: PASS
FTS: PASS
SQLite integrity: PASS
```

The upgrade begins at `20260823092808_AddImmutableKnowledgeDocumentRevisions`; explicit target-era seed SQL prevents current-model fields from being written before their migration exists.

## ModelSnapshot Verification

The snapshot contains all explicit root properties, eight query filters, creator/deleter RESTRICT relationships, DatabaseSource concurrency metadata, eight deletion audit constraints, and nine active-only uniqueness filters. `dotnet ef migrations has-pending-model-changes ... --no-build` reports: `No changes have been made to the model since the last migration.`

## Files Changed

- Eight root domain models and their eight EF configurations.
- Seven legacy-root create models/services/controllers plus the shared canonical creator record and current-user API resolution helper.
- DatabaseSource read projections and frontend contracts/tests.
- KnowledgeDocument FTS index/rebuild and raw search result/count defense.
- Migration, designer, and model snapshot.
- New B01 API/persistence tests plus two adjacent migration/mapping fixture updates required by the new schema.
- This report and `docs/DOCUMENT_INDEX.md`.

## Backend Build

`dotnet build SystemKnowledgeHub.sln --no-restore -c Release`: PASS — 0 warnings, 0 errors.

## Focused Persistence Tests

`SoftDeleteOwnershipFoundation*`: PASS — 5/5. Coverage includes fresh/upgrade migration, exact metadata, defaults, preservation, audit/FK enforcement, active reuse, restore conflict, all query filters, query plans, canonical ownership, required parents, and stale FTS defense.

The two affected legacy fixtures plus B01 tests passed 8/8 after converting pre-schema System setup to explicit target-era columns and selecting relationship FKs by principal type.

## Root Coverage Tests

All eight root create paths are exercised in one authenticated API flow:

| Root | Create path | Canonical current user captured | Snapshot captured |
|---|---|---:|---:|
| System | `POST /api/systems` | Yes | Existing creator-name snapshot |
| DatabaseSource | `POST /api/database-sources` | Yes | Existing creator-name snapshot |
| BusinessFunction | `POST /api/business-functions` | Yes | Existing creator-name snapshot |
| DatabaseObject | `POST /api/database-objects` | Yes | Existing creator-name snapshot |
| DatabaseColumn | `POST /api/database-objects/{id}/columns` | Yes | New column creator snapshot |
| BusinessRule | `POST /api/business-rules` | Yes | Existing creator-name snapshot |
| Integration | `POST /api/integrations` | Yes | Existing creator-name snapshot |
| KnowledgeDocument | `POST /api/knowledge-documents` | Yes | Existing required document snapshot |

Forged request actor names are deliberately different from the authenticated Current User and do not become the owner.

## Full Backend Tests

Approved deterministic serial gate: PASS — 150/150, 0 failed, 0 skipped, 62 seconds. `REV-GAP-011` remains deferred; the task-owned serial runsettings, key directory, and testhost were removed immediately after the cycle.

Affected API regression: PASS — 35/35 across Systems, BusinessFunctions, Database Knowledge authoring, BusinessRules, Integrations, KnowledgeDocuments, and Search.

## Query Plan Verification

`EXPLAIN QUERY PLAN` assertions pass for System name, DatabaseSource name/primary, BusinessFunction key, DatabaseObject key, DatabaseColumn name/ordinal, BusinessRule key, Integration key, KnowledgeDocument paged list, and the FTS canonical join. Plans use the intended existing/converted indexes; FTS uses its virtual-table index plus canonical document primary-key lookup. No measured regression required an additional performance index.

## Runtime Migration Verification

PASS on task-owned port `51901`, isolated Data Protection keys, and `F:\CodeX\Legacy Knowledge Hub\.tmp\delete-b01-runtime\runtime.db`. Fresh `dotnet ef database update` applied every migration through `20260827144345_AddSoftDeleteOwnershipFoundation`; the Release API then started against that database. Only task PID `25232` was stopped, the port was confirmed released, and the complete runtime directory was removed.

## API Smoke

- `GET /api/auth/options`: 200.
- Disposable local Administrator login: 204.
- `GET /api/current-user`: 200, canonical User 1 / `DELETE B01 Admin`.
- Representative active System list: 200 with active data.
- Authenticated System create/detail: 201 / 200.
- Authenticated DatabaseSource create: 201 with nonblank token.
- Database Object browse context: nonblank DatabaseSource token.
- Runtime DB inspection proved both creates stored creator User 1 / `DELETE B01 Admin`, not the forged request actor; DatabaseSource stored Version 1.

Deleted-root lookup/child-create and stale-FTS runtime behavior are covered by the focused real-SQLite WebApplicationFactory flow because no delete API exists in B01.

## Temporary SQLite Integrity

The standalone runtime database ended with `PRAGMA integrity_check = ok` and zero `PRAGMA foreign_key_check` rows. Fresh and upgrade migration tests assert the same results. Temporary database, WAL/SHM, keys, logs, verifier, runsettings, and the two legacy-test temp files created during the first regression cycle were removed.

## Repository DB Protection

Before and after values are identical: 724,992 bytes; `2026-08-26T14:32:28.8616945Z`; SHA-256 `A122F8D208338A8445DCF19CE27601634B98A0B35AC1575FDBB6ADF7A3422DA9`; WAL present; SHM present. Repository App_Data: UNCHANGED.

## Frontend Boundary

Only the DatabaseSource `concurrencyToken` type/strict decoder and three directly affected fixtures changed. There is no delete button, delete UI, restore UI, or recycle bin.

- `npm run type-check`: PASS.
- `npm run build`: PASS (existing Vite chunk-size advisory remains non-blocking).
- Affected Vitest with one worker: PASS — 6/6.
- Direct affected-file ESLint: PASS.
- A full-script lint attempt also traversed the repository and reported two unchanged, out-of-scope pre-existing errors in `CreateIntegrationDialog.vue` and `unknownItemContracts.ts`, plus one unchanged warning; no changed B01 file failed lint.

## New Gap Check

No new Blocker, High, or Medium gap was found. The existing `REV-GAP-011` default-parallel test infrastructure gap remains deferred and was not expanded. EF emits its known design-time required-dependent/global-filter advisory for excluded child entities; B01 root access remains filtered and no new independent child-root behavior was introduced.

## DELETE-B02 Readiness

DELETE-B02 READY: YES

The read/detail/list foundation can now rely on explicit active-root filtering and DatabaseSource tokens. B02 must remain a separate slice.

## Final Result

DELETE-B01 PASS

All PASS gates are satisfied: 8/8 roots, canonical ownership with safe legacy handling, DatabaseSource Version/token, explicit filters, nine active-only unique rules, reuse/restore-conflict behavior, active-parent validation, fresh/upgrade migration and snapshot, FTS defense, builds/tests/query plans/runtime/integrity, repository DB protection, verification cleanup, and the absence of delete/recovery product surface.
