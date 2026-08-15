# System Knowledge Hub — Project File Map

本文件描述当前仓库中主要目录和文件的职责。范围为 Bootstrap 基础设施与 **VS-01～VS-09B** 已实现的 Vertical Slice；不把 `bin/`、`obj/`、`node_modules/`、`dist/`、lock 文件、运行时 SQLite 数据、普通 Migration 生成文件逐项列入。

## 1. Backend

### 1.1 Application foundation

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `src/SystemKnowledgeHub.Api/Program.cs` | 配置 Controllers、JSON、Persistence、CORS 和应用管线。 | Backend foundation | ASP.NET Core 的组合根与运行入口。 |
| `src/SystemKnowledgeHub.Api/SystemKnowledgeHub.Api.csproj` | 定义 .NET 8 Web 项目及 EF Core SQLite 依赖。 | Backend foundation | 让后端可还原、构建和运行。 |
| `src/SystemKnowledgeHub.Api/Persistence/KnowledgeHubDbContext.cs` | 提供当前已落地实体的唯一 EF Core DbContext。 | Persistence foundation / VS-01～VS-09B | 连接应用查询、写入、映射和 SQLite。 |
| `src/SystemKnowledgeHub.Api/Persistence/DbContextConfiguration.cs` | 解析 SQLite 路径，注册 DbContext，并集中设置 SQLite PRAGMA。 | Persistence foundation | 保证开发和运行环境使用一致的连接规则。 |
| `src/SystemKnowledgeHub.Api/Persistence/KnowledgeHubDesignTimeDbContextFactory.cs` | 为 EF CLI 创建设计时 DbContext。 | Persistence tooling / VS-01 | 生成和检查 Migration 时不依赖启动 Web Host。 |
| `src/SystemKnowledgeHub.Api/Persistence/Concurrency/ConcurrencyTokenCodec.cs` | 在整数版本与 opaque `concurrencyToken` 之间安全编解码。 | Concurrency foundation / VS-01 + VS-03 | 支撑详情读取与 C02 条件更新，同时避免客户端理解物理版本。 |
| `src/SystemKnowledgeHub.Api/Shared/Api/ApiIdParser.cs` | 统一校验 API 路由中的正整数 ID。 | Shared API foundation | 防止各 Controller 重复实现 ID 边界规则。 |
| `src/SystemKnowledgeHub.Api/Shared/Api/Contracts/ApiErrorResponse.cs` | 定义冻结的 API 错误响应形状。 | Shared API foundation | 让 Controller 以一致结构返回 400/404/422。 |
| `src/SystemKnowledgeHub.Api/Shared/Domain/KnowledgeStatus.cs` | 定义跨 Feature 共用的封闭 KnowledgeStatus 枚举。 | Shared domain vocabulary / VS-01 + VS-02 | 让 System 与 Database Knowledge 使用同一稳定英文持久化值。 |

`src/SystemKnowledgeHub.Api/Persistence/Migrations/` 保存 EF Core Migration 和 Model Snapshot；它们是已实现 Slice 物理 Schema 的演进记录，但普通生成文件不在本地图中逐项说明。

### 1.2 Bootstrap feature

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `src/SystemKnowledgeHub.Api/Features/Bootstrap/BootstrapController.cs` | 暴露临时 `/api/bootstrap/status`，验证 Controller → DbContext → SQLite 基础链路。 | Implementation Bootstrap | 为业务 Slice 之前的基础工程提供可运行诊断点，并继续支撑 Bootstrap smoke test。 |

### 1.3 DatabaseKnowledge — VS-01 domain/persistence model

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Domain/DatabaseSource.cs` | 表达一个系统登记的实际数据库来源。 | DatabaseKnowledge / VS-01 | DatabaseObject 必须归属明确的数据源。 |
| `.../Domain/DatabaseObject.cs` | 表达 Table/View 及其业务说明、状态和版本。 | DatabaseKnowledge / VS-01 | 是 Database Object Detail 的主查询对象。 |
| `.../Domain/DatabaseColumn.cs` | 表达数据库字段元数据、业务知识、状态和版本。 | DatabaseKnowledge / VS-01 | 支撑字段表与 Column Detail Drawer。 |
| `.../Domain/ColumnKnownValue.cs` | 表达字段的一条已知值及业务含义。 | DatabaseKnowledge / VS-01 | Drawer 需要展示 `STATE_FLAG` 已知值。 |
| `.../Domain/DatabaseEnums.cs` | 定义数据库对象类型与访问模式枚举。 | DatabaseKnowledge / VS-01 | 保持 Table/View 与 Read/Write 值受控。 |
| `.../Persistence/DatabaseSourceConfiguration.cs` | 映射 `database_sources` 的字段、FK、约束和索引。 | DatabaseKnowledge / VS-01 | 落实冻结 Database Model。 |
| `.../Persistence/DatabaseObjectConfiguration.cs` | 映射 `database_objects` 及状态、JSON、版本、索引。 | DatabaseKnowledge / VS-01 | 保证详情查询建立在 canonical 物理模型上。 |
| `.../Persistence/DatabaseColumnConfiguration.cs` | 映射 `database_columns` 及唯一性、状态和版本。 | DatabaseKnowledge / VS-01 | 保证字段顺序、名称和并发读取规则。 |
| `.../Persistence/ColumnKnownValueConfiguration.cs` | 映射 `column_known_values` 及父级 FK 和唯一值。 | DatabaseKnowledge / VS-01 | 保存真实的一对多 Known Values。 |
| `.../Persistence/DatabaseKnowledgeDevelopmentData.cs` | 在 Development 中幂等迁移并写入有限 MES 演示数据。 | DatabaseKnowledge / VS-01 | 让 RP-07 在本地通过真实 SQLite 可直接评审。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Api/Features/DatabaseKnowledge/`。

### 1.4 Systems — VS-02 + VS-03

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Domain/KnowledgeSystem.cs` | 表达唯一 canonical System 实体、生命周期、知识状态和版本。 | Systems / VS-02 | 承接原 VS-01 最小父实体并继续作为 DatabaseSource 的同一 FK 目标。 |
| `.../Domain/SystemLifecycle.cs`、`SystemTechnologyTag.cs` | 定义系统生命周期与技术标签一对多数据。 | Systems / VS-02 | 支撑冻结列表筛选、展示和最小创建后的渐进补充。 |
| `.../Persistence/SystemConfiguration.cs`、`SystemTechnologyTagConfiguration.cs` | 映射唯一 `systems` 表及技术标签表。 | Systems / VS-02 | 保证 canonical mapping、唯一约束、版本和受限删除行为。 |
| `.../Application/SystemQueries.cs` | 以明确 Projection 实现 Q04 系统列表与 Q05 系统详情。 | Systems / VS-02 + VS-03 | 列表和详情不返回 EF Entity，也不建立通用查询框架。 |
| `.../Application/SystemService.cs` | 实现 C01 最小创建与 C02 概览条件更新。 | Systems / VS-02 + VS-03 | 保持明确写用例、字段白名单和整数版本并发控制。 |
| `.../Application/Models/SystemModels.cs` | 定义 Systems 列表、详情、创建和概览更新的应用模型。 | Systems / VS-02 + VS-03 | 让 Application 与 HTTP Contract 边界明确。 |
| `.../Api/Contracts/SystemRequests.cs`、`.../Api/SystemsController.cs` | 实现系统列表/创建、Q05 详情与 C02 概览 canonical routes。 | Systems / VS-02 + VS-03 | 严格对齐冻结 API，并集中处理校验、404 与 409。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Api/Features/Systems/`。

