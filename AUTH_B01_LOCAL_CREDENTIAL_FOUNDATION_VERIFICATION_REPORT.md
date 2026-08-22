# AUTH-B01 — Local Credential Foundation + Login API Verification Report

## Result

AUTH-B01 PASS

## Architecture Conformance

- Added the minimal `LocalLoginCredential` authentication method; `User` remains the sole canonical business person and still owns `AccessLevel` and `KnowledgeRole` assignments.
- Did not add ASP.NET Core Identity stores, `IdentityUser`, `UserManager`, `SignInManager`, a second User model, JWT, a second Cookie, or a generic credential framework.
- OIDC `LoginIdentity` is retained. Local and OIDC both issue `ApplicationCookie` (`SystemKnowledgeHub.Auth`) and use the same request-time Current User and authorization path.
- No Vue Login Gate, SecurityGate, frontend feature page, AUTH-B02, KC-B02, password change/reset, forced change, or credential-management API was implemented.

## Schema and Migration

- Migration: `20260822091626_AddLocalLoginCredentialFoundation`.
- Added `local_login_credentials` with `id`, `user_id`, `username`, `normalized_username`, `password_hash`, `is_active`, failed-attempt/window/lock fields, `session_version`, timestamps, and app-managed `version`.
- Enforces `UNIQUE(user_id)`, `UNIQUE(normalized_username)`, `user_id → users(id) RESTRICT`, active/version/session-version checks, and non-negative failed attempts.
- The migration is additive only. It does not modify or backfill `users` or `login_identities`; existing Users receive no Local credential.
- `dotnet ef migrations has-pending-model-changes ...` reported no pending model changes. Existing OIDC, Users, Evidence, HumanConfirmation, Relationships, Systems, and KnowledgeDocument migration coverage passed.

## Password and Username Security

- Uses `PasswordHasher<LocalLoginCredential>` in Identity V3 mode with an explicit PBKDF2 iteration count of **220,000**.
- Focused test-host benchmark: one successful verification took approximately **90 ms** (Windows/.NET 8 test host). This is a development verification measurement, not a Production capacity benchmark.
- Passwords are accepted only in request body/stdin, are 15–128 characters, and are never trimmed, normalized, case-folded, returned, logged, or stored in plaintext. Successful `SuccessRehashNeeded` verification updates only the hash and metadata, never `SessionVersion`.
- Username normalization is server-only: trim → Unicode Form KC → `ToUpperInvariant()`. It accepts Unicode letters/numbers (including Chinese) and `.`, `_`, `-`, `@`; it rejects internal whitespace/control characters. Email and EmployeeNo are not identity-linking inputs.

## Login Flow, CSRF, Lockout, and Rate Limit

```text
anonymous antiforgery token
→ POST /auth/local/login
→ PasswordHasher verification / dummy hash for missing user
→ application Cookie
→ source-aware Current User
→ latest AccessLevel
```

- `GET /api/auth/options` is anonymous, `Cache-Control: no-store`, and exposes only enabled-method flags plus the optional OIDC display label.
- `GET /api/antiforgery/token` is anonymous; existing authenticated antiforgery behavior remains intact.
- `POST /auth/local/login` requires `X-CSRF-TOKEN`, is limited to 20 attempts/IP/5 minutes by default, and returns `429 too_many_requests` through `ApiErrorResponse` when rejected.
- Unknown user, wrong password, inactive credential/User, and lockout return the same `401 invalid_credentials` response. Missing identities take a process-held compatible dummy-hash verification path.
- Lockout defaults to 5 failed attempts in 15 minutes, locking new attempts for 15 minutes. Locked attempts do not extend the lock. Failed attempts never change `SessionVersion`, so they cannot invalidate existing sessions.
- Disabled Local Login returns `404` for its route, including before antiforgery validation.

## Session Descriptor and Current User

