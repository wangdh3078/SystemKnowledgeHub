export const portalTargetTypes = [
  'System',
  'BusinessFunction',
  'DatabaseObject',
  'KnowledgeDocument',
  'Integration',
] as const
export const portalNodeKinds = ['Folder', 'Page'] as const
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
export const portalKnowledgeStatuses = ['Unknown', 'Inferred', 'Confirmed'] as const

export type PortalTargetType = (typeof portalTargetTypes)[number]
export type PortalNodeKind = (typeof portalNodeKinds)[number]
export type PortalProjectionKind = (typeof portalProjectionKinds)[number]
export type PortalKnowledgeStatus = (typeof portalKnowledgeStatuses)[number]

export interface PortalTargetIdentity {
  readonly type: PortalTargetType
  readonly id: number
  readonly title: string
}

export interface PortalBreadcrumbItem {
  readonly nodeId: number
  readonly title: string
}

export interface PortalTreeNode {
  readonly nodeId: number
  readonly parentNodeId: number | null
  readonly title: string
  readonly nodeKind: PortalNodeKind
  readonly pageId: number | null
}

export interface PortalTreeResponse {
  readonly items: readonly PortalTreeNode[]
  readonly total: number
}

export interface PortalHomeCategory {
  readonly nodeId: number
  readonly title: string
  readonly nodeKind: PortalNodeKind
  readonly pageId: number | null
}

export interface PortalRecentPage {
  readonly id: number
  readonly title: string
  readonly primaryTarget: PortalTargetIdentity
  readonly breadcrumb: readonly PortalBreadcrumbItem[]
  readonly publishedAt: string
}

export interface PortalHomeResponse {
  readonly portalName: string
  readonly categories: readonly PortalHomeCategory[]
  readonly recentPages: readonly PortalRecentPage[]
}

export interface PortalSummaryContent {
  readonly kind: 'Summary'
  readonly targetType: PortalTargetType
  readonly targetId: number
  readonly title: string
  readonly summary: string | null
}

export interface PortalKnowledgeDocumentBodyContent {
  readonly kind: 'KnowledgeDocumentBody'
  readonly documentId: number
  readonly title: string
  readonly documentType: string
  readonly bodyMarkdown: string
  readonly imageAttachmentIds?: readonly number[]
}

export interface PortalSystemOverviewContent {
  readonly kind: 'SystemOverview'
  readonly systemId: number
  readonly name: string
  readonly displayName: string
  readonly systemType: string
  readonly lifecycle: string
  readonly purpose: string | null
}

export interface PortalBusinessFunctionOverviewContent {
  readonly kind: 'BusinessFunctionOverview'
  readonly businessFunctionId: number
  readonly name: string
  readonly displayName: string | null
  readonly functionType: string
  readonly systemName: string
  readonly purpose: string | null
  readonly callerSummary: string | null
  readonly inputDescription: string | null
  readonly outputDescription: string | null
}

export interface PortalDatabaseObjectOverviewContent {
  readonly kind: 'DatabaseObjectOverview'
  readonly databaseObjectId: number
  readonly schemaName: string
  readonly objectName: string
  readonly objectType: string
  readonly businessDescription: string | null
  readonly databaseComment: string | null
  readonly estimatedRows: number | null
  readonly accessMode: string
  readonly businessKeyColumns: readonly string[]
}

export interface PortalIntegrationOverviewContent {
  readonly kind: 'IntegrationOverview'
  readonly integrationId: number
  readonly name: string
  readonly integrationType: string
  readonly sourcePartyName: string
  readonly targetPartyName: string
  readonly flowDirection: string
  readonly purpose: string | null
}

export interface PortalDatabaseColumn {
  readonly ordinal: number
  readonly columnName: string
  readonly nativeDataType: string
  readonly nullable: boolean
  readonly databaseComment: string | null
}

export interface PortalDatabaseStructureContent {
  readonly kind: 'DatabaseStructure'
  readonly databaseObjectId: number
  readonly schemaName: string
  readonly objectName: string
  readonly objectType: string
  readonly businessDescription: string | null
  readonly databaseComment: string | null
  readonly estimatedRows: number | null
  readonly accessMode: string
  readonly businessKeyColumns: readonly string[]
  readonly columns: readonly PortalDatabaseColumn[]
}

