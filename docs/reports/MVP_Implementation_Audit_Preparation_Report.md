# MVP Implementation Audit Preparation Report

## Scope

本次仅完成 VS-11 后的构建确认、项目结构盘点和临时产物清理；未开发功能、未修改业务逻辑、未修改 Frozen Specification，也未启动 API 或 Vite 服务。

## Current implementation point

当前已完成至 **VS-11 — Integration**。未开始 Dashboard、Global Search 或新的 Vertical Slice。

## Build verification

| Command | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | PASS — 0 warnings, 0 errors |
| `npm run type-check` | PASS |
| `npm run build` | PASS — Vite 仅报告既有 bundle-size advisory |

未新增测试，也未运行完整测试套件。

## Current feature list

- Backend: Bootstrap, Systems, DatabaseKnowledge, BusinessFunctions, Evidence, KnowledgeStatus, Relationships, UnknownItems, BusinessRules, Integrations.
- Frontend: 对应的 feature-first 前端目录，以及共享 `api`、`app`、`components`、`layouts`、`styles`。
- 当前后端项目：`src/SystemKnowledgeHub.Api`；当前前端项目：`src/SystemKnowledgeHub.Web`；测试项目：`tests/SystemKnowledgeHub.Api.Tests`。

## Current migrations

1. `InitialDatabaseKnowledge`
2. `AddSystemsListCreate`
3. `AddBusinessFunctions`
4. `AddEvidence`
5. `AddKnowledgeRelations`
6. `AddUnknownItemInvestigation`
7. `AddBusinessRules`
8. `AddIntegrations`

## Test project overview

一个后端测试项目 `SystemKnowledgeHub.Api.Tests` 使用真实 SQLite 集成测试，包含 Bootstrap、DatabaseKnowledge、Systems、BusinessFunctions、Evidence、KnowledgeStatus、Relationships、UnknownItems、Knowledge Resolution、BusinessRules 和 Integrations 的聚焦测试文件。未新增或执行测试。

## Specification deviation

没有已知的阻塞性 Specification Deviation。保留一项既有的非阻塞性 Contract 可见性备注：VS-03 已记录 Q05 未返回 C02 接受的 `mainProjects` / `mainEntryPoints`，因此该页面不会预填这两个字段；本次未改动该冻结 Contract 或其实现。

## Cleanup result

- 已删除：API 与测试项目的 `bin/`、`obj/`，前端 `dist/`，VS04 临时浏览器 Profile，VS05 临时 runtime log。
- 未发现仓库内 `coverage/` 目录。
- 已保留：`docs/`、Migrations、测试源码、正式 UI / Verification 截图、`AGENTS.md`、`README.md`。
- 已保留 `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db`，因为它是当前项目使用的开发 SQLite 数据库，不是明确可判定的临时验证数据库。
- 未发现本次指定范围内仍需清理的临时运行产物。

## Process check

未启动新的验证进程。检查时未发现监听 `5090`、`5098` 或 `5173` 的 API / Vite 验证进程。
