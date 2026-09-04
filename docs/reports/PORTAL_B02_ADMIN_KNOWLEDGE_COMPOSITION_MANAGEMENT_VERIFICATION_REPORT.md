# PORTAL-B02 Admin Knowledge Composition Management Verification Report

## Result

**PORTAL-B02 PASS**

PORTAL-B02 在 PORTAL-A01 冻结设计与 PORTAL-B01 已发布读取基础上，完成 Administrator-only 知识门户管理 API、Page Tree、PortalPage whole-page composition、五类 existing-target picker、同投影 Preview/readiness，以及 PortalPage/PortalPageNode 独立 Publish/Unpublish。没有实现 PORTAL-B03 阅读端，也没有提前实现 PORTAL-B04 Search、Attachment anonymous delivery、TrustSummary、RelatedKnowledge 或 Traceability Portal projection。

## Baseline and Scope Protection

- 开始基线：`main` / `b008f649d53ba03d39d9685e1fd169a62346590e`（`feat(portal): add composition persistence and anonymous read foundation`）。
- PORTAL-A01 保持 frozen，PORTAL-B01 历史 PASS 报告未改写。
- 已有无关工作区修改 `docs/reports/DBDISC_FINAL_R01_VERIFICATION_REPORT.md` 及其 `docs/DOCUMENT_INDEX.md` 索引行被保留，不纳入本任务提交。
- 未引入 Repository/UoW、MediatR、AutoMapper、generic PATCH/CRUD controller 或第二套 Portal truth store。

## Admin Portal API and Security

新增 `/api/admin/portal/**` typed API，覆盖 page inventory/create/detail/whole replacement/delete/preview/publish/unpublish、admin tree、node create/update/full-sibling reorder/delete/publish/unpublish，以及 target picker。所有 mutation 继续使用现有 antiforgery、当前 authenticated User、server UTC、opaque concurrency token 与标准 error contract。

Controller 使用 Administrator policy；Viewer/Editor 被拒绝，Administrator 成功。匿名 `/api/portal/tree` 与 `/api/portal/pages/{id}` 保持 `[AllowAnonymous]`、GET-only、published-only，未降低全局 Viewer/Admin authorization 或附件安全边界。

## Page and Section Management

- Page inventory 使用 server-side search/paging，支持 20/50/100，返回 safe Primary Target summary、publication/reference health、placement count、更新时间和 opaque token。
- Page create 默认 unpublished；Primary Target 只通过 controlled resolver 选择当前、未删除的 System、BusinessFunction、DatabaseObject、KnowledgeDocument 或 Integration。
- Page update 是包含 title、Primary Target、完整 sections 与 token 的原子 whole replacement；旧 section 缺失时仅移除 composition metadata。
- Published page 禁止改内容，明确返回 `409 invalid_state`；unpublish 后可修改。
- section ID 必须属于当前 page；cross-page stealing 返回 `422 reference_invalid`，不泄露另一 page 内容。
- 后端 authoritative 校验 heading、sort order、source/reference shape、safe ID、target existence、projection compatibility、最多 30 sections 与最多 5 个 KnowledgeDocumentBody。
- B02 UI 只开放 Summary、KnowledgeDocumentBody、StructuredOverview、DatabaseStructure；不开放 Derived 或 B04 projections。
- Page delete 只允许 unpublished、无 active placement；soft delete page 并清理自身 composition，不删除 Primary/Explicit target、KnowledgeRelation 或附件。

## Target Picker

Picker 仅支持五类 frozen target，并提供真实 server-side search/filter/paging。System、BusinessFunction、DatabaseObject、KnowledgeDocument、Integration 均返回业务可读名称、必要上下文和安全状态；KnowledgeDocument 以 `KnowledgeDocument + DocumentType/Lifecycle/KnowledgeStatus` 表达，允许管理阶段选择 Draft/Published/Archived current document，但 Draft/Archived 会成为 publish blocker。UI 不要求也不显示手工 target ID。

## Tree Management and Publication

