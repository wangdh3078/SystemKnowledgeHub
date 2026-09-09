# ANALYSIS-VERIFY — Analysis Workspace v1 Final Verification

2026-09-09。**ANALYSIS-VERIFY PASS；ANALYSIS WORKSPACE V1 COMPLETE**。

验收基线为 authoritative `main` / **`9285709e81191b7837230ba4a81a12cd7e4ed21b`**（`fix(ui): refine manual acceptance surfaces`）。开始时工作区干净，已核对 status、diff、最近十次提交。本任务只做最终集成验收和阶段关闭，没有修改产品、测试、配置、依赖、架构或 migration。

## Authority、能力与复用证据

依据根 [AGENTS.md](../../AGENTS.md)、[DOCUMENT_INDEX](../DOCUMENT_INDEX.md) 和 [ANALYSIS-A01 冻结决策](../design/ANALYSIS_A01_ANALYSIS_WORKSPACE_ARCHITECTURE_DECISION.md)及其 [决策报告](ANALYSIS_A01_ANALYSIS_WORKSPACE_ARCHITECTURE_DECISION_REPORT.md)。A01 APPROVED / FROZEN；B01、B02、B03、DEMO-DATA-R01、UI-MANUAL-R01 COMPLETE。相关 KnowledgeDocument、REV-A01、ATTACH-A01/A02、Evidence/HC-B01、KnowledgeStatus、KC-C01、TRACE-A01、DELETE-A01、AUTH/AccessControl、Portal A01/B01–B04/FINAL 与 DBSAFE 边界保持不变。

| 复用来源 | 适用证据与当前源码核对 |
| --- | --- |
| [B01 foundation](ANALYSIS_B01_PERSISTENCE_TREE_API_FOUNDATION_VERIFICATION_REPORT.md) | 19 项聚焦后端测试的 schema/migration/tree、原子创建、并发、rollback/race、权限与 soft-delete 安全证据。`git diff c3fba35..HEAD` 中 AnalysisWorkspace 与 Persistence 无差异；API 后续变化仅显式 Demo tooling 和 Program 的 Demo 条件分支，没有 Analysis 业务修改。本次不重跑后端 suite。 |
| [B02 authoring](ANALYSIS_B02_WORKSPACE_UI_DOCUMENT_REUSE_VERIFICATION_REPORT.md) | 105 项聚焦测试及隔离浏览器证据；复用 Folder rename/delete-empty、Viewer/Editor、Archived/unavailable、附件、Evidence/HC、状态、关系、Trace/Impact、上传/晚响应/冲突保护。共享 DetailPanel 及这些业务逻辑未改变；后续文档区修改为 Evidence 样式及断言。 |
| [B03 organization](ANALYSIS_B03_EXISTING_PLACEMENT_FILTER_PORTAL_HANDOFF_VERIFICATION_REPORT.md) | Existing 七类型/生命周期、标题字面筛选与完整 sibling 保护、canonical handoff 及负向验证。UI-R01 后 Analysis 变动只涉及按钮呈现和分组，未改业务脚本。当前直接相关前端测试另见下表。 |
| [UI-MANUAL-R01](UI_MANUAL_R01_VISUAL_CONSISTENCY_CORRECTION_REPORT.md) | 复用源码未再改变的 4 项：Analysis 原生按钮层级、dirty handoff，以及 KnowledgeDocumentDetailView 的 Evidence 固定目标/抽屉/显式状态、HC 字段标签。当前浏览器再次观察操作层级。 |
| [Demo report](DEMO_DATA_R01_PERSISTENT_MANUAL_ACCEPTANCE_DATASET_REPORT.md) / [guide](../DEMO_DATA_GUIDE.md) | 复用 Archived/unavailable placement 与人工数据能力证据；本任务没有打开 Demo 数据库或对 Demo 执行业务操作。 |

静态核对：Analysis 仅有九字段 `AnalysisNode`，文档节点引用 canonical `KnowledgeDocumentId`；没有 AnalysisDocument、Workspace entity、Revision、Attachment、Evidence、Relation 或 Status 的第二模型。`/analysis` 复用同一 KnowledgeDocumentDetailPanel；正文、Markdown/Mermaid、修订及其他知识能力仍属 canonical 文档。Analysis 为 authoring organization，Portal 为独立 publication composition。

## 当前 HEAD 聚焦检查

