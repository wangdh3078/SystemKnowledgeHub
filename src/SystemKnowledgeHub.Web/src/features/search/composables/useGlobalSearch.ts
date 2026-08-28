import { computed, nextTick, onBeforeUnmount, ref, watch, type Ref } from 'vue'
import { useRouter } from 'vue-router'
import { useOverlayStore } from '../../../app/stores/overlays'
import { searchKnowledge } from '../api/searchApi'
import type { SearchKnowledgeResponse, SearchResultItem } from '../api/searchContracts'
import {
  clearRecentQueries,
  readRecentQueries,
  readRecentVisits,
  rememberQuery,
  rememberVisit,
  type RecentVisit,
} from './searchSession'

export type SearchSelectable =
  | { readonly kind: 'result'; readonly item: SearchResultItem; readonly objectType: string }
  | { readonly kind: 'query'; readonly query: string }
  | { readonly kind: 'visit'; readonly visit: RecentVisit }

export function useGlobalSearch(inputRef: Ref<{ focus: () => void } | null>) {
  const overlayStore = useOverlayStore()
  const router = useRouter()
  const query = ref('')
  const result = ref<SearchKnowledgeResponse | null>(null)
  const loading = ref(false)
  const errorMessage = ref<string | null>(null)
  const recentQueries = ref<readonly string[]>(readRecentQueries())
  const recentVisits = ref<readonly RecentVisit[]>(readRecentVisits())
  const activeIndex = ref(0)
  let debounceHandle: ReturnType<typeof setTimeout> | null = null
  let activeRequest: AbortController | null = null
  let returnFocusElement: HTMLElement | null = null

  const isOpen = computed(() => overlayStore.currentDialog?.kind === 'global-search')
  const normalizedQuery = computed(() => query.value.trim())
  const hasResults = computed(() => (result.value?.total ?? 0) > 0)
  const isNoResult = computed(() => normalizedQuery.value.length > 0 && !loading.value && !errorMessage.value && result.value?.total === 0)
  const selectableItems = computed<readonly SearchSelectable[]>(() => {
    if (hasResults.value && result.value) {
      return result.value.groups.flatMap(group => group.items.map(item => ({ kind: 'result' as const, item, objectType: group.objectType })))
    }

    if (normalizedQuery.value.length === 0) {
      return [
        ...recentQueries.value.map(item => ({ kind: 'query' as const, query: item })),
        ...recentVisits.value.map(visit => ({ kind: 'visit' as const, visit })),
      ]
    }

    return recentQueries.value.map(item => ({ kind: 'query' as const, query: item }))
  })

  function resetForOpen(): void {
    activeRequest?.abort()
    query.value = ''
    result.value = null
    loading.value = false
    errorMessage.value = null
    recentQueries.value = readRecentQueries()
    recentVisits.value = readRecentVisits()
    activeIndex.value = 0
  }

  async function runSearch(): Promise<void> {
    const requestQuery = normalizedQuery.value
    activeRequest?.abort()
    result.value = null
    errorMessage.value = null

    if (!requestQuery) {
      loading.value = false
      activeIndex.value = 0
      return
    }

    if (requestQuery.length > 100) {
      loading.value = false
      errorMessage.value = '搜索关键词不能超过 100 个字符。'
      return
    }

    activeRequest = new AbortController()
    loading.value = true
    try {
      const nextResult = await searchKnowledge({ query: requestQuery, limitPerGroup: 5 }, activeRequest.signal)
      if (requestQuery !== normalizedQuery.value) return
      result.value = nextResult
      recentQueries.value = rememberQuery(requestQuery)
      activeIndex.value = 0
    } catch (error: unknown) {
      if (error instanceof DOMException && error.name === 'AbortError') return
      errorMessage.value = error instanceof Error ? error.message : '全局搜索失败，请重试。'
    } finally {
      if (requestQuery === normalizedQuery.value) loading.value = false
    }
  }

  function scheduleSearch(): void {
    if (debounceHandle !== null) clearTimeout(debounceHandle)
    debounceHandle = setTimeout(() => {
      debounceHandle = null
      void runSearch()
    }, 220)
  }

  function moveSelection(offset: number): void {
    const items = selectableItems.value
    if (items.length === 0) return
    activeIndex.value = (activeIndex.value + offset + items.length) % items.length
  }

  async function navigate(item: SearchResultItem, objectType: string): Promise<void> {
    recentVisits.value = rememberVisit(item, objectType)
    overlayStore.closeDialog()
    if (item.navigation.routeObjectType === 'System') {
      await router.push({ name: 'system-detail', params: { id: String(item.navigation.routeObjectId) } })
      return
    }
    if (item.navigation.routeObjectType === 'BusinessFunction') {
      await router.push({ name: 'business-function-detail', params: { id: String(item.navigation.routeObjectId) } })
      return
    }
    if (item.navigation.routeObjectType === 'DatabaseObject') {
      await router.push({
        name: 'database-object-detail',
        params: { id: String(item.navigation.routeObjectId) },
        query: item.navigation.openDrawer === 'DatabaseColumn' && item.navigation.drawerObjectId !== null
          ? { selectedColumnId: String(item.navigation.drawerObjectId) }
          : {},
      })
      return
    }
    if (item.navigation.routeObjectType === 'BusinessRule') {
      await router.push({ name: 'business-rule-detail', params: { id: String(item.navigation.routeObjectId) } })
      return
    }
    if (item.navigation.routeObjectType === 'Integration') {
      await router.push({ name: 'integration-detail', params: { id: String(item.navigation.routeObjectId) } })
      return
    }
    if (item.navigation.routeObjectType === 'KnowledgeDocument') {
      await router.push({ name: 'knowledge-document-detail', params: { id: String(item.navigation.routeObjectId) } })
      return
    }
    await router.push({ name: 'unknown-item-detail', params: { id: String(item.navigation.routeObjectId) } })
  }

  function useRecentQuery(value: string): void {
    query.value = value
  }

  async function selectActive(): Promise<void> {
    const selected = selectableItems.value[activeIndex.value]
    if (!selected) {
      if (normalizedQuery.value) await runSearch()
      return
    }
    if (selected.kind === 'result') {
      await navigate(selected.item, selected.objectType)
    } else if (selected.kind === 'visit') {
      await openRecentVisit(selected.visit)
    } else {
      useRecentQuery(selected.query)
    }
  }

  async function openRecentVisit(visit: RecentVisit): Promise<void> {
    await navigate({
      id: visit.navigation.drawerObjectId ?? visit.navigation.routeObjectId,
      systemContext: visit.systemContext,
      title: visit.title,
      shortDescription: visit.objectType,
      knowledgeStatus: visit.knowledgeStatus,
      unknownItemStatus: visit.unknownItemStatus,
      navigation: visit.navigation,
      contentType: null,
      lifecycleStatus: null,
      updatedAt: null,
    }, visit.objectType)
  }

  function clearSearch(): void {
    query.value = ''
    result.value = null
    errorMessage.value = null
    activeIndex.value = 0
  }

  function clearQueries(): void {
    clearRecentQueries()
    recentQueries.value = []
    activeIndex.value = 0
  }

  function close(): void {
    overlayStore.closeDialog()
  }

  watch(isOpen, (open, wasOpen) => {
    if (open) {
      returnFocusElement = document.activeElement instanceof HTMLElement
        && document.activeElement !== document.body
        ? document.activeElement
        : null
      resetForOpen()
      return
    }
    if (!wasOpen) return
    const target = returnFocusElement
    returnFocusElement = null
    void nextTick(() => {
      if (target?.isConnected) target.focus({ preventScroll: true })
    })
  }, { flush: 'sync' })

  watch(() => overlayStore.dialogOpenedSequence, () => {
    if (isOpen.value) inputRef.value?.focus()
  })

  watch(query, () => {
    activeIndex.value = 0
    scheduleSearch()
  })

  onBeforeUnmount(() => {
    activeRequest?.abort()
    if (debounceHandle !== null) clearTimeout(debounceHandle)
  })

  return {
    activeIndex,
    clearQueries,
    clearSearch,
    close,
    errorMessage,
    hasResults,
    isNoResult,
    isOpen,
    loading,
    moveSelection,
    openRecentVisit,
    query,
    recentQueries,
    recentVisits,
    result,
    runSearch,
    selectActive,
    selectableItems,
    useRecentQuery,
    navigate,
  }
}
