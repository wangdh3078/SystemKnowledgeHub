# System Knowledge Hub — MVP Final UI Inventory

状态：**CONFIRMED / DESIGN FROZEN**  
产品名称：**系统知识中心**  
目的：为后续 Codex 开发提供唯一 UI 原型索引，明确 Route、Drawer、Overlay、Edit 与 Workflow State 的边界，并阻止引用旧方案或中间稿。

本文件已确认并冻结。其 UI 资产选择优先级高于各阶段 Product Design 说明文档中的“待评审”“第一版”或旧文件清单。需求语义以 `System_Knowledge_Hub_MVP_Design_Baseline.md` 为准。

## 0. 使用规则

### 状态定义

| 标记 | 含义 | 后续开发规则 |
| --- | --- | --- |
| **KEEP** | 当前有效资产 | 允许作为 Golden 或辅助参考 |
| **SUPERSEDED** | 曾经有效，但已被更新版本替代 | 不得用于实现；只能用于历史追溯 |
| **DEPRECATED** | 已否决方向、原始生成稿或非 UI 来源 | 禁止作为任何实现依据 |

### Golden UI Reference 规则

1. 每个正式页面或交互状态只能使用本文件指定的一个 Golden UI Reference。
2. Review Board 与 QA Comparison 即使标记为 KEEP，也只是评审或一致性证据，不是 Golden UI Reference。
3. 同一个视觉状态若同时承担 Route、Drawer、Edit 或 Workflow 语义，只保留一个 canonical Golden 文件；其它分类引用该 canonical 项，不复制第二份 Golden。
4. 所有 Golden 截图中的“遗留系统知识中心”或 `Legacy Knowledge Hub` 均为 **SUPERSEDED 历史截图文案**。正式中文实现 **MUST** 使用“系统知识中心”，正式英文名称 **MUST** 使用 `System Knowledge Hub`。该文案覆盖不触发 Golden 图片重生成。
5. 所有正式 UI 使用简体中文。技术标识保持原文，例如 `MES.TABLE_EQP`、`STATE_FLAG`、`RabbitMQ`、`HTTP API`。
6. 原始 ImageGen 输出目录中的 `exec-*.png` 不是受控设计资产，不得引用。
7. 所有 canonical Golden 图片统一位于 `product-design/final-ui/`。后续开发不得绕过该目录引用原始位置的同内容图片。

## 1. Route Pages

MVP 只有以下 11 个正式 Route Page。Global Search 与 Create Knowledge Object 不属于 Route。

| ID | Route Page | 类型 | 状态 | Golden UI Reference | 说明 |
| --- | --- | --- | --- | --- | --- |
| RP-01 | 总览 | 发现 / 导航 | **KEEP** | `product-design/final-ui/RP-01_Dashboard.png` | 不使用复杂 BI 图表 |
| RP-02 | 系统列表 | 发现 / 导航 | **KEEP** | `product-design/final-ui/RP-02_Systems_List.png` | 点击进入 RP-03 |
| RP-03 | 系统详情 | Detail | **KEEP** | `product-design/final-ui/RP-03_System_Detail.png` | 下半段内容见 KEEP 辅助资产，但不产生第二个 Golden |
| RP-04 | 业务功能列表 | 发现 / 导航 | **KEEP** | `product-design/final-ui/RP-04_Business_Functions_List.png` | 点击进入 RP-05 |
| RP-05 | 业务功能详情 | Detail | **KEEP** | `product-design/final-ui/RP-05_Business_Function_Detail.png` | 当前主状态包含关系 Drawer；Drawer 关闭后仍是同一 Route |
| RP-06 | 数据库对象列表 | 发现 / 导航 | **KEEP** | `product-design/final-ui/RP-06_Database_Objects_List.png` | Database / Schema 浏览属于 Main Content |
| RP-07 | 数据库对象详情 | Detail | **KEEP** | `product-design/final-ui/RP-07_Database_Object_Detail.png` | 结构 Golden；截图英文文案必须按中文术语与产品名规范替换 |
| RP-08 | 待确认事项列表 | 发现 / 导航 | **KEEP** | `product-design/final-ui/RP-08_Unknown_Items_List.png` | 状态为待处理 / 调查中 / 结论已确认 / 已关闭 |
| RP-09 | 待确认事项详情 | Detail | **KEEP** | `product-design/final-ui/RP-09_Unknown_Item_Detail.png` | 默认示例为“调查中”；其他状态不创建新 Route |
| RP-10 | 业务规则详情 | Detail | **KEEP** | `product-design/final-ui/RP-10_Business_Rule_Detail.png` | MVP 不增加业务规则独立列表页 |
| RP-11 | 集成关系详情 | Detail | **KEEP** | `product-design/final-ui/RP-11_Integration_Detail.png` | MVP 不增加集成关系独立列表页 |

