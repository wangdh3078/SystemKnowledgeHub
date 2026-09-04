<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import { parseSafeApiId } from '../../../api/contracts/id'
import PortalSectionRenderer from '../components/PortalSectionRenderer.vue'
import { portalReadApi } from '../api/portalReadApi'
import type { PortalPageResponse, PortalTargetType } from '../api/portalReadContracts'

const route = useRoute()
const page = ref<PortalPageResponse | null>(null)
const state = ref<'loading' | 'ready' | 'not-found' | 'error'>('loading')
let request: AbortController | null = null

const targetLabels: Readonly<Record<PortalTargetType, string>> = {
  System: '系统',
  BusinessFunction: '业务功能',
  DatabaseObject: '数据库对象',
  KnowledgeDocument: '知识文档',
  Integration: '集成关系',
}
const pageId = computed(() => parseSafeApiId(route.params.id))

async function loadPage(): Promise<void> {
  request?.abort()
  page.value = null
  state.value = 'loading'
  if (pageId.value === null) {
    state.value = 'not-found'
    document.title = '页面未找到 · 系统知识中心'
    return
  }
  request = new AbortController()
  try {
    page.value = await portalReadApi.getPage(pageId.value, request.signal)
    state.value = 'ready'
    document.title = `${page.value.title} · 系统知识中心`
  } catch (error: unknown) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    state.value = error instanceof ApiError && error.status === 404 ? 'not-found' : 'error'
    document.title = state.value === 'not-found' ? '页面未找到 · 系统知识中心' : '系统知识中心'
  }
}

watch(pageId, () => void loadPage(), { immediate: true })
onBeforeUnmount(() => request?.abort())
</script>

<template>
  <div v-if="state === 'loading'" class="portal-page portal-loading" aria-live="polite">
    <span class="portal-skeleton portal-skeleton--breadcrumb"></span>
    <span class="portal-skeleton portal-skeleton--title"></span>
    <span class="portal-skeleton"></span>
    <span class="portal-skeleton"></span>
  </div>
  <section v-else-if="state === 'not-found'" class="portal-feedback portal-feedback--centered">
    <h1>页面未找到</h1>
    <p>该知识可能尚未发布、已取消发布，或地址不正确。</p>
    <RouterLink class="portal-link-button" :to="{ name: 'portal-home' }">返回知识首页</RouterLink>
  </section>
  <section v-else-if="state === 'error'" class="portal-feedback portal-feedback--centered">
    <h1>知识暂时无法加载</h1>
    <p>知识暂时无法加载，请稍后重试。</p>
    <button type="button" @click="loadPage">重试</button>
  </section>
  <article v-else-if="page" class="portal-page">
    <nav class="portal-breadcrumb" aria-label="面包屑">
      <RouterLink :to="{ name: 'portal-home' }">知识首页</RouterLink>
      <template v-for="item in page.breadcrumb" :key="item.nodeId">
        <span aria-hidden="true">/</span><span>{{ item.title }}</span>
      </template>
      <span aria-hidden="true">/</span><span aria-current="page">{{ page.title }}</span>
    </nav>
    <header class="portal-page__header">
      <h1>{{ page.title }}</h1>
      <p>
        <span class="portal-type-badge">{{ targetLabels[page.primaryTarget.type] }}</span
        >{{ page.primaryTarget.title }}
      </p>
    </header>
    <PortalSectionRenderer v-for="section in page.sections" :key="section.id" :section="section" />
  </article>
</template>
