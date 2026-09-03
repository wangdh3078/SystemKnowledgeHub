import { mount } from '@vue/test-utils'
import { describe, expect, it } from 'vitest'
import SkhPagination from './SkhPagination.vue'

const ElPagination = {
  props: ['currentPage', 'pageSize', 'pageSizes', 'total', 'layout'],
  emits: ['current-change', 'size-change'],
  template:
    '<div data-pagination :data-layout="layout" :data-total="total"><button data-next @click="$emit(\'current-change\', 2)">下一页</button><button data-size @click="$emit(\'size-change\', 50)">50 条/页</button></div>',
}

describe('SkhPagination', () => {
  it('keeps the unified layout visible whenever total is positive', () => {
    const wrapper = mount(SkhPagination, {
      props: { total: 1, currentPage: 1, pageSize: 20 },
      global: { components: { ElPagination } },
    })

    expect(wrapper.find('[data-pagination]').attributes('data-layout')).toBe(
      'total, sizes, prev, pager, next, jumper',
    )
    expect(wrapper.find('[data-pagination]').attributes('data-total')).toBe('1')
  })

  it('forwards page and page-size changes without owning business loading', async () => {
    const wrapper = mount(SkhPagination, {
      props: { total: 328, currentPage: 1, pageSize: 20 },
      global: { components: { ElPagination } },
    })

    await wrapper.find('[data-next]').trigger('click')
    await wrapper.find('[data-size]').trigger('click')

    expect(wrapper.emitted('update:currentPage')).toEqual([[2]])
    expect(wrapper.emitted('current-change')).toEqual([[2]])
    expect(wrapper.emitted('update:pageSize')).toEqual([[50]])
    expect(wrapper.emitted('size-change')).toEqual([[50]])
  })

  it('does not render for an empty result', () => {
    const wrapper = mount(SkhPagination, {
      props: { total: 0, currentPage: 1, pageSize: 20 },
      global: { components: { ElPagination } },
    })

    expect(wrapper.find('[data-pagination]').exists()).toBe(false)
  })
})
