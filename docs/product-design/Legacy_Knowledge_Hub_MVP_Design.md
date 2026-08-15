# Legacy Knowledge Hub — MVP 最小功能闭环设计

> 目标：为缺少文档、数据库字段无说明、业务规则分散在代码/SQL/MQ/API 中的旧系统，提供一个轻量级“系统梳理与知识登记工具”。
>
> 核心原则：**阅读优先、渐进录入、关联优先、Unknown 也是有效数据、不过度设计。**

---

## 1. 产品定位

这个工具不是 Wiki、不是 CMDB、不是文档平台，也不是代码分析平台。

它第一版只解决一个问题：

> **把旧系统中已经发现的事实结构化保存起来，并且可以通过关联关系快速找到“这个功能用了什么、这张表被谁用、这个字段是什么意思、还有哪些内容没确认”。**

适用场景：

- 旧系统准备重写或重构
- 缺少完整需求文档
- Oracle 表/字段没有说明
- 业务规则散落在后端、前端、SQL 中
- 存在 MQ、HTTP API、后台处理服务
- 需要边分析边补资料，而不是先一次性整理完整

---

## 2. MVP 范围

第一版只实现 6 个核心能力：

1. **系统登记**
2. **业务功能登记**
3. **数据库对象登记**
4. **关联关系**
5. **待确认事项（Unknown）**
6. **全局搜索**

第一版明确不实现：

- 权限系统
- 审批流
- 多租户
- Wiki 富文本编辑器
- 图数据库
- AI 自动分析
- Elasticsearch
- 复杂工作流
- 完整 Oracle 元数据同步
- MQ/API 深度管理
- 代码扫描器
- 版本控制平台
- 插件系统

这些都可以后续按真实需求增加。

---

# 3. MVP 闭环

最小使用闭环如下：

```text
发现旧系统资料
   ↓
登记 System
   ↓
登记 Function
   ↓
登记 Database Object / Column
   ↓
建立 Function ↔ Database 关联
   ↓
遇到不确定信息 → 创建 Unknown
   ↓
后续确认 → 更新说明 / 关闭 Unknown
   ↓
通过搜索和详情页再次查阅
```

如果这一条链路能够顺畅使用，第一版就已经成功。

---

# 4. 功能清单

## 4.1 Dashboard

### 目标

让用户一打开系统就知道：

- 已经整理多少内容
- 哪些信息还没确认
- 最近整理了什么
- 下一步应该继续处理什么

### 页面内容

顶部统计卡片：

- Systems
- Functions
- Database Objects
- Columns
- Unknown Items

中间区域：

- 最近修改
- 高优先级 Unknown
- 未关联业务功能的数据库对象
- 缺少说明的字段

### 操作

- 点击统计卡进入对应列表
- 点击最近修改进入详情
- 点击 Unknown 进入待确认项详情

---

## 4.2 System 管理

### 目标

记录旧系统/程序本身。

### 最小字段

- Name
- Code
- Type
- Description
- Tech Stack
- Status
- Notes

### Type

- Web
- API
- Worker
- Windows Service
- Batch
- Other

### 页面操作

#### System List

展示：

- 名称
- 类型
- 描述
- 功能数量
- 数据库对象数量
- 更新时间

操作：

- 搜索
- 新增
- 点击进入详情

#### System Detail

默认只读。

顶部：

- 名称
- 类型
- 简介
- Edit

下面展示：

- Related Functions
- Related Database Objects
- Open Unknown Items

编辑使用 **Section Edit** 或右侧 Drawer，不跳整页编辑表单。

---

## 4.3 Business Function 管理

### 目标

记录“系统到底在做什么”。

### 最小字段

- Name
- System
- Function Type
- Business Purpose
- Entry
- Process Description
- Output Description
- Rewrite Status
- Notes

### Function Type

- Page Query
- MQ Consumer
- HTTP API
- Scheduled Job
- Background Process
- Other

### Rewrite Status

- KEEP
- CHANGE
- REMOVE
- UNKNOWN

### 页面布局

#### Function List

左侧筛选：

- System
- Type
- Rewrite Status

右侧列表：

- Function Name
- System
- Type
- Purpose
- Related DB Count
- Unknown Count
- Status

#### Function Detail

页面结构：

```text
[标题 / System / 类型 / Rewrite Status]

业务目的

业务流程

关联数据库对象

关联字段

待确认事项

备注
```

默认全部为阅读状态。

每个 Section 右上角单独提供 Edit。

### 操作

- 新增 Function：只要求填写 Name、System、Type、Purpose
- 创建完成后进入详情页逐步补充
- 关联 Database Object
- 关联 Database Column
- 新建 Unknown

---

## 4.4 Database 管理

MVP 第一版只管理：

- Database Object
- Database Column

不先做完整 Database Connection / Schema 管理体系。

