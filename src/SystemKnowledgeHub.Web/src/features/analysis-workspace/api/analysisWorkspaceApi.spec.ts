import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '../../../api/client/apiClient'
import * as api from './analysisWorkspaceApi'
import {
  decodeAnalysisMutation,
  decodeAnalysisTree,
  type AnalysisNode,
} from './analysisWorkspaceContracts'

vi.mock('../../../api/client/apiClient', () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), deleteWithBody: vi.fn() },
}))
const token = `a1.${'A'.repeat(64)}`
const folder: AnalysisNode = {
  id: 11,
  parentId: null,
  nodeType: 'Folder',
  title: 'MES',
  knowledgeDocumentId: null,
  documentType: null,
  lifecycleStatus: null,
  availability: 'Available',
  concurrencyToken: 'node-11',
  sortOrder: 0,
  createdAt: '2026-09-08T00:00:00Z',
  updatedAt: '2026-09-08T00:00:00Z',
}
const document: AnalysisNode = {
  ...folder,
  id: 12,
  parentId: 11,
  nodeType: 'Document',
  title: '分析',
  knowledgeDocumentId: 101,
  documentType: 'DesignNote',
  lifecycleStatus: 'Archived',
}
const tree = { items: [folder, document], treeConcurrencyToken: token }
describe('Analysis wire contract', () => {
  beforeEach(() => vi.clearAllMocks())
  it('decodes the bounded flat tree and mutation including Archived and unavailable nodes', () => {
    expect(decodeAnalysisTree(tree)).toEqual(tree)
    expect(decodeAnalysisMutation({ ...tree, node: document }).node?.knowledgeDocumentId).toBe(101)
    expect(decodeAnalysisMutation({ ...tree, node: null }).node).toBeNull()
    expect(
      decodeAnalysisTree({
        ...tree,
        items: [
          folder,
          {
            ...document,
            availability: 'Unavailable',
            title: '文档不可用',
            documentType: null,
            lifecycleStatus: null,
          },
        ],
      }).items[1]?.availability,
    ).toBe('Unavailable')
  })
  it('fails closed on missing identity/tokens, unsafe integers/enums, leaking unavailable metadata and invalid topology', () => {
    for (const bad of [
      { ...tree, treeConcurrencyToken: '' },
      { ...tree, treeConcurrencyToken: 'node-token' },
      { ...tree, items: undefined },
      { ...tree, items: [folder, folder] },
      ...[
        { id: 1.5 },
        { id: Number.MAX_SAFE_INTEGER + 1 },
        { parentId: undefined },
        { concurrencyToken: '' },
        { concurrencyToken: null },
        { nodeType: 'Page' },
        { availability: 'Maybe' },
        { documentType: 'AnalysisNote' },
        { lifecycleStatus: 'Deleted' },
        { availability: 'Unavailable' },
        { knowledgeDocumentId: null },
        { createdAt: 'yesterday' },
        { parentId: 12 },
        { parentId: 999 },
        { sortOrder: 2 },
      ].map((change) => ({ ...tree, items: [folder, { ...document, ...change }] })),
      { ...tree, items: [{ ...folder, parentId: 12 }, document] },
      { ...tree, items: [folder, document, { ...document, id: 13, sortOrder: 1 }] },
    ])
      expect(() => decodeAnalysisTree(bad)).toThrow()
    expect(() =>
      decodeAnalysisMutation({ ...tree, node: { ...document, title: 'stale' } }),
    ).toThrow()
    expect(() => decodeAnalysisMutation(tree)).toThrow()
  })
  it('uses the eight B01 routes and canonical node/tree token write names without existing-placement creation', async () => {
    const create = { parentId: null, title: 'MES', treeConcurrencyToken: token }
    const write = { nodeConcurrencyToken: 'node-11', treeConcurrencyToken: token }
    await api.getAnalysisTree()
    await api.createAnalysisFolder(create)
    await api.createAnalysisDocument({ ...create, documentType: 'DesignNote' })
    await api.renameAnalysisFolder(11, { ...write, title: 'MES 新目录' })
    await api.moveAnalysisNode(12, { ...write, targetParentId: null, targetPosition: 1 })
    await api.reorderAnalysisChildren({
      parentId: null,
      treeConcurrencyToken: token,
      items: [{ id: 11, nodeConcurrencyToken: 'node-11' }],
    })
    await api.removeAnalysisPlacement(12, write)
    await api.deleteAnalysisFolder(11, write)
    expect(apiClient.get).toHaveBeenCalledWith('/analysis/tree', { decode: decodeAnalysisTree })
    expect(vi.mocked(apiClient.post).mock.calls.map(([path]) => path)).toEqual([
      '/analysis/folders',
      '/analysis/documents',
      '/analysis/nodes/12/move',
    ])
    expect(apiClient.post).toHaveBeenCalledWith(
      '/analysis/documents',
      { ...create, documentType: 'DesignNote' },
      { decode: decodeAnalysisMutation },
    )
    expect(vi.mocked(apiClient.put).mock.calls.map(([path]) => path)).toEqual([
      '/analysis/folders/11/title',
      '/analysis/children/order',
    ])
    expect(apiClient.deleteWithBody).toHaveBeenCalledWith(
      '/analysis/document-placements/12',
      write,
      { decode: decodeAnalysisMutation },
    )
    expect(apiClient.deleteWithBody).toHaveBeenCalledWith('/analysis/folders/11', write, {
      decode: decodeAnalysisMutation,
    })
    expect(() => api.removeAnalysisPlacement(NaN, write)).toThrow()
  })
})
