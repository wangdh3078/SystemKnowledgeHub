# DEMO-DATA-R01 — Persistent Manual Acceptance Dataset

日期：2026-09-09。基线：`main` / `2c85ed9570b53b1a779bdb0151a5777058814251`。

## 当前结果

实现及 Demo tooling 的聚焦检查已通过。最终持久数据已经创建并保留，用户已完成管理员密码设置并登录；五个代表页面的默认桌面浏览器抽查全部通过。DEMO-DATA-R01 COMPLETE；DEMO ENVIRONMENT READY FOR USER MANUAL INSPECTION。

| Gate | 当前结果 |
| --- | --- |
| DEMO-DATA-R01 | PASS — COMPLETE |
| DEMO INIT | PASS |
| PERSISTENT DEMO DB | READY — 数据文件、附件和 manifest 已存在 |
| RUN DEMO | PASS |
| RESET DEMO | PASS — 仅在任务临时副本执行实际重置 |
| PRODUCTION FAIL-CLOSED | PASS |
| REPOSITORY DB ISOLATION | PASS |
| DETERMINISTIC SEED | PASS |
| IDEMPOTENT RE-SEED | PASS |
| SYSTEM DATA | PASS |
| BUSINESS FUNCTION DATA | PASS |
| DATABASE KNOWLEDGE DATA | PASS |
| BUSINESS RULE DATA | PASS |
| INTEGRATION DATA | PASS |
| KNOWLEDGE DOCUMENT ALL TYPES | PASS |
| LIFECYCLE DATA | PASS |
| REVISION DATA | PASS |
| ATTACHMENT DATA | PASS |
| EVIDENCE DATA | PASS |
| HUMAN CONFIRMATION DATA | PASS |
| KNOWLEDGE STATUS DATA | PASS |
| RELATION DATA | PASS |
| TRACE DATA | PASS |
| UNKNOWN ITEM DATA | PASS |
| ANALYSIS TREE DATA | PASS |
| ANALYSIS ARCHIVED DATA | PASS |
| ANALYSIS UNAVAILABLE DATA | PASS |
| PORTAL DATA | PASS |
| PORTAL PUBLISHED/UNPUBLISHED DATA | PASS |
| SEARCH DATA | PASS |
| DEMO MANIFEST | PASS |
| DEMO GUIDE | PASS |
| DEFAULT BROWSER SMOKE | PASS — System/MES.LOT/主 SOP/Analysis/匿名 Portal 实际可浏览 |
| PERSISTENT DEMO ROOT PRESERVED | PASS |
| TEMP VERIFICATION CLEANUP | PASS |
| BROWSER ZOOM | NOT APPLICABLE |

## 持久环境与交付物

- Root：`C:\Users\wang\AppData\Local\SystemKnowledgeHub\demo`。
- DB：`C:\Users\wang\AppData\Local\SystemKnowledgeHub\demo\system-knowledge-hub-demo.db`。
- 同根保留 `attachments`、`keys`、`logs`、`runtime`、`demo-dataset.json`；没有把测试数据库复制或转换成 Demo。
- 管理员用户名：`demo-admin`；用户已通过隐藏输入完成 bootstrap，可用管理员核对成功。另有三个无登录凭据的代表用户。用户密码不在报告、命令参数、manifest 或日志中。
- API / Web：`http://127.0.0.1:5199` / `http://127.0.0.1:5200`。
- 操作指南：`docs/DEMO_DATA_GUIDE.md`；命令：`pwsh -File ./scripts/demo/init-demo.ps1`、`run-demo.ps1`、`reset-demo.ps1`（均位于 `scripts/demo/`）。

## 实现边界

`Program` 在 Serilog、Data Protection 和 DbContext 注册前验证 Demo 的 Development、显式 mode 和独立存储路径；Production 与非法路径拒绝。仅显式 `seed-demo-data` 执行迁移和生成，普通 Demo 启动跳过原 Development 示例 seed。没有 schema/migration、产品 API/前端或依赖变更。

