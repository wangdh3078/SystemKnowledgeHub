# DBDISC-SQLSERVER-B01 — SQL Server Provider & Third-provider Proof Verification Report

## Result

**DBDISC-SQLSERVER-B01 PASS**

The SQL Server adapter completes the existing Connection Profile → Test Connection → durable Run → Canonical Snapshot → Difference pipeline and the existing B04 Reconciliation → Preview → Confirm → Apply pipeline. SQL Server differences remain inside `Providers/SqlServer`; no SQL Server-specific persistence table, EF migration, Canonical/Diff/Reconciliation/Apply branch, DDL execution, business-row import, or automatic Sync was introduced.

| Final gate | Result |
| --- | --- |
| SQL Server Test Connection | PASS |
| Real SQL Server discovery | PASS |
| Canonical Snapshot | PASS |
| Real Snapshot / Difference | PASS |
| FK reference closure | PASS |
| Identifier / collation semantics | PASS |
| Native type mapping | PASS |
| Index mapping | PASS |
| Sequence volatility | PASS |
| Low-privilege metadata access | PASS |
| No business-row read | PASS |
| Failure / redaction | PASS |
| Cancellation / timeout | PASS |
| Third-provider Core proof | PASS |
| B04 SQL Server Sync proof | PASS |
| Human knowledge protection | PASS |
| Oracle regression | PASS |
| PostgreSQL regression | PASS |
| Repository data protection | PASS |
| Docker cleanup | PASS |

## Frozen implementation boundary

- Supported database is explicitly **SQL Server 2022, product major 16**. Other majors fail as `UnsupportedDatabaseVersion`; this report does not claim generic or untested SQL Server support.
- The new implementation consists of an `IDatabaseConnectionTester`, an `IDatabaseDiscoveryProvider`, and a closed SQL Server catalog reader selected by the existing provider resolvers.
- Profile fields remain `ProviderType=SqlServer`, `Host`, `Port`, `DatabaseName`, `Username`, `Password`, and `IncludedSchemas`; `ServiceName` remains null.
- The frontend exposes SQL Server, defaults its convenience port to 1433, uses `DatabaseName`, hides `ServiceName`, and reuses the existing Run/Snapshot/Difference/Manual Sync views.
- No raw connection string, arbitrary provider option, user SQL, SQL execution console, generic plugin framework, second DbContext, SQL Server-specific read UI, or vendor persistence was added.

## SQL Server image and version

| Item | Verified value |
| --- | --- |
| Image | `mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04` |
| Image digest | `sha256:ba4c8329f48fb8f02e1416be6a930ebfd71268caee78aa985f3af4315e457c89` |
| Architecture | `amd64` |
| Product version | `16.0.4265.3` |
| Product major | `16` |
| Edition | `Developer Edition (64-bit)` |
| Fixture database collation | `Latin1_General_100_CS_AS_SC_UTF8` |

The exact Microsoft image tag was reused after its digest was checked; `latest` and third-party images were not used. The provider freezes major 16 in code.

## SqlClient driver

| Item | Decision |
| --- | --- |
| Package | `Microsoft.Data.SqlClient` |
| Version | `7.0.2` |
| Maintenance / license | Microsoft-maintained official .NET SQL Server driver; MIT license |
| Vulnerability gate | `dotnet list ... package --vulnerable --include-transitive` — PASS, no vulnerable packages reported |

Connections are built only with `SqlConnectionStringBuilder`. Encryption is mandatory, pooling and `PersistSecurityInfo` are disabled, and no raw fragment can enter the builder. `SqlServerTrustServerCertificate` is a typed deployment option that defaults to `false`; it is not present in Profile/API payloads. The real self-signed task container alone enabled it through isolated test configuration.

## Connection Test

The SQL Server tester uses the existing `DatabaseDiscoveryOptions` connection/catalog timeouts and validates:

- TCP/session connection and authentication;
- current database and product version/major;
- the frozen major-16 gate;
- exact IncludedSchemas visibility;
- access to the closed Core catalog query set.

The real API returned product version `16.0.4265.3`, database `SkhDiscovery`, and no ServiceName. Test Connection created no Run, Snapshot, or Difference. Bad credentials returned only `AuthenticationFailed`, safe token `MSSQL-18456`, and a normalized summary.

## Low-Privilege Grant Matrix

The task container administrator was used only to create the database and the two separated fixture principals. Discovery ran as `skh_discovery_reader`, never as `sa` or the fixture owner.

