# ANALYSIS-A01 — Analysis Workspace / 分析文档工作区架构与产品边界决策

日期：2026-09-08。状态：**FROZEN / APPROVED**。类型：Product / Architecture Decision。

基线：`main` / `73b997a5c5f8adeab570cb9dfce1cbfa9d77fffa`。本文件批准后续分片的边界，不表示功能已实现。本任务没有产品代码、migration、配置或运行时改动；不开始 ANALYSIS-B01。

## 1. 决策与 authority

“分析文档”是已登录用户的知识编写与组织入口。左侧组织目录，右侧直接阅读、编辑同一个 KnowledgeDocument。v1 只有一个逻辑全局根，只新增 **AnalysisNode**；不新增 AnalysisWorkspace、文档正文模型、DocumentType 或权限体系。

依据根 [AGENTS.md](../../AGENTS.md)、[文档索引](../DOCUMENT_INDEX.md)及以下适用来源，当前实现和最新明确通过的 amendment / closeout 优先于历史阶段的库存描述：

| 来源 | 本决策沿用的边界 |
| --- | --- |
| [Knowledge Content plan](KNOWLEDGE_CONTENT_DOCUMENT_ARCHITECTURE_PLAN.md)、[后续阶段规划](../reports/PHASE_NEXT_MAJOR_PHASE_PLANNING_R01.md) | 一个 KnowledgeDocument aggregate、七种受控类型；不复制结构化知识。早期排除 page tree 是当时的阶段边界。 |
| [Markdown Source Editor decision](KNOWLEDGE_DOCUMENT_MARKDOWN_SOURCE_EDITOR_DECISION.md) | 当前为 CodeMirror 6 原始 Markdown；历史 Milkdown 提案不再是编辑器选择依据。 |
| [REV-A01](REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md) | current head、不可变内容修订、显式保存、Published 即时更新、Draft-only restore。 |
| [KC-C01](KC_C01_RELATIONSHIP_VOCABULARY_ARCHITECTURE_DECISION.md)、[TRACE-A01](TRACE_A01_TRACEABILITY_ARCHITECTURE_AND_CONTRACT_DECISION.md) | 闭合有向关系矩阵、派生追溯；目录层级不表达关系或覆盖。 |
| [ATTACH-A01](ATTACH_A01_ATTACHMENT_IMAGE_REVISION_PERMISSION_STORAGE_ARCHITECTURE_DECISION.md)、[ATTACH-A02](ATTACH_A02_ATTACHMENT_PREVIEW_CAPABILITY_AMENDMENT.md)、[ATTACH final](../reports/ATTACH_VERIFY_FINAL_VERIFICATION_REPORT.md) | 文档归属、revision 引用快照、受保护图片/文件与已有预览能力；A02 已扩展早期 preview 非目标。 |
| [HC-A01](HC_A01_HUMAN_CONFIRMATION_IMMUTABILITY_WITHDRAWAL_REPLACEMENT_DECISION.md)、[HC-B01](../reports/HC_B01_HUMAN_CONFIRMATION_WITHDRAWAL_REPLACEMENT_IMPLEMENTATION_VERIFICATION_REPORT.md) | HC append-only、撤销/替代与有效支持集合，历史审计保留；不自动推进 KnowledgeStatus。 |
| [DELETE-A01](DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md)、[REL-EVIDENCE-A01](REL_EVIDENCE_A01_RELATION_EVIDENCE_DEPENDENCY_AND_REMOVAL_DECISION.md) | canonical soft delete、创建人/管理员删除权限、依赖和历史保护；撤销 HC 仍可阻止关系物理删除。 |
| [AUTH-A01](AUTH_A01_LOCAL_LOGIN_OIDC_COEXISTENCE_DESIGN_REVIEW.md)、当前 AccessControl / CurrentUser | Local/OIDC 会话、Viewer/Editor/Administrator、写入 antiforgery、失效身份 fail closed；KnowledgeRole 不是权限。 |
| [PORTAL-A01](PORTAL_A01_INTERNAL_KNOWLEDGE_PORTAL_ARCHITECTURE_DECISION.md)、[TrustSummary amendment](../reports/PORTAL_A01_AMEND_01_TRUSTSUMMARY_SOURCE_COMPATIBILITY_REPORT.md) | 独立 publication composition、Administrator 管理、匿名 GET-only、安全投影。 |
| [PORTAL-B01](../reports/PORTAL_B01_COMPOSITION_PERSISTENCE_ANONYMOUS_READ_FOUNDATION_VERIFICATION_REPORT.md)、[B02](../reports/PORTAL_B02_ADMIN_KNOWLEDGE_COMPOSITION_MANAGEMENT_VERIFICATION_REPORT.md)、[B03](../reports/PORTAL_B03_PORTAL_READING_EXPERIENCE_VERIFICATION_REPORT.md)、[B04](../reports/PORTAL_B04_SEARCH_ATTACHMENT_TRUST_RELATED_TRACE_INTEGRATION_VERIFICATION_REPORT.md)、[Portal final](../reports/PORTAL_FINAL_VERIFICATION_REPORT.md) | 已完成的持久化、管理、阅读、搜索/附件/可信/关系/追溯集成；实际管理路由是 `/portal-management`。 |

