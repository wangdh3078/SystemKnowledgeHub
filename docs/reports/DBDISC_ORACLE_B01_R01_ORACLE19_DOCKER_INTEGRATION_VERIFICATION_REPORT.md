# DBDISC-ORACLE-B01-R01 Oracle 19c Docker Real Integration Verification Report

## Result

```text
DBDISC-ORACLE-B01-R01 PASS
```

本轮使用本机已有的 Oracle 19c Enterprise Docker 镜像，补齐了原
`DBDISC-ORACLE-B01` 报告中 `ORACLE 19C INTEGRATION: NOT RUN` 的真实容器集成证据。
核心门禁全部通过：真实 Oracle 19c 容器和 PDB 就绪、正常 API Test Connection、
生产 Worker 完整 Discovery、两轮 Canonical Snapshot/Difference、FK Reference Closure、
低权限失败关闭、业务行零读取、Secret/错误脱敏、仓库数据保护和 Docker 清理均为
`PASS`。

真实 Oracle cancellation 已复现并通过。初次 2 秒 overall-timeout 实验暴露了一个真实
Worker lease-liveness 缺陷：operation cancellation 过早停止 heartbeat，而非协作的 ODP
`OpenAsync` 尚未返回，导致 expired-lease recovery 把 Run 收敛为 `RunInterrupted`。本轮完成
最小生产修复、focused/full regression，并用全新 task-owned Docker/runtime 复验为
`Failed / Timeout`，没有 Snapshot/Difference，真实 Oracle timeout 最终为 `PASS`。

Concurrent DDL 确实与运行中的 Discovery 重叠，但本次扫描得到完整一致的成功快照，
未复现并发目录不一致，严格记录为 `NOT REPRODUCED`。

生产变更仅限 Worker timeout 后的 lease heartbeat 生命周期，以及对应 focused regression
test。没有修改 Oracle SQL/Provider mapping、API、Canonical Snapshot/Diff、数据库 schema
或 migration，没有新增 Oracle-specific persistence table，也没有开始 `DBDISC-PG-B01`。

## Scope and evidence boundary

本报告是原实现报告的补充证据，不覆盖：

- `docs/reports/DBDISC_ORACLE_B01_ORACLE19_CORE_PROVIDER_VERIFICATION_REPORT.md`；
- `docs/reports/DBDISC_B01_CONNECTION_PROFILE_SECRET_ORACLE19_TEST_CONNECTION_VERIFICATION_REPORT.md`；
- `docs/reports/DBDISC_B02_CANONICAL_SNAPSHOT_DISCOVERY_RUN_DIFF_FOUNDATION_VERIFICATION_REPORT.md`。

容器证据只证明 task-owned Oracle 19c development/integration 环境。它不是公司真实 Oracle
网络、账号、grant 或规模验收，不是 Production 部署，也不关闭 `SEC-04`。

## Product defect found and corrected

### Root cause

初次真实 timeout 中，`DatabaseDiscoveryWorker` 的 `MonitorLease` 循环同时检查 monitor-stop
token 和 `operation.IsCancellationRequested`。当 overall timeout 取消 operation 时，heartbeat
立即停止；但 ODP `OpenAsync` 没有协作返回，外部调用仍未退出。Run 的 lease 随后过期，
expired-lease recovery 先把 Run 终止为 `Failed / RunInterrupted`，而不是由当前 owner 在
Provider 返回后写入 normalized `Timeout`。

这是由真实 Oracle timing 暴露的产品缺陷，不是普通的 `NOT REPRODUCED` 或 fixture 问题。

### Minimal correction

修复保持既有 durable worker、lease token 和 terminal writer 语义：

- `monitorStop` 改为 `CancellationTokenSource.CreateLinkedTokenSource(stoppingToken)`；
- `MonitorLease` 循环不再因 `operation.IsCancellationRequested` 停止，只在 monitor-stop/host
  stop 时退出；
- overall timeout 触发 operation cancellation 后，只要非协作 Provider 尚未返回，当前 owner
  仍继续 heartbeat 并续租；
- 该 timeout 路径中的 Provider 返回后，processor 写入正确的 `Timeout` terminal state；既有
  durable cancellation 语义保持不变；
