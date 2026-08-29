# DBDISC-B01 Connection Profile + Secret Boundary + Oracle 19c Test Connection Verification Report

## Result

```text
DBDISC-B01 PASS
DBDISC-B02 READY: YES
```

DBDISC-B01 的适用实现、迁移、权限、并发、脱敏、构建、聚焦测试、全量后端测试和隔离运行时验证均通过。当前机器没有 Docker、Oracle 连接环境变量或已批准的 Oracle 19c 服务，因此真实 Oracle 19c integration 为 `NOT RUN — environment unavailable`；这符合任务的明确降级规则，不伪造连接证据。现有 `SEC-04` Production key-at-rest / HTTPS / 运维日志门禁和真实公司 Oracle 验收仍保持开放。

## Scope

本 Slice 只实现：

- `DatabaseConnectionProfile`、`DatabaseConnectionSecret` 和连接审计事件；
- Administrator-only 的 Profile List/Get/Create/Update/Enable/Disable；
- 独立的 Secret Set/Replace/Clear；
- Oracle 19c Test Connection；
- Source 关系验证、enabled Profile 删除阻断、opaque concurrency、Data Protection 和脱敏；
- B01 migration、聚焦测试与任务自有运行时证据。

未实现 Discovery、Run、Snapshot、Difference、Sync、UI、PostgreSQL tester、SQL Server tester、Oracle 10g/11g 或完整 `ALL_*` Catalog Discovery。

## Architecture Conformance

- Feature 位于 `Features/DatabaseDiscovery`，保持 Domain / Application / Provider / Persistence / Api 边界。
- 使用直接 `KnowledgeHubDbContext`、显式 Use Case 和 Controller；没有 Generic Repository、UnitOfWork、MediatR、AutoMapper、通用 Secret 插件框架或 generic CRUD Controller。
- Profile 是 provider-neutral 模型；`ProviderType` 允许 `Oracle`、`PostgreSql`、`SqlServer`，但 DI 只注册 Oracle tester，未实现 Provider 返回 `ProviderUnavailable`。
- Test Connection 是同步请求边界，不创建 Discovery Run、Snapshot、Difference 或任何后续 Slice 表。

## DatabaseConnectionProfile

Profile 持久化固定 `DatabaseSourceId`、名称、Provider、Host/Port、DatabaseName/ServiceName、`UsernamePassword`、Username、版本化 options、显式 IncludedSchemas、enabled/status、测试摘要、Discovery 占位摘要、`ConfigurationRevision`、canonical 创建人和 row `Version`。

关键约束：

- `database_source_id` 唯一且 FK `RESTRICT`；
- Profile 名称使用 `NOCASE` 唯一索引；
- Provider/Auth/Status/Port/locator 由 SQLite CHECK 约束保护；
- Create/Update/Enable/Test 对 Source active 状态和 Engine 映射进行应用层验证；
- Oracle 只接受 Host/Port/ServiceName，PostgreSql/SqlServer 模型只接受 Host/Port/DatabaseName；
- IncludedSchemas 必须显式提供 1–128 个、每项 1–128 字符、按 ordinal 去重，不追加 system schema；
- `ProviderSpecificOptions` v1 只允许精确的 `{"version":1}`，因此 password、token、descriptor、connectionString 等任意绕过字段均被白名单拒绝。

连接相关字段变化和 Enable/Disable 同时递增 `ConfigurationRevision` 与 row `Version`；仅名称变化不改变连接配置修订；测试开始/完成等 operational summary 只递增 row `Version`。

## DatabaseConnectionSecret

Secret 是 Profile PK/FK 一对一独立表：

```text
ProfileId
ProtectedPayload
PayloadFormatVersion
UpdatedAt
Version
```

Profile 不存 Password 或 connection string。Clear 将 `ProtectedPayload` 置空并保留版本墓碑，而不是删除行；这样 Set → Clear → Set 仍保持 Secret Version 单调，旧测试不能错误匹配新密码。API 只投影 `hasSecret` 与 `secretUpdatedAt`，不投影 plaintext、ciphertext、hint、末尾字符或 `SecretReference`。

## Data Protection

实现使用窄边界 `IDatabaseConnectionSecretStore` 和本地 `IDataProtectionProvider`，purpose 为：

```text
SystemKnowledgeHub.DatabaseDiscovery.ConnectionSecret/v1/{profileId}
```

测试证明：

- 同一 task-owned key ring 重启后可解密；
- 错误 key ring、跨 Profile purpose、损坏 ciphertext、未知 payload format 均返回 `SecretUnavailable`；
- 不回退到 appsettings、environment 或空密码；
- 不使用 `PasswordHasher` 保存外部数据库密码。

## Profile APIs

统一前缀：`/api/admin/database-connection-profiles`。

