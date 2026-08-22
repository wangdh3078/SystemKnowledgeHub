# Post-MVP UX Stabilization Report

**Date:** 2026-08-17  
**Scope:** MVP FINAL PASS 后的定点 UX 稳定化；未新增业务 Feature，未修改冻结规格。

## Result

`UX STABILIZATION PASS`

## Issue Checklist

| # | Item | Status | Result |
| --- | --- | --- | --- |
| 1 | Global Search shortcut | **Fixed** | 顶栏与搜索 Overlay 统一显示并监听 `Ctrl + K`。 |
| 2 | Dashboard percentage precision | **Fixed** | Knowledge Progress 仅显示两位小数。 |
| 3 | Recent Activity layout | **Fixed** | 时间列具有最小宽度；类型 Badge 不再挤压时间，长名称省略。 |
| 4 | Technology tags | **Fixed** | System Detail 使用可搜索、可创建、多选的标签选择器；候选值来自现有 System 技术标签集合，不引入主数据。 |
| 5 | Confirmation method | **Fixed** | Human Confirmation 使用固定 code + 中文标签下拉：系统内、现场、会议、邮件、文档、其他。 |
| 6 | Local confirmation time | **Fixed** | Human Confirmation 使用本地日期时间选择器；提交边界转换为既有 UTC Contract，详情以本地可读时间显示。 |
| 7 | Required markers | **Fixed** | 主要创建/编辑表单统一使用 Element Plus `required` 状态，显示红色 `*`。 |
| 8 | Error placement | **Fixed** | 必填字段经 FormItem 就近展示；API/并发等业务错误置于表单顶部 Alert 或主要操作区，不再只堆在底部。 |
| 9 | Column Drawer action overlap | **Fixed** | Column Drawer footer 改为自然文档流，不再绝对定位覆盖正文。 |
| 10 | KnowledgeStatus guidance | **Fixed** | 统一 Progression Panel 解释未知/推断缺少的条件；门槛满足才显示推进操作，推断状态提供“添加人工确认”。 |
| 11 | Unknown Item confirmation dialogs | **Fixed** | 补齐 Element Plus MessageBox 样式注册；共享确认 Dialog 使用同一 canonical mechanism。浏览器实际检查到居中 Mask、内容与 Footer。 |
| 12 | Global Create duplicate | **Fixed** | `AppShell` 成为唯一全局 chooser 挂载点；各 Feature Flow 只承接对应具体表单。 |
| 13 | Business Function create entry | **Fixed** | 全局 Create 可进入 Business Function；无 System Context 时先显式选择 System；业务功能列表提供同一 canonical C05 入口。 |

## Key Implementation Notes

- `KnowledgeStatus` 仍只由显式状态操作改变：创建、保存 Evidence 或保存 Human Confirmation 均不会自动推进。
- Column Drawer 复用 KnowledgeStatus Feature；没有复制第二套字段状态机。
- Global Create 中 Evidence 与待确认事项仍要求明确 Subject / Target，上述行为保持不变。
- 未引入 User、Person、Technology Master Data、Confirmation Method Entity、权限或新的 Overlay/Form Framework。

## Verification

| Check | Result |
| --- | --- |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | **PASS** — 0 warnings, 0 errors. |
| `npm run type-check` | **PASS**. |
| `npm run build` | **PASS**. Vite 仅报告既有的 bundle-size advisory。 |
| New automated tests | **0** — 本次是 UI/交互稳定化，现有构建与 focused runtime 覆盖已足够。 |
| Specification deviation | **None.** |

### Focused browser regression

- Dashboard：`Ctrl + K` 打开搜索；Knowledge Progress 显示 `32.00%`、`40.00%`、`28.00%`；最近整理时间完整可见。
- Global Create：一次只渲染一份“新增知识对象”；进入业务功能创建后，未预填 System，必填项显示 `*`，空提交在字段旁显示错误。
- Database Column Drawer：`STATE_FLAG` 显示独立 Action footer、Known Values/元数据 Section 与推断状态引导；可从 Panel 进入人工确认。
- Human Confirmation：确认方式为下拉、本地时间为日期时间控件、必填标记与 inline 错误正常；未保存任何测试 Evidence。
- System Detail：技术标签编辑状态展示已有的可移除标签与可创建多选输入；未保存测试数据。
- Unknown Item：共享 MessageBox 对话框已目视确认居中，Mask、输入区域和 Footer 均位于 Dialog 内；未提交重新打开操作。
- 浏览器控制台未发现本地应用 error。

## Model / Schema Impact

- Domain Model：**No change**
- Frozen API Contract：**No change**
- SQLite schema：**No change**
- Migration：**No new migration**

## Process Cleanup

- API verification process：stopped before completion.
- Vite verification process：stopped before completion.
- Browser verification tab：closed before completion.
- Watch/test process：none left running.
- Ports `5090` and `5173`：released after cleanup.
- Temporary runtime logs：removed after verification.

## Deferred — explicitly out of scope

- User / Person Foundation
- Current User
- Department / Team
- Job Title
- Knowledge Role
- HumanConfirmation User Snapshot
- Admin User Management
- Auth / Permission

No next Vertical Slice or new product capability was started.
