import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import {
  decodeAddEvidence,
  decodeEvidenceDetail,
  decodeEvidenceList,
  type AddEvidenceRequest,
  type AddEvidenceResponse,
  type AddHumanConfirmationRequest,
  type EvidenceDetailResponse,
  type EvidenceListResponse,
  type UpdateEvidenceRequest,
} from './evidenceContracts'

export function getEvidenceDetail(id: number, signal?: AbortSignal): Promise<EvidenceDetailResponse> {
  if (!isSafeApiId(id)) return Promise.reject(new RangeError('证据 ID 无效。'))
  return apiClient.get(`/evidence/${encodeURIComponent(String(id))}`, {
    signal,
    decode: decodeEvidenceDetail,
  })
}

export function getEvidenceList(
  subjectType: string,
  subjectId: number,
  signal?: AbortSignal,
): Promise<EvidenceListResponse> {
  if (!isSafeApiId(subjectId)) return Promise.reject(new RangeError('证据关联对象 ID 无效。'))
  return apiClient.get(`/evidence?subjectType=${encodeURIComponent(subjectType)}&subjectId=${encodeURIComponent(String(subjectId))}`, {
    signal,
    decode: decodeEvidenceList,
  })
}

export function addEvidence(request: AddEvidenceRequest): Promise<AddEvidenceResponse> {
  return apiClient.post('/evidence', request, { decode: decodeAddEvidence })
}

export function updateEvidence(id: number, request: UpdateEvidenceRequest): Promise<EvidenceDetailResponse> {
  if (!isSafeApiId(id)) return Promise.reject(new RangeError('证据 ID 无效。'))
  return apiClient.put(`/evidence/${encodeURIComponent(String(id))}`, request, {
    decode: decodeEvidenceDetail,
  })
}

export function addHumanConfirmation(
  request: AddHumanConfirmationRequest,
): Promise<AddEvidenceResponse> {
  return apiClient.post('/evidence/human-confirmations', request, { decode: decodeAddEvidence })
}
