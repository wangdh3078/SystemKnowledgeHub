# U04 — HumanConfirmation Current User + Snapshot Verification Report

## Result

`U04 PASS`

## Implemented

- 原位修改 C25 `AddHumanConfirmationRequest`：删除客户端 `confirmer` snapshot，仅接收 Subject、可选 KnowledgeRole 与确认事实。
- `POST /api/evidence/human-confirmations` 保持 canonical route；Add Response 继续使用现有 `AddEvidenceResponse`，未增加字段。
- `EvidenceController` 复用 `ICurrentUserContext`，只有 `Available` 才进入 C25；Missing / Invalid / NotFound / Inactive 沿用 U03 Error Contract。
- C25 Application 在同一显式数据库事务中重新读取 canonical User、校验 Active、解析最新 UserKnowledgeRole / KnowledgeRole、校验 Subject 与确认事实、生成 snapshot 并插入 Evidence。
- 完整实现 Active KnowledgeRole 的 0 / 1 / multiple 解析，以及 supplied Role 的 existence → active → assignment 校验顺序。
- Evidence primitive snapshot 增加 `ProviderUserId`、`ProviderKnowledgeRoleId`、`ProviderEmployeeNo`、`ProviderJobTitle`；未增加 User/KnowledgeRole navigation 或聚合依赖。
- 新 HumanConfirmation 将 `confirmationMethod` 写入 locator，将 `confirmedAt` 规范化为 UTC `provided_at`，并保持 `provider_external_key`、`provider_source`、`provider_note` 为 `NULL`。
- Detail 读取 Confirmation Method 时优先 locator，历史数据回退 `provider_source`。
- 增加 additive Migration `20260821221206_AddHumanConfirmationCurrentUserSnapshot`。
- 原位修改 `AddHumanConfirmationDrawer.vue`：只读展示现有 `actorStore.currentUser`，实现 0/1/multiple Active Role UX、无 Current User 禁用保存、Role 422 后刷新 Profile/Role 并要求重新确认；未增加 Route、Wizard、第二 Drawer 或第二 User Store。

## Contract Before / After

旧 C25 Request 的身份来源为客户端 `confirmer`，其中可自由提交姓名、角色/身份、团队、external user key、source、note 与 occurredAt。

新 C25 Request 仅包含：

- `subject`
- `subjectDetailKey`
- `knowledgeRoleId`
- `confirmationMethod`
- `confirmedAt`
- `confirmationStatement`
- `supportReason`
- `sourceNote`

客户端不再提交 `confirmer`。身份与角色 snapshot 全部由服务端 canonical 数据生成。

`No response contract change required.`

实际测试同时核对成功 JSON 顶层字段集合，确认仍为现有 `AddEvidenceResponse`，且 `knowledgeStatusChanged` 为 `false`。

## Current User Verification

- Header：shared API client 继续统一附加 `X-Current-User-Id`；C25 Controller 没有自行读取或解析 Header。
- Context：Controller 只调用现有 `ICurrentUserContext.ResolveAsync`，并把可用 Profile 的 ID 与 request facts 交给 Application。
- Canonical re-read：`EvidenceService.AddHumanConfirmation` 在写事务内重新查询 User、Active 状态和当前 Role mappings，不持久化浏览器 Profile。
- Missing：聚焦 API 测试为 `400 validation_error`，`details.currentUserStatus = missing`。
- Invalid：聚焦 API 测试为 `400 validation_error`，`details.currentUserStatus = invalid`。
- NotFound：聚焦 API 测试为 `404 not_found`，`details.currentUserStatus = not_found`。
- Inactive：聚焦 API 测试为 `422 invalid_state`，`details.currentUserStatus = inactive`。
- 浏览器将当前操作者停用并刷新后，选择自动失效、显示恢复提示；HumanConfirmation Drawer 可打开但保存被禁用。

`X-Current-User-Id` 的 UI 文案继续明确为业务操作者上下文，不是登录、认证、权限或 security principal。

## KnowledgeRole Verification

