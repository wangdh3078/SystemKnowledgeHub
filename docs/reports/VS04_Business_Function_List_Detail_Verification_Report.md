# VS-04 — Business Function List + Detail Verification Report

## Result

**VS04 PASS**

## 实现范围

- Q06 `GET /api/business-functions`：System Context、关键字、功能类型、改写状态、知识状态、待确认事项、分页与冻结排序。
- Q07 `GET /api/business-functions/{id}`：System Context、Header、Overview、简单有序 Business Process，以及关系/规则/集成/证据/待确认事项的契约 Section。
- RP-04 业务功能列表与 RP-05 业务功能详情；System Detail 的业务功能 Section 改为真实摘要并连接列表/详情。
- 未实现 Create/Edit、Relationship/Evidence/UnknownItem、KnowledgeStatus Change 或 Delete。

## Schema / Migration

- 新增 canonical `business_functions` 与 `business_process_steps`。
- 新增 Migration：`AddBusinessFunctions`；System FK、唯一性、状态 CHECK、流程顺序约束与 `RESTRICT` 均按冻结 Database Model。
- 复用唯一 `systems`、Database Knowledge 与整数 `version` 并发读取路径；没有第二套 System/Database Object 模型。

## API

- `GET /api/business-functions`
- `GET /api/business-functions/{id}`
- 没有新增第二套路由；Controller 不返回 EF Entity。

## 主要文件

- Backend：`Features/BusinessFunctions/Domain`、`Persistence`、`Application/BusinessFunctionQueries.cs`、`Application/Models/BusinessFunctionReadModels.cs`、`Api/BusinessFunctionsController.cs`。
- Frontend：`features/business-functions/` 下的 API contract/client、两个 composable、列表/详情页、Context Rail 和局部 CSS。
- Integration：`SystemQueries.cs`、`SystemDetailView.vue`、Router/Navigation。
- Tests：`tests/SystemKnowledgeHub.Api.Tests/Api/BusinessFunctionsApiTests.cs`。

## Focused tests / build

- `dotnet build SystemKnowledgeHub.sln --no-restore`：PASS，0 warning / 0 error。
- focused backend tests：BusinessFunctions + affected Systems tests，6/6 PASS；其中 VS-04 新增 2 个高价值测试。
- `npm run type-check`：PASS。
- `npm run lint`：PASS。
- `npm run build`：PASS；仅有既有单 chunk > 500 kB 的非阻塞提示。
- 未新增 frontend test：本 Slice 没有复杂前端业务状态，严格 decoder/type-check 与真实运行验证已经覆盖主要风险。

## Runtime verification

- System Detail `/systems/12` 返回 5 个真实 MES Business Function 摘要，并提供“查看全部”和行导航。
- `/business-functions?systemId=12` 返回 5 项；`Equipment Status Query` 可进入 `/business-functions/77`。
- Detail 返回 6 个按 order 排列的流程步骤；尚未落地的 Related Data 等 Section 为真实空集合。
- API、Vite 与 SQLite 真实链路均返回成功；前端 Route 返回 HTTP 200 并成功渲染截图。
- Windows 将 5089–5188 设为 TCP 排除范围，故验证临时使用 5190/5191；临时 Vite 配置已删除，不改变正式 5090/5173 配置。

## Golden UI Review

- Golden：RP-04、RP-05 的 Inventory 唯一 Golden Reference。
- 对照证据：`artifacts/VS04/RP04-source-implementation-comparison.png`、`RP05-source-implementation-comparison.png`。
- 验证 1672×941、1920×1080，以及 1440×900、1366×768；列表密度、简单流程、Section hierarchy、功能级 Rail 与正式简体中文均通过。
- Root `design-qa.md`：`final result: passed`。

## Specification Deviation

- **无阻塞性 Specification Deviation。**
- `hasUnknownItems=true` 在 UnknownItem Schema 尚未进入当前 Slice 时返回空结果，`false`/未指定返回真实功能；没有用硬编码数量伪装关联数据。
- RP-05 Golden 的 Relationship Drawer 依赖 Q15，不属于本 Slice，未实现也未伪造。

## Process cleanup

- Codex 启动的 ASP.NET Core 与 Vite listener 已关闭。
- 5090、5173、5190、5191 均无监听；没有遗留 watch/test server。
- 8 个临时运行日志与临时 Vite 配置已删除；UI Review 截图与组合对照保留在被 Git 忽略的 `artifacts/VS04/`。
- Development SQLite 与 Headless Edge profile 仍位于 Git 忽略目录；本轮没有在缺少明确数据删除授权时物理删除数据库。它们不占用端口，也不属于源码或提交内容。

## Deferred

- Business Function Create/Edit、Process Replace、Relationship、Evidence、UnknownItem、KnowledgeStatus Change。
- BusinessRule、Integration 与 Relationship Detail API/Drawer。
- 不开始 Business Function Create/Edit 或下一条 Vertical Slice。
