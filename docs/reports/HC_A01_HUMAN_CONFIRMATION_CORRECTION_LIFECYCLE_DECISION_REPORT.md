# HC-A01 — HumanConfirmation Correction Lifecycle Decision Report

日期：2026-09-07。**HC-A01 PASS**，仅 architecture / contract decision。

## Baseline 与任务边界

- `main`：`30c745b382ac1d5cd1f50fff1f8a95407b7ae916`，`fix: bound search and improve discovery diagnostics`。
- 已执行 `git status --short`、`git diff`、`git log -6 --oneline`；原有 DBDISC_FINAL_R01 未跟踪报告与 DOCUMENT_INDEX 对应一行修改保留，不纳入提交。
- 产物：[HC-A01 冻结决策](../design/HC_A01_HUMAN_CONFIRMATION_IMMUTABILITY_WITHDRAWAL_REPLACEMENT_DECISION.md)。DOCUMENT_INDEX、PROJECT_FILE_MAP 同步。
- 不修改 src/tests/configuration，不生成/执行 migration，不启动应用，不进行数据库查询/复制/修复。不改写 U04、旧 HumanConfirmation amendment、Stability、Portal/Trace 历史报告或 MVP frozen specifications。

## Authority review — PASS

审查 AGENTS.md、DOCUMENT_INDEX；旧 HUMAN_CONFIRMATION_API_AMENDMENT_REVIEW、U04 snapshot 报告；MVP Evidence C23/C24/C25/Q16；USER_PERSON_FOUNDATION_DESIGN 和后续 AUTH-A01；REV-A01、TRACE-A01、PORTAL-A01/AMEND-01/B04；DELETE-A01、REL-EVIDENCE-A01；STABILITY-R01/R01-R01/R02。

已核对当前 Evidence Domain/Configuration/Service/Controller/Queries/Contracts、CurrentUserContext、AddHumanConfirmation 与 EvidenceDetailDrawer；并检索现有 EvidenceCount/HumanConfirmationCount、coverage、trust/support、historical reference 消费点。

关键发现：

1. C24 在 immediate transaction 内检查 current Subject 和 Version 后，会覆盖来源、结论、支持理由及 ApplyPerson 快照；canonical ProviderUserId/KnowledgeRoleId 保留，因此确有显示快照与 canonical reference 分离风险。它还会覆盖 confirmedAt/locator，但不提供完整纠正链。
2. C25 已在事务内生成 canonical identity snapshot，文档确认已绑定 current revision expectation；这些创建约束应保留。
3. U04 的历史 Header/用户选择器描述已被后续认证设计取代。当前 CurrentUserContext 使用登录会话及 canonical User，KnowledgeRole 仍不是 Permission；新 withdrawal 必须沿用当前安全边界。
4. DELETE-A01/STABILITY-R01 #4 拒绝 deleted Subject 的普通 Evidence mutation；本次需明确批准“历史 HC withdrawal”例外，不能让 HC-B01 猜测是否复用 current resolver。
5. Effective filtering 不仅涉及 Portal/Trace：显式 KnowledgeStatus 推进、调查 Apply/HasSupportingEvidence 也读取 Evidence。相反 Relation remove / ColumnKnownValue remove 属于历史引用保护，必须保留所有行。

## Contract matrix review — PASS

设计第 12 节对全部十五项问题给出唯一答案。冻结结果：HC 事实 append-only，Active/Withdrawn 都退出 C24，返回现有 422 invalid_state；普通七类 Evidence C24 保持。纠正为撤销后全新 C25，绝不编辑/rehydrate 旧身份。

withdraw 使用专用 POST、reason（trim 后 1–1000）+ opaque token；Editor/Administrator 可操作，Viewer 只读；审计身份与时间来自 server Current User/UTC。允许 deleted Subject 的历史 HC 撤销，但仍禁止 C23/C25 及普通 C24。无需 Current User 是原确认人，也不要求原 Role 仍启用。

replacement link 为可选、显式、创建后不可修改。对旧 HC 的同 SubjectType/Id/detail key、Withdrawn、唯一直接 replacement 做事务验证；一条旧记录终身最多一个 replacement，包括 replacement 自己已撤销时，避免分叉。不同文档 revision 或 legacy null snapshot 不得链接；当前 revision 的新确认应作为独立记录。新确认可由不同当前用户记录，但绝不改旧行。

并发顺序和错误冻结：HC C24 类型拒绝优先；withdraw stale 409，最新 token 重复撤销 422；两个 withdraw 仅一个成功；两个 replacement 由 immediate transaction + filtered unique index 保证无分叉。旧 Active 时 replacement 422；reference/subject/revision 不兼容 422；已被替代为 409。

## Current projection impact review — PASS

设计第 11 节列出基线实际消费文件和应做的区分：