| 检查 | 结果 |
| --- | --- |
| analysisWorkspaceApi.spec.ts | PASS — 3 项 |
| AnalysisWorkspaceView.spec.ts | PASS — 本次 27 项；另外 2 项复用 UI-R01 |
| ExistingDocumentPicker.spec.ts | PASS — 2 项 |
| PortalManagementHandoff.spec.ts | PASS — 19 项 |
| 少量 shared panel regression | PASS — 复用上述 UI-R01 的 2 项 KnowledgeDocumentDetailView 测试 |
| `npm run type-check` | PASS |
| `npm run build` | PASS；保留既有大于 500 kB chunk 提示 |
| `dotnet build SystemKnowledgeHub.sln -c Release --disable-build-servers` | PASS；0 warnings / 0 errors |
| `dotnet ef migrations has-pending-model-changes` | PASS；`No changes have been made to the model since the last migration.` |

Vitest 一次运行上述四个 Analysis/handoff 文件，使用 `-t '^(?!.*(?:groups native default-size|guards dirty handoff)).*$'` 排除已经通过的两项重复验证，实际 **4 files / 51 passed / 2 intentionally unselected**。这两项是复用 PASS，未冒充本次运行；加上共享面板的两项复用，直接相关证据共 55 项。未运行 full Vitest、全 Portal、全 KnowledgeDocument 或全后端测试，未新增测试。

EF 使用 `--project src/SystemKnowledgeHub.Api --startup-project src/SystemKnowledgeHub.Api --configuration Release --no-build`；`SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH` 显式指向任务临时目录下绝对路径 `design-time.db`，未创建该文件。既有 required-navigation/global-query-filter warnings 保留。仅改文档，不运行全量 ESLint/Prettier。

## 一个默认桌面浏览器中的闭环

Chrome 单一默认桌面窗口，未调整 viewport、设备模式或 zoom。API 通过 `dotnet run --project src/SystemKnowledgeHub.Api -c Release --no-build --no-launch-profile` 启动，显式 Development，端口 5211；Vite 5212，浏览器使用 `http://localhost:5212`。SQLite、Data Protection、Attachment StorageRoot、日志均显式隔离在 `%TEMP%/analysis-verify-20260909`。supported bootstrap 创建任务临时 Administrator，真实 Local login 成功，没有替代认证或关闭安全校验。

1. 初始 Portal 树 **0 节点**、页面库 **0 页**，Analysis 为空。创建“最终验收”Folder 后自动选中，路由 `/analysis/nodes/1`。
2. 在 Folder 创建 **Analysis Verify · Lot Track In**，默认 DesignNote；仅 **1 次 `POST /api/analysis/documents`**，成功选择 node 2，canonical document 1，自动进入编辑。
3. 保存 MES/EAP/Lot/STATE_FLAG 的标题、段落、列表、小表格与简单 Mermaid，生成 **revision 2**。阅读区实际渲染表格和 MES → EAP → Lot 图；树显示 canonical 标题。Analysis 内打开修订历史，见 revision 1/2 并返回正文；独立 `/knowledge-documents/1` 显示相同标题、正文和 revision 2。
4. 再编辑并用键盘加入未保存缓冲；点击 Folder 后“继续编辑”保持 node 2 路由与缓冲，第二次明确“放弃修改”后切换 node 1。重新读取无未保存文本、仍为 revision 2。
5. 将 node 2 移到根目录，再执行一次上移；最终根顺序为文档、最终验收 Folder。无重复 placement，canonical 正文和 revision 2 未改变。
6. 从现有知识内容入口创建并显式发布 **Analysis Verify · Published EAP**（canonical document 2，KnowledgeArticle），发布不生成 Analysis placement。回 Folder 使用“加入已有文档”，候选来自现有 `page=1&pageSize=20` 服务端分页；加入 node 3 后直接阅读。重开 picker 两篇文档均显示“已在分析目录中”，不再提供重复加入。
7. 筛选 `Published EAP` 只显示命中文档及“最终验收”祖先，reorder 禁用并提示清除筛选；清空后恢复全部三个节点。树方向键焦点具有可见紫色边框；按钮有名称，对话框有明确标题，编辑器文本输入和光标快捷键可用。按钮保持 UI-R01 层级：创建区仅新建分析文档 Primary，其余组织/handoff Secondary，移除 danger plain。
8. Published handoff 到 `/portal-management?targetType=KnowledgeDocument&targetId=2`，仅打开未保存的新建页面对话框并预选 canonical 文档；页面标题仍空，未创建/保存/发布 Page、Section 或 Node。此时 Portal 仍 **0 节点、0 页**。Draft 文档只显示“请先发布知识文档”，无 handoff 操作。
9. 对 node 2 执行“从分析目录移除”，确认提示“知识文档本身不会被删除”；节点消失，原 `/knowledge-documents/1` 仍可读取相同正文及 revision 2。“删除知识内容”是另一个独立动作，本次没有执行 canonical 删除。