### Database Object

支持类型：

- Table
- View
- Procedure
- Other

最小字段：

- Name
- Schema
- Type
- Business Name
- Description
- Has Primary Key
- Business Unique Key
- Read / Write Type
- Notes

### Database Column

最小字段：

- Object
- Name
- Data Type
- Nullable
- DB Comment
- Business Description
- Knowledge Status
- Example Value
- Notes

### Knowledge Status

- Unknown
- Inferred
- Confirmed

### Database Object Detail 布局

顶部：

```text
SCHEMA.TABLE_NAME
Table · Oracle
业务名称
业务说明
事实唯一键
```

中间：字段表格

```text
Column         Type          Nullable    Business Description    Status
EQP_ID         VARCHAR2(20)  NO          设备编号                Confirmed
STATUS         VARCHAR2(10)  YES         设备状态                Inferred
STATE_FLAG     VARCHAR2(2)   YES         未确认                  Unknown
```

底部：

- Used By Functions
- Open Unknown Items

### 字段编辑交互

点击字段 → 右侧 Drawer。

Drawer 展示：

- 数据库原始信息
- Business Description
- Knowledge Status
- Example Value
- Related Functions
- Unknown Items

只在点击编辑后出现输入控件。

---

## 4.5 Relation 关联关系

MVP 第一版只实现两种关系：

```text
Function ↔ Database Object
Function ↔ Database Column
```

不做通用图谱系统。

### 使用方式

在 Function Detail 中：

```text
Related Database Objects
[+ Add]

TABLE_EQP        Read
TABLE_JOB        Read
TABLE_STATUS     Read / Write
```

点击 Add：

- 搜索数据库对象
- 选择 Relation Type
- 保存

Relation Type：

- Read
- Write
- Read / Write
- Reference

### 反向查看

Database Object Detail 也显示：

```text
Used By Functions

设备状态查询
MQ设备状态处理
设备详情
```

这样形成最小知识关系闭环。

---

## 4.6 Unknown Items

### 目标

把“不知道”正式记录下来，而不是散落在备注和脑子里。

### 最小字段

- Title
- Description
- Priority
- Status
- Related System
- Related Function
- Related Database Object
- Related Database Column
- Resolution
- Notes

### Priority

- Low
- Medium
- High

### Status

- Open
- Investigating
- Confirmed
- Closed

### 操作

典型场景：

1. 打开 `TABLE_JOB.STATE`
2. 发现不知道 `70` 代表什么
3. 点击 `Create Unknown`
4. 自动带入当前字段关系
5. 填写问题：`STATE=70 的业务含义是什么？`
6. 保存

后续确认后：

1. 打开 Unknown
2. 填 Resolution
3. 更新字段 Business Description / Knowledge Status
4. 关闭 Unknown

---

## 4.7 Global Search

### 搜索范围

- System Name
- Function Name
- Database Object Name
- Database Column Name
- Business Description
- Unknown Title

### 搜索结果按类型分组

例如搜索：

```text
EQP_ID
```

结果：

```text
Database Columns
- MES.TABLE_EQP.EQP_ID
- MES.TABLE_JOB.EQP_ID

Functions
- Equipment Status Query

Unknown
- EQP_ID 是否在所有机台唯一？
```

### MVP 实现

SQLite `LIKE` 查询即可。

不引入 Elasticsearch。

---

# 5. 页面整体布局

## 5.1 App Shell

推荐布局：

```text
┌───────────────────────────────────────────────────────────────┐
│ Legacy Knowledge Hub     [Global Search........]              │
├───────────────┬───────────────────────────────────────────────┤
│ Dashboard     │                                               │
│ Systems       │                                               │
│ Functions     │              Main Content                     │
│ Database      │                                               │
│ Unknown       │                                               │
│               │                                               │
└───────────────┴───────────────────────────────────────────────┘
```

左侧导航固定宽度。

顶部 Global Search 常驻。

主内容区负责列表和详情。

---

# 6. UI / UX 原则

## 6.1 Read First

默认页面为阅读模式。

不要出现整页 20~30 个输入框。

## 6.2 Progressive Input

第一次创建只填必要字段。

例如创建 Function：

- Name
- System
- Type
- Purpose

剩余信息创建后慢慢补。

## 6.3 Edit in Context

详情页每个 Section 自己编辑。

字段详情使用 Drawer。

## 6.4 Unknown is Data

不知道的内容允许直接标记：

```text
Unknown
```

不要强迫填写猜测答案。

## 6.5 Relationship First

详情页优先显示关联关系。

例如打开 Function 能快速看到：

- 用了哪些表
- 用了哪些字段
- 有哪些 Unknown

---

# 7. 推荐技术栈

## 7.1 Backend

