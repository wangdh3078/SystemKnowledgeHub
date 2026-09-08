# ANALYSIS-B01 — Persistence + Tree API Foundation Verification Report

日期：2026-09-08。**ANALYSIS-B01 PASS / COMPLETE；ANALYSIS-B02 READY: YES**。

基线为用户指定的 `main` / `7c3cf7cb8bc249dd3db07772b840592c0cead165`（`docs: freeze analysis workspace architecture`）。开始时已核对 status、diff、最近八次提交，工作区干净。本任务只实现后端组织基础，没有前端、Portal 功能、Demo Dataset 或部署变更；没有开始 B02、B03 或 ANALYSIS-VERIFY。

## 1. Authority 与实现范围

遵循根 [AGENTS.md](../../AGENTS.md)、[DOCUMENT_INDEX](../DOCUMENT_INDEX.md)、[ANALYSIS-A01 frozen decision](../design/ANALYSIS_A01_ANALYSIS_WORKSPACE_ARCHITECTURE_DECISION.md)及其 [decision report](ANALYSIS_A01_ANALYSIS_WORKSPACE_ARCHITECTURE_DECISION_REPORT.md)。相关语义沿用 REV-A01、ATTACH-A01/A02、HC-A01/HC-B01、KC-C01、TRACE-A01、DELETE-A01、AUTH-A01、PORTAL-A01 / Portal final，以及当前 KnowledgeDocumentService、Queries、DeleteService、DbContext、SqliteImmediateTransaction、AccessControl / CurrentUser 实现。

没有修改任何历史 frozen source。KnowledgeDocumentService.Create 与 SqliteImmediateTransaction **无需修改**即可组合；当前 canonical 正文、revision、FTS、附件、Evidence/HC、关系、status、lifecycle 和 Portal 实现均保留。既有文件的产品接入仅为 DbContext 增加 AnalysisNodes DbSet、Program 注册 AnalysisWorkspaceService，以及 EF model snapshot 的 AnalysisNode 部分。

## 2. Schema 与 additive migration

新增 feature：`src/SystemKnowledgeHub.Api/Features/AnalysisWorkspace/`。

唯一新实体/表为 **AnalysisNode / analysis_nodes**，恰好九列：id、parent_id、node_type、title、knowledge_document_id、sort_order、created_at、updated_at、version。无 root row；ParentId=null 表示单一虚拟全局根。无 Workspace/Owner/CreatedBy/IsDeleted/ACL/MetadataJson/publication state，没有正文或其它知识副本。

- NodeType 为 Folder / Document 闭集，写 API 不提供类型转换；Folder 标题 trim 后 1–200，文档 ID 为 null；Document 标题列为 null、文档 ID 必填，显示标题由 current document 查询投影。
- Id 为 SQLite AUTOINCREMENT / safe positive ID，Version 初始 1、EF concurrency token；SortOrder 为非负 int，时间由服务端 UTC 提供。数据库 CHECK 保护 shape、类型、安全 Id、非 self-parent、顺序范围和 Version ≥ 1。
- parent_id → analysis_nodes.id、knowledge_document_id → knowledge_documents.id 均 **RESTRICT**，无 cascade。
- 根 partial UNIQUE(sort_order) WHERE parent_id IS NULL；非根 partial UNIQUE(parent_id,sort_order) WHERE parent_id IS NOT NULL；文档 partial UNIQUE(knowledge_document_id) WHERE knowledge_document_id IS NOT NULL。

唯一 migration：[20260908130346_AddAnalysisWorkspaceTreeFoundation](../../src/SystemKnowledgeHub.Api/Persistence/Migrations/20260908130346_AddAnalysisWorkspaceTreeFoundation.cs)。Up 只创建 analysis_nodes、CHECK/FK/三个索引；Down 只删除该表。没有 ALTER/重建其它知识表，没有 backfill、Portal tree copy 或 FTS 修改。

验证使用 disposable SQLite：fresh migration 的九列、两条 RESTRICT FK、三个 partial unique indexes 和代表性非法写均通过；以明确 pre-Analysis migration `20260907133706_AddHumanConfirmationCorrectionLifecycle` 升至上述 Analysis migration，分析表为空，所有既有 schema 与数据逐表保持一致。数据样本包含正文、两条内容修订、FTS、已引用附件、HC、canonical relation、Portal Page/Node/Section 及现有 seed 数据。仅 migration history 与新增组织表为预期变化；既有表 schema/rows 不变。Down 只在 disposable DB 执行，验证旧 schema/rows 完整保留。FK violations 为 0。

## 3. API 与安全边界