- 0 Active Role：API 创建成功；保存 `ProviderKnowledgeRoleId = null`、`ProviderRole = "知识提供者（未配置知识身份）"`。浏览器显示同一 fallback，且非阻塞。
- 1 Active Role + no ID：API 自动使用唯一 Active Role；浏览器只读显示该 Role，并说明由服务端自动采用。
- Multiple Active Roles + no ID：API 返回 `400 validation_error`，`fieldErrors.knowledgeRoleId` 要求选择。
- Valid selected Role：API 与真实浏览器创建均成功，runtime 选择 Role B。
- Role not found：API 返回 `422 reference_invalid`。
- Role inactive：API 返回 `422 invalid_state`。
- Role not assigned：API 返回 `422 reference_invalid`。
- 运行中状态变化：Drawer 打开并选择 Role A 后，将 Role A 停用再提交；后端返回 422，UI 显示“指定的知识身份已停用”，重新载入 Current User Profile，候选收敛为仍启用的 Role B；未静默重试，数据库中未产生第二条 Evidence。

KnowledgeRole 仅作为本次知识身份，不作为 Permission。

## Snapshot Verification

真实 Browser → Vue → shared API client → ASP.NET Core → SQLite 闭环创建的 Evidence 实际值：

| Column | Stored value |
| --- | --- |
| `provider_user_id` | `3` |
| `provider_knowledge_role_id` | `2` |
| `provider_employee_no` | `U04-RUNTIME-2` |
| `provider_name` | `U04 多角色用户` |
| `provider_team` | `制造系统组` |
| `provider_job_title` | `高级 MES 工程师` |
| `provider_role` | `U04 运行验证角色 B` |
| `provided_at` | `2026-08-21 22:33:42+00:00` |

同一行还验证：

- `source_locator_json.confirmationMethod = "Meeting"`
- `provider_external_key = null`
- `provider_source = null`
- `provider_note = null`

聚焦 SQLite/API 测试在创建后修改 User 的 DisplayName、EmployeeNo、DepartmentOrTeam、JobTitle，并修改/停用 Role；历史 Evidence 的 User/Role reference 与所有 snapshot strings 均未变化。详情读取使用 Evidence snapshot，没有动态 Join 覆盖历史事实。

## Migration Verification

- Migration：`20260821221206_AddHumanConfirmationCurrentUserSnapshot`
- Added nullable columns：
  - `provider_user_id INTEGER NULL`
  - `provider_knowledge_role_id INTEGER NULL`
  - `provider_employee_no TEXT NULL`
  - `provider_job_title TEXT NULL`
- RESTRICT FKs：
  - `FK_evidence_users_provider_user_id`
  - `FK_evidence_knowledge_roles_provider_knowledge_role_id`
- Single-column indexes：
  - `IX_evidence_provider_user_id`
  - `IX_evidence_provider_knowledge_role_id`
- 没有 composite、covering、snapshot text 或 confirmationMethod index。
- `dotnet ef migrations script` 人工检查确认 SQLite 为安装 FK 执行 `ef_temp_evidence` table rebuild：完整复制所有原 columns（包括 `version`），保留原 CHECK constraints，并重建原 3 个 Evidence indexes 与新增 2 个 indexes。
- `HumanConfirmationSnapshotMigrationTests` 从 U01 Migration 的当前 schema 插入 legacy row，再升级到 latest；验证历史行及原 provider/version 字段原样保留，新四列为 `NULL`，FK delete action 均为 `RESTRICT`，原约束与索引未丢失。
- runtime 普通 Evidence 相关测试确认新四字段仍可保持 `NULL`；新 HumanConfirmation 成功写入 reference + snapshot。
- `dotnet ef migrations has-pending-model-changes --no-build` 返回无 pending model changes。
- 未执行任何姓名、工号或 Role 名回填，未修改历史 provider fields 或 locator。

## Legacy Compatibility

