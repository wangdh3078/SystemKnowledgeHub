import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
export { isSafeApiId, parseSafeApiId } from '../../../api/contracts/id'
import {
  decodeDatabaseColumnDetail,
  decodeDatabaseObjectDetail,
  decodeDatabaseObjectsList,
  decodeCreateDatabaseSource,
  decodeAddColumnKnownValue,
  decodeDatabaseColumnKnowledge,
  decodeDatabaseObjectKnowledge,
  decodeRegisterDatabaseObject,
  decodeRegisterDatabaseColumn,
  decodeRemoveColumnKnownValue,
  type AddColumnKnownValueRequest,
  type AddColumnKnownValueResponse,
  type CreateDatabaseSourceRequest,
  type CreateDatabaseSourceResponse,
  type DatabaseColumnDetailResponse,
  type DatabaseObjectDetailResponse,
  type DatabaseObjectsListParameters,
  type DatabaseObjectsListResponse,
  type DatabaseColumnKnowledgeResponse,
  type DatabaseObjectKnowledgeResponse,
  type RegisterDatabaseObjectRequest,
  type RegisterDatabaseObjectResponse,
  type RegisterDatabaseColumnRequest,
  type RegisterDatabaseColumnResponse,
  type RemoveColumnKnownValueRequest,
  type RemoveColumnKnownValueResponse,
  type UpdateDatabaseColumnKnowledgeRequest,
  type UpdateDatabaseObjectKnowledgeRequest,
} from './databaseKnowledgeContracts'

export function getDatabaseObjectsList(
  parameters: DatabaseObjectsListParameters,
  signal?: AbortSignal,
): Promise<DatabaseObjectsListResponse> {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
    sort: parameters.sort,
  })

  if (parameters.systemId !== undefined) {
    if (!isSafeApiId(parameters.systemId)) {
      return Promise.reject(new RangeError('系统 ID 必须是 JavaScript 安全范围内的正整数。'))
    }
    query.set('systemId', String(parameters.systemId))
  }
  if (parameters.databaseSourceId !== undefined) {
    if (!isSafeApiId(parameters.databaseSourceId)) {
      return Promise.reject(new RangeError('数据库来源 ID 必须是 JavaScript 安全范围内的正整数。'))
    }
    query.set('databaseSourceId', String(parameters.databaseSourceId))
  }
  if (parameters.schema) query.set('schema', parameters.schema)
  if (parameters.objectType) query.set('objectType', parameters.objectType)
  if (parameters.knowledgeStatus) query.set('knowledgeStatus', parameters.knowledgeStatus)
  if (parameters.search) query.set('search', parameters.search)

  return apiClient.get(`/database-objects?${query.toString()}`, {
    signal,
    decode: decodeDatabaseObjectsList,
  })
}

export function createDatabaseSource(
  request: CreateDatabaseSourceRequest,
): Promise<CreateDatabaseSourceResponse> {
  if (!isSafeApiId(request.systemId)) {
    return Promise.reject(new RangeError('系统 ID 必须是 JavaScript 安全范围内的正整数。'))
  }
  return apiClient.post('/database-sources', request, { decode: decodeCreateDatabaseSource })
}

export function registerDatabaseObject(
  request: RegisterDatabaseObjectRequest,
): Promise<RegisterDatabaseObjectResponse> {
  if (!isSafeApiId(request.databaseSourceId)) {
    return Promise.reject(new RangeError('数据库来源 ID 必须是 JavaScript 安全范围内的正整数。'))
  }
  return apiClient.post('/database-objects', request, { decode: decodeRegisterDatabaseObject })
}

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

export function deleteDatabaseSource(id: number, concurrencyToken: string): Promise<void> {
  if (!isSafeApiId(id)) return Promise.reject(new RangeError('数据库来源 ID 无效。'))
  return apiClient.deleteWithBody(`/database-sources/${encodeURIComponent(String(id))}`, { concurrencyToken }, { decode: () => undefined })
}

export function deleteDatabaseObject(id: number, concurrencyToken: string): Promise<void> {
  if (!isSafeApiId(id)) return Promise.reject(new RangeError('数据库对象 ID 无效。'))
  return apiClient.deleteWithBody(`/database-objects/${encodeURIComponent(String(id))}`, { concurrencyToken }, { decode: () => undefined })
}

export function deleteDatabaseColumn(id: number, concurrencyToken: string): Promise<void> {
  if (!isSafeApiId(id)) return Promise.reject(new RangeError('数据库字段 ID 无效。'))
  return apiClient.deleteWithBody(`/database-columns/${encodeURIComponent(String(id))}`, { concurrencyToken }, { decode: () => undefined })
}

export function registerDatabaseColumn(
  databaseObjectId: number,
  request: RegisterDatabaseColumnRequest,
): Promise<RegisterDatabaseColumnResponse> {
  if (!isSafeApiId(databaseObjectId)) return Promise.reject(new RangeError('数据库对象 ID 无效。'))
  return apiClient.post(`/database-objects/${encodeURIComponent(String(databaseObjectId))}/columns`, request, {
    decode: decodeRegisterDatabaseColumn,
  })
}

export function updateDatabaseObjectKnowledge(
  databaseObjectId: number,
  request: UpdateDatabaseObjectKnowledgeRequest,
): Promise<DatabaseObjectKnowledgeResponse> {
  if (!isSafeApiId(databaseObjectId)) return Promise.reject(new RangeError('数据库对象 ID 无效。'))
  return apiClient.put(`/database-objects/${encodeURIComponent(String(databaseObjectId))}/knowledge`, request, {
    decode: decodeDatabaseObjectKnowledge,
  })
}

export function updateDatabaseColumnKnowledge(
  databaseColumnId: number,
  request: UpdateDatabaseColumnKnowledgeRequest,
): Promise<DatabaseColumnKnowledgeResponse> {
  if (!isSafeApiId(databaseColumnId)) return Promise.reject(new RangeError('数据库字段 ID 无效。'))
  return apiClient.put(`/database-columns/${encodeURIComponent(String(databaseColumnId))}/knowledge`, request, {
    decode: decodeDatabaseColumnKnowledge,
  })
}

export function addColumnKnownValue(
  databaseColumnId: number,
  request: AddColumnKnownValueRequest,
): Promise<AddColumnKnownValueResponse> {
  if (!isSafeApiId(databaseColumnId)) return Promise.reject(new RangeError('数据库字段 ID 无效。'))
  return apiClient.post(`/database-columns/${encodeURIComponent(String(databaseColumnId))}/known-values`, request, {
    decode: decodeAddColumnKnownValue,
  })
}

export function removeColumnKnownValue(
  databaseColumnId: number,
  knownValueId: number,
  request: RemoveColumnKnownValueRequest,
): Promise<RemoveColumnKnownValueResponse> {
  if (!isSafeApiId(databaseColumnId) || !isSafeApiId(knownValueId)) {
    return Promise.reject(new RangeError('数据库字段或已知值 ID 无效。'))
  }
  return apiClient.post(
    `/database-columns/${encodeURIComponent(String(databaseColumnId))}/known-values/${encodeURIComponent(String(knownValueId))}/remove`,
    request,
    { decode: decodeRemoveColumnKnownValue },
  )
}
