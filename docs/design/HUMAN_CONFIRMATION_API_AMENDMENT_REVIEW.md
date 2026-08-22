# HumanConfirmation API Amendment Review

审查阶段：HC-A01  
审查日期：2026-08-22  
适用后续 Slice：U04 — HumanConfirmation Current User + Snapshot

## 1. Result

`AMENDMENT READY FOR APPROVAL`

当前真实实现与已批准的 User / Person Foundation Design 之间不存在阻塞性冲突。U04 可以在保留 canonical Route、Evidence 生命周期、KnowledgeStatus 规则与现有 Add Response 的前提下，将 C25 的边界收紧为：客户端提交确认事实，服务端通过现有 Current User Context 生成确认人身份引用与历史快照。

本文件是 Post-MVP 受控 API Amendment 提案，不修改冻结 MVP Specification。未经人工批准，不得据此开始 U04。

## 2. Existing Implementation

### Frontend Drawer

当前 `AddHumanConfirmationDrawer.vue` 仍允许客户端手工录入完整确认人快照：

- `displayName`
- `roleOrIdentity`
- `team`
- `occurredAt`
- `source`（实际承载 Confirmation Method）
- `note`

同时提交确认事实 `confirmationStatement`、`supportReason`、`sourceNote`。Drawer 已将本地时间转换为 UTC ISO 字符串，并在成功后提示“人工确认已记录；知识状态仍需单独推进。”

U03 的 shared API client 已自动附加 `X-Current-User-Id`，但当前 HumanConfirmation Drawer 不读取 Current User Profile，后端 C25 也不使用该 Header。

### Request Contract

当前真实 Request 为：

```text
AddHumanConfirmationRequest
{
    subject: EvidenceTargetRequest
    subjectDetailKey: string | null
    confirmationStatement: string
    supportReason: string
    sourceNote: string | null
    confirmer: PersonSnapshotRequest
    {
        displayName: string
        roleOrIdentity: string
        occurredAt: DateTimeOffset
        team: string | null
        externalUserKey: string | null
        source: string | null
        note: string | null
    }
}
```

该 Request 不包含 `EmployeeNo`、`JobTitle`、`UserId` 或 `KnowledgeRoleId`；服务端完全信任客户端给出的姓名、身份与团队字符串。

### Application / API

`POST /api/evidence/human-confirmations` 将 `confirmer` 直接映射为 `PersonSnapshotCommand`。`EvidenceService.AddHumanConfirmation` 校验 Subject、确认结论、支持理由以及快照中的姓名、身份、时间，随后直接使用客户端快照创建 Evidence。

当前持久化映射为：

- `confirmer.displayName` → `provider_name`
- `confirmer.roleOrIdentity` → `provider_role`
- `confirmer.team` → `provider_team`
- `confirmer.externalUserKey` → `provider_external_key`
- `confirmer.source` → `provider_source`
- `confirmer.note` → `provider_note`
- `confirmer.occurredAt` → `provided_at`

当前 `source_locator_json` 保存 `confirmationStatement` 与 `sourceNote`；`summary` 同时保存确认结论；`source_reference` 保存 `sourceNote`。Confirmation Method 由前端放入 `confirmer.source`，最终错误地落入人员快照来源列 `provider_source`。

### Domain / Persistence / Snapshot

HumanConfirmation 继续是 `EvidenceType.HumanConfirmation`，没有独立实体或表。`Evidence` Domain 当前只保存 primitive value，未依赖 User Entity。`evidence` 表已有完整旧版 `provider_* + provided_at` 快照列，但没有 canonical User / KnowledgeRole reference，也没有 EmployeeNo / JobTitle 快照列。

现有 Evidence 采用 app-managed integer `version`，对 HTTP 暴露 opaque `concurrencyToken`。C25 是 append-only create；Response 返回新 Evidence 的 token。通用 C24 `UpdateEvidence` 是一个独立、受并发令牌保护的“纠正记录”操作，当前可整体替换 ProviderSnapshot；本 Amendment 只修订 C25 创建边界，不静默改写冻结 C24 契约。

### KnowledgeStatus Interaction

