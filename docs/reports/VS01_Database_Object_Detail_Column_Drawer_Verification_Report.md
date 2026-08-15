# System Knowledge Hub — VS-01 Verification Report

## A. Result

`VS01 PASS`

VS-01 — Database Object Detail + Column Drawer is implemented as a runnable end-to-end slice. The frozen specifications and Golden UI assets were not modified. No `VS01_Conflict_Report.md` was required.

## B. Implemented Scope

### Persistence and EF mapping

- Added the smallest canonical SQLite schema required for Database Knowledge detail reads.
- Added simple EF/domain entities for `DatabaseSource`, `DatabaseObject`, `DatabaseColumn` and `ColumnKnownValue`.
- Added the required `System` parent record/table only because the frozen Database Model requires the physical `database_sources.system_id → systems.id` FK. No System feature/use case was implemented.
- Added explicit enum-to-English-TEXT mapping, nullability, checks, uniqueness, indexes and `Restrict` delete behavior.
- Added app-managed integer `version` only to `DatabaseObject` and `DatabaseColumn` and a small opaque `ConcurrencyTokenCodec` read path.
- Added development-only, idempotent seed initialization for one MES example.

### Query and API

- Implemented frozen Q09 `GetDatabaseObjectDetail` with direct EF projections and selected-column ownership validation.
- Implemented frozen Q10 `GetColumnDetail` with direct EF projections and Known Values.
- Implemented only the canonical routes:
  - `GET /api/database-objects/{id}` with optional `selectedColumnId`.
  - `GET /api/database-columns/{id}`.
- Added 400, 404 and invalid-reference 422 responses through the frozen error shape.

### Frontend

- Added strict typed contract decoders, Feature API and safe-ID validation.
- Added page- and Drawer-local composables with loading, error, cancellation and reload state.
- Added `/database/:id`, Database Object Detail, table-level Context Rail, dense Element Plus Column Table and the global Column Detail Drawer.
- Added selected-row and `selectedColumnId` URL persistence without a generic URL-state framework.
- Added Drawer-local Business Knowledge, Evidence, Unknown Items, Knowledge Progression, metadata, Known Values and field-level Relations sections.
- Kept all non-slice authoring controls visible but disabled.
- Added focused backend and frontend tests.

## C. Files Created / Modified

### Backend

Created:

- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Domain/KnowledgeStatus.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Domain/KnowledgeSystem.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Domain/DatabaseSource.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Domain/DatabaseObject.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Domain/DatabaseColumn.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Domain/ColumnKnownValue.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Application/DatabaseKnowledgeQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Application/Models/DatabaseKnowledgeReadModels.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Api/DatabaseObjectsController.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Api/DatabaseColumnsController.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Persistence/KnowledgeSystemConfiguration.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Persistence/DatabaseSourceConfiguration.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Persistence/DatabaseObjectConfiguration.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Persistence/DatabaseColumnConfiguration.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Persistence/ColumnKnownValueConfiguration.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Persistence/DatabaseKnowledgeDevelopmentData.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Concurrency/ConcurrencyTokenCodec.cs`
- `src/SystemKnowledgeHub.Api/Persistence/KnowledgeHubDesignTimeDbContextFactory.cs`
- `src/SystemKnowledgeHub.Api/Shared/Api/ApiIdParser.cs`
- `src/SystemKnowledgeHub.Api/Shared/Api/Contracts/ApiErrorResponse.cs`

Modified:

- `src/SystemKnowledgeHub.Api/Program.cs`
- `src/SystemKnowledgeHub.Api/Persistence/KnowledgeHubDbContext.cs`
- `src/SystemKnowledgeHub.Api/Persistence/DbContextConfiguration.cs`

### Frontend

Created:

- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/api/databaseKnowledgeContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/api/databaseKnowledgeApi.ts`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/composables/useDatabaseObjectDetail.ts`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/composables/useDatabaseColumnDetail.ts`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/components/KnowledgeProgression.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/components/DatabaseObjectContextRail.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/components/ColumnDetailDrawer.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/pages/DatabaseObjectDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/database-knowledge.css`
- `src/SystemKnowledgeHub.Web/src/components/data-display/KnowledgeStatusBadge.vue`

Modified:

- `src/SystemKnowledgeHub.Web/src/app/bootstrap.ts`
- `src/SystemKnowledgeHub.Web/src/app/navigation/navigation.ts`
- `src/SystemKnowledgeHub.Web/src/app/router/routes.ts`
- `src/SystemKnowledgeHub.Web/src/app/router/router.d.ts`
- `src/SystemKnowledgeHub.Web/src/components/feedback/ErrorState.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/AppSidebar.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/ContextRailHost.vue`
- `src/SystemKnowledgeHub.Web/src/layouts/DrawerHost.vue`
- `src/SystemKnowledgeHub.Web/src/styles/tokens.css`
- `src/SystemKnowledgeHub.Web/src/styles/app.css`

### Tests

- `tests/SystemKnowledgeHub.Api.Tests/TestSupport/BootstrapWebApplicationFactory.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Persistence/DatabaseKnowledgeMappingTests.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Application/DatabaseKnowledgeQueriesTests.cs`
- `tests/SystemKnowledgeHub.Api.Tests/Api/DatabaseKnowledgeApiTests.cs`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/api/databaseKnowledgeContracts.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/api/databaseKnowledgeApi.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/composables/useDatabaseObjectDetail.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/composables/useDatabaseColumnDetail.spec.ts`
- `src/SystemKnowledgeHub.Web/src/components/data-display/KnowledgeStatusBadge.spec.ts`

