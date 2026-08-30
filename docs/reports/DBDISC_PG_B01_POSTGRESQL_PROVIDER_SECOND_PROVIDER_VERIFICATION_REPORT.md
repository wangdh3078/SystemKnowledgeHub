# DBDISC-PG-B01 PostgreSQL Provider & Second-provider Proof Verification Report

## Result

```text
DBDISC-PG-B01 PASS
DBDISC-PG-B01 READY: COMPLETE
DBDISC-B03 READY: YES
```

PostgreSQL 18 Test Connection/Core Provider、确定性 mapping、真实官方 PostgreSQL Docker
production path、State 1/2 Canonical Snapshot/Difference、FK Reference Closure、低权限与业务行
零读取、失败/脱敏、generic Worker timeout/cancellation、Oracle/B01/B02/full regression、EF model、
repository-owned SQLite/WAL/SHM 保护和 Docker/runtime 清理门禁全部通过。实现没有新增 EF
migration、PostgreSQL-specific persistence、第二个 `DbContext` 或 Canonical/Diff vendor branch。

最终验证目标：

```text
POSTGRESQL IMAGE: postgres:18
VERIFIED POSTGRESQL VERSION: 18.6 (official image, Debian build)
NPGSQL: 10.0.3
```

## Scope

本 Slice 在既有 DBDISC-B01/B02 和 Oracle Provider 边界上新增：

- PostgreSQL 18 Test Connection；
- PostgreSQL `IDatabaseDiscoveryProvider`；
- 静态、参数化、只读的 PostgreSQL Core catalog reader；
- Schema/Table/View/Column/NativeDataType/comment/PK/FK/UQ/Index/Sequence/FK closure 映射；
- 确定性 Provider conformance tests；
- 官方 PostgreSQL Docker 的低权限真实生产路径测试；
- 对 Canonical/Scope/Diff/Worker/API/persistence 的 second-provider 审计。

本轮没有实现 SQL Server、Discovery Review UI、Manual Sync、DBDISC-B03/B04、generic plugin
framework、第二个 `DbContext`、PostgreSQL-specific persistence table 或新的 EF migration。

## Driver and supported target

- API 项目使用 `Npgsql` `10.0.3`，目标仍为 .NET 8。
- Provider 显式冻结 `PostgreSQL major == 18`；其它 major 安全归一化为
  `UnsupportedDatabaseVersion`，没有隐式兼容或降级路径。
- Profile 沿用 B01 provider-neutral 字段：`ProviderType=PostgreSql`、Host、Port、
  `DatabaseName`、Username、Password Secret 与 IncludedSchemas；`ServiceName = null`。
- Test Connection 和 Discovery 均使用 `NpgsqlConnectionStringBuilder`，不接受 raw connection
  string fragment 或 secret-bearing ProviderSpecificOptions；Pooling/Enlist 关闭，
  `IncludeErrorDetail=false`。
- connection/catalog/overall timeout、limits、lease/heartbeat/poll 继续来自现有
  `DatabaseDiscoveryOptions`，没有为 PostgreSQL 建立第二套配置。

## Test Connection

`PostgreSqlConnectionTester` / `NpgsqlConnectionProbe` 通过现有
`IDatabaseConnectionTester` 分派，检查：

1. 建立连接和 authentication；
2. server major/version；
3. `current_database()` 与 Profile `DatabaseName` 精确匹配；
4. IncludedSchemas 对当前 principal 的 `USAGE` visibility；
5. Core 所需 `pg_catalog` relation 的零行业务无关 probe。

连接测试的 catalog probes 固定涉及：

```text
pg_namespace
pg_class
pg_attribute
pg_attrdef
pg_type
pg_constraint
pg_index
pg_am
pg_sequence
pg_depend
```

错误只投影 normalized failure 与严格 allowlisted `SQLSTATE-xxxxx`。raw PostgreSQL message、
SQL、connection string、Password 和 Secret 不进入 response/profile summary/audit/log。

最新 task-owned real integration 已证明：正常 Test Connection 返回成功、ProviderVersion 为
PostgreSQL 18.6、`DatabaseName` 为目标数据库且 `ServiceName=null`；错误密码返回
`AuthenticationFailed / SQLSTATE-28P01`，响应不包含错误密码。最终 real integration 和
connection/provider focused tests 均通过。

