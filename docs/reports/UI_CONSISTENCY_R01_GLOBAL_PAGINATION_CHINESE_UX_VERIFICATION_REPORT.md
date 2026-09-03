# UI-CONSISTENCY-R01 全系统分页、中文化与关键交互一致性验证报告

## 结论

`UI-CONSISTENCY-R01` 当前为 **FAIL**。

除“编辑数据库对象时可修改估算行数”外，本任务的分页、中文化、Database Discovery 导航与手工同步抽屉交互均已实现并通过定向验证。该单项存在冻结契约冲突：冻结 API C11 `PUT /api/database-objects/{id}/knowledge` 及当前前后端请求模型仅允许更新业务说明、访问方式、业务唯一键、操作人和并发令牌，不包含 `estimatedRows`。本任务又明确禁止修改后端业务/API 契约。因此编辑抽屉只能安全显示当前估算行数，不能在本任务范围内提供可持久化编辑。按照 `AGENTS.md` 的冻结来源与范围纪律，本报告不伪造 PASS，也不通过只改前端输入但不保存的方式制造虚假能力。

根据任务交付规则，FAIL 状态不创建成功提交、不推送。

## 分页盘点与实现

新增轻量共享组件 `SkhPagination.vue`，统一 Element Plus 布局为 `total, sizes, prev, pager, next, jumper`，仅负责分页状态与事件，不包含业务查询逻辑。普通列表默认 20、可选 20/50/100；同步核对对象与字段保留默认 50、可选 50/100/200。`total > 0` 时即显示分页，一页数据也保留总数与每页数量。

替换/统一的分页表面：

- 系统、业务功能、用户、待确认事项、知识文档、数据库对象、附件管理；
- 文档修订历史、影响分析；
- 发现运行、快照历史、快照对象、快照序列、快照对象详情内字段/约束/索引；
- 差异历史、差异条目；
- 手工同步对象、对象字段、同步计划历史。

全仓前端直接 `el-pagination` 已收口到共享组件。服务端分页保留；切换每页数量统一回到第 1 页并重新请求，页码切换保留每页数量与既有筛选、搜索和业务上下文。

## 中文化与专业词汇

确认应用根节点继续通过 Element Plus `zh-cn` locale 提供中文分页。用户可见权限名称映射为查看者、编辑者、管理员；同步计划状态映射为草稿、待应用、已应用、已失效；同步动作、判断状态、实际原因码和预览结构字段均使用中文展示。删除右上角身份来源提示及重复英文权限显示。

保留的专业词汇包括 Oracle、PostgreSQL、SQL Server、SQLite、SQL、API、URL、OIDC、SHA-256，以及真实架构、表、字段、约束、索引、序列名称。普通说明需要提及 Schema 时使用“架构（Schema）”。

## Database Discovery UX 修正

- 发现运行列表移除重复的“查看快照/查看差异”，仅在管理员可取消活动运行时显示操作列。
- 快照详情使用“发现快照 → 快照 #N”和“← 返回快照列表”，移除跨模块差异入口。
- 差异详情使用“差异审查 → 差异 #N”和“← 返回差异列表”，移除打开目标快照入口。
- “能力”改为默认折叠的“元数据采集能力”，Provider capability code 映射为中文能力与支持状态。
- 手工同步保护说明删除内部术语；判断表头提供键盘可达的中文说明。
- 对象/字段展开使用标准 Caret 图标、约 28×28 点击区、旋转状态和 `aria-expanded`。
- 当前实际原因码完成统一中文映射，未知码使用安全中文兜底；普通界面不显示 raw reason code。
- “生成计划并预览”和历史“查看计划”复用同一 `SyncPlanDrawer`；预览、确认、应用与结果均留在抽屉，主页面不插入详情块。
- 抽屉展示计划状态、上下文、八类动作计数、动作明细、中文结构预览、确认状态和应用结果；关闭后页面筛选、分页、展开与滚动保留机制不重置。

## Database Knowledge UX 修正

