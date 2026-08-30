# ATTACH-FIX-R01 — Ordinary Attachment Upload Lifecycle Cancellation Verification Report

## Result

```text
ATTACH-FIX-R01 PASS
ORDINARY ATTACHMENT ABORT: PASS
UNMOUNT CLEANUP: PASS
BATCH STOP: PASS
ABORT UX: PASS
EXISTING UPLOAD REGRESSION: PASS
```

**完成时间（UTC+8）**：2026-08-30

本次修复仅针对知识文档普通附件上传生命周期，确保“离开页面会停止上传”的提示与真实行为一致。
目标是只对当前普通附件上传批次建立并管理 `AbortController`，组件销毁时取消未完成上传、停止后续批次请求，并让 UI 及 `uploading-change` 状态正确回退。

## 范围与不做项

- 修改文件（最小闭环）：
  - `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.vue`
  - `src/SystemKnowledgeHub.Web/src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.spec.ts`
- 未改动：
  - API 签名与实现（`uploadKnowledgeDocumentAttachment` 已支持 `AbortSignal`，无需改动）
  - `knowledgeDocumentAttachmentsApi.ts`
  - 后端端点、AttachmentService、验证、存储、revision 语义与 Serilog

## 实现要点

- 在普通附件批量上传开始时创建批次 `AbortController`：
  - `uploadKnowledgeDocumentAttachment(..., batchController.signal)` 传递同一批次同一个 `signal`。
  - 每次新批次（用户重新选择文件）都会新建 controller，不复用上个批次。
- 组件 `onBeforeUnmount` 中：
  - 标记组件销毁状态；
  - `uploadBatchController?.abort()`；
  - 清理 `uploadBatchController`、`activeFileName`；
  - 发出 `uploading-change(false)`；
- 在循环中对 `componentUnmounting` 做短路判断，已销毁时不再：
  - 启动新一项上传；
  - 处理后续 `update:attachments`；
  - 发起后续请求。
- 对 `AbortError` 做区分处理：作为生命周期中止，不走普通错误文案。
- `finally` 保证：
  - `uploading = false`
  - `activeFileName = null`
  - `uploading-change` 终态回落为 `false`。

## 验证结果

## Automated checks

命令与结果：

- `npm run test -- src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.spec.ts src/features/knowledge-documents/editor/KnowledgeDocumentEditor.spec.ts src/features/knowledge-documents/api/knowledgeDocumentAttachmentsApi.spec.ts`
  - PASS（34 tests）
- `npm run type-check`
  - PASS
- `npm run build`
  - PASS（vite build 成功）
- 受影响 ESLint（前端）：`npm run lint -- src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.vue src/features/knowledge-documents/components/KnowledgeDocumentAttachmentArea.spec.ts src/features/knowledge-documents/api/knowledgeDocumentAttachmentsApi.ts src/features/knowledge-documents/api/knowledgeDocumentAttachmentsApi.spec.ts src/features/knowledge-documents/editor/KnowledgeDocumentEditor.spec.ts`
  - PASS（0 errors）
- API 侧相关测试（未改 API 文件）：复用 `knowledgeDocumentAttachmentsApi.spec.ts`
  - PASS

## 关键验收对照

- ORDINARY ATTACHMENT ABORT：上传请求在普通附件组件里明确使用批次 `AbortSignal`，并且同批次共享、跨批次重建。
- UNMOUNT CLEANUP：组件销毁会中止活动批次并清理本地引用；`uploading-change` 最终回落 `false`。
- BATCH STOP：中断前已完成成功项保留；进行中请求终止；后续尚未开始项不会继续触发上传请求。
- ABORT UX：Abort 不显示“上传失败/网络错误”等红色失败消息；取消时无额外 toast，且不会继续 emit 新的待保存集合。
- EXISTING UPLOAD REGRESSION：常规成功/部分失败、前缀与大小预检、重复提交保护、下载路由与既有行为未回归。

## 备注

- 页面离开提示文案：在 `KnowledgeDocumentDetailView.vue` 保持为
  “附件仍在上传。离开编辑会中止本次请求；服务端若已完成上传，文件会保留为未引用附件。未保存内容也会丢失。”
  与本次实现一致，无需最小文案调整。

No backend code, DB migration、或基础架构改动。