### Migration

- `src/SystemKnowledgeHub.Api/Persistence/Migrations/20260813124350_InitialDatabaseKnowledge.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/20260813124350_InitialDatabaseKnowledge.Designer.cs`
- `src/SystemKnowledgeHub.Api/Persistence/Migrations/KnowledgeHubDbContextModelSnapshot.cs`

## D. Database

### Tables and columns

- `systems`: canonical System parent columns needed by the required FK; no System page or write flow was added.
- `database_sources`: `id`, `system_id`, `name`, `engine`, environment/instance/service/database descriptors, description, `is_primary`, minimal creation snapshot and timestamps. No `KnowledgeStatus` is persisted.
- `database_objects`: source/schema/name/type, business description, row estimate, access mode, PK/business-key JSON arrays, minimal creation/status-change snapshots, timestamps and `version`.
- `database_columns`: parent/ordinal/name/type/nullability/default/comment/business description, status-change snapshot, timestamps and `version`.
- `column_known_values`: parent, value, meaning, sort order and timestamps.

### Foreign keys and delete behavior

- `database_sources.system_id → systems.id` — `Restrict`.
- `database_objects.database_source_id → database_sources.id` — `Restrict`.
- `database_columns.database_object_id → database_objects.id` — `Restrict`.
- `column_known_values.database_column_id → database_columns.id` — `Restrict`.

### Unique constraints / indexes

- Unique System name.
- Unique Database Source `(system_id, name)` and filtered single primary source per System.
- Unique Database Object `(database_source_id, schema_name, object_name)`.
- Unique Column name and ordinal within a Database Object.
- Unique Known Value `(database_column_id, value_text)`.
- Canonical browse/status/name and child ordering indexes from the frozen Database Model are present.
- SQLite check constraints cover closed English enum values, boolean integer flags, positive ordinals/version, non-negative estimated rows and valid JSON arrays.

### Migration and data strategy

- Migration: `20260813124350_InitialDatabaseKnowledge` / `InitialDatabaseKnowledge`.
- Development-only initializer applies migrations and inserts data only when no Database Object exists.
- Test fixtures use one kept-open real SQLite in-memory connection, migrations and the same bounded seed.
- Seed: System `MES` id 12, Database Source id 9, `MES.TABLE_EQP` id 45, eight realistic columns, `STATE_FLAG` id 123 and Known Values `10`, `20`, `30`.

The physical model matches the frozen Database Model. The parent `systems` table is present solely to preserve its required canonical FK and is not a second or inferred schema.

## E. API

### Routes

- `GET /api/database-objects/{id}`
- `GET /api/database-objects/{id}?selectedColumnId={columnId}`
- `GET /api/database-columns/{id}`

### Contract result

- Q09 and Q10 response objects are explicit read models; no EF entity is returned.
- IDs are JSON numbers and route/query inputs reject non-positive or non-numeric values.
- Frontend boundaries reject IDs outside `Number.MAX_SAFE_INTEGER`.
- Frozen English enum wire values are preserved; Chinese labels are frontend-only.
- Nullable properties are emitted as frozen nullable values, not omitted substitutes.
- Both detail APIs return an opaque `concurrencyToken` (`v1_...`) derived from the local integer version. The frontend stores it in the typed local response and never parses or displays it.
- Q09 validates that `selectedColumnId` belongs to the requested object and returns the frozen invalid-reference error instead of opening another object's column.

Result: request and response shapes conform to the frozen API Contract.

## F. Backend Tests

