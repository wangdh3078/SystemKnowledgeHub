# AUTH-USER-B03 Existing User Login Method Management Verification Report

## Result

**AUTH-USER-B03: PASS**

**AUTH-USER-B04 READY: YES**

Existing Users can now be given an explicit Local credential after creation, and Administrators can enable or disable that credential with its own concurrency token. Existing-user OIDC mapping management is aligned with the frozen Provider/Subject rules. User state and login-method state remain independent. AUTH-USER-B04 password-reset and username-rename capabilities were not implemented.

## Implemented Boundary

- `POST /api/users/{id}/local-credential` creates the target User's first and only Local credential.
- `PUT /api/users/{id}/local-credential/active-state` enables or disables that credential.
- Existing `POST /api/users/{userId}/login-identities` and `PUT /api/users/{userId}/login-identities/{id}/active-state` flows now enforce the frozen approved-Provider presentation and security-audit behavior.
- All write endpoints remain Administrator-only and use the existing antiforgery boundary.
- No User deletion, generic credential CRUD, administrator password reset, username rename, or AUTH-USER-B04 capability was added.

No EF migration was required. The B01 schema already contains the Local credential security fields, per-User and normalized-username uniqueness constraints, LoginIdentity versioning, and required indexes.

## Existing User Local Credential Creation

- The target User must exist and may be Active or Inactive.
- The target must not already have a `LocalLoginCredential`.
- Username validation, normalization, and uniqueness reuse `LocalCredentialSecurity`; password validation and hashing reuse `LocalPasswordService` and the existing ASP.NET password hasher.
- The new credential is initialized with `IsActive=true`, `MustChangePassword=true`, `SessionVersion=1`, and `Version=1`. Failed-attempt, lock-window, and lock-until state are cleared, and the existing UTC security timestamps are initialized.
- Database uniqueness remains the final concurrency authority. Two concurrent creation requests for the same User produce exactly one success and one safe conflict.
- The response projection contains only safe UI metadata and an opaque credential concurrency token. It does not expose `PasswordHash` or `SessionVersion`.
- `confirmPassword` exists only in the browser form and is not part of the API request contract.

## Local Credential Active State

- Enable/disable uses the Local credential's own opaque version token; a User token is not accepted as a substitute.
- Each state change increments credential `Version` and `SessionVersion`. Disabling therefore invalidates existing Local sessions without disabling the User or modifying OIDC mappings.
- Enabling the credential does not bypass the independent User-active, global-Local-enabled, password-change, lockout, or other authentication gates.
- A stale credential token returns a safe `409 conflict` identifying `LocalLoginCredential` as the concurrency resource.
- Disabling the last usable Administrator login method is rejected. The guard evaluates usable Local and OIDC methods independently, so an enabled OIDC method can preserve Administrator access while Local is disabled.

## OIDC Existing User Alignment

- Existing Users can receive an explicit OIDC mapping only for the server-approved Provider.
- Subject/sub is explicit, non-empty, length-bounded, and never inferred from email, employee number, display name, or username.
- A configured Provider may be pre-provisioned while global OIDC authentication is disabled. No mapping can be added when the server has no approved Provider.
- OIDC and Local can coexist for the same User. Adding, enabling, or disabling one method does not overwrite the other.
- Provider, Subject/sub, mapping-active state, and global-enabled state are the only identity security metadata shown in the User Drawer.

## User Management UX

- The existing User Drawer keeps `03 登录方式` as the management surface.
- A User with no login method displays `该用户当前无法登录系统。` and offers `添加本地账号` when Local setup is available.
- The Local card displays username, credential enabled/stopped state, first-login password-change requirement, most recent password-change time, global Local availability, and only the currently valid enable/disable action.
- The OIDC card displays safe Provider/Subject/sub information, mapping state, global availability, and explains that personal attributes are never used for automatic binding.
- When the server has no approved Provider, the Drawer explains why a new enterprise-login mapping cannot be added instead of presenting an unusable action.
- User state and login-method state are explicitly described as independent.

## Authorization, Concurrency, and Audit

