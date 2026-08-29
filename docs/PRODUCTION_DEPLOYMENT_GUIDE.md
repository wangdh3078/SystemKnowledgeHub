# System Knowledge Hub Production Deployment Configuration Guide

## Status and boundary

This guide defines the repository-supported configuration boundary for `SystemKnowledgeHub.Api`. It does not select an IIS, Nginx, Caddy, YARP, container, cloud, certificate, secret-store, backup, or monitoring implementation. A localhost HTTP smoke test is not a real Production deployment approval; SEC-04 remains blocked until the chosen environment supplies and verifies those controls.

The checked-in `appsettings.Production.json` contains structure and fail-closed values only. It contains no usable credential, Client Secret, machine path, database path, attachment-storage path, or Data Protection key material.

## Configuration model and precedence

`Program.cs` uses `WebApplication.CreateBuilder(args)` without a custom configuration provider. The project has no `UserSecretsId` and no custom configuration setup. The effective application configuration, from lower to higher priority, is:

1. `appsettings.json`;
2. `appsettings.{Environment}.json`;
3. Development User Secrets when configured by a developer (none is configured by this project);
4. environment variables, using `__` for nested keys;
5. command-line configuration.

The Host environment is supplied by the standard ASP.NET Core host inputs. Deployment should set both `DOTNET_ENVIRONMENT` and `ASPNETCORE_ENVIRONMENT` consistently to `Production`; command-line input has higher priority. `Properties/launchSettings.json` is a local tooling profile and is not loaded by a directly launched executable.

| Concern | Development | Testing | Production |
| --- | --- | --- | --- |
| Environment source | `launchSettings.json` for `dotnet run`, or explicit host input | Test host explicitly selects `Testing` | Explicit deployment input; direct EXE otherwise defaults to Production |
| Local authentication | Enabled only when the key is absent; no default account/password | Test factory controls the method | Must be explicitly enabled or disabled |
| OIDC | Disabled by base configuration unless explicitly configured | Test fixtures may use non-production values | Must be explicitly enabled or disabled; Provider, Authority and ClientId required when enabled |
| Authentication boundary | At least one method outside Testing | Test-only schemes are isolated to tests | At least one of Local/OIDC; otherwise exit `1` |
| Data Protection | Framework development fallback unless a path is supplied | Test-owned ephemeral/key directory | Absolute persistent path outside the deployment directory is required |
| SQLite | Relative base path resolves against API Content Root | Test-owned SQLite | Absolute persistent Data Source path is required |
| Attachment binaries | `App_Data/attachments` below API Content Root | Fixture-owned absolute temporary directory | Explicit absolute persistent filesystem root outside the deployment directory is required |
| Logging | Console, configured levels | Captured by test host | Console/stderr; external transport and retention are deployment-owned |
| URLs | launch profile uses `http://localhost:5090` | Isolated test port | Explicit `ASPNETCORE_URLS`; public HTTPS/proxy topology is deployment-owned |
| HTTPS/proxy | Development HTTP only | Not Production evidence | Secure Cookie and HTTPS redirection are enabled; trusted proxy topology is not guessed |

## Fail-closed Production rules

- `Authentication:Local:Enabled` and `Authentication:Oidc:Enabled` default to `false` in Production. At least one must be explicitly changed to `true`.
- OIDC requires non-empty Provider, Authority and ClientId. ClientSecret is configuration-bound but must come from the deployment secret source, never a committed JSON file.
- `DataProtection:ApplicationName` must be non-empty and stable. `DataProtection:KeyPath` must be an absolute path outside the application deployment directory. The location must persist across restarts/redeployments and deployment operators must restrict access and provide approved encryption at rest. The application currently configures filesystem persistence but does not select a key-encryption provider.
- `ConnectionStrings:KnowledgeHub` must contain a valid SQLite connection string with an absolute, persistent Data Source. `:memory:`, URI/relative Data Sources, repository `App_Data`, build output and publish directories are not supported Production locations.
- `Attachments:StorageRoot` must be a non-root absolute persistent path outside the application deployment directory. Production startup fails when the value is empty, relative, inside the deployment tree, or a filesystem root. The directory must be private application data: never map it as a static/public web directory.
- Attachment limits must be valid positive integers within the application safety ceilings. The defaults are 10 MiB for images, 50 MiB for ordinary files, 100 stored metadata rows per document, 256 KiB for text preview, 200×50 for CSV, and 20 sheets/200×50 cells with a 10 MiB workbook preview ceiling for XLSX. Kestrel's request-body ceiling is derived from the larger configured upload limit plus multipart overhead; any upstream proxy ceiling must be at least as large.
- Known startup configuration failures write an actionable message to stderr and exit `1`. They do not fall back to Development, Local authentication, anonymous access, temporary keys, or the repository database.
- Normal Production host startup does not automatically migrate or seed the database. The existing administrator bootstrap commands migrate only when an operator explicitly invokes them. A deployment-owned migration/backup/rollback procedure remains required before real rollout.

