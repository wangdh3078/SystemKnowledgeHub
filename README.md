# 系统知识中心 / System Knowledge Hub

系统知识中心用于持续沉淀、关联和确认企业系统知识。

当前 MVP 聚焦于把散落在代码、数据库、接口、消息、现有文档和人员经验中的系统知识，整理为**可关联、可追溯、可逐步确认**的结构化知识。

> 当前阶段不是建设“万能知识库”，而是先跑通系统业务、数据、集成、证据和未知项的知识闭环。

## 产品要解决的问题

系统主要帮助回答：

- 这个系统负责什么？
- 一个业务功能如何工作？
- 哪些数据库表、视图和字段参与业务？
- 某个字段的真实业务含义是什么？
- 某条业务规则的条件与结果是什么？
- 系统之间通过哪些 API、MQ 或其他方式集成？
- 我们为什么相信某条知识？
- 哪些内容仍然未知、推断中或等待人工确认？

系统不是单纯的文档编辑器。Knowledge Object、Relationship、Evidence、Knowledge Status 和 Unknown Item 都是正式业务概念。

## 当前 MVP 范围

```text
System
├─ Business Function
├─ Database Knowledge
│  ├─ Database Source
│  ├─ Database Object
│  ├─ Database Column
│  └─ Known Value
├─ Business Rule
├─ Integration
├─ Relationship
├─ Evidence
└─ Unknown Item
```

## 知识成熟度

```text
未知 Unknown
    ↓
推断 Inferred
    ↓
已确认 Confirmed
```

Knowledge Status 的变化必须是明确用户操作。

添加 Evidence 不自动变成“推断”；添加 Human Confirmation 也不自动变成“已确认”。Evidence 负责回答“为什么我们相信这条知识”，状态推进是独立业务决定。

## 渐进式知识整理

系统采用 Progressive Documentation：

```text
最小信息创建
    ↓
Unknown
    ↓
补充 Relationship
    ↓
补充 Evidence
    ↓
明确标记 Inferred
    ↓
人工确认
    ↓
明确标记 Confirmed
```

“不知道”不是录入失败。尚未明确的问题可以进入 Unknown Item 调查闭环。

## Unknown Item

待确认事项用于管理真实知识缺口：

```text
问题
 ↓
调查
 ↓
Finding
 ↓
Evidence
 ↓
Conclusion
 ↓
Knowledge Update
 ↓
确认
 ↓
关闭
```

Unknown Item 工作流状态与 Knowledge Status 是不同概念，不能互相自动替代。

## 技术架构

```text
SystemKnowledgeHub/
├─ SystemKnowledgeHub.sln
├─ AGENTS.md
├─ README.md
├─ docs/
│  ├─ design/
│  ├─ specifications/
│  ├─ standards/
│  ├─ product-design/
│  ├─ reports/
│  └─ PROJECT_FILE_MAP.md
├─ src/
│  ├─ SystemKnowledgeHub.Api/
│  └─ SystemKnowledgeHub.Web/
└─ tests/
   └─ SystemKnowledgeHub.Api.Tests/
```

### Backend
- .NET 8
- ASP.NET Core Controllers
- Entity Framework Core
- SQLite
- Feature-first
- Direct DbContext
- Use Case First
- Page-oriented read projection

### Frontend
- Vue 3
- TypeScript (`strict`)
- Element Plus
- Pinia
- Native Fetch + shared API client
- Feature-first

项目刻意避免为了“企业级”而堆叠不必要的框架层。

## Backend 组织

```text
Features/
└─ DatabaseKnowledge/
   ├─ Domain/
   ├─ Application/
   ├─ Persistence/
   └─ Api/
      └─ Contracts/
```

目录按真实需要创建。Application Service 对应真实 Use Case，而不是数据库表 CRUD。页面查询允许直接使用 EF Core Projection。

当前不采用 Generic Repository、UnitOfWork Framework、CQRS Framework 或 MediatR。

## Frontend 组织

```text
src/
├─ app/
├─ shared/
└─ features/
   └─ database-knowledge/
      ├─ api/
      ├─ components/
      ├─ composables/
      ├─ pages/
      └─ types/
```

典型数据流：

```text
Page / Drawer
      ↓
Composable
      ↓
Feature API
      ↓
Shared apiClient
      ↓
/api
```

业务应用代码使用严格 TypeScript。

## API

MVP Base Path：

```text
/api
```

不增加 `/v1`。

API 原则：
- Use Case First
- UI-oriented Reads
- Explicit Commands
- Concrete Object Routes
- Read / Write Contract 分离
- HTTP Status + Error Contract
- opaque `concurrencyToken`
- English wire enums + Chinese UI

例如数据库知识读取：

