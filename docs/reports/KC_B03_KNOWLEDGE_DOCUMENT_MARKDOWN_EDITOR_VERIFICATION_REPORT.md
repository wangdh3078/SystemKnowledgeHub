# KC-B03 — KnowledgeDocument Markdown Editor Verification Report

## Result

KC-B03 PASS

## Scope Delivered

- 在既有 KnowledgeDocument 详情路由内增加 Editor / Administrator 的 Edit Mode；View Mode 继续使用 KC-B02 的 `markdown-it` 渲染。
- 新增基于 Milkdown 7.22.1 的受限 GFM Markdown 编辑器，提供标题、粗体、斜体、无序/有序列表、引用、行内代码、代码块、链接和表格工具栏。
- 编辑 Title、Summary 与 `bodyMarkdown`，通过既有 `PUT /api/knowledge-documents/{id}/content` 以单一并发令牌原子保存；未新增或变更后端 API、DTO、数据库或 migration。
- 新增未保存内容预览、dirty state、Ctrl/Cmd+S、路由离开/`beforeunload` 放弃确认、409 冲突保留本地内容与显式重新加载。
- 已归档文档不显示编辑入口；Published 文档内容保存后仍保持 Published，且不改变 KnowledgeStatus。编辑模式隐藏生命周期操作。

## Markdown Canonical Storage and Round-trip Spike

- canonical 持久化字段保持为 `body_markdown` / API `bodyMarkdown`；编辑器不保存 HTML、ProseMirror JSON、草稿缓存或富文本快照。
- View 和 Preview 都调用同一个 HTML-disabled `markdown-it` renderer；编辑器产生的 HTML 从不作为保存内容展示或传输。
- 已执行 `milkdownRoundTrip.spec.ts`：标题、中文段落、空行、列表、引用、行内代码、带 `bash` 语言标记的 fenced code block、GFM 表格和链接均经 `Markdown → Milkdown → export → Milkdown → export` 保留，且 B→C 输出稳定。
- Milkdown 对等价 Markdown 做了预期规范化（例如无序列表标记与表格空白/对齐）；该规范化不改变语义，也不产生 HTML/JSON 替代格式。

## Dependencies

- `@milkdown/core` 7.22.1
- `@milkdown/preset-commonmark` 7.22.1
- `@milkdown/preset-gfm` 7.22.1
- `@milkdown/plugin-listener` 7.22.1

编辑器仅在进入 Edit Mode 时以 `defineAsyncComponent` 加载；Viewer 的普通查看路径不会初始化编辑器。

## Files Changed for KC-B03

- `src/SystemKnowledgeHub.Web/package.json`
- `src/SystemKnowledgeHub.Web/package-lock.json`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/knowledgeDocumentContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/knowledgeDocumentsApi.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/editor/documentEditState.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/editor/documentEditState.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/editor/KnowledgeDocumentEditor.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/editor/KnowledgeDocumentEditor.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/editor/milkdownRoundTrip.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/knowledge-documents.css`

## Focused Verification

| Command / check | Result |
| --- | --- |
| `npm run type-check` | Passed. |
| KC-B03 editor/detail/renderer Vitest selection | Passed: 5 files, 8 tests. Covers round-trip stability, dirty/revert state, editor toolbar initialization, unsaved preview, atomic save payload, 409 conflict retention, and Viewer no-edit UI. |
| Local ESLint binary for the KC-B03 files | Passed. Full-project lint still has the pre-existing errors in Integrations and Unknown Items; neither file was changed. |
| `npm run build` | Passed. Vite emitted the existing large-chunk advisory only. |
| `dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj --no-restore --filter "FullyQualifiedName~KnowledgeDocumentsApiTests"` | Passed: 4 passed, 0 failed, 0 skipped. Includes Viewer read/write boundary, lifecycle/concurrency and KnowledgeStatus regression coverage. |
| `dotnet build SystemKnowledgeHub.sln --no-restore` | Passed: 0 warnings, 0 errors. |

## Browser Runtime Verification

An isolated temporary SQLite/local-login environment was used and then removed. With a temporary Administrator account:

- Created `Oracle 数据库连接异常处理` as an SOP containing headings, Chinese prose, ordered/unordered lists, quote, inline code, `bash` code fence, table and link; View Mode rendered all constructs.
- Entered Edit Mode and confirmed the Milkdown toolbar and initial rich Markdown content. Modified title and body, verified the Preview tab rendered the unsaved Markdown, then saved using Ctrl+S and reloaded the route successfully.
- Published the document, edited and saved its content, and confirmed it remained Published with KnowledgeStatus unchanged.
- Archived the document and confirmed the Edit action was absent until Restore; then restored it.
- Used two authenticated browser tabs to produce a real stale concurrency token. The first tab retained its local edit, displayed the conflict message and exposed only explicit reload handling; it did not overwrite the second tab's saved change.
- A Viewer-specific frontend test confirms no editing or lifecycle actions are exposed; the existing focused API test confirms Viewer read/write enforcement. A separate browser Viewer login was not created because the isolated bootstrap command creates only an Administrator credential.

## Cleanup

- Closed both browser tabs used for verification.
- Stopped the exact ASP.NET Core and Vite process trees started for this task.
- Confirmed ports 5099 and 5173 have no listener.
- Removed the verified task-specific temporary SQLite database, logs and Data Protection keys.

## Scope and Dirty Worktree Safety

- The pre-existing worktree already contained DOC-STRUCTURE-B01 documentation moves, AUTH-B02 changes/reports, KC-B02 work (including the untracked KnowledgeDocument feature directory), and unrelated modified/untracked files. KC-B03 neither reset, reverted nor overwrote those changes.
- No production backend code, Authentication, Authorization, KnowledgeDocument persistence schema, migration, API route/shape, lifecycle transition implementation, KnowledgeStatus logic, Evidence, relationship, attachment, category/tag, import/export, or autosave/local-storage behavior was changed by KC-B03.
- `git diff --check` passed. The repository's Git ownership warning was handled with the established local `safe.directory` invocation.

## Deferred

KC-B04 and later slices remain untouched: attachments, Evidence and relationship panels, KnowledgeStatus actions, categories/tags, version history, import/export and any additional KnowledgeDocument workflows.
