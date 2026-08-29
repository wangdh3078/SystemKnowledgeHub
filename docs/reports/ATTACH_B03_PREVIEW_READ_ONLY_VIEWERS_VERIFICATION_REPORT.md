# ATTACH-B03-PREVIEW — PDF / Text / CSV / XLSX Read-only Preview UX Verification Report

## Result

```text
ATTACH-B03-PREVIEW PASS
ATTACH-B04 READY: YES
```

ATTACH-B03-PREVIEW 已完成。适用的实现、自动化验证、真实浏览器验证、权限与历史上下文验证、持久化安全检查及清理均通过；没有未解决的 Blocker / High。

## Scope

本任务在 ATTACH-B01 / ATTACH-B03 已有附件 metadata、受保护内容读取和文档附件区基础上，增加只读预览体验：

- PDF：通过受保护 API 获取 Blob，在应用内只读 iframe 中显示，并保留下载兜底。
- Text family：TXT / LOG / SQL / JSON / XML 使用惰性纯文本展示。
- Markdown：复用现有安全 Markdown renderer。
- CSV：有界表格预览，明确显示行列截断状态。
- XLSX：只读工作表选择，使用后端返回的缓存显示值，不执行公式。
- ZIP 等 download-only 类型：不显示虚假的预览入口。
- Current / exact historical revision / soft-deleted owner historical revision 均使用对应的受保护上下文。
- 统一 loading、取消、失败、限制、重试和下载 fallback UX。

未实现附件编辑、Office 文档转换、缩略图、OCR、云存储、CDN、媒体库或 ATTACH-B04 工作。

## ATTACH-A02 / ATTACH-B01 / ATTACH-B03 Compliance

- 预览能力由后端 metadata 的 `canPreview` / `previewMode` 驱动，前端没有维护独立能力白名单。
- 所有 current / historical preview 与 download 请求均携带精确的 KnowledgeDocument / Revision 上下文。
- 未保存 orphan 不通过正式 current/historical route 预览；UI 明确提示“保存后可预览”。
- PDF 使用带凭据的受保护请求生成临时 Blob URL；未暴露 StorageKey、物理路径或公开静态 URL。
- Text / Markdown / CSV / XLSX 仅消费 B01/A02 既有只读 preview contract；未放宽认证、授权或存储边界。
- 历史预览继续依赖 exact AttachmentReference snapshot；当前修订移除附件不破坏旧 Revision。
- soft-deleted owner 不恢复 current navigation；已批准的历史 Revision 仍可按 exact historical boundary 读取。

## Preview Mode Matrix

| Attachment family | UI mode | Security / behavior |
| --- | --- | --- |
| PDF | Protected Blob + iframe | 受保护读取、对象 URL 生命周期管理、下载 fallback |
| TXT / LOG / SQL / JSON / XML | Plain text | `<pre>` 惰性文本，不执行 HTML/script |
| Markdown | Safe Markdown | 复用安全 renderer，原始 HTML/script 保持惰性 |
| CSV | Bounded table | 行列限制、截断提示、公式样文本不执行 |
| XLSX | Sheet table | 工作表切换、缓存显示值、公式表达式不显示/不执行 |
| ZIP / unsupported preview type | None | 仅下载，不渲染预览按钮 |

## PDF

- 通过 typed API client 以 credentials 获取 Blob，并校验返回 MIME。
- Blob URL 仅存在于预览会话，关闭、切换和卸载时撤销。
- primary protected fetch 成功后立即进入 iframe 状态，不依赖浏览器内建 PDF viewer 的非确定性 `load` 事件。
- 加载失败、限制或不可用时保留精确上下文下载入口。
- 真实 PDF 在 current 与 historical revision 场景均显示成功。

## Text

- TXT / LOG / SQL / JSON / XML 均通过统一 Text preview contract 展示。
- 内容使用文本节点输出；真实浏览器验证中的 `<script>` 保持惰性，页面 script element 数量未增加。
- 长文本区域内部滚动，不推动整个页面产生横向溢出。

## Markdown

- 复用 KnowledgeDocument 既有安全 Markdown renderer。
- 标题等 Markdown 语义正常显示。
- 内嵌 `<script>` 作为惰性文本保留，没有生成或执行 script element。

## CSV

- 表头、行、列按后端 bounded preview contract 渲染。
- 真实浏览器以 `PreviewCsvMaxRows=3` 验证 3 行 × 3 列结果及“行数已截断”提示。
- `=1+1` 等公式样文本仅作为字符串显示，不执行。
- 小屏宽度下表格在预览区内部滚动，Dialog 和页面无横向溢出。

## XLSX

- 展示后端提供的工作表列表并支持只读切换。
- Data / Archive 两个工作表的选择和内容切换已在真实浏览器验证。
- 前端只显示后端返回的缓存显示值，不执行公式、不显示公式表达式。
- 专用 fixture 中 `<f>1+1</f><v>2</v>` 仅显示缓存值 `2`，没有显示 `=1+1`。

## Download-only Types

- ZIP metadata 保持 download-only。
- 文档附件区不显示 Preview 按钮，不创建假预览或客户端解压流程。
- 真实浏览器下载事件验证通过。

## Current / Historical Context

- Current detail 使用 current attachment preview/download route。
- Revision detail 使用精确 `documentId + revisionNumber + attachmentId` route。
- 隔离数据中将 `runtime.txt` 从新 Head 移除后，current 不再显示该附件，Revision 3 仍可预览并下载。
- 隔离文档 soft delete 后，页面显示删除标识、不提供 current navigation / edit 入口；Revision 3 的历史附件仍通过 exact historical route 正常预览。

## Error / Limit UX

