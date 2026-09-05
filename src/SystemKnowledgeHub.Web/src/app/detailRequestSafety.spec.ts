import { afterEach, beforeEach, describe, expect, it, vi, type Mock } from 'vitest'
import { mount } from '@vue/test-utils'
import { defineComponent, h, ref } from 'vue'
import { useSystemDetail } from '../features/systems/composables/useSystemDetail'
import { useBusinessFunctionDetail } from '../features/business-functions/composables/useBusinessFunctionDetail'
import { useBusinessRuleDetail } from '../features/business-rules/composables/useBusinessRuleDetail'
import { useIntegrationDetail } from '../features/integrations/composables/useIntegrationDetail'
import { useUnknownItemDetail } from '../features/unknown-items/composables/useUnknownItemDetail'

const mocks = vi.hoisted(() => Object.fromEntries(
  ['system', 'function', 'rule', 'integration', 'unknown'].map(name => [name, { read: vi.fn(), write: vi.fn() }]),
) as Record<'system' | 'function' | 'rule' | 'integration' | 'unknown', { read: Mock<(...args: unknown[]) => Promise<unknown>>; write: Mock<(...args: unknown[]) => Promise<unknown>> }>)
vi.mock('../features/systems/api/systemsApi', () => ({ getSystemDetail: mocks.system.read, updateSystemOverview: mocks.system.write, updateSystemTechnology: mocks.system.write, updateSystemLifecycle: mocks.system.write }))
vi.mock('../features/business-functions/api/businessFunctionsApi', () => ({ getBusinessFunctionDetail: mocks.function.read, updateBusinessFunctionOverview: mocks.function.write, replaceBusinessProcessSteps: mocks.function.write }))
vi.mock('../features/business-rules/api/businessRulesApi', () => ({ businessRulesApi: { detail: mocks.rule.read, update: mocks.rule.write } }))
vi.mock('../features/integrations/api/integrationsApi', () => ({ integrationsApi: { detail: mocks.integration.read, updateOverview: mocks.integration.write, replaceContractFields: mocks.integration.write } }))
vi.mock('../features/unknown-items/api/unknownItemsApi', () => ({ unknownItemsApi: { detail: mocks.unknown.read } }))

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (cause: Error) => void
  const promise = new Promise<T>((yes, no) => { resolve = yes; reject = no })
  return { promise, resolve, reject }
}
const actor = { displayName: 'Tester', role: null }
const cases = [
  { name: 'system' as const, create: (id: () => number) => { const m = useSystemDetail(id); return { ...m, error: m.pageError, act: () => m.saveTechnology([], actor) } } },
  { name: 'function' as const, create: (id: () => number) => { const m = useBusinessFunctionDetail(id); return { ...m, act: () => m.saveProcess([], actor) } } },
  { name: 'rule' as const, create: (id: () => number) => { const m = useBusinessRuleDetail(id); return { ...m, act: () => m.save({} as never) } } },
  { name: 'integration' as const, create: (id: () => number) => { const m = useIntegrationDetail(id); return { ...m, act: () => m.saveFields([], actor) } } },
  { name: 'unknown' as const, create: (id: () => number) => { const m = useUnknownItemDetail(id); return { ...m, act: () => m.run(() => mocks.unknown.write(m.detail.value!.id)) } } },
]
const response = (id: number) => ({ id, concurrencyToken: `token-${id}`, availableActions: ['Edit', 'Delete', 'AddEvidence'] })
const wrappers: ReturnType<typeof mount>[] = []
afterEach(() => { wrappers.splice(0).forEach(w => w.unmount()) })
beforeEach(() => { Object.values(mocks).forEach(m => { m.read.mockReset(); m.write.mockReset() }) })

