# AUTH-B02 — Login UI + Authentication Options Verification Report

## Result

AUTH-B02 PASS

## Scope

- `SecurityGate.vue`：根据服务端 Authentication Options 显示 Local-only、OIDC-only、Both 或无可用方式的 Login Gate；Local form 使用组件内存状态。
- `authenticationApi.ts`：增加 typed `getAuthenticationOptions`、`localLogin` 与现有 OIDC navigation helper。
- shared `apiClient`：增加带 JSON body、credentials 和 antiforgery header 的 root `POST` no-content 支持，供 `/auth/local/login` 复用。
- `actorStore`：复用现有 antiforgery token state，使匿名 Login Gate 也可在提交前取得 token；Current User、AccessLevel 和 profile 仍只由 `/api/current-user` 加载。
- Login Gate CSS、ApiError code union 与 focused Vitest tests。

## UI Modes

真实 browser verification 已检查三种 server configuration：

| Mode | Auth Options | Rendered result |
| --- | --- | --- |
| Local-only | `localLoginEnabled=true`, `oidcLoginEnabled=false` | 显示账号、密码（Element Plus show/hide password）与登录按钮；不显示企业按钮或“或”。 |
| Both | 两项均为 `true`，display name 为 `企业测试登录` | 同时显示 Local form、“或”和企业登录按钮。未发起 OIDC callback。 |
| OIDC-only | `localLoginEnabled=false`, `oidcLoginEnabled=true` | 仅显示 `企业测试登录` 按钮；不显示 Local form。 |

当 options 加载失败时，Login Gate 显示“无法加载登录配置，请重试或联系管理员。”并提供重试；两种方式均为 false 时显示不可登录提示且不渲染 AppShell。

## Local Login Flow

真实 Local-only browser flow passed:

```text
/dashboard
→ Login Gate
→ GET /api/auth/options
→ GET /api/antiforgery/token
→ POST /auth/local/login
→ ApplicationCookie
→ GET /api/current-user
→ Dashboard / AppShell
```

使用仓库外的临时 SQLite database 和一次性 Local Administrator 验证。成功后 Dashboard 正常显示，TopBar 显示 canonical User，Administrator 的“用户管理”导航入口可见。密码在成功后立即从组件状态清除，未存入 Pinia、localStorage、sessionStorage 或 URL。

Logout 后 Login Gate 重新加载 Authentication Options；刷新页面后仍保持未登录状态，确认 Cookie 已清除。

## Error UX

- `invalid_credentials`：显示统一安全提示“用户名或密码错误，或当前账号暂不可用。”
- `too_many_requests`：显示“登录尝试过于频繁，请稍后再试。”
- `antiforgery_failed`：刷新 token，显示“登录安全令牌已失效，请重新提交。”，不自动重发密码请求。
- `already_authenticated`：重新调用 `actorStore.loadCurrentUser()`；成功则进入 AppShell。
- 网络错误与未知错误分别显示非凭据类的通用重试提示。

## Security Review

- Local Login 通过 shared `apiClient` 发送，保留 `credentials: include` 与 `X-CSRF-TOKEN`。
- 密码仅存在于 SecurityGate component memory；production source search 未发现任何 password persistence。
- `X-Current-User-Id`、selected user 和 `currentUserId` 没有重新引入。
- `AccessLevel` 仍只从 Current User projection 取得；Local Login response 不投影 profile 或 permission。
- `KnowledgeRole` 与 AccessLevel 语义未修改。

全仓搜索发现的 `sessionStorage` 仅为既有 Search recent-query feature；不属于认证状态或本批次变更。

## Tests and Build

| Command | Result |
| --- | --- |
| `npm run type-check` | PASS |
| `npm run test -- --run src/api/client/apiClient.spec.ts src/app/security/authenticationApi.spec.ts src/app/security/SecurityGate.spec.ts src/app/stores/actor.spec.ts` | PASS — 4 files, 14 tests |
| final `npm run test -- --run src/app/security/SecurityGate.spec.ts` | PASS — 7 tests, including final antiforgery regression |
| `npm run build` | PASS — existing Vite large-chunk advisory only |
| scoped `npx eslint` over AUTH-B02 files | PASS — no AUTH-B02 lint errors or warnings |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors |
| `dotnet test ... --filter "FullyQualifiedName~LocalLoginApiTests|FullyQualifiedName~CurrentUserApiTests|FullyQualifiedName~AntiforgeryApiTests|FullyQualifiedName~AccessControlApiTests"` | PASS — 13 tests |

The broad ESLint command still reports the SEC-03 baseline errors in `CreateIntegrationDialog.vue` and `unknownItemContracts.ts`; they were not changed. A first component-test run also encountered a Vitest worker startup timeout while importing full Element Plus; it was corrected by using test-only semantic stubs, and the final focused test commands passed without worker errors.

## Browser Verification and Process Cleanup

- Local-only real browser login, Current User bootstrap, Dashboard, TopBar and Administrator navigation: PASS.
- Both-mode real browser rendering: PASS.
- OIDC-only real browser rendering: PASS; no external OIDC sign-in was attempted.
- Temporary API and Vite process trees were stopped by recorded PID/parent ownership only.
- Verification ports `5099` and `5173` have no listener; no task API, Vite, watcher, test server or browser tab remains.

## Dirty Worktree and Diff Review

At task start, the only dirty worktree content was DOC-STRUCTURE-B01 documentation cleanup (`README.md`, `docs/PROJECT_FILE_MAP.md`, report/design moves and its untracked report). It was preserved without modification.

AUTH-B02 changes are limited to Login UI, authentication API/client integration, antiforgery bootstrap reuse, typed error contract, styles and focused frontend tests. No C# production code, authentication backend behavior, authorization matrix, API route/DTO behavior, database schema, migration, KnowledgeDocument behavior, AppShell, Sidebar, Router or User Management behavior was changed.

## Explicitly Not Implemented

- AUTH-B03 / AUTH-B04 / AUTH-B05.
- Password change/reset, MustChangePassword, forced-change flow, credential management, username rename, MFA, forgot password, Remember Me or federated logout.
- KC-B02, SEC-04 continuation and XML Documentation rollout.
