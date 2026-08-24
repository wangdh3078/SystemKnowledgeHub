# KnowledgeDocument Markdown Read Theme

Status: **Frozen for UI-KC-B05-R03**  
Product: **系统知识中心 / System Knowledge Hub**

`knowledge-markdown-theme.css` provides a scoped VuePress-like reading theme through `.knowledge-markdown-content`. It is applied only after the shared safe renderer has produced read HTML.

Consumers:

- unsaved Preview;
- current KnowledgeDocument Read;
- historical revision Read;
- restore preview.

The theme covers readable type hierarchy, paragraph rhythm, headings, blockquotes, inline and fenced code, lists/task lists, links, GFM tables, Mermaid output, horizontal rules, and responsive overflow. It does not apply to the CodeMirror raw source editor.

Fenced code is rendered as a light technical code card: a language label (`plain` when omitted), a raw-code copy control, and an independent collapse/expand control. The code body keeps literal line breaks and owns horizontal scrolling; controls never alter source Markdown or execute it.

Every GFM table is inside `.knowledge-markdown-table-wrap`, which owns horizontal overflow. Simple tables use the available reading width, while genuinely wide tables scroll inside that wrapper rather than widening the page.

The source/preview toolbar controls are icon-only; their Chinese labels remain available through accessible names and tooltips, not visible button text.

Revision Compare is intentionally excluded: it remains escaped raw Markdown line diff and does not render Markdown or use this reading theme.