本决策是完成 Content、Revision、Attachment、Portal 后，针对 **authoring organization** 的受控后续 extension。仅在此范围取代早期“first phases 不做 page trees / 组织层尚未设计”的延后判断；不认为历史设计错误，不批准 Spaces。历史 frozen specs、Portal、Revision、Trace、Attachment、HC 决策和报告均不改写。

INTERNAL KNOWLEDGE PORTAL V1 与 STABILITY HARDENING 保持 COMPLETE，REV-GAP-012 保持 CLOSED。[SEC-04](../reports/SEC_04_SECURITY_ROLLOUT_VERIFICATION_REPORT.md) 仍是独立 Production rollout blocker；[PHASE-TRACE Product Acceptance](../reports/PHASE_TRACE_FINAL_VERIFICATION_REPORT.md) 仍需独立真实领域验收。本 A01 不关闭这些 gate，不声称 Production 可用。

## 2. 产品目标与范围

目标用户为开发人员、系统/业务分析人员、设备/EAP/MES 工程师。支持系统分析、业务流程、数据库与接口分析、调查过程、设计思路、问题记录、临时技术结论，以及 Mermaid、截图、PDF 和当前允许附件。左侧选择文档后直接显示正文，减少“列表 → 详情 → 编辑”的导航成本。

需要稳定字段、约束、查询的 System、BusinessFunction、DatabaseObject、Integration 等继续使用 canonical 模型；分析笔记可以解释这些对象，但不能取代其字段。未知问题的处理继续使用 UnknownItem；记录调查文字不增加问题管理 workflow，也不自动把文字转为已确认知识。

v1 包含目录创建/重命名/移动/排序、文档创建、加入已有文档、移除 placement、阅读/编辑、既有 Markdown/Mermaid/附件/Revision、标题过滤、既有关系/证据/状态入口及 Portal Management 导航。首页选择简单欢迎空态，不实现最近打开持久化。

不包含 Personal/Team/Project Space、Tenant、Owner/ownership inheritance、Space RBAC、folder/page ACL、comments、mentions、watch、favorites、notifications、activity feed、analytics、实时协作、审批流、模板市场、AI/RAG、block editor、第二个 rich text/Markdown engine、folder attachment、拖拽框架或 generic hierarchy/CRUD/transaction framework。

## 3. 三层模型与唯一 truth

| 层 | 持久化职责 | 不具有的职责 |
| --- | --- | --- |
| A. Knowledge truth | KnowledgeDocument 及其 Revision、Attachment/AttachmentReference；System、BusinessFunction、DatabaseObject、Integration；KnowledgeRelation、Evidence/HC、KnowledgeStatus | 不由目录名、层级或页面编排重定义事实 |
| B. Analysis organization | AnalysisNode：Folder 或指向一个 KnowledgeDocument 的 Document placement | 无正文、文档 lifecycle、知识状态、权限继承或发布 |
| C. Portal publication composition | PortalPage 主对象、PortalPageSection 投影组合、PortalPageNode 阅读导航与各自发布规则 | 不负责分析过程的草稿组织与正文编辑 |

```text
AnalysisNode(Document) ──引用──> KnowledgeDocument <──显式引用── PortalPage / Section
         │                       │                               │
   authoring 目录          内容 / Revision / 附件          PortalPageNode + 发布检查
                                 │                               │
                      canonical 关系 / Evidence            匿名安全阅读投影
```

AnalysisNode 不保存 BodyMarkdown、summary、结构化对象、Evidence、Attachment、KnowledgeStatus 或 Relation 的副本，也不保存 rendered HTML/Mermaid 输出。读 DTO 可以联表投影当前文档标题、类型、lifecycle，但这些值不得写回 AnalysisNode。三个层都留在当前 Web、API、DbContext、SQLite、部署中。

## 4. DocumentType 唯一策略

**不新增 AnalysisNote。** DesignNote 足以表达调查推理、临时技术结论、设计权衡；KnowledgeArticle 适合解释性、参考性分析成果。名称“分析文档”只改变入口，没有新 lifecycle、search、revision 或 Portal 行为可以证明新类型必要。