### 1.5 DatabaseKnowledge — VS-01 application/API

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Application/DatabaseKnowledgeQueries.cs` | 通过 EF Projection 实现 Q09 与 Q10 页面查询。 | DatabaseKnowledge / VS-01 | 在不加载完整 Entity Graph 的情况下构造冻结响应。 |
| `.../Application/Models/DatabaseKnowledgeReadModels.cs` | 定义 Database Object Detail 与 Column Detail 的明确读模型。 | DatabaseKnowledge / VS-01 | 避免 Controller 直接返回 EF Entity，并匹配冻结 API Contract。 |
| `.../Api/DatabaseObjectsController.cs` | 实现 `GET /api/database-objects/{id}` 与可选字段选择。 | DatabaseKnowledge / VS-01 | 暴露 Q09 canonical HTTP 边界。 |
| `.../Api/DatabaseColumnsController.cs` | 实现 `GET /api/database-columns/{id}`。 | DatabaseKnowledge / VS-01 | 暴露 Q10 canonical HTTP 边界。 |

### 1.6 BusinessFunctions — VS-04 + VS-05

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Domain/BusinessFunction.cs`、`BusinessProcessStep.cs`、`RewriteStatus.cs` | 定义 canonical 业务功能、简单有序流程步骤与改写状态。 | BusinessFunctions / VS-04 | 支撑 Q06/Q07，且不提前引入规则、证据或关系框架。 |
| `.../Persistence/BusinessFunctionConfiguration.cs`、`BusinessProcessStepConfiguration.cs` | 映射 `business_functions`、`business_process_steps` 的字段、FK、约束和索引。 | BusinessFunctions / VS-04 | 落实当前 Slice 所需的冻结 SQLite Schema。 |
| `.../Persistence/BusinessFunctionDevelopmentData.cs` | 幂等写入有限业务功能和流程步骤开发数据。 | BusinessFunctions / VS-04 | 让列表与详情通过真实 SQLite 可直接验证。 |
| `.../Application/BusinessFunctionQueries.cs`、`Models/BusinessFunctionReadModels.cs` | 以明确 Projection 实现 Q06 列表和 Q07 详情，并读取真实关系摘要。 | BusinessFunctions / VS-04 + VS-05 + VS-08 | 返回页面组合模型，并让关联数据与 Context Rail 反映 canonical KnowledgeRelation。 |
| `.../Application/BusinessFunctionService.cs`、`Models/BusinessFunctionWriteModels.cs` | 显式实现 C05 最小创建、C06 概览更新和 C07 流程完整替换。 | BusinessFunctions / VS-05 | 集中执行业务校验、版本检查和一次事务保存，不引入通用 CRUD Service。 |
| `.../Api/Contracts/BusinessFunctionRequests.cs` | 定义 C05～C07 的输入 Contract。 | BusinessFunctions / VS-05 | 保持 HTTP 输入与 EF Entity 分离，并承载 actor snapshot 与 opaque token。 |
| `.../Api/BusinessFunctionsController.cs` | 实现 Q06/Q07 与 C05～C07 canonical routes。 | BusinessFunctions / VS-04 + VS-05 | 暴露冻结的读取、创建和 Section 更新 HTTP 边界。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Api/Features/BusinessFunctions/`。

### 1.7 Evidence — VS-06

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Domain/Evidence.cs` | 定义 canonical Evidence、受控类型、Subject 绑定、完整提供人快照与并发版本。 | Evidence / VS-06 | 持久化“为什么相信这条知识”，不建立通用附件或 Claim 框架。 |
| `.../Persistence/EvidenceConfiguration.cs` | 映射 `evidence` 表、JSON/enum CHECK、Subject 和来源索引。 | Evidence / VS-06 | 落实冻结 SQLite Schema 及受控 `type + id`。 |
| `.../Application/EvidenceSubjectResolver.cs` | 对已落地 SubjectType（含 KnowledgeRelation）做显式存在性与上下文解析。 | Evidence / VS-06 + VS-08 | 保护受控多态边界，不演变为 Generic Knowledge Resolver。 |
| `.../Application/EvidenceQueries.cs`、`EvidenceService.cs` 与 `Models/EvidenceModels.cs` | 实现 Q16 以及 C23～C25 的明确读写用例。 | Evidence / VS-06 | 保证 Update 只纠正允许字段，且保存 Evidence/人工确认不自动改变知识状态。 |
| `.../Api/EvidenceController.cs` 与 `Api/Contracts/EvidenceContracts.cs` | 实现 Evidence 新增、详情、纠正和人工确认的 canonical routes/contracts。 | Evidence / VS-06 | 向前端暴露冻结 HTTP 边界，不增加删除、Rebind 或状态路由。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Api/Features/Evidence/`。

### 1.8 KnowledgeStatus — VS-07

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Application/KnowledgeStatusPolicy.cs` | 校验前进门槛、禁止跳级及显式回退原因。 | KnowledgeStatus / VS-07 | 将冻结的状态 progression 规则集中为具体策略，而非通用状态机框架。 |
| `.../Application/KnowledgeStatusService.cs`、`KnowledgeStatusModels.cs` | 对已落地的受控 Subject 执行 C26、Evidence 门槛校验与条件更新。 | KnowledgeStatus / VS-07 | 保证状态只由用户显式操作改变，并复用 canonical entity/version。 |
| `.../Api/KnowledgeStatusController.cs`、`Api/Contracts/KnowledgeStatusContracts.cs` | 实现 `PUT /api/knowledge-status` 的冻结请求、响应和错误边界。 | KnowledgeStatus / VS-07 | 暴露 C26 唯一 canonical route，不建立 Generic Knowledge Service。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Api/Features/KnowledgeStatus/`。

### 1.9 Relationships — VS-08

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Domain/KnowledgeRelation.cs` | 定义 canonical KnowledgeRelation、受控端点类型和 RelationType。 | Relationships / VS-08 | 将关系保存为显式知识，而非自由文本或万能 RelatedTo。 |
| `.../Persistence/KnowledgeRelationConfiguration.cs` | 映射 `knowledge_relations` 的端点、状态、版本、索引和精确去重约束。 | Relationships / VS-08 | 落实冻结 SQLite Schema，并保留受控多态的应用层校验边界。 |
| `.../Application/RelationshipEndpointPolicy.cs`、`RelationshipTargetResolver.cs` | 校验 RelationType 端点矩阵、System Context 并解析/搜索当前已落地目标。 | Relationships / VS-08 | 防止非法端点、跨系统 Calls 和不存在目标进入持久化。 |
| `.../Application/RelationshipQueries.cs`、`RelationshipService.cs` 与 `Models/RelationshipModels.cs` | 实现 Q03/Q15 与 C20～C22 的明确读写用例。 | Relationships / VS-08 | 支撑目标搜索、详情、说明更新及受 Evidence 门槛保护的状态推进。 |
| `.../Api/RelationshipsController.cs`、`KnowledgeTargetsController.cs` 与 `Api/Contracts/RelationshipContracts.cs` | 暴露 Relationship 与 Target Search 的 canonical routes/contracts。 | Relationships / VS-08 | 严格保持冻结 HTTP 边界，不新增第二套路由或通用对象浏览器。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Api/Features/Relationships/`。

### 1.10 UnknownItems — VS-09A + VS-09B

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Domain/UnknownItem.cs` | 定义事项、Target、Finding、Resolution、KnowledgeUpdate、Activity 及冻结状态枚举。 | UnknownItems / VS-09A | 保存真实调查闭环，并明确区分事项状态与 KnowledgeStatus。 |
| `.../Persistence/UnknownItemConfigurations.cs` | 映射六张调查闭环表的约束、索引、受控多态引用和并发版本。 | UnknownItems / VS-09A | 落实当前 Slice 所需 canonical SQLite Schema。 |
| `.../Application/UnknownItemQueries.cs`、`UnknownItemService.cs` 与 `Models/UnknownItemModels.cs` | 实现 Q11/Q12、C27～C31，并提供受事项状态约束的 Resolution Draft。 | UnknownItems / VS-09A + VS-09B | 保证调查内容与 Activity 原子保存，并为显式 Apply/确认闭环提供准确投影。 |
| `.../Application/KnowledgeResolutionService.cs` | 具体实现 C32a/C32b/C32e 与 C33～C35 的原子 Apply、结论确认、关闭和重新打开。 | UnknownItems / VS-09B | 只通过明确目标用例修改正式知识；不建立通用 Patch、Undo 或 Workflow Engine。 |
| `.../Api/UnknownItemsController.cs` 与 `Api/Contracts/UnknownItemContracts.cs` | 暴露列表、调查及 Knowledge Resolution 动作的唯一 canonical routes/contracts。 | UnknownItems / VS-09A + VS-09B | 对齐冻结 API，并集中映射 400/404/409/422。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Api/Features/UnknownItems/`。

### 1.11 BusinessRules — VS-10

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../Domain/BusinessRule.cs`、`Persistence/BusinessRuleConfiguration.cs` | 定义并映射 canonical BusinessRule、结构化 InputData、System 归属、状态与并发版本。 | BusinessRules / VS-10 | 落实唯一 `(SystemId, RuleName)` 规则，且不保存 BusinessFunction FK。 |
| `.../Application/BusinessRuleQueries.cs`、`BusinessRuleService.cs` 与 `Models/BusinessRuleModels.cs` | 实现 Q13、C15、C16 的详情、最小创建和对象自身字段更新。 | BusinessRules / VS-10 | 以明确投影和用例支撑 RP-10/DR-05/DR-12，不生成通用 CRUD。 |
| `.../Api/BusinessRulesController.cs` 与 `Api/Contracts/BusinessRuleRequests.cs` | 暴露 BusinessRule 的 canonical POST/GET/PUT contracts。 | BusinessRules / VS-10 | 保持 frozen route、opaque token 与错误语义。 |
| `Persistence/Migrations/20260815080341_AddBusinessRules.cs` | 增量创建 canonical `business_rules` 表、约束和索引。 | BusinessRules / VS-10 | 只增加本 Slice 真正需要的持久化结构。 |
| `Features/UnknownItems/Application/KnowledgeResolutionService.cs`（BusinessRule 分支） | 具体实现 C32c 业务规则知识更新的校验、原子 Apply 与 Applied 记录。 | UnknownItems + BusinessRules / VS-10 | 通过明确用例修改规则，避免 Generic Patch Engine。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Api/Features/BusinessRules/`；Migration 路径相对于 `src/SystemKnowledgeHub.Api/`。

## 2. Frontend

### 2.1 Application and framework foundation

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `src/SystemKnowledgeHub.Web/src/main.ts` | 调用前端 Bootstrap 函数。 | Frontend foundation | 浏览器入口保持最小且明确。 |
| `src/SystemKnowledgeHub.Web/src/App.vue` | 根据 Route Meta 包装全局 Element Plus 中文环境与 App Shell。 | Frontend foundation | 为所有 Route 提供一致根布局。 |
| `.../src/app/bootstrap/bootstrapApp.ts` | 创建 Vue App，注册 Pinia、Router、实际使用的 Element Plus 组件和全局样式。 | Frontend foundation | 集中完成客户端组合和挂载。 |
| `.../src/app/config/env.ts` | 读取并规范化 `VITE_API_BASE_URL`。 | Frontend foundation | API 地址配置不散落到 Feature。 |
| `.../src/app/config/locale.ts` | 提供 Element Plus 简体中文 locale。 | Frontend foundation | 执行冻结的中文 UI 规则。 |
| `.../src/app/router/index.ts` | 创建 History Router 并维护页面标题。 | Frontend foundation | 提供统一 Route 运行时。 |
| `.../src/app/router/routes.ts` | 注册 Bootstrap、已落地正式页面和 Not Found Route。 | Bootstrap + VS-01～VS-09A | 把 Database、Systems、Business Functions 与 Unknown Items 的正式 Route 绑定到页面。 |
| `.../src/app/router/navigation.ts` | 定义当前 Shell 导航项及启用状态。 | Frontend foundation / VS-01 + VS-02 + VS-04 | 启用已实现入口，并让其他入口保持可见但不可用。 |
| `.../src/types/router.d.ts` | 扩展 Vue Router Meta 类型。 | Frontend foundation | 对 layout、navigation 和 Context Rail 元数据做类型约束。 |
| `.../src/app/stores/overlays.ts` | 保存单实例 Drawer/Dialog 的轻量 descriptor。 | Overlay foundation / VS-01 | 实现不嵌套、可替换的全局 Overlay 规则。 |
| `.../src/app/stores/actor.ts` | 保存第一版创建人名称与角色快照。 | Authoring foundation / VS-02 | C01 需要人员快照，但 MVP 不建立人员中心。 |

### 2.2 Shared HTTP and UI foundation

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../src/api/client/apiClient.ts` | 用 native fetch 提供 typed decoder、AbortSignal、JSON 和错误规范化。 | Shared HTTP foundation | Feature API 不直接处理重复的 HTTP 细节。 |
| `.../src/api/contracts/errors.ts` | 定义冻结 API error code/payload 的前端类型。 | Shared HTTP foundation | 让错误边界与后端契约一致。 |
| `.../src/api/errors/ApiError.ts` | 定义业务、网络和非预期响应错误类。 | Shared HTTP foundation | 区分可解释 API 错误与连接/格式失败。 |
| `.../src/api/errors/normalizeApiError.ts` | 把非 2xx Response 收敛成类型化错误。 | Shared HTTP foundation | 页面和 Drawer 可展示一致错误状态。 |
| `.../src/api/contracts/knowledge.ts` | 定义前端共享的 KnowledgeStatus wire type。 | Shared contract / VS-01 + VS-02 | 避免各 Feature 重复声明核心状态。 |
| `.../src/components/feedback/LoadingState.vue` | 提供统一加载视觉。 | Shared UI foundation | 页面和 Drawer 不使用裸 `Loading...`。 |
| `.../src/components/feedback/EmptyState.vue` | 提供统一空状态。 | Shared UI foundation | 将“没有数据”与错误区分。 |
| `.../src/components/feedback/ErrorState.vue` | 提供局部错误和重试入口。 | Shared UI foundation | 支撑 Page/Drawer 各自处理失败。 |
| `.../src/components/data-display/KnowledgeStatusBadge.vue` | 将英文 KnowledgeStatus 映射为中文状态 Badge。 | Shared knowledge UI / VS-01 | wire enum 保持英文、界面保持中文。 |
| `.../src/components/data-display/KnowledgeProgression.vue` | 只读展示“未知 → 推断 → 已确认”的统一进展路径。 | Shared knowledge UI / VS-07 | 明确状态是 Knowledge Progression 而非可点击 Tab，并供不同对象复用。 |

