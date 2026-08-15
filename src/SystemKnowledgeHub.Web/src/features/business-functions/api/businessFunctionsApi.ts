import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import {
  decodeCreateBusinessFunction,
  decodeBusinessFunctionDetail,
  decodeBusinessFunctionsList,
  decodeReplaceBusinessProcessSteps,
  decodeUpdateBusinessFunctionOverview,
  type BusinessFunctionDetailResponse,
  type BusinessFunctionsListParameters,
  type BusinessFunctionsListResponse,
  type CreateBusinessFunctionRequest,
  type CreateBusinessFunctionResponse,
  type ReplaceBusinessProcessStepsRequest,
  type ReplaceBusinessProcessStepsResponse,
  type UpdateBusinessFunctionOverviewRequest,
  type UpdateBusinessFunctionOverviewResponse,
} from './businessFunctionContracts'

export function getBusinessFunctionsList(
  parameters: BusinessFunctionsListParameters,
  signal?: AbortSignal,
): Promise<BusinessFunctionsListResponse> {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
    sort: parameters.sort,
  })

  if (parameters.systemId) query.set('systemId', String(parameters.systemId))
  if (parameters.search) query.set('search', parameters.search)
  if (parameters.functionType) query.set('functionType', parameters.functionType)
  if (parameters.rewriteStatus) query.set('rewriteStatus', parameters.rewriteStatus)
  if (parameters.knowledgeStatus) query.set('knowledgeStatus', parameters.knowledgeStatus)
  if (parameters.hasUnknownItems !== undefined) query.set('hasUnknownItems', String(parameters.hasUnknownItems))

  return apiClient.get(`/business-functions?${query.toString()}`, {
    signal,
    decode: decodeBusinessFunctionsList,
  })
}

export function getBusinessFunctionDetail(
  id: number,
  signal?: AbortSignal,
): Promise<BusinessFunctionDetailResponse> {
  if (!isSafeApiId(id)) {
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  }

  return apiClient.get(`/business-functions/${encodeURIComponent(String(id))}`, {
    signal,
    decode: decodeBusinessFunctionDetail,
  })
}

export function createBusinessFunction(
  request: CreateBusinessFunctionRequest,
): Promise<CreateBusinessFunctionResponse> {
  return apiClient.post('/business-functions', request, { decode: decodeCreateBusinessFunction })
}

export function updateBusinessFunctionOverview(
  id: number,
  request: UpdateBusinessFunctionOverviewRequest,
): Promise<UpdateBusinessFunctionOverviewResponse> {
  if (!isSafeApiId(id)) {
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  }

  return apiClient.put(`/business-functions/${encodeURIComponent(String(id))}/overview`, request, {
    decode: decodeUpdateBusinessFunctionOverview,
  })
}

export function replaceBusinessProcessSteps(
  id: number,
  request: ReplaceBusinessProcessStepsRequest,
): Promise<ReplaceBusinessProcessStepsResponse> {
  if (!isSafeApiId(id)) {
    return Promise.reject(new RangeError('ID 必须是 JavaScript 安全范围内的正整数。'))
  }

  return apiClient.put(`/business-functions/${encodeURIComponent(String(id))}/process-steps`, request, {
    decode: decodeReplaceBusinessProcessSteps,
  })
}