| 操作 | 唯一规则 |
| --- | --- |
| 新建分析文档 | 默认 `DesignNote`，允许显式改选 `KnowledgeArticle`；创建为 Draft / Unknown，沿用文档校验与 canonical actor。 |
| 加入已有文档 | 七种全部允许：`Requirement`、`Specification`、`TestCase`、`Sop`、`Troubleshooting`、`KnowledgeArticle`、`DesignNote`。 |
| 需要新建需求/规格/用例/SOP/故障排查 | 通过现有知识内容创建入口选择真实类型，再加入目录；本入口不把它们伪装为 DesignNote。 |
| 加入/移动/移除 | 不改 DocumentType、lifecycle、status、修订或语义关系。 |

类型必须显示现有中文标签，`Sop` 等 API wire 值保持原样。Trace/Impact 仍仅对既有 Requirement / Specification / TestCase 类型开放；DesignNote 不因进入树而获得追溯类型资格。

## 5. 最小持久化与 migration 设计

唯一选择：新增 `analysis_nodes` 对应 **AnalysisNode**。没有 AnalysisWorkspace entity 或实体根；UI 固定“分析文档”根对应 `ParentId = null`，不占一行、不可 rename/move/delete。

| 字段 | 唯一规则 |
| --- | --- |
| Id | 正整数 long，API 使用 JavaScript safe integer 范围；SQLite 自增，不复用已移除节点身份。 |
| ParentId | nullable FK → analysis_nodes.id，RESTRICT；null 表示唯一逻辑根，否则必须指向 Folder。 |
| NodeType | 持久化闭合字符串 `Folder` / `Document`；创建后不可转换。 |
| Title | nullable；仅 Folder 存储 trim 后 1–200 字符标题；Document 必须 NULL。 |
| KnowledgeDocumentId | nullable FK → knowledge_documents.id，RESTRICT；Folder 必须 NULL，Document 必须非 NULL。 |
| SortOrder | 非负 int，同级唯一，由服务端生成/归一化；客户端不能任意赋值。 |
| CreatedAt / UpdatedAt | 服务端 UTC；组织元数据时间，不表示内容修订或知识确认。 |
| Version | long，初始 1，应用管理的 EF optimistic concurrency；不向客户端暴露可解释的物理版本。 |

只有上述九个字段名（两个时间字段分别存储）。无 IsDeleted、组织历史、publication audit、Owner、ACL、WorkspaceId 或 content cache。组织节点物理移除后即不存在；“最多一个 active placement”在本模型中就是最多一个现存 Document node。不可把 PortalPageNode 的软删除/发布审计整套复制过来。

后续 B01 使用一个 **additive migration**：创建空表、FK、CHECK 与索引，不回填已有文档，不扫描正文，不从 Portal 复制目录，不修改现有知识表/FTS/修订/Evidence/Portal schema。约束至少包括类型/字段 shape、非 self-parent、safe Id、Version ≥ 1、非负顺序；应用事务负责 Folder-parent、环路、全树深度和容量校验。

SQLite NULL 的唯一性须显式处理：根 `UNIQUE(sort_order) WHERE parent_id IS NULL`；非根 `UNIQUE(parent_id, sort_order) WHERE parent_id IS NOT NULL`。另设 `UNIQUE(knowledge_document_id) WHERE knowledge_document_id IS NOT NULL`，保证两用户同时加入同一文档也只有一个 placement。FK 全部 RESTRICT，无 cascade；现有 soft delete 只更新知识行，不受 FK 阻止。

本 A01 不生成或运行 migration。B01 只在 task-owned SQLite 验证 additive 行为；真实数据部署另行执行受控流程。Down 会丢失新组织信息，不能作为日常撤销操作。

## 6. Tree 与 placement 规则

1. 只有 Folder 可有 children；Document 永远是叶子。Folder 可以为空。标题不承担身份，允许同级重名，使用 Id 区分。
2. 根深度 0，第一层节点深度 1，**最大深度 10（包含 Document）**。沿用当前 Portal 的深度预算以保持体验一致，不复用其实体或 publication 语义。
3. v1 全树最多 **2,000 个现存节点**，包含空目录和不可用文档 placement。沿用 Portal 已有的量级预算以支持有界树读取和快照校验；这是 Analysis 独立上限。超限新增拒绝，不截断成可编辑的“完整树”，提高上限须另行评估。
4. 新建与加入默认追加父级末尾；move 使用目标 Folder/null + 目标插入位置，位置按源节点移出后的目标 child 列表计算，合法范围为 0 到该列表长度（含末尾）。移动 Folder 时保留整个子树。服务端以目标深度加子树高度校验，拒绝 self-parent、移动到自己后代、缺失/Document 父节点与深度 > 10。
5. 顶层 Folder 是同一逻辑根下的普通目录，可互相移动文档/子目录；不称为多个 workspace roots。请求不接受 WorkspaceId/RootId 或 Portal node。ID 只在 Analysis 表中解析，不能把其它资源类型当作父级。
6. 同父排序一次提交完整有序 child Id 列表，不能漏项、重复或混入其它父级节点。排序完成后为 `0..n-1`；新增/移除/跨父移动也归一化受影响的同级顺序。
7. Folder rename 只改 Folder.Title。Document 没有目录 rename/title override：修改其标题必须进入正文编辑，沿用 canonical content save，并按 REV 创建内容修订。文档标题更新后所有入口显示当前 KnowledgeDocument.Title。
8. 一个文档最多一个 placement；已加入的候选显示“已在目录中”并可定位，不能复制。移除后允许重新加入。Portal 多 placement 保持其现有独立规则。
9. “从目录移除”仅物理删除 Document node；文档正文、附件、修订、Evidence、HC、关系、状态、Portal 引用与发布保持原语义。只有一个 placement 也不例外。
10. “删除空目录”仅物理删除无 children 的 Folder。**非空 Folder 拒绝删除**，包括仅有不可用文档占位的情况；先移走/移除 children。无递归删除、隐式提升 children 或批量删除知识。

