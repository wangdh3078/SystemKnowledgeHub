# PORTAL-B04 Search + Attachment + Trust + Related Knowledge + Traceability Integration Verification Report

## Result

**PORTAL-B04 PASS**

PORTAL-B04 在已完成的 B01 composition/published-only read、B02 Administrator composition 和 B03 anonymous reading shell 上，补齐匿名分页搜索、page-scoped 附件读取，以及 `AttachmentList`、`TrustSummary`、`RelatedKnowledge`、`Traceability` 四种安全阅读投影。Admin Preview 复用同一后端投影和前端 renderer；实现只读取 canonical knowledge、attachment、evidence 与 relation，不复制事实、不创建第二套 relation/trace truth。

## Baseline and Authority

- 开始基线：`main` / `f4fbdbff681b3518b78d1694ef6f069e911dca78`，与 `origin/main` 一致。
- 已读取并遵守 PORTAL-A01 frozen decision、PORTAL-A01-AMEND-01、PORTAL-B01/B02/B03 报告、TRACE-A01、Attachment/Evidence/Relationship 既有合同与 `AGENTS.md`。
- TrustSummary 遵循 amendment：只允许 `PrimaryTarget` 或 `ExplicitReference`，每个 section 恰好解析一个 target；`Derived` 由 validator、Admin picker/readiness 与匿名 read 共同 fail closed，未实现多目标聚合。
- 历史 A01/B01/B02/B03 报告未改写。已有无关 DBDISC-R01 报告及其索引行被保留且不纳入本任务提交。

## Anonymous Search

新增 `GET /api/portal/search?q=&page=1&pageSize=20`：

- `q` 去除首尾空白后必须为 1～100 字符；`page >= 1`；`pageSize` 为 1～100，错误返回 `400 validation_error` 与对应 field errors。
- 搜索候选严格来自当前 effective published/readable Page Tree context；未发布 Page/Node/ancestor、Draft/Archived document、soft-deleted/broken target 不进入结果。
- 可命中 Portal Page title、Primary Target title、ExplicitReference title，以及当前 eligible KnowledgeDocument 的安全纯文本标题/摘要/正文。
- 服务端按明确 rank、title、Page ID 稳定排序后分页；response 只含 Page ID、title、Primary Target 安全身份、canonical breadcrumb 与安全 snippet。
- Vue 新增 `/portal/search`、header 搜索入口、URL 驱动的 q/page/pageSize、统一 `SkhPagination`、loading/empty/error states；anonymous GET client 继续使用 `credentials: omit`。

## Attachment Boundary

- `AttachmentList` 仅支持单一 Published KnowledgeDocument target，只返回 current revision reference 中 `Ready` 附件的 safe metadata。
- 新增匿名 page-scoped GET：`/api/portal/pages/{pageId}/attachments/{attachmentId}/content|download|preview`。
- 每次交付都重新验证 Page/Node/ancestor publication、target eligibility、current revision reference、attachment ownership 与 storage state；仅凭 attachment ID、错误 Page、历史 revision、未发布/归档文档均不能读取。
- 实际 stream、integrity、range/preview policy 复用现有 AttachmentService；响应不暴露 StorageKey、hash、absolute path、document/revision internals、token 或 Admin URL。
- Markdown 图片只在后端给出的 current image attachment ID allowlist 中转换为 page-scoped `/content` URL；任意外链/未知 attachment ID 继续由既有 Markdown safety boundary 拒绝。

## Trust, Related Knowledge, and Traceability

### TrustSummary

- 对 section 唯一 target 返回当前 `KnowledgeStatus`、Evidence 数、HumanConfirmation 数。
- KnowledgeDocument 额外返回 current revision confirmation coverage；System/BusinessFunction/DatabaseObject/Integration 的 coverage 为 `null`。
- 不聚合 Primary Target 与 ExplicitReference，也不读取 Portal composition 作为信任事实。

### RelatedKnowledge

