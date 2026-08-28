# PRODUCTION-DEPLOYMENT-CONFIGURATION-R01 Verification Report

## Result

`PRODUCTION-DEPLOYMENT-CONFIGURATION-R01 PASS`

The repository now has a fail-closed Production configuration overlay, explicit persistent-path validation for SQLite and Data Protection, deployment/operator guidance, and process-level regression coverage. This result verifies the repository-supported configuration boundary; it is **NOT VERIFIED AS REAL PRODUCTION DEPLOYMENT** and does not close SEC-04.

## Scope

- Normalize the current .NET 8 API Production configuration without changing authentication architecture, schema, migrations, frontend, packages, or deployment infrastructure.
- Preserve the RUNTIME-STARTUP-FAILURE-R01 behavior: known configuration failures write actionable stderr and exit `1` without `Unhandled exception` or `0xe0434352`.
- Require explicit Production authentication, persistent Data Protection, and an absolute persistent SQLite location.
- Document environment-variable mapping, secret handling, direct-EXE behavior, logging, and the HTTPS/reverse-proxy boundary.
- Verify Development, invalid Production, valid Local-only Production, API authentication behavior, isolated SQLite integrity, cleanup, and repository database protection.

## Baseline

- Branch: `main`.
- HEAD: `6d088442278efe67fde190c96241d0c88230ca9a` (`feat: add delete UX and historical tombstones`).
- Pre-existing worktree change: `AGENTS.md` was modified by the user before this task. It was read and preserved; it is not a task file.
- Baseline `git diff --check`: no whitespace error; Git emitted only the existing inaccessible global-ignore warning.
- No `appsettings.Production.json` or Production Deployment Guide existed.
- Existing RUNTIME-STARTUP-FAILURE-R01 report and implementation already converted four known startup validation failures into actionable stderr plus exit `1`.

The task-specific instruction explicitly prohibited automatic commit/push. No commit, stage, branch change, or push was performed.

## Repository DB Baseline

| File | Length | LastWriteTimeUtc | SHA-256 |
| --- | ---: | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | 897,024 | `2026-08-27T15:46:01.9864232Z` | `7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483` |

No repository-owned `*.db-wal` or `*.db-shm` file existed at baseline. File discovery used the explicit ignored `App_Data` directory rather than `rg --files`, which intentionally omits ignored runtime data.

## Existing Production Configuration

Before this task:

- `appsettings.json` supplied `Data Source=App_Data/system-knowledge-hub.db`, disabled OIDC values, Local lockout/rate-limit defaults, an empty Data Protection key path, Console log levels, and `AllowedHosts=*`.
- `appsettings.Development.json` only repeated log levels.
- `launchSettings.json` selected Development and `http://localhost:5090` for `dotnet run` tooling only.
- Production/non-Development/non-Testing startup already required at least one enabled authentication method, complete enabled OIDC core values, positive Local protection values, and a Data Protection key path.
- Local authentication was auto-enabled only when its `Enabled` key was absent in Development. Testing was an explicit exception controlled by the test host.
- Data Protection used application name `SystemKnowledgeHub` and optional filesystem persistence, but accepted relative/deployment-local paths.
- SQLite relative paths were resolved against API Content Root and their directories were created. This is appropriate for Development but not a durable Production placement rule.
- Console was the only logging provider. HTTPS redirection was enabled outside Development. No forwarded-header trust configuration or file logging framework existed.

## Configuration Precedence

The source uses `WebApplication.CreateBuilder(args)` without any custom configuration provider. The API project has no `UserSecretsId`. Effective application value precedence is:

1. `appsettings.json`;
2. `appsettings.{Environment}.json`;
3. Development User Secrets when a developer configures them (none is configured in this project);
4. environment variables (`__` maps nested keys);
5. command-line configuration.

The standard ASP.NET Core host inputs choose the environment. Deployment guidance sets `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT` consistently. Direct EXE startup does not load `Properties/launchSettings.json`.

## Root Configuration Model

