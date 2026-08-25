# TRACE-B01 — Derived Trace Read Foundation Verification Report

## Result

**TRACE-B01 PASS**。`GET /api/knowledge-documents/{id}/traceability` 已以只读、实时派生、固定 depth-2、root-specific page model 实现；Requirement、Specification、TestCase、coverage、trust、direct Supersedes、cycle defense、limits、authorization、fail-closed 与前端严格 decoder 均通过验证。没有产品 UI、schema、migration、新 relation type、cache 或 background job 变更。

## Worktree Baseline

- Branch: `main`
- Baseline commit: `4c968f4` (`📃 docs: 添加文档`)
- Baseline state: `origin/main` 同步，`git status --short`、`git diff`、`git diff --check` 均为空。
- Repository database baseline: 724,992 bytes；mtime UTC `2026-08-24T15:38:12.9720638Z`；SHA-256 `854EEDAF15B04F5AFD549769D6045689E3C0240C3B41AE2571EA1711F5085CA6`。

## Normative Authority

实现按以下 authority 执行：

1. `docs/design/TRACE_A01_TRACEABILITY_ARCHITECTURE_AND_CONTRACT_DECISION.md`
2. `docs/design/REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md`
3. `docs/design/KC_C01_RELATIONSHIP_VOCABULARY_ARCHITECTURE_DECISION.md`
4. `docs/design/SYSTEM_UI_COMPONENT_AND_INTERACTION_BASELINE.md`
5. `docs/reports/PHASE_NEXT_MAJOR_PHASE_PLANNING_R01.md`
6. `docs/reports/PHASE_REV_DELTA_VERIFICATION_R01_REPORT.md`
7. 当前 TRACE-B01 task definition、`docs/DOCUMENT_INDEX.md` 与 adjacent implementation。

未修改 frozen authority、Golden UI asset 或 task definition。

## Implementation Summary

- 新增具体 `Traceability` Application read feature、封闭 DTO/enums 与 fixed-path cycle guard。
- 在现有 `KnowledgeDocumentsController` 增加唯一 trace read route，并注册 scoped query。
- 直接读取 current KnowledgeDocument head、KnowledgeRelation 与 Evidence；coverage 使用独立 `EXISTS`，display 使用有界候选集。
- 前端新增 API types、runtime decoder、client 与 contract tests；未增加 Vue component、CSS、route、tab、section、Trace Tree 或 impact UI。

## Files Changed

