# SEC-A01 — Security & Access Control Design Review

审查日期：2026-08-22  
适用后续实现：SEC-01 及其后续安全切片  
范围：Authentication、Authorization、Access Control 架构设计；本文件不包含生产代码或 Migration。

## Result

`SEC-A01 READY FOR APPROVAL`

当前源码不存在阻止安全化的架构冲突。推荐在保持单 ASP.NET Core 项目、Vue 3 前端、Feature-first Modular Monolith 与 canonical `User` 不变的前提下，采用 **企业 OIDC / SSO 作为生产 Authentication Provider、ASP.NET Core 安全 HttpOnly Cookie 作为应用会话、canonical User 上的单一 AccessLevel 作为最小授权模型**。

生产环境中 authenticated identity 必须唯一映射到一个 Active canonical User；Current User 不再由浏览器选择，也不再信任 `X-Current-User-Id`。`KnowledgeRole` 与 `UserKnowledgeRole` 完全不参与访问控制。

## Existing Security State

### Backend

当前真实实现没有安全边界：

- `Program.cs` 只注册 Controllers、EF Core、CORS 与业务服务；没有 `AddAuthentication`、`AddAuthorization`、Authentication/Authorization middleware 或 fallback policy。
- 全部 Controller 均没有 `[Authorize]` / `[AllowAnonymous]`；`app.MapControllers()` 直接暴露所有读写 API。
- `/api/users` 与 `/api/knowledge-roles` 的读写、启停操作均可匿名直接调用。
- `CurrentUserContext` 读取 `X-Current-User-Id`，校验 safe integer、User 是否存在及 Active，但不校验请求者是否就是该 User。
- `GET /api/current-user` 返回 Header 所指向的 canonical Profile；Missing / Invalid / NotFound / Inactive 使用 U03 已定义的业务错误。
- U04 的 C25 在事务内重新读取 canonical User 与 KnowledgeRole，这是正确的 snapshot 一致性保护；但 Controller 传入的 User ID 仍源于浏览器可改 Header，因此目前不能证明确认人真实身份。
- `User` 没有 LoginName、PasswordHash、External Subject、AccessLevel 或 Permission 字段。现有 `KnowledgeRole` 是业务知识身份。
- 当前 `ApiErrorResponse(code, message, fieldErrors, details)` 可继续承载 Security 错误，不需要第二套 Envelope。

### Frontend

- `actorStore` 从 `localStorage` 的 `systemKnowledgeHub.currentUserId` 恢复所选 User，并允许从全部 Active User 中任意切换。
- shared `apiClient` 在每次请求上自动附加 `X-Current-User-Id`。
- TopBar 明确说明“当前操作者”不是登录账号或权限身份，但生产 UI 仍允许任意选择其他 Active User。
- `/admin/users` 是普通 Vue Route，Sidebar 永远显示“用户管理”；Router 没有 authentication guard 或 access metadata。
- “新增”、编辑、状态推进、Evidence、Finding 等按钮没有 AccessLevel 判断。
- API client 已有统一 4xx 错误处理边界，可在后续切片集中处理 401 / 403 / session expiration。

### Confirmed boundary

当前 U01–U04 行为在其批准范围内是正确的，但它是 **operator context**，不是可信身份。SEC-A01 不否定历史设计；它定义从非安全 operator context 迁移到安全 authenticated identity 的路径。

## Recommended Authentication Architecture