- .NET 8
- ASP.NET Core Web API
- Entity Framework Core 8
- SQLite
- FluentValidation（可选，MVP 可以不用）
- Swashbuckle / OpenAPI

### 推荐原因

这个工具自己的数据库是全新设计、结构规整、CRUD 为主。

因此这里适合：

```text
EF Core + SQLite
```

没有必要使用 Dapper。

---

## 7.2 Frontend

- Vue 3
- Vite
- Vue Router
- Pinia
- Axios
- Element Plus

### 为什么选 Element Plus

适合快速实现：

- Table
- Form
- Drawer
- Dialog
- Tag
- Pagination
- Select
- Tree（后续可能用）

但产品视觉不要做成传统“全页面 CRUD 表单”。

Element Plus 只作为基础组件库。

---

## 7.3 Optional

第一版可选：

- dayjs：时间格式化
- lodash-es：如果真实出现需要再引入

第一版不建议：

- ECharts
- Mermaid 编辑器
- Markdown 编辑器
- Monaco Editor
- Elasticsearch
- Redis

---

# 8. 后端项目结构

MVP 不做复杂 Clean Architecture。

建议：

```text
src/
  LegacyHub.Api/
    Controllers/
    Data/
    Entities/
    Features/
      Systems/
      Functions/
      Database/
      UnknownItems/
      Search/
    Common/
```

### Features 示例

```text
Features/
  Functions/
    FunctionController.cs
    FunctionService.cs
    FunctionDtos.cs
```

不要拆成：

```text
Domain
Application
Infrastructure
Contracts
Persistence
SharedKernel
```

MVP 没有这个必要。

---

# 9. 前端项目结构

```text
web/src/
  api/
  components/
  layouts/
  router/
  stores/
  modules/
    dashboard/
    systems/
    functions/
    database/
    unknown/
    search/
```

每个模块内部：

```text
functions/
  views/
  components/
  api.js
```

保持简单。

---

# 10. MVP 数据模型

## System

```text
Id
Name
Code
Type
Description
TechStack
Status
Notes
CreatedAt
UpdatedAt
```

## BusinessFunction

```text
Id
SystemId
Name
Type
BusinessPurpose
EntryDescription
ProcessDescription
OutputDescription
RewriteStatus
Notes
CreatedAt
UpdatedAt
```

## DatabaseObject

```text
Id
SystemId nullable
SchemaName
Name
Type
BusinessName
Description
HasPrimaryKey
BusinessUniqueKey
AccessType
Notes
CreatedAt
UpdatedAt
```

## DatabaseColumn

```text
Id
DatabaseObjectId
Name
DataType
Nullable
DbComment
BusinessDescription
KnowledgeStatus
ExampleValue
Notes
CreatedAt
UpdatedAt
```

## FunctionDatabaseObject

```text
Id
BusinessFunctionId
DatabaseObjectId
RelationType
Notes
```

## FunctionDatabaseColumn

```text
Id
BusinessFunctionId
DatabaseColumnId
RelationType
Notes
```

## UnknownItem

```text
Id
Title
Description
Priority
Status
SystemId nullable
BusinessFunctionId nullable
DatabaseObjectId nullable
DatabaseColumnId nullable
Resolution
Notes
CreatedAt
UpdatedAt
```

---

# 11. API 范围

第一版只需要标准 REST API。

示例：

```text
GET    /api/systems
POST   /api/systems
GET    /api/systems/{id}
PUT    /api/systems/{id}

GET    /api/functions
POST   /api/functions
GET    /api/functions/{id}
PUT    /api/functions/{id}

GET    /api/database/objects
POST   /api/database/objects
GET    /api/database/objects/{id}

POST   /api/database/objects/{id}/columns
PUT    /api/database/columns/{id}

POST   /api/functions/{id}/database-objects
DELETE /api/functions/{id}/database-objects/{relationId}

POST   /api/functions/{id}/database-columns
DELETE /api/functions/{id}/database-columns/{relationId}

GET    /api/unknown-items
POST   /api/unknown-items
PUT    /api/unknown-items/{id}

GET    /api/search?q=EQP_ID
```

---

# 12. 核心页面原型描述

## 12.1 Function Detail

```text
┌───────────────────────────────────────────────────────────┐
│ Equipment Status Query                         [Edit]      │
│ Page Query · Equipment System · KEEP                       │
├───────────────────────────────────────────────────────────┤
│ Business Purpose                                  [Edit]   │
│ 查看当前设备、FOUP、Job 与报警状态。                        │
├───────────────────────────────────────────────────────────┤
│ Process                                           [Edit]   │
│ 1. Select Area                                             │
│ 2. Query Equipment                                         │
│ 3. Query Job / FOUP                                        │
│ 4. Build Result                                            │
├───────────────────────────────────────────────────────────┤
│ Related Database                              [+ Add]      │
│ TABLE_EQP      Read                                        │
│ TABLE_JOB      Read                                        │
├───────────────────────────────────────────────────────────┤
│ Unknown Items                                 [+ Add]      │
│ HIGH  STATUS=30 的业务含义是什么？                          │
└───────────────────────────────────────────────────────────┘
```

