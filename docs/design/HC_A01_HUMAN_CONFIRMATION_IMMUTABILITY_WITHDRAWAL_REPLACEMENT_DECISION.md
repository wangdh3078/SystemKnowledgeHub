# HC-A01 — HumanConfirmation 不可变、撤销与替代决策

日期：2026-09-07。状态：**FROZEN / APPROVED**。本次为 architecture / contract decision，实施归属 **HC-B01**。

基线：`main` / `30c745b382ac1d5cd1f50fff1f8a95407b7ae916`。本文件不表示当前运行时已经支持撤销，也不授权自动开始实施。

## 1. Authority 与显式 amendment

本次用户指令明确批准冻结以下纠正模型。依据与优先级：

- [既有 HumanConfirmation amendment](HUMAN_CONFIRMATION_API_AMENDMENT_REVIEW.md)及 [U04 实施报告](../reports/U04_HUMAN_CONFIRMATION_CURRENT_USER_SNAPSHOT_VERIFICATION_REPORT.md)：canonical User/KnowledgeRole + 创建时身份快照；旧文档中“C24 保持不变”是其当时范围。
- [MVP API C23/C24/C25/Q16](../specifications/System_Knowledge_Hub_MVP_API_Contract.md)：普通 Evidence correction、不可 rebind、opaque token、历史详情。
- [User/Person Foundation](USER_PERSON_FOUNDATION_DESIGN.md)及后续 [AUTH-A01](AUTH_A01_LOCAL_LOGIN_OIDC_COEXISTENCE_DESIGN_REVIEW.md)：KnowledgeRole 不是权限；当前以 authenticated principal-backed Current User 为准，不恢复早期浏览器 User selector / X-Current-User-Id。
- [REV-A01](REV_A01_REVISION_ARCHITECTURE_AND_CONTRACT_DECISION.md)、[TRACE-A01](TRACE_A01_TRACEABILITY_ARCHITECTURE_AND_CONTRACT_DECISION.md)、[PORTAL-A01](PORTAL_A01_INTERNAL_KNOWLEDGE_PORTAL_ARCHITECTURE_DECISION.md)、[AMEND-01](../reports/PORTAL_A01_AMEND_01_TRUSTSUMMARY_SOURCE_COMPATIBILITY_REPORT.md)及 [PORTAL-B04](../reports/PORTAL_B04_SEARCH_ATTACHMENT_TRUST_RELATED_TRACE_INTEGRATION_VERIFICATION_REPORT.md)：current revision coverage、独立 trust、单一目标 TrustSummary、安全匿名投影。
- [DELETE-A01](DELETE_A01_SOFT_DELETE_DEPENDENCY_AND_RECOVERY_ARCHITECTURE_DECISION.md)、[REL-EVIDENCE-A01](REL_EVIDENCE_A01_RELATION_EVIDENCE_DEPENDENCY_AND_REMOVAL_DECISION.md)与 [STABILITY-R01](../reports/STABILITY_R01_CONCURRENCY_STALE_DETAIL_HISTORICAL_INTEGRITY_VERIFICATION_REPORT.md)、[R01-R01](../reports/STABILITY_R01_R01_RELATION_EVIDENCE_DEPENDENCY_IMPLEMENTATION_VERIFICATION_REPORT.md)、[R02](../reports/STABILITY_R02_SEARCH_BOUNDED_QUERY_DISCOVERY_DIAGNOSTICS_VERIFICATION_REPORT.md)：current mutation 边界、历史 Subject 保护和已完成的 #1–#9 修复。

本 A01 **仅 supersede C24 可原位纠正 HumanConfirmation（包括 ProviderSnapshot）的旧行为**；七类普通 Evidence 的 C24 不 supersede。它同时显式细化上述 REV/TRACE/Portal 的有效确认集合，并为 DELETE-A01/STABILITY-R01 #4 增加唯一的 HC 历史撤销操作例外。旧冻结文件及历史报告不重写。旧文件的 HC-A01/U04 阶段名称与本次 lifecycle A01 不应混同。

## 2. 核心不可变事实

