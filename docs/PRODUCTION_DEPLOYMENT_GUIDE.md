# System Knowledge Hub Production Deployment Configuration Guide

## Status and boundary

This guide defines the repository-supported configuration and operations boundary for `SystemKnowledgeHub.Api`, including typed runtime options, Serilog outputs, persistent state, authentication, and the safe attachment-capability contract. It does not select an IIS, Nginx, Caddy, YARP, container, cloud, certificate, secret store, backup product, log collector, or monitoring platform. A localhost smoke test is not a real Production deployment approval; SEC-04 remains blocked until the chosen environment supplies and verifies those controls.

The checked-in `appsettings.Production.json` contains only fail-closed values and required empty placeholders. It contains no usable credential, Client Secret, machine path, database path, attachment-storage path, Data Protection key material, or deployment-specific log path.

## Configuration model, precedence, and ownership

`Program.cs` uses `WebApplication.CreateBuilder(args)` without a custom provider. `SystemKnowledgeHub.Api.csproj` declares `UserSecretsId=SystemKnowledgeHub.Api`, so the standard builder automatically loads the current developer's local User Secrets in Development. Effective application configuration, from lower to higher priority, is:

1. `appsettings.json`;
2. `appsettings.{Environment}.json`;
3. Development User Secrets stored outside the repository for `SystemKnowledgeHub.Api`;
4. environment variables, using `__` for nested keys;
5. command-line configuration.

The Host environment is supplied by standard ASP.NET Core host inputs. Deployment should set `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT` consistently to `Production`; command-line input has higher priority. `Properties/launchSettings.json` is a local tooling profile and is not loaded by a directly launched executable.

| File | Ownership |
| --- | --- |
| `appsettings.json` | Safe common defaults: authentication runtime tuning, attachment/preview/multipart limits, Database Discovery timeouts/intervals/limits, SQLite operational timeouts, an empty CORS origin list, and Serilog Console + rolling File configuration. |
| `appsettings.Development.json` | Development-only differences: Local authentication enabled and the two explicit Vite origins. |
| `appsettings.Production.json` | Production-only fail-closed authentication choices and empty required placeholders for SQLite, Attachment StorageRoot, and Data Protection KeyPath. |

Do not copy common values into every environment file. Override only values that the deployment actually owns, and keep passwords, Client Secrets, connection secrets, tokens, and key material out of tracked JSON. User Secrets are a local Development convenience, not a Production secret store; Production must use the approved deployment environment/secret mechanism.

| Concern | Development | Testing | Production |
| --- | --- | --- | --- |
| Environment source | `launchSettings.json` for ordinary `dotnet run`, or explicit host input | Test host explicitly selects `Testing` | Explicit deployment input; direct EXE otherwise defaults to Production |
| Local authentication | Explicitly enabled by Development overlay; no default account/password | Test factory controls the method | Explicitly disabled by Production overlay until deployment enables it |
| OIDC | Disabled by common configuration unless explicitly configured | Test fixtures may use isolated values | Explicitly disabled until deployment enables it; Provider, Authority and ClientId required when enabled |
| Authentication boundary | At least one method outside Testing | Test-only schemes are isolated to tests | At least one of Local/OIDC; otherwise exit `1` |
| Data Protection | Framework development fallback unless a path is supplied | Test-owned ephemeral/key directory | Absolute persistent path outside the deployment directory is required |
| SQLite | Relative common path resolves against API Content Root | Test-owned SQLite | Absolute persistent Data Source path is required |
| Attachment binaries | `App_Data/attachments` below API Content Root | Fixture-owned absolute temporary directory | Explicit absolute persistent root outside the deployment directory is required |
| Logging | Serilog Console + daily/size rolling File | Test-owned file path and captured output | Console + rolling File; durable path, collection, ACL, capacity and retention approval are deployment-owned |
| CORS | Explicit configured Vite origins; credentials allowed only for those origins | Fixture-specific | CORS middleware is not enabled outside Development; configuration does not implicitly open Production cross-origin access |
| URLs | Launch profile uses `http://localhost:5090` | Isolated test port | Explicit `ASPNETCORE_URLS`; public HTTPS/proxy topology is deployment-owned |
| HTTPS/proxy | Development HTTP only | Not Production evidence | Secure Cookie and HTTPS redirection are enabled; trusted proxy topology is not guessed |

## Fail-closed validation and code invariants

Configuration is read and validated before normal Host startup. Invalid values must be corrected at the deployment source; do not weaken validation or switch environments to make startup succeed.