- 仅从 canonical `KnowledgeRelation` 派生；按 relation type + direction 分组并使用冻结中文标签。
- Related target 必须仍满足五类 current eligibility；输出 target/relation 各自最小 trust/evidence 摘要。
- 只有 related target 同时拥有当前可匿名读取的 Portal page 时才返回 Portal page link；不暴露 Admin/canonical detail route。
- 每组有 bounded result limit，composition save/preview 不创建或修改任何 KnowledgeRelation。

### Traceability

- 仅允许 `Derived + Primary KnowledgeDocument`，且 root type 必须为 Requirement/Specification/TestCase。
- 只读取 Published KnowledgeDocument 与 canonical `SpecifiedBy` / `VerifiedBy` relation；输出后端已解释的 direct requirement→test、requirement→specification→test 及 root-specific upstream paths。
- 返回 node/relation trust、missing-link codes、cycle/truncation 标志，以及固定 depth 2、200 nodes、300 edges 限制；前端只消费 safe DTO，不自行重建 generic graph 语义。
- request-scoped cache 使同页重复同类 B04 section 的 query 数不随 section 数增长。

## Admin Composition and Preview

- Admin section selector 现支持全部八类 projection，并明确执行 source compatibility：AttachmentList 仅文档 target；TrustSummary 禁止 Derived；RelatedKnowledge 仅 Derived；Traceability 仅 Derived 且 Primary Target 为 Requirement/Specification/TestCase document。
- save/readiness/preview 使用后端同一 validator、eligible target resolver 与 B04 projector；非法组合返回 `400 validation_error` 或阻塞发布，不靠前端猜测。
- Preview 复用 `PortalSectionRenderer`，与匿名 Page 使用同一 safe content DTO；preview mode 不生成匿名可点击附件/Portal links。
- 既有 whole-page PUT、opaque concurrency token、stale conflict、Page/Node 独立 publish/unpublish 与 antiforgery/Administrator policy 保持不变。

## Automated Verification

| Verification | Result |
| --- | --- |
| 全部 Portal backend focused tests（B01～B04） | PASS — 67/67（Release；用户运行中的 Debug API 未被终止） |
| AccessControl + Attachment foundation + Traceability security regression | PASS — 23/23 |
| Portal/management/Markdown frontend affected tests | PASS — 12 files / 114 tests |
| Release solution build | PASS — 0 warnings / 0 errors |
| `npm run type-check` | PASS |
| `npm run build` | PASS（仅既有 Vite chunk-size 提示） |
| `npm run lint` | PASS |
| affected Prettier check | PASS |
| affected `dotnet format --verify-no-changes` | PASS |
| NuGet direct + transitive vulnerability scan | PASS — 两个项目均无已知漏洞 |
| `dotnet ef migrations has-pending-model-changes` | PASS — no pending model changes；task-owned path 未创建数据库 |
| `git diff --check` | PASS |

自动化覆盖 Search title/primary/explicit/body 命中、分页/validation、100/101 字符边界、unpublished fail closed；附件 current-reference/page-scope/content/preview/download/structured-preview sanitization；四投影 DTO/renderer；TrustSummary source compatibility/no aggregation/coverage；RelatedKnowledge portal-link safety；Trace direct/two-hop/missing/cycle/limits；Admin save/preview；B01/B02/B03 回归及 canonical knowledge/relation 不变式。

## Browser Verification

真实浏览器使用 task-owned Development runtime，隔离端口 `5094/4176`，SQLite、Data Protection keys、attachment/log roots 全部位于专属临时目录。fixture 包含 MES / Lot Track In、Published Requirement/Specification/TestCase/SOP documents、`MES.LOT`、current image/PDF attachments、SpecifiedBy/VerifiedBy/AppliesTo relations、HumanConfirmation evidence，以及已发布三页 Portal tree。

浏览器确认：

