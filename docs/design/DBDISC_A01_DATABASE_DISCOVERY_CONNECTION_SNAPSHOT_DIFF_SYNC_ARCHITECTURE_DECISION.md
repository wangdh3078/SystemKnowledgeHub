# DBDISC-A01 — Database Discovery / Connection / Snapshot / Diff / Manual Sync Architecture Decision

Status: **Frozen Design Decision — Approved**

Product: **系统知识中心 / System Knowledge Hub**

Date: **2026-08-29**

Scope: **PHASE-DB-DISCOVERY architecture, contract direction, and implementation sequencing only**

## 1. Result

```text
DBDISC-A01 APPROVED
Blocking human decisions: NONE
DBDISC-B01 READY: YES
```

This decision freezes the smallest coherent architecture for reading structural metadata from external databases, retaining immutable observations, calculating deterministic differences, and applying selected structural facts to System Knowledge Hub only after an explicit human confirmation.

It does not implement an Oracle, PostgreSQL, or SQL Server production Provider; a migration; a connection or password store; a Discovery API; Snapshot/Diff persistence; Manual Sync; a UI; a Docker composition; or a package change.

The approval is an Internal Pilot architecture/development decision. It is not a real-company Oracle 19c acceptance result and not Production approval. The existing `SEC-04` HTTPS/proxy/protected-key/operations gate remains open. Oracle 10g and 11g are explicitly out of scope after the requester confirmed that the .NET 8 product should support Oracle 19c only.

## 2. Context, authority, and inspected baseline

The starting branch is `main`. At inspection time it was clean, at `d29e984`, and synchronized with `origin/main`.

The applicable authorities and evidence are:

- root `AGENTS.md` and `docs/DOCUMENT_INDEX.md`;
- the frozen MVP Domain, Database, Application Use Case, API, Solution Structure, UI Inventory, and Design Baseline specifications;
- the approved `SEC_A01_SECURITY_ACCESS_CONTROL_DESIGN_REVIEW.md`, `DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md`, and current authentication/authorization/soft-delete implementation;
- `PRODUCTION_DEPLOYMENT_GUIDE.md`, `SEC_04_SECURITY_ROLLOUT_VERIFICATION_REPORT.md`, and `PRODUCTION_DEPLOYMENT_CONFIGURATION_R01_VERIFICATION_REPORT.md`;
- `VS01`, `VS12A`, and `VS12B` Database Knowledge implementation reports;
- `SYSTEM_FULL_REGRESSION_R01_VERIFICATION_REPORT.md`, which passed the full technical baseline;
- `ATTACH_VERIFY_FINAL_VERIFICATION_REPORT.md`, which records `ATTACH-VERIFY PASS`, `PHASE-ATTACHMENTS COMPLETE`, and `NEXT ROADMAP PHASE READY: YES`;
- current `DatabaseSource`, `DatabaseObject`, `DatabaseColumn`, EF mappings, Controllers, application services, concurrency codec, migrations, and focused tests.

Current implementation facts that constrain this design:

- `DatabaseSource` is the knowledge-tree identity of a database under one System. Its frozen model explicitly does not store passwords, connection keys, or runtime monitoring state.
- `DatabaseObject` supports `Table` and `View`, has `SchemaName`, technical summary fields, human knowledge fields, `KnowledgeStatus`, ownership, Version concurrency, and soft delete.
- `DatabaseColumn` has stable ID, ordinal, raw `DataType`, nullability, default, `DatabaseComment`, human `BusinessDescription`, `KnowledgeStatus`, Known Values, Version concurrency, and soft delete.
- current active Object and Column uniqueness uses SQLite `NOCASE`; this cannot faithfully store every external database's quoted/case-sensitive identifier set.
- C11 and C12 edit business knowledge only. They do not authorize a generic technical-metadata patch.
- current soft delete is an explicit, authorized, dependency-guarded, Version-checked use case. Source disappearance is not deletion.
- the backend remains one .NET 8 ASP.NET Core application with feature-first code, direct `KnowledgeHubDbContext`, EF Core, and SQLite. The frontend remains Vue 3 strict TypeScript with native `fetch` and Element Plus.
- current Data Protection configuration provides a stable application name and an optional persistent key-ring path. No business-secret protector and no application-selected key-at-rest encryption mechanism exists today.

No frozen source was modified by this decision. DBDISC-A01 is a later, capability-specific extension. Where a future implementation requires new schema or API contracts, the applicable DBDISC implementation slice must add them explicitly rather than rewriting the MVP source to pretend they already existed.

## 3. Goals

DBDISC-A01 freezes:

1. a strict separation between System Knowledge Hub persistence and external Database Discovery Providers;
2. a provider-neutral Connection Profile, Secret boundary, connection test, capability, and Discovery boundary;
3. a complete, immutable, provider-neutral `DatabaseDiscoverySnapshot` for the approved Core metadata scope;
4. durable Run, Snapshot, Difference, and Manual Sync Plan history;
5. deterministic Added, Changed, MissingFromSource, and Unchanged semantics;
6. provider-owned stable-identity and identifier-comparison rules without Oracle/PostgreSQL/SQL Server catalog leakage;
7. explicit Source Missing semantics that never delete or auto-archive Knowledge Hub entities;
8. a Preview → Confirm → Apply Manual Sync flow that protects human knowledge and uses optimistic concurrency;
9. Oracle 19c as the only Oracle implementation and verification target;
10. early proof that the abstraction can also support PostgreSQL and later SQL Server without changing the core model;
11. read-only, metadata-only, least-privilege, timeout, cancellation, limit, concurrency, error, observability, and redaction rules;
12. one executable follow-up task sequence ending in an honest evidence-based final gate.

## 4. Non-goals

This phase does not add or approve:

- business-row sampling, profiling, value distribution, PII scanning, query execution against business tables, or data import;
- Oracle 10g or 11g connectivity, compatibility probes, legacy client packages, legacy runtime sidecars, or bridge processes;
- DDL, DML, write-back, schema repair, database object creation, or database migration against an external database;
- automatic synchronization, automatic HumanConfirmation, automatic Evidence, or automatic KnowledgeStatus progression;
- automatic soft delete, archive, or destructive reconciliation when an external object disappears;
- rename inference from name similarity, AI matching, fuzzy matching, schema inference, or DDL parsing;
- a generic plug-in marketplace/framework, generic repository, UnitOfWork, CQRS/MediatR, mapper framework, second `DbContext`, or replacement System Knowledge Hub persistence provider;
- multi-PDB orchestration, CDB-root fan-out, database links, cross-database orchestration, distributed transactions, or background scheduler;
- SID, raw Oracle connection descriptors, TNS alias administration, wallet/TCPS, client certificate, Kerberos, integrated authentication, proxy authentication, or external secret-manager production integration in the first implementation;
- Materialized View, Synonym, Trigger, Partition, virtual/invisible/identity-column behavior, or full DDL as a Core success dependency;
- a standalone Schema knowledge entity, generic Constraint/Index knowledge entities, or a generic Relationship mapping for database constraints;
- a migration, Provider package, Docker file, endpoint, UI route, or production deployment in A01.

## 5. Terminology and source-of-truth boundaries

| Term | Meaning | Authority |
| --- | --- | --- |
| System Knowledge Hub persistence provider | EF Core SQLite used by `KnowledgeHubDbContext` to persist product state | Existing frozen/current persistence architecture |
| External Discovery Provider | Vendor adapter that opens a read-only external connection and normalizes catalogs | DBDISC Provider boundary |
| DatabaseSource | Human-facing database identity and parent of DatabaseObject in one System | Existing Database Knowledge domain |
| DatabaseConnectionProfile | Administrator-managed operational connection and Discovery configuration for exactly one DatabaseSource | New DBDISC operational aggregate |
| Run | One accepted full Discovery catalog-read attempt with durable queued/running/terminal status | DBDISC operational ledger |
| Snapshot | One immutable, complete observation of an external database scope | DBDISC observation ledger |
| Difference | Deterministic comparison of two compatible complete Snapshots | Derived, reproducible DBDISC result |
| Sync Plan | Human-selected structural changes bound to one Difference and current Hub concurrency tokens | New explicit write intent |
| Manual Sync | One explicit Preview → Confirm → atomic Apply operation | Knowledge Hub structural projection update |
| Source Missing | A previous external identity is absent from the next compatible Snapshot | Discovery fact, never deletion |

The authoritative split is:

```text
External database current structure
        ↓ read-only observation
Immutable Discovery Snapshot history
        ↓ deterministic comparison
Discovery Difference
        ↓ human selection and confirmation
Knowledge Hub structural projection

Knowledge Hub human knowledge
        = remains authoritative only inside Knowledge Hub
```

Snapshots do not become a second mutable Knowledge Hub. The external database is authoritative for its current structural facts; Snapshots are immutable observations of that truth; Knowledge Hub may intentionally lag until a human applies a Sync Plan. `BusinessDescription`, business meaning, Evidence, HumanConfirmation, Known Values, relationships, and KnowledgeStatus are never sourced from Discovery.

## 6. Core principles and invariants

The following are frozen invariants:

1. **READ ONLY / METADATA ONLY.** Provider SQL is a closed, reviewed catalog-query set. User-supplied SQL is prohibited.
2. **No external mutation.** Test Connection and Discover issue no business-object query, DDL, DML, lock, repair, or write-back command.
3. **No catalog leakage.** `ALL_*`, `pg_catalog`, `information_schema`, and `sys.*` types/column names remain inside their Provider implementation.
4. **Complete Snapshot or no Snapshot.** A Core query failure, timeout, cancellation, limit breach, or persistence failure cannot create a successful or baseline-eligible Snapshot.
5. **Immutable history.** Successful Snapshot content, Difference inputs, and applied Sync Plan payloads are not edited in place.
6. **Human confirmation.** Discovery never mutates `DatabaseSource`, `DatabaseObject`, or `DatabaseColumn`; only a confirmed Sync Apply use case may do so.
7. **Human knowledge protection.** Structural sync has an explicit field allowlist; all other fields are immutable to Sync.
8. **Missing is not deleted.** `MissingFromSource` cannot set `IsDeleted`, call a Delete use case, or hide current knowledge by itself.
9. **No rename guess.** First version represents a renamed identity as old MissingFromSource plus new Added.
10. **Provider-owned identity semantics.** The core compares opaque logical keys and does not contain `if Oracle19c`, collation logic, or quoted-identifier heuristics.
11. **Scope-compatible Diff only.** A changed target, included-schema set, identity algorithm, or canonical format cannot silently produce destructive-looking MissingFromSource results.
12. **Backend authorization is authoritative.** UI capability hiding is convenience only.
13. **No secret disclosure.** Plaintext/ciphertext, `SecretReference`, connection strings, descriptors, and raw provider errors do not enter API responses, logs, Runs, Snapshots, Differences, or Sync Plans.
14. **No System persistence substitution.** External Provider work creates no second business `DbContext` and does not generalize SQLite into multi-database persistence.

## 7. Provider architecture

### 7.1 Feature and dependency boundary

The implementation remains feature-first:

```text
Features/DatabaseDiscovery
├─ Domain            Profile/Run/Snapshot/Difference/SyncPlan state
├─ Application       explicit Test/Discover/Diff/Plan/Apply use cases
├─ Providers
│  ├─ Oracle         Oracle catalog SQL + normalization + identity rules
│  ├─ PostgreSql     later provider
│  └─ SqlServer      later provider
├─ Persistence       EF configurations using KnowledgeHubDbContext
└─ Api               explicit Controllers/contracts
```