| Option | Complexity | Security | Current project fit | Deployment / maintenance | Canonical User mapping | Future SSO |
| --- | --- | --- | --- | --- | --- | --- |
| A. ASP.NET Core local authentication（自有登录页 + Cookie） | 中等；需自行实现账号生命周期、登录限速、重置和运维 | 可安全，但错误空间较大 | 技术上适合单体；当前没有本地账号需求 | 无外部 IdP 依赖，但长期密码运维成本高 | 需要额外本地 LoginIdentity → User mapping | 以后仍需新增 OIDC，并迁移/并存本地凭据 |
| B. ASP.NET Core Identity | 高；会引入完整 Identity 数据模型与大量默认能力 | 成熟，适合正式本地账号 | 对当前最小 User Foundation 过重，且不能把 canonical User 改为 IdentityUser | 本地密码、reset、lockout 完整，但表与 UI 面积显著扩大 | 仍需 IdentityUser → canonical User 映射 | 支持外部 provider，但保留两套用户生命周期 |
| C. OIDC / Enterprise SSO + application Cookie | 中等；依赖 IdP 注册与部署配置 | 最强；密码、MFA、企业账号生命周期交给 IdP | 最符合企业内部 Knowledge Hub；不改变业务 User | 应用只维护映射、会话与授权；IdP 负责凭据 | `(provider, subject)` 明确映射 canonical User | 原生路径；可接 Entra ID、Okta 或其它 OIDC IdP |
| D. Windows Integrated / trusted reverse-proxy identity header | 中等，且高度依赖网络与代理正确配置 | 在严格内网边界可安全，错误配置时风险很高 | 对浏览器、反向代理、平台和本地开发耦合较强 | 跨平台部署与故障定位成本高 | 仍需外部标识 → User mapping | 迁移 OIDC 时需替换整个入口信任模型 |

### Recommended approach

选择 **Option C：OIDC / Enterprise SSO + ASP.NET Core Cookie**。

第一阶段生产实现应直接接一个 OIDC Provider，不建立本地密码登录作为临时方案。项目只增加最小的 Authentication Identity mapping，不建设通用 Identity Platform。若部署环境尚未提供 IdP 注册信息，SEC-01 应明确阻塞该环境的生产启用，而不是静默退回匿名 Viewer、浏览器 User Selector 或默认密码。

推荐运行链路：

```text
Browser
→ Login Gate
→ ASP.NET Core OIDC Challenge
→ Enterprise Identity Provider
→ OIDC callback (issuer/provider + subject)
→ LoginIdentity mapping
→ canonical User + Active validation
→ secure HttpOnly application cookie
→ request-time User / AccessLevel validation
→ ASP.NET Core authorization policy
→ Controller / Application operation
```

此方案不需要自建 Authentication abstraction hierarchy。`LoginIdentity` 数据边界和标准 ASP.NET Core authentication scheme 已足够支持更换或增加 OIDC Provider。

## Identity Model

### Canonical User remains the business person model

现有 `User` 继续保存 DisplayName、EmployeeNo、Email、DepartmentOrTeam、JobTitle、Active 与知识身份映射。它不是 OIDC token、Cookie ticket 或 ASP.NET Core Identity User，也不保存密码。

### New minimal LoginIdentity

后续 SEC-01 建议增加 `login_identities`：

| Field | Required | Purpose |
| --- | --- | --- |
| `Id` | Yes | 内部 safe integer PK |
| `UserId` | Yes | FK → `users(id)`，`RESTRICT` |
| `Provider` | Yes | 稳定 provider/issuer key，例如配置中的 `EntraId` 或 issuer identifier |
| `Subject` | Yes | Provider 签发的不可变 `sub`；不得使用 DisplayName 或 Email 替代 |
| `IsActive` | Yes | 允许单独停用一条登录映射；默认 true |
| `CreatedAt` / `UpdatedAt` | Yes | UTC lifecycle metadata |
| `Version` | Yes | 沿用 app-managed version，管理变更继续使用 opaque token |

约束：

- `(Provider, Subject)` 使用 ordinal/canonical comparison 唯一；`sub` 的大小写语义不得自行改写。
- `UserId` 建普通索引；允许未来一个 User 对应多个 provider identity，但第一阶段 UI 不需要鼓励多身份。
- 不按 Email、EmployeeNo 或 DisplayName 自动匹配已登录身份，避免可变或重复 claim 导致账号接管。
- 新身份必须通过 Administrator 的显式映射操作或受控 bootstrap CLI 建立；第一阶段不做任意用户的 Just-in-Time auto-provisioning。
- OIDC access token、refresh token 与 ID token 不保存到该表，也不发送给 Vue。

### Principal projection

成功映射后，应用 principal 最少包含内部 `login_identity_id`、`user_id` 与 `access_level` claim。它们是后端运行时投影，不是浏览器可提交的授权事实。