- Admin tree 在 2,000-node bound 内显示 draft/published/broken health、safe page summary 与 opaque token。
- Folder/Page node shape、parent existence、cycle、depth 10、subtree resulting depth、page current state与 sibling order均由服务端校验。
- Unpublished node 支持 rename/move/page replacement；published node mutation 被阻止，必须先 unpublish。
- reorder 一次提交完整 sibling set 和每个 opaque token，在短 transaction 中验证同 parent、无遗漏/额外、全部 unpublished/still-current 后确定性重排；任一 stale token 返回 409 且无部分写入。
- Node publish 不自动发布 ancestor；ancestor 未发布时明确阻止。Page node 还要求 referenced page 已发布且 eligible。
- Folder unpublish 立即隐藏整棵 effective subtree，但不批量改写 descendants 自身 publish flag；重新发布 ancestor 后既有 child flag 恢复作用。
- Node delete 只允许 unpublished；非空 Folder 被阻止，Page placement 删除不删除 PortalPage 或 canonical knowledge。同一 PortalPage 可有多个 placements。

## Preview and Publication Readiness

Admin preview 允许读取 unpublished page，但复用匿名 Portal 的 controlled resolver、safe projection builder、section ordering、limits、lifecycle checks 与 sanitizer。返回同一 sanitized page projection及中文、业务可读、可操作的 publication blockers；不返回 stack、SQL、raw entity、raw target ID diagnostics、audit FK 或敏感 connection/discovery 字段。

发布 endpoint 每次重新 authoritative revalidate page、Primary Target、ExplicitReference、section handler/limits、target lifecycle 和 soft-deletion。Draft/Archived/deleted/broken/unsupported target或 projection 均阻止发布；不会自动发布 KnowledgeDocument、改变 KnowledgeStatus、修复关系或忽略坏 section。Page unpublish 后匿名 page 立即 404。

## No Knowledge Copy / No KnowledgeRelation Mutation

Portal tables 仍只保存组织、引用、投影类型与顺序。测试在 Portal reference 新增/移除前后核对 KnowledgeRelation count 不变，并核对 canonical records 不被修改。修改 canonical KnowledgeDocument/BusinessFunction/DatabaseObject 后重新 Preview 会读取当前值，PortalPage/Section 不保存正文或业务事实副本。

## Frontend UX

新增 Administrator-only `/portal-management` 和侧栏“知识门户管理”。工作台使用约 260–320px Page Tree + 可伸缩 composition editor，而不是普通 CRUD table或固定三栏。支持 Folder/Page 创建、Primary Target picker、page selection、多 placement 可见性、紧凑 section list、上移/下移一次保存、dirty guard、409 中文冲突、broken reference、发布检查、Page/Node 独立状态与确认式 unpublish。

Preview 使用带明显“预览”标记的大尺寸 Dialog、body-only vertical scroll、现有安全 Markdown/Mermaid renderer 和局部可横向滚动的 DatabaseStructure table。Target picker 与 host dialog 串行切换，未产生 overlay 重叠。

## Automated Verification

| Verification | Result |
| --- | --- |
| `AdminPortalApiTests` | PASS — 11/11 |
| 全部 Portal backend focused tests | PASS — 39/39 |
| Portal + AccessControl + Antiforgery + CurrentUser security regression | PASS — 35/35 |
| PORTAL-B02 frontend/API/navigation focused tests | PASS — 3 files / 10 tests |
| Release solution build | PASS — 0 warnings / 0 errors |
| `npm run type-check` | PASS |
| `npm run build` | PASS（仅现有 Vite chunk-size 提示） |
| `npm run lint` | PASS |
| affected Prettier check | PASS |
| affected `dotnet format --verify-no-changes` | PASS |
| NuGet direct + transitive vulnerability scan | PASS — 两个项目均无已知漏洞 |
| `dotnet ef migrations has-pending-model-changes` | PASS — no pending model changes |
| `git diff --check` | PASS |

补充尝试的 npm registry audit 未作为冻结 frontend gate；官方 npm audit endpoint 在沙箱内外均网络超时，因此未宣称该补充检查成功。PORTAL-B02 指定的 backend dependency vulnerability scan 已由 NuGet scan 完成。

## Browser Smoke

真实浏览器使用 task-owned Verification backend、SQLite、Data Protection keys、attachment/log roots与隔离端口 5092/4174。fixture 包含 Administrator、MES、BusinessFunction `Lot Track In`、Published KnowledgeDocument `Lot Track In 业务说明` 与 DatabaseObject `MES.LOT`。

