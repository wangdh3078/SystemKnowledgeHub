# SEC-02 — Backend Access Control Verification Report

## Result

SEC-02 PASS

## Policies

- Fallback policy: every endpoint without `AllowAnonymous` requires a current, authenticated, mapped, active canonical User at the Viewer minimum.
- `Editor`: uses the same request-time `ICurrentUserContext` resolution and permits `Editor` or `Administrator`.
- `Administrator`: uses the same resolution and permits only `Administrator`.
- The authorization handler never trusts the access-level claim as the source of truth: `CurrentUserContext` re-reads the LoginIdentity, User active state, and `User.AccessLevel` on every request.
- `/api/**` authorization failures are emitted as JSON `ApiErrorResponse`; `/auth/login` remains the sole `AllowAnonymous` endpoint. Logout remains protected by the fallback policy.

## Endpoint Matrix

| Access | Real routes / operations |
| --- | --- |
| Viewer minimum | `GET /api/dashboard`, `/api/search`, `/api/knowledge-targets`, `/api/systems`, `/api/business-functions`, `/api/database-objects`, `/api/database-columns`, `/api/business-rules/{id}`, `/api/integrations/{id}`, `/api/relationships/{id}`, `/api/evidence/{id}`, `/api/unknown-items`, `/api/current-user`, and the protected development diagnostic `GET /api/bootstrap/status`. |
| Editor or Administrator | All existing writes for Systems, Business Functions, Database Sources/Objects/Columns/known values, Business Rules, Integrations, Relationships, Evidence (including HumanConfirmation), KnowledgeStatus, and every UnknownItems workflow/KnowledgeUpdate apply action. |
| Administrator only | All `GET` and write operations at `/api/users` and `/api/knowledge-roles`; `PUT /api/users/{id}/access-level`; and `GET`/`POST`/active-state operations at `/api/users/{userId}/login-identities`. |

`KnowledgeRole` and `UserKnowledgeRole` are not read by a policy and never influence AccessLevel.

## User Management and AccessLevel

- Ordinary `POST /api/users` remains server-defaulted to `Viewer`; the request contract has no AccessLevel field.
- `PUT /api/users/{id}/access-level` is a separate Administrator-only operation using the existing User opaque `concurrencyToken`; it returns the new AccessLevel and the next token.
- Current token succeeds; invalid token returns validation `400`; stale token returns `409`.
- AccessLevel is resolved from the latest canonical User on every request. The focused matrix test lowers an Editor during an existing session and confirms the next write is `403`.

## LoginIdentity Management

Implemented Administrator-only operations:

- `GET /api/users/{userId}/login-identities`
- `POST /api/users/{userId}/login-identities`
- `PUT /api/users/{userId}/login-identities/{id}/active-state`

Mappings are explicit Provider + Subject values. There is no JIT provisioning, email binding, external-group mapping, bulk import, or claim-to-role mapping.

## Last Administrator Invariant

A usable Administrator is an active User with `AccessLevel.Administrator` and at least one active LoginIdentity. The invariant is enforced inside the same database transaction as:

- User deactivation;
- Administrator downgrade through the independent AccessLevel operation; and
- LoginIdentity deactivation.

The implementation rejects removal of the last usable Administrator with `422 business_rule_violation`. The focused test proves: a second Administrator can be downgraded; the final Administrator cannot be downgraded, deactivated, or have its sole active LoginIdentity disabled.

## Error Contract

- Anonymous business API requests return JSON `401 unauthenticated`, not a redirect or HTML.
- Insufficient Viewer/Editor access returns JSON `403 forbidden`.
- Existing SEC-01 `identity_unmapped`, `identity_inactive`, and `account_inactive` request-time outcomes remain distinct JSON `403` errors.
- HumanConfirmation remains principal-backed: Viewer receives `403`; Editor and Administrator are permitted. Existing forged `X-Current-User-Id` regression continues to save the authenticated User's Snapshot.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors. |
| `dotnet test tests\SystemKnowledgeHub.Api.Tests\SystemKnowledgeHub.Api.Tests.csproj --no-build --no-restore` | PASS — 74 passed, 0 failed, 0 skipped. |
| `npm run type-check` | PASS. |
| `npm run build` | PASS. Existing Vite bundle-size advisory only; no build failure. |
| Controller unsafe-route inventory | PASS — all business `POST`/`PUT` actions are explicitly Editor/Admin protected; `/auth/logout` remains authenticated through fallback. |

## Migration

No migration required. SEC-02 uses the existing `users.access_level`, `login_identities`, and app-managed versions introduced by SEC-01.

## Deviations

None from SEC-A01. `GET /api/bootstrap/status` is retained as an authenticated fallback-protected diagnostic rather than an anonymous production endpoint.

## Explicitly Not Implemented

- SEC-03 frontend login gate, router/sidebar/action UX, and production selector cleanup.
- CSRF/antiforgery rollout.
- SEC-04 real OIDC, reverse proxy, Data Protection deployment, and rollout verification.
- Dynamic permissions, RBAC tables, ACL, ABAC, department/organization scope, external group mapping, local passwords, impersonation, and audit framework.

## Production Readiness

**SEC-02 PASS ≠ Production Security Rollout PASS.** SEC-03 frontend access UX, antiforgery, and SEC-04 real OIDC/proxy/Data Protection rollout verification are still required before production enablement.

## Process Cleanup

All verification commands were one-shot. No ASP.NET Core, Vite, mock-auth, browser, or test process was left running.
