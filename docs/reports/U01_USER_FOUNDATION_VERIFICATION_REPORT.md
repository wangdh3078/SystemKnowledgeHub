# U01 — User Foundation + Persistence 验证报告

## Result

**U01 PASS**

验证日期：2026-08-20

## Implemented

- `User`：列表、详情、创建、基础资料更新、显式启用/停用。
- `KnowledgeRole`：列表、创建、更新、显式启用/停用。
- `UserKnowledgeRole`：用户与知识角色的多对多映射；用户更新时与基础资料原子提交。
- Persistence：在现有 `KnowledgeHubDbContext` 中增加 canonical DbSet 与 EF Core 配置。
- Migration：新增 `AddUserFoundation`，仅创建 `users`、`knowledge_roles`、`user_knowledge_roles`。
- API：实现 `/api/users` 与 `/api/knowledge-roles` 下的 U01 canonical routes。
- Concurrency：继续使用 app-managed integer version；客户端只读写 opaque `concurrencyToken`，stale token 返回 `409`。

## Explicitly Not Implemented

- Admin UI、Current User、`X-Current-User-Id`、TopBar User。
- HumanConfirmation / Evidence Domain、Contract 或 schema 修改。
- Authentication、Authorization、RBAC、Permission、Login、SSO。
- `Person`、Department/Team Entity、JobTitle Entity、Organization Tree。
- User / KnowledgeRole Delete、Audit Framework、通用 Repository/CQRS/Mapper。

## Schema

- `users`
  - `employee_no` 与 `email`：非空时使用 `NOCASE` 唯一索引。
  - `(is_active, display_name)` 索引用于列表读取。
  - `is_active`、`version` 使用 CHECK 约束。
- `knowledge_roles`
  - `name` 使用 `NOCASE` 全局唯一索引。
  - `(is_active, name)` 列表索引；`is_active`、`version` 使用 CHECK 约束。
- `user_knowledge_roles`
  - `(user_id, knowledge_role_id)` 复合主键。
  - 两端 FK 均为 `RESTRICT`；增加 `knowledge_role_id` 索引。
- Migration 未插入用户业务数据，未修改 Evidence 或既有 MVP 表。

## Tests

运行命令：

```text
dotnet test tests\SystemKnowledgeHub.Api.Tests\SystemKnowledgeHub.Api.Tests.csproj --no-build --filter "FullyQualifiedName~UsersApiTests"
```

结果：**3 passed / 0 failed / 0 skipped**。

覆盖的高风险行为：

1. User Create/List/Detail，以及 EmployeeNo/Email 的大小写不敏感唯一性。
2. User 基础资料更新、KnowledgeRole 映射替换与 stale token `409`。
3. User 启停、KnowledgeRole 停用后保留既有映射、禁止新增停用角色分配。

## Build

运行命令：

```text
dotnet build SystemKnowledgeHub.sln --no-restore
```

结果：**成功，0 warnings / 0 errors**。

## Runtime Verification

通过真实 SQLite 测试数据库与 HTTP integration fixture 完成聚焦运行验证：

```text
Create KnowledgeRole
→ Create User / Assign Role
→ Get User Detail
→ Update DepartmentOrTeam / JobTitle
→ Disable User
→ Re-enable User
→ stale concurrencyToken 返回 409
```

本 Slice 无 UI，未执行浏览器 E2E。

## Specification / Design Deviation

未发现阻塞性冲突或规格偏差。实现保持单一 `User`、单一 `KnowledgeRole` 与单一 join model，未引入 Person、认证、权限或通用身份框架。

`GET /api/knowledge-roles` 的每个管理列表项包含 opaque `concurrencyToken`，供已批准的 Update / Active State 路由使用；没有增加额外 Detail route 或第二套并发机制。

## Process Cleanup

- 未启动 ASP.NET Core、Vite、watch/test server 或其他长期验证进程。
- 验证端口 `5090`、`5173` 均未被占用。
- 无需清理后台验证进程。

U01 已完成；未开始 U02、Admin UI、Current User、HumanConfirmation Snapshot 或 Auth/Permission。