- host shutdown 仍通过 linked `stoppingToken` 停止 monitor，不会留下后台 heartbeat。

修改文件仅为：

- `src/SystemKnowledgeHub.Api/Features/DatabaseDiscovery/Application/DatabaseDiscoveryWorker.cs`；
- `tests/SystemKnowledgeHub.Api.Tests/Api/DatabaseDiscoveryRunApiTests.cs`。

新增 focused test：

```text
Overall_timeout_keeps_lease_alive_until_non_cooperative_provider_returns
```

该测试用不协作 cancellation 的 Provider 验证：overall timeout 后 heartbeat 继续前进、lease
expiry 保持在未来、Run 在 Provider 返回前仍为 Running；Provider 返回后 Run 为
`Failed / Timeout`，且 Snapshot/Difference 均为空。

## Docker and Oracle environment

| Item | Verified value / result |
| --- | --- |
| Image | `container-registry.oracle.com/database/enterprise:19.3.0.0` |
| Image ID | `sha256:2df7530ac81566f8659e13d41fda029e5c08a983147e5b372b173476a74ab62e` |
| Image lifecycle | 镜像在任务开始前已存在；本轮没有 pull 或 delete |
| Task resource prefix | `skh-dbdisc-oracle19-r01` |
| Initial-cycle Oracle host port | `51788` |
| Post-fix Oracle host port | `4352` |
| Post-fix API port | `38860` |
| Actual ServiceName | `skhpdb1` |
| Actual PDB / `CON_NAME` | `SKHPDB1` |
| PDB open mode | `READ WRITE` |
| SQL server version | `19.0.0.0.0` |
| Test Connection provider version | `19.3.0.0.0` |
| Post-fix container state | `healthy`; restart count `0` |

两个验证周期的 Readiness 均没有只依赖固定 sleep。容器状态、listener、SQL connection、
server major、实际 ServiceName、当前 `CON_NAME` 和 PDB open mode 均分别确认。修复后全新
container 在正常 Discovery、pause/resume timeout 和最终清理前保持 `healthy`、restart 0；
连接上下文为实际 PDB，不是 `CDB$ROOT`。

容器、volume 和 network 均使用统一 task prefix；host port 与 System Knowledge Hub API
runtime 均为隔离资源。Docker Registry credential 未被修改。

## Fixture and grant matrix

Fixture 使用以下 schema：

```text
IncludedSchemas: DBDISC_A, DBDISC_B
Reference closure only: DBDISC_REF
```

`DBDISC_A`、`DBDISC_B`、`DBDISC_REF` 的 fixture owner 与 Discovery User 分离。
Fixture DDL/DML 仅由 owner 身份执行，生产 Provider 没有得到外部写能力。

Discovery User 的实际授权边界为：

| Permission category | Actual grant/result |
| --- | --- |
| System privilege | 仅 `CREATE SESSION` |
| Roles | 无 |
| Tablespace quota | 无 |
| Fixture table metadata visibility | 对所需对象直接授予 `REFERENCES` |
| Fixture empty-view visibility | 对专用空视图直接授予 `READ` |
| Sequence visibility | 对目标 Sequence 直接授予 `SELECT` |
| Business-table row read | 未授予；负向检查返回 `ORA-01031` |
| Fixture DDL/DML | 未授予 Discovery User |

本轮没有为方便验证授予 DBA、`SELECT ANY TABLE`、`SELECT ANY DICTIONARY` 或业务表
`SELECT`。该 grant matrix 是本 task-owned fixture 的实际证据；它不外推为所有公司 Oracle
部署的通用最小权限方案。

修复后全新 fixture 对 Discovery User 的实际字典统计为：

```text
SYS_PRIVS = 1          (CREATE SESSION)
ROLES = 0
QUOTAS = 0
OBJECT_PRIVS = 10
business-table SELECT = ORA-01031
empty-view rows = 0
```

State 1 fixture 覆盖任务要求的 Table、View、BYTE/CHAR 字符长度语义、NUMBER
precision/scale、DATE/TIMESTAMP、nullable/NOT NULL、default、table/view/column comments、
PK、Unique、same-schema FK、cross-schema FK、ordinary index、unique index、
function-based index、Sequence 和 quoted/case-sensitive identifier。`DBDISC_A` 指向
`DBDISC_REF` 的 FK 用于验证 IncludedSchemas 外的 Reference Closure。

