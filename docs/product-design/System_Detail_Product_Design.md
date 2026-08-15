# System Detail / 系统详情 — Product Design

状态：待评审  
页面范围：仅 System Detail，不包含 Systems List、总览、Global Search、独立 Integrations、Business Rules 或正式代码。

## 可评审原型

1. 系统概览、知识概况与业务功能  
   `product-design/system-detail/01_System_Overview.png`
2. 数据库对象、集成关系、代码 / 仓库与系统级待确认事项  
   `product-design/system-detail/02_System_Lower_Content.png`
3. 选中业务功能后的 Object Detail Drawer  
   `product-design/system-detail/03_Business_Function_Object_Drawer.png`

评审板：  
`product-design/system-detail/System_Detail_Review_Board.png`

Design Baseline 视觉对照：  
`product-design/qa/Baseline_vs_System_Detail.png`

## 页面职责

System Detail 是系统级知识入口，用于快速建立对旧系统的整体理解，并让用户继续进入业务功能、数据库对象、字段、待确认事项等具体知识对象。页面不承担把所有系统知识完整摊开的职责。

- Main Content 回答“MES 系统本身是什么、当前梳理到什么程度、包含哪些核心对象”。
- System-level「关系与缺口」Context Rail 回答“MES 与哪些系统或基础设施有关、当前有哪些系统级缺口”。
- Object Detail Drawer 回答“当前选中的业务功能对象是什么”，并提供进入完整详情或关联数据对象的明确入口。

## 信息架构

### Header

- System Name：`MES`
- Display Name：制造执行系统
- System Type：核心业务系统
- Status：运行中
- Technology：`C# / .NET Framework`
- Knowledge Status：推断
- 明确操作：编辑

### 概览

使用紧凑的两列信息行，避免大卡片：

- 用途
- 主要用户
- 技术
- 代码仓库
- 部署
- 数据库
- 备注

技术标识使用原文与等宽表达，例如 `mes-legacy`、`.NET Framework 4.8`、`Oracle`、`RabbitMQ`、`MES-APP-01`。

### 知识概况

使用单一、克制的知识完整度分段条和计数，不使用 KPI 卡片或 BI 图表：

- 已确认：42%
- 推断：38%
- 未知：20%
- 开放待确认事项：12

这一信息只帮助用户理解当前系统梳理程度，不表达绩效或业务经营指标。

### 业务功能

高密度表格列：

- 功能名称
- 类型
- 用途
- 改写状态
- 知识状态
- 待确认事项

行 Hover 表示可探索；单击后在当前上下文打开业务功能 Object Detail Drawer，不立即离开系统页。

### 数据库对象

高密度表格列：

- 数据库 / Schema
- 对象名称
- 类型
- 业务说明
- 知识状态
- 关联功能
- 待确认事项

对象名称保持原文，例如 `MES.TABLE_EQP`、`MES.V_CURRENT_JOB`。

### 集成关系

高密度表格列：

- 名称 / 端点
- 类型
- 方向
- 关联系统
- 关联业务功能
- 知识状态

系统页只展示集成摘要与关键关联；完整关系详情由 Relationship Detail Drawer 承担。

### 代码 / 仓库

紧凑展示：

- 代码仓库：`mes-legacy`
- 主要项目：`MES.Web`、`MES.Services`、`MES.DataAccess`
- 主要入口：`Global.asax`、`EquipmentStatusController.cs`、`MessageConsumer.cs`

Code Reference 仍属于统一 Evidence 模型；本区只作为系统入口索引，不建立平行证据体系。

### 系统级待确认事项

仅展示 System-level Open Unknown Items，列：

- 问题
- 关联对象
- 优先级
- 状态
- 更新于

待确认事项状态统一为：`待处理 → 调查中 → 结论已确认 → 已关闭`。  
Knowledge Status 独立使用：`未知 → 推断 → 已确认`。

## System-level「关系与缺口」Context Rail

Context Rail 只展示 MES 的系统级摘要：

- 关联系统
- 集成概况
- 主数据库
- 高优先级待确认事项
- 知识缺口

它不复制 Main Content 的完整业务功能、数据库对象或集成表格，也不展示字段级、规则级或业务功能级完整详情。

