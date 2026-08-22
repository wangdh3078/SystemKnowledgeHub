# U03 — Current User 验证报告

## Result

**U03 PASS**

验证日期：2026-08-20

## Implemented

- **Current User Selector**：在现有 TopBar 增加紧凑 Profile / Switcher；明确使用“当前操作者”，并说明它不是登录账号或权限身份。
- **Current User Store / Context**：将既有 `actorStore` 演进为唯一 Current User / Actor 来源，保存 selected UserId 与最新 canonical User Profile；普通冻结 Actor Request 继续从 Profile 派生兼容值，没有建立第二个长期并行 store。
- **Persistence**：浏览器仅在 `localStorage` 保存 `systemKnowledgeHub.currentUserId`；刷新时通过服务端重新读取最新 Profile，不把完整人员资料缓存为事实来源。
- **Active-only Selection**：Switcher 通过 U01 `GET /api/users?isActive=true` 分页读取全部 Active User；Inactive User 不出现在新选择候选中，服务端也会再次拒绝停用 User。
- **Request Header Propagation**：共享 native-fetch API Client 统一附加 `X-Current-User-Id`；页面和 Feature API 不手工设置 Header，也没有第二套 API Client。
- **Backend Current User Context**：新增最小 `ICurrentUserContext` / `CurrentUserContext`，从 Request Header 解析 safe integer ID，并用 canonical User/KnowledgeRole 查询确认存在与 Active 状态。Resolution 明确区分 Available、Missing、Invalid、NotFound、Inactive；具体业务操作可决定 optional 或 required，未加入权限判断。
- **Current User API**：新增 `GET /api/current-user`，返回最新 User Profile 与 Knowledge Roles，不返回编辑用 `concurrencyToken`。
- **Invalid / Inactive Handling**：缺失或格式无效 Header 返回 `400`；User 不存在返回 `404`；User 已停用返回 `422`。前端识别稳定 `details.currentUserStatus`，清除失效的本地选择，显示原因并要求重新选择，不自动切换。
- **U01 / U02 Compatibility**：既有 Admin API 没有被强制要求 Current User；无选择时仍保留由同一 store 计算的非身份 fallback Actor，以满足冻结的普通 MVP Actor Body Contract。

未新增 CurrentUser Entity、CurrentUser Table、Server Session、Migration 或第二套 User 模型。

## Explicitly Not Implemented

- Authentication、Authorization、RBAC、Permission、Claims。
- Login、Logout、Password、JWT、OAuth、OIDC、SSO、Session Authentication、ASP.NET Core Identity 或 IdentityServer。
- HumanConfirmation Request / Domain / API / schema 修改。
- HumanConfirmation Current User 自动带入或 User Snapshot。
- Evidence schema 修改。
- Person Entity、Department Entity、Team Entity、JobTitle Entity、Organization Tree。
- Audit Framework、AI 功能或新的 Identity Framework。
- U04 或 HumanConfirmation API Amendment Review。

## Current User Semantics

`Current User is an operator context, not an authentication identity.`

`X-Current-User-Id` 只是当前客户端选择的业务人员上下文，不是认证凭据、授权依据或 Permission Principal。

## UI Verification

通过真实 `Browser → Vue → shared API Client → ASP.NET Core → EF Core → SQLite` 闭环验证：

| Area | Result | 实际验证 |
| --- | --- | --- |
| Select | PASS | 首次无选择时 TopBar 自动展开“当前操作者”选择器；只显示 Active User；选择后 TopBar 显示 DisplayName 与 JobTitle。 |
| Switch | PASS | 创建第二个 Active User 后，从既有用户切换至 `U03 切换验证用户`；TopBar 与随后 Current User Profile 使用新 User。 |
| Refresh restore | PASS | 选择第一个 Current User 后刷新页面；TopBar 从只保存的 UserId 恢复同一 canonical Profile。 |
| Inactive handling | PASS | 将当前选中的验证用户通过 U02 UI 停用；再次打开 Switcher 时收到“当前操作者已停用，请重新选择其他启用用户”，本地选择被清理；候选列表只保留 Active User，不包含刚停用的 User。 |
| Invalid User handling | PASS | Backend integration 验证不存在 ID 返回稳定 `404/not_found`；store 聚焦测试验证该响应会清除本地 ID、显示“重新选择”原因，并拒绝不在 Active candidates 中的 UserId。 |

Switcher 与 TopBar 的浅色、紧凑视觉和信息密度经运行截图检查；浏览器 Console 最终为 0 error / 0 warning。验证后恢复为有效 Current User，验证页已关闭。

## Request Verification

