import { createPinia, setActivePinia } from 'pinia'
import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import { useOverlayStore } from '../../../app/stores/overlays'
import DeleteConfirmationDialogContent from './DeleteConfirmationDialogContent.vue'

const messages = vi.hoisted(() => ({ success: vi.fn(), error: vi.fn(), warning: vi.fn() }))
vi.mock('element-plus', () => ({ ElMessage: messages }))

const components = {
  ElButton: {
    props: ['disabled', 'loading'],
    emits: ['click'],
    template: '<button type="button" :disabled="disabled" @click="$emit(\'click\')"><slot /></button>',
  },
  ElAlert: { template: '<div role="alert">{{ $attrs.title }}</div>' },
  ElIcon: { template: '<span><slot /></span>' },
}

function button(label: string): HTMLButtonElement {
  const match = Array.from(document.body.querySelectorAll('button')).find((item) => item.textContent?.trim() === label)
  if (!match) throw new Error(`Missing button: ${label}`)
  return match
}

function mountDialog(execute: () => Promise<void>) {
  const overlays = useOverlayStore()
  const callbacks = { onDeleted: vi.fn(), onRefresh: vi.fn(), onUnavailable: vi.fn() }
  overlays.openDialog({
    kind: 'delete-root', id: null, mode: 'edit', payload: {
      objectTypeLabel: '系统', actionLabel: '删除系统', displayName: 'Legacy MES',
      concurrencyToken: 'opaque-token', execute, ...callbacks,
    },
  })
  const wrapper = mount(DeleteConfirmationDialogContent, { global: { components } })
  return { wrapper, overlays, callbacks }
}

function apiError(status: number, code: 'business_rule_violation' | 'conflict' | 'forbidden' | 'not_found', details: Record<string, unknown> | null = null) {
  return new ApiError(status, { code, message: 'request failed', fieldErrors: null, details })
}

describe('DeleteConfirmationDialogContent', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    document.body.innerHTML = '<div id="dialog-feature-content"></div>'
    vi.clearAllMocks()
  })
  afterEach(() => { document.body.innerHTML = '' })

  it('cancels without calling the API and confirms exactly once on success', async () => {
    const execute = vi.fn().mockResolvedValue(undefined)
    const cancelled = mountDialog(execute)
    button('取消').click()
    await flushPromises()
    expect(execute).not.toHaveBeenCalled()
    expect(cancelled.overlays.currentDialog).toBeNull()
    cancelled.wrapper.unmount()

    const confirmed = mountDialog(execute)
    button('确认删除').click()
    button('确认删除').click()
    await flushPromises()
    expect(execute).toHaveBeenCalledTimes(1)
    expect(confirmed.callbacks.onDeleted).toHaveBeenCalledTimes(1)
    expect(messages.success).toHaveBeenCalledWith('已删除“Legacy MES”')
    expect(confirmed.overlays.currentDialog).toBeNull()
  })

  it('keeps a 422 dependency failure in the dialog with structured blockers', async () => {
    const execute = vi.fn().mockRejectedValue(apiError(422, 'business_rule_violation', {
      blockers: [
        { dependencyType: 'BusinessFunction', displayName: '业务功能', count: 3 },
        { dependencyType: 'KnowledgeRelationship', displayName: '知识关系', count: 2 },
      ],
    }))
    const { overlays } = mountDialog(execute)

    button('确认删除').click()
    await flushPromises()

    expect(document.body.textContent).toContain('无法删除，仍存在依赖项')
    expect(document.body.textContent).toContain('业务功能3')
    expect(document.body.textContent).toContain('知识关系2')
    expect(overlays.currentDialog?.kind).toBe('delete-root')
  })

  it.each([
    [409, 'conflict' as const, 'onRefresh' as const, '数据已发生变化，请刷新后重试。'],
    [403, 'forbidden' as const, 'onRefresh' as const, '你没有权限删除此对象'],
    [404, 'not_found' as const, 'onUnavailable' as const, '该对象已不存在或当前不可访问'],
  ])('handles status %s by closing and invoking the authoritative callback', async (status, code, callback, message) => {
    const execute = vi.fn().mockRejectedValue(apiError(status, code))
    const { overlays, callbacks } = mountDialog(execute)

    button('确认删除').click()
    await flushPromises()

    expect(callbacks[callback]).toHaveBeenCalledTimes(1)
    expect(overlays.currentDialog).toBeNull()
    expect([...messages.error.mock.calls, ...messages.warning.mock.calls].flat()).toContain(message)
  })
})
