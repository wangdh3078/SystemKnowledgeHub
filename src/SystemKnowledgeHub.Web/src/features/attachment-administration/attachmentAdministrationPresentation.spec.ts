import { describe, expect, it } from 'vitest'
import {
  administratorAttachmentReferenceLabels,
  administratorAttachmentStorageFilterOptions,
  administratorAttachmentStorageLabels,
  formatAdministratorAttachmentReferenceCounts,
} from './attachmentAdministrationPresentation'

describe('attachment administration presentation', () => {
  it('keeps storage filter wire values while presenting every option in Chinese', () => {
    expect(administratorAttachmentStorageFilterOptions).toEqual([
      { label: '全部存储状态', value: '' },
      { label: '可用', value: 'Ready' },
      { label: '等待删除重试', value: 'DeletePending' },
    ])
    expect(administratorAttachmentStorageLabels).toMatchObject({
      Ready: '可用',
      DeletePending: '等待删除重试',
      Missing: '文件缺失',
      LengthMismatch: '长度不一致',
      Corrupt: '校验不一致',
      Unavailable: '文件不可用',
    })
  })

  it('formats orphan, current and historical-only reference states from exact counts', () => {
    expect(administratorAttachmentReferenceLabels).toEqual({
      Referenced: '当前引用',
      HistoricalOnly: '仅历史引用',
      Orphan: '孤立附件',
    })
    expect(
      formatAdministratorAttachmentReferenceCounts({
        referenceStatus: 'Orphan',
        referenceCount: 0,
        currentReferenceCount: 0,
        historicalReferenceCount: 0,
      }),
    ).toBe('0 个引用')
    expect(
      formatAdministratorAttachmentReferenceCounts({
        referenceStatus: 'Referenced',
        referenceCount: 3,
        currentReferenceCount: 1,
        historicalReferenceCount: 2,
      }),
    ).toBe('1 个当前修订引用 · 2 个历史修订引用')
    expect(
      formatAdministratorAttachmentReferenceCounts({
        referenceStatus: 'HistoricalOnly',
        referenceCount: 2,
        currentReferenceCount: 0,
        historicalReferenceCount: 2,
      }),
    ).toBe('2 个历史修订引用')
  })
})
