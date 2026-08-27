# RUNTIME-STARTUP-FAILURE-R01

## Result

`RUNTIME-STARTUP-FAILURE-R01 PASS`

The Windows `0xe0434352` symptom was reproduced as an unhandled managed startup exception, the real exception and first project-owned frame were captured, and the fail-closed configuration boundary was preserved. Invalid startup configuration now exits with code `1` and an actionable diagnostic instead of entering the CLR unhandled-exception path. The current Release executable also starts and responds when supplied with the explicit Production configuration required by the frozen authentication design.

## Original User-visible Symptom

```text
Executable:
SystemKnowledgeHub.Api.exe

Windows exception code:
0xe0434352
```

The user supplied a Windows “应用程序错误” dialog showing unknown software exception `0xe0434352`. `0xe0434352` is only the outer CLR/managed exception signal, not the application root cause.

## Windows Error Code

The controlled pre-fix direct-EXE reproduction returned signed process code `-532462766`, whose unsigned hexadecimal representation is `0xE0434352`. The direct console output supplied the actual managed exception and stack trace.

## Worktree Baseline

- Branch: `main`, tracking `origin/main`.
- HEAD: `3b40b12314d7b93bd57e7c060e29eddcd2168492` (`fix: unify datetime display formatting`).
- `git status --short`: clean.
- `git diff --stat`, `git diff`, and `git diff --check`: no baseline changes or errors.
- Recent commits: `3b40b12`, `42dfe54`, `00da4b2`, `3e7876c`, `0f3246a`.
- Git required a command-scoped `safe.directory` override because the repository owner SID differs from the sandbox process SID. No global Git configuration was changed.

## Repository DB Baseline

| File | Length | LastWriteTimeUtc | SHA-256 |
| --- | ---: | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | 724,992 | `2026-08-26T14:32:28.8616945Z` | `A122F8D208338A8445DCF19CE27601634B98A0B35AC1575FDBB6ADF7A3422DA9` |
| `system-knowledge-hub.db-wal` | 0 | `2026-08-27T12:04:49.8688303Z` | `E3B0C44298FC1C149AFBF4C8996FB92427AE41E4649B934CA495991B7852B855` |
| `system-knowledge-hub.db-shm` | 32,768 | `2026-08-27T12:07:30.8326208Z` | `FD4C9FDA9CD3F9AE7C962B0DDF37232294D55580E1AA165AA06129B8549389EB` |

No running `SystemKnowledgeHub.Api` or `dotnet` process associated with this repository was found at baseline. The existing empty WAL and SHM files were treated as user-owned repository state and were not deleted, checkpointed, or modified.

## Environment

- OS: Windows `10.0.26200`, x64.
- Process architecture: x64.
- Time zone: Asia/Shanghai.
- Baseline `ASPNETCORE_ENVIRONMENT`: not set.
- Baseline `DOTNET_ENVIRONMENT`: not set.
- `dotnet run` applies `Properties/launchSettings.json` and therefore runs as `Development` on `http://localhost:5090` unless overridden.
- A direct executable launch does not apply `launchSettings.json`; with no environment variable it runs as `Production`.

## .NET Runtime Information

- Repository SDK from `global.json`: .NET SDK `8.0.404`.
- MSBuild: `17.11.9`.
- API target framework: `net8.0`.
- Installed applicable runtimes include `Microsoft.AspNetCore.App 8.0.11/8.0.28` and `Microsoft.NETCore.App 8.0.11/8.0.28`.
- Host available to the `dotnet` command: `10.0.9` x64; the net8 application rolled forward only within its applicable net8 runtime family.
- The project declares no `RuntimeIdentifier` or `SelfContained`; the inspected Debug and Release executables are framework-dependent apphosts, not publish output.

## Reproduction

All runtime checks used task-owned absolute SQLite paths, Data Protection directories, logs, and ports. The repository database was never opened by a task runtime.

Pre-fix comparison:

1. `dotnet run --project src/SystemKnowledgeHub.Api` used the `Development` launch profile, auto-enabled Local authentication when its `Enabled` key was absent, migrated/seeded a temporary database, and started normally.
2. `src/SystemKnowledgeHub.Api/bin/Release/net8.0/SystemKnowledgeHub.Api.exe`, launched from its output directory with no environment setting, ran as `Production`. `Authentication:Local:Enabled` was absent and `Authentication:Oidc:Enabled` was false, so the deliberate fail-closed validation threw before Host construction and before the configured console logger was available.

Windows error mode was suppressed only for the diagnostic child process so the blocking GUI dialog could not hide the console exception or exit code. This did not alter application configuration or the thrown exception.

## dotnet run Result

Pre-fix and post-fix isolated Development runs both started successfully.

Post-fix evidence on task port `51811`:

- `Application started` and `Hosting environment: Development` were logged.
- Content root: `src/SystemKnowledgeHub.Api`.
- `GET /api/auth/options`: `200`, Local enabled and OIDC disabled.
- `GET /api/current-user` without a session: `401 unauthenticated`.
- stderr was empty; no Fatal, Critical, fail, or unhandled-exception output was found.
- Exact task PIDs were stopped and the port was released.

## Direct EXE Result

Pre-fix direct launch reproduced the crash. Post-fix behavior is split deliberately:

- Invalid Production configuration: exits promptly with `1` / `0x00000001`, prints the environment, the missing authentication requirement, the supported configuration sources, and the fact that direct EXE launch does not apply `launchSettings.json`. It does not print `Unhandled exception` and does not enter the Windows CLR crash path.
- Valid explicit Production configuration (`Authentication:Local:Enabled=true`, task-owned `DataProtection:KeyPath`, isolated connection string and URL): the current Release EXE starts and stays alive.

This preserves the approved requirement that Production authentication mode be explicitly configured; it does not silently switch the process to Development or enable anonymous access.

## Windows Event Log Evidence

- The latest 500 Application events before reproduction contained no matching `.NET Runtime`, `Application Error`, or `Windows Error Reporting` entry for `SystemKnowledgeHub.Api`.
- The controlled reproduction captured stronger primary evidence directly from the application console and process exit code.
- No matching event was added during the post-fix verification window. Diagnostic pre-fix reproduction used inherited `SEM_NOGPFAULTERRORBOX`/non-GUI error handling so Windows Error Reporting would not block evidence capture.

## Application Log Evidence

- No repository-owned persistent `*.log` file existed outside task temporary output.
- Pre-fix stderr contained the complete unhandled exception and stack.
- Post-fix invalid configuration contained only the two-line actionable startup diagnostic.
- Valid post-fix runs contained normal Lifetime startup output. The isolated Production HTTP smoke emitted expected environment-only warnings for an unencrypted disposable key directory and an unavailable HTTPS redirect port; neither was an error or crash, and this check does not claim Production deployment approval.

## Actual Exception Type

```text
System.InvalidOperationException
```

## Exception Message

```text
至少必须启用 Authentication:Local 或 Authentication:Oidc 之一。
```

## InnerException Chain

None. The captured `InvalidOperationException` was the root managed exception.

## Stack Trace

```text
Unhandled exception. System.InvalidOperationException: 至少必须启用 Authentication:Local 或 Authentication:Oidc 之一。
   at Program.<Main>$(String[] args) in F:\CodeX\Legacy Knowledge Hub\src\SystemKnowledgeHub.Api\Program.cs:line 55
   at Program.<Main>(String[] args)
```

This stack and line refer to baseline commit `3b40b123` before the fix shifted source lines.

## First Project-owned Stack Frame

```text
Program.<Main>$(String[] args)
src/SystemKnowledgeHub.Api/Program.cs:55
```

## Root Cause

`ROOT CAUSE CONFIRMED`.

The direct executable ran in Production because no environment variable was set and `launchSettings.json` is a development-tool input, not runtime configuration for direct EXE launch. The base settings intentionally did not explicitly enable Local authentication and explicitly disabled OIDC. The approved authentication boundary rejects this unsafe state outside Testing, but the rejection was implemented as an unhandled `InvalidOperationException` before normal logging setup. The CLR converted that unhandled managed exception into process code `0xE0434352`, producing the generic Windows dialog.

