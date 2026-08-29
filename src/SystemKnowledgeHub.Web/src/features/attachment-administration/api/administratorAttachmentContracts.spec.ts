import { describe, expect, it } from 'vitest'
import {
  decodeAdministratorAttachmentDetail,
  decodeAdministratorAttachmentIntegrity,
  decodeAdministratorAttachmentList,
  decodeAdministratorAttachmentStatistics,
} from './administratorAttachmentContracts'

const sha256 = 'a'.repeat(64)

function listItem() {
  return {
    attachmentId: 17,
    originalFileName: '历史规范.pdf',
    extension: '.pdf',
    kind: 'File',
    contentType: 'application/pdf',
    sizeBytes: 4096,
    createdByDisplayName: '附件管理员',
    createdAt: '2026-08-29T01:02:03Z',
    owner: {
      documentId: 8,
      title: '已删除知识内容',
      lifecycleStatus: 'Published',
      isDeleted: true,
    },
    referenceCount: 2,
    currentReferenceCount: 0,
    historicalReferenceCount: 2,
    referenceStatus: 'HistoricalOnly',
    storageState: 'Ready',
    storageHealth: 'Ready',
    previewMode: 'Pdf',
    canPreview: true,
    sha256,
  }
}

describe('administrator attachment contracts', () => {
  it('decodes historical-only list and exact detail reference data', () => {
    const list = decodeAdministratorAttachmentList({
      items: [listItem()],
      page: 1,
      pageSize: 20,
      total: 1,
    })
    const detail = decodeAdministratorAttachmentDetail({
      ...listItem(),
      createdByUserId: 3,
      concurrencyToken: 'attachment-version-4',
      references: [{ revisionNumber: 4, isCurrent: false, createdAt: '2026-08-28T01:00:00Z' }],
      referencesTruncated: false,
    })

    expect(list.items[0]?.referenceStatus).toBe('HistoricalOnly')
    expect(list.items[0]?.owner.isDeleted).toBe(true)
    expect(detail.referenceCount).toBe(2)
    expect(detail.references[0]?.revisionNumber).toBe(4)
  })

  it('decodes bounded metadata statistics and integrity results', () => {
    const statistics = decodeAdministratorAttachmentStatistics({
      totalCount: 3,
      totalSizeBytes: 6000,
      imageCount: 1,
      imageSizeBytes: 1000,
      fileCount: 2,
      fileSizeBytes: 5000,
      orphanCount: 1,
      orphanSizeBytes: 2000,
      referencedCount: 2,
      currentReferencedCount: 1,
      historicalOnlyCount: 1,
      deletedOwnerCount: 1,
      readyCount: 2,
      deletePendingCount: 1,
      recentWindowDays: 7,
      recentUploadCount: 2,
      largestAttachments: [
        {
          attachmentId: 17,
          originalFileName: '历史规范.pdf',
          kind: 'File',
          sizeBytes: 4096,
          createdAt: '2026-08-29T01:02:03Z',
        },
      ],
      recentUploads: [],
    })
    const integrity = decodeAdministratorAttachmentIntegrity({
      attachmentId: 17,
      status: 'Corrupt',
      sizeBytes: 4096,
      actualSizeBytes: 4096,
      sha256,
      actualSha256: 'b'.repeat(64),
      checkedAt: '2026-08-29T02:00:00Z',
    })

    expect(statistics.historicalOnlyCount).toBe(1)
    expect(statistics.largestAttachments).toHaveLength(1)
    expect(integrity.status).toBe('Corrupt')
  })

  it('fails closed for unknown states and malformed hashes', () => {
    expect(() =>
      decodeAdministratorAttachmentList({
        items: [{ ...listItem(), referenceStatus: 'MaybeOrphan' }],
        page: 1,
        pageSize: 20,
        total: 1,
      }),
    ).toThrow('unsupported value')
    expect(() =>
      decodeAdministratorAttachmentIntegrity({
        attachmentId: 17,
        status: 'Ready',
        sizeBytes: 4096,
        actualSizeBytes: 4096,
        sha256: 'storage/path.pdf',
        actualSha256: null,
        checkedAt: '2026-08-29T02:00:00Z',
      }),
    ).toThrow('lowercase SHA-256')
  })
})
