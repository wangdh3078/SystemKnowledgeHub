# DBDISC-ORACLE-B01 Oracle 19c Core Provider Verification Report

## Result

```text
DBDISC-ORACLE-B01 PASS
DBDISC-PG-B01 READY: YES
```

DBDISC-ORACLE-B01 已在 DBDISC-B01/B02 冻结边界上接入实际 Oracle 19c `IDatabaseDiscoveryProvider`。本轮没有实现 PostgreSQL、SQL Server、Discovery UI、Sync、Binding、B04 technical identity migration 或新的 EF migration。

## Implementation

- 生产 DI 仅为 `DatabaseProviderType.Oracle` 注册 Oracle Core Provider；PostgreSql/SqlServer 继续按 B02 返回 `ProviderUnavailable`。
- Oracle Managed Driver 使用 `OracleConnectionStringBuilder` 从已验证的 Host/Port/ServiceName/Username 和短生命周期 Password 构建连接；连接不池化，connection timeout 为 15 秒，catalog command timeout 为 60 秒，B02 overall timeout/cancellation/lease 继续生效。
- Discover 在一条 Oracle session 内完成版本、Service/PDB、Schema 可见性和完整 Core catalog 读取；不在等待 Oracle 时持有 SQLite transaction。
- Oracle major 必须等于 19。18、21 和不可解析版本均安全失败为 `UnsupportedDatabaseVersion`；Service 不匹配和 `CDB$ROOT` 被拒绝，不执行 `ALTER SESSION`、跨 PDB fan-out 或 database link。
- Catalog 查询是静态审核清单，Schema 只作为 bind value；没有用户 SQL、任意 SQL 片段或 identifier interpolation。
- 所有 command/reader/connection 均异步执行、传播 cancellation 并异步释放；取消不能被解释为空成功结果。

## Closed Query Inventory

Core 读取仅使用：

```text
ALL_USERS
ALL_TABLES
ALL_VIEWS
ALL_TAB_COLUMNS
ALL_TAB_COMMENTS
ALL_COL_COMMENTS
ALL_CONSTRAINTS
ALL_CONS_COLUMNS
ALL_INDEXES
ALL_IND_COLUMNS
ALL_IND_EXPRESSIONS
ALL_SEQUENCES
```

能力探测仅使用批准的 `ALL_TAB_IDENTITY_COLS`、`ALL_TAB_COLS`、`ALL_MVIEWS`、`ALL_TAB_PARTITIONS`、`ALL_SYNONYMS`、`ALL_TRIGGERS` 和 `ALL_SEQUENCES` 零行 probes。上下文只读取 `SYS_CONTEXT`。查询清单静态测试确认不存在 `DBA_*`、`SYS.*`、`SYSTEM.*`、`DBMS_METADATA`、业务表、`SELECT *`、DDL、DML、lock 或 `FOR UPDATE`。

## Canonical Mapping

- 保留 Oracle 返回的 Owner/Object/Column/Constraint/Index/Sequence 精确大小写；身份键使用 versioned length-delimited、ordinal components，不调用 `ToUpper`、`ToLower` 或 `OrdinalIgnoreCase` 处理 catalog identity。
- Table、View、Column、comments、PK、FK、Unique、Index、Sequence 全部映射到 B02 provider-neutral contracts。
- `NativeDataType` 保留 catalog type、type owner namespace、确定性 declaration、byte/character length semantics、knownness-aware precision/scale；Oracle 负 numeric scale 由 canonical validator 正确接受。
- `COLUMN_ID = null` 保留为 `SourceOrdinal = null`，不制造 ordinal。
- 约束和索引列按 Oracle `POSITION` 排序；父对象、列、重复 identity、列数、引用列数和 concurrent-catalog consistency 均 fail closed。
- out-of-scope FK target 使用成批 bind lookup 建立有界 `ReferenceOnly` closure stubs；缺失/不可见引用返回 `UnresolvedForeignKeyReference`，不丢弃 FK。
- 普通和 function-based Index 均保留；表达式来自 `ALL_IND_EXPRESSIONS` 并受结构文本上限保护，不调用/解析 DDL。
- Oracle Sequence 使用 Provider-implicit `NUMBER` type，`StartValue = null`；查询、row contract、canonical JSON/hash 均不包含 volatile `LAST_NUMBER`。
- `ALL_VIEWS` 不读取 View SQL text。

## Capabilities

能力快照明确投影：

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

状态只使用 `Supported / NotSupported / Unavailable / NotApplicable`。目录不可见是 `Unavailable`，不是 `NotSupported`；Full DDL 明确为 `NotSupported / CoreScopeExcluded`，未被伪装成已收集能力。

## Failure, Redaction, and Complete-or-Failed