`dotnet run` did not fail because its launch profile set `ASPNETCORE_ENVIRONMENT=Development`; current code intentionally auto-enables Local authentication in Development when the key is absent.

## Root Cause Mechanism

```text
direct SystemKnowledgeHub.Api.exe
→ launchSettings.json not applied
→ environment defaults to Production
→ Local Enabled absent + OIDC Enabled false
→ approved fail-closed startup validation
→ unhandled InvalidOperationException before normal logger setup
→ CLR exit 0xE0434352
→ generic Windows application-error dialog
```

The fix resolves the crash mechanism by reporting known configuration-validation failures and returning a normal non-zero application exit. It does not weaken or bypass the validation. With explicit valid Production configuration, the same current EXE builds the Host and responds normally.

## Fix

- Replaced the four adjacent, explicit startup configuration `throw` sites with one consistent `ReportStartupConfigurationFailure` path.
- The reporter writes Simplified Chinese actionable stderr containing the actual environment, configuration error, valid configuration sources, and direct-EXE/launch-profile distinction.
- The application sets exit code `1` and returns before Host construction.
- Authentication, OIDC completeness, lockout/rate-limit positivity, and Production Data Protection path requirements remain fail-closed.
- No exception is swallowed after Host startup; unrelated/unexpected exceptions retain their normal failure behavior.

## Files Changed

| File | Why changed |
| --- | --- |
| `src/SystemKnowledgeHub.Api/Program.cs` | Convert known startup configuration validation failures from unhandled CLR exceptions to actionable non-zero exits while retaining every safety check. |
| `tests/SystemKnowledgeHub.Api.Tests/Runtime/StartupConfigurationProcessTests.cs` | Run the built Windows EXE as a child process and prove invalid Production authentication configuration returns `1`, emits the real diagnostic, and does not emit `Unhandled exception`. |
| `docs/reports/RUNTIME_STARTUP_FAILURE_R01_VERIFICATION_REPORT.md` | Record reproduction, root cause, fix, verification, database protection, cleanup, and limitations. |
| `docs/DOCUMENT_INDEX.md` | Register this new verification report and its update trigger. |

## Regression Test

`StartupConfigurationProcessTests.DirectExecutable_WithAuthenticationDisabledInProduction_ExitsWithActionableDiagnostic` starts the current output EXE with explicit Production and both authentication methods disabled. It uses redirected output, a 15-second timeout, a unique temporary directory, and exact child-tree cleanup.

Before the fix, the same condition returned `-532462766` / `0xE0434352` and printed `Unhandled exception`. After the fix, the test asserts exit code `1`, exact configuration guidance, empty stdout, and absence of the unhandled-exception marker.

## Backend Build

- Pre-fix: `dotnet build SystemKnowledgeHub.sln --no-restore -c Release` — PASS, 0 warnings, 0 errors.
- Post-fix: same command — PASS, 0 warnings, 0 errors.

## Focused Tests

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj
  --no-build -c Release
  --filter "FullyQualifiedName~StartupConfigurationProcessTests"
```

PASS — 1/1, 0 failed, 0 skipped.

## Full Backend Tests

The repository's approved deterministic serial gate was used because existing `REV-GAP-011` records that default collection-parallel SQLite/WebApplicationFactory execution stalls.

```text
dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj
  --no-build -c Release
  --settings <task-owned serial.runsettings>