---

## 12.2 Database Object Detail

```text
┌───────────────────────────────────────────────────────────┐
│ MES.TABLE_EQP                                  [Edit]      │
│ Table · Equipment Current State                           │
├───────────────────────────────────────────────────────────┤
│ Description                                       [Edit]   │
│ 保存设备当前状态。                                          │
│ Business Unique Key: EQP_ID                               │
├───────────────────────────────────────────────────────────┤
│ Columns                                                   │
│ EQP_ID      VARCHAR2(20)   Equipment ID     Confirmed     │
│ STATUS      VARCHAR2(10)   Status           Inferred      │
│ FLAG        VARCHAR2(2)    Unknown          Unknown       │
├───────────────────────────────────────────────────────────┤
│ Used By Functions                                         │
│ Equipment Status Query                                    │
│ Equipment Message Handler                                 │
├───────────────────────────────────────────────────────────┤
│ Unknown Items                                             │
│ FLAG 字段真实用途是什么？                                  │
└───────────────────────────────────────────────────────────┘
```

---

# 13. 开发顺序

## Phase 1 — 基础骨架

实现：

- .NET 8 Web API
- Vue 3
- SQLite
- EF Core Migration
- Layout
- Router

验收：

- 前后端能启动
- SQLite 自动建库
- Vue 能调用 API

---

## Phase 2 — System + Function

实现：

- System List / Detail / Create
- Function List / Detail / Create

验收：

- 能创建系统
- 能在系统下创建 Function
- 默认详情页为阅读状态

---

## Phase 3 — Database

实现：

- Database Object List / Detail / Create
- Column List
- Column Drawer
- Knowledge Status

验收：

- 能创建表
- 能维护字段业务说明
- 能标记 Unknown/Inferred/Confirmed

---

## Phase 4 — Relations

实现：

- Function ↔ Database Object
- Function ↔ Database Column
- 双向展示

验收：

- Function Detail 可以看到相关表
- Table Detail 可以看到被哪些 Function 使用

---

## Phase 5 — Unknown

实现：

- Unknown List
- Unknown Create/Edit
- 从 Function / Column 快速创建 Unknown

验收：

- 可以记录问题
- 可以关联对象
- 可以记录 Resolution
- 可以关闭问题

---

## Phase 6 — Global Search

实现：

- 顶部搜索框
- Search Result 页面
- 按类型分组

验收：

搜索表名、字段名、功能名都能找到结果。

---

# 14. MVP 完成定义

第一版完成时，必须能够完整执行以下流程：

1. 创建旧系统 `Equipment System`
2. 创建业务功能 `Equipment Status Query`
3. 创建数据库表 `MES.TABLE_EQP`
4. 添加字段 `EQP_ID / STATUS / FLAG`
5. 标记 `FLAG` 为 Unknown
6. 把 `TABLE_EQP` 关联到 `Equipment Status Query`
7. 创建 Unknown：`FLAG 字段业务含义是什么？`
8. 后续确认后填写字段说明
9. 将字段 Knowledge Status 修改为 Confirmed
10. 关闭 Unknown
11. 搜索 `FLAG` 能找到字段和相关 Unknown
12. 从 Function 可以进入 Table，从 Table 可以回到 Function

只要这条链跑通，MVP 就成立。

---

# 15. Codex 开发约束建议

建议项目根目录 AGENTS.md 至少写入：

```text
- Implement only current MVP requirements.
- Do not introduce speculative abstractions.
- Do not add CQRS, MediatR, repository frameworks, Redis, messaging, plugins, or microservices.
- Prefer simple feature-based organization.
- Use EF Core directly for persistence.
- Default UI is read-first; avoid full-page edit forms for detail pages.
- Use section editing and drawer editing where appropriate.
- Unknown information must be representable explicitly.
- Build and test after each task.
```

---

# 16. 后续可扩展方向

MVP 使用稳定以后，再根据真实需求选择增加：

- Oracle 元数据自动导入
- MQ / HTTP Integration
- Business Rule
- Status Value Dictionary
- Code Reference
- SQL Reference
- Markdown Export
- JSON Export
- Mermaid 关系图
- Git Repository Link
- Codex-friendly docs export

这些全部是 Phase 2 以后再考虑。

---

# 17. 最终建议

这个工具第一版不要追求“完整知识管理平台”。

真正目标只有三个：

1. **东西能登记**
2. **东西能关联**
3. **东西能再次找到**

只要满足这三点，并且 Unknown 可以持续被确认和关闭，它就已经能真正帮助旧系统重写工作。