## Closed catalog query inventory

Discovery reader 使用一个显式审核闭集，共八类固定 query：

| Query | Catalog / safe function | Purpose |
| --- | --- | --- |
| Target context | `current_database()` | Database identity |
| Connected principal | `CURRENT_USER` | Visibility/scope identity |
| Schemas | `pg_namespace` + `has_schema_privilege` | IncludedSchemas exact visibility |
| Objects | `pg_class`, `pg_namespace`, `obj_description` | Table/View and comments |
| Columns | `pg_attribute`, `pg_type`, `pg_attrdef`, formatting/comment functions | Column/type/default/comment |
| Constraints | `pg_constraint`, `pg_class`, `pg_attribute` | PK/UQ/FK and referenced columns |
| Index parts | `pg_index`, `pg_am`, `pg_constraint`, structural deparse functions | key/expression/include/predicate/backing constraint |
| Sequences | `pg_sequence`, `pg_class`, `pg_type` | structural Sequence definition |

所有 schema-scoped queries 使用 typed `text[]` 的 `@schemas` parameter。清单中没有
business-table SELECT、任意用户 SQL、DDL、DML、LOCK、`FOR UPDATE`、`SELECT *`、完整 View/
Constraint/Index DDL dump 或 Sequence runtime/current-value read。Catalog rows 使用 async
`SequentialAccess` reader 和 cancellation；全部 Core groups 完成后才交给 Canonical boundary。

## Canonical mapping

PostgreSQL Provider 只创建既有 B02 `CanonicalDatabaseDiscoverySnapshot`，没有第二套
`PostgreSqlSnapshot/Table/Column` persistence/domain model。映射覆盖：

- exact Schema/Table/View/Column names、nullable ordinal、nullability、default 与 comments；
- typed `CanonicalNativeDataType`；
- multi-column PK/UQ、same-schema/cross-schema FK；
- ordinary/unique/expression/partial/INCLUDE Index 和 backing constraint；
- structural Sequence；
- bounded FK Reference Closure；
- 既有 generic Capability state model。

任何 schema visibility、父子关系、duplicate identity、constraint/index position、unresolved
reference、invalid/not-ready index、limit 或 Core query/mapping failure 都使 Run 失败，并且不会
产生成功 Snapshot/Difference。Counts 仍由 `CanonicalSnapshotService` 重新计算。

## Identifier semantics

- Provider 从 catalog 保留 PostgreSQL 返回的精确大小写；不在 Core 或 Provider 对 catalog
  identity 调用 `ToUpper`、`ToLower` 或 `OrdinalIgnoreCase`。
- PostgreSQL logical identity 使用 Provider-owned length-delimited ordinal components；Core 只比较
  opaque identity。
- deterministic tests 和真实 fixture 均覆盖 `"CaseSensitiveTable"` /
  `"MiXeDColumn"`。
- case-only name change产生旧 identity `MissingFromSource` 与新 identity `Added`；不推断 Rename。

## NativeDataType

Provider 保留 `TypeName`、type namespace 与 `format_type` declaration，并分别投影 length/
precision/scale 的 `Exact / Unbounded / Unknown / NotApplicable`。实现保留 domain/array/UDT 的
catalog type identity/declaration；base type 只辅助通用 measure，不向 Core 泄漏 PostgreSQL 私有
字段。

真实 fixture 覆盖并通过：

```text
integer
bigint
numeric(12,2)
character varying(32)
text
boolean
date
timestamp without time zone
timestamp with time zone
uuid
```

## Constraints, indexes, and Sequence

- PK/UQ/FK 列由 catalog position 确定性排序；FK update/delete action 从 PostgreSQL token 映射为
  generic canonical rule。
- Index key position、expression、sort direction、INCLUDE non-key part、partial predicate、unique
  flag、access-method kind、constraint backing、valid/ready state均从结构 catalog 字段建立；不依赖
 完整 CREATE DDL。
- Sequence 只包含 type、start、increment、min/max、cache、cycle 等结构值。row contract、query、
  canonical JSON/hash 均不包含 `last_value`、current value 或 `is_called`。
