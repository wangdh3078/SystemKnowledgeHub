export const impactPathKinds = [
  'DirectAppliesTo',
  'DirectDocuments',
  'ViaSpecificationDocuments',
  'ViaRequirementAppliesTo',
  'ViaRequirementDocuments',
  'ViaVerifiedRequirementAppliesTo',
  'ViaVerifiedSpecificationDocuments',
] as const

export const impactMeanings = [
  'ExplicitRequirementScope',
  'DocumentedByRequirement',
  'DocumentedBySpecification',
  'DocumentedByTestCase',
  'UpstreamRequirementScope',
  'UpstreamRequirementDocumentedContext',
  'VerifiedRequirementScope',
  'VerifiedSpecificationDocumentedContext',
] as const

export const impactTargetTypes = [
  'System',
  'BusinessFunction',
  'DatabaseObject',
  'BusinessRule',
  'Integration',
] as const

export const impactRelationTypes = ['AppliesTo', 'Documents', 'SpecifiedBy', 'VerifiedBy'] as const
export const impactDirections = ['Outgoing', 'Incoming'] as const

export type ImpactPathKind = (typeof impactPathKinds)[number]
export type ImpactMeaning = (typeof impactMeanings)[number]
export type ImpactTargetType = (typeof impactTargetTypes)[number]
export type ImpactRelationType = (typeof impactRelationTypes)[number]
export type ImpactDirection = (typeof impactDirections)[number]

export interface ImpactPathSegment {
  readonly relationshipId: number
  readonly relationType: ImpactRelationType
  readonly direction: ImpactDirection
}

export interface ImpactSystemContext {
  readonly id: number
  readonly name: string
}

export interface ImpactTarget {
  readonly type: ImpactTargetType
  readonly id: number
  readonly title: string
  readonly systemContext: readonly ImpactSystemContext[]
}

export interface ImpactItem {
  readonly pathKind: ImpactPathKind
  readonly meaning: ImpactMeaning
  readonly target: ImpactTarget
  readonly path: readonly ImpactPathSegment[]
}

export interface ImpactResponse {
  readonly items: readonly ImpactItem[]
  readonly page: number
  readonly pageSize: number
  readonly total: number
  readonly maxDepth: 2
}

type JsonObject = Readonly<Record<string, unknown>>

function readObject(value: unknown, field: string): JsonObject {
  if (typeof value !== 'object' || value === null || Array.isArray(value))
    throw new Error(`${field} must be an object`)
  return value as JsonObject
}

function readArray(value: unknown, field: string): readonly unknown[] {
  if (!Array.isArray(value)) throw new Error(`${field} must be an array`)
  return value
}

function readString(value: unknown, field: string): string {
  if (typeof value !== 'string') throw new Error(`${field} must be a string`)
  return value
}

function readNonBlankString(value: unknown, field: string): string {
  const text = readString(value, field)
  if (!text.trim()) throw new Error(`${field} must not be blank`)
  return text
}

function readPositiveInteger(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 1)
    throw new Error(`${field} must be a safe positive integer`)
  return value
}

function readNonNegativeInteger(value: unknown, field: string): number {
  if (typeof value !== 'number' || !Number.isSafeInteger(value) || value < 0)
    throw new Error(`${field} must be a non-negative integer`)
  return value
}

function readClosedValue<TValue extends string>(
  value: unknown,
  field: string,
  values: readonly TValue[],
): TValue {
  const text = readString(value, field)
  if (!values.includes(text as TValue)) throw new Error(`${field} has an unsupported value`)
  return text as TValue
}

function readSystemContext(value: unknown, field: string): ImpactSystemContext {
  const context = readObject(value, field)
  return {
    id: readPositiveInteger(context.id, `${field}.id`),
    name: readNonBlankString(context.name, `${field}.name`),
  }
}

function readTarget(value: unknown, field: string): ImpactTarget {
  const target = readObject(value, field)
  return {
    type: readClosedValue(target.type, `${field}.type`, impactTargetTypes),
    id: readPositiveInteger(target.id, `${field}.id`),
    title: readNonBlankString(target.title, `${field}.title`),
    systemContext: readArray(target.systemContext, `${field}.systemContext`).map((item, index) =>
      readSystemContext(item, `${field}.systemContext[${index}]`),
    ),
  }
}

