# SEC-01 — OIDC Authentication Foundation + Canonical User Binding Verification Report

## Result

SEC-01 PASS

## Implemented

- Added canonical `LoginIdentity` (`Provider + Subject → User`) without changing `User` into an identity-account model.
- Added `User.AccessLevel` with the closed `Viewer` / `Editor` / `Administrator` vocabulary.
- Added OIDC configuration, OIDC challenge endpoint (`GET /auth/login`), secure application Cookie configuration, middleware ordering, and JSON API redirect handling.
- OIDC callback resolves only exact configured `Provider` plus exact OIDC `sub`; it does not use email, employee number, or display name for automatic binding or provisioning.
- OIDC callback projects server-created `login_identity_id`, `user_id`, and `access_level` claims. Tokens are not persisted and `SaveTokens` is disabled.
- Replaced production `ICurrentUserContext` resolution from `X-Current-User-Id` with authenticated principal claims plus request-time database validation of LoginIdentity, canonical User, active state, and latest AccessLevel.
- `GET /api/current-user` now returns the authenticated canonical profile and `accessLevel`.
- HumanConfirmation retains U04 transaction/snapshot logic; its current user now comes from `ICurrentUserContext`. A forged `X-Current-User-Id` cannot alter the saved provider User ID.
- Added one-shot `bootstrap-admin` mode. It migrates the database and exits without calling `app.Run()`; it accepts only the configured provider, preserves the supplied Subject verbatim, creates/binds one active canonical administrator in a transaction, and creates no password.
- Added test authentication support to `WebApplicationFactory`. It emits controlled principal claims and does not use `X-Current-User-Id` as its identity source.

## Schema / Migration

- Migration: `20260822025403_AddOidcAuthenticationFoundation`.
- `users.access_level`: `TEXT NOT NULL DEFAULT 'Viewer'` with `Viewer|Editor|Administrator` CHECK constraint. Existing users receive `Viewer`; no Administrator is inferred or seeded by the migration.
- `login_identities`: safe-integer PK, `user_id` RESTRICT FK, exact `provider` and `subject`, active flag, UTC timestamps, and app-managed version.
- Constraints/indexes: unique `(provider, subject)` and index `user_id`.
- SQLite generated migration script was inspected. Adding the User CHECK triggers a rebuild that copies `id`, `access_level`, all existing User fields, and `version`, then recreates the existing `(is_active, display_name)` index.
- Existing U04 Evidence User FK/snapshot schema is unchanged. `HumanConfirmationSnapshotMigrationTests` remains in the focused regression run.

## Authentication Flow

```text
OIDC challenge
→ OIDC callback (`Provider` + exact `sub`)
→ LoginIdentity
→ active canonical User
→ HttpOnly application Cookie
→ principal claims
→ request-time LoginIdentity/User/AccessLevel validation
→ ICurrentUserContext
```

Cookie configuration is HttpOnly, `SameSite=Lax`, eight-hour sliding expiry, and Secure outside Development. Production startup requires OIDC Provider/Authority/ClientId plus a persistent Data Protection key path; no secret is stored in source configuration.

## Current User Migration

- Production backend no longer reads `X-Current-User-Id` in `CurrentUserContext` or uses it for HumanConfirmation.
- `/api/current-user` returns JSON `401 unauthenticated` for no principal and JSON `403` security errors for unmapped, inactive identity, or inactive user.
- The frontend contract now includes `accessLevel`; legacy selector/header code remains temporarily for SEC-03 compatibility but cannot alter the backend principal.
- Focused test: authenticated User A plus forged `X-Current-User-Id = User B` saves the HumanConfirmation snapshot with User A.

## Error Contract

All security failures use `ApiErrorResponse`:

| Condition | HTTP | Code | `details.authStatus` |
| --- | ---: | --- | --- |
| No principal | 401 | `unauthenticated` | `missing` |
| Rejected/invalid ticket | 401 | `session_expired` | `expired` |
| Mapping missing | 403 | `identity_unmapped` | `unmapped` |
| LoginIdentity inactive | 403 | `identity_inactive` | `identity_inactive` |
| User inactive | 403 | `account_inactive` | `inactive` |

Cookie redirect events return this JSON envelope for `/api/**`; OIDC login/challenge remains a normal protocol redirect.

## Bootstrap Admin

Command:

```text
dotnet run -- bootstrap-admin --provider <configured-provider-key> --subject <exact-oidc-subject> --display-name <name> [--employee-no <value>] [--email <value>] [--user-id <safe-id>]
```

- Success creates a canonical active User, active LoginIdentity, and `Administrator` AccessLevel in one transaction.
- Existing active Administrator, provider outside allowlist, duplicate mapping, invalid input, or ambiguous existing-user input returns a nonzero exit code.
- No default password, local credential, token persistence, or web-server listener is created.

## Tests

```text
dotnet test tests\SystemKnowledgeHub.Api.Tests\SystemKnowledgeHub.Api.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~BootstrapAdministratorCommandTests|FullyQualifiedName~LoginIdentityPersistenceTests|FullyQualifiedName~CurrentUserApiTests|FullyQualifiedName~EvidenceApiTests|FullyQualifiedName~HumanConfirmationSnapshotMigrationTests|FullyQualifiedName~KnowledgeStatusApiTests|FullyQualifiedName~RelationshipsApiTests|FullyQualifiedName~UsersApiTests"
```

Result: **23 passed, 0 failed**.

Coverage includes LoginIdentity uniqueness/RESTRICT FK, a real U04-to-SEC-01 SQLite upgrade preserving historical User ID with default `Viewer`, principal/current-user projection, unmapped identity, inactive LoginIdentity, inactive User, exact bootstrap behavior, no automatic identity binding, U01–U04 regressions, and the forged-header HumanConfirmation regression.

## Build

| Command | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors. |
| `dotnet ef migrations has-pending-model-changes --no-build ...` | PASS — no pending model changes. |
| `npm run type-check` | PASS. |
| `npm run build` | PASS — current `dist/index.html` generated successfully. |
| Scoped ESLint for changed frontend contract/spec | PASS — exited cleanly with no diagnostic output. |

## Real OIDC Provider Verification

- Framework/configuration implementation: complete.
- Automated application mapping verification: complete through the test authentication handler and real SQLite integration tests.
- Real enterprise IdP authority, callback, client registration, certificate/reverse-proxy, and deployment key-ring verification: **Not yet externally verified; deferred to SEC-04.**

## CSRF Boundary

Cookie authentication is established, but antiforgery issuance/validation is not enabled in this slice. It must be completed with the SEC-03/SEC-04 same-origin frontend rollout before any production enablement. SEC-01 alone is not production rollout approval.

## Explicitly Not Implemented

- SEC-02 Viewer/Editor/Administrator controller matrix, User Management enforcement, last-Administrator management invariant, or dynamic permissions.
- SEC-03 login gate, router guard, sidebar/action gating, production actor selector removal, and final logout UX.
- SEC-04 real IdP closed loop, reverse-proxy/Data Protection deployment verification, and antiforgery rollout.
- ASP.NET Core Identity, local passwords, JWT bearer SPA authentication, JIT provisioning, token vault, RBAC tables, ACL/ABAC, and audit framework.

## Deviations

None from SEC-A01. The deliberately deferred CSRF and full authorization/frontend rollouts are recorded above, not implemented prematurely.

## Process Cleanup

- No ASP.NET Core, Vite, mock OIDC, browser, or CLI verification server was left running.
- The temporary SQLite files used by bootstrap command tests were deleted by the test fixture.
- Frontend verification Node processes exited; ports 5090, 5173, and 5174 have no listener from this verification.