- Both methods now create the internal protected-ticket descriptor: `auth_method`, `auth_identity_id`, `auth_version`, `user_id`, `access_level`.
- OIDC validates active `LoginIdentity`, its current `Version`, User mapping, active User, and latest `AccessLevel`.
- Local validates active `LocalLoginCredential`, matching `SessionVersion`, User mapping, active User, and latest `AccessLevel`.
- Unknown/legacy descriptors fail closed as `session_expired`; old pre-AUTH-B01 Cookie tickets therefore require reauthentication.
- Cookie `access_level` is only a projection. `CurrentUserContext` reads the current canonical User value on every request, so downgrade and User deactivation take effect immediately.
- Local credential disable/version mismatch uses `session_expired`; OIDC inactive-identity behavior remains `identity_inactive`.

## Bootstrap

- Added one-shot `dotnet run -- bootstrap-local-admin --username <username> [--display-name <name>] [--user-id <id>] [--password-stdin]`.
- Interactive mode reads hidden password plus confirmation. Automation uses `--password-stdin`; `--password` is rejected.
- The command migrates, starts no web server, creates/binds one active Administrator and one active Local credential transactionally, and refuses duplicate credentials/usernames or an existing usable Administrator across OIDC or Local methods.
- No default account, password, hash, migration seed, or appsettings secret was created.

## Verification

| Command | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors. |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build` | PASS — 90 passed, 0 failed, 0 skipped. |
| focused Local authentication / migration / OIDC / authorization regression filter | PASS — 34 passed, 0 failed. |
| `dotnet test ... --filter "FullyQualifiedName~LocalLoginApiTests"` | PASS — 8 passed, 0 failed. |
| focused password security benchmark test | PASS — 1 passed; 220,000-iteration verification ≈ 90 ms. |
| `dotnet ef migrations has-pending-model-changes ... --no-build` | PASS — no pending model changes. |

Focused coverage includes Local Login success and generic failures, anonymous/missing/invalid antiforgery behavior, disabled route behavior, options for Local/OIDC/Both, IP rate limiting, lockout no-extension, dummy-hash path, Unicode/password whitespace semantics, unique constraints, Local session revocation/re-enable resistance, Viewer/Editor/Administrator policy entry, OIDC Current User regression, forged-header regression, AccessLevel freshness, and bootstrap stdin/duplicate/no-plaintext behavior.

## Runtime Verification

Executed against a temporary SQLite database using the real command and HTTP stack:

```text
bootstrap-local-admin --password-stdin
→ start Local-only ASP.NET Core app
→ GET anonymous antiforgery token
→ POST /auth/local/login
→ ApplicationCookie
→ GET /api/current-user
→ Administrator API
```

The API child process was stopped after verification and no listener remained on verification port 5099. The temporary verification database was outside the repository and no secret/hash was added to project files.

## Verification Process Cleanup Audit

- Rechecked the previously observed `dotnet` parent PIDs `16224`, `29148`, `29296`, and `33516` with elevated read-only process inspection. All four had exited before the audit completed, so no unsupported ownership inference or termination was required.
- No `SystemKnowledgeHub.Api` process remains.
- Verification port `5099` has no listener.
- No AUTH-B01 test server, watcher, bootstrap process, or ASP.NET Core child process remains from this task.
- Existing build, full test, migration-model, and runtime verification results remain valid; no production code or test was modified during this gate-only audit.

## Dirty Worktree and Diff Review

The repository was already heavily dirty before AUTH-B01, including existing Security, Users, KnowledgeDocument, migration, report, API, frontend, and test changes. Those files were preserved; no reset, clean, revert, broad formatting, or KnowledgeDocument overwrite was performed.

AUTH-B01 changes are confined to Local credential/security/authentication configuration, source-aware Current User handling, bootstrapping, the additive migration, focused tests, and this report. It did not alter canonical User/AccessLevel/KnowledgeRole data shapes, Evidence behavior, KnowledgeDocument behavior, frontend behavior, or the OIDC mapping model.

## Explicitly Not Implemented

- Vue Local Login form / Login Gate changes / AUTH-B02.
- Self password change, reset, `MustChangePassword`, forced-change flow, credential administration UI/API, username rename, or federated logout.
- Full ASP.NET Core Identity, JWT, browser token storage, MFA, recovery flows, KC-B02, or SEC-04 continuation.