- 真实 State 2 仅调用 `setval` 改变 Sequence runtime value 后，Sequence 仍为 `Unchanged`。

## FK Reference Closure

真实 fixture 使用：

```text
IncludedSchemas: dbdisc_a, dbdisc_b
Reference-only schema: dbdisc_ref
```

`dbdisc_a -> dbdisc_ref` 的真实 FK 被完整解析。Referenced schema/object/column 只有在当前
principal 对 referenced schema 具有 `USAGE` 时才投影；无法解析时 fail closed 为
`UnresolvedForeignKeyReference`。Closure 不扩大 IncludedSchemas、不成为 standalone Object/
Column，也不产生 standalone Difference entry。

## Capabilities

Provider 沿用既有：

```text
Supported
NotSupported
Unavailable
NotApplicable
```

当前 capability set：

| Capability | PostgreSQL 18 state |
| --- | --- |
| Identity Columns | Supported |
| Invisible Columns | NotSupported |
| Materialized Views | Supported |
| Partitions | Supported |
| Sequences | Supported |
| Synonyms | NotApplicable |
| Triggers | Supported |
| Container Database | NotApplicable |
| Full DDL | NotSupported / CoreScopeExcluded |

Capability presentation 没有新增 PostgreSQL-specific upper-layer contract；未纳入 Core scope 的
advanced capability 不会被伪装为已经收集。

## Provider-neutral core corrections

PostgreSQL 接入暴露并修正了两处既有 Oracle-shaped Core/API 假设；两者都是
provider-neutral correction，不是 PostgreSQL 特例：

1. `DatabaseConnectionTestResult` / `DatabaseConnectionTestResponse` 现在同时拥有 nullable
   `DatabaseName` 与 nullable `ServiceName`。Oracle 返回 `DatabaseName=null`；PostgreSQL 返回
   `ServiceName=null`。没有添加 vendor-specific response 或 persistence column。
2. Worker/terminal failure safety 集中为通用 normalized code/summary/vendor-token boundary；不再
   以 Oracle 中文摘要 allowlist 或仅 `ORA-xxxxx` 的判断作为 Core authority。当前结构安全地
   接受 Provider 已归一化的 `ORA-xxxxx` / `SQLSTATE-xxxxx`，拒绝 raw message、SQL、Secret 和
   任意错误文本。

其余 Canonical validation/serialization/hash、Scope Fingerprint/Generation、baseline selection、
Difference、Run lease/heartbeat/recovery、Snapshot/Difference persistence 和 APIs 没有增加
Oracle/PostgreSQL 分支。Vendor catalog fields 仍只存在于 `Providers/Oracle` 或
`Providers/PostgreSql`。

## Sequential reader correction found during real integration

第一次真实 Discovery 的八类 raw catalog query 均能执行和消费，但 `ReadObjects` typed mapping
在 Npgsql `SequentialAccess` 下先读取 ordinal 2 的 `relkind`，随后回读 ordinal 0/1，触发
`InvalidOperationException` 并使 Run 归一化失败为 `MetadataQueryFailed`。

最终实现按 schema/name/type/comment 的 0→1→2→3 顺序读取。静态复核确认所有其他 mapper
也严格前向读取；修复后真实 PostgreSQL 18 production-path integration 重跑为 1/1 通过。
该缺陷在本任务内修复并复验，不创建重复 Gap。

## Real Docker integration

已知环境事实：

| Item | Verified value / result |
| --- | --- |
| Image | Official `postgres:18` |
| Image lifecycle | Retained after cleanup; observed ID `sha256:4ef4…2280` |
| Server | PostgreSQL 18.6, Debian build |
| Resource prefix | `skh-dbdisc-postgres-b01` |
| Fixture owner vs discovery role | Separated |
| Discovery role | Non-superuser; database CONNECT and schema USAGE; no schema CREATE/business-table SELECT |
| Included schemas | `dbdisc_a`, `dbdisc_b` |
| Reference-only schema | `dbdisc_ref` |
| Denied schema | `dbdisc_denied` |

最终真实测试使用正常应用路径，而不是直接调用 Provider：

