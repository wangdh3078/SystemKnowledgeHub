import { isSafeApiId } from '../../../api/contracts/id'
import { apiClient } from '../../../api/client/apiClient'
import {
  decodeKnowledgeDocumentDetail,
  decodeKnowledgeDocumentRevisionDetail,
  decodeKnowledgeDocumentRevisionList,
  decodeKnowledgeDocumentsList,
  type CreateKnowledgeDocumentRequest,
  type DocumentLifecycleStatus,
  type KnowledgeDocumentDetail,
  type KnowledgeDocumentRevisionDetail,
  type KnowledgeDocumentRevisionListResponse,
  type KnowledgeDocumentListParameters,
  type KnowledgeDocumentsListResponse,
  type UpdateKnowledgeDocumentContentRequest,
} from './knowledgeDocumentContracts'

const maximumRevisionPageSize = 100

export function getKnowledgeDocuments(
  parameters: KnowledgeDocumentListParameters,
  signal?: AbortSignal,
): Promise<KnowledgeDocumentsListResponse> {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
    sort: 'updatedAt:desc',
  })
  if (parameters.query) query.set('query', parameters.query)
  if (parameters.documentType) query.set('documentType', parameters.documentType)
  if (parameters.lifecycleStatus) query.set('lifecycleStatus', parameters.lifecycleStatus)
  if (parameters.knowledgeStatus) query.set('knowledgeStatus', parameters.knowledgeStatus)
  return apiClient.get(`/knowledge-documents?${query.toString()}`, {
    signal,
    decode: decodeKnowledgeDocumentsList,
  })
}

export function getKnowledgeDocument(
  id: number,
  signal?: AbortSignal,
): Promise<KnowledgeDocumentDetail> {
  if (!isSafeApiId(id))
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  return apiClient.get(`/knowledge-documents/${encodeURIComponent(String(id))}`, {
    signal,
    decode: decodeKnowledgeDocumentDetail,
  })
}

export function listKnowledgeDocumentRevisions(
  id: number,
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
): Promise<KnowledgeDocumentRevisionListResponse> {
  if (!isSafeApiId(id))
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  if (!Number.isSafeInteger(page) || page < 1)
    return Promise.reject(new RangeError('页码必须从 1 开始。'))
  if (!Number.isSafeInteger(pageSize) || pageSize < 1 || pageSize > maximumRevisionPageSize)
    return Promise.reject(new RangeError('每页数量必须在 1 到 100 之间。'))
  const query = new URLSearchParams({ page: String(page), pageSize: String(pageSize) })
  return apiClient.get(
    `/knowledge-documents/${encodeURIComponent(String(id))}/revisions?${query.toString()}`,
    { signal, decode: decodeKnowledgeDocumentRevisionList },
  )
}

export function getKnowledgeDocumentRevision(
  id: number,
  revisionNumber: number,
  signal?: AbortSignal,
): Promise<KnowledgeDocumentRevisionDetail> {
  if (!isSafeApiId(id) || !isSafeApiId(revisionNumber))
    return Promise.reject(new RangeError('ID 与修订号必须是 JavaScript 安全范围内的正整数。'))
  return apiClient.get(
    `/knowledge-documents/${encodeURIComponent(String(id))}/revisions/${encodeURIComponent(String(revisionNumber))}`,
    { signal, decode: decodeKnowledgeDocumentRevisionDetail },
  )
}

export function createKnowledgeDocument(
  request: CreateKnowledgeDocumentRequest,
): Promise<KnowledgeDocumentDetail> {
  return apiClient.post('/knowledge-documents', request, { decode: decodeKnowledgeDocumentDetail })
}

export function updateKnowledgeDocumentContent(
  id: number,
  request: UpdateKnowledgeDocumentContentRequest,
): Promise<KnowledgeDocumentDetail> {
  if (!isSafeApiId(id)) {
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  }
  return apiClient.put(`/knowledge-documents/${encodeURIComponent(String(id))}/content`, request, {
    decode: decodeKnowledgeDocumentDetail,
  })
}

export function updateKnowledgeDocumentLifecycle(
  id: number,
  targetLifecycleStatus: DocumentLifecycleStatus,
  concurrencyToken: string,
): Promise<KnowledgeDocumentDetail> {
  if (!isSafeApiId(id))
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  return apiClient.put(
    `/knowledge-documents/${encodeURIComponent(String(id))}/lifecycle`,
    { targetLifecycleStatus, concurrencyToken },
    { decode: decodeKnowledgeDocumentDetail },
  )
}
