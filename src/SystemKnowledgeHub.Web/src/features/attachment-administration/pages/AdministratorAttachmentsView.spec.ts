/* eslint-disable vue/one-component-per-file -- local render stubs keep the table presentation test focused */
import { createPinia } from 'pinia'
import { defineComponent, h, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import type { AdministratorAttachmentListItem } from '../api/administratorAttachmentContracts'
import { useAdministratorAttachments } from '../composables/useAdministratorAttachments'
import AdministratorAttachmentsView from './AdministratorAttachmentsView.vue'

vi.mock('../composables/useAdministratorAttachments', () => ({
  useAdministratorAttachments: vi.fn(),
}))

const baseItem: AdministratorAttachmentListItem = {
  attachmentId: 1,
  originalFileName: 'current.txt',
  extension: '.txt',
  kind: 'File',
  contentType: 'text/plain',
  sizeBytes: 128,
  createdByDisplayName: '本地管理员',
  createdAt: '2026-08-29T01:02:03Z',
  owner: { documentId: 8, title: '验证文档', lifecycleStatus: 'Draft', isDeleted: false },
  referenceCount: 1,
  currentReferenceCount: 1,
  historicalReferenceCount: 0,
  referenceStatus: 'Referenced',
  storageState: 'Ready',
  storageHealth: 'Ready',
  previewMode: 'Text',
  canPreview: true,
  sha256: 'a'.repeat(64),
}

let renderedRows: readonly AdministratorAttachmentListItem[] = []

const ElTable = defineComponent({
  name: 'ElTable',
  setup(_, { slots }) {
    return () => h('div', { 'data-table': '' }, slots.default?.())
  },
})

const ElTableColumn = defineComponent({
  name: 'ElTableColumn',
  props: { label: { type: String, required: true } },
  setup(props, { slots }) {
    return () =>
      h(
        'section',
        { 'data-column': props.label },
        renderedRows.map((row) => slots.default?.({ row })),
      )
  },
})

const ElButton = defineComponent({
  name: 'ElButton',
  inheritAttrs: false,
  setup(_, { attrs, slots }) {
    return () => h('button', attrs, slots.default?.())
  },
})

const ElTag = defineComponent({
  name: 'ElTag',
  setup(_, { slots }) {
    return () => h('span', { 'data-bordered-tag': '' }, slots.default?.())
  },
})

function mockList(items: readonly AdministratorAttachmentListItem[]): void {
  renderedRows = items
  vi.mocked(useAdministratorAttachments).mockReturnValue({
    query: ref(''),
    kind: ref(''),
    extension: ref(''),
    referenceStatus: ref(''),
    storageState: ref(''),
    page: ref(1),
    pageSize: ref(20),
    data: ref({ items, page: 1, pageSize: 20, total: items.length }),
    statistics: ref(null),
    loading: ref(false),
    statisticsLoading: ref(false),
    error: ref(null),
    statisticsError: ref(null),
    loadList: vi.fn(),
    loadStatistics: vi.fn(),
    resetPageAndLoad: vi.fn(),
    clearFilters: vi.fn(),
    refresh: vi.fn(),
  })
}

describe('AdministratorAttachmentsView presentation', () => {
  beforeEach(() => {
    vi.mocked(useAdministratorAttachments).mockReset()
  })

  it('renders business-readable unboxed statuses and the accessible detail operation', () => {
    mockList([
      baseItem,
      {
        ...baseItem,
        attachmentId: 2,
        originalFileName: 'history.jpg',
        extension: '.jpg',
        kind: 'Image',
        contentType: 'image/jpeg',
        referenceCount: 2,
        currentReferenceCount: 0,
        historicalReferenceCount: 2,
        referenceStatus: 'HistoricalOnly',
        previewMode: 'Image',
        sha256: 'b'.repeat(64),
      },
      {
        ...baseItem,
        attachmentId: 3,
        originalFileName: 'orphan.pdf',
        extension: '.pdf',
        contentType: 'application/pdf',
        referenceCount: 0,
        currentReferenceCount: 0,
        historicalReferenceCount: 0,
        referenceStatus: 'Orphan',
        storageState: 'DeletePending',
        storageHealth: 'DeletePending',
        previewMode: 'Pdf',
        sha256: 'c'.repeat(64),
      },
    ])

    const wrapper = mount(AdministratorAttachmentsView, {
      global: {
        plugins: [createPinia()],
        components: { ElButton, ElTable, ElTableColumn, ElTag },
        stubs: {
          ElInput: true,
          ElOption: true,
          ElPagination: true,
          ElRadioButton: true,
          ElRadioGroup: true,
          ElSelect: true,
        },
      },
    })

    const references = wrapper.get('[data-column="引用"]')
    expect(references.text()).toContain('当前引用 · 1 个修订')
    expect(references.text()).toContain('仅历史引用 · 2 个历史修订')
    expect(references.text()).toContain('孤立附件 · 无引用')
    expect(references.find('[data-bordered-tag]').exists()).toBe(false)
    expect(references.findAll('.attachment-admin-table__status')).toHaveLength(3)

    const storage = wrapper.get('[data-column="存储"]')
    expect(storage.text()).toContain('可用')
    expect(storage.text()).toContain('等待删除重试')
    expect(storage.text()).not.toContain('Ready')
    expect(storage.text()).not.toContain('DeletePending')
    expect(storage.find('[data-bordered-tag]').exists()).toBe(false)
    expect(storage.findAll('.attachment-admin-table__status')).toHaveLength(3)

    const operations = wrapper.get('[data-column="操作"]')
    expect(operations.text()).toBe('详情详情详情')
    expect(operations.text()).not.toContain('查看')
    expect(operations.get('button[aria-label="查看附件详情 current.txt"]')).toBeDefined()
    expect(operations.get('button[aria-label="查看附件详情 history.jpg"]')).toBeDefined()
    expect(operations.get('button[aria-label="查看附件详情 orphan.pdf"]')).toBeDefined()
  })
})
