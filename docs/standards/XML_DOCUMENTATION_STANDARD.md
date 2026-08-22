# System Knowledge Hub — C# XML Documentation Standard

状态：XML-DOC-A01 评审产物，等待人工 Approval 后按 Feature 分批执行。

适用范围：<code>src/SystemKnowledgeHub.Api</code> 中由团队维护的 C# 源码。测试代码可复用本规范，但不要求为了覆盖率机械补充 XML 注释。EF Core 生成的 Migration / Designer 文件不属于人工补注范围。

## Goals

XML 注释用于说明仅靠名称和类型无法可靠表达的契约：

- 业务语义、使用边界与生命周期；
- nullable 值、状态值和 Result / Error 的真实含义；
- 事务、并发、Snapshot 与历史兼容等非显而易见行为；
- Current User、KnowledgeRole、Evidence、HumanConfirmation 和 KnowledgeStatus 等容易被误解的领域边界；
- 调用方必须遵守、且实现变更时必须继续保持的约束。

XML 注释不是名称翻译器，也不是实现过程复述。若删除一段注释后，读者仍能从签名和紧邻代码得到完全相同的信息，则该注释通常没有保留价值。

本仓库当前未在 <code>SystemKnowledgeHub.Api.csproj</code> 中启用 <code>GenerateDocumentationFile</code>，也未将 <code>CS1591</code> 作为构建门禁；现有人工源码几乎没有 XML 注释，Migration 中的 <code>&lt;inheritdoc /&gt;</code> 来自生成模板。因此采用按 Feature 渐进补充，不在本标准阶段启用全仓警告或批量生成注释。

## General Rules

1. 注释描述 Contract / Semantics / Boundary，不描述逐行实现。
2. 只描述当前代码、已批准设计与冻结契约已经保证的行为，不猜测未来能力。
3. XML 正文以简体中文为主；类型名、协议名和 Domain Term 保留英文，例如 User、Current User、KnowledgeRole、Evidence、HumanConfirmation、Snapshot、Result、API、SQLite。
4. 简单说明控制在 1–2 行；重要边界放入 <code>&lt;remarks&gt;</code>，不要把小论文塞进 <code>&lt;summary&gt;</code>。
5. 注释必须随契约变更一同更新。过期注释比缺少注释更危险。
6. 不为满足数量、覆盖率或 <code>public</code> 成员计数而写无信息量注释。

## Must Document

### Public types

以下由团队维护、承担可调用或业务契约的 public 类型，在其所属 Feature 进入 rollout 时必须有 <code>&lt;summary&gt;</code>：

- public interface；
- API Request / Response / Error contract；
- Application Command / Query / Result / Failure 类型；
- Controller、Application Service、Query Service 与公开 Policy；
- Domain entity、value type 和具有业务语义的 enum；
- Shared 层被多个 Feature 使用的 API、Domain 或 Persistence primitive；
- 对调用方具有非显而易见配置行为的 public Infrastructure / Persistence 类型。

“public 类型原则上需要说明”不等于“所有带 public 关键字的类型必须机械补注”。下列类型可排除：

- EF Core 生成的 Migration、Designer 与 ModelSnapshot；
- 仅为 ASP.NET Core 测试宿主暴露的空 <code>Program</code> partial；
- 纯框架适配实现，其完整契约已由 interface 说明且实现没有额外语义；
- 没有业务契约、仅因框架发现机制而为 public 的简单 EF Configuration。若配置包含 SQLite、concurrency、Snapshot、RESTRICT FK 或历史兼容等特殊行为，则转为“必须说明”。

类型摘要应回答“这个类型在系统中代表什么或承担什么契约”，而不是复述类型名。

### Public methods

以下 public 方法属于 contract entry point，必须写 XML：

- interface 成员；
- Controller action；
- Application Service / Query Service 的公开 Use Case 或查询；
- public Policy 方法；
- Shared helper 中供其他 Feature 调用的方法；
- 具有非显而易见副作用、事务或持久化行为的公开基础设施方法。

标签要求：