初次 fixture 脚本曾因测试密码长度不符合 Oracle 要求而失败。该问题只存在于任务夹具
准备脚本，修正后重新创建 task-owned fixture；它不是产品实现缺陷，没有因此修改生产代码。

## System Knowledge Hub runtime and API path

System Knowledge Hub runtime 使用：

- task-owned SQLite；
- task-owned Data Protection key directory；
- task-owned Attachment StorageRoot；
- task-owned Serilog log path；
- isolated API port；
- 当前 `DatabaseDiscovery` typed configuration，包括 connection/catalog/overall timeout、
  2000 ms queue poll、lease/heartbeat 和全部 limits。

没有重新在 Provider 或验证脚本中硬编码 15/60 秒。Test Connection 和 Discovery 均通过
现有授权、antiforgery 和 API 边界执行：

```text
DatabaseSource
→ DatabaseConnectionProfile
→ encrypted Secret
→ Test Connection
→ Trigger committed Run
→ durable Worker claim
→ Oracle Provider
→ Canonical Validate / Hash
→ Baseline / Difference
→ atomic persistence / terminal Run
```

没有直接调用 Provider 绕过 Worker，也没有把内存 queue 当作事实来源。

修复后第二轮使用全新的 task-owned Docker/runtime、Oracle host port `4352` 和 API port
`38860`。正常 Discovery 使用默认受验证配置；真实 timeout 使用报告后述的显式 typed
configuration override。两条链路均运行修复后的生产 Worker。

正常 Profile 使用：

```text
Provider = Oracle
Host = Docker mapped host
Port = 51788
ServiceName = skhpdb1
IncludedSchemas = DBDISC_A, DBDISC_B
Username = task-owned Discovery User
```

通过正常 API 的 Oracle 19c Test Connection 为 `PASS`。响应与后续 read boundary 未暴露
Password、ProtectedPayload、connection string、raw descriptor 或 raw Oracle message。

## Initial integration cycle: State 1 real Discovery

第一轮真实生产链路完成：

```text
Run 1: Queued → Running → Succeeded
Snapshot 1: Complete
Difference 1: persisted
BaseSnapshotId: null
```

Canonical Snapshot 1：

| Canonical group | Count |
| --- | ---: |
| Schemas | 2 |
| Objects | 7 |
| Columns | 27 |
| Primary Keys | 6 |
| Foreign Keys | 3 |
| Unique Constraints | 3 |
| Indexes | 12 |
| Sequences | 1 |
| FK Reference Closure stubs | 1 |

```text
Content SHA-256:
F5C73C51B299FBF8C5D9C679AA6FA30738DFAABD99538AEB9F6758736CF1CB972
```

Difference 1：

```text
Added = 61
Changed = 0
MissingFromSource = 0
Unchanged = 0
```

61 个 Added identity 与 State 1 的 Core Schema/Object/Column/PK/FK/UQ/Index/Sequence
计数一致。ReferenceOnly closure stub 不作为独立 discovered Object/Column，也不产生独立
Difference entry。

实际 Snapshot 检查覆盖 Table/View/Column、完整 `NativeDataType`、comments、PK/FK/Unique、
ordinary/function-based index、Sequence、quoted identifiers、ReferenceOnly closure 和
capabilities。Snapshot 通过 Canonical validation、确定性 serialization、size limit 和
SHA-256 持久化门禁。

## Initial integration cycle: State 2 real Discovery and Difference

State 1 完成后由 Fixture Owner 调用 Sequence `NEXTVAL`。Oracle 目录中的 runtime
`LAST_NUMBER` 从 `100` 变化到 `200`，Sequence 定义未改变。

Fixture Owner 随后执行 State 2 结构变更，制造 Added、Changed、MissingFromSource 和
Unchanged，并保持同一 Profile、principal、target、IncludedSchemas、Scope Generation 和
Scope Fingerprint。

第二轮真实生产链路完成：