当前 C25 创建成功时 `knowledgeStatusChanged` 固定为 `false`。Service 不更新 Subject KnowledgeStatus；现有前端也明确提示状态需由后续显式操作推进。该行为与冻结 Domain、Application Use Case、API Contract 和 Final UI Inventory 一致。

## 3. Problem Statement

U01–U03 已建立唯一 canonical `User`、`KnowledgeRole`、`UserKnowledgeRole` 和 Current User Context。继续允许 C25 客户端手工提交确认人姓名、团队与身份，会形成两个互相冲突的身份事实来源：canonical User 与任意 Request Body。

这会导致：

- Header 表示王敏，但 Body 可以保存为另一人；
- 客户端可以提交未分配、已停用或伪造的 KnowledgeRole 名称；
- User 的 EmployeeNo、JobTitle 无法进入历史快照；
- 身份引用无法追踪到 canonical User / KnowledgeRole；
- Confirmation Method 继续被误存为人员快照来源。

正式边界应改为：

> Client owns confirmation facts; server owns confirmer identity.

Current User 是业务操作者上下文，不是认证身份。服务端的职责是验证客户端声明的 Current User 对应一个存在且 Active 的 canonical User，并从该时点的 canonical 数据生成不可随 Profile 动态变化的 Snapshot。

## 4. Proposed Request Contract

Canonical Route 保持不变：

```text
POST /api/evidence/human-confirmations
```

建议将现有 `AddHumanConfirmationRequest` 原位修订为：

```csharp
public sealed record AddHumanConfirmationRequest(
    EvidenceTargetRequest? Subject,
    string? SubjectDetailKey,
    long? KnowledgeRoleId,
    string? ConfirmationMethod,
    DateTimeOffset? ConfirmedAt,
    string? ConfirmationStatement,
    string? SupportReason,
    string? SourceNote);
```

对应 JSON：

```json
{
  "subject": { "type": "DatabaseColumn", "id": 123 },
  "subjectDetailKey": "KnownValues:30",
  "knowledgeRoleId": 17,
  "confirmationMethod": "Meeting",
  "confirmedAt": "2026-08-22T02:30:00Z",
  "confirmationStatement": "确认 STATE_FLAG=30 表示设备未知或离线。",
  "supportReason": "MES 业务负责人确认生产语义。",
  "sourceNote": "现场评审会议"
}
```

Contract 决策：

- 保留现有 `subject`、`subjectDetailKey`、`confirmationStatement`、`supportReason`、`sourceNote` 名称与语义。
- 新增 nullable `knowledgeRoleId`；它只表示本次确认所采用的知识身份。
- 将当前 `confirmer.source` 中的业务事实正式提升为 `confirmationMethod`。
- 将当前 `confirmer.occurredAt` 的业务事实正式提升为 `confirmedAt`。
- 删除整个 `confirmer` 输入；客户端不得提交 `displayName`、`employeeNo`、`departmentOrTeam`、`jobTitle`、`userId`、KnowledgeRole 名称或其它人员 Snapshot 字段。
- `confirmationMethod` 继续使用当前稳定 code：`InSystem`、`OnSite`、`Meeting`、`Email`、`Document`、`Other`。
- `confirmedAt` 必须是有效的带偏移时间，保存时规范化为 UTC；不新增未经批准的未来时间限制。
- `knowledgeRoleId` 非 null 时必须是 JavaScript 安全范围内的正整数。

这是一项有意的 C25 breaking request amendment。项目当前没有外部版本化 API consumer；U04 必须同步更新 Web 与 API，不提供第二条兼容 Route，也不长期同时接受旧 `confirmer` 与新 Contract。

当前 `AddEvidenceResponse` 已足够：它返回 Evidence ID、类型、Subject、Source Title、Subject 当前状态、`knowledgeStatusChanged: false` 与 token。**No response contract change required.**

## 5. Server-side Hydration Flow

```text
HTTP Request
  → shared client sends X-Current-User-Id
  → ICurrentUserContext.ResolveAsync
  → Missing / Invalid / NotFound / Inactive handling
  → obtain canonical CurrentUser.Id
  → C25 transaction re-reads active User + current role assignments
  → resolve optional KnowledgeRoleId
  → validate confirmation facts and Subject
  → materialize primitive references + immutable snapshot values
  → persist one HumanConfirmation Evidence
  → return existing AddEvidenceResponse
```