## 7. Lifecycle、soft delete 与知识能力复用

| 文档状态 | 树 / 正文 | 写入及衔接 |
| --- | --- | --- |
| Draft | 显示并明确“草稿”；Viewer+ 可读 | Editor+ 可编辑；不因 placement 发布或进入 Portal。 |
| Published | 显示并明确“已发布” | Editor+ 沿用现有保存路径；内容变更产生下一修订并立即更新已发布 head。 |
| Archived | **保留原位置并标记“已归档”**；可读 | 正文不可编辑/上传；组织移动、排序、移除仍可操作。恢复编辑须走既有 Archived → Draft 操作，不能从目录中绕过。 |
| soft-deleted / 不可解析引用 | 保留位置，安全标签“文档不可用”，不返回旧标题、summary、正文或附件；不自动跳到历史正文 | Editor+ 可移动/移除 placement。禁止编辑、加入该失效文档或 handoff；当前正文请求按原接口拒绝。历史仅经既有受保护 tombstone/revision 入口访问。 |

失效占位保留 nodeId、parent/order、目标标识和通用不可用状态，以便清理；不得用 IgnoreQueryFilters 将历史文档详情混入 current tree。FK 正常情况下防止物理悬空，soft delete 仍可能使引用不可读。维护端若按原有受控规则恢复同一文档，重新解析后该 placement 恢复显示，不产生新文档或自动发布。

真正“删除知识文档”仍走原 `/api/knowledge-documents/{id}` delete：原创建人 Editor 或 Administrator、当前 token、canonical relation dependency guards。Analysis placement 是组织引用，**不增加 canonical 删除 blocker**；删除后显示不可用占位。此规则是新增组织引用的分类，不改变既有八类知识 root 的软删除规则。

Revision 完整复用：创建文档即原子创建 revision 1；保存语义变更产生下一 revision；no-op 不产生修订；history/compare/restore 使用既有组件/API，restore Draft-only。目录创建/rename/move/reorder/remove 不创建 KnowledgeDocumentRevision，也没有 AnalysisRevision、TreeRevision 或 WorkspaceRevision。

Attachment 只归属 KnowledgeDocument，引用随内容修订保存；图片使用现有受保护 `attachment:<id>`，PDF 与其它预览/下载依服务端 capability，不猜扩展名能力。新建草稿成功后才允许上传，避免 Folder 或尚不存在文档拥有附件。沿用完整 fileAttachmentIds 保存、上传中阻止保存/切换、失效引用和存储错误处理；不复制附件 UI 或引入 FolderAttachment/WorkspaceAttachment。

Evidence、HumanConfirmation、KnowledgeStatus 继续绑定 KnowledgeDocument，HC 捕获既有 current revision snapshot。AnalysisNode 不进入 Evidence SubjectType 或 KnowledgeTargetType。撤销/替代、历史有效集合、status progression guards 完整复用；目录操作与普通内容保存均不自动确认知识。canonical KnowledgeRelation 仍由用户明确创建/移除并遵守类型矩阵；放入“MES”目录不会创建 System 关系，移动目录不会改变关系，移除 placement 不删除关系。

## 8. 权限与 API 安全

| 能力 | Viewer | Editor | Administrator |
| --- | --- | --- | --- |
| 工作区树、有效文档阅读、标题过滤 | 是 | 是 | 是 |
| Folder create/rename/move/reorder/delete-empty；placement create/move/remove | 否 | 是，所有组织节点 | 是 |
| 新建/编辑文档、既有附件/关系/证据/状态动作 | 否 | 按现有文档及各能力规则 | 按现有规则 |
| 真正删除文档 | 否 | 原创建人且通过既有依赖规则 | 通过既有依赖规则 |
| Portal Management / handoff | 否 | 否 | 是 |

