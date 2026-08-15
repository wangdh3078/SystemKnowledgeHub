# Legacy Knowledge Hub MVP

## Knowledge Object Completion & Authoring Flow

状态：已完成第一版可评审原型，等待统一 MVP Design Review。  
设计基线：继承已冻结的 `System Knowledge Hub MVP Design Baseline`，本轮不改变 Application Shell、导航、Detail / List 信息架构或视觉体系。

## 本轮范围

本轮仅补齐：

1. Integration Detail 与 Preview / Drawer
2. Business Rule Detail 与 Preview / Drawer
3. Create Knowledge Object Flow
4. Add Relationship Flow
5. Add Evidence Flow
6. System、Business Function、Database Knowledge、Business Rule、Integration 的必要 Edit 状态
7. Knowledge Flow A–E 的交互闭环验证

本轮不包含新的普通 List Page，不包含 Integration List、Business Rule List，不进入 Vue3、.NET 8、后端架构或正式代码。

## 评审入口

- `product-design/knowledge-object-authoring/Knowledge_Objects_Review_Board.png`
- `product-design/knowledge-object-authoring/Authoring_Flows_Review_Board.png`
- `product-design/knowledge-object-authoring/Edit_Patterns_Review_Board.png`
- `product-design/qa/Baseline_vs_Knowledge_Object_Authoring.png`

## 1. Integration Detail

Integration 被定义为正式 Knowledge Object。当前原型以 `equipment.status.changed` 为例，表达：

- Header：Name、Integration Type、Source System、Target System、Direction、Knowledge Status、Edit
- Overview：Purpose、Topic / Queue / Exchange、Delivery、Publisher、Consumer
- Message Flow：`MES → RabbitMQ → Equipment Gateway`
- Related Business Functions
- Related Data
- Message / Data Contract
- Code References，作为 Evidence 的一种来源
- Evidence
- Unknown Items
- Integration-level“关系与缺口”Context Rail

Context Rail 仅承担 Integration 级关系与缺口摘要：参与系统、关联功能、相关数据、开放待确认事项，不复制 Main Content 的消息契约与证据详情。

Integration Preview / Drawer 用于在 Business Function、System 或其他知识对象中快速查看选中的 Integration。Drawer 展示对象摘要、消息路径、关键契约、核心证据与待确认事项，并提供“打开完整 Integration Detail”。

原型：

- `01_Integration_Detail.png`
- `02_Integration_Preview_Drawer.png`

## 2. Business Rule Detail

Business Rule 被定义为正式 Knowledge Object。当前原型以“显示状态计算”为例，表达：

- Header：Rule Name、System、Related Business Function、Knowledge Status、Edit
- Overview：Description、Input、Output
- Rule Definition：Condition、Additional Condition、Result、Exception / Fallback
- Input Data
- Related Fields
- Related Integrations
- Code References / Evidence
- Unknown Items
- Rule-level“关系与缺口”Context Rail

规则条件保留技术标识原文，例如：

`STATE_FLAG IN ('10','20','30')`

Business Rule Preview / Drawer 用于在 Business Function Detail 的规则列表中原位查看规则内容。Drawer 只展示当前规则的定义、输入、关联字段、核心证据和待确认事项，不复制 Function Main Content。

原型：

- `03_Business_Rule_Detail.png`
- `04_Business_Rule_Preview_Drawer.png`

## 3. Create Knowledge Object Flow

统一入口为顶栏 `+ 新增`。第一步选择知识对象类型：

- 系统
- 业务功能
- 数据库知识
- 业务规则
- 集成关系
- 待确认事项
- 证据

选择后进入轻量 Focused Form，仅要求最小必要信息。创建成功后，用户可以继续补充业务知识、显式关系和证据。新对象默认 Knowledge Status 为“未知”，创建动作不会自动推进知识状态。

最小字段建议：