每个请求通过 Cookie validation 重新读取 LoginIdentity、User.Active 与 AccessLevel。当前 SQLite 内部工具规模下，一次小型 request-time query 比依赖可能陈旧的长寿命 Cookie claim 更可靠，并能立即阻止停用用户和撤销后的旧会话。

## Current User Migration

### Required production decision

登录成功后必须自动解析 canonical User。生产中 authenticated identity 与 Current User 是同一个主体，不允许用户在二者之间自由切换。

| Current artifact | Decision |
| --- | --- |
| canonical `User` / `KnowledgeRole` / `UserKnowledgeRole` | 保留；KnowledgeRole 继续只表达知识身份 |
| `ICurrentUserContext` | 保留这个已被 C25 使用的窄接口；替换实现来源 |
| `CurrentUserContext` | 从解析 Header 改为读取已验证 principal 的内部 UserId，并按需要重读 canonical User |
| `GET /api/current-user` | 保留 canonical route；不再要求 Header，返回 authenticated User Profile，并增加前端所需 `accessLevel` |
| `actorStore.currentUser` / compatible `actor` | 保留为前端唯一 Profile / Actor 来源，改为从 authenticated `/api/current-user` 初始化 |
| `selectedUserId`、`activeUsers`、`selectCurrentUser` | 从生产 store 删除 |
| `systemKnowledgeHub.currentUserId` localStorage | 删除；不迁移旧值，不作为登录后 fallback |
| `X-Current-User-Id` provider in shared API client | 删除；生产请求不再发送该 Header |
| TopBar Current User Selector | 生产删除；改为只读 Profile 与标准 logout 入口 |

生产后端应忽略或明确拒绝 `X-Current-User-Id`，绝不能在 authenticated principal 与该 Header 不同时“选择一个优先”。系统只保留一个生产身份来源。

### Development and tests

- Integration tests 使用 `WebApplicationFactory` 替换 authentication scheme 的 test handler，并签发受控 claims；不继续依靠自由 Header。
- 本地 Development 若确需切换 User，可提供显式配置 `EnableDevelopmentUserSwitching=true` 下的 development-only sign-in helper。它必须同时满足 `IHostEnvironment.IsDevelopment()`、loopback origin 和显式开关，并由服务端签发同一种认证 Cookie。
- Development helper 不得注册于 Production，Production 启动时若该开关为 true 必须 fail fast。
- 即使存在 development selector，它也不再通过 `X-Current-User-Id` 改写单个请求；切换代表重新建立开发会话。
- 不设计生产 Administrator impersonation；如未来确有支持需求，必须独立设计审计、原因和退出机制。

## Access Role Model

### Decision

采用一个封闭 enum / wire vocabulary，并在 `users` 增加单一 `access_level` column：

```text
Viewer < Editor < Administrator
```

理由：当前每个 User 只需要一个全局访问等级，没有 scope、deny、组合角色或动态 permission。单列比 AccessRole / Permission / UserRole 多表更小；claims 只是该 canonical 列的 request-time 投影，不是独立事实来源。

### Semantics

- `Viewer`：authenticated、mapped、Active User 的默认值；只读当前及未来已批准的知识内容。
- `Editor`：包含 Viewer；允许当前明确的知识创建、修改、Evidence、HumanConfirmation、Finding、KnowledgeStatus 与 UnknownItem workflow 操作。
- `Administrator`：包含 Editor；额外允许 User、KnowledgeRole、LoginIdentity 与 AccessLevel 管理。

规则：

- Migration 中所有现有 User 默认为 `Viewer`；不得根据 KnowledgeRole、姓名、EmployeeNo 或当前浏览器选择猜测 Administrator。
- `POST /api/users` 服务器端固定创建 Viewer；普通 User create body 不允许自行声明 Administrator。
- AccessLevel 通过独立 Administrator-only operation（建议 `PUT /api/users/{id}/access-level` + concurrencyToken）修改，不混入基础 Profile 保存。
- 停用或降级最后一个 Active Administrator 必须返回业务错误；bootstrap/recovery 不能依赖已经不存在的管理员。
- `KnowledgeRole`、`UserKnowledgeRole` 与 AccessLevel 无 FK、映射或隐式推导关系。