- Provider: real `Microsoft.EntityFrameworkCore.Sqlite`; EF Core InMemory was not used.
- Final count: **15 passed, 0 failed, 0 skipped**.
- Persistence coverage: table mapping, FK delete behavior, nullability, concurrency marking, SQLite PRAGMAs and enforced Known Value uniqueness.
- Query coverage: Q09, Q10, selected-column ownership, missing resources, KnowledgeStatus mapping, Known Values and non-empty concurrency tokens.
- API coverage: canonical routes, 200 shape, 400 invalid IDs, 404 missing resources and 422 invalid selected-column reference.
- `dotnet restore SystemKnowledgeHub.sln --configfile NuGet.Config`: passed.
- `dotnet build SystemKnowledgeHub.sln --no-restore`: passed with 0 warnings and 0 errors.
- `dotnet test SystemKnowledgeHub.sln --no-build --no-restore`: passed.

## G. Frontend Verification

- `npm run type-check`: passed.
- `npm run lint`: passed.
- `npm run test -- --pool=forks --maxWorkers=1`: **18 passed, 0 failed** across 9 files.
- `npm run build`: passed; Vite production bundle generated.

Focused tests cover contract decoding, safe ID boundaries, loading/success/error state, column selection opening the global Drawer descriptor, Drawer-local ID loading, single-overlay replacement, Chinese KnowledgeStatus labels and shared network/API error behavior.

## H. Runtime Verification

The API and Vite app were started locally and exercised in the Codex in-app browser against the development SQLite file.

```text
Browser /database/45
→ typed Vue Feature API
→ GET /api/database-objects/45
→ ASP.NET Core Controller
→ DatabaseKnowledgeQueries
→ EF Core SQLite
→ MES.TABLE_EQP page renders
```

```text
Click STATE_FLAG
→ selected row + selectedColumnId=123
→ one global Drawer descriptor
→ GET /api/database-columns/123
→ SQLite projection
→ Column Detail Drawer renders
```

Verified additionally:

- Reload restores `selectedColumnId=123`, selected row and Drawer.
- Close action removes the query parameter; reopening restores it.
- Known Values show `10`, `20`, `30` and `30 → Unknown / Offline`.
- Missing object shows page-local error/retry.
- Invalid selected-column reference recovers to `/database/45` with an explanatory notice.
- Filter empty state and reset work.
- Browser console contained no application warning/error.
- Verification-only API and Vite processes were stopped; ports 5090 and 5173 were confirmed released.

## I. Golden UI Review

Golden References:

- `product-design/final-ui/RP-07_Database_Object_Detail.png`
- `product-design/final-ui/DR-03_Column_Detail.png`

Review result:

- Application Shell and official product name remain aligned with the frozen baseline.
- Main Content, table-level Context Rail and single Column Drawer preserve the Golden information architecture.
- Column Table remains dense, technical and easily scannable; selected and hover states are explicit.
- Evidence uses an icon + count treatment and remains visible at 1366 with the Drawer open.
- Knowledge Status uses frozen Chinese labels and Knowledge Progression is explicitly non-clickable, not a tab.
- Drawer uses the expected hierarchy: Business Knowledge, Evidence and Unknown Items first; metadata, Known Values and Relations are collapsible low-frequency sections.
- At 1920, Main Content + Context Rail + 500 px Drawer coexist. At 1440/1366, Context Rail is hidden and the 440 px Drawer plus Main Content are prioritized without viewport overflow.
- All product copy is Simplified Chinese while technical identifiers remain original.

Expected, documented differences:

- Golden screenshots contain historical English UI copy; frozen implementation rules require Simplified Chinese and the official `系统知识中心 / System Knowledge Hub` name.
- Golden examples contain rich Evidence, Relations and Unknown Items. VS-01 intentionally does not create those tables/features; the frozen response arrays/counts are empty and the UI shows truthful empty states. No mock evidence or adjacent workflow was invented.

Detailed visual evidence and two QA iterations are recorded in `docs/reports/design-qa.md`. Final Design QA result: `passed`.

## J. Specification Deviations

`None`

The required minimal `systems` parent table is not a deviation: it preserves the physical FK explicitly required by the frozen Database Model. No frozen document, API route, response contract, domain concept, Golden asset or UI information hierarchy was modified.

## K. Deferred

Explicitly not implemented in VS-01:

- Create or Edit Database Source/Object/Column.
- Add or Remove Known Value.
- Add/Update Evidence.
- Add Relationship.
- Add Unknown Item or Unknown Item workflow.
- Change KnowledgeStatus.
- Full Database Objects List/browse feature.
- Database metadata import.
- Search, Dashboard, Business Function, Business Rule, Integration, authentication or authorization.

VS-01 stops at the verified Database Object Detail + Column Detail Drawer read path. No VS-02 work was started.
