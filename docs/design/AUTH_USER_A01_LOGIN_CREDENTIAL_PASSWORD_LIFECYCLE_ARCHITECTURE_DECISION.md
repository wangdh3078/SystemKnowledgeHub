# AUTH-USER-A01 — User Login Credential & Password Lifecycle Architecture Decision

> Product: 系统知识中心 / System Knowledge Hub
>
> Task: AUTH-USER-A01
>
> Decision date: 2026-08-29
>
> Status: **Frozen Design Decision — Approved**

## 1. Result

```text
AUTH-USER-A01 APPROVED
AUTH-USER-B01 READY: YES
```

本决策冻结“创建 User → 配置登录方式 → 本地登录 → 首次强制改密 → 用户改密 → 管理员重置 → 会话失效”的完整本地认证生命周期。

当前没有未解决的 Blocker 或 High 级设计问题。本文列出的缺口都是后续实现缺口，不阻塞本次架构批准。

本文只冻结设计，不实现 Password UI、API、Migration、认证逻辑或 Session middleware。

## 2. Authority and Compatibility

### 2.1 Sources reviewed

本决策基于以下真实来源：

- 根目录 `AGENTS.md` 与 `docs/DOCUMENT_INDEX.md`；
- Frozen MVP Domain / Database / Application / API / UI / Solution Structure；
- `SEC_A01_SECURITY_ACCESS_CONTROL_DESIGN_REVIEW.md`；
- `AUTH_A01_LOCAL_LOGIN_OIDC_COEXISTENCE_DESIGN_REVIEW.md`；
- 已批准的 `AUTH_A01_LOCAL_PASSWORD_POLICY_AMENDMENT.md`；
- AUTH-B01/B02、SEC-01～SEC-04、U01～U04 的实现与验证记录；
- 当前 `main` 上的 User、LocalLoginCredential、LoginIdentity、LocalLoginService、LocalPasswordService、CurrentUserContext、Cookie principal、User Management、Login Gate 与 TopBar 实现。

### 2.2 Compatibility decision

- Frozen MVP 最初将 Authentication / Authorization / User Management 排除在当时 MVP 范围外；后续 User、SEC 与 AUTH 明确设计是受控增量，不回写原 Frozen MVP 文档。
- SEC-A01 的 OIDC-first 边界继续有效；后来批准的 Local Authentication amendment 已受控增加 Local 登录，不恢复匿名访问、浏览器 User Selector 或 `X-Current-User-Id` 信任。
- `AUTH_A01_LOCAL_PASSWORD_POLICY_AMENDMENT.md` 的 **8～128 字符**规则继续是当前唯一密码长度规则，并覆盖早期 AUTH 提案中的 15 字符最小长度。
- `AUTH_A01_LOCAL_LOGIN_OIDC_COEXISTENCE_DESIGN_REVIEW.md` 当前文件状态仍是 Proposed。本文保留其已实现的 credential、hash、lockout、Cookie descriptor 与 method-scoped `SessionVersion` 设计，但在以下两点作最终收口：
  - `Change My Password` 只允许当前由 Local authentication 建立的会话，不允许 OIDC-origin session 直接修改本地密码；
  - 用户改密采用“方案 A”：所有旧 Local Session（包括当前）失效，用户重新登录；不重签当前 Cookie。
- 没有发现需要停止 A01 的 Frozen Authentication / Security Contract 冲突。

## 3. Context and Current Baseline

当前实现已经具备：

- canonical `User` 与独立 `LoginIdentity`、`LocalLoginCredential`；
- `User → 0..1 LocalLoginCredential` 的数据库唯一约束；
- `User → 0..N LoginIdentity`；
- Local 与 OIDC 共用 `SystemKnowledgeHub.Auth` application Cookie；
- principal 中的 `auth_method`、`auth_identity_id`、`auth_version`、`user_id`、`access_level`；
- `CurrentUserContext` 每次请求重读 method identity、User Active 与最新 AccessLevel；
- Local credential 的 `SessionVersion` 实际校验；
- PasswordHasher Identity V3、220,000 iterations、dummy hash、登录 rate limit 与 lockout；
- User / LoginIdentity Active 状态和现有 last-usable-Administrator 保护；
- Local-only、OIDC-only、Local + OIDC 的启动配置与 fail-closed “两种方式都关闭”检查；
- Local/OIDC Login Gate、Current User 与退出登录。

