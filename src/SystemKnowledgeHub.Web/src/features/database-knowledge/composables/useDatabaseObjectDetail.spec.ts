import { createPinia, setActivePinia } from 'pinia'
import { beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import { useDatabaseObjectDetail } from './useDatabaseObjectDetail'
import { getDatabaseObjectDetail } from '../api/databaseKnowledgeApi'

vi.mock('../api/databaseKnowledgeApi', () => ({
  getDatabaseObjectDetail: vi.fn(),
}))

const detail = {
  id: 45,
  system: { id: 12, name: 'MES' },
  databaseSource: { id: 9, name: 'MES Oracle', engine: 'Oracle', concurrencyToken: 'source-token' },
  concurrencyToken: 'AAAAAQ',
  overview: {
    qualifiedName: 'MES.TABLE_EQP', objectType: 'Table' as const,
    businessDescription: '设备主数据', accessMode: 'ReadWrite' as const,
    knowledgeStatus: 'Inferred' as const,
  },
  metadata: { estimatedRows: 2400000, primaryKeyColumns: ['EQP_ID'], businessKeyColumns: ['EQP_CODE'] },
  columns: [],
  contextRail: { usedByFunctions: [], relatedRuleCount: 0, integrationCount: 0, openUnknownCount: 0 },
  selectedColumnDrawer: null,
  availableActions: [],
}

describe('useDatabaseObjectDetail', () => {
  beforeEach(() => {
    setActivePinia(createPinia())
    vi.mocked(getDatabaseObjectDetail).mockReset()
  })

  it('loads by id and opens the single global drawer for a selected column', async () => {
    vi.mocked(getDatabaseObjectDetail).mockResolvedValue(detail)
    const model = useDatabaseObjectDetail()

    await model.load(45, null)
    model.selectColumn(123)
    await nextTick()

    expect(getDatabaseObjectDetail).toHaveBeenCalledWith(45, undefined, expect.any(AbortSignal))
    expect(model.detail.value?.overview.qualifiedName).toBe('MES.TABLE_EQP')
    expect(model.selectedColumnId.value).toBe(123)
    expect(useOverlayStore().currentDrawer).toEqual({
      surface: 'drawer', kind: 'database-column', id: 123, mode: 'read',
    })
  })

  it('surfaces a query failure without leaving loading active', async () => {
    vi.mocked(getDatabaseObjectDetail).mockRejectedValue(new Error('offline'))
    const model = useDatabaseObjectDetail()

    await model.load(45, null)

    expect(model.loading.value).toBe(false)
    expect(model.errorMessage.value).toBe('offline')
  })

  it('exposes the page loading state until the detail query settles', async () => {
    let resolveDetail: ((value: typeof detail) => void) | undefined
    vi.mocked(getDatabaseObjectDetail).mockImplementation(
      () => new Promise((resolve) => { resolveDetail = resolve }),
    )
    const model = useDatabaseObjectDetail()

    const pending = model.load(45, null)
    expect(model.loading.value).toBe(true)

    resolveDetail?.(detail)
    await pending
    expect(model.loading.value).toBe(false)
    expect(model.detail.value?.id).toBe(45)
  })
})