| Discovery permission / membership | Real result |
| --- | --- |
| `sysadmin` | NO |
| `db_owner` | NO |
| `db_datareader` | NO |
| `db_datawriter` | NO |
| `CONNECT` on task database | YES |
| `VIEW DEFINITION ON SCHEMA::dbdisc_a` | YES |
| `VIEW DEFINITION ON SCHEMA::dbdisc_b` | YES |
| `VIEW DEFINITION ON SCHEMA::dbdisc_ref` | YES, only for FK target closure |
| `VIEW DEFINITION ON SCHEMA::dbdisc_denied` | NO |
| `SELECT` on business canary table | NO; real query failed with SQL Server error 229 |
| DML / DDL grants | NONE |

No database-wide `GRANT VIEW DEFINITION TO user` was needed.

## Catalog Query Inventory

The static and real-command inventory is closed to:

- `sys.schemas`;
- `sys.tables` / `sys.views` / `sys.objects`;
- `sys.columns`, `sys.types`, `sys.default_constraints`;
- `sys.key_constraints`, `sys.foreign_keys`, `sys.foreign_key_columns`;
- `sys.indexes`, `sys.index_columns`;
- `sys.sequences`;
- `sys.extended_properties` restricted to `MS_Description`;
- bounded target/version/database metadata functions.

Included schema values are passed as command parameters; they are not concatenated as identifiers. The commands contain no business-table read, DML, DDL, lock clause, user SQL, or full CREATE DDL extraction. System/internal objects are excluded with catalog flags, not name prefixes; normal user schema `dbo` is not special-cased away.

## Core Mapping

### Schema, Table, View, Columns, types, defaults, and comments

- Exact catalog identifier text and source ordinals are retained; no `ToUpper`, `ToLower`, or `OrdinalIgnoreCase` is applied to technical identity.
- Tables and views are mapped to the existing provider-neutral object kinds.
- Column nullability, bounded default expressions, length, precision, scale, `MAX`, and source-native type declarations map into the existing `NativeDataType` contract.
- Real fixtures passed for `int`, `bigint`, `decimal(12,2)`, `varchar(100)`, `nvarchar(100)`, `nvarchar(max)`, `bit`, `date`, `datetime2(3)`, `datetimeoffset(3)`, `uniqueidentifier`, and `varbinary(16)`.
- Schema-owned alias types retain alias schema/name plus their underlying native declaration. Unsafe/unrepresentable native types fail closed as `UnsupportedNativeType`; no `SqlServerDataType` escaped the adapter.
- Only `MS_Description` is mapped, separately for objects and columns. Other extended properties are not copied into Canonical metadata.

### Identifier and collation semantics

The real case-sensitive UTF-8 collation fixture preserved `[CaseSensitiveTable]` and `[MiXeDColumn]`. Length-delimited opaque logical identities are deterministic and case-sensitive inside the provider. The provider relies on the actual SQL Server catalog to resolve names; Core does not reimplement database collation. Any identity ambiguity that cannot be represented safely fails as `UnsupportedIdentifierCollision`; no suffix, normalization, merge, or rename inference is performed.

### PK, Unique, FK, and reference closure

- Single and multi-column primary keys and unique constraints retain ordered columns.
- Same-schema and cross-schema foreign keys retain ordered owning/referenced columns and update/delete rules.
- The real `dbdisc_a → dbdisc_ref` FK generated a `ReferenceOnly` closure entry for the referenced schema/object/columns even though `dbdisc_ref` was not in IncludedSchemas.
- Closure entities do not enlarge IncludedSchemas, do not become ordinary objects/columns or standalone Differences, and do not become B04 candidates.
- Missing closure visibility fails closed as `UnresolvedForeignKeyReference` or `InsufficientPrivilege`; an FK is never silently discarded.
- Unique constraints remain structural review metadata and are not written to `BusinessKeyColumnsJson`.

### Indexes

Supported SQL Server index families are rowstore clustered and nonclustered indexes (`type` 1/2) that the current Canonical Index can express. The real fixture proved ordinary, unique, INCLUDE, filtered, and clustered-primary backing indexes. Key ordering/direction and INCLUDE columns are distinct. `key_ordinal=0` is interpreted together with index family, `is_included_column`, and partition ordinal; it is never treated unconditionally as INCLUDE.

Filtered predicates are retained as bounded native structural expressions and are neither executed nor interpreted as business semantics. Columnstore, XML, spatial, hash, and other specialized families fail the required Core Run as `UnsupportedIndexFamily`; they are not silently skipped or reported as a successful unavailable detail.

### Sequence volatility

