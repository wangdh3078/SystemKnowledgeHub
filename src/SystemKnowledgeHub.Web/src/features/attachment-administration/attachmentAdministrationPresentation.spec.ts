import { describe, expect, it } from 'vitest'
import {
  administratorAttachmentKindLabels,
  administratorAttachmentPreviewModeLabels,
  administratorAttachmentReferenceLabels,
  administratorAttachmentStorageFilterOptions,
  administratorAttachmentStorageLabels,
  formatAdministratorAttachmentOwnerLifecycle,
  formatAdministratorAttachmentReferenceSummary,
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
      Corrupt: '校验异常',
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
      formatAdministratorAttachmentReferenceSummary({
        referenceStatus: 'Orphan',
        referenceCount: 0,
        currentReferenceCount: 0,
        historicalReferenceCount: 0,
      }),
    ).toBe('孤立附件 · 无引用')
    expect(
      formatAdministratorAttachmentReferenceSummary({
        referenceStatus: 'Referenced',
        referenceCount: 1,
        currentReferenceCount: 1,
        historicalReferenceCount: 0,
      }),
    ).toBe('当前引用 · 1 个修订')
    expect(
      formatAdministratorAttachmentReferenceSummary({
        referenceStatus: 'Referenced',
        referenceCount: 3,
        currentReferenceCount: 1,
        historicalReferenceCount: 2,
      }),
    ).toBe('当前引用 · 共 3 个修订')
    expect(
      formatAdministratorAttachmentReferenceSummary({
        referenceStatus: 'HistoricalOnly',
        referenceCount: 2,
        currentReferenceCount: 0,
        historicalReferenceCount: 2,
      }),
    ).toBe('仅历史引用 · 2 个历史修订')
  })

  it('presents attachment and owner wire values through Chinese labels', () => {
    expect(administratorAttachmentKindLabels).toEqual({ Image: '图片', File: '文件' })
    expect(administratorAttachmentPreviewModeLabels).toMatchObject({
      Image: '图片',
      Pdf: 'PDF',
      Text: '文本',
      Spreadsheet: '电子表格',
      None: '不可预览',
    })
    expect(formatAdministratorAttachmentOwnerLifecycle('Draft')).toBe('草稿')
    expect(formatAdministratorAttachmentOwnerLifecycle('Published')).toBe('已发布')
    expect(formatAdministratorAttachmentOwnerLifecycle('Archived')).toBe('已归档')
    expect(formatAdministratorAttachmentOwnerLifecycle('FutureValue')).toBe('FutureValue')
  })
})
