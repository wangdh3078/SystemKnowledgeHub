# 知识发现与导航页面 — Product Design

状态：待统一评审  
范围：总览、系统列表、业务功能列表、数据库对象列表、待确认事项列表、全局搜索。  
明确排除：Integration Detail、Business Rule Detail、Vue3 / .NET 8 正式代码。

## 可评审原型

### 总览与列表页

1. 总览  
   `product-design/knowledge-discovery-navigation/01_Dashboard.png`
2. 系统列表  
   `product-design/knowledge-discovery-navigation/02_Systems_List.png`
3. 业务功能列表  
   `product-design/knowledge-discovery-navigation/03_Business_Functions_List.png`
4. 数据库对象列表  
   `product-design/knowledge-discovery-navigation/04_Database_Objects_List.png`
5. 待确认事项列表  
   `product-design/knowledge-discovery-navigation/05_Unknown_Items_List.png`

### 全局搜索关键状态

6. `STATE_FLAG` 分组结果与键盘选择  
   `product-design/knowledge-discovery-navigation/06_Global_Search_Results.png`
7. 最近搜索与最近访问  
   `product-design/knowledge-discovery-navigation/07_Global_Search_Recent.png`
8. 无结果与恢复路径  
   `product-design/knowledge-discovery-navigation/08_Global_Search_No_Result.png`

完整评审板：  
`product-design/knowledge-discovery-navigation/Knowledge_Discovery_Navigation_Review_Board.png`

全局搜索状态评审板：  
`product-design/knowledge-discovery-navigation/Global_Search_States_Review_Board.png`

Design Baseline 视觉对照：  
`product-design/qa/Baseline_vs_Knowledge_Discovery_Navigation.png`

## 统一页面职责

这些页面只承担：

- Find
- Filter
- Browse
- Navigate

信息架构保持：

- List Page 回答“我要找什么”。
- Detail Page 回答“它是什么”。
- Context Rail 回答“当前对象与什么有关、还缺什么”。
- Detail Drawer 回答“当前选中对象的细节是什么”。

外围入口页面没有单一“当前知识对象”，因此默认不显示对象级“关系与缺口”Context Rail，也不打开 Detail Drawer。用户选择列表行或搜索结果后进入已确认的 Detail 页面；进入对象上下文后再恢复 Context Rail 与 Drawer 模式。

## 1. 总览

### 信息层级

- 知识总览：系统、业务功能、表 / 视图、字段、集成关系、业务规则、待确认事项。
- 知识进展：已确认、推断、未知、开放待确认事项。
- 需要关注：高优先级待确认事项、未知较多的系统、缺少业务说明的表、仍为未知的字段、等待确认的推断知识、未关联数据的业务功能。
- 最近整理：最近更新的系统、业务功能、字段与待确认事项。

### 关键交互

- 知识总览数字进入对应 List Page。
- 知识进展项进入带对应知识状态过滤的列表。
- “需要关注”进入目标对象或预设过滤结果。
- “最近整理”进入对应已确认 Detail 页面。

总览使用单一计数条和知识进展分段条，不使用 KPI 卡片、复杂图表、趋势分析或 BI 控件。

## 2. 系统列表

### 查找与过滤

- 搜索系统名称、显示名称或用途
- 状态
- 技术
- 知识状态

### 列表字段

- 系统名称
- 显示名称
- 系统类型
- 用途
- 技术
- 业务功能数量
- 数据库对象数量
- 开放待确认事项数量
- 知识状态
- 更新于

点击 `MES` 进入已确认的 System Detail。

## 3. 业务功能列表

### 查找与过滤

- 系统
- 功能类型
- 改写状态
- 知识状态
- 是否存在待确认事项

### 列表字段

- 功能名称
- 系统
- 类型
- 用途
- 关联数据数量
- 业务规则数量
- 待确认事项数量
- 改写状态
- 知识状态
- 更新于

点击 `Equipment Status Query` 进入已确认的 Business Function Detail。

## 4. 数据库对象列表

### 浏览模型

Main Content 内使用两栏：

- 左侧 Database / Schema 导航
- 右侧当前 Schema 的对象搜索、过滤与列表

该两栏属于数据库浏览器内部结构，不是对象级 Context Rail。

### 搜索与过滤

- 表名
- 视图名
- 字段名
- 业务说明
- 对象类型
- 读写方式
- 知识状态

### 列表字段

- 对象名称
- 类型
- 业务说明
- 预估行数
- 读写方式
- 关联功能数量
- 待确认事项数量
- 知识状态
- 更新于

点击 `MES.TABLE_EQP` 进入已确认的 Database Table Detail。

## 5. 待确认事项列表

### 查找与过滤

- 系统
- 关联对象类型
- 优先级
- 状态
- 更新时间

### 列表字段

- 问题
- 系统
- 关联对象
- 优先级
- 状态
- 调查发现数量
- 证据数量
- 更新于

待确认事项状态严格使用：