### Backend policy mapping

- Fallback policy：要求 authenticated identity 已映射到 Active User；这等价于最低 Viewer。
- `Editor` policy：AccessLevel >= Editor。
- `Administrator` policy：AccessLevel == Administrator。
- Controller/API 是最终 enforcement boundary。Frontend visibility 不参与授权决定。
- 不建立动态 Permission 表、policy database、ACL、ABAC 或 department scope。

## Endpoint Authorization Matrix

`✓` 表示允许；`—` 表示后端返回 403。所有列都隐含必须已认证、身份已映射且 User Active；未认证统一为 401。

| Current endpoint / operation | Viewer | Editor | Administrator |
| --- | :---: | :---: | :---: |
| `GET /api/dashboard` | ✓ | ✓ | ✓ |
| `GET /api/search`、`GET /api/knowledge-targets` | ✓ | ✓ | ✓ |
| `GET /api/systems`、`GET /api/systems/{id}` | ✓ | ✓ | ✓ |
| `GET /api/business-functions`、`GET /api/business-functions/{id}` | ✓ | ✓ | ✓ |
| `GET /api/database-objects`、`GET /api/database-objects/{id}`、`GET /api/database-columns/{id}` | ✓ | ✓ | ✓ |
| `GET /api/business-rules/{id}`、`GET /api/integrations/{id}` | ✓ | ✓ | ✓ |
| `GET /api/relationships/{id}`、`GET /api/evidence/{id}` | ✓ | ✓ | ✓ |
| `GET /api/unknown-items`、`GET /api/unknown-items/{id}` | ✓ | ✓ | ✓ |
| `GET /api/current-user` | ✓ | ✓ | ✓ |
| `GET /api/users`、`GET /api/users/{id}` | — | — | ✓ |
| `GET /api/knowledge-roles` | — | — | ✓ |
| `POST /api/systems`；`PUT .../overview|technology|lifecycle` | — | ✓ | ✓ |
| `POST /api/business-functions`；`PUT .../overview|process-steps` | — | ✓ | ✓ |
| `POST /api/database-sources`、`POST /api/database-objects`、`POST .../columns` | — | ✓ | ✓ |
| `PUT /api/database-objects/{id}/knowledge`；Column knowledge / known-value add/remove | — | ✓ | ✓ |
| BusinessRule create/update；Integration create/overview/contract-fields update | — | ✓ | ✓ |
| Relationship create、description update、relationship knowledge-status operation | — | ✓ | ✓ |
| `POST /api/evidence`、`PUT /api/evidence/{id}` | — | ✓ | ✓ |
| `POST /api/evidence/human-confirmations` | — | ✓ | ✓ |
| `PUT /api/knowledge-status` | — | ✓ | ✓ |
| UnknownItem create / related-targets / start-investigation / findings / investigation evidence | — | ✓ | ✓ |
| UnknownItem resolution、五种 concrete KnowledgeUpdate apply、confirm、close、reopen | — | ✓ | ✓ |
| User create/update/active-state | — | — | ✓ |
| KnowledgeRole create/update/active-state | — | — | ✓ |
| Proposed LoginIdentity mapping / active-state and User AccessLevel operation | — | — | ✓ |

Additional rules:

- `GET /api/bootstrap/status` 是 development diagnostic。Production 不应暴露；如果保留生产健康检查，应另行提供不泄露业务信息的运维端点，而不是把 Bootstrap Controller 作为匿名业务入口。
- SOP / Troubleshooting 当前源码没有 Route；未来一旦批准，读取默认映射 Viewer，写入必须在对应 Slice 明确评审，不能因 Administrator 存在就自动归类。
- 普通 Request Body `actor` 与 PersonSnapshot 不参与任何 policy 计算。它们可为冻结业务事实兼容保留，但不能覆盖 principal、UserId 或 AccessLevel。

## Frontend Access Model

### Login Gate

