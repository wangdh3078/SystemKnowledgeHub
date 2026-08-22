# SEC-04 — Security Rollout Verification Report

## Result

```text
SEC-04 BLOCKED
```

Code-level verification passed. Real OIDC rollout verification remains blocked by missing deployment environment and configuration; this is not a code-level `FAIL`.

## Environment

| Item | Actual verification environment |
|---|---|
| OS | Windows local development workspace |
| ASP.NET Core environment | `Testing` for integration tests; no deployed Production process was started |
| Frontend | Local Vite production build and Vitest; no browser/OIDC smoke test |
| Database | SQLite in-memory integration-test database |
| HTTPS / reverse proxy | Not supplied or exercised |
| OIDC provider / callback registration | Not supplied or exercised |
| Deployment topology | Not supplied; multiple-instance applicability is unknown |

No server, Vite process, browser automation, proxy, or mock IdP was started by SEC-04.

## Static Security Audit

### OIDC, identity, and Current User

- `Authentication:Oidc:Provider`, `Authority`, `ClientId`, `ClientSecret`, `CallbackPath`, and scopes are configuration-bound. `appsettings.json` contains empty OIDC values and no credential value.
- A non-Development/non-Testing startup rejects absent Provider, Authority, ClientId, or `DataProtection:KeyPath`; it does not silently start with the development fallback values.
- OIDC uses authorization-code flow, the standard ASP.NET Core OIDC handler, and `SaveTokens = false`. The handler provides standard state, nonce, and correlation handling; no custom callback validator exists.
- Login return targets must start with one local `/`, and reject `//` and `/\\`; external absolute URLs cannot become a redirect target.
- Authentication mapping is exact `Provider + Subject -> LoginIdentity -> canonical User`. It does not link by email, display name, employee number, or name, and does not JIT-create a User.
- `CurrentUserContext` validates the principal-backed LoginIdentity/User relationship and both Active states on every resolution, then reprojects the current AccessLevel. Cookie claims are not trusted as long-lived authorization state.
- Production runtime has zero trusted `X-Current-User-Id` usages. The only matches are deliberate spoofing assertions in `CurrentUserApiTests` and `EvidenceApiTests`.
- `KnowledgeRole` is not used by authorization policies, controller policy attributes, router guards, sidebar permission gating, or write controls. `AccessLevel` remains `Viewer < Editor < Administrator`.

### Cookie, API errors, antiforgery, and logout

- Cookie name: `SystemKnowledgeHub.Auth`; `HttpOnly = true`, `SameSite = Lax`, eight-hour sliding expiration, and `Secure = Always` outside Development (`SameAsRequest` only in Development).
- API authentication/authorization failures are JSON `ApiErrorResponse` values: unauthenticated/session-expired are 401; unmapped, inactive identity, inactive account, and access denied are 403. API cookie redirects are converted to JSON rather than HTML/302.
- Safe requests do not require an antiforgery token. Unsafe `/api/**` and `/auth/logout` requests pass through `IAntiforgery.ValidateRequestAsync`; failure is 403 `ApiErrorResponse` with `code = antiforgery_failed`.
- The frontend obtains an antiforgery request token after Current User bootstrap, holds it only in Pinia memory, sends it only for unsafe methods, and sends credentialed same-origin requests. Logout is a POST through that same token flow and clears frontend Current User state on success.
- A real browser cookie-invalidating logout/reload/back-button test could not run without real OIDC and HTTPS.

### Production configuration, proxy, and Data Protection

- Data Protection has a configured application name and supports a configured filesystem key path. The checked-in production-neutral configuration leaves the path empty; Production startup fails closed if it remains empty.
- No Data Protection key-at-rest encryption configuration was found. Persistence and encryption-at-rest are separate deployment responsibilities.
- No `UseForwardedHeaders`, `ForwardedHeadersOptions`, `KnownProxies`, or `KnownNetworks` configuration was found. A proxy boundary must not be guessed or widened to trust arbitrary forwarded headers.
- HTTPS redirection is enabled outside Development. HSTS is not configured in the application; an external proxy may own it, but no such responsibility or configuration was available for verification.
- Development-only CORS explicitly permits only the two Vite origins with credentials. Production does not enable that policy. There is no `AllowAnyOrigin` plus credentials combination.
- Test authentication and ephemeral Data Protection are confined to `tests/.../BootstrapWebApplicationFactory` under the `Testing` environment; Production `Program` does not register that scheme.

### Frontend and secrets source audit