```text
Run 2: Queued → Running → Succeeded
Snapshot 2: Complete
Difference 2: persisted
BaseSnapshotId: Snapshot 1
Scope Generation: same as Snapshot 1
Scope Fingerprint: same as Snapshot 1
```

Canonical Snapshot 2：

| Canonical group | Count |
| --- | ---: |
| Schemas | 2 |
| Objects | 8 |
| Columns | 30 |
| Primary Keys | 7 |
| Foreign Keys | 3 |
| Unique Constraints | 3 |
| Indexes | 13 |
| Sequences | 1 |
| FK Reference Closure stubs | 1 |

Difference 2：

```text
Added = 11
Changed = 2
MissingFromSource = 5
Unchanged = 54
```

Added、Changed 和 MissingFromSource 使用 durable Difference entries；Unchanged 按冻结
设计从两个 immutable Snapshots 确定性派生并通过 read boundary 分页，不被错误描述为
durable row。

Sequence 在 Difference 2 中为 Unchanged。Canonical Snapshot、Difference 和 hash 均不含
volatile `LAST_NUMBER`，因此 `100 → 200` 没有制造结构 Changed。

Rename fixture 按冻结规则表现为：

```text
old logical identity = MissingFromSource
new logical identity = Added
```

没有使用名称相似、ordinal、shape、comment 或 native object ID 推断 Rename。

## Post-fix fresh runtime: normal full path

生产修复后使用全新 task-owned Docker、SQLite、Data Protection、Attachment、Serilog 和 API
runtime 重新建立独立 fixture。以下 `Run 1 / Snapshot 1 / Difference 1` 是该全新 task SQLite
中的标识，不与第一验证周期的同名记录混用。

Oracle 和 API readiness 完成后，正常 API Test Connection 为 `PASS`，随后完整生产链路为：

```text
Run 1 queued:    2026-08-30T13:14:40Z
Run 1 running:   2026-08-30T13:14:41Z
Run 1 succeeded: 2026-08-30T13:14:44Z

Snapshot 1: Complete
FormatVersion: 1
IdentityAlgorithmVersion: 1
Difference 1: persisted
BaseSnapshotId: null
```

修复后 fresh Snapshot 1：

| Canonical group | Count |
| --- | ---: |
| Schemas | 2 |
| Objects | 7 |
| Columns | 29 |
| Primary Keys | 6 |
| Foreign Keys | 3 |
| Unique Constraints | 3 |
| Indexes | 13 |
| Sequences | 2 |
| FK Reference Closure stubs | 1 |

```text
Content SHA-256:
BD2E45E5259F532BFA50E88DA6F187798A9BF6B2E384896609107D1DF68D870EA

Added = 65
Changed = 0
MissingFromSource = 0
Unchanged = 0
```

该 fresh full-path 证明最小 Worker 修复没有回归正常 Oracle Test/Trigger/Claim/Discover/
Canonical Validate/Hash/Persist/terminal success，也没有改变 first-snapshot baseline 语义。

## FK Reference Closure

`DBDISC_REF` 不在 IncludedSchemas 中。`DBDISC_A → DBDISC_REF` 的真实 cross-schema FK
仍被完整解析为一个 bounded `ReferenceOnly` closure stub：

- closure 只提供 owning FK 所需的 referenced schema/object/column identity；
- 不扩大 `IncludedSchemas`；
- 不计入 discovered Objects/Columns；
- 不产生 standalone Added/Changed/MissingFromSource；
- 不成为 Sync candidate。

State 1 和 State 2 的 closure count 均为 1，FK 完整性门禁通过。

## Low-privilege failure

独立 `DBDISC_DENIED` Profile 通过正常 API 和 Worker 链路执行：

```text
Run 3: Failed
ErrorCode: ConnectionFailed
Safe summary: 无法建立 Oracle 连接。
Snapshot: none
Difference: none
```

失败没有被解释为空成功 Snapshot，也没有产生 baseline。API、durable Run、task SQLite
和日志中只保留 normalized code/summary；raw Oracle message、SQL、descriptor、Password、
connection string 和 Secret canary 均为 0 命中。

该结果证明真实失败链路 fail closed；它不声称 Oracle `ALL_*` visibility 能证明当前账号可见
范围之外的全部物理对象。

## Query safety and no business-row read