当前 `POST /api/users` 只创建 User Profile 与 KnowledgeRole assignment。它不会创建 LocalLoginCredential 或 LoginIdentity。因此一个没有后续映射的新 User 可以合法存在，但不能登录。

## 4. Current Gaps

以下均为 implementation gap，不是 A01 design blocker：

| Gap | Current state | Owning follow-up |
| --- | --- | --- |
| AUTH-USER-G01 | Create User 没有显式 Local / OIDC / 暂不配置登录选择，也没有跨 User + login method 的事务 | AUTH-USER-B02 |
| AUTH-USER-G02 | `LocalLoginCredential` 没有 `MustChangePassword`，后端没有强制改密门禁 | AUTH-USER-B01 |
| AUTH-USER-G03 | 没有用户自助改密 API/UI，也没有改密后的正式 Session 行为 | AUTH-USER-B01 |
| AUTH-USER-G04 | 没有管理员读取/新增/启停 Local credential 或重置密码的 API/UI | AUTH-USER-B03/B04 |
| AUTH-USER-G05 | 现有 usable Administrator 查询虽已包含 Local + OIDC，却没有考虑全局 Enabled、当前配置 Provider；bootstrap 判定也不完全一致 | AUTH-USER-B01/B03 |
| AUTH-USER-G06 | Local/OIDC 全局关闭只阻止新 challenge/login；当前 method-specific Cookie validation 没有同时检查 method 是否仍全局 Enabled | AUTH-USER-B01 |
| AUTH-USER-G07 | 当前没有本文要求的 password/credential structured security audit events | AUTH-USER-B01～B04 |
| AUTH-USER-G08 | OIDC mapping 管理 API 可保存任意非空 Provider；登录只接受已配置 Provider，因而错误 Provider 可能被误算为“可用” | AUTH-USER-B02/B03 |

## 5. User vs Credential vs LoginIdentity Boundary

```text
User
= 系统中的人员 / 业务主体 / 权限与知识身份主体

LocalLoginCredential
= 本地用户名密码登录凭据及其安全状态

LoginIdentity
= 外部 OIDC / SSO Provider + Subject 映射
```

冻结 cardinality：

```text
User
  → 0..1 LocalLoginCredential
  → 0..N LoginIdentity
```

规则：

- User 可以没有任何登录方式；
- User 可以只有 Local、只有 OIDC，或同时拥有 Local + OIDC；
- `User.AccessLevel`、KnowledgeRole、Profile、业务引用和历史归属不进入 credential/identity；
- `PasswordHash` 不进入 User；
- Email、EmployeeNo、DisplayName 不自动成为 Username，也不用于 OIDC 自动绑定；
- Local 与 OIDC 同时存在时，它们只是同一 User 的两种独立 authentication method，不拥有独立 AccessLevel。

## 6. Login Method Model

User Management 使用三个明确状态：

| Login setup | Rows created | Can sign in when method globally enabled |
| --- | --- | --- |
| 本地账号 | User + one LocalLoginCredential | Yes，要求 User/Credential Active 且未处于有效 lockout |
| 企业统一登录（OIDC / SSO） | User + one LoginIdentity | Yes，要求 User/Identity Active 且 Provider 是当前允许值 |
| 暂不配置登录 | User only | No |

创建时只选择一种初始方式。创建完成后，Administrator 可以为同一 User 增加另一种方式，因此最终允许 Local + OIDC coexistence。

所有登录方式配置和变更仅允许 Administrator。普通用户只能修改自己当前 Local session 对应 credential 的密码。

## 7. User Creation

### 7.1 UI direction

新增用户 Drawer 固定为：

```text
01 基础资料
02 知识身份
03 登录方式
```

“03 登录方式”必须显式选择：

```text
○ 本地账号
○ 企业统一登录（OIDC / SSO）
○ 暂不配置登录
```

不允许通过留空字段隐式猜测登录方式。

### 7.2 Application/API direction

保留 canonical `POST /api/users`，以一个明确 discriminated `loginSetup` 扩充 Create User request；不增加第二个平行 User-create endpoint，也不建立 generic credential CRUD。

概念 contract：

```text
loginSetup.type = local | oidc | none
```

