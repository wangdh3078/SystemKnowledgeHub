# AUTH-A01 — Local Username/Password Login + OIDC Coexistence Architecture Review

> Product: 系统知识中心 / System Knowledge Hub
>
> Deliverable: Authentication Architecture Amendment / Product Login Design
>
> Status: Proposed — awaiting Architecture Gate
>
> Review date: 2026-08-22

## 1. Executive Decision

### Required answers

| Question | Decision |
|---|---|
| Should System Knowledge Hub support local username/password login? | **YES** |
| Should OIDC remain supported? | **YES** |
| Adopt full ASP.NET Core Identity? | **NO** |
| Adopt a minimal `LocalLoginCredential` model? | **YES** |
| Local and OIDC use the same application Cookie? | **YES** |
| At most one LocalLoginCredential per canonical User? | **YES** |
| Temporary account lockout? | **YES** |
| Login rate limiting? | **YES** |
| Administrator password reset? | **YES** |
| User self-service password change? | **YES** |
| `MustChangePassword` in the complete Local Credential MVP? | **YES**; it is required before Administrator-created/reset credentials are released, but it is not required for the smaller AUTH-B01 + AUTH-B02 development-recovery gate where the bootstrap Administrator securely chooses their own password. |
| Password reset invalidates existing sessions? | **YES**, existing Local sessions for that credential only. OIDC sessions for the same User remain valid. |
| Public authentication-options endpoint? | **YES** |

The current deployment reality justifies Local Login: without a real enterprise OIDC Provider, the application cannot establish an authenticated development or small-deployment session, even though SEC01–SEC03 code is valid. A safe local method restores a real Browser → Cookie → Current User → AccessLevel path without reviving the insecure browser User Selector or `X-Current-User-Id`.

The architecture remains:

```text
LocalLoginCredential ───────────┐
                               │
                               ▼
                          canonical User
                               │
OIDC LoginIdentity ─────────────┘
                               │
                               ▼
                    Application Authentication Cookie
                               │
                               ▼
                     principal-backed Current User
                               │
                               ▼
                    latest canonical AccessLevel
```

Local Login and OIDC are authentication methods. They do not own a separate profile, AccessLevel, KnowledgeRole, Current User, Evidence actor, HumanConfirmation identity, or KnowledgeDocument author.

The chosen session design is a **source-aware authentication session descriptor** in the protected Cookie ticket:

```text
AuthMethod + AuthIdentityId + AuthVersion + UserId + AccessLevel projection
```

`CurrentUserContext` keeps its current business meaning but resolves the appropriate OIDC identity or Local credential on every request, then re-reads the canonical User and latest AccessLevel. No generic AuthenticationIdentity table and no User-wide SecurityVersion are required.

## 2. Current Authentication Baseline

The current repository has a coherent SEC01–SEC03 implementation:

- ASP.NET Core Cookie Authentication scheme `ApplicationCookie` with cookie name `SystemKnowledgeHub.Auth`.
- HttpOnly application Cookie, `SameSite=Lax`, eight-hour sliding expiration, Secure outside Development.
- OIDC challenge at `GET /auth/login`, authorization-code flow, standard handler state/nonce/correlation, and `SaveTokens = false`.
- Exact configured `Provider + sub` lookup through `login_identities`; no email/name/employee-number auto-linking and no JIT User creation.
- Cookie ticket claims currently contain `login_identity_id`, `user_id`, and `access_level`.
- `CurrentUserContext` reads those server-created claims, re-reads LoginIdentity and canonical User, rejects inactive/mismatched state, and projects the latest User AccessLevel on each request.
- Viewer/Editor/Administrator backend authorization policies use `ICurrentUserContext`; KnowledgeRole is not permission.
- Unsafe `/api/**` and `/auth/logout` requests validate `X-CSRF-TOKEN` through the existing antiforgery service.
- Vue bootstraps through `GET /api/current-user`; `actorStore` is the only current-user/profile/access store.
- The Login Gate currently has only “使用企业账号登录.”
- The TopBar is read-only identity presentation plus `POST /auth/logout`; there is no browser User selector and no trusted `X-Current-User-Id` path.
- Administrator User Management can create/enable/disable OIDC `LoginIdentity` mappings.
- `bootstrap-admin` creates or binds one canonical Administrator plus an OIDC LoginIdentity and starts no web server.

Current persistence:

```text
users
  ├─ profile fields
  ├─ is_active
  ├─ access_level
  └─ version

login_identities
  ├─ user_id → users(id) RESTRICT
  ├─ provider + subject UNIQUE
  ├─ is_active
  └─ version
```

There is no local credential, password hash, password API, rate limiter, login lockout, or local-login UI. `Microsoft.AspNetCore.Authentication.OpenIdConnect` is the only explicit authentication package; the project does not use full ASP.NET Core Identity.

SEC04 remains `BLOCKED`, not code-failed. The missing real provider/callback, public HTTPS/reverse proxy, protected persistent Data Protection keys, and deployment topology still prevent Production rollout approval.

## 3. Problem Statement

The OIDC-only production model is appropriate for enterprise SSO, but it leaves three concrete supported deployment scenarios without a usable application identity:

1. Local development without a registered OIDC application.
2. Functional/browser verification before an enterprise IdP environment is available.
3. Small-team or isolated deployments where no OIDC Provider is operated.

Test Authentication is not the answer. It is intentionally scoped to `WebApplicationFactory` integration tests and must never become a browser or deployed authentication mechanism.

The solution must not be a development backdoor, a default password, an anonymous fallback, or a browser-selected User. It must be a real credential method with password hashing, throttling, lockout, CSRF protection, explicit canonical User binding, session revocation, and the same authorization boundary as OIDC.

## 4. Target Authentication Model

### Authentication method separation

```text
Authentication Method ≠ canonical User
```

Target cardinality:

```text
1 canonical User
  ├─ 0..1 LocalLoginCredential
  └─ 0..N LoginIdentity (OIDC)
```

The canonical User remains the business person and continues to own:

- DisplayName, EmployeeNo, Email, DepartmentOrTeam, JobTitle;
- Active state;
- AccessLevel;
- KnowledgeRole assignments;
- references and historical attribution.

The authentication-method entities own only the information necessary to establish and revoke an authentication session. `User.Email` and `User.EmployeeNo` remain profile metadata and are never implicit usernames.

### Target sign-in flows

```text
Local:
Browser form
→ POST /auth/local/login + antiforgery token
→ normalized username lookup
→ IP rate limit + credential lockout
→ PasswordHasher verification
→ Local credential active + canonical User active
→ same ApplicationCookie

OIDC:
Browser redirect
→ GET /auth/login
→ standard OIDC challenge/callback
→ exact LoginIdentity mapping
→ LoginIdentity active + canonical User active
→ same ApplicationCookie

Both:
ApplicationCookie
→ Auth session descriptor
→ source-aware request-time identity validation
→ canonical User
→ latest AccessLevel
→ existing authorization policies and Current User
```

No access or refresh token is exposed to Vue. No second Cookie, Local principal class, Local Current User service, JWT flow, or browser credential storage is introduced.

## 5. Local Credential Domain Model

### Name

Use **`LocalLoginCredential`** and table **`local_login_credentials`**.

