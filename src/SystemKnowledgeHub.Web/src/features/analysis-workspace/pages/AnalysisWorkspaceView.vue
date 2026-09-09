<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, watch } from 'vue'
import { onBeforeRouteLeave, onBeforeRouteUpdate, useRoute, useRouter } from 'vue-router'
import { ElMessage, ElMessageBox, ElTree } from 'element-plus'
import { Document, Folder } from '@element-plus/icons-vue'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { ApiError } from '../../../api/errors/ApiError'
import KnowledgeDocumentDetailPanel from '../../knowledge-documents/components/KnowledgeDocumentDetailPanel.vue'
import ExistingDocumentPicker from '../components/ExistingDocumentPicker.vue'
import {
  documentTypeLabels,
  lifecycleLabels,
  type KnowledgeDocumentDetail,
} from '../../knowledge-documents/api/knowledgeDocumentContracts'
import * as api from '../api/analysisWorkspaceApi'
import type {
  AnalysisNode,
  AnalysisTree,
  AnalysisMutation,
} from '../api/analysisWorkspaceContracts'
import '../styles/analysisWorkspace.css'

const route = useRoute()
const router = useRouter()
const actor = useActorStore()
const overlays = useOverlayStore()
const tree = ref<AnalysisTree | null>(null)
const loading = ref(false)
const treeError = ref('')
const message = ref('')
const busy = ref(false)
const panel = ref<InstanceType<typeof KnowledgeDocumentDetailPanel> | null>(null)
const treeControl = ref<InstanceType<typeof ElTree> | null>(null)
const selectedId = computed(() => {
  const id = Number(route.params.nodeId)
  return Number.isSafeInteger(id) && id > 0 ? id : null
})
const selected = computed(
  () => tree.value?.items.find((item) => item.id === selectedId.value) ?? null,
)
// Retain the mounted document across organization refreshes, including a missing placement.
// Only committed navigation or a canonical unavailable event replaces its content.
const activeDocumentId = ref<number | null>(null)
const activeUnavailable = ref(false)
const autoEditNodeId = ref<number | null>(null)
const writable = computed(
  () => actor.canEdit && tree.value !== null && !treeError.value && !loading.value && !busy.value,
)
const children = (parentId: number | null): AnalysisNode[] =>
  [...(tree.value?.items ?? [])]
    .filter((item) => item.parentId === parentId)
    .sort((a, b) => a.sortOrder - b.sortOrder)
interface TreeItem extends AnalysisNode {
  children: TreeItem[]
}
const filter = ref('')
const filterActive = computed(() => filter.value.trim().length > 0)
const visibleIds = computed(() => {
  const items = tree.value?.items ?? []
  const query = filter.value.trim().toLowerCase()
  const byId = new Map(items.map((item) => [item.id, item]))
  const visible = new Set<number>()
  for (const item of items) {
    const safeTitle = item.availability === 'Unavailable' ? '文档不可用' : item.title
    if (query && !safeTitle.toLowerCase().includes(query)) continue
    let current: AnalysisNode | undefined = item
    while (current && !visible.has(current.id)) {
      visible.add(current.id)
      current = current.parentId === null ? undefined : byId.get(current.parentId)
    }
  }
  return visible
})
const treeItems = computed(() => {
  const entries = new Map<number, TreeItem>(
    (tree.value?.items ?? [])
      .filter((item) => visibleIds.value.has(item.id))
      .map((item) => [
        item.id,
        {
          ...item,
          title: item.availability === 'Unavailable' ? '文档不可用' : item.title,
          children: [],
        },
      ]),
  )
  const roots: TreeItem[] = []
  for (const item of entries.values()) {
    if (item.parentId === null) roots.push(item)
    else entries.get(item.parentId)?.children.push(item)
  }
  for (const item of entries.values()) item.children.sort((a, b) => a.sortOrder - b.sortOrder)
  return roots.sort((a, b) => a.sortOrder - b.sortOrder)
})
const siblings = computed(() => (selected.value ? children(selected.value.parentId) : []))
const siblingIndex = computed(() =>
  siblings.value.findIndex((item) => item.id === selectedId.value),
)
let disposed = false
let loadSequence = 0