组织节点不继承创建人所有权，也不保护“私人草稿”；Draft 与当前 canonical read authority 一致，所有 Viewer+ 可读。树只负责组织，不是安全隔离措施。现有用户停用、身份失效、强制改密、权限下降和 antiforgery 都继续 fail closed。隐藏按钮不能替代后端政策；`/analysis` 使用现有 authenticated app-shell 和 SecurityGate，绝不标为 Portal anonymous route。`/api/analysis/**` 显式 Viewer 读取、Editor mutation，不添加 AllowAnonymous。

## 9. 组织并发的唯一方案

使用 **AnalysisNode.Version + 派生全树组织快照 token + SqliteImmediateTransaction**。这是一棵有界小树的保守并发控制，接受不同目录的并行修改也可能发生 409，以换取不增加根实体或持久化树版本。不得 last-write-wins。

- `GET /api/analysis/tree` 在一致读取快照内返回完整节点 metadata、节点 opaque concurrencyToken 与 `treeConcurrencyToken`。后者为按 Id 排序的全部节点组织元组（Id、ParentId、NodeType、KnowledgeDocumentId、SortOrder、Version）的确定性 SHA-256 摘要，含固定协议前缀与无歧义编码；客户端仅原样回传。空树也有确定 token。
- 该摘要 **不落库、不构成 revision/history，也不是权限凭据**。Folder rename 必须递增 Version，因此进入摘要；正文/标题/文档状态的 current 投影不纳入组织摘要，正文另用 KnowledgeDocument token。
- 每个组织 mutation，包括根新增、加入、原子创建、rename、move、reorder、remove、delete-empty，都必须提交最近完整树的 `treeConcurrencyToken`。作用于现有节点的操作还提交该节点 token；reorder 提交完整 child Id/token 列表。客户端不得从过滤后的可见子集构造快照或排序请求。
- 先取得 SQLite immediate 写保留，再读取完整组织状态、核对 tokens、目标/父级存在性、同级集合、全部祖先与子树深度、唯一 placement、容量和当前文档可用性。所有校验与写入处于同一事务；成功才 commit。复用已存在的 `SqliteImmediateTransaction`，不依赖进程锁或新的事务框架。
- 变化节点 Version +1、UpdatedAt 更新；组织 no-op 不改 Version/time。跨父 move 归一化两组 sibling，reorder 归一化一组；所有 order/parent 实际改变的节点均 touch。先在事务内移动到无冲突临时顺序区，再写最终 `0..n-1`，避免唯一索引逐行冲突；临时值不得对外提交，溢出必须拒绝。
- token 过期或读取后发生组织变化 → `409 conflict`（沿用现有 wire code）；数据库唯一 placement 竞争同样受事务保护，不能留下第二位置。刷新后由用户重试，不自动套用新 token 重放旧命令。错误不返回部分成功树。

这一方案覆盖祖先移动与后代编辑、两个 root create、两个跨父 move、reorder 对 create/remove、empty-folder delete 对 create-child，以及两个用户加入相同文档。content save 与目录 move 独立；canonical document delete 与新增 placement 使用同一 SQLite 写串行边界：delete 先提交则加入拒绝，加入先提交后 delete 合法并转不可用占位。

## 10. 明确的创建事务与 API 边界

**CreateAnalysisDocument 单一 backend use case，单一数据库事务**，不采用前端两个 HTTP 写请求。外层先取得 immediate transaction 并验证目录 tokens/目标/容量，再调用现有 `KnowledgeDocumentService.Create`，沿用校验、canonical actor、Draft/Unknown、revision 1、FTS 更新；随后加入 Document placement、调整顺序，全部成功后由外层 commit。

当前 `SqliteImmediateTransaction.BeginAsync` 已能加入 DbContext 现有事务，内层 CommitAsync 不提交外层事务，因此有具体复用落点。B01 须验证真实调用链、失败 rollback 和响应；若需要调整，只允许局部使原 create 可组合，不复制验证/修订/FTS 逻辑，不改变 `/api/knowledge-documents` 创建行为。任一步失败，KnowledgeDocument、revision、FTS 和 placement 均不得部分提交；失败后不复用带脏 tracked entities 的 request DbContext。

默认只需标题即可创建空正文草稿，服务端复用既有空 Draft 规则，返回 documentId/nodeId 与新 tokens；右侧立即进入编辑，上传在此后走 canonical 附件端点。UI 防双击；创建响应丢失时先刷新目录/核对创建结果，禁止盲目重试创建。v1 不新增 idempotency 存储或通用重试框架。

下表为 **未来 B01 明确端点合同**，当前仓库尚无这些接口：