Implementation boundary for U04:

1. `EvidenceController.AddHumanConfirmation` 必须注入并调用现有 `ICurrentUserContext`；不得自行读取或解析 Header。
2. 只有 `Available` 可以进入 C25 Application operation；其它状态复用 U03 的 HTTP / Error Contract。
3. Controller 只把 Current User ID 与确认事实交给 C25。Application 在写事务中重新读取 canonical User、当前 UserKnowledgeRole mappings 与 KnowledgeRole Active 状态，避免使用陈旧的浏览器显示值。
4. Subject 与 Role 校验、Snapshot materialization、Evidence insert 必须属于同一 C25 transaction。
5. Evidence Domain 只接收 `ProviderUserId`、`ProviderKnowledgeRoleId` 和 Snapshot primitive values。不得把 User / KnowledgeRole Entity、navigation property、aggregate 或 User service abstraction 引入 Evidence Domain。
6. `SourceTitle` 继续由服务端生成，例如 `人工确认 · {ProviderName}`；客户端不拥有该身份派生字段。

## 6. KnowledgeRole Resolution Rules

“Active KnowledgeRole”严格定义为：Role 存在、`KnowledgeRole.IsActive=true`，且存在当前 User 的 `UserKnowledgeRole` mapping。KnowledgeRole 是知识身份，不是 Permission。

| 当前 User 的 Active Role 数量 | Request `knowledgeRoleId` | 服务端行为 |
| --- | --- | --- |
| 0 | `null` / omitted | 允许创建；`ProviderKnowledgeRoleId=null`，`ProviderRole="知识提供者（未配置知识身份）"`。 |
| 0 | supplied | 不静默忽略；按实际状态返回 Role not found / inactive / not assigned 错误。 |
| 1 | `null` / omitted | 自动采用唯一 Active assigned Role，并保存 ID reference 与当时 Name Snapshot。 |
| 1 | supplied and matches | 接受，保存该 Role。 |
| 1 | supplied but differs | 按 not found / inactive / not assigned 规则拒绝。 |
| 2+ | `null` / omitted | `400 validation_error`；`knowledgeRoleId` 必填，UI 必须让用户明确选择本次知识身份。 |
| 2+ | supplied and valid | 接受，保存该 Role。 |
| 任意 | Role 不存在 | `422 reference_invalid`。 |
| 任意 | Role 已停用 | `422 invalid_state`。 |
| 任意 | Active Role 未分配给 Current User | `422 reference_invalid`。 |

验证顺序为：ID shape → Role existence → Role Active state → Current User assignment → 0/1/multiple resolution。若 Role 在 Drawer 加载后被停用或解除分配，提交时以服务端事务内最新状态为准，不使用客户端缓存。

没有 Active Role 不阻止 HumanConfirmation；不得把 fallback 解释为授权降级，也不得因此创建 Permission、RBAC 或审批逻辑。

## 7. Snapshot Mapping

### Identity and References

| Canonical source | Evidence Domain / column | Reference field | Nullable | Notes |
| --- | --- | --- | --- | --- |
| `CurrentUser.Id` | `ProviderUserId` / `provider_user_id` | User reference | DB: Yes; new C25: No | 新 HumanConfirmation 必须保存；普通旧 Evidence 与历史行保持 null。 |
| `CurrentUser.EmployeeNo` | `ProviderEmployeeNo` / `provider_employee_no` | — | Yes | 当时值的 Snapshot；同名消歧，不是有效性门槛。 |
| `CurrentUser.DisplayName` | 现有 `ProviderName` / `provider_name` | — | No | 服务端 trim 后保存；历史显示以该 Snapshot 为准。 |
| `CurrentUser.DepartmentOrTeam` | 现有 `ProviderTeam` / `provider_team` | — | Yes | 复用现有 Team / Organization Snapshot 列。 |
| `CurrentUser.JobTitle` | `ProviderJobTitle` / `provider_job_title` | — | Yes | 当时职位 Snapshot。 |
| resolved `KnowledgeRole.Id` | `ProviderKnowledgeRoleId` / `provider_knowledge_role_id` | KnowledgeRole reference | Yes | 0 Active Role fallback 时为 null。 |
| resolved `KnowledgeRole.Name` | 现有 `ProviderRole` / `provider_role` | — | No | 保存当时 Role Name；0 Active Role 时保存固定 fallback 文案。 |
| Request `confirmedAt` | 现有 `ProvidedAt` / `provided_at` | — | No | 规范化为 UTC 后保存。 |