function syncDocument(): void {
  const node = selected.value
  activeUnavailable.value = node?.availability === 'Unavailable'
  activeDocumentId.value =
    node?.nodeType === 'Document' && !activeUnavailable.value ? node.knowledgeDocumentId : null
}
function adopt(response: AnalysisTree): void {
  tree.value = response
  treeError.value = ''
  if (activeDocumentId.value === null) syncDocument()
  else if (
    selected.value?.knowledgeDocumentId === activeDocumentId.value &&
    selected.value.availability === 'Unavailable'
  )
    activeUnavailable.value = true
}
async function loadTree(): Promise<void> {
  const sequence = ++loadSequence
  loading.value = true
  try {
    const response = await api.getAnalysisTree()
    if (!disposed && sequence === loadSequence) adopt(response)
  } catch (error: unknown) {
    if (!disposed && sequence === loadSequence)
      treeError.value = error instanceof Error ? error.message : '无法读取分析目录。'
  } finally {
    if (sequence === loadSequence) loading.value = false
  }
}
function target(nodeId: number | null) {
  return nodeId === null
    ? { name: 'analysis-workspace' }
    : { name: 'analysis-workspace-node', params: { nodeId: String(nodeId) } }
}
async function selectNode(node: AnalysisNode): Promise<void> {
  await router.push(target(node.id))
  if (!disposed) treeControl.value?.setCurrentKey(selectedId.value ?? undefined)
}
async function requestLeave(): Promise<boolean> {
  if (busy.value) return false
  return (await panel.value?.requestLeave()) ?? true
}
onBeforeRouteLeave(requestLeave)
onBeforeRouteUpdate((to, from) =>
  to.params.nodeId !== from.params.nodeId || to.query.view !== from.query.view
    ? requestLeave()
    : true,
)
watch(selectedId, (_value, previous) => {
  if (previous === autoEditNodeId.value) autoEditNodeId.value = null
  syncDocument()
  message.value = ''
})
// ElTree's internal highlight changes before navigation guards finish. Controlled custom
// selection and resetting currentKey keep a cancelled navigation on the original node.
watch(
  [selectedId, treeItems],
  () => treeControl.value?.setCurrentKey(selectedId.value ?? undefined),
  { flush: 'post' },
)