- 未认证只能进入最小 Login Gate 与 OIDC protocol callback；不得渲染业务 Shell、Dashboard 或 Anonymous Viewer。
- Login Gate 发起后端 OIDC Challenge，不在 Vue 中收集企业密码。
- 登录成功回到原目标 Route；目标必须经过 Router Guard 重新校验。

### Router and navigation

- Vue Router 增加全局 `beforeEach`；初始化时调用 authenticated `/api/current-user`。
- 业务 Route 默认要求 Viewer；`/admin/users` meta 要求 Administrator。
- Sidebar 只向 Administrator显示“管理 → 用户管理”。Viewer / Editor 手工输入 `/admin/users` 时 Router Guard 显示无权访问或跳转安全页。
- Router Guard 只改善 UX；即使被绕过，Users / KnowledgeRoles API 仍由后端 Administrator policy 拒绝。

### Actions

- Viewer 隐藏或禁用“新增”、编辑、添加 Evidence、HumanConfirmation、Finding、状态推进和 workflow 写操作。
- Editor 显示知识维护操作，但不显示 User Management / KnowledgeRole Management。
- Administrator 显示全部现有维护操作。
- 页面级权限使用同一个 `accessLevel` projection；不在每个组件维护自定义 permission string 列表。

### 401 / 403 / expiration

- shared API client 遇到 401：清除内存 Profile，保存当前安全 return URL，进入 Login Gate；不得自动重放写请求。
- 403：保留登录状态，显示“无权执行此操作”；Role 刚被降级时刷新 Current User Profile 与按钮状态。
- session expiration：按 401 处理；用户重新认证后由用户明确重新提交未完成写操作。

`actorStore` 继续是唯一 Current User Profile / Actor 来源；不要同时创建另一个长期持有同一 User Profile 的 auth store。

## Session / Token Strategy

### Decision

采用 **服务端 ASP.NET Core Cookie Authentication**：

- Cookie 必须 `HttpOnly`、`Secure`，应用 Cookie 使用 `SameSite=Lax`；OIDC correlation/nonce Cookie 按标准 handler 要求配置。
- Cookie 只保存 ASP.NET Core Data Protection 保护的认证 ticket；Vue 不读取 Cookie，也不把 access token、ID token 或 refresh token 放入 `localStorage` / `sessionStorage`。
- Production 必须持久化并保护 Data Protection keys；多实例部署共享同一 key ring 与 application name。
- 应用与 API 优先经同一 origin / reverse proxy 发布。Development 的 Vite CORS 仅允许显式本地 origin，不使用 wildcard credentials。
- 不采用 JWT bearer 作为浏览器到本应用 API 的默认方案。当前没有跨域第三方 API consumer；JWT 会扩大 token storage、refresh、revocation 与 XSS 风险面。
- 不建立 server-side session table。受保护 Cookie + 每请求 canonical identity/access validation 已满足停用和降级即时生效。

### CSRF

Cookie 会自动随请求发送，因此所有 POST / PUT / DELETE（及未来 PATCH）必须验证 ASP.NET Core antiforgery token。Vue 从受控 same-origin bootstrap/session response 获取 token，并通过 shared API client 发送专用 header。`SameSite=Lax` 是附加防线，不替代 antiforgery validation。

OIDC callback、login challenge 与 logout 使用框架推荐的 state / correlation / antiforgery 行为，不自行实现协议参数。

## Local Account and Password Decision

第一阶段生产不支持本地密码，因此不创建 `UserCredential`、PasswordHash、Password Reset 或默认账号密码。

原因：企业 OIDC 能把密码存储、MFA、锁定、reset 与离职账号生命周期交给现有 IdP，避免当前小型业务项目自行承担高风险凭据管理。

如果未来出现离线部署且没有任何 OIDC Provider 的明确需求，必须单独评审 Local Authentication Amendment。届时必须使用 ASP.NET Core Identity 或框架提供的 `IPasswordHasher`、经过批准的密码策略、登录限速/锁定和一次性 reset token；禁止 plaintext、可逆加密或自制 hash。该未来可能性不是 SEC-01 的 fallback。