生产 Oracle Provider 的静态 reviewed inventory 共 23 个 query templates。清单只涉及：

```text
DUAL / SYS_CONTEXT
approved ALL_* Core catalogs
approved ALL_* capability probes
```

静态清单和 focused test 确认不存在：

- `DBA_*`；
- `DBMS_METADATA`；
- `FOR UPDATE`；
- `SELECT *`；
- 业务表 `SELECT`；
- Provider DDL/DML。

Fixture Owner 的 setup/mutation DDL/DML 与生产 Provider query inventory 分开记录，不能将
Fixture SQL 归因于 Provider。

连接关闭后，Oracle `V$SQL` 没有保留可归属本次 Discovery 的 cursor，因此本报告不把
`V$SQL` 当作正向运行时 query-trace 证据，也不伪造该项 PASS。查询安全证据由闭集静态清单、
focused inventory test、Discovery User 权限负向检查和业务行 canary 零命中共同组成。

Fixture table 中插入了随机、明显的 business-row canary。验证完成后对以下边界扫描：

- API responses；
- Run / Snapshot / Difference projections；
- task-owned SQLite / WAL / SHM；
- Serilog rolling log；
- runtime stdout / stderr；
- Docker logs。

业务行 canary 与随机数据库 Password canary 均为 0 命中。任务对 9 个 task-owned 文件
artifact 的字节扫描同样为 0 命中。Discovery User 直接读取业务表的负向控制返回
`ORA-01031`。

生产修复后的全新 runtime 再次执行独立 leakage scan：

```text
normal/timeout API projections: secret canary 0; business canary 0
task files: 11
task bytes scanned: 1,803,998
scan failures: 0
task-file secret canary hits: 0
task-file business canary hits: 0
Docker log secret/business canary hits: 0
```

因此第一周期的 9-file scan 与修复后第二周期的 11-file scan 是两份独立证据；没有用第一
周期结果替代修复后真实 timeout 的 API/SQLite/WAL/SHM/Serilog/stdout/stderr/Docker-log
复核。

## Real Oracle timing evidence

### Cancellation

Run 4 对真实 Oracle Discovery 通过正常 API 请求取消：

```text
Run 4: Running → Cancelled
Snapshot: none
Difference: none
```

取消没有被解释为空成功结果，terminal state 和 artifact 原子性正确。

```text
REAL ORACLE CANCELLATION: PASS
```

### Timeout

#### Initial attempt — product defect found

第一验证周期使用当前 typed configuration 将 overall timeout 配置为 2 秒。Run 5 在真实
Oracle 路径进入 Running，但 ODP connection open 没有在该窗口内返回；旧 Worker 停止
heartbeat 后，expired-lease recovery 将其收敛为：

```text
Run 5: Running → Failed
ErrorCode: RunInterrupted
Snapshot: none
Difference: none
```

没有发布错误 artifact，但 terminal classification 和 lease ownership 不符合 timeout contract。
该次门禁为 `FAIL — product defect found`，并直接触发本报告前述最小生产修复；它不再被描述
为普通 `NOT REPRODUCED`。

#### Post-fix real Oracle retry

第二轮使用全新 task-owned Docker/runtime，并通过当前 typed configuration 显式设置：

```text
DatabaseDiscovery:OverallTimeoutSeconds = 2
DatabaseDiscovery:ConnectionTimeoutSeconds = 60
DatabaseDiscovery:CatalogCommandTimeoutSeconds = 60
DatabaseDiscovery:LeaseDurationSeconds = 10
DatabaseDiscovery:HeartbeatIntervalSeconds = 1
DatabaseDiscovery:QueuePollIntervalMilliseconds = 100
```

Oracle container 被 pause，使真实 ODP operation 在 overall timeout 后暂不协作返回。实际时间线：

```text
Run 2 queued:   2026-08-30T13:17:45.021Z
Run 2 running:  2026-08-30T13:17:45.173Z
Run 2 failed:   2026-08-30T13:17:59.381Z
Terminal code:  Timeout
Oracle paused:  14.68 seconds
```

共记录 13 个 lease samples：

