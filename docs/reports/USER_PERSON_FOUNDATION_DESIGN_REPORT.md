# User / Person Foundation Design Report

状态：**USER/PERSON FOUNDATION DESIGN PASS**  
日期：2026-08-20

## Reviewed existing areas

- Frozen UI Inventory 与 Design Baseline 的人员 / 权限边界。
- Frozen Domain Model 的 `PersonSnapshot`、Evidence 与 KnowledgeStatus 规则。
- Frozen Database Model 的 `evidence.provider_*`、调查快照与“无 Person 表”现状。
- Frozen Application / API Model 的 `ActorContext`、`PersonSnapshotInput`、C25 HumanConfirmation 与显式 KnowledgeStatus progression。
- Frozen Solution Structure 的 Feature-first、单后端项目、actorStore 与无 Authentication / Authorization 决策。
- 当前 Evidence Domain / EF mapping / Service / Controller / API contracts。
- 当前 Vue `actorStore`、TopBar、HumanConfirmation Drawer 与 Evidence contracts。
- Post-MVP UX Stabilization Report 中 HumanConfirmation method / local time 与 User Foundation deferred 项。

## Design decisions

| Area | Decision |
| --- | --- |
| User vs Person | 选择单一 `User`；不建立独立 `Person` |
| Department / Team | User 上 optional `DepartmentOrTeam` 文本；无组织实体 / 树 |
| Job Title | User 上 optional 自由文本；无主数据表 |
| Knowledge Role | `KnowledgeRole` + `UserKnowledgeRole`；一人多角色；不等于权限 |
| Current User | 首次选择 + 浏览器保存 CurrentUserId；不是登录 / 权限 |
| HumanConfirmation | 继续复用 Evidence；Current User 自动带入、服务端生成历史 Snapshot |
| Snapshot | nullable User / Role reference + 不可变姓名、工号、部门、职位、KnowledgeRole 与时间快照 |
| Deactivation | Disable / Inactive；历史 Evidence 保留；默认不物理删除 |
| Admin UI | `管理 → 用户管理`；List/Create/Edit/Active + Role assignment；无安全 enforcement |
| Auth boundary | Password、SSO、Session、RBAC、Permission 全部 Deferred |

## Compatibility result

- Evidence 仍是一条记录一个 Subject；没有新增第二套确认模型。
- HumanConfirmation 保存后仍不会自动推进 KnowledgeStatus。
- 现有历史 PersonSnapshot 不通过实时 User Join 改写。
- Post-MVP 数据库与 API 变化仅作为后续实施提案；本任务没有修改冻结规范。
- 当前设计未发现阻塞性 Specification Conflict。

## Files created

- `docs/design/USER_PERSON_FOUNDATION_DESIGN.md`
- `docs/reports/USER_PERSON_FOUNDATION_DESIGN_REPORT.md`

## Change status

- Code changed: **No**
- Frozen Specification changed: **No**
- Schema changed: **No**
- Migration created: **No**
- Tests added: **0**
- Build / test / browser runtime executed: **No**（设计任务不需要）

## Final result

`USER/PERSON FOUNDATION DESIGN PASS`