明确不存在的 Route：Column Detail、Edit System、Edit Business Function、Edit Database Knowledge、Edit Business Rule、Edit Integration、Add Evidence、Add Relationship、Add Confirmation、Add Finding、Global Search、Create Knowledge Object。

## 2. Detail Pages

Detail Page 均沿用 `Main Content + 对象级 Context Rail + Detail Drawer`。本表引用 Route ID，不新增 Golden。

| Route ID | Detail Page | Main Content 职责 | Context Rail 职责 |
| --- | --- | --- | --- |
| RP-03 | 系统详情 | 系统概览、知识概况、业务功能、数据库对象、集成、代码与系统级待确认事项 | 仅系统级关联系统、集成概况、主数据库、高优先级待确认事项与知识缺口 |
| RP-05 | 业务功能详情 | 功能概览、业务过程、关联数据、业务规则、集成、证据与待确认事项 | 仅功能级调用入口、相邻功能、集成摘要与开放缺口 |
| RP-07 | 数据库对象详情 | Table / View 概览、数据库元数据、Column Table | 仅 Table-level relationships / gaps |
| RP-09 | 待确认事项详情 | 问题、Finding、Evidence、Resolution、Knowledge Update 与 Activity | 仅事项级相关对象、知识影响与调查摘要 |
| RP-10 | 业务规则详情 | Description、Condition、Result、Input、Fields、Integrations、Evidence | 仅规则级关系与开放缺口 |
| RP-11 | 集成关系详情 | 参与系统、方向、端点、消息契约、功能、数据、Evidence | 仅集成级参与方、关联功能、相关数据与开放缺口 |

## 3. Drawers

Drawer 原位替换，禁止嵌套或叠加。1440px / 1366px 打开 Drawer 时优先隐藏 Context Rail。

| ID | Drawer | 状态 | Golden UI Reference | 归属 / 说明 |
| --- | --- | --- | --- | --- |
| DR-01 | 业务功能 Object Preview Drawer | **KEEP** | `product-design/final-ui/DR-01_Business_Function_Object_Preview.png` | System Detail 中预览业务功能 |
| DR-02 | Relationship Detail Drawer | **KEEP** | `product-design/final-ui/RP-05_Business_Function_Detail.png` | canonical Golden 为 RP-05 中的内嵌关系 Drawer；不复制第二份图片 |
| DR-03 | Column Detail Drawer | **KEEP** | `product-design/final-ui/DR-03_Column_Detail.png` | Column Detail 不是 Route；显示字段级知识、证据、已知值、关系与待确认事项 |
| DR-04 | Integration Preview Drawer | **KEEP** | `product-design/final-ui/DR-04_Integration_Preview.png` | 预览后可进入 RP-11 |
| DR-05 | Business Rule Preview Drawer | **KEEP** | `product-design/final-ui/DR-05_Business_Rule_Preview.png` | 预览后可进入 RP-10 |
| DR-06 | Add Relationship Drawer | **KEEP** | `product-design/final-ui/DR-06_Add_Relationship.png` | 关系类型 → 目标搜索 → Preview → 保存 |
| DR-07 | Relationship Saved / Detail Drawer | **KEEP** | `product-design/final-ui/DR-07_Relationship_Saved_Detail.png` | 新关系初始为“未知”；提供添加证据入口 |
| DR-08 | Add Evidence Drawer | **KEEP** | `product-design/final-ui/DR-08_Add_Evidence.png` | 统一 Evidence Authoring Golden；可绑定对象、Claim、关系、Finding 或待确认事项 |
| DR-09 | Evidence Detail Drawer | **KEEP** | `product-design/final-ui/DR-09_Evidence_Detail.png` | 显示证据详情与显式 `未知 → 推断` 决策 |
| DR-10 | Add Human Confirmation Drawer | **KEEP** | `product-design/final-ui/DR-10_Add_Human_Confirmation.png` | 人工确认属于 Evidence；显式预览 `推断 → 已确认` |
| DR-11 | Edit Database Knowledge Drawer | **KEEP** | `product-design/final-ui/DR-11_Edit_Database_Knowledge.png` | 编辑 Column 业务知识、已知值与证据；数据库元数据只读 |
| DR-12 | Edit Business Rule Drawer | **KEEP** | `product-design/final-ui/DR-12_Edit_Business_Rule.png` | 关系通过 DR-06 单独维护 |
| DR-13 | Edit Integration Drawer | **KEEP** | `product-design/final-ui/DR-13_Edit_Integration.png` | 参与方关系通过 DR-06 单独维护 |