完成真实 Admin flow：登录 → 知识门户管理 → 创建 `MES / 生产管理 / Lot Track In` → BusinessFunction Primary Target → 添加 StructuredOverview、Explicit KnowledgeDocumentBody、Explicit DatabaseStructure → whole-page 保存 → Preview → 依次发布 page、root folder、child folder、page node。Preview 显示 `Lot Track In` 业务信息、安全 Markdown/Mermaid 正文和 `MES.LOT` 字段结构/EstimatedRows 48,000。随后匿名 tree/page 均为 200；取消发布 PageNode 后 tree 立即移除该 page，page URL 返回 404。

浏览器首次集成检查发现并修复 Element Plus `ElTree`/`ElLoading` 注册遗漏及 Preview 默认 margin 导致的 viewport overflow；修复后重新加载和完整 flow 未产生新 console warning/error。

## Responsive Evidence

| Requested viewport | Browser inner viewport | Evidence |
| --- | --- | --- |
| 1366×768 | 1313×738 | document 无水平 overflow；tree 302px、editor 751px；Preview top 37 / bottom 697，body 独立滚动 |
| 1440×900 | 1384×865 | document 无水平 overflow；tree 318px、editor 806px；Preview top 43 / bottom 818 |
| 1920×1080 | 1846×1038 | document 无水平 overflow；tree 320px、editor 1269px；Preview top 52 / bottom 982 |

三种宽度下 target picker、section list、右对齐 pagination、Dialog overlay与阅读型 Preview均可用；Preview 宽度上限 1160px，1366 下没有增加强制第三栏。

## Migration and Data Protection

PORTAL-B02 复用 B01 的 `portal_pages`、`portal_page_nodes`、`portal_page_sections`，没有新 migration。EF design-time 检查使用 task-owned absolute SQLite path并报告 no pending model changes。

验证前后仅通过 filesystem metadata/hash核对 repository-owned SQLite，未用 SQLite/EF 打开：

| File | Before | After |
| --- | --- | --- |
| `system-knowledge-hub.db` | 1,257,472 bytes; `2026-09-03T12:18:00.8468494Z`; SHA-256 `3671141339C979BE2DCA6853811D18E2BD2CD607D99CE231C3BE29B2EB77F271` | 完全一致 |
| `system-knowledge-hub.db-wal` | Missing | Missing |
| `system-knowledge-hub.db-shm` | Missing | Missing |

**REPOSITORY DATA PROTECTION: PASS**

## Cleanup

仅停止本任务创建的 API/Vite进程，isolated ports 5092/4174 无 listener。task-owned runtime SQLite、keys、attachments、logs、temporary seeder/harness、design-time DB与其目录均在校验精确路径后删除；未关闭用户浏览器、Codex或其它开发服务器。

## Deferred Frozen Work

- PORTAL-B03：PortalLayout、`/portal` frontend、首页、阅读树和正式匿名阅读体验。
- PORTAL-B04：Portal Search backend/UI、anonymous attachment delivery、TrustSummary、RelatedKnowledge、Traceability Portal projection。

以上为冻结后续阶段，不是 B02 未披露缺口。本任务完成后不自动开始 B03/B04。

## Final Status

| Gate | Status |
| --- | --- |
| PORTAL-B02 | PASS |
| ADMIN PORTAL API | PASS |
| ADMIN AUTHORIZATION | PASS |
| ANTIFORGERY | PASS |
| PAGE MANAGEMENT | PASS |
| PRIMARY TARGET PICKER | PASS |
| COMPOSITE PAGE | PASS |
| SECTION MANAGEMENT | PASS |
| SECTION ORDERING | PASS |
| TREE MANAGEMENT | PASS |
| TREE MOVE / REORDER | PASS |
| TREE PUBLICATION | PASS |
| PREVIEW | PASS |
| PUBLICATION READINESS | PASS |
| PAGE PUBLISH / UNPUBLISH | PASS |
| NODE PUBLISH / UNPUBLISH | PASS |
| NO KNOWLEDGE COPY | PASS |
| NO KNOWLEDGE RELATION MUTATION | PASS |
| CONCURRENCY | PASS |
| BROKEN REFERENCE SAFETY | PASS |
| FRONTEND UX | PASS |
| RESPONSIVE | PASS |
| B01 REGRESSION | PASS |
| BACKEND REGRESSION | PASS |
| FRONTEND REGRESSION | PASS |
| MIGRATION CHAIN | PASS |
| NO NEW MIGRATION | PASS |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |

**PORTAL-B02 COMPLETE**

**PORTAL-B03 READY: YES**
