import {
  decodePortalPage,
  type PortalPageResponse,
} from '../../portal-reading/api/portalReadContracts'

export const portalTargetTypes = [
  'System',
  'BusinessFunction',
  'DatabaseObject',
  'KnowledgeDocument',
  'Integration',
] as const
export const portalNodeKinds = ['Folder', 'Page'] as const
export const portalSourceKinds = ['PrimaryTarget', 'ExplicitReference', 'Derived'] as const
export const portalProjectionKinds = [
  'Summary',
  'KnowledgeDocumentBody',
  'StructuredOverview',
  'DatabaseStructure',
  'AttachmentList',
  'TrustSummary',
  'RelatedKnowledge',
  'Traceability',
] as const
export const portalPersistedProjectionKinds = portalProjectionKinds

export type PortalTargetType = (typeof portalTargetTypes)[number]
export type PortalNodeKind = (typeof portalNodeKinds)[number]
export type PortalSourceKind = (typeof portalSourceKinds)[number]
export type PortalProjectionKind = (typeof portalProjectionKinds)[number]
export type PortalPersistedProjectionKind = PortalProjectionKind

export interface PortalTargetSummary {
  readonly type: PortalTargetType
  readonly id: number
  readonly title: string
  readonly context: string | null
  readonly status: string
  readonly documentType: string | null
  readonly lifecycle: string | null
}

export interface PortalHealth {
  readonly code: string
  readonly message: string
  readonly isHealthy: boolean
}

export interface PortalPageListItem {
  readonly id: number
  readonly title: string
  readonly primaryTarget: PortalTargetSummary
  readonly isPublished: boolean
  readonly publicationLabel: string
  readonly referenceHealth: PortalHealth
  readonly nodePlacementCount: number
  readonly updatedAt: string
  readonly concurrencyToken: string
}

