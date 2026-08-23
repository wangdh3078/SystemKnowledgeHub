import { describe, expect, it } from 'vitest'
import {
  decodeKnowledgeDocumentDetail,
  decodeKnowledgeDocumentRevisionDetail,
  decodeKnowledgeDocumentRevisionList,
} from './knowledgeDocumentContracts'

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

describe('revision history read contracts', () => {
  const revision = {
    id: 31,
    revisionNumber: 3,
    revisionOrigin: 'ContentSave',
    lifecycleContext: 'Draft',
    authorUserId: 8,
    authorDisplayName: 'Immutable Author',
    createdAt: '2026-08-23T03:00:00Z',
    changeSummary: 'Clarified recovery steps',
    restoreReason: null,
    restoredFromRevisionNumber: null,
    isCurrent: true,
    isLatestPublished: false,
  }

  it('decodes list metadata without inventing historical content fields', () => {
    const response = decodeKnowledgeDocumentRevisionList({
      items: [revision],
      page: 1,
      pageSize: 20,
      total: 3,
    })

    expect(response.items[0]).toEqual(revision)
    expect(response.total).toBe(3)
    expect('bodyMarkdown' in response.items[0]).toBe(false)
  })

  it('decodes baseline null actors and immutable detail content', () => {
    const detail = decodeKnowledgeDocumentRevisionDetail({
      ...revision,
      id: 11,
      revisionNumber: 1,
      revisionOrigin: 'MigrationBaseline',
      authorUserId: null,
      authorDisplayName: null,
      isCurrent: false,
      knowledgeDocumentId: 7,
      title: 'Migrated knowledge',
      summary: null,
      bodyMarkdown: '# Snapshot',
    })

    expect(detail.authorDisplayName).toBeNull()
    expect(detail.knowledgeDocumentId).toBe(7)
    expect(detail.bodyMarkdown).toBe('# Snapshot')
    expect('concurrencyToken' in detail).toBe(false)
  })
})
