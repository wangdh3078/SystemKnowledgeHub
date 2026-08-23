import { describe, expect, it } from 'vitest'
import { decodeKnowledgeDocumentDetail } from './knowledgeDocumentContracts'

describe('decodeKnowledgeDocumentDetail revision contract', () => {
  it('decodes the current revision pointers and confirmation coverage without parsing concurrency', () => {
    const detail = decodeKnowledgeDocumentDetail({
      id: 7,
      documentType: 'KnowledgeArticle',
      title: 'Revision contract',
      summary: null,
      bodyMarkdown: 'body',
      lifecycleStatus: 'Published',
      knowledgeStatus: 'Confirmed',
      currentRevisionNumber: 3,
      latestPublishedRevisionNumber: 3,
      confirmationCoverage: {
        state: 'ChangedSinceConfirmation',
        lastConfirmedRevisionNumber: 2,
      },
      createdByUserId: 1,
      createdByDisplayName: 'Creator',
      updatedByUserId: 2,
      updatedByDisplayName: 'Editor',
      createdAt: '2026-08-23T01:00:00Z',
      updatedAt: '2026-08-23T02:00:00Z',
      publishedAt: '2026-08-23T02:00:00Z',
      archivedAt: null,
      concurrencyToken: 'opaque-token',
    })

    expect(detail.currentRevisionNumber).toBe(3)
    expect(detail.latestPublishedRevisionNumber).toBe(3)
    expect(detail.confirmationCoverage).toEqual({
      state: 'ChangedSinceConfirmation',
      lastConfirmedRevisionNumber: 2,
    })
    expect(detail.concurrencyToken).toBe('opaque-token')
  })
})
