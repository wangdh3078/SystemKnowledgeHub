# VS12A — Database Objects List + Database Source/Object Registration

## Result

**VS12A PASS**

## Implemented scope

- Q08 — `GET /api/database-objects`
- C08 — `POST /api/database-sources`
- C09 — `POST /api/database-objects`
- RP-06 数据库对象列表，以及“新增知识对象 → 数据库知识”的来源登记 / Table、View 最小登记流程。

未实现 C10～C14、自动数据库发现、连接串或凭据存储、实际数据库连接、字段自动导入、Database Object 编辑、Evidence、Relationship、UnknownItem 或 KnowledgeStatus 写操作。

## Persistence and API

- 复用既有 canonical `database_sources`、`database_objects` 和 `systems` 表、实体与 EF mapping；**没有新增 Migration，也没有第二套数据库知识模型**。
- C08 校验 System 存在、同系统来源名称唯一、单一主来源约束，以及敏感凭据文本；Source 不设置 KnowledgeStatus。
- C09 只登记 `Table` / `View`；校验 Source、Schema / 对象名称唯一性、受控 AccessMode、非负估算行数和去重键字段；新对象固定为 `Unknown`，不自动创建 Column、Evidence、Relation 或 UnknownItem。
- Q08 支持冻结的 `systemId`、`databaseSourceId`、`schema`、`objectType`、`knowledgeStatus`、`search`、分页和排序白名单；字段命中归并到其 DatabaseObject。Source 与 System Context 不一致时返回 `422 reference_invalid`。
- `DatabaseSource` 沿用冻结 Database Model，不新增物理 version 字段。C08 所需的 opaque `concurrencyToken` 在创建响应中返回；当前 MVP 没有 DatabaseSource 更新 Use Case，因此它不引入第二套 schema 或并发机制。

## Main files

- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Application/DatabaseKnowledgeQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Application/DatabaseKnowledgeService.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Api/DatabaseSourcesController.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Api/DatabaseObjectsController.cs`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/pages/DatabaseObjectsListView.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/components/CreateDatabaseKnowledgeFlow.vue`
- `tests/SystemKnowledgeHub.Api.Tests/Api/DatabaseObjectsListRegistrationApiTests.cs`

## Focused tests

Executed:

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter FullyQualifiedName~DatabaseObjectsListRegistrationApiTests
```

Result: **3 passed, 0 failed**.

The tests cover Q08 Source/System context with Column search roll-up, C08 SQLite persistence and subsequent browse-context visibility, and C09 registration with an `Unknown` status and immediate list visibility.

## Build and type validation

- `dotnet build SystemKnowledgeHub.sln --no-restore` — **passed** (0 warnings, 0 errors).
- `npm run type-check` — **passed**.
- `npm run build` — **passed**. Vite reported only its standard bundle-size advisory; it is not a build failure or Specification Deviation.

## Focused runtime verification

Using the local ASP.NET Core API, Vite development server, SQLite and browser:

1. Opened `/database-objects`; RP-06 showed the light shell, Database / Schema browser, compact object table and the existing `MES.TABLE_EQP` row.
2. Opened `新增 → 数据库知识 → 登记数据库来源`, chose `MES`, and created `MES 运行时验证库` with Engine `SQLite`.
3. The list returned to the new System/Source context and displayed the explicit empty state.
4. Opened `新增 → 数据库知识 → 登记数据库对象`, selected the newly registered source, and registered `MES.TABLE_RUNTIME_VERIFY`.
5. The list refreshed at the Source context and displayed the new object with status `未知`; opening the row navigated to the existing Database Object Detail route successfully.
6. Browser console error check returned no errors.

The two intentionally named runtime verification records remain in the existing development SQLite database. They were not physically deleted: the MVP has no core knowledge-object delete operation, and no direct database mutation was used to bypass that constraint.

## Golden UI review

RP-06 inherits the frozen light desktop shell and high-density database language: Database / Schema browse is in Main Content, not a Context Rail; the table remains compact and technical; and the list limits itself to Find / Filter / Browse / Navigate. Source/object registration uses the existing single Dialog Host and progressive minimal forms, rather than a full-page CRUD form. All product-facing text is Simplified Chinese; technical identifiers remain original.

## Specification deviation

No blocking Specification Deviation identified.

The frozen RP-06 mock includes an access-mode filter, while the frozen API Contract's Q08 filter allowlist does not. The implementation follows the frozen API Contract: it displays access mode but does not add an uncontracted filter.

## Deferred

- C10～C14: Column registration, object/column knowledge editing and Known Value maintenance.
- DatabaseSource editing, credentials, connection strings, actual database connection and automatic schema discovery.
- Database Object relationship, evidence, status progression and UnknownItem authoring beyond their already implemented generic feature entry points.
- Global Search, Dashboard and any generic CRUD / repository / mapping framework.

## Final validation and cleanup

- Final build, focused tests, type-check and frontend build: **PASS**.
- Temporary ASP.NET Core and Vite verification processes: **stopped**.
- Verification ports `5090` and `5173`: **released**.
- Temporary `.runtime-vs12a` runtime logs: **deleted**.