The provider maps structural sequence type, start, increment, min, max, cycle, and cache fields. `current_value` and `last_used_value` are absent from the query result records and Canonical mapping. The real test advanced `NEXT VALUE FOR` between snapshots without changing the definition; the subsequent Difference reported the Sequence as `Unchanged`.

## Capabilities

The existing provider-neutral `Supported` / `NotSupported` / `Unavailable` / `NotApplicable` contract is reused.

| Capability | SQL Server 2022 Core state | Reason |
| --- | --- | --- |
| Identity Columns | NotSupported | Current Core column contract does not project identity behavior |
| Computed Columns | NotSupported | Current Core column contract does not project the computed expression |
| Invisible Columns | NotSupported | SQL Server 2022 Core does not provide the frozen concept |
| Materialized Views | NotApplicable | Indexed views are outside this Core scope |
| Partitions | NotSupported | Outside current Core projection |
| Sequences | Supported | Structural fields are mapped; runtime value excluded |
| Synonyms | NotSupported | Outside current Core scope |
| Triggers | NotSupported | Outside current Core scope |
| Container Database | NotApplicable | Oracle-specific concept |
| Full DDL | NotSupported | Deliberately excluded |

No SQL Server-specific capability response shape was added, and unsupported optional capability details are not mislabeled as errors.

## Real Docker State 1 / State 2

The environment-gated test executed against a loopback-only isolated port and a task-owned database, volume, and network.

State 1 covered tables, views, object/column comments, single and multi-column PK/UQ/FK, same/cross-schema FK, FK reference closure, ordinary/unique/INCLUDE/filtered indexes, Sequence, exact mixed-case identifiers, defaults, and all required native types. The production path created a DatabaseSource, Profile, separately protected Secret, successful Test Connection, committed Queued Run, Worker claim, SQL Server discovery, immutable Snapshot/Difference, and terminal Succeeded Run. `BaseSnapshotId` was null and Core identities were Added.

A repeat unchanged discovery used the first Snapshot as baseline and returned only Unchanged entries. State 2 changed an object comment, added objects, dropped objects, modeled a rename as old Missing plus new Added, retained unchanged entities, and advanced only Sequence runtime state. Its compatible baseline was the repeat Snapshot and the durable Difference contained Added, Changed, MissingFromSource, and Unchanged concurrently; no rename was inferred.

Post-test database inspection found all four fixture schemas and all three State 2 marker tables, proving the environment-gated real test executed rather than taking its disabled-environment early return.

## Failure, redaction, cancellation, and timeout

- Bad password: `AuthenticationFailed`, safe `MSSQL-18456`, no Snapshot.
- Schema without metadata grant: `InsufficientPrivilege`, no Snapshot.
- Deterministic unsupported major: `UnsupportedDatabaseVersion`, no Snapshot.
- Deterministic catalog fault/specialized index/native-type ambiguity: normalized fail-closed code, no successful Snapshot/Difference.
- Queued cancellation: `Cancelled`, no Snapshot/Difference.
- Timeout: `Failed / Timeout`, no Snapshot/Difference; it does not regress to `RunInterrupted`.
- Worker lease heartbeat/recovery remains the generic Oracle-R01-corrected implementation.

Only a tightly validated `MSSQL-<digits>` token is permitted across the durable/API safety boundary. Raw `SqlException.Message`, SQL, descriptor/server string, connection string, usernames, passwords, and raw provider errors are excluded from Run/API/audit/log summaries.

The business-row canary produced zero hits across API payloads, Run/Snapshot/Difference content, task SQLite/WAL/SHM, captured Serilog, test stdout/stderr, and Docker logs. The Discovery principal's direct business-table SELECT was independently denied.

## Third-provider and B04 proof

Static audit found SQL Server selection only in the tester/provider resolver and the explicit `Providers/SqlServer` adapter. `CanonicalSnapshotService`, validation/serialization/hash, Scope generation/fingerprint, baseline/Difference, Worker, persistence, reconciliation, plan, preview, apply, and bindings contain no SQL Server business-structure branch.

The existing B04 API regression now executes its provider-neutral proof for both PostgreSQL and SQL Server. The real SQL Server Snapshot additionally produced:

- `CreateDatabaseObject` plus `CreateDatabaseColumn` actions;
- a link/update target;
- deterministic Preview;
- explicit Confirm;
- atomic Apply;
- successfully created/updated `DatabaseObject` and `DatabaseColumn` technical fields.

