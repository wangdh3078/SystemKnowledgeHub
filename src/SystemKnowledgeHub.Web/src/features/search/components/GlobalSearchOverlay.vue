<script setup lang="ts">
import { ArrowRight, Close, Clock, Search } from '@element-plus/icons-vue'
import { computed, ref } from 'vue'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import type { SearchResultItem, UnknownItemStatus } from '../api/searchContracts'
import { useGlobalSearch } from '../composables/useGlobalSearch'

const inputRef = ref<{ focus: () => void } | null>(null)
const {
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
  navigate,
  openRecentVisit,
  query,
  recentQueries,
  recentVisits,
  result,
  runSearch,
  selectActive,
  selectableItems,
  useRecentQuery,
} = useGlobalSearch(inputRef)

const unknownItemStatusLabels: Readonly<Record<UnknownItemStatus, string>> = {
  Open: '待处理',
  Investigating: '调查中',
  ConclusionConfirmed: '结论已确认',
  Closed: '已关闭',
}

const totalLabel = computed(() => result.value?.total ?? 0)

const documentTypeLabels: Readonly<Record<string, string>> = {
  Requirement: '需求',
  Specification: '规格',
  TestCase: '测试用例',
  Sop: 'SOP',
  Troubleshooting: '故障排查',
  KnowledgeArticle: '知识文章',
  DesignNote: '设计说明',
}

const lifecycleLabels: Readonly<Record<NonNullable<SearchResultItem['lifecycleStatus']>, string>> = {
  Draft: '草稿',
  Published: '已发布',
  Archived: '已归档',
}

function resultTypeLabel(item: SearchResultItem, groupLabel: string): string {
  return item.contentType ? documentTypeLabels[item.contentType] ?? groupLabel : groupLabel
}

function selectableIndex(item: SearchResultItem): number {
  return selectableItems.value.findIndex(selectable => selectable.kind === 'result' && selectable.item.id === item.id && selectable.item.navigation.drawerObjectId === item.navigation.drawerObjectId)
}

function onKeydown(event: KeyboardEvent): void {
  if (event.key === 'ArrowDown') {
    event.preventDefault()
    moveSelection(1)
  } else if (event.key === 'ArrowUp') {
    event.preventDefault()
    moveSelection(-1)
  } else if (event.key === 'Enter') {
    event.preventDefault()
    void selectActive()
  } else if (event.key === 'Escape') {
    event.preventDefault()
    close()
  }
}

function selectResult(item: SearchResultItem, objectType: string): void {
  void navigate(item, objectType)
}
</script>