```text
initial lease expiry:        2026-08-30T13:17:55.008Z
last observed sample:        2026-08-30T13:17:58.898Z
last renewed lease expiry:   2026-08-30T13:18:08.260Z
beyond initial lease expiry: true
```

最后一个 heartbeat sample 已跨过初始 lease expiry，且续租 expiry 仍在未来，证明 overall
operation token 取消后 heartbeat 持续到 Oracle 恢复并让 ODP 返回。最终：

```text
API status: Failed
API errorCode: Timeout
API snapshotId: null
API differenceId: null
task SQLite Run status/error: Failed / Timeout
task SQLite Snapshot rows: 0
Difference artifact: none (API differenceId is null; no timeout Snapshot target)
terminal lease fields: cleared
```

```text
REAL ORACLE TIMEOUT: PASS
```

### Concurrent DDL

Run 6 扫描期间，Fixture Owner 的 DDL 前后检查均显示 Run 仍为 Running，证明 DDL 与真实
catalog read 发生时间重叠。最终结果为：

```text
Run 6: Running → Succeeded
Snapshot 3: Complete
Difference 3: persisted
```

Snapshot 3 内部完整一致，没有出现可由 Provider consistency validation 拒绝的目录竞态。
因此本次只证明并发 DDL 没有破坏成功快照，不能声称已经复现 concurrent-DDL failure path。

```text
REAL ORACLE CONCURRENT DDL: NOT REPRODUCED
```

## Regression and model verification

| Gate | Result |
| --- | --- |
| Release build | PASS — 0 warnings, 0 errors |
| Overall-timeout lease focused regression | PASS — 1/1 |
| Oracle Provider focused tests | PASS — 13/13 |
| DBDISC-B01 regression | PASS — 18/18 |
| DBDISC-B02 grouped regression | PASS — 9/9 |
| Full backend, Release, approved serial gate | PASS — 345/345, 0 failed, 0 skipped |
| EF pending model changes | PASS — no pending model changes |
| New EF Migration | PASS — none |
| Oracle-specific persistence tables | PASS — none |
| `git diff --check` after final report/index update | PASS |

EF pending-model gate 只产生既有 query-filter model warnings；没有 pending model delta。

真实 Oracle 暴露产品缺陷并修改 production Worker，因此按任务要求完成 Release build、
新增 focused test、Oracle/B01/B02 regression 和批准的 serial full backend gate。Focused 与
grouped/full 数量存在覆盖，不作为可相加的独立 test total。

`git diff --check` 在本报告与索引完成后再次通过。Task-specific commit/push 不在报告内预先
编造，并由最终交付记录实际结果。

## Repository data protection

验证开始前记录了 repository-owned SQLite、现有 WAL/SHM 及两个受保护 attachment object
的 file size、UTC mtime 和 SHA-256。生产修复、Release/focused/full regression、两个真实
Docker/runtime 周期和最终清理全部完成后，每项均与任务开始前完全一致：

| Protected file | Size | LastWriteTimeUtc | SHA-256 | Result |
| --- | ---: | --- | --- | --- |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` | 995,328 | `2026-08-30T04:51:50.7995165Z` | `B55F1652FA4CC5F0BC6A12B6EB205CAB2F505C8301AF296BDDF0CBF910A2FCE1` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db-wal` | 45,352 | `2026-08-30T07:03:49.6742928Z` | `FB8A5D292010143E7D423587CA64DFF166B2B97000CD08A8628BD8DA6F061458` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db-shm` | 32,768 | `2026-08-30T06:52:31.7391666Z` | `DE1160806E82759BFC0C049CF82E4095C875717F0CB3AF509F7237598112B1B3` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/attachments/objects/23/231fd0636be9fb6a3434ce49a896a1ac.bin` | 257,812 | `2026-08-29T10:58:36.7518484Z` | `E37C091AA405909B584E8466D8E354E5162E2B39782A5257497A7A8AD40C3188` | UNCHANGED |
| `src/SystemKnowledgeHub.Api/App_Data/attachments/objects/cd/cd05edd690d377be2e56a79ce76290ef.bin` | 20,596 | `2026-08-29T10:58:44.6255427Z` | `A16CC4995F4D080815FFDFA3A2B8D9A15911DC56895DB8397CDC2B6330B77A45` | UNCHANGED |

