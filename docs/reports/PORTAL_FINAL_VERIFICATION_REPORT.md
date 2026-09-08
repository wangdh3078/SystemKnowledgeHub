# PORTAL-VERIFY — Internal Knowledge Portal v1 Final Verification

2026-09-08。**PORTAL-VERIFY PASS / INTERNAL KNOWLEDGE PORTAL V1 COMPLETE**。

验收对象为 `main` 的 authoritative HEAD **`8859f3fe7a2df85db13a2a73b707454cc295cc0a`**（`test: scope discovery sync migration verification`）。开始时工作区干净，已检查 status、diff 和最近八次提交。本任务没有修改产品代码、测试、配置、实体或 migration；交付仅为本报告、文档索引和文件地图的 phase 完成记录。

本结论只覆盖当前本地隔离验证环境中的 Portal v1 产品与技术闭环，不批准 Production deployment。未开始 Analysis Workspace 或其他阶段。

## Authority 与范围

依据根 `AGENTS.md`、`docs/DOCUMENT_INDEX.md`、[PORTAL-A01](../design/PORTAL_A01_INTERNAL_KNOWLEDGE_PORTAL_ARCHITECTURE_DECISION.md)及其 [freeze report](PORTAL_A01_INTERNAL_KNOWLEDGE_PORTAL_ARCHITECTURE_FREEZE_REPORT.md)、[AMEND-01](PORTAL_A01_AMEND_01_TRUSTSUMMARY_SOURCE_COMPATIBILITY_REPORT.md)、PORTAL-B01/B02/B03/B04 verification reports、[UI-R01 / R01-R01](PORTAL_UI_R01_MANAGEMENT_HOME_VISUAL_CONSISTENCY_VERIFICATION_REPORT.md)。相关语义采用 HC-A01 / HC-B01、TRACE-A01、ATTACH final verification、DELETE-A01、REL-EVIDENCE-A01、REV-A01、KC-C01 与当前 Evidence / KnowledgeStatus / Security 实现；运行时隔离遵守 DBSAFE-R01。

最新明确关闭记录优先：REV-GAP-012 的 R01 关闭段与本次 authoritative HEAD 已将其关闭；历史报告/旧汇总行中的 OPEN、后续阶段 READY 描述未改写为新的验收证据。SEC-04 与 PHASE-TRACE 独立 gate 保持原状态。

边界核查确认同一 Web、API、DbContext、SQLite 和部署。匿名 `/portal`、`/portal/pages/{id}`、`/portal/search` 使用独立 GET-only client；管理端 `/portal-management` 与 `/api/admin/portal/**` 为 Administrator-only。没有第二套应用、数据库、知识事实或关系模型。

## Focused 自动化与构建

| 当前 HEAD 检查 | 实际结果 |
| --- | --- |
| `AdminPortalApiTests` | PASS — 12/12 |
| `PortalAnonymousReadApiTests` | PASS — 51/51 |
| Portal frontend focused tests | PASS — 10 files / 31 tests |
| `dotnet build SystemKnowledgeHub.sln -c Release --disable-build-servers` | PASS — 0 warnings / 0 errors |
| `npm run type-check` | PASS |
| `npm run build` | PASS — 仅既有 chunk-size 提示 |
| `dotnet ef migrations has-pending-model-changes` | PASS — no pending model changes |
| 文档最终 diff / whitespace review | PASS |

