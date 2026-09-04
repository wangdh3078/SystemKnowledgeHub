<script setup lang="ts">
import { ArrowRight, Document, Folder, FolderOpened } from '@element-plus/icons-vue'
import { RouterLink } from 'vue-router'
import type { PortalTreeNode } from '../api/portalReadContracts'

defineOptions({ name: 'PortalTreeBranch' })

export interface PortalDisplayNode extends PortalTreeNode {
  readonly children: readonly PortalDisplayNode[]
}

const props = defineProps<{
  nodes: readonly PortalDisplayNode[]
  expandedNodeIds: ReadonlySet<number>
  activePageId: number | null
}>()
const emit = defineEmits<{
  toggle: [nodeId: number]
  navigate: []
}>()

function isExpanded(nodeId: number): boolean {
  return props.expandedNodeIds.has(nodeId)
}
</script>

<template>
  <ul class="portal-tree-list">
    <li v-for="node in nodes" :key="node.nodeId" class="portal-tree-item">
      <button
        v-if="node.nodeKind === 'Folder'"
        type="button"
        class="portal-tree-entry portal-tree-entry--folder"
        :aria-expanded="isExpanded(node.nodeId)"
        @click="emit('toggle', node.nodeId)"
      >
        <el-icon class="portal-tree-chevron" :class="{ 'is-expanded': isExpanded(node.nodeId) }">
          <ArrowRight />
        </el-icon>
        <el-icon><FolderOpened v-if="isExpanded(node.nodeId)" /><Folder v-else /></el-icon>
        <span>{{ node.title }}</span>
      </button>
      <RouterLink
        v-else
        class="portal-tree-entry portal-tree-entry--page"
        :class="{ 'is-active': node.pageId === activePageId }"
        :to="{ name: 'portal-page', params: { id: node.pageId } }"
        :aria-current="node.pageId === activePageId ? 'page' : undefined"
        @click="emit('navigate')"
      >
        <span class="portal-tree-chevron" aria-hidden="true"></span>
        <el-icon><Document /></el-icon>
        <span>{{ node.title }}</span>
      </RouterLink>
      <PortalTreeBranch
        v-if="node.children.length > 0 && isExpanded(node.nodeId)"
        :nodes="node.children"
        :expanded-node-ids="expandedNodeIds"
        :active-page-id="activePageId"
        @toggle="emit('toggle', $event)"
        @navigate="emit('navigate')"
      />
    </li>
  </ul>
</template>
