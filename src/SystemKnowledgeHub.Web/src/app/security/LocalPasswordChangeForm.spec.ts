import { flushPromises, mount } from '@vue/test-utils'
import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { changeMyLocalPassword } from '../../features/users/api/usersApi'
import LocalPasswordChangeForm from './LocalPasswordChangeForm.vue'

vi.mock('../../features/users/api/usersApi', () => ({
  changeMyLocalPassword: vi.fn(),
  getCurrentUser: vi.fn(),
}))

const components = {
  ElInput: {
    props: ['modelValue', 'type', 'disabled'],
    emits: ['update:modelValue'],
    template: '<input :type="type" :value="modelValue" :disabled="disabled" @input="$emit(\'update:modelValue\', $event.target.value)" />',
  },
  ElButton: {
    props: ['disabled'],
    template: '<button :disabled="disabled"><slot /></button>',
  },
  ElAlert: {
    props: ['title'],
    template: '<div role="alert">{{ title }}</div>',
  },
}

function mountForm() {
  const pinia = createPinia()
  setActivePinia(pinia)
  return mount(LocalPasswordChangeForm, { global: { plugins: [pinia], components } })
}

describe('LocalPasswordChangeForm', () => {
  beforeEach(() => vi.mocked(changeMyLocalPassword).mockReset())

  it('keeps password input exact and requires matching confirmation', async () => {
    const wrapper = mountForm()
    const inputs = wrapper.findAll('input')
    await inputs[0]!.setValue('  当前密码 保留空格  ')
    await inputs[1]!.setValue('  新密码 保留空格  ')
    await inputs[2]!.setValue('不一致的新密码')
    await wrapper.get('form').trigger('submit')
    expect(changeMyLocalPassword).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('两次输入的新密码不一致')

    await inputs[2]!.setValue('  新密码 保留空格  ')
    vi.mocked(changeMyLocalPassword).mockResolvedValue()
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(changeMyLocalPassword).toHaveBeenCalledWith('  当前密码 保留空格  ', '  新密码 保留空格  ')
    expect(wrapper.emitted('changed')).toHaveLength(1)
  })

  it('blocks out-of-range new passwords before sending them', async () => {
    const wrapper = mountForm()
    const inputs = wrapper.findAll('input')
    await inputs[0]!.setValue('current-password')
    await inputs[1]!.setValue('short')
    await inputs[2]!.setValue('short')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(changeMyLocalPassword).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('新密码长度必须为 8 到 128 个字符')
  })

  it('requires the confirmation value before sending a valid new password', async () => {
    const wrapper = mountForm()
    const inputs = wrapper.findAll('input')
    await inputs[0]!.setValue('current-password')
    await inputs[1]!.setValue('new-password')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(changeMyLocalPassword).not.toHaveBeenCalled()
    expect(wrapper.text()).toContain('请再次输入新密码')

    vi.mocked(changeMyLocalPassword).mockResolvedValue()
    await inputs[2]!.setValue('new-password')
    await wrapper.get('form').trigger('submit')
    await flushPromises()
    expect(changeMyLocalPassword).toHaveBeenCalledWith('current-password', 'new-password')
  })
})
