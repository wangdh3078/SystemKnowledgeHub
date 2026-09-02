# DBSAFE-R01 — Verification Runtime Repository SQLite Guard

## Result

```text
DBSAFE-R01 PASS
DBDISC-VERIFY-R02 READY: YES
```

DBSAFE-R01 adds a fail-closed storage boundary for `Testing` and the explicit `Verification` runtime environment. No Database Discovery product behavior, EF model, migration, Provider, Snapshot, Difference, Reconciliation, Sync, or Database Knowledge behavior changed.

## Root Cause and Attribution Boundary

The pre-fix runtime contained a real unsafe fallback: the base configuration supplied `Data Source=App_Data/system-knowledge-hub.db`, while `Testing` was exempt from the Production connection-string check. A shared `WebApplicationFactory` replaced the registered DbContext with an in-memory connection later, but did not give `Program` an explicit safe connection string before initial configuration and persistence registration. A `Verification` environment also had no dedicated task-owned storage guard.

This condition could allow a Testing/Verification bootstrap path or an incomplete verification host to resolve the repository `App_Data` database. The new tests prove that this fallback existed as a risk and that current code rejects it before DbContext registration or SQLite open.

There is still insufficient evidence to prove that this fallback was the sole cause of the historical DBDISC-VERIFY-R01 repository mutation. `DBDISC_FINAL_VERIFICATION_REPORT.md` and `DBDISC_FINAL_R01_VERIFICATION_REPORT.md` remain unchanged historical FAIL evidence; this report does not rewrite their attribution.

## Implemented Guard

- Added `IsolatedRuntimeStorageGuard`, applied before Serilog and `KnowledgeHubDbContext` registration.
- `Testing` and `Verification` require an explicit, valid SQLite connection string with a fully-qualified filesystem Data Source.
- Missing, relative, `:memory:`, URI-style, repository/content-root, source-tree, `bin`, `obj`, and `publish` Data Source paths fail closed with:

  ```text
  Verification/Testing runtime must use an explicit task-owned SQLite database.
  ```

- Data Protection KeyPath, Attachment StorageRoot, and the Serilog file sink path must also be explicit, fully-qualified, task-owned paths outside repository/content/build output.
- Validation performs only configuration parsing and `Path`/directory-name checks. It never opens the candidate SQLite database to classify the path.
- Serilog host configuration is deferred until the isolated storage guard passes, so Verification cannot initialize the inherited repository-relative log sink first.

## Verification Environment

`Verification` is now an explicit runtime environment with these behaviors:

- it requires all four isolated storage paths;
- it automatically migrates only the accepted task-owned SQLite database;
- it logs the resolved absolute SQLite path to the task-owned Serilog file as a safe startup diagnostic;
- it does not call `DatabaseKnowledgeDevelopmentData.InitializeAsync` or `BusinessFunctionDevelopmentData.SeedAsync`;
- it retains all non-Development authentication, security, and configuration validation.

The process test verified that the task-owned database contained the complete migration history and zero development `database_objects` rows. The startup diagnostic recorded `Verification` and the exact task-owned database path without a secret.

Development remains unchanged: an isolated Development process started successfully and retained the established database migration plus development seed behavior. Production validation and persistence rules remain unchanged.

## Testing Environment

The shared `BootstrapWebApplicationFactory` now provides, through host settings before `Program` consumes configuration:

- an absolute task-owned guard SQLite path;
- an absolute task-owned Data Protection key path;
- the existing task-owned Attachment root;
- the existing task-owned Serilog file path.

The test factory still replaces application persistence with its established in-memory SQLite connection. Regression evidence confirms that the guard database file is never created and the repository database is never touched. Derived factories inherit the same safe initial configuration, including the few Development-mode authentication factories.

## Process Isolation Matrix

One non-parallel repository-sentinel process test covered the complete requested matrix:

| Case | Runtime | Expected / observed result |
| --- | --- | --- |
| A | Verification with no connection override | Startup rejected before SQLite open |
| B | Verification with relative `App_Data` path | Startup rejected before SQLite open |
| C | Verification with absolute repository database path | Startup rejected before SQLite open |
| D | Verification with explicit task-owned SQLite, keys, attachments, logs, and isolated port | Startup succeeded; task database migrated; no Development seed |
| E | Testing `WebApplicationFactory` | Host succeeded on in-memory SQLite; guard database absent |