- Provider failure 映射为 `ConnectionFailed`、`AuthenticationFailed`、`InsufficientPrivilege`、`UnsupportedDatabaseVersion`、`MetadataQueryFailed`、`Timeout`、`Cancelled`、`LimitExceeded` 或 `UnresolvedForeignKeyReference`。
- 可持久化 vendor metadata 只接受严格 `ORA-` 加五位数字；raw Oracle message、SQL、descriptor、connection string、Password 和内部 exception 不进入 API、Run summary、audit 或日志。
- B02 worker 为 Provider normalized failures 保留准确 error code；任一 Core group、版本/上下文、limit、取消、timeout、closure 或 consistency 失败均不创建 Snapshot/Difference。
- Profile/Secret active-Run mutation guard、one-active-Run database constraint、lease/heartbeat/recovery、immutable Snapshot、baseline compatibility 和 diff 语义未改变。

## Deterministic Provider Tests

确定性 fake catalog reader 覆盖：

- 19c/non-19 version、Service mismatch、CDB Root；
- exact quoted/case-sensitive identifiers 与 null ordinal；
- Table/View/Column/comments 和 structured native types（含 CHAR semantics、schema-owned type、负 scale）；
- PK/FK/Unique、ordered columns、external FK reference closure；
- ordinary/function-based indexes 与 backing constraint；
- Sequence structural values及 `LAST_NUMBER` 排除；
- capability states、schema visibility、limits、unresolved FK、safe ORA mapping；
- 同内容不同 catalog row order 经 `CanonicalSnapshotService` 后 canonical JSON 和 SHA-256 完全一致；
- secret canary 不进入 canonical content。

## Verification

| Check | Result |
| --- | --- |
| Release build | PASS — 0 warnings, 0 errors |
| Oracle provider focused tests | PASS — 13/13 |
| Oracle normalized Run failure/no-Snapshot test | PASS |
| DBDISC-B01 focused regression | PASS — 8/8 |
| DBDISC-B02 focused regression | PASS — 13/13 |
| Full backend, Release, approved serial gate | PASS — 273/273, 0 failed, 0 skipped |
| EF pending model changes | PASS — no changes since last migration |
| Task-owned runtime smoke | PASS |
| Repository SQLite/WAL/SHM protection | PASS |
| `git diff --check` | PASS |

Full backend 使用既有 `REV-GAP-011` 批准的临时 serial runsettings；runsettings 已删除。

## Task-owned Runtime Safety

Runtime 使用 repository-supported `dotnet run --project src/SystemKnowledgeHub.Api -c Release --no-build --no-launch-profile`，并显式配置：

- task-owned SQLite；
- task-owned Data Protection key directory；
- task-owned Attachment StorageRoot；
- isolated port `51519`；
- task-owned local Administrator、Profile 和 Secret。

生产 worker 成功 claim committed Run，并通过已注册的 Oracle Provider 连接 task-owned、不可达的本机端口，安全终止为 `Failed / ConnectionFailed`；不是 B02 的 `ProviderUnavailable`。Run 没有 Snapshot/Difference。task DB 为 806912 bytes；数据库、key/log runtime 文件对数据库 Secret 和本地 Administrator password 的明文扫描均为 0 命中。

首次停止 `dotnet run` 父进程后仍有同次验证启动的子进程占用 task DB；该子进程按启动时间/PID 精确确认并停止。异常停止产生的 task-owned WAL/SHM 与 task DB、key、attachment、logs 一并删除。端口已释放，验证进程为 0，runtime root 已删除。

## Repository Data Protection

开始前和全部验证/Runtime/清理后，repository-owned SQLite 完全一致：

```text
Path: src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db
Size: 995328 bytes
mtime (UTC): 2026-08-30T04:51:50.7995165Z
SHA-256: B55F1652FA4CC5F0BC6A12B6EB205CAB2F505C8301AF296BDDF0CBF910A2FCE1
WAL: absent
SHM: absent
```

本任务没有连接、迁移、checkpoint、删除或覆盖 repository-owned database/WAL/SHM。

## Oracle 19c Integration

```text
ORACLE 19C INTEGRATION: NOT RUN — environment unavailable
```

本机没有 Docker，也没有任何 DBDISC/ORACLE task-owned connection environment。任务未连接公司 Oracle 或其他未知目标。因此没有声称真实 Oracle session、low-privilege grant matrix、真实 PDB/catalog 或真实并发 DDL integration PASS；根据任务明确规则，此环境不可用不使确定性实现验证失败。

## Existing / New Gaps

- 既有 `SEC-04` 保持开放；本轮不改变 Production HTTPS/proxy、Data Protection key-at-rest 和运维日志门禁。
- 既有 `DBDISC-GAP-001` 至 `DBDISC-GAP-004` 保持原状态；Provider read/Snapshot/Diff 不绕过这些后续 Sync 边界。
- task-owned/公司 Oracle 19c 的真实低权限、网络、PDB、catalog visibility、规模和 concurrent-DDL 证据仍需在批准环境可用时补跑；本轮没有新增 Blocker/High implementation gap。

## Final Status

```text
DBDISC-ORACLE-B01 PASS
IMPLEMENTATION: PASS
DETERMINISTIC PROVIDER TESTS: PASS
B01 REGRESSION: PASS
B02 REGRESSION: PASS
ORACLE 19C INTEGRATION: NOT RUN — environment unavailable
DBDISC-PG-B01 READY: YES
```