| Candidate | Decision |
|---|---|
| `LocalLoginCredential` | **Selected.** Clearly identifies a local interactive login credential. |
| `LocalCredential` | Too broad; could later be mistaken for API keys or recovery secrets. |
| `PasswordCredential` | Accurate about the secret, but not as explicit about local application login. |
| `UserCredential` | Too generic and risks becoming a `Type + Secret` framework. |

Keep existing `LoginIdentity` naming unchanged. It continues to represent external OIDC identity mapping. Renaming it to `ExternalLoginIdentity` would create migration, API, frontend, report, and claim churn for no current behavior gain.

### Field decisions

| Candidate field | Decision | Reason |
|---|---|---|
| `Id` | Required | Safe integer PK and Cookie identity descriptor. |
| `UserId` | Required | Explicit FK to canonical User. |
| `Username` | Required | Administrator-visible/display value; independent of profile metadata. |
| `NormalizedUsername` | Required | Case-insensitive deterministic lookup and unique constraint. |
| `PasswordHash` | Required | Framework-encoded one-way hash only; never returned. |
| `IsActive` | Required | Independent Local method revocation. |
| `FailedLoginAttempts` | Required | Per-credential brute-force control. |
| `FailedLoginWindowStartedAt` | Required, nullable | Prevents failures spread across months from accumulating into a lock. |
| `LockedUntil` | Required, nullable | Temporary lockout; never permanent account lock. |
| `SessionVersion` | Required | Invalidate Local sessions on password change/reset, credential disable, or username change without invalidating OIDC sessions. |
| `MustChangePassword` | Required for AUTH-B03/B04, not needed by bootstrap-only B01/B02 path | Enforces safe Administrator-created/reset temporary credentials. |
| `CreatedAt` / `UpdatedAt` | Required | Credential lifecycle metadata. |
| `LastPasswordChangedAt` | Required | Security-relevant password lifecycle fact. |
| `LastLoginAt` | Deferred | Operationally useful, but creates a write on every success and is not needed to authenticate. Structured security logs provide first-phase visibility. |
| `Version` | Required | Existing app-managed opaque concurrency for Administrator mutations. |

`Version` and `SessionVersion` are deliberately different:

- `Version` protects management writes and may change when admin-visible credential state changes.
- `SessionVersion` changes only when existing Local sessions must become invalid.
- Failed-attempt/lockout bookkeeping must not change `SessionVersion`, otherwise an attacker could log out a victim by submitting bad passwords.

Do not add navigation dependencies from the canonical User aggregate. Application queries may join directly through `KnowledgeHubDbContext`, consistent with the existing architecture.

## 6. ASP.NET Identity vs Minimal Credential

| Criterion | Option A — Minimal LocalLoginCredential | Option B — Full ASP.NET Core Identity |
|---|---|---|
| Canonical User fit | Direct `LocalLoginCredential → User`; User stays unchanged | Requires IdentityUser beside canonical User or turns User into an Identity model, both conflicting with the approved boundary |
| Schema | One focused table plus a small claim amendment | Identity user/role/token/login/claim schema or custom stores/managers |
| Password hashing | Reuse `PasswordHasher<T>` without stores/UI | Built in |
| Lockout | Small explicit credential fields and use case | Built in but tied to Identity user/store semantics |
| Password reset | Explicit Administrator reset and self change | Rich token flows, many unused capabilities |
| Future MFA/passwordless | Requires future amendment | Better built-in path |
| OIDC coexistence | Two explicit authentication methods map canonical User | Supported, but with an additional Identity account lifecycle |
| Migration/operations | Additive and understandable | Larger data model, operational and upgrade surface |
| Current project size | **Good fit** | Overweight for a single-project internal tool |
| Main risk | Application owns lockout/reset/session details | Architecture and identity duplication |

**Decision: Option A.** Use the secure framework password-hashing component, Cookie middleware, antiforgery, rate limiting, and authorization primitives without adopting Identity stores, IdentityUser, SignInManager, UserManager, roles, token providers, or generated Identity UI.

Full ASP.NET Core Identity should be reconsidered only if the product approves multiple local credentials, email/self-service recovery, MFA, passkeys, external user registration, complex account lifecycle, or regulatory identity requirements. None is currently approved.

## 7. Username Semantics

Username is a dedicated credential identifier:

- required;
- 3–64 characters after trimming;
- leading/trailing whitespace is trimmed;
- internal whitespace and control characters are rejected;
- Unicode letters and numbers are allowed, including Chinese characters;
- a small punctuation set such as `.`, `_`, `-`, and `@` is allowed;
- original `Username` is stored for display;
- `NormalizedUsername` is produced server-side as trimmed Unicode Form KC followed by `ToUpperInvariant()`;
- unique comparison uses `NormalizedUsername`, so `Admin`, `admin`, and `ADMIN` conflict;
- clients never supply or choose `NormalizedUsername`;
- the same normalization function is used for creation, lookup, and any future rename.

The compatibility normalization reduces full-width/canonical variants before case folding. Because visually confusable Unicode characters still exist, credentials remain Administrator-created and the UI must display the exact stored Username during assignment. No automatic account linking uses username.

Username is not Email and not EmployeeNo. An Administrator may deliberately choose a string equal to either value, but changing the profile field never renames the credential, and matching values never create a credential.

First release does not expose username rename. AUTH-B04 may add one explicit Administrator operation if correction is required; it must enforce uniqueness, use the credential token, increment `SessionVersion`, and invalidate existing Local sessions. Physical delete/recreate is not the rename mechanism because each User has at most one retained credential.

## 8. Password Hashing

Use `Microsoft.AspNetCore.Identity.PasswordHasher<LocalLoginCredential>` as an isolated hashing component, without adopting full ASP.NET Core Identity.

Required behavior:

- Identity V3 encoded format;
- PBKDF2 work factor configured explicitly rather than relying silently on a framework default;
- initial planning target: 220,000 iterations, benchmarked on the actual deployment host with an interactive verification target around 100–250 ms and adjusted upward when practical;
- framework-generated random salt embedded in the encoded hash;
- hash format metadata allows later algorithm/work-factor upgrade;
- on `PasswordVerificationResult.SuccessRehashNeeded`, replace the hash in the successful login transaction without changing the password or `SessionVersion`;
- store the encoded value in an unbounded/adequately sized SQLite `TEXT` column, not a short fixed string;
- never implement SHA-256/MD5 password hashing, salt generation, reversible encryption, or custom crypto.

Microsoft documents that `PasswordHasherOptions` controls Identity V3 PBKDF2 work factor and the encoded format supports verifying older versions: [Configure ASP.NET Core Identity — Password Hasher options](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/identity-configuration?view=aspnetcore-8.0). OWASP recommends modern adaptive password hashing and notes Argon2id or appropriately configured PBKDF2 as valid choices: [OWASP Password Storage Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Password_Storage_Cheat_Sheet.html).

Argon2id is technically strong, but adopting it now requires a new third-party library, parameter/upgrade ownership, and a separate supply-chain decision. The framework PasswordHasher is the smaller maintainable choice for this .NET 8 application. An Argon2id migration can be introduced later through successful-login rehash if a security requirement justifies it.

