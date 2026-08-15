import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import {
  decodeAddEvidence,
  decodeEvidenceDetail,
  type AddEvidenceRequest,
  type AddEvidenceResponse,
  type AddHumanConfirmationRequest,
  type EvidenceDetailResponse,
  type UpdateEvidenceRequest,
} from './evidenceContracts'

export function getEvidenceDetail(id: number, signal?: AbortSignal): Promise<EvidenceDetailResponse> {
  if (!isSafeApiId(id)) return Promise.reject(new RangeError('证据 ID 无效。'))
  return apiClient.get(`/evidence/${encodeURIComponent(String(id))}`, {
    signal,
    decode: decodeEvidenceDetail,
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