| 标签 | 要求 |
| --- | --- |
| <code>&lt;summary&gt;</code> | 必须；说明业务操作、查询结果或策略判断。 |
| <code>&lt;param&gt;</code> | contract declaration 上必须覆盖全部参数；说明业务含义、单位、允许值、nullable 或 opaque 语义。实现使用 <code>&lt;inheritdoc /&gt;</code> 时不重复。 |
| <code>&lt;returns&gt;</code> | 非 <code>void</code> 方法必须；解释完成后得到的业务值以及有意义的 null / Result 状态。 |
| <code>&lt;remarks&gt;</code> | 仅在存在重要边界时使用，例如 transaction boundary、不会自动推进状态、历史兼容或安全边界。 |
| <code>&lt;exception&gt;</code> | 仅当方法直接抛出、且该异常属于调用契约时使用。 |

Controller 返回的 400 / 404 / 409 / 422 是 HTTP Result，不是 CLR exception；应在摘要、remarks 或 API contract 类型中解释，不得伪造成 <code>&lt;exception&gt;</code>。

### API contracts

Request / Response / DTO 至少在类型级说明其业务角色。以下信息存在时必须记录：

- null 表示省略、清空、自动解析、无筛选或“未配置”等特殊业务语义；
- enum wire value 大小写敏感或具有受控取值；
- <code>concurrencyToken</code> 是 opaque token，客户端只能原样回传；
- Result / Error 中 validation、not found、invalid state、reference invalid、conflict 的真实区别；
- legacy read fallback 或 breaking request amendment 等兼容边界；
- Snapshot 字段是写入时历史事实，而不是动态 User / KnowledgeRole 视图。

不要逐个解释 <code>Id</code>、<code>Name</code>、<code>CreatedAt</code> 等显而易见字段。只有 JavaScript safe integer、UTC、nullable 或不可变 Snapshot 等附加语义存在时才补充。

### Domain types

Domain 注释重点说明：

- 类型代表的业务概念，而不是数据库表名；
- 当前代码真实执行的 invariant 与生命周期；
- Active / Inactive、状态转换和不允许的行为；
- Snapshot 与 canonical reference 的差别；
- 哪些保存动作不会触发其他领域状态变化。

当前 Domain entity 多数同时作为 EF Core persistence model。不得把仅由 Application Service 校验的规则写成“实体自身保证”；应明确规则由对应 Use Case / Policy 执行。

### Application services

每个公开 Use Case / query 应说明：

- 完成的业务动作或返回的页面/详情投影；
- Result 的成功值与主要失败类别；
- 关键 transaction boundary；
- 可观察 side effect；
- 容易被误判的非自动行为。

例如 HumanConfirmation create 需要说明 transaction 内 canonical User / KnowledgeRole re-read、Snapshot materialization 和 Evidence insert 属于同一操作，同时明确不自动推进 KnowledgeStatus。不要把查询、if 分支和 <code>SaveChangesAsync</code> 顺序逐行翻译成注释。

### Result / Error

Failure enum 和 Result 类型必须说明它们是 Application 层的显式结果，不是异常列表。对非显而易见 member 记录真实映射，例如：

- <code>Validation</code>：输入字段无法进入 Use Case；
- <code>NotFound</code>：route resource 或 required current entity 不存在；
- <code>InvalidState</code> / 业务 violation：资源存在，但当前状态不允许操作；
- <code>Conflict</code>：stale concurrency token 或其他明确冲突；
- <code>ReferenceInvalid</code>：引用对象不存在或不属于当前允许集合。

不要承诺 Controller 尚未实现的 HTTP status，也不要把所有失败都笼统描述为“发生错误”。

## Should Document

以下内容在有额外语义时建议写 XML：

- public / internal property 的特殊 null、UTC、Snapshot、version 或 token 语义；
- 具有跨 Feature 复用价值的 internal contract；
- enum 中名称不能完整表达的 member；
- SQLite PRAGMA、JSON constraint、RESTRICT FK、app-managed version 等非显而易见 Persistence 决策；
- 为兼容历史数据保留的读取 fallback；
- 泛型 public helper 的类型参数约束和返回语义。

如果一条说明只服务于维护当前方法内部算法，可使用普通 <code>//</code> 注释；如果代码结构已经清楚，则不加注释。

## Do Not Document Mechanically