- `local`：`username`、`initialPassword`；
- `oidc`：`provider`、`subject`；
- `none`：不带 credential/identity secret fields。

“确认密码”只用于前端相等性检查。API 只接收一份 `initialPassword`，服务端仍独立执行全部 password policy 校验。

### 7.3 Transaction

选择 Local 或 OIDC 时，User、初始 KnowledgeRole assignments 与选中的 login method 必须在同一个数据库 transaction 中创建：

```text
validate full request
→ begin transaction
→ create User
→ create role assignments
→ create LocalLoginCredential OR LoginIdentity
→ commit
```

任一 validation、duplicate、FK 或写入失败均整体回滚。不得产生“User 已成功但选择的登录方式创建失败”的半完成状态。

`none` 仍在一个 User-create transaction 中创建 User 与 role assignments，只是不创建 authentication method row。

新 User 继续默认：

- `IsActive = true`；
- `AccessLevel = Viewer`；
- 不能从 Create User request 自行声明 Administrator。

## 8. Local Credential Creation

### 8.1 Required values

- Username required；
- Initial Password required；
- UI 确认密码 required 且必须相等；
- `MustChangePassword = true`，不允许 Administrator 在第一版取消；
- `IsActive = true`；
- `SessionVersion = 1`；
- `Version = 1`；
- failed-attempt/window/lock fields 清空；
- `LastPasswordChangedAt = credential hash 首次建立的 UTC 时间`。

`LastPasswordChangedAt` 表示当前 hash 何时建立，不表示最终用户何时亲自选择了密码。首次强制改密完成后再次更新为用户改密时间。

### 8.2 Username

- UI 可以从 EmployeeNo 带出默认值，但必须允许修改；
- Username 与 EmployeeNo 独立，后续 EmployeeNo 变化不改 Username；
- 复用 `LocalCredentialSecurity.TryNormalizeUsername`；
- 原值用于展示，NormalizedUsername 由服务端生成；
- `NormalizedUsername` 全局唯一；
- duplicate 返回 `409 conflict`，字段原因指向 `username`。

### 8.3 Password

- 复用当前 `LocalPasswordService` / `PasswordHasher<LocalLoginCredential>`；
- 不增加第二套 hash 或 policy；
- Password 只进入 HTTPS request body 与短生命周期内存；
- 只持久化 PasswordHash；
- 不允许创建 password 为 null/空、稍后再补的 Local credential；
- 如暂时没有密码，必须选择“暂不配置登录”，后续通过显式 Add Local Credential 操作创建完整 credential。

### 8.4 Existing User

允许 Administrator 为已有且没有 LocalLoginCredential 的 User 后续添加本地账号：

```text
POST /api/users/{id}/local-credential
```

规则与新建 User 的 Local setup 相同。目标 User 可以当前 inactive；credential 可以作为准备状态被创建，但 User inactive 时仍不能登录。

Add operation 没有可回传的旧 credential token；并发的两个创建由 `UNIQUE(user_id)` / `UNIQUE(normalized_username)` 决胜，一次成功，另一次返回 `409 conflict`。不得用 User Version 伪装成 credential Version。

## 9. OIDC / SSO

- 创建时要求 Provider 与 Subject / sub；
- 不显示、收集或保存本地密码；
- 不提供 OIDC 密码修改或重置；
- 密码、MFA 与 IdP account lifecycle 由企业身份提供方管理；
- 不按 Email、EmployeeNo、DisplayName 或 Username 自动映射；
- LoginIdentity 创建与 User 创建同事务；
- `LoginIdentity.IsActive = true`、`Version = 1`；
- Provider 必须精确匹配服务器配置的 approved provider key。当前实现只有一个 OIDC Provider 配置，因此第一版不是任意字符串 allowlist；
- 若 OIDC 当前 Disabled 但 Provider key 已配置，Administrator 可以预先建立 mapping；UI 必须显示“当前部署未启用企业统一登录”；
- 若没有任何 approved Provider key，不允许选择 OIDC setup，也不允许用任意 Provider 创建一个看似可用的 mapping。

一个 User 可有多个 LoginIdentity，继续遵循 `(Provider, Subject)` 唯一约束。

## 10. No-login User

选择“暂不配置登录”只创建 User，不创建 LocalLoginCredential 或 LoginIdentity。

UI 必须显示：

