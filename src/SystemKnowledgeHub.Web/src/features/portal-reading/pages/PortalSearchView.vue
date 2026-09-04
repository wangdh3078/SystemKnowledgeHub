<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { RouterLink, useRoute, useRouter } from 'vue-router'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
import { portalReadApi } from '../api/portalReadApi'
import type { PortalSearchResponse, PortalTargetType } from '../api/portalReadContracts'

const route = useRoute()
const router = useRouter()
const response = ref<PortalSearchResponse | null>(null)
const loading = ref(false)
const failed = ref(false)
let request: AbortController | null = null
const labels: Readonly<Record<PortalTargetType, string>> = {
  System: '系统',
  BusinessFunction: '业务功能',
  DatabaseObject: '数据库对象',
  KnowledgeDocument: '知识文档',
  Integration: '集成关系',
}
const query = computed(() =>
  typeof route.query.q === 'string' ? route.query.q.trim().slice(0, 100) : '',
)
const page = computed(() => positive(route.query.page, 1))
const pageSize = computed(() => Math.min(100, positive(route.query.pageSize, 20)))

function positive(value: unknown, fallback: number): number {
  const parsed = typeof value === 'string' ? Number(value) : Number.NaN
  return Number.isSafeInteger(parsed) && parsed > 0 ? parsed : fallback
}
async function load(): Promise<void> {
  request?.abort()
  response.value = null
  if (!query.value) return
  request = new AbortController()
  loading.value = true
  failed.value = false
  try {
    response.value = await portalReadApi.search(
      query.value,
      page.value,
      pageSize.value,
      request.signal,
    )
  } catch (error: unknown) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    failed.value = true
  } finally {
    loading.value = false
  }
}
function changePage(value: number): void {
  void router.push({
    name: 'portal-search',
    query: { q: query.value, page: value, pageSize: pageSize.value },
  })
}
function changePageSize(value: number): void {
  void router.push({ name: 'portal-search', query: { q: query.value, page: 1, pageSize: value } })
}
watch(
  () => route.fullPath,
  () => void load(),
  { immediate: true },
)
onBeforeUnmount(() => request?.abort())
</script>

<template>
  <div class="portal-search-page">
    <header>
      <p class="portal-eyebrow">搜索</p>
      <h1>“{{ query }}”的搜索结果</h1>
    </header>
    <p v-if="loading" class="portal-muted">正在搜索已发布知识…</p>
    <section v-else-if="failed" class="portal-feedback">
      <h2>搜索暂时不可用</h2>
      <button type="button" @click="load">重试</button>
    </section>
    <p v-else-if="!query || response?.items.length === 0" class="portal-empty">
      未找到匹配的已发布知识。
    </p>
    <ul v-else class="portal-search-results">
      <li v-for="item in response?.items" :key="item.pageId">
        <RouterLink :to="{ name: 'portal-page', params: { id: item.pageId } }">
          <span class="portal-type-badge">{{ labels[item.primaryTargetType] }}</span>
          <h2>{{ item.title }}</h2>
          <p class="portal-search-breadcrumb">
            {{ [...item.breadcrumb.map((entry) => entry.title), item.title].join(' / ') }}
          </p>
          <p>{{ item.snippet }}</p>
        </RouterLink>
      </li>
    </ul>
    <SkhPagination
      v-if="response"
      :total="response.total"
      :current-page="response.page"
      :page-size="response.pageSize"
      aria-label="门户搜索分页"
      @current-change="changePage"
      @size-change="changePageSize"
    />
  </div>
</template>