- 历史 HumanConfirmation 的四个新 reference/snapshot fields 为 `NULL` 时仍可读取。
- locator 不含 `confirmationMethod` 时，前后端 Detail 继续从旧 `provider_source` fallback。
- locator 有新 method 时优先新值，不修改或 backfill 历史记录。
- 新 C25 不再写 `provider_source`；legacy fallback 仅用于读取。

## KnowledgeStatus Verification

`knowledgeStatusChanged = false`

- 聚焦 API 测试验证 C25 前后 Subject KnowledgeStatus 不变化。
- 真实浏览器闭环前后 `Equipment Status Query` 均为 `Inferred` / “推断”。
- 保存成功提示明确说明 HumanConfirmation 已记录，但知识状态仍需单独推进。
- 没有发生 `Unknown → Confirmed` 或 `Inferred → Confirmed` 自动推进。

## Tests

实际执行：

1. `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --filter "FullyQualifiedName~EvidenceApiTests|FullyQualifiedName~HumanConfirmationSnapshotMigrationTests"`
   - 首次：7 个 API 测试通过；Migration 测试因测试 SQL 中 JSON braces 被 `ExecuteSqlRaw` 当作 format token 而失败。该失败位于新测试夹具，不是产品 Migration。
   - 修正测试夹具改用 SQLite command 后重跑：`8 passed, 0 failed`。
2. `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~EvidenceApiTests|FullyQualifiedName~HumanConfirmationSnapshotMigrationTests|FullyQualifiedName~CurrentUserApiTests|FullyQualifiedName~UsersApiTests"`
   - `13 passed, 0 failed`；覆盖 U01–U03 相关回归。
3. `npm run test -- src/features/evidence/api/evidenceContracts.spec.ts src/app/stores/actor.spec.ts src/api/client/apiClient.spec.ts`
   - assertions 均通过，但首次并行 worker 退出超时，命令未作为最终通过依据。
4. `npm run test -- src/features/evidence/api/evidenceContracts.spec.ts src/app/stores/actor.spec.ts src/api/client/apiClient.spec.ts --maxWorkers=1`
   - `3 files passed, 7 tests passed`，无 worker error。

新增测试保持风险导向：后端 5 个 U04 C25 场景测试扩展既有 Evidence 集成测试、1 个 Migration 保留性测试；前端 2 个 legacy method decoder 测试。未建立大型 E2E 或新测试框架。

## Frontend Verification

- Current User：Drawer 只读显示 DisplayName、EmployeeNo、DepartmentOrTeam、JobTitle，数据来自唯一 `actorStore.currentUser`；提供现有 User Management 修改入口。
- Missing/Inactive Current User：实际浏览器验证恢复提示；保存按钮为 disabled，并可唤起现有 Current User Selector。
- 0 Role：显示批准的 fallback wording，非阻塞且不提交 role ID。
- 1 Role：只读显示唯一 Role，request 省略 ID，由服务端解析。
- Multiple Roles：Select 必选，实际选择 Role B 后创建成功。
- Save Request：前端 type contract 和实际调用不含 `confirmer`，只提交可选 `knowledgeRoleId` 与五项确认事实。
- ConfirmedAt：本地 datetime 在提交边界转成带 offset ISO；数据库保存 UTC。
- 422 recovery：实际后台停用已选 Role 后提交，UI 显示明确错误、刷新 Profile/Role、保留用户事实草稿且不自动重试。
- Detail Method：新记录显示“会议确认”，其来源为 locator `Meeting`。
- Legacy Method：Vitest 与 API legacy test 均验证 `provider_source` fallback。
- 成功状态：提示“人工确认已记录；知识状态仍需单独推进”，详情显示 snapshot，Subject 仍为“推断”。
- 可视检查：Drawer 延续 DR-10 的信息层级、浅色高密度企业工具视觉、单 Drawer 行为；浏览器控制台无 error/warning。

## Browser Closed-Loop Verification

实际路径：

```text
Browser
→ AddHumanConfirmationDrawer.vue
→ shared API client
→ X-Current-User-Id: 3
→ EvidenceController
→ ICurrentUserContext
→ transaction canonical User / UserKnowledgeRole / KnowledgeRole
→ Evidence snapshot
→ SQLite
→ Evidence Detail Drawer
```