```text
该用户当前无法登录系统。
```

No-login User 仍可以合法作为：

- 知识提供者；
- Owner / 负责人；
- 关系引用；
- HumanConfirmation provider；
- 历史事实中的 canonical business person。

不能登录不等于 User 无效，也不阻止其业务引用。User 可在以后增加 Local 或 OIDC 登录方式，无需重新创建。

## 11. MustChangePassword

### 11.1 Field

新增：

```text
LocalLoginCredential.MustChangePassword : bool
```

不放在 User，因为它只描述本地密码 credential。

Migration 规则：

- non-null；
- SQLite default `false`；
- 现有 Local credentials 保持 `false`，不得让已部署 bootstrap account 无法完成上线；
- 由 Administrator 在 User Management 创建或重置的密码一律写 `true`；
- operator 通过受控 bootstrap/recovery command 亲自输入自己的秘密时可以写 `false`。

### 11.2 Authoritative gate

Local login 验证 Username/Password、Active、lockout 成功后仍可签发 application Cookie，但 `CurrentUserContext` 必须从数据库读取 `MustChangePassword`，并返回 password-change-required 状态。

Backend 是唯一 authority。不得只靠 Vue 跳转或隐藏菜单。

强制改密 session 只允许：

| Method / route | Reason |
| --- | --- |
| `GET /api/current-user` | 读取最小 profile、`authenticationMethod`、`mustChangePassword` |
| `GET /api/antiforgery/token` | 为改密请求取得当前身份对应 token；基础设施例外 |
| `PUT /api/current-user/password` | 完成改密 |
| `POST /auth/logout` | 退出 |

其它 business/Admin API 一律由后端返回 `403 must_change_password`，不渲染 AppShell。

`must_change_password` 不作为可信 Cookie claim。Cookie 继续携带 method/id/version；MustChangePassword 每次从 credential row 读取。前端只使用 Current User response 的投影改善 UX。

## 12. Change My Password

### 12.1 Authorization

```text
current authenticated session
AND auth_method = local
AND mapped LocalLoginCredential Active
AND canonical User Active
```

OIDC-origin session 即使映射的 User 同时拥有 Local credential，也不能调用 Change My Password。用户需要退出并通过 Local 登录后再修改；Administrator 可通过独立 Reset Password 操作恢复。

### 12.2 Input and validation

UI 输入：

- 当前密码；
- 新密码；
- 确认新密码。

API 只接收 `currentPassword` 与 `newPassword`。确认值不重复发送。

服务端必须：

- 验证当前密码；
- 验证当前唯一 password policy；
- 拒绝新旧密码相同；
- 不 trim、normalize、case-fold 或 truncate password；
- 不在 error、log 或 response 中回显密码。

### 12.3 Atomic write

成功时在一个 transaction 中：

```text
PasswordHash = LocalPasswordService.Hash(newPassword)
LastPasswordChangedAt = now UTC
MustChangePassword = false
FailedLoginAttempts = 0
FailedLoginWindowStartedAt = null
LockedUntil = null
SessionVersion += 1
Version += 1
UpdatedAt = now UTC
```

随后清除当前 application Cookie。所有旧 Local Session（包括当前）因 `SessionVersion` stale 而失效；用户必须使用新密码重新登录。

这是冻结的 **方案 A**。不在改密响应中重签 Cookie。

同一 User 的 OIDC Session 不受 Local credential `SessionVersion` 影响。

## 13. Administrator Reset Password

Administrator-only operation：

```text
POST /api/users/{id}/local-credential/reset-password
```

规则：

- 不要求旧密码；
- request body 只包含新临时密码与最近 credential concurrency token；
- 复用相同 password policy/hash service；
- `MustChangePassword = true`，第一版不能取消；
- 更新 PasswordHash、LastPasswordChangedAt、UpdatedAt；
- 清除 failed attempts / window / lockout，立即解除 lockout；
- `SessionVersion += 1`；
- `Version += 1`；
- 所有旧 Local Session 失效；
- 不改变 `LocalLoginCredential.IsActive`；inactive credential reset 后仍需单独 Enable；
- 不使同一 User 的 OIDC Session 失效；
- 不返回旧密码、新密码或 PasswordHash；
- 不提供“查看当前密码”。

