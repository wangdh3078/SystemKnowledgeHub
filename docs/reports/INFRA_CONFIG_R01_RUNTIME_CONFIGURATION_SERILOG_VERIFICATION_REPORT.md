# INFRA-CONFIG-R01 Runtime Configuration and Serilog Verification Report

## Result

`INFRA-CONFIG-R01 PASS`

Runtime configuration centralization, configuration-driven Serilog, attachment capability publication, Database Discovery polling/timeout configuration, authentication and SQLite operational options, startup validation, and the required frontend integration were implemented without changing frozen business or security semantics. Release build, focused and full backend regression, full frontend regression, isolated runtime verification, repository-data protection, cleanup, migration review, and final whitespace review all passed.

## Scope

- Replace the ASP.NET Core Console-only logging setup with configuration-driven Serilog Console and rolling File sinks.
- Centralize deployment/runtime tuning for Database Discovery, attachments/multipart, authentication session/password hashing, SQLite operational waits, CORS origins, and logging.
- Keep domain, protocol, security, parsing, file-recognition, Oracle 19c-only, authorization, and SQLite integrity invariants in code.
- Publish only safe attachment capability data to authenticated frontend clients so deployment allowlists and limits have one source.
- Change the common Database Discovery idle queue poll default from 500 ms to 2000 ms without changing claim, lease, heartbeat, cancellation, or recovery semantics.
- Preserve tracked configuration as secret-free common defaults, Development-only differences, and Production-only fail-closed placeholders.
- Add no EF Migration and do not begin Oracle Docker R01.

## Verified implementation

- `Program.cs` uses `UseSerilog(..., writeToProviders: false)`, reads typed runtime options before normal Host startup, and reports known invalid configuration through the existing actionable exit path.
- `SystemKnowledgeHub.Api.csproj` declares `UserSecretsId=SystemKnowledgeHub.Api`; the standard builder therefore supports local Development User Secrets without putting their values in tracked configuration. Production still requires its approved deployment secret mechanism.
- `appsettings.json` owns safe common defaults; `appsettings.Development.json` contains only Local-authentication and explicit Vite-CORS differences; `appsettings.Production.json` contains only Production fail-closed choices/placeholders.
- `Shared/Configuration/RuntimeOptions.cs` defines bounded Cookie, password-hasher, SQLite, CORS, and Serilog validation.
- `DatabaseDiscoveryOptions` owns connection/catalog/overall timeouts, polling/lease/heartbeat values, and snapshot limits. Oracle connection/catalog components consume the typed options.
- `AttachmentOptions` owns deployment allowlists, existing upload/preview limits, multipart buffering, and upload overhead. `AttachmentFilePolicy` still owns the closed safe extension-to-kind/MIME/preview/recognition catalog.
- Kestrel request-body and Form multipart limits are derived from the same effective attachment maximum plus configured overhead.
- The authorized `GET /api/runtime-capabilities/attachments` endpoint returns only the five safe attachment fields required by the frontend.
- SQLite `DefaultTimeout` and `busy_timeout` are configurable while `foreign_keys=ON` and WAL remain code-enforced.
- Development CORS origins come from configuration; Production CORS is not implicitly enabled.

The implementation was covered by the verification evidence below.

## Configuration ownership

| File | Owned content | Secret policy |
| --- | --- | --- |
| `src/SystemKnowledgeHub.Api/appsettings.json` | Safe common defaults for runtime/operational tuning and Serilog | No real Secret, credential, password, token, machine-specific persistent path, or Production connection string |
| `src/SystemKnowledgeHub.Api/appsettings.Development.json` | Local authentication enabled and two explicit Vite origins | No credential or reusable Secret |
| `src/SystemKnowledgeHub.Api/appsettings.Production.json` | Disabled authentication choices and empty required persistent-path/connection placeholders | Deployment must supply values through environment/secret mechanisms; tracked file remains unusable/fail-closed |

## Configuration Inventory

Every listed configuration key can be overridden through standard ASP.NET Core configuration, including environment variables with `__` separators. “可环境覆盖” does not authorize crossing the listed safety boundary.