`DeveloperSupport/Demo` 使用当前真实服务生成全部数据，包括基础 master 对象；没有直接 DbContext INSERT 例外。canonical create/save 生成 Revision，附件通过上传/内容保存建立引用，HC/状态/关系/调查/发布/placement 通过既有用例转移。初始化失败保留部分现场且无完成 marker，下次拒绝追加；不会把失败数据标为已完成。

manifest version 1 记录关键 ID、路径、时间、推荐路由和搜索词，并以知识整理员的 canonical User ID + CreatedAt 核对。重复运行直接返回、不覆盖人工修改。初始化使用任务目录内独占锁；不新增 marker 数据库表。内容、顺序及 ID 是显式确定的，运行时间/存储名/令牌等仍由 canonical 机制生成。

脚本默认根在仓库外；拒绝相对/空路径、盘符/用户根、仓库及父级、src/App_Data/.git 和目录联接。reset 另核对 ownership、后代链接和本脚本运行进程。PID 校验比较解析后的 UTC 启动时间，兼容 PowerShell 将 JSON 时间自动转换为日期对象。run 记录自己的子进程、在同一控制台输出启动诊断，finally 只结束这些进程，不删除持久数据。

## 数据与内部验证

最终 Demo 有 3 Systems、7 BusinessFunctions、2 DatabaseSources、6 DatabaseObjects、30 Columns、12 KnownValues、5 Rules、4 Integrations、14 篇可用文档及 1 篇 soft-deleted 文档、32 Relations、22 普通 Evidence + 6 HC、6 UnknownItems、6 PortalPages/10 Nodes/17 Sections、16 AnalysisNodes。原始文档表 15 行、Revision 18 行；临时人工编辑副本多 1 版是验证动作，未带入最终 Demo。

全部七种文档类型、Draft/Published/Archived、SOP 3 版和 DesignNote 2 版均存在。普通 Evidence 的当前闭集为 ExistingDocument、CodeReference、Sql、DatabaseSample、DatabaseComment、Api、MqMessage。系统/文档/关系分别覆盖 Unknown/Inferred/Confirmed。HC 包括 active、withdrawn、replacement 和旧修订确认；Portal 当前撤销确认计数为 0，不披露撤销 actor/reason。

完整 Requirement → Specification → TestCase 路径和缺规格/缺测试两类样本由当前关系矩阵合法创建。UnknownItem 状态为 Open 1、Investigating 3、ConclusionConfirmed 1、Closed 1，带 Finding、Evidence、Resolution 和已应用的 BusinessFunction 更新；没有伪造未支持的文档 KnowledgeUpdate 类型。

Analysis 有通过 CreateDocument 原子创建的 DesignNote、已有 Requirement/KnowledgeArticle/SOP placement、Archived 文档和 canonical soft delete 后的 unavailable placement。Portal 的八种 projection 均有合法兼容来源，包含已发布/未发布 Page/Node。

内部 summary 已实际执行：关键 MES/LOT、关系详情端点解析、`foreign_key_check` 无结果、主 SOP FTS 匹配 1、Analysis 树可读、Portal 已发布树非空。最终持久 DB 又经只读数据摘要核对：FK=0、SOP FTS=1、类别和数量符合上述结果。

小型固定 PNG 为 240×80，PDF 为单页固定文本。最终匿名 SOP 显示图片、PDF 预览/下载链接和 changed-since-confirmation。SQL 仅是文本样本，没有执行。Database Discovery 不 seed provider 成功结果、凭据或伪造的 Run/Snapshot；指南说明需真实 provider。

## 聚焦验证证据