本轮没有让 task runtime 连接、迁移、checkpoint、删除或覆盖 repository-owned SQLite，
也没有删除或修改用户已有 WAL/SHM。

```text
REPOSITORY DATA PROTECTION: PASS
```

## Cleanup

两个验证周期结束后均执行 task-owned cleanup。修复后最终 closeout 已清理：

- task System Knowledge Hub API/runtime process；
- isolated API port `38860` 和 Oracle host port `4352`；
- task runtime root；
- task SQLite/WAL/SHM；
- task Data Protection keys；
- task Attachment StorageRoot；
- task Serilog/stdout/stderr artifacts；
- task Oracle fixture credentials；
- `skh-dbdisc-oracle19-r01` container；
- `skh-dbdisc-oracle19-r01` volume；
- `skh-dbdisc-oracle19-r01` network。

最终检查结果：

```text
task API process: stopped
task runtime root: absent
API port 38860: released
Oracle port 4352: released
prefix containers: 0
prefix volumes: 0
prefix networks: 0
task secret variables: cleared
```

清理前最后一次 container 检查仍为 running/healthy、restart 0；随后只删除本任务
container/volume/network，没有删除 image。

任务镜像未删除，清理后仍为：

```text
container-registry.oracle.com/database/enterprise:19.3.0.0
sha256:2df7530ac81566f8659e13d41fda029e5c08a983147e5b372b173476a74ab62e
```

```text
DOCKER CLEANUP: PASS
```

## Existing / new gaps and limitations

- `REAL COMPANY ORACLE 19C ACCEPTANCE` 仍为 `OPEN`：本轮没有连接或修改公司 Oracle，
  容器 grant/network/scale 证据不能替代公司环境验收。
- `PRODUCTION / SEC-04` 仍为 `OPEN`：Production HTTPS/reverse proxy、Data Protection
  key-at-rest、受保护运营日志/retention 和真实部署证据未由本轮关闭。
- `DBDISC-GAP-004` 保持开放：Oracle `ALL_*` 表示 connected principal 可见目录，容器
  Snapshot `Complete` 不等于能证明账号不可见范围内不存在物理对象。
- 初次 real-timeout 暴露 lease-heartbeat liveness 产品缺陷；已在本任务内完成最小修复，
  并由 focused 1/1、full backend 345/345 和 post-fix real Oracle `Failed / Timeout`
  共同关闭。该缺陷在同一任务内修复验证，没有创建重复 Gap。
- Concurrent-DDL failure race 仍为 `NOT REPRODUCED`：已证明 DDL 与 Running scan 重叠，
  但所得 Snapshot 完整一致，没有观察到产品错误或伪造 failure-path PASS。
- 没有未关闭的 Blocker/High implementation gap。

## Delivery boundary

本报告与 `docs/DOCUMENT_INDEX.md` 索引项、Worker correction 和 focused regression 属于同一
R01 交付。最终 branch、commit SHA、push 以及文档更新后的最后一次 `git diff --check`
由 task-specific closeout 记录；报告本身不预先编造尚未产生的 commit SHA。

## Final status

```text
DBDISC-ORACLE-B01-R01 PASS

ORACLE 19C CONTAINER STARTUP: PASS
ORACLE 19C TEST CONNECTION: PASS
ORACLE 19C REAL DISCOVERY: PASS
CANONICAL SNAPSHOT: PASS
REAL SNAPSHOT / DIFF: PASS
FK REFERENCE CLOSURE: PASS
LOW-PRIVILEGE FAILURE: PASS
NO BUSINESS-ROW READ EVIDENCE: PASS
SECRET / ERROR REDACTION: PASS
REPOSITORY DATA PROTECTION: PASS
DOCKER CLEANUP: PASS

REAL ORACLE CANCELLATION: PASS
REAL ORACLE TIMEOUT: PASS
REAL ORACLE CONCURRENT DDL: NOT REPRODUCED

DBDISC-ORACLE-B01 REAL ORACLE 19C DOCKER INTEGRATION: PASS
DBDISC-PG-B01 READY: YES

REAL COMPANY ORACLE 19C ACCEPTANCE: OPEN
PRODUCTION / SEC-04: OPEN
```
