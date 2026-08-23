# REV-FIX-01 Medium Gaps Correction Verification Report

## Result

**REV-FIX-01 PASS**

PHASE-REV 审计确认的 5 个 Medium gap 已用最小、契约一致的修改关闭，并通过 focused automation、serial full backend、frontend affected regression、build gates及隔离 Browser/API/SQLite runtime。没有 schema/migration change，没有修改 frozen specifications、Golden assets、task definitions或任何 Low gap。

## Worktree Baseline

- Branch：`main`
- Baseline commit：`bba9b26bc095ec41ebbc2f750f7b12d90f16bfa2`（`✅ test: 完成修订安全阶段总验收`）
- Baseline worktree：clean；branch相对 `origin/main` ahead 2。
- 实施前完整复核：`AGENTS.md`、PHASE-REV verification/gap register、REV-A01 freeze、REV-B01 foundation report、REV-B04 safety report及当前 `REV-FIX-01` task definition。
- 未发现需要停止实现的 material specification conflict。

## Scope

本任务只关闭：

- `REV-GAP-001` Restore Unicode scalar/NUL validation
- `REV-GAP-002` HumanConfirmation stale 409 details
- `REV-GAP-003` Progression Panel subject revision propagation
- `REV-GAP-004` HumanConfirmation success后的 authoritative coverage refresh
- `REV-GAP-010` legacy migration preservation fixture

未实现新 feature、未改变 revision/lifecycle/status/evidence/relationship语义，未增加架构层或通用框架。

## REV-GAP-001 — Restore Unicode Validation

- Restore reason继续先 trim；随后显式拒绝 U+0000，并用 `.EnumerateRunes().Count()` 按 Unicode scalar执行 5～500 inclusive validation。
- Invalid reason在进入 transaction/persistence前返回 frozen `400 validation_error`；没有添加 catch-all `DbUpdateException`或把数据库约束异常伪装为 validation。
- Focused integration覆盖：whitespace、trimmed 4/5、3/5 supplementary emoji、500/501 scalar、NUL，并对每个 invalid case断言 head、Version、current revision、revision rows、UpdatedAt与 FTS不变。
- Real API：`😀😀😀` → 400 `validation_error`；current revision/history/token保持 2/2/unchanged。`😀😀😀😀😀` → 200，生成 revision 3 `Restore`，`RestoredFromRevisionNumber=1`，stored reason精确保留 5 个 scalar；未出现 500。

## REV-GAP-002 — Exact Stale HumanConfirmation Contract

- Evidence application result在 stale KnowledgeDocument revision时携带 authoritative server current revision。
- Controller `409 conflict` details精确包含：`resourceType`、`resourceId`、`currentRevisionNumber`；OpenAPI metadata补充 409。
- Exact integration test断言 details keys/value、Evidence count不变、HumanConfirmation未创建、head/token/current revision/KnowledgeStatus/FTS不变。
- Runtime从 revision 1加载后由 server推进至 revision 2，再提交 HC@1：409 `conflict`，details keys精确为 `currentRevisionNumber,resourceId,resourceType`，值为 `KnowledgeDocument / 1 / 2`；Evidence 2→2、History 2→2、token unchanged、status仍 Inferred。

## REV-GAP-003 — Progression Panel Cross-Component Propagation

- `KnowledgeStatusProgressionPanel` 的 prominent HumanConfirmation action在 prop存在时把 `subjectRevisionNumber` 放入现有 single-overlay payload；其它 target不伪造该字段。
- Integration test覆盖完整链路：Detail revision 7 → Progression Panel → overlay descriptor → HumanConfirmation Drawer → typed API request，最终 request携带 `subjectRevisionNumber=7`。
- Browser runtime使用 Progression Panel入口；Drawer明确显示“本次人工确认将覆盖当前显示的修订 1”，保存成功并持久化 HC snapshot 1，不再出现 missing revision 400。

## REV-GAP-004 — Authoritative Detail Refresh

- HumanConfirmation仅在 save成功后派发定向 `human-confirmation:changed` event；既有 `evidence:changed`继续负责 Evidence list。
- 当前 KnowledgeDocument Detail只响应匹配 subject的事件，并重新调用 authoritative Detail API；frontend不计算、不合并、不复制 confirmation coverage rules。
- `detailLoadSequence`保证并发/out-of-order refresh只采用最新 request，且 current revision变化时不会被旧 response覆盖。
- Tests覆盖 `NoConfirmation`、`ChangedSinceConfirmation`、`LegacyConfirmationUnknown` → backend `CurrentRevisionConfirmed`，save error不伪造 coverage，旧 response不覆盖新 revision，以及一次 Evidence + 一次 Detail的必要 request count。
- Browser save HC后无需 reload，页面立即显示“人工确认覆盖当前修订 1”；KnowledgeStatus仍 Inferred，明确证明 coverage refresh没有自动推进 status。

## REV-GAP-010 — Legacy Migration Fixture

- Fixture仍只先迁移至 `20260822025403_AddOidcAuthenticationFoundation`。
- Old-schema Evidence seed从 current EF model改为 target-era explicit-column raw SQL；没有引用尚未存在的 `knowledge_document_revision_number_snapshot`。
- Migrate latest后继续证明 User/LoginIdentity、Evidence、System、Relationship rows保留，并新增 snapshot null、完整 applied migration history、`PRAGMA integrity_check=ok`和 FK violation=0。
- 没有修改 historical migration、ModelSnapshot、production schema或增加 migration。

## Backend Build and Tests

