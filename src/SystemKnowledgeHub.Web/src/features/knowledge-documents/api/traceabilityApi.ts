import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import {
  decodeTraceabilityResponse,
  type TraceabilityResponse,
} from './traceabilityContracts'

export function getKnowledgeDocumentTraceability(
  id: number,
  signal?: AbortSignal,
): Promise<TraceabilityResponse> {
  if (!isSafeApiId(id))
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  return apiClient.get(`/knowledge-documents/${encodeURIComponent(String(id))}/traceability`, {
    signal,
    decode: decodeTraceabilityResponse,
  })
}
