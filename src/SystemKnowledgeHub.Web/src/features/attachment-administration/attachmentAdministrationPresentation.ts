import type {
  AdministratorAttachmentReferenceStatus,
  AdministratorAttachmentStorageHealth,
  AdministratorAttachmentStorageState,
} from './api/administratorAttachmentContracts'
import type {
  AttachmentKind,
  AttachmentPreviewMode,
} from '../knowledge-documents/api/attachmentContracts'

export const administratorAttachmentKindLabels: Readonly<Record<AttachmentKind, string>> = {
  Image: '图片',
  File: '文件',
}

export const administratorAttachmentPreviewModeLabels: Readonly<
  Record<AttachmentPreviewMode, string>
> = {
  Image: '图片',
  Pdf: 'PDF',
  Text: '文本',
  Markdown: 'Markdown',
  Csv: 'CSV',
  Spreadsheet: '电子表格',
  None: '不可预览',
}

export const administratorAttachmentReferenceLabels: Readonly<
  Record<AdministratorAttachmentReferenceStatus, string>
> = {
  Referenced: '当前引用',
  HistoricalOnly: '仅历史引用',
  Orphan: '孤立附件',
}

export const administratorAttachmentStorageLabels: Readonly<
  Record<AdministratorAttachmentStorageHealth, string>
> = {
  Ready: '可用',
  Missing: '文件缺失',
  LengthMismatch: '长度不一致',
  Corrupt: '校验异常',
  DeletePending: '等待删除重试',
  Unavailable: '文件不可用',
}

export const administratorAttachmentStorageFilterOptions: readonly {
  readonly label: string
  readonly value: AdministratorAttachmentStorageState | ''
}[] = [
  { label: '全部存储状态', value: '' },
  { label: administratorAttachmentStorageLabels.Ready, value: 'Ready' },
  {
    label: administratorAttachmentStorageLabels.DeletePending,
    value: 'DeletePending',
  },
]

interface AttachmentReferenceCounts {
  readonly referenceStatus: AdministratorAttachmentReferenceStatus
  readonly referenceCount: number
  readonly currentReferenceCount: number
  readonly historicalReferenceCount: number
}

export function formatAdministratorAttachmentReferenceSummary(
  attachment: AttachmentReferenceCounts,
): string {
  if (attachment.referenceStatus === 'Orphan') {
    return `${administratorAttachmentReferenceLabels.Orphan} · 无引用`
  }

  if (attachment.referenceStatus === 'HistoricalOnly') {
    return `${administratorAttachmentReferenceLabels.HistoricalOnly} · ${attachment.historicalReferenceCount} 个历史修订`
  }

  const revisionCount =
    attachment.referenceCount === 1 ? '1 个修订' : `共 ${attachment.referenceCount} 个修订`
  return `${administratorAttachmentReferenceLabels.Referenced} · ${revisionCount}`
}

export function formatAdministratorAttachmentOwnerLifecycle(value: string): string {
  const labels: Readonly<Record<string, string>> = {
    Draft: '草稿',
    Published: '已发布',
    Archived: '已归档',
  }
  return labels[value] ?? value
}