export interface PortalAttachment {
  readonly attachmentId: number
  readonly displayName: string
  readonly kind: string
  readonly contentType: string
  readonly sizeBytes: number
  readonly previewMode: string
  readonly canPreview: boolean
  readonly canDownload: boolean
}
export interface PortalAttachmentListContent {
  readonly kind: 'AttachmentList'
  readonly documentId: number
  readonly attachments: readonly PortalAttachment[]
}
export interface PortalTrustSummaryContent {
  readonly kind: 'TrustSummary'
  readonly targetType: PortalTargetType
  readonly targetTitle: string
  readonly knowledgeStatus: PortalKnowledgeStatus
  readonly evidenceCount: number
  readonly humanConfirmationCount: number
  readonly confirmationCoverage: string | null
}
export interface PortalRelatedKnowledgeItem {
  readonly targetType: PortalTargetType
  readonly targetTitle: string
  readonly knowledgeStatus: PortalKnowledgeStatus
  readonly evidenceCount: number
  readonly humanConfirmationCount: number
  readonly relationKnowledgeStatus: PortalKnowledgeStatus
  readonly relationEvidenceCount: number
  readonly relationHumanConfirmationCount: number
  readonly portalPageId: number | null
}
export interface PortalRelatedKnowledgeGroup {
  readonly relationType: string
  readonly relationLabel: string
  readonly direction: 'Incoming' | 'Outgoing'
  readonly items: readonly PortalRelatedKnowledgeItem[]
}
export interface PortalRelatedKnowledgeContent {
  readonly kind: 'RelatedKnowledge'
  readonly groups: readonly PortalRelatedKnowledgeGroup[]
}
export interface PortalTraceNode {
  readonly documentType: string
  readonly title: string
  readonly knowledgeStatus: PortalKnowledgeStatus
  readonly evidenceCount: number
  readonly humanConfirmationCount: number
  readonly confirmationCoverage: string
  readonly portalPageId: number | null
}
export interface PortalTraceEdge {
  readonly relationType: string
  readonly knowledgeStatus: PortalKnowledgeStatus
  readonly evidenceCount: number
  readonly humanConfirmationCount: number
}
export interface PortalTracePath {
  readonly kind: string
  readonly nodes: readonly PortalTraceNode[]
  readonly edges: readonly PortalTraceEdge[]
}
export interface PortalTraceabilityContent {
  readonly kind: 'Traceability'
  readonly root: PortalTraceNode
  readonly paths: readonly PortalTracePath[]
  readonly missingLinkCodes: readonly string[]
  readonly cycleDetected: boolean
  readonly isTruncated: boolean
  readonly limits: {
    readonly maxDepth: number
    readonly maxNodes: number
    readonly maxEdges: number
  }
}

export type PortalSectionContent =
  | PortalSummaryContent
  | PortalKnowledgeDocumentBodyContent
  | PortalSystemOverviewContent
  | PortalBusinessFunctionOverviewContent
  | PortalDatabaseObjectOverviewContent
  | PortalIntegrationOverviewContent
  | PortalDatabaseStructureContent
  | PortalAttachmentListContent
  | PortalTrustSummaryContent
  | PortalRelatedKnowledgeContent
  | PortalTraceabilityContent

export interface PortalPageSection {
  readonly id: number
  readonly heading: string
  readonly sourceKind: 'PrimaryTarget' | 'ExplicitReference' | 'Derived'
  readonly projectionKind: PortalProjectionKind
  readonly content: PortalSectionContent
}

export interface PortalPageResponse {
  readonly id: number
  readonly title: string
  readonly primaryTarget: PortalTargetIdentity
  readonly breadcrumb: readonly PortalBreadcrumbItem[]
  readonly sections: readonly PortalPageSection[]
}

