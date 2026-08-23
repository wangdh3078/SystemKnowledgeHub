# UI-KC-FIX-01 Editor Toolbar / Alignment / Markdown Line-break Verification Report

## Result

**UI-KC-FIX-01 PASS**

KnowledgeDocument Editor 的 toolbar、正文对齐和 Markdown 换行问题已按最小范围修正。新保存内容不再生成 HTML `<br>`，历史 standalone legacy BR 可安全读取并在下一次内容保存时 canonicalize，raw HTML仍不可执行；Revision历史未被改写。没有 backend、schema、migration、frozen specification、Golden asset或其它 Low gap变更。

## Worktree Baseline

- Branch：`main`
- Baseline commit：`418e3ad5553836f3f211c895811ddf0bb8782ad5`（`🐛 fix: 关闭修订阶段中优先级缺口`）
- 实施前 worktree：clean。
- 开始前复核了 `AGENTS.md`、Gap Register、UI-B04、KC-B03、REV-B02报告、当前 task definition，以及 editor/bootstrap/CSS、Milkdown preset、shared renderer、Preview、Detail、Revision History/Compare与内容保存边界。
- 未发现 material frozen-contract conflict；Canonical store继续是 `body_markdown` Markdown，raw HTML继续关闭。

## Toolbar Before / After

Before：toolbar只有 H2、粗体、斜体、项目符号/编号列表、引用、行内代码、代码块、链接和表格；若干 icon语义含混，`ElTooltip`只在 test stub存在，production-like mount产生 unresolved component warning。

After：按现有 Milkdown可靠 command分为4组、共15项：

- Text Structure：正文、H1、H2、H3
- Inline：加粗、斜体、删除线、行内代码
- Blocks：无序列表、有序列表、引用、代码块
- Content：插入链接、插入表格、分隔线

未配置 history plugin，因此没有显示不可可靠工作的撤销/重做假按钮。Editor ready前或初始化失败时所有 command明确 disabled。

## Tooltip / Icon Semantics

- `ElTooltip` component及其CSS已加入既有 Element Plus selective bootstrap，不依赖 test-only stub。
- 每个可见 action都有唯一中文 tooltip和同义 `aria-label`，显式 trigger为 `hover + focus`。
- H1/H2/H3、正文、`B/I/S`、`</>`、`1.`、`{ }`使用直接 typography label；列表、引用、链接、表格、分隔线使用既有 Element Plus icon，没有新增 icon library。
- Focused production-like mount验证15个 action全部可解析、可访问且command dispatch正确；Browser验证 mouse hover“加粗”和keyboard focus“代码块”均显示提示。
- Browser fresh editor initialization的 warning/error为0；没有 `Failed to resolve component: el-tooltip`、Vue error或application error。

## Editor Alignment Root Cause

居中/窄列来自组合规则：`.ProseMirror { max-width: 1040px; margin: 0 auto; }`，同时 surface使用随viewport增长的 `clamp(..., 4vw, ...)`水平padding。不是编辑器节点定位问题。

修复后：surface使用固定 `20px 24px` padding与`text-align:left`；`.milkdown`、`.ProseMirror`均为`width:100%`、`min-width:0`，ProseMirror为`max-width:none`、`margin:0`、`box-sizing:border-box`、`text-align:left`。另外设置`white-space:pre-wrap`，消除ProseMirror运行时样式警告并保持编辑态空格/换行显示语义。

## Editor Layout Verification

Browser在四个要求viewport验证相同布局规则；正文左/上 inset分别为24px/20px，toolbar、table和code均受editor宽度约束，root没有新增横向overflow：

| Viewport | Surface / ProseMirror width | Left / top inset | Toolbar overflow | Root overflow |
| --- | ---: | ---: | ---: | ---: |
| 1920×1080 | 1647.33 / 1599.33px | 24 / 20px | 0 | 0 |
| 1714×892 | 1441.33 / 1393.33px | 24 / 20px | 0 | 0 |
| 1366×768 | 1109.33 / 1061.33px | 24 / 20px | 0 | 0 |
| 1024×768 | 767.33 / 719.33px | 24 / 20px | 0 | 0 |

四档均确认ProseMirror computed `max-width:none`、左右margin `0px`、`text-align:left`；list、blockquote、horizontal code scroll与100% table没有溢出editor surface。

## Markdown Line-break Root Cause

复现表明 Shift+Enter生成的Milkdown native hardbreak本来就会序列化为Markdown反斜杠加换行；生成 `<br />` 的真正来源是 commonmark内的可选 `remarkPreserveEmptyLinePlugin`。当ProseMirror出现中间空paragraph时，该serializer把它写为`<br />`；对应parser还会吞掉历史fixture中的BR，造成潜在数据丢失。

实现使用当前 commonmark preset的最小过滤副本，只排除 `remarkPreserveEmptyLinePlugin`导出的两个plugin成员，不替换Milkdown、不增加自定义Markdown token。Normal Enter继续保持paragraph/block语义；Shift+Enter继续使用Milkdown native hardbreak。

## Canonical Markdown Rule

- 新生成 hard break只保存标准Markdown `\\` + newline，例如runtime R2包含 `运行时普通段落\\\n运行时硬换行`。
- 新R2 raw body长度464、SHA-256 `0E2FBA9F54599DD6B78F233742AB1573DCE8204C1D60753EBB4C94B0F1902472`，不含`<br`。
- Focused round-trip覆盖普通paragraph、中文hard break、连续hard break、空paragraph、list/code/table以及二次round-trip稳定性。
- 没有启用 `markdown-it html: true`，没有broad HTML stripping或后台历史重写。

## Legacy BR Compatibility