- `Authentication:Local:Enabled` and `Authentication:Oidc:Enabled` are `false` in Production. At least one must be explicitly changed to `true`.
- Enabled OIDC requires non-empty Provider, Authority and ClientId. ClientSecret is configuration-bound but must come from the deployment secret source, never committed JSON.
- `Authentication:Cookie:ExpireHours` must be from 1 through 720; the default is 8. `SlidingExpiration` defaults to `true`.
- `Authentication:Local:PasswordHasher:IterationCount` defaults to and cannot be lower than 220000; the current upper validation bound is 2000000. Configuration cannot lower hashing below the approved safety floor. Frozen password-length and credential lifecycle rules remain code/security invariants.
- Existing Local lockout and rate-limit values must be positive. Their common defaults remain 5 failed attempts over 15 minutes, a 15-minute lockout, and 20 permits per 5 minutes.
- `DataProtection:ApplicationName` must be non-empty and stable. Production `DataProtection:KeyPath` must be an absolute persistent path outside the deployment directory. The application configures filesystem persistence but does not select key encryption at rest.
- `ConnectionStrings:KnowledgeHub` must contain a valid SQLite connection string with an absolute persistent Data Source in Production. `:memory:`, URI/relative Data Sources, repository `App_Data`, build output, and publish directories are not supported Production locations.
- `Persistence:Sqlite:DefaultTimeoutSeconds` defaults to 5 and must be 1–300. `BusyTimeoutMilliseconds` defaults to 5000 and must be 1–300000. `foreign_keys=ON` and `journal_mode=WAL` remain enforced in code and cannot be disabled by ordinary configuration.
- `Attachments:StorageRoot` must be a non-root absolute persistent path outside the deployment directory in Production. It must remain private application data and must never be mapped as a public/static directory.
- `Attachments:AllowedImageExtensions` and `AllowedFileExtensions` are comma-separated deployment allowlists. Every entry must be a lowercase canonical extension from the code-owned `AttachmentFilePolicy` safety catalog and must match its Image/File kind. Unsupported, misclassified, or duplicate entries fail startup; configuration cannot define extension-to-MIME, PreviewMode, or Recognition mappings.
- Attachment size, count, preview, multipart memory threshold, and multipart overhead values must be positive and remain within code-owned safety ceilings. Kestrel `MaxRequestBodySize` and Form `MultipartBodyLengthLimit` are both derived from `max(MaxImageBytes, MaxFileBytes) + UploadRequestOverheadBytes`; there is no independent third upload-size limit.
- `DatabaseDiscovery` timeouts, limits, queue polling, lease, heartbeat, and the SQL Server certificate-trust switch are typed. Heartbeat must remain shorter than the lease, and `QueuePollIntervalMilliseconds` must not equal `HeartbeatIntervalSeconds × 1000`. `SqlServerTrustServerCertificate` defaults to `false`; keep certificate-chain validation enabled in Production. Setting it to `true` is limited to an explicitly isolated/self-signed verification target and is never a Connection Profile field or user-supplied provider option.
- `Cors:AllowedOrigins` accepts only distinct explicit HTTP(S) origins without wildcard, credentials, path, query, or fragment. Development requires at least one origin. Production is not opened by this list because the CORS middleware is Development-only.
- Serilog requires a default level of `Information` or higher, explicit `Microsoft`, `Microsoft.AspNetCore`, and `Microsoft.EntityFrameworkCore.Database.Command` overrides of `Warning` or higher, and rejects lower child Microsoft overrides. This prevents deployment configuration from enabling framework request/action-argument or EF command detail. Console and rolling File sinks remain required; file path, rolling interval, retained file count, file-size limit, and roll-on-size setting are also validated. Sensitive-data logging remains prohibited at every allowed level.
- Known configuration failures write an actionable diagnostic to stderr and exit `1`. They do not fall back to Development, Local authentication, anonymous access, temporary keys, or the repository database.
- Normal Production Host startup does not automatically migrate or seed. Existing administrator bootstrap commands migrate only when explicitly invoked; a deployment-owned backup/migration/rollback procedure remains required.

The following stay in code and are not deployment tuning: Viewer/Editor/Administrator semantics, last-usable-Administrator protection, password rules other than the bounded hasher work factor, Oracle 19c-only and approved `ALL_*` query gates, canonical Snapshot/Diff identity, routes/error codes, file recognition/signatures, archive/XML safety ceilings, and SQLite foreign-key/WAL enforcement.

## Environment-variable mapping

These PowerShell examples are templates. Replace paths and non-secret provider values with approved deployment values. Keep secrets in the deployment secret mechanism.

