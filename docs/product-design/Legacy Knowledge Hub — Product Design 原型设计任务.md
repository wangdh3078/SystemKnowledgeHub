# Legacy Knowledge Hub — Product Design 原型设计任务

请先完整阅读项目根目录中的：

`docs/product-design/Legacy_Knowledge_Hub_MVP_Design.md`

该文档是本次产品原型设计的主要需求依据。

---

# 1. 当前任务目标

本阶段只完成：

**Legacy Knowledge Hub MVP 的产品界面与交互原型设计。**

当前阶段不要：

- 编写正式 .NET 8 后端代码
- 编写正式 Vue 3 前端代码
- 创建数据库
- 实现 API
- 自行增加业务模块
- 扩大 MVP 范围
- 为未来可能出现的需求提前设计复杂功能

先通过 Product Design 能力完成产品原型。

原型确认后，再进入 Codex 编码阶段。

---

# 2. 产品定位

Legacy Knowledge Hub 是一个轻量级的：

**旧系统梳理与知识登记工作台。**

它不是：

- Wiki
- CMS
- 通用文档管理系统
- CMDB
- 项目管理系统
- 工作流系统
- 数据治理平台
- 企业级知识平台

当前主要解决的问题：

旧系统缺少业务文档、数据库字段说明、业务规则说明以及系统关系说明。

使用者需要在阅读旧代码、SQL、数据库、MQ 和 API 的过程中，逐步把发现的信息登记下来，并建立关联。

最终能够快速回答：

- 这个系统是做什么的？
- 这个功能解决什么业务问题？
- 这个页面使用了哪些 Oracle 表？
- 这张表被哪些业务功能使用？
- 这个字段是什么意思？
- 哪些字段目前还不知道含义？
- 某个 MQ 消息会影响哪些数据库表？
- 某条业务规则在哪里使用？
- 某个功能对应旧代码的什么位置？
- 目前还有哪些业务问题没有确认？

---

# 3. MVP 核心功能

当前 MVP 只围绕以下核心能力设计。

## 3.1 System

登记旧系统。

主要信息：

- Name
- Display Name
- Description
- System Type
- Technology
- Repository
- Deployment
- Status
- Notes

---

## 3.2 Business Function

登记业务功能。

Function Type：

- Page Query
- MQ Consumer
- HTTP API
- Scheduled Job
- Background Process
- Other

主要信息：

- Name
- System
- Type
- Purpose
- User / Caller
- Input
- Process
- Output
- Business Notes
- Current Problems
- Rewrite Status

Rewrite Status：

- KEEP
- CHANGE
- REMOVE
- UNKNOWN

---

## 3.3 Database Dictionary

登记数据库对象。

第一版重点：

- Database
- Schema
- Table
- View
- Column

Table 信息：

- Table Name
- Business Name
- Description
- Estimated Rows
- Read / Write
- Database Primary Key
- Business Unique Key
- Structure Editable
- Notes

Column 信息：

- Column Name
- Data Type
- Length
- Nullable
- Database Comment
- Business Description
- Example Value
- Key Field
- Notes

---

## 3.4 Knowledge Status

数据库字段、业务规则等知识允许存在三个阶段：

`Unknown`

表示目前完全不知道。

`Inferred`

表示根据代码、SQL、数据等推测得到，但尚未确认。

`Confirmed`

表示已经确认。

这是产品非常重要的概念。

不要强迫用户录入“正确答案”。

系统应该允许知识逐步完善：

Unknown
→ Inferred
→ Confirmed

---

## 3.5 Relation

支持建立最基本的关联：

- System → Business Function
- Business Function → Database Table
- Business Function → Database Column
- Business Function → Business Rule
- Business Function → Integration

并支持反向查看。

例如：

打开 TABLE_EQP：

可以看到：

Used By

- Equipment Status Query
- Equipment Detail
- EquipmentChanged MQ

打开 Equipment Status Query：

可以看到：

Uses

