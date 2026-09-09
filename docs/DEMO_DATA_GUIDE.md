# 持久 Demo / 人工检查指南

这套数据供系统知识中心的长期人工检查使用。数据明确标为“演示”，围绕 MES Lot Track In 组织；数量适中，允许继续编辑。它独立于自动化测试数据库和生产数据库。

## 初始化、启动与停止

需要 PowerShell 7、.NET 8 SDK、Node.js，以及已安装的前端依赖（首次可在 `src/SystemKnowledgeHub.Web` 执行 `npm ci`）。从仓库根目录执行：

```powershell
# 第一次初始化；按隐藏提示输入管理员密码
pwsh -File ./scripts/demo/init-demo.ps1

# 日常启动 API 和前端
pwsh -File ./scripts/demo/run-demo.ps1
```

默认地址为 `http://127.0.0.1:5200`，API 使用 `http://127.0.0.1:5199`。脚本发现端口已占用时拒绝启动，不会停止原有进程；可指定 `-ApiPort 5219 -WebPort 5220`。在启动窗口按 **Ctrl+C**，脚本只停止自己启动的进程及其子进程，保留全部数据。保持窗口运行时即可持续人工检查。

默认管理员用户名为 `demo-admin`，可在首次初始化时指定 `-Username`。密码由用户通过隐藏输入提供，以标准输入传给现有 `bootstrap-local-admin --password-stdin`，不进入命令参数、JSON 或日志。数据中的 Viewer、Editor、知识整理员没有登录凭据；如需角色登录，请使用现有用户管理的安全凭据流程配置。

初始化中途失败时保留现场，不自动删除。若只是管理员 bootstrap 失败，再运行 init 即可继续；如果数据写入未完成、marker 与 canonical sentinel 不符，则拒绝继续插入，需检查后显式 reset。已有可用管理员时不会另建或覆盖账号。

## 数据存放与保留规则

默认根目录：`%LOCALAPPDATA%\SystemKnowledgeHub\demo`。

```text
demo/
  system-knowledge-hub-demo.db
  attachments/
  keys/
  logs/
  demo-dataset.json
  runtime/
```

`runtime/` 保存 ownership、初始化锁及本脚本进程标识；`keys/` 保存本 Demo 的 Data Protection 状态；附件和 API 日志也只写入此根目录。SQLite 运行时可能产生 WAL/SHM。所有这些都是 **用户所有的持久运行时数据**，不提交 Git。普通测试、浏览器验收和清理不得删除这个目录。

三个脚本均支持 `-DemoRoot 'D:\MyData\demo-acceptance'`。必须是仓库外的绝对独立目录，末级名称为 `demo` 或 `demo-*`；拒绝盘符根、用户根、仓库及其父目录、`src`/`App_Data`/`.git` 路径和链接/目录联接。脚本不会使用仓库的 `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db`。

**run-demo 不迁移、不重新 seed、不恢复初始值。** 用户编辑正文、保存修订、修改状态、上传附件、移动 Analysis 节点或调整 Portal 后，后续启动继续保留这些修改。再次 init/seed 核对 manifest 和数据库中的 canonical 用户 sentinel 后，返回“Demo 数据已经初始化”，不重复插入。sentinel 是初始知识整理员的 ID 和创建时间，不是新增 marker 表。

恢复初始数据只有一个入口：

```powershell
pwsh -File ./scripts/demo/reset-demo.ps1
# 确认输出的绝对路径，输入 RESET，再输入新管理员密码

# 已明确决定重置时，可跳过 RESET 提示；仍执行路径和 ownership 检查
pwsh -File ./scripts/demo/reset-demo.ps1 -Force
```

reset 删除指定 Demo 根目录内的全部人工修改并重新调用 init。先正常停止 Demo；记录中的进程仍在运行时拒绝重置。`-Force` 不跳过路径检查，也不代替管理员密码输入。

## 数据概览与生成边界

| 内容 | 初始代表数据 |
| --- | --- |
| 系统 / 功能 | MES、EAP、OCS；7 个 Track In/Out、Hold/Release、通信、Recipe、Dispatch 功能 |
| 数据库知识 | 2 个来源；6 个对象；30 个字段；12 个 KnownValue；MES.LOT 的 STATE_FLAG 为 10/20/30/40 |
| 规则 / 集成 | 5 条规则；4 个 HttpApi / OneWay 集成及契约字段；只含 `demo.invalid` 元数据 |
| 文档 | 14 篇可用文档，覆盖全部 7 种类型；另有 1 篇 canonical soft delete 文档用于不可用 placement |
| 生命周期 / 修订 | Draft、Published、Archived；主 SOP 3 版，DesignNote 2 版 |
| 附件 | 确定的小 PNG 和 PDF，绑定主 SOP 当前内容及修订；PNG 嵌入 Markdown，PDF 在附件区 |
| 证据 / 确认 | 22 条普通 Evidence 覆盖全部 7 种普通类型；6 条 HC 覆盖 active、withdrawn、replacement、旧修订确认 |
| 状态 / 关系 | 系统、文档、关系分别有 Unknown/Inferred/Confirmed；32 条合法有向关系 |
| 调查 | 6 个 UnknownItem，覆盖 Open、Investigating、Finding/Evidence、Resolution、Apply、ConclusionConfirmed、Closed |
| Analysis | 16 个节点，包含目录、原子创建、已有文档、Archived 和 unavailable placement |
| Portal | 6 页、10 个树节点；覆盖全部 8 种 projection，包含已发布/未发布页面和节点 |