function documentUpdated(document: KnowledgeDocumentDetail): void {
  if (!tree.value) return
  tree.value = {
    ...tree.value,
    items: tree.value.items.map((item) =>
      item.knowledgeDocumentId === document.id && item.availability === 'Available'
        ? {
            ...item,
            title: document.title,
            documentType: document.documentType,
            lifecycleStatus: document.lifecycleStatus,
          }
        : item,
    ),
  }
}
function documentUnavailable(documentId: number): void {
  if (!tree.value) return
  tree.value = {
    ...tree.value,
    items: tree.value.items.map((item) =>
      item.knowledgeDocumentId === documentId
        ? {
            ...item,
            title: '文档不可用',
            availability: 'Unavailable',
            documentType: null,
            lifecycleStatus: null,
          }
        : item,
    ),
  }
  if (activeDocumentId.value === documentId) {
    activeUnavailable.value = true
    activeDocumentId.value = null
  }
}
type DialogAction = 'folder' | 'document' | 'rename' | 'move' | 'existing'
const action = ref<DialogAction>('folder')
const dialogNode = ref<AnalysisNode | null>(null)
const title = ref('')
const parent = ref<number>(0)
const documentType = ref<'DesignNote' | 'KnowledgeArticle'>('DesignNote')
const formError = ref('')
const dialogOpen = computed(() => overlays.currentDialog?.kind === 'analysis-organization')
const dialogTitle = computed(
  () =>
    ({
      folder: '新建目录',
      document: '新建分析文档',
      rename: '重命名目录',
      move: '移动节点',
      existing: '加入已有文档',
    })[action.value],
)
function path(nodeId: number | null): string {
  const names: string[] = []
  let current = tree.value?.items.find((item) => item.id === nodeId)
  while (current) {
    names.unshift(current.title)
    current = tree.value?.items.find((item) => item.id === current?.parentId)
  }
  return ['分析文档', ...names].join(' / ')
}
const parentOptions = computed(() =>
  (tree.value?.items ?? []).filter((item) => {
    if (item.nodeType !== 'Folder') return false
    if (action.value !== 'move' || !dialogNode.value) return true
    let current: AnalysisNode | undefined = item
    while (current) {
      if (current.id === dialogNode.value.id) return false
      const ancestor: number | null = current.parentId
      current = tree.value?.items.find((node) => node.id === ancestor)
    }
    return true
  }),
)
function openDialog(nextAction: DialogAction): void {
  if (!writable.value) return
  action.value = nextAction
  dialogNode.value = selected.value
  title.value = nextAction === 'rename' ? (selected.value?.title ?? '') : ''
  documentType.value = 'DesignNote'
  parent.value =
    (nextAction === 'move'
      ? selected.value?.parentId
      : selected.value?.nodeType === 'Folder'
        ? selected.value.id
        : selected.value?.parentId) ?? 0
  formError.value = ''
  overlays.openDialog({ kind: 'analysis-organization', id: selectedId.value, mode: 'edit' })
}
function tokens(node: AnalysisNode) {
  return {
    nodeConcurrencyToken: node.concurrencyToken,
    treeConcurrencyToken: tree.value!.treeConcurrencyToken,
  }
}
async function mutate(
  operation: () => Promise<AnalysisMutation>,
): Promise<AnalysisMutation | null> {
  if (!writable.value) return null
  busy.value = true
  message.value = ''
  try {
    const response = await operation()
    if (disposed) return null
    adopt(response)
    return response
  } catch (error: unknown) {
    if (disposed) return null
    if (error instanceof ApiError && error.status === 409) {
      await loadTree()
      message.value = treeError.value
        ? '目录已变化，但最新目录加载失败。请重试刷新。'
        : '分析目录已被其他操作修改，已刷新最新目录。'
    } else {
      message.value = error instanceof Error ? error.message : '分析目录操作失败。'
      // Unknown transport results may have committed. Refresh, never replay a create.
      if (!(error instanceof ApiError)) await loadTree()
    }
    return null
  } finally {
    busy.value = false
  }
}
async function submitDialog(): Promise<void> {
  if (action.value === 'existing') return
  if (!writable.value || !tree.value) return
  const titleLimit = action.value === 'document' ? 300 : 200
  if (action.value !== 'move' && (!title.value.trim() || title.value.trim().length > titleLimit)) {
    formError.value = `名称须为 1～${titleLimit} 个字符。`
    return
  }
  if ((action.value === 'folder' || action.value === 'document') && !(await requestLeave())) return
  const treeConcurrencyToken = tree.value.treeConcurrencyToken
  const parentId = parent.value === 0 ? null : parent.value
  const node = dialogNode.value && tree.value.items.find((item) => item.id === dialogNode.value?.id)
  const requestedAction = action.value
  const response = await mutate(() => {
    if (requestedAction === 'folder')
      return api.createAnalysisFolder({ parentId, title: title.value.trim(), treeConcurrencyToken })
    if (requestedAction === 'document')
      return api.createAnalysisDocument({
        parentId,
        title: title.value.trim(),
        documentType: documentType.value,
        treeConcurrencyToken,
      })
    if (!node) throw new Error('此节点已不存在，请刷新目录。')
    if (requestedAction === 'rename')
      return api.renameAnalysisFolder(node.id, { ...tokens(node), title: title.value.trim() })
    return api.moveAnalysisNode(node.id, {
      ...tokens(node),
      targetParentId: parentId,
      targetPosition: children(parentId).filter((item) => item.id !== node.id).length,
    })
  })
  if (!response) {
    formError.value = message.value
    return
  }
  overlays.closeDialog()
  if ((requestedAction === 'folder' || requestedAction === 'document') && response.node) {
    panel.value?.finishEdit()
    autoEditNodeId.value = requestedAction === 'document' ? response.node.id : null
    await router.push(target(response.node.id))
  }
  ElMessage.success('分析目录已更新。')
}
async function reorder(direction: -1 | 1): Promise<void> {
  if (filterActive.value || !writable.value || !selected.value || !tree.value) return
  const ordered = [...siblings.value]
  const from = siblingIndex.value
  const to = from + direction
  if (from < 0 || to < 0 || to >= ordered.length) return
  const node = ordered.splice(from, 1)[0]!
  ordered.splice(to, 0, node)
  await mutate(() =>
    api.reorderAnalysisChildren({
      parentId: node.parentId,
      treeConcurrencyToken: tree.value!.treeConcurrencyToken,
      items: ordered.map((item) => ({ id: item.id, nodeConcurrencyToken: item.concurrencyToken })),
    }),
  )
}
async function removeSelected(): Promise<void> {
  const node = selected.value
  if (!writable.value || !node || (node.nodeType === 'Folder' && children(node.id).length > 0))
    return
  if (!(await requestLeave())) return
  try {
    await ElMessageBox.confirm(
      node.nodeType === 'Document'
        ? '确认从分析目录移除？知识文档本身不会被删除。'
        : '确认删除此空目录？',
      node.nodeType === 'Document' ? '从分析目录移除' : '删除空目录',
      { type: 'warning', confirmButtonText: '确认', cancelButtonText: '取消' },
    )
  } catch {
    return
  }
  const response = await mutate(() =>
    node.nodeType === 'Document'
      ? api.removeAnalysisPlacement(node.id, tokens(node))
      : api.deleteAnalysisFolder(node.id, tokens(node)),
  )
  if (!response) return
  panel.value?.finishEdit()
  await router.push(target(node.parentId))
}
async function addExisting(knowledgeDocumentId: number): Promise<void> {
  if (
    !writable.value ||
    !tree.value ||
    tree.value.items.some((item) => item.knowledgeDocumentId === knowledgeDocumentId)
  )
    return
  if (!(await requestLeave())) return
  const response = await mutate(() =>
    api.addAnalysisPlacement({
      knowledgeDocumentId,
      parentId: parent.value || null,
      treeConcurrencyToken: tree.value!.treeConcurrencyToken,
    }),
  )
  if (!response?.node) {
    formError.value = message.value
    return
  }
  overlays.closeDialog()
  panel.value?.finishEdit()
  autoEditNodeId.value = null
  await router.push(target(response.node.id))
}
async function locateExisting(node: AnalysisNode): Promise<void> {
  await selectNode(node)
  if (selectedId.value === node.id) {
    overlays.closeDialog()
    filter.value = ''
  }
}
const canHandoff = computed(
  () =>
    actor.isAdministrator &&
    selected.value?.nodeType === 'Document' &&
    selected.value.availability === 'Available' &&
    selected.value.lifecycleStatus === 'Published' &&
    !activeUnavailable.value,
)
async function handoff(): Promise<void> {
  if (!canHandoff.value || busy.value) return
  await router.push({
    path: '/portal-management',
    query: {
      targetType: 'KnowledgeDocument',
      targetId: String(selected.value!.knowledgeDocumentId),
    },
  })
}
onMounted(() => {
  void loadTree()
})
onBeforeUnmount(() => {
  disposed = true
  loadSequence += 1
  if (dialogOpen.value) overlays.closeDialog()
})
</script>