`Features/DatabaseKnowledge` remains the owner of `DatabaseSource`, `DatabaseObject`, and `DatabaseColumn`. DBDISC-B04 may call one explicit Database Knowledge structural-sync application boundary; it must not reach through Controllers or expose a generic entity patcher.

### 7.2 Frozen interfaces

The conceptual contracts are:

```csharp
public interface IDatabaseConnectionTester
{
    DatabaseProviderType ProviderType { get; }

    Task<DatabaseConnectionTestResult> TestConnectionAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);
}

public interface IDatabaseDiscoveryProvider
{
    DatabaseProviderType ProviderType { get; }

    Task<DatabaseProviderCapabilities> DetectCapabilitiesAsync(
        DatabaseDiscoveryConnectionContext connection,
        CancellationToken cancellationToken);

    Task<DatabaseDiscoverySnapshot> DiscoverAsync(
        DatabaseDiscoveryConnectionContext connection,
        DatabaseDiscoveryRequest request,
        DatabaseProviderCapabilities capabilities,
        CancellationToken cancellationToken);
}
```

These are separate contracts because testing reachability/authentication must not imply that a full scan ran. One concrete vendor adapter may implement both and share an internal connection/session factory. There is no runtime plug-in discovery; DI registers the closed set of compiled Providers, and a small resolver rejects an unregistered `ProviderType` as `ProviderUnavailable`.

Test Connection is a request-bound Administrator operation, not a `DatabaseDiscoveryRun`: it creates no Snapshot/Difference and performs no business-data read. Its start transaction captures `ConfigurationRevision`/Secret Version, assigns a new opaque `LatestConnectionTestAttemptId`, and records test start time. Its completion conditionally writes the normalized summary only when that attempt is still latest and both captured revisions are current. A superseded or configuration-changed attempt returns 409 and cannot overwrite the summary. Thus concurrent Tests are safe, and Profile/Secret replacement cannot attribute an old-password result to new configuration. A Test whose start sees an active Discovery Run is rejected; a Run accepted after Test start is safe because both operations are read-only and Test summary fields do not change `ConfigurationRevision`.

`DatabaseDiscoveryConnectionContext` is application-internal, short-lived, and secret-bearing. It contains the resolved Profile revision and an in-memory credential. It is never serialized, logged, returned, or persisted.

### 7.3 Provider responsibility

Each Provider exclusively owns:

- connection-string construction with the vendor's typed builder;
- server-version and current database/service/context detection;
- safe catalog SQL and parameterization;
- identifier preservation, comparison-key construction, and system-schema filters;
- capability detection;
- mapping vendor types and metadata to the Canonical Snapshot;
- safe vendor error-code extraction and normalized error mapping;
- batching/streaming behavior needed by the vendor.

Application/core code owns:

- authorization, Profile lookup, secret resolution, Run state, limits, overall cancellation, persistence, baseline selection, Diff, Sync Plan, Hub concurrency, and audit;
- validation that the Provider output is complete, internally consistent, within limits, free of connection/authentication secrets introduced by this feature, and canonical-format compatible.

### 7.4 Connection and command safety

- Connection strings are built only from validated fields. No arbitrary connection-string fragment or descriptor is accepted.
- Catalog queries use static SQL plus parameters. Schema names are values, not interpolated identifiers.
- Provider commands are tagged by a safe internal operation name such as `ReadColumns`; SQL text is not logged.
- Connection pooling may be used by the vendor driver, but secret-bearing connection objects are disposed at operation end and no connection is held in Domain state.
- Provider cancellation must call the driver's asynchronous cancellation/close path. A cancelled command cannot be treated as a successful empty result.

## 8. DatabaseConnectionProfile

### 8.1 Aggregate and fields

The first schema direction is one concrete Profile aggregate, not one model per vendor.

| Field | Rule |
| --- | --- |
| `Id` | Stable numeric application ID |
| `DatabaseSourceId` | Required, unique Source FK; Source must be active while Profile is enabled or used; exactly one Profile per Source in v1 |
| `Name` | Administrator-facing operational label; active unique under the Profile naming rule |
| `ProviderType` | Closed value: `Oracle`, `PostgreSql`, `SqlServer`; immutable after first successful Snapshot |
| `Host` | Required DNS name/IP text; never includes credentials or a descriptor |
| `Port` | Required 1–65535; provider default may be offered by UI but is persisted explicitly |
| `DatabaseName` | Required for PostgreSQL/SQL Server; null for Oracle service mode |
| `ServiceName` | Required for Oracle v1; null for PostgreSQL/SQL Server |
| `AuthenticationMode` | `UsernamePassword` only in v1; future modes require an explicit extension |
| `Username` | Required; returned only by Administrator Profile APIs |
| `SecretReference` | Server-internal opaque locator; never an API field |
| `ProviderSpecificOptionsJson` | Versioned, size-bounded, allowlisted, non-secret options only |
| `IncludedSchemasJson` | Required non-empty explicit discovery allowlist after Provider normalization |
| `IsEnabled` | Explicit operational enable/disable; disabled Profiles cannot test, scan, or apply Sync |
| `ConnectionStatus` | `Unknown`, `Succeeded`, or `Failed`; normalized latest test summary only |
| `ConfigurationRevision` | Monotonic revision of Provider/locator/username/options/included-schema/enabled configuration; not changed by operational summary writes |
| `LatestConnectionTestAttemptId` | Internal opaque latest-test guard; never a credential or public write field |
| `LastConnectionTestStartedAt` | UTC ordering/audit support for conditional test completion |
| `LastConnectionTestAt` | UTC timestamp or null |
| `LastDiscoveryAt` | UTC start/completion summary from the latest Run or null |
| `LastSuccessfulDiscoveryAt` | UTC timestamp from the latest successful complete Snapshot or null |
| `CreatedByUserId`, `CreatedAt`, `UpdatedAt` | Existing canonical actor/audit style |
| `Version` | App-managed concurrency, exposed only as opaque `concurrencyToken` |

`Last*` and `ConnectionStatus` are operational summaries. Runs remain the Discovery-history authority and audit events record Test commands. A failed Run must not overwrite `LastSuccessfulDiscoveryAt`. Operational test/scan summary writes may increment the row `Version` for optimistic concurrency but do not increment `ConfigurationRevision`; configuration commands increment both.

### 8.2 Profile to DatabaseSource relationship

The relationship is frozen as:

```text
DatabaseSource 1 → 0..1 DatabaseConnectionProfile
DatabaseConnectionProfile → exactly 1 fixed DatabaseSource row
database_source_id UNIQUE, FK RESTRICT
```

Manual Sources remain valid without a Profile. A Profile cannot float without a Source and cannot choose a different Source at Sync time. The Source must be active when the Profile is created, enabled, tested, scanned, or synchronized. A disabled historical Profile may retain its FK to a later soft-deleted Source for audit/history. This prevents one scan history from becoming a competing baseline for multiple knowledge trees without breaking retained history.

The ownership split is:

- Profile: Provider, endpoint, database/service locator, credential reference, included schemas, connection/discovery status;
- Source: System context, business-facing name, engine/environment/instance/service/database description, primary-source semantics, creator, and soft-delete state.

`ProviderType` must be consistent with `DatabaseSource.Engine` using a validated mapping (`Oracle` → `Oracle`, `PostgreSql` → `PostgreSQL`, `SqlServer` → `SQL Server`). Profile creation does not silently rewrite Source fields. A mismatch is `reference_invalid`/422 and must be corrected explicitly.

An enabled Profile is a new bounded dependency blocker for DatabaseSource soft delete. Its active Run is covered by that same blocker and is not counted as a duplicate blocker. Disabling the Profile stops future external work; it never cascades a Source/Object/Column delete. Historical Runs and Snapshots remain readable under their authorized historical boundary.

### 8.3 Locator decisions

| Provider | Host/Port | Database locator | First-version decision |
| --- | --- | --- | --- |
| Oracle | Required, normally 1521 | `ServiceName` | Supported main path |
| PostgreSQL | Required, normally 5432 | `DatabaseName` | Supported by later Provider |
| SQL Server | Required, normally 1433 | `DatabaseName` | Supported by later Provider |

For Oracle v1:

- one Service/PDB/non-CDB service equals one Profile;
- SID is not supported in the first implementation;
- a raw connection descriptor, TNS alias, LDAP naming, proxy authentication, and privileged connection are not supported;
- TCP service-name connection is the development/Internal Pilot path;
- TCPS/wallet/client-certificate support is deferred to a focused security/connection amendment and may be mandatory before a particular real Production deployment;
- changing the Oracle Service, PostgreSQL/SQL Server DatabaseName, or Provider after a successful Snapshot requires a new Profile. Host/port failover correction may be allowed only after Test Connection proves the same provider database identity; otherwise a new baseline/Profile is required.

### 8.4 Mutation and lifecycle

- Profile create/update/enable/disable uses Administrator policy, antiforgery, and opaque concurrency.
- Profile configuration and Secret mutation while that Profile has an active Run returns 409; the operation does not silently choose old or new connection state. A conditionally valid operational test-summary write is not a configuration mutation and does not change `ConfigurationRevision`.
- Changing `Username`, included schemas, normalization-affecting options, or target locator invalidates the previous comparison scope. The next successful scan starts a new baseline unless compatibility is explicitly proven by the frozen scope fingerprint rules.
- First version has enable/disable, not a generic hard-delete UI. Physical removal of a Profile with history is out of scope.

## 9. Secret and password boundary

### 9.1 Storage decision

The first version stores a recoverable external database password in the System Knowledge Hub database only as authenticated ciphertext in a separate one-to-one secret record. It never stores plaintext and does not reuse the local-login PasswordHasher, because Test Connection/Discover must recover the credential.

Conceptual record:

```text
DatabaseConnectionSecret
- ProfileId (PK/FK)
- ProtectedPayload
- PayloadFormatVersion
- UpdatedAt
- Version
```

The Profile's internal `SecretReference` resolves to this record. Ciphertext is not considered harmless: it is never returned or logged.

One narrow `IDatabaseConnectionSecretStore` is allowed inside the feature. V1 has exactly one local Data Protection implementation; it is not a generic plug-in system.

### 9.2 Data Protection decision

- Use ASP.NET Core `IDataProtector` with a stable, versioned purpose that includes the Profile identity, conceptually `SystemKnowledgeHub.DatabaseDiscovery.ConnectionSecret/v1/{profileId}`.
- Purpose isolation prevents a ciphertext copied between Profiles from being accepted.
- `CryptographicException`, missing keys, corrupt ciphertext, or unsupported payload version fails closed as `SecretUnavailable`.
- Data Protection key rotation can still decrypt payloads made with expired retained keys. Operators must therefore retain and back up the complete key ring together with the SQLite database.
- Key loss makes stored external credentials irrecoverable. The application must require an Administrator to set the Secret again; it must not fall back to configuration or an empty password.

Direct DPAPI encryption of Profile passwords is rejected. On a single-machine Windows deployment, operators may later choose `ProtectKeysWithDpapi` to protect the Data Protection key ring. That choice binds key usability to the selected machine/user scope and changes backup/migration behavior, so it is a deployment decision, not a Domain or Provider dependency.

The current repository persists a configured Data Protection key ring but does not select key-at-rest protection. Therefore:

- task-owned Development/integration tests use a stable task-owned key directory when restart decryption is under test;
- Production use of stored database credentials remains inside the existing `SEC-04` protected persistent-key, backup/restore, HTTPS/proxy, and log-handling gate;
- Data Protection path validation alone is not a claim that the key ring is encrypted at rest.

Official Data Protection behavior and purpose isolation are documented by Microsoft in [using Data Protection](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/using-data-protection), [purpose strings](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/consumer-apis/purpose-strings), [key storage providers](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-storage-providers), and [key encryption at rest](https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/implementation/key-encryption-at-rest).