HumanConfirmation 是 `EvidenceType.HumanConfirmation` 的 **append-only historical confirmation fact**，不新建确认实体或通用 Evidence lifecycle enum。

创建完成后，下列事实一律不可原位修改：

| 类别 | Immutable fields |
| --- | --- |
| 目标身份 | EvidenceType、SubjectType、SubjectId、SubjectDetailKey、KnowledgeDocumentRevisionNumberSnapshot |
| canonical 确认人 | ProviderUserId、ProviderKnowledgeRoleId |
| 历史身份 | ProviderEmployeeNo、ProviderName、ProviderRole、ProviderTeam、ProviderJobTitle，以及 legacy ProviderExternalKey/ProviderSource/ProviderNote |
| 时间与确认内容 | ProvidedAt/confirmedAt、confirmationMethod、confirmationStatement、supportReason、sourceNote |
| 同一事实的持久化表示 | SourceTitle、SourceReference、SourceLocatorJson、Summary、Confidence、CreatedAt、创建时 ReplacesHumanConfirmationId |

旧 locator、summary、reference 或 legacy method fallback 不能成为修改事实的旁路。仅一次撤销可写入撤销审计，并推进该 Evidence 的 UpdatedAt/Version；这不改写确认发生时的内容。新确认拥有独立 Id、Version、身份、时间、事实及 revision snapshot。

拒绝编辑时按 ProviderUserId 从当前 User 表重新 hydrate：当前姓名、工号、团队、职位、Role 名称/分配不能替代确认当时的身份。也禁止只改姓名而保留 canonical reference，或猜测历史 Role。历史快照永不随 User/Role 管理传播。

## 3. C23/C24/C25 唯一行为

- C23 保持拒绝 HumanConfirmation；普通 CodeReference、Sql、DatabaseSample、DatabaseComment、Api、MqMessage、ExistingDocument 继续原有创建及 C24 correction。
- 普通 C24 保持 Subject immutable、Provider Snapshot correction、actor/request 和 opaque concurrency 语义；deleted Subject 仍拒绝修改。
- `PUT /api/evidence/{id}` 对目标 HC（Active 或 Withdrawn）返回 **422 `invalid_state`**，文案：`人工确认属于历史确认记录，不能直接编辑；如需纠正，请先撤销原确认后重新记录。`
- 使用现有 ApiErrorResponse，fieldErrors 为 null，details 可为现有资源定位 `{ resourceType: "Evidence", resourceId: id }`，不增加错误框架。不忽略部分字段、不重新 hydrate、不改变 User/Role reference。
- 安全中间件/权限及 route/body 语法错误先处理；安全 ID 对应不存在记录为 404 `not_found`。加载到 HC 后，类型拒绝优先于通用 correction 字段校验、Subject current 校验和 token freshness；因此 stale HC C24 也不能成为可编辑入口。普通 Evidence 的校验顺序维持现状。
- C25 继续使用 `POST /api/evidence/human-confirmations` 和既有事实请求，增加唯一可选字段 `replacesHumanConfirmationId`（null 或安全正整数）。原 AddEvidenceResponse/201 和 `knowledgeStatusChanged: false` 保持；读详情提供 linkage。
- C25 的 Current User、0/1/多个 active KnowledgeRole、确认方式闭集、confirmedAt UTC 规范化、current Subject 和文档 revision 校验保持。confirmedAt 仍是请求提供的事实时间，不强制改成 server now；与撤销时 server UTC 严格区分。

## 4. Withdrawal API、审计与权限

冻结专用操作：`POST /api/evidence/human-confirmations/{id}/withdraw`。

请求仅含 `reason`、`concurrencyToken`。reason 必填，按 .NET Trim 规范化后长度 1–1000（按既有 .NET string.Length/UTF-16 长度计算）；不是 HTML/Markdown，按纯文本展示。空白、超长、无效 ID/token 返回 400 `validation_error`。不接受 client actor/name/UserId/withdrawnAt；这些额外写入字段应返回字段验证错误，绝不采纳。

