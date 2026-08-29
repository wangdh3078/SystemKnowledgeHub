import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { ElMessage, ElMessageBox } from 'element-plus'
import {
  deleteAdministratorAttachment,
  getAdministratorAttachment,
} from '../api/administratorAttachmentsApi'
import type { AdministratorAttachmentDetail } from '../api/administratorAttachmentContracts'
import AdministratorAttachmentDetailDrawer from './AdministratorAttachmentDetailDrawer.vue'

vi.mock('element-plus', () => ({
  ElMessage: { success: vi.fn(), error: vi.fn() },
  ElMessageBox: { confirm: vi.fn() },
}))
vi.mock('../api/administratorAttachmentsApi', () => ({
  checkAdministratorAttachmentIntegrity: vi.fn(),
  deleteAdministratorAttachment: vi.fn(),
  getAdministratorAttachment: vi.fn(),
}))

const sha256 = 'a'.repeat(64)

function detail(
  overrides: Partial<AdministratorAttachmentDetail> = {},
): AdministratorAttachmentDetail {
  return {
    attachmentId: 17,
    originalFileName: '历史规范.pdf',
    extension: '.pdf',
    kind: 'File',
    contentType: 'application/pdf',
    sizeBytes: 4096,
    sha256,
    createdAt: '2026-08-29T01:02:03Z',
    createdByUserId: 3,
    createdByDisplayName: '附件管理员',
    storageState: 'Ready',
    storageHealth: 'Ready',
    previewMode: 'Pdf',
    canPreview: true,
    concurrencyToken: 'version-4',
    owner: {
      documentId: 8,
      title: '已删除知识内容',
      lifecycleStatus: 'Published',
      isDeleted: true,
    },
    referenceCount: 1,
    currentReferenceCount: 0,
    historicalReferenceCount: 1,
    referenceStatus: 'HistoricalOnly',
    references: [{ revisionNumber: 4, isCurrent: false, createdAt: '2026-08-28T01:00:00Z' }],
    referencesTruncated: false,
    ...overrides,
  }
}

function mountDrawer() {
  return mount(AdministratorAttachmentDetailDrawer, {
    props: { attachmentId: 17 },
    global: {
      plugins: [createPinia()],
      components: {
        ElButton: {
          props: ['loading'],
          emits: ['click'],
          template: '<button type="button" @click="$emit(\'click\')"><slot /></button>',
        },
        ElTag: { template: '<span><slot /></span>' },
        RouterLink: { template: '<a><slot /></a>' },
      },
    },
  })
}

describe('AdministratorAttachmentDetailDrawer', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(getAdministratorAttachment).mockReset()
    vi.mocked(deleteAdministratorAttachment).mockReset()
    vi.mocked(ElMessageBox.confirm).mockReset()
    vi.mocked(ElMessage.success).mockReset()
    vi.mocked(ElMessage.error).mockReset()
  })

  it('preserves historical references for a deleted owner and uses exact revision download', async () => {
    vi.mocked(getAdministratorAttachment).mockResolvedValue(detail())
    const wrapper = mountDrawer()
    await flushPromises()

    expect(wrapper.text()).toContain('仅历史修订引用')
    expect(wrapper.text()).toContain('Revision 4')
    expect(wrapper.text()).toContain('已删除')
    expect(wrapper.text()).not.toContain('查看当前文档')
    expect(wrapper.get('a[download]').attributes('href')).toBe(
      '/api/knowledge-documents/8/revisions/4/attachments/17/download',
    )
    expect(wrapper.text()).not.toContain('永久删除附件？')
    expect(wrapper.find('button[aria-label^="永久删除"]').exists()).toBe(false)
  })

  it('requires irreversible confirmation and version token for a single zero-reference delete', async () => {
    vi.mocked(getAdministratorAttachment).mockResolvedValue(
      detail({
        originalFileName: '孤立附件.txt',
        previewMode: 'Text',
        canPreview: true,
        owner: { documentId: 8, title: '活跃文档', lifecycleStatus: 'Draft', isDeleted: false },
        referenceCount: 0,
        historicalReferenceCount: 0,
        referenceStatus: 'Orphan',
        references: [],
      }),
    )
    vi.mocked(ElMessageBox.confirm).mockResolvedValue({} as never)
    vi.mocked(deleteAdministratorAttachment).mockResolvedValue()
    const wrapper = mountDrawer()
    await flushPromises()

    await wrapper.get('button[aria-label="永久删除附件 孤立附件.txt"]').trigger('click')
    await flushPromises()

    expect(ElMessageBox.confirm).toHaveBeenCalledWith(
      expect.stringContaining('删除附件 metadata 和物理文件，且不可恢复'),
      '永久删除附件？',
      expect.objectContaining({ confirmButtonText: '永久删除' }),
    )
    expect(deleteAdministratorAttachment).toHaveBeenCalledWith(17, 'version-4')
    expect(wrapper.emitted('deleted')).toEqual([[17]])
  })
})