## Object Detail Drawer

选择 `Equipment Status Query` 后打开业务功能 Object Detail Drawer：

- 业务功能名称与类型
- 只读知识进展：`未知 → 推断 → 已确认`，当前为推断
- 业务知识摘要
- 关联数据：`MES.TABLE_EQP`、`MES.V_CURRENT_JOB`
- 核心证据：`EquipmentStatusService.cs : line 184`、`QueryEquipmentStatus.sql`、`MES.TABLE_EQP · 24 条样本`
- 待确认事项：`STATE_FLAG=30 具体表示什么？`
- 低频集成关系与更多代码引用折叠

Drawer 底部提供两个明确动作：

- 查看 `MES.TABLE_EQP`
- 打开业务功能详情

Drawer 不复制 System Main Content 的完整概览，也不改变 System-level Context Rail 的职责。

## 关键交互闭环

正向探索：

`MES`  
→ 在业务功能表格选择 `Equipment Status Query`  
→ 在对象抽屉查看业务知识、关联数据与证据  
→ 查看 `MES.TABLE_EQP`  
→ 进入已确认的 Database Table Detail  
→ 选择 `STATE_FLAG`  
→ 查看 Column Detail Drawer 中的 Evidence  
→ 打开 `STATE_FLAG=30 具体表示什么？`  
→ 进入待确认事项详情  
→ 查看 Resolution 与已应用的知识更新

反向探索：

`STATE_FLAG`  
→ Column-level Relations 中选择 `Equipment Status Query`  
→ 打开业务功能详情  
→ 从 Header / System 关联返回 `MES`

所有跳转都保留当前对象与来源上下文，避免用户在跨对象探索时失去路径。

## 响应式桌面规则

- 1920px：保留 Navigation + Main Content + System-level Context Rail；Drawer 打开时保留四层结构。
- 1366px / 1440px：Drawer 打开时优先保证 Main Content 与 Drawer，可自动隐藏 System-level Context Rail；关闭 Drawer 后恢复 Context Rail。
- 不把 Navigation、Main Content、Context Rail 与 Drawer 强行压缩成不可读的四栏。

## 视觉与交互 QA

- 通过：Application Shell、浅色方向、左侧导航与顶部搜索继承 Baseline。
- 通过：Section hierarchy、细分隔线、紧凑表格、Hover / Selected 状态保持一致。
- 通过：知识概况是梳理程度摘要，不是 Dashboard 或 KPI 卡片。
- 通过：System-level Context Rail 未复制 Main Content 完整内容。
- 通过：业务功能 Object Detail Drawer 与系统上下文职责分离。
- 通过：所有产品 UI 文案使用简体中文，技术标识保持原文。
- 通过：待确认事项“结论已确认”与 Knowledge Status“已确认”清楚区分。
- 通过：未进入 Vue3 / .NET 8 正式开发。

## 本轮待确认设计决策

1. 知识概况采用“单一分段条 + 文字计数”，不使用卡片或复杂图表。
2. Main Content 分为上半页概览 / 业务功能与下半页数据库对象 / 集成 / 代码 / 系统级待确认事项，避免超级详情页。
3. System-level Context Rail 固定为关联系统、集成概况、主数据库、高优先级待确认事项与知识缺口五类摘要。
4. 单击业务功能行先打开 Object Detail Drawer；只有明确选择“打开业务功能详情”才导航到完整 Business Function Detail。
5. 系统级待确认事项只在 Main Content 显示完整列表，Context Rail 仅提示高优先级摘要。
6. 原型中的 MES 数量、百分比、部署节点与仓库信息为交互验证用示例数据，后续需要由领域专家校准。

## Product Design 生成说明

使用 Product Design 工作流与内置 ImageGen，以已确认的简体中文 Business Function Detail 为固定视觉参考，生成同一 System Detail 的三个必要状态：系统概览、下半页内容、业务功能对象抽屉。

所有提示均要求保持当前 Design Baseline、System-level Context Rail 职责、Evidence 统一模型、两套状态体系、简体中文产品文案与英文技术标识，并禁止生成新视觉方向、CRUD 后台、BI Dashboard 或正式代码。