以下内容不应机械添加 XML：

- private field、private helper、局部函数和 private nested record；
- 自动属性的逐字翻译；
- 简单 EF <code>builder.Property(...)</code>、<code>HasColumnName(...)</code> 调用；
- 构造函数依赖注入参数；
- 仅表示显而易见值的 enum member；
- 测试方法和测试 fixture 的重复说明；
- EF Core 生成文件中已有的 <code>&lt;inheritdoc /&gt;</code>；
- 实现已完整继承 interface contract 且没有额外行为的方法；
- 名称已经完整表达行为、且没有额外边界的 <code>Activate()</code> / <code>Deactivate()</code> 一类方法。

private / internal 方法通常不写 XML。遇到复杂分支、算法原因或 workaround 时，优先在相关代码旁写一句解释“为什么”的普通注释。只有 internal 类型实际形成跨 Feature contract 时，才按 public contract 的规则记录 XML。

## Tag Rules

### <code>&lt;summary&gt;</code>

- 首句直接说明业务职责或返回内容。
- 通常 1–2 行。
- 不复述类型名、方法名或参数名。
- 不包含实现步骤、性能宣称或未来规划。

### <code>&lt;remarks&gt;</code>

用于补充 summary 无法容纳的重要边界：

- Current User 不是认证身份；
- KnowledgeRole 不是 Permission Role；
- transaction boundary；
- Snapshot 不随后续 canonical 数据变化；
- 某操作不会自动推进 KnowledgeStatus；
- legacy fallback 的优先顺序。

没有这类边界时省略。

### <code>&lt;param&gt;</code>

- contract method 上覆盖全部参数，说明调用方需要知道的语义。
- 使用 <code>&lt;paramref name="..." /&gt;</code> 引用同一签名中的参数。
- 对 nullable 参数只在 null 有特殊业务含义时明确解释。
- 不写“<code>id</code> 参数”“<code>request</code> 请求”这类同义反复。

### <code>&lt;returns&gt;</code>

- 解释业务结果，不写“返回一个 Task”。
- 对 nullable 返回值说明 null 的业务含义。
- 对 Result 类型说明成功与失败由哪个字段表达，不把失败描述成异常。

对于 <code>Task&lt;T&gt;</code> / <code>ValueTask&lt;T&gt;</code>，推荐句式：

> 异步操作完成后，返回……；若……则……

对于非泛型 <code>Task</code>，说明异步操作完成所代表的业务效果。

### <code>&lt;typeparam&gt;</code>

仅用于 public generic type / method，说明类型参数在业务或协议中的角色。不要只写“类型参数”。

### <code>&lt;exception&gt;</code>

- 只列方法可能直接抛出且调用方应处理的异常。
- 使用准确的 <code>cref</code>。
- 不列数据库驱动、CLR 或框架可能间接产生的所有异常。
- 禁止 <code>&lt;exception cref="Exception"&gt;</code>。
- validation、not found、invalid state 和 concurrency conflict 若通过 Result / HTTP response 返回，不写为 exception。

### <code>&lt;value&gt;</code>

仅用于属性自身具有重要值语义时，例如 opaque token、UTC 时间或不可变 Snapshot。普通 DTO 属性和 EF scalar property 不需要逐项添加。

### <code>&lt;see cref="..." /&gt;</code>

用于链接当前 solution 中确实相关的类型或成员，例如从 Current User contract 链接到 <code>CurrentUserResolution</code>。不要为了增加链接密度而链接每个普通类型，也不要用错误的 <code>cref</code> 代替自然语言。

### <code>&lt;paramref&gt;</code>

在正文中讨论具体参数行为时使用，例如“当 <code>&lt;paramref name="knowledgeRoleId" /&gt;</code> 为 null 时”。不要在每个 param 描述中重复参数名。

### <code>&lt;c&gt;</code>

用于短标识符、header、wire value 和状态码，例如 <code>X-Current-User-Id</code>、<code>Unknown</code>、<code>409</code>。多行代码使用 <code>&lt;code&gt;</code>；不要用 <code>&lt;c&gt;</code> 包裹普通中文句子。

### <code>&lt;list&gt;</code>