<template>
  <Teleport v-if="isOpen" defer to="#dialog-feature-content">
    <section class="global-search" aria-labelledby="global-search-title">
      <h2 id="global-search-title" class="sr-only">全局搜索</h2>
      <div class="global-search__input-row">
        <el-input
          ref="inputRef"
          v-model="query"
          size="large"
          :prefix-icon="Search"
          placeholder="搜索系统、业务功能、数据库对象、知识内容或待确认事项"
          aria-label="搜索所有知识对象"
          @keydown.stop="onKeydown"
        />
        <button class="global-search__close" type="button" aria-label="关闭全局搜索" @click="close">
          <el-icon><Close /></el-icon>
        </button>
        <kbd>Ctrl + K</kbd>
      </div>

      <div class="global-search__body">
        <div v-if="loading" class="global-search__state">正在搜索知识对象…</div>
        <div v-else-if="errorMessage" class="global-search__state global-search__state--error" role="alert">
          <strong>搜索失败</strong>
          <span>{{ errorMessage }}</span>
          <button type="button" @click="void runSearch()">重试</button>
        </div>

        <template v-else-if="query.trim().length === 0">
          <section v-if="recentQueries.length" class="global-search__section" aria-labelledby="recent-searches-title">
            <header>
              <h3 id="recent-searches-title">最近搜索</h3>
              <button type="button" @click="clearQueries">清除记录</button>
            </header>
            <button
              v-for="(item, index) in recentQueries"
              :key="item"
              type="button"
              class="global-search__recent-row"
              :class="{ 'is-active': activeIndex === index }"
              @mouseenter="activeIndex = index"
              @click="useRecentQuery(item)"
            >
              <el-icon><Clock /></el-icon><strong class="technical-text">{{ item }}</strong><span>重新搜索</span><span>↵</span>
            </button>
          </section>

          <section v-if="recentVisits.length" class="global-search__section" aria-labelledby="recent-visits-title">
            <header><h3 id="recent-visits-title">最近访问</h3></header>
            <button
              v-for="(item, index) in recentVisits"
              :key="`${item.navigation.routeObjectType}-${item.navigation.routeObjectId}-${item.navigation.drawerObjectId ?? ''}`"
              type="button"
              class="global-search__recent-row"
              :class="{ 'is-active': activeIndex === recentQueries.length + index }"
              @mouseenter="activeIndex = recentQueries.length + index"
              @click="void openRecentVisit(item)"
            >
              <el-icon><Clock /></el-icon><strong class="technical-text">{{ item.title }}</strong><span>{{ item.systemContext }} · {{ item.objectType }}</span><ArrowRight />
            </button>
          </section>

          <p v-if="!recentQueries.length && !recentVisits.length" class="global-search__hint">可搜索技术标识或业务描述，例如 <code>STATE_FLAG</code>、<code>MES.TABLE_EQP</code>。</p>
        </template>

        <template v-else-if="hasResults && result">
          <p class="global-search__summary">在所有知识对象中找到 {{ totalLabel }} 个结果</p>
          <section v-for="group in result.groups" :key="group.objectType" class="global-search__section global-search__group">
            <header><h3>{{ group.label }} <small>{{ group.items.length }}</small></h3></header>
            <button
              v-for="item in group.items"
              :key="`${group.objectType}-${item.id}`"
              type="button"
              class="global-search__result-row"
              :class="{ 'is-active': activeIndex === selectableIndex(item) }"
              @mouseenter="activeIndex = selectableIndex(item)"
              @click="selectResult(item, group.objectType)"
            >
              <span class="global-search__type">{{ resultTypeLabel(item, group.label) }}</span>
              <strong class="technical-text">{{ item.title }}</strong>
              <span class="global-search__context">{{ item.systemContext }}</span>
              <span class="global-search__description">{{ item.shortDescription }}</span>
              <KnowledgeStatusBadge v-if="item.knowledgeStatus" :status="item.knowledgeStatus" />
              <span v-else-if="item.unknownItemStatus" class="global-search__unknown-status">{{ unknownItemStatusLabels[item.unknownItemStatus] }}</span>
              <span v-if="item.lifecycleStatus" class="global-search__lifecycle">{{ lifecycleLabels[item.lifecycleStatus] }}</span>
              <el-icon><ArrowRight /></el-icon>
            </button>
          </section>
        </template>

        <template v-else-if="isNoResult">
          <section class="global-search__empty">
            <h3>未找到匹配的知识对象</h3>
            <p>没有找到与 <strong class="technical-text">{{ query.trim() }}</strong> 匹配的系统、业务功能、数据库对象、知识内容或待确认事项。</p>
            <ul>
              <li>检查技术标识的拼写</li>
              <li>尝试搜索更短的名称，例如 <code>STATE_FLAG</code></li>
              <li>尝试使用业务描述，例如“设备状态”</li>
            </ul>
            <div><button type="button" @click="useRecentQuery('STATE_FLAG')">搜索 STATE_FLAG</button><button type="button" @click="clearSearch">清除搜索</button></div>
          </section>
          <section v-if="recentQueries.length" class="global-search__section">
            <header><h3>最近搜索</h3></header>
            <button v-for="item in recentQueries" :key="item" type="button" class="global-search__recent-row" @click="useRecentQuery(item)"><el-icon><Clock /></el-icon><strong class="technical-text">{{ item }}</strong></button>
          </section>
        </template>
      </div>

      <footer class="global-search__footer"><kbd>↑ ↓</kbd><span>选择</span><kbd>Enter</kbd><span>{{ query.trim().length === 0 ? '搜索或打开' : '打开' }}</span><kbd>Esc</kbd><span>关闭</span></footer>
    </section>
  </Teleport>
</template>

<style src="../search.css"></style>