- TABLE_EQP
- TABLE_JOB
- TABLE_ALARM

第一版不要设计复杂知识图谱。

---

## 3.6 Unknown Item

专门记录目前无法确认的问题。

例如：

`TABLE_JOB.STATE = 70 代表什么？`

主要信息：

- Question
- Related System
- Related Function
- Related Table / Column
- Priority
- Status
- Notes
- Resolution

状态：

- Open
- Investigating
- Confirmed
- Closed

Unknown 本身就是有效数据，不应该隐藏在普通备注里。

---

## 3.7 Global Search

支持全局搜索：

- System
- Business Function
- Table
- Column
- Business Rule
- Unknown Item

例如搜索：

`EQP_ID`

能够看到：

Database Columns

- TABLE_EQP.EQP_ID
- TABLE_JOB.EQP_ID

Business Functions

- Equipment Status Query

Related Rules

- Equipment Matching Rule

---

# 4. 核心产品设计原则

整个产品必须遵守以下原则。

## 4.1 Read First

默认页面用于：

**阅读、理解、探索。**

不要默认展示大量 Input、Textarea、Select。

详情页面默认应该像：

知识页面 / 数据目录 / 开发者工作台。

而不是传统后台 CRUD Form。

---

## 4.2 Edit In Context

需要修改信息时：

优先：

- Inline Edit
- Section Edit
- Drawer
- Dialog

避免频繁：

View Page
→ Edit Page
→ Full Form
→ Save
→ Back

---

## 4.3 Progressive Documentation

用户不需要一次填写完整资料。

例如第一次发现：

`TABLE_FOUP_HISTORY`

可以只创建：

- Name
- Schema
- Type

以后逐渐补充：

- Business Description
- Business Key
- Related Functions
- Column Description
- Rules

不要设计大量必填字段。

---

## 4.4 Relationship First

详情页面不仅显示“它是什么”。

更重要的是显示：

**它和什么有关。**

例如 Database Table 页面重点显示：

- Columns
- Used By Functions
- Related Rules
- Related Integrations
- Unknown Items

Business Function 页面重点显示：

- Related Tables
- Related Columns
- Rules
- Integration
- Code References
- Unknown Items

---

## 4.5 Unknown Is Data

Unknown 是产品核心概念。

不要隐藏未知信息。

需要让用户明显看到：

- Unknown
- Inferred
- Confirmed

并方便用户逐步把：

Unknown
→ Inferred
→ Confirmed

---

# 5. 整体布局

产品定位为：

**Desktop First Web Application**

主要运行于开发人员电脑浏览器。

推荐：

Top Header
+
Left Navigation
+
Main Content

大致结构：

Legacy Knowledge Hub

Top：

- Global Search
- Quick Add
- Oracle Import（可以作为后续入口展示）
- Settings

Left Navigation：

- Dashboard
- Systems
- Functions
- Database
- Integrations
- Business Rules
- Unknown Items

Main：

当前页面内容。

不要设计复杂多级导航。

---

# 6. 优先设计页面

本次原型优先完成以下页面。

---

# 6.1 Dashboard

Dashboard 的目标不是 BI。

而是：

**告诉用户当前旧系统梳理到了什么程度，以及下一步应该整理什么。**

建议包含：

Knowledge Overview

- Systems
- Business Functions
- Tables
- Columns
- MQ
- APIs
- Rules
- Unknown Items

Knowledge Progress

例如：

- 82 Tables
- 61 Analyzed
- 327 Columns Unknown
- 14 Rules Inferred
- 8 High Priority Unknown Items

Needs Attention

例如：

- High Priority Unknown
- Tables without Business Description
- Columns without Description
- Functions without Database Relation
- Inferred Rules waiting confirmation

Recent Activity

例如：

- TABLE_EQP
- Equipment Status Query
- EquipmentChanged
- TABLE_JOB

Dashboard 不要堆大量无意义图表。

---

# 6.2 System Detail

页面顶部：