## Bootstrap Administrator

推荐使用 **同一 ASP.NET Core 项目的显式 one-shot CLI mode**，不使用 Migration seed、默认密码或匿名注册：

```text
dotnet run -- bootstrap-admin
  --provider <configured-provider-key>
  --subject <exact-oidc-subject>
  --display-name <name>
  [--employee-no <value>]
  [--email <value>]
```

后续实现要求：

1. CLI 不启动 Web Server。
2. 只在数据库不存在 Active Administrator 时运行。
3. 在一个事务中创建或明确绑定 canonical User、创建 LoginIdentity，并设为 Administrator。
4. Provider 必须来自已配置 allowlist；Subject 必须精确保存，不按 Email 猜测。
5. 如果已存在 Administrator、identity 已绑定其他 User 或输入发生歧义，拒绝并返回非零退出码。
6. 部署完成后不需要保留任何 bootstrap password 或公开 bootstrap endpoint。

后续 Administrator 为其它 canonical User 显式维护 LoginIdentity 与 AccessLevel。创建普通 User 仍默认为 Viewer。

## Disabled User Handling

- OIDC 登录成功后仍必须检查 LoginIdentity.Active 与 canonical User.Active；任一为 false 都不签发应用 Cookie。
- Cookie validation 在每个请求重读映射与 User.Active；因此 User 被停用后，已有 Cookie 的下一次请求立即失败，而不是等待 Cookie 到期。
- Inactive User 的现有 ticket 被 reject 并删除；API 返回 403 `account_inactive`，前端清除 Profile 并显示联系管理员提示。
- User 重新启用后仍需重新认证；不得自动恢复旧浏览器会话。
- 历史 Evidence / HumanConfirmation snapshot 保持不变，User 停用不回写历史事实。
- 停用最后一个 Active Administrator 必须被后端业务规则拒绝。

## HumanConfirmation Security

U04 的 domain、transaction 与 snapshot mapping 保持不变；只替换 Current User 的可信来源：

```text
OIDC identity
→ protected authentication cookie
→ LoginIdentity mapping
→ Active canonical User
→ ICurrentUserContext
→ C25 transaction re-read User / Role mappings
→ immutable HumanConfirmation snapshot
```

- `EvidenceController.AddHumanConfirmation` 继续只依赖 `ICurrentUserContext`，不直接解析 claims 或 Header。
- C25 transaction 内的 User Active、KnowledgeRole existence/active/assignment re-read 必须保留。
- `X-Current-User-Id` 不再发送或读取；修改 localStorage 或自行添加该 Header 不能改变 C25 的 UserId。
- HumanConfirmation 要求 Editor 或 Administrator policy；Viewer 直接调用返回 403。
- KnowledgeRole 仍只决定“本次以什么知识身份确认”，不授予调用 C25 的权限。
- 历史 Snapshot 与 U04 legacy compatibility 不变。

## Security Error Behavior

继续使用 `ApiErrorResponse`；Cookie handler 对 API 请求不得 302 重定向到 HTML 登录页。

| Condition | HTTP | `code` | Required details / behavior |
| --- | ---: | --- | --- |
| Unauthenticated / no valid Cookie | 401 | `unauthenticated` | `details.authStatus = "missing"`；前端进入 Login Gate |
| Session expired / rejected ticket | 401 | `session_expired` | `details.authStatus = "expired"`；清 Cookie，不自动重放 write |
| Authenticated identity has no mapping | 403 | `identity_unmapped` | `details.authStatus = "unmapped"`；联系 Administrator |
| LoginIdentity inactive | 403 | `identity_inactive` | `details.authStatus = "identity_inactive"` |
| canonical User inactive | 403 | `account_inactive` | `details.authStatus = "inactive"`；立即 reject ticket |
| AccessLevel too low | 403 | `forbidden` | `details.requiredAccessLevel` 与当前级别；不泄露敏感资源内容 |

登录页导航可接受 OIDC challenge 的 302；`/api/**` 的 401 / 403 始终返回 JSON Error Contract。

## Threat Review