<template>
  <div class="analysis-workspace">
    <aside class="analysis-tree" aria-label="分析目录">
      <header>
        <button class="analysis-root" @click="router.push(target(null))">分析文档</button
        ><el-button :disabled="loading || busy" @click="loadTree">刷新</el-button>
      </header>
      <div v-if="actor.canEdit" class="analysis-actions">
        <el-button :disabled="!writable" @click="openDialog('folder')">新建目录</el-button>
        <el-button type="primary" :disabled="!writable" @click="openDialog('document')"
          >新建分析文档</el-button
        >
        <el-button :disabled="!writable" @click="openDialog('existing')">加入已有文档</el-button>
      </div>
      <el-input
        v-model="filter"
        clearable
        aria-label="筛选目录和文档标题"
        placeholder="筛选目录和文档标题"
      />
      <p v-if="filterActive && tree && !treeError && treeItems.length === 0">
        未找到匹配的目录或文档
      </p>
      <p v-if="filterActive && selected && !visibleIds.has(selected.id)">当前选中项已被筛选隐藏</p>
      <p v-if="loading" role="status">正在读取分析目录…</p>
      <p v-if="treeError" class="analysis-error" role="alert">{{ treeError }}</p>
      <p v-else-if="tree && tree.items.length === 0">暂无分析内容</p>
      <ElTree
        v-if="tree && tree.items.length > 0 && !treeError"
        ref="treeControl"
        :data="treeItems"
        node-key="id"
        :current-node-key="selectedId ?? undefined"
        :expand-on-click-node="false"
        default-expand-all
        @node-click="selectNode"
      >
        <template #default="{ data: node }">
          <span
            class="analysis-tree-row"
            :class="{ 'is-selected': node.id === selectedId }"
            :title="node.title"
          >
            <el-icon><Folder v-if="node.nodeType === 'Folder'" /><Document v-else /></el-icon>
            <span class="analysis-tree-row__text"
              ><span>{{ node.title }}</span
              ><small v-if="node.nodeType === 'Document' && node.availability === 'Available'"
                >{{ documentTypeLabels[node.documentType as keyof typeof documentTypeLabels] }} ·
                {{ lifecycleLabels[node.lifecycleStatus as keyof typeof lifecycleLabels] }}</small
              ></span
            >
          </span>
        </template>
      </ElTree>
    </aside>
    <main class="analysis-content">
      <p v-if="message" class="analysis-notice" role="status">{{ message }}</p>
      <div
        v-if="selected && actor.canEdit"
        class="analysis-actions analysis-organization"
        aria-label="目录组织操作"
      >
        <el-button
          v-if="selected.nodeType === 'Folder'"
          :disabled="!writable"
          @click="openDialog('rename')"
          >重命名目录</el-button
        >
        <el-button :disabled="!writable" @click="openDialog('move')">移动</el-button>
        <el-button
          v-if="selected.availability === 'Available'"
          :disabled="filterActive || !writable || siblingIndex <= 0"
          @click="reorder(-1)"
          >上移</el-button
        >
        <el-button
          v-if="selected.availability === 'Available'"
          :disabled="filterActive || !writable || siblingIndex >= siblings.length - 1"
          @click="reorder(1)"
          >下移</el-button
        >
        <el-button
          type="danger"
          plain
          :disabled="
            !writable || (selected.nodeType === 'Folder' && children(selected.id).length > 0)
          "
          @click="removeSelected"
          >{{ selected.nodeType === 'Folder' ? '删除空目录' : '从分析目录移除' }}</el-button
        >
        <el-button v-if="canHandoff" :disabled="busy" @click="handoff"
          >在知识门户管理中使用</el-button
        >
      </div>
      <p v-if="filterActive && actor.canEdit">清除筛选后可调整顺序</p>
      <p
        v-if="
          actor.isAdministrator &&
          selected?.availability === 'Available' &&
          selected.lifecycleStatus === 'Draft'
        "
      >
        请先发布知识文档。
      </p>
      <section v-if="activeUnavailable" class="analysis-welcome">
        <h1>文档不可用</h1>
        <p>该知识文档当前已不可读取，你仍可以移动或从分析目录移除此位置。</p>
      </section>
      <KnowledgeDocumentDetailPanel
        v-if="activeDocumentId !== null"
        v-show="!activeUnavailable"
        ref="panel"
        :document-id="activeDocumentId"
        :unavailable="activeUnavailable"
        embedded
        :auto-edit="autoEditNodeId === selectedId"
        @updated="documentUpdated"
        @unavailable="documentUnavailable"
      />
      <section
        v-else-if="!activeUnavailable && selected?.nodeType === 'Folder'"
        class="analysis-welcome"
      >
        <h1>{{ selected.title }}</h1>
        <p>包含 {{ children(selected.id).length }} 个直接子项。</p>
        <p v-if="children(selected.id).length">仅空目录可以删除。</p>
      </section>
      <section
        v-else-if="!activeUnavailable && selectedId !== null && tree && !loading"
        class="analysis-welcome"
      >
        <h1>节点不存在</h1>
        <p>此位置已被移除，请从左侧重新选择。</p>
      </section>
      <section v-else-if="!activeUnavailable" class="analysis-welcome">
        <h1>分析文档</h1>
        <p>从左侧目录选择一个文档开始查看，或新建目录和分析文档。</p>
      </section>
    </main>
    <Teleport v-if="dialogOpen" defer to="#dialog-feature-content">
      <section class="analysis-dialog" :aria-label="dialogTitle">
        <h2>{{ dialogTitle }}</h2>
        <el-form label-position="top" @submit.prevent="submitDialog">
          <el-form-item
            v-if="action !== 'move' && action !== 'existing'"
            :label="action === 'document' ? '标题' : '目录名称'"
            required
            ><el-input
              v-model="title"
              :disabled="busy"
              :maxlength="action === 'document' ? 300 : 200"
          /></el-form-item>
          <el-form-item v-if="action === 'document'" label="文档类型"
            ><el-select v-model="documentType" :disabled="busy"
              ><el-option label="设计说明" value="DesignNote" /><el-option
                label="知识文章"
                value="KnowledgeArticle" /></el-select
          ></el-form-item>
          <el-form-item v-if="action !== 'rename'" label="所在位置"
            ><el-select v-model="parent" :disabled="busy"
              ><el-option label="分析文档（根目录）" :value="0" /><el-option
                v-for="folder in parentOptions"
                :key="folder.id"
                :label="path(folder.id)"
                :value="folder.id" /></el-select
          ></el-form-item>
          <p v-else>所在位置：{{ path(dialogNode?.parentId ?? null) }}</p>
          <ExistingDocumentPicker
            v-if="action === 'existing'"
            :nodes="tree?.items ?? []"
            :disabled="!writable"
            @select="addExisting"
            @locate="locateExisting"
          />
          <p v-if="formError" class="analysis-error" role="alert">{{ formError }}</p>
          <footer>
            <el-button :disabled="busy" @click="overlays.closeDialog">取消</el-button
            ><el-button
              v-if="action !== 'existing'"
              type="primary"
              :disabled="!writable"
              :loading="busy"
              @click="submitDialog"
              >确认</el-button
            >
          </footer>
        </el-form>
      </section>
    </Teleport>
  </div>
</template>
