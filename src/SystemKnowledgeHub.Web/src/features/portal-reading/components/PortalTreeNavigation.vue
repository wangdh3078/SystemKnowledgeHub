<script setup lang="ts">
import { computed } from 'vue'
import PortalTreeBranch, { type PortalDisplayNode } from './PortalTreeBranch.vue'
import type { PortalTreeNode } from '../api/portalReadContracts'

const props = defineProps<{
  items: readonly PortalTreeNode[]
  expandedNodeIds: ReadonlySet<number>
  activePageId: number | null
}>()
const emit = defineEmits<{
  toggle: [nodeId: number]
  navigate: []
}>()

const tree = computed<readonly PortalDisplayNode[]>(() => {
  const children = new Map<number | null, PortalTreeNode[]>()
  for (const item of props.items) {
    const siblings = children.get(item.parentNodeId) ?? []
    siblings.push(item)
    children.set(item.parentNodeId, siblings)
  }
  const build = (parentId: number | null): PortalDisplayNode[] =>
    (children.get(parentId) ?? []).map((item) => ({
      ...item,
      children: build(item.nodeId),
    }))
  return build(null)
})
</script>

<template>
  <nav class="portal-tree-navigation" aria-label="知识目录">
    <p v-if="tree.length === 0" class="portal-tree-empty">暂无已发布知识</p>
    <PortalTreeBranch
      v-else
      :nodes="tree"
      :expanded-node-ids="expandedNodeIds"
      :active-page-id="activePageId"
      @toggle="emit('toggle', $event)"
      @navigate="emit('navigate')"
    />
  </nav>
</template>