```

PASS — 145/145, 0 failed, 0 skipped, 24 seconds. The temporary runsettings and test key directory were deleted immediately afterward.

Two default-parallel diagnostic attempts were stopped after reproducing the already-documented stall. One diagnostic attempt also exposed that the sandbox user cannot read its default `%LOCALAPPDATA%/ASP.NET/DataProtection-Keys`; the successful deterministic run used a task-owned key directory and suppressed framework log noise. No product setting or user key directory was changed.

## Runtime Verification

- `dotnet run -c Release --no-build`: PASS on isolated port `51811`.
- Current direct Release EXE with explicit valid Production configuration: PASS on isolated port `51812`.
- Both processes stayed alive for HTTP checks, then only their identified task PIDs were stopped.
- Ports `51801`, `51802`, `51803`, `51811`, and `51812` were released; no task API/testhost process remained.

## Direct Executable Verification

Verified executable:

```text
F:\CodeX\Legacy Knowledge Hub\src\SystemKnowledgeHub.Api\bin\Release\net8.0\SystemKnowledgeHub.Api.exe
```

- Configuration: current framework-dependent Release build.
- Build-output timestamp used for runtime verification: `2026-08-27T13:21:51.7125091Z`.
- Working directory: the executable output directory, matching an Explorer/double-click output-folder launch basis.
- Invalid config after fix: normal exit `1`, no CLR crash marker.
- Explicit valid Production config: Host started and responded.
- No publish directory was present; stale publish output was not tested or mistaken for the current build.

## API Smoke Test

Both post-fix runtime modes returned:

| Request | Expected | Actual |
| --- | --- | --- |
| `GET /api/auth/options` | Public authentication options | `200`; Local enabled, OIDC disabled |
| `GET /api/current-user` without a session | Fail closed | `401 unauthenticated` |

Authentication was not disabled for the smoke test.

## Windows Event Log After Fix

No `.NET Runtime`, `Application Error`, or `Windows Error Reporting` Application event referencing `SystemKnowledgeHub.Api` was recorded during the post-fix window. The invalid-config process returned normally with code `1`; the valid-config process remained alive until exact task cleanup.

## Temporary SQLite Integrity

The actual project provider (`Microsoft.Data.Sqlite`, bundled SQLite `3.53.3`) opened both Development runtime databases in read-only mode after their processes stopped:

| Database | `PRAGMA integrity_check` | `PRAGMA foreign_key_check` |
| --- | --- | --- |
| Pre-fix `dotnet run` isolated database | `ok` | 0 rows |
| Post-fix `dotnet run` isolated database | `ok` | 0 rows |

The system Python binding was SQLite `3.5.9`, predating WAL support, and therefore produced an invalid “encrypted or is not a database” result. That tool result was rejected after file-header inspection and replaced by the project's real SQLite provider result above.

## Repository DB Protection

Final length, timestamp, and SHA-256 values for the repository DB, WAL, and SHM matched the baseline table exactly. Result: `UNCHANGED`.

No repository DB migration, seed, checkpoint, integrity command, deletion, rebuild, or test fixture occurred. All runtime and test state was under the task-owned temporary directory.

## New Gap Check

No new product gap was introduced or found.

- `REV-GAP-011` (default parallel backend-suite stall) remains existing OPEN / Deferred; the already-approved serial gate passed 145/145.
- SEC-04 Production deployment blockers (real HTTPS/reverse proxy and protected persistent Data Protection keys) remain unchanged. The isolated HTTP/key-directory smoke is not Production approval.
- No migration, package upgrade, frontend change, product feature, or unrelated refactor was made.

## Final Result

```text
RUNTIME-STARTUP-FAILURE-R01 PASS

Original Windows Error: 0xe0434352
Actual Exception: System.InvalidOperationException
First Project-owned Frame: Program.<Main>$, Program.cs:55 (pre-fix)
Root Cause: direct EXE defaulted to Production, launch profile was absent,
            authentication was intentionally unconfigured, and the fail-closed
            validation escaped as an unhandled managed exception.
Fix: actionable stderr + normal exit 1 for known startup configuration failures;
     all fail-closed requirements preserved.
Build: PASS — 0 warnings / 0 errors
Focused Tests: PASS — 1/1
Full Backend Tests: PASS — 145/145 deterministic serial gate
dotnet run: PASS
Direct EXE: PASS with valid explicit Production config
Invalid Direct EXE: exits 1; no unhandled CLR crash
API Smoke: PASS — 200 / 401
Windows Event Log After Fix: no matching crash event
SQLite integrity_check: ok
SQLite foreign_key_check: 0 rows
Repository App_Data: UNCHANGED
New Gaps: none; existing REV-GAP-011 remains Deferred
```
