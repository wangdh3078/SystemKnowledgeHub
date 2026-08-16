import { apiClient } from '../../../api/client/apiClient'
import { decodeIntegrationContractFields, decodeIntegrationDetail, decodeIntegrationWrite, type IntegrationContractField, type IntegrationContractFieldsResponse, type IntegrationDetailResponse, type IntegrationWriteInput, type IntegrationWriteResponse } from './integrationContracts'

export const integrationsApi={
  detail:(id:number,signal?:AbortSignal):Promise<IntegrationDetailResponse>=>apiClient.get(`/integrations/${id}`,{signal,decode:decodeIntegrationDetail}),
  create:(input:IntegrationWriteInput):Promise<IntegrationWriteResponse>=>apiClient.post('/integrations',input,{decode:decodeIntegrationWrite}),
  updateOverview:(id:number,input:IntegrationWriteInput&{concurrencyToken:string}):Promise<IntegrationWriteResponse>=>apiClient.put(`/integrations/${id}/overview`,input,{decode:decodeIntegrationWrite}),
  replaceContractFields:(id:number,input:{fields:readonly IntegrationContractField[];actor:IntegrationWriteInput['actor'];concurrencyToken:string}):Promise<IntegrationContractFieldsResponse>=>apiClient.put(`/integrations/${id}/contract-fields`,input,{decode:decodeIntegrationContractFields}),
}