- 覆盖网络、认证/授权、not found、state/concurrency、invalid reference、preview unavailable 和安全限制等 typed error 映射。
- 请求切换/关闭时使用 AbortSignal，并通过 request identity 防止迟到响应覆盖新状态。
- 失败不关闭附件区、不清空页面状态；支持重试并保留精确上下文下载 fallback。
- 真实浏览器使用超过 `PreviewSpreadsheetMaxWorkbookBytes=10000` 的 XLSX 验证限制提示：`该附件超过安全预览限制，请下载原文件查看。`
- 限制场景的下载 fallback 指向正确的 current attachment URL。

## Accessibility / Responsive

- Preview 按钮、状态、错误、重试、下载和工作表选择均有可读标签。
- Preview overlay 使用既有 DialogHost；实际 `role=dialog` 具有名称“附件只读预览”。
- Loading / error 不只依赖颜色；键盘可操作入口与关闭流程保留。
- 真实检查 1440×900 与 1280×720；受浏览器外壳影响的有效 viewport 分别为 1384×865 与 1231×692。
- 两个 viewport 均无页面级横向溢出；Dialog 位于可视区域，CSV/XLSX 表格保持内部滚动。

## Files Changed

Frontend API / contracts:

- `src/SystemKnowledgeHub.Web/src/api/client/apiClient.ts`
- `src/SystemKnowledgeHub.Web/src/api/client/apiClient.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/attachmentContracts.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/attachmentContracts.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/knowledgeDocumentAttachmentsApi.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/api/knowledgeDocumentAttachmentsApi.spec.ts`

Frontend UI / tests:

- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/AttachmentPreviewHost.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/AttachmentPreviewHost.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentRevisionHistory.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentRevisionHistory.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.vue`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts`
- `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/knowledge-documents.css`
- `src/SystemKnowledgeHub.Web/src/layouts/DialogHost.vue`

Documentation:

- `docs/reports/ATTACH_B03_PREVIEW_READ_ONLY_VIEWERS_VERIFICATION_REPORT.md`
- `docs/DOCUMENT_INDEX.md`

## Automated Verification

Frontend:

```text
npm run type-check
PASS

npm test -- src/api/client/apiClient.spec.ts src/features/knowledge-documents/api/attachmentContracts.spec.ts src/features/knowledge-documents/api/knowledgeDocumentAttachmentsApi.spec.ts src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.spec.ts src/features/knowledge-documents/components/AttachmentPreviewHost.spec.ts src/features/knowledge-documents/pages/KnowledgeDocumentDetailView.spec.ts src/features/knowledge-documents/components/KnowledgeDocumentRevisionHistory.spec.ts
PASS — 7 files / 73 tests

affected ESLint
PASS — 0 errors

affected Prettier check
PASS

npm run build
PASS — existing Vite large-chunk advisory only
```

Backend compatibility gate:

```text
dotnet build SystemKnowledgeHub.sln -c Release --no-restore
PASS — 0 warnings / 0 errors

dotnet test tests/SystemKnowledgeHub.Api.Tests/SystemKnowledgeHub.Api.Tests.csproj -c Release --no-build --filter "FullyQualifiedName~AttachmentFoundationApiTests"
PASS — 9 / 9
```

本任务未修改生产后端代码。

## Browser Verification

使用真实应用内浏览器和隔离 runtime 完成：

- File picker 上传并 semantic save 后，PDF / TXT / LOG / SQL / JSON / XML / Markdown / CSV / XLSX / ZIP metadata 与入口正确。
- PDF current / historical iframe 预览成功，下载事件成功。
- Text family 与 Markdown 的恶意/特殊标记保持惰性，console 无执行错误。
- CSV 行限制、截断提示、公式样文本及内部滚动正确。
- XLSX 工作表切换、缓存公式值、oversize limit UX 正确。
- ZIP 无 Preview，仅下载。
- 当前移除附件后，旧 Revision 仍能 exact historical preview/download。
- soft-deleted owner 的历史预览可用，current navigation/edit 不可用。
- 1440×900 与 1280×720 responsive 验证通过。
- 页面 DOM 未出现 StorageKey、`objects/` 或物理仓库路径。
- Browser console：`0 new errors`。

## Persistent Data Safety

Repository SQLite baseline 与验证后完全一致：

```text
Length: 950272
LastWriteTimeUtc: 2026-08-29T05:58:49.2454191Z
SHA-256: AF0509630E229801735361AF257CEBD1B4C11947D9A98E8E0358E00F676B664D
WAL: absent
SHM: absent
Repository DB: UNCHANGED
```

Repository attachment storage baseline / final：

```text
Files: 20
Bytes: 3835039
Newest LastWriteTimeUtc: 2026-08-29T05:53:22.8514401Z
Result: UNCHANGED
```

Runtime 使用 task-owned SQLite、Attachment StorageRoot、Data Protection 和隔离端口 5331 / 5332。

## Cleanup

- task-owned runtime 根目录已按精确路径删除。
- task-owned attachment object files：12；staging residue：0；已随 task root 清理。
- 端口 5331 / 5332 listener：0。
- agent 启动的 API / Vite / browser tab 均已停止或关闭。
- repository SQLite 和 attachment storage 清理后复核仍保持 unchanged。

## Existing / New Gaps

Existing non-blocking gaps remain unchanged:

- ATTACH-A01 已记录的 malware scanning / internal-pilot boundary。
- 已有 SEC-04 Production security gap。
- Vite production build 的既有 large-chunk advisory。

New gaps:

- None。

## ATTACH-B04 Readiness

ATTACH-B03-PREVIEW 的 applicable PASS 条件均已满足，且没有未解决的 Blocker / High。

```text
ATTACH-B04 READY: YES
```

本任务到此停止，未开始 ATTACH-B04。
