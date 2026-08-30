# DBDISC-B02 Canonical Snapshot + Discovery Run + Diff Foundation Verification Report

## Result

```text
DBDISC-B02 PASS
DBDISC-ORACLE-B01 READY: YES
```

DBDISC-B02 已按冻结的 DBDISC-A01 设计实现并通过全部适用门禁。实现包括 provider-neutral Canonical Snapshot、确定性验证/序列化/SHA-256、Scope Generation/Fingerprint、durable Run/Snapshot/Difference、SQLite durable worker、lease/heartbeat/recovery、触发/取消/read boundary、数据库级单活动 Run 约束、B01 mutation guard、确定性 fake provider 及 conformance tests。

本轮未实现真实 Oracle Catalog Discovery、Discovery/Diff UI、Sync、Binding、PostgreSQL/SQL Server Provider 或 B04 technical identity migration。

## Scope Delivered

- `CanonicalDatabaseDiscoverySnapshot` v1 覆盖 database、scope、capabilities、schemas、objects、columns、native types/measures、PK/FK/reference closure、unique constraints、indexes、sequences 与 counts。
- Canonical validation 对格式版本、身份规则、UTC、显式 schema scope、父子关系、FK closure、capabilities、counts、全局 opaque identity 唯一性和全部设计 limits 执行 fail-closed 验证。
- Canonical serialization 使用固定 camelCase/string-enum JSON 配置与确定性排序；Snapshot、Difference、Scope 分别生成 SHA-256。
- Scope Fingerprint 包含 provider target、format/identity/scope、object types、closure、comparison/options、visibility 和 connected principal；不包含 secret、运行时间戳或其他运行时噪声。
- `DatabaseDiscoveryRun`、`DatabaseDiscoveryScopeGeneration`、`DatabaseDiscoverySnapshot`、`DatabaseDiscoveryDifference`、`DatabaseDiscoveryDifferenceEntry` 已持久化，并由应用层阻止 Snapshot/Scope/Difference mutation。
- Difference 持久化 `Added`、`Changed`、`MissingFromSource`；`Unchanged` 由当前/base Snapshot 确定性派生，不制造 rename 推断。
- 第一份兼容 Snapshot 使用 `BaseSnapshotId = null`，全部 Core identity 为 `Added`。
- Scope 不兼容时创建新 Scope Generation，并重新建立 baseline，不把旧 scope 对象误报为 `MissingFromSource`。

## Durable Worker and State Machine

- 已提交到 SQLite 的 `Queued` Run 是唯一 queue authority；没有 request fire-and-forget Task 或仅内存 queue。
- `BackgroundService` 以短事务 claim Run，写入 lease owner/token、heartbeat 与 expiry；外部 discovery 期间不持有 SQLite transaction。
- 已实现 `Queued -> Running -> Succeeded/Failed/Cancelled` 与 `Queued -> Cancelled`。
- Running cancel 使用持久化 cancellation request；provider、timeout、canonical validation、persistence 与 lease finalization 均 fail closed。
- heartbeat 使用独立 DbContext scope；expired lease recovery 将待取消 Run 终止为 `Cancelled`，其他中断 Run 终止为 `Failed / RunInterrupted`。
- terminal writer 必须匹配 lease token；stale owner 不能覆盖新 owner 或 terminal state。
- Snapshot、Difference、Difference entries 与 Run success 在最终短事务中原子提交。任何 Core Discovery、超时、取消、越界、canonical validation 或持久化失败都不会产生成功 Snapshot。
- SQLite partial unique index `ux_database_discovery_runs_one_active_profile` 对 `Queued`/`Running` 强制 one-active-Run-per-Profile；并发服务检查不是唯一防线。

## Baseline and Difference Semantics

- 兼容 baseline 只从同 Profile、同 Scope Generation、成功且 complete 的 Snapshot 中选择。
- Added/Changed/MissingFromSource/Unchanged 的 provider-neutral conformance 已覆盖。
- deterministic fake provider 端到端覆盖：Trigger -> Queue -> Claim -> Discover -> Canonical Validate -> Hash -> Baseline -> Diff -> Persist -> Succeeded。
- 后续兼容 Snapshot 验证 Changed、MissingFromSource 与 derived Unchanged；不兼容 target/scope 验证新 generation、null baseline、全部 Added。
- FK reference closure 必须完整、与引用 object/column 一致，并拒绝 cross-object column 或未使用 closure stub。

## API, Authorization, and Concurrency

- Administrator 可触发和取消 Run；Viewer/Admin 按冻结规则读取 Run、Snapshot、Difference 与分页 entries。
- Viewer 只能读取 enabled Profile 且 Source 当前 active 的投影；Administrator 可以读取 disabled/deleted Profile 的历史记录。
- Run API 不暴露 Profile configuration revision、Secret version、lease owner/token、内部异常或 secret material。
- Trigger 使用 Profile concurrency token，并验证 enabled Profile、active Source 和可用 Secret。
- active Run 期间，Profile update/enable-state mutation、Secret replace/clear 和 Test Connection 返回冲突，避免 Run 混用新旧配置。
- 首次成功 Snapshot 后，Provider/Host/Port/DatabaseName/ServiceName target identity mutation 返回 `DiscoveryTargetImmutable`；需建立新 Profile。允许改变的 principal/schema 配置通过 scope compatibility 重新建立 baseline。
- 既有 B01 Connection Profile、Secret/Data Protection、Oracle 19c Test Connection、ConfigurationRevision、Secret Version、Source dependency、redaction 与 API contract 均保持。