| 方法 / route | 结果与关键输入 |
| --- | --- |
| GET `/api/analysis/tree` | 200：完整节点 metadata 与 treeConcurrencyToken；默认 Viewer policy。 |
| POST `/api/analysis/folders` | 201：parentId?、title、treeConcurrencyToken；追加 Folder。 |
| PUT `/api/analysis/folders/{id}/title` | 200：title、nodeConcurrencyToken、treeConcurrencyToken；仅 Folder rename。 |
| POST `/api/analysis/document-placements` | 201：parentId?、knowledgeDocumentId、treeConcurrencyToken；已有文档最多一个位置。 |
| POST `/api/analysis/documents` | 201：parentId?、treeConcurrencyToken 与 canonical create 内容字段；原子创建知识文档及 placement。 |
| POST `/api/analysis/nodes/{id}/move` | 200：targetParentId?、targetPosition、nodeConcurrencyToken、treeConcurrencyToken。 |
| PUT `/api/analysis/children/order` | 200：parentId?、完整有序 items[{id,nodeConcurrencyToken}]、treeConcurrencyToken。 |
| DELETE `/api/analysis/document-placements/{id}` | 200：nodeConcurrencyToken、treeConcurrencyToken；id 为 nodeId，只移除 Document placement。 |
| DELETE `/api/analysis/folders/{id}` | 200：nodeConcurrencyToken、treeConcurrencyToken；仅空 Folder。 |

GET 返回 `{items, treeConcurrencyToken}`；mutation 返回 `{node, items, treeConcurrencyToken}`。node 是创建/rename/move 后选中节点，remove/reorder 时为 null；items 是同一事务中的完整新树。节点含 id、parentId、nodeType、sortOrder、opaque concurrencyToken、title、knowledgeDocumentId?、documentType?、lifecycleStatus?、availability、createdAt/updatedAt。没有正文、summary、附件、Evidence、关系或 revision body。

已有文档允许七种类型以及 Draft/Published/Archived；禁止 soft-deleted/unavailable 文档新增 placement，重复 placement 返回 422 business_rule_violation。Archived 可组织，不能借组织 API 编辑正文。文档 soft delete 后 placement 保留，title 为“文档不可用”、availability=Unavailable、documentType/lifecycleStatus=null；只用现有 current query filter，不以 IgnoreQueryFilters 泄漏旧标题或内容。不可用 placement 仍可移动/移除。

controller 使用 `[Authorize]` 复用应用默认 Viewer policy；所有八类写操作附加现有 Editor policy，故 Editor/Administrator 可写，Viewer 403，匿名 401。所有写操作经过既有全局 antiforgery middleware，没有新增 AllowAnonymous 或改变授权注册。CreateAnalysisDocument 的 author 经现有 CurrentUserApiResolution 从 canonical current actor 解析；不接受客户端作者/KnowledgeRole 权限或目录 owner。

沿用 ApiErrorResponse：400 validation_error、原 401/403、404 not_found、409 conflict、422 reference_invalid / business_rule_violation。仅映射已知组织失败和 DbUpdateConcurrencyException，意外存储错误仍传播，不吞掉异常或返回伪成功。

## 4. Tree、排序与并发

AnalysisLimits 明确 MaxDepth=10、MaxNodes=2000，根深度 0。Folder 可有 children，Document 只能是叶子。读取最多探测 2001 行用于判定超限；有效响应最多 2000 节点，不截断成可编辑的假完整树。节点与文档身份查询位于同一数据库读取事务；身份查询只选 Id/Title/DocumentType/LifecycleStatus，没有逐节点正文查询。

move 目标位置按源节点移出后的目标 child list 计算，范围 0..Count（含追加）；目标必须是 Analysis Folder 或 null，禁止 self-parent、后代环路、缺失/Document parent。校验完整子树最终深度，不只检查被移动 Folder 自身。跨父移动归一化两组 sibling，同父归一化一组；reorder 要求完整、准确、不重复的 child 集合，空 sibling 集合允许 no-op。

每次组织写先取得 **SqliteImmediateTransaction**，随后重读完整树并核对 treeConcurrencyToken；已有 node 操作另核对 node token，reorder 核对每个 child token。过期返回 409，不自动刷新重放，不使用进程锁。节点实际改变时 Version +1、UpdatedAt 更新，未变化节点和 no-op 不 touch。

treeConcurrencyToken 为 `a1.` + SHA-256 摘要，固定协议前缀 `SystemKnowledgeHub.AnalysisTree.v1`，BinaryWriter 长度前缀字符串与固定宽度整数编码；按 Id 排序输入节点数与 Id、ParentId、NodeType、KnowledgeDocumentId、SortOrder、Version，null ID 用有效域之外的 0。空树 token 稳定。摘要不落库、不构成 TreeRevision 或授权凭据，也不包含正文/知识状态。canonical 文档标题保存会立即反映在读投影，但不改变组织 token。

