import { defineComponent, nextTick, ref } from 'vue'
import { mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { createMemoryHistory, createRouter } from 'vue-router'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useOverlayStore } from '../../../app/stores/overlays'
import { useGlobalSearch } from './useGlobalSearch'

describe('useGlobalSearch focus lifecycle', () => {
  afterEach(() => {
    document.body.innerHTML = ''
    window.localStorage.clear()
  })

  it('focuses after the dialog opened lifecycle and restores the invoking control without scrolling', async () => {
    const pinia = createPinia()
    setActivePinia(pinia)
    const router = createRouter({
      history: createMemoryHistory(),
      routes: [{ path: '/', name: 'dashboard', component: { template: '<div />' } }],
    })
    await router.push('/')
    await router.isReady()
    const focus = vi.fn()
    const Harness = defineComponent({
      setup() {
        useGlobalSearch(ref({ focus }))
        return () => null
      },
    })
    const invocation = document.createElement('button')
    document.body.append(invocation)
    const wrapper = mount(Harness, { attachTo: document.body, global: { plugins: [pinia, router] } })
    invocation.focus()
    const overlays = useOverlayStore()

    overlays.openDialog({ kind: 'global-search', id: null, mode: 'read' })
    await nextTick()
    expect(focus).not.toHaveBeenCalled()
    overlays.notifyDialogOpened()
    await nextTick()
    expect(focus).toHaveBeenCalledOnce()

    overlays.closeDialog()
    await nextTick()
    expect(document.activeElement).toBe(invocation)
    wrapper.unmount()
  })
})