- `dotnet build SystemKnowledgeHub.sln --no-restore` — PASS，0 warnings / 0 errors。
- Restore + Revision focused classes — 10/10 PASS。
- Legacy migration focused test — 1/1 PASS。
- Full backend project，以已验证的 `xUnit.ParallelizeTestCollections=false` serial collection gate运行 — **123/123 PASS，0 failed，0 skipped，23s**。
- 旧 REV-GAP-010 deterministic failure已消失；新增 boundary tests使总数从 121增加到 123。

## Frontend Tests and Build Gates

- Focused Detail + HumanConfirmation Drawer — 2 files / 19 tests PASS。
- Affected Evidence / HumanConfirmation / KnowledgeStatus / Revision Detail / Restore / Published safety selection — **13 files / 57 tests PASS**。
- `npm run type-check` — PASS。
- `npm run build` — PASS，2133 modules；只有既有 chunk-size advisory。
- REV-FIX-01 modified-scope ESLint — PASS，0 warnings / 0 errors。
- `git diff --check` — PASS；仅 Git 的既有 LF→CRLF working-copy提示，不是 whitespace error。

## Browser → API → EF Core → SQLite Runtime

Runtime使用独立临时 SQLite、独立 Data Protection key目录、disposable Local Administrator、API `127.0.0.1:5128` 与 Vite `127.0.0.1:5195`。

### Flow A — Progression Panel HumanConfirmation

`Login → Create KnowledgeDocument R1 → ordinary Evidence → explicit Inferred → Knowledge Progression Panel → 添加人工确认 → Save`

- Prominent Panel入口打开的 Drawer显示 revision 1 context。
- Save成功，Evidence list立即包含 HumanConfirmation。
- Detail无需手工 reload即显示 `CurrentRevisionConfirmed` 文案；status仍 Inferred。
- Final SQLite Evidence为 2 rows，其中 HumanConfirmation=1、snapshot=1。

### Flow B — Stale HumanConfirmation

`Load R1 → server Content Save产生 R2 → submit HC@R1`

- HTTP 409 `conflict`。
- Exact details：`resourceType=KnowledgeDocument`、`resourceId=1`、`currentRevisionNumber=2`。
- Evidence 2→2、History 2→2、R2 token unchanged、current revision=2、KnowledgeStatus=Inferred；no-write成立。

### Flow C — Restore Unicode

- Invalid 3-scalar supplementary reason：400 `validation_error`，无 revision/token/history变化，无 500。
- Valid 5-scalar supplementary reason：200，生成 R3 Restore from R1，reason精确保存；R1/R2保留。
- Restore后 KnowledgeStatus仍 Inferred；HC仍 snapshot=1，authoritative coverage为 `ChangedSinceConfirmation`，证明 status与coverage保持独立。

### SQLite Read-only Cross-Check

- `integrity_check=ok`，foreign key violations=0。
- Migration history=18，latest=`20260823092808_AddImmutableKnowledgeDocumentRevisions`。
- Document 1：Version=4、current revision=3、Draft/Inferred。
- Revisions：1 Created、2 ContentSave、3 Restore-from-1；连续且未删除历史。
- Evidence=2、HumanConfirmation=1、snapshot=1。
- FTS current row不再包含 R2 unique `Flow B server revision 2` 内容。
- Browser console error/warning=0；三条业务流程没有 API 5xx。一个最初 readiness probe误用了非 API root `/auth/options`，随即改为正确 `/api/auth/options`=200；它不参与业务验收，也没有数据写入。

## Repository Database Protection

`src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db` 前后只读指纹完全一致：

- Length：`507904`
- LastWriteTimeUtc：`2026-08-23T09:50:37.0173536Z`
- SHA-256：`FCA1B1B7B3CBDC44E16EDA09E296C6C7E0DAD2AEBC8E975B8EFC2026135251FB`

未 migrate、write、reset、delete或replace repository DB。

## Cleanup Safety

- Browser task-created tab已关闭，remaining controlled tabs=0；未保留 viewport override。
- Cleanup前重新核对 listener：API PID 30756 (`SystemKnowledgeHub.Api`)，Vite PID 25612 (`node`)。
- 只对这两个精确 PID执行 `Stop-Process`；没有按 name、parent tree或 wildcard终止，也没有触碰 Codex。
- 两个 listener停止后，ports 5128/5195 listener count=0；task-owned wrapper PID 34332/5992均已自然退出。
- Full test gate结束后 final `testhost` count=0；没有遗留 watch/test process。
- 临时目录 `skh-rev-fix-01-f727d2317c6f4ff9ae8e2eb39e4a6d5e` 在确认位于系统 temp且匹配精确前缀后删除；10 items removed，`ExistsAfter=false`。

## Git Diff / Change Discipline

- Production：4 backend + 3 frontend files。
- Tests：3 backend + 2 frontend files。
- Documentation：Gap Register + 本报告。
- No schema/migration/ModelSnapshot change；No frozen specification/Golden/task-definition change。
- 未修改无关模块，未引入新 dependency、test framework、repository/CQRS/command bus或第二 overlay manager。

## Deferred Low Gaps and Existing Baselines

以下保持 OPEN / Deferred，未顺手处理：

- `REV-GAP-005` ElTooltip registration
- `REV-GAP-006` Restore dialog accessible name
- `REV-GAP-007` nested main landmark
- `REV-GAP-008` Published confirm overlay guard
- `REV-GAP-009` Restore Version direct assertion
- `REV-GAP-011` backend parallel test stall

以下既有 unrelated baseline也未修改：`AppShell.spec.ts` stale expectation、`CreateIntegrationDialog.vue` unused props、`unknownItemContracts.ts` empty interface、ResizeObserver dev warning。

## Completion

`REV-GAP-001`、`002`、`003`、`004`、`010` 已在 Gap Register标记 CLOSED并附 closure evidence。REV-FIX-01 到此停止；不会自动开始 `PHASE-REV-DELTA-VERIFY`，后续只在人工明确请求后执行。