排序保留 DB 唯一索引：同一事务先把实际变化/新节点置于全局无冲突的临时高位 order，再写最终 parent/order 的 `0..n-1`；不改变的节点不写。int 溢出或版本耗尽拒绝；临时状态不会被 commit。失败/取消由外层事务回滚。

## 5. 原子创建、删除与数据保全

CreateAnalysisDocument 默认 DesignNote，可显式 KnowledgeArticle，其它创建类型 400。外层 immediate transaction 验证 tree token、parent、depth、capacity，再调用原 KnowledgeDocumentService.Create；复用原校验、trim/换行归一化、canonical actor、Draft/Unknown、revision 1、FTS。随后创建 placement，全部成功才由外层 commit。返回的 node 给出新 knowledgeDocumentId，正文继续使用 canonical API 读取/编辑。

真实 SQLite trigger 在 analysis_nodes 插入阶段强制失败，发生在 canonical Create 已执行其内部 CommitAsync 之后；最终证明 **新 KnowledgeDocument、revision、FTS、placement 均未留下**。这验证了内层已有事务加入逻辑未提前提交外层。canonical 内容校验失败同样不创建 placement；普通 KnowledgeDocument create 路径未改，并通过两个现有直接回归用例。

remove placement 只物理删除 AnalysisNode(Document)，delete Folder 仅允许 children=0，不递归、不提升 children、不删除知识。使用含附件引用/HC/关系/Portal 引用的文档验证唯一 placement 移除及空目录删除，所有既有表 rows 完全不变，正文仍可读，重新加入成功。

canonical 文档删除与 placement 创建使用独立 HTTP 请求/DbContext/SQLite connections，并由测试 gate 保持首个写事务、观察第二连接重叠：

- delete 先提交：placement 返回 422 reference_invalid，没有新位置，历史 revision 保留。
- placement 先提交：随后 canonical delete 成功（204），组织引用不成为知识删除 blocker；位置保留并显示安全不可用状态。
- 两请求同时加入同一文档：一个 201，另一个因 stale tree 为 409，最多一个 placement；数据库 unique constraint 另有直接非法插入验证作为最终屏障。

相同机制覆盖两个 root create、祖先 move 对后代 rename、reorder 对 create/remove、delete-empty-folder 对 create-child；首个合法命令成功、旧快照命令 409，最终顺序完整、无 stale 写入、FK violations=0。

## 6. Focused 验证与实际执行记录

| 范围 | 最终有效证据 |
| --- | --- |
| AnalysisWorkspaceApiTests | 6/6：全部 read/write 权限与 antiforgery、Folder/token/no-op、move/reorder/子树 depth/cycle、2000/2001/overflow、七类型/lifecycle/deleted projection、原子 create/rollback。 |
| AnalysisWorkspaceConcurrencyTests | 8/8：六种组织竞争，以及 canonical delete/placement 两种提交顺序。 |
| AnalysisWorkspacePersistenceTests | 3/3：fresh schema/约束、pre-Analysis upgrade/Down 全数据保全、移除 placement 不触碰已填充知识/Portal。 |
| 既有 KnowledgeDocumentRevisionApiTests 直接回归 | 2/2：Create_and_semantic_saves_create_trusted_contiguous_revisions_while_noop_and_stale_writes_do_nothing；Create_rolls_back_document_revision_and_fts_when_revision_insert_fails。 |
| Release solution build | PASS：0 warnings / 0 errors。 |
| EF has-pending-model-changes | PASS：No changes have been made to the model since the last migration。 |

共 **19 个不同的相关测试最终均有通过证据，0 未解决失败、0 skipped**，其中新增 17 个 Analysis 风险用例（Theory 行计入），既有 2 个直接回归。没有 full backend suite，也没有全部 Revision/Attachment/Portal/Trace/HC 测试。