- No frontend identity selector, `selectedUserId`, `currentUserId` identity storage, bearer token storage, authorization-header token flow, or legacy Current User header was found.
- The only browser storage match is `sessionStorage` for recent search queries/visits; it contains no authentication identity or token.
- No committed credential, signing key, password, client secret value, or local-password implementation was found in application/test source. `ClientSecret` appears only as a configuration property assignment; test/example values are non-secret fixtures.
- Authentication/access code does not log cookies, raw tokens, authorization headers, or ClientSecret. Production log transport/retention was not available to inspect.

## Identity Integrity

```text
OIDC authenticated Principal
  -> exact LoginIdentity (Provider + Subject)
  -> canonical User
  -> CurrentUserContext
  -> current AccessLevel authorization
```

The focused regression confirms that an authenticated User A remains the returned Current User and the HumanConfirmation snapshot actor when a request supplies a forged `X-Current-User-Id` for User B.

## Access Matrix

| Capability | Viewer | Editor | Administrator | Evidence |
|---|---:|---:|---:|---|
| Authenticated reads / Current User | Allow | Allow | Allow | fallback Viewer policy; focused backend tests |
| Knowledge writes and status workflow | Deny | Allow | Allow | Editor controller policies; focused backend tests |
| Evidence and HumanConfirmation | Deny | Allow | Allow | Editor policy and Evidence regression |
| User Management | Deny | Deny | Allow | Administrator controller policy and regression |
| KnowledgeRole Management | Deny | Deny | Allow | Administrator controller policy and regression |
| LoginIdentity Management | Deny | Deny | Allow | Administrator controller policy and regression |
| AccessLevel mutation | Deny | Deny | Allow | standalone Administrator API and regression |

The frontend marks `/admin/users` as Administrator-only and routes authenticated Viewer/Editor direct navigation to `/forbidden`; backend authorization remains the authoritative direct-API protection.

## Administrator Safety and Recovery

- New Users default to `Viewer`; ordinary profile create/update requests do not carry AccessLevel.
- AccessLevel changes are an independent Administrator-only operation with opaque concurrency tokens. Current, stale, and malformed tokens are regression-tested (success, 409, and 400 respectively).
- User deactivation, Administrator downgrade, and LoginIdentity deactivation preserve at least one usable Administrator: active User + Administrator AccessLevel + active LoginIdentity. The service applies these checks inside database transactions and uses version concurrency tokens.
- Existing focused tests cover two-admin downgrade, final usable-admin downgrade/deactivation rejection, final LoginIdentity deactivation rejection, and immediate AccessLevel freshness after downgrade. No separate concurrent two-request load rehearsal was run; the transaction plus version-token implementation was statically reviewed.
- `bootstrap-admin` is an explicit one-shot command, not startup behavior. It only permits the configured provider, requires exact operator-supplied subject data, refuses existing active administrators and existing mappings, and creates no password. It is the approved, operator-controlled recovery path for a mapping mistake.

## Antiforgery Verification

- Token acquisition: authenticated `GET /api/antiforgery/token`.
- Safe read: allowed without token.
- Missing token: authenticated unsafe write returns 403 `antiforgery_failed`.
- Invalid token: authenticated unsafe write returns 403 `antiforgery_failed`.
- Valid token: Administrator unsafe write reaches the endpoint and succeeds.
- Authorization remains independent: Viewer/anonymous behavior is covered by the access-control tests; a CSRF token cannot establish authentication or grant Editor access.

## Executed Verification

| Command | Result |
|---|---|
| `dotnet build SystemKnowledgeHub.sln --no-restore` | Passed: 0 warnings, 0 errors |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~OidcAuthenticationFoundationMigrationTests|FullyQualifiedName~BootstrapApiTests|FullyQualifiedName~BootstrapAdministratorCommandTests|FullyQualifiedName~LoginIdentityPersistenceTests|FullyQualifiedName~CurrentUserApiTests|FullyQualifiedName~AccessControlApiTests|FullyQualifiedName~UsersApiTests|FullyQualifiedName~EvidenceApiTests|FullyQualifiedName~HumanConfirmationSnapshotMigrationTests|FullyQualifiedName~AntiforgeryApiTests"` | Passed: 22; failed: 0; skipped: 0 |
| `npm run type-check` | Passed |
| `npm run build` | Passed; Vite emitted its existing chunk-size advisory only |
| `npm run test -- --run src/api/client/apiClient.spec.ts src/app/stores/actor.spec.ts src/app/stores/overlays.spec.ts` | Passed: 7; failed: 0 |
| `npm run test` | Baseline failure: 25 passed, 1 failed (`src/layouts/AppShell.spec.ts`, stale expectation for `关系与缺口`) |
| `npm run lint` | Baseline failures: 2 unrelated errors (`CreateIntegrationDialog.vue` unused `props`; `unknownItemContracts.ts` empty interface) |