| Concern | Development | Testing | Production |
| --- | --- | --- | --- |
| Authentication | Local becomes enabled only if key absent; OIDC opt-in | Test-owned configuration/schemes | Local/OIDC explicitly disabled by checked-in overlay; deployment must enable at least one |
| Data Protection | Framework fallback or configured path | Test-owned ephemeral/path | absolute persistent path outside deployment directory required |
| SQLite | base relative path resolved from Content Root | isolated test SQLite | valid connection string with absolute persistent Data Source required |
| Logging | Console | test capture | Console/stderr; transport/retention deployment-owned |
| URLs | launch profile HTTP 5090 | isolated port | explicit deployment URL input |
| HTTPS/proxy | Development HTTP | not Production evidence | Secure Cookie + HTTPS redirection; no guessed proxy trust |

## Authentication Rules

- Production never changes to Development, enables Local automatically, accepts both methods disabled, enables anonymous access, or continues after a known validation failure.
- Local-only, OIDC-only and both-enabled modes remain supported when explicitly configured.
- Local mode contains no default account/password and no committed credential. Administrator bootstrap remains explicit and accepts a password only through hidden input or an operator-controlled stdin channel.
- Enabled OIDC requires Provider, Authority and ClientId. ClientSecret remains an optional bound value supplied by deployment when its provider requires one; it is not committed.
- Fallback authorization and `/api/current-user` remain fail-closed. Runtime validation returned `401` for an anonymous request in both Development and Production.

## Data Protection Rules

- Production requires a non-empty `DataProtection:ApplicationName`; the checked-in stable non-secret value is `SystemKnowledgeHub`.
- Production `KeyPath` cannot be empty, relative, or inside the application deployment directory.
- The configured location must be persistent across restart/redeploy, access-restricted, backed up as appropriate, and protected at rest by the selected deployment environment.
- The application configures filesystem persistence but does not select a DPAPI, certificate, cloud store, or other encryption-at-rest mechanism because no platform/topology is approved. Real store permissions, encryption and restart cookie continuity remain SEC-04 deployment evidence.
- Development/Testing verification may use disposable task-owned key directories; these are not valid Production stores.

## SQLite Rules

- Production requires `ConnectionStrings:KnowledgeHub` to parse as SQLite and specify an absolute persistent Data Source.
- Empty, `:memory:`, URI and relative Data Sources fail before Host construction. Production therefore cannot silently use repository `App_Data`, a build/publish directory, or a working-directory-dependent database.
- Normal Production host startup does not migrate or seed. Existing bootstrap commands migrate only under explicit operator invocation. Backup/migration/rollback ownership is still required for deployment.
- No schema, migration, provider, pragma, or repository database was changed by this task.

## Secret Handling

- `appsettings.Production.json` contains empty/disabled structure only.
- The deployment guide uses `<YOUR_SECRET>` for ClientSecret and does not contain a password, token, private key, or real provider credential.
- Secrets must be injected by the deployment secret/configuration system and must not appear in JSON, command arguments, source, reports, logs, SQLite fixtures, or Git.
- Data Protection key XML was created only in the isolated task directory and was removed during cleanup.

## Environment Variable Mapping

The deployment guide maps the actual code keys:

- `DOTNET_ENVIRONMENT`, `ASPNETCORE_ENVIRONMENT`;
- `Authentication__Local__Enabled`;
- `Authentication__Oidc__Enabled`, `Provider`, `Authority`, `ClientId`, `ClientSecret`, `CallbackPath`;
- `DataProtection__ApplicationName`, `DataProtection__KeyPath`;
- `ConnectionStrings__KnowledgeHub`;
- `ASPNETCORE_URLS`.

Machine-specific values are environment overrides, not checked-in paths.

## HTTPS / Proxy Boundary

- Production retains `CookieSecurePolicy.Always` and `UseHttpsRedirection`.
- No proxy/ingress product was selected and no permissive forwarded-header trust was added.
- The guide distinguishes a loopback HTTP origin-binding example from public HTTPS and requires an approved trusted-proxy/network policy when TLS terminates upstream.
- Real TLS, certificate loading, HSTS ownership, forwarded scheme/host behavior, public OIDC callback/path-base, Secure Cookie browser flow, and proxy body/log redaction were not available.
- Result: `NOT VERIFIED AS REAL PRODUCTION DEPLOYMENT`.

