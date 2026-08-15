import { apiClient } from '../../../api/client/apiClient'
import { decodeBusinessRuleDetail, decodeBusinessRuleWrite, type BusinessRuleDetailResponse, type BusinessRuleWriteResponse, type CreateBusinessRuleInput, type UpdateBusinessRuleInput } from './businessRuleContracts'

export const businessRulesApi = {
  detail: (id: number, signal?: AbortSignal): Promise<BusinessRuleDetailResponse> => apiClient.get(`/business-rules/${id}`, { signal, decode: decodeBusinessRuleDetail }),
  create: (input: CreateBusinessRuleInput): Promise<BusinessRuleWriteResponse> => apiClient.post('/business-rules', input, { decode: decodeBusinessRuleWrite }),
  update: (id: number, input: UpdateBusinessRuleInput): Promise<BusinessRuleWriteResponse> => apiClient.put(`/business-rules/${id}`, input, { decode: decodeBusinessRuleWrite }),
}
