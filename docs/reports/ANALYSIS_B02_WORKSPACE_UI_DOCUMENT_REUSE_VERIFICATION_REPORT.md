# ANALYSIS-B02 — Workspace UI + KnowledgeDocument Authoring Reuse

日期：2026-09-08。**ANALYSIS-B02 PASS / COMPLETE；ANALYSIS-B03 READY: YES**。

开始基线为用户指定的 `main` / `c3fba3592988b899dfb1e86c1a62204f43aeecde`（`feat(analysis): add workspace tree foundation`）。已核对 status、diff、最近八次提交，开始时工作区干净。本任务只实现 B02；未开始 B03、Demo Dataset 或 ANALYSIS-VERIFY。

## 1. Authority 与范围

依据 [AGENTS.md](../../AGENTS.md)、[DOCUMENT_INDEX](../DOCUMENT_INDEX.md)、[ANALYSIS-A01](../design/ANALYSIS_A01_ANALYSIS_WORKSPACE_ARCHITECTURE_DECISION.md)、[B01 verification](ANALYSIS_B01_PERSISTENCE_TREE_API_FOUNDATION_VERIFICATION_REPORT.md) 和本次用户任务定义。文档能力沿用 Markdown Source Editor、REV-A01、ATTACH-A01/A02、HC-A01、KC-C01、TRACE-A01、DELETE-A01、AUTH 与 Portal 的既有冻结边界。

B01 合同足够支持本次 UI，无 `B01 CONTRACT GAP`。没有修改 backend product code、migration、API 合同、任何 frozen source、PortalManagement/Portal reading、packages 或运行安全默认值。没有已有文档选择器、标题过滤、Portal handoff、第二正文模型或 Analysis 编辑器。

## 2. 实现结果

- `/analysis` 与 `/analysis/nodes/:nodeId` 共用 `AnalysisWorkspaceView.vue`，均为 authenticated app-shell、navigationKey=analysis、无 Context Rail。菜单“分析文档”位于“知识内容”后、“待确认事项”前，Viewer+ 可见。
- 310px 左目录采用现有 Element Plus Tree、Folder/Document icons 与共享 tokens，右侧为欢迎态、简单 Folder 状态或共享知识文档面板。包含 selected/focus、loading/error、empty 与 unavailable 状态；无 drag/drop 或新增 UI 依赖。
- typed native-fetch client 消费八类 B01 操作：GET tree、create Folder、rename Folder、原子 create document、move、完整 sibling reorder、remove placement、delete empty Folder。树/节点/availability/tokens/mutation 严格解码；验证 safe ID、闭合 enum、日期、树引用/循环/深度/唯一性/连续顺序及 unavailable 安全摘要。
- 创建位置默认取当前 Folder、当前文档的父目录或 root，显示可读路径。Folder 名称 1–200；新文档标题 1–300，类型默认 DesignNote，可选 KnowledgeArticle。新文档只发一次 `/api/analysis/documents`，成功采用返回快照、选中新节点并自动编辑；离开后再选该文档默认阅读。
- Folder rename 不改变文档标题；move picker 排除自身/后代/Document，targetPosition 使用移除源节点后的目标子项数。同级上下移发送完整 id/nodeConcurrencyToken 列表，首/尾相应禁用。非空 Folder 禁删；remove placement 明确提示“知识文档本身不会被删除”，成功返回原父目录/root。
- mutation 成功直接采用返回 items/tree token，无多余 GET；409 只刷新树、提示冲突、保留表单输入与正文，不重放写操作。树读取失败禁用组织写操作，可保留已加载文档。未知传输结果先刷新核对，代码没有自动重试创建。
- 可读文档显示 canonical title、类型中文与 lifecycle；Archived 保留、正文沿原规则只读且可恢复草稿，组织操作仍可用。初次选择 Unavailable 只显示通用标题和安全文案，不挂载文档面板、不发内容 GET；Editor+ 仅可移动/移除该位置，Viewer 只读。

## 3. 共享文档与切换保护

将原 `KnowledgeDocumentDetailView.vue` 的完整实现局部提取到同 feature 的 `components/KnowledgeDocumentDetailPanel.vue`，接受实际 `documentId`。原 route wrapper 只负责解析 `route.params.id` 和委托导航保护；Analysis 先由 nodeId 解析 canonical documentId。未复制 load/save/history/evidence/relation 逻辑。

共享面板保留原 Markdown/CodeMirror、Mermaid 阅读/预览、图片/普通附件及预览、修订 history/compare/restore、Evidence/HC、KnowledgeStatus、Relationships、Traceability/Impact、canonical delete 与 lifecycle。原详情保留 breadcrumb/history query；Analysis 隐藏列表 breadcrumb，canonical delete 成功转换相应节点为 unavailable，目录移除调用独立的 placement API。

