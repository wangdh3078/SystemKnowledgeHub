import { ElMessageBox } from 'element-plus'
import { createPinia } from 'pinia'
import { defineComponent, h, nextTick } from 'vue'
import { mount } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { useOverlayStore } from '../app/stores/overlays'
import { markDrawerDirty, resetDrawerDirty } from './drawerDirtyState'
import DrawerHost from './DrawerHost.vue'

const ElDrawerStub = defineComponent({
  name: 'ElDrawer',
  inheritAttrs: false,
  props: {
    modelValue: Boolean,
    size: { type: String, default: '' },
    modal: Boolean,
    modalClass: { type: String, default: '' },
    closeOnClickModal: Boolean,
    closeOnPressEscape: Boolean,
    lockScroll: Boolean,
    beforeClose: { type: Function, default: undefined },
  },
  emits: ['close', 'closed', 'opened', 'open-auto-focus', 'close-auto-focus'],
  setup(_props, { slots }) {
    return () => h('aside', { class: 'drawer-stub' }, slots.default?.())
  },
})

function mountHost() {
  const pinia = createPinia()
  const wrapper = mount(DrawerHost, {
    attachTo: document.body,
    global: {
      plugins: [pinia],
      stubs: {
        ElDrawer: ElDrawerStub,
        ElIcon: true,
        EvidenceDrawerContent: true,
        RelationshipDrawerContent: true,
        BusinessRuleDrawerContent: true,
        IntegrationDrawerContent: true,
        ColumnDetailDrawer: true,
        DatabaseObjectKnowledgeDrawer: true,
      },
    },
  })
  return { wrapper, store: useOverlayStore(pinia) }
}

describe('DrawerHost', () => {
  afterEach(() => {
    resetDrawerDirty()
    vi.restoreAllMocks()
    document.body.innerHTML = ''
  })

  it('uses modal overlay semantics without locking or resizing the page', async () => {
    const { wrapper, store } = mountHost()
    store.openDrawer({ kind: 'foundation', id: 1, mode: 'read' })
    await nextTick()

    const drawer = wrapper.getComponent(ElDrawerStub)
    expect(drawer.props()).toMatchObject({
      modal: true,
      modalClass: 'skh-drawer-overlay',
      closeOnClickModal: true,
      closeOnPressEscape: true,
      lockScroll: false,
      size: 'var(--drawer-width-standard)',
    })
    expect(wrapper.classes()).not.toContain('app-shell--drawer-open')
  })

  it('uses the shared large width for authoring Drawers', async () => {
    const { wrapper, store } = mountHost()
    store.openDrawer({ kind: 'add-evidence', id: null, mode: 'create' })
    await nextTick()

    expect(wrapper.getComponent(ElDrawerStub).props('size')).toBe('var(--drawer-width-large)')
  })

  it('allows a clean read-only outside or Esc close request', async () => {
    const { wrapper, store } = mountHost()
    store.openDrawer({ kind: 'foundation', id: 1, mode: 'read' })
    await nextTick()
    const done = vi.fn()

    await wrapper.getComponent(ElDrawerStub).props('beforeClose')?.(done)

    expect(done).toHaveBeenCalledOnce()
  })

  it('does not close a dirty authoring Drawer when outside-close confirmation is cancelled', async () => {
    const { wrapper, store } = mountHost()
    vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('cancel')
    store.openDrawer({ kind: 'foundation', id: null, mode: 'edit' })
    await nextTick()
    await wrapper.get('.skh-drawer-host__content').trigger('input')
    const done = vi.fn()

    await wrapper.getComponent(ElDrawerStub).props('beforeClose')?.(done)

    expect(done).not.toHaveBeenCalled()
    expect(store.isDrawerOpen).toBe(true)
  })

  it('restores focus to the Drawer trigger after close', async () => {
    const { wrapper, store } = mountHost()
    const trigger = document.createElement('button')
    document.body.append(trigger)
    trigger.focus()
    store.openDrawer({ kind: 'foundation', id: 1, mode: 'read' })
    await nextTick()
    ;(document.activeElement as HTMLElement | null)?.blur()

    store.closeDrawer()
    wrapper.getComponent(ElDrawerStub).vm.$emit('closed')
    await Promise.resolve()

    expect(document.activeElement).toBe(trigger)
  })

  it('restores the trigger after the focus trap completes a mask close', async () => {
    const { wrapper, store } = mountHost()
    const trigger = document.createElement('button')
    document.body.append(trigger)
    trigger.focus()
    store.openDrawer({ kind: 'foundation', id: 1, mode: 'read' })
    await nextTick()
    ;(document.activeElement as HTMLElement | null)?.blur()

    wrapper.getComponent(ElDrawerStub).vm.$emit('close-auto-focus')
    await Promise.resolve()

    expect(document.activeElement).toBe(trigger)
  })

  it('honors the same dirty guard for explicit close controls', async () => {
    const { store } = mountHost()
    vi.spyOn(ElMessageBox, 'confirm').mockRejectedValue('cancel')
    store.openDrawer({ kind: 'foundation', id: null, mode: 'edit' })
    markDrawerDirty()

    await expect(store.requestDrawerClose()).resolves.toBe(false)
    expect(store.isDrawerOpen).toBe(true)
  })
})
