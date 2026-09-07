# STABILITY-R02 — Search / Discovery Diagnostics Verification

- Date: 2026-09-07
- Baseline: `main`, `b627f26e1e330c31e3d537ff93568c8f2a9c92fc` (`fix: protect relation evidence history on removal`).
- Result: **STABILITY-R02 PASS / COMPLETE**.
- Authority: AGENTS.md; task STABILITY-R02; STABILITY-R01 and STABILITY-R01-R01 reports; Q02/current Search contracts; KC-B06 FTS; PORTAL-B04; DBDISC-A01 and DBDISC final R02 security/verification decisions.
- STABILITY-R01 remains COMPLETE. This task implements original findings #7/#8/#9 only; prior #1–#6 closure remains authoritative.

## Implementation and LIKE audit

`Persistence/LikeLiteral.cs` creates a literal substring pattern, escaping backslash first, then `%` and `_`; every user-controlled EF LIKE passes the explicit backslash escape argument. Input remains a query parameter, never a SQL fragment. No wildcard/regex mode was added.

Audit found **72 EF LIKE invocations on 64 source lines in eight files**, using nine pattern construction sites. All were literal user search and now use the shared helper:

| File / feature | Audited scope |
| --- | --- |
| SearchQueries | Seven structured global-search groups |
| RelationshipTargetResolver | Relationship target lookup |
| SystemQueries | System list |
| BusinessFunctionQueries | Business-function list |
| DatabaseKnowledgeQueries | Database-object list and matching fields |
| KnowledgeDocumentQueries | Ordinary document list (not global FTS) |
| UnknownItemQueries | Unknown-item list |
| UserQueries | User list |

Portal B04 search already uses ordinal-ignore-case C# `Contains` on published projections: no LIKE fix is applicable. Admin Portal literal `Contains` also has no wildcard contract. Global KnowledgeDocument search continues to use parameterized FTS MATCH, ORDER BY/LIMIT and a separate COUNT. Fixed/internal provider catalog SQL and intentional non-user patterns were not changed.

All seven structured groups now execute filtered COUNT plus ranked, title/Id-ordered, `Take(limitPerGroup)` result queries before materialization. Total is the uncapped matching count. Required-parent current predicates keep COUNT consistent with the existing result projection, including closed UnknownItems under deleted Systems. Integration ordering reconstructs the same two-system, single-system or party-name context.

SQLite connection-local `search_rank` and `SEARCH_ORDINAL_IGNORE_CASE` preserve the former .NET exact/prefix/context ranking (0/1/2/3) and Unicode ordinal-ignore-case title ordering. SQLite invokes these operations during ORDER BY before LIMIT; matching DTOs are not loaded into application memory for sorting. Id resolves equal-title ties. SQLite LIKE case behavior remains unchanged. No generic search engine or schema object was introduced. Seven structured types require 14 fixed queries; including FTS gives at most 16 search queries.

## Discovery diagnostics and security

One closed diagnostic path records only RunId, ProfileId/provider when known, Stage, sanitized PublicErrorCode, DiagnosticCategory, ExceptionType, HResult and allowlisted VendorCode. It never supplies an exception object, Message, Data, stack, SQL, connection string, credential or storage path to ILogger. WorkerLoop retains Error severity; other failure diagnostics use Warning.

Closed stages: WorkerLoop, LoadWork, CapabilityDetection, MetadataDiscovery, CanonicalPreparation, DifferenceCalculation, SnapshotPersistence, Finalize, LeaseMonitor, ConnectionTest. Categories distinguish KnownProviderFailure, PersistenceConcurrency, PersistenceConstraint, PersistenceFailure, EnvironmentFailure, UnexpectedProgramFailure and non-exception ValidationFailure. DbUpdateException is not automatically classified as concurrency.

Oracle, PostgreSQL and SQL Server unknown catalog-reader failures now preserve the original exception as the optional internal InnerException of DatabaseDiscoveryProviderException. Native mappings continue to return existing safe codes/summaries/vendor tokens. Three connection testers and the connection-test service use the same safe diagnostic path. Worker metadata/snapshot/terminal/lease/loop boundaries preserve sanitized durable failure behavior; LoadWork unexpected failures rethrow into the existing worker recovery boundary after logging. No exception cause enters DTOs or persistence. No catalog SQL or public contract changed.

## Verification evidence

| Gate | Actual result |
| --- | --- |
| Release build: `dotnet build SystemKnowledgeHub.sln -c Release --no-restore --disable-build-servers` | PASS; final build 0 warnings / 0 errors |
| Focused backend regression | PASS; 245 passed, 0 failed, 0 skipped; 4m22s |
| Final worker/diagnostics/connection API recheck after final diagnostic adjustments | PASS; 26 passed, 0 failed, 0 skipped; 54s |
| New SearchHardeningApiTests | PASS; 17 cases, also included in focused regression |
| Search frontend contract: `npm test -- src/features/search/api/searchContracts.spec.ts` | PASS; 1 test |
| EF `migrations has-pending-model-changes --project src/SystemKnowledgeHub.Api --configuration Release --no-build` | PASS; no pending model changes; task-owned absolute design-time path |
| `dotnet list SystemKnowledgeHub.sln package --vulnerable --include-transitive` | PASS; API and tests have no known vulnerable packages |
| `git diff --check` and staged diff check | PASS |