| 配置项 | 原位置 | 新配置 Key | 默认值 | 是否可环境覆盖 | 安全边界 |
| --- | --- | --- | --- | --- | --- |
| Application minimum log level | Console provider setup and logging JSON | `Serilog:MinimumLevel:Default` | `Information` | 是，可调高 | Must be `Information` or higher so deployment tuning cannot enable Debug/Verbose sensitive framework detail |
| Framework log overrides | Logging JSON / provider defaults | `Serilog:MinimumLevel:Override:*` | `Microsoft=Warning`; `Microsoft.AspNetCore=Warning`; `Microsoft.EntityFrameworkCore.Database.Command=Warning` | 是，可调高 | Required Microsoft safety overrides and every Microsoft child override must remain `Warning` or higher; all overrides must be at least `Information` |
| Log sinks | `Program.cs` Console provider only | `Serilog:WriteTo` | Console plus File | 是 | Startup requires Console and a rolling File sink; do not log passwords, Secrets, connection strings, headers/cookies, raw Oracle data, or bodies |
| Rolling log file path | No application File sink | `Serilog:WriteTo:1:Args:path` | `logs/system-knowledge-hub-.log` | 是 | Non-empty valid non-root path; Production ACL/durability/capacity are deployment-owned |
| Rolling interval | No application File sink | `Serilog:WriteTo:1:Args:rollingInterval` | `Day` | 是 | Must be a valid Serilog rolling interval |
| Retained file count | No application File sink | `Serilog:WriteTo:1:Args:retainedFileCountLimit` | `14` | 是 | Bounded 1–3650; deployment still owns legal/operational retention |
| File size / roll-on-size | No application File sink | `Serilog:WriteTo:1:Args:fileSizeLimitBytes`; `rollOnFileSizeLimit` | `104857600`; `true` | 是 | Size bounded 1 MiB–1 GiB; roll flag must be Boolean |
| Oracle/Test-Connection connection timeout | Oracle connection components used code-local 15-second values | `DatabaseDiscovery:ConnectionTimeoutSeconds` | `15` seconds | 是 | Bounded 1–300; Oracle 19c-only/service/root/visibility gates remain code-owned |
| Oracle catalog command timeout | Oracle catalog reader used a code-local 60-second value | `DatabaseDiscovery:CatalogCommandTimeoutSeconds` | `60` seconds | 是 | Bounded 1–3600; approved `ALL_*` query inventory and redaction remain code-owned |
| Discovery overall timeout | `DatabaseDiscoveryOptions` code default | `DatabaseDiscovery:OverallTimeoutSeconds` | `900` seconds | 是 | Bounded 1–86400; timeout must preserve durable terminal-state behavior |
| Idle queue poll | `DatabaseDiscoveryOptions` / worker code default (`500` ms before this task) | `DatabaseDiscovery:QueuePollIntervalMilliseconds` | `2000` ms | 是 | Bounded 25–60000 and cannot equal heartbeat duration in milliseconds |
| Lease and heartbeat | `DatabaseDiscoveryOptions` code defaults | `DatabaseDiscovery:LeaseDurationSeconds`; `HeartbeatIntervalSeconds` | `30`; `5` seconds | 是 | Lease 2–3600; heartbeat positive and shorter than lease; recovery semantics unchanged |
| Discovery included-schema limit | `DatabaseDiscoveryOptions` code default | `DatabaseDiscovery:MaximumIncludedSchemas` | `128` | 是 | Bounded 1–1024; scope fingerprint/canonical identity remains code-owned |
| Discovery object/column/constraint/index limits | `DatabaseDiscoveryOptions` code defaults | `DatabaseDiscovery:MaximumObjects`; `MaximumColumns`; `MaximumConstraintsAndIndexes` | `25000`; `250000`; `250000` | 是 | Positive validated operational ceilings; no query/identity rule becomes configurable |
| Discovery sequence/snapshot limits | `DatabaseDiscoveryOptions` code defaults | `DatabaseDiscovery:MaximumSequences`; `MaximumCanonicalSnapshotBytes` | `10000`; `134217728` bytes | 是 | Positive; snapshot bytes bounded 1024–536870912; canonical serialization/hash remains code-owned |
| Attachment image allowlist | Backend safe catalog plus frontend image extensions | `Attachments:AllowedImageExtensions` | `.png,.jpg,.jpeg,.gif,.webp` | 是 | Comma-separated lowercase subset of code-owned Image descriptors only; unsupported/misclassified/duplicate entries fail startup |
| Attachment ordinary-file allowlist | Backend safe catalog plus frontend accepted extensions/content types | `Attachments:AllowedFileExtensions` | `.pdf,.docx,.xlsx,.pptx,.txt,.log,.sql,.md,.csv,.json,.xml,.zip` | 是 | Comma-separated lowercase subset of code-owned File descriptors only; configuration cannot define MIME/preview/recognition mappings |
| Attachment storage | Existing attachment configuration | `Attachments:StorageRoot` | `App_Data/attachments` in common config; empty Production placeholder | 是 | Production requires private non-root absolute persistent path outside deployment tree |
| Attachment upload/count limits | Existing attachment configuration | `Attachments:MaxImageBytes`; `MaxFileBytes`; `MaxStoredAttachmentsPerDocument` | `10485760`; `52428800`; `100` | 是 | Positive within absolute code ceilings; backend is final authority |
| Multipart memory threshold | `Program.cs` / framework form default | `Attachments:MemoryBufferThresholdBytes` | `65536` | 是 | Positive and at most 4 MiB; protected temp storage still required |
| Multipart upload overhead | Request-body derivation in `Program.cs` | `Attachments:UploadRequestOverheadBytes` | `2097152` | 是 | Positive and at most 16 MiB; Kestrel/Form limits derive from the same calculation |
| Attachment preview limits | Existing `AttachmentOptions` / configuration | `Attachments:Preview*` keys | Text 262144 bytes; CSV 200×50/262144 chars; workbook 10 MiB/20 sheets/200×50/1048576 shared-string chars | 是 | Positive within parser/resource ceilings; DTD/archive/XML/signature safety remains code-owned |
| Cookie lifetime and sliding renewal | Authentication cookie setup in `Program.cs` | `Authentication:Cookie:ExpireHours`; `SlidingExpiration` | `8`; `true` | 是 | Expiry bounded 1–720 hours; request-time session revalidation and invalidation remain code-owned |
| Password hasher work factor | Password hasher setup in `Program.cs` | `Authentication:Local:PasswordHasher:IterationCount` | `220000` | 是 | Minimum 220000, maximum 2000000; frozen password/lifecycle rules cannot be weakened |
| Local lockout and rate limit | Existing Local options/configuration | `Authentication:Local:Lockout:*`; `RateLimit:*` | `5/15/15`; `20/5` | 是 | Values must be positive; last-Administrator/session/error semantics remain code-owned |
| Authentication methods/OIDC | Existing authentication configuration | `Authentication:Local:Enabled`; `Authentication:Oidc:*` | Production methods disabled; OIDC empty/disabled | 是 | At least one method outside Testing; enabled OIDC requires Provider/Authority/ClientId; ClientSecret stays external |
| SQLite default timeout | `DbContextConfiguration` hardcoded `5` | `Persistence:Sqlite:DefaultTimeoutSeconds` | `5` seconds | 是 | Bounded 1–300; Production path rules unchanged |
| SQLite busy timeout | `DbContextConfiguration` hardcoded `5000` PRAGMA | `Persistence:Sqlite:BusyTimeoutMilliseconds` | `5000` ms | 是 | Bounded 1–300000; `foreign_keys=ON` and WAL cannot be configured off |
| Development CORS origins | `Program.cs` hardcoded Vite origins | `Cors:AllowedOrigins` | Base `[]`; Development `localhost:5173`, `127.0.0.1:5173` | 是 | Explicit unique HTTP(S) origins only; no wildcard; middleware is Development-only |
| SQLite, Data Protection, OIDC and provider Secrets | Existing configuration boundary plus project User Secrets support | `ConnectionStrings:KnowledgeHub`; `DataProtection:*`; `Authentication:Oidc:ClientSecret` | Safe relative Development/default or empty Production placeholders | 是 | User Secrets are local Development-only and live outside tracked config; real Production values come from the approved environment/deployment secret system; never tracked or logged |

