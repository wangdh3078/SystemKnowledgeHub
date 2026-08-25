import { flushPromises, mount } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { ApiError } from '../../../api/errors/ApiError'
import type {
  RequirementTraceabilityResponse,
  SpecificationTraceabilityResponse,
  TestCaseTraceabilityResponse,
  TraceDocument,
  TraceDocumentRelation,
  TraceRelationship,
  TraceabilityResponse,
} from '../api/traceabilityContracts'
import { getKnowledgeDocumentTraceability } from '../api/traceabilityApi'
import TraceabilitySection from './TraceabilitySection.vue'

const routerState = vi.hoisted(() => ({ push: vi.fn() }))
const overlayState = vi.hoisted(() => ({ openDrawer: vi.fn() }))

vi.mock('vue-router', () => ({ useRouter: () => routerState }))
vi.mock('../../../app/stores/overlays', () => ({ useOverlayStore: () => overlayState }))
vi.mock('../api/traceabilityApi', () => ({ getKnowledgeDocumentTraceability: vi.fn() }))

const node = <TDocumentType extends TraceDocument['documentType']>(
  id: number,
  documentType: TDocumentType,
  title = `${documentType} ${id}`,
): TraceDocument & { readonly documentType: TDocumentType } => ({
  id,
  documentType,
  title,
  lifecycleStatus: id % 2 ? 'Draft' : 'Published',
  knowledgeStatus: id % 3 === 0 ? 'Confirmed' : id % 3 === 1 ? 'Inferred' : 'Unknown',
  currentRevisionNumber: 2,
  evidenceCount: 2,
  humanConfirmationCount: 1,
  confirmationCoverage: {
    state: 'CurrentRevisionConfirmed',
    lastConfirmedRevisionNumber: 2,
  },
})

const relationship = (
  id: number,
  relationType: TraceRelationship['relationType'],
  direction: TraceRelationship['direction'] = 'Outgoing',
): TraceRelationship => ({
  id,
  relationType,
  direction,
  knowledgeStatus: id % 2 ? 'Unknown' : 'Confirmed',
  evidenceCount: 1,
  humanConfirmationCount: 0,
})

const relation = (
  id: number,
  document: TraceDocument,
  relationType: TraceRelationship['relationType'] = 'VerifiedBy',
  direction: TraceRelationship['direction'] = 'Outgoing',
): TraceDocumentRelation => ({ relationship: relationship(id, relationType, direction), document })

const metadata = {
  lineage: { incoming: [], outgoing: [], total: 0, isTruncated: false },
  cycleDetected: false,
  isTruncated: false,
  truncationReasons: [],
  limits: { maxDepth: 2, maxNodes: 200, maxEdges: 300, maxLineageEntries: 20 },
} as const

function requirementTrace(): RequirementTraceabilityResponse {
  const specificationA = node(2, 'Specification', '规格 A')
  const specificationB = node(4, 'Specification', '规格 B')
  const testCase = node(3, 'TestCase', '测试 A')
  return {
    root: node(1, 'Requirement', '需求 R'),
    coverage: {
      eligibility: 'Active',
      hasSpecification: true,
      hasDirectTestDefinition: true,
      hasSpecificationTestDefinition: true,
      hasAnyTestDefinition: true,
      missingLinkCodes: [],
    },
    specifications: [
      {
        relationship: relationship(10, 'SpecifiedBy'),
        document: specificationA,
        coverage: { hasTestDefinition: true, missingLinkCodes: [] },
        testCases: [relation(11, testCase)],
      },
      {
        relationship: relationship(12, 'SpecifiedBy'),
        document: specificationB,
        coverage: { hasTestDefinition: false, missingLinkCodes: ['MissingTestDefinition'] },
        testCases: [],
      },
    ],
    directTestCases: [relation(13, testCase)],
    upstreamRequirements: [],
    ...metadata,
  }
}

function specificationTrace(): SpecificationTraceabilityResponse {
  return {
    root: node(2, 'Specification', '规格 S'),
    coverage: { eligibility: 'Active', hasTestDefinition: false, missingLinkCodes: ['MissingTestDefinition'] },
    upstreamRequirements: [],
    testCases: [],
    ...metadata,
  }
}

function coveredSpecificationTrace(): SpecificationTraceabilityResponse {
  return {
    ...specificationTrace(),
    coverage: { eligibility: 'Active', hasTestDefinition: true, missingLinkCodes: [] },
    testCases: [relation(31, node(3, 'TestCase', '测试 T'))],
  }
}

function testCaseTrace(): TestCaseTraceabilityResponse {
  return {
    root: node(3, 'TestCase', '测试 T'),
    coverage: { eligibility: 'Active', missingLinkCodes: [] },
    directRequirements: [relation(20, node(1, 'Requirement', '需求 R'), 'VerifiedBy', 'Incoming')],
    upstreamSpecifications: [
      {
        relationship: relationship(21, 'VerifiedBy', 'Incoming'),
        document: node(2, 'Specification', '规格 S'),
        upstreamRequirements: [relation(22, node(4, 'Requirement', '需求 U'), 'SpecifiedBy', 'Incoming')],
      },
    ],
    ...metadata,
  }
}