仅在 remarks 中确有三个及以上并列规则、状态或步骤时使用。两项内容优先写成一句话，避免把短注释膨胀成列表。

## Async and CancellationToken

public async contract method 的 <code>&lt;returns&gt;</code> 必须描述完成后的业务值，不能写“返回 Task”。

在 interface、Controller action、Application Use Case 或 Shared contract declaration 上，<code>CancellationToken</code> 使用统一 wording：

~~~xml
<param name="cancellationToken">用于取消当前异步操作的令牌。</param>
~~~

不在每层重复扩写取消机制。实现方法若从 interface 继承契约，使用 <code>&lt;inheritdoc /&gt;</code>；private async helper 不因为带 <code>CancellationToken</code> 就补 XML。除非代码明确实现额外取消边界，否则不要声称“取消后会回滚”“立即取消”或“不会产生 side effect”。

## Nullable Semantics

nullable 只在 null 有业务意义时说明。推荐区分：

- “null 表示不筛选”；
- “null 表示清空该可选字段”；
- “null 表示没有 canonical reference，但历史 Snapshot 仍存在”；
- “null 表示由服务端解析唯一 Active KnowledgeRole”；
- “null 表示历史记录创建于该 reference 引入之前”。

不得从 <code>string?</code>、<code>long?</code> 或 <code>Foo?</code> 自行推断未实现语义。

## Interface and Implementation

interface 是 contract 的首选注释位置。实现与 interface 语义完全一致、且 IDE 能解析继承关系时，使用：

~~~csharp
/// <inheritdoc />
public Task<CurrentUserResolution> ResolveAsync(
    CancellationToken cancellationToken)
~~~

只有在隐式解析不清楚时才使用显式 <code>cref</code>。以下情况不能只写 <code>&lt;inheritdoc /&gt;</code>：

- 实现增加了调用方必须知道的约束或 side effect；
- 一个方法综合多个 interface contract；
- 类型没有可继承的文档来源；
- 文档工具或 IDE 无法解析目标。

当前项目尚未生成 XML documentation file，因此 rollout 时至少用 IDE hover / Quick Info 人工检查一处 interface 与实现的继承效果；不要盲目批量插入 <code>&lt;inheritdoc /&gt;</code>。

## Positional Records and DTO Properties

当前项目大量使用 positional record。推荐保持现有形态：

~~~csharp
/// <summary>
/// 创建 HumanConfirmation 时由客户端提交的确认事实。
/// </summary>
/// <param name="KnowledgeRoleId">
/// 本次确认使用的 KnowledgeRole；为 null 时由服务端按 Active KnowledgeRole 数量解析。
/// </param>
public sealed record AddHumanConfirmationRequest(
    EvidenceTargetRequest? Subject,
    string? SubjectDetailKey,
    long? KnowledgeRoleId,
    string? ConfirmationMethod,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmationStatement,
    string? SupportReason,
    string? SourceNote);
~~~

规则：

- 类型写 <code>&lt;summary&gt;</code>。
- 仅对有重要业务语义的 positional parameter 写详细 <code>&lt;param&gt;</code>；在启用严格文档检查前，不为其余显而易见字段制造低价值文本。
- 若未来对该类型启用参数完整性检查，可为其余参数补充简洁但有信息量的描述。
- 不得为了添加注释把 record 重构成 class 或 property model。
- 普通 property 只在值语义特殊时使用 <code>&lt;value&gt;</code>。

## Enum Rules

enum 类型有领域或协议含义时写 <code>&lt;summary&gt;</code>。member 只有在名称不足以表达边界时才写注释。

例如 <code>KnowledgeStatus</code> 的 member 应说明可信度阶段和 transition 约束，而不是简单翻译“未知 / 推断 / 已确认”。相反，若某个局部 enum 的 <code>Active</code> / <code>Inactive</code> 没有额外语义，则不需要：

~~~csharp
/// <summary>
/// 启用。
/// </summary>
Active
~~~

Failure enum member 只有在 HTTP 映射、状态存在性或 reference 语义可能混淆时才单独解释。

## Domain-specific Rules

### User

