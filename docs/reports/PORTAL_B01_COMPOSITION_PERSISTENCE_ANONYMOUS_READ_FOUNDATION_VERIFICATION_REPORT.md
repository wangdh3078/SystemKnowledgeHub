# PORTAL-B01 Composition Persistence + Anonymous Read Foundation Verification Report

## Result

**PORTAL-B01 PASS**

PORTAL-B01 在冻结的 PORTAL-A01 边界内完成 Portal composition persistence、受控 target resolution、publication/lifecycle eligibility，以及两个专用匿名 GET read endpoints。没有实现 Admin Portal write API、Portal frontend、Search、Attachment delivery、Trust、Related Knowledge 或 Trace projection。

## Scope

- 新增 `PortalPage`、`PortalPageNode`、`PortalPageSection` 三个实体及同一 `KnowledgeHubDbContext` 映射。
- 新增闭合 `PortalTargetType`、`PortalPageNodeKind`、`PortalPageSectionSourceKind`、`PortalPageProjectionKind`。
- 新增 Portal 专用受控 resolver、composition/tree validator、page-oriented read queries 和 closed-discriminator response DTO。
- 新增且仅新增 `GET /api/portal/tree` 与 `GET /api/portal/pages/{id}`；Controller 显式 `[AllowAnonymous]`，没有 Portal mutation route。
- `SystemKnowledgeHub.Web` 未修改，Frontend verification 为 **NOT APPLICABLE**。

## Schema / Migration

Migration `20260903142533_AddPortalCompositionFoundation` 是 additive-only：只创建 `portal_pages`、`portal_page_nodes`、`portal_page_sections` 及其 CHECK、FK、filtered unique index 和 lookup index。它不修改现有知识表、不 backfill、不创建 Portal page，也不自动发布知识。

Fresh migration、从 `20260831170031_AddManualDiscoverySyncFoundation` 升级、rollback、`foreign_key_check`、root/non-root sibling partial unique indexes、section order unique index 和 `RESTRICT` 均由 focused migration tests 验证。

## PortalPage

`PortalPage` 持久化 A01 冻结的 title、Primary Target、publication/unpublication audit、create/update audit、positive concurrency version 和 soft-delete audit。Title 通过 EF value conversion trim 并由 1–200 CHECK/validation 保护；PrimaryTarget ID 受 JavaScript safe positive range 保护。默认 `IsPublished=false`，匿名 DTO 不暴露 version/concurrency/audit。

## PortalPageNode

`PortalPageNode` 使用 self-FK 和 page FK，均为 `RESTRICT`。Folder/Page shape、self-parent、non-negative order、publication audit、soft-delete audit 和 positive version 由 DB constraint 保护。应用验证另外阻止 cycle、resulting depth > 10、subtree move depth overflow 及 sibling order collision。

## PortalPageSection

Section 是 PortalPage aggregate 内的物理子项，没有独立 lifecycle、publish、version 或 route。DB CHECK 与应用 validation 同时约束 PrimaryTarget/ExplicitReference/Derived 的 reference nullable shape；特别使用显式 `IS NOT NULL` 避免 SQLite CHECK 三值逻辑接受空 ExplicitReference。Page 内 SortOrder 唯一且非负，Heading 为 trim 后 1–200。

## Tree Integrity

- root depth 1，最大 depth 10；depth 10 accepted，depth 11 rejected。
- cycle/self-parent/subtree-move overflow 被拒绝。
- active root SortOrder 和 active non-root `(ParentId, SortOrder)` 分别由 filtered unique index 保护。
- 输出采用 **flat ordered contract**；每个节点含 `nodeId/parentNodeId/title/nodeKind/pageId`。
- 读取按照每层 `SortOrder ASC, Id ASC` 形成确定性 pre-order；无效 Page node 及其会造成 dangling parent 的后代被 fail-closed 排除。
- 同一 page 多 placement 时，canonical breadcrumb 按逐层 ancestor `SortOrder`、再 `Id` 选择第一条有效路径。

## Target Resolver