### 9.3 Write-only API and rotation rules

- Profile reads return `hasSecret: true|false` and optionally `secretUpdatedAt`; they never return plaintext, ciphertext, hint, last characters, or `SecretReference`.
- Secret set/replace and clear are separate Administrator-only, antiforgery-protected commands with the Profile `concurrencyToken`.
- Omitted Secret means no Secret mutation. Empty strings are rejected; they are not interpreted as preserve or clear.
- Replace stores only the new ciphertext, increments Version, sets ConnectionStatus to `Unknown`, and retains no application-readable password history.
- SQLite pages/WAL/backups may retain old ciphertext. Secure erasure is not promised; actual credential revocation happens in the external database.
- External database credential rotation is not distributed-transactional with System Knowledge Hub. The operational sequence is external rotation with overlap where available, local replace, Test Connection, then external old-secret revocation.

### 9.4 Secret resolution and redaction

Test/Discover resolves the Secret just in time, builds a connection with the vendor's typed builder, opens the connection, and disposes all secret-bearing objects at operation end. The credential is not placed in a Run, Snapshot, Difference, Sync Plan, background message, cache, or Domain event.

The feature must not log a raw connection string, raw descriptor, provider exception message, command object, or exception object whose message may contain secret-bearing connection data. It logs normalized allowlisted fields only.

`ProviderSpecificOptionsJson` rejects credential-like keys and raw fragments, including `password`, `pwd`, `secret`, `token`, `clientSecret`, and `user id`, case-insensitively. Provider-specific validation is an allowlist, not only a marker blacklist.

### 9.5 Future external Secret Manager

The application use cases depend on secret resolution, not local ciphertext details. A future approved deployment may add a storage kind and opaque external reference behind `IDatabaseConnectionSecretStore`. The public Profile API continues to expose only `hasSecret`; Test/Discover remains unchanged. A provider registry, marketplace, lease-renewal framework, or vendor SDK is not created until a concrete external manager is selected.

## 10. Canonical DatabaseDiscoverySnapshot

### 10.1 Root contract

Every Provider returns the same complete, versioned document:

```text
DatabaseDiscoverySnapshot
- FormatVersion
- CapturedAt
- ProviderType
- ProviderVersion
- DatabaseInfo
- DiscoveryScope
- IdentityAlgorithmVersion
- Capabilities
- Schemas
- Objects
- Columns
- PrimaryKeys
- ForeignKeys
- UniqueConstraints
- Indexes
- Sequences
- ForeignKeyReferenceClosure
- Counts
```

The Provider does not assign a persistence ID. The application validates the document, calculates its canonical SHA-256 content hash, and persists it only after the Run's entire Core Discovery and Snapshot persistence transaction succeeds.

`DatabaseInfo` contains safe structural identity only: provider, server version, current database/service/container context, and a provider-produced target fingerprint. It contains no host credential, username, connection string, or raw connection descriptor.

`DiscoveryScope` contains the provider-normalized included schemas, fixed Core object types, scope-format version, and a compatibility fingerprint. System schemas are never added implicitly after Profile confirmation.

### 10.2 Canonical records

| Record | Required Core fields |
| --- | --- |
| Schema | exact `Name`, opaque `LogicalIdentity` |
| Database object | schema identity/name, exact object name, `Table` or `View`, optional database comment, opaque logical identity, optional native diagnostic identity |
| Column | parent object identity, exact name, nullable `SourceOrdinal`, structured `NativeDataType`, nullable/knownness-aware length and numeric measures, nullable, default expression, PK flag, database comment, logical identity |
| Primary key | exact constraint name, parent object, ordered column identities, logical identity |
| Foreign key | exact name, parent object/ordered columns, referenced schema/object/ordered columns, update/delete rules when exposed, logical identity |
| Foreign-key reference stub | exact schema/object/column identities needed to resolve an included object's out-of-scope FK target; `ReferenceOnly = true`; never a discovered Object or Sync candidate |
| Unique constraint | exact name, parent object, ordered columns, logical identity |
| Index | exact name, parent object, required `NativeIndexKind`, unique flag, zero-or-more ordered key parts (`ColumnIdentity` or bounded native expression) and sort direction, ordered non-key column parts with a closed role (`Included`, `Stored`, `Partitioning`, `UnorderedMember`), optional native predicate/filter, linked backing constraint when known, logical identity |
| Sequence | schema, exact name, structured `NativeDataType`, increment/min/max/cache/cycle/order values when statically exposed, optional start value only when the vendor exposes a structural start value, logical identity |

Core does not retrieve view SQL text, trigger bodies, index expressions that require DDL parsing, object grants, storage/tablespace layout, statistics/histograms, estimated row count, business data, or complete DDL.

### 10.3 Native and normalized data types

`NativeDataType` is a required structured value rather than one lossy string:

```text
Origin                       CatalogDeclared | ProviderImplicit
Name                         exact catalog type name, or an officially documented stable Provider-implicit type name
Namespace?                   owning schema/namespace for user-defined/domain/alias types
Declaration                  bounded deterministic provider-produced display signature; not parsed DDL
Length.Kind                  Exact | Unbounded | NotApplicable | Unknown
Length.Value?                present only for Exact
Length.Unit?                 Bytes | Characters | Bits | ProviderUnits
CharacterLengthSemantics?    provider catalog semantics where exposed
NumericPrecision.Kind        Exact | NotApplicable | Unknown
NumericPrecision.Value?
NumericScale.Kind            Exact | NotApplicable | Unknown
NumericScale.Value?
```

This preserves Oracle schema-owned types and byte/character semantics, PostgreSQL domains/arrays, and SQL Server alias/CLR/MAX distinctions without pretending that null always means the same thing. `Declaration` may be used for UI and current `DatabaseColumn.DataType` sync, for example `VARCHAR2(100 CHAR)`, but it is derived only from bounded catalog fields.

V1 does **not** persist a lossy cross-vendor `NormalizedDataType` enum. No approved upper-layer behavior requires one, and Oracle `NUMBER`, PostgreSQL domains/arrays, and SQL Server types cannot be safely collapsed without product semantics. A later use case may add a derived normalized classification without changing Native fields.

### 10.4 Provider-specific metadata

Provider-specific metadata is optional, schema-versioned, size-bounded JSON containing allowlisted primitive values only. It is for diagnostics or later capability presentation. Core Diff ignores it unless a later frozen capability promotes one field into the Canonical contract. Application/Manual Sync code cannot branch on an Oracle JSON key.

No Provider metadata may contain SQL text, a connection string, a path to a credential/wallet, host credentials, usernames, secrets, data samples, or an unbounded driver object serialization.

Canonical Default Expressions, database Comments, Index native expressions, and Index predicates/filters are externally authored structural metadata and may themselves contain sensitive literals that no Provider can reliably classify. They are bounded, persisted and returned only through the authorized structural-data boundary, and never copied to ordinary logs/error summaries. “Credential redaction” means that this feature never adds connection/authentication secrets or secret-bearing connection/error fields to Canonical content; it does not make an unverifiable claim that all external catalog text is intrinsically secret-free.

### 10.5 Canonical ordering, validation, and completeness

- Query result/display order is not identity.
- The Provider preserves nullable source ordinal fields, then the application serializes arrays in deterministic opaque logical-key order for hashing. Oracle user-created invisible columns may have no `COLUMN_ID`; they remain distinct records with `SourceOrdinal = null` and are not assigned a fake structural ordinal.
- Logical identities must be unique within their scope; every parent/column reference must resolve either to an included record or to the bounded FK reference closure described below.
- Counts are recalculated by the application, not trusted from Provider-supplied summary values.
- Every catalog-authored string/collection has a centralized per-field/per-record bound. A required value that exceeds its lossless bound fails the Run as `LimitExceeded`; Core never truncates a name, default, comment, expression, predicate, or key list and then hashes the truncated value as truth.
- The persisted `Completeness` value is `Complete` in v1. There is no successful `Partial` state.
- Optional advanced-capability failure is represented in the Capability Snapshot and cannot remove a Core record. Failure of a Core query fails the Run.
- Snapshot format and identity algorithm are independently versioned so a format-only change does not silently alter matching behavior.

Included schemas are not required to be transitively closed over Foreign Keys. For an FK owned by an included Object, the Provider performs a bounded targeted metadata lookup and stores exact referenced schema/object/column identity stubs in `ForeignKeyReferenceClosure`. These stubs do not enlarge `IncludedSchemas`, do not count as discovered Objects/Columns, and can never be selected for Sync. The closure algorithm/version is part of scope compatibility. If the current credential cannot resolve a required referenced target, Core fails with `InsufficientPrivilege`/`UnresolvedForeignKeyReference`; it never silently drops the FK or claims a Partial success.

Reference-only stubs participate only in the owning FK's structural hash/comparison. They never produce standalone Added/Changed/MissingFromSource entries, Hub mappings, or Sync actions.

A successful Snapshot is a complete, internally consistent observation over the scan interval, not a claim that several catalog queries observed one database-wide physical instant. Providers use one connection/session where practical, deterministic bulk queries, and parent/reference/count/duplicate-key validation. Concurrent DDL that produces an inconsistent result fails the Run for a human retry. Core does not lock business objects, require DBA privileges, or claim an SCN/transaction-frozen inventory.

## 11. Core metadata scope and Capability Detection

### 11.1 Required Core scope

The first complete Snapshot must cover, for the explicit included schemas:

- Schema;
- Table and View identity;
- Column name/order/native type/length/precision/scale/nullability/default;
- Primary Key, Foreign Key, Unique Constraint, and Index;
- table/view and column comments;
- Sequence.

An Oracle 19c Run that cannot read any one of these catalog groups is Failed, not a smaller successful Snapshot.

### 11.2 Advanced capabilities

The following are capability-gated and are not Core success dependencies:

- identity columns;
- virtual columns;
- invisible columns;
- partitions;
- materialized views;
- synonyms;
- triggers;
- CDB/PDB metadata beyond the current connection context;
- complete DDL.

V1 may detect and report these capabilities without collecting their full metadata. Collection requires a later explicit scope amendment. However, every user-defined column returned by the approved Core column catalog is still retained as a Core Column. In particular, an Oracle user-created invisible column with no structural ordinal remains in the Snapshot with `SourceOrdinal = null`; advanced invisibility semantics may be capability-only, and Manual Sync blocks that item as `UnsupportedOrdinal` rather than inventing an ordinal.

### 11.3 Capability state model

Each capability has a closed state:

| State | Meaning |
| --- | --- |
| `Supported` | Server/context/catalog access can provide the capability |
| `NotSupported` | Server or Provider version does not provide it |
| `Unavailable` | Potentially supported but current privilege/configuration cannot prove or read it |
| `NotApplicable` | Capability has no meaning in the current target context |

`Unavailable` is never mislabeled `NotSupported`. The Capability Snapshot records safe reason/error codes, not raw driver messages.

Oracle 19c detection evaluates at least:

```text
SupportsIdentityColumns
SupportsInvisibleColumns
SupportsMaterializedViews
SupportsPartitions
SupportsSequences
SupportsSynonyms
SupportsTriggers
SupportsContainerDatabase
SupportsFullDdl
```

The upper layer renders these names/states generically. Only the Oracle Provider knows which Oracle catalog probe produced them.

## 12. Oracle 19c Provider strategy

### 12.1 Supported target

Oracle 19c is the only Oracle target in this phase. Oracle 10g and 11g Providers, client compatibility, Docker gates, bridge processes, and task slices are removed from scope by explicit requester direction.