- User 是系统知识中心中的本地参与者 Profile。
- User 不等于 login account，不保存 Password、Session、Permission 或认证凭据。
- <code>IsActive=false</code> 表示停用；当前业务不提供物理删除。
- User 的 EmployeeNo、DisplayName、DepartmentOrTeam、JobTitle 等后续变化，不得被描述为会动态更新历史 Evidence Snapshot。
- 不要把普通 Actor body contract 与 Current User 自动等同。

### Current User

- 固定术语：Current User 是 operator context / 当前操作者上下文。
- <code>X-Current-User-Id</code> 只是选择当前操作者的 header，不是 authenticated user、logged-in user、security principal、claim 或 permission identity。
- <code>ICurrentUserContext</code> 的注释应说明 Missing、Invalid、NotFound、Inactive 与 Available 的 resolution 语义。
- 需要历史身份事实的写操作仍应在 transaction 内重新读取 canonical User；不能把已加载的 UI profile 描述为最终持久化事实来源。
- 只有后续已批准并实施的 Security Slice 真正改变契约后，才允许更新这些表述。

### KnowledgeRole

- KnowledgeRole 表示知识身份和 domain attribution，例如“MES 业务专家”。
- KnowledgeRole 不等于 Permission Role、authorization role 或 RBAC role。
- Active 控制新选择 / 新分配是否可用，不授予 Route、API、Edit、Confirm 或 Admin 权限。
- 停用 KnowledgeRole 不会自动删除既有 UserKnowledgeRole mapping，也不会修改历史 Snapshot。
- nullable KnowledgeRole reference 与不可变 Role Name Snapshot 必须分别说明。

### Evidence

- Evidence 回答“为什么相信这条知识”，不是通用附件中心。
- Evidence 可以支持显式 KnowledgeStatus transition，但保存 Evidence 不执行 transition。
- Provider 的 canonical User / KnowledgeRole ID 是 reference；ProviderName、ProviderRole、ProviderTeam、ProviderEmployeeNo、ProviderJobTitle 与 ProvidedAt 是写入时 Snapshot。
- 普通 Evidence 与 HumanConfirmation 的 provider 来源不同，不得用一条泛化注释抹平差异。

### HumanConfirmation

- HumanConfirmation 仍是 <code>EvidenceType.HumanConfirmation</code>，不是独立 Entity、Table、Approval 或 Permission。
- 新建 HumanConfirmation 只接收确认事实；确认人 Snapshot 由服务端根据 Current User、canonical User 与 KnowledgeRole 生成。
- 0 / 1 / multiple Active KnowledgeRole 的解析和 supplied Role 的 existence / active / assignment 校验属于必须记录的业务边界。
- Snapshot 是历史事实；User 或 KnowledgeRole 后续改名、停用或解除 mapping 不修改既有 Evidence。
- 新记录的 <code>confirmationMethod</code> 位于 locator；读取历史记录时可回退旧 <code>provider_source</code>。不得把 fallback 描述为数据回填或迁移。
- create 返回的 <code>knowledgeStatusChanged</code> 保持 false，且 Subject 的 KnowledgeStatus 不变化。

### KnowledgeStatus

- 固定 progression 是 <code>Unknown → Inferred → Confirmed</code>，但状态变更必须走显式操作。
- <code>Unknown → Confirmed</code> 不允许直接发生。
- Evidence 可满足进入 Inferred 的前置条件，HumanConfirmation 可满足进入 Confirmed 的前置条件，但二者的创建都不会自动推进状态。
- rollback、reason、evidence requirement 与 HumanConfirmation requirement 只按当前 <code>KnowledgeStatusPolicy</code> 的真实规则描述。

### Concurrency

- persistence 内部使用 app-managed integer <code>Version</code>，并由 EF Core 标记为 concurrency token。
- API 只暴露 opaque <code>concurrencyToken</code>。客户端不得解析、生成、排序或推断其内部格式。
- 修改操作原样提交最近一次读取到的 token；stale token 返回 <code>409 conflict</code>。
- invalid token 属于 validation；有效但过期的 token 属于 conflict。
- 不得在 XML 中泄露 token 当前编码格式，把它变成外部 contract，或暗示存在 ETag / If-Match 等第二套机制。

## Good Examples