Administrator 可以重置自己的 Local credential。若当前请求本身来自被重置 credential，请求提交后该 Cookie 立即 stale，下一次请求必须重新认证。

## 14. SessionVersion and Session Invalidation

`SessionVersion` 只负责 Local credential session revocation；它不是 User concurrency token，也不是 LoginIdentity version。

| Event | SessionVersion | Local sessions | OIDC sessions |
| --- | ---: | --- | --- |
| Local credential created | `1` | none yet | unchanged |
| Successful login hash rehash only | unchanged | remain valid | unchanged |
| Failed login / lockout bookkeeping | unchanged | remain valid | unchanged |
| User changes password | `+1` | all old tickets invalid, including current | unchanged |
| Administrator resets password | `+1` | all old tickets invalid | unchanged |
| Credential disabled | `+1` | all old tickets invalid | unchanged |
| Credential enabled | `+1` | no old ticket can revive | unchanged |

Cookie principal 继续携带当前 `SessionVersion` 作为 `auth_version`。`CurrentUserContext` 每次请求比较数据库值；stale 时清除 Cookie 并返回 `401 session_expired`。

不增加 `User.SecurityVersion`。User inactive 已通过每请求 User Active 校验使 Local/OIDC 全部失效；AccessLevel 继续每请求读取最新值。

## 15. Global Authentication Enablement

`Authentication:Local:Enabled` 与 `Authentication:Oidc:Enabled` 是 authentication authority，不是 UI feature flag。

当 Local Disabled：

- 不显示 Local login form；
- `POST /auth/local/login` 不建立 session；
- 已存在的 Local-origin Cookie 在下一次 request-time validation 被拒绝并清除；
- Local credential rows 不删除；
- Administrator 仍可查看、创建、重置、启用/停用 credential，用于部署准备；
- UI 明确显示“当前部署未启用本地登录”，不能声称该 User 当前可通过 Local 登录。

OIDC Disabled 使用对称规则：不 challenge、不接受 OIDC-origin session，保留 mappings 供准备或未来启用。

method 被全局关闭导致的旧 ticket 对外使用 `401 session_expired`，`details.reason = local_auth_disabled | oidc_auth_disabled`。不得在数据库中批量修改 credentials/identities 来模拟部署配置。

## 16. Lockout

继续冻结当前默认：

```text
5 failed logins within 15 minutes
→ lock new Local logins for 15 minutes
```

规则：

- public login 对 missing/wrong/inactive/locked/User inactive 统一返回 `401 invalid_credentials`；
- lockout 只阻止新 Local login，不终止已存在 session；
- lockout 期间的额外尝试不无限延长锁定；
- 成功 Local login 清除 failed/window/lock；
- 成功 self password change 清除 failed/window/lock；
- Administrator reset 清除 failed/window/lock，并立即解除 lockout；
- `MustChangePassword=true` 的 credential 仍走相同 login rate limit 与 lockout；
- 修改密码时输错 current password 不写入 public-login lockout counter；它返回安全错误并记录失败 security event；
- 不增加永久锁定。

## 17. Credential and Identity Active States

三个状态必须独立：

```text
User.IsActive
LocalLoginCredential.IsActive
LoginIdentity.IsActive
```

- User inactive：所有 Local/OIDC 方式都不能建立或继续 session；不删除 credential/identity；
- Local credential inactive：只禁止该 User 的 Local authentication；OIDC 独立判断；
- LoginIdentity inactive：只禁止该 mapping；同一 User 的其它 OIDC mapping 与 Local 独立判断；
- 状态切换不是删除；Local credential 第一版不物理删除；
- re-enable 必须增加 method version，旧 Cookie 不得复活。

## 18. Last Usable Administrator

### 18.1 Frozen definition

```text
usable Administrator =
  User.IsActive
  AND User.AccessLevel = Administrator
  AND
  (
    Authentication:Local:Enabled
    AND LocalLoginCredential.IsActive
    OR
    Authentication:Oidc:Enabled
    AND LoginIdentity.IsActive
    AND LoginIdentity.Provider is currently approved
  )
```

Provider 匹配按现有 ordinal/exact semantics；任意但未配置的 Provider mapping 不算可用。

### 18.2 Lockout and forced change