| Threat | Current exposure | Required control | Residual risk after design |
| --- | --- | --- | --- |
| 修改 `X-Current-User-Id` 冒充他人 | 当前可选择任意 Active User | 生产移除 Header；Current User 只来自 protected principal mapping | IdP / mapping 管理错误，需管理员显式核对 Subject |
| Viewer 直接调用写 API | 当前全部匿名可写 | Backend Editor policy 覆盖所有现有写 Route | Controller 新增 action 时若漏 policy；fallback + authorization test inventory 控制 |
| Editor 直接调用 User API | 当前可直接调用 | Users / KnowledgeRoles / identities / access-level 全部 Administrator policy | 管理 API 新增时需同一 policy review |
| 未登录直接访问 API | 当前允许 | fallback default-deny policy；仅 login/callback 明确 AllowAnonymous | 错误的 AllowAnonymous；需聚焦测试扫描 |
| Inactive User 使用旧 session | Header 查询时部分拦截，普通 API 不拦截 | Cookie 每请求重验 User.Active 并 reject ticket | DB 不可用时必须 fail closed |
| 篡改 localStorage 身份 | 当前可改变 Current User | 删除生产 currentUserId storage；不把 authorization data 存浏览器 | XSS 仍可在当前会话发起允许的操作 |
| CSRF | 当前无 auth Cookie，因此尚无该风险模型 | unsafe methods 强制 antiforgery header + same-origin + SameSite | 同源 XSS 可读取 antiforgery token，因此仍需 XSS 控制 |
| XSS | 可篡改 UI / 发请求 | Vue escaping、避免 `v-html`、CSP、依赖更新、HttpOnly Cookie、最小 session lifetime | HttpOnly 防 token 盗取但不能阻止同页代发请求；后端 authorization 仍必要 |

这是针对当前单体架构的最小 threat review，不扩展为企业 Security Audit、SOC、SIEM 或通用 Audit Framework。

## Future SSO Compatibility

推荐方案本身就是 OIDC-compatible：

- Entra ID、Okta 或其它标准 OIDC Provider 只改变 provider registration / issuer configuration。
- `(Provider, Subject) → UserId` mapping 隔离外部身份与业务 Profile；canonical User ID、KnowledgeRole、Evidence reference 和历史 Snapshot 不变。
- 更换 IdP 时为同一 User 增加新 LoginIdentity，完成验证后停用旧 mapping；不迁移或重写业务知识。
- 不使用 Email 作为永久 subject，因此企业邮箱变更不会改变 canonical User。
- 不建立厂商专用 Domain Entity 或把 Entra group 直接当作 KnowledgeRole。

第一版 AccessLevel 由本地 `users.access_level` 管理，不自动信任外部 group claim。未来若批准企业 group mapping，应作为独立 amendment，不能在 SEC-01 隐式增加。

## Migration / Schema Impact

本节只描述后续影响；SEC-A01 不生成 Migration。

1. 新增 `login_identities` 表及 `(provider, subject)` unique、`user_id` index、User `RESTRICT` FK。
2. `users` additive 增加非空 `access_level TEXT DEFAULT 'Viewer'`，CHECK 仅允许 `Viewer|Editor|Administrator`。
3. 现有 User 全部迁移为 Viewer；first Administrator 由 one-shot CLI 显式产生，不由 Migration 猜测。
4. User / KnowledgeRole / UserKnowledgeRole 现有数据、Active 语义和 concurrency 不变；AccessLevel 独立操作继续使用 User token 或明确的新 token 规则，不能引入第二套并发机制。
5. Evidence 与 U04 snapshot schema 不变化；不回填 LoginIdentity、User reference 或历史 Actor snapshot。
6. 后续 API contract 最小变化：`GET /api/current-user` 增加 `accessLevel`；增加 Administrator-only access-level 与 LoginIdentity mapping operations；移除生产 `X-Current-User-Id` contract。
7. Production 配置新增 OIDC authority/client、callback、Data Protection key storage 与 cookie settings。Secret 使用 deployment secret store，不提交到 appsettings 源码。