No SQL Server condition was added to B04. Existing BusinessDescription, AccessMode, BusinessKeyColumnsJson, KnowledgeStatus, soft-delete state, column BusinessDescription/KnownValue, DatabaseComment evidence, and HumanConfirmation evidence remained unchanged. FK, unique detail, index, and Sequence remained Snapshot/Diff/Review-only and never became automatic knowledge relations, business keys, or generic metadata entities.

## Verification

| Check | Result |
| --- | --- |
| Release build | PASS — 0 warnings, 0 errors |
| SQL Server focused deterministic tests | PASS — Connection/version/catalog/identity/type/comment/constraint/FK/index/sequence/failure/redaction/timeout coverage |
| Real SQL Server Docker integration | PASS — 1/1; post-test fixture markers verified |
| Provider-neutral B04 focused regression | PASS |
| Approved serial full backend | PASS — 409/409 |
| Oracle / PostgreSQL provider regression | PASS — 48/48 focused, also included in full backend gate |
| EF `has-pending-model-changes` | PASS — no model changes; no migration added |
| Package vulnerability scan | PASS — no vulnerable packages reported |
| Frontend focused | PASS — 7 files / 43 tests |
| Full frontend | PASS — 85 files / 509 tests |
| Frontend type-check | PASS |
| Frontend production build | PASS — existing chunk-size advisory only |
| Affected lint | PASS |
| Affected Prettier | PASS — all nine touched frontend files |
| `git diff --check` | PASS |

The complete backend gate uses the repository-approved task-owned serial runsettings for existing deferred `REV-GAP-011`; focused and full totals overlap and are not summed.

## Repository Safety

All external/database runtime verification used task-owned SQL Server/SQLite, Data Protection, attachment, log, credential, and port state. The repository-owned database was never opened by the verification runtime.

| Protected file | Start | End |
| --- | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | exists; 1,052,672 bytes; mtime `2026-08-31T16:22:52.0160774Z`; SHA-256 `0A3F52A89BDE6C656445F95A3341CB9041D343394759B92467798750B68C89A8` | byte-for-byte identical |
| repository `*.db-wal` | absent | absent |
| repository `*.db-shm` | absent | absent |

No repository checkpoint, migration, WAL/SHM deletion, hash restoration, or user-process stop was performed.

## Cleanup

- Removed only `skh-dbdisc-sqlserver-b01*` containers, volumes, networks, fixture database, generated credentials, and isolated port bindings.
- Removed task SQLite/WAL/SHM, keys, attachments, logs, and temporary serial runsettings.
- Docker log redaction scan passed; no task resource remains.
- The pinned official SQL Server image remains cached as required.
- No browser or Codex process was closed.

## Remaining Gaps

- `REV-GAP-011` remains existing/open/deferred; the approved deterministic serial backend gate is still required.
- Full-repository Prettier remains an existing baseline issue in 163 unrelated files. Every frontend file touched by this task passes Prettier; unrelated files were not rewritten.
- Major 16 is the only supported SQL Server major. Identity/computed behavior, partitions, synonyms, triggers, specialized indexes, and full DDL remain explicitly unsupported/out of scope rather than silently approximated.
- These bounded limitations are neither Blocker nor High for DBDISC-SQLSERVER-B01 and do not weaken complete-or-failed Core discovery.

## Final Status

```text
DBDISC-SQLSERVER-B01 PASS

SQL SERVER IMAGE: mcr.microsoft.com/mssql/server:2022-CU26-ubuntu-22.04
VERIFIED SQL SERVER VERSION: 16.0.4265.3 (major 16)
SQLCLIENT VERSION: 7.0.2

SQL SERVER TEST CONNECTION: PASS
SQL SERVER REAL DISCOVERY: PASS
CANONICAL SNAPSHOT: PASS
REAL SNAPSHOT / DIFF: PASS
FK REFERENCE CLOSURE: PASS

IDENTIFIER / COLLATION SEMANTICS: PASS
NATIVE TYPE MAPPING: PASS
INDEX MAPPING: PASS
SEQUENCE VOLATILITY: PASS

LOW-PRIVILEGE METADATA ACCESS: PASS
NO BUSINESS-ROW READ: PASS
FAILURE / REDACTION: PASS
CANCELLATION / TIMEOUT: PASS

THIRD-PROVIDER CORE PROOF: PASS
B04 SQL SERVER SYNC PROOF: PASS
HUMAN KNOWLEDGE PROTECTION: PASS

ORACLE REGRESSION: PASS
POSTGRESQL REGRESSION: PASS
REPOSITORY DATA PROTECTION: PASS
DOCKER CLEANUP: PASS

DBDISC-SQLSERVER-B01 COMPLETE
DBDISC-VERIFY READY: YES
```
