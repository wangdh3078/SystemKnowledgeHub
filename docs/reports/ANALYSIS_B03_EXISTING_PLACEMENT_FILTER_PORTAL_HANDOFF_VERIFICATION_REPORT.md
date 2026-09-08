# ANALYSIS-B03 — Existing Placement / Tree Filter / Portal Handoff Verification

2026-09-08。**ANALYSIS-B03 COMPLETE / PASS。ANALYSIS-VERIFY READY YES。**

基线：`main` / `d8bb8c2f67b1b2e8ab368e81d69582dd911d5a9c`。开始时工作区干净，检查了 status、diff、最近八次提交。按用户当前 B03 任务、根 AGENTS、[A01 冻结决策](../design/ANALYSIS_A01_ANALYSIS_WORKSPACE_ARCHITECTURE_DECISION.md)及其 decision report、B01/B02 verification reports、Portal A01/B02/B04/final 和当前 canonical API 实现执行。没有修改 frozen source。

## 实现与边界

- Editor/Administrator 在现有组织 DialogHost 中“加入已有文档”；Viewer 无此入口。候选复用 `/api/knowledge-documents` server paging、query、documentType、lifecycleStatus；七种类型使用既有中文标签，同时显示生命周期与 KnowledgeStatus。默认不传 lifecycleStatus，沿用 Draft/Published；显式选择“已归档”才请求 Archived。
- soft-deleted 排除沿用 `KnowledgeDocumentConfiguration.HasQueryFilter(!IsDeleted)` 和原 `KnowledgeDocumentQueries.GetList`；没有 IgnoreQueryFilters、新候选 endpoint 或本地正文搜索。前端仅呈现当前列表响应。此边界采用现有后端实现静态核查，没有重复运行 B01 后端测试。
- 完整 Analysis tree 的 knowledgeDocumentId 映射识别“已在分析目录中”，禁止再次加入并可定位现有 node。目标目录默认遵循 Folder 本身、Document 的 parent、无选择时 root，可在完整 Folder 列表中调整，显示可读路径。
- 加入仅 POST 现有 `/api/analysis/document-placements`，提交 parentId、knowledgeDocumentId 和当前 treeConcurrencyToken；采用 mutation items/token 并选择新 placement，阅读 canonical 文档，不自动编辑或额外 GET tree。409 刷新且不重放，422 显示业务消息，保留选择表单。唯一性、当前文档、容量、父级及并发始终由 B01 校验。
- 左侧标题筛选是 trim、大小写不敏感的字面子串；`%/_` 均为普通字符。只保留命中项及祖先，Folder 命中不隐式保留全部后代。不可用节点仅用“文档不可用”。空白恢复全树，无结果显示明确空态；筛选不改变 route、selection、documentId 或 dirty buffer。排序按钮与 reorder 函数同时阻止筛选状态提交，提示“清除筛选后可调整顺序”；其它组织动作继续使用完整快照。
- “在知识门户管理中使用”仅向 Administrator 的 Available / Published 文档显示；Draft 提示先发布，Archived/Unavailable/Editor/Viewer 无入口。调用 router.push，canonical documentId 而非 Analysis nodeId 进入 query；共享 requestLeave 处理 dirty 取消/确认。
- Portal 仅首次 mount 采用 `targetType=KnowledgeDocument` 和安全正整数 targetId。先从既有 current document API 读取 canonical 文档，再按 canonical 标题（最多 200 字符）查询既有 Administrator Portal targets 分页，逐页核对 type/id/Published；没有假设首屏包含目标，也不采用 query title/lifecycle。不存在、不可读取、Draft/Archived、已删除或 Portal 目标不匹配均显示“无法使用该知识文档作为门户目标”。
- 合法 handoff 只打开原有未保存“新建 Portal 页面”表单并预选 PrimaryTarget。query 后续变化不重新接管页面；异步返回不会覆盖已有 composition 或用户已打开的创建表单。无 query/其它 targetType 保持原管理入口。后续创建、编排、保存和发布仍需要用户显式操作。

没有 backend product change、entity/schema/migration、新 API、Portal 数据写入或 Analysis/Portal 树同步。没有修改共享文档面板，也没有重新实现正文、附件、Revision、关系、证据或状态。

## Focused 验证

共 **52 项 focused 前端测试通过**：

| 文件 / 范围 | 结果 | 直接风险 |
| --- | --- | --- |
| analysisWorkspaceApi.spec.ts | 3 PASS | 既有严格组织 DTO/API 边界 |
| AnalysisWorkspaceView.spec.ts | 28 PASS | B02 create、dirty switch、move/order、unavailable、共享 panel 回归；B03 root/Folder/Document context、加入阅读、去重定位、409/422、标题/祖先/字面字符/不可用筛选、dirty buffer 与 reorder guard、角色及 dirty handoff |
| ExistingDocumentPicker.spec.ts | 2 PASS | 七类型、默认 Draft/Published、显式 Archived、server search/paging、完整树去重及定位 |
| PortalManagementHandoff.spec.ts | 19 PASS | 合法预选、safe ID、错误类型、current/Portal lifecycle 重验、缺失/不可读目标、分页定位、原入口、首次接管、异步不覆盖、所有场景 NO POST/PUT/DELETE |

运行 `npx vitest run src/features/analysis-workspace src/features/portal-management/pages/PortalManagementHandoff.spec.ts`，首轮仅两个新测试的原入口空态文案断言不准确，修正后 50 项通过。追加分页解析与异步保护两项后，仅重跑 Portal handoff 文件，19 项通过；总计 52 个不同测试。没有 full Vitest、完整 Portal suite 或 backend suite。