两个入口的 route leave/update guard 在提交导航前调用同一 `requestLeave()`，复用 `documentEditState.ts` 的快照、丢弃确认及更强的上传离开提示。覆盖同组件 A→B、Folder/root、直接路由参数变化、history query、离开和浏览器 back；beforeunload 继续由共享面板处理。取消保留 route、选中节点与 buffer；确认且导航成功后才清理旧编辑状态。Element Plus 内部 currentKey 在取消导航后也恢复。

detail、relations、evidence 请求都有 identity/sequence 检查。save/lifecycle/Published-save confirmation 绑定提交时的实际文档与选择序列；旧请求已经提交不会被虚构为失败，也不会覆盖新文档。组织快照刷新不为文档面板设置变化的 key，不重置 dirty buffer。保存事件即时更新树的安全 title/type/lifecycle，不改变组织 token。若目录刷新把当前节点标为 unavailable，立即隐藏旧正文并禁止其后台读取/快捷保存，但保持已经存在的 dirty buffer 和离开确认；初次选择 unavailable 节点不会挂载面板。

既有 Evidence、HC、Relationship、KnowledgeStatus overlays 的事件携带实际 subject；关系还携带另一端。面板仅刷新当前文档或其已加载关系的相关事件。文档 history 按 documentId 隔离；正文、附件和 HC 的服务端权限/并发 authority 均保留。

## 4. Focused verification

在 `src/SystemKnowledgeHub.Web` 执行，均为非 watch：

| Gate | 实际结果 |
| --- | --- |
| `vitest run src/features/analysis-workspace src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts` | **47/47 PASS**，3 files（目录/API 17；原详情 30，追加不可用保护后单独重跑 30/30）；包含 strict DTO/API、route/menu、树状态/所有组织操作、Viewer/Archived/unavailable、自动编辑一次性、树冲突输入/正文保留、取消/确认/back、上传保护、detail/relations/evidence/save 晚到保护及原详情回归 |
| 既有 `KnowledgeDocumentRevisionHistory.spec.ts`、`KnowledgeDocumentAttachmentArea.spec.ts`、`KnowledgeDocumentEditor.spec.ts`、`KnowledgeDocumentMarkdown.spec.ts` | 与下行合计 **58/58 PASS**，共 7 files；保护共享面板的真实依赖入口 |
| 既有 `EvidenceDetailDrawer.spec.ts`、`AddHumanConfirmationDrawer.spec.ts`、`KnowledgeStatusDialogContent.spec.ts` | 同上；确认原有撤销/替换、状态推进和 overlay 行为 |
| `npm run type-check` | **PASS** |
| `npm run build` | **PASS**；保留既有 >500kB chunk 提示，无本任务包升级/拆包调整 |
| 修改 TS/Vue 文件 ESLint `--max-warnings 0` | **PASS**，0 error / 0 warning |
| 本次新增/提取、route/navigation/host 与相关测试 Prettier；`git diff --check` | **PASS**；未全仓格式化 |

共 **105 个不同 focused tests 通过**，未运行 full Vitest、full backend、B01 backend suite、Portal regression 或其它无关测试。已通过且未变更的 58 个依赖用例未为最终计数重复执行。

## 5. 隔离 browser evidence

一个默认桌面浏览器环境，全程未设置 viewport、未执行 browser zoom、mobile/responsive 或多尺寸测试。任务目录为 `%TEMP%/analysis-b02-20260908`，API `127.0.0.1:5195`、Vite `127.0.0.1:5196`。显式 Development、task-owned SQLite、Data Protection application/key path、Attachment StorageRoot、日志和 CORS；用项目支持的 `dotnet run --project src/SystemKnowledgeHub.Api` 启动（当前构建使用 `--no-restore --no-launch-profile`，Development 由显式环境提供）。未直接运行输出 EXE。

使用既有 `bootstrap-local-admin --password-stdin` 只在临时 DB 创建本任务管理员 `analysisb02`。密码未写入跟踪文件。仅创建验收所需的一个 Folder 和两篇文档；未引入正式 Demo Dataset。

实际浏览器链路：