## Files Changed

| File | Purpose |
| --- | --- |
| `src/SystemKnowledgeHub.Api/appsettings.Production.json` | Fail-closed, secret-free Production overlay. |
| `src/SystemKnowledgeHub.Api/Program.cs` | Validate Production Data Protection placement and SQLite configuration through the existing actionable exit path. |
| `src/SystemKnowledgeHub.Api/Persistence/DbContextConfiguration.cs` | Centralize Production SQLite connection-string/path validation with the actual provider parser. |
| `tests/SystemKnowledgeHub.Api.Tests/Runtime/StartupConfigurationProcessTests.cs` | Add OIDC, Data Protection, SQLite, Development and valid Production process regressions. |
| `docs/PRODUCTION_DEPLOYMENT_GUIDE.md` | Record supported configuration, environment mapping, startup, secret, persistence and HTTPS/proxy boundaries. |
| `docs/DOCUMENT_INDEX.md` | Register the guide and this report. |
| `docs/reports/PRODUCTION_DEPLOYMENT_CONFIGURATION_R01_VERIFICATION_REPORT.md` | Record implementation and verification evidence. |

The pre-existing `AGENTS.md` change is excluded.

## Build

```text
dotnet build SystemKnowledgeHub.sln --no-restore -c Release
```

PASS — 0 errors, 0 warnings; 5 seconds on the final pre-test build. An initial compile found one new nullable-flow warning, which was corrected before verification; the final gate contains no warning.

## Focused Tests

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj
  --no-build -c Release
  --filter "FullyQualifiedName~StartupConfigurationProcessTests"
```

PASS — 8 passed, 0 failed, 0 skipped, 4 seconds. Coverage includes:

- both Production authentication methods disabled;
- enabled but incomplete OIDC;
- missing Data Protection application name;
- missing Data Protection path;
- relative Data Protection path;
- relative Production SQLite path;
- valid Local-only Production host/API behavior;
- Development absent-Local-key fallback behavior.

Every invalid case asserts exit `1`, actionable stderr, no `Unhandled exception`, and no `0xe0434352` marker.

## Full Tests

The approved `REV-GAP-011` deterministic serial gate used a task-owned runsettings file and task-owned Data Protection directory:

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj
  --no-build -c Release
  --settings <task-owned>/serial.runsettings
```

PASS — 175 passed, 0 failed, 0 skipped, 35 seconds. The temporary runsettings and key directory were removed.

## Runtime Verification

All processes used the current framework-dependent Release apphost, task-owned paths and isolated loopback ports.

| Mode | Configuration/result | PID | Port | Cleanup |
| --- | --- | ---: | ---: | --- |
| Development | Explicit Development; Local enabled; isolated SQLite/keys; Host started | 24608 | 18078 | exact process tree stopped; port released |
| Production Invalid | Local=false, OIDC=false; exit `1`; empty stdout; actionable configuration diagnostic | 25032 | 18086 | process exited itself; port free |
| Production Valid | Explicit Local=true, OIDC=false; absolute SQLite/key paths; Host started | 25204 | 18087 | exact process tree stopped; port released |

The direct EXE remains a build apphost, not formal publish output. Runtime did not use `launchSettings.json`.

## Invalid / Valid Production Startup

- Invalid: exit code `1`; diagnostic identified Production and required at least one of `Authentication:Local` or `Authentication:Oidc`; no CLR crash dialog path was used.
- Valid: process stayed alive until exact cleanup, authentication options reported Local enabled/OIDC disabled, and anonymous current-user resolution stayed unauthorized.
- The automated process tests independently asserted readable Simplified Chinese stderr and absence of `Unhandled exception`/`0xe0434352`; the ad-hoc PowerShell console rendered redirected Chinese text with its host code-page limitation only.