- 统一 Header：`X-Current-User-Id`。
- Header provider 在应用 Bootstrap 时连接到唯一 `actorStore.selectedUserId`。
- API Client 聚焦测试验证第一次请求发送 ID `21`，切换后下一请求发送 ID `34`，证明 Header 不是初始化时的静态值。
- 浏览器选择、切换与刷新均成功调用 `GET /api/current-user` 并取得对应 Profile，证明真实运行链路完成 Header 传播与后端解析。
- Backend integration 同时验证 Header 缺失、格式无效、不存在 User 和 Inactive User 的实际 HTTP 结果。

## API / Contract Changes

U03 只增加以下最小 contract：

1. `GET /api/current-user`
   - Request Header：`X-Current-User-Id: {UserId}`。
   - `200`：返回 `id`、`employeeNo`、`displayName`、`email`、`departmentOrTeam`、`jobTitle`、`isActive`、`knowledgeRoles`。
   - `400 validation_error`：Header 缺失或格式/安全整数范围无效。
   - `404 not_found`：User 不存在。
   - `422 invalid_state`：User 已停用。
   - 错误 `details.currentUserStatus` 分别使用 `missing`、`invalid`、`not_found`、`inactive`。
2. 所有现有前端 API 请求在已选择 Current User 时，由 shared API Client 自动附加 `X-Current-User-Id`；未选择时不发送 Header。

U01 `/api/users`、`/api/knowledge-roles` 的 canonical route、请求、响应、KnowledgeRole 映射与 `concurrencyToken` 语义均未修改。HumanConfirmation API 未修改。

## Tests

### Backend focused tests

```text
dotnet test tests\SystemKnowledgeHub.Api.Tests\SystemKnowledgeHub.Api.Tests.csproj --no-build --no-restore --filter "FullyQualifiedName~CurrentUserApiTests|FullyQualifiedName~UsersApiTests"
```

结果：**5 passed / 0 failed / 0 skipped**。

- 2 个 U03 tests：Active canonical Profile/Role 解析；Missing、Invalid、NotFound、Inactive 错误；无 Header 的 Admin API 兼容。
- 3 个 U01 regression tests：User/Role 创建读取、映射、启停、停用 Role 规则与 stale token `409`。

### Frontend focused tests

```text
npm run test -- src/app/stores/actor.spec.ts src/api/client/apiClient.spec.ts
```

结果：**2 files passed，5 tests passed / 0 failed**。

覆盖：刷新恢复、ActorContext 派生、无效 UserId 清理、Active candidate 约束、Header 自动附加与切换后使用新 ID。

### Frontend static checks

```text
npm run type-check
```

结果：**PASS**。

```text
node_modules\.bin\eslint.cmd src\api\client\apiClient.ts src\api\client\apiClient.spec.ts src\app\bootstrap\bootstrapApp.ts src\app\stores\actor.ts src\app\stores\actor.spec.ts src\features\users\api\userContracts.ts src\features\users\api\usersApi.ts src\layouts\AppTopBar.vue
```

结果：**PASS，0 error / 0 warning**。

U02 报告已确认的全仓非 U03 历史 lint 问题未在本 Slice 越界修改；U03 自身未新增 lint error/warning。

## Build

```text
dotnet build SystemKnowledgeHub.sln --no-restore
```

结果：**成功，0 warnings / 0 errors**。

```text
npm run build
```

结果：**成功**。Vite 保留既有大 chunk 提示，产物正常生成；该提示不是 U03 lint error 或功能失败。

## Specification / Design Deviation

- 未发现阻塞性 Specification / Design Conflict，**无设计偏差**。
- 实现遵守已批准的 User / Person Foundation Design：Current User 引用唯一 canonical User，只保存客户端 UserId，TopBar 明确表达业务操作者上下文。
- 没有修改 frozen MVP Specification、Golden UI、U01/U02 canonical API、数据库 schema 或 HumanConfirmation。
- 继续保持单 ASP.NET Core 项目 + Feature-first Modular Monolith；未增加物理架构层或身份/权限框架。

## Process Cleanup

- 本次启动的 ASP.NET Core Development Server 已停止；验证端口 `5090` 已释放。
- 默认 `5173` 在 U03 启动前已被本项目此前验证遗留的 Vite 进程（PID `33736`，启动时间 22:20）占用，因此本次 U03 Vite 改用 `5174`。首次报告错误判断该既有 listener 已退出；交付后复核通过端口与页面内容确认其仍在运行，随后已精确停止 PID `33736` 并确认 `5173` 释放。
- 本次 Vite 改用 `5174`，其精确 listener PID 已停止；端口 `5174` 已释放。
- Browser 自动化验证 tab 已关闭，Browser session 已重置；对应临时运行进程已退出。
- 未启动 watch test server、mock server 或其它长期验证进程。
- 更正后的结束检查确认 `5090`、`5173`、`5174` 均无 Listen；没有遗留 ASP.NET Core 或 Vite 验证 listener。

U03 已完成；未开始 HumanConfirmation API Amendment Review 或 U04。