## 4. Dialogs / Overlays

| ID | Dialog / Overlay | 状态 | Golden UI Reference | 说明 |
| --- | --- | --- | --- | --- |
| OV-01 | Global Search — 最近搜索 | **KEEP** | `product-design/final-ui/OV-01_Global_Search_Recent.png` | 全局入口默认状态 |
| OV-02 | Global Search — 分组结果 | **KEEP** | `product-design/final-ui/OV-02_Global_Search_Results.png` | 按对象类型分组，支持键盘选择 |
| OV-03 | Global Search — 无结果 | **KEEP** | `product-design/final-ui/OV-03_Global_Search_No_Result.png` | 提供查询恢复路径 |
| OV-04 | Create Knowledge Object — 类型选择 | **KEEP** | `product-design/final-ui/OV-04_Create_Knowledge_Object_Type.png` | `+ 新增`后的全局对象选择 Overlay |
| OV-05 | Create Knowledge Object — 最小信息 | **KEEP** | `product-design/final-ui/OV-05_Create_Knowledge_Object_Minimum_Form.png` | Focused Form / Dialog；其它对象复用同一结构 |

## 5. Edit States

这些状态不是 Route。保存内容不会自动改变 Knowledge Status。

| ID | Edit State | 状态 | Golden UI Reference | 模式 |
| --- | --- | --- | --- | --- |
| ES-01 | Edit System | **KEEP** | `product-design/final-ui/ES-01_Edit_System_Inline.png` | Detail Overview Section Inline Edit |
| ES-02 | Edit Business Function | **KEEP** | `product-design/final-ui/ES-02_Edit_Business_Function_Inline.png` | Detail Overview Section Inline Edit |
| ES-03 | Edit Database Knowledge | **KEEP** | `product-design/final-ui/DR-11_Edit_Database_Knowledge.png` | canonical Golden 为 DR-11；Drawer Edit |
| ES-04 | Edit Business Rule | **KEEP** | `product-design/final-ui/DR-12_Edit_Business_Rule.png` | canonical Golden 为 DR-12；Drawer Edit |
| ES-05 | Edit Integration | **KEEP** | `product-design/final-ui/DR-13_Edit_Integration.png` | canonical Golden 为 DR-13；Drawer Edit |

## 6. Workflow States

### 6.1 Unknown Item Workflow

四个事项状态属于同一个 RP-09，不是四个页面，也不增加 Route。

| ID | 状态 / 操作 | 状态 | Golden UI Reference | 说明 |
| --- | --- | --- | --- | --- |
| WF-00 | 待处理 | **KEEP** | `product-design/final-ui/RP-09_Unknown_Item_Detail.png` | 沿用 RP-09 页面结构，仅切换状态轴与允许操作；没有独立截图 |
| WF-01 | 调查中 | **KEEP** | `product-design/final-ui/RP-09_Unknown_Item_Detail.png` | canonical Golden 为 RP-09 的默认工作状态 |
| WF-02 | Add Finding | **KEEP** | `product-design/final-ui/WF-02_Unknown_Item_Add_Finding.png` | Main Content Inline State；Finding 不等于 Resolution |
| WF-03 | Add Evidence | **KEEP** | `product-design/final-ui/DR-08_Add_Evidence.png` | canonical Golden 为 DR-08；旧的 Unknown Item 内联 Evidence 编辑器已被替代 |
| WF-04 | Resolution + Knowledge Update Preview | **KEEP** | `product-design/final-ui/WF-04_Unknown_Item_Resolution_Update_Preview.png` | Main Content Inline State |
| WF-05 | 结论已确认，等待关闭 | **KEEP** | `product-design/final-ui/WF-05_Unknown_Item_Conclusion_Confirmed.png` | 知识更新已应用，但事项仍未关闭 |
| WF-06 | 已关闭 | **KEEP** | `product-design/final-ui/WF-06_Unknown_Item_Closed.png` | 同一 Detail 的只读状态，可显式重新打开 |

### 6.2 Knowledge Status Progression

Knowledge Status 与 Unknown Item Status 独立。进展组件不是 Tab，禁止点击节点直接切换。

