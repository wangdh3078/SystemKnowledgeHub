import { isKnowledgeStatus, type KnowledgeStatus } from '../../../api/contracts/knowledge'
import {
  documentLifecycleStatuses,
  type ConfirmationCoverageState,
  type DocumentLifecycleStatus,
} from './knowledgeDocumentContracts'

export const traceDocumentTypes = ['Requirement', 'Specification', 'TestCase'] as const
export const traceRelationTypes = ['SpecifiedBy', 'VerifiedBy', 'Supersedes'] as const
export const traceDirections = ['Outgoing', 'Incoming'] as const
export const traceCoverageEligibilities = ['Active', 'ExcludedArchived'] as const
export const traceMissingLinkCodes = ['MissingSpecification', 'MissingTestDefinition'] as const
export const traceTruncationReasons = ['MaxNodes', 'MaxEdges'] as const

export type TraceDocumentType = (typeof traceDocumentTypes)[number]
export type TraceRelationType = (typeof traceRelationTypes)[number]
export type TraceDirection = (typeof traceDirections)[number]
export type TraceCoverageEligibility = (typeof traceCoverageEligibilities)[number]
export type TraceMissingLinkCode = (typeof traceMissingLinkCodes)[number]
export type TraceTruncationReason = (typeof traceTruncationReasons)[number]

export interface TraceConfirmationCoverage {
  readonly state: ConfirmationCoverageState
  readonly lastConfirmedRevisionNumber: number | null
}

export interface TraceDocument {
  readonly id: number
  readonly documentType: TraceDocumentType
  readonly title: string
  readonly lifecycleStatus: DocumentLifecycleStatus
  readonly knowledgeStatus: KnowledgeStatus
  readonly currentRevisionNumber: number
  readonly evidenceCount: number
  readonly humanConfirmationCount: number
  readonly confirmationCoverage: TraceConfirmationCoverage
}

export interface TraceRelationship {
  readonly id: number
  readonly relationType: TraceRelationType
  readonly direction: TraceDirection
  readonly knowledgeStatus: KnowledgeStatus
  readonly evidenceCount: number
  readonly humanConfirmationCount: number
}

export interface TraceDocumentRelation {
  readonly relationship: TraceRelationship
  readonly document: TraceDocument
}

export interface TraceLineage {
  readonly incoming: readonly TraceDocumentRelation[]
  readonly outgoing: readonly TraceDocumentRelation[]
  readonly total: number
  readonly isTruncated: boolean
}

export interface TraceLimits {
  readonly maxDepth: number
  readonly maxNodes: number
  readonly maxEdges: number
  readonly maxLineageEntries: number
}

interface TraceabilityMetadata {
  readonly lineage: TraceLineage
  readonly cycleDetected: boolean
  readonly isTruncated: boolean
  readonly truncationReasons: readonly TraceTruncationReason[]
  readonly limits: TraceLimits
}

export interface RequirementTraceabilityResponse extends TraceabilityMetadata {
  readonly root: TraceDocument & { readonly documentType: 'Requirement' }
  readonly coverage: {
    readonly eligibility: TraceCoverageEligibility
    readonly hasSpecification: boolean
    readonly hasDirectTestDefinition: boolean
    readonly hasSpecificationTestDefinition: boolean
    readonly hasAnyTestDefinition: boolean
    readonly missingLinkCodes: readonly TraceMissingLinkCode[]
  }
  readonly specifications: readonly {
    readonly relationship: TraceRelationship
    readonly document: TraceDocument & { readonly documentType: 'Specification' }
    readonly coverage: {
      readonly hasTestDefinition: boolean
      readonly missingLinkCodes: readonly TraceMissingLinkCode[]
    }
    readonly testCases: readonly TraceDocumentRelation[]
  }[]
  readonly directTestCases: readonly TraceDocumentRelation[]
  readonly upstreamRequirements: readonly TraceDocumentRelation[]
}

export interface SpecificationTraceabilityResponse extends TraceabilityMetadata {
  readonly root: TraceDocument & { readonly documentType: 'Specification' }
  readonly coverage: {
    readonly eligibility: TraceCoverageEligibility
    readonly hasTestDefinition: boolean
    readonly missingLinkCodes: readonly TraceMissingLinkCode[]
  }
  readonly upstreamRequirements: readonly TraceDocumentRelation[]
  readonly testCases: readonly TraceDocumentRelation[]
}

export interface TestCaseTraceabilityResponse extends TraceabilityMetadata {
  readonly root: TraceDocument & { readonly documentType: 'TestCase' }
  readonly coverage: {
    readonly eligibility: TraceCoverageEligibility
    readonly missingLinkCodes: readonly TraceMissingLinkCode[]
  }
  readonly directRequirements: readonly TraceDocumentRelation[]
  readonly upstreamSpecifications: readonly {
    readonly relationship: TraceRelationship
    readonly document: TraceDocument & { readonly documentType: 'Specification' }
    readonly upstreamRequirements: readonly TraceDocumentRelation[]
  }[]
}