### 2.3 Application Shell

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../src/layouts/AppShell.vue` | 组合侧栏、顶部栏、主工作区、Context Rail、全局 Overlay Hosts 及全局待确认事项创建内容。 | Application Shell / VS-09B | 实现冻结的统一桌面框架，并保证 Drawer 切换为 Dialog 时创建内容仍可挂载。 |
| `.../src/layouts/AppSidebar.vue` | 渲染正式产品标识与左侧导航。 | Application Shell | 提供稳定的全局知识入口。 |
| `.../src/layouts/AppTopBar.vue` | 渲染全局搜索、新增和人员快照；在 Systems 列表接入创建入口。 | Application Shell / VS-02 | 复用冻结 Shell 并只在已实现上下文开放写入口。 |
| `.../src/layouts/AppContentArea.vue` | 定义 Main Content 与 Context Rail 两列区域。 | Application Shell | 维持 Detail 页统一信息架构。 |
| `.../src/layouts/ContextRailHost.vue` | 提供 Feature 注入对象级 Context Rail 的目标。 | Application Shell / VS-01 | 避免页面创建第二套 Rail 容器。 |
| `.../src/layouts/DrawerHost.vue` | 承载全局单实例 Element Plus Drawer。 | Application Shell / VS-01 + VS-06 + VS-08 | Column、Evidence 与 Relationship Drawer 复用同一 Overlay 状态。 |
| `.../src/layouts/DialogHost.vue` | 承载全局单实例 Dialog 并提供 Feature 内容挂载点。 | Application Shell / VS-02 + VS-07 | 让创建与状态确认复用同一 Overlay Host，不产生页面级第二套 Dialog。 |
| `.../src/styles/tokens.css` | 定义颜色、尺寸、Rail/Drawer 宽度等全局 tokens。 | Design foundation | 避免 Feature 重复定义视觉体系。 |
| `.../src/styles/typography.css` | 定义全局字体和技术标识排版。 | Design foundation | 保持 Developer Tool 的文字层级。 |
| `.../src/styles/element-plus-overrides.css` | 约束 Element Plus 与 Golden Design Baseline 的差异。 | Design foundation | 防止页面退化为默认后台样式。 |
| `.../src/styles/app.css` | 定义 Shell、基础页面和响应式工作区样式。 | Design foundation | 在 1366/1440/1920 下维护统一布局。 |

### 2.4 Bootstrap feature

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../src/app/bootstrap/bootstrapApi.ts` | 调用并解码 Bootstrap 状态 API。 | Implementation Bootstrap | 验证 Vue → API → SQLite 基础链路。 |
| `.../src/features/bootstrap/pages/FoundationView.vue` | 展示基础工程、Drawer/Dialog Host 和连接状态。 | Implementation Bootstrap | 是 Bootstrap 阶段的人工 smoke 页面，不是正式总览。 |
| `.../src/features/bootstrap/pages/NotFoundView.vue` | 展示未匹配 Route 的统一 404 页面。 | Frontend foundation | 避免无效地址出现空白应用。 |