内容、顺序和重要实体 ID 可重复生成；创建时间、并发令牌、附件存储名和密码哈希等由 canonical use case 生成，不承诺数据库文件逐字节相同。运行时 `demo-dataset.json` 记录版本、时间、数据库路径、重要 ID、推荐路由和查询词，不含密码或 cookie。

生成器位于 `DeveloperSupport/Demo`，显式 `seed-demo-data` 命令同时要求 Development、`Demo:Enabled=true` 和合法的独立 DB/附件/keys/log 路径。普通应用启动不会生成 Demo 数据；Demo 模式跳过原有 Development 示例 seed。现有 EF migrations 只在显式初始化时应用。

基础对象也通过现有服务创建；修订、附件引用、状态、关系、HC 生命周期、调查更新、Portal 发布及 Analysis placement 均通过 canonical 用例。没有直接 INSERT 业务状态，也没有新增 schema、包或通用 seed 框架。SQL Evidence 只是文本，绝不执行。UnknownItem 的应用结果使用当前支持的 BusinessFunction 更新，补充等待 EAP 回报及参考 SOP 的说明，不伪造未支持的文档更新类型。

**Database Discovery 不预置虚假的成功运行。** 当前发现流程需要真实 provider 才具有真实性，本 Demo 没有连接凭据，也不发起 Oracle/PostgreSQL/SQL Server 调用。已有手工登记的数据库知识可正常检查。

## 建议人工检查顺序

下表供用户逐页检查，不是要求自动执行的 E2E 矩阵。具体 ID 以本机 manifest 为准。

| 顺序 / 页面 | 推荐对象与应见内容 |
| --- | --- |
| 总览 `/dashboard` | 系统、文档、未知项及关系有代表数据 |
| 系统 `/systems` | 演示 · MES：Confirmed、统一知识视图中的功能/数据库/文档/证据/未知项；对比 EAP Inferred、OCS Unknown |
| 业务功能 | Lot Track In：规则、读写 MES.LOT、STATE_FLAG、集成和 Hold 关联；查看来自调查的更新说明 |
| 数据库 `/database-objects` | MESDB → MES.LOT → STATE_FLAG → 10/20/30/40；查看类型、可空性、主键/业务键、估算行数和说明 |
| 业务规则 / 集成 | Track In 前置条件、Hold 禁止入站；MES → EAP 接口和 lotId/equipmentId 契约 |
| 知识内容 `/knowledge-documents` | 全部七种类型，Published 主链、Draft 分析/需求、Archived 旧排查文档 |
| 修订 / 附件 | 主 SOP 的第 1/2/3 版：基础步骤 → Equipment → Hold；Markdown 表格、代码、任务列表、链接、Mermaid、图片；查看 PDF 预览/下载和修订附件 |
| Evidence / HC | MES active HC；状态说明 withdrawn HC；业务要求 replacement HC；SOP 旧修订已确认、当前内容显示 changed-since-confirmation |
| 关系 / Trace | Requirement → Specification → TestCase 完整链；缺规格需求、缺测试的 Hold 规格；检查 incoming/outgoing、Coverage 和 Impact |
| Unknown Items | “Lot Track In 后 STATE_FLAG 未更新”：EAP 回报延迟 Finding、样本 Evidence、Resolution、已应用的功能知识更新；对比 Open 到 Closed 的实例 |
| 全局 / 文档搜索 | `Lot Track`、`Track In`、`STATE_FLAG`、`MES.LOT`、`Equipment`、`Hold`、`EAP`；不同搜索面的字段范围按产品实际行为 |
| Analysis `/analysis` | 按下节检查目录与 canonical 文档复用 |
| Portal Management `/portal-management` | 管理员检查 Demo 页面、section 配置及发布状态 |
| 匿名 Portal `/portal` | 无需登录检查树、搜索、页面、附件、Trust、Related 和 Trace |

## Analysis 人工清单

打开“MES 分析”的系统概览、业务流程、数据库分析、接口目录。已有 Requirement、KnowledgeArticle、SOP placement 应保留各自文档类型；Lot Track In 分析和原子创建分析文档均打开 canonical KnowledgeDocument。

- 历史分析中的 Archived 文档有归档标识、正文只读，仍可组织移动/移除。
- 删除文档的 placement 仅显示“文档不可用”，不泄漏原标题或正文。
- 用 `MES 分析`、`Lot Track In`、`数据库分析` 筛选标题，观察祖先路径与清除筛选。
- 尝试新建目录、新建分析文档、编辑/保存、查看 Revision，以及有未保存内容时切换节点。
- 尝试移动、同级上下排序、移除 placement、加入已有文档。移除 placement 不删除原文档。
- 从文档执行 Portal handoff，检查目标预选；实际编排与发布仍由 Portal 管理入口完成。

这些修改会持久保留。需要恢复演示初始树时显式 reset。

## Portal 人工清单

管理员先在 `/portal-management` 检查 System、Requirement、MES.LOT、接口和 SOP 页面；Summary、KnowledgeDocumentBody、StructuredOverview、DatabaseStructure、AttachmentList、TrustSummary、RelatedKnowledge、Traceability 分散在这些页面中。

存在未发布的 DesignNote 页面/节点，以及指向已发布 SOP 的未发布节点。匿名 `/portal` 只显示 effectively published 的树和页面。使用树、搜索和页面入口，检查 SOP 图片/PDF、MES active HC、状态说明的 withdrawn HC 和 SOP 旧修订覆盖；匿名内容不应包含撤销 actor/reason。

这里的数据帮助人工观察现有功能，不代表真实 Production 部署、PHASE-TRACE 领域验收或 ANALYSIS-VERIFY 已执行。