SQLite 如因 CHECK/FK 需要 table rebuild，后续 Migration verification 必须证明 users、Evidence references、indexes、version 与历史数据保留。

## Specification / Design Compatibility

- Frozen MVP 明确把 Authentication / Authorization 延后，并要求未来登录不得覆盖历史 PersonSnapshot；本设计遵守该边界。
- User / Person Foundation 已预留“authenticated principal 映射既有 User”的演进方向；本设计保持 canonical User，不引入 Person。
- U04 的 C25 server-side hydration、KnowledgeRole resolution 与 immutable snapshot 全部保留，仅把 UserId 来源从非可信 Header 收紧为 authenticated principal。
- 当前源码没有 ADR-named 文件；相关已批准决策来自 Frozen Specifications、User / Person Foundation、HC-A01 与 U01–U04 reports。
- 未发现阻塞性设计冲突。

## Explicitly Not Implemented

SEC-A01 只生成本设计文件；本阶段没有：

- 修改 C#、Vue、Router、Sidebar、apiClient、actorStore、Controller、Program.cs 或配置；
- 安装 OIDC、Identity、JWT、authentication 或 authorization package；
- 生成 EF Migration、修改 DbContext 或 SQLite；
- 实现 Login、Logout、Cookie、Session、CSRF、claims、policy、Router Guard 或 UI gating；
- 实现本地密码、Password Reset、MFA、JWT bearer、OAuth resource server；
- 实现 Department scope、Organization scope、object ACL、ABAC、dynamic permission、approval workflow；
- 将 KnowledgeRole / UserKnowledgeRole 用作权限；
- 建立 Person、Organization、Audit Framework、enterprise IAM platform 或 multi-tenant authorization；
- 开始 SEC-01 或其它生产实现。

## Proposed Implementation Slices

### SEC-01 — OIDC Authentication Foundation + Canonical User Binding

- 增加 LoginIdentity model、`users.access_level` additive column / Migration、OIDC challenge/callback 与 secure Cookie。
- 实现 explicit bootstrap-admin CLI、request-time mapping / Active validation 与 JSON 401/403。
- 将 `ICurrentUserContext` 改为 principal-backed；`GET /api/current-user` 返回 authenticated Profile + AccessLevel。
- C25 focused regression 证明 Header 不能冒充其它 User。
- Gate：未映射、Identity inactive、User inactive、旧 Cookie 与 Data Protection restart 行为。

### SEC-02 — Backend Access Control

- 实现 Viewer/Editor/Administrator policies 与 fallback default-deny。
- 按本矩阵标注全部当前 Controller action；增加 AccessLevel/identity explicit admin operation。
- 聚焦验证匿名 401、Viewer write 403、Editor admin 403、Administrator success 与 last-admin invariant。
- 不引入 dynamic permission engine。

### SEC-03 — Frontend Login and Access UX

- 增加 Login Gate、Router Guard、401/403/session-expired handling。
- `actorStore` 改为 authenticated Profile source；删除 production selectedUserId/localStorage/header propagation。
- TopBar 改为只读 Profile / logout；Sidebar、User Management Route 与所有写 action 按 AccessLevel 收敛。
- Development switcher 如确需保留，只按本文 development-only server-issued session 规则实现。

### SEC-04 — Security Rollout Verification

- OIDC staging closed-loop、CSRF、Cookie / Data Protection deployment、reverse-proxy headers 与 same-origin verification。
- 运行完整 endpoint authorization matrix 的风险导向测试，并检查新增 Controller 不存在匿名绕过。
- 复核 HumanConfirmation authenticated User snapshot、Inactive User session invalidation 与 User Management protection。
- 清理所有验证服务器、浏览器与端口，生成独立 Security verification report。

这些 Slice 必须分别审批、实现、验证并停止。SEC-A01 结束后等待人工 Approval；不得自动开始 SEC-01。

SEC-01～SEC-03 是一个安全 rollout 的连续实现单元；任何中间状态都不得单独启用到 Production。Production 只在 SEC-04 证明 Authentication、backend matrix、frontend gate、Current User migration 与 CSRF 全部闭环后切换到新安全模式。
