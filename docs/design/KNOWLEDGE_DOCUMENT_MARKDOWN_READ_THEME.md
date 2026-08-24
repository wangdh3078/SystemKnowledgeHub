# KnowledgeDocument Markdown Read Theme

Status: **Frozen for UI-KC-B05-R06**
Product: **系统知识中心 / System Knowledge Hub**

`knowledge-markdown-theme.css` provides a scoped VuePress-like reading theme through `.knowledge-markdown-content`. It is applied only after the shared safe renderer has produced read HTML.

Consumers:

- unsaved Preview;
- current KnowledgeDocument Read;
- historical revision Read;
- restore preview.

The theme covers readable type hierarchy, paragraph rhythm, headings, blockquotes, inline and fenced code, lists/task lists, links, GFM tables, Mermaid output, horizontal rules, and responsive overflow. It does not apply to the CodeMirror raw source editor.

Fenced code is rendered as a light technical code card: a language label (`plain` when omitted), a raw-code copy control, and an independent collapse/expand control. The code body keeps literal line breaks and owns horizontal scrolling; controls never alter source Markdown or execute it. Copy writes raw code only. Its per-card default is the `复制代码` copy icon; a successful clipboard write becomes the `已复制` check icon for 2500ms before resetting. Clipboard failure keeps the copy icon and exposes local `复制失败` feedback. Pending per-card timers are cleared on component unmount.

Code cards use `highlight.js` core with explicitly registered language modules only—never the full default bundle or a global theme. `plaintext` remains literal. The supported fence mapping covers C#, JavaScript, TypeScript, TSX, JSX, Vue SFC, JSON/JSONC, SQL/PLSQL, Bash/Shell, Batch, PowerShell, Python, Java, Kotlin, C++, C, Go, Rust, PHP, Ruby, HTML/XML, CSS/SCSS/Less, YAML, TOML, INI, Nginx, Markdown, and Dockerfile. Vue, JSX, and TSX use the same registered core language modules with escaped tag-aware local rendering; they do not load a full bundled grammar. Highlight output is generated from escaped source as scoped `hljs-*` token spans inside the existing light card; unknown languages remain escaped literal text under their original fence label. The theme supplies only restrained token colors, including tag/attribute tokens for frontend samples, and does not change code-card copy, collapse, source preservation, or XSS boundaries.

Every GFM table is inside `.knowledge-markdown-table-wrap`, which owns horizontal overflow. Simple tables use the available reading width, while genuinely wide tables scroll inside that wrapper rather than widening the page.

The source/preview toolbar controls are icon-only; their Chinese labels remain available through accessible names and tooltips, not visible button text.

Revision Compare is intentionally excluded: it remains escaped raw Markdown line diff and does not render Markdown or use this reading theme.
