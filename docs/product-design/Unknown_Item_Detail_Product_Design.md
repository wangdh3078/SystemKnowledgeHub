# 待确认事项详情 — Product Design

状态：第一版可评审原型  
设计基线：System Knowledge Hub MVP Design Baseline  
界面语言：简体中文（技术标识保持原文）  
范围：仅待确认事项详情

## 原型文件

1. 调查中主状态  
   `product-design/unknown-item-detail/01_Investigating_StatusTerm-v2.png`
2. 内联添加调查发现  
   `product-design/unknown-item-detail/02_Add_Finding_StatusTerm-v2.png`
3. 内联添加证据  
   `product-design/unknown-item-detail/03_Add_Evidence_StatusTerm-v2.png`
4. 填写结论与知识更新预览  
   `product-design/unknown-item-detail/04_Resolution_Knowledge_Update_Preview_StatusTerm-v2.png`
5. 结论已确认、知识已更新、等待关闭  
   `product-design/unknown-item-detail/05_Conclusion_Confirmed_Ready_To_Close.png`
6. 已关闭只读状态  
   `product-design/unknown-item-detail/06_Closed_StatusTerm-v2.png`

六状态评审板：  
`product-design/unknown-item-detail/Unknown_Item_Detail_Review_Board_v3.png`

视觉基线对比：  
`product-design/qa/Baseline_vs_Unknown_Item_Detail.png`

## 页面目标

待确认事项详情把一个未知问题转化为可追溯、可应用的知识更新：

发现问题  
→ 创建待确认事项  
→ 查看问题上下文  
→ 开始调查  
→ 添加调查发现  
→ 添加证据  
→ 形成结论  
→ 预览并应用知识更新  
→ 必要时更新关联对象的知识状态  
→ 标记事项结论已确认  
→ 显式关闭事项。

本原型使用 `UNK-023` 和问题“`STATE_FLAG=30` 具体表示什么？”演示完整闭环。

## 信息架构

### Main Content

Main Content 展示当前待确认事项本身：

- 问题：问题、上下文、优先级、状态、创建时间、更新时间。
- 待确认事项状态：`待处理 → 调查中 → 结论已确认 → 已关闭`。
- 调查发现：记录调查过程中的观察结果。
- 证据：统一展示支撑调查发现或结论的依据。
- 结论：记录最终确认结果。
- 知识更新预览 / 结果：明确更新目标、更新前内容、更新后内容和知识状态变化。
- 活动记录：简单记录创建、状态变化、调查发现、证据、结论、知识更新和关闭。

### Item-level Context Rail — 相关对象与影响

Context Rail 只展示事项级上下文摘要，不复制 Main Content 完整记录：

- 相关对象：系统、业务功能、表、字段、业务规则、集成关系。
- 知识影响：目标知识对象、待补充已知值、关联对象知识状态。
- 调查摘要：调查发现数量、证据数量、人工确认数量和最近更新时间。
- 最近活动 / 事项摘要：只显示简要状态，不复制完整活动表。

### Detail Drawer

点击 Context Rail 中的关联对象时，沿用基线中的两类 Drawer：

- Object Detail Drawer：例如查看 `MES.TABLE_EQP.STATE_FLAG` 的字段知识。
- Relationship Detail Drawer：例如查看 `UNK-023` 与 `STATE_FLAG` 之间的“需要补充已知值”关系。

Drawer 原位替换，不叠加。添加调查发现、添加证据和填写结论采用 Main Content 内联编辑，不引入第三种编辑 Drawer。

## 三种知识记录的区别

| 类型 | 回答的问题 | 是否最终结论 | 示例 |
| --- | --- | --- | --- |
| 调查发现 | 调查过程中观察到了什么 | 否 | 代码分支将 `30` 映射为 `DisplayStatus.Unknown` |
| 证据 | 为什么相信这条发现或结论 | 否 | `EquipmentStatusService.cs : line 184`、数据库样本、人工确认 |
| 结论 | 最终确认的业务含义是什么 | 是 | `30 = Unknown / Offline` |

调查发现和证据不会自动修改知识。只有显式应用结论的知识更新操作才能修改关联对象。

## 统一 Evidence 模型

页面复用统一证据模型：

- 代码引用
- SQL
- 数据库样本
- 数据库注释
- API
- MQ 消息
- 现有文档
- 人工确认

Code Reference 只是“代码引用”证据类型，不建立平行的 Code Reference 知识体系。

## 两套状态模型

### 待确认事项状态

`待处理 → 调查中 → 结论已确认 → 已关闭`

- 待处理：已记录问题，尚未开始正式调查。
- 调查中：允许添加调查发现、证据和结论草稿。
- 结论已确认：结论已形成，知识更新已应用，但事项尚未关闭。
- 已关闭：闭环完成，页面以只读为主；只能通过“重新打开”显式改变状态。

状态进展是只读生命周期表达，不是 Tab。所有状态变化通过“开始调查”“应用知识更新并标记结论已确认”“关闭待确认事项”或“重新打开”等明确操作完成。

### 关联知识对象的 Knowledge Status

`未知 → 推断 → 已确认`