export interface PortalPageListResponse {
  readonly items: readonly PortalPageListItem[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface PortalSection {
  readonly id: number
  readonly heading: string
  readonly sourceKind: PortalSourceKind
  readonly referenceTarget: PortalTargetSummary | null
  readonly projectionKind: PortalPersistedProjectionKind
  readonly sortOrder: number
  readonly isHealthy: boolean
  readonly healthMessage: string
}

export interface PortalPlacement {
  readonly nodeId: number
  readonly path: string
  readonly isPublished: boolean
  readonly isEffectivelyPublished: boolean
}

export interface PortalPageDetail {
  readonly id: number
  readonly title: string
  readonly primaryTarget: PortalTargetSummary
  readonly isPublished: boolean
  readonly publicationLabel: string
  readonly sections: readonly PortalSection[]
  readonly placements: readonly PortalPlacement[]
  readonly referenceHealth: PortalHealth
  readonly updatedAt: string
  readonly concurrencyToken: string
}

export interface PortalTreeNode {
  readonly nodeId: number
  readonly parentNodeId: number | null
  readonly title: string
  readonly nodeKind: PortalNodeKind
  readonly pageId: number | null
  readonly pageTitle: string | null
  readonly isPublished: boolean
  readonly isEffectivelyPublished: boolean
  readonly health: PortalHealth
  readonly concurrencyToken: string
}

export interface PortalTreeResponse {
  readonly items: readonly PortalTreeNode[]
  readonly total: number
}

export interface PortalReadinessItem {
  readonly code: string
  readonly message: string
}

export interface PortalReadiness {
  readonly canPublish: boolean
  readonly checks: readonly PortalReadinessItem[]
  readonly blockers: readonly PortalReadinessItem[]
  readonly warnings: readonly PortalReadinessItem[]
}

export interface PortalPreviewSection {
  readonly id: number
  readonly heading: string
  readonly sourceKind: string
  readonly projectionKind: PortalProjectionKind
  readonly content: Readonly<Record<string, unknown>>
}

export type PortalPreviewPage = PortalPageResponse

export interface PortalPreview {
  readonly page: PortalPreviewPage | null
  readonly readiness: PortalReadiness
}

export interface PortalTargetListResponse {
  readonly items: readonly PortalTargetSummary[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface PortalSectionWrite {
  readonly id?: number | null
  readonly heading: string
  readonly sourceKind: PortalSourceKind
  readonly referenceTarget: { readonly type: PortalTargetType; readonly id: number } | null
  readonly projectionKind: PortalProjectionKind
  readonly sortOrder: number
}

type JsonObject = Readonly<Record<string, unknown>>

function object(value: unknown, field: string): JsonObject {
  if (value === null || typeof value !== 'object' || Array.isArray(value))
    throw new Error(`${field} must be an object`)
  return value as JsonObject
}
function array(value: unknown, field: string): readonly unknown[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value
}
function string(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}
function nullableString(value: unknown, field: string): string | null {
  return value === null ? null : string(value, field)
}
function boolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') throw new Error(`${field} must be a boolean`)
  return value
}
function integer(value: unknown, field: string, minimum = 0): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < minimum)
    throw new Error(`${field} must be a safe integer`)
  return value
}
function nullableInteger(value: unknown, field: string): number | null {
  return value === null ? null : integer(value, field, 1)
}
function enumValue<T extends string>(value: unknown, field: string, values: readonly T[]): T {
  const result = string(value, field)
  if (!values.includes(result as T)) throw new Error(`${field} has an unsupported value`)
  return result as T
}

function decodeTarget(value: unknown, field: string): PortalTargetSummary {
  const item = object(value, field)
  return {
    type: enumValue(item.type, `${field}.type`, portalTargetTypes),
    id: integer(item.id, `${field}.id`, 1),
    title: string(item.title, `${field}.title`),
    context: nullableString(item.context, `${field}.context`),
    status: string(item.status, `${field}.status`),
    documentType: nullableString(item.documentType, `${field}.documentType`),
    lifecycle: nullableString(item.lifecycle, `${field}.lifecycle`),
  }
}

function decodeHealth(value: unknown, field: string): PortalHealth {
  const item = object(value, field)
  return {
    code: string(item.code, `${field}.code`),
    message: string(item.message, `${field}.message`),
    isHealthy: boolean(item.isHealthy, `${field}.isHealthy`),
  }
}

function decodePageListItem(value: unknown, field: string): PortalPageListItem {
  const item = object(value, field)
  return {
    id: integer(item.id, `${field}.id`, 1),
    title: string(item.title, `${field}.title`),
    primaryTarget: decodeTarget(item.primaryTarget, `${field}.primaryTarget`),
    isPublished: boolean(item.isPublished, `${field}.isPublished`),
    publicationLabel: string(item.publicationLabel, `${field}.publicationLabel`),
    referenceHealth: decodeHealth(item.referenceHealth, `${field}.referenceHealth`),
    nodePlacementCount: integer(item.nodePlacementCount, `${field}.nodePlacementCount`),
    updatedAt: string(item.updatedAt, `${field}.updatedAt`),
    concurrencyToken: string(item.concurrencyToken, `${field}.concurrencyToken`),
  }
}

export function decodePortalPageList(value: unknown): PortalPageListResponse {
  const root = object(value, 'portalPages')
  return {
    items: array(root.items, 'portalPages.items').map((item, index) =>
      decodePageListItem(item, `portalPages.items[${index}]`),
    ),
    page: integer(root.page, 'portalPages.page', 1),
    pageSize: integer(root.pageSize, 'portalPages.pageSize', 1),
    total: integer(root.total, 'portalPages.total'),
  }
}

function decodeSection(value: unknown, field: string): PortalSection {
  const item = object(value, field)
  return {
    id: integer(item.id, `${field}.id`, 1),
    heading: string(item.heading, `${field}.heading`),
    sourceKind: enumValue(item.sourceKind, `${field}.sourceKind`, portalSourceKinds),
    referenceTarget:
      item.referenceTarget === null
        ? null
        : decodeTarget(item.referenceTarget, `${field}.referenceTarget`),
    projectionKind: enumValue(
      item.projectionKind,
      `${field}.projectionKind`,
      portalPersistedProjectionKinds,
    ),
    sortOrder: integer(item.sortOrder, `${field}.sortOrder`),
    isHealthy: boolean(item.isHealthy, `${field}.isHealthy`),
    healthMessage: string(item.healthMessage, `${field}.healthMessage`),
  }
}

export function decodePortalPageDetail(value: unknown): PortalPageDetail {
  const root = object(value, 'portalPage')
  return {
    id: integer(root.id, 'portalPage.id', 1),
    title: string(root.title, 'portalPage.title'),
    primaryTarget: decodeTarget(root.primaryTarget, 'portalPage.primaryTarget'),
    isPublished: boolean(root.isPublished, 'portalPage.isPublished'),
    publicationLabel: string(root.publicationLabel, 'portalPage.publicationLabel'),
    sections: array(root.sections, 'portalPage.sections').map((item, index) =>
      decodeSection(item, `portalPage.sections[${index}]`),
    ),
    placements: array(root.placements, 'portalPage.placements').map((value, index) => {
      const item = object(value, `portalPage.placements[${index}]`)
      return {
        nodeId: integer(item.nodeId, 'placement.nodeId', 1),
        path: string(item.path, 'placement.path'),
        isPublished: boolean(item.isPublished, 'placement.isPublished'),
        isEffectivelyPublished: boolean(
          item.isEffectivelyPublished,
          'placement.isEffectivelyPublished',
        ),
      }
    }),
    referenceHealth: decodeHealth(root.referenceHealth, 'portalPage.referenceHealth'),
    updatedAt: string(root.updatedAt, 'portalPage.updatedAt'),
    concurrencyToken: string(root.concurrencyToken, 'portalPage.concurrencyToken'),
  }
}

export function decodePortalTree(value: unknown): PortalTreeResponse {
  const root = object(value, 'portalTree')
  return {
    items: array(root.items, 'portalTree.items').map((value, index) => {
      const item = object(value, `portalTree.items[${index}]`)
      return {
        nodeId: integer(item.nodeId, 'node.nodeId', 1),
        parentNodeId: nullableInteger(item.parentNodeId, 'node.parentNodeId'),
        title: string(item.title, 'node.title'),
        nodeKind: enumValue(item.nodeKind, 'node.nodeKind', portalNodeKinds),
        pageId: nullableInteger(item.pageId, 'node.pageId'),
        pageTitle: nullableString(item.pageTitle, 'node.pageTitle'),
        isPublished: boolean(item.isPublished, 'node.isPublished'),
        isEffectivelyPublished: boolean(item.isEffectivelyPublished, 'node.isEffectivelyPublished'),
        health: decodeHealth(item.health, 'node.health'),
        concurrencyToken: string(item.concurrencyToken, 'node.concurrencyToken'),
      }
    }),
    total: integer(root.total, 'portalTree.total'),
  }
}

function decodeReadiness(value: unknown): PortalReadiness {
  const root = object(value, 'readiness')
  const items = (value: unknown, field: string): readonly PortalReadinessItem[] =>
    array(value, field).map((entry, index) => {
      const item = object(entry, `${field}[${index}]`)
      return {
        code: string(item.code, 'readiness.code'),
        message: string(item.message, 'readiness.message'),
      }
    })
  return {
    canPublish: boolean(root.canPublish, 'readiness.canPublish'),
    checks: items(root.checks, 'readiness.checks'),
    blockers: items(root.blockers, 'readiness.blockers'),
    warnings: items(root.warnings, 'readiness.warnings'),
  }
}

export function decodePortalPreview(value: unknown): PortalPreview {
  const root = object(value, 'preview')
  let page: PortalPreviewPage | null = null
  if (root.page !== null) {
    page = decodePortalPage(root.page)
  }
  return { page, readiness: decodeReadiness(root.readiness) }
}

export function decodePortalTargetList(value: unknown): PortalTargetListResponse {
  const root = object(value, 'portalTargets')
  return {
    items: array(root.items, 'portalTargets.items').map((item, index) =>
      decodeTarget(item, `portalTargets.items[${index}]`),
    ),
    page: integer(root.page, 'portalTargets.page', 1),
    pageSize: integer(root.pageSize, 'portalTargets.pageSize', 1),
    total: integer(root.total, 'portalTargets.total'),
  }
}