## Safe attachment capability boundary

`GET /api/runtime-capabilities/attachments` is authenticated and returns only:

- `allowedImageExtensions`;
- `allowedFileExtensions`;
- `maxImageBytes`;
- `maxFileBytes`;
- `maxStoredAttachmentsPerDocument`.

StorageRoot, internal filesystem data, canonical MIME mappings, PreviewMode, Recognition rules, parser limits, Secrets, and connection information are excluded. Frontend `accept`, early validation, and user hints may consume this projection; the backend still repeats authoritative extension, kind, declared-MIME, content/signature, exact-byte, archive/XML, authorization, and persistence checks.

## Preserved security and architecture invariants

Static review and regression coverage confirmed that configuration did not make any of the following deployment-adjustable:

- Viewer/Editor/Administrator semantics or last-usable-Administrator protection;
- frozen password length/lifecycle/session authorization rules;
- Oracle 19c-only, service/root/schema visibility, approved `ALL_*` query inventory, normalization, or redaction;
- canonical Snapshot/Diff identity, scope fingerprint, routes, or error codes;
- attachment extension-to-kind/MIME/preview/recognition catalog, signatures, DTD/archive/XML protections, or absolute parser ceilings;
- SQLite `foreign_keys=ON` or WAL;
- Secret exposure in tracked configuration, capability responses, logs, or reports.
- Local Development User Secrets entering tracked configuration/artifacts or being treated as a Production secret store.
- deployment overrides enabling framework Debug/Verbose request, action-argument, or EF command detail.