| Method | Route | Semantics |
| --- | --- | --- |
| GET | `/` | List Profile |
| GET | `/{id}` | Get Profile |
| POST | `/` | Create Profile |
| PUT | `/{id}` | Update Profile |
| PUT | `/{id}/enabled-state` | 显式 Enable / Disable |
| POST | `/{id}/test-connection` | request-bound Test Connection |

Profile API 不返回 Password、ProtectedPayload、SecretReference、完整 connection string 或 raw Oracle descriptor。

## Secret APIs

| Method | Route | Semantics |
| --- | --- | --- |
| POST | `/{id}/secret` | Set；已有 Secret 时拒绝，要求 Profile concurrency token |
| PUT | `/{id}/secret` | Replace；缺少 Secret 时拒绝，要求 Profile concurrency token |
| DELETE | `/{id}/secret` | Clear；显式命令，要求 Profile concurrency token |

空字符串拒绝；省略 Password 不等于 Clear；Replace/Clear 将 `ConnectionStatus` 重置为 `Unknown`；旧 plaintext 不保留为应用可读历史。

## Authorization

Controller 使用 class-level `Administrator` policy，覆盖 Profile read/write、Secret 命令和 Test Connection。全局 unsafe `/api/**` antiforgery 中间件继续保护全部 POST/PUT/DELETE。聚焦 API 测试证明 Administrator 成功，Viewer/Editor 对 read/create/update/enable/secret/test 直接请求均为 403。

## Oracle Driver / Version