```text
DatabaseSource
→ Connection Profile
→ encrypted Secret
→ Test Connection API
→ Trigger committed Queued Run
→ production BackgroundService claim
→ PostgreSQL Provider
→ Canonical Validate / Hash
→ Baseline / Difference persistence
→ terminal Run
```

### State 1

最新重跑已验证第一轮 `Queued -> Running -> Succeeded`、Complete Snapshot、
`BaseSnapshotId=null`、Difference persisted、`Added > 0`，并验证 Table/View/comments、quoted
identifiers、native types、PK/UQ/FK/indexes/Sequence 与 Reference Closure。

### State 2

Fixture Owner 随后制造结构变化。第二轮使用同一 compatible Scope/Baseline 并验证：

```text
BaseSnapshotId = Snapshot 1
Added > 0
Changed > 0
MissingFromSource > 0
Unchanged > 0
```

`rename_before -> rename_after` 被正确表达为 Missing + Added；没有 Rename inference。
Sequence 仅改变 runtime value后保持 Unchanged。

该真实 integration 最终 Release 重跑为 `1/1` 通过（test 约 6 秒；command 7.423 秒）。

## Failure, cancellation, timeout, and redaction

已实现并已有当前证据：

- real bad password：Test Connection 422，`AuthenticationFailed / SQLSTATE-28P01`；对应 Run
  Failed 且无 Snapshot/Difference；
- real insufficient schema privilege：Test Connection 422 `InsufficientPrivilege`；对应 Run
  Failed 且无 Snapshot/Difference；
- API cancel：terminal `Cancelled`，无 Snapshot/Difference；
- deterministic timeout/cancellation/error mapping 与 Oracle-R01 overall-timeout lease-liveness
  regression继续适用于 generic Worker；
- Provider raw errors不进入 durable summary/log/API，strict SQLSTATE vendor token可以保留；
- Password/Secret/PasswordHash/connection string/SQL不进入 Run/Snapshot/Difference。

最终 Oracle overall-timeout lease regression `1/1` 通过，证明 overall timeout 在非协作
Provider 返回前继续续租，随后以 `Failed / Timeout` 收敛且不创建 Snapshot/Difference；
PostgreSQL/Npgsql normalized timeout/cancellation tests、B02 grouped regression 和 full backend 同时
通过。真实 PostgreSQL integration 的 Trigger 后即时 Cancel API 路径终止为
`Cancelled / Cancelled`，`SnapshotId`/`DifferenceId` 均为空。

## Business-row canary and low privilege

Fixture Owner 在业务表插入随机明显 canary。Discovery role 的实际负向控制证明：

- `rolsuper = false`；
- 对目标数据库有 CONNECT；
- 对 `dbdisc_a`、`dbdisc_b`、`dbdisc_ref` 有 USAGE；
- 对 `dbdisc_denied` 无 USAGE；
- 对目标 schema 无 CREATE；
- 对 canary business table 无 SELECT，直接读取返回 PostgreSQL insufficient privilege。

真实集成已对 API payload、Profile/Secret/audit/Run/Snapshot/Difference/difference-entry
persistence、task-owned SQLite/WAL/SHM 和 Serilog 文件扫描 canary 与 task password，均无命中。
最终 stdout/stderr 对 business canary、Fixture Owner/Discovery password 和 bad-password marker
均为 0 命中；PostgreSQL root password 未提供给测试进程。Docker external log 共 9,611 bytes，
对 business/setup canary、Owner/Discovery/root password 与 bad-password marker 均为 0 命中。

## Verification evidence

| Gate | Result |
| --- | --- |
| Release solution build | PASS — 0 warnings, 0 errors |
| PostgreSQL connection + provider focused tests | PASS — 25/25 |
| PostgreSQL real production-path integration | PASS — 1/1; test approximately 6 s, command 7.423 s |
| Real State 1/State 2 / bad password / insufficient / cancel / canary | PASS |
| Oracle Provider regression | PASS — 23/23 |
| DBDISC-B01 grouped regression | PASS — 9/9 |
| DBDISC-B02 grouped regression | PASS — 27/27 |
| Oracle overall-timeout lease regression | PASS — 1/1 |
| Full backend approved serial gate | PASS — final authoritative rerun 371/371, 0 failed |
| EF pending-model changes | PASS — no pending model changes; only existing global-query-filter warnings |
| New EF migration | PASS — none |
| Repository SQLite/WAL/SHM protection | PASS — exact size/mtime/SHA-256 match |
| Docker/runtime cleanup | PASS |
| Final `git diff --check` | PASS — final report/index update included |

