# System Knowledge Hub MVP Design Baseline

状态：**CONFIRMED / DESIGN FROZEN**  
生效日期：2026-08-11

## 基线范围

当前已确认的 System Detail、Business Function Detail、Database Table Detail、Table-level Context Rail、Column Detail Drawer 与 Unknown Item Detail 正式作为 System Knowledge Hub MVP 的产品设计基线。

后续 MVP 页面必须继承以下已确认结构，不再重新探索新的整体布局或视觉方案：

- Application Shell：浅色桌面界面、左侧全局导航、顶部全局搜索。
- Main Content：承载当前知识对象本身的主要信息，保持高信息密度与清晰扫描路径。
- Context Rail：仅承载当前对象层级的关系与缺口摘要，用于快速探索，不复制 Main Content 的完整详情。
- Detail Drawer：承载当前选中子对象的完整上下文详情；一次只打开一层，切换对象时原位替换，不叠加 Drawer。
- Drawer 类型：保留 Object Detail Drawer 与 Relationship Detail Drawer。Object Detail Drawer 展示字段等具体知识对象；Relationship Detail Drawer 展示两个知识对象之间的关系。
- Section hierarchy：高频、高价值信息优先；低频区块允许折叠。
- Table style：紧凑、技术化、适合批量扫描；Hover 与 Selected 状态明确。
- Evidence presentation：证据是一级知识对象，必须说明“为什么我们相信该描述、规则或状态”。
- Unified Evidence model：Code Reference 是 Evidence 类型之一，不建立与 Evidence 平行的重复知识体系。
- Knowledge Status presentation：知识进展为只读的“未知 → 推断 → 已确认”，不能通过点击阶段直接切换，状态变更必须由明确操作触发。
- Unknown Item presentation：待确认事项是正式数据，需要展示问题、关联对象、优先级、状态和时间信息，但不在 Main Content、Context Rail、Drawer 之间重复完整内容。

## 固定设计原则

- Read First
- Edit In Context
- Progressive Documentation
- Relationship First
- Unknown Is Data
- Evidence as first-class knowledge
- Desktop First

## Progressive Authoring 基线

- 最小信息创建完成后，对象即可保存，Knowledge Status 保持“未知”。
- 标准完整知识完善路径为：创建对象 → 添加 Relationship → 添加 Evidence → 明确标记为推断 → 添加 Human Confirmation → 明确标记为已确认。
- 上述路径是推荐的完整完善路径，不是强制创建向导。Relationship、Evidence、推断、Human Confirmation 均允许在后续独立、渐进补充。
- 添加或保存 Evidence 不自动推进 Knowledge Status；所有状态变化必须通过明确操作完成。

## MVP 人员与权限范围

- MVP 不增加人员、角色或权限管理 Route，不建立独立人员中心。
- 创建人、调查人、Evidence 提供人、人工确认人、业务专家等第一版保存人员名称、角色 / 身份、时间及必要来源信息的事件快照。
- 人员快照用于表达知识来源和调查上下文，不承担权限判定或组织架构管理。
- 后续接入统一身份系统时，可以新增关联标识，但必须保留历史快照。

## 三层职责

### Main Content

回答“当前对象本身是什么”。承载概览、核心业务内容、主要结构与对象自身的事实。

### Context Rail — 关系与缺口

回答“当前对象和什么有关，还有什么没有搞清楚”。只展示与当前对象同层级的关系和缺口摘要，并提供继续探索入口。

### Detail Drawer

回答“当前选中的具体子对象详细是什么”。承载该子对象的业务知识、证据、状态、关系和待确认事项。Drawer 中不复制 Main Content 或 Context Rail 的完整内容。

## Business Function Detail 基线

- Main Content 展示当前业务功能本身的核心知识。
- Function-level“关系与缺口”Context Rail 只展示调用方与入口、相邻业务功能、集成关系和开放待确认事项。
- Detail Drawer 根据当前选中的关联对象展示 Object Detail 或 Relationship Detail。
- Business Process 使用简洁、可扫描的步骤表达，不引入 BPMN 或复杂流程编辑器。
- Evidence 是统一知识依据模型；Code Reference 仅作为其中一种证据类型。

## Unknown Item Detail 基线

- 保留“发现问题 → 创建待确认事项 → 调查 → 添加调查发现 → 添加证据 → 形成结论 → 预览知识更新 → 应用知识更新 → 更新关联 Knowledge Status → 关闭待确认事项”的完整闭环。
- Main Content 承载问题、调查发现、证据、结论、知识更新预览 / 结果和活动记录。
- Item-level Context Rail 承载相关对象、知识影响、调查摘要和事项摘要，不复制 Main Content 完整记录。
- 添加调查发现、添加证据和填写结论采用 Main Content 内联编辑，不引入第三种编辑 Drawer。
- 待确认事项的“结论已确认”与关联知识对象的 Knowledge Status“已确认”是两个独立概念。