for (const scenario of cases) describe(`${scenario.name} detail request safety`, () => {
  function setup() {
    const selected = ref(1)
    let model!: ReturnType<typeof scenario.create>
    wrappers.push(mount(defineComponent({ setup() { model = scenario.create(() => selected.value); return () => h('div') } })))
    return { model, selected, api: mocks[scenario.name] }
  }
  it('A slow / B fast / A late keeps B selection, detail and mutation subject', async () => {
    const { model, selected, api } = setup()
    const a = deferred<ReturnType<typeof response>>()
    api.read.mockReturnValueOnce(a.promise).mockResolvedValue(response(2))
    const old = model.load(1)
    selected.value = 2
    await model.load(2)
    a.resolve(response(1)) // deliberately ignores AbortSignal
    await old
    expect(selected.value).toBe(2)
    expect(model.detail.value?.id).toBe(2)
    api.write.mockRejectedValue(new Error('controlled write failure'))
    await model.act()
    expect(api.write.mock.calls[0]?.[0]).toBe(2)
    expect((api.read.mock.calls[0]?.[1] as AbortSignal).aborted).toBe(true)
  })
  it('clears A immediately, blocks mutations while B loads, and leaves a safe B error', async () => {
    const { model, selected, api } = setup()
    api.read.mockResolvedValueOnce(response(1))
    await model.load(1)
    const b = deferred<ReturnType<typeof response>>()
    api.read.mockReturnValue(b.promise)
    selected.value = 2
    const pending = model.load(2)
    expect(model.detail.value).toBeNull()
    expect(model.loading.value).toBe(true)
    expect(await model.act()).toBe(false)
    expect(api.write).not.toHaveBeenCalled()
    b.reject(new Error('B unavailable'))
    await pending
    expect(model.detail.value).toBeNull()
    expect(model.error.value).toBe('B unavailable')
    expect(model.loading.value).toBe(false)
  })
  it('rapid A/B/A discards both stale errors and stale successes, including loading changes', async () => {
    const { model, selected, api } = setup()
    const first = deferred<ReturnType<typeof response>>()
    const second = deferred<ReturnType<typeof response>>()
    const third = deferred<ReturnType<typeof response>>()
    api.read.mockReturnValueOnce(first.promise).mockReturnValueOnce(second.promise).mockReturnValueOnce(third.promise)
    const a = model.load(1); selected.value = 2; const b = model.load(2); selected.value = 1; const newest = model.load(1)
    second.reject(new Error('stale B error')); first.resolve(response(1))
    await Promise.all([a, b])
    expect(model.loading.value).toBe(true)
    expect(model.error.value).toBeNull()
    expect(model.detail.value).toBeNull()
    third.resolve(response(1)); await newest
    expect(model.detail.value?.id).toBe(1)
  })
  it('validates live route identity before mutation even before a new load starts', async () => {
    const { model, selected, api } = setup()
    api.read.mockResolvedValue(response(1)); await model.load(1)
    selected.value = 2
    expect(await model.act()).toBe(false)
    expect(api.write).not.toHaveBeenCalled()
  })
  it('late mutation completion cannot reload the old entity or change the new error', async () => {
    const { model, selected, api } = setup()
    api.read.mockResolvedValue(response(1)); await model.load(1)
    const write = deferred<unknown>(); api.write.mockReturnValue(write.promise)
    const pending = model.act()
    selected.value = 2; api.read.mockRejectedValue(new Error('B failed')); await model.load(2)
    write.resolve({}); expect(await pending).toBe(false)
    expect(api.read).toHaveBeenCalledTimes(2)
    expect(model.detail.value).toBeNull()
    expect(model.error.value).toBe('B failed')
  })
  it('unmount makes outstanding success unable to restore detail', async () => {
    const { model, api } = setup()
    const read = deferred<ReturnType<typeof response>>(); api.read.mockReturnValue(read.promise)
    const pending = model.load(1); wrappers.pop()!.unmount(); read.resolve(response(1)); await pending
    expect(model.detail.value).toBeNull()
  })
})