- `src/SystemKnowledgeHub.Api/Features/Traceability/Application/TraceabilityQueries.cs`
- `src/SystemKnowledgeHub.Api/Features/Traceability/Application/TraceTraversalGuard.cs`
- `src/SystemKnowledgeHub.Api/Features/Traceability/Application/Models/TraceabilityModels.cs`
- `src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Api/KnowledgeDocumentsController.cs`
- `src/SystemKnowledgeHub.Api/Program.cs`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/traceabilityContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/traceabilityApi.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/traceabilityContracts.spec.ts`
- `tests/SystemKnowledgeHub.Api.Tests/Api/TraceabilityApiTests.cs`
- `docs/PROJECT_FILE_MAP.md`
- `docs/DOCUMENT_INDEX.md`
- 本报告。

## Architecture Compliance

实现是 feature-first concrete query，直接使用 `KnowledgeHubDbContext` 与 `AsNoTracking`。未引入 repository/UoW、CQRS/MediatR、generic graph/projection framework、第二套关系模型、graph database、cache 或 read model table。TRACE 与 System Unified View 保持各自清晰的业务投影。

## Canonical Truth / Read-only Boundary

Trace response 每次从当前 canonical tables 派生。GET 不写 KnowledgeDocument、revision、KnowledgeRelation、Evidence 或 FTS；不创建 materialized trace、coverage snapshot 或 lineage record。现有 relationship authoring 与 canonical write service 未修改。

## API Contract

Endpoint: `GET /api/knowledge-documents/{id}/traceability`。

- root 使用 `root.documentType` 区分 Requirement / Specification / TestCase page model。
- 共同字段：`root`、`coverage`、`lineage`、`cycleDetected`、`isTruncated`、`truncationReasons`、`limits`。
- document trust：ID、DocumentType、title、lifecycle、KnowledgeStatus、current revision、Evidence/HC count、confirmation coverage。
- relationship trust：ID、RelationType、direction、KnowledgeStatus、Evidence/HC count。
- 不返回 BodyMarkdown、HTML、SVG、snippet 或 relationship description。
- 非法 ID → `400 validation_error`；missing root → `404 not_found`；unsupported root → `422 business_rule_violation`；invalid selected endpoint → `422 reference_invalid`。

## Requirement Projection

Requirement 返回 `specifications[]`（每项含 SpecifiedBy、Specification trust、branch coverage、nested TestCases）、`directTestCases[]`、frozen `upstreamRequirements[]`、coverage 与 lineage。VerifiedBy 仅表示 Test Definition。相同 TestCase 通过 direct 与 Specification 两条语义 path 到达时，两条 path context 均保留。

## Specification Projection

Specification 返回 immediate `upstreamRequirements[]`（incoming SpecifiedBy）与 `testCases[]`（outgoing VerifiedBy），以及 `hasTestDefinition` / `MissingTestDefinition`、trust、lineage、cycle/truncation/limits。

## TestCase Projection

TestCase 返回 `directRequirements[]`（incoming direct VerifiedBy）与 `upstreamSpecifications[]`；每个 Specification entry 可携带其 immediate upstream Requirements。未开放 arbitrary ancestors，也未将 execution result 混入 VerifiedBy。

## Structural Coverage

Structural coverage 与 KnowledgeStatus / Evidence / HC trust 独立：

- Requirement `hasSpecification`：至少一条 active SpecifiedBy → Specification。
- `hasDirectTestDefinition`：至少一条 active direct VerifiedBy → TestCase。
- `hasSpecificationTestDefinition`：至少一个 active Specification 具有 active VerifiedBy → TestCase。
- `hasAnyTestDefinition` 是 direct / specification-path 两者的 OR。
- 完整 coverage 由独立数据库 `EXISTS` 计算，不受 display truncation 影响。

## Missing-Link Rules

- 无 active Specification → `MissingSpecification`。
- direct 与 specification-path 均无 active Test Definition → `MissingTestDefinition`。
- Specification branch 无 active Test Definition → branch `MissingTestDefinition`。
- Requirement 存在任一 Test Definition 时不产生全局 `MissingTestDefinition`，即使另一个 Specification branch 缺失。
- Archived root 为 `ExcludedArchived`，不生成缺口。

## Trust Projection

Document 与 relationship 的 Evidence 以 `(subject_type, subject_id)` 批量聚合；`evidenceCount` 统计全部 Evidence，`humanConfirmationCount` 仅统计 `HumanConfirmation`。Document 同时返回 `NoConfirmation`、`LegacyConfirmationUnknown`、`CurrentRevisionConfirmed` 或 `ChangedSinceConfirmation` 及 last confirmed revision。Structural coverage 不读取或依赖 trust 状态。

## Lifecycle Semantics

Draft 与 Published root/child 均 eligible。Archived child 不参与 structural expansion 或 coverage；Archived root 仍可读，但 eligibility 为 `ExcludedArchived`，structural collections 与 gaps 为空。Direct Supersedes lineage 是非 structural context，可独立读取，不改变 lifecycle/coverage。

## Revision Boundary

Trace 使用 KnowledgeDocument current head 与 `currentRevisionNumber`，不读取历史 body 来替代当前内容。HC revision snapshot 只用于 confirmation coverage。Revision creation、save/no-op、history、compare、restore、FTS current-only contract 均未修改，并由完整 backend regression 保护。

## Supersedes Lineage

仅返回 root 的 direct outgoing / incoming `Supersedes`，outgoing 与 incoming 分组明确，total 独立计算；最多显示 20 entries。Lineage 不进入 structural coverage、不递归、不改变 lifecycle，也不引入新的 relation type。

## Cycle Defense

`TraceTraversalGuard` 对固定 path 使用 request-local path node/relation uniqueness 检查；同一 TestCase 经不同有效语义 path 可重复显示。Supersedes 对 direct reciprocal cycle 设 `cycleDetected=true`。防御测试覆盖 repeated node 与 repeated relationship，未对数据库做修复性写入。

## Limits / Truncation

- `maxDepth=2`
- `maxNodes=200`（unique displayed document IDs，包含 root）
- `maxEdges=300`
- `maxLineageEntries=20`
- structural reason 为 `MaxNodes` / `MaxEdges`；lineage 另有 `lineage.isTruncated`。

Fan-out fixture 实测：152 unique nodes、300 edges、143,785 UTF-8 bytes、`isTruncated=true`。Payload 不含 BodyMarkdown/RenderedHtml；相同 TestCase metadata 的重复只发生在必须保留的 page-model path context 内，并受 300-edge hard cap 约束。

## Deterministic Ordering

所有集合由 backend 排序：SQLite NOCASE title，然后 document ID、relationship ID；Requirement direct relation type order 固定，lineage outgoing / incoming 分组固定。重复读取与 focused ordering assertions 一致。

## Authorization

Viewer、Editor、Administrator 均返回 200；Anonymous 返回 401。复用现有 fallback policy / access-level handler，未新增 TraceViewer、ACL、tenant、space 或 permission model。

## Invalid Reference Fail-Closed

所选 structural 或 lineage relationship 的端点缺失、类型不符或 DocumentType 不符合 contract 时，整个 trace request 返回 `422 reference_invalid`，`details=null`；测试确认不会泄露 invalid target ID/title/body/metadata。

## Query Strategy

查询阶段明确且有界：root projection；eligible relation validation；root-specific direct/nested candidate bulk queries（`Take(maxEdges+1)`）；full coverage `EXISTS`；displayed branch coverage ID aggregation；direct Supersedes count/candidate；document/relationship Evidence bulk aggregation。所有实体读取使用 `AsNoTracking`。

## N+1 Check

代码审查与 EF command observation 均确认没有 `foreach relation -> ResolveTargetAsync`。Node/edge 增长只扩大有界 relation/document result sets 与两个 `IN/json_each` 聚合集合，不增加 one-query-per-node。Trust 使用单次 grouped aggregate；coverage 使用固定 root-specific `EXISTS`，因此 query stage 数量不随 node 数线性增长。

## SQLite Query Plan Evidence

在最终 isolated master fixture 上执行 `EXPLAIN QUERY PLAN`：

| Query | Plan Summary | Index Used | Scan Risk | Decision |
| --- | --- | --- | --- | --- |
| Root trace | `SEARCH knowledge_documents USING INTEGER PRIMARY KEY (rowid=?)` | INTEGER PRIMARY KEY | None | Accept |
| Outgoing SpecifiedBy/VerifiedBy | `SEARCH knowledge_relations ... (source_type=? AND source_id=?)` | covering endpoint unique index | None; relation filter is over one bounded source slice | Accept |
| Incoming VerifiedBy/SpecifiedBy | `SEARCH knowledge_relations ... (target_type=? AND target_id=? AND relation_type=?)` | `IX_knowledge_relations_target_type_target_id_relation_type` | None | Accept |
| Evidence aggregation | `SEARCH evidence ... (subject_type=? AND subject_id=?)` | `IX_evidence_subject_type_subject_id_subject_detail_key` | None | Accept |
| Supersedes | `SEARCH knowledge_relations ... (source_type=? AND source_id=?)` | covering endpoint unique index | None; direct lineage only | Accept |

## Index Decision

**Existing indexes sufficient: YES.** Root PK、source/target relation indexes、endpoint unique index 与 Evidence subject index 覆盖代表性读取。没有观察到 table scan 或需要实测改善的瓶颈，因此不提前创建 index。

## Database / Migration Decision

- Database change: **NO**
- Migration: **NO**
- New relation type: **NO**
- Schema/model snapshot change: **NO**

## Backend Focused Tests

Serial focused gate 覆盖 Traceability、KnowledgeRelation、Evidence、HumanConfirmation、revision interaction/restore 与 authorization：**41/41 PASS，0 failed，0 skipped**。其中新增 `TraceabilityApiTests` 为 **12/12 PASS**；fan-out payload diagnostic 单测复验 **1/1 PASS**。

## Backend Full Regression

使用 task-owned temporary runsettings 设置 `xUnit.ParallelizeTestCollections=false`，按已批准的 deterministic serial gate 执行：**135/135 PASS，0 failed，0 skipped，23s**。`REV-GAP-011` 继续 **OPEN / Deferred**；runsettings 已删除。

## Frontend API Contract / Decoder

新增 closed TypeScript union 与 manual runtime decoder，覆盖 root/document/relation enum、coverage eligibility/missing code、confirmation coverage、direction、lineage 与 truncation。Decoder 对非法 enum、负 count、unsafe ID、缺失字段或错误 root shape fail closed；client 复用 `apiClient` 和 `isSafeApiId`。没有新增 runtime-schema dependency。

## Frontend Type Check

`npm run type-check`：**PASS**。

## Frontend Build

`npm run build`：**PASS**。Vite 既有 >500 kB chunk advisory 仍存在；本任务没有 UI bundle entry、component、CSS 或 route change，也未把 advisory 解释为 TRACE failure。

## Frontend Focused Tests

`traceabilityContracts.spec.ts`：**8/8 PASS**。覆盖 Requirement、Specification、TestCase、coverage codes、trust、lineage、archived/truncation 以及 invalid enum/malformed payload。

## ESLint

对三个新增/修改 frontend source/test 文件执行 scoped ESLint：**PASS，0 errors**。

## API Runtime Smoke

使用 isolated SQLite、disposable Local Administrator、isolated Data Protection keys、task-owned `127.0.0.1:5097`，未启动 Vite。Master fixture 为 5 documents / 4 relationships / 3 Evidence：Requirement A、Specification A/B、TestCase A/B；关系为两条 SpecifiedBy、Specification A → TestCase A VerifiedBy、Requirement A → TestCase B VerifiedBy。

结果：

- Anonymous 401；authenticated GET 全部 200。
- Requirement：2 Specifications、1 direct TestCase、Spec A covered、Spec B `MissingTestDefinition`、global gaps empty。
- Specification A/B 与 TestCase A/B 均返回正确 discriminated root context。
- Requirement trust 为 `Inferred`、Evidence 2、HC 1、`CurrentRevisionConfirmed`；SpecifiedBy relationship Evidence 1 / HC 0。
- Spec A 在 archive 前为 Published；archive 后从 active Requirement structural projection 排除，`hasSpecificationTestDefinition=false`，Archived root 为 `ExcludedArchived` 且不展开 structural trace。
- Small-fixture payload bytes：Requirement 2,701；Specification A 1,521；Specification B 1,105；TestCase A 1,533；TestCase B 1,056；archive 后 Requirement 1,739。

## Read-only Runtime Invariants

最终 trace GET 前后均为：`integrity_check=ok`、foreign-key violations 0、KnowledgeDocuments 5、revisions 5、relations 4、Evidence 3、FTS rows 5。GET 未改变任何 count；archive 是明确的 fixture setup write，read-only comparison 在该写入完成后重新取 baseline。

## Repository DB Protection

结束指纹与开始完全一致：724,992 bytes；mtime UTC `2026-08-24T15:38:12.9720638Z`；SHA-256 `854EEDAF15B04F5AFD549769D6045689E3C0240C3B41AE2571EA1711F5085CA6`。Repository `App_Data` 未 migrate、seed、reset、replace 或写 fixture。

## Existing Frozen Baseline Regression

没有 product UI change，因此未伪造 Trace Tree UI PASS，也无需重跑 UI Foundation browser matrix。Full backend 135/135 保护 revision、Evidence/HC、authorization、relationships 与 FTS；frontend type/build/decoder/lint 保护新增 boundary。产品状态仍为 **Internal Pilot**，未声明 Production Ready 或 PHASE-TRACE closed。

## New Gap Check

未发现 TRACE-B01 新 Blocker / High / Medium / Low gap。`REV-GAP-006`～`009` 与 `011` 未顺手修复；`REV-GAP-011` 保持 OPEN / Deferred。Trace correctness、authorization、read-only safety 与 bounded-query safety 均通过。

## Cleanup

最终 runtime cycle 后只停止 task-owned API PID，确认 port 5097 released；删除 isolated SQLite/WAL/SHM、Data Protection keys、disposable admin data、logs、smoke/query-plan scripts 与 temporary serial runsettings。未按 process name/wildcard kill，未使用 `git clean`、reset、gc 或 prune。前两个被修正的 disposable smoke attempts 也各自精确停止并只重建相同 isolated fixture；未接触 repository database。

## TRACE-B02 Readiness

**TRACE-B02 READY: YES.** Endpoint、三种 root、coverage、trust、authorization、limits、query plan、no schema drift、backend full suite 与 frontend decoder gate 均稳定。此结论仅允许进入后续人工批准的 TRACE-B02；不代表 TRACE-B02/B03/PHASE-TRACE-VERIFY 已完成。

## Final Result

**TRACE-B01 PASS**
