import { describe, expect, it } from 'vitest'
import {
  decodeAddEvidence,
  decodeEvidenceDetail,
  getHumanConfirmationMethod,
  type EvidenceDetailResponse,
} from './evidenceContracts'

function humanConfirmation(
  sourceLocator: Readonly<Record<string, unknown>>,
  legacySource: string | null,
): EvidenceDetailResponse {
  return {
    id: 1,
    concurrencyToken: 'opaque-token',
    evidenceType: 'HumanConfirmation',
    subject: { type: 'BusinessFunction', id: 77 },
    subjectIdentity: {
      id: 77,
      targetType: 'BusinessFunction',
      displayName: '设备状态查询',
      isDeleted: false,
      isNavigable: true,
    },
    subjectDetailKey: 'Purpose',
    knowledgeDocumentRevisionNumberSnapshot: null,
    sourceTitle: '人工确认 · 王敏',
    sourceReference: null,
    sourceLocator,
    summary: '确认内容',
    supportReason: '支持理由',
    confidence: null,
    provider: {
      displayName: '王敏',
      roleOrIdentity: 'MES 业务专家',
      occurredAt: '2026-08-22T02:30:00Z',
      team: '制造系统组',
      externalUserKey: null,
      source: legacySource,
      note: null,
    },
    subjectContext: { title: 'MES · 设备状态查询', knowledgeStatus: 'Inferred' },
    availableActions: ['UpdateEvidence', 'ChangeKnowledgeStatus'],
  }
}

describe('getHumanConfirmationMethod', () => {
  it('uses the new locator method before the legacy provider source', () => {
    const detail = humanConfirmation({ confirmationMethod: 'Meeting' }, 'OnSite')
    expect(getHumanConfirmationMethod(detail)).toBe('Meeting')
  })

  it('falls back to provider source for legacy HumanConfirmation rows', () => {
    const detail = humanConfirmation({ confirmationStatement: 'legacy' }, 'OnSite')
    expect(getHumanConfirmationMethod(detail)).toBe('OnSite')
  })

  it('decodes nullable revision snapshots on Evidence responses', () => {
    expect(decodeEvidenceDetail({
      ...humanConfirmation({ confirmationMethod: 'Meeting' }, null),
      knowledgeDocumentRevisionNumberSnapshot: 4,
    }).knowledgeDocumentRevisionNumberSnapshot).toBe(4)

    expect(decodeAddEvidence({
      id: 9,
      evidenceType: 'ExistingDocument',
      subject: { type: 'KnowledgeDocument', id: 7 },
      subjectDetailKey: null,
      knowledgeDocumentRevisionNumberSnapshot: null,
      sourceTitle: 'ordinary evidence',
      subjectKnowledgeStatus: 'Unknown',
      knowledgeStatusChanged: false,
      concurrencyToken: 'opaque-evidence-token',
    }).knowledgeDocumentRevisionNumberSnapshot).toBeNull()
  })
})