测试命令采用既有 REV-GAP-011 批准的 serial gate：MaxCpuCount=1、xUnit ParallelizeTestCollections=false、MaxParallelThreads=1。基础命令如下，results/settings 均在 task-owned 临时目录：

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj -c Release --no-build --no-restore --filter "<focused filter>" --settings <task-owned>/serial.runsettings --logger "trx;LogFileName=<run>.trx" --results-directory <task-owned>/results
```

如实记录三次有针对性的执行：

1. 初次 filter 为 `FullyQualifiedName~AnalysisWorkspace` 加上述两个既有方法：19 项中 4 passed、15 failed。原因是新 controller 引用了未注册的命名 Viewer policy；默认 Viewer policy 已存在。改为 `[Authorize]` 复用当前默认政策，没有修改全局授权或降低权限。
2. 只重跑受 controller 修正影响的 AnalysisWorkspaceApiTests、AnalysisWorkspaceConcurrencyTests 和 `AnalysisWorkspacePersistenceTests.Removing_the_only_placement`：**15/15 passed**。两个 migration tests 与两个原文档 tests 未受影响，不重复运行。
3. 最终复核在现有 Folder/move/capacity 用例中补充 malformed token、未变节点 token、整个两层 subtree 超深移动，以及外部超限数据必须拒绝完整读取的断言；只重跑这三个方法：**3/3 passed**。未修改产品代码或扩大到其它 feature。

最终执行 `dotnet build SystemKnowledgeHub.sln -c Release --disable-build-servers` 成功。EF 命令使用 `--project src/SystemKnowledgeHub.Api --startup-project src/SystemKnowledgeHub.Api --configuration Release --no-build`，且显式 `SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH=<task-owned absolute>/design-time.db`；该文件实际未创建。EF 仅输出既有 required-navigation/global-query-filter 提示，无新增 Analysis 警告，无 pending model changes。

## 7. DBSAFE、清理与文档

真实仓库数据库仅做 filesystem existence / size / UTC mtime / SHA-256 前后比对。未使用 SQLite/EF 打开、migrate、seed、copy 或 checkpoint，也未触碰 WAL/SHM。

| 文件 | 验证前 / 后 |
| --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | 同为 1,372,160 bytes；UTC mtime `2026-09-07T15:51:07.1311811Z`；SHA-256 `F6C2DC43EB04110A830E86635920AC8DE1B52C60D9C19E9E178741AFABCCAC88`。 |
| 对应 `-wal` / `-shm` | 前后均不存在。 |

普通 API/persistence 测试复用既有 BootstrapWebApplicationFactory 的独立 SQLite 与 task-owned guard/附件/日志/Data Protection 配置；race tests 使用另外的 GUID 临时目录、file SQLite、Pooling=False 与独立 connections。factory dispose 清理自建状态，测试后未发现 analysis-b01-race 目录或 testhost 残留。build/EF/test 均为已退出的一次性命令；未启动 dotnet run、Vite、watcher 或浏览器。任务自身 EF/settings/TRX 目录在提取结果后清理，不提交 runtime artifacts。

同步 DOCUMENT_INDEX 与 PROJECT_FILE_MAP，后者新增实际 backend feature/test 文件职责及本报告入口。没有前端产品文件变更，因此 npm/type-check/浏览器均不适用；未做 viewport、responsive 或 zoom 验证。

## 8. Final gate 与交付

| Gate | 状态 |
| --- | --- |
| ANALYSIS-B01 | PASS / COMPLETE |
| ANALYSIS NODE SCHEMA | PASS |
| ADDITIVE MIGRATION | PASS |
| TREE READ | PASS |
| FOLDER CREATE/RENAME | PASS |
| DOCUMENT PLACEMENT | PASS |
| MOVE | PASS |
| REORDER | PASS |
| REMOVE PLACEMENT | PASS |
| EMPTY FOLDER DELETE | PASS |
| DEPTH/CYCLE/CAPACITY | PASS |
| TREE CONCURRENCY | PASS |
| NODE CONCURRENCY | PASS |
| ATOMIC CREATE ANALYSIS DOCUMENT | PASS |
| REVISION/FTS ROLLBACK | PASS |
| DELETE/PLACEMENT RACE | PASS |
| UNIQUE PLACEMENT RACE | PASS |
| AUTHORIZATION | PASS |
| ANTIFORGERY | PASS |
| SOFT-DELETED SAFE PROJECTION | PASS |
| NO PORTAL COUPLING | PASS |
| NO FRONTEND CHANGE | PASS |
| FOCUSED BACKEND | PASS |
| NO PENDING MODEL CHANGES | PASS |
| BUILD | PASS |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |
| BROWSER | NOT APPLICABLE |
| ANALYSIS-B02 READY | YES |

没有新增未解决的功能 gap；不关闭既有 REV-GAP-011、SEC-04 Production rollout blocker 或 PHASE-TRACE 真实领域 Product Acceptance。Portal v1 / Stability Hardening COMPLETE、REV-GAP-012 CLOSED 保持原状态。本地隔离 backend 验证不等同 Production 部署或工作区 UI 验收。

当前任务代码、migration、测试与文档采用同一 `main` 提交，消息为 `feat(analysis): add workspace tree foundation`。完整 SHA 和 push origin main 的实际结果见任务最终回执。到 B01 交付停止，不自动开始 B02/B03/VERIFY 或 Demo Dataset。
