import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
export { isSafeApiId, parseSafeApiId } from '../../../api/contracts/id'
import {
  decodeDatabaseColumnDetail,
  decodeDatabaseObjectDetail,
  type DatabaseColumnDetailResponse,
  type DatabaseObjectDetailResponse,
} from './databaseKnowledgeContracts'

export function getDatabaseObjectDetail(
  id: number,
  selectedColumnId?: number,
  signal?: AbortSignal,
): Promise<DatabaseObjectDetailResponse> {
  if (!isSafeApiId(id) || (selectedColumnId !== undefined && !isSafeApiId(selectedColumnId))) {
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  }

  const query =
    selectedColumnId === undefined
      ? ''
      : `?selectedColumnId=${encodeURIComponent(String(selectedColumnId))}`
  return apiClient.get(`/database-objects/${encodeURIComponent(String(id))}${query}`, {
    signal,
    decode: decodeDatabaseObjectDetail,
  })
}

export function getDatabaseColumnDetail(
  id: number,
  signal?: AbortSignal,
): Promise<DatabaseColumnDetailResponse> {
  if (!isSafeApiId(id)) {
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  }
  return apiClient.get(`/database-columns/${encodeURIComponent(String(id))}`, {
    signal,
    decode: decodeDatabaseColumnDetail,
  })
}
