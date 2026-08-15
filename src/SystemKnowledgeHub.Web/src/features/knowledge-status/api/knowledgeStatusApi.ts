import { apiClient } from '../../../api/client/apiClient'
import {
  decodeKnowledgeStatusChange,
  type ChangeKnowledgeStatusRequest,
  type ChangeKnowledgeStatusResponse,
} from './knowledgeStatusContracts'

export function changeKnowledgeStatus(
  request: ChangeKnowledgeStatusRequest,
): Promise<ChangeKnowledgeStatusResponse> {
  return apiClient.put('/knowledge-status', request, { decode: decodeKnowledgeStatusChange })
}