## Environment-variable mapping

The following PowerShell examples are templates. Replace paths and non-secret identity-provider values with approved deployment values; keep secrets in the deployment secret source.

```powershell
$env:DOTNET_ENVIRONMENT = 'Production'
$env:ASPNETCORE_ENVIRONMENT = 'Production'
$env:ASPNETCORE_URLS = 'http://127.0.0.1:5090'
$env:ConnectionStrings__KnowledgeHub = 'Data Source=D:\SystemKnowledgeHub\Data\system-knowledge-hub.db'
$env:DataProtection__ApplicationName = 'SystemKnowledgeHub'
$env:DataProtection__KeyPath = 'D:\SystemKnowledgeHub\DataProtection-Keys'
$env:Attachments__StorageRoot = 'D:\SystemKnowledgeHub\Attachments'
$env:Attachments__MaxImageBytes = '10485760'
$env:Attachments__MaxFileBytes = '52428800'
$env:Attachments__MaxStoredAttachmentsPerDocument = '100'
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

## Starting the application

Development:

```powershell
dotnet run --project src/SystemKnowledgeHub.Api
```

Production direct executable after the environment has been configured:

```powershell
& '<PUBLISH_OR_DEPLOYMENT_DIRECTORY>\SystemKnowledgeHub.Api.exe'
```

The repository's `bin/Release/net8.0/SystemKnowledgeHub.Api.exe` is a framework-dependent build apphost used for controlled verification. It is not a formal publish artifact. The repository does not currently define a publish profile or final delivery topology, so deployment owners must not treat the build directory as a durable Production installation.

## HTTPS and reverse-proxy boundary

Outside Development the authentication Cookie is always Secure and the app enables HTTPS redirection. If TLS terminates at a reverse proxy, the application must receive a trustworthy original scheme/host and the proxy trust list must be restricted to approved addresses or networks. This repository intentionally does not enable forwarded headers or trust arbitrary forwarded values because no proxy topology has been approved.

`ASPNETCORE_URLS=http://127.0.0.1:5090` is only an origin-binding example for an approved same-host TLS proxy. It must not be exposed publicly or cited as HTTPS verification. Direct Kestrel HTTPS, certificate loading, HSTS ownership, proxy header rules, public callback URLs and path-base behavior must be defined and verified for the selected deployment.

Attachment uploads use authenticated multipart API requests and are never served from a public filesystem mapping. Antiforgery validation may buffer multipart bodies to the host's protected temporary storage before the application streams them into its own same-root staging area, so the service account's temporary directory must have restricted ACLs and capacity for the configured maximum request size. The application subsequently enforces its own exact byte limit, content recognition, streaming SHA-256, opaque storage key, and atomic same-filesystem move.

## Attachment storage, backup, and recovery

SQLite owns attachment metadata and immutable revision references; `Attachments:StorageRoot` owns binary objects. They are one logical backup set. A valid operational backup must coordinate both locations under a write-quiesced or otherwise consistency-preserving procedure, capture SQLite with an SQLite-aware method (including any live WAL state), and capture the complete attachment object tree. Copying only SQLite or only the filesystem is not a recoverable System Knowledge Hub backup.

Restore the matched database/object pair to approved persistent paths, apply least-privilege ACLs, validate SQLite `integrity_check` and `foreign_key_check`, and verify referenced object size/SHA-256 before reopening writes. Do not edit opaque object names, reconstruct paths from display filenames, delete unreferenced objects manually, or treat a current-revision removal as an orphan: physical deletion is Administrator-only and zero-reference across all revisions.

## Operational checks before rollout

- Inject all required values from the deployment configuration/secret system and confirm no secret or key material is present in the artifact or repository.
- Provision the SQLite directory, attachment storage root and Data Protection directory outside the deployment tree with least-privilege ACLs, durable storage, backup ownership and approved encryption at rest. The API service account requires read/write/create/delete access to its private attachment root; web/proxy/static-file identities do not.
- Prepare the database through an approved backup/migration procedure; do not point verification at repository `App_Data`.
- Verify real HTTPS, callback registration, proxy trust, request-body limits, protected temporary-file capacity, Secure Cookie behavior, login/logout, restart cookie continuity, logging transport/redaction, coordinated SQLite+attachment backup/restore and rollback.
- Confirm `/api/auth/options` returns the intended enabled methods and anonymous `/api/current-user` returns `401`.

Until these real-environment checks pass, report `NOT VERIFIED AS REAL PRODUCTION DEPLOYMENT` and keep `SEC-04 BLOCKED`.