以下示例展示推荐的信息密度；rollout 时应按真实签名和实现同步调整。

### 1. Current User interface

~~~csharp
/// <summary>
/// 解析当前请求的业务操作者上下文。
/// </summary>
/// <remarks>
/// Current User 仅表示 operator context，不代表经过认证的安全主体。
/// </remarks>
public interface ICurrentUserContext
{
    /// <summary>
    /// 根据当前请求中的 X-Current-User-Id 解析可用 User Profile。
    /// </summary>
    /// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
    /// <returns>
    /// 异步操作完成后，返回包含 Available、Missing、Invalid、NotFound 或 Inactive 状态的解析结果。
    /// </returns>
    Task<CurrentUserResolution> ResolveAsync(CancellationToken cancellationToken);
}
~~~

价值：定义 operator context 和安全边界，并说明状态对象而非异常。

### 2. HumanConfirmation request nullable role

~~~csharp
/// <summary>
/// 创建 HumanConfirmation 时提交的 Subject 与确认事实。
/// </summary>
/// <param name="KnowledgeRoleId">
/// 本次确认使用的 KnowledgeRole；为 null 时，服务端允许 0 个 Active Role 使用 fallback，
/// 自动采用唯一 Active Role，或在存在多个 Active Role 时返回 validation error。
/// </param>
public sealed record AddHumanConfirmationRequest(
    EvidenceTargetRequest? Subject,
    string? SubjectDetailKey,
    long? KnowledgeRoleId,
    string? ConfirmationMethod,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmationStatement,
    string? SupportReason,
    string? SourceNote);
~~~

价值：解释 null 的业务语义，没有把 record 改成 class。

### 3. HumanConfirmation Use Case

~~~csharp
/// <summary>
/// 使用当前 canonical User 记录一条 HumanConfirmation Evidence。
/// </summary>
/// <remarks>
/// User / KnowledgeRole re-read、Role 解析、Subject 与确认事实校验、Snapshot 生成和 Evidence insert
/// 位于同一 transaction。该操作不会自动推进 Subject 的 KnowledgeStatus。
/// </remarks>
/// <param name="request">当前操作者 ID、Subject、可选 KnowledgeRole 与确认事实。</param>
/// <param name="cancellationToken">用于取消当前异步操作的令牌。</param>
/// <returns>异步操作完成后，返回创建结果或明确的 validation / reference / state failure。</returns>
public Task<EvidenceCommandResult> AddHumanConfirmation(
    AddHumanConfirmationCommand request,
    CancellationToken cancellationToken);
~~~

价值：说明 transaction、Snapshot 和非自动行为，而非复述方法步骤。

### 4. Evidence domain type

~~~csharp
/// <summary>
/// 支撑某个知识 Subject 的可追溯依据。
/// </summary>
/// <remarks>
/// HumanConfirmation 是 Evidence 的一种。Provider reference 指向创建时的 canonical User /
/// KnowledgeRole，Provider 文本和时间字段保存不可变历史 Snapshot。
/// </remarks>
public sealed class Evidence
{
}
~~~

价值：解释领域目的与 reference / Snapshot 边界。

### 5. KnowledgeStatus

~~~csharp
/// <summary>
/// 表示知识从未知、推断到已确认的可信度阶段。
/// </summary>
/// <remarks>
/// 状态只通过显式 transition 操作变化；保存对象、Evidence 或 HumanConfirmation 均不会自动推进状态。
/// </remarks>
public enum KnowledgeStatus
{
    Unknown,
    Inferred,
    Confirmed,
}
~~~

价值：没有机械翻译 member，而是记录生命周期约束。

### 6. Opaque concurrency token

~~~csharp
/// <summary>
/// 返回可编辑 User 的详情和当前并发标记。
/// </summary>
/// <param name="ConcurrencyToken">
/// 客户端更新 User 时必须原样回传的 opaque token；客户端不得解析或生成。
/// </param>
public sealed record UserDetailResponse(
    long Id,
    string? EmployeeNo,
    string DisplayName,
    string? Email,
    string? DepartmentOrTeam,
    string? JobTitle,
    bool IsActive,
    IReadOnlyList<KnowledgeRoleSummaryResponse> KnowledgeRoles,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string ConcurrencyToken);