`npm run type-check`、`npm run build`、仅修改 TS/Vue 文件的 ESLint 和仅修改源码的 Prettier 检查通过。build 保留已有 >500 kB chunk 提示，不作为本任务新增缺陷。新增测试中的错误 ApiError fixture 字段已在类型检查阶段修正。最后候选默认范围提示文案调整后 build 再次通过。

## 一个默认桌面浏览器验收

环境：Development，经 `dotnet run --no-restore --no-launch-profile --project src/SystemKnowledgeHub.Api` 启动；API 5197，Vite 5198。SQLite、Data Protection、Attachment StorageRoot、日志及临时脚本均位于任务拥有的 `%TEMP%/analysis-b03-20260908`。使用本任务临时 Administrator，密码未写入文件或版本控制。

少量 fixture 为一个 Folder、Draft DesignNote、Published KnowledgeArticle、Archived Sop、另一个未放置 Requirement。Sop 按原有 Draft → Published → Archived 转换，直接 Draft → Archived 被既有后端正确拒绝后按合法流程完成。除已有迁移自带基线外，没有创建 Demo Dataset。

浏览器实际通过：

1. 登录并进入 `/analysis`，候选默认仅出现未归档文档；搜索 Published Lot，加入 MES 验收目录，自动选中 node 3 并以阅读模式展示 canonical document 2。
2. 再开候选，该文档显示“已在分析目录中”及“定位”；显式选择“已归档”仅出现 Archived Sop。
3. 输入带首尾空格、小写的 `published lot`，左侧只显示命中文档与 MES 祖先，排序禁用；清除后完整目录恢复，右侧保持当前文档。
4. 点击 handoff，URL 为 `/portal-management?targetType=KnowledgeDocument&targetId=2`，原新建页面表单预选当前知识文章，页面库与 Portal 节点数均为 0；取消表单并返回 Analysis，没有创建/保存/发布。
5. Draft 节点显示“请先发布知识文档。”且无 handoff。编辑 Published 文档 Markdown 为临时未保存内容后跳转；选择“继续编辑”保持 `/analysis/nodes/3` 和原 buffer，再次跳转并确认“放弃修改”才进入相同 canonical query 与未保存预选。再次取消并返回工作区。
6. 查看默认桌面截图确认筛选工作区和预选 Dialog 可用。浏览器 error/warn 记录为空。没有 viewport matrix、响应式、移动端或 zoom 验证。

任务拥有的 Vite proxy 记录请求 **方法 + URL**，不记录密码、cookie、header 或正文。整个 runtime 的 `/api/admin/portal/**` 只有六条 GET（两次 tree、pages、targets），**POST/PUT/PATCH/DELETE = 0**。handoff 解析使用既有 `/api/knowledge-documents/2` 与 `/api/admin/portal/targets?...`，没有新 handoff API。placement POST 总计两次：一次 fixture 草稿 placement、一次浏览器加入 Published 文档。临时网络日志完成核查后随验收目录清理。

## 数据保护、清理与交付

真实库仅做文件元数据/SHA-256 检查，未用 SQLite、EF、迁移、复制或 checkpoint 打开真实数据库。

| 项目 | 验证前后 |
| --- | --- |
| 路径 | `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` |
| 字节数 | 1372160，相同 |
| UTC mtime | `2026-09-07T15:51:07.1311811Z`，相同 |
| SHA-256 | `F6C2DC43EB04110A830E86635920AC8DE1B52C60D9C19E9E178741AFABCCAC88`，相同 |
| WAL / SHM | 前后均不存在 |

本任务标签页关闭；现场核验临时 run-api/run-web 脚本的进程身份及父子链后仅停止自有进程；5197/5198 已释放。核对绝对路径后删除 task-owned 临时库、keys、attachments、日志及脚本目录，没有留下服务、watch、运行数据或凭据。

本报告、DOCUMENT_INDEX 与 PROJECT_FILE_MAP 同任务同步。没有新 contract gap 或未关闭的 B03 gap。SEC-04 的 Production rollout blocker 和 PHASE-TRACE 的独立真实领域验收仍按既有报告保持，不由本任务关闭。

## Final Gate

| Gate | Result |
| --- | --- |
| ANALYSIS-B03 | PASS / COMPLETE |
| EXISTING DOCUMENT PICKER | PASS |
| SEVEN DOCUMENT TYPES | PASS |
| ARCHIVED EXPLICIT SELECTION | PASS |
| EXISTING PLACEMENT DETECTION | PASS |
| PLACEMENT CREATE | PASS |
| TREE TITLE FILTER | PASS |
| ANCESTOR PRESERVATION | PASS |
| UNAVAILABLE FILTER SAFETY | PASS |
| FILTER REORDER GUARD | PASS |
| PORTAL HANDOFF VISIBILITY | PASS |
| PUBLISHED-ONLY HANDOFF | PASS |
| DIRTY HANDOFF GUARD | PASS |
| PORTAL TARGET PRESELECTION | PASS |
| PORTAL TARGET REVALIDATION | PASS |
| NO IMPLICIT PORTAL WRITE | PASS |
| NORMAL PORTAL MANAGEMENT REGRESSION | PASS |
| NO BACKEND PRODUCT CHANGE | PASS |
| NO MIGRATION | PASS |
| FOCUSED FRONTEND | PASS |
| TYPE CHECK | PASS |
| BUILD | PASS |
| DEFAULT BROWSER E2E | PASS |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |
| BROWSER ZOOM | NOT APPLICABLE |
| MULTI-VIEWPORT | NOT APPLICABLE |
| ANALYSIS-VERIFY READY | YES |

技术验证与 Git delivery 分开记录。本任务在 main 上形成一个 `feat(analysis): complete workspace organization flow` 提交并尝试推送 origin main，完整 SHA 与推送结果以最终回报为准。到 B03 为止，不自动开始 Demo Dataset 或 ANALYSIS-VERIFY。
