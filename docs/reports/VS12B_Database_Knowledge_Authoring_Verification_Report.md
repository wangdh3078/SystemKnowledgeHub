# VS12B — Database Object / Column Knowledge Authoring + Known Values

## Result

**VS12B PASS**

## Implemented scope

- C10 — `POST /api/database-objects/{id}/columns`
- C11 — `PUT /api/database-objects/{id}/knowledge`
- C12 — `PUT /api/database-columns/{id}/knowledge`
- C13 — `POST /api/database-columns/{id}/known-values`
- C14 — `POST /api/database-columns/{id}/known-values/{knownValueId}/remove`
- RP-07 的对象级编辑和字段登记入口；DR-03 / DR-11 的字段业务知识、已知值维护状态。

未实现自动发现/导入、数据库连接、通用数据库编辑器、字段技术元数据编辑、Evidence/Relationship/UnknownItem 的新工作流、状态推进、删除核心知识对象或任何下一条 Vertical Slice。

## Persistence and API

- 复用既有 canonical `database_objects`、`database_columns`、`column_known_values`、`evidence`、`knowledge_updates` 与 `unknown_items` 模型；**没有新增 Migration，也没有第二套 Entity、Mapping 或 Service**。
- C10 校验父对象、正数字段顺序、非空技术标识、同对象名称/顺序唯一性和父对象 opaque token；新字段固定为 `Unknown`，不自动创建 Known Value、Evidence、Relation 或 UnknownItem。
- C11 只更新对象业务说明、访问方式、业务唯一键；业务唯一键必须引用当前已登记字段。C12 只更新字段业务说明。两者均不改变技术元数据或 KnowledgeStatus。
- C13 要求非空且同字段唯一的值/含义，更新 Column `updated_at` 与版本，但不自动推进 KnowledgeStatus。
- C14 要求显式 `confirmed=true`。若 `Evidence.SubjectDetailKey` 或未关闭 UnknownItem 的 `KnowledgeUpdate.SubjectDetailKey` 精确引用 `KnownValues:<值>`，返回 `422 reference_invalid`；否则只移除依赖的 Known Value 行。
- Q09/Q10 现在返回真实字段级 Evidence/Unknown 数量，以及字段抽屉中的 Relation、Evidence、开放待确认事项摘要，避免维护后丢失已有知识上下文。

## Main files

- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Application/DatabaseKnowledgeService.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Application/DatabaseKnowledgeQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Application/Models/DatabaseKnowledgeWriteModels.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Api/DatabaseObjectsController.cs`
- `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/Api/DatabaseColumnsController.cs`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/pages/DatabaseObjectDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/components/RegisterDatabaseColumnDialog.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/components/DatabaseObjectKnowledgeDrawer.vue`
- `src/SystemKnowledgeHub.Web/src/features/database-knowledge/components/ColumnDetailDrawer.vue`
- `tests/SystemKnowledgeHub.Api.Tests/Api/DatabaseKnowledgeAuthoringApiTests.cs`

## Focused tests

Executed:

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter FullyQualifiedName~DatabaseKnowledgeAuthoringApiTests
```

Result: **4 passed, 0 failed**.

The tests use real EF Core SQLite and HTTP endpoints to prove:

1. C10 writes a new `Unknown` Column and rejects duplicate names.
2. C11/C12 retain the existing `Inferred` status, Evidence and KnowledgeRelation.
3. C13/C14 can add then remove an unreferenced value without changing status.
4. C14 blocks both exact Evidence and open UnknownItem Investigation (`KnowledgeUpdate`) references.

## Build and type validation

- `dotnet build SystemKnowledgeHub.sln --no-restore` — **passed**, 0 warnings / 0 errors.
- `npm run type-check` — **passed**.
- `npm run build` — **passed**. Vite emitted only its standard bundle-size advisory; it is not a Specification Deviation.

## Focused runtime verification

Using the local ASP.NET Core API, Vite development server, SQLite and browser:

1. Opened `MES.TABLE_EQP` at RP-07 and confirmed the high-density column table, Table-level `关系与缺口` rail and existing `STATE_FLAG` evidence count.
2. Used `登记字段` to create `VS12B_RUNTIME_COLUMN`; it appeared in the table with `未知` status.
3. Opened Object Edit in the existing single Drawer, updated the object business description and observed the persisted result after the Drawer closed.
4. Opened `VS12B_RUNTIME_COLUMN` in the Column Drawer, edited its business description, added `VS12B_FREE`, and removed it via the explicit confirmation dialog. The Column status remained `未知`.
5. Added a real exact Evidence reference for `KnownValues:VS12B_EVIDENCE_REF`, then attempted its removal from the same Column Drawer. The UI displayed the `reference_invalid` message and retained the value.
6. The focused SQLite integration test separately exercised the equivalent open UnknownItem Investigation reference branch.
7. A runtime warning exposed two newly used Element Plus controls missing from the existing selective registration. `ElInputNumber` and `ElSwitch` were registered in the existing application bootstrap, then the page was reloaded and exposed one semantic spinbutton and one switch with no unresolved-component warning.

The intentionally named runtime verification field, its object/field descriptions, Known Value and Evidence remain in the existing development SQLite database. They were not physically deleted because the MVP does not expose a core knowledge-object delete operation and no direct database mutation was used to bypass it.

## Golden UI review

- RP-07 remains the frozen light desktop Detail layout: compact technical Column Table in Main Content and Table-level-only Context Rail.
- Object editing uses the existing single right Drawer rather than an edit Route or whole-page form.
- Column editing uses the current DR-03 Drawer in its DR-11 Edit state. Technical metadata stays read-only; business knowledge, Evidence, Known Values, Relations and Unknown Items retain separate responsibilities.
- Field registration is a focused Dialog from the Object Detail, and no second Drawer manager or generic form/table abstraction was introduced.
- UI text is Simplified Chinese; technical identifiers such as `MES.TABLE_EQP`, `STATE_FLAG` and `VARCHAR2(24)` remain unchanged.

## Specification deviation

No blocking Specification Deviation identified.

## Deferred

- DatabaseSource editing, live database connection, automatic schema discovery/import and technical metadata editing.
- Known Value-specific Evidence/UnknownItem authoring UI. The frozen model already supports exact `SubjectDetailKey` references; C14 enforces them, while creation continues through the existing Evidence/UnknownItem flows.
- KnowledgeStatus progression, relationship authoring and other existing Feature workflows remain separate explicit operations.

## Final validation and cleanup

- Final build, focused tests, type-check and frontend build: **PASS**.
- Temporary ASP.NET Core and Vite verification processes: **stopped**.
- Verification ports `5090` and `5173`: **released**.
- Temporary `.runtime-vs12b` logs: **deleted**.
