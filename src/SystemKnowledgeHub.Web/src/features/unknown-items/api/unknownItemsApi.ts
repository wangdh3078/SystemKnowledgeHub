import { apiClient } from '../../../api/client/apiClient'
type ApiId = number
import type {
  AddEvidenceResponse, AddFindingResponse, CreateUnknownItemInput, CreateUnknownItemResponse,
  ApplyKnowledgeUpdateResponse, KnowledgeUpdateDraft, PersonSnapshotInput, ReopenUnknownItemResponse,
  SaveResolutionResponse, UnknownItemDetailResponse, UnknownItemsListParams, UnknownItemsListResponse,
  UnknownTarget, WorkflowResponse,
} from './unknownItemContracts'

function queryString(params: UnknownItemsListParams): string {
  const query = new URLSearchParams()
  for (const [key, value] of Object.entries(params)) if (value !== undefined && value !== '') query.set(key, String(value))
  const text = query.toString()
  return text ? `?${text}` : ''
}

function decodeObject<T>(payload: unknown): T {
  if (typeof payload !== 'object' || payload === null || Array.isArray(payload)) {
    throw new TypeError('API 响应必须是 JSON Object。')
  }
  return payload as T
}

export const unknownItemsApi = {
  list: (params: UnknownItemsListParams) => apiClient.get(`/unknown-items${queryString(params)}`, { decode: decodeObject<UnknownItemsListResponse> }),
  detail: (id: ApiId) => apiClient.get(`/unknown-items/${id}`, { decode: decodeObject<UnknownItemDetailResponse> }),
  create: (input: CreateUnknownItemInput) => apiClient.post('/unknown-items', input, { decode: decodeObject<CreateUnknownItemResponse> }),
  start: (id: ApiId, actor: PersonSnapshotInput, concurrencyToken: string) =>
    apiClient.post(`/unknown-items/${id}/start-investigation`, { actor, concurrencyToken }, { decode: decodeObject<WorkflowResponse> }),
  addFinding: (id: ApiId, content: string, recorder: PersonSnapshotInput, concurrencyToken: string) =>
    apiClient.post(`/unknown-items/${id}/findings`, { content, recorder, concurrencyToken }, { decode: decodeObject<AddFindingResponse> }),
  addEvidence: (id: ApiId, input: Record<string, unknown>) =>
    apiClient.post(`/unknown-items/${id}/evidence`, input, { decode: decodeObject<AddEvidenceResponse> }),
  saveResolution: (id: ApiId, conclusion: string, knowledgeUpdates: KnowledgeUpdateDraft[], actor: PersonSnapshotInput, concurrencyToken: string) =>
    apiClient.put(`/unknown-items/${id}/resolution`, { conclusion, knowledgeUpdates, actor, concurrencyToken }, { decode: decodeObject<SaveResolutionResponse> }),
  applyColumnKnownValue: (id: ApiId, updateId: ApiId, input: Record<string, unknown>) =>
    apiClient.post(`/unknown-items/${id}/knowledge-updates/${updateId}/apply-column-known-value`, input, { decode: decodeObject<ApplyKnowledgeUpdateResponse> }),
  applyColumnKnowledge: (id: ApiId, updateId: ApiId, input: Record<string, unknown>) =>
    apiClient.post(`/unknown-items/${id}/knowledge-updates/${updateId}/apply-column-knowledge`, input, { decode: decodeObject<ApplyKnowledgeUpdateResponse> }),
  applyBusinessFunction: (id: ApiId, updateId: ApiId, input: Record<string, unknown>) =>
    apiClient.post(`/unknown-items/${id}/knowledge-updates/${updateId}/apply-business-function`, input, { decode: decodeObject<ApplyKnowledgeUpdateResponse> }),
  applyBusinessRule: (id: ApiId, updateId: ApiId, input: Record<string, unknown>) =>
    apiClient.post(`/unknown-items/${id}/knowledge-updates/${updateId}/apply-business-rule`, input, { decode: decodeObject<ApplyKnowledgeUpdateResponse> }),
  confirmConclusion: (id: ApiId, confirmer: PersonSnapshotInput, concurrencyToken: string) =>
    apiClient.post(`/unknown-items/${id}/confirm-conclusion`, { confirmer, concurrencyToken }, { decode: decodeObject<WorkflowResponse> }),
  close: (id: ApiId, closeNote: string | null, actor: PersonSnapshotInput, concurrencyToken: string) =>
    apiClient.post(`/unknown-items/${id}/close`, { closeNote, actor, concurrencyToken }, { decode: decodeObject<WorkflowResponse> }),
  reopen: (id: ApiId, reason: string, actor: PersonSnapshotInput, concurrencyToken: string) =>
    apiClient.post(`/unknown-items/${id}/reopen`, { reason, actor, concurrencyToken }, { decode: decodeObject<ReopenUnknownItemResponse> }),
}

export type InvestigationEvidenceInput = {
  evidenceType: string; subject: UnknownTarget; subjectDetailKey: string | null; sourceTitle: string
  sourceReference: string | null; sourceLocator: Record<string, unknown> | null; summary: string | null
  supportReason: string; confidence: string | null; provider: PersonSnapshotInput; concurrencyToken: string
}