const global = {
  stubs: {
    ElTag: { template: '<span><slot /></span>' },
    LoadingState: { props: ['message'], template: '<div>{{ message }}</div>' },
    EmptyState: { props: ['title', 'description'], template: '<div>{{ title }} {{ description }}</div>' },
    ErrorState: {
      props: ['title', 'message'],
      emits: ['retry'],
      template: '<div>{{ title }} {{ message }}<button @click="$emit(\'retry\')">重试</button></div>',
    },
  },
}

function mountSection(documentId = 1) {
  return mount(TraceabilitySection, { props: { documentId }, global })
}

describe('TraceabilitySection', () => {
  beforeEach(() => {
    routerState.push.mockReset()
    overlayState.openDrawer.mockReset()
    vi.mocked(getKnowledgeDocumentTraceability).mockReset()
  })

  afterEach(() => vi.restoreAllMocks())

  it('renders Requirement branches, direct Test Definitions, trust, and existing navigation affordances', async () => {
    vi.mocked(getKnowledgeDocumentTraceability).mockResolvedValue(requirementTrace())
    const wrapper = mountSection()
    await flushPromises()

    expect(wrapper.text()).toContain('规格说明')
    expect(wrapper.text()).toContain('直接测试定义')
    expect(wrapper.text()).toContain('规格 A')
    expect(wrapper.text()).toContain('规格 B')
    expect(wrapper.text()).toContain('缺少测试定义')
    expect(wrapper.findAll('[aria-label="打开测试用例：测试 A"]')).toHaveLength(2)
    expect(wrapper.text()).toContain('证据 2 · 人工确认 1')
    expect(wrapper.text()).toContain('草稿')
    expect(wrapper.text()).toContain('已发布')

    await wrapper.get('[aria-label="打开规格说明：规格 A"]').trigger('click')
    expect(routerState.push).toHaveBeenCalledWith({
      name: 'knowledge-document-detail',
      params: { id: '2' },
    })
    await wrapper.get('[aria-label="查看关系详情：由规格说明定义"]').trigger('click')
    expect(overlayState.openDrawer).toHaveBeenCalledWith({ kind: 'relationship', id: 10, mode: 'read' })
  })

  it('presents Requirement missing Specification and Test Definition as structural gaps', async () => {
    const response = requirementTrace()
    vi.mocked(getKnowledgeDocumentTraceability).mockResolvedValue({
      ...response,
      coverage: {
        ...response.coverage,
        hasSpecification: false,
        hasDirectTestDefinition: false,
        hasSpecificationTestDefinition: false,
        hasAnyTestDefinition: false,
        missingLinkCodes: ['MissingSpecification', 'MissingTestDefinition'],
      },
      specifications: [],
      directTestCases: [],
    })
    const wrapper = mountSection()
    await flushPromises()

    expect(wrapper.text()).toContain('缺少规格说明')
    expect(wrapper.text()).toContain('缺少测试定义')
    expect(wrapper.text()).not.toContain('验证失败')
  })

  it('renders Specification neutral upstream empty state independently from missing Test Definition', async () => {
    vi.mocked(getKnowledgeDocumentTraceability).mockResolvedValue(specificationTrace())
    const wrapper = mountSection(2)
    await flushPromises()

    expect(wrapper.text()).toContain('上游需求')
    expect(wrapper.text()).toContain('暂无上游需求关系')
    expect(wrapper.text()).toContain('缺少测试定义')
    expect(wrapper.text()).not.toContain('缺少需求')
  })

  it('renders TestCase direct Requirement and Specification contexts without flattening them', async () => {
    vi.mocked(getKnowledgeDocumentTraceability).mockResolvedValue(testCaseTrace())
    const wrapper = mountSection(3)
    await flushPromises()

    expect(wrapper.text()).toContain('验证对象')
    expect(wrapper.text()).toContain('需求 R')
    expect(wrapper.text()).toContain('规格 S')
    expect(wrapper.text()).toContain('上游需求')
    expect(wrapper.text()).toContain('需求 U')
  })

  it('keeps archived roots free of active structural gaps and shows bounded warnings and lineage', async () => {
    const response = requirementTrace()
    vi.mocked(getKnowledgeDocumentTraceability).mockResolvedValue({
      ...response,
      root: { ...response.root, lifecycleStatus: 'Archived' },
      coverage: {
        ...response.coverage,
        eligibility: 'ExcludedArchived',
        missingLinkCodes: [],
      },
      specifications: [],
      directTestCases: [],
      cycleDetected: true,
      isTruncated: true,
      truncationReasons: ['MaxNodes'],
      lineage: {
        incoming: [],
        outgoing: [relation(90, node(8, 'Requirement', '旧需求'), 'Supersedes')],
        total: 21,
        isTruncated: true,
      },
    })
    const wrapper = mountSection()
    await flushPromises()

    expect(wrapper.text()).toContain('此文档已归档，不计入当前可追溯覆盖。')
    expect(wrapper.text()).toContain('检测到循环关系，已停止继续展开。')
    expect(wrapper.text()).toContain('可追溯关系较多，当前仅显示部分结果。')
    expect(wrapper.text()).toContain('仅显示部分替代关系。')
    expect(wrapper.text()).toContain('此文档替代')
    expect(wrapper.text()).not.toContain('缺少规格说明')
  })

  it('uses a contextual invalid-reference error and retries only the trace request', async () => {
    vi.mocked(getKnowledgeDocumentTraceability)
      .mockRejectedValueOnce(new ApiError(422, { code: 'reference_invalid', message: 'invalid', fieldErrors: null, details: null }))
      .mockResolvedValueOnce(requirementTrace())
    const wrapper = mountSection()
    await flushPromises()

    expect(wrapper.text()).toContain('可追溯关系中存在无效引用，无法安全展示该链路。')
    await wrapper.get('button').trigger('click')
    await flushPromises()
    expect(getKnowledgeDocumentTraceability).toHaveBeenCalledTimes(2)
    expect(wrapper.text()).toContain('规格说明')
  })

  it('keeps the newest route trace when an earlier request completes last', async () => {
    let resolveA: ((value: TraceabilityResponse) => void) | undefined
    let resolveB: ((value: TraceabilityResponse) => void) | undefined
    vi.mocked(getKnowledgeDocumentTraceability)
      .mockImplementationOnce(() => new Promise<TraceabilityResponse>((resolve) => { resolveA = resolve }))
      .mockImplementationOnce(() => new Promise<TraceabilityResponse>((resolve) => { resolveB = resolve }))
    const wrapper = mountSection(1)
    await flushPromises()
    await wrapper.setProps({ documentId: 2 })
    resolveB?.({
      ...specificationTrace(),
      upstreamRequirements: [relation(30, node(9, 'Requirement', '需求 B'), 'SpecifiedBy', 'Incoming')],
    })
    await flushPromises()
    resolveA?.(requirementTrace())
    await flushPromises()

    expect(wrapper.text()).toContain('需求 B')
    expect(wrapper.text()).not.toContain('需求 R')
  })

  it('exposes one authoritative refresh entry point', async () => {
    vi.mocked(getKnowledgeDocumentTraceability).mockResolvedValue(requirementTrace())
    const wrapper = mountSection()
    await flushPromises()
    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    await flushPromises()

    expect(getKnowledgeDocumentTraceability).toHaveBeenCalledTimes(2)
  })

  it('replaces covered and missing Specification states through authoritative refreshes', async () => {
    vi.mocked(getKnowledgeDocumentTraceability)
      .mockResolvedValueOnce(coveredSpecificationTrace())
      .mockResolvedValueOnce(specificationTrace())
      .mockResolvedValueOnce(coveredSpecificationTrace())
    const wrapper = mountSection(2)
    await flushPromises()

    expect(wrapper.text()).toContain('测试 T')
    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    await flushPromises()
    expect(wrapper.text()).toContain('缺少测试定义')
    expect(wrapper.text()).not.toContain('测试 T')

    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    await flushPromises()
    expect(wrapper.text()).toContain('测试 T')
    expect(wrapper.text()).not.toContain('缺少测试定义')
  })

  it('does not present stale trace data when an authoritative refresh fails', async () => {
    vi.mocked(getKnowledgeDocumentTraceability)
      .mockResolvedValueOnce(coveredSpecificationTrace())
      .mockRejectedValueOnce(new Error('refresh failed'))
    const wrapper = mountSection(2)
    await flushPromises()
    expect(wrapper.text()).toContain('测试 T')

    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    await flushPromises()

    expect(wrapper.text()).toContain('可追溯性加载失败')
    expect(wrapper.text()).not.toContain('测试 T')
  })

  it('keeps the newest same-root refresh when an older refresh completes last', async () => {
    let resolveA: ((value: TraceabilityResponse) => void) | undefined
    let resolveB: ((value: TraceabilityResponse) => void) | undefined
    vi.mocked(getKnowledgeDocumentTraceability)
      .mockResolvedValueOnce(coveredSpecificationTrace())
      .mockImplementationOnce(() => new Promise<TraceabilityResponse>((resolve) => { resolveA = resolve }))
      .mockImplementationOnce(() => new Promise<TraceabilityResponse>((resolve) => { resolveB = resolve }))
    const wrapper = mountSection(2)
    await flushPromises()

    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    ;(wrapper.vm as unknown as { refresh: () => void }).refresh()
    resolveB?.(specificationTrace())
    await flushPromises()
    resolveA?.(coveredSpecificationTrace())
    await flushPromises()

    expect(wrapper.text()).toContain('缺少测试定义')
    expect(wrapper.text()).not.toContain('测试 T')
  })
})