新 C25 不再接受 `provider_external_key`、`provider_source` 或 `provider_note` 的客户端输入：

- `provider_external_key`：新 C25 写 null；不得把 UserId 编码到该字符串列。
- `provider_source`：新 C25 写 null；Current User reference 已明确表达来源。旧记录原值保持不变。
- `provider_note`：新 C25 写 null；确认来源说明继续使用 `sourceNote`，不得混入人员备注。

### Confirmation Fact Mapping

| Request fact | Persistence | Notes |
| --- | --- | --- |
| `confirmationMethod` | `source_locator_json.confirmationMethod` | 不再写入 `provider_source`。 |
| `confirmedAt` | `provided_at` | 同时是 PersonSnapshot 的 OccurredAt。 |
| `confirmationStatement` | `summary`，并保留 `source_locator_json.confirmationStatement` | 保持当前读兼容与可读摘要。 |
| `supportReason` | `support_reason` | 现有必填语义不变。 |
| `sourceNote` | `source_locator_json.sourceNote`；可继续规范化到 `source_reference` | 保持当前 Evidence Detail 可读性；null 允许。 |

新写入的 HumanConfirmation locator 至少为：

```json
{
  "confirmationMethod": "Meeting",
  "confirmationStatement": "确认内容",
  "sourceNote": "现场评审会议"
}
```

历史 HumanConfirmation 的 Confirmation Method 读取兼容顺序为：先读 `source_locator_json.confirmationMethod`，不存在时回退到旧 `provider_source`。不得批量猜测或重写旧值。

## 8. Schema Amendment

U04 只允许对现有 `evidence` 表进行 additive amendment。

### Existing Columns Retained

`provider_name`、`provider_role`、`provider_team`、`provider_external_key`、`provider_source`、`provider_note`、`provided_at` 以及全部现有 Evidence / concurrency 列原样保留，不重命名、不删除、不改变历史 nullability。

### Proposed Nullable Columns

| Column | SQLite type | Nullable | Constraint / purpose |
| --- | --- | --- | --- |
| `provider_user_id` | INTEGER | Yes | FK → `users(id)`，`RESTRICT`；canonical source reference。 |
| `provider_knowledge_role_id` | INTEGER | Yes | FK → `knowledge_roles(id)`，`RESTRICT`；selected knowledge identity reference。 |
| `provider_employee_no` | TEXT | Yes | immutable snapshot value；无 FK、无 unique。 |
| `provider_job_title` | TEXT | Yes | immutable snapshot value。 |

FK 决策：采用真实数据库 FK，并使用 `RESTRICT`。User 与 KnowledgeRole 均无物理 Delete API；Evidence 是长期历史事实。FK 防止未来意外删除 canonical source，但 Snapshot 读取不依赖 Join。Domain 只保存 nullable primitive IDs，不增加 navigation property。

Index 决策：为两个 nullable FK 各保留一个简单单列索引：`provider_user_id`、`provider_knowledge_role_id`。它们服务于 reference traceability 与 `RESTRICT` 引用检查；不增加复合、覆盖、Snapshot 文本或 Confirmation Method 索引。若生成的 Migration 名称遵循 EF convention，可沿用 convention 名称。

Migration 行为：

- 只增加四个 nullable columns、两个 `RESTRICT` FK 和上述两个简单索引。
- 所有历史行的四个新字段自然为 null。
- 不执行姓名 / 工号匹配，不 backfill UserId，不建立 Role reference。
- 不修改任何历史 Snapshot、locator 或 `provider_source`。
- 不生成 destructive data migration；SQLite 若因加 FK 需要内部 table rebuild，必须检查生成 SQL，证明行数据与现有约束完整保留。
- 普通 Evidence 写入继续允许四个新字段为 null；只有新 C25 HumanConfirmation 在 Application 层要求 `ProviderUserId` 非 null。