沿用 Editor policy：Editor/Administrator 可撤销任意 HC，不要求是原确认人，不新增 ownership 或 KnowledgeRole 权限；Viewer 403。登录、会话、停用账号、Current User 不可用和 antiforgery 均复用现有安全链。审计操作者只能来自 authenticated principal-backed ICurrentUserContext，事务内重读 canonical active User，姓名快照取撤销时值。KnowledgeRole 不用于撤销授权，也不要求原确认人的账号/Role 当前仍启用。

事务内依次检查存在、HC 类型、token current、Active。不存在返回 404 `not_found`；普通 Evidence 调用返回 422 `invalid_state`；过期的有效 token 返回 409 `conflict`；已撤销且使用最新 token 返回 422 `invalid_state`。撤销不可重复、不可 undo，不能修改已写入的撤销原因。

成功以 server UTC 同时设置 WithdrawnAt、WithdrawnByUserId、WithdrawnByDisplayName、WithdrawalReason；Version +1，UpdatedAt 为该 server 时间。原 confirmation 字段、Subject Version/KnowledgeStatus、revision、关系和其他 Evidence 一律不变。

成功返回 200 的专用紧凑响应：`{ id, evidenceType: "HumanConfirmation", status: "Withdrawn", withdrawnAt, withdrawnByDisplayName, withdrawalReason, concurrencyToken, knowledgeStatusChanged: false }`。不返回 withdrawal actor 的 canonical ID。该响应不依赖 current Subject 详情，客户端显式刷新历史详情及当前 trust。失败整体回滚。

## 5. Deleted Subject 裁决

**允许撤销已 soft deleted Subject 的 HC**。该操作只终结一条历史确认的有效支持，不修改、恢复或重新绑定 Subject。只要求 HC 行存在、操作者有权限、token/state 合法；不调用 current Subject resolver 作为撤销前置门槛。即使历史上下文当前无法解析，也不应阻止对现存 HC 行记录撤销，成功响应独立于 Subject；这不构成 legacy orphan 自动修复授权。

这是本 A01 对 DELETE-A01 “所有 ordinary edits/current writes 都要求 active target”的狭窄显式 amendment。STABILITY-R01 #4 对 **普通 C24、C23、C25** 的拒绝仍完整保留。deleted Subject 的 HC 不可 C24；也不可 C25 重新确认/替代；只有以后经独立已批准流程恢复并满足既有 C25 条件时才可新增。本任务不设计恢复。

现有 Q16/list 的 historical resolver/tombstone、无 deleted-target 导航保持。撤销操作不添加 Subject snapshot，不创建 Relation tombstone。

## 6. Replacement 与无分叉历史链

纠正流程固定为 **WITHDRAW + NEW CONFIRMATION**。撤销与 C25 是两次显式请求，不自动串行提交、不自动复制旧确认人或事实；第二步失败时旧记录保持 Withdrawn，可稍后重试。

普通独立新确认 link 为 null。用户显式选择纠正某一旧确认时提交 replacesHumanConfirmationId；成功后新行指向旧行。事务内验证：

1. reference 存在且为 HC，已 Withdrawn；不存在/类型错误为 422 `reference_invalid`，Active 为 422 `invalid_state`。
2. 新旧 SubjectType、SubjectId、SubjectDetailKey 完全一致。detail key 使用既有 trim/空白转 null 的存储规范后做 ordinal、null-safe 比较，不模糊比较。
3. 不得 self、不形成 cycle。新自动生成 Id 只指向事务开始前已存在记录；link 一经创建不可修改，因而不会产生回边。数据库同时约束非 self。客户端不提交新 Id，也无 link-update API。
4. 旧行**终身最多一个直接 replacement**，不限其 replacement 是否仍 Active。已有任一 replacement，返回 409 `conflict`；唯一约束作为最终屏障。此条优先于较弱的“不得已有 active replacement”。
5. replacement 不要求同一 ProviderUserId/Role/时间；按本次 C25 当前身份重新物化，保留两人的独立事实。不得将新身份写回旧行。

允许 A Withdrawn → B replaces A；B Withdrawn 后由 C replaces B。禁止 B 撤销后再让 C replaces A。读取 inverse replacedByHumanConfirmationId 从唯一 reference 派生，不在旧行保存第二个指针、不为了关联推进旧行 Version。