## Migration

Migration `20260830030122_AddDatabaseDiscoveryRunSnapshotDiffFoundation` 新增且仅新增以下 B02 durable tables：

- `database_discovery_scope_generations`
- `database_discovery_runs`
- `database_discovery_snapshots`
- `database_discovery_differences`
- `database_discovery_difference_entries`

Migration tests 验证 table/index/check/FK、partial unique active-Run index、没有提前引入 Sync/Binding 表，以及 SQLite Down 可还原到 B01 表集合并保持 `PRAGMA foreign_keys = 1`。EF model pending-change gate 通过。

## Security and Redaction

- Secret/plaintext 不进入 Run、Snapshot、Difference、API、audit 或持久化 error summary。
- Worker 只在执行前 JIT resolve secret，并只传入 provider request；持久模型不保存 plaintext 或 reversible secret reference。
- provider exception 不直接写日志/API/数据库；terminal error 使用固定 allowlist code/summary。
- fake provider 测试注入 secret/error canary，验证失败、超时和 invalid canonical 均无 Snapshot/Difference，且响应、日志捕获和数据库没有 canary。
- task-owned runtime 文件扫描未发现登录口令 canary 或数据库 secret canary。

## Test Evidence

| Check | Result |
| --- | --- |
| Release build | PASS — 0 warnings, 0 errors |
| B02 focused canonical/API/migration/concurrency tests | PASS — 13/13, 0 skipped |
| Migration tests | PASS — 14/14 |
| B01 focused regression | PASS — 18/18 |
| Full backend regression, Release, approved serial gate | PASS — 245/245, 0 failed, 0 skipped |
| Worker claim/lease/heartbeat/cancel/expired recovery | PASS |
| Concurrent trigger and database unique constraint | PASS |
| Baseline/scope compatibility | PASS |
| Deterministic canonical hash and Scope Fingerprint | PASS |
| Added/Changed/MissingFromSource/Unchanged | PASS |
| Failure/timeout/cancel/limit/invalid canonical -> no Snapshot | PASS |
| Redaction/canary safety | PASS |
| EF pending model changes | PASS |
| Task-owned SQLite runtime smoke | PASS |
| Repository SQLite/WAL/SHM protection | PASS |
| `git diff --check` | PASS |

The full backend suite used the repository-approved serial workaround for existing `REV-GAP-011`. Temporary runsettings were removed after verification.

During verification, two intermediate test infrastructure/migration issues were found and corrected before final PASS: the generated SQLite Down path attempted unsupported `DropForeignKey`, and the first worker-enabled shared test factory changed existing single-connection test semantics. The final migration, focused, B01 regression, Release build, and full backend results above are the clean reruns after those corrections.

## Runtime Safety

Runtime smoke used only task-owned resources:

- isolated SQLite database under `.tmp/dbdisc-b02-runtime`
- isolated Data Protection key directory
- isolated Attachment StorageRoot
- isolated port `51482`
- repository-supported `dotnet run --project src/SystemKnowledgeHub.Api -c Release --no-build --no-launch-profile`

The smoke created a task-owned Source/Profile/Secret, triggered a committed Run, and verified the production B02 host claimed it from SQLite and safely terminated it as `Failed / ProviderUnavailable` without Snapshot or Difference. This is the expected B02 production behavior because no real catalog provider is registered in this slice. The complete successful pipeline is proved separately by the deterministic fake-provider integration tests.

The task-owned runtime process was stopped, the port released, and the exact task-owned database/WAL/SHM/key/attachment directories removed after canary scanning. No verification process or temporary artifact remains.

## Repository Data Protection

A pre-existing development API process was initially observed using the repository-owned SQLite database. It was not killed or modified by this task and exited independently. All B02 verification began only after establishing this stable repository baseline:

```text
Path: src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db
Size: 950272 bytes
mtime (UTC): 2026-08-30T02:50:06.2271348Z
SHA-256: 2D15792C057F6F051A823A80107856977A4F1784CA17A440219B0B6DD0DB5B91
WAL: absent
SHM: absent
```

After all build/test/runtime checks, size, mtime, SHA-256 and WAL/SHM state exactly matched this stable baseline. No B02 migration or runtime connection targeted the repository-owned database.

## Existing / New Gaps

- Existing `REV-GAP-011` remains unchanged; the approved serial test gate was used successfully.
- Real Oracle 19c Catalog Discovery is intentionally not part of B02 and belongs to `DBDISC-ORACLE-B01`; this is sequencing scope, not a B02 defect.
- No new Blocker or High gap was found.

## Readiness

The provider-neutral contracts, canonical validator/hash, durable worker, concurrency/lease model, baseline/diff persistence, APIs, safety boundaries and deterministic provider conformance suite required for an Oracle catalog adapter are complete.

```text
DBDISC-B02 PASS
DBDISC-ORACLE-B01 READY: YES
```