- 新增对象标签统一为“估算行数（可选）”。编辑抽屉显示当前估算行数，但因冻结 C11 更新契约不包含该字段而保持只读；这是本任务唯一未通过项。
- 字段详情右下角四个动作按“删除/编辑、证据/待确认事项”固定为两列等宽、两行等高的 42px action grid，图标统一为 16px。
- 删除无意义身份提示；右上角权限只显示中文“管理员”。

## 定向验证

| 检查 | 结果 |
| --- | --- |
| 受影响 Frontend focused tests | PASS — 19 files / 91 tests |
| `npm run type-check` | PASS（共享组件、API 调用签名与抽屉初次实现后执行一次） |
| Affected ESLint | PASS |
| Affected Prettier | PASS |
| 前端 build | PASS；执行原因：新增共享组件、同步计划抽屉、Element Plus 图标引用及路由 import 调整需要验证打包解析；仅有既有 chunk-size 提示 |
| `git diff --check` | PASS |
| Backend verification | NOT APPLICABLE — 无后端代码修改，未运行后端测试 |

## Focused browser smoke

使用临时、任务专属的前端 mock HTTP 服务和 Vite 端口 `26241/26242`，未启动后端、未连接或修改任何 SQLite 数据。真实 Vue/Element Plus 渲染确认：

- 普通系统列表显示“共 45 条”、中文前往页、20/50/100 每页选项、正确上一页禁用状态；
- 发现运行无重复详情按钮；快照/差异列表可进入独立详情并显示明确返回列表入口；元数据采集能力为中文折叠内容；
- 手工同步判断说明完整中文、展开按钮状态可达、`UnsupportedOrdinal` 仅显示“字段顺序不受支持”及中文说明；
- 生成计划与历史查看均打开同一个右侧同步计划抽屉，主页面内容仍保留；
- 最终抽屉预览显示“架构（Schema）/名称/对象类型：表/数据库注释/主键”，不显示 raw JSON 字段名或 `Table`；
- 数据库对象编辑抽屉可见估算行数 48000 及只读契约说明；字段详情显示四个一致动作按钮；当前用户面板只显示“管理员”，无重复英文身份提示；
- 1366×768、1440×900、1920×1080 三档无 document-level horizontal overflow。

浏览器标签、临时 mock/harness 文件、Vite/mock 进程均已清理；端口 `26241/26242` 无监听。浏览器控制遵循 `computer-use` 技能的可见状态与清理流程。

## 最终状态

```text
UI-CONSISTENCY-R01 FAIL

GLOBAL PAGINATION COMPONENT: PASS
PAGINATION LAYOUT CONSISTENCY: PASS
PAGE SIZE SELECTION: PASS
SERVER-SIDE PAGING PRESERVED: PASS

ELEMENT PLUS CHINESE LOCALE: PASS
USER-VISIBLE CHINESE CONSISTENCY: PASS
STATUS LABEL LOCALIZATION: PASS
REASON CODE LOCALIZATION: PASS
PROFESSIONAL TERM EXCEPTIONS: PASS

DISCOVERY RUN UX: PASS
SNAPSHOT NAVIGATION: PASS
DIFFERENCE NAVIGATION: PASS
METADATA CAPABILITY DISPLAY: PASS

RECONCILIATION STATUS HELP: PASS
TREE EXPAND ICON: PASS

SYNC PLAN DRAWER: PASS
GENERATE/PREVIEW DRAWER FLOW: PASS
HISTORY PLAN DRAWER FLOW: PASS
MAIN PAGE LAYOUT STABILITY: PASS

DATABASE OBJECT ESTIMATED ROWS: FAIL — 当前值可见，但冻结 C11 更新契约不支持持久化编辑
IDENTITY HINT REMOVAL: PASS
FIELD ACTION BUTTON CONSISTENCY: PASS

FOCUSED FRONTEND TESTS: PASS
AFFECTED STATIC CHECKS: PASS
FOCUSED BROWSER SMOKE: PASS

BACKEND VERIFICATION:
NOT APPLICABLE

REPOSITORY DATA PROTECTION:
NOT APPLICABLE — 未启动后端或数据库运行时

UI-CONSISTENCY-R01 INCOMPLETE
```