Concurrency 决策：**No new concurrency mechanism required.** C25 仍创建 version 1 的新 Evidence，并返回现有 opaque token；Request 不新增 token。User / Role 在 C25 transaction 内重新读取，不引入第二套 version、锁 token 或分布式并发框架。

## 9. Legacy Compatibility

历史 Evidence / HumanConfirmation 保持原样：

- `provider_user_id`、`provider_knowledge_role_id`、`provider_employee_no`、`provider_job_title` 均为 null。
- 不按 `provider_name`、`provider_external_key`、EmployeeNo 或任何自由文本猜测 canonical User。
- 不按 `provider_role` 猜测或建立 KnowledgeRole mapping。
- 现有 `provider_name / role / team / external_key / source / note / provided_at` 继续是有效历史 Snapshot。
- 历史详情显示优先使用 Snapshot，不要求 User / Role Join；null reference 不是数据错误。
- 旧 Confirmation Method 继续从 `provider_source` 兼容读取；新记录从 locator 读取。

新 HumanConfirmation 保存后，User 改名、变更 EmployeeNo、DepartmentOrTeam 或 JobTitle，均不得回写 Evidence。KnowledgeRole 后续改名、停用或解除 assignment，也不得改变 `provider_role` Snapshot 或使历史确认失效。Reference 用于追踪 canonical source；Snapshot 用于保存确认发生时的事实。

## 10. API Error Behavior

继续使用现有 `ApiErrorResponse(code, message, fieldErrors, details)`；不新增错误 Envelope 或通用 User Context framework。

| Condition | HTTP / code | Required behavior |
| --- | --- | --- |
| Current User Missing | `400 validation_error` | 复用 U03 message 与 `details.currentUserStatus="missing"`、Header 名；UI 要求选择当前操作者。 |
| Current User Invalid | `400 validation_error` | `details.currentUserStatus="invalid"`；清除无效选择并重新选择。 |
| Current User NotFound | `404 not_found` | `details.currentUserStatus="not_found"`；不得创建 Evidence。 |
| Current User Inactive | `422 invalid_state` | `details.currentUserStatus="inactive"`；不得使用停用 User 创建确认。 |
| required fact missing / invalid | `400 validation_error` | 使用现有字段错误；字段名改为 proposed Contract 名称。 |
| multiple Active Roles but ID omitted | `400 validation_error` | `fieldErrors.knowledgeRoleId` 明确要求选择本次知识身份。 |
| Role ID invalid safe-integer shape | `400 validation_error` | `fieldErrors.knowledgeRoleId`。 |
| Role does not exist | `422 reference_invalid` | details 可包含 `resourceType="KnowledgeRole"`、`resourceId`。 |
| Role inactive | `422 invalid_state` | message 明确角色已停用，请刷新后重选。 |
| Role not assigned to Current User | `422 reference_invalid` | message 明确该知识身份未分配给当前操作者。 |
| Subject invalid / unavailable | 保持当前 `400 validation_error` / `422 reference_invalid` | 不改变 C25 Subject Contract。 |

必审边界场景结论：

| # | Scenario | Expected behavior |
| --- | --- | --- |
| 1 | Current User Missing | 400；不得创建。 |
| 2 | Current User Invalid | 400；不得创建。 |
| 3 | Current User NotFound | 404；不得创建。 |
| 4 | Current User Inactive | 422；不得创建。 |
| 5 | 0 Active KnowledgeRole | 使用 fallback，Role reference 为 null；允许创建。 |
| 6 | exactly 1 Active KnowledgeRole | 未传 ID 时服务端自动采用唯一 Role。 |
| 7 | multiple Active KnowledgeRoles | 必须明确提交一个有效 ID。 |
| 8 | ID 不属于 Current User | 422 `reference_invalid`。 |
| 9 | ID 对应 Inactive Role | 422 `invalid_state`。 |
| 10 | Role 在确认后被停用 | 历史 ID 与 Name Snapshot 保留，确认仍有效。 |
| 11 | Role 在确认后改名 | 历史 `provider_role` 不变；reference 仍指向同一 Role。 |
| 12 | User 在确认后改名 | 历史 `provider_name` 不变。 |
| 13 | User 后续变更 Department / JobTitle | 历史 team / job title Snapshot 不变。 |
| 14 | 历史 Evidence 没有 User reference | 继续正常读取；reference 保持 null，不猜测回填。 |
| 15 | C25 创建成功 | 返回 201 且 `knowledgeStatusChanged=false`；Subject 状态不变。 |