## System Detail 基线

- Main Content 展示系统本身的概览、知识概况、业务功能、数据库对象、集成关系、代码 / 仓库与系统级待确认事项。
- 知识概况使用单一分段条与文字计数表达梳理程度，不使用 KPI 卡片或复杂 BI 图表。
- System-level“关系与缺口”Context Rail 只展示关联系统、集成概况、主数据库、高优先级待确认事项和知识缺口摘要。
- 单击业务功能等具体对象时先打开 Object Detail Drawer；用户通过明确操作进入完整 Detail 页面。
- System Detail 是跨知识对象的上层入口，但不把所有对象详情堆叠成超级详情页。

## 知识发现与导航页面约束

- 总览与各类 List Page 只负责 Find、Filter、Browse、Navigate，不复制 Detail Page 的大量知识内容。
- List Page 回答“我要找什么”；Detail Page 回答“它是什么”。
- 搜索和列表结果必须携带系统上下文、对象类型、短描述和知识状态，不能只展示对象名称。
- 点击列表或搜索结果后进入已确认的 Detail 页面；需要原位预览时复用已确认的 Detail Drawer 模式。

## 知识发现与导航页面基线

以下页面已经确认并正式纳入 MVP Design Baseline：

- Dashboard / 总览
- Global Search / 全局搜索
- Systems List / 系统列表
- Business Functions List / 业务功能列表
- Database Objects List / 数据库对象列表
- Unknown Items List / 待确认事项列表

固定交互规则：

- 总览使用紧凑的知识总览计数、单一知识进展分段条、需要关注与最近整理，不使用 KPI 卡片或复杂 BI 图表。
- List Page 默认不显示对象级 Context Rail；用户进入具体知识对象后才恢复 Context Rail 与 Detail Drawer。
- 系统、业务功能、数据库对象和待确认事项列表行直接进入对应已确认 Detail 页面。
- 数据库对象页的 Database / Schema 导航属于 Main Content 内部浏览结构，不是 Context Rail。
- 全局搜索按知识对象类型分组，每条结果显示系统上下文、对象类型、短描述与状态。
- 全局搜索保留键盘选择、最近搜索、最近访问和无结果恢复路径。
- 字段搜索结果进入 Database Table Detail，并自动打开对应 Column Detail Drawer。

## 响应式桌面规则

- 1920px：允许同时展示 Navigation、Main Content、Context Rail 与 Detail Drawer。
- 1440px / 1366px：Detail Drawer 打开时优先保证 Main Content 与 Drawer 宽度，Context Rail 自动收起或临时隐藏。
- 不把四栏强行压缩进窄桌面视口。

## 简体中文界面规范

从本基线开始，所有 Product Design 原型的页面标题、导航、按钮、字段标签、状态说明、空状态和提示文案统一使用简体中文。

顶栏中文产品名称 **MUST** 显示为“系统知识中心”；正式英文名称 **MUST** 使用 `System Knowledge Hub`。历史 Golden 截图中出现的“遗留系统知识中心”或 `Legacy Knowledge Hub` 仅为旧图像文字，开发实现时不得沿用，也不因此重新生成 Golden 图片。

技术标识保持原文，包括数据库对象、字段名、数据类型、文件名、类名、方法名、SQL 名称、MQ 和 API 技术名称，例如 `MES.TABLE_EQP`、`STATE_FLAG`、`VARCHAR2(20)`、`EquipmentStatusService.cs`、`QueryEquipmentStatus.sql`、`RabbitMQ`、`HTTP API`。

固定术语：

| 产品概念 | 简体中文 |
| --- | --- |
| Dashboard | 总览 |
| Systems | 系统 |
| Functions | 业务功能 |
| Database | 数据库 |
| Unknown Items | 待确认事项 |
| Relationships & gaps | 关系与缺口 |
| Business Knowledge | 业务知识 |
| Evidence | 证据 |
| Known Values | 已知值 |
| Database Metadata | 数据库元数据 |
| Used By | 被以下功能使用 |
| Business Rules | 业务规则 |
| Integrations | 集成关系 |

知识状态：`未知`、`推断`、`已确认`。  
知识进展：`未知 → 推断 → 已确认`。  
改写状态：`保留`、`调整`、`移除`、`待确认`。  
待确认事项状态：`待处理`、`调查中`、`结论已确认`、`已关闭`。  
优先级：`高`、`中`、`低`。

## 视觉参考

已确认实现截图（仅作为布局、密度、组件结构与视觉语言参考）：

`product-design/final-ui/RP-07_Database_Object_Detail.png`

该截图中的英文产品界面文案不再作为术语基线；本文件的简体中文规范具有更高优先级。
