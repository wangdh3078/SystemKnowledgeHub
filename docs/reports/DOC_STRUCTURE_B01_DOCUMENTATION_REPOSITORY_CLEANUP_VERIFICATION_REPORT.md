# DOC-STRUCTURE-B01 — Documentation Repository Cleanup Verification Report

## Result

DOC-STRUCTURE-B01 PASS

## Scope

本批次仅整理 Repository Documentation Structure：移动根目录中的设计评审、架构规划和 Verification Report，更新文档地图与 README 的目录导航，并验证 Markdown 引用。未修改任何业务文档正文语义。

## Pre-existing Dirty Worktree

任务开始前已执行 `git status`、`git diff --stat` 与 `git diff`。工作树没有 tracked modified 或 untracked 文件；Git 仅报告用户全局 ignore 文件的读取权限 warning。DOC-STRUCTURE-B01 没有覆盖任何 pre-existing change。

## Moved Files

| Old Path | New Path |
| --- | --- |
| `AUTH_A01_LOCAL_LOGIN_OIDC_COEXISTENCE_DESIGN_REVIEW.md` | `docs/design/AUTH_A01_LOCAL_LOGIN_OIDC_COEXISTENCE_DESIGN_REVIEW.md` |
| `KNOWLEDGE_CONTENT_DOCUMENT_ARCHITECTURE_PLAN.md` | `docs/design/KNOWLEDGE_CONTENT_DOCUMENT_ARCHITECTURE_PLAN.md` |
| `AUTH_B01_LOCAL_CREDENTIAL_FOUNDATION_VERIFICATION_REPORT.md` | `docs/reports/AUTH_B01_LOCAL_CREDENTIAL_FOUNDATION_VERIFICATION_REPORT.md` |
| `KC_B01_KNOWLEDGE_DOCUMENT_FOUNDATION_VERIFICATION_REPORT.md` | `docs/reports/KC_B01_KNOWLEDGE_DOCUMENT_FOUNDATION_VERIFICATION_REPORT.md` |
| `SEC_01_OIDC_AUTHENTICATION_FOUNDATION_VERIFICATION_REPORT.md` | `docs/reports/SEC_01_OIDC_AUTHENTICATION_FOUNDATION_VERIFICATION_REPORT.md` |
| `SEC_02_BACKEND_ACCESS_CONTROL_VERIFICATION_REPORT.md` | `docs/reports/SEC_02_BACKEND_ACCESS_CONTROL_VERIFICATION_REPORT.md` |
| `SEC_03_FRONTEND_LOGIN_ACCESS_UX_VERIFICATION_REPORT.md` | `docs/reports/SEC_03_FRONTEND_LOGIN_ACCESS_UX_VERIFICATION_REPORT.md` |
| `SEC_04_SECURITY_ROLLOUT_VERIFICATION_REPORT.md` | `docs/reports/SEC_04_SECURITY_ROLLOUT_VERIFICATION_REPORT.md` |
| `UI_B02_LAYOUT_DIALOG_VERIFICATION_REPORT.md` | `docs/reports/UI_B02_LAYOUT_DIALOG_VERIFICATION_REPORT.md` |
| `XML_DOC_B01_SHARED_CONTRACTS_VERIFICATION_REPORT.md` | `docs/reports/XML_DOC_B01_SHARED_CONTRACTS_VERIFICATION_REPORT.md` |
| `XML_DOC_B02_USERS_VERIFICATION_REPORT.md` | `docs/reports/XML_DOC_B02_USERS_VERIFICATION_REPORT.md` |
| `XML_DOC_B03_CURRENT_USER_VERIFICATION_REPORT.md` | `docs/reports/XML_DOC_B03_CURRENT_USER_VERIFICATION_REPORT.md` |
| `XML_DOC_B04_EVIDENCE_HUMAN_CONFIRMATION_VERIFICATION_REPORT.md` | `docs/reports/XML_DOC_B04_EVIDENCE_HUMAN_CONFIRMATION_VERIFICATION_REPORT.md` |

所有 tracked 文件均通过 `git mv` 移动，未重命名，根目录未保留旧副本。

## Root State

整理后根目录保留的 Markdown entry files 为：

- `AGENTS.md`：Coding Agent 的 repository rules。
- `README.md`：项目入口和开发概览。
- `design-qa.md`：根目录直接可发现的现有视觉 QA entry artifact。

根目录不再包含本批次归档的 `*_VERIFICATION_REPORT.md`、`*_DESIGN_REVIEW.md` 或 `*_ARCHITECTURE_PLAN.md` 文件。

## References Updated

- `docs/PROJECT_FILE_MAP.md`：增加文档归档约定、未来 Codex report 路径，并以实际 `docs/design/`、`docs/reports/` 路径登记本批次移动文件。
- `README.md`：更新 `docs/` 目录树，显示 `design/`、`standards/` 与 `PROJECT_FILE_MAP.md`。
- `AGENTS.md`：已检查；其中没有指向本批次移动文件的旧根目录路径，因此不需要修改。

## Link Verification

对 repository Markdown 执行相对 `.md` link 扫描，结果为 `0 broken links`。对所有 13 个移动文件名执行全仓 Markdown 引用检查后，除本报告的历史 `Old Path` 表外，未发现旧根目录路径引用；没有 unresolved link。DOC-STRUCTURE-B01 变更没有写入绝对本地 Windows 路径；全仓扫描发现一条既有的冻结规格历史示例（`docs/specifications/System_Knowledge_Hub_MVP_Final_UI_Inventory.md`），本批次未修改该文件。

## Build

执行：

```text
dotnet build SystemKnowledgeHub.sln --no-restore
```

结果：成功，`0` warnings，`0` errors。

## Scope Verification

No production code, frontend code, API behavior, database schema, migration, Authentication behavior, Authorization behavior, KnowledgeDocument behavior, or business logic was changed.

本批次 diff 仅包含 Markdown 移动、Markdown 路径/导航更新、`PROJECT_FILE_MAP.md` 更新和本 Verification Report。未来 Verification Report 必须直接生成于 `docs/reports/`；例如 AUTH-B02 应使用 `docs/reports/AUTH_B02_LOGIN_UI_AUTH_OPTIONS_VERIFICATION_REPORT.md`。