兼容边界只接受由Markdown token/source map证明为top-level、单行、standalone paragraph的精确旧值：`<br>`、`<br/>`、`<br >`、`<br />`。Read renderer把该token替换为安全hardbreak render token；进入editor时把有后续正文的旧paragraph canonicalize为Markdown反斜杠换行。

以下均保持惰性文本、不转换：inline `A<br />B`、blockquote/list/indented code/fence、escaped token、uppercase `<BR />`、带attribute BR。该窄规则不会删除任意`<...>`内容。

## Preview / Read / Revision Compatibility

- Preview：normal paragraph与native hardbreak语义正确；H2、list、blockquote、code block、safe link、table全部渲染，未显示literal generated BR。
- Save → reload → Read：实际`br`=1，list/blockquote/code/table/safe links保留，literal BR=0。
- Edit again：Milkdown重新加载后hardbreak=1，H2/list/blockquote/code/table/link节点均保留，literal BR=0。
- Historical Preview：R1 standalone `<br />`渲染为真实line break，raw script/image/anchor显示为惰性文本。
- Revision Compare继续比较immutable raw source；Browser明确显示R1的`删除：<br />`、R2的Markdown-native反斜杠新增行，以及三条raw HTML删除行，没有把shared read compatibility带入diff source。
- R1前后raw body均为131 bytes，SHA-256始终是`15B70A02658F885005C75F92921D1F1E71E07C2D5AED8C71355EFCEA336B6B68`；历史revision未被修改。

## XSS Regression

Fixture包含：`<script>alert(1)</script>`、`<img src=x onerror=alert(1)>`、`<a href="javascript:alert(1)">x</a>`。

- Shared renderer、Detail、Historical Preview与Compare中实际`script`、`img`、`javascript:` anchor均为0。
- Historical Preview仍显示三段原始文本，证明没有以删除用户数据换取安全。
- Legacy BR compatibility仅替换已证明的standalone exact token，没有打开raw HTML bypass。

## Browser Runtime

隔离runtime使用独立临时SQLite、独立Data Protection keys、disposable Local Administrator、API `127.0.0.1:5130`与Vite `127.0.0.1:5197`：

`Login → KnowledgeDocument R1 → Edit → normal Enter + Shift+Enter → H2/Bold/List/Quote/Code/Link/Table → hover/focus tooltip → Preview → Save R2 → Reload → Read → Edit again → Historical Preview R1 → Compare R1→R2`

- R2修订数为2；R1 hash/length/legacy BR/XSS text在保存前后完全不变。
- R2不含legacy BR，保留native hardbreak、heading、code fence、table和safe link source。
- Console最初暴露ProseMirror缺`white-space:pre-wrap`的局部样式warning；补齐后重新初始化editor的fresh warning/error=0。没有tooltip unresolved warning或API 5xx。
- 最初readiness probe误用了非API root，错误API PID 4252在核对后被单独停止；正式runtime随后使用正确`/api/auth/options`。该probe没有业务写入。

## Build / Tests / Lint

- `dotnet build SystemKnowledgeHub.sln --no-restore` — PASS，0 warnings / 0 errors。
- Backend focused sequential regression：`KnowledgeDocumentsApiTests` 4/4、`KnowledgeDocumentRevisionApiTests` 5/5、`KnowledgeDocumentRevisionReadApiTests` 2/2、`KnowledgeDocumentSearchApiTests` 8/8；合计19/19 PASS。
- Frontend focused：9 files / 67 tests PASS，覆盖Editor、Milkdown round-trip、edit state、legacy canonicalizer、renderer、Detail、Revision History及Compare。
- `npm run type-check` — PASS。
- `npm run build` — PASS；仅既有Vite chunk-size advisory。
- Modified-scope ESLint `--max-warnings 0` — PASS，0 warnings / 0 errors。
- `git diff --check` — PASS。

## Repository Database / Cleanup Safety

Repository DB `src/SystemKnowledgeHub.Api/App_Data/system-knowledge-hub.db`前后只读指纹完全相同：Length `507904`、LastWriteTimeUtc `2026-08-23T15:39:00.6505310Z`、SHA-256 `64EC2B4BFFA5200EF0F2F91C23DA498E666B490E20AC30E2E7EED868E4249B13`。

- Browser task tab已关闭，temporary viewport override已reset。
- 只停止核验过的正式runtime API PID `35424`与Vite PID `18500`；没有按process name、parent tree、port或wildcard结束任何进程，没有触碰Codex。
- 两个PID退出后wrapper sessions `92859`、`58422`自然结束；API/Web均不可访问。
- 临时目录`skh-ui-kc-fix-01-abb4bdadd7ad44768b146e240346abc6`在确认resolved target位于system temp且leaf完全匹配后精确删除；final absent。

## REV-GAP-005 Closure

`REV-GAP-005`更新为 **CLOSED — UI-KC-FIX-01**。Closure evidence包括selective production bootstrap、15项tooltip/aria-label test、Browser hover/focus、fresh console clean与构建门禁；原问题、风险和recommended fix历史描述均保留。

## Explicitly Deferred Gaps

以下保持原状态，未处理：`REV-GAP-006`、`REV-GAP-007`、`REV-GAP-008`、`REV-GAP-009`、`REV-GAP-011`。没有新增Medium/High gap，也没有开始PHASE-TRACE。

## PHASE-REV-DELTA-VERIFY Readiness

UI-KC-FIX-01适用PASS gate全部满足，未发现会阻塞PHASE-REV-DELTA-VERIFY的新Medium/High问题。本任务只形成readiness evidence，不自动开始Delta Verification；后续仍以用户明确安排为准。
