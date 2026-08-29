import { environment } from '../../../app/config/env'
import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import { UnexpectedResponseError } from '../../../api/errors/ApiError'
import {
  decodeAttachmentJsonPreview,
  decodeAttachmentMetadata,
  type AttachmentJsonPreview,
  type AttachmentMetadata,
} from './attachmentContracts'

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

export function knowledgeDocumentAttachmentPreviewPath(
  knowledgeDocumentId: number,
  attachmentId: number,
  revisionNumber?: number,
  sheet?: string,
): string {
  if (!isSafeApiId(knowledgeDocumentId) || !isSafeApiId(attachmentId)) {
    throw new RangeError('知识内容或附件 ID 无效。')
  }
  if (revisionNumber !== undefined && !isSafeApiId(revisionNumber)) {
    throw new RangeError('修订号无效。')
  }
  const documentSegment = encodeURIComponent(String(knowledgeDocumentId))
  const attachmentSegment = encodeURIComponent(String(attachmentId))
  const context =
    revisionNumber === undefined
      ? `/knowledge-documents/${documentSegment}/attachments/${attachmentSegment}/preview`
      : `/knowledge-documents/${documentSegment}/revisions/${encodeURIComponent(String(revisionNumber))}/attachments/${attachmentSegment}/preview`
  return sheet === undefined ? context : `${context}?sheet=${encodeURIComponent(sheet)}`
}

export function getKnowledgeDocumentAttachmentPreview(
  knowledgeDocumentId: number,
  attachmentId: number,
  revisionNumber?: number,
  sheet?: string,
  signal?: AbortSignal,
): Promise<AttachmentJsonPreview> {
  let path: string
  try {
    path = knowledgeDocumentAttachmentPreviewPath(
      knowledgeDocumentId,
      attachmentId,
      revisionNumber,
      sheet,
    )
  } catch (error: unknown) {
    return Promise.reject(error)
  }
  return apiClient.get(path, { signal, decode: decodeAttachmentJsonPreview })
}

export async function getKnowledgeDocumentPdfPreview(
  knowledgeDocumentId: number,
  attachmentId: number,
  revisionNumber?: number,
  signal?: AbortSignal,
): Promise<Blob> {
  let path: string
  try {
    path = knowledgeDocumentAttachmentPreviewPath(knowledgeDocumentId, attachmentId, revisionNumber)
  } catch (error: unknown) {
    return Promise.reject(error)
  }
  const blob = await apiClient.getBlob(path, {
    signal,
    headers: { Accept: 'application/pdf' },
  })
  if (blob.type.toLowerCase() !== 'application/pdf') {
    throw new UnexpectedResponseError('服务器返回的 PDF 预览类型不符合预期。')
  }
  return blob
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