Status: `PASS`.

## Verification matrix

| Gate | Status | Evidence |
| --- | --- | --- |
| Release build | PASS | `dotnet build SystemKnowledgeHub.sln -c Release --no-restore`: 0 warnings, 0 errors |
| Configuration/backend focused tests | PASS | 87/87 passed: runtime options and Serilog safety floors, attachments, discovery, Oracle timeout wiring, SQLite wiring, capability API, Serilog, and startup validation |
| Worker/auth/attachment regression | PASS | 37/37 passed for discovery run/claim/concurrency, attachment foundation, Local login/lifecycle, password reset, and authentication disablement |
| Full backend regression | PASS | 344/344 passed using the approved deterministic serial `REV-GAP-011` gate; 0 failed, 0 skipped |
| Serilog startup and sinks | PASS | Isolated ordinary Host produced the application startup event once in Console and once in one task-owned daily rolling File; no duplicate provider output |
| Idle EF command suppression | PASS | Host remained idle for more than three 2000 ms poll periods; neither sink contained `Executed DbCommand`, while the application startup Information event remained visible |
| Database Discovery configuration | PASS | Default 15/60/900 second timeouts, 2000 ms polling, lease/heartbeat and bounds covered; Oracle fake tests proved connection/catalog timeout consumption without a real Oracle server |
| Queue claim / heartbeat / recovery | PASS | Focused durable worker tests and full regression passed without lease, cancellation, or recovery changes |
| Attachment policy and multipart | PASS | Safe subset binding, unsupported/wrong-kind/duplicate fail-fast, derived Kestrel/Form limits, memory threshold, upload security, and staging cleanup covered |
| Attachment runtime capability | PASS | Authenticated exact five-field response, anonymous rejection, internal-field exclusion, strict frontend decoding/cache, dynamic accept/hints, and backend authority covered |
| Authentication runtime configuration | PASS | Cookie lifetime/sliding renewal and bounded 220000-minimum password hasher wiring covered; lifecycle/session/security regression passed |
| SQLite operational configuration | PASS | `DefaultTimeout` and `busy_timeout` binding covered; `foreign_keys=ON` and WAL invariants remained code-enforced |
| CORS runtime configuration | PASS | Valid origins and wildcard rejection covered; isolated Development runtime returned credentialed CORS only for the configured Vite origin and none for an unconfigured origin |
| Frontend focused regression | PASS | 34/34 passed with affected ESLint and Prettier checks passing |
| Frontend full regression | PASS | 76 files, 449/449 tests passed |
| Frontend type-check/build | PASS | `npm run type-check` and `npm run build` passed; only the pre-existing chunk-size advisory remained |
| Repository SQLite/object protection | PASS | Final size, UTC mtime, and SHA-256 exactly matched the pre-verification baseline for DB/WAL/SHM and both protected attachment objects |
| No new EF Migration | PASS | Migration-directory diff is empty |
| `git diff --check` | PASS | No whitespace errors; Git emitted line-ending notices only |
| Verification cleanup | PASS | Exact task processes stopped, isolated port closed, and task DB/WAL/SHM/keys/attachments/logs/runsettings/temp roots removed |

## Isolated runtime evidence

- Command model: repository-supported `dotnet run --project src/SystemKnowledgeHub.Api -c Release --no-build --no-launch-profile`.
- Environment: explicit `Development`; loopback port `48334`; task-owned SQLite, Data Protection, Attachment StorageRoot, Serilog path, and process-output paths.
- `/api/auth/options` returned `200` with Local enabled and OIDC disabled.
- Configured origin `http://localhost:5173` received its exact `Access-Control-Allow-Origin` and credentials response; an unconfigured origin received no CORS headers.
- After more than seven idle seconds, Console and the rolling File each contained exactly one application startup event and no EF `Executed DbCommand` Information event.
- Runtime logs contained no `Data Source=`, password, Secret, connection string, Authorization, Cookie, raw `SELECT`, or stderr output.
- Runtime created only task-owned state: one SQLite database, one Data Protection key, and zero attachment objects. All were removed after the process and port were closed.

