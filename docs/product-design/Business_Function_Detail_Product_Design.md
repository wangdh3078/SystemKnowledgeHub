# Business Function Detail — Product Design

状态：第一版可评审原型  
设计基线：System Knowledge Hub MVP Design Baseline  
界面语言：简体中文（技术标识保持原文）  
范围：仅 Business Function Detail

## 原型文件

1. 主状态：业务流程选中 `MES.TABLE_EQP`，打开数据关系详情抽屉  
   `product-design/Business_Function_Detail_Relation_Drawer_ZH-CN.png`
2. 关键交互状态：点击 `STATE_FLAG` 后，在原位切换为字段详情抽屉  
   `product-design/Business_Function_Detail_Column_Drawer_ZH-CN.png`
3. 页面下半段滚动状态：业务规则、集成关系、代码引用、功能证据、待确认事项  
   `product-design/Business_Function_Detail_Lower_Content_ZH-CN.png`

视觉基线对比：  
`product-design/qa/Baseline_vs_Business_Function_Detail_ZH-CN.png`

## 页面目标

用户打开 `Equipment Status Query` 后，可以快速回答：

1. 功能用途是什么。
2. 谁调用或使用它。
3. 输入是什么。
4. 输出是什么。
5. 业务处理过程是什么。
6. 使用了哪些 Table、View 和重要 Column。
7. 有哪些业务规则。
8. 调用了哪些 MQ、API 或其他集成。
9. 旧代码位于哪里。
10. 为什么相信当前业务逻辑描述。
11. 还有哪些内容未确认。

## 信息架构

### Header

- Function Name：`Equipment Status Query`
- 中文名称：设备状态查询
- System：`MES`
- Function Type：页面查询
- Rewrite Status：保留
- Knowledge Status：推断
- 明确操作：编辑

Header 先提供可读事实，不把页面变成表单。状态只能通过明确的编辑操作改变。

### Main Content

Main Content 回答“这个业务功能本身是什么”，包含完整的功能级内容：

- 概览：用途、用户 / 调用方、输入、输出。
- 业务流程：简单、可扫描的六步流程，不提供 BPMN 编辑能力。
- 关联数据：完整展示数据对象、类型、关系类型、关键字段和证据数量。
- 业务规则：展示规则、说明、知识状态和证据。
- 集成关系：展示类型、端点、方向、用途、知识状态和证据。
- 代码引用：展示代码仓库、文件、类、方法和行号。
- 功能证据：集中展示支持功能级知识的代码、SQL、数据库样本、现有文档和人工确认。
- 待确认事项：完整展示问题、关联对象、优先级、状态和更新时间。

### Function-level Context Rail — 关系与缺口

Context Rail 只回答“这个业务功能与什么有关，还有什么没有搞清楚”，仅放摘要和探索入口：

- 调用方与入口。
- 相邻业务功能。
- 集成关系摘要。
- 开放待确认事项摘要。

Context Rail 不重复 Main Content 中完整的关联数据、业务规则、集成明细、证据或待确认事项记录。

### Detail Drawer

Drawer 只回答“当前选中的子对象详细是什么”，不复制完整的功能级内容：

- 数据关系详情：展示 `Equipment Status Query → MES.TABLE_EQP` 的关系说明、关键字段、关系证据和相关待确认事项。
- 字段详情：点击 `STATE_FLAG` 后原位替换 Drawer，复用已确认的 Column Detail Drawer 模式，展示字段级业务知识、证据、待确认事项、数据库元数据、已知值和字段级关系。
- 不允许叠加第二层 Drawer。

## 关键交互状态

### 状态 0：阅读默认态

- Drawer 关闭。
- Main Content 展示功能概览、业务流程和完整内容。
- Context Rail 保持功能级关系与缺口摘要。
- 用户可纵向滚动查看业务规则、集成、代码、证据和待确认事项。

### 状态 1：查看数据关系

触发方式：点击业务流程中的“查询 `MES.TABLE_EQP`”步骤，或点击关联数据表格中的 `MES.TABLE_EQP` 行。

结果：

- 流程步骤和表格行保持 Selected 状态。
- 右侧打开“数据关系详情”Drawer。
- Drawer 展示关系说明、关键字段、证据和相关待确认事项。
- 主操作“打开数据表详情”进入已确认的 Database Table Detail。

### 状态 2：继续查看字段

触发方式：点击关系 Drawer 中的 `STATE_FLAG`，或点击“查看 `STATE_FLAG`”。

结果：

