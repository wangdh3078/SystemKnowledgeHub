import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import { decodeImpactResponse, type ImpactResponse } from './impactContracts'

export function getKnowledgeDocumentImpact(
  id: number,
  page = 1,
  pageSize = 20,
  signal?: AbortSignal,
): Promise<ImpactResponse> {
  if (!isSafeApiId(id))
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  if (!isSafeApiId(page))
    return Promise.reject(new RangeError('页码必须是 JavaScript 安全范围内的正整数。'))
  if (!Number.isSafeInteger(pageSize) || pageSize < 1 || pageSize > 100)
    return Promise.reject(new RangeError('每页数量必须是 1 到 100 之间的整数。'))
  return apiClient.get(
    `/knowledge-documents/${encodeURIComponent(String(id))}/traceability/impact?page=${page}&pageSize=${pageSize}`,
    { signal, decode: decodeImpactResponse },
  )
}