```http
GET /api/database-objects/{id}
GET /api/database-columns/{id}
```

Detail API 可以返回页面所需的组合 Read Model，避免 Vue 为一个详情页拼装大量碎片请求。

## UI 设计

正式产品名称统一为 **系统知识中心**。

正式 UI 使用简体中文，技术标识保持原文。

```text
Application Shell
├─ Left Navigation
├─ Global Search
├─ Main Content
├─ Context Rail
└─ Detail / Authoring Drawer
```

Detail 页面遵循：

```text
Main Content + 对象级 Context Rail + Detail Drawer
```

Context Rail 只承担对象级关系与知识缺口摘要，不复制 Main Content。

Drawer 单实例运行：不允许 Drawer 套 Drawer；新对象/操作替换当前 Drawer。1440px / 1366px 打开 Drawer 时可以隐藏 Context Rail，1920px 可以同时保留。

正式页面必须使用 `docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md` 指定的 Golden Reference。

## 当前开发方式：Vertical Slice

项目不按“先写完全部 Entity → Service → 页面”的方式开发。

每条任务尽量跑通：

```text
SQLite
 ↓
EF Core
 ↓
Application Use Case
 ↓
ASP.NET Core Controller
 ↓
Frozen API Contract
 ↓
Typed Vue API
 ↓
Composable
 ↓
Page / Drawer
```

当前第一条正式业务 Slice：

**VS-01 — Database Object Detail + Column Drawer**

目标链路：

```text
SQLite
→ EF Core
→ Query
→ Controller
→ API
→ Vue TypeScript
→ Database Object Detail
→ Column Drawer
```

每条 Slice 完成并验证后，再决定下一条；不要顺便把整个 Feature 做完。

## 规范阅读顺序

```text
1. docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md
2. docs/specifications/System_Knowledge_Hub_MVP_Design_Baseline.md
3. docs/specifications/System_Knowledge_Hub_MVP_Domain_Model.md
4. docs/specifications/System_Knowledge_Hub_MVP_Database_Model.md
5. docs/specifications/System_Knowledge_Hub_MVP_Application_Use_Case_Model.md
6. docs/specifications/System_Knowledge_Hub_MVP_API_Contract.md
7. docs/specifications/System_Knowledge_Hub_MVP_Solution_Structure.md
8. 当前 Vertical Slice / Task Specification
```

不要用 UI 截图反推数据库 Schema，也不要为了代码方便反向修改冻结业务语义。

## Golden UI

Final UI Inventory 是受控 UI 资产索引：

- `KEEP`：当前有效
- `SUPERSEDED`：已被替代，不得用于实现
- `DEPRECATED`：禁止作为实现依据

每个正式页面/状态只使用 Inventory 指定的单一 Golden Reference。Review Board 仅用于评审。原始 ImageGen `exec-*.png` 不是正式实现依据。

历史截图中的“遗留系统知识中心 / Legacy Knowledge Hub”不是正式产品名。

## 测试与验证

Backend：

```bash
dotnet restore
dotnet build
dotnet test
```

Persistence/API 集成测试使用真实 EF Core SQLite provider，不使用 EF Core InMemory 替代关系数据库行为。

Frontend：

```bash
npm run type-check
npm run lint
npm run test
npm run build
```

完成 Vertical Slice 后还需要实际运行前后端，验证真实：

```text
Browser → API → EF Core → SQLite
```

链路。

## 当前明确不引入

MVP 不引入：
- Generic Repository
- Repository per Entity
- UnitOfWork Framework
- Specification Pattern
- CQRS Framework / MediatR
- AutoMapper / Mapster
- Generic CRUD / Query / Drawer Framework
- 第二套 Data Grid
- Axios

抽象来自真实重复需求，而不是提前设计。

## 后续方向

长期可以扩展工程知识：

```text
开发知识
开发流程
部署知识
运维知识
故障排查
架构说明
技术方案
知识记录
```

但这些**暂不属于当前 MVP**。

当前不要提前增加 `KnowledgeDocument`、Wiki、Markdown 知识文章、标签体系或文档目录树。待当前系统业务/数据/集成知识闭环稳定后，再单独设计。

## Codex / Coding Agent

仓库根目录 `AGENTS.md` 是 Coding Agent 的强制执行约束。

执行任务时：

1. 阅读 `AGENTS.md`
2. 阅读相关冻结规范
3. 阅读当前 Vertical Slice 指令
4. 检查已有实现
5. 只实现当前 Slice
6. 完成构建、测试、Runtime 与 Golden UI 验证
7. 输出任务要求的 Verification Report
8. 停止并等待 Review

**代码生成完成不等于任务完成；验证通过才算完成。**
