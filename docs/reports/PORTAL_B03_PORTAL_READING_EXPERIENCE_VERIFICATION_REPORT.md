# PORTAL-B03 Internal Knowledge Portal Reading Experience Verification Report

## Result

**PORTAL-B03 PASS**

PORTAL-B03 在 PORTAL-A01 frozen decision、PORTAL-B01 anonymous read foundation 与 PORTAL-B02 Administrator composition management 上，实现同一 Vue/API 部署中的独立匿名知识阅读端。`/portal`、`/portal/pages/{safeId}` 与 Portal-specific catch-all 均不经过 Current User bootstrap、SecurityGate、ForcedPasswordChangeGate 或 Admin shell；Admin routes 继续保持原登录和授权流程。

本轮没有实现 PORTAL-B04 Search、anonymous attachment delivery、AttachmentList、TrustSummary、RelatedKnowledge 或 Traceability projection，也没有创建第二 SPA/API/DbContext/数据库或新 migration。

## Baseline and Authority

- 开始基线：`main` / `d2e76b49`（`feat(portal): add admin knowledge composition management`），与 `origin/main` 一致。
- 已确认：PORTAL-A01 FROZEN、PORTAL-B01 PASS、PORTAL-B02 PASS、PORTAL-B03 READY: YES。
- PORTAL-A01、B01、B02 历史报告保持原状。
- 已有无关工作区修改 `docs/reports/DBDISC_FINAL_R01_VERIFICATION_REPORT.md` 及其 `docs/DOCUMENT_INDEX.md` 索引行被保留，不纳入本任务提交。

## Anonymous Route and Client Boundary

- `App.vue` 在任何 Admin security gate 之前按 route meta 选择 `PortalLayout`。
- Router guard 对 Portal family 立即放行；只有 Admin route 才初始化 actor/current-user 并执行 Administrator gate。
- 应用启动不再无条件请求 Current User；Admin 首次导航与 Portal → Admin 导航仍按需初始化。
- Portal 专用 client 只有 `getHome`、`getTree`、`getPage` 三个 GET 操作，使用 `credentials: omit`，不接 antiforgery 或 Admin global 401 handler。
- Portal frontend 不调用 systems、database objects、knowledge documents、attachments、users、database discovery 等 Admin/canonical endpoints；意外 401 被显示为可重试读取失败，不跳转登录。
- strict client 和 route-guard tests 对 method、URL、credentials、Current User 调用次数与 Admin regression 做了可重复断言；真实浏览器从未登录即成功加载 Portal。

## Portal Home API

新增匿名 `GET /api/portal/home`：

- 返回 Portal 名称、effective visible 顶层 tree categories，以及最多 8 个最近发布且当前仍可匿名读取的页面。
- recent 使用 `PublishedAt DESC, Id DESC`；每次读取均重新应用 page/node/ancestor publication、Primary Target、ExplicitReference、KnowledgeDocument lifecycle 与 soft-delete eligibility。
- breadcrumb 使用 backend canonical placement；未返回 User、Admin metrics、Draft/broken management data、Discovery state 或敏感字段。

测试覆盖 anonymous 200、top-level only、8 条上限/排序，以及 unpublished page/node/ancestor、broken Primary Target、Draft/Archived document、deleted explicit target 的排除。

## Portal Layout and Reading UX

- 独立 `PortalLayout` 只包含轻量“系统知识中心”header、Portal tree navigation 与一个 `main` reading landmark，不渲染 AppShell、Admin sidebar/header、用户信息、登录或管理入口。
- 桌面目录为 268px，可折叠至 52px；同一 Portal session 内组件状态保留，当前 canonical path 自动展开，active Page 有明确状态。
- 窄屏切换为带 accessible name 的“目录”overlay，Escape 可关闭，关闭后正文恢复全宽。
- Home 展示已发布顶层目录与最多 8 个最近页面；空、loading、retryable error 均有独立阅读型状态，不出现新增或后台引导。
- Page 使用轻量 skeleton、backend canonical breadcrumb、页面标题、Primary Target 中文类型和有留白的 section 流；404 使用统一、不泄露状态的 frozen 文案。

## Closed Section Rendering

`PortalSectionRenderer` 只接受当前闭合集：

- `Summary`：目标类型、标题和安全摘要。
- `KnowledgeDocumentBody`：复用现有 `KnowledgeDocumentMarkdown`，继续提供 heading、table、syntax-highlighted code/copy、strict Mermaid 与 safe link/XSS 边界。
- `StructuredOverview`：System、BusinessFunction、DatabaseObject、Integration 使用 reading definition list，不模拟 Admin form。
- `DatabaseStructure`：展示 `Schema.Object`、对象类型、业务说明、数据库注释、EstimatedRows、访问方式、业务唯一键与紧凑字段表；不暴露 connection、identity、snapshot/diff/sync/discovery 数据。

未知 discriminator 显示“该内容暂不可显示”，仅记录 section ID/projection kind，不显示 raw JSON 或内部异常。普通正文有阅读宽度上限；page 最大 1160px，数据库表、Markdown table/code 与 Mermaid 使用局部 overflow，不制造 document-level horizontal overflow。

## Automated Verification

| Verification | Result |
| --- | --- |
| 全部 Portal backend focused tests（B01/B02/B03） | PASS — 48/48 |
| Portal Home eligibility/sanitization tests | PASS — 包含在 48/48 |
| PORTAL-B03 + Router/SecurityGate + Markdown/Mermaid frontend affected tests | PASS — 14 files / 119 tests |
| Page loading focused recheck | PASS — 1 file / 4 tests |
| Release solution build | PASS — 0 warnings / 0 errors |
| `npm run type-check` | PASS |
| `npm run build` | PASS（仅既有 Vite chunk-size 提示） |
| `npm run lint` | PASS |
| affected Prettier check | PASS |
| affected `dotnet format --verify-no-changes` | PASS |
| NuGet direct + transitive vulnerability scan | PASS — 两个项目均无已知漏洞 |
| `dotnet ef migrations has-pending-model-changes` | PASS — no pending model changes |
| `git diff --check` | PASS |