## 11. UI Amendment for U04

U04 只调整现有 DR-10 风格的单 Drawer，不新增 Route、Wizard 或第二个 Drawer manager。

“确认人快照”手工表单改为“当前操作者”只读区：

- DisplayName
- EmployeeNo
- DepartmentOrTeam
- JobTitle

这些值来自唯一 `actorStore.currentUser` / `GET /api/current-user`；浏览器显示只用于可见性，最终持久化仍由服务端重新 Hydrate。资料修改入口只指向现有用户管理，不允许在 Drawer 内编辑 Snapshot。

KnowledgeRole UI：

- 0 Active Role：显示“知识提供者（未配置知识身份）”与非阻塞说明；不传 ID。
- 1 Active Role：自动选中，可只读显示；允许省略 ID，由服务端自动解析。
- 多个 Active Role：Select 必填，提交 `knowledgeRoleId`；不得手工输入 Role 名称。
- Inactive Role 不显示为可选项；若保存前状态变化，展示后端 422 并刷新 Current User Profile。

确认事实继续使用：确认方式、确认时间、确认结论、支持理由、来源说明。删除姓名、角色自由文本、团队自由文本和人员 Snapshot 备注输入。Confirmation Method 继续使用现有 fixed code + 中文标签；本地日期时间在提交边界转换为 UTC。

Current User 缺失或失效时，Drawer 显示现有“选择当前操作者”恢复路径并禁用保存，不以 fallback Actor 创建 HumanConfirmation。保存成功继续显示“人工确认已记录；知识状态仍需单独推进”，并打开现有 Evidence Detail。

Add Response 与现有 Evidence Detail Response 足以完成 U04 创建路径；本 Slice 不为显示引用状态扩大 Response 或创建第二个 ViewModel。详情中的新旧 Confirmation Method 通过 locator-first / legacy `provider_source` fallback 读取。

## 12. KnowledgeStatus Guarantee

`HumanConfirmation creation does not automatically change KnowledgeStatus.`

`No KnowledgeStatus transition is introduced by this amendment.`

C25 只插入一条 `EvidenceType.HumanConfirmation`。它不得写 Subject 的 `knowledge_status`、status reason、changed-at/by 或 Subject version。Response 必须继续返回创建前读取到的 Subject 状态与 `knowledgeStatusChanged: false`。

冻结路径保持 `Unknown → Inferred → Confirmed`。`Unknown → Confirmed` 仍禁止；`Inferred → Confirmed` 仍需相关且完整的 HumanConfirmation，并由现有显式 C22 / C26 操作完成。KnowledgeRole 名称或存在性绝不自动触发状态变化。

## 13. Explicitly Unchanged

- User Foundation：继续使用 U01 的唯一 User、KnowledgeRole、UserKnowledgeRole 与 Active 语义。
- Current User：继续是 U03 的 operator context，不是 authentication identity；继续复用唯一 `actorStore` 与 `ICurrentUserContext`。
- Security boundary：`X-Current-User-Id` 不是可信认证凭据。Snapshot 代表“当前客户端声明、并由系统验证 canonical User 存在且 Active 的业务操作者上下文”，不是 cryptographically authenticated identity。
- Authentication、Authorization、RBAC、Permission、Login、Password、Session、SSO 均不进入本 Amendment 或 U04。
- 不建立 Person、Department、Team、JobTitle Entity 或 Organization Tree。
- HumanConfirmation 继续属于 Evidence Feature、`EvidenceType.HumanConfirmation`、现有 `evidence` 表与 canonical Route。
- 普通 Evidence 的完整 ProviderSnapshot input 不变；只有 C25 HumanConfirmation 创建改为服务端 Hydration。
- 通用 C24 `UpdateEvidence` 的显式纠正与 concurrencyToken 契约不在本 Amendment 中重构。纠正是有意写操作，不等同于 User Profile 动态回写。
- Evidence lifecycle、单 Subject 规则、Source Locator 规则、现有 Add Response 与 app-managed integer concurrency strategy 不变。
- KnowledgeStatus lifecycle、Evidence 门槛和显式推进 / 回退行为不变。
- Frozen MVP Specification 与 Golden asset 不修改；DR-10 继续作为布局与信息层级参考，但 Post-MVP Drawer 身份输入按本 Amendment 收紧。
- 不重构 Actor、ActorRequest、CreatedBy、UpdatedBy、Audit 或其它 PersonSnapshot 使用位置。
- 不引入 Repository、CQRS/MediatR、Mapper、Generic Snapshot、Generic Identity 或 Generic User Context framework。