For local Development only, the declared `UserSecretsId` supports standard commands such as the following placeholder example; the value is stored in the current user's profile and is not committed:

```powershell
dotnet user-secrets set 'Authentication:Oidc:ClientSecret' '<LOCAL_DEVELOPMENT_SECRET>' --project src/SystemKnowledgeHub.Api
```

Do not use local User Secrets as Production evidence or copy their backing files into an artifact.

### Required Production placement and Host values

```powershell
$env:DOTNET_ENVIRONMENT = 'Production'
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5090'
$env:ConnectionStrings__KnowledgeHub = 'Data Source=D:\SystemKnowledgeHub\Data\system-knowledge-hub.db'
$env:DataProtection__ApplicationName = 'SystemKnowledgeHub'
$env:DataProtection__KeyPath = 'D:\SystemKnowledgeHub\DataProtection-Keys'
$env:Attachments__StorageRoot = 'D:\SystemKnowledgeHub\Attachments'
```

### Attachment policy, multipart, and preview tuning

The extension values are comma-separated scalar strings, not configuration arrays.

```powershell
$env:Attachments__AllowedImageExtensions = '.png,.jpg,.jpeg,.gif,.webp'
$env:Attachments__AllowedFileExtensions = '.pdf,.docx,.xlsx,.pptx,.txt,.log,.sql,.md,.csv,.json,.xml,.zip'
$env:Attachments__MaxImageBytes = '10485760'
$env:Attachments__MaxFileBytes = '52428800'
$env:Attachments__MaxStoredAttachmentsPerDocument = '100'
$env:Attachments__MemoryBufferThresholdBytes = '65536'
$env:Attachments__UploadRequestOverheadBytes = '2097152'
$env:Attachments__PreviewTextMaxBytes = '262144'
$env:Attachments__PreviewCsvMaxRows = '200'
$env:Attachments__PreviewCsvMaxColumns = '50'
$env:Attachments__PreviewCsvMaxCharacters = '262144'
$env:Attachments__PreviewSpreadsheetMaxWorkbookBytes = '10485760'
$env:Attachments__PreviewSpreadsheetMaxSheets = '20'
$env:Attachments__PreviewSpreadsheetMaxRows = '200'
$env:Attachments__PreviewSpreadsheetMaxColumns = '50'
$env:Attachments__PreviewSpreadsheetMaxSharedStringCharacters = '1048576'
```

### Database Discovery and SQLite tuning

```powershell
$env:DatabaseDiscovery__ConnectionTimeoutSeconds = '15'
$env:DatabaseDiscovery__CatalogCommandTimeoutSeconds = '60'
$env:DatabaseDiscovery__OverallTimeoutSeconds = '900'
$env:DatabaseDiscovery__QueuePollIntervalMilliseconds = '2000'
$env:DatabaseDiscovery__LeaseDurationSeconds = '30'
$env:DatabaseDiscovery__HeartbeatIntervalSeconds = '5'
$env:DatabaseDiscovery__MaximumIncludedSchemas = '128'
$env:DatabaseDiscovery__MaximumObjects = '25000'
$env:DatabaseDiscovery__MaximumColumns = '250000'
$env:DatabaseDiscovery__MaximumConstraintsAndIndexes = '250000'
$env:DatabaseDiscovery__MaximumSequences = '10000'
$env:DatabaseDiscovery__MaximumCanonicalSnapshotBytes = '134217728'
$env:DatabaseDiscovery__SqlServerTrustServerCertificate = 'false'
$env:Persistence__Sqlite__DefaultTimeoutSeconds = '5'
$env:Persistence__Sqlite__BusyTimeoutMilliseconds = '5000'
```

### Authentication runtime tuning

```powershell
$env:Authentication__Cookie__ExpireHours = '8'
$env:Authentication__Cookie__SlidingExpiration = 'true'
$env:Authentication__Local__PasswordHasher__IterationCount = '220000'
$env:Authentication__Local__Lockout__MaxFailedAttempts = '5'
$env:Authentication__Local__Lockout__WindowMinutes = '15'
$env:Authentication__Local__Lockout__DurationMinutes = '15'
$env:Authentication__Local__RateLimit__PermitLimit = '20'
$env:Authentication__Local__RateLimit__WindowMinutes = '5'
```

Local-only mode:

```powershell
$env:Authentication__Local__Enabled = 'true'
$env:Authentication__Oidc__Enabled = 'false'
```

OIDC-only mode:

```powershell
$env:Authentication__Local__Enabled = 'false'
$env:Authentication__Oidc__Enabled = 'true'
$env:Authentication__Oidc__Provider = 'YOUR_PROVIDER_KEY'
$env:Authentication__Oidc__Authority = 'https://YOUR_APPROVED_AUTHORITY'
$env:Authentication__Oidc__ClientId = 'YOUR_CLIENT_ID'
$env:Authentication__Oidc__ClientSecret = '<YOUR_SECRET>'
$env:Authentication__Oidc__CallbackPath = '/signin-oidc'
```

Both methods are supported when both `Enabled` values are explicitly `true`. No method creates a default credential. For Local mode, use the existing interactive `bootstrap-local-admin` command or a deployment-controlled stdin secret channel; never put a password in command arguments, JSON, logs, or this guide.

### Serilog tuning

The checked-in sink array is Console at index `0` and File at index `1`; environment overrides of an array must preserve that structure.

```powershell
$env:Serilog__MinimumLevel__Default = 'Information'
${env:Serilog__MinimumLevel__Override__Microsoft} = 'Warning'
${env:Serilog__MinimumLevel__Override__Microsoft.AspNetCore} = 'Warning'
${env:Serilog__MinimumLevel__Override__Microsoft.EntityFrameworkCore.Database.Command} = 'Warning'
$env:Serilog__WriteTo__1__Args__path = 'D:\SystemKnowledgeHub\Logs\system-knowledge-hub-.log'
$env:Serilog__WriteTo__1__Args__rollingInterval = 'Day'
$env:Serilog__WriteTo__1__Args__retainedFileCountLimit = '14'
$env:Serilog__WriteTo__1__Args__fileSizeLimitBytes = '104857600'
$env:Serilog__WriteTo__1__Args__rollOnFileSizeLimit = 'true'
```

The common default is `Information` for application logs and `Warning` for `Microsoft`, `Microsoft.AspNetCore`, and `Microsoft.EntityFrameworkCore.Database.Command`. This prevents the idle Discovery Worker poll from continuously producing EF `Executed DbCommand` Information events while preserving application Information/Warning/Error events. Do not enable sensitive-data logging or record passwords, secrets, connection strings, Authorization/Cookie values, Oracle descriptors, raw Oracle SQL/provider error messages, or request bodies.

The default relative file pattern is `logs/system-knowledge-hub-.log`, rolling daily and on 100 MiB size, retaining 14 files. A real deployment should override it to an approved durable path when logs must survive redeploys, provision least-privilege write/create ACLs, monitor capacity, and confirm collection/retention requirements. The File sink does not replace an approved central log transport or alerting system.

### Development-only CORS overrides

Configuration arrays use numeric indices. These examples are for Development only; they are not a mechanism for enabling Production CORS.

```powershell
$env:Cors__AllowedOrigins__0 = 'http://localhost:5173'
$env:Cors__AllowedOrigins__1 = 'http://127.0.0.1:5173'
```

## Attachment runtime capability

Authenticated clients can read `GET /api/runtime-capabilities/attachments`. The response contains only:

- `allowedImageExtensions`;
- `allowedFileExtensions`;
- `maxImageBytes`;
- `maxFileBytes`;
- `maxStoredAttachmentsPerDocument`.

It does not expose StorageRoot, server paths, MIME/Recognition mappings, preview internals, secrets, or connection information. The frontend uses this capability for file-input `accept`, early size/type checks, and user-facing type/limit hints. These checks are advisory: the API and code-owned `AttachmentFilePolicy` remain the final authority for exact size, extension, declared MIME, signature/content recognition, archive/XML safety, authorization, and persistence.

## Authentication session and local password lifecycle

- Cookie lifetime and sliding renewal come from `Authentication:Cookie`; changing them affects newly issued/renewed sessions but does not weaken request-time session revalidation.
- Every authenticated request revalidates the currently enabled authentication method, approved OIDC Provider when applicable, active identity/credential mapping, active canonical User, and method-scoped session version. Disabling Local or OIDC rejects existing cookies for that method on the next request with `401 session_expired` and clears the cookie.
- A Local credential marked `MustChangePassword` may access only the current-user projection, antiforgery token retrieval, current-user password change, and logout. Other APIs return `403 must_change_password`.
- `PUT /api/current-user/password` is available only to the current Local session. A successful change atomically replaces the hash, clears forced-change/failure/lock state, increments the Local credential session version, and clears the current cookie. OIDC sessions are unaffected.
- OIDC passwords remain owned by the enterprise identity provider. The application does not expose a Local password-change action for an OIDC-authenticated session.

## Starting the application

Development:

```powershell
dotnet run --project src/SystemKnowledgeHub.Api
```

