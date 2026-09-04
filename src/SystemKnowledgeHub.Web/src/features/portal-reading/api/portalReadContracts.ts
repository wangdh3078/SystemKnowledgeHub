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
] as const

export type PortalTargetType = (typeof portalTargetTypes)[number]
export type PortalNodeKind = (typeof portalNodeKinds)[number]
export type PortalProjectionKind = (typeof portalProjectionKinds)[number]

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

export type PortalSectionContent =
  | PortalSummaryContent
  | PortalKnowledgeDocumentBodyContent
  | PortalSystemOverviewContent
  | PortalBusinessFunctionOverviewContent
  | PortalDatabaseObjectOverviewContent
  | PortalIntegrationOverviewContent
  | PortalDatabaseStructureContent

export interface PortalPageSection {
  readonly id: number
  readonly heading: string
  readonly sourceKind: 'PrimaryTarget' | 'ExplicitReference'
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
        ] as const),
        projectionKind,
        content,
      }
    }),
  }
}