错绑 Subject 的纠正：撤销旧 HC，针对正确 current Subject 创建 link=null 的独立确认；不伪造 same-subject replacement，不提供 rebind。

### KnowledgeDocument revision

C25 仍必须提交当前显示的 subjectRevisionNumber，事务内与 current head 不符则返回原 409 `conflict`。链接 replacement 还要求旧 snapshot 与这次新 snapshot **同为非 null 且相等**；否则 422 `reference_invalid`，不自动去掉 link，不确认旧 revision。

当 current revision 已推进，旧确认可 Withdraw；新 current-revision HC 只能作为独立确认（link=null）。legacy null snapshot 无法证明同一次 revision，亦不能链接到新已知 revision；保留 null，不按 confirmedAt 或当前 head 猜测。非文档 Subject 仍不得传 subjectRevisionNumber。并发内容保存必须通过原 SQLite authoritative transaction/revision expectation 保证最终验证。

## 7. Effective 与 historical 集合

以下表达式仅用于 current trust/support，不加 Evidence 全局 query filter：

```text
ActiveHC(e) = e.EvidenceType == HumanConfirmation && e.WithdrawnAt == null
EffectiveEvidence(e) = e.EvidenceType != HumanConfirmation || e.WithdrawnAt == null
HistoricalEvidence(e) = 所有原 Evidence 行（包括 Withdrawn HC）
```

同一已有 Subject/type/detail-key 范围内，HumanConfirmationCount = ActiveHC 数；current trust EvidenceCount = 普通 Evidence + ActiveHC。普通 Evidence 原有 locator/支持有效性规则继续叠加；Active 不代表跨 Subject 或满足一切条件。

KnowledgeDocument coverage 仅对 ActiveHC 计算：零条为 NoConfirmation；存在但 snapshot 全 null 为 LegacyConfirmationUnknown；非 null MAX 等于 current revision 为 CurrentRevisionConfirmed；低于 current 为 ChangedSinceConfirmation。高于 current 为既有数据完整性错误，不悄悄归类。最后一个当前 revision HC 撤销后可能回退到较旧的 active snapshot 或 NoConfirmation；不修改 Subject KnowledgeStatus。

TRACE-A01 的结构覆盖（是否存在 eligible requirement/spec/test edge）不依赖 HC，不随撤销变化；只调整 node/edge trust counts 与 revision-confirmation coverage。Portal TrustSummary 仍是一目标、不跨目标聚合；Related/Trace 按每一 node/relation 单独使用 effective 集合。

显式 C22/C26 以及调查 Resolution/KnowledgeUpdate 的后续支持验证也必须排除 Withdrawn，避免已撤销记录仍满足 Inferred/Confirmed 或 Apply 的依据门槛。原 transition policy、revision 与 KnowledgeStatus 独立性不变；**撤销本身不调用状态推进/回退、不重算或变更既存 KnowledgeStatus、不回滚已完成 Resolution/KnowledgeUpdate**。既存 Confirmed 缺少支持时只使用既有 trust gap 呈现，不设计自动降级。

## 8. Historical read 与 UI/privacy

认证管理端 Q16 和按 Subject 的 Evidence list 保留全部历史记录，不默认隐藏 Withdrawn，也不以 replacement 替换旧行展示。原 Provider snapshot、原确认事实和旧 revision snapshot 保持原值。

在 detail/list 增加 nullable `humanConfirmationLifecycle`：普通 Evidence 为 null；HC 对象包含 `status: Active|Withdrawn`、nullable withdrawnAt/withdrawnByDisplayName/withdrawalReason、replacesHumanConfirmationId、replacedByHumanConfirmationId。Active 的三个撤销显示字段为 null；legacy HC 也返回 Active。沿用原 concurrencyToken；canonical withdrawal actor ID 仅持久化，不增加到公开 DTO。inverse link 最多一条，用 indexed bounded lookup/batch lookup，禁止全链递归展开或 N+1。