| 方法 / route | 责任与关键输入 |
| --- | --- |
| `GET /api/analysis/tree` | 完整有界组织 metadata 与 current document identity projection、tokens；不批量拉正文/附件/关系。 |
| `POST /api/analysis/folders` | parentId、title、tree token；追加空 Folder。 |
| `PUT /api/analysis/folders/{id}/title` | title、node/tree tokens；仅 rename Folder。 |
| `POST /api/analysis/documents` | CreateAnalysisDocument：parentId、tree token 和 canonical create 内容字段；类型仅 DesignNote/KnowledgeArticle。 |
| `POST /api/analysis/document-placements` | parentId、knowledgeDocumentId、tree token；引用当前非 deleted 文档，允许显式选择 Archived。 |
| `POST /api/analysis/nodes/{id}/move` | targetParentId、目标插入位置、node/tree tokens；只移动，不更新正文或节点类型。 |
| `PUT /api/analysis/children/order` | parentId、完整有序 Id/token 列表、tree token；仅同级 reorder。 |
| `DELETE /api/analysis/document-placements/{id}` | 此 id 是 nodeId；node/tree tokens；只移除 Document placement。 |
| `DELETE /api/analysis/folders/{id}` | node/tree tokens；只删除空 Folder。 |

内容读取/保存、lifecycle、history/compare/restore、附件等继续使用 `/api/knowledge-documents/**` 与现有其它 feature 端点；不增加 `/api/analysis/**/content`、search index endpoint、publish-folder 或任意字段 PATCH。API 返回沿用共享 ApiErrorResponse：400 validation_error（格式/字段/列表重复）、401/403 原安全错误、404 not_found（操作节点不存在）、409 conflict（过期 token/并发状态）、422 reference_invalid（目标不可用）或 business_rule_violation（循环、深度/容量、非空删除、已有 placement 等）。stale snapshot 先返回 409，不把旧视图操作解释成新的请求。

## 11. 搜索、已有文档与 Portal handoff

工作区内选择 **B：只过滤当前 tree 的标题**，输入文案“筛选目录和文档标题”。按 Folder.Title 与当前 KnowledgeDocument.Title 做 trim 后大小写不敏感的字面子串匹配，不用正则、LIKE 通配或正文匹配；保留匹配项及祖先路径，空输入恢复全树。过滤 Folder 命中不视为其所有子孙命中；筛选时禁用 reorder，防止用隐藏 sibling 的不完整集合排序。不可用文档只显示安全占位，不能用历史标题命中。

正文继续进入 **现有 KnowledgeDocument FTS**，索引只由 canonical create/save/restore/delete 维护。工作区不提供新全文搜索：用户使用 app-shell 原有全局搜索（其结果不限分析目录，仍跳 canonical 文档），不把全局前 20 项再过滤成伪“目录全文搜索”。当前全局文档结果包含 Draft/Published，排除 Archived/deleted；底层 FTS 保留 current head、查询 lifecycle 条件负责排除 Archived，不搜索历史 revision。树显示 Archived 与 FTS 排除 Archived 是不同目的的现有/新增视图规则。

“加入已有文档”复用现有 `/api/knowledge-documents` 分页列表、类型和 lifecycle 筛选。默认列表仍是非 Archived；要加入归档文档必须显式选择 Archived。已放置 Id 可从完整树判断用于 UI 提示，后端始终重验唯一性/权限/当前状态。不新增候选索引、second FTS、AnalysisSearchIndex 或 semantic search。

PortalPageNode 与 AnalysisNode **完全独立**。加入目录、文档 Published、移动/移除、Folder 操作均不生成或同步 PortalPage/Node/Section。成熟内容路径固定为：

```text
Analysis Workspace 编写 → 同一个 KnowledgeDocument
→ Administrator 在 Portal Management 显式引用并编排
→ 既有 Page / Node / ancestor 发布与 readiness 检查
→ 匿名 safe Portal projection
```

v1 提供 Administrator-only **“在知识门户管理中使用”**。仅当当前目标是可读 Published 文档时可用；Draft/Archived 明确提示先通过原文档 lifecycle 处理，不自动发布；不可用目标禁用。目标为现有 `/portal-management`，未来 B03 加只读导航提示 `targetType=KnowledgeDocument&targetId={id}`，在未保存的新建页面表单中预选当前目标。管理页重新走现有 target resolution/readiness，不能相信 query 中的 title/lifecycle/permission；无效提示显示错误，不影响正常管理入口。

导航/预选本身 **不发 POST、不持久化 Draft composition、不自动创建或更改已有 Page/Node，更不匿名发布**。创建页面、选择 sections、放置和发布仍由管理员在管理页逐步明确操作。回到工作区不回写目录。管理页已有 dirty guard 同样保护导航与表单切换。

