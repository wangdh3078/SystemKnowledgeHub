<script setup lang="ts">
import { ArrowLeft, ArrowRight, Close, Menu } from '@element-plus/icons-vue'
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { RouterLink, useRoute } from 'vue-router'
import { parseSafeApiId } from '../api/contracts/id'
import PortalTreeNavigation from '../features/portal-reading/components/PortalTreeNavigation.vue'
import { portalReadApi } from '../features/portal-reading/api/portalReadApi'
import type { PortalTreeNode } from '../features/portal-reading/api/portalReadContracts'
import '../features/portal-reading/portal-reading.css'

const route = useRoute()
const treeItems = ref<readonly PortalTreeNode[]>([])
const treeLoading = ref(true)
const treeFailed = ref(false)
const treeCollapsed = ref(false)
const narrowTreeOpen = ref(false)
const expandedNodeIds = ref<ReadonlySet<number>>(new Set())
let treeRequest: AbortController | null = null

const activePageId = computed(() =>
  route.name === 'portal-page' ? parseSafeApiId(route.params.id) : null,
)

function expandActivePath(): void {
  if (activePageId.value === null) return
  const byId = new Map(treeItems.value.map((item) => [item.nodeId, item]))
  const placement = treeItems.value.find(
    (item) => item.nodeKind === 'Page' && item.pageId === activePageId.value,
  )
  if (!placement) return
  const expanded = new Set(expandedNodeIds.value)
  let parentId = placement.parentNodeId
  while (parentId !== null) {
    expanded.add(parentId)
    parentId = byId.get(parentId)?.parentNodeId ?? null
  }
  expandedNodeIds.value = expanded
}

function toggleNode(nodeId: number): void {
  const next = new Set(expandedNodeIds.value)
  if (next.has(nodeId)) next.delete(nodeId)
  else next.add(nodeId)
  expandedNodeIds.value = next
}

async function loadTree(): Promise<void> {
  treeRequest?.abort()
  treeRequest = new AbortController()
  treeLoading.value = true
  treeFailed.value = false
  try {
    const response = await portalReadApi.getTree(treeRequest.signal)
    treeItems.value = response.items
    if (expandedNodeIds.value.size === 0) {
      expandedNodeIds.value = new Set(
        response.items
          .filter((item) => item.parentNodeId === null && item.nodeKind === 'Folder')
          .map((item) => item.nodeId),
      )
    }
    expandActivePath()
  } catch (error: unknown) {
    if (error instanceof DOMException && error.name === 'AbortError') return
    treeFailed.value = true
  } finally {
    treeLoading.value = false
  }
}

function closeNarrowTree(): void {
  narrowTreeOpen.value = false
}

function handleKeydown(event: KeyboardEvent): void {
  if (event.key === 'Escape' && narrowTreeOpen.value) closeNarrowTree()
}

watch(activePageId, expandActivePath)
onMounted(() => {
  document.addEventListener('keydown', handleKeydown)
  void loadTree()
})
onBeforeUnmount(() => {
  treeRequest?.abort()
  document.removeEventListener('keydown', handleKeydown)
})
</script>

<template>
  <div class="portal-layout">
    <header class="portal-header">
      <RouterLink class="portal-header__brand" :to="{ name: 'portal-home' }"
        >系统知识中心</RouterLink
      >
    </header>
    <div class="portal-layout__body" :class="{ 'is-tree-collapsed': treeCollapsed }">
      <aside class="portal-sidebar" aria-label="知识目录侧栏">
        <div class="portal-sidebar__heading">
          <strong v-if="!treeCollapsed">知识目录</strong>
          <button
            type="button"
            class="portal-icon-button"
            :aria-label="treeCollapsed ? '展开知识目录' : '折叠知识目录'"
            @click="treeCollapsed = !treeCollapsed"
          >
            <el-icon><ArrowRight v-if="treeCollapsed" /><ArrowLeft v-else /></el-icon>
          </button>
        </div>
        <div v-if="!treeCollapsed" class="portal-sidebar__content">
          <p v-if="treeLoading" class="portal-tree-feedback">正在加载知识目录…</p>
          <div v-else-if="treeFailed" class="portal-tree-feedback">
            <p>知识目录暂时无法加载。</p>
            <button type="button" @click="loadTree">重试</button>
          </div>
          <PortalTreeNavigation
            v-else
            :items="treeItems"
            :expanded-node-ids="expandedNodeIds"
            :active-page-id="activePageId"
            @toggle="toggleNode"
          />
        </div>
      </aside>

      <button
        type="button"
        class="portal-directory-trigger"
        aria-label="打开知识目录"
        @click="narrowTreeOpen = true"
      >
        <el-icon><Menu /></el-icon><span>目录</span>
      </button>

      <div v-if="narrowTreeOpen" class="portal-directory-overlay" @click.self="closeNarrowTree">
        <aside class="portal-directory-panel" role="dialog" aria-modal="true" aria-label="知识目录">
          <div class="portal-sidebar__heading">
            <strong>知识目录</strong>
            <button
              type="button"
              class="portal-icon-button"
              aria-label="关闭知识目录"
              @click="closeNarrowTree"
            >
              <el-icon><Close /></el-icon>
            </button>
          </div>
          <p v-if="treeLoading" class="portal-tree-feedback">正在加载知识目录…</p>
          <div v-else-if="treeFailed" class="portal-tree-feedback">
            <p>知识目录暂时无法加载。</p>
            <button type="button" @click="loadTree">重试</button>
          </div>
          <PortalTreeNavigation
            v-else
            :items="treeItems"
            :expanded-node-ids="expandedNodeIds"
            :active-page-id="activePageId"
            @toggle="toggleNode"
            @navigate="closeNarrowTree"
          />
        </aside>
      </div>

      <main id="portal-main" class="portal-main">
        <slot />
      </main>
    </div>
  </div>
</template>