历史 Evidence drawer 仍受 Viewer read 权限；“Admin historical UI”表示认证管理端，而非改为 Administrator-only。Viewer 可读该历史状态与审计纯文本，但无写按钮。当前事实页面的 trust/count 使用 effective 集合；普通详情依据摘要若承担 historical Evidence 列表职责则保留 Withdrawn 并标记状态，不把数组 length 当有效 count。提供既有历史入口，不能把当前支持过滤误用为隐藏历史。

HC Detail：Active 不显示普通编辑；有 Editor/Admin 权限时显示“撤销确认”。填写必填原因并明确提交后显示“已撤销”、时间、撤销人、原因；Withdrawn 不再提供撤销，满足 current Subject/revision/unique replacement 条件时可“重新确认”，进入全新 C25。已推进 revision 时明确创建独立当前确认，不静默生成 link；deleted Subject 无重新确认入口。普通 Evidence 保持编辑。成功后刷新当前 trust/coverage 和旧/新详情；失败保留输入，stale 重新加载，不自动重试变更。

匿名 Portal v1 绝不展示 withdrawal ID/姓名/原因/历史审计、原 Provider 身份或 Evidence Detail。仅输出 active effective counts 和既有 coverage；不输出 withdrawnCount 或历史链。Admin Portal Preview 同样复用 safe Portal DTO，不借 preview 泄漏审计。withdrawal reason 不写入普通日志或传给 Portal；按现有安全策略和纯文本编码处理。

## 9. 最小 additive schema（仅设计，不生成 migration）

保留一张 evidence 表，新增五个 nullable 列：

| Domain / column | 类型与语义 |
| --- | --- |
| WithdrawnAt / withdrawn_at | DateTimeOffset nullable；server UTC；null=Active（仅 HC），非 null=Withdrawn |
| WithdrawnByUserId / withdrawn_by_user_id | long nullable；FK users.id，RESTRICT |
| WithdrawnByDisplayName / withdrawn_by_display_name | string nullable；撤销时 canonical User.DisplayName 历史快照，沿用 User 名称规范 |
| WithdrawalReason / withdrawal_reason | string nullable；trim 后 1–1000，应用与持久化测试覆盖边界 |
| ReplacesHumanConfirmationId / replaces_evidence_id | long nullable；FK evidence.id，RESTRICT |

数据库约束：四个 withdrawal 列全部 null 或全部非 null；非空原因/显示名不得空白；非 HC 的全部五列必须 null；replaces_evidence_id 非 null 时不得等于 id。普通 Evidence 没有 Active 生命周期。建立 withdrawn_by_user_id FK index，以及 `UNIQUE(replaces_evidence_id) WHERE replaces_evidence_id IS NOT NULL`（包括 withdrawn replacement）；不额外建立通用 status index、反向指针或通用审计表。

cross-row HC 类型/Withdrawn/same Subject/same revision/cycle 由 authoritative application transaction 验证；普通 CHECK 不能假装验证跨行关系。SQLite unique/FK/check 作为最后屏障，精确映射已知 replacement uniqueness conflict，不把所有 DbUpdateException 伪装为并发。

HC-B01 migration 为 **ADDITIVE**。既有 HC 四个 withdrawal 列及 link 全 null，解释为 Active；普通 Evidence 同样 null。不 backfill snapshot/role/revision/link，不根据不一致姓名猜测坏数据、不自动撤销。保留全部旧列、事实、Id、Version、索引、CHECK 和 RESTRICT FK；SQLite 为增加约束可能需要物理 table rebuild，但语义必须是无损 additive，须在 task-owned DB 验证。不可把可回滚 Down 当成丢弃新撤销审计的无风险操作，生产回退另行评估。

## 10. 并发与事务

沿用 app-managed Version + opaque token；withdraw 使用 `SqliteImmediateTransaction`，获取写边界后重读 Evidence/type/state/version 与 active actor，原子写审计/Version。两个使用相同旧 token 的 withdraw：仅一个 200，另一个 409，无重复审计。