A password hash only raises the cost of offline cracking. A stolen SQLite database still exposes hashes to offline guessing; strong user passwords, appropriate KDF cost, database/backup access controls, and incident response remain necessary.

## 9. Password Policy

Initial policy:

| Rule | Decision |
|---|---|
| Minimum length | 15 characters because Local MVP has no MFA |
| Maximum length | 128 characters to allow passphrases while bounding CPU/memory abuse |
| Unicode | Allowed |
| Whitespace | Allowed and significant, including leading/trailing whitespace |
| Trimming/normalization | **Never** trim, normalize, case-fold, or truncate a password |
| Composition checklist | None; do not require uppercase/lowercase/number/symbol combinations |
| Paste/password managers | Allowed and supported |
| Periodic expiration | Not implemented |
| Password history | Not implemented |
| Breached-password service/blocklist | Deferred; no network dependency in MVP |

Length is validated before hashing; an overlong password is rejected, never truncated. The API, model validation, exception logging, and error envelope must never echo the password.

OWASP recommends long passwords/passphrases, a maximum of at least 64 characters, Unicode/whitespace support, no silent truncation, generic login errors, and login throttling: [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html).

## 10. Login API

### Endpoint

```text
POST /auth/local/login
```

Request:

```json
{
  "username": "local-admin",
  "password": "..."
}
```

Do not include `returnUrl`. Local Login is an XHR/fetch operation on the already displayed SPA route; after `204`, the frontend reloads Current User and continues at the same safe local URL. OIDC keeps its current server-validated `returnUrl` on `GET /auth/login`.

Success:

```text
204 No Content
Set-Cookie: SystemKnowledgeHub.Auth=...
```

`204` is preferred over returning `CurrentUserResponse`: it avoids a second identity projection and forces both login methods to converge through the canonical `GET /api/current-user` path. After Current User succeeds, the frontend obtains a fresh authenticated antiforgery token.

Login request processing, in order:

1. Require Local authentication to be server-enabled.
2. Validate antiforgery token and same-origin deployment policy.
3. Apply the named per-IP login rate limit.
4. Validate input shape and password maximum before KDF work.
5. Normalize username and read credential plus canonical User.
6. For a missing credential, perform verification against a process-held dummy PasswordHasher hash to reduce timing discrepancy.
7. For an existing credential, apply temporary lockout state and still use a comparable KDF path where safe.
8. Verify PasswordHasher result.
9. On failure, update the per-credential failure window atomically, emit a generic response, and log only non-secret security metadata.
10. On success, require `Credential.IsActive` and `User.IsActive`, clear failure/lock fields, perform any required rehash, and issue a fresh application Cookie with the standardized descriptor.

If a valid application session already exists, reject Local Login with `409 already_authenticated` and require logout first. The first release does not implement account switching.

### Public login error behavior

| Condition | HTTP / code | UI |
|---|---|---|
| Username missing, password wrong, credential inactive, User inactive, or credential temporarily locked | `401 invalid_credentials` | “用户名或密码错误，或当前账号暂不可用。” |
| Per-IP limiter exceeded | `429 too_many_requests` | “登录尝试过于频繁，请稍后再试。” |
| Missing/invalid login antiforgery token | `403 antiforgery_failed` | Refresh login configuration/token; do not submit automatically. |
| Local method disabled | `404 not_found` or endpoint not mapped | Login UI must not show the form. |
| Already authenticated | `409 already_authenticated` | Continue current session or log out explicitly. |

Do not expose `account_locked`, `credential_inactive`, `account_inactive`, or “username not found” to an unauthenticated caller. Those distinctions enable enumeration. Internal structured logs may record the true reason.

## 11. Cookie / Principal Integration

Both methods issue the existing `SystemKnowledgeHub.Auth` Cookie through `HttpContext.SignInAsync(ApplicationCookie, principal, properties)`.

Cookie properties remain:

- HttpOnly;
- Secure outside Development and mandatory over Production HTTPS;
- SameSite Lax;
- existing eight-hour sliding lifetime;
- non-persistent browser session behavior; no Remember Me;
- protected by the existing Data Protection application name/key ring.

Do not add a Local Cookie. Cookie collision is intentional convergence, not a problem.

### Standard principal descriptor

| Internal claim | OIDC value | Local value | Purpose |
|---|---|---|---|
| `auth_method` | `oidc` | `local` | Select request-time credential validation and future logout UX. This now has a concrete current purpose. |
| `auth_identity_id` | `LoginIdentity.Id` | `LocalLoginCredential.Id` | Points to the method-specific credential row. |
| `auth_version` | `LoginIdentity.Version` at issuance | `LocalLoginCredential.SessionVersion` at issuance | Prevents disabled/reset credentials from reviving old tickets. |
| `user_id` | canonical User ID | canonical User ID | Mapping integrity check. |
| `access_level` | current projection | current projection | UX/claim projection only; request-time database value remains authoritative. |

Use one narrow principal-construction helper shared by OIDC callback and Local Login so claim names and formatting cannot drift. This is a concrete two-caller helper, not an identity framework.

During AUTH-B01 deployment, existing tickets with only `login_identity_id` lack the source/version descriptor. Reject them as `session_expired` and require one OIDC re-login. SEC04 has not approved a production rollout, so preserving an ambiguous legacy ticket is less safe than a deliberate one-time session reset.

