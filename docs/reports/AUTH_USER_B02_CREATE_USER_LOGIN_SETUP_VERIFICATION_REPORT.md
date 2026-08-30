# AUTH-USER-B02 Create User with Login Setup Verification Report

## Result

**AUTH-USER-B02: PASS**

**AUTH-USER-B03 READY: YES**

The existing `POST /api/users` flow now requires one explicit `loginSetup.type` value (`local`, `oidc`, or `none`) and creates the User, KnowledgeRole assignments, and selected login method in one transaction. No AUTH-USER-B03/B04 capability was added.

## Create User Contract

- `POST /api/users` remains the only user-creation endpoint.
- `loginSetup.type = local` accepts `username` and one write-only `initialPassword`.
- `loginSetup.type = oidc` accepts `provider` and `subject`.
- `loginSetup.type = none` rejects credential/identity fields.
- Login setup is mandatory and is never inferred from empty/non-empty fields.
- The request cannot declare `AccessLevel`; every created User starts as `Viewer`.

## Local Login Setup

- Username validation and normalization reuse `LocalCredentialSecurity.TryNormalizeUsername`; normalized usernames remain globally unique.
- The existing `LocalPasswordService` and ASP.NET password hasher are reused. Password input is preserved exactly, with the existing 8–128 character rule.
- Created credentials are active and use `MustChangePassword=true`, `SessionVersion=1`, `Version=1`, zero failed attempts, no lock, and UTC password-change timestamps.
- The browser-confirmation password is used only for client-side equality validation and is never sent to the API.
- A newly created local user successfully authenticated with the initial password and was routed to the existing forced-password-change gate before application navigation was exposed.

## OIDC Login Setup

- Provider values must exactly match the server-approved Provider; arbitrary Provider values and duplicate Provider/Subject mappings are rejected.
- No email, employee number, or display-name auto-binding is performed.
- A configured Provider can be pre-provisioned while OIDC is globally disabled; the UI then displays `当前部署未启用企业统一登录`.
- When no approved Provider exists, OIDC setup is disabled in the UI and rejected by the API.
- OIDC creation writes an active, version-1 `LoginIdentity` and never creates a local credential.

## No-login User

- `loginSetup.type = none` creates only the User and requested KnowledgeRole assignments.
- No `LocalLoginCredential` or `LoginIdentity` row is created.
- Create and existing-user views display `该用户当前无法登录系统。` while preserving the User as a valid knowledge/ownership/reference participant.

## Transaction Atomicity

The service validates the complete request, begins one EF Core transaction, creates the User, creates KnowledgeRole assignments, creates the selected Local credential or OIDC identity, and commits only after every write succeeds. Focused integration tests inject Local-credential and OIDC-identity insert failures; both branches roll back the User and assignments completely.

## Authorization

- Create User with login setup: Administrator only.
- `GET /api/users/{id}/login-methods`: Administrator only.
- `GET /api/users/login-setup-options`: Administrator only.
- Direct Editor and Viewer API calls are rejected by the backend.
- Antiforgery remains mandatory; a rejected Local-create request produces no partial User.

## Security and Secret Safety

- `initialPassword` is accepted only on the write contract and is absent from responses, projections, errors, audit details, and runtime log output.
- `PasswordHash` and `SessionVersion` are not exposed by the login-method projection.
- Successful Local and OIDC creation emit `LocalCredentialCreated` and `LoginIdentityCreated` security events with actor user ID, target user ID, result, reason code, timestamp, correlation ID, and the applicable safe record ID.
- A dedicated logger-backed integration test confirms both audit events and confirms that neither the initial password nor `PasswordHash` appears in captured logs.

## Login Method Projection

`GET /api/users/{id}/login-methods` returns only UI-required safe metadata:

- Local existence, username, active state, must-change state, password-change time, lock time, and global-enabled state.
- OIDC Provider, Subject/sub, active state, and global-enabled state.

It does not return password hashes, session versions, or unrelated raw security state.

## Frontend UX