Viewer runtime 未另建账号；Viewer 只读、Editor/Admin 组织写、Administrator-only handoff 复用 B01/B02/B03 权限证据及当前 focused tests。没有为 Archived/unavailable 制造额外 soft-delete fixture；其他共享知识能力复用 B02，运行时只取 Revision History 为代表。

网络仅记录 method、URL、status，不记录 headers、cookie、密码或正文：组织写为 `/api/analysis/**`；内容保存为 `PUT /api/knowledge-documents/1/content`；参考文档创建/发布走 canonical endpoints。handoff 为 canonical GET、Portal tree/pages/targets GET；全过程 **Portal mutation 0**、Analysis content copy API **0**、意外 HTTP 4xx/5xx **0**。登录前一次 current-user 401 属正常认证流程。浏览器 console warn/error 记录为空，API 日志无 ERR/FTL。

## 数据保护、清理与交付

仓库 DB 仅执行文件系统 existence/size/UTC mtime/SHA-256，未 SQLite/EF open、copy、migrate、seed 或 checkpoint；before == after：**1,372,160 bytes**，**2026-09-07T15:51:07.1311811Z**，SHA-256 **`F6C2DC43EB04110A830E86635920AC8DE1B52C60D9C19E9E178741AFABCCAC88`**；`-wal`、`-shm` 前后均不存在。

用户持久 Demo `C:\Users\wang\AppData\Local\SystemKnowledgeHub\demo` 仅检查文件系统 metadata，前后相同，DB 1,191,936 bytes、manifest 3,167 bytes 及 attachments/keys/logs/runtime 全部保留；没有 reset/seed/迁移/复制/删除，也未停止用户进程。

已关闭任务浏览器标签，按 PID、启动时间、命令及父进程核对并停止任务 API/Vite，启动会话已退出、5211/5212 无监听。已核对绝对路径并删除整个任务临时目录，包括 DB/WAL/SHM、keys、日志和临时启动/配置文件；Attachment StorageRoot 未产生附件，未遗留测试结果或截图文件。临时目录不存在，工作区无运行时 artifacts。

本报告和 DOCUMENT_INDEX 为唯一交付文件；文件职责未变，PROJECT_FILE_MAP 无需更新。未发现产品 blocker 或新增 gap。最终 diff/whitespace/文档链接检查通过；在当前 `main` 使用单一提交 `docs(analysis): complete workspace verification`，完整 SHA 与 `push origin main` 实际结果记于交付回执。实现/验收结论与 Git 交付结果分别报告。

## Final gate

| Gate | 结果 |
| --- | --- |
| ANALYSIS-VERIFY | PASS |
| ANALYSIS WORKSPACE V1 | COMPLETE |
| A01 CONTRACT | PASS |
| B01 BACKEND FOUNDATION | PASS |
| B02 AUTHORING EXPERIENCE | PASS |
| B03 ORGANIZATION FLOW | PASS |
| ANALYSIS TREE | PASS |
| ATOMIC ANALYSIS DOCUMENT CREATE | PASS |
| CANONICAL KNOWLEDGEDOCUMENT REUSE | PASS |
| DIRTY SWITCH | PASS |
| MOVE / REORDER | PASS |
| REMOVE PLACEMENT | PASS |
| EXISTING DOCUMENT PLACEMENT | PASS |
| TITLE FILTER | PASS |
| PORTAL HANDOFF | PASS |
| NO IMPLICIT PORTAL WRITE | PASS |
| ANALYSIS / PORTAL TREE SEPARATION | PASS |
| VIEWER / EDITOR / ADMIN BOUNDARY | PASS |
| SHARED REVISION ENTRY | PASS |
| FOCUSED FRONTEND | PASS |
| TYPE CHECK | PASS |
| FRONTEND BUILD | PASS |
| SOLUTION BUILD | PASS |
| NO PENDING MODEL CHANGES | PASS |
| DEFAULT BROWSER E2E | PASS |
| ACCESSIBILITY MINIMUM | PASS |
| REPOSITORY DATA PROTECTION | PASS |
| PERSISTENT DEMO PRESERVED | PASS |
| CLEANUP | PASS |
| BROWSER ZOOM | NOT APPLICABLE |
| MULTI-VIEWPORT | NOT APPLICABLE |
| SEC-04 | UNCHANGED |
| PHASE-TRACE PRODUCT ACCEPTANCE | UNCHANGED |

Analysis A01/B01/B02/B03/VERIFY 全部完成。此结论不构成真实 Production deployment 或 PHASE-TRACE 领域 Product Acceptance；持久 Demo 继续供用户人工检查。本任务到此关闭，不开始下一 major phase。