replacement C25 复用同一 SQLite immediate write 边界，在同一事务内验证旧 Withdrawn/没有任何 replacement、current Subject/revision、新身份，并插入新 HC。并发两个 replacement 仅一个成功、另一个 409，不产生分叉。withdraw 未提交时 replacement 不可看到已撤销状态；replacement 先获得边界则因旧 Active 拒绝，withdraw 先提交则允许有效创建。

HC C24 永远拒绝，不能与 withdraw 覆盖。Relation remove 对所有 Evidence 行检查依赖，与 C25 使用相同写串行边界；**Withdrawn Relation HC 仍阻止 remove**。同理 ColumnKnownValue 的历史 Evidence/detail-key 删除引用检查不使用 effective filter。不得因 current trust count 为零判断可删。

withdraw 与 Subject soft delete 可按任意顺序成功，因为撤销不写 Subject；C25 与 delete 仍只能产生合法 current create 或引用拒绝。取消、失效权限/账号、stale token、约束错误不得留下部分审计/新行。不得通过应用内 mutex 替代数据库边界。

## 11. 实際消费点与 HC-B01 影响清单

下表是基线源码审查所得，不表示本次已改代码；路径前缀为 `src/SystemKnowledgeHub.Api/Features/`。

| 当前文件 / 消费点 | 后续要求 |
| --- | --- |
| Evidence/Application/EvidenceService.cs、Api/EvidenceController.cs、Domain/Evidence.cs、Persistence/EvidenceConfiguration.cs | C24 类型拒绝；专用撤销；C25 link；五列/约束；ordinary correction 保持 |
| Evidence/Application/EvidenceQueries.cs、Models/EvidenceModels.cs；Api/Contracts/EvidenceContracts.cs | Q16/list 保留历史，增加 HC 专用状态与 one-hop links、动作；禁止 global filter |
| KnowledgeDocuments/Application/KnowledgeDocumentQueries.cs ToDetail | ActiveHC COUNT/MAX revision 四态 |
| Traceability/Application/TraceabilityQueries.cs LoadTrustContext / ToDocument / relation trust | effective count 与 ActiveHC revision；不改结构覆盖 |
| Portal/Application/PortalB04ProjectionService.cs LoadEvidenceAsync / LoadTrustAsync | TrustSummary、Related、Trace 及 Admin Preview 一致过滤；safe DTO 不加审计 |
| Systems/Application/SystemKnowledgeViewQueries.cs | overview count 与当前支持 section 用 effective；历史通过 Evidence list 可达 |
| BusinessFunctions/Application/BusinessFunctionQueries.cs | 本体依据历史摘要加标记；relation/related rule EvidenceCount 用 effective |
| DatabaseKnowledge/Application/DatabaseKnowledgeQueries.cs | Column count/current支持用 effective；历史 Column evidence 摘要保留状态 |
| BusinessRules/Application/BusinessRuleQueries.cs、Integrations/Application/IntegrationQueries.cs、Relationships/Application/RelationshipQueries.cs | 内嵌历史 Evidence 摘要保留并标记 Withdrawn，任何 trust 数量不得使用历史数组 length |
| UnknownItems/Application/UnknownItemQueries.cs | 调查跨 UnknownItem/Finding/Resolution 的 count/context支持用 effective；历史 investigation evidence 保留并标记；拆开当前 evidence.Length 复用 |
| KnowledgeStatus/Application/KnowledgeStatusService.cs、Relationships/Application/RelationshipService.cs 的 status 更新 | 后续显式 transition hasEvidence/hasHuman 只认可有效支持；不自动改状态 |
| UnknownItems/Application/KnowledgeResolutionService.cs | HasSupportingEvidence 和 Apply 显式状态校验使用 effective；不回滚既存历史流程 |
| Relationships/Application/RelationshipService.cs Delete | 继续 ANY 所有 historical Evidence，绝不排除 Withdrawn |
| DatabaseKnowledge/Application/DatabaseKnowledgeService.cs RemoveColumnKnownValue | 同上，保留 detail-key 历史引用 blocker |

