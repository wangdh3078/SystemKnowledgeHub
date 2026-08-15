import { describe, expect, it } from 'vitest'
import {
  decodeDatabaseColumnDetail,
  decodeDatabaseObjectDetail,
} from './databaseKnowledgeContracts'

describe('database knowledge API contracts', () => {
  it('decodes the frozen database object detail shape', () => {
    const result = decodeDatabaseObjectDetail({
      id: 45,
      system: { id: 12, name: 'MES' },
      databaseSource: { id: 9, name: 'MES Oracle', engine: 'Oracle' },
      concurrencyToken: 'AAAAAQ',
      overview: {
        qualifiedName: 'MES.TABLE_EQP',
        objectType: 'Table',
        businessDescription: '设备主数据与当前状态',
        accessMode: 'ReadWrite',
        knowledgeStatus: 'Inferred',
      },
      metadata: { estimatedRows: 2400000, primaryKeyColumns: ['EQP_ID'], businessKeyColumns: ['EQP_CODE'] },
      columns: [{
        id: 123,
        ordinalPosition: 3,
        columnName: 'STATE_FLAG',
        dataType: 'VARCHAR2(20)',
        nullable: false,
        businessDescription: '设备状态标识',
        evidenceCount: 0,
        unknownCount: 0,
        knowledgeStatus: 'Inferred',
        selected: true,
      }],
      contextRail: { usedByFunctions: [], relatedRuleCount: 0, integrationCount: 0, openUnknownCount: 0 },
      selectedColumnDrawer: { columnId: 123 },
      availableActions: [],
    })

    expect(result.overview.qualifiedName).toBe('MES.TABLE_EQP')
    expect(result.columns[0]?.selected).toBe(true)
    expect(result.concurrencyToken).toBe('AAAAAQ')
  })

  it('decodes column detail and rejects an unknown status', () => {
    const payload = {
      id: 123,
      parent: { databaseObjectId: 45, qualifiedName: 'MES.TABLE_EQP' },
      system: { id: 12, name: 'MES' },
      concurrencyToken: 'AAAAAQ',
      databaseMetadata: { columnName: 'STATE_FLAG', dataType: 'VARCHAR2(20)', nullable: false, defaultValue: null, ordinalPosition: 3 },
      businessKnowledge: { description: '设备状态标识', knowledgeStatus: 'Inferred' },
      knownValues: [{ id: 701, value: '30', meaning: 'Unknown / Offline' }],
      evidence: [],
      relations: [],
      unknownItems: [],
      availableActions: [],
    }

    expect(decodeDatabaseColumnDetail(payload).knownValues[0]?.value).toBe('30')
    expect(() => decodeDatabaseColumnDetail({
      ...payload,
      businessKnowledge: { ...payload.businessKnowledge, knowledgeStatus: 'Invalid' },
    })).toThrow('knowledgeStatus')
  })
})
