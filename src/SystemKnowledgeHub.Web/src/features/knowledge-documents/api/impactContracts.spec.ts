import { describe, expect, it } from 'vitest'
import {
  decodeImpactResponse,
  impactMeanings,
  impactPathKinds,
  impactTargetTypes,
} from './impactContracts'

const pathContracts = {
  DirectAppliesTo: {
    meaning: 'ExplicitRequirementScope',
    path: [{ relationshipId: 1, relationType: 'AppliesTo', direction: 'Outgoing' }],
  },
  DirectDocuments: {
    meaning: 'DocumentedByRequirement',
    path: [{ relationshipId: 2, relationType: 'Documents', direction: 'Outgoing' }],
  },
  ViaSpecificationDocuments: {
    meaning: 'DocumentedBySpecification',
    path: [
      { relationshipId: 3, relationType: 'SpecifiedBy', direction: 'Outgoing' },
      { relationshipId: 4, relationType: 'Documents', direction: 'Outgoing' },
    ],
  },
  ViaRequirementAppliesTo: {
    meaning: 'UpstreamRequirementScope',
    path: [
      { relationshipId: 5, relationType: 'SpecifiedBy', direction: 'Incoming' },
      { relationshipId: 6, relationType: 'AppliesTo', direction: 'Outgoing' },
    ],
  },
  ViaRequirementDocuments: {
    meaning: 'UpstreamRequirementDocumentedContext',
    path: [
      { relationshipId: 7, relationType: 'SpecifiedBy', direction: 'Incoming' },
      { relationshipId: 8, relationType: 'Documents', direction: 'Outgoing' },
    ],
  },
  ViaVerifiedRequirementAppliesTo: {
    meaning: 'VerifiedRequirementScope',
    path: [
      { relationshipId: 9, relationType: 'VerifiedBy', direction: 'Incoming' },
      { relationshipId: 10, relationType: 'AppliesTo', direction: 'Outgoing' },
    ],
  },
  ViaVerifiedSpecificationDocuments: {
    meaning: 'VerifiedSpecificationDocumentedContext',
    path: [
      { relationshipId: 11, relationType: 'VerifiedBy', direction: 'Incoming' },
      { relationshipId: 12, relationType: 'Documents', direction: 'Outgoing' },
    ],
  },
} as const

interface ImpactFixture {
  items: Array<{
    pathKind: string
    meaning: string
    target: { type: string; id: number; title: string; systemContext: Array<{ id: number; name: string }> }
    path: Array<{ relationshipId: number; relationType: string; direction: string }>
  }>
  page: number
  pageSize: number
  total: number
  maxDepth: number
}

function validResponse(): ImpactFixture {
  return {
    items: impactPathKinds.map((pathKind, index) => ({
      pathKind,
      meaning: pathContracts[pathKind].meaning,
      path: pathContracts[pathKind].path.map((segment) => ({ ...segment })),
      target: {
        type: impactTargetTypes[index % impactTargetTypes.length],
        id: index + 1,
        title: `Target ${index + 1}`,
        systemContext: [{ id: 100 + index, name: `System ${index + 1}` }],
      },
    })),
    page: 1,
    pageSize: 20,
    total: impactPathKinds.length,
    maxDepth: 2,
  }
}

describe('impact contracts', () => {
  it('decodes every closed path kind, target type, path segment, and pagination field', () => {
    const decoded = decodeImpactResponse(validResponse())
    expect(decoded.items.map((item) => item.pathKind)).toEqual(impactPathKinds)
    expect(new Set(decoded.items.map((item) => item.target.type))).toEqual(new Set(impactTargetTypes))
    expect(decoded).toMatchObject({ page: 1, pageSize: 20, total: 7, maxDepth: 2 })
  })

  it('accepts each closed meaning on its valid path contract', () => {
    const cases = [
      ['ExplicitRequirementScope', 'DirectAppliesTo'],
      ['DocumentedByRequirement', 'DirectDocuments'],
      ['DocumentedBySpecification', 'DirectDocuments'],
      ['DocumentedByTestCase', 'DirectDocuments'],
      ['UpstreamRequirementScope', 'ViaRequirementAppliesTo'],
      ['UpstreamRequirementDocumentedContext', 'ViaRequirementDocuments'],
      ['VerifiedRequirementScope', 'ViaVerifiedRequirementAppliesTo'],
      ['VerifiedSpecificationDocumentedContext', 'ViaVerifiedSpecificationDocuments'],
    ] as const
    expect(cases.map(([meaning, pathKind], index) =>
      decodeImpactResponse({
        ...validResponse(),
        items: [{
          pathKind,
          meaning,
          path: pathContracts[pathKind].path,
          target: { type: 'System', id: index + 1, title: 'Target', systemContext: [] },
        }],
        total: 1,
      }).items[0].meaning,
    )).toEqual(impactMeanings)
  })

  it.each([
    ['unknown pathKind', (value: ReturnType<typeof validResponse>) => { value.items[0].pathKind = 'DynamicPath' as never }],
    ['unknown meaning', (value: ReturnType<typeof validResponse>) => { value.items[0].meaning = 'BlastRadius' as never }],
    ['unsafe target id', (value: ReturnType<typeof validResponse>) => { value.items[0].target.id = Number.MAX_SAFE_INTEGER + 1 }],
    ['negative total', (value: ReturnType<typeof validResponse>) => { value.total = -1 }],
    ['invalid page', (value: ReturnType<typeof validResponse>) => { value.page = 0 }],
    ['oversized pageSize', (value: ReturnType<typeof validResponse>) => { value.pageSize = 101 }],
    ['invalid target type', (value: ReturnType<typeof validResponse>) => { value.items[0].target.type = 'DatabaseSource' as never }],
    ['blank title', (value: ReturnType<typeof validResponse>) => { value.items[0].target.title = ' ' }],
    ['invalid relation type', (value: ReturnType<typeof validResponse>) => { value.items[0].path[0].relationType = 'References' as never }],
    ['missing path', (value: ReturnType<typeof validResponse>) => { delete (value.items[0] as { path?: unknown }).path }],
    ['path mismatch', (value: ReturnType<typeof validResponse>) => { value.items[0].path[0].direction = 'Incoming' }],
    ['invalid max depth', (value: ReturnType<typeof validResponse>) => { value.maxDepth = 3 }],
  ])('rejects %s', (_, mutate) => {
    const value = structuredClone(validResponse())
    mutate(value)
    expect(() => decodeImpactResponse(value)).toThrow()
  })
})
