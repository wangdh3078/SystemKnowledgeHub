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
      canDelete: true,
      attachmentReferences: [
        {
          attachmentId: 91,
          kind: 'Image',
          originalFileName: '拓扑.png',
          extension: '.png',
          contentType: 'image/png',
          sizeBytes: 24,
          sha256: 'a'.repeat(64),
          previewMode: 'Image',
          canPreview: true,
          canDownload: true,
        },
      ],
    })

    expect(detail.currentRevisionNumber).toBe(3)
    expect(detail.latestPublishedRevisionNumber).toBe(3)
    expect(detail.confirmationCoverage).toEqual({
      state: 'ChangedSinceConfirmation',
      lastConfirmedRevisionNumber: 2,
    })
    expect(detail.concurrencyToken).toBe('opaque-token')
    expect(detail.attachmentReferences[0]).toEqual(
      expect.objectContaining({
        attachmentId: 91,
        kind: 'Image',
        previewMode: 'Image',
      }),
    )
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
      owner: {
        id: 7,
        targetType: 'KnowledgeDocument',
        displayName: 'Document',
        isDeleted: false,
        isNavigable: true,
      },
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
      owner: {
        id: 7,
        targetType: 'KnowledgeDocument',
        displayName: 'Document',
        isDeleted: true,
        isNavigable: false,
      },
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
      attachmentReferences: [
        {
          attachmentId: 92,
          kind: 'Image',
          originalFileName: '历史图.webp',
          extension: '.webp',
          contentType: 'image/webp',
          sizeBytes: 42,
          sha256: 'b'.repeat(64),
          previewMode: 'Image',
          canPreview: true,
          canDownload: true,
        },
      ],
    })

    expect(detail.authorDisplayName).toBeNull()
    expect(detail.knowledgeDocumentId).toBe(7)
    expect(detail.bodyMarkdown).toBe('# Snapshot')
    expect(detail.attachmentReferences[0]?.attachmentId).toBe(92)
    expect('concurrencyToken' in detail).toBe(false)
  })
})