Focused/grouped/full totals overlap and are not added into one artificial total. The first serial full attempt
encountered one existing `REV-GAP-011` log-cleanup file-lock condition; the affected singleton then passed and
the same approved serial configuration completed an authoritative clean rerun of 371/371. Task-owned
runsettings and output-only state were removed.

## Repository safety and cleanup

Real test只使用 task-owned PostgreSQL container/network/volume、SQLite、Data Protection、Attachment、
Serilog、credentials 和隔离端口。开始前与全部验证/清理后的 repository-owned SQLite/WAL/SHM
逐项完全一致：

| Protected file | Size | LastWriteTimeUtc | SHA-256 | Result |
| --- | ---: | --- | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | 995,328 | `2026-08-30T04:51:50.7995165Z` | `B55F1652FA4CC5F0BC6A12B6EB205CAB2F505C8301AF296BDDF0CBF910A2FCE1` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db-wal` | 45,352 | `2026-08-30T07:03:49.6742928Z` | `FB8A5D292010143E7D423587CA64DFF166B2B97000CD08A8628BD8DA6F061458` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db-shm` | 32,768 | `2026-08-30T06:52:31.7391666Z` | `DE1160806E82759BFC0C049CF82E4095C875717F0CB3AF509F7237598112B1B3` | UNCHANGED |

最终 cleanup 检查：

```text
skh-dbdisc-postgres-b01 containers: 0
skh-dbdisc-postgres-b01 volumes: 0
skh-dbdisc-postgres-b01 networks: 0
host port 20114 listeners: 0
integration temporary residue: 0
task testhost/vstest processes: 0
task credentials: cleared
```

只删除本任务资源，没有执行 `docker system prune`。官方 image 保留：

```text
postgres:18
observed image ID: sha256:4ef4…2280
```

最终数据与清理状态：

```text
REPOSITORY DATA PROTECTION: PASS
DOCKER CLEANUP: PASS
```

## Existing / new gaps

- Existing `SEC-04` remains open for real Production HTTPS/reverse proxy, protected Data Protection keys,
  operational log/retention and deployment evidence. A task-owned PostgreSQL container does not close it.
- Existing real-company Oracle 19c network/grant/scale acceptance remains open; PostgreSQL proof does not
  replace that external Oracle gate.
- Existing `DBDISC-GAP-001` through `DBDISC-GAP-004` retain their A01 ownership and closure semantics.
  In particular, `DBDISC-GAP-004` means catalog visibility remains principal/scope-relative and cannot prove
  every invisible physical object.
- Existing `REV-GAP-011` remains open/deferred; the approved serial backend gate is still required.
- No duplicate Gap was created for the corrected `SequentialAccess` implementation defect.
- Static review、聚焦/真实集成和最终回归均未发现新的 unresolved Blocker/High implementation gap。

## Final status matrix

```text
DBDISC-PG-B01 PASS

POSTGRESQL IMAGE: postgres:18
VERIFIED POSTGRESQL VERSION: 18.6 (official Debian build)

POSTGRESQL TEST CONNECTION: PASS
POSTGRESQL REAL DISCOVERY: PASS
CANONICAL SNAPSHOT: PASS
REAL SNAPSHOT / DIFF: PASS
FK REFERENCE CLOSURE: PASS
IDENTIFIER SEMANTICS: PASS
NO BUSINESS-ROW READ: PASS
FAILURE / REDACTION: PASS
CANCELLATION / TIMEOUT: PASS
SECOND-PROVIDER PROOF: PASS
ORACLE REGRESSION: PASS
REPOSITORY DATA PROTECTION: PASS
DOCKER CLEANUP: PASS

DBDISC-PG-B01 READY: COMPLETE
DBDISC-B03 READY: YES
```
