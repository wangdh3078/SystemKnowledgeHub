# KnowledgeDocument Markdown Source Editor Decision

Status: **Frozen for UI-KC-B05-R05**
Product: **系统知识中心 / System Knowledge Hub**

## Decision

KnowledgeDocument editing is raw Markdown source editing. `bodyMarkdown: string` is the single authoritative edit state.

```text
bodyMarkdown source → CodeMirror source editor → safe rendered preview/read → save bodyMarkdown exactly
```

Edit shows source syntax such as headings, lists, quotes, task items, fenced code, GFM tables, links, Mermaid fences, and horizontal rules. Preview and Read render the same source through the existing safe shared renderer. Compare remains an escaped raw-source diff.

## Milkdown / CodeMirror decision

Milkdown was audited as a ProseMirror WYSIWYG authoring surface. Its document model and Markdown serializer are not a true raw-source editing experience, and retaining both a ProseMirror document and source string would create two competing states. It is therefore removed from KnowledgeDocument authoring.

No reusable source-editor dependency existed in the repository. The selected implementation is minimal CodeMirror 6 with Markdown syntax highlighting, selection/caret, undo/redo history, line wrapping, keyboard editing, and controlled source updates. It deliberately excludes language servers, diagnostics, autocomplete, multi-file editing, and IDE features. MdEditorV3 is only a visual/interaction reference and is neither installed nor copied.

## Toolbar and save boundary

Toolbar actions transform the selected raw source or current source line. They do not manipulate rendered DOM marks or rich nodes. The toolbar has no Save action. Detail-page Save is the sole content write path and submits title, summary, and exact `bodyMarkdown` in one request; Ctrl/Cmd+S emits the same page-level save request.

The source toolbar uses compact 27px icon controls in a 34–38px single-row layout where width permits, grouped by block type, inline, list/quote, insert, history, and view controls. At narrower widths it may wrap without creating page-level horizontal overflow. Unordered, ordered, and task list controls use distinct list, numbered-list, and checklist semantics. Source and Preview are icon-only controls with accessible labels/tooltips (`源码编辑` and `预览`) and explicit selected state; fullscreen remains icon-only. The toolbar does not show Save, text/background-color, or clear-color controls.

The diagram control is an `插入图表` menu. It inserts one of eight complete Mermaid fences directly into `bodyMarkdown`: Flowchart, Sequence, Gantt, Class, State, Pie, ER, and Journey. Templates are bounded local source transforms; no rendered SVG, editor plug-in state, or diagram metadata is persisted. The caret is placed inside the inserted fence immediately after its `mermaid` header so the template remains ordinary editable source.

The create dialog has its own raw source editor and its only write action is `创建草稿`, which submits document type, title, summary, and exact source in one request.

## Compatibility

Existing immutable revisions are never rewritten. Historical `{color:#RRGGBB|...}` and `{bg:#RRGGBB|...}` source remains supported by the safe renderer as read-only compatibility, but no authoring toolbar action creates or clears those extensions. Raw HTML stays disabled. Mermaid output is never persisted.