| 对象类型 | 最小必要信息 |
| --- | --- |
| 系统 | System Name、Display Name、System Type |
| 业务功能 | System、Function Name、Purpose |
| 数据库知识 | Database / Schema、Object 或 Column、简短业务描述 |
| 业务规则 | System、Rule Name、Description；Related Function 可选 |
| 集成关系 | Integration Type、Name、Source System、Target System、Purpose |
| 待确认事项 | Question、System、Related Object、Priority |
| 证据 | Evidence Type、Source、支持的 Claim、关联对象 |

创建按钮区分：

- `仅创建`：保存最小信息并返回对象详情
- `创建并继续补充`：保存后进入 Relationship / Evidence / Business Knowledge 的后续补充

原型：

- `05_Create_Object_Type_Chooser.png`
- `06_Create_Business_Rule_Minimum_Form.png`

## 4. Add Relationship Flow

Relationship 是显式知识，不依赖描述文本隐含表达。统一流程：

1. 选择关系类型
2. 选择目标对象类型
3. 搜索目标 Knowledge Object
4. 在 System Context 中 Preview Target
5. 补充可选的关系说明
6. 保存关系

保存后打开 Relationship Detail Drawer，清楚展示：

- Source Object
- Relationship Type
- Target Object
- Relationship Description
- Knowledge Status
- Evidence
- Related Unknown Items

新关系默认“未知”。用户需要添加 Evidence 后，再通过明确操作决定是否推进到“推断”。

原型：

- `07_Add_Relationship_Target_Preview.png`
- `08_Relationship_Saved_Unknown.png`

## 5. Add Evidence Flow

Evidence 不是附件中心，而是“为什么我们相信这条知识”的依据。支持类型：

- 代码引用
- SQL
- 数据库样本
- 数据库注释
- 现有文档
- API / MQ
- 人工确认

统一流程：

1. 选择 Evidence Type
2. 关联 Knowledge Object
3. 关联具体 Claim / Business Knowledge
4. 可选关联 Relationship、Unknown Item 或 Investigation Finding
5. 填写 Source、Locator / Reference、摘要与“为什么支持该主张”
6. 保存 Evidence
7. 查看知识影响，并显式决定是否改变 Knowledge Status

状态推进原则：

- 普通 Evidence 添加后，可建议 `未知 → 推断`，但不自动改变
- Human Confirmation 添加后，可建议 `推断 → 已确认`，但仍需用户预览知识影响并执行明确操作
- Unknown Item Status 与 Knowledge Status 始终独立；添加 Evidence 不自动关闭待确认事项

原型：

- `09_Add_Evidence_Authoring.png`
- `10_Evidence_Added_Status_Decision.png`
- `11_Human_Confirmation_Status_Preview.png`

## 6. 必要 Edit 状态

编辑模式复用三类模式，不为每个对象创造新的视觉结构：

| 对象 | 编辑模式 | 设计说明 |
| --- | --- | --- |
| System | Overview Inline Edit | 仅将 Overview 切换为编辑态，知识概况、业务功能和 Context Rail 保持可读 |
| Business Function | Overview Inline Edit | Purpose、Caller、Input、Output 原位编辑；Process、Related Data 保持可读 |
| Database Knowledge | Column Drawer Edit | 编辑业务知识、已知值与证据；数据库元数据保持只读 |
| Business Rule | Drawer Edit | 聚焦描述、Condition、Result、Evidence；关系通过 Add Relationship 维护 |
| Integration | Drawer Edit | 聚焦 Purpose、Endpoint / Topic / Queue、Delivery、Evidence；参与方关系单独维护 |

Focused Form 仅用于初次创建，或确实跨越多个 Section 的大范围变更。保存内容不自动改变 Knowledge Status。

原型：

- `12_Edit_System_Inline.png`
- `13_Edit_Business_Function_Inline.png`
- `14_Edit_Database_Knowledge_Drawer.png`
- `15_Edit_Business_Rule_Drawer.png`
- `16_Edit_Integration_Drawer.png`

## 7. Knowledge Flow 交互验证

### Flow A

`System → Business Function → Business Rule → Database Field → Evidence`