后端仅运行上述两个现有 API test classes，共 **63 passed、0 failed、0 skipped**，未重跑全部 B01～B04、Persistence 或全后端 suite。命令为：

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj -c Release --no-build --no-restore --filter "FullyQualifiedName~AdminPortalApiTests|FullyQualifiedName~PortalAnonymousReadApiTests" --settings <task-owned>/serial.runsettings --logger "trx;LogFileName=portal-focused.trx" --results-directory <task-owned>/results
```

使用既有 REV-GAP-011 serial gate：MaxCpuCount=1、ParallelizeTestCollections=false、MaxParallelThreads=1。现有用例覆盖 Administrator/Viewer/Editor 与 antiforgery、whole-page composition、compatibility/readiness/preview、Page/Node publication、Home/Tree/Page/Search、current-revision page-scoped attachments、四类 B04 projection、Draft/Archived/deleted/broken reference fail-closed，以及 HC withdrawal/Preview privacy。复用现有 Search title/target/explicit/body 命中与 paging、Related incoming/outgoing/filter/link、Trace path/lifecycle/limit tests；未增加用例。

前端运行：

```text
npm run test -- src/layouts/PortalLayout.spec.ts src/app/router/portalRouteSecurity.spec.ts src/features/portal-reading src/features/portal-management
```

十个文件包括 Layout、route security、Home、TreeNavigation、PageView、SectionRenderer、Portal read API/decoder、Search surface、Management API 与 Management surface。未运行 full Vitest、全前端回归或无关 HC UI suite。没有产品文件修改，因此未重复执行全量 lint/Prettier。当前 Portal final gate 未另行要求 NuGet vulnerability scan，未因历史报告曾运行而扩大范围。

EF 命令指定 `--project src/SystemKnowledgeHub.Api --startup-project src/SystemKnowledgeHub.Api --configuration Release --no-build`，并将 `SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH` 设为 task-owned absolute `<TEMP>/portal-verify-20260908/design-time.db`。输出 `No changes have been made to the model since the last migration.`，未创建该文件。现有 required-navigation/global-query-filter warnings 如实保留；未新增 migration。

## 一个默认桌面浏览器中的集成流程

真实 Chrome；未调用 viewport override、窗口尺寸修改、responsive/device matrix 或任何 browser zoom 操作。API 通过 `dotnet run --project src/SystemKnowledgeHub.Api -c Release --no-build --no-launch-profile` 启动，显式 Development；API/Vite 使用 5193/5194。SQLite、Data Protection、Attachment StorageRoot、Serilog 日志全部为 `<TEMP>/portal-verify-20260908` 下的独立绝对路径。未使用测试认证替代真实浏览器登录。

使用 supported bootstrap 创建本任务 Local Administrator。浏览器登录最初被自动审批拦截；用户随后在当前对话明确授权此隔离账号及编排、发布、HC 撤销、取消发布操作，之后登录成功。该执行授权不改变产品权限模型。

Fixture 复用隔离 Development seed 的 MES / Lot Track In，新增 `MES.LOT` / `LOT_ID` 与四个 Published KnowledgeDocument：Requirement、Specification、TestCase、Sop。操作说明 current revision 2 包含 Markdown、表格、strict Mermaid、68-byte PNG 与 612-byte PDF。Requirement current revision 1 有一条普通 Evidence 与一条 Active HC。canonical relations 为 Requirement → SpecifiedBy → Specification → VerifiedBy → TestCase，另有 Requirement AppliesTo BusinessFunction、Sop References Requirement。

通过真实 API 准备主页面六个草稿 sections、MES 目录及辅助规格/测试页面；通过 **Admin UI** 添加第七个 `Derived + Traceability` 章节并执行 whole-page 保存，代表性验证真实编排到阅读闭环：

1. `/portal-management` 中保存按钮初始禁用；添加追溯后启用并提示未保存；保存成功后禁用且提示“所有修改已保存”。七类章节为 Summary、KnowledgeDocumentBody、DatabaseStructure、AttachmentList、TrustSummary、RelatedKnowledge、Traceability。
2. Preview 显示以上内容与顺序、Mermaid、`MES.LOT` 字段和 48,000 estimated rows；需求 Trust 为 Unknown / Evidence 2 / HC 1 / 当前修订已覆盖。Preview 复用安全 DTO；其管理端附件链接使用 authenticated canonical delivery，匿名页面使用独立 page-scoped URL。
3. 在 UI 依次发布主 Page、MES Folder、主 PageNode，确认 Page 已发布、内容编辑禁用，导航发布独立生效；辅助节点经管理 API 发布以提供真实 Related/Trace 导航。
4. 使用未登录的 `localhost:5194` 会话访问 `/portal`（管理员 cookie 仅在 `127.0.0.1`）。Home/Tree/recent 正常，无登录、用户头像或管理入口；搜索 `Lot Track` 返回三条 PortalPage，点击进入完整知识页。
5. 匿名页面七类内容正确，Markdown 图片实际加载，Mermaid 图实际生成。Related 同时显示 incoming“被引用”、outgoing“适用于/由规格定义”；Specification 有 Portal link，未建 published PortalPage 的 Sop/BusinessFunction 仅摘要。点击 Related Specification 与 Trace TestCase 均进入 `/portal/pages/{id}`。
6. PDF 预览链接打开浏览器内置 viewer，真实显示 `MES Lot Track In - Portal verification`；页面下载链接触发实际 PDF 下载。HTTP 补充核对图片 content、PDF preview/download 均 200，字节与源 fixture SHA-256 一致，保留 nosniff 与正确 inline/attachment disposition。

未在浏览器构建大型 limit fixture，也未扩展 ATTACH、Relationship 或 Global Search 验收范围。

## HC active / withdrawn integration

在 canonical KnowledgeDocument → Evidence UI 打开代表性 HC 并撤销，输入 task-only privacy canary。认证详情保留原确认事实并显示“已撤销”及审计。刷新匿名 Portal 后：

| 投影 | 撤销前 | 撤销后 |
| --- | --- | --- |
| Requirement KnowledgeStatus | Unknown | Unknown |
| CurrentRevisionNumber | 1 | 1 |
| Trust EvidenceCount | 2 | 1 |
| Trust HumanConfirmationCount | 1 | 0 |
| ConfirmationCoverage | CurrentRevisionConfirmed | NoConfirmation |
| Trace root/path 中 Requirement trust | Evidence 2 / HC 1 / current confirmed | Evidence 1 / HC 0 / NoConfirmation |

匿名页面、匿名 JSON 与 Admin Preview safe page JSON 均无 withdrawal actor、reason、time、replacement chain、Provider identity 或 privacy canary。没有自动变更 KnowledgeStatus、修订、关系或结构路径。当前 `EffectiveEvidence.Predicate` 只排除 Withdrawn HC，普通 Evidence 仍计入支持。

## Publication、Search 与附件 fail-closed

只执行一条 browser negative lifecycle：在 Admin UI 取消发布主 PageNode。

- Home/recent、Tree 与 Search 不再暴露该页；浏览器搜索由三条变为两条。
- Direct page 返回 HTTP 404，浏览器显示统一“页面未找到”，没有登录或隐藏资源详情。
- 原 image content 与 PDF preview URL 均 HTTP 404。
- 在 UI 重新发布节点后 Home/Tree/Page 恢复，Search 再次包含主页面，image/PDF 均 200；HC 仍为撤销后的 count/coverage。

其余 Draft/Archived/deleted target、explicit reference 不合资格、Page unpublish、错误 attachment scope 与非 current revision 的情形由本次已有 focused backend tests 证明。Search server paging 的 runtime 补充为 `pageSize=1` 返回一条、total=3；title/target/document 命中及 eligibility 由现有 tests 覆盖。

## Security、Sanitization、Accessibility 与请求边界

仅检查当前真实 Portal DTO、controller、client 和上述代表路径，没有通用 scanner。匿名 DTO 为 concrete allowlist，不含 UserId/actor/publisher、storage key/path/filesystem path、connection/credentials/host/username、raw technical SQL/provider error、Discovery technical identities、Evidence raw payload 或 Admin route。现有 canary tests 与 runtime privacy 检查通过。Related/Trace safe metadata、Portal-only links 和当前 HC semantics 一致；composition/read 没有 canonical KnowledgeRelation 写入。

匿名代表性真实 HTTP probes 对 `/api/admin/portal/pages`、`/api/evidence/2`、`/api/admin/attachments`、`/api/search?q=Lot`、`/api/relationships/1`、`/api/knowledge-documents/1/traceability`、`/api/users`、`/api/database-discovery/runs` 均为 401；fallback 路径 `/api/attachments/1`、`/api/trace/1` 同样为 401。它们不被当作已实现 canonical endpoint 的功能证明。Administrator 及非 Administrator 的 200/403 边界由真实管理流程和本次 Admin tests 共同覆盖。

最小 accessibility：正常 Tab 可到达品牌链接、搜索输入、搜索按钮与目录控制，focus-visible 可见；Search/Tree navigation/buttons/links 的代表性名称可识别，Portal 只有一个 main，页面没有 dialog landmark，Preview 使用单一 dialog。未运行全站 accessibility scanner 或尺寸矩阵。

代表流程控制台无新 unexpected errors；负向页面的预期 404 不被当作产品错误。浏览器 inspection API 不提供 Resource Timing，因此用现有 Vite `DEBUG=vite:proxy` 记录真实代理请求，未修改产品文件。关闭 Admin tab 后，20:17:43～20:18:12 的 Home → Search → Page / image 记录仅包含 `/api/portal/home`、`/tree`、`/search`、`/pages/1`、`/pages/1/attachments/1/content`；没有 `/api/admin/**`、current-user bootstrap 或 canonical Admin API。此前 Admin tab 自动刷新产生的合法认证请求不混入匿名观测窗口。

## Bounds 与复用证据

当前 validator/limits 保留 sections/page ≤30、KnowledgeDocumentBody ≤5、Related ≤20/group、Trace ≤200 nodes / 300 edges / depth 2、Search default 20/max 100。附件仍返回受控 Stream；前端读取一次 composed page，未逐章节请求 canonical API。

本次现有 Admin/anonymous tests 已覆盖 section 限制与 Trace hard limits。没有再运行独立 performance class 或压力测试；复用 [B04](PORTAL_B04_SEARCH_ATTACHMENT_TRUST_RELATED_TRACE_INTEGRATION_VERIFICATION_REPORT.md) 的固定 query-count evidence。源码与 B04 交付 `0ba5eb6` 比较：PortalQueries、PortalLimits 及 performance test 未变；B04 projector 唯一差异是 HC effective predicate，并未增加 query/section。request-scoped caches 仍在。

## DBSAFE 与 cleanup

repository-owned SQLite 仅通过 filesystem existence、size、UTC mtime、SHA-256 检查；未以 SQLite/EF 打开、复制、迁移、seed 或 checkpoint。起始与全部运行时停止后的序列化 fingerprint **完全一致**：

| 文件 | Before = After |
| --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | Exists; 1,372,160 bytes; `2026-09-07T15:51:07.1311811Z`; SHA-256 `F6C2DC43EB04110A830E86635920AC8DE1B52C60D9C19E9E178741AFABCCAC88` |
| `system-knowledge-hub.db-wal` | Absent |
| `system-knowledge-hub.db-shm` | Absent |

一次 PowerShell Compare-Object 将 JSON 自动反序列化的 DateTime 与新采集的 ISO string 误判为不同；检查具体字段后改用原始 JSON 精确比较，结果一致。这是验收脚本的类型比较问题，未发生持久化文件变化、未重设基线。

只停止本任务 API/Vite sessions，5193/5194 无 LISTENING；关闭任务浏览器 tabs。下载的 PDF 以精确路径、612-byte 长度和源 SHA-256 识别后删除。focused test factory 已清理自己的 runtime/attachment/log directories，本次运行时段无残留；历史其他任务目录未动。专属临时 root 下的 DB、keys、attachments、logs、凭据、脚本、结果与 EF path 在核对绝对路径后清理，不提交生成物。

## Final gate 与交付

| Gate | Result |
| --- | --- |
| PORTAL-VERIFY | PASS |
| ADMIN COMPOSITION | PASS |
| ADMIN PREVIEW | PASS |
| PAGE/NODE PUBLICATION | PASS |
| ANONYMOUS HOME/TREE/PAGE | PASS |
| PORTAL SEARCH | PASS |
| PAGE-SCOPED ATTACHMENTS | PASS |
| TRUST SUMMARY | PASS |
| RELATED KNOWLEDGE | PASS |
| TRACEABILITY | PASS |
| HC ACTIVE/WITHDRAWN TRUST INTEGRATION | PASS |
| PUBLISHED-ONLY / FAIL-CLOSED | PASS |
| ANONYMOUS SECURITY | PASS |
| PORTAL SANITIZATION | PASS |
| ADMIN SECURITY | PASS |
| FOCUSED BACKEND | PASS |
| FOCUSED FRONTEND | PASS |
| DEFAULT BROWSER E2E | PASS |
| ACCESSIBILITY MINIMUM | PASS |
| NO PENDING MODEL CHANGES | PASS |
| BUILD | PASS |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |
| BROWSER ZOOM | NOT APPLICABLE — prohibited by AGENTS |
| MULTI-VIEWPORT / RESPONSIVE MATRIX | NOT APPLICABLE — not requested |
| SEC-04 | UNCHANGED — Production rollout blocker remains |
| PHASE-TRACE PRODUCT ACCEPTANCE | UNCHANGED — real-domain Product acceptance remains independent |

未发现阻塞 Portal v1 的新缺陷，无新增 Gap。Portal phase 的 A01、AMEND-01、B01、B02、B03、B04、UI-R01、UI-R01-R01、VERIFY 已完成。

验证 PASS 与 Git delivery 分开：本报告随 task-specific documentation commit `docs(portal): complete internal portal verification` 交付到当前 `main`；完整 commit SHA 和实际 push 结果以任务最终回执为准，不把本地验证解释为生产发布批准。