- temporary lockout **仍算可用**：它会自动到期，也可由 reset 恢复；不能让攻击者通过失败登录改变 last-admin 业务判定；
- `MustChangePassword=true` **仍算可用**：该 Administrator 能登录受限 session、完成改密并恢复管理能力；
- inactive User、inactive method、globally disabled method 不算可用。

### 18.3 Protected operations

以下操作必须在同一 transaction 内按“变更后的目标状态”检查：

- User deactivation；
- Administrator downgrade；
- LoginIdentity disable；
- LocalLoginCredential disable。

若结果为零个 usable Administrator，返回 `422 business_rule_violation`，`details.reason = last_usable_administrator`。

同一 Administrator 同时有 Local + OIDC 时，可停用其中一种，只要另一种在当前部署配置下仍可用。当前 Administrator 操作自己时使用相同规则，不增加特例。

部署配置变化不属于数据库 transaction。切换 Local/OIDC Enabled 或 OIDC Provider 前，部署流程必须预检目标配置下至少存在一个 usable Administrator；否则先通过受控 bootstrap/recovery 建立目标方式。普通 Web startup 不创建或猜测管理员。

现有 OIDC 与 Local bootstrap/recovery 的“已有 usable Administrator”判定必须复用同一 resolver，不能各自维护不同公式。

## 19. Password Policy

唯一 policy：

| Rule | Frozen decision |
| --- | --- |
| Minimum | 8 characters |
| Maximum | 128 characters |
| Unicode | Allowed |
| Whitespace | Allowed and significant |
| trim / normalization / case-fold / truncation | Forbidden |
| uppercase/lowercase/number/symbol composition checklist | Not added |
| periodic expiration | Not added |
| password history | Not added |
| breached-password online service | Not added |

Password validation 在服务端 hash 前执行。前端可复用提示，但不成为 policy authority。

## 20. Password Storage

继续冻结：

```text
only PasswordHash
no plaintext
no reversible encryption
no plaintext history
no password/hash in response, DTO projection, log, audit, exception, URL or CLI argument
```

使用当前 `LocalPasswordService` 与 Identity V3 encoded hash；不引入 ASP.NET Core Identity stores、第二套 PasswordHasher、custom crypto 或第三方 hash framework。

`PasswordHash` 不进入普通 DTO、User Detail、Login Method projection 或 frontend type。

## 21. Authorization

| Capability | Required authorization |
| --- | --- |
| Change My Password | 当前 authenticated Local-origin session；允许 normal 或 MustChange state |
| Create User with login setup | Administrator |
| Add Local Credential | Administrator |
| Enable/Disable Local Credential | Administrator |
| Administrator Reset Password | Administrator |
| Manage LoginIdentity | Administrator |
| Read User Login Methods | Administrator |

Viewer/Editor/Administrator 的现有业务 API matrix 不改变。KnowledgeRole 不参与上述判断。

所有 password writes 必须验证 antiforgery。Request body logging 必须保持关闭；反向代理不得记录 password body。

## 22. Concurrency

User 与 Local credential 使用独立 token：

- User profile/active/access writes 使用 `User.Version` 对应的 opaque token；
- existing Local credential reset 与 active-state write 使用 `LocalLoginCredential.Version` 对应的 opaque token；
- 不用 User token 代替 credential token；
- Add Local Credential 没有旧 credential token，依赖唯一约束解决同时创建；
- 两个 Administrator 用同一 credential token 同时 reset：一个成功并推进 Version，另一个 `409 conflict`；
- stale credential token：`409 conflict`，`details.resourceType = LocalLoginCredential`；
- `SessionVersion` 不作为管理并发 token 返回；
- failed login / lockout metadata 可推进 `Version`，管理员遇到 stale token 必须 reload，不静默覆盖更新后的安全状态。

## 23. Audit and Secret-safe Logging

不建立 generic Audit Framework 或 SIEM。每个实现 Slice 使用统一、结构化 security event 约定记录：

```text
eventType
actorUserId
targetUserId
credentialId / loginIdentityId when available
result
reasonCode
occurredAt UTC
correlationId (HttpContext.TraceIdentifier or equivalent)
```

最低事件：

- `LocalCredentialCreated`；
- `LocalCredentialEnabled`；
- `LocalCredentialDisabled`；
- `LocalPasswordChangedByUser`；
- `LocalPasswordResetByAdministrator`。

成功与拒绝结果都记录；未知 username 的 public login 不把 raw username 写入安全日志。

