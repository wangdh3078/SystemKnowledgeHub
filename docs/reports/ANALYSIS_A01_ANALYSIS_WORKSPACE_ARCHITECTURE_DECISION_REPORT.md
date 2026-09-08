# ANALYSIS-A01 — Analysis Workspace Architecture Decision Report

日期：2026-09-08。**ANALYSIS-A01 PASS / APPROVED**，**ANALYSIS-B01 READY: YES**。

交付为 [Analysis Workspace 架构决策](../design/ANALYSIS_A01_ANALYSIS_WORKSPACE_ARCHITECTURE_DECISION.md)及本报告、文档索引。本结论仅表示产品/架构合同冻结，功能 **NOT IMPLEMENTED**，不是运行时或端到端验收 PASS。未开始 ANALYSIS-B01。

## 1. 基线与范围

用户指定 authoritative `main`：`73b997a5c5f8adeab570cb9dfce1cbfa9d77fffa`（`docs(portal): complete internal portal verification`）。实际 HEAD 一致。开始时执行并检查 `git status --short`、`git diff`、`git log -8 --oneline`；工作区干净，无需处理用户修改。

本任务只做 Product / Architecture Decision，未修改产品代码、测试、package、配置、实体、DbContext 或 schema；没有生成 migration，没有启动服务器或浏览器。没有 reset、clean、revert、换分支或覆盖其它工作。

## 2. 来源与当前实现核对

依据根 AGENTS.md 与 DOCUMENT_INDEX 定位适用来源。完整链接和 authority 优先关系列于决策 §1；本报告只记录核对结果，不复制历史报告的测试结果作为本次运行证据。

| 核对对象 | 静态事实与决策影响 |
| --- | --- |
| Knowledge Content architecture / PHASE_NEXT_MAJOR_PHASE_PLANNING_R01 | 早期 spaces/page tree 为延期范围，本次是后续 authoring organization 受控 extension；不追认历史错误，不批准 Spaces。 |
| DocumentType / KnowledgeDocument / KnowledgeDocumentService | 七种受控类型，一个 current head；Create 创建 Draft/Unknown、revision 1 与 FTS；没有需要 AnalysisNote 的新业务不变量。 |
| KnowledgeDocumentService + REV-A01 | 内容保存支持 Draft/Published，Archived 拒绝；Published 内容保存立即成为新发布修订，no-op 不改 revision。目录操作与内容保存不能混同。 |
| SqliteImmediateTransaction | 已支持加入当前 DbContext 外层事务，内层 commit 不提前提交。CreateAnalysisDocument 可以具体组合现有 create，而不复制 revision/FTS 逻辑。实际组合与 rollback 仍待 B01 验证。 |
| PortalPage / PortalPageNode / PortalPageSection、AdminPortalService、PortalLimits | Portal 有独立发布审计/软删除、Administrator mutation、完整 sibling token reorder 与双阶段顺序写入；深度上限 10，effective tree 上限 2,000。只借用有限树预算和顺序约束经验，不重用 publication entity 或全部管理 service。 |
| PORTAL-A01 / B01–B04 / FINAL | Portal v1 已完成；管理是 `/portal-management`，匿名 reading 是独立 PortalLayout / GET-only client。Analysis 仍为 authenticated app-shell，Portal 无反向 authoring 入口。 |
| SearchQueries / KnowledgeDocumentSearchIndex / KnowledgeDocumentQueries | 全局文档 FTS 查询排除 Archived/deleted，索引为 current head；列表默认排除 Archived，但允许显式 lifecycle 筛选。v1 tree 只做标题过滤，候选复用已有分页列表。 |
| Attachment A01 + A02 / final、当前附件与修订组件 | A02 已批准受保护 PDF 等预览，不能照搬 A01 最初的 preview 非目标；归属、current/history 引用、server capabilities、上传/保存约束沿用。 |
| HC-A01 / HC-B01、Evidence、KnowledgeStatus、KC-C01 / TRACE-A01 | 既有文档级 trust、append-only HC 与撤销/替代、显式 status、关系与派生追溯均独立于组织节点。AnalysisNode 不新增 subject/target wire 值。 |
| KnowledgeDocumentDeleteService / DELETE-A01 / REL-EVIDENCE-A01 | 真删除文档仍有原创建人/Administrator、opaque token 与 canonical relation blocker；组织引用不自动变知识依赖或 cascade。 |
| AccessControl / CurrentUser、当前 router | Viewer+ read、Editor+ 普通 write、Administrator Portal Management；账号/身份失效、强制改密、antiforgery 不弱化，KnowledgeRole 不变权限。 |
| KnowledgeDocumentDetailView / Editor / documentEditState / Markdown / AttachmentArea / RevisionHistory | 可复用组件已存在，详情还有依赖 route.params.id 的内联状态；不是现成的工作区面板。B02 必须局部提取并保护同页参数切换、dirty buffer 与晚到响应。 |

