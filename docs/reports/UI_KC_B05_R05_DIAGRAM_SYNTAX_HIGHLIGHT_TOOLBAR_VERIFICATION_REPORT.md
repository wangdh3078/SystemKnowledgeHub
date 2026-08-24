# UI-KC-B05-R05 — Diagram, Syntax Highlighting, and Toolbar Verification Report

## Result

**UI-KC-B05-R05 PASS**

## Scope and source boundary

KnowledgeDocument continues to use `bodyMarkdown` as its only authoring and persistence value. CodeMirror edits raw source; unsaved Preview, current Read, historical Read, and restore preview use the shared safe renderer; revision Compare remains an escaped raw-source line diff. No backend contract, schema, migration, revision data, or repository `App_Data` was changed.

## Diagram insertion menu

The compact `插入图表` menu is local to the existing source toolbar and writes complete Mermaid fences directly into raw source. It has eight accessible menu items: `流程图`, `时序图`, `甘特图`, `类图`, `状态图`, `饼图`, `ER 图`, and `用户旅程`. The local transform places the caret immediately after the `mermaid` fence header, so all template content remains ordinary editable Markdown.

Focused transform coverage verifies every template marker and caret placement. Browser verification opened the menu, observed all eight options, and invoked every item. The saved acceptance document contains eight valid Mermaid fences; current Read rendered eight SVG diagrams and historical Read rendered the same eight diagrams through the shared renderer.

## Code cards and syntax highlighting

`highlight.js` 11.12.0 is used through `highlight.js/lib/core` plus explicit individual language modules only. No full default highlight bundle, global theme, executable code path, or source mutation was introduced.

The code-fence mapping covers `plaintext`, C#, JavaScript, TypeScript, JSON, SQL, Bash, PowerShell, Python, Java, C++, C, Go, Rust, HTML, XML, CSS, YAML, Markdown, and Dockerfile. Registered highlighters produce scoped `hljs-*` spans; `plaintext` and unknown fences remain escaped literal source. The existing light technical code-card header, raw copy action, collapse action, internal horizontal scrolling, and XSS boundary remain unchanged.

The browser-created document `R05 Markdown UI验收` contained Bash, SQL, C#, and JSON cards. Current Read reported four `hljs language-*` code blocks with token spans (2, 2, 3, and 9 respectively). Historical revision Read reported four shared code cards, 16 highlight spans, and the same responsive table wrapper. The code-language dialog exposed all 20 expected choices.

## Toolbar and reading theme

Toolbar controls are 27×27px with 15px Font Awesome icons, 1–3px grouping gaps, and compact 3px vertical padding. The diagram menu is a small local positioned menu rather than a second drawer, editor, or global toolbar framework.

At the browser's 1280px acceptance viewport, the toolbar measured 33.67px high, 1023px client/scroll width, and its first icon button measured 27×27px. The root measured 1280px client/scroll width, with no page-level horizontal overflow. Existing responsive wrap rules remain in effect for narrower widths; no fixed minimum width was introduced.

The scoped read theme retains light technical code cards and table-local overflow. Current and historical read both rendered the eight-column table inside exactly one `.knowledge-markdown-table-wrap`; the page root did not widen.

## Browser → API → SQLite verification

An isolated local SQLite database, local Administrator, and data-protection key directory were created only for this task. The browser performed:

1. Local login and creation of `R05 Markdown UI验收`.
2. Raw-source authoring with Bash/SQL/C#/JSON, a wide table, and all eight Mermaid templates.
3. Menu inspection and invocation of all eight diagram actions.
4. Save to immutable revision 2, current Read inspection, Revision History inspection, and revision 1 → 2 Compare inspection.

Compare showed literal Markdown fence/table/code lines and zero rendered code cards, preserving the frozen raw-source comparison model. Current and historical Read showed the shared rendered cards/theme. The browser console had no new application error after the final local-menu implementation.

## Automated verification

| Gate | Result |
|---|---|
| Focused Vitest: source transforms, editor, renderer, Markdown component | PASS — 4 files, 63 tests |
| `npm run type-check` | PASS |
| Modified-scope ESLint | PASS (the CSS path is outside the repository ESLint configuration and was reported as ignored, not as an error) |
| `npm run build` | PASS — existing Vite chunk-size advisory only |
| Relevant backend KnowledgeDocument/revision regression | PASS — 11/11; backend production code unchanged |
| `git diff --check` | PASS |

## Documentation and explicit exclusions

`KNOWLEDGE_DOCUMENT_MARKDOWN_SOURCE_EDITOR_DECISION.md` now records the R05 compact toolbar and diagram-source decision. `KNOWLEDGE_DOCUMENT_MARKDOWN_READ_THEME.md` records the core-plus-language-module highlighting and scoped light theme.

This slice does not add diagram persistence, SVG persistence, a diagram AST, a second editor, a generic toolbar framework, an external code viewer, a global highlight theme, code execution, image upload, backend content changes, or any adjacent capability.

## Cleanup

The browser tabs and every task-owned API/Vite process are stopped after this report is written. The exact isolated runtime directory, SQLite files, logs, disposable account data, and data-protection keys are deleted. Pre-existing API process `28756` and repository `App_Data` remain untouched.
