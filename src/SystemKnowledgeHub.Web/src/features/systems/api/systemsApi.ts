import { apiClient } from '../../../api/client/apiClient'
import {
  decodeCreateSystem,
  decodeSystemDetail,
  decodeSystemsList,
  decodeUpdateSystemOverview,
  type CreateSystemRequest,
  type CreateSystemResponse,
  type SystemDetailResponse,
  type SystemsListParameters,
  type SystemsListResponse,
  type UpdateSystemOverviewRequest,
  type UpdateSystemOverviewResponse,
} from './systemsContracts'
import { isSafeApiId } from '../../../api/contracts/id'

export function getSystemsList(
  parameters: SystemsListParameters,
  signal?: AbortSignal,
): Promise<SystemsListResponse> {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
    sort: parameters.sort,
  })

  if (parameters.search) query.set('search', parameters.search)
  if (parameters.lifecycle) query.set('lifecycle', parameters.lifecycle)
  if (parameters.technology) query.set('technology', parameters.technology)
  if (parameters.knowledgeStatus) query.set('knowledgeStatus', parameters.knowledgeStatus)

  return apiClient.get(`/systems?${query.toString()}`, {
    signal,
    decode: decodeSystemsList,
  })
}

export function createSystem(request: CreateSystemRequest): Promise<CreateSystemResponse> {
  return apiClient.post('/systems', request, { decode: decodeCreateSystem })
}

export function getSystemDetail(
  id: number,
  signal?: AbortSignal,
): Promise<SystemDetailResponse> {
  if (!isSafeApiId(id)) {
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  }

  return apiClient.get(`/systems/${encodeURIComponent(String(id))}`, {
    signal,
    decode: decodeSystemDetail,
  })
}

export function updateSystemOverview(
  id: number,
  request: UpdateSystemOverviewRequest,
): Promise<UpdateSystemOverviewResponse> {
  if (!isSafeApiId(id)) {
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  }

  return apiClient.put(`/systems/${encodeURIComponent(String(id))}/overview`, request, {
    decode: decodeUpdateSystemOverview,
  })
}
