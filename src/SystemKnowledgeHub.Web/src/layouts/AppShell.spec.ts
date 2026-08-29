import { ElButton, ElDialog, ElDrawer, ElIcon, ElTag } from 'element-plus'
import { createPinia } from 'pinia'
import { mount } from '@vue/test-utils'
import { createMemoryHistory, createRouter } from 'vue-router'
import { describe, expect, it } from 'vitest'
import AppShell from './AppShell.vue'

describe('AppShell', () => {
  it('mounts the shared shell with navigation and context rail host', async () => {
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [
        {
          path: '/',
          component: { template: '<div />' },
          meta: {
            title: '基础工程',
            layout: 'app-shell',
            navigationKey: 'dashboard',
          },
        },
      ],
    })
    await router.push('/')
    await router.isReady()

    const wrapper = mount(AppShell, {
      global: {
        plugins: [router, createPinia()],
        components: {
          ElButton,
          ElDialog,
          ElDrawer,
          ElIcon,
          ElTag,
        },
      },
      slots: {
        default: '<div data-test="content">内容</div>',
      },
    })

    expect(wrapper.text()).toContain('系统知识中心')
    expect(wrapper.text()).toContain('待确认事项')
    expect(wrapper.find('[data-test="content"]').exists()).toBe(true)
  })
})