1. 菜单进入 Analysis，看到空树/欢迎态；新建 **MES 分析** Folder（node 1）。
2. 在 Folder 内新建 **Lot Track In 分析**（node 2 → KnowledgeDocument 1），默认设计说明，自动进入原 CodeMirror 编辑器。
3. 修改标题为 **Lot Track In 分析 · 已保存** 并保存 Markdown“入口条件 / 校验批次状态 / 记录 Track In 结果”；树标题即时更新，原文档达到修订 2，Markdown 正常阅读。
4. 新建第二篇 **Track In 异常分析**（node 3 → KnowledgeDocument 2）。返回首篇，写入未保存正文，点击第二篇；选择“继续编辑”后 URL 仍为 `/analysis/nodes/2`，旧标题/缓冲区完整。再次点击并确认“放弃修改”后成功切换。
5. 执行一次同级上移；把首篇位置移动到 root；确认“从分析目录移除”，返回 `/analysis`，树中仅保留 Folder/第二篇。
6. 从原“知识内容”列表打开 `/knowledge-documents/1`，同一标题、已保存正文、修订 2 仍存在，未保存实验内容未进入该修订。移除没有删除 canonical 文档。
7. 再次点击第二篇默认阅读，沿共享编辑器保存“异常处理”正文。Vite 自带代理调试记录直接确认 `/api/analysis/tree`、`/api/knowledge-documents/2`、`/api/knowledge-documents/2/content`，以及原 `/api/relationships?...objectId=2`、`/api/evidence?...subjectId=2`；无 Analysis content 副本 API。工具未提供 Network 面板，采用真实请求经过的 Vite proxy path 日志核对。

最终 browser console warn/error 读取为空。默认桌面截图确认两栏、选中节点、原文档操作区及 Markdown 显示正常。

启动过程中发现过旧 Debug 构建缺少 B01 route；已用当前源码重建纠正。后续尝试临时 ASP.NET 请求日志被既有安全配置校验拒绝，已移除该配置，改用 Vite 自带 proxy 调试；未放宽安全校验。最终运行链路及适用门禁均通过。

## 6. Data protection、清理与文档

真实数据库只读取文件属性/哈希，未用 SQLite/EF 打开、复制、迁移、seed、checkpoint 或修改。前后结果完全一致：

| 文件状态 | before = after |
| --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` 存在 / bytes | 是 / 1372160 |
| LastWriteTimeUtc | `2026-09-07T15:51:07.1311811Z` |
| SHA256 | `F6C2DC43EB04110A830E86635920AC8DE1B52C60D9C19E9E178741AFABCCAC88` |
| `-wal` / `-shm` | 均不存在 |

已关闭本任务浏览器 tab；按新鲜 PID/父进程/命令行证据停止本任务 API、Vite 及子进程。最终检查 5195/5196 无监听。核对临时目录绝对路径后，删除其数据库、keys、attachments、logs、启动脚本与进程记录，目录不存在。无 task-owned runtime state 入库，未终止用户已有服务。

同步本报告、[DOCUMENT_INDEX](../DOCUMENT_INDEX.md) 与 [PROJECT_FILE_MAP](../PROJECT_FILE_MAP.md)。无新增未解决 gap。SEC-04 Production rollout blocker 与 PHASE-TRACE real-domain Product Acceptance 保持各自原有状态；本次本地 smoke 不表示 Production 部署验收。

## 7. Final Gate

| Gate | Result |
| --- | --- |
| ANALYSIS-B02 | PASS / COMPLETE |
| ANALYSIS ROUTES | PASS |
| NAVIGATION | PASS |
| TREE UI | PASS |
| FOLDER CREATE | PASS |
| ANALYSIS DOCUMENT CREATE | PASS |
| AUTO EDIT AFTER CREATE | PASS |
| FOLDER RENAME | PASS |
| MOVE | PASS |
| REORDER | PASS |
| REMOVE PLACEMENT | PASS |
| EMPTY FOLDER DELETE | PASS |
| VIEWER READ-ONLY | PASS |
| ARCHIVED DISPLAY | PASS |
| UNAVAILABLE SAFE STATE | PASS |
| KNOWLEDGEDOCUMENT PANEL REUSE | PASS |
| ORIGINAL DETAIL ROUTE REGRESSION | PASS |
| MARKDOWN / EDIT SAVE | PASS |
| ATTACHMENT / REVISION ENTRY REUSE | PASS |
| EVIDENCE / HC / RELATION / STATUS ENTRY REUSE | PASS |
| DIRTY NODE SWITCH | PASS |
| ROUTE UPDATE / BACK-FORWARD GUARD | PASS |
| UPLOAD LEAVE GUARD | PASS |
| LATE RESPONSE PROTECTION | PASS |
| TREE 409 RECOVERY | PASS |
| TITLE REFRESH WITHOUT EDITOR RESET | PASS |
| NO BACKEND PRODUCT CHANGE | PASS |
| NO PORTAL CHANGE | PASS |
| FOCUSED FRONTEND | PASS |
| TYPE CHECK | PASS |
| BUILD | PASS |
| DEFAULT BROWSER E2E | PASS |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |
| BROWSER ZOOM | NOT APPLICABLE |
| MULTI-VIEWPORT | NOT APPLICABLE |
| ANALYSIS-B03 READY | YES |

技术验证与 Git delivery 分开记录。实现与本报告按任务要求形成一个 `feat(analysis): add workspace authoring experience` 提交；完整 SHA、`origin main` 推送结果以本任务最终回报为准。任务到 B02 为止，不自动继续下一阶段。
