import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'
import type { ActorContext } from '../../../app/stores/actor'

export type { KnowledgeStatus } from '../../../api/contracts/knowledge'

export interface SystemContext {
  readonly id: number
  readonly name: string
}

export interface DatabaseSourceContext {
  readonly id: number
  readonly name: string
  readonly engine: string
}

export type DatabaseObjectType = 'Table' | 'View'
export type DatabaseAccessMode = 'Read' | 'Write' | 'ReadWrite' | 'Unknown'
export type DatabaseObjectsSort =
  | 'objectName:asc'
  | 'objectName:desc'
  | 'schema:asc'
  | 'schema:desc'
  | 'estimatedRows:asc'
  | 'estimatedRows:desc'
  | 'knowledgeStatus:asc'
  | 'knowledgeStatus:desc'
  | 'unknownCount:asc'
  | 'unknownCount:desc'

export interface DatabaseObjectsListParameters {
  readonly systemId?: number
  readonly databaseSourceId?: number
  readonly schema?: string
  readonly objectType?: DatabaseObjectType
  readonly knowledgeStatus?: KnowledgeStatus
  readonly search?: string
  readonly sort: DatabaseObjectsSort
  readonly page: number
  readonly pageSize: number
}

export interface DatabaseObjectListItem {
  readonly id: number
  readonly databaseSource: DatabaseSourceContext
  readonly schema: string
  readonly objectName: string
  readonly objectType: DatabaseObjectType
  readonly businessDescription: string | null
  readonly estimatedRows: number | null
  readonly accessMode: DatabaseAccessMode
  readonly relatedFunctionCount: number
  readonly unknownCount: number
  readonly knowledgeStatus: KnowledgeStatus
  readonly matchedColumn: { readonly id: number; readonly columnName: string } | null
}