- System Name
- Description
- Type
- Technology
- Status
- Repository
- Edit

下面按 Section 展示：

Overview

Business Functions

Database Objects

Integrations

Unknown Items

Recent Changes

默认阅读状态。

编辑某个 Section 时，只编辑该区域。

---

# 6.3 Business Function Detail

这是核心页面之一。

Header：

- Function Name
- System
- Function Type
- Rewrite Status
- Knowledge Status
- Edit

主要 Section：

## Overview

显示：

- Purpose
- User / Caller
- Input
- Output

## Business Process

以简单、易读方式展示业务步骤：

1.
2.
3.
4.

如果合适，可以设计简单的纵向流程展示。

不要开发复杂 BPMN 编辑器。

## Related Data

显示：

- Database Tables
- Database Columns

支持：

`+ Add Relation`

## Business Rules

显示：

- Rule Name
- Rule Description
- Knowledge Status

## Integrations

显示：

- MQ
- API
- Other

## Code References

显示：

- Repository
- File
- Class
- Method
- Line

## Unknown Items

明显展示尚未确认的问题。

---

# 6.4 Database Table Detail

这是整个 MVP 最重要的页面之一。

请重点设计。

Header：

- Table Name
- Business Name
- Schema
- Object Type
- Knowledge Status
- Edit

Overview：

- Business Description
- Estimated Rows
- Read / Write
- Database PK
- Business Unique Key
- Structure Editable

Columns：

使用高可读性的 Table。

至少显示：

Column

Type

Nullable

Business Description

Knowledge Status

例如：

EQP_ID
VARCHAR2(20)
NO
Equipment ID
Confirmed

STATUS
VARCHAR2(10)
YES
Equipment Status
Inferred

STATE_FLAG
VARCHAR2(2)
YES
Unknown
Unknown

需要明显区分：

Unknown / Inferred / Confirmed

点击 Column：

不要进入新页面。

打开：

**Right Drawer**

---

# 6.5 Column Detail Drawer

重点设计这个交互。

Drawer 包含：

Header

- Column Name
- Data Type

Database Metadata

- Type
- Length
- Nullable
- Database Comment

Business Knowledge

- Business Description
- Knowledge Status
- Example Value

Known Values

例如：

10 → Idle
20 → Running
30 → Unknown

Relations

Used By：

- Business Function
- Rule
- Integration

Unknown Items

例如：

`STATE_FLAG=10 代表什么？`

编辑：

优先 Section Edit。

不要让整个 Drawer 默认变成 Form。

---

# 6.6 Unknown Items

这是用户日常整理业务的重要工作页面。

建议设计成：

Filter
+
List/Table

Filter：

- System
- Type
- Priority
- Status
- Related Object

Table：

Question

Related Object

Priority

Status

Updated

点击进入 Detail Drawer 或 Detail Page。

用户应该能够：

创建 Unknown

开始调查

记录推测

确认结果

关闭问题

---

# 6.7 Global Search

搜索应该是产品核心入口之一。

搜索框支持：

- System Name
- Function Name
- Table Name
- Column Name
- Rule
- Unknown

Search Result 按类型分组。

例如搜索：

EQP_ID

Columns

TABLE_EQP.EQP_ID
TABLE_JOB.EQP_ID

Functions

Equipment Status Query

Rules

Equipment Matching Rule

点击结果直接进入对应 Detail。

---

# 7. 新增体验

不要使用大型 Full Form。

例如新增 Business Function：

第一步只需要：

Name

System

Type

Purpose

然后：

Create

创建后进入：

Business Function Detail

再逐步补：

Process

Database

Rules

Integration

Code Reference

Unknown

同样原则适用于：

System

Database Object

Rule

Unknown Item

---

# 8. 编辑体验

默认：

Read Mode

用户点击 Section 的：

Edit

当前 Section 进入编辑状态。

例如：

Business Description

正常：

Equipment current status table.

点击 Edit：

Textarea

Cancel

Save

保存后恢复 Read Mode。

