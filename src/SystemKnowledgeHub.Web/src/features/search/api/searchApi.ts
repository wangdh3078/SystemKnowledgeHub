import { apiClient } from '../../../api/client/apiClient'
import { decodeSearchKnowledge, type SearchKnowledgeRequest, type SearchKnowledgeResponse } from './searchContracts'

export function searchKnowledge(
  request: SearchKnowledgeRequest,
  signal?: AbortSignal,
): Promise<SearchKnowledgeResponse> {
  const query = request.query.trim()
  if (query.length < 1 || query.length > 100) {
    return Promise.reject(new RangeError('搜索关键词长度必须在 1 到 100 个字符之间。'))
  }

  const limitPerGroup = request.limitPerGroup ?? 5
  if (!Number.isInteger(limitPerGroup) || limitPerGroup < 1 || limitPerGroup > 20) {
    return Promise.reject(new RangeError('每个分组的结果数量必须在 1 到 20 之间。'))
  }

  const parameters = new URLSearchParams({ q: query, limitPerGroup: String(limitPerGroup) })
  if (request.types && request.types.length > 0) {
    parameters.set('types', request.types.join(','))
  }

  return apiClient.get(`/search?${parameters.toString()}`, { signal, decode: decodeSearchKnowledge })
}