ASP.NET Core Cookie Authentication serializes a protected principal into a cookie and reconstructs it on later requests; Local Login should use the same standard mechanism: [Microsoft cookie authentication without full Identity](https://learn.microsoft.com/en-us/aspnet/core/security/authentication/cookie?view=aspnetcore-10.0).

Successful SignIn creates a new protected ticket. Reject an already-authenticated login request and issue a fresh Cookie after successful authentication, preventing the old application ticket from being reused as an account-switch mechanism.

## 12. Session Validation

### Compared options

| Option | Benefits | Problems | Decision |
|---|---|---|---|
| A. `AuthMethod + AuthIdentityId + AuthVersion + UserId` and source-specific lookup | Small additive change, preserves existing tables, method-specific revocation, explicit | Adds a small switch in CurrentUserContext | **Selected** |
| B. Unified `AuthenticationIdentity` table for OIDC and Local | Uniform lookup | Generic credential abstraction, migration/rename churn, secret/type table temptation | Reject |
| C. Cookie depends only on User plus `User.SecurityVersion` | Simple lookup and global logout | Local password reset would also kill OIDC sessions; loses identity-level deactivation semantics | Reject |

### Request-time behavior

`ICurrentUserContext` interface and returned canonical profile remain unchanged in purpose. Its Web implementation becomes source-aware:

```text
read protected descriptor
→ validate safe IDs/version/method
→ if oidc: LoginIdentity ID + Version + Active + User mapping
→ if local: LocalLoginCredential ID + SessionVersion + Active + User mapping
→ read canonical User and require Active
→ read latest AccessLevel
→ return the same CurrentUserResponse
```

Important details:

- Lockout blocks new Local logins; it does **not** invalidate an already valid Local session. Otherwise an attacker could terminate a victim's session by causing failed attempts.
- Local credential disable increments `SessionVersion` and sets inactive. The next request rejects the ticket immediately.
- Re-enabling the credential does not revive an older ticket because its `auth_version` no longer matches.
- Password self-change/reset increments `SessionVersion`; all older Local tickets fail next request.
- OIDC LoginIdentity deactivate continues to invalidate OIDC tickets; its existing `Version` increment prevents an old ticket reviving after re-enable.
- User inactive invalidates all OIDC and Local sessions at the next request.
- AccessLevel downgrade takes effect on all methods at the next request without a new Cookie.
- Database failure fails closed; it does not trust the claim fallback.

No `User.SecurityVersion` is added. Credential-specific SessionVersion achieves the required reset semantics without unnecessarily invalidating another authentication method.

## 13. OIDC Coexistence

OIDC remains a first-class supported authentication method for enterprise SSO, future MFA policy, centralized employee lifecycle, and production deployments.

Keep:

- `GET /auth/login`;
- standard OIDC handler/callback;
- exact Provider + Subject mapping;
- current `LoginIdentity` table and Administrator management routes;
- no JIT provisioning or email matching;
- no token persistence;
- same application Cookie.

Amend only the Cookie claim projection to the standardized session descriptor. Do not rename or merge LoginIdentity with LocalLoginCredential.

### Deployment modes

| Mode | Local enabled | OIDC enabled | Supported |
|---|---:|---:|---|
| Local only | Yes | No | **Yes** — development, isolated, or small-team deployment |
| OIDC only | No | Yes | **Yes** — preferred enterprise SSO mode |
| Local + OIDC | Yes | Yes | **Yes** — mixed/recovery-compatible deployment with both explicitly administered |

Suggested server-controlled configuration:

```text
Authentication:Local:Enabled
Authentication:Oidc:Enabled
Authentication:Oidc:DisplayName
Authentication:Oidc:Provider / Authority / ClientId / ...
```

Development default: Local enabled, OIDC disabled unless configured. This provides no default account or password; the operator must run the secure bootstrap command.

Production: every method must be explicitly enabled. OIDC configuration is mandatory only when OIDC is enabled. Data Protection and HTTPS requirements remain mandatory for either method. If both methods are false, application startup fails closed outside the explicit Testing environment; it must never fall back to anonymous access.

An enterprise deployment can later set Local false and OIDC true while retaining disabled/unreachable Local credential rows. There is no special URL or emergency bypass when Local is disabled.

Account linking remains Administrator-explicit. Matching Local username to OIDC email/subject is prohibited.

## 14. Login Page UX

Default combined layout:

```text
┌────────────────────────────────────┐
│ System Knowledge Hub               │
│ 系统知识中心                       │
│                                    │
│ 账号                               │
│ [______________________________]   │
│                                    │
│ 密码                          [👁]  │
│ [______________________________]   │
│                                    │
│ [             登录             ]   │
│                                    │
│ ───────────── 或 ───────────────   │
│                                    │
│ [       使用企业账号登录       ]   │
└────────────────────────────────────┘
```

Behavior:

- Local form is first when enabled.
- Username receives initial focus.
- Enter submits once; loading disables repeated submission.
- Password show/hide is available but defaults hidden.
- Password paste is allowed.
- `autocomplete="username"` and `autocomplete="current-password"` are used.
- No Remember Me.
- No password, hash, access token, or credential is stored in localStorage/sessionStorage.
- Browser/password-manager username memory is allowed; application code does not persist it.
- Generic invalid-credential message does not distinguish missing, inactive, locked, or wrong password.
- After `204`, call `actorStore.loadCurrentUser()`, refresh the authenticated antiforgery token, and render the existing shell.

Configuration-specific UI:

- Local only: show the form, no divider or enterprise button.
- OIDC only: preserve a simple enterprise login action, with configured friendly label.
- Both: form, “或”, enterprise button.
- Neither: server should not start; no anonymous fallback UI.
- Authentication options load failure: show “无法加载登录配置，请重试或联系管理员”; do not guess enabled methods.

Do not add a separate “choose login method” page. Do not restore a User selector.

## 15. Authentication Options

Add an anonymous, safe, non-sensitive endpoint:

```text
GET /api/auth/options
```

Response:

```json
{
  "localLoginEnabled": true,
  "oidcLoginEnabled": true,
  "oidcDisplayName": "使用企业账号登录"
}
```

Do not return Authority, ClientId, ClientSecret, callback configuration, provider Subject rules, lockout thresholds, or internal claim names. ClientId is not normally secret, but the frontend does not need it.

The server is authoritative. Build-time Vue flags are rejected because one static SPA artifact must reflect the deployed server configuration. Server-rendered configuration is unnecessary because the current application is a Vue SPA with an established shared API client.

The endpoint uses `[AllowAnonymous]`, `Cache-Control: no-store`, and the existing JSON conventions. The OIDC friendly name is configured; never display Authority URLs.

## 16. Credential Administration

Administrator-only User Management eventually gains a separate **“本地登录”** section beside, not inside, OIDC LoginIdentity management.

Capabilities:

- show whether a Local credential exists;
- show Username, Active state, temporary lock state if operationally useful, password-change-required state, last password change time, and concurrency token;
- create the one credential for an existing canonical User;
- enable/disable it;
- reset its password;
- optionally rename username through an explicit later operation;
- never show PasswordHash or the existing/new password.

At most one Local credential per User. `UNIQUE(user_id)` and `UNIQUE(normalized_username)` enforce this.

Credential creation and reset use a temporary password set by the Administrator over HTTPS and mark `MustChangePassword = true`. The Administrator communicates it out of band. The system does not add email/SMS delivery. A generated one-time value would still require secure display/handoff and recovery; it is not materially simpler for this internal tool.

Disable rather than physically delete. Retention is needed for stable session validation and security history. Re-enable does not revive old sessions.

The current “last usable Administrator” invariant must be amended:

```text
usable Administrator =
  User.Active
  AND User.AccessLevel = Administrator
  AND (
       any active OIDC LoginIdentity
       OR active LocalLoginCredential
  )
```

User deactivation, Administrator downgrade, OIDC identity disable, and Local credential disable must check this invariant in the same transaction. An Administrator with both methods may safely disable either one while the other remains usable.

## 17. Password Change / Reset

### Self password change

Recommended route:

```text
PUT /api/current-user/password
```

This route belongs to Current User because it changes the current canonical User's one Local credential. It is clearer than a root auth route and reuses the authenticated current-user context.

Request:

```json
{
  "currentPassword": "...",
  "newPassword": "..."
}
```

Requirements:

- authenticated session;
- canonical User Active;
- Local credential exists and is Active;
- verify current password even if the current session was established by OIDC;
- validate new policy and reject equal old/new password;
- replace hash, clear `MustChangePassword`, set `LastPasswordChangedAt`, increment `SessionVersion` and management `Version` in one transaction;
- never log or echo either password.

If called from a Local session, reissue the current application Cookie with the new `SessionVersion` after commit. Other Local sessions fail at their next request. If called from an OIDC session, keep the OIDC Cookie; only Local sessions are invalidated.

Use `autocomplete="current-password"` and `autocomplete="new-password"` in the UI.

### Administrator reset

Administrator reset:

- does not require the old password;
- requires Administrator policy and current credential concurrency token;
- accepts a temporary password in the HTTPS request body only;
- sets `MustChangePassword = true`;
- increments `SessionVersion`, invalidating all Local sessions for that credential;
- does not invalidate any OIDC session for the same canonical User;
- records a structured security event without password/hash.

### Forced password change

When a Local login succeeds with `MustChangePassword = true`, issue the same application Cookie but mark/resolve the session as password-change-required. Only these operations are permitted:

- read minimal Current User/password-change state;
- `PUT /api/current-user/password`;
- `POST /auth/logout`.

The normal Viewer/Editor/Administrator policies must not authorize business APIs until the change completes. The backend enforces this; hiding the shell is only UX. After change, reissue a normal descriptor Cookie and continue.

No forgot-password, email token, SMS, security question, anonymous reset, password expiration, or password history is included.

## 18. Lockout / Rate Limiting

Rate limiting and account lockout solve different layers and both are required.

### Per-credential temporary lockout

Initial configurable safe defaults:

```text
5 failed attempts within 15 minutes
→ lock new logins for 15 minutes
```

Rules:

- the counter belongs to the credential, so attempts from many IPs still accumulate;
- after the observation window expires, start a new counter;
- success resets the counter/window/lock;
- attempts during lock do not extend it indefinitely;
- no permanent lock;
- lock does not terminate an existing session;
- public response remains generic `invalid_credentials`;
- Administrator can see the lock state and may reset it only as part of a deliberate credential reset/enable operation, not through a broad audit framework.

### Per-IP middleware limit

Apply ASP.NET Core Rate Limiting only to `POST /auth/local/login`, with a no-queue sliding/fixed-window policy. Initial planning value: 20 attempts per IP per 5 minutes, configuration-validated and reviewed against expected NAT/shared-office traffic.

Combined protection is:

```text
per-IP ASP.NET limiter
+ per-credential database failure window/lockout
```

This handles nonexistent usernames through the IP limiter and existing usernames across distributed IPs through the credential counter. Do not add Redis/distributed rate-limit infrastructure for the current single-instance SQLite application. If SEC04 later approves multiple instances, the in-memory IP limiter's per-instance residual risk must be reconsidered; the database credential lock remains shared.

ASP.NET Core provides partitioned/named rate-limiting middleware suitable for an endpoint-specific policy: [Microsoft ASP.NET Core rate limiting](https://learn.microsoft.com/en-us/aspnet/core/performance/rate-limit?view=aspnetcore-10.0). OWASP recommends login throttling and account-associated lockout while warning against permanent lockout denial of service: [OWASP Authentication Cheat Sheet](https://cheatsheetseries.owasp.org/cheatsheets/Authentication_Cheat_Sheet.html).

SQLite serializes writes; a short transaction/atomic update is sufficient at the current scale. Failed-attempt updates do not need a generic distributed lock, pessimistic-lock framework, background job, or second concurrency convention.

## 19. Antiforgery / Login CSRF

Local Login requires antiforgery protection even though the caller is not authenticated. Without it, an attacker could submit the attacker's credentials cross-site and cause the victim browser to receive the attacker's application Cookie (login CSRF/session swapping), after which the victim may write sensitive data into the attacker's account context.

Recommended design:

1. Make the existing `GET /api/antiforgery/token` explicitly `[AllowAnonymous]` so the Login Gate can obtain a synchronizer token/cookie pair.
2. Keep the token only in Pinia memory; do not put it in browser storage.
3. Extend the existing antiforgery middleware path check to include `POST /auth/local/login`.
4. Submit the token in the existing `X-CSRF-TOKEN` header through the shared API client.
5. After successful sign-in, fetch a fresh antiforgery token for authenticated unsafe APIs.
6. Preserve same-origin deployment, explicit Development CORS origins, and `credentials: include`; never use `AllowAnyOrigin` with credentials.
7. Keep `SameSite=Lax` as defense in depth, not as the only login-CSRF control.

The current API client needs one small body-capable root POST operation for `/auth/local/login`; `SecurityGate` must not call raw `fetch` directly.

Microsoft explains that Cookie authentication creates ambient browser credentials and antiforgery token/header validation protects cookie-authenticated state changes: [ASP.NET Core antiforgery guidance](https://learn.microsoft.com/en-us/aspnet/core/security/anti-request-forgery?view=aspnetcore-10.0).

Rate limiting does not replace antiforgery: rate limiting limits guesses; antiforgery proves the request originated from a page that obtained the same-origin token.

OIDC keeps its handler state/correlation/nonce flow. Do not apply the Local form token to the OIDC callback.

## 20. Bootstrap / Recovery

### First Local Administrator

Add a separate one-shot command:

```text
dotnet run -- bootstrap-local-admin --username <username> [--display-name <name>] [--user-id <id>]
```

Do not alter or overload the approved `bootstrap-admin` OIDC command. Separate commands keep OIDC Provider/Subject validation and Local secret handling unambiguous.

The Local command:

- runs migrations and exits without starting the web server;
- requires `Authentication:Local:Enabled = true`;
- creates a new canonical Administrator or binds an explicit active existing User;
- creates one active LocalLoginCredential in the same transaction;
- refuses duplicate username/User credential and ambiguous bindings;
- refuses ordinary bootstrap when a usable Administrator already exists across either auth method;
- never creates a default password;
- never writes a password to appsettings or seed data.

### Password input

Preferred:

1. interactive hidden console prompt with confirmation;
2. explicit `--password-stdin` for controlled non-interactive operator automation.

Do not accept `--password <plaintext>` as the recommended path because it can appear in shell history and process listings. Do not print input or hash. Environment-variable input is not preferred because environment capture/process diagnostics can expose it.

### Recovery

Keep existing OIDC bootstrap recovery. For a local-only deployment whose sole Administrator forgets the password, provide a later explicit one-shot operator command:

```text
dotnet run -- reset-local-admin-password --username <username>
```

It uses the same hidden/stdin secret input, operates only on an existing active canonical Administrator credential, increments SessionVersion, marks `MustChangePassword` according to the recovery policy, and exits. It must not require editing SQLite or copying a hash. Filesystem/database access to run the command is the operational trust boundary.

Production never seeds fixed credentials. Integration tests may seed test-only credentials in their isolated database.

## 21. Database Schema Proposal

AUTH-A01 creates no migration. The conceptual additive schema for AUTH-B01 is:

```text
local_login_credentials
────────────────────────────────────────────────────────────
id                            INTEGER PK
user_id                       INTEGER NOT NULL FK users(id) RESTRICT
username                      TEXT NOT NULL
normalized_username           TEXT NOT NULL
password_hash                 TEXT NOT NULL
is_active                     INTEGER NOT NULL DEFAULT 1 CHECK (0,1)
failed_login_attempts          INTEGER NOT NULL DEFAULT 0 CHECK (>= 0)
failed_login_window_started_at TEXT NULL
locked_until                  TEXT NULL
session_version               INTEGER NOT NULL DEFAULT 1 CHECK (>= 1)
created_at                    TEXT NOT NULL
updated_at                    TEXT NOT NULL
last_password_changed_at      TEXT NOT NULL
version                       INTEGER NOT NULL DEFAULT 1 CHECK (>= 1)

UNIQUE(user_id)
UNIQUE(normalized_username)
INDEX(is_active, normalized_username) only if query-plan evidence shows value;
the unique normalized index already serves login lookup.
```

`must_change_password INTEGER NOT NULL DEFAULT 0 CHECK (0,1)` is added in AUTH-B03 before Administrator create/reset is enabled. It may be included in the initial B01 table if B01 contract is frozen to the complete MVP, but no unrelated generic credential fields are added.

`password_hash` remains SQLite TEXT with no short database length limit; application writes only PasswordHasher output. User delete is not a business capability and the FK is RESTRICT.

Migration compatibility:

- additive only;
- no deletion/rename of `login_identities`;
- no modification or inference of existing LoginIdentity rows;
- no User model rebuild except any SQLite mechanics required by an approved additive constraint;
- no local credential backfill from Email, EmployeeNo, DisplayName, OIDC Subject, or existing users;
- no default credential row;
- existing OIDC Users gain no Local login until explicit bootstrap/Admin action.

The standardized OIDC session version can use existing `LoginIdentity.Version`; no new LoginIdentity table is required. If the migration adds a claim-support column later, it must default safely and preserve all rows, but this plan does not require one.

## 22. API Proposal

### Anonymous authentication surface

| Method | Route | Result / notes |
|---|---|---|
| `GET` | `/api/auth/options` | Enabled methods and OIDC friendly label only; `AllowAnonymous`, no-store. |
| `GET` | `/api/antiforgery/token` | Existing route amended to allow anonymous login bootstrap; same token route reused after authentication. |
| `POST` | `/auth/local/login` | Username/password body, antiforgery required, rate-limited, `204` on success. |
| `GET` | `/auth/login?returnUrl=...` | Existing OIDC challenge, retained when OIDC enabled. |

### Shared session surface

| Method | Route | Notes |
|---|---|---|
| `GET` | `/api/current-user` | Existing canonical projection; later additive password-change-required state only when forced-change flow is introduced. |
| `POST` | `/auth/logout` | Existing authenticated, antiforgery-protected application Cookie logout for both methods. |
| `PUT` | `/api/current-user/password` | Self change; current/new password; B03. |

### Administrator credential surface

| Method | Route | Notes |
|---|---|---|
| `GET` | `/api/users/{id}/local-credential` | Metadata only; never PasswordHash. |
| `POST` | `/api/users/{id}/local-credential` | Create one credential with username and temporary password; B04 after forced-change support. |
| `PUT` | `/api/users/{id}/local-credential/active-state` | Active state + opaque credential concurrency token. |
| `POST` | `/api/users/{id}/local-credential/reset-password` | Temporary password + token; increments SessionVersion and sets MustChangePassword. |
| `PUT` | `/api/users/{id}/local-credential/username` | Optional B04 correction operation; uniqueness, token, session invalidation. |

Credential read response may contain:

```text
id, userId, username, isActive, isTemporarilyLocked,
lockedUntil?, mustChangePassword, lastPasswordChangedAt,
createdAt, updatedAt, concurrencyToken
```

It never contains PasswordHash, password policy internals, failed-password values, or secrets.

Password values appear only in HTTPS JSON request bodies. They never appear in query strings, URLs, routes, logs, Error Contract details, model-state echoes, OpenAPI examples, or responses. If OpenAPI is later present, password fields use `format: password`; AUTH-B01 must not introduce Swagger solely for this.

All authenticated/admin endpoints continue using current `ApiErrorResponse`, Access policies, antiforgery, safe IDs, and opaque concurrency. Do not create a second Error envelope or version convention.

## 23. Frontend Proposal

Reuse the current frontend architecture:

```text
SecurityGate / Login form
→ authenticationApi
→ shared apiClient
→ auth/options + antiforgery + local login
→ actorStore.loadCurrentUser
→ existing AppShell / Router Guard / action gates
```

Changes planned, not implemented here:

- `SecurityGate.vue` loads Authentication Options and renders Local/OIDC/Both states.
- `authenticationApi.ts` gains typed options and Local Login calls through the shared client.
- `actorStore` remains the only Current User store; do not create `authStore` containing a duplicate profile.
- After Local login, actorStore follows its existing Current User and antiforgery bootstrap.
- Existing Router Guard, Sidebar, Viewer/Editor/Administrator action gating, User Management protection, and TopBar profile remain.
- TopBar logout stays one action. The UI does not need to expose auth method in the first release.
- User Management later adds `LocalCredentialManagementPanel.vue` beside `LoginIdentityManagementPanel.vue`; no generic Credential component framework.
- Forced password change later uses a focused security gate and never renders the business shell until the backend permits normal access.

OIDC button label comes only from options response. Local labels remain simple Chinese: “账号”, “密码”, “登录”.

### Logout decision

The current `POST /auth/logout` clears only the application Cookie. Keep that behavior for both methods in the first coexistence release.

- Local logout needs only application sign-out.
- Automatic OIDC provider logout would require a browser redirect/end-session flow and provider-specific behavior; the current fetch-based `204` endpoint cannot safely imply it.
- Application logout may be followed by immediate OIDC SSO on the next enterprise login; the UI should describe it as “退出系统”, not “退出企业账号”.
- If federated logout becomes required, use the already justified `auth_method` descriptor to select OIDC sign-out only for OIDC sessions in a separate amendment. Local users must never enter a nonexistent provider logout flow.

No first-phase “switch authentication method” or account-switch control. Logout, then select a method.

## 24. Threat Model

| Threat | Mitigation | Residual risk |
|---|---|---|
| Password brute force | Adaptive PasswordHasher, per-credential 5/15-minute temporary lock, per-IP rate limit, strong minimum length | Distributed low-and-slow attacks; multi-instance IP limits need later review |
| Credential stuffing | Rate limit, lockout, generic error, no email auto-login semantics, future breached-password review | Reused valid passwords can still succeed; MFA is not in MVP |
| Account enumeration | Same public `invalid_credentials` for nonexistent/wrong/inactive/locked; dummy-hash verification; no UI distinction | Network/database timing cannot be made mathematically identical; logging/monitoring required |
| Login CSRF/session swapping | Anonymous synchronizer token, header validation, same-origin, restricted CORS, fresh sign-in ticket | Same-origin XSS can obtain token and submit; XSS controls remain necessary |
| Session fixation/account switch | Reject login while authenticated; fresh protected Cookie on SignIn; no caller-supplied UserId | Compromised browser can act within its valid session |
| Password leakage in logs/errors | Body-only secret, no model-state echo, structured safe logging, no hash/secret response, no CLI argument | Reverse proxy/body logging must be checked in SEC04 |
| Insecure transport | Production HTTPS, Secure Cookie, SEC04 real HTTPS gate | Local Development HTTP is not Production-safe; proxy topology remains blocked |
| Stale credential session | Auth method/id/version checked every request; active-state and SessionVersion | Database outage must fail closed; stolen valid cookie works until invalidation/expiry |
| Administrator reset abuse | Administrator policy, antiforgery, concurrency, MustChangePassword, security event, Local-only session invalidation | A compromised Administrator can reset credentials; no four-eyes approval in scope |
| Default credentials | No seed/default password; explicit secure bootstrap prompt | Operator can still choose a weak policy-compliant passphrase |
| Database theft | Adaptive salted hashes, no plaintext/reversible secret, access-controlled backups | Offline guessing remains possible; hashes do not make a stolen database safe |
| Credential-disable denial of service | Admin-only operation, last-usable-admin invariant, concurrency | Malicious Administrator remains highly privileged by design |
| Lockout denial of service | Temporary fixed lock, no indefinite extension, existing sessions unaffected | Attacker can temporarily block new login for a known username |
| Old session revival after re-enable | SessionVersion/identity version mismatch survives re-enable | None beyond deliberate new login |
| Password reset affecting OIDC | Credential-level version, not User-wide SecurityVersion | User compromise through another still-active method remains possible by design |
| Unicode username confusion | Server normalization, normalized unique index, Administrator creation, exact display | Homoglyphs remain possible and require admin care |

Structured logs should record event type, timestamp, outcome/reason category, safe UserId/credential ID when known, and network correlation/IP under the deployment privacy policy. Avoid logging raw password, hash, Cookie, antiforgery token, OIDC token, or full request body. Username should be omitted or irreversibly reduced where UserId/credential ID is available; failed unknown usernames need only a bounded/hash correlation if operationally necessary.

Initial events: local login success/failure/rate-limit/lock, credential enabled/disabled, password self-change, Administrator reset, bootstrap/recovery. This is ordinary structured logging, not a generic Audit Framework or SIEM project.

## 25. Migration / Compatibility

AUTH-B01 is additive:

- add `local_login_credentials` only;
- optionally add the later `must_change_password` column in B03;
- preserve `users`, `login_identities`, Evidence, HumanConfirmation snapshots, KnowledgeRole mappings, KnowledgeDocuments, and all current data;
- do not convert existing OIDC identities into Local credentials;
- do not infer Username or Password from profile data;
- do not delete or rename current OIDC schema/routes;
- one existing OIDC User may later receive a Local credential without creating another User.

Claim compatibility intentionally requires reauthentication when the standardized descriptor first deploys. An ambiguous legacy Cookie ticket is not persisted business data. OIDC mappings and canonical users are unchanged.

No password migration/backfill exists. Test fixtures may create explicit test hashes through PasswordHasher in isolated SQLite; production migration never seeds a credential.

If SQLite migration generation rebuilds a table unexpectedly, inspect SQL and prove all User/LoginIdentity rows, FKs, unique indexes, versions, KnowledgeDocument authorship, and Evidence references remain. The expected Local table creation itself should not require rebuilding existing tables.

## 26. Existing SEC Impact

### SEC-A01

Still valid:

- canonical User boundary;
- Cookie session;
- OIDC exact mapping;
- principal-backed Current User;
- AccessLevel model and policy matrix;
- KnowledgeRole separation;
- antiforgery/same-origin model;
- no browser selector/header trust.

Amended:

- its “first phase production does not support local password” decision is superseded by this approved product requirement;
- OIDC is no longer the only allowed deployment authentication method;
- production startup validation becomes enabled-method-aware.

### SEC-01

Remains valid for OIDC mapping, Cookie settings, User/AccessLevel, current-user trust, bootstrap-admin, and forged-header protection. Amendment changes the ticket descriptor and makes CurrentUserContext source-aware. OIDC itself is not removed.

### SEC-02

The entire Viewer/Editor/Administrator endpoint matrix remains valid. The “usable Administrator” calculation expands from active LoginIdentity only to any active OIDC or Local method. KnowledgeRole remains unrelated.

### SEC-03

Current actorStore, Router Guard, backend-authoritative gating, shared client, TopBar, logout, and authenticated unsafe antiforgery remain valid. Login Gate and authentication bootstrap expand to options + local form, and the antiforgery token route becomes available before login.

### SEC-04

Remains:

```text
SEC-04 BLOCKED
```

Do not resume it during AUTH-A01. Its eventual real deployment verification must add:

- Local login over real HTTPS;
- anonymous login antiforgery/login-CSRF rejection;
- per-IP rate limiter and per-credential lockout;
- generic enumeration-resistant responses/timing path;
- credential disable and re-enable old-session rejection;
- self change and Administrator reset session invalidation;
- Local/OIDC session isolation for the same User;
- local-only, OIDC-only, and both-enabled configuration behavior;
- bootstrap/recovery operations and no-default-credential audit;
- proxy logging verification that password bodies are not captured.

Real OIDC callback/proxy/Data Protection blockers remain for any deployment that enables OIDC. Local-only Production still requires real HTTPS, proxy/cookie, Data Protection, secret-input, logging, backup, and rate-limit verification before Production approval.

KC-B01 remains valid and untouched. AUTH-A01 does not modify KnowledgeDocument/content architecture. XML documentation rollout remains paused.

## 27. Implementation Slices

Each slice must be separately approved, implemented, verified, reported, cleaned up, and stopped.

### AUTH-B01 — Local Credential Foundation + Login API

- Add LocalLoginCredential table/domain/configuration and additive migration.
- Use PasswordHasher with explicit options and rehash support.
- Add Local enabled configuration, fail-closed mode validation, authentication options endpoint, anonymous antiforgery bootstrap, Local login route, IP limiter, lockout, dummy-hash path, and generic errors.
- Add source-aware principal descriptor and CurrentUserContext validation for OIDC and Local.
- Add secure `bootstrap-local-admin` with interactive/stdin secret input.
- Preserve current Cookie, OIDC, AccessLevel, Current User, authorization, and OIDC bootstrap.
- Focused API/SQLite tests; no Vue Login form.

### AUTH-B02 — Login UI + Authentication Options

- Update SecurityGate to Local/OIDC/Both layouts.
- Add typed options/local-login API through shared client.
- Bootstrap anonymous antiforgery token, submit safely, then use existing Current User/antiforgery initialization.
- Add loading, generic failure, 429, show/hide, Enter, autocomplete, password-manager, and no-browser-storage behavior.
- Focused browser path: local form → Cookie → Current User → Viewer/Editor/Admin shell.

### AUTH-B03 — Self Password Change + Forced Change

- Add MustChangePassword schema/state and password-change use case.
- Add password-change-required backend authorization gate and frontend recovery view.
- Reissue current Local Cookie after self change; invalidate all other Local sessions.
- Confirm OIDC sessions are unaffected.

### AUTH-B04 — Credential Administration + Reset / Rename

- Add Administrator read/create/active-state/reset and optional username correction routes.
- Add User Management Local credential panel.
- Enforce one-per-User/username uniqueness, temporary credential + MustChangePassword, concurrency, SessionVersion, and unified last-usable-Administrator invariant.
- Add operator recovery command.

### AUTH-B05 — Local + OIDC Security Verification

- Run focused migration, hashing/rehash, enumeration, lockout, rate-limit, CSRF, session fixation/revocation, dual-method, authorization, frontend, and real SQLite/browser tests.
- Extend the future SEC04 deployment checklist but do not claim Production approval without the real environment.
- Stop all ASP.NET Core/Vite/browser/test/watch processes and release verification ports.

Do not combine these into an Identity platform rewrite.

## 28. Minimum Gate Before KC-B02

The minimum safe development/product-recovery gate is:

```text
AUTH-B01 PASS
+ AUTH-B02 PASS
```

After both pass, a developer/operator can:

```text
bootstrap-local-admin securely
→ open Login Gate
→ submit username/password with antiforgery
→ receive the existing protected Cookie
→ load canonical Current User
→ exercise real AccessLevel authorization
→ continue KC-B02 product work
```

AUTH-B03/B04 may follow without blocking KC-B02 because this minimum gate exposes only the securely bootstrapped Administrator credential whose owner chose the secret directly. Do not release Administrator-created credentials or reset workflows to general users until AUTH-B03 forced-change and AUTH-B04 management both pass.

This gate does not approve Production. AUTH-B05 and the still-blocked SEC04 real deployment controls remain required.

## 29. Explicitly Not Implemented

AUTH-A01 creates only this design review. It does not implement or authorize:

- any C#, Vue, router, Login Gate, Program.cs, Cookie, OIDC, CurrentUserContext, AccessLevel, database, migration, configuration, test, or package change;
- full ASP.NET Core Identity, IdentityUser, UserManager, SignInManager, Identity roles/claims/tokens/UI, or Identity database tables;
- a generic AuthenticationIdentity/Credential/Secret framework;
- JWT bearer SPA auth, access-token storage, refresh tokens, API keys, OAuth resource server, or browser token storage;
- removal or renaming of LoginIdentity, OIDC routes, OIDC bootstrap, or existing Cookie;
- default usernames/passwords, anonymous registration, password in appsettings, migration seed, startup seed, or command-line password argument;
- Email/EmployeeNo/DisplayName/KnowledgeRole-based username or identity linking;
- browser User Selector, impersonation, `X-Current-User-Id`, development auth backdoor, or production Test Authentication;
- MFA, passkeys, passwordless, email/SMS reset, forgot-password, recovery questions, password expiration/history, Remember Me, account switching, or federated logout;
- permanent lockout, CAPTCHA, Redis/distributed limiter, SIEM, generic audit framework, or approval workflow;
- per-method AccessLevel or LocalCredential Role; AccessLevel remains on canonical User;
- KnowledgeRole permission semantics;
- physical credential deletion;
- SEC04 resumption or Production Security Rollout approval;
- KnowledgeDocument/KC-B01/KC-B02 changes;
- XML documentation continuation;
- AUTH-B01 or any implementation slice.

## 30. Architecture Decision Table

| Decision | Recommendation | Reason |
|---|---|---|
| Local Login | YES | Required for real local/dev/small deployment sessions without an IdP. |
| OIDC | YES | Remains preferred enterprise SSO path and coexists cleanly. |
| Canonical User | Unchanged | Profile, AccessLevel, KnowledgeRole, Current User, actor, and authorship need one trusted business person. |
| Full ASP.NET Identity | NO | IdentityUser/store/UI/token schema is disproportionate and duplicates canonical User lifecycle. |
| Credential model | `LocalLoginCredential` | Concrete, one-purpose model; avoids generic secret framework. |
| LoginIdentity rename/merge | NO | Existing OIDC semantics are clear; renaming creates churn without behavior gain. |
| Cardinality | 0..1 Local credential and 0..N OIDC identities per User | Meets current need and preserves enterprise identity flexibility. |
| Username | Independent, trimmed, Unicode, case-insensitive normalized unique | Does not bind mutable profile metadata; allows Chinese/custom accounts. |
| Password storage | `PasswordHasher<LocalLoginCredential>` Identity V3 with explicit benchmarked work factor | Framework format/salt/rehash without full Identity or custom crypto. |
| Password policy | 15–128, Unicode/whitespace, no trim/normalization/composition checklist | Strong passphrases, bounded KDF work, modern policy. |
| Cookie | Same existing ApplicationCookie | Frontend, Current User, authorization, Evidence actors, and KnowledgeDocuments stay method-agnostic. |
| Principal descriptor | AuthMethod + AuthIdentityId + AuthVersion + UserId | Enables source-specific validation and revocation. |
| Authentication method claim | YES | Required now for request-time credential lookup and possible logout UX. |
| User SecurityVersion | NO | Would over-invalidate OIDC on Local password reset. |
| Local SessionVersion | YES | Reset/disable/rename invalidates Local sessions only. |
| Request-time validation | Method credential active/version + User active + latest AccessLevel | Preserves SEC01/02 immediate revocation semantics. |
| Lockout | 5 failures/15-minute window, 15-minute temporary lock | Per-account brute-force control without permanent DoS. |
| Rate limit | Endpoint-specific per-IP ASP.NET limiter plus DB credential counter | Covers nonexistent and distributed-account attempts without new infrastructure. |
| Enumeration | Generic 401 and dummy-hash path | Avoids exposing existence/state through messages and major timing differences. |
| Login antiforgery | Anonymous token + existing header validation on Local login | Prevents login CSRF/session swapping; rate limiting is separate. |
| Login response | 204 then canonical Current User bootstrap | Avoids duplicate profile projection. |
| Auth options | Public server-controlled endpoint | One SPA artifact safely represents Local/OIDC/Both deployment. |
| Default Development | Local enabled, OIDC optional, no default account | Restores development access without weakening authentication. |
| Production modes | Local-only, OIDC-only, or both, explicitly configured | Supports small and enterprise deployments. |
| Both methods disabled | Fail application startup outside Testing | Never degrade to anonymous access. |
| Bootstrap | Separate `bootstrap-local-admin`, hidden/stdin password input | Keeps OIDC command intact and secrets out of arguments/history. |
| Self password change | YES | Fundamental daily Local Login capability. |
| Administrator reset | YES | Only practical recovery without email/SMS infrastructure. |
| MustChangePassword | YES before general admin create/reset release | Prevents Administrator-known temporary password from remaining permanent. |
| Reset invalidation | Increment Local SessionVersion only | Invalidates Local tickets and preserves valid OIDC sessions for same User. |
| Credential delete | NO; disable | Keeps stable revocation/session identity and history. |
| Username change | Not in B01/B02; explicit admin operation later if needed | Avoids early complexity; never delete/recreate silently. |
| LastLoginAt | Deferred | Not required for auth and avoids login write churn; logs cover first phase. |
| Logout | Clear application Cookie for both methods | Fits current endpoint; federated sign-out is provider-specific future scope. |
| Minimum KC-B02 gate | AUTH-B01 + AUTH-B02 | Provides a secure real local browser session without waiting for full admin lifecycle. |

## 31. Recommended Next Task

After this document passes the Authentication Architecture Gate, start exactly one task:

```text
AUTH-B01 — Local Credential Foundation + Login API
```

AUTH-B01 must freeze the LocalLoginCredential schema and password policy, implement the additive migration, secure bootstrap, PasswordHasher, lockout/rate-limit/antiforgery login path, standardized Cookie descriptor, source-aware CurrentUserContext, authentication-options endpoint, focused OIDC regressions, and its verification report. It must not modify the Vue Login Gate, implement general credential administration, start KC-B02, resume SEC04, or continue AUTH-B02 automatically.

---

**Architecture Gate status:** Awaiting human review. No implementation is authorized by this document alone.