特别保留既有 live projection 行为：未编排的 Published 文档不会自行出现在 Portal；**已经被有效 Portal composition 引用的 Published 文档**在工作区保存后，其新发布修订会被现有 Portal 投影读取。这不是本工作区触发新的发布动作，也不能宣传为“工作区编辑不会影响门户”。复用现有 Published 保存提示；需要私下改稿时使用既有退回 Draft 流程，Portal 按原规则 fail closed。

## 12. Frontend IA 与复用落点

一级菜单“分析文档”，路由 `/analysis`（欢迎空态）及 `/analysis/nodes/:nodeId`（选择 Folder/Document）。均用 authenticated app-shell；左侧为此 feature 的树，右侧为欢迎/Folder 简单空态或选中文档正文。保留原“知识内容”列表/详情入口。

未来目录为 `src/SystemKnowledgeHub.Web/src/features/analysis-workspace/`；后端 `Features/AnalysisWorkspace/` 中放具体 controller/use cases/domain/configuration。仅 tree/selection/organization orchestration 属于新 feature。

| 当前真实实现 | 后续复用方式 |
| --- | --- |
| `knowledge-documents/pages/KnowledgeDocumentDetailView.vue` | 当前依赖 route.params.id，load/save/history/附件与部分 guard 内联在页面。B02 做最小局部提取：在原 feature 中形成接受 documentId 的文档面板/状态逻辑，原详情和工作区共同调用，不复制详情页，不直接将 nodeId 当 documentId。 |
| `editor/KnowledgeDocumentEditor.vue`、`editor/documentEditState.ts` | 保留 CodeMirror、唯一 bodyMarkdown、显式保存、Ctrl/Cmd+S、dirty 快照/丢弃提示、上传中限制。 |
| `markdown/KnowledgeDocumentMarkdown.vue` 与既有 renderer | 阅读/预览共用，禁原始 HTML、沿用 Mermaid strict、安全链接/附件上下文；不另写 renderer。 |
| `KnowledgeDocumentAttachmentArea.vue`、RevisionHistory/Restore 与既有 overlays | 保留附件、历史、compare/restore、证据/关系/状态入口；必要时链接到原详情的对应能力，不新建第二套 API/UI。 |
| PortalManagementView / PortalLayout | 只参考现有两栏视觉/交互；不嵌入匿名 PortalLayout、Portal DTO/client 或 Administrator-only management tree。使用现有 Element Plus 控件和共享样式，不新增树框架。 |

文档点击默认查看模式，Editor+ 可进入编辑；新建成功直接编辑。标题/summary/正文与附件选择是一份编辑状态，一次既有保存。save 的 400/403/404/409/422/503 沿用原错误语义，失败保留未保存内容；权限刷新或引用不可用不应错误宣称保存成功。

**dirty switch 是明确新增集成风险**：同页点击其它 document/folder/home、改变 nodeId 路由、浏览器前进后退、移除当前 placement、Portal handoff、离开页面与窗口关闭都经过现有丢弃确认/上传保护。当前 `onBeforeRouteLeave` 不足以证明同组件参数切换安全；B02 要在 route update/selection 提交前运行同一 guard。取消后保持旧选中节点与原 buffer；不先更新 ID 再询问，也不自动保存。

每次加载/保存/overlay 都绑定实际 documentId 和请求序列，晚到的旧响应不能覆盖新选择；树刷新/文档标题保存只更新安全 metadata，不卸载 dirty editor。组织 409 刷新树不会重置正文 buffer；content 409 不用 tree token 覆盖 document token。没有实时协作、后台轮询或持久化最近打开；进入/显式刷新/成功操作时重读所需状态即可。

Folder 用简单空态；tree filtering 无结果、无文档、引用不可用、加载失败分别给明确状态。目录移动通过现有控件选择父目录，排序用明确上移/下移操作组装完整 sibling 请求；不引入 drag-and-drop framework。

## 13. 后续分片与最小验证

| Slice | 独立交付边界 | 必要验证风险 |
| --- | --- | --- |
| ANALYSIS-B01 | AnalysisNode additive migration、tree/Folder/move/reorder/remove、existing placement API、CreateAnalysisDocument 原子复用与安全合同。暂不做工作区 UI。 | tree shape/cycle/depth/unique root+sibling order；node/tree stale 与 root/ancestor/move/reorder 竞争；create+revision+FTS+placement rollback；Viewer/Editor/Admin 与 antiforgery；remove/empty-folder、soft-delete 竞争及无知识 cascade。仅 focused backend。 |
| ANALYSIS-B02 | authenticated 菜单/route、树、创建、selection、Folder mutation、右侧文档面板局部提取复用、dirty/concurrency、既有附件/修订/关系/证据/状态入口。 | focused tree/selection/create、共享编辑保存及原详情直接回归、同 route 参数 dirty switch/晚响应保护；相关 type-check/build。 |
| ANALYSIS-B03 | 已有文档选择 UI、标题过滤、定位重复 placement、Administrator Portal handoff 与未保存预选。API 使用 B01 基础，不延期核心 schema。 | focused candidate/lifecycle/filter 与权限、query validation、导航无隐式 Portal 写入；只覆盖新增 UI 与管理页直接风险。 |
| ANALYSIS-VERIFY | 前述切片完成后的最小端到端闭环与报告。 | 一个默认桌面尺寸：新建目录 → 新建分析文档 → 编辑保存 → 切换 → Portal Management 入口；相关已通过且未变更的证据复用。 |