export interface DatabaseObjectsListResponse {
  readonly browseContext: {
    readonly system: SystemContext | null
    readonly databaseSources: readonly DatabaseSourceContext[]
    readonly schemas: readonly string[]
  }
  readonly items: readonly DatabaseObjectListItem[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
}

export interface CreateDatabaseSourceRequest {
  readonly systemId: number
  readonly name: string
  readonly engine: string
  readonly environment?: string | null
  readonly instanceName?: string | null
  readonly serviceName?: string | null
  readonly databaseName?: string | null
  readonly description?: string | null
  readonly isPrimary?: boolean
  readonly actor: ActorContext
}

export interface CreateDatabaseSourceResponse {
  readonly id: number
  readonly systemId: number
  readonly name: string
  readonly engine: string
  readonly concurrencyToken: string
}

export interface RegisterDatabaseObjectRequest {
  readonly databaseSourceId: number
  readonly schemaName: string
  readonly objectName: string
  readonly objectType: DatabaseObjectType
  readonly estimatedRows?: number | null
  readonly accessMode?: DatabaseAccessMode
  readonly primaryKeyColumns?: readonly string[] | null
  readonly businessKeyColumns?: readonly string[] | null
  readonly businessDescription?: string | null
  readonly actor: ActorContext
}

export interface RegisterDatabaseObjectResponse {
  readonly id: number
  readonly databaseSourceId: number
  readonly qualifiedName: string
  readonly objectType: DatabaseObjectType
  readonly knowledgeStatus: KnowledgeStatus
  readonly concurrencyToken: string
}

export interface DatabaseColumnSummary {
  readonly id: number
  readonly ordinalPosition: number
  readonly columnName: string
  readonly dataType: string
  readonly nullable: boolean
  readonly businessDescription: string | null
  readonly evidenceCount: number
  readonly unknownCount: number
  readonly knowledgeStatus: KnowledgeStatus
  readonly selected: boolean
}

export interface UsedByFunctionSummary {
  readonly id: number
  readonly name: string
  readonly relationType: string
  readonly reference: string | null
}

export interface DatabaseObjectDetailResponse {
  readonly id: number
  readonly system: SystemContext
  readonly databaseSource: DatabaseSourceContext
  readonly concurrencyToken: string
  readonly overview: {
    readonly qualifiedName: string
    readonly objectType: 'Table' | 'View'
    readonly businessDescription: string | null
    readonly accessMode: 'Read' | 'Write' | 'ReadWrite' | 'Unknown'
    readonly knowledgeStatus: KnowledgeStatus
  }
  readonly metadata: {
    readonly estimatedRows: number | null
    readonly primaryKeyColumns: readonly string[]
    readonly businessKeyColumns: readonly string[]
  }
  readonly columns: readonly DatabaseColumnSummary[]
  readonly contextRail: {
    readonly usedByFunctions: readonly UsedByFunctionSummary[]
    readonly relatedRuleCount: number
    readonly integrationCount: number
    readonly openUnknownCount: number
  }
  readonly selectedColumnDrawer: { readonly columnId: number } | null
  readonly availableActions: readonly string[]
}

export interface DatabaseColumnDetailResponse {
  readonly id: number
  readonly parent: {
    readonly databaseObjectId: number
    readonly qualifiedName: string
  }
  readonly system: SystemContext
  readonly concurrencyToken: string
  readonly databaseMetadata: {
    readonly columnName: string
    readonly dataType: string
    readonly nullable: boolean
    readonly defaultValue: string | null
    readonly ordinalPosition: number
  }
  readonly businessKnowledge: {
    readonly description: string | null
    readonly knowledgeStatus: KnowledgeStatus
  }
  readonly knownValues: readonly {
    readonly id: number
    readonly value: string
    readonly meaning: string
  }[]
  readonly evidence: readonly {
    readonly id: number
    readonly evidenceType: string
    readonly sourceTitle: string
    readonly supportReason: string
  }[]
  readonly relations: readonly {
    readonly id: number
    readonly relationType: string
    readonly otherObject: {
      readonly type: string
      readonly id: number
      readonly title: string
    }
  }[]
  readonly unknownItems: readonly {
    readonly id: number
    readonly question: string
    readonly status: string
  }[]
  readonly availableActions: readonly string[]
}

export interface RegisterDatabaseColumnRequest {
  readonly ordinalPosition: number
  readonly columnName: string
  readonly dataType: string
  readonly nullable: boolean
  readonly defaultValue?: string | null
  readonly databaseComment?: string | null
  readonly businessDescription?: string | null
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface RegisterDatabaseColumnResponse {
  readonly column: {
    readonly id: number
    readonly columnName: string
    readonly knowledgeStatus: KnowledgeStatus
    readonly concurrencyToken: string
  }
  readonly parentConcurrencyToken: string
}

export interface UpdateDatabaseObjectKnowledgeRequest {
  readonly businessDescription?: string | null
  readonly accessMode: DatabaseAccessMode
  readonly businessKeyColumns?: readonly string[] | null
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface DatabaseObjectKnowledgeResponse {
  readonly id: number
  readonly businessDescription: string | null
  readonly accessMode: DatabaseAccessMode
  readonly businessKeyColumns: readonly string[]
  readonly knowledgeStatus: KnowledgeStatus
  readonly concurrencyToken: string
}

export interface UpdateDatabaseColumnKnowledgeRequest {
  readonly businessDescription?: string | null
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface DatabaseColumnKnowledgeResponse {
  readonly id: number
  readonly businessDescription: string | null
  readonly knowledgeStatus: KnowledgeStatus
  readonly concurrencyToken: string
}

export interface AddColumnKnownValueRequest {
  readonly value: string
  readonly meaning: string
  readonly sortOrder?: number | null
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface AddColumnKnownValueResponse {
  readonly knownValue: {
    readonly id: number
    readonly value: string
    readonly meaning: string
    readonly sortOrder: number
  }
  readonly knowledgeStatus: KnowledgeStatus
  readonly concurrencyToken: string
}

export interface RemoveColumnKnownValueRequest {
  readonly confirmed: boolean
  readonly actor: ActorContext
  readonly concurrencyToken: string
}

export interface RemoveColumnKnownValueResponse {
  readonly columnId: number
  readonly knownValues: readonly {
    readonly id: number
    readonly value: string
    readonly meaning: string
    readonly sortOrder: number
  }[]
  readonly concurrencyToken: string
}

type JsonObject = Readonly<Record<string, unknown>>

function isJsonObject(value: unknown): value is JsonObject {
  return typeof value === 'object' && value !== null && !Array.isArray(value)
}

function readObject(value: unknown, field: string): JsonObject {
  if (!isJsonObject(value)) {
    throw new Error(`${field} must be an object`)
  }
  return value
}

function readArray(value: unknown, field: string): readonly unknown[] {
  if (!Array.isArray(value)) {
    throw new Error(`${field} must be an array`)
  }
  return value
}

function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') {
    throw new Error(`${field} must be a string`)
  }
  return value
}

function readNullableString(value: unknown, field: string): string | null {
  return value === null ? null : readString(value, field)
}

function readBoolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') {
    throw new Error(`${field} must be a boolean`)
  }
  return value
}

function readInteger(value: unknown, field: string, minimum = 0): number {
  if (!Number.isSafeInteger(value) || typeof value !== 'number' || value < minimum) {
    throw new Error(`${field} must be a safe integer`)
  }
  return value
}

function readId(value: unknown, field: string): number {
  return readInteger(value, field, 1)
}

function readKnowledgeStatus(value: unknown, field: string): KnowledgeStatus {
  if (isKnowledgeStatus(value)) {
    return value
  }
  throw new Error(`${field} has an unsupported status`)
}

function readDatabaseObjectType(value: unknown, field: string): DatabaseObjectType {
  const objectType = readString(value, field)
  if (objectType === 'Table' || objectType === 'View') return objectType
  throw new Error(`${field} has an unsupported object type`)
}

function readDatabaseAccessMode(value: unknown, field: string): DatabaseAccessMode {
  const accessMode = readString(value, field)
  if (
    accessMode === 'Read'
    || accessMode === 'Write'
    || accessMode === 'ReadWrite'
    || accessMode === 'Unknown'
  ) return accessMode
  throw new Error(`${field} has an unsupported access mode`)
}

function readStringArray(value: unknown, field: string): readonly string[] {
  return readArray(value, field).map((item, index) => readString(item, `${field}[${index}]`))
}

function readSystemContext(value: unknown, field: string): SystemContext {
  const item = readObject(value, field)
  return { id: readId(item.id, `${field}.id`), name: readString(item.name, `${field}.name`) }
}

function readDatabaseSourceContext(value: unknown, field: string): DatabaseSourceContext {
  const source = readObject(value, field)
  return {
    id: readId(source.id, `${field}.id`),
    name: readString(source.name, `${field}.name`),
    engine: readString(source.engine, `${field}.engine`),
  }
}

export function decodeDatabaseObjectsList(value: unknown): DatabaseObjectsListResponse {
  const root = readObject(value, 'databaseObjectsList')
  const browseContext = readObject(root.browseContext, 'browseContext')
  const system = browseContext.system === null
    ? null
    : readSystemContext(browseContext.system, 'browseContext.system')

  return {
    browseContext: {
      system,
      databaseSources: readArray(browseContext.databaseSources, 'browseContext.databaseSources')
        .map((item, index) => readDatabaseSourceContext(item, `browseContext.databaseSources[${index}]`)),
      schemas: readStringArray(browseContext.schemas, 'browseContext.schemas'),
    },
    items: readArray(root.items, 'items').map((value, index) => {
      const item = readObject(value, `items[${index}]`)
      const matchedColumn = item.matchedColumn === null
        ? null
        : (() => {
            const column = readObject(item.matchedColumn, `items[${index}].matchedColumn`)
            return {
              id: readId(column.id, `items[${index}].matchedColumn.id`),
              columnName: readString(column.columnName, `items[${index}].matchedColumn.columnName`),
            }
          })()
      return {
        id: readId(item.id, `items[${index}].id`),
        databaseSource: readDatabaseSourceContext(item.databaseSource, `items[${index}].databaseSource`),
        schema: readString(item.schema, `items[${index}].schema`),
        objectName: readString(item.objectName, `items[${index}].objectName`),
        objectType: readDatabaseObjectType(item.objectType, `items[${index}].objectType`),
        businessDescription: readNullableString(item.businessDescription, `items[${index}].businessDescription`),
        estimatedRows: item.estimatedRows === null
          ? null
          : readInteger(item.estimatedRows, `items[${index}].estimatedRows`),
        accessMode: readDatabaseAccessMode(item.accessMode, `items[${index}].accessMode`),
        relatedFunctionCount: readInteger(item.relatedFunctionCount, `items[${index}].relatedFunctionCount`),
        unknownCount: readInteger(item.unknownCount, `items[${index}].unknownCount`),
        knowledgeStatus: readKnowledgeStatus(item.knowledgeStatus, `items[${index}].knowledgeStatus`),
        matchedColumn,
      }
    }),
    page: readInteger(root.page, 'page', 1),
    pageSize: readInteger(root.pageSize, 'pageSize', 1),
    total: readInteger(root.total, 'total'),
  }
}

export function decodeCreateDatabaseSource(value: unknown): CreateDatabaseSourceResponse {
  const root = readObject(value, 'createdDatabaseSource')
  return {
    id: readId(root.id, 'id'),
    systemId: readId(root.systemId, 'systemId'),
    name: readString(root.name, 'name'),
    engine: readString(root.engine, 'engine'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}

export function decodeRegisterDatabaseObject(value: unknown): RegisterDatabaseObjectResponse {
  const root = readObject(value, 'registeredDatabaseObject')
  return {
    id: readId(root.id, 'id'),
    databaseSourceId: readId(root.databaseSourceId, 'databaseSourceId'),
    qualifiedName: readString(root.qualifiedName, 'qualifiedName'),
    objectType: readDatabaseObjectType(root.objectType, 'objectType'),
    knowledgeStatus: readKnowledgeStatus(root.knowledgeStatus, 'knowledgeStatus'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}

export function decodeDatabaseObjectDetail(value: unknown): DatabaseObjectDetailResponse {
  const root = readObject(value, 'databaseObjectDetail')
  const source = readObject(root.databaseSource, 'databaseSource')
  const overview = readObject(root.overview, 'overview')
  const metadata = readObject(root.metadata, 'metadata')
  const contextRail = readObject(root.contextRail, 'contextRail')
  const selectedColumnDrawer =
    root.selectedColumnDrawer === null
      ? null
      : (() => {
          const drawer = readObject(root.selectedColumnDrawer, 'selectedColumnDrawer')
          return { columnId: readId(drawer.columnId, 'selectedColumnDrawer.columnId') }
        })()

  const objectTypeValue = readString(overview.objectType, 'overview.objectType')
  if (objectTypeValue !== 'Table' && objectTypeValue !== 'View') {
    throw new Error('overview.objectType has an unsupported value')
  }
  const objectType: 'Table' | 'View' = objectTypeValue
  const accessModeValue = readString(overview.accessMode, 'overview.accessMode')
  if (
    accessModeValue !== 'Read' &&
    accessModeValue !== 'Write' &&
    accessModeValue !== 'ReadWrite' &&
    accessModeValue !== 'Unknown'
  ) {
    throw new Error('overview.accessMode has an unsupported value')
  }
  const accessMode: 'Read' | 'Write' | 'ReadWrite' | 'Unknown' = accessModeValue

  return {
    id: readId(root.id, 'id'),
    system: readSystemContext(root.system, 'system'),
    databaseSource: {
      id: readId(source.id, 'databaseSource.id'),
      name: readString(source.name, 'databaseSource.name'),
      engine: readString(source.engine, 'databaseSource.engine'),
    },
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
    overview: {
      qualifiedName: readString(overview.qualifiedName, 'overview.qualifiedName'),
      objectType,
      businessDescription: readNullableString(
        overview.businessDescription,
        'overview.businessDescription',
      ),
      accessMode,
      knowledgeStatus: readKnowledgeStatus(
        overview.knowledgeStatus,
        'overview.knowledgeStatus',
      ),
    },
    metadata: {
      estimatedRows:
        metadata.estimatedRows === null
          ? null
          : readInteger(metadata.estimatedRows, 'metadata.estimatedRows'),
      primaryKeyColumns: readStringArray(
        metadata.primaryKeyColumns,
        'metadata.primaryKeyColumns',
      ),
      businessKeyColumns: readStringArray(
        metadata.businessKeyColumns,
        'metadata.businessKeyColumns',
      ),
    },
    columns: readArray(root.columns, 'columns').map((value, index) => {
      const column = readObject(value, `columns[${index}]`)
      return {
        id: readId(column.id, `columns[${index}].id`),
        ordinalPosition: readInteger(
          column.ordinalPosition,
          `columns[${index}].ordinalPosition`,
          1,
        ),
        columnName: readString(column.columnName, `columns[${index}].columnName`),
        dataType: readString(column.dataType, `columns[${index}].dataType`),
        nullable: readBoolean(column.nullable, `columns[${index}].nullable`),
        businessDescription: readNullableString(
          column.businessDescription,
          `columns[${index}].businessDescription`,
        ),
        evidenceCount: readInteger(column.evidenceCount, `columns[${index}].evidenceCount`),
        unknownCount: readInteger(column.unknownCount, `columns[${index}].unknownCount`),
        knowledgeStatus: readKnowledgeStatus(
          column.knowledgeStatus,
          `columns[${index}].knowledgeStatus`,
        ),
        selected: readBoolean(column.selected, `columns[${index}].selected`),
      }
    }),
    contextRail: {
      usedByFunctions: readArray(contextRail.usedByFunctions, 'contextRail.usedByFunctions').map(
        (value, index) => {
          const item = readObject(value, `contextRail.usedByFunctions[${index}]`)
          return {
            id: readId(item.id, `contextRail.usedByFunctions[${index}].id`),
            name: readString(item.name, `contextRail.usedByFunctions[${index}].name`),
            relationType: readString(
              item.relationType,
              `contextRail.usedByFunctions[${index}].relationType`,
            ),
            reference: readNullableString(
              item.reference,
              `contextRail.usedByFunctions[${index}].reference`,
            ),
          }
        },
      ),
      relatedRuleCount: readInteger(contextRail.relatedRuleCount, 'contextRail.relatedRuleCount'),
      integrationCount: readInteger(contextRail.integrationCount, 'contextRail.integrationCount'),
      openUnknownCount: readInteger(contextRail.openUnknownCount, 'contextRail.openUnknownCount'),
    },
    selectedColumnDrawer,
    availableActions: readStringArray(root.availableActions, 'availableActions'),
  }
}

export function decodeDatabaseColumnDetail(value: unknown): DatabaseColumnDetailResponse {
  const root = readObject(value, 'databaseColumnDetail')
  const parent = readObject(root.parent, 'parent')
  const metadata = readObject(root.databaseMetadata, 'databaseMetadata')
  const knowledge = readObject(root.businessKnowledge, 'businessKnowledge')

  return {
    id: readId(root.id, 'id'),
    parent: {
      databaseObjectId: readId(parent.databaseObjectId, 'parent.databaseObjectId'),
      qualifiedName: readString(parent.qualifiedName, 'parent.qualifiedName'),
    },
    system: readSystemContext(root.system, 'system'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
    databaseMetadata: {
      columnName: readString(metadata.columnName, 'databaseMetadata.columnName'),
      dataType: readString(metadata.dataType, 'databaseMetadata.dataType'),
      nullable: readBoolean(metadata.nullable, 'databaseMetadata.nullable'),
      defaultValue: readNullableString(metadata.defaultValue, 'databaseMetadata.defaultValue'),
      ordinalPosition: readInteger(
        metadata.ordinalPosition,
        'databaseMetadata.ordinalPosition',
        1,
      ),
    },
    businessKnowledge: {
      description: readNullableString(knowledge.description, 'businessKnowledge.description'),
      knowledgeStatus: readKnowledgeStatus(
        knowledge.knowledgeStatus,
        'businessKnowledge.knowledgeStatus',
      ),
    },
    knownValues: readArray(root.knownValues, 'knownValues').map((value, index) => {
      const item = readObject(value, `knownValues[${index}]`)
      return {
        id: readId(item.id, `knownValues[${index}].id`),
        value: readString(item.value, `knownValues[${index}].value`),
        meaning: readString(item.meaning, `knownValues[${index}].meaning`),
      }
    }),
    evidence: readArray(root.evidence, 'evidence').map((value, index) => {
      const item = readObject(value, `evidence[${index}]`)
      return {
        id: readId(item.id, `evidence[${index}].id`),
        evidenceType: readString(item.evidenceType, `evidence[${index}].evidenceType`),
        sourceTitle: readString(item.sourceTitle, `evidence[${index}].sourceTitle`),
        supportReason: readString(item.supportReason, `evidence[${index}].supportReason`),
      }
    }),
    relations: readArray(root.relations, 'relations').map((value, index) => {
      const item = readObject(value, `relations[${index}]`)
      const otherObject = readObject(item.otherObject, `relations[${index}].otherObject`)
      return {
        id: readId(item.id, `relations[${index}].id`),
        relationType: readString(item.relationType, `relations[${index}].relationType`),
        otherObject: {
          type: readString(otherObject.type, `relations[${index}].otherObject.type`),
          id: readId(otherObject.id, `relations[${index}].otherObject.id`),
          title: readString(otherObject.title, `relations[${index}].otherObject.title`),
        },
      }
    }),
    unknownItems: readArray(root.unknownItems, 'unknownItems').map((value, index) => {
      const item = readObject(value, `unknownItems[${index}]`)
      return {
        id: readId(item.id, `unknownItems[${index}].id`),
        question: readString(item.question, `unknownItems[${index}].question`),
        status: readString(item.status, `unknownItems[${index}].status`),
      }
    }),
    availableActions: readStringArray(root.availableActions, 'availableActions'),
  }
}

export function decodeRegisterDatabaseColumn(value: unknown): RegisterDatabaseColumnResponse {
  const root = readObject(value, 'registeredDatabaseColumn')
  const column = readObject(root.column, 'column')
  return {
    column: {
      id: readId(column.id, 'column.id'),
      columnName: readString(column.columnName, 'column.columnName'),
      knowledgeStatus: readKnowledgeStatus(column.knowledgeStatus, 'column.knowledgeStatus'),
      concurrencyToken: readString(column.concurrencyToken, 'column.concurrencyToken'),
    },
    parentConcurrencyToken: readString(root.parentConcurrencyToken, 'parentConcurrencyToken'),
  }
}

export function decodeDatabaseObjectKnowledge(value: unknown): DatabaseObjectKnowledgeResponse {
  const root = readObject(value, 'databaseObjectKnowledge')
  return {
    id: readId(root.id, 'id'),
    businessDescription: readNullableString(root.businessDescription, 'businessDescription'),
    accessMode: readDatabaseAccessMode(root.accessMode, 'accessMode'),
    businessKeyColumns: readStringArray(root.businessKeyColumns, 'businessKeyColumns'),
    knowledgeStatus: readKnowledgeStatus(root.knowledgeStatus, 'knowledgeStatus'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}

export function decodeDatabaseColumnKnowledge(value: unknown): DatabaseColumnKnowledgeResponse {
  const root = readObject(value, 'databaseColumnKnowledge')
  return {
    id: readId(root.id, 'id'),
    businessDescription: readNullableString(root.businessDescription, 'businessDescription'),
    knowledgeStatus: readKnowledgeStatus(root.knowledgeStatus, 'knowledgeStatus'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}

function readKnownValueWrite(value: unknown, field: string): { readonly id: number; readonly value: string; readonly meaning: string; readonly sortOrder: number } {
  const item = readObject(value, field)
  return {
    id: readId(item.id, `${field}.id`),
    value: readString(item.value, `${field}.value`),
    meaning: readString(item.meaning, `${field}.meaning`),
    sortOrder: readInteger(item.sortOrder, `${field}.sortOrder`),
  }
}

export function decodeAddColumnKnownValue(value: unknown): AddColumnKnownValueResponse {
  const root = readObject(value, 'addColumnKnownValue')
  return {
    knownValue: readKnownValueWrite(root.knownValue, 'knownValue'),
    knowledgeStatus: readKnowledgeStatus(root.knowledgeStatus, 'knowledgeStatus'),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}

export function decodeRemoveColumnKnownValue(value: unknown): RemoveColumnKnownValueResponse {
  const root = readObject(value, 'removeColumnKnownValue')
  return {
    columnId: readId(root.columnId, 'columnId'),
    knownValues: readArray(root.knownValues, 'knownValues').map((item, index) =>
      readKnownValueWrite(item, `knownValues[${index}]`)),
    concurrencyToken: readString(root.concurrencyToken, 'concurrencyToken'),
  }
}