- KnowledgeDocument detail、Trace node/edge、Portal TrustSummary/Related/Trace/Admin Preview：ActiveHC count 与 revision MAX，effective Evidence count。
- System unified view、BusinessFunction/related rules/relations、DatabaseKnowledge columns、UnknownItem summary/context：current support count 排除 Withdrawn。
- BusinessRule/Integration/Relationship/Column/Investigation 等内嵌历史依据摘要以及 Q16/list：保留原行，附加 HC 专用状态，不用历史数组长度推断有效支持。
- KnowledgeStatusService、RelationshipService 状态推进、KnowledgeResolutionService 的显式支持检查：排除 Withdrawn，保持原状态政策。
- RelationshipService.Delete 与 DatabaseKnowledgeService.RemoveColumnKnownValue：保持 ANY historical Evidence，Withdrawn 仍为依赖；不使用 global query filter。

四态 coverage 仅重新限定输入集合，不改 REV-A01 revision identity；Trace 结构覆盖不变；Portal 仍是单目标 TrustSummary，没有跨目标聚合。撤销本身永不自动修改 Subject KnowledgeStatus，也不改既存 workflow history。

## Migration impact analysis — PASS

未来 HC-B01 为 **ADDITIVE**：五个 nullable evidence 列（withdrawn_at、withdrawn_by_user_id、withdrawn_by_display_name、withdrawal_reason、replaces_evidence_id）；两项 RESTRICT FK、withdrawal all-or-none、非 HC 字段全 null、non-self CHECK、replacement filtered unique index，withdrawal actor FK index。

没有普通 Evidence lifecycle enum、独立确认表、generic revisions/events 或额外反向指针。跨行类型/Subject/revision/state 由 authoritative transaction 验证，不能假装由本行 CHECK 完成。SQLite 可能 rebuild table 以安装约束；未来迁移须证明所有原字段、Id/Version、索引、CHECK/FK 保留。

所有旧 HC 默认 Active；旧 snapshot/reference/role/revision 不推断修复。legacy null revision 无法证明对应 current revision，禁止伪造 replacement linkage。**本次未生成、执行或验证 migration**。

## Security / privacy review — PASS

- 使用现有 authenticated Current User、Editor policy、antiforgery 和 opaque token；没有新 RBAC、client actor authority 或旧 User selector 回退。
- withdrawal canonical actor ID 仅内部持久化；认证历史详情允许现有 Viewer 读取撤销时间、显示名、原因，不把“管理端”误解成新增 Administrator-only read 权限。
- Portal 与同构 Admin Preview 不返回 withdrawal audit/姓名/原因/ID、Provider identity 或历史链，只返回 allowlisted effective trust。
- 旧事实保持；不重新读取当前 Profile 替代 snapshot，不删除、不 cascade、不 rebind、不自动 KnowledgeStatus downgrade。

## Validation、DBSAFE 与 cleanup

实际执行：authority review、十五项 contract matrix review、所有当前 projection/support/reference 消费点审查、迁移影响分析、安全/隐私审查、文档链接及 scope 检查、`git diff --check` / staged diff check，结果 PASS。

本任务为文档任务：build、backend/frontend tests、EF model/migration execution、runtime/browser/PORTAL-VERIFY 均 NOT APPLICABLE，未运行。没有读取 repository-owned SQLite 内容，没有 SQLite/EF open，也没有启动进程、端口、keys/attachments/logs runtime。DBSAFE：PASS（无数据库操作）；cleanup：PASS（仅 task-owned index 暂存辅助文件，提交前移除）。原有用户文件/进程未改动。

## Final status

| Required item | Result |
| --- | --- |
| HC-A01 | PASS |
| HUMAN CONFIRMATION APPEND-ONLY | FROZEN |
| GENERIC C24 HUMAN CONFIRMATION UPDATE | REJECTED |
| IDENTITY SNAPSHOT IMMUTABLE | FROZEN |
| CONFIRMATION FACTS IMMUTABLE | FROZEN |
| WITHDRAWAL | FROZEN |
| WITHDRAWAL AUDIT | FROZEN |
| REPLACEMENT LINK | FROZEN |
| CURRENT ACTIVE CONFIRMATION COUNT | FROZEN |
| REVISION COVERAGE | FROZEN |
| PORTAL TRUST | FROZEN |
| RELATION DEPENDENCY | FROZEN |
| HISTORICAL READ | FROZEN |
| EXISTING ROWS | FROZEN |
| MIGRATION | ADDITIVE（后续 HC-B01；本次未创建） |

**ORIGINAL STABILITY FINDING #10: CLOSED — DESIGN**。

**HC-A01 APPROVED / HC-B01 READY: YES**。

没有未裁决的架构问题；当前产品仍存在待 HC-B01 实施的 #10 enforcement gap，不能把 design closure 当作 runtime fix。#1–#9 的既有闭环保持；REV-GAP-012 仍 OPEN / DEFERRED，不纳入此文档任务。

交付采用单一文档 commit `docs: freeze human confirmation correction lifecycle`，push main 后在任务最终回复报告完整 SHA 与实际推送结果。未自动开始 HC-B01、PORTAL-VERIFY 或 Analysis Workspace。