The future in-process Provider must use an Oracle-managed .NET data provider version that officially supports the repository's .NET 8 runtime and Oracle 19c server target. Exact package/version selection occurs in the implementing slice with package security and license review; DBDISC-A01 does not add a package.

Oracle's current [ODP.NET system requirements](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/InstallSystemRequirements.html) state that ODP.NET Core supports .NET 8, is 64-bit only, and supports Oracle Database 19c or later. Oracle's [21c-era matrix](https://docs.oracle.com/en/database/oracle/oracle-database/21/odpnt/InstallSystemRequirements.html) documents ODP.NET Core 21.12 as the minimum line for .NET 8. The implementation is not pinned to that old floor: B01 selects a then-supported/security-reviewed newer package and verifies the current 19c matrix. The product target remains Oracle major version 19 only.

### 12.2 Core catalog source

Core Discovery uses stable `ALL_*` Data Dictionary views scoped to the Profile's included owners:

| Metadata | Primary Oracle catalog source |
| --- | --- |
| schema/owner validation | `ALL_USERS` plus actual scoped catalog visibility |
| tables | `ALL_TABLES` |
| views | `ALL_VIEWS` without fetching/depending on view text |
| columns/default/nullability/types | `ALL_TAB_COLUMNS` |
| table/view comments | `ALL_TAB_COMMENTS` |
| column comments | `ALL_COL_COMMENTS` |
| PK/FK/Unique | `ALL_CONSTRAINTS` + `ALL_CONS_COLUMNS` |
| indexes | `ALL_INDEXES` + `ALL_IND_COLUMNS` + `ALL_IND_EXPRESSIONS` |
| sequences | `ALL_SEQUENCES` |

Optional capability probes may use `ALL_TAB_IDENTITY_COLS`, `ALL_TAB_COLS`, `ALL_MVIEWS`, `ALL_SYNONYMS`, `ALL_TRIGGERS`, and partition views when the capability slice is implemented.

For Oracle Sequences, volatile/runtime `LAST_NUMBER` is excluded from Canonical Diff/hash, and [`ALL_SEQUENCES`](https://docs.oracle.com/en/database/oracle/oracle-database/19/refrn/ALL_SEQUENCES.html) does not provide a structural start value. Oracle `StartValue` is therefore null in Core. Its required Sequence type is the documented Provider-implicit numeric type (`Origin = ProviderImplicit`, stable `NUMBER` declaration), while PostgreSQL/SQL Server Providers use their catalog-declared Sequence type. Function-based index expressions are read as bounded native expression metadata from [`ALL_IND_EXPRESSIONS`](https://docs.oracle.com/en/database/oracle/oracle-database/19/refrn/ALL_IND_EXPRESSIONS.html); the Provider does not parse `GET_DDL`.

The Provider does not depend on `DBA_*`, `SYS`, or `SYSTEM`. `DBMS_METADATA.GET_DDL` is not a Core source and no first-version code parses generated Oracle DDL.

Oracle documents that `ALL_` views describe objects accessible to the current user, while `DBA_` views require administrative dictionary privileges; see [static Data Dictionary view semantics](https://docs.oracle.com/en/database/oracle/oracle-database/19/refrn/about-static-data-dictionary-views.html), [`ALL_TABLES`](https://docs.oracle.com/en/database/oracle/oracle-database/19/refrn/ALL_TABLES.html), [`ALL_CONSTRAINTS`](https://docs.oracle.com/en/database/oracle/oracle-database/19/refrn/ALL_CONSTRAINTS.html), and [`ALL_CONS_COLUMNS`](https://docs.oracle.com/en/database/oracle/oracle-database/19/refrn/ALL_CONS_COLUMNS.html).

### 12.3 CDB/PDB boundary

The frozen rule is:

```text
one PDB/service connection context = one DatabaseConnectionProfile
```

- The Profile connects directly to the intended service/PDB.
- Discovery reads only the session's current container context.
- It does not connect to CDB Root and fan out across PDBs.
- It does not issue `ALTER SESSION SET CONTAINER`.
- A CDB root profile that does not represent an explicitly approved single discovery context is rejected during Test Connection.
- Current database/service/container identifiers are captured in the safe target fingerprint.

Oracle describes a session's current container and PDB-specific service boundary in its [multitenant architecture documentation](https://docs.oracle.com/en/database/oracle/oracle-database/19/multi/overview-of-the-multitenant-architecture.html).

### 12.4 Version and context detection

The Provider uses the driver-exposed server version and low-privilege context functions first. A catalog version probe such as `PRODUCT_COMPONENT_VERSION` may be used when available; the Provider does not make `V$` access a Core privilege requirement.

The detected major version must be 19. Any other Oracle major version fails Test Connection/Discover as `UnsupportedDatabaseVersion`; it is not routed through legacy SQL.

### 12.5 Oracle identity and case behavior

Catalog-returned owner/object/column names are preserved exactly. Unquoted Oracle identifiers have already been folded by Oracle; quoted identifiers remain exact. The Oracle Provider therefore produces logical keys from the exact catalog values with ordinal byte comparison and does not uppercase/lowercase names again in the core.

Oracle native object IDs may be retained as safe diagnostic metadata. They do not override name-based logical identity, do not trigger rename detection, and are not trusted across drop/recreate, export/import, or another database.

## 13. Snapshot, Run, and Difference persistence

### 13.1 DatabaseDiscoveryRun

One Run records:

```text
Id
ProfileId
ProfileConfigurationRevision
SecretVersion
BaseSnapshotId?
ScopeGenerationId?          // assigned only during successful finalize
QueuedAt
StartedAt?
CompletedAt?
Status
LeaseOwnerId
LeaseToken
LeaseHeartbeatAt
LeaseExpiresAt
CancellationRequestedAt?
CancellationRequestedByUserId?
ProviderType
ProviderVersion?
ScopeFingerprint?
CapabilitySnapshotJson?
ErrorCode?
ErrorSummary?
SafeErrorMetadataJson?
ObjectCountsJson?
Version
```

Run states are:

```text
Queued → Running → Succeeded
Queued → Running → Failed
Queued → Running → Cancelled
Queued → Cancelled
```

A terminal state never transitions back to Running and there is no `PartiallySucceeded` state. A Running operation owns a bounded renewable lease and heartbeat; it does not hold a SQLite transaction while reading the external database. Another process may recover it as `RunInterrupted` only after the lease expires and a token-checked transaction proves the lease was not renewed. “Next run” cannot unconditionally kill a Run that may still be active on another instance.

### 13.2 DatabaseDiscoverySnapshot

One successful Run owns exactly one immutable Snapshot row:

```text
Id
RunId (unique)
ProfileId
CapturedAt
FormatVersion
IdentityAlgorithmVersion
ScopeGenerationId
ScopeFingerprint
Completeness = Complete
CanonicalContentJson
ContentSha256
CountsJson
```

V1 persists the full versioned Canonical Snapshot as JSON in System Knowledge Hub SQLite. This preserves an exact replayable observation without prematurely creating one product table for every external metadata kind. The payload is immutable, size-limited, deterministically serialized, and free of connection/authentication secrets introduced by this feature; externally authored bounded default/comment/index-expression/predicate text remains authorized structural metadata. Compression is deferred until measured size proves it necessary.

Snapshot content is written in the same System Knowledge Hub transaction that makes the Run Succeeded. Finalize must match the current Run lease token and prove no cancellation request exists; an expired/replaced worker can never overwrite the new owner or publish a Snapshot. A failed finalize transaction cannot leave an orphan successful baseline. The application then makes a separate best-effort token-checked transaction to mark the Run Failed as `SnapshotPersistenceFailed`; if storage remains unavailable, the Run stays Running until expired-lease recovery closes it as interrupted. The system never claims the Snapshot succeeded merely because vendor reads completed.

### 13.3 DatabaseDiscoveryDifference

One Difference header records:

```text
Id
ProfileId
BaseSnapshotId?     // null for first scan
TargetSnapshotId
ScopeGenerationId
AlgorithmVersion
CreatedAt
SummaryCountsJson
ContentSha256
```

Change entries are durable, pageable rows for Added, Changed, and MissingFromSource. Each stores entity kind, logical identity, parent identity, safe display snapshot, state, and bounded before/after changed-field values. Unchanged detail is derivable from the immutable Snapshots and is not duplicated row-for-row; the header stores Unchanged counts and the read API may derive/page unchanged items on explicit request.

A first Snapshot compares against an empty conceptual baseline: every Core identity is Added and Unchanged/MissingFromSource are zero.

### 13.4 Baseline selection

Run acceptance captures the Profile/Secret revisions and requested scope configuration, but the connected principal, target identity, and final scope fingerprint are authoritative only after the Provider connects and builds a complete validated Snapshot. After external reads and Canonical validation, the application resolves the latest compatible baseline candidate and computes the deterministic Diff outside a SQLite write transaction. The short finalize transaction then revalidates that candidate as the latest generation Snapshot, gets or creates the immutable Profile-scoped `DatabaseDiscoveryScopeGeneration` identified by the final scope fingerprint, stores `ScopeGenerationId` and `BaseSnapshotId`, persists the already-built Snapshot/Difference, and marks the Run Succeeded. A Scope Generation therefore means one stable `(ProfileId, ScopeFingerprint)` compatibility lineage; it is carried by Snapshot, Difference, Sync Plan, and bindings. The one-active-Run invariant prevents a same-Profile scan race, while the transaction recheck prevents a stale administrative/history race. Failed/cancelled Runs and partial content are never baselines.

If no compatible Snapshot exists, `BaseSnapshotId` is null. A scope/target/identity-version change starts a new baseline instead of emitting MissingFromSource for objects outside the new scope.

## 14. Diff semantics

### 14.1 State definitions

| State | Frozen meaning |
| --- | --- |
| `Added` | Logical identity exists only in target Snapshot |
| `Changed` | Same logical identity exists in both and one or more approved canonical structural fields differ |
| `MissingFromSource` | Logical identity exists only in compatible base Snapshot and was not observed through the current Profile visibility in the target Snapshot |
| `Unchanged` | Same logical identity and all approved canonical structural fields are equal |

Diff compares Canonical fields, not JSON text order and not Provider-specific extension data. Lists such as key columns are compared in semantic order. Null and empty are distinct only where the Canonical contract declares them distinct; Providers normalize vendor empty/unknown semantics before returning a Snapshot.

Changed entries carry a field-level allowlist, for example:

```text
Column NativeDataType: VARCHAR2 → NVARCHAR2
Column Length: 50 → 100
Column IsNullable: false → true
Column DefaultExpression: null → 'N'
Object Comment: old → new
Index KeyColumns: [A] → [A, B]
```

### 14.2 Rename

V1 never infers rename by edit distance, ordinal position, shape, comment, or native object ID. A changed logical name produces:

```text
old identity = MissingFromSource
new identity = Added
```

The Diff UI may display the two entries near each other but cannot label them as a rename or create a combined Apply action.

### 14.3 Scope compatibility

The scope fingerprint includes at least:

- ProviderType and provider-produced target database/service identity;
- exact provider-normalized included-schema logical keys;
- Core metadata scope version;
- Canonical Snapshot format compatibility version;
- Provider identity algorithm version;
- exact connected principal/username;
- allowlisted normalization-affecting Profile options;
- system-schema exclusion and FK reference-closure policy/version;
- identifier/case comparison mode;
- a safely observable role/grant/catalog-visibility fingerprint when the Provider can produce one without elevated privilege.

Secret rotation under the same principal is not a scope change; changing the principal starts a new baseline. When a reliable visibility fingerprint is unavailable, successful query coverage plus explicit UI warning is the honest boundary; the application never fabricates proof of unchanged privileges.

Host failover, password rotation, and timestamps do not by themselves change scope when the target identity is proven unchanged. Provider/database/service changes, included-schema changes, or identity-algorithm changes do.

If a low-privilege Provider cannot reliably prove that a same-named target is the same database/service after a locator/failover change, it starts a new Scope Generation. It never guesses continuity from display name alone.

An incompatible comparison is `BaselineIncompatible`; it never returns a normal Difference containing mass MissingFromSource.

Oracle `ALL_*` views describe objects visible to the connected account, not an independently provable physical inventory of every object in an included owner. Snapshot `Complete` therefore means: every required Core query completed consistently for the frozen schema allowlist and that credential's catalog visibility. It does not mean the account can prove that no invisible object exists. A silent privilege loss can appear as MissingFromSource even when DDL did not remove the object. The Diff/Sync UI must show this warning, and manual confirmation is mandatory. A detected privilege/query failure fails the Run rather than producing MissingFromSource.

## 15. Stable identity and identifier collisions

### 15.1 Provider-neutral logical identity

Core treats `LogicalIdentity` as an opaque, versioned, ordinal string/byte key produced by the Provider. Providers construct keys from length-delimited components so dots, quotes, delimiters, and Unicode cannot collide.

The conceptual components are:

| Kind | Logical components |
| --- | --- |
| Schema | `Schema` family discriminator + provider-normalized schema identity |
| Table/View | `Object` family discriminator + schema identity + object name identity; `ObjectType` is a structural field, not identity |
| Column | `Column` family discriminator + parent object identity + column name identity |
| PK/FK/Unique | `Constraint` family discriminator + constraint kind + parent object + constraint name identity |
| Index | `Index` family discriminator + parent object + index name identity |
| Sequence | `Sequence` family discriminator + schema identity + sequence name identity |

The family discriminator is the first length-delimited key component, so a same-named Column and Index cannot collide. Display order and array position are never identity. Native IDs are diagnostics only. Database semantics are used only to resolve configured Profile names such as `IncludedSchemas`; catalog-returned identity components are preserved exactly and compared ordinally after they cross the Provider boundary. Providers and core never call `ToUpper`/`ToLower`, use `OrdinalIgnoreCase`, or simulate a database collation over Snapshot identities. A case-only/accent-only change is therefore conservatively MissingFromSource plus Added, never an inferred rename.

For PostgreSQL, the later Provider must preserve the server's already-folded unquoted lower-case names and exact quoted names; PostgreSQL documents this distinction in its [lexical structure](https://www.postgresql.org/docs/current/sql-syntax-lexical.html). For SQL Server, the later Provider preserves returned names exactly while the database itself applies its identifier/collation rules; core does not assume case-insensitive names or simulate those rules. Microsoft documents that boundary in [Database Identifiers](https://learn.microsoft.com/en-us/sql/relational-databases/databases/database-identifiers).

### 15.2 Current Knowledge Hub `NOCASE` gap

Current Object and Column active uniqueness uses SQLite `NOCASE`, but Oracle quoted identifiers and later PostgreSQL/SQL Server databases may legally contain identifiers that current Hub constraints collapse. This is a real post-MVP schema gap, not a reason to corrupt the Snapshot.

The frozen handling is:

1. Snapshot and Diff always preserve both identities.
2. A Sync Plan detects current-target collisions before Apply and reports `UnsupportedIdentifierCollision`; it never silently merges or suffixes names.
3. DBDISC-B04 owns the minimal provider-neutral technical-identity extension and migration needed before general Apply: Object/Column identity uniqueness must use a provider-produced, versioned technical key rather than SQLite `NOCASE` as Discovery identity authority.
4. Legacy manual rows are backfilled under a deterministic legacy identity rule that preserves all currently valid rows. Provider-linked registration/sync uses the Provider identity rule.
5. Exact physical columns/indexes are finalized in B04 against the then-current schema; A01 creates no migration.

This gap is planned and non-blocking for B01/B02/Provider read work. B04 cannot pass while a valid external identity set would be silently folded.

## 16. Manual Sync architecture

### 16.1 Frozen workflow

```text
latest complete Snapshot
        + adjacent Difference
        + current Discovery bindings / Hub state
↓
Reconciliation Review
↓ human selects supported actions
Sync Plan Preview
↓ explicit confirmation
Atomic Apply
↓
Knowledge Hub structural projection + immutable applied-plan record
```

Difference and Reconciliation are deliberately distinct:

- Difference answers what changed between two external observations.
- Reconciliation answers what the latest complete observation still requires relative to the current Knowledge Hub and durable Discovery bindings.

This distinction prevents a missed `MissingFromSource` from disappearing. If an object is absent in Snapshot N and remains absent in N+1, the adjacent N→N+1 Difference contains no identity to mark missing again. Reconciliation must still surface every bound Hub target absent from the latest Snapshot until a human resolves or applies that state.

### 16.2 Sync Plan model

A Plan records at least:

```text
Id
ProfileId
DifferenceId
BaseSnapshotId?
TargetSnapshotId
ScopeGenerationId
CreatedByUserId
CreatedAt
Status = Draft | Ready | Applied | Superseded
SelectedActionsJson / normalized action rows
PreviewHash
ConfirmedByUserId?
ConfirmedAt?
ConfirmedPreviewHash?
AppliedAt?
Version
```

Each action is explicit and typed, for example:

```text
CreateDatabaseObject
LinkExistingDatabaseObject
CreateDatabaseColumn
LinkExistingDatabaseColumn
UpdateDatabaseObjectStructure
UpdateDatabaseColumnStructure
MarkObjectSourceMissing
MarkColumnSourceMissing
ClearObjectSourceMissing
ClearColumnSourceMissing
```

There is no generic JSON Patch, reflection-based mapper, generic CRUD service, or reuse of UnknownItem `KnowledgeUpdate.before_json/after_json` as a patch engine.

`Ready` means that an authorized human explicitly confirmed the exact current `PreviewHash`; confirmation records that hash, actor, and UTC time. Any selected-action, target-token, Snapshot, scope, or preview change clears confirmation and returns the Plan to Draft. Apply is allowed only when `ConfirmedPreviewHash == PreviewHash`; a button click against an older rendered Preview can never authorize a newer plan.

There is no durable `Applying` state. The same immediate transaction performs the conditional `Ready → Applied` transition together with all structural writes and the immutable result record. A process/database failure rolls the transaction back to Ready; stale/scope failure either leaves it Ready or atomically marks it Superseded as applicable. This avoids a Plan lease/recovery subsystem and a permanently stranded intermediate status.

### 16.3 Latest-Snapshot revalidation

Plan/Preview builds the full Reconciliation outside a SQLite write transaction. Apply then opens one short write transaction and performs bounded revalidation of the frozen selected actions:

- If a newer successful Snapshot exists than the Plan target, the Plan becomes `Superseded` and Apply returns 409 without writes.
- If an identity marked MissingFromSource has reappeared, Apply cannot mark it missing.
- If a Profile/scope/identity version changed, the Plan is invalid.
- If the target Snapshot remains latest, the transaction rechecks Profile/Plan/latest-Snapshot/Scope Generation/current target tokens and every selected action's binding/structural precondition. It does not reparse the full Snapshot or rebuild the full Reconciliation while holding the write lock.

An active scan may coexist with Plan review. Apply may begin while a scan is still Running because no newer successful Snapshot yet exists; SQLite serialization determines whether scan completion or Apply commits first. A later successful Snapshot always becomes the next reconciliation authority.

### 16.4 Existing-object linking

An Added external identity that matches an active manually registered Object/Column is not automatically claimed. Preview offers an explicit `LinkExisting*` action only when the provider identity, Source, parent, type, and exact structural identity are unambiguous. The human confirms the link. Existing business knowledge and status remain unchanged.

If more than one candidate, a case/collation collision, a soft-deleted candidate, or an incompatible parent exists, the action is blocked. Sync never resurrects, name-rebinds, or silently chooses the first row.

### 16.5 Atomicity and concurrency

- Apply is one short System Knowledge Hub SQLite immediate transaction for one Plan.
- The Plan carries its opaque token and every existing Source/Object/Column target token captured for Preview.
- Apply re-reads every target and binding inside the transaction.
- Bound ordinal reordering is collision-safe: B04 stages all affected bound Columns at guaranteed-unused positive temporary ordinals inside the same transaction, then writes final source ordinals before commit. Overflow, an unbound active Column occupying a target ordinal, a null source ordinal, or a Missing+Added rename that reuses an active old ordinal blocks the action as `UnsupportedOrdinal`/`ActiveOrdinalConflict`; Sync never fabricates an ordinal, renames/deletes the old Column, or weakens uniqueness.
- Any stale/missing/deleted/ownership-invalid target makes the entire Plan 409/422 with zero writes. V1 does not partially apply the remaining actions.
- Successful Apply returns new opaque tokens and records the exact applied action/result set.
- Automatic retry, last-write-wins, field merge, or silent plan regeneration is prohibited.

Large selections must respect a configured maximum actions per Plan. Users split a larger reconciliation into multiple explicit Plans rather than creating a long transaction.

## 17. Knowledge Hub mapping and field ownership

### 17.1 Profile and Source

The Profile points to one existing Source. Discovery does not create or rename a Source and does not overwrite Source `Name`, `Environment`, `InstanceName`, `ServiceName`, `DatabaseName`, `Description`, `IsPrimary`, creator, Version, or soft-delete fields.

Profile creation validates ProviderType/Source.Engine and locator consistency but keeps the operational Profile as connection authority and Source as knowledge-tree authority.

### 17.2 Snapshot mapping matrix

| Canonical metadata | Knowledge Hub target | V1 Manual Sync decision |
| --- | --- | --- |
| Schema | `DatabaseObject.SchemaName` | Set only on Create/Link; rename is not inferred |
| Table/View | `ObjectName`, `ObjectType` | Name participates in identity; Create/Link by name identity; a same-name Table↔View transition is a Changed `ObjectType` and may be explicitly applied |
| Table/View comment | no current Object field | B04 adds a dedicated technical `DatabaseComment`; never use `BusinessDescription` |
| Column name/order | `ColumnName`, `OrdinalPosition` | Create/Link; ordinal may update on a bound existing Column; null source ordinal is Snapshot-only/`UnsupportedOrdinal`; active target-ordinal collisions require explicit human resolution |
| Structured native type | deterministic `NativeDataType.Declaration` in `DataType` | Update technical `DataType`; namespace, length knownness/unit, precision, and scale remain structured in Snapshot |
| Nullable/default | `IsNullable`, `DefaultValue` | Update allowed |
| Column comment | `DatabaseComment` | Update allowed through the new Sync use case/read projection |
| Primary key ordered columns | `PrimaryKeyColumnsJson` | Update field-name array only; constraint name/details stay Snapshot-only |
| Foreign key | no current holder | Snapshot/Diff/Review only; not selectable for Apply |
| Unique constraint | no current holder | Snapshot-only; never map to human `BusinessKeyColumnsJson` |
| Index | no current holder | Snapshot/Diff/Review only |
| Sequence | no current holder | Snapshot/Diff/Review only |
| Provider-specific metadata | no current holder | Snapshot-only |

Foreign Keys, constraints, indexes, and sequences are not converted into `KnowledgeRelation`; that vocabulary is closed and expresses knowledge relationships, not physical database constraints.

### 17.3 Protected human-owned fields

Sync can never overwrite or clear:

- Source business identity/description/primary semantics;
- Object `BusinessDescription`, `AccessMode`, `BusinessKeyColumnsJson`, creator/owner, KnowledgeStatus, deletion state, or Version except the normal concurrency increment caused by an allowed structural update;
- Column `BusinessDescription`, Known Values, creator/owner, KnowledgeStatus, or deletion state;
- Evidence, HumanConfirmation, UnknownItems, Findings, Resolutions, KnowledgeUpdates, KnowledgeRelations, KnowledgeDocuments, revision history, attachment references, or search/trace content.

Creating an Object or Column through Sync uses the current canonical actor as creator and initializes KnowledgeStatus to `Unknown`. Discovery never creates Evidence/HumanConfirmation and never marks the result Confirmed.

### 17.4 Dedicated Discovery bindings

V1 adds two concrete typed binding concepts rather than a generic polymorphic target table:

```text
DatabaseObjectDiscoveryBinding
- ProfileId
- ScopeGenerationId
- IdentityAlgorithmVersion
- SchemaLogicalIdentity
- LogicalIdentity
- DatabaseObjectId (unique FK)
- FirstAppliedSnapshotId
- LastAppliedSnapshotId
- SourceMissingSinceSnapshotId?
- Version

DatabaseColumnDiscoveryBinding
- ProfileId
- ScopeGenerationId
- IdentityAlgorithmVersion
- SchemaLogicalIdentity
- LogicalIdentity
- DatabaseColumnId (unique FK)
- FirstAppliedSnapshotId
- LastAppliedSnapshotId
- SourceMissingSinceSnapshotId?
- Version
```

Bindings establish provenance and prevent name-only rebinding. `sourceMissing` is projected as `SourceMissingSinceSnapshotId != null`; `missingSince` comes from that Snapshot time. `lastDiscoveredAt` is derived from the most recent complete Snapshot that contains the bound identity, so a scan does not need to mutate the Hub entity or binding before confirmation.

Reconciliation may mark Missing only when the latest Snapshot has the same target/scope generation and identity algorithm and still includes the binding's schema. A removed schema, changed principal/target, or new identity algorithm yields `OutOfScope`/`RebaselineRequired`, never MissingFromSource. Moving compatible bindings into a new approved baseline generation is an explicit preview/confirmation action, not an automatic mass state change.

The exact EF names may be refined in B04, but the two typed FK-backed boundaries and their semantics are frozen. A generic `TargetType/TargetId` table without referential integrity is rejected.

Each binding table has both directions enforced in SQLite:

```text
UNIQUE(DatabaseObjectId) / UNIQUE(DatabaseColumnId)
UNIQUE(ProfileId, ScopeGenerationId, IdentityAlgorithmVersion, LogicalIdentity)
```

`LogicalIdentity` already contains the Object schema or Column parent identity, so the external-key tuple is complete without a name-only lookup. Apply rechecks both directions inside the same immediate transaction. A concurrent or stale Plan that would bind one external identity to a second Hub target, or one Hub target to a second identity, fails the whole Plan as 409 `DiscoveryBindingConflict` with zero structural writes; the database unique constraints remain the final race guard.

### 17.5 Source Missing behavior

`MissingFromSource` means “not observed under the latest compatible Profile visibility,” not “proved physically dropped.” The Profile's privilege/scope warning remains visible in Review.

The human choices are:

- leave unapplied/keep current knowledge;
- explicitly mark the bound Object/Column Source Missing through a Sync Plan;
- explicitly ignore the Difference for the current review without altering the Hub entity;
- outside Sync, later choose any approved archive or existing soft-delete workflow.

Marking Source Missing only updates the Discovery binding. The entity remains current, searchable, linked, historically readable, and governed by existing KnowledgeStatus/Evidence rules. Actual soft delete remains the existing authorized dependency/concurrency flow and is never embedded in Apply.

If the identity reappears in a later latest Snapshot, Reconciliation offers `Clear*SourceMissing`; clearing is also explicit and preserves all knowledge.

## 18. External read-only security and least privilege

### 18.1 Universal Provider rule

The external account should have only login/connect plus metadata visibility required for the selected schemas. It must not be `SYS`, `SYSTEM`, Oracle DBA, PostgreSQL superuser, SQL Server `sysadmin`/`db_owner`, or an application write account. The Provider never requires INSERT/UPDATE/DELETE/DDL permissions and never queries business rows.

Metadata visibility and business-data read permission are vendor-specific and cannot be made identical by the core. Included schemas are an application scope filter, not an authorization boundary; the external database remains responsible for permissions.

### 18.2 Oracle 19c

Required semantic permissions are:

- `CREATE SESSION`;
- ability to query the approved `ALL_*` Data Dictionary views;
- sufficient catalog visibility for every explicitly included owner/schema.

No Core operation requires `SYS`, `SYSTEM`, DBA role, `SELECT ANY TABLE`, `SELECT ANY DICTIONARY`, business-object write, or `EXECUTE` on `DBMS_METADATA`.

Oracle's `ALL_*` visibility follows what the connected account can access. There is no narrow, universal, schema-scoped grant that proves all physical metadata while necessarily forbidding all business-row reads. Where ordinary object visibility is insufficient, a database administrator must choose an organization-approved metadata exposure method. `SELECT_CATALOG_ROLE` is a broad catalog role and is **not** the default recommendation merely for convenience. Any use requires a security review and still does not authorize the application to query business rows.

Test Connection validates login, Oracle major version 19, current service/container context, included-schema/catalog-query coverage, and safe capability probes. It does not perform a write test.

### 18.3 PostgreSQL portability boundary

The later Provider must use a non-superuser account with database `CONNECT` and only the metadata visibility required by its explicit `pg_catalog`/safe-information-function query set. It must not require table SELECT/DML or schema `CREATE`. Because metadata views/functions can filter rows by privilege, the implementing slice must prove its exact visibility/grant matrix with a real PostgreSQL test role and must not treat privilege-filtered empty rows as proof of physical absence.

PostgreSQL Core uses explicit, supported-major-version-adapted `pg_catalog` queries as its authority, with safe information functions where needed. It does not use `information_schema` as the complete Core source: several Information Schema views are privilege-filtered, and [`referential_constraints`](https://www.postgresql.org/docs/current/infoschema-referential-constraints.html) can require ownership or non-SELECT write access to the referencing table, which conflicts with this feature's no-DML account boundary. The later Provider freezes supported PostgreSQL majors and an explicit catalog column list because [system catalogs can change between majors](https://www.postgresql.org/docs/current/bki.html). Type namespace/domain/array identity comes from [`pg_type`](https://www.postgresql.org/docs/current/catalog-pg-type.html), with bounded display declarations produced through documented [type-formatting information functions](https://www.postgresql.org/docs/current/functions-info.html).

The minimum account direction is database `CONNECT` plus ability to query the required `pg_catalog`/safe information functions; schema `USAGE` may be granted for explicit scope resolution, but Core does not require table SELECT/DML or schema `CREATE`. The implementing slice must prove the exact grants, FK reference closure, and metadata visibility with a real low-privilege PostgreSQL role.

### 18.4 SQL Server portability boundary

The later SQL Server account should have database `CONNECT` plus `VIEW DEFINITION` at the narrow approved database/schema/object scope, without table SELECT or write permissions and without `db_owner`/`sysadmin`. A directly testable schema-scoped pattern is `GRANT VIEW DEFINITION ON SCHEMA::[schema] TO [principal]`; it grants metadata visibility, not business-row SELECT. Microsoft documents that catalog visibility is limited to owned/permitted securables and that `VIEW DEFINITION` grants metadata visibility in [Metadata Visibility Configuration](https://learn.microsoft.com/en-us/sql/relational-databases/security/metadata-visibility-configuration) and [GRANT Schema Permissions](https://learn.microsoft.com/en-us/sql/t-sql/statements/grant-schema-permissions-transact-sql).

The implementing Provider must prove cross-schema FK reference visibility with the exact low-privilege role. It must also map SQL Server index families explicitly: [`sys.index_columns.key_ordinal = 0`](https://learn.microsoft.com/en-us/sql/relational-databases/system-catalog-views/sys-index-columns-transact-sql) is not universally an INCLUDE column. `NativeIndexKind` and non-key-part roles preserve the distinction; a discovered Core index that cannot be completely represented fails the Run. `Unavailable` is reserved for a declared non-Core property/capability, never for silently omitting a required Index.

SQL Server table/column comments use only the explicitly frozen `MS_Description` extended-property convention. Other extended properties are not rebranded as comments or copied into Provider metadata by default; changing that convention requires a Provider contract amendment.

## 19. Timeouts, cancellation, limits, and performance

### 19.1 Initial engineering defaults

The implementation uses centrally validated settings, not arbitrary per-request values:

| Setting | Initial default |
| --- | --- |
| connection timeout | 15 seconds |
| catalog command timeout | 60 seconds |
| overall Discovery timeout | 15 minutes |
| maximum included schemas | 128 |
| maximum Objects | 25,000 |
| maximum Columns | 250,000 |
| maximum total constraints/indexes | 250,000 |
| maximum Sequences | 10,000 |
| maximum canonical Snapshot payload | 128 MiB |
| maximum selected actions per Sync Plan | 2,000 |

These are safety defaults, not a promise that every database below the limit meets a latency target. Raising them requires measured memory/SQLite/API review and remains deployment configuration, not a Profile-supplied bypass.

Limit breach fails the Run as `LimitExceeded` with safe counts and no Snapshot.

### 19.2 Cancellation and failure

- All application/Provider methods accept the request/Run `CancellationToken`.
- Cancellation closes/cancels the active command, marks the Run Cancelled, and persists no Snapshot/Difference.
- Command timeout maps to `Timeout`; overall timeout cancels the full Run.
- A client disconnect does not cancel an accepted scan. The committed `Queued` row in SQLite is the only authoritative queue fact. A bounded in-process channel may carry a best-effort Run-ID wake hint, but channel-full, missed-write, or process-loss cannot lose the Run.
- Each hosted `BackgroundService` checks SQLite at startup and on a bounded periodic poll, transactionally claims an eligible Queued row with owner/lease token, and then transitions it to Running. Multi-instance workers cannot share a claim. This is not a fire-and-forget request Task or a generic scheduler.
- Cancel on Queued atomically changes it to Cancelled. Cancel on Running durably sets `CancellationRequestedAt/By`; any instance may issue it. The current owner observes that request on heartbeat/between catalog commands, cancels its operation token, and only the matching lease owner writes terminal Cancelled after command exit. Finalize also rejects a pending cancellation.
- A process loss during Running is not automatically replayed against the external database. Expired-lease recovery, using a token-checked transaction, records Cancelled when a durable cancel was pending or Failed/`RunInterrupted` otherwise; a human may then trigger a new Run.

### 19.3 No N+1

Providers issue bounded bulk catalog queries by metadata kind and selected-schema chunks. They do not query once per table and never once per column. Oracle Core should require approximately one bounded query per catalog group, with chunking only for vendor bind/list limits.

Rows are read asynchronously with sequential readers and cancellation. Provider-side joins/grouping collect constraints/index columns in bulk; the Canonical builder resolves parent identities in memory. Snapshot JSON is persisted only after all groups validate. Diff runs over canonical identities/hashes, not live vendor catalogs or Hub display order.

## 20. Concurrency

### 20.1 Discovery Run

Only one active (`Queued` or `Running`) Run per Profile is allowed. This invariant must be database-enforced by a partial unique index or equivalent transactional constraint, not only an in-process semaphore. A second trigger returns 409 `DiscoveryAlreadyRunning`.

Run acceptance captures Profile Configuration Revision, Secret Version, requested included schemas/options, and the requesting actor. Worker connection/finalization captures the actual principal, provider/target identity, final scope fingerprint/Scope Generation, and compatible BaseSnapshotId. Profile/Secret mutation is rejected while the Run is active. Every claim/heartbeat/terminal/finalize write matches the current lease token. A completed Run always describes the exact revisions and final scope it used.

### 20.2 Diff baseline

The Base Snapshot is the latest compatible successful complete Snapshot captured under the single-run invariant. Failed, Cancelled, incompatible-scope, and persistence-failed Runs never influence Diff.

### 20.3 Discovery and Manual Sync

- Review and Plan generation can coexist with a Running scan.
- Apply performs no durable intermediate state: concurrent Ready plans serialize at the SQLite immediate transaction, and the later request fails token/precondition revalidation if the earlier Apply changed a shared target.
- Apply is pinned to one latest successful Snapshot and revalidates it as described above.
- A newer successful Snapshot supersedes every unapplied older Plan.
- Scan completion and Apply each use short System Knowledge Hub transactions; neither holds a SQLite transaction while waiting on an external database.

### 20.4 Hub mutation races

Every current target token is re-read inside the Apply transaction. An intervening manual edit, status operation, delete, link change, or earlier Plan Apply produces a stale token/conflict and no writes. Manual Sync does not silently overwrite a human who edited after Preview.

## 21. Error model and observability

### 21.1 Normalized error codes

The feature uses at least:

```text
ConnectionFailed
AuthenticationFailed
InsufficientPrivilege
UnsupportedDatabaseVersion
MetadataQueryFailed
Timeout
Cancelled
ProviderUnavailable
SnapshotPersistenceFailed
SecretMissing
SecretUnavailable
LimitExceeded
DiscoveryAlreadyRunning
BaselineIncompatible
UnsupportedIdentifierCollision
UnresolvedForeignKeyReference
UnsupportedOrdinal
ActiveOrdinalConflict
DiscoveryBindingConflict
SyncPlanSuperseded
ConcurrencyConflict
RunInterrupted
```

Known vendor failures map to one code plus an optional allowlisted vendor code such as `ORA-01017`. Raw driver messages do not enter the API or durable Run error text. Safe summaries are bounded and actionable without echoing a connect identifier or SQL.

### 21.2 API and durable error envelope

API errors follow the existing safe envelope/correlation style. `details` may include only safe resource IDs, Profile alias, ProviderType, database/service display name where the caller is authorized, normalized code, vendor code, limit/count, and correlation ID.

It never contains password, ciphertext, `SecretReference`, Username for non-Administrator reads, connection string, host credential, raw Oracle descriptor, SQL, stack, internal type name, raw provider error message, or arbitrary Provider metadata.

### 21.3 Logging and audit

Operational logs allowlist:

```text
correlationId
profileId
runId
providerType
safe profile alias
normalized error/vendor code
operation category
duration
counts
terminal status
```

Expected provider failures are logged without the raw exception object/message. Unexpected failures are not swallowed: the Run is Failed, a correlation is emitted, and internal exception type/stack may go only to an approved protected diagnostic sink after secret-bearing messages/data are excluded. Current console logging does not by itself satisfy a real Production diagnostic/retention gate.

Audit events include Profile create/update/enable/disable, Secret set/replace/clear, Test Connection, Run trigger/cancel, Plan create/confirm/apply, selected action counts, actor, and result. Audit records contain no credential.

### 21.4 Required redaction tests

Implementation tests must inject canary secrets into provider exception text and connection-string delimiters, then prove the canary is absent from:

- API responses;
- captured logs;
- Run/Snapshot/Difference/Plan persistence;
- ordinary SQLite columns queried by the fixture;
- task-owned WAL after the tested operation, except encrypted Secret ciphertext whose plaintext is not recoverable without the key ring.

Wrong/missing key ring, restart with the same key ring, stale Secret/Profile token, missing Secret, wrong password, timeout, privilege loss, and unexpected provider failure all require focused fail-closed coverage.

## 22. Oracle 19c and Docker verification strategy

### 22.1 Evidence levels

Oracle validation has two deliberately separate gates:

| Gate | Purpose | What it may prove |
| --- | --- | --- |
| task-owned Oracle 19c container | repeatable development/integration verification | connection, capability, Core catalogs, Canonical mapping, Snapshot/Diff, failure/redaction, cleanup |
| company-owned real Oracle 19c service | later real-domain acceptance | actual network/service/security/grant/scale behavior in the approved company environment |

A container PASS is not a Production deployment, company-network acceptance, or `SEC-04` closure. A real-company acceptance run uses a separately approved read-only credential and configuration; no fixture task may connect to or mutate it.

Oracle 10g/11g images, test matrices, compatibility claims, bridges, and acceptance gates do not exist in the approved sequence. This is the requester's explicit .NET 8/Oracle scope amendment, not an unrecorded skip.

### 22.2 Container fixture

`DBDISC-ORACLE-B01` may add one reusable integration fixture based on an Oracle-authorized 19c container/image workflow. It creates only a task-owned database/PDB/schema, minimum-privilege discovery user, and deterministic structures:

```text
Table and View
ordinary and nullable Columns
PK, cross-table FK, Unique Constraint
ordinary and function-based Index
Sequence
table/view and column Comments
quoted/case-sensitive identifier fixture
cross-included-schema FK reference-closure fixture
```

The provider account is not the fixture owner and has no DDL/DML/business-row SELECT granted merely for Discovery. Fixture setup/mutation runs under a separate task-owned owner, then the read-only account runs Test/Discover. A second DDL state exercises Added, Changed, MissingFromSource, Unchanged, comment/default/type/ordinal, and no-rename semantics. Expected Canonical JSON/hash comparisons are deterministic and free of connection/authentication secrets introduced by the fixture; intentionally authored bounded default/comment/index-expression metadata remains structural fixture content.

The container, volumes, schema, credentials, temporary SQLite database, Data Protection key directory, attachment root, and ports are task-owned and cleaned after verification. CI may reuse the same fixture when its licensed runner supports containers. Docker is never a runtime or Production requirement.

### 22.3 Required Oracle implementation gates

The Oracle Provider cannot pass until focused tests cover:

- supported 64-bit .NET 8 driver and Oracle major-version 19 enforcement;
- `ALL_*` visibility, every Core catalog group, FK reference closure, and one service/PDB context;
- low-privilege success and missing-privilege failure with no Partial Snapshot;
- concurrent-DDL/internal-consistency failure behavior;
- limits, timeout, cancellation, lease interruption, redaction, and no secret in task-owned SQLite/WAL/log/API;
- no business-row query and no DDL/DML from Test/Discover;
- exact identifiers, null source ordinal, function index, Sequence volatility exclusion, and stable hash/Diff behavior.

## 23. PostgreSQL and SQL Server portability review

The architecture portability review is **PASS at design level**. It does not claim that either production Provider has been implemented or tested in A01.

| Boundary | PostgreSQL direction | SQL Server direction | Core impact |
| --- | --- | --- | --- |
| Profile locator | Host/Port/DatabaseName/UsernamePassword | Host/Port/DatabaseName/UsernamePassword | no new Profile model |
| catalog source | version-adapted `pg_catalog` plus safe information functions | `sys.schemas`, `sys.tables`, `sys.views`, `sys.columns`, constraints/FK/index/sequence catalogs, `sys.extended_properties`, `sys.types` | Provider-only query/mapping |
| identifiers | preserve catalog-returned folded/quoted text exactly | preserve catalog-returned text; database collation is not simulated in .NET | opaque ordinal Canonical key |
| native types | namespace/domain/array identity plus bounded declaration | schema-owned alias/CLR/MAX identity plus bounded declaration | structured `NativeDataType` already covers it |
| indexes | expression/partial/include mapping | rowstore plus explicit native kind/non-key roles; specialized families gated | Canonical Index already covers ordinary forms and exposes unsupported capability honestly |
| visibility | low-privilege `pg_catalog` matrix and FK closure must be proven | schema/object `VIEW DEFINITION` matrix and cross-schema FK closure must be proven | same Complete/Failed semantics |
| sync | Canonical identities and typed bindings | Canonical identities and typed bindings | no Provider condition in Apply |

PostgreSQL is deliberately implemented immediately after Oracle Core, before committing all review/sync UX, to prove the second Provider against real catalogs while changes to the abstraction remain cheap. SQL Server follows Manual Sync and proves the third Provider plus collation/alias/index-family boundaries. Any Provider that forces Oracle, PostgreSQL, or SQL Server catalog fields into Application/Domain fails its task gate.

The later Provider tasks use task-owned local databases/accounts only, record their supported server-major matrix, and run the same Canonical conformance suite. Availability of local PostgreSQL/SQL Server is an opportunity for those tasks, not evidence produced by A01.

## 24. Performance and consistency direction

The performance contract is intentionally simple:

- bulk catalog reads by metadata group and bounded schema chunks;
- sequential/asynchronous readers and in-memory normalization/joins;
- no per-table/per-column catalog N+1;
- no SQLite write transaction while connecting, reading catalogs, parsing up to the Snapshot limit, or computing Diff/Reconciliation;
- deterministic Canonical hash/Diff outside the short finalize transaction;
- durable pageable Added/Changed/Missing entries, with Unchanged detail derived only on demand;
- configured hard limits from §19.1 and no Profile override that bypasses them.

Pagination is an API/read-model concern, not catalog query-per-row behavior. The Provider may stream rows internally but cannot expose an incomplete stream as a successful Snapshot. Performance tuning, compression, incremental catalogs, and parallel queries are deferred until measurements preserve the consistency and least-privilege rules.

## 25. Authorization matrix

The current role order remains `Viewer < Editor < Administrator`; no Discovery-specific role is introduced.

| Resource/action | Minimum role | Frozen exposure/rule |
| --- | --- | --- |
| list/read active Source-linked Runs, complete Snapshots, Differences, capability/count summaries | Viewer | sanitized structural data only; no host, username, Secret reference, raw provider error, SQL, or connection details |
| list/read draft/ready/applied Sync Plans and preview results | Viewer | same active Source read boundary; Viewer cannot select, confirm, or apply |
| create/edit/enable/disable Profile; view Host/Port/DatabaseName/ServiceName/Username/hasSecret | Administrator | operational connection metadata is Administrator-only |
| set/replace/clear Secret | Administrator | separate write-only body; response is `hasSecret` only |
| Test Connection; trigger/cancel Run | Administrator | external access is an administrative operation; audited |
| create/change Preview selection; confirm; Apply Manual Sync | Editor | Administrator inherits; explicit existing Database Knowledge write authority plus Source access required; all tokens and field allowlists still enforced |
| view history for a disabled Profile or soft-deleted Source | Administrator | retained operational audit boundary; ordinary active-tree reads do not reveal it |
| archive/soft-delete Source/Object/Column | existing Delete authorization | Discovery grants no shortcut and does not change the existing policy |

All endpoints recheck Source state/ownership/reference validity. An Editor cannot use Sync to manage credentials or trigger an external connection. No role can auto-delete, auto-confirm, overwrite protected human fields, or bypass stale-token handling.

## 26. UI and workflow direction

The later frontend exposes five focused surfaces rather than one large page:

1. **数据库连接配置 / Database Connection Profiles** — Administrator profile list/editor, write-only secret replacement, enable/disable, Test Connection, and safe status.
2. **发现运行 / Discovery Runs** — trigger/cancel, queued/running/terminal progress, counts, normalized error, scope/privilege warning, and history.
3. **发现快照 / Discovery Result & Snapshot** — read-only schema/object/column/constraint/index/sequence tree and capabilities for one immutable Snapshot.
4. **差异审查 / Diff Review** — Added/Changed/MissingFromSource/Unchanged filters, before/after fields, visibility warning, unsupported/collision badges, and selection entry point.
5. **手工同步计划 / Manual Sync Plan** — selected actions, protected-field explanation, target-token conflicts, exact Preview hash/confirmation state, atomic Apply result, and link-existing choices.

The primary path is:

```text
配置连接 → 设置凭据 → 测试连接 → 扫描
→ 查看快照 → 与兼容基线比较 → 审查并选择
→ 生成预览 → 明确确认 → 应用 → 查看结果
```

UI never renders the password/ciphertext/reference and never echoes a raw driver message. MissingFromSource is translated as “源端未发现”, never “已删除”. Visibility/scope warnings remain adjacent to Missing selections and confirmation. Buttons are role/state-gated for convenience, while backend authorization remains decisive.

## 27. Alternatives considered

| Alternative | Decision | Reason |
| --- | --- | --- |
| one-time import utility | Rejected | loses immutable history, deterministic Diff, scope compatibility, and explicit reconciliation |
| treat external engines as `KnowledgeHubDbContext` providers | Rejected | confuses product persistence with read-only external discovery and violates the frozen architecture |
| generic ADO.NET schema APIs only | Rejected | cannot provide the required complete, least-privilege, vendor-correct metadata contract |
| raw vendor JSON as the Snapshot | Rejected | leaks catalogs upward and makes cross-provider Diff/Sync branch on vendor fields |
| `DBA_*`, `SYS`/`SYSTEM`, DBA/sysadmin defaults | Rejected | violates least privilege and makes ordinary deployment unsafe |
| `DBMS_METADATA.GET_DDL` plus SQL parsing | Rejected | broad privilege/complexity and unstable parser surface for Core facts already in catalogs |
| auto Sync, auto rename, or auto delete | Rejected | can corrupt human knowledge and mistakes visibility changes for physical deletion |
| mutable per-kind relational Snapshot tables in v1 | Rejected | premature schema breadth; immutable versioned Canonical JSON plus pageable Difference is sufficient |
| provider-specific Profile/Secret models | Rejected | Host/locator/auth/options fit one allowlisted aggregate; vendor behavior stays in Provider |
| generic secret plug-in framework now | Rejected | local Data Protection store plus narrow resolver boundary meets v1; no selected external manager exists |
| generic polymorphic Discovery binding | Rejected | loses FK integrity; two typed Object/Column bindings are the only Apply targets |
| include ObjectType in object identity | Rejected | a same-name Table↔View transition should be Changed, not a conflicting new identity |
| adjacent Difference alone as Sync source | Rejected | a persistently absent bound item would disappear from subsequent adjacent diffs |
| force IncludedSchemas to contain the full FK transitive closure | Rejected | unexpectedly expands scan scope; bounded reference-only stubs preserve required FK identity |
| support Oracle 10g/11g through a sidecar/legacy runtime | Rejected | explicit requester direction selects .NET 8 + Oracle 19c only |

## 28. Frozen decisions

The binding decisions are:

1. external Providers and System Knowledge Hub persistence are separate boundaries;
2. all Discovery operations are read-only/metadata-only with closed catalog queries;
3. one Source has zero or one fixed Profile; enabled Profile blocks Source delete;
4. local v1 Secrets use a separate Data-Protection-encrypted row and write-only API; future stores stay behind one narrow resolver;
5. Canonical Snapshot is immutable, complete-or-failed, structured/native-type-preserving, vendor-neutral, and hashable;
6. Oracle major version 19 is the sole Oracle target; one service/PDB context equals one Profile;
7. one active queued/running Run per Profile uses a hosted worker plus renewable lease;
8. Diff is scope-generation compatible, deterministic, and never infers rename;
9. exact catalog identifiers form ordinal opaque keys; SQLite `NOCASE` is not the Discovery identity authority;
10. Manual Sync is Reconciliation-based Preview → explicit hash-bound Confirm → atomic Apply;
11. Sync updates only allowlisted structural fields, never human knowledge, KnowledgeStatus, Evidence, confirmation, or deletion;
12. MissingFromSource is a typed binding state, not a delete/archive instruction;
13. PostgreSQL is the second Provider proof, followed later by SQL Server, without core interface changes;
14. Docker is task-owned verification infrastructure, never a Production dependency or acceptance substitute.

## 29. Open questions, constraints, and gaps

### 29.1 Blocking decisions

There are no blocking human decisions for DBDISC-B01. SID, TCPS/wallet, integrated authentication, external Secret Manager choice, full DDL, advanced capabilities, and true cross-query point-in-time snapshots are deliberate future amendments, not ambiguous v1 choices.

### 29.2 Existing gates retained

- `SEC-04` remains the existing real-Production gate for protected Data Protection keys at rest, HTTPS/reverse-proxy hardening, and operational logging/retention. DBDISC must not duplicate, close, or bypass it.
- Real-company Oracle 19c access/grants/network/scale acceptance remains a later external evidence gate. Container success cannot close it.

### 29.3 New, non-blocking tracked design gaps

| Gap | Impact | Owner/closure |
| --- | --- | --- |
| `DBDISC-GAP-001` current SQLite `NOCASE` Object/Column uniqueness can collapse valid external identifiers | Snapshot/Diff safe; affected Apply blocked | B04 technical identity migration and collision tests |
| `DBDISC-GAP-002` DatabaseObject has no technical database-comment field | table/view comment Snapshot-only until mapping exists | B04 adds the minimal technical field/read projection; never uses BusinessDescription |
| `DBDISC-GAP-003` no Hub holder for FK/Unique/Index/Sequence | Snapshot/Diff/Review only | accepted v1 limitation; later use case must justify typed holders |
| `DBDISC-GAP-004` provider catalog visibility cannot prove every physical object | privilege change may resemble Missing | stable principal/scope, query coverage, optional visibility fingerprint, prominent warning, manual confirmation, provider grant gates |

These are not blockers for B01. B04 cannot pass with unresolved GAP-001 or GAP-002 for actions that need them. GAP-003 and GAP-004 are explicit accepted product limits rather than silent omissions.

## 30. Unique follow-up task sequence

Only this sequence is approved; no Oracle 10g/11g compatibility slice remains:

1. **DBDISC-B01 — Connection Profile + Secret Boundary + Oracle 19c Test Connection**

   Add the Profile/Secret persistence and Administrator APIs, local Data Protection store, Oracle 19-only connection tester, normalized/redacted failures, Source dependency, focused authorization/concurrency/restart tests, and non-production documentation. No catalog Discovery.

2. **DBDISC-B02 — Canonical Snapshot + Discovery Run + Diff Foundation**

   Add Canonical contracts/validation/hash, scope generations, durable queued/leased worker, Run/Snapshot/Difference persistence/APIs, limits/cancellation, and deterministic fake-provider conformance tests. No production vendor catalog Provider.

3. **DBDISC-ORACLE-B01 — Oracle 19c Core Provider**

   Implement the approved `ALL_*` bulk queries, capability/context/version checks, reference closure, low-privilege 19c container fixture, Canonical conformance, failure/redaction, and real-company acceptance instructions.

4. **DBDISC-PG-B01 — PostgreSQL Provider and Second-Provider Proof**

   Freeze supported majors, implement explicit `pg_catalog` mapping and low-privilege local integration gate, and prove no Oracle leakage/core interface change.

5. **DBDISC-B03 — Discovery Result + Diff Review UX**

   Build sanitized Run/Snapshot/Diff APIs/read models and the focused UI surfaces with scope/visibility/unsupported warnings. No Apply.

6. **DBDISC-B04 — Manual Sync Planning + Apply**

   Close required identity/comment mappings, add typed bindings/Reconciliation, Preview/hash-bound Confirm/atomic Apply, collision-safe ordinal handling, field protection, Source Missing, and role/concurrency tests.

7. **DBDISC-SQLSERVER-B01 — SQL Server Provider**

   Freeze supported majors, implement `sys.*` mapping, schema-scoped `VIEW DEFINITION` and FK visibility tests, native type/collation/index-family conformance, and prove the third Provider.

8. **DBDISC-VERIFY — Database Discovery End-to-End Verification**

   Run focused backend/frontend/container/local-provider gates, task-owned persistence/redaction/cleanup checks, documentation synchronization, and separately report Internal Pilot, container, local Provider, real-company Oracle, Production, and push evidence.

Each slice must stop at its boundary, update the closest maintainable documentation/index, use task-owned persistence/runtime paths, and produce one task-specific commit/push only after applicable verification passes.

## 31. Acceptance criteria

DBDISC-A01 is approved because this decision provides all of the following:

- [x] read-only/metadata-only behavior and external no-write/no-business-row boundary;
- [x] vendor-neutral Provider/tester/capability contracts with no catalog leakage;
- [x] unified Source-linked Profile and explicit locator/lifecycle choices;
- [x] no plaintext Secret persistence/disclosure and a concrete encrypted local v1 boundary;
- [x] complete Canonical Snapshot, type fidelity, Core scope, capabilities, FK closure, and failure semantics;
- [x] immutable Run/Snapshot/Difference history and Added/Changed/MissingFromSource/Unchanged meanings;
- [x] stable exact identity/case behavior, incompatible-scope protection, and no rename guess;
- [x] hash-bound human-confirmed Manual Sync, protected human fields, typed bindings, concurrency, and no auto delete;
- [x] Oracle 19c-only .NET 8/provider/CDB-PDB/catalog/verification boundary; the later requester direction explicitly supersedes the original 10g/11g priorities;
- [x] PostgreSQL/SQL Server feasibility review with an early second-Provider proof;
- [x] Docker/task-owned verification strategy and separate real-domain/Production evidence;
- [x] timeout, limits, worker/lease/cancellation, performance, normalized errors, redaction, roles, and UI direction;
- [x] separation from any future `PERSISTENCE-MULTI-DB` work and no second mutable source of truth;
- [x] unique executable follow-up sequence with no unresolved Blocker/High architecture issue.

Therefore:

```text
DBDISC-A01 APPROVED
DBDISC-B01 READY: YES
```

## 32. A01 verification and delivery gate

This is a documentation-only architecture task. Full backend/frontend builds and tests are not applicable and must not be run for ceremony. Close-out requires:

- final mapping review against current Source/Object/Column implementation and frozen contracts;
- Secret/authorization/Production/soft-delete/persistence-provider boundary review;
- Oracle 19c official-driver/catalog/visibility review and PostgreSQL/SQL Server feasibility review;
- review that Snapshot/Diff/Sync does not create a second mutable source of truth;
- `git diff --check`;
- `docs/DOCUMENT_INDEX.md` synchronization;
- final Git diff/status review and delivery per root `AGENTS.md`.

No backend, frontend, migration, package, runtime database, WAL/SHM, Data Protection key, container, server, or verification process is created by A01.

Recorded A01 result on 2026-08-29:

| Gate | Result |
| --- | --- |
| current Source/Object/Column, frozen contract, delete/concurrency, and mapping review | PASS |
| Secret/Data Protection, authorization, Production, logging/redaction, and persistence-provider separation review | PASS |
| Oracle 19c official-driver/catalog/CDB-PDB/visibility review | PASS |
| PostgreSQL and SQL Server Canonical/Profile/identity/least-privilege portability review | PASS |
| independent architecture review of Run queue/lease/cancel, Scope/Diff, binding uniqueness, Manual Sync atomicity, and task sequencing | PASS — no remaining Blocker/High |
| `docs/DOCUMENT_INDEX.md` synchronization | PASS |
| full working-tree `git diff --check` and task-staged `git diff --cached --check` | PASS |
| backend/frontend build and test | NOT RUN — not applicable to this documentation-only task |
| runtime/persistent-data/process cleanup | PASS — no runtime started and no database/key/container/process created or changed |