~~~

价值：记录 API 边界，不泄露内部 Version 编码。

### 7. Direct exception contract

~~~csharp
/// <summary>
/// 将内部 version 编码为 API 使用的 opaque concurrency token。
/// </summary>
/// <param name="version">从 1 开始的 app-managed version。</param>
/// <returns>调用方只能原样传递、不应解析的 token。</returns>
/// <exception cref="ArgumentOutOfRangeException">
/// <paramref name="version"/> 小于 1。
/// </exception>
public string Encode(long version);
~~~

价值：<code>ConcurrencyTokenCodec.Encode</code> 确实直接抛出该异常，因此 exception 属于真实 contract。

### 8. Persistence special behavior

~~~csharp
/// <summary>
/// 注册 KnowledgeHub 的 EF Core SQLite persistence。
/// </summary>
/// <remarks>
/// 相对 Data Source 按应用 content root 解析；连接启用 foreign keys、busy timeout 和 WAL。
/// </remarks>
/// <param name="services">应用服务集合。</param>
/// <param name="configuration">包含 KnowledgeHub connection string 的配置。</param>
/// <param name="environment">用于解析相对 SQLite 路径的宿主环境。</param>
/// <returns>完成 persistence 注册后的同一服务集合。</returns>
/// <exception cref="InvalidOperationException">
/// connection string 缺失、没有 Data Source，或相对路径无法解析。
/// </exception>
public static IServiceCollection AddKnowledgeHubPersistence(
    this IServiceCollection services,
    IConfiguration configuration,
    IWebHostEnvironment environment);
~~~

价值：解释 SQLite 特殊行为和代码中真实存在的直接异常。

### 9. Interface implementation

~~~csharp
/// <inheritdoc />
public Task<CurrentUserResolution> ResolveAsync(
    CancellationToken cancellationToken);
~~~

价值：完整 contract 已在 interface，不复制一份可能腐化的实现注释。

## Bad Examples

### 机械翻译名称

~~~csharp
/// <summary>
/// 获取用户。
/// </summary>
public Task<UserDetailResponse?> GetUser(...);
~~~

问题：没有说明返回 null 的含义，也没有说明返回的是可编辑详情与 opaque token。

### 把 Current User 写成认证身份

~~~csharp
/// <summary>
/// 获取当前登录用户。
/// </summary>
~~~

问题：当前 <code>X-Current-User-Id</code> 只是 operator context，不是 login 或 authentication。

### 把 KnowledgeRole 写成权限

~~~csharp
/// <summary>
/// 用户拥有的授权角色。
/// </summary>
~~~

问题：KnowledgeRole 是知识身份 / domain attribution，不授予权限。

### 泄露 concurrency token 实现

~~~csharp
/// <param name="concurrencyToken">Base64 编码的数据库 version。</param>
~~~

问题：API contract 要求 token opaque，编码格式不能成为客户端依赖。

### 虚构 exception

~~~csharp
/// <exception cref="Exception">操作失败时抛出。</exception>
~~~

问题：没有准确类型、条件或直接抛出保证；Result / HTTP error 也不是 CLR exception。

### 复述实现过程

~~~csharp
/// <summary>
/// 查询用户，再查询角色，然后创建对象，调用 SaveChangesAsync 并返回响应。
/// </summary>
~~~

问题：这是易腐化的实现 narration，没有说明业务 contract。

### 无证据的保证

~~~csharp
/// <remarks>此方法线程安全且高性能。</remarks>
~~~

问题：当前代码和契约没有提供这些保证。

### 复制整个规格

~~~csharp
/// <remarks>
/// （粘贴完整 API request、数据库列、UI 流程和所有验收条款）
/// </remarks>
~~~

问题：XML 应保留最接近代码的稳定契约，不复制 Specification。

## Avoiding Comment Decay