Portal 专用 resolver 显式 switch/typed-query 支持且仅支持：System、BusinessFunction、DatabaseObject、KnowledgeDocument、Integration。所有 target query 使用 `AsNoTracking`、closed type buckets 和 current-row integrity checks；没有 reflection、dynamic target type 或 Admin detail DTO reuse。

DatabaseObject 同时验证其 DatabaseSource/System current context；BusinessFunction 验证 owning System；Integration 验证所有已绑定 System/DatabaseSource/DatabaseObject current context。不存在、soft-deleted 或 context-invalid target 不会解析。

## Publication Eligibility

匿名读取要求 page published/non-deleted、至少一条 published/non-deleted node-to-root path、全部 ancestor 有效、Primary Target eligible、全部 ExplicitReference eligible、section/handler/limit 均有效。不满足统一返回 sanitized `404 not_found`。Published KnowledgeDocument 通过；Draft、Archived、deleted 文档均失败。无资格 page node 不进入 tree。

## Anonymous API

- `GET /api/portal/tree`：返回最多 2,000 个 effective published nodes 的 bounded flat tree；2,001 返回 `422 portal_limit_exceeded`，不截断。
- `GET /api/portal/pages/{id}`：返回 page safe identity、canonical breadcrumb、稳定排序的 closed-discriminator sections。
- malformed/unsafe ID 返回 `400 validation_error`；不可读内容返回 `404 not_found`。
- 未定义的 Portal POST 仍由现有 fallback authorization 拒绝；没有新增 POST/PUT/PATCH/DELETE endpoint。

## Sanitization

Response 由 concrete DTO 构成，不包含 generic property bag、raw entity、connection profile/host/username/secret/connection string、technical/logical identity、raw SQL/provider error、Discovery artifacts、storage key/path、creator/updater/publisher identity、concurrency token 或 Admin capability。

DatabaseStructure 仅投影 schema/name/type/business description/manual EstimatedRows/access mode/business keys，以及最多 500 个按 ordinal/id 稳定排序的 columns。Column DTO 仅包含 ordinal、columnName、nativeDataType、nullable、databaseComment。超过该 bounded column cap 时包含 DatabaseStructure 的 page fail closed。

## Security Regression

匿名 Portal tree/page 对 eligible content 返回 200；匿名访问 `/api/systems`、`/api/database-objects`、`/api/database-discovery/runs`、`/api/users` 和既有 attachment route 仍返回当前 401 行为。Default/Fallback Viewer policy、Editor/Administrator policy、antiforgery 和现有 Controllers 均未降低。

## No Duplicate Knowledge Truth

三个 Portal 表只保存 page/tree/section 的组织、引用、投影类型和顺序；没有存 Markdown、rendered HTML、System/BusinessFunction/DatabaseObject/Integration copy、KnowledgeStatus/Evidence/HumanConfirmation/KnowledgeRelation/Trace/attachment/search copy。读取实时投影 canonical current rows。

Portal read path 全部 `AsNoTracking`，没有写入 KnowledgeRelation 或任何 canonical knowledge table。Migration 也不修改这些表。

## Projection Support Matrix

| ProjectionKind | Persistence enum | B01 handler | B01 behavior |
| --- | --- | --- | --- |
| Summary | Yes | Yes, all five v1 targets | Concrete safe summary DTO |
| KnowledgeDocumentBody | Yes | Yes, Published current document only | Raw Markdown for existing safe renderer |
| StructuredOverview | Yes | Yes, System/BusinessFunction/DatabaseObject/Integration | Concrete allowlisted DTO per target |
| DatabaseStructure | Yes | Yes, DatabaseObject only | Bounded safe column projection |
| AttachmentList | Yes | No (B04) | Fail closed |
| TrustSummary | Yes | No (B04) | Fail closed |
| RelatedKnowledge | Yes | No (B04) | Fail closed |
| Traceability | Yes | No (B04) | Fail closed |

## Performance Limits

Frozen constants remain: depth 10, effective nodes 2,000, sections/page 30, full document body sections/page 5, derived related results 20, trace 200 nodes/300 edges/depth 2, search default 20/max 100. B01 additionally fixes DatabaseStructure columns at 500.