### 2.5 DatabaseKnowledge — VS-01

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../src/features/database-knowledge/api/databaseKnowledgeContracts.ts` | 定义并运行时解码 Q09/Q10 frozen JSON contract。 | DatabaseKnowledge / VS-01 | 外部 JSON 从 `unknown` 安全收窄为严格类型。 |
| `.../src/features/database-knowledge/api/databaseKnowledgeApi.ts` | 提供对象/字段详情 GET 方法和安全 ID helper。 | DatabaseKnowledge / VS-01 | Feature 复用 shared apiClient 且不保存状态。 |
| `.../src/features/database-knowledge/composables/useDatabaseObjectDetail.ts` | 管理对象详情加载、错误、取消和字段选择。 | DatabaseKnowledge / VS-01 | 页面不直接 fetch，也不建立通用 Detail abstraction。 |
| `.../src/features/database-knowledge/composables/useDatabaseColumnDetail.ts` | 按 Drawer descriptor ID 管理字段详情请求。 | DatabaseKnowledge / VS-01 | Drawer 数据留在局部状态，不塞入 Pinia。 |
| `.../src/features/database-knowledge/pages/DatabaseObjectDetailView.vue` | 渲染对象 Header、Metadata、Column Table，并连接 Rail/Drawer。 | DatabaseKnowledge / VS-01 | 是 RP-07 对应的正式 Route Page。 |
| `.../src/features/database-knowledge/components/DatabaseObjectContextRail.vue` | 渲染 Table-level 关系与缺口摘要。 | DatabaseKnowledge / VS-01 | 保持 Rail 与字段级 Drawer 职责分离。 |
| `.../src/features/database-knowledge/components/ColumnDetailDrawer.vue` | 渲染 Column-level 业务知识、证据、缺口和低频折叠区。 | DatabaseKnowledge / VS-01 | 是 DR-03 对应的单实例对象详情 Drawer。 |
| `.../src/features/database-knowledge/database-knowledge.css` | 定义 VS-01 页面、表格、Rail 和 Drawer 的局部样式。 | DatabaseKnowledge / VS-01 | 补充 Golden 特有布局，不污染全局 token。 |

### 2.6 Systems — VS-02 + VS-03

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../src/features/systems/api/systemsContracts.ts` | 定义并运行时解码 Q04/C01/Q05/C02 frozen JSON contract。 | Systems / VS-02 + VS-03 | 把外部 JSON 安全收窄为严格 TypeScript 类型。 |
| `.../src/features/systems/api/systemsApi.ts` | 调用系统列表、创建、详情与概览更新的 canonical routes。 | Systems / VS-02 + VS-03 | Feature 复用 native-fetch apiClient，不自行保存全局状态。 |
| `.../src/features/systems/composables/useSystemsList.ts` | 管理筛选、分页、加载、错误和刷新等页面局部状态。 | Systems / VS-02 | 遵守列表数据不进入 Pinia 的冻结决策。 |
| `.../src/features/systems/composables/useSystemDetail.ts` | 管理 Q05 加载、C02 保存、冲突与重载等详情局部状态。 | Systems / VS-03 | 发生 409 时保留编辑草稿，不引入全局详情 Store。 |
| `.../src/features/systems/pages/SystemsListView.vue` | 渲染 RP-02 系统列表并连接新增刷新与系统详情导航。 | Systems / VS-02 + VS-03 | 是 Q04 对应的正式 Route Page 与 Q05 入口。 |
| `.../src/features/systems/pages/SystemDetailView.vue` | 渲染 RP-03 系统详情，并导航到真实业务功能与数据库对象。 | Systems / VS-03 + VS-04 | 是 Q05 对应的正式 Route Page 与 Business Function 上层入口。 |
| `.../src/features/systems/components/SystemOverviewSection.vue` | 渲染系统概览只读/内联编辑、校验与并发冲突状态。 | Systems / VS-03 | 复用 ES-01 模式实现 C02，而不新增编辑 Route。 |
| `.../src/features/systems/components/SystemContextRail.vue` | 渲染系统级关系与缺口摘要。 | Systems / VS-03 | 保持 Context Rail 只回答系统关联与缺口，不复制 Main Content。 |
| `.../src/features/systems/components/CreateSystemFlow.vue` | 在全局 Dialog Host 中编排知识类型选择与系统最小创建。 | Systems / VS-02 | 落实 Progressive Documentation，而非巨大表单。 |
| `.../src/features/systems/components/CreateKnowledgeObjectChooser.vue` | 展示冻结知识对象类型入口，当前只开放 System。 | Systems / VS-02 | 保留 Golden Create Flow 信息架构且不虚构未实现能力。 |
| `.../src/features/systems/components/CreateSystemDialog.vue` | 收集 C01 最小字段、人员快照并提交创建。 | Systems / VS-02 | 创建后关闭、刷新并保持知识状态“未知”。 |
| `.../src/features/systems/systems.css` | 定义 RP-02、RP-03、ES-01 与系统创建流程的局部样式。 | Systems / VS-02 + VS-03 | 对齐 Golden 密度且不改变 Application Shell。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Web/`。

### 2.7 BusinessFunctions — VS-04 + VS-05

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../api/businessFunctionContracts.ts`、`businessFunctionsApi.ts` | 定义、解码并调用 Q06/Q07 与 C05～C07 frozen contract。 | BusinessFunctions / VS-04 + VS-05 | 外部 JSON 从 `unknown` 收窄，且只访问 canonical routes。 |
| `.../composables/useBusinessFunctionsList.ts`、`useBusinessFunctionDetail.ts` | 管理列表、详情、两类保存和并发冲突的 Feature-local state。 | BusinessFunctions / VS-04 + VS-05 | 页面不直接 fetch，也不把业务数据放入 Pinia；409 时保留草稿。 |
| `.../pages/BusinessFunctionsListView.vue` | 渲染 RP-04 列表并连接新增业务功能后的筛选刷新。 | BusinessFunctions / VS-04 + VS-05 | 完成查找、导航与 C05 创建后的即时可见闭环。 |
| `.../pages/BusinessFunctionDetailView.vue` | 渲染 RP-05，并编排概览、流程、知识状态和关系入口。 | BusinessFunctions / VS-04 + VS-05 + VS-07 + VS-08 | 保持 Read First，并让关系摘要和 Drawer 与既有 Evidence/状态能力形成闭环。 |
| `.../components/BusinessFunctionContextRail.vue` | 渲染功能级调用方、相邻功能、关系摘要与开放缺口。 | BusinessFunctions / VS-04 | 保持 Context Rail 与 Main Content 职责分离。 |
| `.../components/CreateBusinessFunctionFlow.vue`、`CreateBusinessFunctionDialog.vue` | 复用统一对象选择入口并实现 C05 最小创建表单。 | BusinessFunctions / VS-05 | 落实 Progressive Documentation，创建后仍保持“未知”。 |
| `.../components/BusinessFunctionOverviewSection.vue` | 渲染 ES-02 概览只读/Inline Edit、保存和冲突重载。 | BusinessFunctions / VS-05 | 不新增编辑 Route，并在 409 时保留用户草稿。 |
| `.../components/BusinessProcessSection.vue` | 渲染有序流程的新增、编辑、删除、移动与完整替换。 | BusinessFunctions / VS-05 | 落实 C07 的有序集合语义，继续保持简单可扫描流程。 |
| `.../business-functions.css` | 定义 RP-04/RP-05、创建、Inline Edit 和流程编辑的密度与响应式样式。 | BusinessFunctions / VS-04 + VS-05 | 继承 Design Baseline，不引入新视觉体系。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Web/src/features/business-functions/`。

### 2.8 Evidence — VS-06

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../api/evidenceContracts.ts`、`evidenceApi.ts` | 定义、解码并调用 Q16/C23～C25 frozen contract。 | Evidence / VS-06 | 将外部 JSON 安全收窄，且只访问 canonical routes。 |
| `.../components/AddEvidenceDrawer.vue` | 在固定 Subject 上收集普通 Evidence 最小必要信息与提供人快照。 | Evidence / VS-06 | 实现 DR-08，不将证据保存与知识状态推进混合。 |
| `.../components/EvidenceDetailDrawer.vue` | 呈现 DR-09 Evidence 来源、支持理由、提供人和允许纠正项。 | Evidence / VS-06 | 支持 Q16/C24 且保持 EvidenceType/Subject 不可变。 |
| `.../components/AddHumanConfirmationDrawer.vue` | 收集 DR-10 人工确认内容和完整确认人快照。 | Evidence / VS-06 | 实现 C25，并明示保存后知识状态不变。 |
| `.../components/EvidenceDrawerContent.vue`、`evidence.css` | 将三类 Evidence 状态接入全局单 Drawer Host 并实现 Golden 局部样式。 | Evidence / VS-06 | 复用已冻结 Overlay 模式，避免堆叠 Drawer 或新视觉体系。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Web/src/features/evidence/`。

### 2.9 KnowledgeStatus — VS-07

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../api/knowledgeStatusContracts.ts`、`knowledgeStatusApi.ts` | 定义、解码并调用 C26 frozen contract。 | KnowledgeStatus / VS-07 | 保持 opaque token 和受控 Target 的严格 TypeScript 边界。 |
| `.../components/KnowledgeStatusProgressionPanel.vue` | 在业务功能详情展示只读进展、当前门槛与显式推进入口。 | KnowledgeStatus / VS-07 | 让用户先读状态和依据，再执行明确操作。 |
| `.../components/KnowledgeStatusDialogContent.vue` | 在全局 Dialog Host 中确认前进目标、门槛与并发错误。 | KnowledgeStatus / VS-07 | 状态节点不可点击切换，服务端仍最终校验 Evidence/HumanConfirmation。 |
| `.../knowledge-status.css` | 定义进展面板和确认对话框的局部样式。 | KnowledgeStatus / VS-07 | 继承现有高密度浅色设计，不引入新视觉体系。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Web/src/features/knowledge-status/`。

### 2.10 Relationships — VS-08

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../api/relationshipContracts.ts`、`relationshipApi.ts` | 定义、解码并调用 Q03/Q15 与 C20～C22 frozen contract。 | Relationships / VS-08 | 将外部 JSON 安全收窄，且只访问 canonical routes。 |
| `.../components/AddRelationshipDrawer.vue` | 在已知 Source 下完成 RelationType、Target Search/Preview 和可选说明。 | Relationships / VS-08 | 实现 DR-06 的最小显式关系创建流程。 |
| `.../components/RelationshipDetailDrawer.vue` | 展示关系两端、说明、Evidence、记录和统一 Knowledge Progression。 | Relationships / VS-08 | 实现 DR-07，并将说明修改和显式状态推进留在对象上下文。 |
| `.../components/RelationshipDrawerContent.vue`、`relationships.css` | 将创建/详情接入全局单 Drawer Host 并定义 Golden 局部样式。 | Relationships / VS-08 | 避免第二套 Drawer、搜索或状态框架。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Web/src/features/relationships/`。

### 2.11 UnknownItems — VS-09A + VS-09B

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../api/unknownItemContracts.ts`、`unknownItemsApi.ts` | 定义并调用 Q11/Q12、C27～C31、C32a/C32b/C32e 与 C33～C35 frozen contract。 | UnknownItems / VS-09A + VS-09B | 保持英文 wire enum、opaque token 和 canonical routes。 |
| `.../composables/useUnknownItemsList.ts`、`useUnknownItemDetail.ts` | 管理列表筛选及详情调查/解决动作的 Feature local state。 | UnknownItems / VS-09A + VS-09B | 页面不把查询数据放入 Pinia，也不直接调用 fetch。 |
| `.../pages/UnknownItemsListView.vue`、`UnknownItemDetailView.vue` | 实现 RP-08/RP-09 的调查、预览、显式 Apply、确认、关闭和重新打开路径。 | UnknownItems / VS-09A + VS-09B | 提供正式 Route Pages，并清楚区分 Resolution、KnowledgeUpdate 与事项状态。 |
| `.../components/CreateUnknownItemFlow.vue`、`UnknownItemContextRail.vue` | 提供全局渐进式最小创建与事项级关系/知识影响/缺口摘要。 | UnknownItems / VS-09A + VS-09B | 复用全局 Dialog/Rail，不复制 Main Content 或建立新布局。 |
| `.../unknown-items.css` | 定义列表、详情、知识更新预览/Applied 状态和 Context Rail 局部样式。 | UnknownItems / VS-09A + VS-09B | 继承 Golden 的浅色、高密度 Developer Workspace 语言。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Web/src/features/unknown-items/`。

### 2.12 BusinessRules — VS-10

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../api/businessRuleContracts.ts`、`businessRulesApi.ts` | 定义、解码并调用 Q13/C15/C16 frozen contract。 | BusinessRules / VS-10 | 将外部 JSON 从 `unknown` 收窄并只访问 canonical routes。 |
| `.../composables/useBusinessRuleDetail.ts` | 管理规则详情、保存及刷新状态。 | BusinessRules / VS-10 | 页面不直接 fetch，也不引入全局业务数据状态。 |
| `.../pages/BusinessRuleDetailView.vue`、`components/BusinessRuleContextRail.vue` | 实现 RP-10 Main Content 与规则级关系/缺口摘要。 | BusinessRules / VS-10 | 保持高密度 Read First，并避免 Context Rail 复制正文。 |
| `.../components/CreateBusinessRuleFlow.vue`、`CreateBusinessRuleDialog.vue` | 接入全局“新增”并实现最小 BusinessRule 创建。 | BusinessRules / VS-10 | 创建后保持“未知”，关系和证据继续渐进补充。 |
| `.../components/BusinessRulePreviewDrawer.vue`、`EditBusinessRuleDrawer.vue`、`BusinessRuleDrawerContent.vue` | 实现 DR-05 Preview 与 DR-12 对象自身字段编辑。 | BusinessRules / VS-10 | 复用单 Drawer Host，不新增 Rule List 或 Edit Route。 |
| `.../business-rules.css` | 定义 RP-10 与 Rule Drawer 的局部密度、表格和响应式样式。 | BusinessRules / VS-10 | 继承 Frozen Design Baseline，不建立新视觉体系。 |