前端实际消费：EvidenceDetailDrawer / EvidenceDrawerContent / AddHumanConfirmationDrawer 与 typed evidence API/contracts；KnowledgeDocumentDetailView、TraceabilitySection、TraceDocumentNode、PortalSectionRenderer；SystemUnifiedKnowledgeView、BusinessFunctionDetailView、DatabaseObjectDetailView、ColumnDetailDrawer、UnknownItemsListView/UnknownItemContextRail、KnowledgeStatusDialogContent/KnowledgeStatusProgressionPanel。必须核对 counts、support summaries、按钮与 mutation 后刷新，不从列表长度再推导 effective 数量。没有新增独立 trust engine、统计持久化或通用生命周期框架。

## 12. Compatibility matrix：十五项唯一答案

| 问题 | 冻结答案 |
| --- | --- |
| 1. C24 对 HC？ | Active/Withdrawn 均 422 invalid_state，不原位修改 |
| 2. 哪些字段 immutable？ | 第 2 节全部目标、身份、时间、确认事实及其存储表示；仅一次撤销审计和 Version/UpdatedAt 可变 |
| 3. 如何撤销？ | POST 专用 withdraw，reason + opaque token，server audit |
| 4. 谁可撤销？ | authenticated Editor/Administrator，可撤销他人确认；Viewer 只读 |
| 5. deleted Subject？ | 可撤销；不得普通修改或新增/替代；显式狭窄 amendment |
| 6. 如何重新确认？ | 全新 C25、当前身份/Role、自己的事实/时间/revision/Version |
| 7. replacement link？ | 可选；表达同一目标纠正时显式提交，不自动推断；不可更新 |
| 8. 文档 revision 不同？ | 不许链接；撤销旧记录后新 current 确认为独立 link=null；legacy null 同样不能猜 |
| 9. Withdrawn 计 HC count？ | 不计 |
| 10. Withdrawn 计 current EvidenceCount？ | 不计；历史 list 保留 |
| 11. Withdrawn 满足 confirmation coverage？ | 不满足，四态算法仅 ActiveHC |
| 12. Withdrawn Relation HC 阻止删除？ | 是，历史依赖与有效支持分离 |
| 13. Portal 显示 withdrawal history？ | 否，含 Admin Portal Preview 的匿名同构 DTO |
| 14. existing rows？ | 保持全部原值，新列 null，HC 默认 Active，无推断修复 |
| 15. migration additive？ | 是；HC-B01 实施，本次无 migration |

U04 identity snapshot 继续成立；普通 C23/C24 保持；C25 仅 additive link；Q16 增加历史状态而非隐藏；REV 四态/TRACE 结构/Portal 单目标仍不冲突。DELETE mutation 例外已明确裁决；REL-EVIDENCE blocker 不收窄；KnowledgeStatus 仍是独立 explicit fact。不存在留给实现猜测的冲突。

## 13. 明确拒绝与 HC-B01 验收

拒绝 HC 原位编辑、客户端 snapshot replacement、identity 分离修改、动态刷新 User/Role 历史快照、删除旧 HC、cascade、覆盖或隐藏旧记录、自动 KnowledgeStatus downgrade、generic Evidence revision/event sourcing、approval workflow。无独立 HumanConfirmation 表、通用 lifecycle enum、Subject snapshot 或 Relation tombstone。

后续实施必须验证：普通七类 C24 回归；Active/Withdrawn HC C24 拒绝和所有事实不变；Editor/Admin/Viewer/antiforgery/Current User；reason 边界和客户端审计伪造；deleted Subject 撤销及禁止 C23/C25；重复/stale 撤销与真实双连接并发；same-subject/detail/revision/link validation；两个 replacement 竞争、A→B→C 和无分叉；legacy null revision；doc-save vs C25；计数/coverage/Trace/Portal/显式 status 与调查 Apply 一致；历史读和匿名审计不泄漏；Withdrawn Relation HC 和 KnownValue 历史依赖；additive migration 全列/版本/索引/FK 保留；失败原子性及 task-owned DBSAFE。以上是 HC-B01 gates，本次未运行。

**ORIGINAL STABILITY FINDING #10: CLOSED — DESIGN**。实现缺口待 HC-B01，不得宣称运行时已修复。

**HC-A01 APPROVED / HC-B01 READY: YES**。完成本次文档后停止。