## SEC-04 Scoped Change

The existing untracked `tests/SystemKnowledgeHub.Api.Tests/Api/AntiforgeryApiTests.cs` received one verification-only addition: an invalid `X-CSRF-TOKEN` is now asserted to return 403 `antiforgery_failed`. No authentication, authorization, controller, route, DTO, persistence, migration, or frontend production behavior changed.

## Pre-existing Dirty Worktree

Before SEC-04, the worktree already contained a large set of modified and untracked backend, frontend, tests, migrations, design documents, and earlier verification reports. This included the untracked security feature/test files and the source files audited here. SEC-04 did not revert, format, overwrite, or clean any unrelated content. `git diff --check` completed without whitespace errors (Git also reports a pre-existing inaccessible global-ignore path warning).

## Blockers

| Blocker | Why final verification cannot complete | Required next environment step | Verification once available |
|---|---|---|---|
| Real OIDC provider and registered callback | No Authority/ClientId/ClientSecret deployment configuration or callback registration was supplied | Approved provider, mapped identities for Viewer/Editor/Admin/unmapped cases, and secret-store injection | Browser login -> callback -> mapping -> Current User -> role matrix -> logout |
| Public HTTPS and reverse proxy | No proxy/ingress type, public host/path-base, or trusted proxy IP/network was supplied; code has no safe forwarded-header trust configuration to validate | Production/rehearsal proxy topology and explicitly trusted proxy/network configuration | Confirm public HTTPS callback/cookie behavior and that host/scheme reconstruction cannot be client-forged |
| Persistent Data Protection keys | No actual persistent key location, access policy, key-at-rest protection, or restart deployment exists | Approved persistent key store/volume with restricted access and key protection | Login, restart/redeploy, and verify the existing cookie decrypts according to the approved session policy |
| Real deployment configuration | No Production process/topology was available | Rehearsal deployment configuration and operational logging/rollback ownership | Repeat the complete closed-loop smoke suite; assess multiple instances if applicable |

## Production Configuration Checklist

| Item | Status | Note |
|---|---|---|
| OIDC Authority | BLOCKED | Configurable and fail-closed; no real value/provider verified |
| OIDC ClientId | BLOCKED | Configurable and fail-closed; no real registration verified |
| OIDC ClientSecret source | PASS (static) | Configurable; no committed value found |
| Callback URL | BLOCKED | No public host/proxy/callback registration |
| Cookie Secure / HttpOnly / SameSite | PASS (static) | Secure outside Development; real HTTPS redirect flow blocked |
| Forwarded Headers / trusted proxy | BLOCKED | No safe deployment configuration or topology |
| HTTPS / HSTS | BLOCKED | Redirect exists; public HTTPS/HSTS ownership unverified |
| Data Protection persistence / encryption | BLOCKED | Fail-closed path requirement exists; actual store and encryption unverified |
| Antiforgery | PASS (code and focused regression) | Valid, missing, invalid, and safe-read cases covered |
| CORS | PASS (static) | Explicit Development origins only; no wildcard-with-credentials |
| Production error handling | PASS (static) | JSON API security errors; real deployment exception handling unexercised |
| Production logging | BLOCKED | Sensitive fields absent in reviewed paths; transport/retention unverified |
| Bootstrap administrator procedure | PASS (code and focused regression) | Explicit operator command only |
| Rollback procedure | BLOCKED | Requires deployment owner and configuration/version inventory |

## Rollback, Recovery, and Data

- Roll back application/configuration to the previously approved deployment artifact after preserving the incident evidence and configuration version. Do **not** restore trust in `X-Current-User-Id` as a rollback mechanism.
- Correct an Administrator mapping through the explicit `bootstrap-admin` procedure only when no usable active Administrator remains; it is operator-controlled and must use the configured provider plus exact subject.
- SEC-04 introduced no migration or schema change. Database backup ownership, backup point, and migration rollback dependency must be assigned by the deployment owner before rehearsal.

## Follow-up

`Managed User AccessLevel` read projection and its per-user frontend selector remain a **non-blocking product/admin UX follow-up** from SEC-03. The backend Administrator-only AccessLevel mutation already exists and is protected; this task did not alter DTOs to expand that UI.

## Final Production Decision

```text
Production Security Rollout:
NOT APPROVED
```

SEC-04 must be resumed in a real HTTPS deployment/rehearsal with a registered OIDC callback, explicitly trusted reverse proxy configuration, and persistent protected Data Protection keys. Only then can the requested browser-to-OIDC-to-cookie restart closed loop be considered for `SEC-04 PASS`.
