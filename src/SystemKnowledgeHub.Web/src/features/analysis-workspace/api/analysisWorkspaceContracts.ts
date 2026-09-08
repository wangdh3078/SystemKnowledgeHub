import {
  documentTypes,
  documentLifecycleStatuses,
  type DocumentType,
  type DocumentLifecycleStatus,
} from '../../knowledge-documents/api/knowledgeDocumentContracts'

export interface AnalysisNode {
  readonly id: number
  readonly parentId: number | null
  readonly nodeType: 'Folder' | 'Document'
  readonly sortOrder: number
  readonly concurrencyToken: string
  readonly title: string
  readonly knowledgeDocumentId: number | null
  readonly documentType: DocumentType | null
  readonly lifecycleStatus: DocumentLifecycleStatus | null
  readonly availability: 'Available' | 'Unavailable'
  readonly createdAt: string
  readonly updatedAt: string
}
export interface AnalysisTree {
  readonly items: readonly AnalysisNode[]
  readonly treeConcurrencyToken: string
}
export interface AnalysisMutation extends AnalysisTree {
  readonly node: AnalysisNode | null
}
function invalid(): never {
  throw new Error('分析目录响应格式无效。')
}
function object(value: unknown): Record<string, unknown> {
  if (typeof value !== 'object' || value === null || Array.isArray(value)) return invalid()
  return value as Record<string, unknown>
}
function text(value: unknown): string {
  return typeof value === 'string' && value.trim().length > 0 ? value : invalid()
}
function integer(value: unknown, minimum: number): number {
  return typeof value === 'number' && Number.isSafeInteger(value) && value >= minimum
    ? value
    : invalid()
}
function nullableId(value: unknown): number | null {
  return value === null ? null : integer(value, 1)
}
function enumValue<T extends string>(value: unknown, values: readonly T[]): T {
  const result = values.find((item) => item === value)
  return result ?? invalid()
}
function date(value: unknown): string {
  const result = text(value)
  return /^\d{4}-\d{2}-\d{2}T/.test(result) && Number.isFinite(Date.parse(result))
    ? result
    : invalid()
}
export function decodeAnalysisNode(value: unknown): AnalysisNode {
  const row = object(value)
  const node: AnalysisNode = {
    id: integer(row.id, 1),
    parentId: nullableId(row.parentId),
    nodeType: enumValue(row.nodeType, ['Folder', 'Document']),
    sortOrder: integer(row.sortOrder, 0),
    concurrencyToken: text(row.concurrencyToken),
    title: text(row.title),
    knowledgeDocumentId: nullableId(row.knowledgeDocumentId),
    documentType: row.documentType === null ? null : enumValue(row.documentType, documentTypes),
    lifecycleStatus:
      row.lifecycleStatus === null
        ? null
        : enumValue(row.lifecycleStatus, documentLifecycleStatuses),
    availability: enumValue(row.availability, ['Available', 'Unavailable']),
    createdAt: date(row.createdAt),
    updatedAt: date(row.updatedAt),
  }
  if (node.nodeType === 'Folder') {
    if (
      node.knowledgeDocumentId !== null ||
      node.documentType !== null ||
      node.lifecycleStatus !== null ||
      node.availability !== 'Available'
    )
      invalid()
  } else {
    if (node.knowledgeDocumentId === null) invalid()
    if (
      node.availability === 'Available' &&
      (node.documentType === null || node.lifecycleStatus === null)
    )
      invalid()
    if (
      node.availability === 'Unavailable' &&
      (node.title !== '文档不可用' || node.documentType !== null || node.lifecycleStatus !== null)
    )
      invalid()
  }
  return node
}
export function decodeAnalysisTree(value: unknown): AnalysisTree {
  const row = object(value)
  const token = text(row.treeConcurrencyToken)
  if (!/^a1\.[A-F0-9]{64}$/.test(token) || !Array.isArray(row.items) || row.items.length > 2000)
    return invalid()
  const items = row.items.map(decodeAnalysisNode)
  const nodes = new Map(items.map((item) => [item.id, item]))
  const documents = items.flatMap((item) =>
    item.knowledgeDocumentId === null ? [] : [item.knowledgeDocumentId],
  )
  if (nodes.size !== items.length || new Set(documents).size !== documents.length) invalid()
  const siblings = new Map<number | null, number[]>()
  for (const item of items) {
    let ancestor = item.parentId
    const visited = new Set([item.id])
    while (ancestor !== null) {
      const parent = nodes.get(ancestor)
      if (!parent || parent.nodeType !== 'Folder' || visited.has(ancestor) || visited.size >= 10)
        return invalid()
      visited.add(ancestor)
      ancestor = parent.parentId
    }
    const orders = siblings.get(item.parentId) ?? []
    orders.push(item.sortOrder)
    siblings.set(item.parentId, orders)
  }
  for (const orders of siblings.values()) {
    if (orders.sort((a, b) => a - b).some((order, index) => order !== index)) invalid()
  }
  return { items, treeConcurrencyToken: token }
}
export function decodeAnalysisMutation(value: unknown): AnalysisMutation {
  const tree = decodeAnalysisTree(value)
  const row = object(value)
  const node = row.node === null ? null : decodeAnalysisNode(row.node)
  if (
    node &&
    JSON.stringify(tree.items.find((item) => item.id === node.id)) !== JSON.stringify(node)
  )
    invalid()
  return { ...tree, node }
}