主要代码落点（均为本次只读检查）：

- [KnowledgeDocumentService](../../src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Application/KnowledgeDocumentService.cs)、[SqliteImmediateTransaction](../../src/SystemKnowledgeHub.Api/Persistence/SqliteImmediateTransaction.cs)、[KnowledgeDocumentDeleteService](../../src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Application/KnowledgeDocumentDeleteService.cs)。
- [PortalComposition](../../src/SystemKnowledgeHub.Api/Features/Portal/Domain/PortalComposition.cs)、[Portal node configuration](../../src/SystemKnowledgeHub.Api/Features/Portal/Persistence/PortalPageNodeConfiguration.cs)、[AdminPortalService](../../src/SystemKnowledgeHub.Api/Features/Portal/Application/AdminPortalService.cs)、[PortalLimits](../../src/SystemKnowledgeHub.Api/Features/Portal/Application/PortalLimits.cs)。
- [SearchQueries](../../src/SystemKnowledgeHub.Api/Features/Search/Application/SearchQueries.cs)、[KnowledgeDocumentSearchIndex](../../src/SystemKnowledgeHub.Api/Features/Search/Application/KnowledgeDocumentSearchIndex.cs)、[KnowledgeDocumentQueries](../../src/SystemKnowledgeHub.Api/Features/KnowledgeDocuments/Application/KnowledgeDocumentQueries.cs)、[AccessControl](../../src/SystemKnowledgeHub.Api/Shared/Security/AccessControl.cs)。
- [KnowledgeDocumentDetailView](../../src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue)、[PortalManagementView](../../src/SystemKnowledgeHub.Web/src/features/portal-management/pages/PortalManagementView.vue)、[PortalLayout](../../src/SystemKnowledgeHub.Web/src/layouts/PortalLayout.vue)。

## 3. 决策闭合检查

| 设计问题 | 唯一结论 / rationale |
| --- | --- |
| 正文与类型 | 复用 KnowledgeDocument；新建默认 DesignNote，可显式选 KnowledgeArticle；existing 七种全可加入；不新增 AnalysisNote。 |
| 持久化 | 只有 AnalysisNode，九字段，单一虚拟全局根；无 Workspace/Owner/ACL/IsDeleted/正文副本。后续一个空表 additive migration。 |
| Tree | Folder 分支、Document 叶子；根深度 0，节点深度 ≤10，总数 ≤2,000；server-owned sibling order，拒绝 cycle/self/非法 parent/超深 move。 |
| Placement | 同一文档最多一处；移除仅删位置，非空 Folder 拒删；Title 仅 Folder 自有，Document 无 override。 |
| Lifecycle | Draft/Published/Archived 都显示，Archived 正文只读；soft-deleted 保留通用不可用占位，可受控移除，不能泄漏旧正文/标题。 |
| 权限 | Viewer+ 读、Editor+ 组织 mutation；文档删除保留原所有权限制；Portal Management 仍 Administrator-only。 |
| 并发 | Node.Version + 不落库全树 snapshot token + SQLite immediate transaction；根新增/祖先移动/同级竞争都有比较依据，不采用 last-write-wins。 |
| 原子创建 | 外层事务组合已有 canonical Create，KnowledgeDocument + revision 1 + FTS + placement 全有或全无；不做前端两次写入。 |
| Search | 选择 B：tree title filter；全局 FTS 和列表筛选原样复用，不增加 workspace 全文索引/伪 scoped search。 |
| 知识能力 | Evidence/HC/Status、Attachment/Revision、KnowledgeRelation 继续绑定原知识对象；tree 不创造语义关系或可信状态。 |
| Portal | 不复用/同步 Portal tree，不自动创建 Draft composition 或发布；Administrator 只导航到现有管理页并未保存预选 Published 文档。 |
| Published live 内容 | 未编排文档不自动入 Portal；已经被门户引用的 Published 文档保存仍即时影响原投影，完整保留 REV/Portal 原语义。 |
| UI/历史扩展 | 左树右正文，欢迎空态，复用原 feature 中的编辑/阅读/附件/修订能力；受控扩展早期阶段排除，不新增 Confluence clone/Spaces。 |

