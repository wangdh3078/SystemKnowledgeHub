import { describe, expect, it } from 'vitest'
import { decodeTraceabilityResponse } from './traceabilityContracts'

const node = (id: number, documentType: 'Requirement' | 'Specification' | 'TestCase') => ({
  id,
  documentType,
  title: `${documentType} ${id}`,
  lifecycleStatus: 'Draft',
  knowledgeStatus: 'Inferred',
  currentRevisionNumber: 2,
  evidenceCount: 3,
  humanConfirmationCount: 1,
  confirmationCoverage: {
    state: 'ChangedSinceConfirmation',
    lastConfirmedRevisionNumber: 1,
  },
})

const relationship = (
  id: number,
  relationType: 'SpecifiedBy' | 'VerifiedBy' | 'Supersedes',
  direction: 'Outgoing' | 'Incoming' = 'Outgoing',
) => ({
  id,
  relationType,
  direction,
  knowledgeStatus: 'Unknown',
  evidenceCount: 1,
  humanConfirmationCount: 0,
})

const metadata = {
  lineage: {
    incoming: [],
    outgoing: [{ relationship: relationship(90, 'Supersedes'), document: node(99, 'Requirement') }],
    total: 1,
    isTruncated: false,
  },
  cycleDetected: false,
  isTruncated: false,
  truncationReasons: [],
  limits: { maxDepth: 2, maxNodes: 200, maxEdges: 300, maxLineageEntries: 20 },
}

describe('traceability response decoder', () => {
  it('decodes the Requirement structural, trust, lineage, and missing-link contract', () => {
    const response = decodeTraceabilityResponse({
      root: node(1, 'Requirement'),
      coverage: {
        eligibility: 'Active',
        hasSpecification: true,
        hasDirectTestDefinition: true,
        hasSpecificationTestDefinition: true,
        hasAnyTestDefinition: true,
        missingLinkCodes: [],
      },
      specifications: [{
        relationship: relationship(10, 'SpecifiedBy'),
        document: node(2, 'Specification'),
        coverage: { hasTestDefinition: true, missingLinkCodes: [] },
        testCases: [{ relationship: relationship(11, 'VerifiedBy'), document: node(3, 'TestCase') }],
      }],
      directTestCases: [{ relationship: relationship(12, 'VerifiedBy'), document: node(3, 'TestCase') }],
      upstreamRequirements: [],
      ...metadata,
    })

    expect(response.root.documentType).toBe('Requirement')
    if (!('specifications' in response)) throw new Error('unexpected root')
    expect(response.specifications[0].coverage.hasTestDefinition).toBe(true)
    expect(response.directTestCases[0].document.id).toBe(3)
    expect(response.root.confirmationCoverage.state).toBe('ChangedSinceConfirmation')
    expect(response.lineage.outgoing[0].relationship.relationType).toBe('Supersedes')
    expect('bodyMarkdown' in response.root).toBe(false)
  })

  it('decodes the Specification projection and missing Test Definition code', () => {
    const response = decodeTraceabilityResponse({
      root: node(2, 'Specification'),
      coverage: {
        eligibility: 'Active',
        hasTestDefinition: false,
        missingLinkCodes: ['MissingTestDefinition'],
      },
      upstreamRequirements: [{
        relationship: relationship(10, 'SpecifiedBy', 'Incoming'),
        document: node(1, 'Requirement'),
      }],
      testCases: [],
      ...metadata,
      lineage: { incoming: [], outgoing: [], total: 0, isTruncated: false },
    })

    expect(response.root.documentType).toBe('Specification')
    if (!('testCases' in response)) throw new Error('unexpected root')
    expect(response.coverage.missingLinkCodes).toEqual(['MissingTestDefinition'])
    expect(response.upstreamRequirements[0].relationship.direction).toBe('Incoming')
  })

  it('decodes direct and Specification-path TestCase contexts independently', () => {
    const response = decodeTraceabilityResponse({
      root: node(3, 'TestCase'),
      coverage: { eligibility: 'Active', missingLinkCodes: [] },
      directRequirements: [{
        relationship: relationship(12, 'VerifiedBy', 'Incoming'),
        document: node(1, 'Requirement'),
      }],
      upstreamSpecifications: [{
        relationship: relationship(11, 'VerifiedBy', 'Incoming'),
        document: node(2, 'Specification'),
        upstreamRequirements: [{
          relationship: relationship(10, 'SpecifiedBy', 'Incoming'),
          document: node(1, 'Requirement'),
        }],
      }],
      ...metadata,
      lineage: { incoming: [], outgoing: [], total: 0, isTruncated: false },
    })

    expect(response.root.documentType).toBe('TestCase')
    if (!('directRequirements' in response)) throw new Error('unexpected root')
    expect(response.directRequirements[0].document.id).toBe(1)
    expect(response.upstreamSpecifications[0].upstreamRequirements[0].document.id).toBe(1)
  })

  it('decodes archived eligibility and controlled truncation', () => {
    const response = decodeTraceabilityResponse({
      root: { ...node(1, 'Requirement'), lifecycleStatus: 'Archived' },
      coverage: {
        eligibility: 'ExcludedArchived',
        hasSpecification: false,
        hasDirectTestDefinition: false,
        hasSpecificationTestDefinition: false,
        hasAnyTestDefinition: false,
        missingLinkCodes: [],
      },
      specifications: [],
      directTestCases: [],
      upstreamRequirements: [],
      ...metadata,
      isTruncated: true,
      truncationReasons: ['MaxNodes', 'MaxEdges'],
    })

    expect(response.coverage.eligibility).toBe('ExcludedArchived')
    expect(response.truncationReasons).toEqual(['MaxNodes', 'MaxEdges'])
  })

  it.each([
    ['invalid document type', { root: { ...node(1, 'Requirement'), documentType: 'Sop' } }],
    ['invalid relation type', { lineage: { ...metadata.lineage, outgoing: [{ relationship: relationship(90, 'Supersedes'), document: node(99, 'Requirement') }], incoming: [{ relationship: { ...relationship(91, 'VerifiedBy'), relationType: 'References' }, document: node(98, 'Requirement') }] } }],
    ['invalid missing code', { coverage: { eligibility: 'Active', hasSpecification: false, hasDirectTestDefinition: false, hasSpecificationTestDefinition: false, hasAnyTestDefinition: false, missingLinkCodes: ['MissingExecution'] } }],
    ['malformed count', { root: { ...node(1, 'Requirement'), evidenceCount: -1 } }],
  ])('fails closed for %s', (_name, replacement) => {
    const valid = {
      root: node(1, 'Requirement'),
      coverage: {
        eligibility: 'Active',
        hasSpecification: false,
        hasDirectTestDefinition: false,
        hasSpecificationTestDefinition: false,
        hasAnyTestDefinition: false,
        missingLinkCodes: ['MissingSpecification', 'MissingTestDefinition'],
      },
      specifications: [], directTestCases: [], upstreamRequirements: [], ...metadata,
    }
    expect(() => decodeTraceabilityResponse({ ...valid, ...replacement })).toThrow()
  })
})
