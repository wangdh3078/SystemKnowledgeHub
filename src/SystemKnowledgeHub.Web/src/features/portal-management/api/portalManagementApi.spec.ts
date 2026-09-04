import { beforeEach, describe, expect, it, vi } from 'vitest'
import { apiClient } from '../../../api/client/apiClient'
import {
  createPortalNode,
  createPortalPage,
  deletePortalNode,
  deletePortalPage,
  getPortalPages,
  getPortalPreview,
  getPortalTargets,
  publishPortalPage,
  publishPortalNode,
  reorderPortalNodes,
  unpublishPortalNode,
  unpublishPortalPage,
  updatePortalNode,
  updatePortalPage,
} from './portalManagementApi'

vi.mock('../../../api/client/apiClient', () => ({
  apiClient: { get: vi.fn(), post: vi.fn(), put: vi.fn(), deleteWithBody: vi.fn() },
}))

describe('portal management API', () => {
  beforeEach(() => {
    vi.mocked(apiClient.get)
      .mockReset()
      .mockResolvedValue({} as never)
    vi.mocked(apiClient.post)
      .mockReset()
      .mockResolvedValue({} as never)
    vi.mocked(apiClient.put)
      .mockReset()
      .mockResolvedValue({} as never)
    vi.mocked(apiClient.deleteWithBody)
      .mockReset()
      .mockResolvedValue(undefined as never)
  })

  it('keeps page inventory and target picker server-paged with search', async () => {
    await getPortalPages({ page: 2, pageSize: 50, search: 'Lot Track In' })
    await getPortalTargets({ type: 'KnowledgeDocument', page: 3, pageSize: 20, search: '业务说明' })
    expect(apiClient.get).toHaveBeenNthCalledWith(
      1,
      '/admin/portal/pages?page=2&pageSize=50&search=Lot+Track+In',
      expect.objectContaining({ decode: expect.any(Function) }),
    )
    expect(apiClient.get).toHaveBeenNthCalledWith(
      2,
      '/admin/portal/targets?type=KnowledgeDocument&page=3&pageSize=20&search=%E4%B8%9A%E5%8A%A1%E8%AF%B4%E6%98%8E',
      expect.objectContaining({ decode: expect.any(Function) }),
    )
  })

  it('sends primary target and complete ordered sections in one whole-page PUT', async () => {
    await createPortalPage({
      title: 'Lot Track In',
      primaryTarget: { type: 'BusinessFunction', id: 42 },
    })
    await updatePortalPage(81, {
      title: 'Lot Track In',
      primaryTarget: { type: 'BusinessFunction', id: 42 },
      sections: [
        {
          id: 301,
          heading: '业务概览',
          sourceKind: 'PrimaryTarget',
          referenceTarget: null,
          projectionKind: 'StructuredOverview',
          sortOrder: 0,
        },
        {
          id: null,
          heading: '业务说明',
          sourceKind: 'ExplicitReference',
          referenceTarget: { type: 'KnowledgeDocument', id: 99 },
          projectionKind: 'KnowledgeDocumentBody',
          sortOrder: 1,
        },
      ],
      concurrencyToken: 'opaque-page-token',
    })
    expect(apiClient.post).toHaveBeenCalledWith(
      '/admin/portal/pages',
      expect.objectContaining({ primaryTarget: { type: 'BusinessFunction', id: 42 } }),
      expect.anything(),
    )
    expect(apiClient.put).toHaveBeenCalledWith(
      '/admin/portal/pages/81',
      expect.objectContaining({
        concurrencyToken: 'opaque-page-token',
        sections: expect.arrayContaining([
          expect.objectContaining({ projectionKind: 'KnowledgeDocumentBody' }),
        ]),
      }),
      expect.objectContaining({ decode: expect.any(Function) }),
    )
  })

  it('uses dedicated preview, publish and atomic sibling reorder contracts', async () => {
    await getPortalPreview(81)
    await publishPortalPage(81, 'page-token')
    await reorderPortalNodes(null, [
      {
        nodeId: 2,
        parentNodeId: null,
        title: 'B',
        nodeKind: 'Folder',
        pageId: null,
        pageTitle: null,
        isPublished: false,
        isEffectivelyPublished: false,
        health: { code: 'healthy', message: '正常', isHealthy: true },
        concurrencyToken: 'node-2',
      },
      {
        nodeId: 1,
        parentNodeId: null,
        title: 'A',
        nodeKind: 'Folder',
        pageId: null,
        pageTitle: null,
        isPublished: false,
        isEffectivelyPublished: false,
        health: { code: 'healthy', message: '正常', isHealthy: true },
        concurrencyToken: 'node-1',
      },
    ])
    expect(apiClient.get).toHaveBeenCalledWith(
      '/admin/portal/pages/81/preview',
      expect.objectContaining({ decode: expect.any(Function) }),
    )
    expect(apiClient.post).toHaveBeenCalledWith(
      '/admin/portal/pages/81/publish',
      { concurrencyToken: 'page-token' },
      expect.anything(),
    )
    expect(apiClient.put).toHaveBeenCalledWith(
      '/admin/portal/nodes/reorder',
      {
        parentId: null,
        items: [
          { id: 2, concurrencyToken: 'node-2' },
          { id: 1, concurrencyToken: 'node-1' },
        ],
      },
      expect.objectContaining({ decode: expect.any(Function) }),
    )
  })

  it('uses explicit page and node lifecycle endpoints with opaque tokens', async () => {
    await createPortalNode({
      title: '生产管理',
      nodeKind: 'Folder',
      parentId: 1,
      portalPageId: null,
      sortOrder: 0,
    })
    await updatePortalNode(2, {
      title: '生产执行',
      nodeKind: 'Folder',
      parentId: 1,
      portalPageId: null,
      sortOrder: 0,
      concurrencyToken: 'node-token',
    })
    await publishPortalNode(2, 'node-token')
    await unpublishPortalNode(2, 'published-node-token')
    await unpublishPortalPage(81, 'published-page-token')
    await deletePortalNode(2, 'unpublished-node-token')
    await deletePortalPage(81, 'unpublished-page-token')

    expect(apiClient.post).toHaveBeenCalledWith(
      '/admin/portal/nodes',
      expect.objectContaining({ nodeKind: 'Folder', parentId: 1 }),
      expect.anything(),
    )
    expect(apiClient.put).toHaveBeenCalledWith(
      '/admin/portal/nodes/2',
      expect.objectContaining({ concurrencyToken: 'node-token' }),
      expect.anything(),
    )
    expect(apiClient.post).toHaveBeenCalledWith(
      '/admin/portal/nodes/2/publish',
      { concurrencyToken: 'node-token' },
      expect.anything(),
    )
    expect(apiClient.post).toHaveBeenCalledWith(
      '/admin/portal/nodes/2/unpublish',
      { concurrencyToken: 'published-node-token' },
      expect.anything(),
    )
    expect(apiClient.post).toHaveBeenCalledWith(
      '/admin/portal/pages/81/unpublish',
      { concurrencyToken: 'published-page-token' },
      expect.anything(),
    )
    expect(apiClient.deleteWithBody).toHaveBeenNthCalledWith(
      1,
      '/admin/portal/nodes/2',
      { concurrencyToken: 'unpublished-node-token' },
      expect.anything(),
    )
    expect(apiClient.deleteWithBody).toHaveBeenNthCalledWith(
      2,
      '/admin/portal/pages/81',
      { concurrencyToken: 'unpublished-page-token' },
      expect.anything(),
    )
  })
})