## Repository protected-data comparison

The following values were captured immediately before and after the final verification cycle. Every length, UTC mtime, and SHA-256 value remained identical.

| Protected file | Length | LastWriteTimeUtc | SHA-256 | Result |
| --- | ---: | --- | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | 995,328 | `2026-08-30T04:51:50.7995165Z` | `B55F1652FA4CC5F0BC6A12B6EB205CAB2F505C8301AF296BDDF0CBF910A2FCE1` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db-wal` | 45,352 | `2026-08-30T07:03:49.6742928Z` | `FB8A5D292010143E7D423587CA64DFF166B2B97000CD08A8628BD8DA6F061458` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db-shm` | 32,768 | `2026-08-30T06:52:31.7391666Z` | `DE1160806E82759BFC0C049CF82E4095C875717F0CB3AF509F7237598112B1B3` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/attachments/objects/23/231fd0636be9fb6a3434ce49a896a1ac.bin` | 257,812 | `2026-08-29T10:58:36.7518484Z` | `E37C091AA405909B584E8466D8E354E5162E2B39782A5257497A7A8AD40C3188` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/attachments/objects/cd/cd05edd690d377be2e56a79ce76290ef.bin` | 20,596 | `2026-08-29T10:58:44.6255427Z` | `A16CC4995F4D080815FFDFA3A2B8D9A15911DC56895DB8397CDC2B6330B77A45` | UNCHANGED |

Repository-data protection status: `PASS`. Verification never connected to the repository-owned SQLite database and did not alter or remove its WAL/SHM files or protected attachment objects.

## Full backend serial gate

`REV-GAP-011` remains `OPEN / Deferred`: default collection-parallel SQLite/WebApplicationFactory execution can stall. The complete backend gate must use the already approved task-owned serial runsettings, not invent a new workaround or claim a partial suite is full regression:

```xml
<RunSettings>
  <RunConfiguration>
    <MaxCpuCount>1</MaxCpuCount>
  </RunConfiguration>
  <xUnit>
    <ParallelizeTestCollections>false</ParallelizeTestCollections>
    <MaxParallelThreads>1</MaxParallelThreads>
  </xUnit>
</RunSettings>
```

The task-owned serial runsettings was used for the authoritative full regression, all 344 tests passed, and the runsettings plus all output-only test state were deleted afterward. `REV-GAP-011` remains an existing deferred infrastructure gap; it did not reduce this task's deterministic regression coverage. Status: `PASS`.

## Documentation synchronization

| Document | Final state |
| --- | --- |
| `docs/PRODUCTION_DEPLOYMENT_GUIDE.md` | Updated for appsettings ownership, typed validation, Serilog, safe runtime capability, environment mapping, and operational checks |
| `docs/PROJECT_FILE_MAP.md` | Updated for runtime configuration, Serilog, capability, environment files, and this report |
| `docs/DOCUMENT_INDEX.md` | Registers this PASS report and reflects the expanded deployment-guide ownership |
| Frozen specifications/design decisions | Not edited |

## Existing observation

- The ordinary attachment upload component already lacked an unmount-time abort mechanism while an existing editor-leave message says an in-progress upload will be stopped. This low-severity UX/lifecycle mismatch predates INFRA-CONFIG-R01, was not caused by the runtime-capability change, does not weaken the backend upload boundary, and was not expanded into an unrelated ATTACH correction.

## Delivery status

- Branch: `main`.
- Commit SHA and push result: recorded in the final task handoff because the task-specific commit necessarily encloses this report.
- Real Production deployment: `NOT VERIFIED AS REAL PRODUCTION DEPLOYMENT`.
- SEC-04: remains `BLOCKED` pending real environment evidence.

## Final status matrix

```text
INFRA-CONFIG-R01: PASS
SERILOG: PASS
RUNTIME CONFIG CENTRALIZATION: PASS
ATTACHMENT POLICY CONFIG: PASS
DATABASE DISCOVERY CONFIG: PASS
AUTH RUNTIME CONFIG: PASS
SQLITE OPERATIONAL CONFIG: PASS
CONFIG VALIDATION: PASS
REPOSITORY DATA PROTECTION: PASS
```
