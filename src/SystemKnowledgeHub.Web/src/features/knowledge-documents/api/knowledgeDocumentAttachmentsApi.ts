import { environment } from '../../../app/config/env'
import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import { decodeAttachmentMetadata, type AttachmentMetadata } from './attachmentContracts'

export function uploadKnowledgeDocumentAttachment(
  knowledgeDocumentId: number,
  file: File,
  signal?: AbortSignal,
): Promise<AttachmentMetadata> {
  if (!isSafeApiId(knowledgeDocumentId)) {
    return Promise.reject(new RangeError('知识内容 ID 无效。'))
  }
  const form = new FormData()
  form.append('file', file, file.name)
  return apiClient.postForm(
    `/knowledge-documents/${encodeURIComponent(String(knowledgeDocumentId))}/attachments`,
    form,
    { signal, decode: decodeAttachmentMetadata },
  )
}

export function uploadKnowledgeDocumentImage(
  knowledgeDocumentId: number,
  file: File,
  signal?: AbortSignal,
): Promise<AttachmentMetadata> {
  return uploadKnowledgeDocumentAttachment(knowledgeDocumentId, file, signal)
}

export function knowledgeDocumentAttachmentDownloadUrl(
  knowledgeDocumentId: number,
  attachmentId: number,
  revisionNumber?: number,
): string {
  if (!isSafeApiId(knowledgeDocumentId) || !isSafeApiId(attachmentId)) {
    throw new RangeError('知识内容或附件 ID 无效。')
  }
  const documentSegment = encodeURIComponent(String(knowledgeDocumentId))
  const attachmentSegment = encodeURIComponent(String(attachmentId))
  if (revisionNumber === undefined) {
    return `${environment.apiBaseUrl}/knowledge-documents/${documentSegment}/attachments/${attachmentSegment}/download`
  }
  if (!isSafeApiId(revisionNumber)) throw new RangeError('修订号无效。')
  return `${environment.apiBaseUrl}/knowledge-documents/${documentSegment}/revisions/${encodeURIComponent(String(revisionNumber))}/attachments/${attachmentSegment}/download`
}

export function knowledgeDocumentImageContentUrl(
  knowledgeDocumentId: number,
  attachmentId: number,
  revisionNumber?: number,
): string {
  if (!isSafeApiId(knowledgeDocumentId) || !isSafeApiId(attachmentId)) {
    throw new RangeError('知识内容或附件 ID 无效。')
  }
  const documentSegment = encodeURIComponent(String(knowledgeDocumentId))
  const attachmentSegment = encodeURIComponent(String(attachmentId))
  if (revisionNumber === undefined) {
    return `${environment.apiBaseUrl}/knowledge-documents/${documentSegment}/attachments/${attachmentSegment}/content`
  }
  if (!isSafeApiId(revisionNumber)) throw new RangeError('修订号无效。')
  return `${environment.apiBaseUrl}/knowledge-documents/${documentSegment}/revisions/${encodeURIComponent(String(revisionNumber))}/attachments/${attachmentSegment}/content`
}