Tree traversal uses at most ten bounded level queries, then page/section and five target-type bulk reads. Page projection resolves target buckets in bulk. Command interception proves the DB reader count is identical for one versus thirty sections and remains under a fixed cap; no section causes an independent query.

## Migration Verification

- `dotnet test ... --filter "FullyQualifiedName~Portal"`: **PASS, 28/28**.
- Fresh/current-chain upgrade and rollback migration tests: **PASS**.
- `dotnet ef migrations has-pending-model-changes` with an absolute task-owned `SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH`: **PASS — no pending model changes**.
- Final Release solution build: **PASS, 0 warnings / 0 errors**.

## Runtime Smoke

真实 HTTP smoke 使用 `ASPNETCORE_ENVIRONMENT=Verification`、task-owned absolute SQLite/Data Protection/Attachment/Serilog paths 和 isolated port 5091；没有运行 Development seed。

Task fixture 为 `MES → 生产管理 → Lot Track In`，page Primary Target 是 BusinessFunction，并包含 StructuredOverview、Published KnowledgeDocumentBody、DatabaseStructure 三个 sections。匿名 tree/page 均为 200；page 同时包含业务正文和 `MES.LOT` safe structure，敏感 canary、technical identity 和 Admin 字段均未出现；匿名 Admin API 为 401。随后把引用文档改为 Archived，page 返回 sanitized 404，tree 排除 Page node且不泄露内容。

## Repository Data Protection

验证前后仅通过 filesystem metadata/hash 检查 repository-owned SQLite，未用 SQLite/EF 打开：

| File | Before | After |
| --- | --- | --- |
| `system-knowledge-hub.db` | 1,257,472 bytes; `2026-09-03T12:18:00.8468494Z`; SHA-256 `3671141339C979BE2DCA6853811D18E2BD2CD607D99CE231C3BE29B2EB77F271` | 完全一致 |
| `system-knowledge-hub.db-wal` | Missing | Missing |
| `system-knowledge-hub.db-shm` | Missing | Missing |

**REPOSITORY DATA PROTECTION: PASS**

## Cleanup

Verification API processes已停止，ports 5089/5091 无 listener。临时 Seeder project/build、task SQLite、Data Protection keys、attachment/log roots 和 EF design-time directories 已按校验后的精确路径删除。未终止用户既有进程。

## Known Gaps

- AttachmentList、TrustSummary、RelatedKnowledge、Traceability handlers 按冻结顺序留给 PORTAL-B04；B01 对这些 persisted sections fail closed。
- Admin Knowledge Composition write API/UI、Preview/Publish/Unpublish workflow 留给 PORTAL-B02。
- PortalLayout 和正式匿名阅读 frontend 留给 PORTAL-B03。
- Portal Search 和 Portal attachment delivery 未实现，符合 B01 scope。

这些是冻结的后续阶段，不是 B01 未披露缺口。

## Final Status

| Gate | Status |
| --- | --- |
| PORTAL-B01 | PASS |
| PORTAL PAGE PERSISTENCE | PASS |
| PORTAL TREE PERSISTENCE | PASS |
| PORTAL SECTION PERSISTENCE | PASS |
| MIGRATION | PASS |
| TREE INTEGRITY | PASS |
| DEPTH LIMIT | PASS |
| ORDERING | PASS |
| CONTROLLED TARGET RESOLVER | PASS |
| KNOWLEDGE DOCUMENT LIFECYCLE | PASS |
| PUBLICATION ELIGIBILITY | PASS |
| ANONYMOUS TREE API | PASS |
| ANONYMOUS PAGE API | PASS |
| ADMIN AUTHORIZATION PRESERVED | PASS |
| SANITIZATION | PASS |
| NO DUPLICATE KNOWLEDGE TRUTH | PASS |
| NO KNOWLEDGE RELATION MUTATION | PASS |
| HARD LIMITS | PASS |
| BACKEND REGRESSION | PASS — 89/89 focused existing tests |
| MIGRATION CHAIN | PASS |
| DEPENDENCY SECURITY | PASS — no vulnerable direct/transitive package |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |

**PORTAL-B01 COMPLETE**

**PORTAL-B02 READY: YES**