决策 §14 对用户要求的 **26 项 Compatibility Questions** 逐项给出唯一答案，§15 给出全部冻结状态。不存在互斥候选仍待实现者选择的核心合同。

静态情景推演还检查了：两个 root create、祖先/后代 move、reorder 对 create/remove、重复加入、空 Folder delete 对 create-child、canonical delete 对 placement create、Archived 与 FTS 不同可见性、内容标题变更与 Folder rename 的修订差异、dirty 切换和 Portal 已有发布引用。它们是设计审查，**并非已运行测试**；真实约束/事务/组件断言归属 B01–B03。

## 4. 当前验证、数据保护与清理

| 本次检查 | 结果 |
| --- | --- |
| authoritative HEAD、工作区、近期提交核对 | PASS；与指定 main 一致，开始时干净。 |
| 当前实现与适用 frozen/amendment 兼容、26 项唯一性 | PASS；未发现阻塞本次决策的冲突。 |
| 文档相对链接与索引、最终 diff / whitespace / 文件范围 | PASS；仅两份新增 Markdown 与 DOCUMENT_INDEX。 |
| build / backend/frontend tests / EF migration / browser | N/A；文档决策不改产品行为，没有运行这些检查或宣称实现 PASS。 |
| 真实数据库/WAL/SHM | PASS；只做文件系统 metadata/hash 比对，未用 SQLite/EF 打开、迁移、seed 或查询。 |
| 运行时清理 | N/A；本任务未创建进程、浏览器、监听端口、临时数据库、Data Protection 或附件目录。 |

前后文件系统基线相同：`src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` 为 **1,372,160 bytes**，UTC mtime **2026-09-07T15:51:07.1311811Z**，SHA-256 **`F6C2DC43EB04110A830E86635920AC8DE1B52C60D9C19E9E178741AFABCCAC88`**。对应 `-wal`、`-shm` 均不存在。没有打开真实 DB 执行 SQL，没有生成或提交 runtime artifacts。

## 5. 后续切片、文档与独立 gate

后续仅按决策 §13 分片：B01 persistence/tree API/atomic creation，B02 workspace UI/编辑能力复用，B03 existing placement UI/filter/Portal handoff，最后 VERIFY 最小闭环。B01 的 READY 不构成当前自动开始授权。本任务到文档交付停止。

DOCUMENT_INDEX 新增设计与报告的导航 metadata。PROJECT_FILE_MAP 未改：本次没有实际 feature 文件/职责变化，任务限定仅在规则要求时更新，未将尚未实现的目录/API 写成当前文件清单。历史 frozen Knowledge Content、Portal、Revision、Trace、Attachment、HC 文档和报告均未改。

没有新设计 blocker 或新 Gap ID。实现风险已归入后续 focused acceptance，不宣称已通过。REV-GAP-011 等既有测试基础设施限制不因本次文档工作关闭；REV-GAP-012 CLOSED、Portal v1 COMPLETE、Stability Hardening COMPLETE 保持当前状态。SEC-04 Production rollout 与 PHASE-TRACE 真实领域 Product Acceptance 保持独立，不在 ANALYSIS-A01 关闭。

## 6. 最终状态与交付边界

```text
ANALYSIS-A01: PASS
KNOWLEDGEDOCUMENT REUSE: FROZEN
ANALYSIS TREE: FROZEN
PORTAL TREE SEPARATION: FROZEN
NO DUPLICATE CONTENT TRUTH: FROZEN
DOCUMENT TYPE STRATEGY: FROZEN
PERMISSION: FROZEN
TREE CONCURRENCY: FROZEN
PLACEMENT SEMANTICS: FROZEN
PORTAL HANDOFF: FROZEN
MIGRATION DESIGN: FROZEN
ANALYSIS-A01 APPROVED
ANALYSIS-B01 READY: YES
Implementation / runtime acceptance: NOT STARTED
```

本报告随上述三份文档在当前 `main` 的同一任务提交交付，提交消息为 `docs: freeze analysis workspace architecture`。完整提交 SHA 与 `push origin main` 的实际结果在任务最终回执记录，不把交付结果冒充功能实现验收。