不要整个页面同时进入编辑状态。

---

# 9. 视觉方向

整体视觉目标：

**Professional Developer Tool**

关键词：

- Clean
- Calm
- Information Dense
- Readable
- Technical
- Desktop First

可以参考：

- GitHub
- Linear
- Notion
- Modern Data Catalog

但不要直接复制任何产品。

避免：

- 大面积渐变
- 过度卡片化
- 巨大圆角
- 营销网站风格
- 大量装饰图
- Dashboard BI 风格
- 过度动画
- 每个内容都放 Card

信息密度可以比普通 SaaS 产品稍高。

---

# 10. 状态视觉

需要设计统一的 Knowledge Status：

Unknown

Inferred

Confirmed

以及：

Rewrite Status：

KEEP

CHANGE

REMOVE

UNKNOWN

Unknown 应该明显，但不要让页面充满警告色。

状态主要用于快速扫描和过滤。

---

# 11. Responsive 范围

第一版：

Desktop First

重点适配：

1366px
1440px
1920px

不需要优先设计手机端。

Tablet / Mobile 不是 MVP 重点。

---

# 12. 当前不设计

本次原型不要加入：

- 用户权限
- Role
- Permission
- Workflow
- Approval
- AI Assistant
- Chat
- Notification Center
- Complex Audit
- Version Control
- Knowledge Graph Editor
- Graph Database
- Plugin System
- Multi Tenant
- Custom Dashboard Builder
- Report Designer
- SQL Editor
- Database IDE
- Full Markdown Wiki
- Real-time Collaboration

除非 `docs/product-design/Legacy_Knowledge_Hub_MVP_Design.md` 明确要求，否则不要增加。

---

# 13. Product Design 本次输出要求

首先分析：

`docs/product-design/Legacy_Knowledge_Hub_MVP_Design.md`

然后设计：

1. Global Application Shell
2. Dashboard
3. System Detail
4. Business Function Detail
5. Database Table Detail
6. Column Detail Drawer
7. Unknown Items
8. Global Search

优先保证：

Database Table Detail

Business Function Detail

Column Detail Drawer

三个核心体验。

---

# 14. 原型需要验证的问题

设计完成后，请检查原型能否自然完成以下操作。

## Scenario A — 发现一张未知表

用户发现：

TABLE_EQP_STATUS

能够：

创建 / 导入 Table

→ 查看 Columns

→ 给 STATUS 添加 Inferred Description

→ 创建 Unknown Item

→ 关联 Equipment Status Function

→ 后续确认 STATUS 含义

→ 修改为 Confirmed

形成完整闭环。

---

## Scenario B — 梳理旧页面

用户分析：

Equipment Status Page

能够：

创建 Business Function

→ 填写 Purpose

→ 填写 Business Process

→ 关联 TABLE_EQP

→ 关联 TABLE_JOB

→ 添加 Business Rule

→ 添加 Code Reference

→ 创建 Unknown Item

形成完整闭环。

---

## Scenario C — 查询历史知识

几个月以后用户遇到：

EQP_ID

能够：

Global Search

→ 找到相关 Column

→ 查看相关 Table

→ 查看 Used By Functions

→ 查看 Rules

→ 查看 Code Reference

快速理解旧系统。

---

# 15. 设计原则优先级

如果设计过程中发生冲突，按照以下优先级决策：

1. 易于理解
2. 易于查找
3. 易于逐步补充知识
4. 易于建立关联
5. 易于编辑
6. 视觉美观

不要为了视觉效果牺牲信息阅读效率。

---

# 16. 最重要的约束

本阶段：

**只做产品设计与原型。**

不要开始正式项目编码。

不要自行设计完整企业级架构。

不要因为未来可能需要而增加功能。

不要扩大 MVP。

如果发现需求不明确：

优先采用最简单、最容易修改的产品设计。

设计完成后，先让我确认原型。

确认之后再进入：

.NET 8 + Vue 3 的正式实现阶段。