以上 `...` 均指 `src/SystemKnowledgeHub.Web/src/features/business-rules/`。

## 3. Tests

### 3.1 Backend tests

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj` | 定义 xUnit、WebApplicationFactory 和真实 SQLite 测试项目。 | Test foundation | 让 API/Persistence 集成测试引用正式后端。 |
| `.../TestSupport/BootstrapWebApplicationFactory.cs` | 创建共享打开的 SQLite in-memory Host，执行 Migration 与有限 seed。 | Test foundation / VS-01 | 多 HTTP/DbContext 请求可共享同一真实关系数据库。 |
| `.../Api/BootstrapApiTests.cs` | 验证临时 Bootstrap status endpoint。 | Implementation Bootstrap | 防止基础诊断链路失效。 |
| `.../Persistence/KnowledgeHubDbContextTests.cs` | 验证 SQLite provider 与关键 PRAGMA。 | Persistence foundation | 证明测试和运行配置不是 EF InMemory。 |
| `.../Persistence/DatabaseKnowledgeMappingTests.cs` | 验证 VS-01 表名、FK、nullability、并发和唯一约束。 | DatabaseKnowledge / VS-01 | 捕获 SQLite 物理映射偏差。 |
| `.../Application/DatabaseKnowledgeQueriesTests.cs` | 验证 Q09/Q10 projection、状态、选择校验和 token。 | DatabaseKnowledge / VS-01 | 证明核心读取 Use Case。 |
| `.../Api/DatabaseKnowledgeApiTests.cs` | 验证 canonical routes、成功 shape 与 400/404/422。 | DatabaseKnowledge / VS-01 | 证明冻结 HTTP contract 的外部行为。 |
| `.../Api/SystemsApiTests.cs` | 用真实 SQLite 验证 Q04/C01 以及 Q05/C02 持久化与 stale token 冲突。 | Systems / VS-02 + VS-03 | 以少量高价值测试覆盖系统列表、创建、详情和概览更新。 |
| `.../Api/BusinessFunctionsApiTests.cs` | 验证 Q06/Q07，并覆盖 C05 创建、C06/C07 持久化和 stale token 409。 | BusinessFunctions / VS-04 + VS-05 | 用少量真实 SQLite/HTTP 测试证明关键读写与并发行为。 |
| `.../Api/EvidenceApiTests.cs` | 验证普通 Evidence 新增/纠正、stale token 与人工确认快照。 | Evidence / VS-06 | 用 3 个真实 SQLite/HTTP 测试证明 Subject 绑定、可变字段和“不自动改状态”。 |
| `.../Api/KnowledgeStatusApiTests.cs` | 验证 C26 前进门槛、禁止跳级、显式回退原因、stale token 与人员快照持久化。 | KnowledgeStatus / VS-07 | 用 2 个真实 SQLite/HTTP 测试覆盖最高风险状态规则。 |
| `.../Api/RelationshipsApiTests.cs` | 验证合法/非法端点、精确去重、同系统 Calls 及关系 Evidence 状态门槛。 | Relationships / VS-08 | 用 3 个真实 SQLite/HTTP 测试覆盖本 Slice 最高风险规则。 |
| `.../Api/UnknownItemsApiTests.cs` | 验证创建/列表/详情、合法状态、并发、Finding、调查 Evidence 和 Resolution Draft。 | UnknownItems / VS-09A | 用 3 个真实 SQLite/HTTP 测试证明调查事务闭环且不修改目标知识。 |
| `.../Api/KnowledgeResolutionApiTests.cs` | 验证 concrete Apply 的原子性、合法状态顺序及 Close/Reopen 后历史 Applied Update 保留。 | UnknownItems / VS-09B | 用 3 个真实 SQLite/HTTP 测试覆盖本 Slice 最高风险规则。 |
| `.../Api/BusinessRulesApiTests.cs` | 验证 C15/Q13 唯一性、C16 保留关系/证据，以及 C32c 失败回滚与原子 Apply。 | BusinessRules / VS-10 | 用 3 个真实 SQLite/HTTP 测试覆盖本 Slice 最高风险规则。 |

以上 `...` 均指 `tests/SystemKnowledgeHub.Api.Tests/`。

### 3.2 Frontend tests

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `.../src/api/client/apiClient.spec.ts`、`.../src/api/errors/normalizeApiError.spec.ts` | 验证 shared fetch 与错误边界。 | Frontend test foundation | 保护 Feature 共用的 HTTP 行为。 |
| `.../src/app/config/env.spec.ts` | 验证 API base path 解析。 | Frontend test foundation | 防止空值或尾斜杠造成请求错误。 |
| `.../src/app/stores/overlays.spec.ts` | 验证单实例 Drawer/Dialog 替换规则。 | Overlay foundation / VS-01 | 防止 nested overlay 回归。 |
| `.../src/layouts/AppShell.spec.ts` | 验证共享 Shell 基本挂载。 | Application Shell | 保护主布局组合。 |
| `.../src/features/database-knowledge/api/*.spec.ts` | 验证 contract decoder 和安全 ID。 | DatabaseKnowledge / VS-01 | 保护前端 frozen contract 边界。 |
| `.../src/features/database-knowledge/composables/*.spec.ts` | 验证对象/字段加载与 Drawer descriptor。 | DatabaseKnowledge / VS-01 | 保护页面关键交互状态。 |
| `.../src/components/data-display/KnowledgeStatusBadge.spec.ts` | 验证 KnowledgeStatus 的冻结中文标签映射。 | Shared knowledge UI / VS-01 | 防止英文 wire value 被直接显示或中文术语漂移。 |
| `.../src/test/setup.ts` | 提供 Vitest/Vue Test Utils 的公共测试初始化。 | Frontend test foundation | 保持测试环境最小一致。 |

以上前端 `...` 均指 `src/SystemKnowledgeHub.Web/`。

## 4. Configuration

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `SystemKnowledgeHub.sln` | 聚合 API 与后端测试项目。 | Repository foundation | 统一执行 .NET restore/build/test。 |
| `global.json` | 固定 .NET SDK 8.0.404 并允许最新 patch。 | Repository foundation | 降低开发机 SDK 漂移。 |
| `NuGet.Config` | 仅声明官方 nuget.org 源。 | Repository foundation | 让受控环境无需读取用户级配置。 |
| `.gitignore` | 排除构建、依赖、SQLite、日志和 QA artifacts。 | Repository foundation | 防止生成物进入版本控制。 |
| `src/SystemKnowledgeHub.Api/appsettings.json` | 配置 SQLite connection string、日志和 hosts。 | Backend foundation | 提供默认运行配置。 |
| `src/SystemKnowledgeHub.Api/appsettings.Development.json` | 覆盖开发日志级别。 | Backend foundation | 分离开发环境设置。 |
| `src/SystemKnowledgeHub.Api/Properties/launchSettings.json` | 定义 Development profile 与 5090 端口。 | Backend tooling | 支持一致的本地 `dotnet run`。 |
| `src/SystemKnowledgeHub.Web/package.json` | 定义 Vue 依赖和 dev/build/type-check/lint/test 命令。 | Frontend foundation | Node 项目的 canonical 入口。 |
| `src/SystemKnowledgeHub.Web/vite.config.ts` | 配置 Vue、开发端口和可由 `VITE_API_PROXY_TARGET` 覆盖的 `/api` 代理。 | Frontend tooling / VS-06 | 默认仍使用 5090，同时允许在本地端口被系统保留时进行受控验证。 |
| `src/SystemKnowledgeHub.Web/vitest.config.ts` | 配置 jsdom、setup 和单 worker 测试环境。 | Frontend test foundation | 保持 Windows 受控环境下测试稳定。 |
| `src/SystemKnowledgeHub.Web/eslint.config.js` | 配置 TypeScript/Vue/Prettier 静态检查。 | Frontend quality foundation | 维护严格 TypeScript 代码质量。 |
| `src/SystemKnowledgeHub.Web/tsconfig*.json` | 分离应用与 Node tooling 的 TypeScript 编译配置。 | Frontend foundation | 支撑 vue-tsc 与 Vite 配置类型检查。 |
| `src/SystemKnowledgeHub.Web/index.html` | 提供 Vite HTML 壳和 `#app` 挂载点。 | Frontend foundation | 浏览器加载 Vue 应用所必需。 |

## 5. Documentation and design assets

| 路径 | 一句话职责 | Feature / Vertical Slice | 为什么需要 |
| --- | --- | --- | --- |
| `AGENTS.md` | 定义 Coding Agent 的强制架构、范围和验证规则。 | Repository governance | 防止后续 Slice 破坏冻结规格或过度设计。 |
| `README.md` | 提供产品目标、技术栈和当前开发方式概览。 | Repository documentation | 新参与者的入口说明。 |
| `docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md` | 指定正式 UI、状态及唯一 Golden Reference。 | Frozen product specification | 防止引用 SUPERSEDED/DEPRECATED 原型。 |
| `docs/specifications/System_Knowledge_Hub_MVP_Design_Baseline.md` | 冻结 UI 语言、布局和交互原则。 | Frozen product specification | 后续页面/实现共用视觉规则。 |
| `docs/specifications/System_Knowledge_Hub_MVP_Domain_Model.md` | 冻结 MVP 核心领域对象和边界。 | Frozen domain specification | 防止实现发明通用知识框架。 |
| `docs/specifications/System_Knowledge_Hub_MVP_Database_Model.md` | 冻结 canonical SQLite 持久化模型。 | Frozen persistence specification | Schema 与映射的唯一来源。 |
| `docs/specifications/System_Knowledge_Hub_MVP_Application_Use_Case_Model.md` | 冻结 Query/Write 用例和业务规则。 | Frozen application specification | Application 层不退化为表 CRUD。 |
| `docs/specifications/System_Knowledge_Hub_MVP_API_Contract.md` | 冻结 route、请求、响应与错误 contract。 | Frozen API specification | 前后端逐字段对齐。 |
| `docs/specifications/System_Knowledge_Hub_MVP_Solution_Structure.md` | 冻结项目、Feature 和技术结构决策。 | Frozen architecture specification | 限制项目数量和框架选择。 |
| `docs/reports/Implementation_Bootstrap_Verification_Report.md` | 记录基础工程建立时的范围和验证。 | Implementation Bootstrap | 解释现有 Shell、status API 和测试基础为何存在。 |
| `docs/reports/VS01_Database_Object_Detail_Column_Drawer_Verification_Report.md` | 记录 VS-01 实现、Schema、测试、运行和偏差。 | DatabaseKnowledge / VS-01 | 为 Review 提供可追溯完成证据。 |
| `docs/reports/VS02_System_List_Create_Verification_Report.md` | 记录 VS-02 实现、Schema、测试、运行、Golden Review 与进程清理。 | Systems / VS-02 | 为第二条 Vertical Slice Review 提供完成证据。 |
| `docs/reports/VS03_System_Detail_Overview_Edit_Verification_Report.md` | 记录 VS-03 实现、并发、测试、运行、Golden Review 与进程清理。 | Systems / VS-03 | 为第三条 Vertical Slice Review 提供完成证据。 |
| `docs/reports/VS04_Business_Function_List_Detail_Verification_Report.md` | 记录 VS-04 Schema、Q06/Q07、测试、运行、Golden Review 与进程清理。 | BusinessFunctions / VS-04 | 为第四条 Vertical Slice Review 提供完成证据。 |
| `docs/reports/VS05_Business_Function_Create_Edit_Verification_Report.md` | 记录 VS-05 C05～C07、并发、测试、运行、Golden Review 与进程清理。 | BusinessFunctions / VS-05 | 为第五条 Vertical Slice Review 提供完成证据。 |
| `docs/reports/VS06_Evidence_Verification_Report.md` | 记录 VS-06 Schema、Q16/C23～C25、测试、运行、Golden Review 与进程清理。 | Evidence / VS-06 | 为第六条 Vertical Slice Review 提供完成证据。 |
| `docs/reports/VS07_Knowledge_Status_Progression_Verification_Report.md` | 记录 VS-07 C26、门槛测试、运行闭环、Golden Review 与进程清理。 | KnowledgeStatus / VS-07 | 为第七条 Vertical Slice Review 提供完成证据。 |
| `docs/reports/VS08_Relationship_Verification_Report.md` | 记录 VS-08 Schema、Q03/Q15、C20～C22、测试、运行闭环和进程清理。 | Relationships / VS-08 | 为第八条 Vertical Slice Review 提供完成证据。 |
| `docs/reports/VS09A_Unknown_Item_Investigation_Verification_Report.md` | 记录 VS-09A Schema、Q11/Q12、C27～C31、运行闭环和进程清理。 | UnknownItems / VS-09A | 为第九阶段调查 Slice Review 提供完成证据。 |
| `docs/reports/VS09B_Knowledge_Resolution_Verification_Report.md` | 记录 VS-09B concrete Apply、结论状态动作、原子性测试、运行闭环和进程清理。 | UnknownItems / VS-09B | 为第九阶段知识解决 Slice Review 提供完成证据。 |
| `docs/reports/VS10_Business_Rule_Verification_Report.md` | 记录 VS-10 Schema、Q13/C15/C16/C32c、复用能力、测试、运行闭环与进程清理。 | BusinessRules / VS-10 | 为第十条 Vertical Slice Review 提供完成证据。 |
| `docs/reports/System_Knowledge_Hub_MVP_Final_Freeze_Validation_Report.md` | 记录 Product Design Final Freeze 的路径、引用和唯一性校验。 | Design freeze | 证明 Golden UI 包在进入实现前已冻结。 |
| `docs/reports/design-qa.md` | 记录 VS-01 Golden UI 对比与修订结果。 | DatabaseKnowledge / VS-01 | 证明详情页实现通过视觉 QA。 |
| `design-qa.md` | 记录 DR-08/DR-09/DR-10 与 VS-06 实现的同视口组合视觉对照。 | Evidence / VS-06 | 证明 Evidence 新增、详情和人工确认 Drawer 通过最终视觉 QA。 |
| `product-design/final-ui/` | 保存 Inventory 指定的正式 RP/DR/OV/ES/WF Golden 图片。 | Frozen UI assets | 是后续 UI 实现的唯一视觉真相。 |
| `product-design/archive/` | 保存已归档的重复别名/旧引用。 | Historical design assets | 避免旧资产丢失，但不得替代 Golden。 |
| `product-design/{knowledge-discovery-navigation,knowledge-object-authoring,system-detail,unknown-item-detail,qa}/` | 保存原型生成过程、Review Board 与对比材料。 | Product Design history | 支撑设计追溯；正式实现仍以 Inventory + `final-ui/` 为准。 |
| `docs/product-design/*_Product_Design.md` 与早期 MVP Design 文档 | 保存各阶段产品设计任务和演进记录。 | Product Design history | 解释设计来源；不高于冻结规格。 |
| `artifacts/` | 保存本地运行截图、日志和 QA 对比生成物，且被 Git 忽略。 | Verification evidence | 便于本地复核，不属于产品源码。 |

## 6. Potentially Unnecessary / Needs Review

共 **1** 项；本次未删除或重构。

1. **Bootstrap 诊断表面在 VS-01 后仍然保留。** `BootstrapController.cs`、`bootstrapApi.ts`、`FoundationView.vue` 及相应测试职责明确，但现在正式业务链路已经可运行；后续应确认它们继续作为长期健康诊断/开发页，还是在不再需要 Bootstrap 回归时收口。当前保留不会影响 VS-01。