| ID | 状态推进 | 状态 | Golden UI Reference | 明确操作 |
| --- | --- | --- | --- | --- |
| WF-07 | 新对象 / 新关系为未知 | **KEEP** | `product-design/final-ui/DR-07_Relationship_Saved_Detail.png` | canonical Golden 为 DR-07；创建或保存关系后保持“未知” |
| WF-08 | 未知 → 推断 | **KEEP** | `product-design/final-ui/DR-09_Evidence_Detail.png` | canonical Golden 为 DR-09；Evidence 保存后由用户选择“标记为推断” |
| WF-09 | 推断 → 已确认 | **KEEP** | `product-design/final-ui/DR-10_Add_Human_Confirmation.png` | canonical Golden 为 DR-10；Human Confirmation 与知识影响预览后明确确认 |

### 6.3 Create / Authoring Workflow

标准完整知识完善路径（非强制）：`OV-04 → OV-05 → DR-06 → DR-08 → DR-09 → DR-10`。  
即：选择对象 → 最小信息创建 → 添加显式关系 → 添加证据 → 明确标记推断 → 人工确认 → 明确标记已确认。

最小信息创建完成后，对象即可保存并保持 Knowledge Status“未知”。用户可以在此结束本次操作并返回对象详情。Relationship、Evidence、`未知 → 推断`、Human Confirmation 与`推断 → 已确认`均为可在后续渐进补充的独立操作，不是创建成功的前置条件，也不会自动串联执行。

### 6.4 人员快照与权限范围

- MVP 不增加人员、角色或权限管理 Route，也不建立人员中心依赖。
- 创建人、调查人、Evidence 提供人、人工确认人和业务专家等信息，第一版保存为事件发生时的人员快照。
- 快照至少包含：人员名称、角色 / 身份、发生时间；需要时可补充团队、来源或备注。
- 快照只用于知识来源、调查过程和责任上下文展示，不在 MVP 中承担权限判定或组织架构管理。
- 后续如接入统一身份系统，可增加关联标识，但不得覆盖已经保存的历史姓名、角色 / 身份与时间快照。

## 7. Deprecated Prototypes

### 7.1 已否决视觉方向

| 原型组 | 标记 | 原因 |
| --- | --- | --- |
| 方案 A：深蓝色顶部导航 | **DEPRECATED** | 非最终 Application Shell |
| 方案 C：深色左侧导航 | **DEPRECATED** | 非最终浅色 Baseline |
| 最初方案 B 单图与早期变体 | **SUPERSEDED** | 方向保留，但已由正式页面 Golden 资产替代 |
| 所有新视觉方向、深色模式、传统 CRUD 后台变体 | **DEPRECATED** | 与固定 Design Baseline 冲突 |

### 7.2 Business Function 旧语言版本

以下文件均为 **SUPERSEDED**，由同名 `_ZH-CN` 文件替代：

- `product-design/Business_Function_Detail_Relation_Drawer.png`
- `product-design/Business_Function_Detail_Column_Drawer.png`
- `product-design/Business_Function_Detail_Lower_Content.png`
- `product-design/qa/Baseline_vs_Business_Function_Detail.png`

### 7.3 Unknown Item 旧状态版本

以下文件均为 **SUPERSEDED**：

- `product-design/unknown-item-detail/01_Investigating.png`
- `product-design/unknown-item-detail/02_Add_Finding.png`
- `product-design/unknown-item-detail/03_Add_Evidence.png`
- `product-design/unknown-item-detail/03_Add_Evidence_StatusTerm-v2.png` — 被统一 DR-08 替代
- `product-design/unknown-item-detail/04_Resolution_Knowledge_Update_Preview.png`
- `product-design/unknown-item-detail/05_Confirmed_Ready_To_Close.png`
- `product-design/unknown-item-detail/06_Closed.png`
- `product-design/unknown-item-detail/Unknown_Item_Detail_Review_Board.png`
- `product-design/unknown-item-detail/Unknown_Item_Detail_Review_Board_v2.png`
- `product-design/unknown-item-detail/Unknown_Item_Detail_Review_Board_v3.png` — 其中 Inline Add Evidence 已被 DR-08 替代，因此不再作为 Golden 或完整流程依据

### 7.4 Database Detail 中间实现与 QA 工作稿

