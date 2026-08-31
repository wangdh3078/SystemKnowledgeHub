import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import { getKnowledgeDocumentImpact } from '../api/impactApi'
import type { ImpactItem, ImpactResponse } from '../api/impactContracts'
import ImpactContextSection from './ImpactContextSection.vue'

const routerState = vi.hoisted(() => ({ push: vi.fn() }))
vi.mock('vue-router', () => ({ useRouter: () => routerState }))
vi.mock('../api/impactApi', () => ({ getKnowledgeDocumentImpact: vi.fn() }))

function item(
  id: number,
  pathKind: ImpactItem['pathKind'],
  meaning: ImpactItem['meaning'],
  title: string,
): ImpactItem {
  const path = pathKind === 'DirectAppliesTo'
    ? [{ relationshipId: id * 10, relationType: 'AppliesTo' as const, direction: 'Outgoing' as const }]
    : pathKind === 'DirectDocuments'
      ? [{ relationshipId: id * 10, relationType: 'Documents' as const, direction: 'Outgoing' as const }]
      : pathKind === 'ViaSpecificationDocuments'
        ? [
            { relationshipId: id * 10, relationType: 'SpecifiedBy' as const, direction: 'Outgoing' as const },
            { relationshipId: id * 10 + 1, relationType: 'Documents' as const, direction: 'Outgoing' as const },
          ]
        : pathKind === 'ViaRequirementAppliesTo'
          ? [
              { relationshipId: id * 10, relationType: 'SpecifiedBy' as const, direction: 'Incoming' as const },
              { relationshipId: id * 10 + 1, relationType: 'AppliesTo' as const, direction: 'Outgoing' as const },
            ]
          : pathKind === 'ViaRequirementDocuments'
            ? [
                { relationshipId: id * 10, relationType: 'SpecifiedBy' as const, direction: 'Incoming' as const },
                { relationshipId: id * 10 + 1, relationType: 'Documents' as const, direction: 'Outgoing' as const },
              ]
            : pathKind === 'ViaVerifiedRequirementAppliesTo'
              ? [
                  { relationshipId: id * 10, relationType: 'VerifiedBy' as const, direction: 'Incoming' as const },
                  { relationshipId: id * 10 + 1, relationType: 'AppliesTo' as const, direction: 'Outgoing' as const },
                ]
              : [
                  { relationshipId: id * 10, relationType: 'VerifiedBy' as const, direction: 'Incoming' as const },
                  { relationshipId: id * 10 + 1, relationType: 'Documents' as const, direction: 'Outgoing' as const },
                ]
  return {
    pathKind,
    meaning,
    target: {
      type: id % 3 === 0 ? 'DatabaseObject' : id % 2 === 0 ? 'BusinessFunction' : 'System',
      id,
      title,
      systemContext: [{ id: 90, name: 'MES' }],
    },
    path,
  }
}

function response(items: readonly ImpactItem[], page = 1, pageSize = 20, total = items.length): ImpactResponse {
  return { items, page, pageSize, total, maxDepth: 2 }
}

const global = {
  stubs: {
    LoadingState: { props: ['message'], template: '<div>{{ message }}</div>' },
    EmptyState: { props: ['title', 'description'], template: '<div>{{ title }} {{ description }}</div>' },
    ErrorState: {
      props: ['title', 'message'],
      emits: ['retry'],
      template: '<div role="alert">{{ title }} {{ message }}<button @click="$emit(\'retry\')">重试</button></div>',
    },
    ElPagination: {
      props: ['currentPage', 'pageSize', 'total'],
      emits: ['current-change'],
      template: '<nav aria-label="影响上下文分页"><button class="next-page" @click="$emit(\'current-change\', 2)">第 2 页</button></nav>',
    },
  },
}

function mountSection(documentId = 1) {
  return mount(ImpactContextSection, { props: { documentId }, global })
}

function deferred<T>() {
  let resolve!: (value: T) => void
  let reject!: (reason?: unknown) => void
  const promise = new Promise<T>((resolvePromise, rejectPromise) => {
    resolve = resolvePromise
    reject = rejectPromise
  })
  return { promise, resolve, reject }
}