Focused backend selection covers Search (including KD FTS), PortalAnonymousReadApiTests, DatabaseDiscoveryRunApiTests/RunConcurrencyTests/DiagnosticsTests, all DiscoveryProviderTests and ConnectionTesterTests, ConnectionApiTests, ConnectionSecretSecurityTests, CanonicalSnapshotTests, SyncApiTests, and the affected Systems/BusinessFunctions/Users/UnknownItems/Relationships/DatabaseObjectsListRegistration/DatabaseKnowledge/KnowledgeDocuments API suites. Runs use the repository-approved serial xUnit runsettings (MaxCpuCount/MaxParallelThreads 1, ParallelizeTestCollections false). No full-backend pass is claimed.

The 520-System fixture plus one result in each of six other groups returns Total=526 at limits 1/5/20, excluding a deleted matching System. DbCommandInterceptor captures exactly 14 commands: seven COUNTs and seven result queries, each with ORDER BY, search_rank, ESCAPE and LIMIT. This proves the database applies the result bound, rather than merely truncating the response. Tests also cover types/invalid types, default and explicit limits, empty/100/101-character queries, exact/prefix/context rank, Unicode title ties, unchanged non-ASCII LIKE behavior, deleted-parent filtering and Integration context rank.

Literal canaries cover STATE_FLAG versus STATEXFLAG/STATEAFLAG, 50% versus 500/50ABC, mixed A_B%C, backslash, LOT and Chinese. Portal literal cases and published-only regression pass.

Nine real-worker fault cases cover auth, privilege, timeout, wrapped unknown cause, generic InvalidOperationException, IOException, DbUpdateConcurrencyException, DbUpdateException and SQLite constraint failure. API payload, stored Run ErrorSummary/SafeErrorMetadataJson and captured logs contain no password/raw-provider/SQL/connection-string canaries. Logs retain stage, category, IDs, original type and HResult. Snapshot failures leave no partial snapshots/differences. Additional tests exercise all stage values, cause identity and each tester's program/environment failures. Provider mapping, authorization, sanitization and Manual Sync regression pass.

During test development, fixture audit fields and SQL-interceptor attachment were corrected, and a missing test-only Http.Json import was added. The final gates above passed after those corrections; intermediate failures are not treated as successful evidence.

Frontend product changes: NOT APPLICABLE. Full Docker provider acceptance: NOT REQUIRED by this task; catalog SQL is unchanged and focused provider regression found no behavioral change. These results do not claim a Production deployment or PORTAL-VERIFY acceptance.

## DBSAFE and cleanup

Repository SQLite was accessed only for existence, filesystem metadata and SHA-256, never through SQLite/EF, copied, seeded, migrated or checkpointed. Before == after:

| File | Before and after |
| --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | 1,355,776 bytes; mtime UTC 2026-09-04T15:12:55.7499757Z; SHA-256 `12aca3ad05b199d3c8591c17d38fa1f145924df317956132b7d0dc2dfc6a328a` |
| `system-knowledge-hub.db-wal` / `system-knowledge-hub.db-shm` | Absent / absent |

Tests use task-owned Bootstrap/Discovery SQLite and isolated auxiliary state. EF used `C:/Users/wang/AppData/Local/Temp/stability-r02-ef/model-check.db` via SYSTEMKNOWLEDGEHUB_DESIGNTIME_SQLITE_PATH; model comparison created no database/directory. Environment override was removed. Verification is one-shot; test hosts and owned processes ended, no runtime server/port was launched, and task-only runsettings/baseline/staging artifacts were removed. No user/pre-existing process or data was cleaned up. No runtime artifacts or migration are committed.

## Final status

STABILITY-R02: **PASS / COMPLETE**

| Required status | Result |
| --- | --- |
| LIKE LITERAL UNDERSCORE | PASS |
| LIKE LITERAL PERCENT | PASS |
| LIKE ESCAPE CHARACTER | PASS |
| USER-CONTROLLED LIKE AUDIT | PASS |
| SEARCH SERVER-SIDE LIMIT | PASS |
| SEARCH TOTAL ACCURACY | PASS |
| SEARCH RANKING PRESERVED | PASS |
| SEARCH LARGE-FIXTURE BOUNDING | PASS |
| KNOWLEDGE DOCUMENT FTS REGRESSION | PASS |
| PORTAL SEARCH REGRESSION | PASS |
| DISCOVERY FAILURE STAGE | PASS |
| DISCOVERY SAFE CATEGORY | PASS |
| PROVIDER CAUSE PRESERVATION | PASS |
| WORKER DIAGNOSTICS | PASS |
| SNAPSHOT PERSISTENCE DIAGNOSTICS | PASS |
| CONNECTION TEST DIAGNOSTICS | PASS |
| NO RAW PROVIDER ERROR LEAK | PASS |
| NO SECRET LOGGING | PASS |
| PUBLIC ERROR CONTRACT PRESERVED | PASS |
| BACKEND REGRESSION | PASS |
| NO NEW MIGRATION | PASS |
| REPOSITORY DATA PROTECTION | PASS |
| CLEANUP | PASS |

REV-GAP-012: **OPEN / DEFERRED**. The historical SyncMigrationTests exact-table-count assertion is outside this focused gate and was not modified. No new gap was identified.

Original stability findings #1–#9: **CLOSED** (prior R01 evidence plus this implementation). **HC-A01 READY: YES**. HumanConfirmation identity/correction semantics remain for HC-A01. No HC-A01, PORTAL-VERIFY or Analysis Workspace work was started.

Delivery: task code, tests, report and navigation metadata belong to the single commit `fix: bound search and improve discovery diagnostics` on main. Push status and the resulting full SHA are reported separately after delivery. Pre-existing DBDISC_FINAL_R01 report and its index row remain uncommitted and untouched.