Production direct executable after the complete environment has been configured:

```powershell
& '<PUBLISH_OR_DEPLOYMENT_DIRECTORY>\SystemKnowledgeHub.Api.exe'
```

The repository's `bin/Release/net8.0/SystemKnowledgeHub.Api.exe` is a framework-dependent build apphost for controlled verification, not a formal publish artifact. The repository does not define a final publish profile or delivery topology; deployment owners must not treat a build directory as a durable installation.

Ordinary automated/runtime verification must use `dotnet run --project src/SystemKnowledgeHub.Api` with an explicit supported environment, isolated port, task-owned SQLite, Data Protection, Attachment StorageRoot, and Serilog file path. Direct EXE startup is reserved for an explicit Production/direct-executable verification with complete fail-closed configuration.

## HTTPS and reverse-proxy boundary

Outside Development the authentication Cookie is always Secure and the app enables HTTPS redirection. If TLS terminates at a reverse proxy, the application must receive a trustworthy original scheme/host and the proxy trust list must be restricted to approved addresses or networks. This repository intentionally does not enable forwarded headers or trust arbitrary forwarded values because no proxy topology has been approved.

`ASPNETCORE_URLS=http://127.0.0.1:5090` is only an origin-binding example for an approved same-host TLS proxy. It must not be exposed publicly or cited as HTTPS verification. Direct Kestrel HTTPS, certificate loading, HSTS ownership, proxy header rules, public callback URLs, path-base behavior, and any required Production CORS policy must be defined and verified for the selected deployment.

Attachment uploads use authenticated multipart API requests and are never served from a public filesystem mapping. Antiforgery validation may buffer multipart bodies to the Host's protected temporary storage before the application streams them into its own same-root staging area. The service account's temporary directory therefore needs restricted ACLs and capacity for the derived maximum request size. The application subsequently enforces exact byte limits, content recognition, streaming SHA-256, opaque storage keys, and atomic same-filesystem moves.

## Attachment storage, backup, and recovery

SQLite owns attachment metadata and immutable revision references; `Attachments:StorageRoot` owns binary objects. They are one logical backup set. A valid operational backup must coordinate both locations under a write-quiesced or otherwise consistency-preserving procedure, capture SQLite with an SQLite-aware method including live WAL state, and capture the complete attachment object tree. Copying only SQLite or only the filesystem is not a recoverable System Knowledge Hub backup.

Restore the matched database/object pair to approved persistent paths, apply least-privilege ACLs, validate SQLite `integrity_check` and `foreign_key_check`, and verify referenced object size/SHA-256 before reopening writes. Do not edit opaque object names, reconstruct paths from display filenames, delete unreferenced objects manually, or treat a current-revision removal as an orphan: physical deletion is Administrator-only and zero-reference across all revisions.

## Operational checks before rollout

- Inject all required values from the deployment configuration/secret system and confirm no Secret or key material exists in the artifact, repository, command line, or logs.
- Provision SQLite, attachment, Data Protection, log, and protected temporary directories outside the deployment tree where required, with least-privilege ACLs, durable storage, capacity monitoring, backup ownership, and approved encryption at rest.
- Prepare the database through an approved backup/migration procedure; never point verification at repository `App_Data` or repository attachment objects.
- Confirm invalid typed values fail before normal startup with an actionable diagnostic; specifically exercise unsafe password-hasher values, unsupported attachment extensions, malformed CORS origins, invalid Discovery intervals, invalid SQLite timeouts, and invalid Serilog sink/retention settings.
- Confirm Console and rolling File events are produced, application Information/Warning/Error events remain available, and an idle Discovery Worker does not continuously emit EF `Executed DbCommand` Information events.
- Confirm the default Discovery queue poll is 2000 ms and that claim, heartbeat, cancellation, lease expiry, and recovery behavior remains correct.
- Confirm the authenticated attachment capability matches the effective deployment allowlists/limits, returns no internal path, drives frontend hints/prechecks, and does not bypass server-side recognition/security checks.
- Verify real HTTPS, callback registration, proxy trust, derived request-body limits, protected temporary-file capacity, Secure Cookie behavior, login/logout, restart cookie continuity, coordinated SQLite+attachment backup/restore, log redaction/transport/retention, alerting, and rollback.
- Confirm `/api/auth/options` returns the intended enabled methods, anonymous `/api/current-user` returns `401`, and anonymous runtime-capability access is not allowed.

Until these real-environment checks pass, report `NOT VERIFIED AS REAL PRODUCTION DEPLOYMENT` and keep `SEC-04 BLOCKED`.