describe('ImpactContextSection', () => {
  beforeEach(() => {
    routerState.push.mockReset()
    vi.mocked(getKnowledgeDocumentImpact).mockReset()
  })

  afterEach(() => vi.restoreAllMocks())

  it('renders Requirement meanings as separate compact groups and preserves same-target meanings', async () => {
    vi.mocked(getKnowledgeDocumentImpact).mockResolvedValue(response([
      item(1, 'DirectAppliesTo', 'ExplicitRequirementScope', 'MES'),
      item(1, 'DirectDocuments', 'DocumentedByRequirement', 'MES'),
      item(3, 'ViaSpecificationDocuments', 'DocumentedBySpecification', 'dbo.orders'),
    ]))
    const wrapper = mountSection()
    await flushPromises()

    expect(wrapper.text()).toContain('影响上下文')
    expect(wrapper.text()).toContain('不代表实际或必然影响')
    expect(wrapper.text()).toContain('明确适用范围')
    expect(wrapper.text()).toContain('需求直接文档化的上下文')
    expect(wrapper.text()).toContain('由规格说明带入的上下文')
    expect(wrapper.findAll('[aria-label="打开系统 MES"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('为什么显示：')
    expect(wrapper.text()).toMatch(/关系性质：\s*直接上下文/)
    expect(wrapper.text()).toContain('上下文对象：')
    expect(wrapper.text()).toMatch(/关系性质：\s*间接上下文/)
    expect(wrapper.text()).toContain('当前需求 → 规格说明 → 说明 → dbo.orders')
    expect(wrapper.text()).toContain('仅用于辅助人工复核，不表示当前文档一定直接影响该对象。')
    expect(wrapper.text()).toContain('系统上下文：MES')
  })

  it('renders Specification and TestCase derived meanings without claiming inherited canonical AppliesTo', async () => {
    vi.mocked(getKnowledgeDocumentImpact).mockResolvedValueOnce(response([
      item(1, 'DirectDocuments', 'DocumentedBySpecification', 'MES'),
      item(2, 'ViaRequirementAppliesTo', 'UpstreamRequirementScope', 'WMS'),
      item(3, 'ViaRequirementDocuments', 'UpstreamRequirementDocumentedContext', 'dbo.orders'),
    ]))
    const specification = mountSection(2)
    await flushPromises()
    expect(specification.text()).toContain('上游需求声明的适用范围')
    expect(specification.text()).toContain('作为当前规格说明的间接复核上下文显示')
    expect(specification.text()).toContain('上游需求文档化的上下文')

    vi.mocked(getKnowledgeDocumentImpact).mockResolvedValueOnce(response([
      item(1, 'DirectDocuments', 'DocumentedByTestCase', 'MES'),
      item(2, 'ViaVerifiedRequirementAppliesTo', 'VerifiedRequirementScope', 'WMS'),
      item(3, 'ViaVerifiedSpecificationDocuments', 'VerifiedSpecificationDocumentedContext', 'dbo.orders'),
    ]))
    await specification.setProps({ documentId: 3 })
    await flushPromises()
    expect(specification.text()).toContain('测试用例直接文档化的对象')
    expect(specification.text()).toContain('直接验证需求的适用范围')
    expect(specification.text()).toContain('所验证规格说明文档化的上下文')
  })

  it('renders independent loading, neutral empty, generic error/retry, and invalid-reference states', async () => {
    const initial = deferred<ImpactResponse>()
    vi.mocked(getKnowledgeDocumentImpact).mockReturnValueOnce(initial.promise)
    const wrapper = mountSection()
    expect(wrapper.text()).toContain('正在读取影响上下文')
    initial.resolve(response([]))
    await flushPromises()
    expect(wrapper.text()).toContain('暂无影响上下文')
    expect(wrapper.text()).toContain('当前没有通过已支持关系表达的影响上下文')
    expect(wrapper.text()).not.toContain('没有影响')

    vi.mocked(getKnowledgeDocumentImpact).mockRejectedValueOnce(new Error('offline'))
    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    await flushPromises()
    expect(wrapper.text()).toContain('影响上下文加载失败')
    expect(wrapper.text()).toContain('当前无法读取影响上下文')

    vi.mocked(getKnowledgeDocumentImpact).mockRejectedValueOnce(
      new ApiError(422, { code: 'reference_invalid', message: 'invalid', fieldErrors: null, details: null }),
    )
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(wrapper.text()).toContain('影响上下文中存在无法安全解析的引用')
  })

  it('changes only the Impact page and navigates targets through existing detail routes', async () => {
    vi.mocked(getKnowledgeDocumentImpact)
      .mockResolvedValueOnce(response(
        [item(1, 'DirectAppliesTo', 'ExplicitRequirementScope', 'MES')],
        1,
        20,
        21,
      ))
      .mockResolvedValueOnce(response(
        [item(2, 'DirectDocuments', 'DocumentedByRequirement', 'Inventory')],
        2,
        20,
        21,
      ))
    const wrapper = mountSection()
    await flushPromises()
    expect(wrapper.text()).toContain('当前 1–20 / 21')
    await wrapper.get('.next-page').trigger('click')
    await flushPromises()
    expect(getKnowledgeDocumentImpact).toHaveBeenLastCalledWith(1, 2, 20, expect.any(AbortSignal))
    expect(wrapper.text()).toContain('当前 21–21 / 21')
    await wrapper.get('[aria-label="打开业务功能 Inventory"]').trigger('click')
    expect(routerState.push).toHaveBeenCalledWith({
      name: 'business-function-detail',
      params: { id: '2' },
    })
  })

  it('keeps the newest pagination response when an older page completes late', async () => {
    const refreshedFirstPage = deferred<ImpactResponse>()
    const secondPage = deferred<ImpactResponse>()
    vi.mocked(getKnowledgeDocumentImpact)
      .mockResolvedValueOnce(response(
        [item(1, 'DirectDocuments', 'DocumentedByRequirement', 'Initial')],
        1,
        20,
        21,
      ))
      .mockReturnValueOnce(refreshedFirstPage.promise)
      .mockReturnValueOnce(secondPage.promise)
    const wrapper = mountSection()
    await flushPromises()
    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    await wrapper.get('.next-page').trigger('click')
    secondPage.resolve(response(
      [item(2, 'DirectDocuments', 'DocumentedByRequirement', 'Page 2 newest')],
      2,
      20,
      21,
    ))
    await flushPromises()
    refreshedFirstPage.resolve(response(
      [item(1, 'DirectDocuments', 'DocumentedByRequirement', 'Page 1 stale')],
      1,
      20,
      21,
    ))
    await flushPromises()
    expect(wrapper.text()).toContain('Page 2 newest')
    expect(wrapper.text()).not.toContain('Page 1 stale')
    expect(wrapper.text()).toContain('当前 21–21 / 21')
  })

  it('keeps the newest authoritative relationship refresh when an older reload completes late', async () => {
    const older = deferred<ImpactResponse>()
    const newer = deferred<ImpactResponse>()
    vi.mocked(getKnowledgeDocumentImpact)
      .mockResolvedValueOnce(response([item(1, 'DirectAppliesTo', 'ExplicitRequirementScope', 'Initial')]))
      .mockReturnValueOnce(older.promise)
      .mockReturnValueOnce(newer.promise)
    const wrapper = mountSection()
    await flushPromises()
    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    newer.resolve(response([item(2, 'DirectAppliesTo', 'ExplicitRequirementScope', 'Re-added current')]))
    await flushPromises()
    older.resolve(response([]))
    await flushPromises()
    expect(wrapper.text()).toContain('Re-added current')
    expect(wrapper.text()).not.toContain('暂无影响上下文')
  })

  it('protects root replacement and clears stale content after a failed relationship refresh', async () => {
    const oldRoot = deferred<ImpactResponse>()
    vi.mocked(getKnowledgeDocumentImpact)
      .mockReturnValueOnce(oldRoot.promise)
      .mockResolvedValueOnce(response([item(2, 'DirectDocuments', 'DocumentedBySpecification', 'New root')]))
    const wrapper = mountSection(1)
    await wrapper.setProps({ documentId: 2 })
    await flushPromises()
    oldRoot.resolve(response([item(1, 'DirectDocuments', 'DocumentedByRequirement', 'Old root')]))
    await flushPromises()
    expect(wrapper.text()).toContain('New root')
    expect(wrapper.text()).not.toContain('Old root')

    vi.mocked(getKnowledgeDocumentImpact).mockRejectedValueOnce(new Error('refresh failed'))
    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    await flushPromises()
    expect(wrapper.text()).toContain('影响上下文加载失败')
    expect(wrapper.text()).not.toContain('New root')
  })
})