## Browser Smoke

真实 Chrome 使用 task-owned Verification backend、SQLite、Data Protection keys、attachment/log roots 与隔离端口 5093/4175；未先登录。fixture 包含 MES、BusinessFunction“批次进站过账”、Published KnowledgeDocument、DatabaseObject `MES.LOT_TRACK_IN`，以及已发布 `MES → 生产过账 → 批次进站` tree/page。文档覆盖 heading、table、SQL code block 与 Mermaid，page sections 覆盖 StructuredOverview、KnowledgeDocumentBody、DatabaseStructure。

浏览器确认：

- `/portal` 直接显示“系统知识中心”、顶层目录与最近发布，不显示登录、Current User、Admin navigation 或管理操作。
- Tree folder 展开、当前 Page、canonical breadcrumb、页面标题、业务概览、Markdown table/code、Mermaid 与 `MES.LOT_TRACK_IN` 字段表均正确显示。
- 页面只有一个 `main`；目录使用 semantic nav/list，桌面 collapse 与窄屏 overlay/Escape 均正常。
- 不存在的 page 与 Portal catch-all 均显示“页面未找到 / 该知识可能尚未发布、已取消发布，或地址不正确。”，未出现 403、登录或 Admin UI。
- 将 PageNode 取消发布后，Tree 不再显示 leaf，直接 Page URL 立即显示 Portal 404；重新发布后恢复可读。
- `/dashboard` 匿名直达仍进入现有登录表单；Administrator-only Portal Management 继续由既有 frontend/backend authorization regression 覆盖。
- 浏览器 console warning/error 为 0。

## Responsive and Accessibility Evidence

| Viewport | Evidence |
| --- | --- |
| 1366×768 | `documentWidth = 1366`，无整页水平 overflow；sidebar 268px，content 1018.7px；tree、Markdown table/code、Mermaid 与数据库表可读。 |
| 1440×900 | `documentWidth = 1440`；sidebar 268px，content 1092.7px，无整页水平 overflow。 |
| 1920×1080 | `documentWidth = 1920`；content 上限 1160px，数据库表 1100px，普通正文没有无限拉宽。 |
| 200% 等效检查（1366/2 = 683 CSS px） | desktop sidebar 隐藏并出现“目录”按钮；`documentWidth = 683`；数据库表 client 631px / scroll 720px，仅表容器局部滚动，核心阅读保持可用。 |

窄屏 overlay 有 accessible open/close name，Escape 后 overlay 从 accessibility tree 移除；breadcrumb 是 semantic nav，section heading hierarchy 与 focus-visible 样式存在。三种桌面宽度均无 document-level horizontal overflow。

## Migration, Data Protection, and Cleanup

PORTAL-B03 未修改 Portal schema、未增加 migration。EF design-time 使用 task-owned absolute SQLite path并报告 `No changes have been made to the model since the last migration.`

验证前后只通过 filesystem metadata/hash 核对 repository-owned SQLite，未用 SQLite/EF 打开：

| File | Before | After |
| --- | --- | --- |
| `system-knowledge-hub.db` | 1,257,472 bytes; `2026-09-03T12:18:00.8468494Z`; SHA-256 `3671141339C979BE2DCA6853811D18E2BD2CD607D99CE231C3BE29B2EB77F271` | 完全一致 |
| `system-knowledge-hub.db-wal` | Missing | Missing |
| `system-knowledge-hub.db-shm` | Missing | Missing |

仅停止本任务 API/Vite/mock/temporary seeder 进程，释放 5093/4175；task-owned runtime DB、WAL/SHM、keys、attachments、logs、design-time DB、temporary fixture与 layout mock 均在校验精确路径后删除。没有关闭用户浏览器、Codex 或无关进程。

## Deferred Frozen Work

PORTAL-B04 的 Search backend/UI、anonymous attachment delivery、AttachmentList、TrustSummary、RelatedKnowledge 与 Traceability projection 仍保持冻结后续范围，不是 B03 缺口。本任务完成后不自动开始 B04 或 PORTAL-VERIFY。

## Final Status

| Gate | Status |
| --- | --- |
| PORTAL-B03 | PASS |
| PORTAL LAYOUT | PASS |
| ANONYMOUS ROUTE BOOTSTRAP | PASS |
| NO CURRENT USER DEPENDENCY | PASS |
| NO LOGIN UI | PASS |
| NO ADMIN NAVIGATION | PASS |
| PORTAL HOME | PASS |
| PORTAL TREE | PASS |
| TREE NAVIGATION | PASS |
| PORTAL PAGE | PASS |
| COMPOSITE PAGE RENDERING | PASS |
| SUMMARY SECTION | PASS |
| KNOWLEDGE DOCUMENT BODY | PASS |
| STRUCTURED OVERVIEW | PASS |
| DATABASE STRUCTURE | PASS |
| MARKDOWN SAFETY | PASS |
| MERMAID | PASS |
| RESPONSIVE | PASS |
| ACCESSIBILITY | PASS |
| NO DOCUMENT HORIZONTAL OVERFLOW | PASS |
| ADMIN SECURITY REGRESSION | PASS |
| B01 REGRESSION | PASS |
| B02 REGRESSION | PASS |
| NO NEW MIGRATION | PASS |
| BACKEND REGRESSION | PASS |
| FRONTEND REGRESSION | PASS |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |

**PORTAL-B03 COMPLETE**

**PORTAL-B04 READY: YES**
