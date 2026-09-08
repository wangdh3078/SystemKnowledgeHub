import { enableAutoUnmount, flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import ElementPlus, { ElSelect } from 'element-plus'
import ExistingDocumentPicker from './ExistingDocumentPicker.vue'
import { getKnowledgeDocuments } from '../../knowledge-documents/api/knowledgeDocumentsApi'
import {
  documentTypes,
  documentTypeLabels,
} from '../../knowledge-documents/api/knowledgeDocumentContracts'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
vi.mock('../../knowledge-documents/api/knowledgeDocumentsApi', () => ({
  getKnowledgeDocuments: vi.fn(),
}))
enableAutoUnmount(afterEach)
beforeEach(() => vi.clearAllMocks())
describe('existing canonical document candidates', () => {
  it('uses server paging/search and seven types, default nonArchived and explicit Archived', async () => {
    const items = documentTypes.map((documentType, index) => ({
      id: index + 1,
      documentType,
      title: `文档${index}`,
      summary: null,
      lifecycleStatus: index % 2 ? ('Published' as const) : ('Draft' as const),
      knowledgeStatus: 'Unknown' as const,
      createdByDisplayName: '作者',
      updatedByDisplayName: '作者',
      createdAt: '2026-09-08T00:00:00Z',
      updatedAt: '2026-09-08T00:00:00Z',
    }))
    vi.mocked(getKnowledgeDocuments).mockResolvedValue({ items, page: 1, pageSize: 20, total: 45 })
    const wrapper = mount(ExistingDocumentPicker, {
      props: { nodes: [], disabled: false },
      global: { plugins: [ElementPlus] },
    })
    await flushPromises()
    expect(getKnowledgeDocuments).toHaveBeenLastCalledWith(
      expect.objectContaining({ page: 1, pageSize: 20, lifecycleStatus: undefined }),
      expect.any(AbortSignal),
    )
    for (const type of documentTypes) expect(wrapper.text()).toContain(documentTypeLabels[type])
    expect(wrapper.text()).toContain('草稿')
    expect(wrapper.text()).toContain('已发布')
    wrapper.findComponent(SkhPagination).vm.$emit('update:currentPage', 2)
    await flushPromises()
    expect(getKnowledgeDocuments).toHaveBeenLastCalledWith(
      expect.objectContaining({ page: 2 }),
      expect.any(AbortSignal),
    )
    await wrapper.get('input[aria-label="搜索已有文档"]').setValue('查找正文')
    await wrapper.get('input[aria-label="搜索已有文档"]').trigger('keyup.enter')
    await flushPromises()
    expect(getKnowledgeDocuments).toHaveBeenLastCalledWith(
      expect.objectContaining({ page: 1, query: '查找正文' }),
      expect.any(AbortSignal),
    )
    wrapper.findAllComponents(ElSelect)[0]!.vm.$emit('update:modelValue', 'Sop')
    wrapper.findAllComponents(ElSelect)[1]!.vm.$emit('update:modelValue', 'Archived')
    await flushPromises()
    expect(getKnowledgeDocuments).toHaveBeenLastCalledWith(
      expect.objectContaining({ documentType: 'Sop', lifecycleStatus: 'Archived' }),
      expect.any(AbortSignal),
    )
  })
  it('only renders current API candidates and uses the complete tree to locate duplicates', async () => {
    vi.mocked(getKnowledgeDocuments).mockResolvedValue({
      items: [
        {
          id: 1,
          documentType: 'Sop',
          title: '已有',
          summary: null,
          lifecycleStatus: 'Archived',
          knowledgeStatus: 'Unknown',
          createdByDisplayName: '作者',
          updatedByDisplayName: '作者',
          createdAt: '2026-09-08T00:00:00Z',
          updatedAt: '2026-09-08T00:00:00Z',
        },
      ],
      page: 1,
      pageSize: 20,
      total: 1,
    })
    const node = {
      id: 5,
      parentId: null,
      nodeType: 'Document' as const,
      knowledgeDocumentId: 1,
      title: '已有',
      documentType: 'Sop' as const,
      lifecycleStatus: 'Archived' as const,
      availability: 'Available' as const,
      sortOrder: 0,
      concurrencyToken: 'token',
      createdAt: '',
      updatedAt: '',
    }
    const wrapper = mount(ExistingDocumentPicker, {
      props: { nodes: [node], disabled: false },
      global: { plugins: [ElementPlus] },
    })
    await flushPromises()
    expect(wrapper.text()).toContain('已在分析目录中')
    expect(wrapper.findAll('button').some((button) => button.text() === '加入')).toBe(false)
    await wrapper
      .findAll('button')
      .find((button) => button.text() === '定位')!
      .trigger('click')
    expect(wrapper.emitted('locate')).toEqual([[node]])
    expect(wrapper.emitted('select')).toBeUndefined()
    expect(wrapper.findAll('tbody tr')).toHaveLength(1)
  })
})