| 文件 | 标记 | 替代 / 用途 |
| --- | --- | --- |
| `implementation-1920.png` | **SUPERSEDED** | RP-07 |
| `implementation-1920-pass2.png` | **SUPERSEDED** | RP-07 |
| `implementation-1440.png` | **SUPERSEDED** | `implementation-1440-final.png` |
| `comparison-full-pass1.png` | **DEPRECATED** | QA 工作稿，不是 UI 来源 |
| `comparison-full-pass2.png` | **DEPRECATED** | QA 工作稿，不是 UI 来源 |
| `comparison-drawer-pass1.png` | **DEPRECATED** | QA 工作稿，不是 UI 来源 |
| `comparison-drawer-pass2.png` | **DEPRECATED** | QA 工作稿，不是 UI 来源 |
| `comparison-table-rail-pass1.png` | **DEPRECATED** | QA 工作稿，不是 UI 来源 |
| `comparison-table-rail-pass2.png` | **DEPRECATED** | QA 工作稿，不是 UI 来源 |

保留但不是 Golden：

- `implementation-1440-final.png` — **KEEP**，仅作为 1440px 响应式辅助参考
- `implementation-1366.png` — **KEEP**，仅作为 1366px 响应式辅助参考

### 7.5 原始 ImageGen 输出

以下整个目录标记为 **DEPRECATED**，不得被后续 Codex 直接引用：

`C:\Users\wang\.codex\generated_images\019ff005-d367-7f63-88a5-12d00dd64d25\exec-*.png`

其中包括方案 A / B / C、生成中间稿、错误状态稿与已复制到项目目录的重复文件。只有本 Inventory 中明确列出的项目内资产可以作为受控参考。

## 8. KEEP 但非 Golden 的评审与 QA 资产

以下文件仅用于查看多状态全貌或核对一致性，不得替代上文单一 Golden：

- `product-design/knowledge-discovery-navigation/Knowledge_Discovery_Navigation_Review_Board.png`
- `product-design/knowledge-discovery-navigation/Global_Search_States_Review_Board.png`
- `product-design/system-detail/System_Detail_Review_Board.png`
- `product-design/knowledge-object-authoring/Knowledge_Objects_Review_Board.png`
- `product-design/knowledge-object-authoring/Authoring_Flows_Review_Board.png`
- `product-design/knowledge-object-authoring/Edit_Patterns_Review_Board.png`
- `product-design/qa/Baseline_vs_Business_Function_Detail_ZH-CN.png`
- `product-design/qa/Baseline_vs_System_Detail.png`
- `product-design/qa/Baseline_vs_Unknown_Item_Detail.png`
- `product-design/qa/Baseline_vs_Knowledge_Discovery_Navigation.png`
- `product-design/qa/Baseline_vs_Knowledge_Object_Authoring.png`

`product-design/system-detail/02_System_Lower_Content.png` 为 **KEEP 辅助状态**，只补充 RP-03 下半段内容；RP-03 的唯一 Golden 仍是 `01_System_Overview.png`。

`product-design/Business_Function_Detail_Lower_Content_ZH-CN.png` 为 **KEEP 辅助状态**，只补充 RP-05 的业务规则、集成、证据与待确认事项下半段；RP-05 的唯一 Golden 仍是 `Business_Function_Detail_Relation_Drawer_ZH-CN.png`。

## 9. 非 UI Reference 文档

| 文档 | 状态 | 用途 |
| --- | --- | --- |
| `System_Knowledge_Hub_MVP_Design_Baseline.md` | **KEEP / DESIGN FROZEN** | 术语、原则、职责与响应式规范 |
| `System_Knowledge_Hub_MVP_Final_UI_Inventory.md` | **KEEP / DESIGN FROZEN** | 唯一 UI 资产索引与开发引用入口 |
| 各 `*_Product_Design.md` | **KEEP** | 页面语义与交互说明；资产选择以本 Inventory 为准 |
| `Legacy_Knowledge_Hub_MVP_Design.md` | **KEEP** | 原始需求文档，不是 UI Golden |
| `Legacy Knowledge Hub — Product Design 原型设计任务.md` | **KEEP** | 原型任务文档，不是 UI Golden |

## 10. 后续 Codex 开发读取顺序

1. 先读 `System_Knowledge_Hub_MVP_Final_UI_Inventory.md`，确定 Route / Drawer / State 与 Golden 资产。
2. 再读 `System_Knowledge_Hub_MVP_Design_Baseline.md`，获取设计原则、中文术语与三层职责。
3. 只打开当前开发对象在 `product-design/final-ui/` 中对应的 Golden UI Reference。
4. 需要语义细节时，再读取对应 Product Design 文档。
5. 不扫描或引用 `generated_images/exec-*.png`、SUPERSEDED 文件或 DEPRECATED 文件。