## API Smoke

| Mode | `GET /api/auth/options` | `GET /api/current-user` anonymous |
| --- | --- | --- |
| Development | `200`; `localLoginEnabled=true`, `oidcLoginEnabled=false` | `401` |
| Production Valid | `200`; `localLoginEnabled=true`, `oidcLoginEnabled=false` | `401` |

No anonymous bypass or disabled-authentication smoke configuration was used.

## Windows Event Log

Application log entries from `2026-08-28T12:17:11.1828537Z` onward were checked for `.NET Runtime`, `Application Error`, and `Windows Error Reporting` events whose message referenced `SystemKnowledgeHub.Api`. Matching events: 0.

## Temporary SQLite Integrity

After the Development process stopped, the task-owned `development.db` was opened read-only by the project's `Microsoft.Data.Sqlite` assembly using bundled SQLite `3.53.3`:

```text
PRAGMA integrity_check: ok
PRAGMA foreign_key_check: 0 rows
```

The database, WAL, SHM and Data Protection keys were then removed with the task directory.

## Repository DB Protection

Final metadata exactly matched the baseline:

| File | Length | LastWriteTimeUtc | SHA-256 |
| --- | ---: | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | 897,024 | `2026-08-27T15:46:01.9864232Z` | `7F6C35A2BB5120FFA77326D8E594C130F32C53C1285D9890229F9459294D4483` |

No WAL/SHM appeared. No task command opened, migrated, seeded, checkpointed, rebuilt, deleted, or otherwise changed repository `App_Data`.

Result: `Repository App_Data: UNCHANGED`.

## Cleanup

- PIDs 24608 and 25204 were stopped by exact process object/tree; PID 25032 exited normally with code `1`.
- Ports 18078, 18086 and 18087 have no listener.
- No `SystemKnowledgeHub.Api` or `testhost` process remained at final inspection.
- The task runtime directory and repository-local temporary runsettings directory were removed; both paths no longer exist.
- A broad Win32 process command-line inventory was denied by the environment. Exact process ownership, exit waits, port checks, named-process checks and temporary-directory removal supplied the cleanup evidence without terminating any pre-existing process.

## Limitations

- No real public HTTPS endpoint, reverse proxy, certificate, OIDC provider/callback, protected durable key store, deployment secret store, publish artifact, backup/restore rehearsal, restart-cookie continuity, production log transport/retention, or rollback system was supplied.
- The application does not itself select Data Protection key encryption at rest; deployment must provide and verify it.
- The repository has no approved final publish/deployment topology or Production database migration runbook.
- Localhost HTTP smoke proves configuration and authentication behavior only, not Production readiness.

## Existing Gaps

- `REV-GAP-011` remains the existing default-parallel backend-suite stall. The approved deterministic serial gate passed 174/174; no test-infrastructure change was made.
- `SEC-04` remains `BLOCKED` for real OIDC/HTTPS/proxy, persistent protected keys, deployment logging/operations and real environment evidence.

## New Gap Check

No new gap was created. Missing deployment topology and operational controls are already owned by SEC-04/Production Engineering and were not duplicated or speculatively implemented.

## Final Result

```text
PRODUCTION-DEPLOYMENT-CONFIGURATION-R01 PASS

Configuration model: VERIFIED
Authentication fail-closed: PASS
Production secret-free overlay: PASS
Data Protection persistent-path boundary: PASS
Production SQLite absolute-path boundary: PASS
Known invalid startup: exit 1; no unhandled CLR crash
Valid Local-only Production startup: PASS
Development regression: PASS
API smoke: 200 / 401
Build: PASS — 0 warnings / 0 errors
Focused tests: PASS — 8/8
Full backend serial gate: PASS — 175/175
Temporary SQLite: integrity ok / foreign keys 0
Repository App_Data: UNCHANGED
Cleanup: PASS
Real HTTPS/proxy/deployment: NOT VERIFIED AS REAL PRODUCTION DEPLOYMENT
SEC-04: remains BLOCKED
New gaps: none
```
