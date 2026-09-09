# UI-MANUAL-R01 — Manual Acceptance Visual Consistency Corrections

日期：2026-09-09。实际基线：`main` / `bc66bf5eddf9304f5a7d33bed2e7f8407f3a52c3`，包含任务所列 `2c85ed9` 及随后已交付的持久 Demo；开始时工作区干净。

**UI-MANUAL-R01：PASS / COMPLETE。** 本次只修前端表现与静态交互暗示，ANALYSIS-VERIFY 仍保持 READY，未执行。

## 六项修正

以下文件路径均相对于 `src/SystemKnowledgeHub.Web/src/features/`。

| 问题 | 修正文件与结果 |
| --- | --- |
| Portal 宽度利用不足 | `portal-reading/portal-reading.css`：Home/Page 复用 1500px 页面上限和统一 gutter；最近发布与结构化章节使用主内容宽度；Hero、Summary、Markdown 正文限制为 1120px。侧栏宽度不变。 |
| Context Rail 假点击暗示 | `systems/components/SystemContextRail.vue`、`database-knowledge/components/DatabaseObjectContextRail.vue` 移除静态箭头；`business-rules/components/BusinessRuleContextRail.vue`、`unknown-items/components/UnknownItemContextRail.vue` 将无事件按钮改为静态摘要，并同步各自 CSS。BusinessFunction/Integration 已是静态摘要，无需改动。 |
| 估算行数居中 | `database-knowledge/components/DatabaseObjectKnowledgeDrawer.vue`：仅对估算行数输入添加 scoped 左对齐，不改变校验、数值或其它数字控件。 |
| Evidence 查看记录位置 | `knowledge-documents/knowledge-documents.css`：记录头分为标题区和右侧操作区，保留原 link button 与打开详情行为，缩小记录及字段垂直间距。 |
| Analysis 按钮不统一 | `analysis-workspace/pages/AnalysisWorkspaceView.vue`、`styles/analysisWorkspace.css`：默认尺寸 Element Plus 控件，创建区唯一 Primary 为新建分析文档；移除为 danger plain，Portal handoff 为普通 secondary；操作收进统一间距、可换行的组。 |
| Portal 树挤压页面库 | `portal-management/pages/PortalManagementView.vue`、`portal-management.css`：左栏使用 viewport 高度约束和 42%/58% Grid 分配；树、页面列表各自滚动，页面库标题、搜索和分页保持在列表外。未改变右侧编排业务。 |

组件职责未改变，`PROJECT_FILE_MAP.md` 无需更新。既有压缩为单行的 BusinessRule/UnknownItem CSS 按本任务要求格式化；语义改动仅为静态摘要选择器。

## 聚焦验证

- Vitest：8 个相关文件，26 个测试通过。PortalHomeView、PortalPageView、PortalLayout、SystemContextRail、DatabaseObjectKnowledgeDrawer、portalManagementSurface 共 22 项；AnalysisWorkspaceView 与 KnowledgeDocumentDetailView 仅选择本次动作层级、handoff、Evidence/HC 的 4 项，其余 55 项有意不执行。未跑 full Vitest 或 backend tests。
- 新增 System 静态摘要可读但无交互语义的断言、Analysis 原生按钮层级及禁用状态断言；扩展已有 Evidence 用例验证记录头操作仍打开正确详情。
- `npm run type-check`、`npm run build`、修改文件 ESLint（最终无 warning）、修改文件 Prettier、`git diff --check` 均通过。构建保留既有大于 500 kB chunk 提示，未为本任务调整分包或依赖。
- 浏览器使用现有 `http://127.0.0.1:5200` Demo 与已有管理员会话，仅一个默认 Chrome 桌面大小，未调整 viewport 或 zoom。
- `/portal`：最近发布行从原先窄列表扩展至主内容区，日期在行末；`/portal/pages/5`：面包屑、标题、章节同起点，正文宽度受限，附件和 TrustSummary 更宽，无新增页面水平滚动条。
- `/systems/2`：DEMO_EAP 的 DEMO_MES/DEMO_OCS 摘要无箭头或按钮；`/database/2` 编辑抽屉：MES.EQUIPMENT 的 300 左对齐，仅查看后取消，没有保存。
- `/knowledge-documents/4`：普通 Evidence 与 HC 的“查看记录”均在记录头右侧；`/analysis/nodes/7`：创建、移动、禁用上移、移除与 handoff 层级正确。
- `/portal-management`：现有 10 节点树与 6 页页面库分别滚动；滚动树不改变页面库起点，滚动页面列表不移动搜索框；折叠至单个根节点后页面库高度仍保持不变。未为验证创建、删除或重排数据。

## Final Gate

| Gate | 结果 |
| --- | --- |
| UI-MANUAL-R01 | PASS / COMPLETE |
| PORTAL HOME WIDTH | PASS |
| PORTAL PAGE WIDTH | PASS |
| PORTAL READING WIDTH | PASS |
| CONTEXT RAIL AFFORDANCE | PASS |
| DATABASE ESTIMATED ROW ALIGNMENT | PASS |
| EVIDENCE RECORD ACTION ALIGNMENT | PASS |
| ANALYSIS BUTTON CONSISTENCY | PASS |
| PORTAL TREE HEIGHT | PASS |
| PORTAL PAGE LIBRARY SPACE | PASS |
| INDEPENDENT TREE/LIBRARY SCROLL | PASS |
| NO BACKEND CHANGE | PASS |
| NO API CHANGE | PASS |
| NO MIGRATION | PASS |
| DEMO DATA PRESERVED | PASS |
| FOCUSED FRONTEND | PASS |
| TYPE CHECK | PASS |
| BUILD | PASS |
| DEFAULT BROWSER | PASS |
| BROWSER ZOOM | NOT APPLICABLE |
| MULTI-VIEWPORT | NOT APPLICABLE |

## 数据、清理与交付

未执行 Demo init/seed/reset、数据库迁移或任何业务写操作。`C:\Users\wang\AppData\Local\SystemKnowledgeHub\demo` 的 DB、manifest、附件、keys/logs 保留；用户启动的 API/Vite 未停止。仓库 DB 未经 SQLite 打开，文件 SHA256 仍为 `F6C2DC43EB04110A830E86635920AC8DE1B52C60D9C19E9E178741AFABCCAC88`。

所有检查为一次性命令，均已退出；任务创建的浏览器标签已关闭。截图仅由浏览器工具临时捕获自查，未写入仓库、未提交 PNG，未遗留任务截图文件。没有新增未处理产品 gap。

交付分支 `main`，单一任务提交消息 `fix(ui): refine manual acceptance surfaces`；完整 SHA 和 `push origin main` 实际结果在最终交付消息记录。