- `dotnet build src/SystemKnowledgeHub.Api --no-restore --verbosity quiet`：成功，0 warnings / 0 errors。
- `dotnet test tests/SystemKnowledgeHub.Api.Tests --no-restore --filter FullyQualifiedName~DemoRuntimeTests --verbosity quiet`：5/5 PASS。覆盖显式 Development、Production/Testing/未启用 mode 拒绝及根/DB 路径边界。
- 四个 PowerShell 文件语法解析 PASS；reset 的 8 个不安全目录在删除前拒绝。
- JSON 进程记录使用当前活跃进程的真实 PID/启动时间，reset 明确拒绝，证明日期反序列化后的活动进程检查有效。
- 显式 CLI：第一次 seed PASS；第二次返回已初始化，静止副本 DB 与 marker 哈希不变；marker sentinel 篡改后拒绝，测试后恢复临时 marker。
- Production CLI 和 repository DB path CLI 均 exit 1，未打开仓库 DB。
- 临时 `demo-r01-tooling-2` 上执行 reset → init → bootstrap PASS。仅临时验证凭据在进程内随机生成，未写入文件或命令参数；最终管理员已由用户隐藏输入创建，`--check-admin` 返回成功。
- 真实 API 上编辑临时 DesignNote，停止 run、再次 seed、重启后标题和修订保留，原登录会话仍有效；seed 没有重复生成实体。校验业务值/修订，避免把 SQLite 关闭后 WAL 合并引起的物理文件变化误判为业务覆盖。
- 子进程退出时 run 清理自己创建的 API/Vite；最终 Demo 实际 Ctrl+C 输出“持久数据完整保留”，随后可重新启动同一 DB。
- 仅使用一个默认桌面浏览器大小。最终匿名 `/portal` 树和 SOP 页面实际可浏览，截图显示中文正文、布局和树；AX 显示 Markdown、Mermaid 容器、PNG、PDF 和 HC 当前覆盖。
- 用户登录后的 Chrome 中，`/systems/1` 显示演示 MES 及非空统一知识视图；`/database/1` 显示 MES.LOT 字段及 STATE_FLAG 的 10/20/30/40 四个已知值；`/knowledge-documents/4` 显示主 SOP 的三版修订入口、已发布状态、确认后已修改提示、Markdown、已加载 Mermaid、PNG 及 PDF 预览/下载入口。
- `/analysis` 显示 MES 分析及历史分析目录、不同类型已有文档、已归档文档和不泄漏原标题的“文档不可用”。打开 `/analysis/nodes/10` 成功加载 DesignNote 第 2 版、Mermaid 图表和关联对象。此次仅作数据可浏览抽查，没有执行完整人工检查清单或修改用户数据。

未执行前端全套测试、全后端测试、响应式/移动端矩阵或 zoom。未执行 ANALYSIS-VERIFY。

## 数据保护、限制与 Git

仓库真实 DB 只读取文件元数据和哈希，未通过 SQLite 打开/复制/迁移/seed/checkpoint。前后均为：长度 `1372160`，UTC mtime `2026-09-07T15:51:07.1311811Z`，SHA256 `F6C2DC43EB04110A830E86635920AC8DE1B52C60D9C19E9E178741AFABCCAC88`，WAL/SHM 不存在。

一次 sandbox 内脚本 restore 因本机 NuGet.Config 读取权限失败，使用正常权限重跑后通过；未改变 NuGet、环境安全或产品配置。已有 EF global-query-filter 模型警告在启动时可见，不扩大任务修复。

全部适用 gate 已通过，没有新增未处理产品 gap。ANALYSIS-VERIFY 保持 READY，未执行；Portal v1 / Analysis A01–B03 的既有状态不变。本 Demo 不证明 Production 部署或真实领域 Product Acceptance。

已清理任务专属 Temp 下的 `demo-r01-tooling-1`、`demo-r01-tooling-2` 及临时核对脚本/日志。代理启动的验证 API/Vite 已停止并核对无残留；随后用户自行启动并登录的 Demo 会话继续保留，未停止其进程或关闭其浏览器。最终 Demo DB、manifest、两份附件及 keys/logs 均保留。PERSISTENT DEMO ROOT PRESERVED。

Git 交付：分支 `main`；实现、指南、索引及本报告使用单一任务提交 `feat(dev): add persistent demo acceptance dataset`。完整提交 SHA 与 `push origin main` 的实际结果在最终交付消息中单独记录，以区分验证结果与 Git 交付结果。