- 匿名 `/portal` 与完整知识页无需登录；header 搜索 `Lot Track` 返回 3 条按统一分页展示的已发布结果。
- KnowledgeDocument body 的 1×1 PNG 经 page-scoped content URL 成功加载；AttachmentList 同时显示 PNG/PDF 预览与下载链接；PDF preview 由浏览器内置 viewer 成功打开。
- 页面显示 `MES.LOT` 结构、单目标 TrustSummary、按关系中文分组的 RelatedKnowledge，以及 direct requirement→test 和 requirement→specification→test 两条 Traceability path。
- Related/Trace 链接只跳转已发布 `/portal/pages/{id}`；无 Admin 或 canonical detail URL。
- Administrator 登录后可见八类 section 的既有 composition，Preview 对附件、信任、相关知识与追溯的文本和顺序与匿名阅读投影一致。
- 取消发布主页面 Node 后，匿名 tree 立即移除该页，直接 URL 显示统一“页面未找到”；恢复节点后重新可见。
- 在第二个隔离 fixture 中将显式 KnowledgeDocument 从 Published 改为 Archived 后，匿名目录移除页面、搜索从 1 条变为冻结空态、Page 变为统一 404，page-scoped attachment preview 返回 sanitized 404，未泄漏 snippet、附件 metadata 或文档标题。
- 浏览器 console warning/error 为 0。

## Migration, Data Protection, and Cleanup

PORTAL-B04 只扩展现有 enum/API/projection 与 UI，没有实体字段或 schema 变化，没有新 migration。EF design-time 使用绝对 task-owned SQLite path，报告 `No changes have been made to the model since the last migration.`，且未创建数据库文件。

验证期间仅通过 filesystem metadata/hash 核对 repository-owned SQLite，未用 SQLite/EF 打开。任务开始时记录的主库为 1,257,472 bytes、`2026-09-03T12:18:00.8468494Z`、SHA-256 `3671141339C979BE2DCA6853811D18E2BD2CD607D99CE231C3BE29B2EB77F271`；外部 5090 runtime 停止后主库状态发生变化。用户于 2026-09-04 明确指示“数据库按照现在大小作为基线”，因此以下状态作为最终受保护基线，并保留本说明以避免隐藏基线重置：

| File | User-authorized baseline | Final |
| --- | --- | --- |
| `system-knowledge-hub.db` | 1,355,776 bytes; `2026-09-04T15:12:55.7499757Z`; SHA-256 `12ACA3AD05B199D3C8591C17D38FA1F145924DF317956132B7D0DC2DFC6A328A` | 完全一致 |
| `system-knowledge-hub.db-wal` | Missing | Missing |
| `system-knowledge-hub.db-shm` | Missing | Missing |

仅停止本任务启动的 API/Vite 及其 child process，释放 5094/4176。task-owned runtime DB、keys、attachments、logs、temporary seeder/build output 与 EF path 均在校验精确绝对路径后删除；没有关闭、回滚或覆盖用户进程和数据。最终检查时外部 PID 23692 与 5090 listener 均不存在，WAL/SHM 均不存在，用户授权的新基线保持完全一致。

## Final Status

| Gate | Status |
| --- | --- |
| PORTAL-B04 | PASS |
| PORTAL SEARCH | PASS |
| SEARCH PUBLISHED-ONLY | PASS |
| ATTACHMENT LIST | PASS |
| ATTACHMENT AUTHORIZATION | PASS |
| ATTACHMENT SECURITY | PASS |
| TRUST SUMMARY | PASS |
| TRUST PRIMARY TARGET | PASS |
| TRUST EXPLICIT REFERENCE | PASS |
| TRUST DERIVED REJECTED | PASS |
| TRUST NO AGGREGATION | PASS |
| RELATED KNOWLEDGE | PASS |
| TRACEABILITY | PASS |
| TRACE SEMANTICS | PASS |
| ADMIN COMPOSITION / PREVIEW | PASS |
| NO KNOWLEDGE COPY | PASS |
| NO RELATION MUTATION | PASS |
| ANONYMOUS / ADMIN SECURITY | PASS |
| B01 / B02 / B03 REGRESSION | PASS |
| NO NEW MIGRATION | PASS |
| BACKEND REGRESSION | PASS |
| FRONTEND REGRESSION | PASS |
| REPOSITORY DATA PROTECTION | PASS（用户明确授权重置基线后完全一致） |
| CLEANUP | PASS |

**PORTAL-B04 COMPLETE**

**PORTAL-VERIFY READY: YES**
