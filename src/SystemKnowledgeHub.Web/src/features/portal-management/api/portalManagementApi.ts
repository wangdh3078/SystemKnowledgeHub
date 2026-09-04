import { apiClient } from '../../../api/client/apiClient'
import { isSafeApiId } from '../../../api/contracts/id'
import {
  decodePortalPageDetail,
  decodePortalPageList,
  decodePortalPreview,
  decodePortalTargetList,
  decodePortalTree,
  type PortalNodeKind,
  type PortalPageDetail,
  type PortalPageListResponse,
  type PortalPreview,
  type PortalSectionWrite,
  type PortalTargetListResponse,
  type PortalTargetType,
  type PortalTreeNode,
  type PortalTreeResponse,
} from './portalManagementContracts'

function id(value: number, label: string): string {
  if (!isSafeApiId(value)) throw new RangeError(`${label} ID 无效。`)
  return encodeURIComponent(String(value))
}

export function getPortalPages(
  parameters: {
    page: number
    pageSize: number
    search?: string
  },
  signal?: AbortSignal,
): Promise<PortalPageListResponse> {
  const query = new URLSearchParams({
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
  })
  if (parameters.search) query.set('search', parameters.search)
  return apiClient.get(`/admin/portal/pages?${query.toString()}`, {
    signal,
    decode: decodePortalPageList,
  })
}

export function getPortalPage(pageId: number, signal?: AbortSignal): Promise<PortalPageDetail> {
  return apiClient.get(`/admin/portal/pages/${id(pageId, '页面')}`, {
    signal,
    decode: decodePortalPageDetail,
  })
}

export function createPortalPage(request: {
  title: string
  primaryTarget: { type: PortalTargetType; id: number }
}): Promise<PortalPageDetail> {
  return apiClient.post('/admin/portal/pages', request, { decode: decodePortalPageDetail })
}

export function updatePortalPage(
  pageId: number,
  request: {
    title: string
    primaryTarget: { type: PortalTargetType; id: number }
    sections: readonly PortalSectionWrite[]
    concurrencyToken: string
  },
): Promise<PortalPageDetail> {
  return apiClient.put(`/admin/portal/pages/${id(pageId, '页面')}`, request, {
    decode: decodePortalPageDetail,
  })
}

export function deletePortalPage(pageId: number, concurrencyToken: string): Promise<void> {
  return apiClient.deleteWithBody(
    `/admin/portal/pages/${id(pageId, '页面')}`,
    { concurrencyToken },
    { decode: () => undefined },
  )
}

export function getPortalPreview(pageId: number, signal?: AbortSignal): Promise<PortalPreview> {
  return apiClient.get(`/admin/portal/pages/${id(pageId, '页面')}/preview`, {
    signal,
    decode: decodePortalPreview,
  })
}

export function publishPortalPage(
  pageId: number,
  concurrencyToken: string,
): Promise<PortalPageDetail> {
  return apiClient.post(
    `/admin/portal/pages/${id(pageId, '页面')}/publish`,
    { concurrencyToken },
    { decode: decodePortalPageDetail },
  )
}

export function unpublishPortalPage(
  pageId: number,
  concurrencyToken: string,
): Promise<PortalPageDetail> {
  return apiClient.post(
    `/admin/portal/pages/${id(pageId, '页面')}/unpublish`,
    { concurrencyToken },
    { decode: decodePortalPageDetail },
  )
}

export function getPortalTree(signal?: AbortSignal): Promise<PortalTreeResponse> {
  return apiClient.get('/admin/portal/tree', { signal, decode: decodePortalTree })
}

export function createPortalNode(request: {
  title: string
  nodeKind: PortalNodeKind
  parentId: number | null
  portalPageId: number | null
  sortOrder: number
}): Promise<PortalTreeNode> {
  return apiClient.post('/admin/portal/nodes', request, {
    decode: (value) => decodePortalTree({ items: [value], total: 1 }).items[0]!,
  })
}

export function updatePortalNode(
  nodeId: number,
  request: {
    title: string
    nodeKind: PortalNodeKind
    parentId: number | null
    portalPageId: number | null
    sortOrder: number
    concurrencyToken: string
  },
): Promise<PortalTreeNode> {
  return apiClient.put(`/admin/portal/nodes/${id(nodeId, '节点')}`, request, {
    decode: (value) => decodePortalTree({ items: [value], total: 1 }).items[0]!,
  })
}

export function reorderPortalNodes(
  parentId: number | null,
  items: readonly PortalTreeNode[],
): Promise<PortalTreeResponse> {
  return apiClient.put(
    '/admin/portal/nodes/reorder',
    {
      parentId,
      items: items.map((item) => ({ id: item.nodeId, concurrencyToken: item.concurrencyToken })),
    },
    { decode: decodePortalTree },
  )
}

export function deletePortalNode(nodeId: number, concurrencyToken: string): Promise<void> {
  return apiClient.deleteWithBody(
    `/admin/portal/nodes/${id(nodeId, '节点')}`,
    { concurrencyToken },
    { decode: () => undefined },
  )
}

export function publishPortalNode(
  nodeId: number,
  concurrencyToken: string,
): Promise<PortalTreeNode> {
  return apiClient.post(
    `/admin/portal/nodes/${id(nodeId, '节点')}/publish`,
    { concurrencyToken },
    { decode: (value) => decodePortalTree({ items: [value], total: 1 }).items[0]! },
  )
}

export function unpublishPortalNode(
  nodeId: number,
  concurrencyToken: string,
): Promise<PortalTreeNode> {
  return apiClient.post(
    `/admin/portal/nodes/${id(nodeId, '节点')}/unpublish`,
    { concurrencyToken },
    { decode: (value) => decodePortalTree({ items: [value], total: 1 }).items[0]! },
  )
}

export function getPortalTargets(
  parameters: {
    type: PortalTargetType
    search?: string
    page: number
    pageSize: number
  },
  signal?: AbortSignal,
): Promise<PortalTargetListResponse> {
  const query = new URLSearchParams({
    type: parameters.type,
    page: String(parameters.page),
    pageSize: String(parameters.pageSize),
  })
  if (parameters.search) query.set('search', parameters.search)
  return apiClient.get(`/admin/portal/targets?${query.toString()}`, {
    signal,
    decode: decodePortalTargetList,
  })
}