- 不打开嵌套 Drawer。
- 当前 Drawer 原位替换为 Column Detail Drawer。
- 展示 `STATE_FLAG` 的业务知识、只读知识进展、证据和字段级待确认事项。
- 返回时恢复之前的数据关系 Drawer 和选中状态。

### 状态 3：显式编辑

- “知识进展：未知 → 推断 → 已确认”是只读生命周期表达，不是 Tab。
- 点击阶段本身不会改变状态。
- 只有“编辑知识状态”等明确操作可以打开编辑流程。
- 编辑完成后保留原页面、滚动位置、选中对象和 Drawer 上下文。

## 证据层级

证据按所支持的知识对象归属，避免跨层重复：

- 功能级证据：支持功能用途、整体流程、集成和业务规则，位于 Main Content。
- 数据关系证据：支持“该功能如何使用 `MES.TABLE_EQP`”，位于数据关系 Drawer。
- 字段级证据：支持 `STATE_FLAG` 的业务含义和状态，位于 Column Detail Drawer。
- Context Rail 只显示证据存在性或数量，不显示完整证据明细。

## 双向探索闭环

`Equipment Status Query`  
→ 查看业务流程  
→ 选中“查询 `MES.TABLE_EQP`”  
→ 打开数据关系 Drawer  
→ 发现关键字段 `STATE_FLAG`  
→ 原位切换到 Column Detail Drawer  
→ 查看 `EquipmentStatusService.cs : line 184`、`QueryEquipmentStatus.sql`、数据库样本等证据  
→ 发现“`STATE_FLAG=30` 具体表示什么？”  
→ 继续查看相关字段知识，或打开 `MES.TABLE_EQP` 的 Database Table Detail。

从 Database Table Detail 的“被以下功能使用”返回 `Equipment Status Query` 时，使用同一条关系对象恢复该页面的选中步骤、选中数据行和关系 Drawer，实现双向探索。

## 桌面响应式规则

- 1920px：Navigation、Main Content、Function-level Context Rail 与 Detail Drawer 可以同时显示。
- 1440px / 1366px：Drawer 打开时临时隐藏 Context Rail，优先保证 Main Content 与 Drawer 的可读宽度。
- Drawer 关闭时恢复 Context Rail。

## 评审前检查

- 通过：与已确认基线并排检查后，Application Shell、浅色导航、列宽关系、字体层级、分隔、表格密度、Selected 状态、Context Rail 和 Drawer 视觉语言保持一致。
- 通过：Main Content、Function-level Context Rail、数据关系 Drawer、Column Detail Drawer 的对象层级职责明确。
- 通过：证据分别归属于功能、数据关系和字段层级，Context Rail 只提供摘要，不复制完整详情。
- 通过：知识进展为只读生命周期，状态改变必须使用“编辑知识状态”。
- 通过：顶栏产品名称显示为“系统知识中心”，所有产品 UI 文案使用简体中文；数据库对象、字段、类型、文件、类、方法、SQL、MQ 与 API 标识保持原文。历史截图中的旧产品名仅作为图像内容保留。
- 通过：三个状态共同覆盖页面上半段、Drawer 深入探索和页面下半段内容，不引入其他 MVP 页面。

## 本轮需要确认的设计决策

1. Function-level Context Rail 固定为“调用方与入口 / 相邻业务功能 / 集成关系摘要 / 开放待确认事项摘要”，不再放完整关联数据和业务规则。
2. 点击业务流程步骤与点击关联数据行共用同一条关系选中状态，并打开同一个数据关系 Drawer。
3. `STATE_FLAG` 采用 Drawer 原位替换，而不是叠加 Drawer 或跳转新页。
4. Main Content 下半段顺序固定为：业务规则 → 集成关系 → 代码引用 → 功能证据 → 待确认事项。
5. `RabbitMQ equipment.status.changed` 暂按“间接依赖 / 未知”示例表达，等待领域事实确认。

## 生成方式

使用 Product Design 工作流与内置 ImageGen，以已确认 Database Table Detail 截图作为布局和视觉参考，生成同一设计方向的三个交互状态：

1. 将页面内容替换为 `Equipment Status Query`，展示 `MES.TABLE_EQP` 数据关系 Drawer。
2. 仅将右侧 Drawer 原位替换为 `STATE_FLAG` 字段详情。
3. 保持同一页面和 Function-level Context Rail，展示 Drawer 关闭后的下半段滚动内容。

所有生成提示都要求保留基线布局、浅色技术工具视觉、高信息密度、简体中文产品文案和英文技术标识，并禁止生成新视觉方向、CRUD 后台、BPMN、BI 图表或深色导航。