- Package：`Oracle.ManagedDataAccess.Core` `23.26.300`；只添加到 API 项目。
- 该 Oracle-owned package 明确提供 `net8.0` 资产，2026-07-23 发布；[NuGet package page](https://www.nuget.org/packages/Oracle.ManagedDataAccess.Core/23.26.300) 与 [Oracle ODP.NET system requirements](https://docs.oracle.com/en/database/oracle/oracle-database/26/odpnt/InstallSystemRequirements.html) 支持当前 .NET 8 / Oracle Database 19c-or-later 技术组合。
- 产品层仍严格 gate `major == 19`；11c、21c 等均为 `UnsupportedDatabaseVersion`，没有 legacy fallback。
- `dotnet list ... package --vulnerable --include-transitive`：当前配置的 NuGet 漏洞源未报告已知漏洞。
- 包内许可证为 Oracle Free Distribution, Hosting, and Use Terms and Conditions v1.0；允许在约束下开发、测试、运行和再分发未修改程序。再分发必须携带许可证并遵守其费用、notice、出口和逆向工程条件。此处是工程兼容性记录，不是法律批准；参见 [package license](https://www.nuget.org/packages/Oracle.ManagedDataAccess.Core/23.26.300/License)。

## Oracle 19c Test Connection

生产 adapter 使用 `OracleConnectionStringBuilder` 组合验证后的字段，禁用 Test pooling，并设置 15 秒连接/命令边界。短生命周期 context 的 Password 为 internal 属性，context 不被序列化、持久化或记录，`ToString()` 只包含 ProfileId/ProviderType。

只执行以下只读安全检查：

1. 建立 Oracle session，成功即证明凭据与 `CREATE SESSION` 能力；
2. 读取 driver server version，并要求 major 19；
3. 通过 `SYS_CONTEXT` 读取当前 Service/Container，要求与 Profile Service 匹配并拒绝 `CDB$ROOT`；
4. 通过 bind parameter 对 `ALL_USERS` 验证全部 IncludedSchemas；
5. 对冻结 Core 所需目录视图执行 `WHERE 1 = 0` capability probes。

这些 probe 不枚举目录对象、不读取业务表行、不执行 DML/DDL/lock、`ALTER SESSION SET CONTAINER` 或 `DBMS_METADATA.GET_DDL`，也不创建 Discovery artifacts。schema 值永远不是 SQL identifier/interpolation；动态部分只有应用生成的 bind parameter 名称。

## Concurrency

Test start 的 SQLite IMMEDIATE 短事务捕获：

```text
ConfigurationRevision
Secret Version
LatestConnectionTestAttemptId
start time
```

外部连接在事务外执行。完成事务只有在 attempt 仍最新、ConfigurationRevision 未变且 Secret Version 未变时写回状态/摘要；否则记录 `Superseded` audit 并返回 409。测试覆盖并发 Test、Profile 变化、Secret rotation 三种竞态，旧结果均不能覆盖新状态。

## Source Dependency

Enabled Profile 被加入现有 DatabaseSource soft-delete dependency 集合，blocker type 为 `enabledDatabaseConnectionProfiles`。Disable 后 Source 可按原流程软删除；没有 cascade 或 Profile hard delete。

## Error / Redaction

实现覆盖 `ConnectionFailed`、`AuthenticationFailed`、`InsufficientPrivilege`、`UnsupportedDatabaseVersion`、`Timeout`、`Cancelled`、`ProviderUnavailable`、`SecretMissing`、`SecretUnavailable` 和 concurrency conflict。Oracle vendor code 仅格式化为 allowlisted `ORA-xxxxx`。

Provider exception object/message 不向上抛出或写日志；测试注入包含 canary、raw descriptor 和 SQL 的异常，API、Profile summary、audit、captured logs 均只保留归一化中文摘要和允许字段。

## Canary Secret Tests

聚焦测试和 task-owned runtime 使用独立 canary，证明 plaintext 不出现在：

- API response；
- captured logs；
- Profile 普通字段；
- audit；
- task-owned SQLite 文件字节；
- task-owned WAL/SHM（运行结束后不存在）。

Ciphertext 仅存在 Secret 表；Profile purpose isolation、restart 和 wrong-key 失败关闭均有独立测试。

## Migration

Migration：`20260829150313_AddDatabaseDiscoveryConnectionFoundation`。

只新增：

```text
database_connection_profiles
database_connection_secrets
database_connection_audit_events
```

迁移测试从 `20260829012501_AddAttachmentFoundation` 升级，验证新增表集合、FK `RESTRICT`、Source 唯一索引、CHECK 约束和 `PRAGMA foreign_key_check = 0`。未新增 DiscoveryRuns、Snapshots、Differences、SyncPlans 或 Bindings。

## Test Evidence

| Check | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln -c Release --no-restore` | PASS — 0 warnings, 0 errors |
| B01 focused filter | PASS — 18/18 |
| Full backend | PASS — 210/210, 0 skipped, 41 s |
| Migration focused test | PASS |
| `dotnet ef migrations has-pending-model-changes` | PASS — no pending model changes |
| NuGet vulnerability query | PASS — no known vulnerable package from configured source |
| `git diff --check` | PASS |

默认高并行 full run 曾使大量既有测试集合同时处于 active 状态并触发 testhost thread starvation。hang diagnostics 确认不是某个 B01 断言失败；本任务清理了生成的约 500 MB dump，并用临时、未入库的 xUnit serial runsettings 完整复跑 210/210。只有成功复跑计入 PASS 证据。

## Runtime Safety

隔离运行时使用：

- `dotnet run --no-launch-profile --project src/SystemKnowledgeHub.Api -c Release --no-build -- --urls http://127.0.0.1:51391`；
- Development 环境；
- task-owned SQLite、Data Protection key directory、Attachment StorageRoot；
- task-owned local Administrator；
- 未连接任何 Oracle 或公司网络目标。

Smoke 结果：登录 204、Create Profile 201、Set Secret `hasSecret=true`、List 只显示安全字段、disabled Test Connection 422 `Disabled`、Clear Secret `hasSecret=false`。task DB 为 692224 bytes，任务 Secret 与 local login plaintext 的文件级扫描均为 false；WAL/SHM 不存在。

Repository-owned `App_Data/system-knowledge-hub.db` 前后 SHA-256 均为：

```text
0ED137C0A673F8E19CE83CFD55866443C498DD6620F52582383AA624732EF3C9
```

长度和 LastWriteTime 未变化。验证 API 进程已停止，51391 已释放，task-owned DB/key/attachment 目录、失败测试目录、hang dump 和临时 runsettings 均已删除。

## Oracle 19c Integration Evidence

```text
NOT RUN — environment unavailable
```

检查结果：Docker 不可用，未发现 Oracle/DBDISC 环境变量，且任务禁止连接公司 Oracle。因此没有真实 session/authentication/service/PDB/grant 证据，也没有声称真实 Oracle 19c PASS。Provider-independent contract、Oracle tester decision logic 和错误归一化均由确定性测试覆盖；真实 task-owned Oracle 19c integration 仍应在可用环境中补跑。

## Existing / New Gaps

- 既有 `SEC-04` 保持开放：Production Data Protection key-at-rest、HTTPS/reverse proxy、受保护诊断日志与备份恢复尚未由 B01 关闭。
- 真实公司 Oracle 19c 网络、Service/PDB、最小 grants 和运行规模验收仍是外部环境门禁。
- DBDISC-A01 的 `DBDISC-GAP-001` 至 `DBDISC-GAP-004` 与后续 Discovery/Sync 有关，不阻塞 B01。
- 没有新增 Blocker/High gap。

## B02 Readiness

```text
DBDISC-B02 READY: YES
```

B02 可以建立在已验证的 Profile/Secret、Provider resolver、Oracle 19c-only gate、测试连接安全边界、Source 固定关系和 revision capture 上。B02 不得把 Test Connection 重新解释为 Run，也不得跳过真实 Oracle 环境证据要求。