function readPath(value: unknown, field: string): readonly ImpactPathSegment[] {
  const path = readArray(value, field).map((item, index) => {
    const segment = readObject(item, `${field}[${index}]`)
    return {
      relationshipId: readPositiveInteger(
        segment.relationshipId,
        `${field}[${index}].relationshipId`,
      ),
      relationType: readClosedValue(
        segment.relationType,
        `${field}[${index}].relationType`,
        impactRelationTypes,
      ),
      direction: readClosedValue(
        segment.direction,
        `${field}[${index}].direction`,
        impactDirections,
      ),
    }
  })
  if (path.length < 1 || path.length > 2) throw new Error(`${field} must contain one or two segments`)
  return path
}

function validatePathContract(item: ImpactItem, field: string): void {
  const expected: Readonly<
    Record<ImpactPathKind, { meaning: readonly ImpactMeaning[]; path: readonly string[] }>
  > = {
    DirectAppliesTo: {
      meaning: ['ExplicitRequirementScope'],
      path: ['AppliesTo:Outgoing'],
    },
    DirectDocuments: {
      meaning: ['DocumentedByRequirement', 'DocumentedBySpecification', 'DocumentedByTestCase'],
      path: ['Documents:Outgoing'],
    },
    ViaSpecificationDocuments: {
      meaning: ['DocumentedBySpecification'],
      path: ['SpecifiedBy:Outgoing', 'Documents:Outgoing'],
    },
    ViaRequirementAppliesTo: {
      meaning: ['UpstreamRequirementScope'],
      path: ['SpecifiedBy:Incoming', 'AppliesTo:Outgoing'],
    },
    ViaRequirementDocuments: {
      meaning: ['UpstreamRequirementDocumentedContext'],
      path: ['SpecifiedBy:Incoming', 'Documents:Outgoing'],
    },
    ViaVerifiedRequirementAppliesTo: {
      meaning: ['VerifiedRequirementScope'],
      path: ['VerifiedBy:Incoming', 'AppliesTo:Outgoing'],
    },
    ViaVerifiedSpecificationDocuments: {
      meaning: ['VerifiedSpecificationDocumentedContext'],
      path: ['VerifiedBy:Incoming', 'Documents:Outgoing'],
    },
  }
  const contract = expected[item.pathKind]
  if (!contract.meaning.includes(item.meaning))
    throw new Error(`${field}.meaning does not match pathKind`)
  const actualPath = item.path.map((segment) => `${segment.relationType}:${segment.direction}`)
  if (actualPath.length !== contract.path.length || actualPath.some((value, index) => value !== contract.path[index]))
    throw new Error(`${field}.path does not match pathKind`)
}

function readItem(value: unknown, field: string): ImpactItem {
  const source = readObject(value, field)
  const item: ImpactItem = {
    pathKind: readClosedValue(source.pathKind, `${field}.pathKind`, impactPathKinds),
    meaning: readClosedValue(source.meaning, `${field}.meaning`, impactMeanings),
    target: readTarget(source.target, `${field}.target`),
    path: readPath(source.path, `${field}.path`),
  }
  validatePathContract(item, field)
  return item
}

export function decodeImpactResponse(value: unknown): ImpactResponse {
  const response = readObject(value, 'impact')
  const maxDepth = readPositiveInteger(response.maxDepth, 'maxDepth')
  if (maxDepth !== 2) throw new Error('maxDepth must be 2')
  const pageSize = readPositiveInteger(response.pageSize, 'pageSize')
  if (pageSize > 100) throw new Error('pageSize must not exceed 100')
  return {
    items: readArray(response.items, 'items').map((item, index) =>
      readItem(item, `items[${index}]`),
    ),
    page: readPositiveInteger(response.page, 'page'),
    pageSize,
    total: readNonNegativeInteger(response.total, 'total'),
    maxDepth: 2,
  }
}