- The User Drawer is organized as `01 基础资料`, `02 知识身份`, and `03 登录方式`.
- Three mutually exclusive radio-card choices control mutually exclusive Local/OIDC/no-login fields.
- Local mode provides username, initial/confirmation password, fixed must-change state, visible mismatch feedback, and disabled submit while mismatched.
- OIDC mode uses the approved Provider as read-only UI state and displays the required disabled-deployment warning.
- The OIDC/SSO label now wraps inside an equal-width, equal-height option card. Real-browser computed geometry at the constrained Drawer width was 158 × 56 px for all three cards, with no horizontal or vertical label overflow.
- The account-menu `修改密码` and `退出登录` controls were regression-checked at identical x-position, width, height, and zero left margin; no additional implementation delta was required.
- User-visible copy remains Simplified Chinese except approved technical terms such as OIDC, SSO, Provider, and Subject/sub.

## Verification

| Gate | Result | Evidence |
| --- | --- | --- |
| Release solution build | PASS | `dotnet build SystemKnowledgeHub.sln -c Release --no-restore`: 0 warnings, 0 errors |
| Focused AUTH-USER-B02 backend | PASS | 10/10 tests passed, including Local/OIDC/none, validation, rollback, authorization, antiforgery, projection, and safe audit logging |
| Full backend suite | PASS | Final serialized run: 229/229 passed in 54 seconds |
| Focused frontend | PASS | User Drawer, LoginIdentity copy, and top-bar regression: 3/3 files, 10/10 tests |
| Full frontend suite | PASS | 69/69 files and 416/416 tests passed |
| Frontend type check | PASS | `npm run type-check` |
| Frontend production build | PASS | `npm run build`; only the existing large-chunk advisory was emitted |
| Affected frontend lint | PASS | Direct repository ESLint execution against all affected TypeScript/Vue files; exit code 0 |
| Git whitespace gate | PASS | `git diff --check` |

The first ordinary full-backend attempt did not complete because of the repository's existing parallel xUnit runner behavior; only the agent-owned testhost was stopped. The final run used a temporary output-only xUnit serialization configuration, passed 229/229, and the temporary configuration was removed. During the final CSS regression cycle, a newly added test-stub class assertion initially failed; the stub was corrected and both focused and full frontend suites then passed. Neither intermediate runner event represents a product failure or an undisclosed skipped gate.

## Browser Verification

All browser work used Development mode with a task-owned SQLite database, task-owned Data Protection keys, task-owned attachment storage, and isolated ports 5388/5399.

- Local: create → authenticate with exact initial password → forced MustChangePassword gate; business/navigation shell remained unavailable.
- OIDC: disabled-but-configured `TestOidc` Provider → mapping created and displayed with the required warning.
- None: create → existing-user details display `该用户当前无法登录系统。`.
- Layout: three login choices fit at the normal constrained Drawer width; at the mobile breakpoint the option cards stack and remain inside the Drawer.
- Button consistency: Change Password and Logout rendered with matching geometry.
- Browser console: clean post-fix tab reported 0 new errors and 0 new warnings.

## Repository Safety

Before the valid B02 baseline, an existing development process (PID 31376) held the repository database and sidecars. The task did not force-stop that unknown process. Verification remained paused until it exited, then a new baseline was recorded before any B02 runtime was started.

| Protected file | Start and end state |
| --- | --- |
| `system-knowledge-hub.db` | 950,272 bytes; mtime `2026-08-30T01:12:08.4424362Z`; SHA-256 `144A455510D9CA162041F3093B644E319A8291C390580B4D7EBA5883930698A1` |
| `system-knowledge-hub.db-wal` | Absent at start and end |
| `system-knowledge-hub.db-shm` | Absent at start and end |

The repository-owned database was never connected to during verification. Final filesystem metadata and SHA-256 comparison are identical to the valid baseline, so no task migration/write/checkpoint touched repository data and no WAL/SHM file was added, removed, or modified by the task.

## Cleanup

- Agent-created browser tabs closed.
- Task-owned API/Vite processes stopped; ports 5388/5399 released.
- Task-owned database, possible sidecars, Data Protection keys, attachment storage, logs, and root temporary directory removed.
- Temporary xUnit serialization files removed.
- Agent-used .NET build servers shut down.

## Existing/New Gaps

- No Blocker or High gap exists for AUTH-USER-B02.
- The existing Vite large-chunk advisory is unchanged and unrelated to this slice.
- Existing-user credential creation, credential enable/disable, administrator reset, username rename, and other AUTH-USER-B03/B04 work remain intentionally out of scope; they are not hidden B02 gaps.

## B03 Readiness

**AUTH-USER-B03 READY: YES**

AUTH-USER-B03 was not started.