验证按 AGENTS 最小风险范围，不 full backend/frontend suite、不多 viewport/responsive matrix、不 browser zoom、不无关 feature 回归。B01 必须以 task-owned SQLite 测实际事务/约束，不能只 mock atomicity；无需每种 DocumentType 重复整条 UI 流程。未来 browser/runtime 必须 task-owned DB、Data Protection、附件/日志与隔离端口，记录原数据库基线并清理自建进程；不访问真实数据库做测试。

A01 本身只做来源/实现静态核对、26 项决策唯一性检查、文档链接与 diff 检查；build/test/migration/browser 均不适用，不借文档决策重复 Portal 验收。

## 14. 26 项兼容问题的唯一答案

| # | 问题 | 冻结答案 |
| --- | --- | --- |
| 1 | 复用 KnowledgeDocument？ | 是，唯一正文 truth（§3）。 |
| 2 | 默认新建类型？ | DesignNote，可显式选 KnowledgeArticle（§4）。 |
| 3 | 新增 AnalysisNote？ | 否（§4）。 |
| 4 | AnalysisWorkspace entity？ | 不需要；单一逻辑全局根（§5）。 |
| 5 | AnalysisNode 最小字段？ | Id、ParentId、NodeType、Title?、KnowledgeDocumentId?、SortOrder、CreatedAt、UpdatedAt、Version（§5）。 |
| 6 | Folder / Document？ | Folder 可有 children；Document 引用一个文档且是叶子；类型不可变（§6）。 |
| 7 | 最大深度？ | 根 0，节点 1–10；总节点上限 2,000（§6）。 |
| 8 | 多 placement？ | 否，一个文档最多一个现存 Analysis Document node（§6）。 |
| 9 | Folder delete？ | 仅空目录可物理删除；非空拒绝（§6）。 |
| 10 | remove 是否删文档？ | 永不；只物理移除 placement（§6–7）。 |
| 11 | existing 文档可加入？ | 是，当前未 soft-delete 文档（§11）。 |
| 12 | 可加入类型？ | 当前七种全部允许，列于 §4。 |
| 13 | 三种 lifecycle 显示？ | 都保留并标记；Archived 正文只读（§7）。 |
| 14 | soft-deleted placement？ | 保留安全不可用占位，可移动/移除，不复制或读取历史正文（§7）。 |
| 15 | 权限？ | Viewer+ 读、Editor+ 组织/普通写，知识删除保留原所有权规则；Portal 管理仅 Administrator（§8）。 |
| 16 | tree concurrency？ | Node.Version + 派生全树 token + SQLite immediate transaction（§9）。 |
| 17 | title override？ | 否；Folder 自有 Title，Document.Title 来自 canonical（§5–6）。 |
| 18 | Search？ | B：仅 tree 标题过滤；保留全局 KnowledgeDocument FTS，无工作区全文搜索（§11）。 |
| 19 | Evidence/HC/Status 绑定？ | KnowledgeDocument，原 contract，不是 AnalysisNode（§7）。 |
| 20 | Attachment 绑定？ | KnowledgeDocument 与既有 Revision 引用，不是 Folder/Workspace（§7）。 |
| 21 | Relation 与 placement？ | 独立；placement 不自动创建、更改、删除语义关系（§7）。 |
| 22 | Portal / Analysis tree？ | 独立持久化与生命周期，无树同步（§3、§11）。 |
| 23 | 进入 Portal Management？ | Administrator 对有效 Published 文档只导航和未保存预选（§11）。 |
| 24 | 自动创建 Portal Page？ | 禁止，连 Draft composition 也不自动持久化（§11）。 |
| 25 | migration？ | 后续 B01 一个 additive migration，仅 AnalysisNode；A01 不生成（§5）。 |
| 26 | 后续拆分？ | B01 后端、B02 树/编辑复用、B03 existing/filter/handoff、VERIFY 最小闭环（§13）。 |

## 15. 最终状态

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
Implementation: NOT STARTED
Blocking product / architecture decisions: NONE
```
