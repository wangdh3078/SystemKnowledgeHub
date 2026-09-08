import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import { decodeAnalysisMutation, decodeAnalysisTree } from './analysisWorkspaceContracts'

interface TreeWrite {
  readonly treeConcurrencyToken: string
}
interface NodeWrite extends TreeWrite {
  readonly nodeConcurrencyToken: string
}
interface CreateFolder extends TreeWrite {
  readonly parentId: number | null
  readonly title: string
}
function id(value: number): string {
  if (!isSafeApiId(value)) throw new RangeError('分析节点 ID 无效。')
  return String(value)
}
const mutation = { decode: decodeAnalysisMutation }
export const getAnalysisTree = () => apiClient.get('/analysis/tree', { decode: decodeAnalysisTree })
export const createAnalysisFolder = (request: CreateFolder) =>
  apiClient.post('/analysis/folders', request, mutation)
export const addAnalysisPlacement = (
  request: TreeWrite & { readonly parentId: number | null; readonly knowledgeDocumentId: number },
) => apiClient.post('/analysis/document-placements', request, mutation)
export const createAnalysisDocument = (
  request: CreateFolder & { readonly documentType: 'DesignNote' | 'KnowledgeArticle' },
) => apiClient.post('/analysis/documents', request, mutation)
export const renameAnalysisFolder = (
  nodeId: number,
  request: NodeWrite & { readonly title: string },
) => apiClient.put(`/analysis/folders/${id(nodeId)}/title`, request, mutation)
export const moveAnalysisNode = (
  nodeId: number,
  request: NodeWrite & { readonly targetParentId: number | null; readonly targetPosition: number },
) => apiClient.post(`/analysis/nodes/${id(nodeId)}/move`, request, mutation)
export const reorderAnalysisChildren = (
  request: TreeWrite & {
    readonly parentId: number | null
    readonly items: readonly { readonly id: number; readonly nodeConcurrencyToken: string }[]
  },
) => apiClient.put('/analysis/children/order', request, mutation)
export const removeAnalysisPlacement = (nodeId: number, request: NodeWrite) =>
  apiClient.deleteWithBody(`/analysis/document-placements/${id(nodeId)}`, request, mutation)
export const deleteAnalysisFolder = (nodeId: number, request: NodeWrite) =>
  apiClient.deleteWithBody(`/analysis/folders/${id(nodeId)}`, request, mutation)
