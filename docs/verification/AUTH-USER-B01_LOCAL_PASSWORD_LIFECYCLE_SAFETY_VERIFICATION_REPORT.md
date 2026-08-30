# AUTH-USER-B01 Local Password Lifecycle Safety Verification Report

> Closeout: the two baseline assertions and repository-isolation gate were closed by
> `AUTH-USER-B01-FIX-R01`. AUTH-USER-B01 is now PASS; see
> `docs/reports/AUTH_USER_B01_FIX_R01_DELIVERY_CLOSEOUT_VERIFICATION_REPORT.md`.

## Result

**Overall: PARTIAL / NOT DELIVERED**

The AUTH-USER-B01 implementation and its focused backend, frontend, migration, concurrency, audit, and browser checks are complete. The repository-wide delivery gate is not green because two pre-existing frontend copy assertions still fail and the repository-owned SQLite file changed during the task while a pre-existing development runtime was present. No success commit was created and no push was attempted. AUTH-USER-B02 was not implemented and is not declared ready.

## Scope and authority

- Task: `AUTH-USER-B01 — Local Password Lifecycle Safety Foundation`
- Approved source: `docs/design/AUTH_USER_A01_LOGIN_CREDENTIAL_PASSWORD_LIFECYCLE_ARCHITECTURE_DECISION.md`
- Scope boundary: Local self/forced password change, method-scoped session safety, usable-Administrator resolution, audit, and frontend gate only.
- Explicitly excluded: AUTH-USER-B02 administrator credential-management UI/API and adjacent product work.

## Implemented state

- Added persisted `LocalLoginCredential.MustChangePassword` with a SQLite migration whose existing-row default is `false` and whose model configuration constrains the value to `0/1`.
- Extended current-user projection with `authenticationMethod` and `mustChangePassword`.
- Revalidates every authenticated request against the enabled Local/OIDC method, current approved OIDC Provider, active identity/credential and User, and method-scoped session version. Invalid sessions are signed out and return structured `session_expired` reasons.
- Enforces the forced-password whitelist: current user, antiforgery token, password change, and logout remain available; all business/administration endpoints, including endpoints carrying a bare `[Authorize]`, return `403 must_change_password`.
- Added `PUT /api/current-user/password` for Local sessions only. Validation preserves exact input, requires 8–128 characters, verifies the current password, and rejects reuse of the same password.
- A successful change atomically replaces the hash, updates password timestamps, clears `MustChangePassword`, failed-attempt/window/lock state, increments `SessionVersion` and row `Version`, updates `UpdatedAt`, commits, and clears the current cookie without issuing a replacement.
- All older Local sessions for the credential are invalidated; OIDC sessions and provider-owned passwords are unaffected.
- Centralized usable-Administrator resolution respects enabled authentication methods and the current OIDC Provider. A locked or forced-change Local administrator still counts when the credential is active.
- Added structured safe-field audit events for accepted and rejected self-change attempts. Captured logs contained actor User ID, Local credential ID, authentication method, result, reason, correlation ID, IP address, user agent, and timestamp; password values were absent.
- Added a full-shell-free forced-change UI, Local-only profile password dialog, OIDC provider-owned password guidance, exact-input client validation, mismatch/confirmation handling, and responsive styles.

## Verification evidence

| Check | Result | Evidence |
| --- | --- | --- |
| Release solution build | Passed | `dotnet build SystemKnowledgeHub.sln -c Release --no-restore`: 0 warnings, 0 errors |
| Full backend suite | Passed | `dotnet test ... -c Release --no-build --no-restore` with a temporary single-thread xUnit runner configuration: 219/219 passed; temporary runner file removed |
| Focused AUTH-USER-B01 backend | Passed | Password lifecycle, method disablement, usable-Administrator, and migration classes: 9/9 passed |
| Focused AUTH-USER-B01 frontend | Passed | Password form, app gate, and top-bar suites: 10/10 passed |
| Frontend type check | Passed | `npm run type-check` |
| Frontend production build | Passed | `npm run build`; only the repository's existing large-chunk warning was emitted |
| Full frontend suite | **Failed** | 411/413 passed; two unchanged baseline copy assertions failed, listed below |
| Isolated browser check | Passed for the inspected scope | Temporary Local administrator and SQLite/Data Protection/attachment paths on ports 5188/5199; forced gate shown after login, business shell/navigation count `0`, mismatch error shown with submit disabled, 375 px viewport had no horizontal overflow, browser console errors `0` |
| Password mutation and old-session invalidation | Passed through API integration | Atomic state assertions, old/current cookie rejection, old-password rejection, new-password login, OIDC rejection, and concurrent single-winner behavior covered by backend tests; browser did not submit a final password mutation |

### Unchanged frontend baseline failures

1. `RevisionCompareView.spec.ts` expects `按 Attachment ID + Kind 比较，不比较二进制内容`, while the committed component contains the Simplified Chinese wording `按附件编号与类型比较，不比较二进制内容`.
2. `LoginIdentityManagementPanel.copy.spec.ts` expects `技术对象名：LoginIdentity`, while the committed component does not contain that copy.

Neither failing component nor its assertion was changed by AUTH-USER-B01. They were not corrected opportunistically because they are outside this task's approved scope. Until their owning task resolves them and the full suite passes, this report cannot claim a full verification PASS.

## Persistence and runtime safety

- The isolated browser database passed `PRAGMA integrity_check` (`ok`) and `foreign_key_check` (0 violations), then the temporary database, Data Protection keys, attachment directory, runtime logs, browser tab, and agent-started services were removed. Ports 5188 and 5199 were released.
- Repository database baseline: 950,272 bytes, `2026-08-29T11:50:30.0126773Z`.
- Repository database after verification: 950,272 bytes, `2026-08-29T23:38:02.4552113Z`.
- Read-only inspection found `integrity_check=ok`, no AUTH-USER-B01 migration in `__EFMigrationsHistory`, and no `must_change_password` column. The latest observed row timestamps were on `local_login_credentials` at `23:35:29Z` and `systems` at `23:37:49Z`, consistent with activity from a development runtime rather than application of this task's migration, but attribution cannot be proven after the fact.
- The first post-runtime check did not list repository sidecars. After the subsequent SQLite `Mode=ReadOnly` inspection, `system-knowledge-hub.db-shm` (32,768 bytes, `2026-08-30T00:31:36.5692896Z`) and an empty `system-knowledge-hub.db-wal` (`2026-08-30T00:30:37.1871106Z`) were present. Their creation may have been triggered by the SQLite inspection or a concurrent runtime; attribution cannot be proven. They were treated as protected user state and were not deleted or altered.
- Because the repository-owned database timestamp changed while a pre-existing development executable had also locked Debug output, and because repository WAL/SHM sidecars appeared during the audit, the repository-protection gate is recorded as unresolved. The database and its sidecars were not rolled back, overwritten, migrated, checkpointed, or deleted.

## Delivery status and next gate

- Branch: `main`.
- Commit: none.
- Push: not attempted.
- Required before AUTH-USER-B01 delivery: resolve the two owning frontend baseline assertions, establish an unchanged repository-database baseline with the pre-existing runtime stopped or isolated, rerun full backend/frontend/build/browser gates, then create the task-specific commit and push.
- AUTH-USER-B02 readiness: **NO** until AUTH-USER-B01 receives a full green verification and delivery record.