结果：

- `POST /api/evidence/human-confirmations` 成功。
- Evidence Detail 显示当前操作者 snapshot、Role B、会议确认与确认事实。
- SQLite 逐列读取结果与 Snapshot Verification 表一致。
- 针对 BusinessFunction `78` 的 stale Role 422 尝试后 `HumanConfirmation count = 0`，证明错误路径未写入。
- Browser Console：0 error，0 warning。

## Build

- `dotnet restore SystemKnowledgeHub.sln`：通过。初次 `--no-restore` build 因本地缺少 `project.assets.json` 失败；restore 后继续按 Gate 验证。
- `dotnet build SystemKnowledgeHub.sln --no-restore`：最终通过，`0 warnings, 0 errors`。
- `npm install`：恢复现有 lockfile 定义的依赖，`0 vulnerabilities`；未增加库或框架。
- `npm run type-check`：通过。
- `npm run build`：通过；仅有既有 Vite chunk-size informational warning，无 build error。
- U04 scoped ESLint：
  - `node_modules/.bin/eslint.cmd src/features/evidence/api/evidenceContracts.ts src/features/evidence/api/evidenceContracts.spec.ts src/features/evidence/components/AddHumanConfirmationDrawer.vue src/features/evidence/components/EvidenceDetailDrawer.vue src/layouts/AppTopBar.vue`
  - 通过，`0 errors, 0 warnings`。

## API / Contract Changes

- 仅按 HC-A01 原位 amendment `AddHumanConfirmationRequest`；未增加 route、V2 contract 或 compatibility confirmer body。
- Add Response 未修改。
- 仅为 U04 增加 Evidence additive columns/FKs/indexes；没有第二套 User、KnowledgeRole、Current User 或 Error API。
- C24 `UpdateEvidence` contract 与 concurrency behavior 未修改。

## Explicitly Not Implemented

本阶段未实现或设计：

- Authentication、Authorization、RBAC、Permission、Claims
- Login、Logout、Password、JWT、OAuth、OIDC、SSO、ASP.NET Core Identity
- Person、Department、Team、JobTitle Entity 或 Organization Tree
- Audit Framework
- Generic Snapshot / Identity / Current User Framework
- 独立 HumanConfirmation Entity/Table
- Evidence architecture rewrite
- C24 redesign 或 append-only correction history
- 全仓 Actor / CreatedBy / UpdatedBy 重构
- 新 concurrency mechanism、distributed lock 或 pessimistic lock framework
- KnowledgeStatus 自动推进
- U05 或后续阶段

## Specification / Amendment Deviation

实现完全遵循已批准 HC-A01：Request、canonical route、Current User required semantics、transaction 内 canonical re-read、Role 解析顺序、snapshot mapping、additive schema、RESTRICT FK、legacy method fallback、Error Contract、KnowledgeStatus behavior 与 U04 scope 均未偏离。

没有修改冻结 MVP Specification、Golden UI、HC-A01 设计决策或 U01–U03 canonical API。

## Follow-ups

无 U04 completion blocker 或必须跟进项。C24 保持现状；未在 U04 内扩大 correction/snapshot redesign。

## Process Cleanup

- 已关闭本次 Browser automation 创建的 tab；浏览器 session 无遗留 tab。
- 已停止 ASP.NET Core 验证进程及其 host process。
- 已停止 Vite/npm process tree，并额外发现并停止同一启动时间的 orphan Node listener。
- 未启动 test/watch、preview、mock 或 migration server。
- 已删除本次临时 runtime SQLite DB、WAL/SHM 与 API/Vite 日志目录 `artifacts/U04`。
- 最终 `netstat` 检查：`5090`、`5173`、`5174` 的 `LISTENING` 数量为 `0`。
- 本次使用的已知 PID 均不存在，API/Web probe 均不再由 U04 verification server 响应。

U04 完成后停止；未开始 U05。