## 14. Risks / Open Decisions

没有阻塞 Amendment Approval 的未决设计问题。

| Risk | Resolution in this Amendment |
| --- | --- |
| C25 Request 是 breaking change | 同一内部产品的 Web 与 API 在 U04 同步发布；不增加 `/v2` 或双 Contract。 |
| UI Profile / Role 在提交前变旧 | UI 只预览；服务端在 C25 transaction 内重读并校验 canonical User / assignment / Role。 |
| 旧 Method 位于 `provider_source` | 新写入 locator；读取采用 locator-first、legacy fallback；不回填。 |
| nullable FK 与历史事实语义冲突 | 新 C25 要求 User reference，历史 / 普通 Evidence 允许 null；Snapshot 读取不依赖 Join。 |
| Header 可由客户端切换 | 明确为非认证 operator context；U04 不宣称安全身份，不增加 Permission。 |
| C24 仍允许显式纠正 ProviderSnapshot | 保留冻结纠正能力与 409 保护；若未来需要 append-only correction history，作为独立 Follow-up，不扩大 U04。 |

批准本 Amendment 即批准以下核心决策：移除 C25 `confirmer` 输入、采用 proposed fact-only Request、使用 0/1/multiple Role 规则、增加四个 nullable Evidence columns 与两个 `RESTRICT` FK、保持 Add Response 与 KnowledgeStatus 行为不变。

## 15. Proposed U04 Scope

批准后，U04 只允许实现：

1. 原位修订前后端 `AddHumanConfirmationRequest`，移除 `confirmer`，增加 `knowledgeRoleId`、`confirmationMethod`、`confirmedAt`。
2. C25 复用 `ICurrentUserContext`，在写事务内重读 User / Role mapping 并 Hydrate Snapshot。
3. Evidence Domain 增加四个 nullable primitive fields；不增加 User / KnowledgeRole navigation 或 aggregate dependency。
4. EF Configuration 与一条 additive Migration：四列、两个 `RESTRICT` FK、两个简单 FK index；历史值保持 null。
5. 新 HumanConfirmation 将 Method 写入 locator；详情读取兼容旧 `provider_source`。
6. 调整现有 HumanConfirmation Drawer：Current User 资料只读、Role 0/1/multiple 交互、事实字段提交与错误恢复。
7. 风险导向验证：
   - Current User Missing / Invalid / NotFound / Inactive；
   - 0、1、multiple Active Roles；not assigned 与 inactive Role；
   - Snapshot/reference 正确落盘，User / Role 后续变化不改历史；
   - legacy null reference 读取与 Method fallback；
   - C25 201 且 KnowledgeStatus 不变；
   - 现有 Add Response shape 不变；
   - backend build / focused SQLite integration tests、frontend type-check/build 与一个关键 Browser → API → SQLite 路径。
8. 生成独立 U04 Verification Report，并清理所有验证进程与端口。

U04 明确不包含：Authentication、Authorization、RBAC、Permission、Login、SSO、Person / Organization 模型、普通 Actor 全仓重构、其它 PersonSnapshot Hydration、Evidence architecture rewrite、通用框架、C24 redesign、KnowledgeStatus 自动推进或 U05 及后续阶段。

HC-A01 到此停止；等待人工 Review / Approval，不自动开始 U04。