永不记录：old/new/initial password、confirmation password、PasswordHash、Cookie、antiforgery token、OIDC token、完整 password request body。

## 24. Error Model

沿用 `ApiErrorResponse(code, message, fieldErrors, details)` 与现有通用 code。任务中列出的更细语义放入稳定 `details.reason`，避免建立第二套错误 envelope。

| Semantic condition | HTTP | `code` | `details.reason` / public behavior |
| --- | ---: | --- | --- |
| public login missing/wrong/inactive/locked | 401 | `invalid_credentials` | 不暴露细分状态 |
| password policy invalid | 400 | `validation_error` | `password_policy_invalid`，fieldErrors 指向 password field |
| credential not found | 404 | `not_found` | `credential_not_found` |
| credential inactive during authenticated management flow | 422 | `business_rule_violation` | `credential_inactive` |
| credential locked | — | — | Admin projection 可见；public login 折叠为 `invalid_credentials` |
| forced change blocks business API | 403 | `must_change_password` | `must_change_password` |
| stale credential token | 409 | `conflict` | `concurrency_conflict`，resourceType=`LocalLoginCredential` |
| last usable Administrator | 422 | `business_rule_violation` | `last_usable_administrator` |
| auth method globally disabled, existing ticket | 401 | `session_expired` | `local_auth_disabled` / `oidc_auth_disabled` |
| caller lacks required access | 403 | `forbidden` | existing behavior |

`POST /auth/local/login` 在 Local Disabled 时继续保持当前 `404 not_found`/route-unavailable public behavior；Login Gate 以 `/api/auth/options` 为 authority，不显示该表单。

全部用户可见 message 使用简体中文。前端必须依据 `code`/`details.reason`，不得解析中文 message。

## 25. API and Use Case Direction

明确 Use Case：

```text
CreateUserWithLoginSetup
GetUserLoginMethods
CreateUserLocalCredential
SetLocalCredentialActiveState
ChangeMyLocalPassword
ResetUserLocalPassword
```

保留并收紧现有 LoginIdentity operations。不得建立 generic CredentialController、generic secret CRUD 或 Password entity。

规划 route：

| Method | Route | Purpose |
| --- | --- | --- |
| `POST` | `/api/users` | User + discriminated loginSetup |
| `GET` | `/api/users/{id}/login-methods` | Local metadata + OIDC mappings + global enabled state；never hash |
| `POST` | `/api/users/{id}/local-credential` | Existing User add Local credential |
| `PUT` | `/api/users/{id}/local-credential/active-state` | Enable/disable with credential token |
| `POST` | `/api/users/{id}/local-credential/reset-password` | Administrator reset with credential token |
| `PUT` | `/api/current-user/password` | Local-origin self change |

现有 `/api/users/{id}/login-identities` route 继续承担具体 OIDC mapping 管理，不合并为通用 CRUD。

## 26. UI Direction

### 26.1 Create User

- 01 基础资料；
- 02 知识身份；
- 03 登录方式；
- Local：登录用户名、初始密码、确认密码、“首次登录必须修改密码”（checked + required）；
- OIDC：身份提供方、Subject / sub；不显示 password；
- none：明确警告该 User 当前无法登录。

### 26.2 Edit User

增加统一“登录方式”区域：

```text
本地账号
- 用户名
- 当前状态
- 当前部署是否启用本地登录
- 最近修改密码时间
- 是否要求首次改密
- 临时锁定状态
- [重置密码]
- [启用/停用本地登录]

企业统一登录（OIDC / SSO）
- Provider
- Subject / sub
- 状态
- 当前部署是否启用企业统一登录
- [添加映射]
- [启用/停用映射]
```

永不显示 PasswordHash、当前密码或可逆 secret。

### 26.3 Current User menu

- 当前 session `auth_method=local`：显示“修改密码”和“退出登录”；
- 当前 session `auth_method=oidc`：只显示“退出登录”，可提示“密码由企业统一登录系统管理”；
- 同一 User 即使拥有 Local credential，只要当前 session 来自 OIDC，就不显示 Local change-password action；
- MustChange state 不渲染业务 Shell，只渲染强制改密视图与退出入口。

全部主标签使用简体中文；OIDC、SSO、Provider、Subject / sub 可作为必要技术词汇。