The successful process was probed through `/api/auth/options`, then stopped. Its database and log were inspected only inside the task-owned temporary root. The test captures repository DB/WAL/SHM existence, size, UTC mtime, and SHA-256 before the matrix and compares the same values afterward.

## EF Design-time Safety

The existing `KnowledgeHubDesignTimeDbContextFactory` remains fail closed. Both the direct factory regression and a real command were checked with `SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH` absent:

```text
dotnet ef dbcontext info ... --configuration Release --no-build
→ non-zero exit
→ requires SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH
→ repository fingerprint unchanged
```

No design-time database was created and no EF migration or model change was introduced.

## Verification

| Gate | Result |
| --- | --- |
| Release solution build | PASS — 0 warnings, 0 errors |
| Startup/path/Testing/Development focused regression | PASS — 60/60 |
| Verification process matrix and repository sentinel | PASS — 2/2 test methods; Cases A–E covered |
| Existing startup configuration and Development behavior | PASS — included in focused 60/60 |
| EF design-time fail-closed direct factory regression | PASS |
| Real `dotnet ef --no-build` fail-closed check | PASS — expected non-zero exit and actionable diagnostic |
| Approved deterministic serial full backend gate | PASS — 427/427, 0 failed, 0 skipped |
| Frontend | NOT APPLICABLE — no frontend files or contracts changed |
| EF migration | NOT APPLICABLE — no model/persistence schema change |
| `git diff --check` | PASS |

The full backend gate used the approved `REV-GAP-011` task-owned serial runsettings (`MaxCpuCount=1`, xUnit collection parallelization disabled, one maximum parallel thread). The temporary runsettings file was removed after verification.

## Repository Data Protection

The baseline for this task is the current post-R01 repository state. It was captured and rechecked only with OS filesystem metadata and SHA-256 operations; SQLite, EF, and `Mode=ReadOnly` were never used against these files.

| File | Start | Final |
| --- | --- | --- |
| `system-knowledge-hub.db` | Exists; 1,249,280 bytes; `2026-09-01T15:44:07.1336663Z`; `5FEB2A474CF9E99B12D8114D6B64DC35B5F56F9CFC0CBEFF7F62F44FD0E7E564` | Identical |
| `system-knowledge-hub.db-wal` | Absent | Absent |
| `system-knowledge-hub.db-shm` | Absent | Absent |

```text
REPOSITORY DATA PROTECTION: PASS
```

No repository database, WAL, or SHM was created, opened, deleted, checkpointed, restored, or timestamp-manipulated by this task.

## Cleanup

- Stopped only task-owned orphaned `dotnet` build/test worker processes from completed verification sessions; each was identified by the exact task-session start timestamp and had no listening port.
- Final cleanup left no API, testhost, VSTest, task-owned build/test worker, or verification listener.
- Removed task-owned process databases/WAL/SHM, Data Protection keys, attachments, logs, dynamic ports, temporary runsettings, and empty DBSAFE test parent directories.
- Historical temporary attachment/log directories not created by this task were not modified.
- No browser or Codex process was opened or closed.

```text
CLEANUP: PASS
```

## Existing / New Gaps

- `DBDISC-VERIFY-BLOCKER-001` remains historical evidence in the two final-verification FAIL reports. DBSAFE-R01 closes the missing runtime guard prerequisite; only DBDISC-VERIFY-R02 may close the Database Discovery phase gate.
- `REV-GAP-011` remains OPEN / Deferred; its approved deterministic serial backend gate passed.
- No new Blocker or High gap was found.

## Final Status

```text
DBSAFE-R01 PASS

VERIFICATION ENVIRONMENT GUARD: PASS
TESTING ENVIRONMENT GUARD: PASS
REPOSITORY PATH REJECTION: PASS
TASK-OWNED PATH STARTUP: PASS
DEVELOPMENT BEHAVIOR REGRESSION: PASS
EF DESIGN-TIME SAFETY: PASS
PROCESS ISOLATION: PASS
BACKEND REGRESSION: PASS
REPOSITORY DATA PROTECTION: PASS

DBDISC-VERIFY-R02 READY: YES
```
