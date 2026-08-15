import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import { decodeAddRelationship, decodeDescription, decodeRelationshipDetail, decodeStatusChange, decodeTargets, type AddRelationshipRequest, type AddRelationshipResponse, type ChangeRelationshipStatusRequest, type KnowledgeTargetsResponse, type RelationType, type RelationshipDetailResponse, type UpdateRelationshipDescriptionRequest } from './relationshipContracts'

export function searchRelationshipTargets(p:{systemId:number;sourceType:string;sourceId:number;relationType:RelationType;q:string},signal?:AbortSignal):Promise<KnowledgeTargetsResponse>{const query=new URLSearchParams({purpose:'RelationTarget',systemId:String(p.systemId),sourceType:p.sourceType,sourceId:String(p.sourceId),relationType:p.relationType,q:p.q,page:'1',pageSize:'20'});return apiClient.get(`/knowledge-targets?${query}`,{signal,decode:decodeTargets})}
export function addRelationship(request:AddRelationshipRequest):Promise<AddRelationshipResponse>{return apiClient.post('/relationships',request,{decode:decodeAddRelationship})}
export function getRelationshipDetail(id:number,signal?:AbortSignal):Promise<RelationshipDetailResponse>{if(!isSafeApiId(id))return Promise.reject(new RangeError('关系 ID 无效。'));return apiClient.get(`/relationships/${id}`,{signal,decode:decodeRelationshipDetail})}
export function updateRelationshipDescription(id:number,request:UpdateRelationshipDescriptionRequest){return apiClient.put(`/relationships/${id}/description`,request,{decode:decodeDescription})}
export function changeRelationshipStatus(id:number,request:ChangeRelationshipStatusRequest){return apiClient.put(`/relationships/${id}/knowledge-status`,request,{decode:decodeStatusChange})}