export interface PortalSearchItem {
  readonly pageId: number
  readonly title: string
  readonly primaryTargetType: PortalTargetType
  readonly primaryTargetTitle: string
  readonly breadcrumb: readonly PortalBreadcrumbItem[]
  readonly snippet: string
}
export interface PortalSearchResponse {
  readonly items: readonly PortalSearchItem[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
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
function stringList(value: unknown, field: string): readonly string[] {
  return array(value, field).map((item, index) => string(item, `${field}[${index}]`))
}

function decodeTarget(value: unknown, field: string): PortalTargetIdentity {
  const item = object(value, field)
  return {
    type: enumValue(item.type, `${field}.type`, portalTargetTypes),
    id: integer(item.id, `${field}.id`, 1),
    title: string(item.title, `${field}.title`),
  }
}

function decodeBreadcrumb(value: unknown, field: string): readonly PortalBreadcrumbItem[] {
  return array(value, field).map((entry, index) => {
    const item = object(entry, `${field}[${index}]`)
    return {
      nodeId: integer(item.nodeId, `${field}[${index}].nodeId`, 1),
      title: string(item.title, `${field}[${index}].title`),
    }
  })
}

function decodeNode(value: unknown, field: string): PortalTreeNode {
  const item = object(value, field)
  const nodeKind = enumValue(item.nodeKind, `${field}.nodeKind`, portalNodeKinds)
  const pageId = nullableInteger(item.pageId, `${field}.pageId`)
  if ((nodeKind === 'Folder' && pageId !== null) || (nodeKind === 'Page' && pageId === null))
    throw new Error(`${field} has an invalid node shape`)
  return {
    nodeId: integer(item.nodeId, `${field}.nodeId`, 1),
    parentNodeId: nullableInteger(item.parentNodeId, `${field}.parentNodeId`),
    title: string(item.title, `${field}.title`),
    nodeKind,
    pageId,
  }
}

export function decodePortalTree(value: unknown): PortalTreeResponse {
  const root = object(value, 'portalTree')
  const items = array(root.items, 'portalTree.items').map((item, index) =>
    decodeNode(item, `portalTree.items[${index}]`),
  )
  const total = integer(root.total, 'portalTree.total')
  if (total !== items.length) throw new Error('portalTree.total does not match items')
  return { items, total }
}

export function decodePortalHome(value: unknown): PortalHomeResponse {
  const root = object(value, 'portalHome')
  const categories = array(root.categories, 'portalHome.categories').map((value, index) => {
    const node = object(value, `portalHome.categories[${index}]`)
    const nodeKind = enumValue(node.nodeKind, 'portalHome.category.nodeKind', portalNodeKinds)
    const pageId = nullableInteger(node.pageId, 'portalHome.category.pageId')
    if ((nodeKind === 'Folder' && pageId !== null) || (nodeKind === 'Page' && pageId === null))
      throw new Error('portalHome category has an invalid node shape')
    return {
      nodeId: integer(node.nodeId, 'portalHome.category.nodeId', 1),
      title: string(node.title, 'portalHome.category.title'),
      nodeKind,
      pageId,
    }
  })
  const recentPages = array(root.recentPages, 'portalHome.recentPages').map((value, index) => {
    const item = object(value, `portalHome.recentPages[${index}]`)
    return {
      id: integer(item.id, 'recentPage.id', 1),
      title: string(item.title, 'recentPage.title'),
      primaryTarget: decodeTarget(item.primaryTarget, 'recentPage.primaryTarget'),
      breadcrumb: decodeBreadcrumb(item.breadcrumb, 'recentPage.breadcrumb'),
      publishedAt: string(item.publishedAt, 'recentPage.publishedAt'),
    }
  })
  if (recentPages.length > 8) throw new Error('portalHome.recentPages exceeds the limit')
  return {
    portalName: string(root.portalName, 'portalHome.portalName'),
    categories,
    recentPages,
  }
}

function decodeContent(value: unknown, field: string): PortalSectionContent {
  const item = object(value, field)
  const kind = string(item.kind, `${field}.kind`)
  switch (kind) {
    case 'Summary':
      return {
        kind,
        targetType: enumValue(item.targetType, `${field}.targetType`, portalTargetTypes),
        targetId: integer(item.targetId, `${field}.targetId`, 1),
        title: string(item.title, `${field}.title`),
        summary: nullableString(item.summary, `${field}.summary`),
      }
    case 'KnowledgeDocumentBody':
      return {
        kind,
        documentId: integer(item.documentId, `${field}.documentId`, 1),
        title: string(item.title, `${field}.title`),
        documentType: string(item.documentType, `${field}.documentType`),
        bodyMarkdown: string(item.bodyMarkdown, `${field}.bodyMarkdown`),
        imageAttachmentIds: array(item.imageAttachmentIds ?? [], `${field}.imageAttachmentIds`).map(
          (value, index) => integer(value, `${field}.imageAttachmentIds[${index}]`, 1),
        ),
      }
    case 'SystemOverview':
      return {
        kind,
        systemId: integer(item.systemId, `${field}.systemId`, 1),
        name: string(item.name, `${field}.name`),
        displayName: string(item.displayName, `${field}.displayName`),
        systemType: string(item.systemType, `${field}.systemType`),
        lifecycle: string(item.lifecycle, `${field}.lifecycle`),
        purpose: nullableString(item.purpose, `${field}.purpose`),
      }
    case 'BusinessFunctionOverview':
      return {
        kind,
        businessFunctionId: integer(item.businessFunctionId, `${field}.businessFunctionId`, 1),
        name: string(item.name, `${field}.name`),
        displayName: nullableString(item.displayName, `${field}.displayName`),
        functionType: string(item.functionType, `${field}.functionType`),
        systemName: string(item.systemName, `${field}.systemName`),
        purpose: nullableString(item.purpose, `${field}.purpose`),
        callerSummary: nullableString(item.callerSummary, `${field}.callerSummary`),
        inputDescription: nullableString(item.inputDescription, `${field}.inputDescription`),
        outputDescription: nullableString(item.outputDescription, `${field}.outputDescription`),
      }
    case 'DatabaseObjectOverview':
      return decodeDatabaseObjectOverview(item, kind, field)
    case 'IntegrationOverview':
      return {
        kind,
        integrationId: integer(item.integrationId, `${field}.integrationId`, 1),
        name: string(item.name, `${field}.name`),
        integrationType: string(item.integrationType, `${field}.integrationType`),
        sourcePartyName: string(item.sourcePartyName, `${field}.sourcePartyName`),
        targetPartyName: string(item.targetPartyName, `${field}.targetPartyName`),
        flowDirection: string(item.flowDirection, `${field}.flowDirection`),
        purpose: nullableString(item.purpose, `${field}.purpose`),
      }
    case 'DatabaseStructure': {
      const overview = decodeDatabaseObjectOverview(item, 'DatabaseObjectOverview', field)
      return {
        ...overview,
        kind,
        columns: array(item.columns, `${field}.columns`).map((value, index) => {
          const column = object(value, `${field}.columns[${index}]`)
          return {
            ordinal: integer(column.ordinal, 'column.ordinal', 1),
            columnName: string(column.columnName, 'column.columnName'),
            nativeDataType: string(column.nativeDataType, 'column.nativeDataType'),
            nullable: boolean(column.nullable, 'column.nullable'),
            databaseComment: nullableString(column.databaseComment, 'column.databaseComment'),
          }
        }),
      }
    }
    case 'AttachmentList':
      return {
        kind,
        documentId: integer(item.documentId, `${field}.documentId`, 1),
        attachments: array(item.attachments, `${field}.attachments`).map((value, index) => {
          const attachment = object(value, `${field}.attachments[${index}]`)
          return {
            attachmentId: integer(attachment.attachmentId, 'attachment.attachmentId', 1),
            displayName: string(attachment.displayName, 'attachment.displayName'),
            kind: string(attachment.kind, 'attachment.kind'),
            contentType: string(attachment.contentType, 'attachment.contentType'),
            sizeBytes: integer(attachment.sizeBytes, 'attachment.sizeBytes'),
            previewMode: string(attachment.previewMode, 'attachment.previewMode'),
            canPreview: boolean(attachment.canPreview, 'attachment.canPreview'),
            canDownload: boolean(attachment.canDownload, 'attachment.canDownload'),
          }
        }),
      }
    case 'TrustSummary':
      return {
        kind,
        targetType: enumValue(item.targetType, `${field}.targetType`, portalTargetTypes),
        targetTitle: string(item.targetTitle, `${field}.targetTitle`),
        knowledgeStatus: enumValue(
          item.knowledgeStatus,
          `${field}.knowledgeStatus`,
          portalKnowledgeStatuses,
        ),
        evidenceCount: integer(item.evidenceCount, `${field}.evidenceCount`),
        humanConfirmationCount: integer(
          item.humanConfirmationCount,
          `${field}.humanConfirmationCount`,
        ),
        confirmationCoverage: nullableString(
          item.confirmationCoverage,
          `${field}.confirmationCoverage`,
        ),
      }
    case 'RelatedKnowledge':
      return {
        kind,
        groups: array(item.groups, `${field}.groups`).map((value, groupIndex) => {
          const group = object(value, `${field}.groups[${groupIndex}]`)
          return {
            relationType: string(group.relationType, 'related.group.relationType'),
            relationLabel: string(group.relationLabel, 'related.group.relationLabel'),
            direction: enumValue(group.direction, 'related.group.direction', [
              'Incoming',
              'Outgoing',
            ] as const),
            items: array(group.items, 'related.group.items').map((value, itemIndex) => {
              const related = object(value, `related.group.items[${itemIndex}]`)
              return {
                targetType: enumValue(related.targetType, 'related.targetType', portalTargetTypes),
                targetTitle: string(related.targetTitle, 'related.targetTitle'),
                knowledgeStatus: enumValue(
                  related.knowledgeStatus,
                  'related.knowledgeStatus',
                  portalKnowledgeStatuses,
                ),
                evidenceCount: integer(related.evidenceCount, 'related.evidenceCount'),
                humanConfirmationCount: integer(
                  related.humanConfirmationCount,
                  'related.humanConfirmationCount',
                ),
                relationKnowledgeStatus: enumValue(
                  related.relationKnowledgeStatus,
                  'related.relationKnowledgeStatus',
                  portalKnowledgeStatuses,
                ),
                relationEvidenceCount: integer(
                  related.relationEvidenceCount,
                  'related.relationEvidenceCount',
                ),
                relationHumanConfirmationCount: integer(
                  related.relationHumanConfirmationCount,
                  'related.relationHumanConfirmationCount',
                ),
                portalPageId: nullableInteger(related.portalPageId, 'related.portalPageId'),
              }
            }),
          }
        }),
      }
    case 'Traceability': {
      const decodeNode = (value: unknown, nodeField: string): PortalTraceNode => {
        const node = object(value, nodeField)
        return {
          documentType: string(node.documentType, `${nodeField}.documentType`),
          title: string(node.title, `${nodeField}.title`),
          knowledgeStatus: enumValue(
            node.knowledgeStatus,
            `${nodeField}.knowledgeStatus`,
            portalKnowledgeStatuses,
          ),
          evidenceCount: integer(node.evidenceCount, `${nodeField}.evidenceCount`),
          humanConfirmationCount: integer(
            node.humanConfirmationCount,
            `${nodeField}.humanConfirmationCount`,
          ),
          confirmationCoverage: string(
            node.confirmationCoverage,
            `${nodeField}.confirmationCoverage`,
          ),
          portalPageId: nullableInteger(node.portalPageId, `${nodeField}.portalPageId`),
        }
      }
      const limits = object(item.limits, `${field}.limits`)
      return {
        kind,
        root: decodeNode(item.root, `${field}.root`),
        paths: array(item.paths, `${field}.paths`).map((value, pathIndex) => {
          const path = object(value, `${field}.paths[${pathIndex}]`)
          return {
            kind: string(path.kind, 'trace.path.kind'),
            nodes: array(path.nodes, 'trace.path.nodes').map((node, nodeIndex) =>
              decodeNode(node, `trace.path.nodes[${nodeIndex}]`),
            ),
            edges: array(path.edges, 'trace.path.edges').map((value, edgeIndex) => {
              const edge = object(value, `trace.path.edges[${edgeIndex}]`)
              return {
                relationType: string(edge.relationType, 'trace.edge.relationType'),
                knowledgeStatus: enumValue(
                  edge.knowledgeStatus,
                  'trace.edge.knowledgeStatus',
                  portalKnowledgeStatuses,
                ),
                evidenceCount: integer(edge.evidenceCount, 'trace.edge.evidenceCount'),
                humanConfirmationCount: integer(
                  edge.humanConfirmationCount,
                  'trace.edge.humanConfirmationCount',
                ),
              }
            }),
          }
        }),
        missingLinkCodes: stringList(item.missingLinkCodes, `${field}.missingLinkCodes`),
        cycleDetected: boolean(item.cycleDetected, `${field}.cycleDetected`),
        isTruncated: boolean(item.isTruncated, `${field}.isTruncated`),
        limits: {
          maxDepth: integer(limits.maxDepth, 'trace.limits.maxDepth', 1),
          maxNodes: integer(limits.maxNodes, 'trace.limits.maxNodes', 1),
          maxEdges: integer(limits.maxEdges, 'trace.limits.maxEdges', 1),
        },
      }
    }
    default:
      throw new Error(`${field}.kind has an unsupported value`)
  }
}

function decodeDatabaseObjectOverview(
  item: JsonObject,
  kind: 'DatabaseObjectOverview',
  field: string,
): PortalDatabaseObjectOverviewContent {
  const estimatedRows =
    item.estimatedRows === null ? null : integer(item.estimatedRows, `${field}.estimatedRows`)
  return {
    kind,
    databaseObjectId: integer(item.databaseObjectId, `${field}.databaseObjectId`, 1),
    schemaName: string(item.schemaName, `${field}.schemaName`),
    objectName: string(item.objectName, `${field}.objectName`),
    objectType: string(item.objectType, `${field}.objectType`),
    businessDescription: nullableString(item.businessDescription, `${field}.businessDescription`),
    databaseComment: nullableString(item.databaseComment, `${field}.databaseComment`),
    estimatedRows,
    accessMode: string(item.accessMode, `${field}.accessMode`),
    businessKeyColumns: stringList(item.businessKeyColumns, `${field}.businessKeyColumns`),
  }
}

export function decodePortalPage(value: unknown): PortalPageResponse {
  const root = object(value, 'portalPage')
  return {
    id: integer(root.id, 'portalPage.id', 1),
    title: string(root.title, 'portalPage.title'),
    primaryTarget: decodeTarget(root.primaryTarget, 'portalPage.primaryTarget'),
    breadcrumb: decodeBreadcrumb(root.breadcrumb, 'portalPage.breadcrumb'),
    sections: array(root.sections, 'portalPage.sections').map((value, index) => {
      const item = object(value, `portalPage.sections[${index}]`)
      const projectionKind = enumValue(
        item.projectionKind,
        'portalPage.section.projectionKind',
        portalProjectionKinds,
      )
      const content = decodeContent(item.content, 'portalPage.section.content')
      if (
        (projectionKind === 'Summary' && content.kind !== 'Summary') ||
        (projectionKind === 'KnowledgeDocumentBody' && content.kind !== 'KnowledgeDocumentBody') ||
        (projectionKind === 'DatabaseStructure' && content.kind !== 'DatabaseStructure') ||
        (projectionKind === 'AttachmentList' && content.kind !== 'AttachmentList') ||
        (projectionKind === 'TrustSummary' && content.kind !== 'TrustSummary') ||
        (projectionKind === 'RelatedKnowledge' && content.kind !== 'RelatedKnowledge') ||
        (projectionKind === 'Traceability' && content.kind !== 'Traceability') ||
        (projectionKind === 'StructuredOverview' &&
          ![
            'SystemOverview',
            'BusinessFunctionOverview',
            'DatabaseObjectOverview',
            'IntegrationOverview',
          ].includes(content.kind))
      )
        throw new Error('portalPage section projection/content mismatch')
      return {
        id: integer(item.id, 'portalPage.section.id', 1),
        heading: string(item.heading, 'portalPage.section.heading'),
        sourceKind: enumValue(item.sourceKind, 'portalPage.section.sourceKind', [
          'PrimaryTarget',
          'ExplicitReference',
          'Derived',
        ] as const),
        projectionKind,
        content,
      }
    }),
  }
}

export function decodePortalSearch(value: unknown): PortalSearchResponse {
  const root = object(value, 'portalSearch')
  return {
    items: array(root.items, 'portalSearch.items').map((value, index) => {
      const item = object(value, `portalSearch.items[${index}]`)
      return {
        pageId: integer(item.pageId, 'search.pageId', 1),
        title: string(item.title, 'search.title'),
        primaryTargetType: enumValue(
          item.primaryTargetType,
          'search.primaryTargetType',
          portalTargetTypes,
        ),
        primaryTargetTitle: string(item.primaryTargetTitle, 'search.primaryTargetTitle'),
        breadcrumb: decodeBreadcrumb(item.breadcrumb, 'search.breadcrumb'),
        snippet: string(item.snippet, 'search.snippet'),
      }
    }),
    page: integer(root.page, 'portalSearch.page', 1),
    pageSize: integer(root.pageSize, 'portalSearch.pageSize', 1),
    total: integer(root.total, 'portalSearch.total'),
  }
}