## 27. Forgot Password

第一版本不实现 Forgot Password / Email reset。

```text
用户忘记本地密码
→ 联系 Administrator
→ Administrator Reset Password
→ 使用临时密码登录
→ 强制修改密码
```

未来如需要 Email reset token、expiry、one-time use，必须独立立项。

## 28. Non-goals

本任务及其已冻结生命周期不引入：

- full ASP.NET Core Identity replacement；
- Password UI/API/Migration implementation in A01；
- MFA、TOTP、Passkey、Recovery Code；
- password history、password expiry、breached-password online dependency；
- email/SMS reset、security questions；
- LDAP、AD、JIT user provisioning；
- multiple Local credentials per User；
- local credential physical delete；
- username rename；
- generic credential/secret framework；
- JWT SPA auth、browser token storage、second Cookie；
- generic Audit Framework、SIEM 或 approval workflow。

## 29. Follow-up Task Sequence

以下是唯一批准顺序。为避免 Administrator-created temporary password 在 backend forced-change gate 完成前可进入业务系统，安全基础先于 User Management credential release：

### AUTH-USER-B01 — Local Password Lifecycle Safety Foundation

- `MustChangePassword` additive migration/default；
- authoritative forced-change backend gate；
- Current User method/must-change projection；
- `ChangeMyLocalPassword` API/UI；
- 本文冻结的“改密后全部 Local Session（含当前）失效”；
- globally disabled auth-method ticket rejection；
- unified usable-Administrator resolver 用于现有 User/LoginIdentity/bootstrap paths；
- focused security events and tests。

### AUTH-USER-B02 — Create User with Login Setup

- Create User 的 Local / OIDC / none discriminated contract；
- User + assignments + selected method 同事务；
- Create User “03 登录方式”UI；
- Provider allowlist validation；
- Get User Login Methods projection；
- no-login / globally-disabled method presentation。

### AUTH-USER-B03 — Existing User Login Method Management

- 为 Existing User Add Local Credential；
- Local credential enable/disable；
- OIDC mapping presentation/management alignment；
- independent credential concurrency token；
- unified last-usable-Administrator guard for new Local active-state operation。

### AUTH-USER-B04 — Administrator Password Reset

- reset temporary password；
- mandatory MustChangePassword；
- lockout clear；
- SessionVersion invalidation；
- reset UI、concurrency、audit 与 self-target behavior。

### AUTH-USER-VERIFY — Full Login Lifecycle Verification

- Local/OIDC/none create；
- Local + OIDC coexistence；
- MustChange bypass attempts；
- self change/current and other Local session invalidation；
- admin reset/lockout/active-state；
- global method enablement；
- last usable Administrator；
- antiforgery、authorization、concurrency、audit secret-safety；
- isolated SQLite/browser/runtime verification and cleanup。

不得跳过 B01 先发布 Administrator-created/reset Local credentials。不得自动开始任何后续任务。

## 30. Acceptance Criteria

AUTH-USER-A01 的 architecture gate 已满足：

- [x] User / LocalLoginCredential / LoginIdentity 边界明确；
- [x] Local / OIDC / 暂不登录三种方式明确；
- [x] User + selected login method 事务与 rollback 明确；
- [x] Existing User 后续 Add Local 明确；
- [x] Local + OIDC coexistence 明确；
- [x] MustChangePassword 字段、default、gate 与 projection 明确；
- [x] Change My Password authorization、write 与 Session 行为明确；
- [x] Administrator Reset Password 行为明确；
- [x] SessionVersion 职责和 Local/OIDC isolation 明确；
- [x] lockout reset/clear 语义明确；
- [x] User/Credential/Identity/global method Active 语义明确；
- [x] last usable Administrator 定义完整；
- [x] password policy/hash/storage 明确；
- [x] authorization、antiforgery、concurrency、audit 明确；
- [x] error model 与简体中文 UI 明确；
- [x] OIDC-only 与 Local-disabled deployment 行为明确；
- [x] 后续任务序列唯一；
- [x] 无 Blocker / High 未解决设计问题。

## 31. Final Decision

```text
AUTH-USER-A01 APPROVED

Blocking human decisions:
- None

Next:
- AUTH-USER-B01 READY: YES
```

完成 A01 后停止。不得自动开始 AUTH-USER-B01。
