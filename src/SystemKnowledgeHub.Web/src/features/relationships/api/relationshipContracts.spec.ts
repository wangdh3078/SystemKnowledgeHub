import { describe, expect, it } from 'vitest'
import { contextualRelationTypeLabel, decodeRelationshipDetail, relationTypes } from './relationshipContracts'

describe('relationship vocabulary contract', () => {
  it('exposes only the retained relation values', () => {
    expect(relationTypes).toEqual([
      'Calls', 'Reads', 'Writes', 'UsesField', 'AppliesRule', 'PublishesVia', 'ConsumesVia', 'UsesIntegration', 'DependsOn',
      'Documents', 'References', 'AppliesTo', 'SpecifiedBy', 'VerifiedBy', 'Supersedes',
    ])
  })

  it('rejects legacy relation values returned by the API', () => {
    expect(() => decodeRelationshipDetail({
      id: 1, concurrencyToken: 'token',
      source: { target: { type: 'KnowledgeDocument', id: 1 }, title: '需求', systemContext: '' },
      target: { target: { type: 'System', id: 12 }, title: 'MES', systemContext: 'MES' },
      relationType: 'RelatedTo', description: null, knowledgeStatus: 'Unknown', evidence: [], unknownItems: [],
      created: { displayName: '测试', roleOrIdentity: null, occurredAt: '2026-08-23T00:00:00Z' },
      statusChanged: { displayName: '测试', roleOrIdentity: null, occurredAt: '2026-08-23T00:00:00Z' }, availableActions: [],
    })).toThrow('relationType invalid')
  })

  it('uses the directed semantic label when a relation is displayed from its target side', () => {
    expect(contextualRelationTypeLabel('Documents', 'Incoming')).toBe('由文档说明')
    expect(contextualRelationTypeLabel('SpecifiedBy', 'Incoming')).toBe('定义需求')
    expect(contextualRelationTypeLabel('VerifiedBy', 'Incoming')).toBe('验证需求/规格')
  })
})
