# UI-KC-B05-R06 — Consolidated UI Acceptance Verification Report

## Result

**UI-KC-B05-R06 PASS**

## Delivered scope

- The raw Markdown editor is now a bounded flex viewport. Its compact toolbar is outside the scrolling workspace; CodeMirror source and unsaved preview each own internal vertical scrolling. The detail editor, creation dialog, and fullscreen use the same separation. Long Markdown no longer makes the page grow with its line count.
- The existing compact, icon-only toolbar remains page-save only. It has no Save, foreground color, background color, or clear-color action. `源码编辑` / `预览`, undo/redo, inline actions, block actions, link, table, diagram, and fullscreen keep accessible labels and tooltips.
- The diagram menu now has the confirmed Chinese labels `流程图`、`时序图`、`甘特图`、`类图`、`状态图`、`饼图`、`关系图`、`旅程图`, and inserts the R06 Mermaid source defaults directly into `bodyMarkdown`.
- The scoped light code-card renderer keeps `highlight.js/lib/core` plus individual modules only. It adds frontend and configuration coverage for Vue SFC, TSX, JSX, SCSS, Less, JSONC, Kotlin, PHP, Ruby, Shell, Batch, PLSQL, TOML, INI, and Nginx. Vue/JSX/TSX tags are escaped and highlighted locally; no full highlighter bundle, global theme, source mutation, or executable path is introduced.
- The relation list now has spaced section hierarchy, compact relation-type pills, keyboard-visible target buttons, target text truncation, and a narrow-width row fallback. It remains a real navigation control rather than a simulated link.

## Browser → API → SQLite acceptance

An isolated task-owned API, SQLite database, data-protection directory, and Vite server were used. Browser verification created `R06 长文编辑器验收` and exercised the actual UI/API/persistence path.

| Check | Result |
| --- | --- |
| 1,000-line source editor | PASS — 468px dialog editor shell; CodeMirror client height 431px / scroll height 19,716px; `scrollTo` reached its actual bottom while the toolbar stayed visible. |
| Long preview | PASS — preview client height 431px / scroll height 8,293px; `创建草稿` remained visible. |
| Source-bottom actions | PASS — Bold, inline code, quote, task list, diagram menu, and link dialog remained usable after source-bottom scrolling. |
| Diagram menu | PASS — all eight confirmed labels were present; `流程图` inserted the R06 source fence. |
| Read rendering | PASS — created document displayed four code cards (Vue, TSX, C#, JSONC), 23 scoped highlight tokens, one GFM table, one external link, and rendered Mermaid output. No root horizontal overflow at the active 1081px viewport. |
| Relations | PASS — created three real records: `说明 → MES`, `引用 → ERP`, and `说明 → WMS`; type pills were distinct and `系统 · WMS` navigated to `/systems/13`. |
| Fullscreen implementation | PASS by component/CSS contract — fullscreen is a fixed viewport flex shell with internal source/preview overflow and body scroll lock. |

The in-app browser exposes the active desktop viewport but does not expose a safe viewport-resize operation. The 1081px live-browser check above was paired with the existing 1100px, 1366px, 1200px, and 720px responsive CSS boundaries; no fixed editor or relationship width was introduced. This is an environment limitation, not a product fallback or a skipped runtime path.

## Automated verification

| Gate | Result |
| --- | --- |
| Focused Vitest: editor, source transforms, renderer, detail view, Markdown component | PASS — 5 files, 98 tests |
| `npm run type-check` | PASS |
| `npm run build` | PASS — existing Vite chunk-size advisory only |
| Backend KnowledgeDocument regression | PASS — `dotnet test --no-build --filter FullyQualifiedName~KnowledgeDocument`, 30/30 |
| `git diff --check` | PASS |

The first backend command without `--no-build` was blocked only because the user's pre-existing API process `28756` locks its Debug apphost. It was deliberately preserved. The no-build targeted regression passed against the available compiled test output; this UI-only slice makes no backend production, contract, schema, migration, or `App_Data` change.

## Code Copy Feedback

Each rendered code card now owns its copy feedback state. The default is the Font Awesome `faCopy` icon with `复制代码` title and accessible name. A successful `navigator.clipboard.writeText` of the exact raw `<code>` text switches only that card to `faCheck`, title `已复制`, and accessible name `已复制`. Its timer is 2500ms and a repeated successful click clears and restarts that card's timer.

Clipboard rejection never creates a fake success: the copy icon and `复制代码` accessible name remain, and the card displays the local, polite `复制失败` feedback. Copy state and timers are isolated between cards. Pending timers are cleared when the Markdown component unmounts, so preview/read/revision/page changes cannot update detached DOM.

Focused fake-timer tests cover exact raw copy content, success icon/label, 2500ms reset, rejection feedback, two-card isolation, repeated-click timer restart, and unmount cleanup. Browser acceptance created a document with TypeScript and SQL cards: after clicking the TypeScript card, it reported `已复制` plus `check`, while the SQL card remained `复制代码` plus `copy`; after 2.6 seconds the first card returned to `复制代码` plus `copy`. The browser observed no application warning or error on this path.

## Documentation and exclusions

`KNOWLEDGE_DOCUMENT_MARKDOWN_SOURCE_EDITOR_DECISION.md` now freezes the R06 bounded editor/creation/fullscreen behavior and the confirmed diagram labels. `KNOWLEDGE_DOCUMENT_MARKDOWN_READ_THEME.md` now freezes core-plus-explicit-module highlighting and its expanded mappings.

This slice does not add a second editor, generic toolbar framework, rich-text state, diagram persistence, SVG persistence, code execution, a global highlight theme, image upload, backend content changes, or any workflow after R06.

## Cleanup

The task-owned browser tab, API/Vite process trees, isolated SQLite/data-protection/log directory, and verification ports are removed before the commit. The pre-existing API process `28756` was never selected as a cleanup target. It was present after the original R06 cleanup and had exited before this supplement's final read-only process check.
