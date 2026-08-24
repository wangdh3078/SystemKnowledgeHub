# UI-KC-B05-R04 — Markdown Source Toolbar / Code Block / Table / Read Theme 验证报告

**Result: UI-KC-B05-R04 PASS**  
**验证日期：2026-08-24**  
**Worktree baseline：** `ddc42fb ✨ feat: 切换知识文档原始 Markdown 编辑器`

## Human Acceptance Findings

R04 的五项人工反馈均已落实：代码围栏不再以纯黑块显示；UL/OL/任务列表有可区分的语义图标；表格使用完整正文宽度；源码/预览切换不再显示文字；工具栏收紧为图标优先的紧凑布局。

## Raw Source Editor 与保存边界

- 编辑态继续是 CodeMirror 原始 Markdown 源码，唯一作者状态为 `bodyMarkdown: string`。
- Preview/Current Read/History/Restore Preview 使用共享安全渲染器；Compare 保持转义的原始 Markdown diff。
- 创建弹窗继续使用源码编辑器和可见模板；编辑器没有 Save，页面级 Save 与 `Ctrl/Cmd+S` 仍是唯一持久化路径。
- 未恢复颜色作者控件、MdEditorV3、图片上传或链接标题抓取端点。

## Final Toolbar Layout / Compact Metrics

工具栏顺序保持块类型、行内、引用/列表、插入、撤销/重做、视图分组。CSS 固定 29px 图标按钮、4px 工具栏纵向内边距、2px 组内间距、5px 组内横向内边距、细分隔线和 6px 圆角，对应 37px 可视工具栏高度。

| 控件 | 图标语义 / 状态 |
| --- | --- |
| UL | 三个圆点与横线 |
| OL | 1/2/3 与横线 |
| Task | 勾选框与横线 |
| Source | 文档图标，`源码编辑` tooltip/aria，selected state |
| Preview | 眼睛图标，`预览` tooltip/aria，selected state |
| Fullscreen | expand/collapse 图标，icon-only |
| Image | icon-only disabled，`图片上传功能开发中` |

浏览器 DOM 检查确认 Source、Preview、Fullscreen 三个按钮均没有可见文字；没有 Save、颜色、背景色或清色控件；UL/OL/Task 使用三种不同的图标类。

## Code Block Card Design

所有非 Mermaid fence 由共享 renderer 输出安全代码卡片：浅色 header/body、细边框、8px 圆角、语言标签、`复制代码` 与折叠箭头。无语言 fence 显示 `plain`。代码正文保留字面行断与 `overflow-x: auto`；复制读取 `code.textContent`，因此不包含 header、语言、按钮或潜在行号。折叠只切换单卡片 CSS 状态，绝不修改 Markdown；每张卡片状态独立。

自动化测试覆盖语言/无语言、原始复制内容、收起/展开和多卡片独立性。浏览器在 Bash/SQL 卡片上确认 language、复制成功反馈 `已复制`、收起/展开和另一张卡片不受影响。代码卡片在 Preview、当前 Read、历史 Read 与恢复预览均使用同一组件。

## Table Responsive Width

GFM table 已由 `.knowledge-markdown-table-wrap` 包装。wrapper 为 `width/max-width: 100%` 并拥有横向滚动；table 为 `width: max-content; min-width: 100%`。简单表格铺满正文，真正宽表只在 wrapper 内滚动。单元格使用 8×12px padding、浅色表头、轻边框、顶端对齐与轻量行 hover。

浏览器当前 1024px 宽环境中，正文、table wrapper 与三列表宽度均为 777px，正文 `scrollWidth` 等于 `clientWidth`；页面没有横向溢出。History 与 Restore Preview 的 DOM 也分别发现 table wrapper 和两张代码卡片。R03 已记录 1920/1714/1366/1024 源码工作区无溢出；本次减少了视图按钮宽度，并将新增宽度压力限制在 code/table 内部滚动容器。

## Toolbar Functional / Theme Matrix

| 领域 | 结果 |
| --- | --- |
| H1–H6、行内代码、引用、UL、OL、Task | PASS，源码 transform 回归覆盖 |
| Code Block / language dropdown / Link / Table / Mermaid / HR | PASS，源码 transform 回归覆盖 |
| Preview / current read | PASS，浏览器验收 `R04 Markdown UI验收` |
| Historical read | PASS，代码卡片 2、table wrapper 1 |
| Restore preview | PASS，代码卡片 2、table wrapper 1 |
| Compare raw diff exception | PASS，既有 Compare 回归未改变 |

## Browser Runtime

在隔离 SQLite、临时本地 Administrator、API `5211`、Vite `5178` 中创建 `R04 Markdown UI验收`。正文包含 H2、段落、UL、OL、Task、Quote、Inline Code、Bash/SQL fence、三列表、Mermaid、Link 和 HR。确认原始源码可见、Preview icon active state 可切换、创建及页面 Save 生成不可变修订（最终修订 3）、当前阅读/历史/恢复预览均呈现新主题。浏览器日志只有 Vite 连接 debug 条目，无 warning/error。

## Security

`markdown-it` 继续使用 `html: false`。代码卡片仅将 escaped fence source 放入 `<code>` 文本；复制/折叠不执行、不保存、不改变源码。Link metadata 仍 Deferred，未增加 SSRF surface。表格包装不改变 raw HTML 安全边界。

## Gates

| Gate | Result |
| --- | --- |
| `npm run type-check` | PASS |
| 聚焦 Vitest（8 files / 74 tests） | PASS |
| modified-scope ESLint | PASS |
| `npm run build` | PASS（仅现有 Vite large-chunk advisory） |
| KnowledgeDocument/revision backend regression | PASS，11 passed |
| `git diff --check` | PASS |

首次带编译的后端测试被任务开始前已存在的 `SystemKnowledgeHub.Api` PID 28756 锁定；未触碰该预存进程，改以 `--no-build --no-restore` 对同一受影响回归选择执行并通过。R04 不含后端生产代码变更。

## Documentation / PHASE-REV-DELTA-VERIFY Readiness

已更新 Source Editor Decision 和 Read Theme 文档，记录 compact icon-only toolbar、列表图标语义、代码卡片、复制/折叠和响应式表格边界。修订写入、历史阅读、恢复预览和 raw Compare 均保持兼容，具备后续显式 `PHASE-REV-DELTA-VERIFY` 所需的阅读侧证据；本任务未启动该阶段。

## Cleanup

已关闭验收浏览器页，停止本任务启动的 API/Vite 父进程及 Vite 子进程，并释放 `5211`、`5178`。任务专属临时数据库目录已删除。PID 28756 是任务开始前已有的 API 进程，未停止、未修改。