1. 先判断注释是否补充了签名无法表达的信息；没有则不写。
2. 一个 contract 只维护一个主要注释源。interface 已完整说明时，实现使用 <code>&lt;inheritdoc /&gt;</code>。
3. 业务规则变化时，把 XML 更新纳入同一 change；review 必须对照实现和冻结 contract。
4. 不记录容易变化的内部顺序、局部变量、SQL 形态或 token 编码。
5. 优先引用稳定的类型 / member，而不是复制其全部规则。
6. 删除已经变成错误、重复或无价值的注释，不为“注释覆盖率”保留腐化文本。
7. 每个 Feature rollout 只触及该 Feature 与必要 Shared contract，避免全仓混合修改。

## Review Checklist

- [ ] 注释描述的是当前真实 Contract / Semantics / Boundary，而不是名称翻译。
- [ ] 所有本批次的 interface、API contract、公开 Use Case、Result / Failure 与关键 Domain type 均已评审。
- [ ] public method 的 summary、params、returns 与必要 remarks 完整且有信息量。
- [ ] async returns 描述业务结果，没有写“返回 Task”。
- [ ] CancellationToken 在 contract declaration 使用统一 wording，implementation 没有重复扩写。
- [ ] nullable 只在 null 有特殊业务语义时解释，且没有自行发明含义。
- [ ] Result / Error 语义与 Controller / Application 实际映射一致。
- [ ] <code>&lt;exception&gt;</code> 只记录真实直接抛出的 contract exception。
- [ ] Current User 始终描述为 operator context，不是 authenticated / logged-in identity。
- [ ] KnowledgeRole 始终描述为知识身份 / domain attribution，不是 Permission。
- [ ] Evidence / HumanConfirmation 的 reference、Snapshot、legacy fallback 与历史不变性正确。
- [ ] 明确 HumanConfirmation create 不自动推进 KnowledgeStatus。
- [ ] <code>concurrencyToken</code> 始终描述为 opaque，未泄露内部编码。
- [ ] positional record 没有为了注释被重构成 class。
- [ ] EF Configuration、private helper、property 与 enum member 没有被机械注释。
- [ ] interface implementation 的 <code>&lt;inheritdoc /&gt;</code> 可由 IDE 正确解析。
- [ ] 注释语言为简洁中文，技术术语和 wire value 保持原名。
- [ ] 没有 TODO、未来承诺、线程安全 / 性能宣称或整段 Specification 复制。
- [ ] 本批次只有注释变化，没有业务逻辑、route、contract shape 或 persistence 行为变化。

## Rollout Order

后续由 Terra 按 Feature 分批执行，每批单独 review、build 并停止：

1. **Shared / Contracts**：<code>ApiErrorResponse</code>、<code>ApiIdParser</code>、共享 Domain primitive、API Request / Response / Result 基线。
2. **Users**：User、KnowledgeRole、UserKnowledgeRole、User API / Application 与 opaque concurrency contract。
3. **Current User**：<code>ICurrentUserContext</code>、resolution states、<code>X-Current-User-Id</code> operator-context 边界。
4. **Evidence / HumanConfirmation**：Evidence 语义、server-side Snapshot、Role resolution、legacy confirmation method fallback、KnowledgeStatus non-progression。
5. **Knowledge**：KnowledgeStatus policy，以及 Systems、BusinessFunctions、DatabaseKnowledge、Relationships、UnknownItems 中的显式状态与知识语义。
6. **Persistence / Infrastructure**：SQLite 配置、app-managed version、transaction、JSON constraint、RESTRICT FK；跳过生成的 Migration / Designer / ModelSnapshot。
7. **Remaining API Features**：Dashboard、Search、BusinessRules、Integrations、Bootstrap 等剩余公开 contract 与 Use Case。

每批遵守以下最小流程：

1. 只读取并修改当前 Feature 与必要 Shared contract。
2. 先删去低价值候选，再补充高价值 XML。
3. 检查 <code>inheritdoc</code> IDE 展示与 XML tag / cref 有效性。
4. 运行 <code>dotnet build SystemKnowledgeHub.sln --no-restore</code>。
5. 确认 diff 只有注释，不包含格式化扩散或业务行为变化。
6. 在覆盖率足够且人工批准前，不全局启用 <code>CS1591</code>，不批量生成注释。

本标准批准后才进入上述 rollout；XML-DOC-A01 本身不修改生产代码，也不自动开始任何 Feature 的 XML 注释补充。