该状态属于 `MES.TABLE_EQP.STATE_FLAG` 等知识对象，不属于待确认事项。原型始终使用完整标签“关联字段知识状态”或“字段知识状态”，避免与待确认事项状态混淆。

本示例中：

- 待确认事项：`调查中 → 结论已确认 → 已关闭`
- `STATE_FLAG` 字段知识状态：`推断 → 已确认`

事项关闭不会再次改变字段知识状态。

## 六个关键状态

### 1. 调查中

- 展示问题和完整上下文。
- 展示只读事项状态进展。
- 展示已有调查发现、证据、空结论和活动记录。
- Context Rail 展示相关对象与潜在知识影响。

### 2. 添加调查发现

- 在“调查发现”区块原位展开编辑器。
- 支持记录发现内容、关联对象、发现日期、关联证据和补充说明。
- 明确提示“调查发现不等于最终结论”。

### 3. 添加证据

- 在“证据”区块原位展开统一证据编辑器。
- 示例选择“人工确认”，并展示全部统一 Evidence 类型。
- 证据可关联调查发现，但不会直接成为 Resolution。

### 4. 填写结论与知识更新预览

- 结论：`30 = Unknown / Offline`。
- 结论依据：代码引用、数据库样本、SQL、人工确认。
- 应用前明确展示知识更新目标和前后差异。
- 主操作“应用知识更新并标记结论已确认”同时应用知识更新，并把待确认事项从调查中改为结论已确认。
- 该操作不会自动关闭事项。

### 5. 结论已确认，等待关闭

- 结论与知识更新结果只读。
- `MES.TABLE_EQP.STATE_FLAG` 已知值更新为 `30 → Unknown / Offline`。
- 字段知识状态由推断更新为已确认。
- 待确认事项状态为结论已确认。
- 用户通过独立操作“关闭待确认事项”完成最后一步。

### 6. 已关闭

- 状态进展完整显示为已关闭。
- 展示最终结论、已应用的知识更新、关闭原因和完整活动记录。
- 页面以只读为主，仅保留“查看关联知识”和“重新打开”。

## 最小完整闭环

`STATE_FLAG=30 具体表示什么？`  
→ 状态改为调查中  
→ 记录代码调查发现  
→ 添加 `EquipmentStatusService.cs : line 184` 代码引用  
→ 添加 `MES.TABLE_EQP · 24 条记录` 数据库样本  
→ 添加赵工人工确认  
→ 形成结论 `30 = Unknown / Offline`  
→ 预览更新 `MES.TABLE_EQP.STATE_FLAG`  
→ 已知值新增 `30 → Unknown / Offline`  
→ 字段知识状态 `推断 → 已确认`  
→ 待确认事项状态变为结论已确认  
→ 显式关闭待确认事项。

## 响应式桌面规则

- 宽桌面：同时展示 Navigation、Main Content 与 Item-level Context Rail。
- 1440px / 1366px：Main Content 保留主要编辑宽度；Context Rail 可压缩或按基线规则临时隐藏。
- 打开 Object Detail 或 Relationship Detail Drawer 时，窄桌面优先保证 Main Content 与 Drawer，隐藏 Context Rail。

## 评审前检查

- 通过：Application Shell、浅色导航、内容密度、表格、分隔和 Context Rail 与现有基线一致。
- 通过：Main Content 与 Context Rail 职责不重复。
- 通过：调查发现、证据、结论和知识更新的语义明确。
- 通过：Code Reference 被纳入统一 Evidence 模型。
- 通过：待确认事项状态和关联知识对象状态在名称、位置和操作上明确分离。
- 通过：状态改变必须使用明确操作，进展条不可点击。
- 通过：结论已确认不会自动关闭，保留独立关闭动作。
- 通过：活动记录简单、按时间排列，不包含审批流或 Workflow Engine。
- 通过：六个状态覆盖用户要求的最小知识闭环。
- 通过：所有产品 UI 文案使用简体中文，技术标识保持原文。

## 本轮需要确认的设计决策

1. Item-level Context Rail 使用“相关对象与影响”，固定展示相关对象、知识影响、调查摘要和最近活动 / 事项摘要。
2. 添加调查发现、添加证据、填写结论统一使用 Main Content 内联编辑，不占用 Object / Relationship Detail Drawer。
3. “应用知识更新并标记结论已确认”同时执行知识更新与事项状态变化，但不自动关闭事项。
4. “结论已确认”与“已关闭”保留为两个独立状态，关闭必须通过独立操作完成。
5. 活动记录只记录事实事件，不设计审批人、审批节点或回退流程。
6. `30 = Unknown / Offline` 仅作为当前原型示例，最终术语需要由领域专家确认。

## 生成方式

使用 Product Design 工作流与内置 ImageGen，以已确认的简体中文 Business Function Detail 为固定视觉参考，生成同一页面的六个连续交互状态：调查中、添加调查发现、添加证据、结论与知识更新预览、结论已确认待关闭、已关闭。

所有生成提示均要求保留已确认布局、浅色高密度 Developer Knowledge Workspace、统一 Evidence 模型、两套状态体系的明确区分、简体中文产品文案和原始英文技术标识，并禁止生成新视觉方向、CRUD 后台、BPMN、审批流或 Workflow Engine。