export type TraceabilityResponse =
  | RequirementTraceabilityResponse
  | SpecificationTraceabilityResponse
  | TestCaseTraceabilityResponse

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

function readBoolean(value: unknown, field: string): boolean {
  if (typeof value !== 'boolean') throw new Error(`${field} must be a boolean`)
  return value
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

function readNullablePositiveInteger(value: unknown, field: string): number | null {
  return value === null ? null : readPositiveInteger(value, field)
}

function readClosedValue<TValue extends string>(
  value: unknown,
  field: string,
  values: readonly TValue[],
): TValue {
  const item = readString(value, field)
  if (!values.includes(item as TValue)) throw new Error(`${field} has an unsupported value`)
  return item as TValue
}

function readKnowledgeStatus(value: unknown, field: string): KnowledgeStatus {
  if (!isKnowledgeStatus(value)) throw new Error(`${field} has an unsupported knowledge status`)
  return value
}

function readConfirmationCoverage(value: unknown, field: string): TraceConfirmationCoverage {
  const coverage = readObject(value, field)
  const state = readClosedValue(
    coverage.state,
    `${field}.state`,
    ['NoConfirmation', 'LegacyConfirmationUnknown', 'CurrentRevisionConfirmed', 'ChangedSinceConfirmation'] as const,
  )
  return {
    state,
    lastConfirmedRevisionNumber: readNullablePositiveInteger(
      coverage.lastConfirmedRevisionNumber,
      `${field}.lastConfirmedRevisionNumber`,
    ),
  }
}

function readDocument(value: unknown, field: string): TraceDocument {
  const document = readObject(value, field)
  return {
    id: readPositiveInteger(document.id, `${field}.id`),
    documentType: readClosedValue(document.documentType, `${field}.documentType`, traceDocumentTypes),
    title: readString(document.title, `${field}.title`),
    lifecycleStatus: readClosedValue(
      document.lifecycleStatus,
      `${field}.lifecycleStatus`,
      documentLifecycleStatuses,
    ),
    knowledgeStatus: readKnowledgeStatus(document.knowledgeStatus, `${field}.knowledgeStatus`),
    currentRevisionNumber: readPositiveInteger(
      document.currentRevisionNumber,
      `${field}.currentRevisionNumber`,
    ),
    evidenceCount: readNonNegativeInteger(document.evidenceCount, `${field}.evidenceCount`),
    humanConfirmationCount: readNonNegativeInteger(
      document.humanConfirmationCount,
      `${field}.humanConfirmationCount`,
    ),
    confirmationCoverage: readConfirmationCoverage(
      document.confirmationCoverage,
      `${field}.confirmationCoverage`,
    ),
  }
}

function readRelationship(value: unknown, field: string): TraceRelationship {
  const relationship = readObject(value, field)
  return {
    id: readPositiveInteger(relationship.id, `${field}.id`),
    relationType: readClosedValue(
      relationship.relationType,
      `${field}.relationType`,
      traceRelationTypes,
    ),
    direction: readClosedValue(relationship.direction, `${field}.direction`, traceDirections),
    knowledgeStatus: readKnowledgeStatus(
      relationship.knowledgeStatus,
      `${field}.knowledgeStatus`,
    ),
    evidenceCount: readNonNegativeInteger(
      relationship.evidenceCount,
      `${field}.evidenceCount`,
    ),
    humanConfirmationCount: readNonNegativeInteger(
      relationship.humanConfirmationCount,
      `${field}.humanConfirmationCount`,
    ),
  }
}

function readDocumentRelation(value: unknown, field: string): TraceDocumentRelation {
  const relation = readObject(value, field)
  return {
    relationship: readRelationship(relation.relationship, `${field}.relationship`),
    document: readDocument(relation.document, `${field}.document`),
  }
}

function readDocumentRelations(value: unknown, field: string): readonly TraceDocumentRelation[] {
  return readArray(value, field).map((item, index) =>
    readDocumentRelation(item, `${field}[${index}]`),
  )
}

function readMissingLinkCodes(value: unknown, field: string): readonly TraceMissingLinkCode[] {
  return readArray(value, field).map((item, index) =>
    readClosedValue(item, `${field}[${index}]`, traceMissingLinkCodes),
  )
}

function readMetadata(root: JsonObject): TraceabilityMetadata {
  const lineage = readObject(root.lineage, 'lineage')
  const limits = readObject(root.limits, 'limits')
  return {
    lineage: {
      incoming: readDocumentRelations(lineage.incoming, 'lineage.incoming'),
      outgoing: readDocumentRelations(lineage.outgoing, 'lineage.outgoing'),
      total: readNonNegativeInteger(lineage.total, 'lineage.total'),
      isTruncated: readBoolean(lineage.isTruncated, 'lineage.isTruncated'),
    },
    cycleDetected: readBoolean(root.cycleDetected, 'cycleDetected'),
    isTruncated: readBoolean(root.isTruncated, 'isTruncated'),
    truncationReasons: readArray(root.truncationReasons, 'truncationReasons').map((item, index) =>
      readClosedValue(item, `truncationReasons[${index}]`, traceTruncationReasons),
    ),
    limits: {
      maxDepth: readPositiveInteger(limits.maxDepth, 'limits.maxDepth'),
      maxNodes: readPositiveInteger(limits.maxNodes, 'limits.maxNodes'),
      maxEdges: readPositiveInteger(limits.maxEdges, 'limits.maxEdges'),
      maxLineageEntries: readPositiveInteger(
        limits.maxLineageEntries,
        'limits.maxLineageEntries',
      ),
    },
  }
}

export function decodeTraceabilityResponse(value: unknown): TraceabilityResponse {
  const root = readObject(value, 'traceability')
  const rootDocument = readDocument(root.root, 'root')
  const coverage = readObject(root.coverage, 'coverage')
  const metadata = readMetadata(root)
  const eligibility = readClosedValue(
    coverage.eligibility,
    'coverage.eligibility',
    traceCoverageEligibilities,
  )
  const missingLinkCodes = readMissingLinkCodes(
    coverage.missingLinkCodes,
    'coverage.missingLinkCodes',
  )

  if (rootDocument.documentType === 'Requirement') {
    return {
      root: { ...rootDocument, documentType: 'Requirement' },
      coverage: {
        eligibility,
        hasSpecification: readBoolean(coverage.hasSpecification, 'coverage.hasSpecification'),
        hasDirectTestDefinition: readBoolean(
          coverage.hasDirectTestDefinition,
          'coverage.hasDirectTestDefinition',
        ),
        hasSpecificationTestDefinition: readBoolean(
          coverage.hasSpecificationTestDefinition,
          'coverage.hasSpecificationTestDefinition',
        ),
        hasAnyTestDefinition: readBoolean(
          coverage.hasAnyTestDefinition,
          'coverage.hasAnyTestDefinition',
        ),
        missingLinkCodes,
      },
      specifications: readArray(root.specifications, 'specifications').map((item, index) => {
        const branch = readObject(item, `specifications[${index}]`)
        const document = readDocument(branch.document, `specifications[${index}].document`)
        if (document.documentType !== 'Specification')
          throw new Error(`specifications[${index}].document must be a Specification`)
        const branchCoverage = readObject(branch.coverage, `specifications[${index}].coverage`)
        return {
          relationship: readRelationship(
            branch.relationship,
            `specifications[${index}].relationship`,
          ),
          document: { ...document, documentType: 'Specification' as const },
          coverage: {
            hasTestDefinition: readBoolean(
              branchCoverage.hasTestDefinition,
              `specifications[${index}].coverage.hasTestDefinition`,
            ),
            missingLinkCodes: readMissingLinkCodes(
              branchCoverage.missingLinkCodes,
              `specifications[${index}].coverage.missingLinkCodes`,
            ),
          },
          testCases: readDocumentRelations(
            branch.testCases,
            `specifications[${index}].testCases`,
          ),
        }
      }),
      directTestCases: readDocumentRelations(root.directTestCases, 'directTestCases'),
      upstreamRequirements: readDocumentRelations(
        root.upstreamRequirements,
        'upstreamRequirements',
      ),
      ...metadata,
    }
  }

  if (rootDocument.documentType === 'Specification') {
    return {
      root: { ...rootDocument, documentType: 'Specification' },
      coverage: {
        eligibility,
        hasTestDefinition: readBoolean(
          coverage.hasTestDefinition,
          'coverage.hasTestDefinition',
        ),
        missingLinkCodes,
      },
      upstreamRequirements: readDocumentRelations(
        root.upstreamRequirements,
        'upstreamRequirements',
      ),
      testCases: readDocumentRelations(root.testCases, 'testCases'),
      ...metadata,
    }
  }

  return {
    root: { ...rootDocument, documentType: 'TestCase' },
    coverage: { eligibility, missingLinkCodes },
    directRequirements: readDocumentRelations(root.directRequirements, 'directRequirements'),
    upstreamSpecifications: readArray(root.upstreamSpecifications, 'upstreamSpecifications').map(
      (item, index) => {
        const branch = readObject(item, `upstreamSpecifications[${index}]`)
        const document = readDocument(branch.document, `upstreamSpecifications[${index}].document`)
        if (document.documentType !== 'Specification')
          throw new Error(`upstreamSpecifications[${index}].document must be a Specification`)
        return {
          relationship: readRelationship(
            branch.relationship,
            `upstreamSpecifications[${index}].relationship`,
          ),
          document: { ...document, documentType: 'Specification' as const },
          upstreamRequirements: readDocumentRelations(
            branch.upstreamRequirements,
            `upstreamSpecifications[${index}].upstreamRequirements`,
          ),
        }
      },
    ),
    ...metadata,
  }
}
