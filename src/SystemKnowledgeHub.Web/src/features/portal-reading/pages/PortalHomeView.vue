<script setup lang="ts">
import { ArrowRight, Collection, Document } from '@element-plus/icons-vue'
import { onBeforeUnmount, onMounted, ref } from 'vue'
import { RouterLink } from 'vue-router'
import { portalReadApi } from '../api/portalReadApi'
import type { PortalHomeResponse, PortalTargetType } from '../api/portalReadContracts'

const home = ref<PortalHomeResponse | null>(null)
const loading = ref(true)
const failed = ref(false)
let request: AbortController | null = null

const targetLabels: Readonly<Record<PortalTargetType, string>> = {
  System: '系统',
  BusinessFunction: '业务功能',
  DatabaseObject: '数据库对象',
  KnowledgeDocument: '知识文档',
  Integration: '集成关系',
}

async function loadHome(): Promise<void> {
  request?.abort()
  request = new AbortController()
  loading.value = true
  failed.value = false
  try {
    home.value = await portalReadApi.getHome(request.signal)
  } catch (error: unknown) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    failed.value = true
  } finally {
    loading.value = false
  }
}

onMounted(() => void loadHome())
onBeforeUnmount(() => request?.abort())
</script>

<template>
  <div class="portal-home">
    <header class="portal-home__hero">
      <p class="portal-eyebrow">Knowledge Portal</p>
      <h1>系统知识中心</h1>
      <p>浏览已发布的系统、业务、数据库和知识文档。</p>
    </header>

    <div v-if="loading" class="portal-loading" aria-live="polite">
      <span class="portal-skeleton portal-skeleton--title"></span>
      <span class="portal-skeleton"></span>
      <span class="portal-skeleton"></span>
    </div>
    <section v-else-if="failed" class="portal-feedback" aria-live="polite">
      <h2>知识暂时无法加载</h2>
      <p>知识暂时无法加载，请稍后重试。</p>
      <button type="button" @click="loadHome">重试</button>
    </section>
    <template v-else-if="home">
      <section class="portal-home__section" aria-labelledby="portal-home-categories">
        <div class="portal-home__section-heading">
          <div>
            <p class="portal-eyebrow">目录</p>
            <h2 id="portal-home-categories">知识目录</h2>
          </div>
        </div>
        <p v-if="home.categories.length === 0" class="portal-empty">暂无已发布知识</p>
        <ul v-else class="portal-category-list">
          <li v-for="category in home.categories" :key="category.nodeId">
            <RouterLink
              v-if="category.nodeKind === 'Page'"
              :to="{ name: 'portal-page', params: { id: category.pageId } }"
            >
              <el-icon><Document /></el-icon><span>{{ category.title }}</span
              ><el-icon><ArrowRight /></el-icon>
            </RouterLink>
            <div v-else>
              <el-icon><Collection /></el-icon><span>{{ category.title }}</span
              ><small>从目录展开浏览</small>
            </div>
          </li>
        </ul>
      </section>

      <section class="portal-home__section" aria-labelledby="portal-home-recent">
        <div class="portal-home__section-heading">
          <div>
            <p class="portal-eyebrow">最近更新</p>
            <h2 id="portal-home-recent">最近发布</h2>
          </div>
        </div>
        <p v-if="home.recentPages.length === 0" class="portal-empty">暂无已发布知识</p>
        <ul v-else class="portal-recent-list">
          <li v-for="page in home.recentPages" :key="page.id">
            <RouterLink :to="{ name: 'portal-page', params: { id: page.id } }">
              <div>
                <span class="portal-type-badge">{{ targetLabels[page.primaryTarget.type] }}</span>
                <h3>{{ page.title }}</h3>
                <p>{{ [...page.breadcrumb.map((item) => item.title), page.title].join(' / ') }}</p>
              </div>
              <el-icon><ArrowRight /></el-icon>
            </RouterLink>
          </li>
        </ul>
      </section>
    </template>
  </div>
</template>