验证结果：System Detail 进入 `Equipment Status Query`；Function Detail 选择“显示状态计算”；Rule Detail / Drawer 中选择 `MES.TABLE_EQP.STATE_FLAG`；进入 Column Drawer 后查看支持字段含义与规则判断的 Evidence。

### Flow B

`System → Business Function → Integration → Target System`

验证结果：MES System 进入 `Equipment Status Query`；从集成关系选择 `equipment.status.changed`；Integration Drawer / Detail 展示 `MES → RabbitMQ → Equipment Gateway`；选择 Target System 继续进入 Equipment Gateway System Detail。

### Flow C

`Unknown Item → Investigation Finding → Evidence → Conclusion → Knowledge Update → Confirmed Knowledge`

验证结果：复用已确认的 Unknown Item Detail 闭环；Evidence Authoring 可绑定 Finding；Resolution 显示 Knowledge Update Preview；应用更新后通过明确操作将关联字段从“推断”推进为“已确认”，最后关闭待确认事项。

### Flow D

`Global Search → Knowledge Object → Relationships → Related Knowledge Object`

验证结果：全局搜索按类型打开目标对象；对象 Detail 的 Context Rail 或关联行打开 Object / Relationship Drawer；从 Drawer 的明确入口进入相关对象完整 Detail。

### Flow E

`Create Knowledge Object → Add Relationship → Add Evidence → Inferred → Human Confirmation → Confirmed`

验证结果：`+ 新增` 选择对象类型并以最小信息创建；Add Relationship 明确选择关系与目标；Add Evidence 绑定主张；用户显式执行“标记为推断”；添加 Human Confirmation 后预览影响，再显式执行“标记为已确认”。整个过程无自动状态跳转。

## 桌面响应式验证

- 1920px：允许 Navigation、Main Content、Context Rail 与 Detail / Authoring Drawer 同时存在。
- 1440px / 1366px：Drawer 打开时暂时收起或隐藏 Context Rail，优先保证 Main Content 与 Drawer 的可读宽度。
- Drawer 不叠加；新选择的对象或操作在当前 Drawer 中替换内容。

## 需要统一 Review 的设计决策

1. Integration 与 Business Rule 成为正式 Knowledge Object，但 MVP 不增加独立 List Page。
2. 新建 Knowledge Object 和新建 Relationship 的初始 Knowledge Status 均为“未知”。
3. Evidence 可以给出状态推进建议，但永远不自动改变 Knowledge Status。
4. Human Confirmation 是 Evidence 类型；`推断 → 已确认` 仍需独立、明确的确认操作。
5. 1440px / 1366px 中，Add Relationship、Add Evidence 和 Edit Drawer 打开时临时替代 Context Rail。
6. 编辑模式映射固定为：System / Business Function 使用 Inline Edit；Database Knowledge / Business Rule / Integration 使用 Drawer Edit。

## Product Design 生成与 QA 记录

本轮使用 Product Design 的 ImageGen 工作流，逐屏生成并在选定基线图上保持同一浅色 Application Shell、简体中文 UI、高信息密度、技术字段表达、Context Rail 与 Drawer 结构。Prompt 集合按以下任务分组：

- Knowledge Object Detail：Integration、Business Rule
- Object Preview：Integration Drawer、Business Rule Drawer
- Progressive Create：类型选择、最小必要信息创建
- Explicit Relationship：目标搜索与预览、保存后的 Relationship Detail
- Evidence as first-class knowledge：证据绑定、状态建议、Human Confirmation
- Edit In Context：System / Function Inline Edit、Database / Rule / Integration Drawer Edit

最终项目资产已复制到 `product-design/knowledge-object-authoring/`。一致性 QA 对比板位于 `product-design/qa/Baseline_vs_Knowledge_Object_Authoring.png`。检查结论：Application Shell、主内容密度、Context Rail 职责、Drawer 层级、证据表达、知识状态推进与简体中文术语均与当前 Design Baseline 一致。