- Administrator requests with valid antiforgery tokens are accepted; Editor and Viewer requests are rejected with `403`.
- Missing antiforgery tokens are rejected without mutation.
- Local creation and Local active-state changes emit structured `LocalCredentialCreated`, `LocalCredentialEnabled`, or `LocalCredentialDisabled` security events, including actor, target, result, reason, timestamp, correlation ID, and safe credential ID where applicable.
- Existing-user OIDC create and active-state changes emit corresponding structured `LoginIdentityCreated`, `LoginIdentityEnabled`, and `LoginIdentityDisabled` events.
- Dedicated captured-log and response checks found no initial password, password hash, or session version disclosure.

## Verification

| Gate | Result | Evidence |
| --- | --- | --- |
| Release solution build | PASS | 0 warnings, 0 errors |
| Focused AUTH-USER-B03 backend | PASS | 8/8 integration tests |
| Affected authentication regression | PASS | 31/31 tests covering B03, B02 create setup, Local login, method disablement, usable-Administrator, and LoginIdentity persistence |
| Full backend regression | PASS | 253/253, 0 failed, 0 skipped; final serialized run used the repository-approved `REV-GAP-011` runner workaround and removed its output-only configuration afterward |
| Focused frontend | PASS | 2 files, 6/6 tests; final deterministic fork-pool run |
| Frontend type check | PASS | `npm run type-check` after the final frontend changes |
| Frontend production build | PASS | `npm run build`; only the pre-existing large-chunk advisory was emitted |
| Affected frontend lint | PASS | Direct ESLint run against the affected User API, Drawer, LoginIdentity panel, and tests; exit code 0 |
| Git whitespace gate | PASS | `git diff --check` |

The focused backend coverage verifies:

- existing Active and Inactive User Local-credential creation;
- exact initial-password authentication and the `MustChangePassword` gate;
- normalized duplicate username conflict and concurrent same-User creation;
- Administrator-only and antiforgery enforcement;
- Local-session invalidation on disable, restoration after enable, and stale credential-token conflict;
- inactive User and globally disabled Local authentication remaining authoritative;
- last usable Administrator protection and Local/OIDC coexistence;
- approved OIDC Provider, explicit Subject, and no attribute inference;
- structured audit completeness and password/hash/session-version redaction.

A full frontend suite was not required for this focused slice under the repository's risk-based verification rule. The affected component tests, final type check, production build, and affected lint all passed.

## Browser and Runtime Verification

The runtime used Development mode with a task-owned SQLite database, task-owned Data Protection keys, task-owned attachment storage, and isolated API/Vite ports 5488/5499.

The browser flow passed:

1. Authenticate as the task-owned bootstrap Administrator.
2. Create an Active User with no login method.
3. Open the existing User Drawer and observe `该用户当前无法登录系统。`.
4. Add a Local credential using client-only confirmation-password validation.
5. Observe username, enabled state, first-login change requirement, password-change time, and global Local-enabled state.
6. Disable the Local credential and observe the card switch to stopped state with only `启用` available.
7. Re-enable it and observe the card return to enabled state with only `停用` available.
8. Observe the OIDC unavailable explanation when no approved Provider is configured.

All UI/API writes in this flow targeted only the disposable task database.

## Repository Data Safety

The protected repository database was measured before and after all verification and remained byte-for-byte unchanged:

| Protected file | Start and end state |
| --- | --- |
| `system-knowledge-hub.db` | 950,272 bytes; mtime `2026-08-30T02:50:06.2271348Z`; SHA-256 `2D15792C057F6F051A823A80107856977A4F1784CA17A440219B0B6DD0DB5B91` |
| `system-knowledge-hub.db-wal` | Absent at start and end |
| `system-knowledge-hub.db-shm` | Absent at start and end |

No B03 runtime connected to the repository-owned database, and no migration, write, checkpoint, WAL, or SHM change affected it.

## Cleanup

- Agent-created browser tab closed.
- Task-owned API and Vite processes stopped; ports 5488/5499 released.
- Task database, WAL/SHM, Data Protection key, attachment path, logs path, and isolated runtime root removed.
- The task-owned runtime files were scanned before deletion; neither generated verification password nor the strings `PasswordHash` or `SessionVersion` were present in those artifacts.
- Temporary test-runner configuration removed.

## Existing/New Gaps

- No Blocker or High gap remains for AUTH-USER-B03.
- The existing frontend large-chunk advisory is unchanged and unrelated to this slice.
- Administrator password reset and username rename remain intentionally assigned to AUTH-USER-B04; they are not B03 delivery gaps.

## B04 Readiness

**AUTH-USER-B04 READY: YES**

AUTH-USER-B04 was not started.