`待处理 → 调查中 → 结论已确认 → 已关闭`

Knowledge Status 独立使用：

`未知 → 推断 → 已确认`

点击 `STATE_FLAG=30 具体表示什么？` 进入已确认的 Unknown Item Detail。

## 6. 全局搜索

### 打开与关闭

- 单击顶部全局搜索或使用 `⌘ K` 打开 Search Overlay。
- `Esc` 关闭并返回原页面，不丢失原页面筛选和滚动位置。

### 最近搜索状态

- 空查询时显示最近搜索和最近访问。
- 支持上下方向键选择。
- `Enter` 重新搜索或直接打开最近访问对象。
- 允许清除最近搜索记录。

### 搜索结果状态

结果按类型分组：

- 系统
- 业务功能
- 数据库对象
- 字段
- 业务规则
- 集成关系
- 待确认事项

每条结果必须包含：

- 对象名称
- System Context
- Object Type
- Short Description
- Knowledge Status；待确认事项同时展示独立 Item Status

键盘交互：

- `↑ / ↓`：移动选择
- `Enter`：打开当前结果
- `Esc`：关闭搜索

搜索 `STATE_FLAG` 时，默认选中 `MES · MES.TABLE_EQP.STATE_FLAG`。打开后进入 Database Table Detail，并自动打开 `STATE_FLAG` Column Detail Drawer。

### 无结果状态

- 清楚说明没有匹配对象。
- 提供更短的技术标识和业务描述搜索建议。
- 提供“搜索 STATE_FLAG”和“清除搜索”。
- 保留最近搜索，避免形成导航死路。

## 两条探索闭环

### 从列表进入

系统列表  
→ `MES`  
→ System Detail 的业务功能  
→ `Equipment Status Query`  
→ Business Function Detail 的关联数据  
→ `MES.TABLE_EQP`  
→ Database Table Detail  
→ `STATE_FLAG` Column Detail Drawer  
→ `STATE_FLAG=30 具体表示什么？`  
→ Unknown Item Detail

### 从全局搜索进入

全局搜索  
→ 搜索 `STATE_FLAG`  
→ 选择 `MES.TABLE_EQP.STATE_FLAG`  
→ Database Table Detail + Column Detail Drawer  
→ 关联业务功能 `Equipment Status Query`  
→ 待确认事项 `STATE_FLAG=30 具体表示什么？`

正向和反向关系均复用已确认的 Detail Page、Context Rail、Object Detail Drawer 与 Relationship Detail Drawer，不在入口页面重新发明交互。

## Desktop First

- 1920px：完整展示过滤器、宽表格和搜索 Overlay。
- 1440px / 1366px：优先保留名称、上下文、状态与核心数量；较低优先级列允许水平滚动或在列设置中隐藏。
- 数据库对象页保留 Schema 导航最小宽度，不把对象表格压缩到不可扫描。
- 全局搜索 Overlay 保持主要结果可见，超出部分在 Overlay 内滚动。

## 视觉与信息架构 QA

- 通过：六个页面全部继承已确认浅色 Application Shell。
- 通过：产品 UI 文案为简体中文，技术标识保持英文原文。
- 通过：总览不是 BI Dashboard。
- 通过：列表页没有复制 Detail 页面中的完整知识、关系或 Evidence。
- 通过：列表 Hover / Selected、表格密度、Section hierarchy 与 Baseline 一致。
- 通过：待确认事项状态与 Knowledge Status 独立。
- 通过：搜索结果按类型分组，并包含上下文、类型、短描述和状态。
- 通过：最近搜索、键盘选择和无结果恢复路径完整。
- 通过：两条跨对象探索路径可以自然进入四类已确认 Detail 页面。
- 通过：未设计 Integration Detail、Business Rule Detail，也未进入 Vue3 / .NET 8 正式开发。

## 本轮待统一确认的设计决策

1. List Page 默认不显示对象级 Context Rail；进入具体对象后才恢复 Context Rail 与 Drawer。
2. 系统、业务功能、数据库对象和待确认事项的列表行均直接导航至已确认 Detail 页面，不在列表上复制详情。
3. 数据库对象页使用 Main Content 内部的 Database / Schema 浏览栏，它不是 Context Rail。
4. 搜索字段结果直接打开 Database Table Detail，并自动打开对应 Column Detail Drawer。
5. 总览“需要关注”采用任务型列表，不使用 KPI 卡片或复杂图表。

## Product Design 生成说明

使用 Product Design 工作流与内置 ImageGen，以已确认的 System Detail、Business Function Detail 和 Unknown Item Detail 截图作为固定视觉参考，生成五个入口页面与全局搜索三个关键状态。

所有生成提示均锁定当前 Design Baseline、简体中文产品文案、英文技术标识、两套独立状态、Find / Filter / Browse / Navigate 职责，并禁止生成新的视觉方向、复杂 BI Dashboard、传统 CRUD 后台或正式代码。
