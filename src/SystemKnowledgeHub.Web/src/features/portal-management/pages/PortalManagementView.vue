<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref } from 'vue'
import { onBeforeRouteLeave } from 'vue-router'
import {
  ArrowDown,
  DocumentAdd,
  FolderAdd,
  MoreFilled,
  Plus,
  Refresh,
  Search,
} from '@element-plus/icons-vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
import {
  createPortalNode,
  createPortalPage,
  deletePortalNode,
  deletePortalPage,
  getPortalPage,
  getPortalPages,
  getPortalPreview,
  getPortalTree,
  publishPortalNode,
  publishPortalPage,
  reorderPortalNodes,
  unpublishPortalNode,
  unpublishPortalPage,
  updatePortalNode,
  updatePortalPage,
} from '../api/portalManagementApi'
import type {
  PortalNodeKind,
  PortalPageDetail,
  PortalPageListResponse,
  PortalPersistedProjectionKind,
  PortalPreview,
  PortalProjectionKind,
  PortalSourceKind,
  PortalTargetSummary,
  PortalTargetType,
  PortalTreeNode,
  PortalTreeResponse,
} from '../api/portalManagementContracts'
import PortalPreviewDialog from '../components/PortalPreviewDialog.vue'
import PortalTargetPickerDialog from '../components/PortalTargetPickerDialog.vue'
import '../portal-management.css'

interface TreeItem extends PortalTreeNode {
  children: TreeItem[]
}
interface EditableSection {
  id: number | null
  heading: string
  sourceKind: PortalSourceKind
  referenceTarget: PortalTargetSummary | null
  projectionKind: PortalPersistedProjectionKind
  sortOrder: number
  isHealthy: boolean
  healthMessage: string
}
type PickerPurpose = 'new-page' | 'primary-target' | 'section-reference'
type PickerHost = 'new-page' | 'section' | null

const targetLabels: Readonly<Record<PortalTargetType, string>> = {
  System: '系统',
  BusinessFunction: '业务功能',
  DatabaseObject: '数据库对象',
  KnowledgeDocument: '知识文档',
  Integration: '集成',
}
const projectionLabels: Readonly<Record<PortalProjectionKind, string>> = {
  Summary: '摘要',
  KnowledgeDocumentBody: '知识文档正文',
  StructuredOverview: '结构化概览',
  DatabaseStructure: '数据库结构',
}
const sourceLabels: Readonly<Record<PortalSourceKind, string>> = {
  PrimaryTarget: '主知识对象',
  ExplicitReference: '已有知识引用',
}
const allTargetTypes: readonly PortalTargetType[] = [
  'System',
  'BusinessFunction',
  'DatabaseObject',
  'KnowledgeDocument',
  'Integration',
]

const tree = ref<PortalTreeResponse | null>(null)
const pages = ref<PortalPageListResponse | null>(null)
const selectedNodeId = ref<number | null>(null)
const selectedPage = ref<PortalPageDetail | null>(null)
const loading = ref(true)
const saving = ref(false)
const error = ref<string | null>(null)
const pageSearch = ref('')
const pageNumber = ref(1)
const pageSize = ref(20)
const dirty = ref(false)
const editorTitle = ref('')
const editorPrimary = ref<PortalTargetSummary | null>(null)
const editorSections = ref<EditableSection[]>([])
const previewOpen = ref(false)
const previewLoading = ref(false)
const preview = ref<PortalPreview | null>(null)
const pickerOpen = ref(false)
const pickerPurpose = ref<PickerPurpose>('new-page')
const pickerTypes = ref<readonly PortalTargetType[]>(allTargetTypes)
const pickerHost = ref<PickerHost>(null)
const folderDialogOpen = ref(false)
const pageDialogOpen = ref(false)
const sectionDialogOpen = ref(false)
const nodeDialogOpen = ref(false)
const newFolder = reactive({ title: '', parentId: null as number | null })
const newPage = reactive({
  title: '',
  target: null as PortalTargetSummary | null,
  parentId: null as number | null,
})
const sectionDraft = reactive<EditableSection>({
  id: null,
  heading: '',
  sourceKind: 'PrimaryTarget',
  referenceTarget: null,
  projectionKind: 'Summary',
  sortOrder: 0,
  isHealthy: true,
  healthMessage: '正常',
})
const nodeDraft = reactive({
  id: 0,
  title: '',
  nodeKind: 'Folder' as PortalNodeKind,
  parentId: null as number | null,
  portalPageId: null as number | null,
  sortOrder: 0,
  concurrencyToken: '',
})

const treeItems = computed<TreeItem[]>(() => {
  const source = tree.value?.items ?? []
  const map = new Map<number, TreeItem>()
  source.forEach((item) => map.set(item.nodeId, { ...item, children: [] }))
  const roots: TreeItem[] = []
  source.forEach((item) => {
    const node = map.get(item.nodeId)!
    const parent = item.parentNodeId === null ? null : map.get(item.parentNodeId)
    if (parent) parent.children.push(node)
    else roots.push(node)
  })
  return roots
})
const selectedNode = computed(
  () => tree.value?.items.find((item) => item.nodeId === selectedNodeId.value) ?? null,
)
const folderOptions = computed(() =>
  (tree.value?.items ?? []).filter(
    (item) => item.nodeKind === 'Folder' && item.nodeId !== nodeDraft.id,
  ),
)
const pickerTitle = computed(() =>
  pickerPurpose.value === 'new-page'
    ? '选择主知识对象'
    : pickerPurpose.value === 'primary-target'
      ? '更换主知识对象'
      : '选择章节知识',
)
const canEdit = computed(() => selectedPage.value !== null && !selectedPage.value.isPublished)
const selectedFolderForPlacement = computed(() =>
  selectedNode.value?.nodeKind === 'Folder'
    ? selectedNode.value.nodeId
    : (selectedNode.value?.parentNodeId ?? null),
)

async function loadAll(selectPageId?: number): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const [treeResult, pageResult] = await Promise.all([
      getPortalTree(),
      getPortalPages({
        page: pageNumber.value,
        pageSize: pageSize.value,
        search: pageSearch.value.trim(),
      }),
    ])
    tree.value = treeResult
    pages.value = pageResult
    if (selectPageId) await selectPage(selectPageId)
  } catch (reason: unknown) {
    error.value = message(reason, '无法读取知识门户配置。')
  } finally {
    loading.value = false
  }
}

async function loadPages(): Promise<void> {
  try {
    pages.value = await getPortalPages({
      page: pageNumber.value,
      pageSize: pageSize.value,
      search: pageSearch.value.trim(),
    })
  } catch (reason: unknown) {
    ElMessage.error(message(reason, '无法读取页面库。'))
  }
}

function hydrateEditor(page: PortalPageDetail): void {
  selectedPage.value = page
  editorTitle.value = page.title
  editorPrimary.value = page.primaryTarget
  editorSections.value = page.sections.map((item) => ({ ...item }))
  dirty.value = false
}

async function confirmDiscard(): Promise<boolean> {
  if (!dirty.value) return true
  try {
    await ElMessageBox.confirm('尚有未保存的页面编排修改，确认放弃？', '放弃编辑', {
      confirmButtonText: '放弃修改',
      cancelButtonText: '继续编辑',
      type: 'warning',
    })
    return true
  } catch {
    return false
  }
}

async function selectPage(pageId: number): Promise<void> {
  if (selectedPage.value?.id !== pageId && !(await confirmDiscard())) return
  try {
    hydrateEditor(await getPortalPage(pageId))
  } catch (reason: unknown) {
    ElMessage.error(message(reason, '无法读取页面详情。'))
  }
}

async function selectTreeNode(item: TreeItem): Promise<void> {
  if (item.nodeId !== selectedNodeId.value && !(await confirmDiscard())) return
  selectedNodeId.value = item.nodeId
  if (item.pageId) await selectPage(item.pageId)
}

function markDirty(): void {
  if (canEdit.value) dirty.value = true
}

function openPicker(purpose: PickerPurpose, types: readonly PortalTargetType[]): void {
  pickerPurpose.value = purpose
  pickerTypes.value = types
  pickerHost.value =
    purpose === 'new-page' ? 'new-page' : purpose === 'section-reference' ? 'section' : null
  if (pickerHost.value === 'new-page') pageDialogOpen.value = false
  if (pickerHost.value === 'section') sectionDialogOpen.value = false
  pickerOpen.value = true
}

function handleTargetSelected(target: PortalTargetSummary): void {
  if (pickerPurpose.value === 'new-page') newPage.target = target
  else if (pickerPurpose.value === 'primary-target') {
    editorPrimary.value = target
    markDirty()
  } else sectionDraft.referenceTarget = target
}

function restorePickerHost(): void {
  if (pickerHost.value === 'new-page') pageDialogOpen.value = true
  if (pickerHost.value === 'section') sectionDialogOpen.value = true
  pickerHost.value = null
}

function openNewFolder(): void {
  newFolder.title = ''
  newFolder.parentId = selectedFolderForPlacement.value
  folderDialogOpen.value = true
}

async function createFolder(): Promise<void> {
  if (!newFolder.title.trim()) return void ElMessage.warning('请输入目录名称。')
  const siblings = (tree.value?.items ?? []).filter(
    (item) => item.parentNodeId === newFolder.parentId,
  )
  try {
    await createPortalNode({
      title: newFolder.title.trim(),
      nodeKind: 'Folder',
      parentId: newFolder.parentId,
      portalPageId: null,
      sortOrder: siblings.length,
    })
    folderDialogOpen.value = false
    await loadAll()
    ElMessage.success('目录已创建。')
  } catch (reason: unknown) {
    ElMessage.error(message(reason, '目录创建失败。'))
  }
}

function openNewPage(): void {
  newPage.title = ''
  newPage.target = null
  newPage.parentId = selectedFolderForPlacement.value
  pageDialogOpen.value = true
}

async function createPage(): Promise<void> {
  if (!newPage.title.trim() || !newPage.target)
    return void ElMessage.warning('请输入页面标题并选择主知识对象。')
  saving.value = true
  try {
    const page = await createPortalPage({
      title: newPage.title.trim(),
      primaryTarget: { type: newPage.target.type, id: newPage.target.id },
    })
    const siblings = (tree.value?.items ?? []).filter(
      (item) => item.parentNodeId === newPage.parentId,
    )
    const node = await createPortalNode({
      title: newPage.title.trim(),
      nodeKind: 'Page',
      parentId: newPage.parentId,
      portalPageId: page.id,
      sortOrder: siblings.length,
    })
    pageDialogOpen.value = false
    selectedNodeId.value = node.nodeId
    await loadAll(page.id)
    ElMessage.success('页面及导航位置已创建。')
  } catch (reason: unknown) {
    ElMessage.error(message(reason, '页面创建失败。'))
  } finally {
    saving.value = false
  }
}

function allowedTypesForProjection(projection: PortalProjectionKind): readonly PortalTargetType[] {
  if (projection === 'KnowledgeDocumentBody') return ['KnowledgeDocument']
  if (projection === 'DatabaseStructure') return ['DatabaseObject']
  if (projection === 'StructuredOverview')
    return ['System', 'BusinessFunction', 'DatabaseObject', 'Integration']
  return allTargetTypes
}

function projectionLabel(projection: PortalPersistedProjectionKind): string {
  return projection in projectionLabels
    ? projectionLabels[projection as PortalProjectionKind]
    : '当前版本暂不支持'
}

function projectionCompatible(
  projection: PortalPersistedProjectionKind,
  targetType: PortalTargetType | undefined,
): projection is PortalProjectionKind {
  return (
    targetType !== undefined &&
    projection in projectionLabels &&
    allowedTypesForProjection(projection as PortalProjectionKind).includes(targetType)
  )
}

function openSection(section?: EditableSection): void {
  Object.assign(
    sectionDraft,
    section ?? {
      id: null,
      heading: '',
      sourceKind: 'PrimaryTarget',
      referenceTarget: null,
      projectionKind: 'Summary',
      sortOrder: editorSections.value.length,
      isHealthy: true,
      healthMessage: '正常',
    },
  )
  sectionDialogOpen.value = true
}

function saveSectionDraft(): void {
  if (!sectionDraft.heading.trim()) return void ElMessage.warning('请输入章节标题。')
  const targetType =
    sectionDraft.sourceKind === 'PrimaryTarget'
      ? editorPrimary.value?.type
      : sectionDraft.referenceTarget?.type
  if (!projectionCompatible(sectionDraft.projectionKind, targetType))
    return void ElMessage.warning('章节类型与知识对象不兼容。')
  if (sectionDraft.sourceKind === 'ExplicitReference' && !sectionDraft.referenceTarget)
    return void ElMessage.warning('请选择已有知识。')
  const value = {
    ...sectionDraft,
    heading: sectionDraft.heading.trim(),
    referenceTarget:
      sectionDraft.sourceKind === 'ExplicitReference' ? sectionDraft.referenceTarget : null,
  }
  const index =
    sectionDraft.id === null
      ? -1
      : editorSections.value.findIndex((item) => item.id === sectionDraft.id)
  if (index >= 0) editorSections.value[index] = value
  else editorSections.value.push({ ...value, id: null, sortOrder: editorSections.value.length })
  editorSections.value.forEach((item, order) => {
    item.sortOrder = order
  })
  sectionDialogOpen.value = false
  markDirty()
}

function removeSection(index: number): void {
  editorSections.value.splice(index, 1)
  editorSections.value.forEach((item, order) => {
    item.sortOrder = order
  })
  markDirty()
}

function moveSection(index: number, direction: -1 | 1): void {
  const destination = index + direction
  if (destination < 0 || destination >= editorSections.value.length) return
  const [item] = editorSections.value.splice(index, 1)
  editorSections.value.splice(destination, 0, item!)
  editorSections.value.forEach((section, order) => {
    section.sortOrder = order
  })
  markDirty()
}

async function saveComposition(): Promise<void> {
  const page = selectedPage.value
  const primary = editorPrimary.value
  if (!page || !primary || !editorTitle.value.trim())
    return void ElMessage.warning('页面标题和主知识对象不能为空。')
  saving.value = true
  try {
    const updated = await updatePortalPage(page.id, {
      title: editorTitle.value.trim(),
      primaryTarget: { type: primary.type, id: primary.id },
      sections: editorSections.value.map((section, index) => ({
        id: section.id,
        heading: section.heading,
        sourceKind: section.sourceKind,
        referenceTarget:
          section.sourceKind === 'ExplicitReference' && section.referenceTarget
            ? { type: section.referenceTarget.type, id: section.referenceTarget.id }
            : null,
        projectionKind: section.projectionKind as PortalProjectionKind,
        sortOrder: index,
      })),
      concurrencyToken: page.concurrencyToken,
    })
    hydrateEditor(updated)
    await loadAll(updated.id)
    ElMessage.success('页面编排已保存。')
  } catch (reason: unknown) {
    if (reason instanceof ApiError && reason.response.code === 'conflict')
      ElMessage.error('页面已被其他操作修改，请重新加载后再继续。')
    else ElMessage.error(message(reason, '页面保存失败，编辑内容已保留。'))
  } finally {
    saving.value = false
  }
}

async function showPreview(): Promise<void> {
  if (!selectedPage.value) return
  previewOpen.value = true
  previewLoading.value = true
  try {
    preview.value = await getPortalPreview(selectedPage.value.id)
  } catch (reason: unknown) {
    ElMessage.error(message(reason, '无法生成预览。'))
  } finally {
    previewLoading.value = false
  }
}

async function publishPage(): Promise<void> {
  if (!selectedPage.value || dirty.value)
    return void ElMessage.warning(dirty.value ? '请先保存页面编排。' : '未选择页面。')
  try {
    const readiness = await getPortalPreview(selectedPage.value.id)
    preview.value = readiness
    if (!readiness.readiness.canPublish) {
      previewOpen.value = true
      return
    }
    hydrateEditor(
      await publishPortalPage(selectedPage.value.id, selectedPage.value.concurrencyToken),
    )
    await loadAll(selectedPage.value.id)
    ElMessage.success('页面内容已发布；导航位置需单独发布。')
  } catch (reason: unknown) {
    ElMessage.error(message(reason, '页面发布失败。'))
  }
}

async function unpublishPage(): Promise<void> {
  if (!selectedPage.value) return
  try {
    await ElMessageBox.confirm(
      '取消发布后，该页面将立即无法在知识门户中查看，但不会删除页面配置或知识内容。',
      '取消发布页面',
      { type: 'warning', confirmButtonText: '取消发布', cancelButtonText: '保留发布' },
    )
    hydrateEditor(
      await unpublishPortalPage(selectedPage.value.id, selectedPage.value.concurrencyToken),
    )
    await loadAll(selectedPage.value.id)
    ElMessage.success('页面已取消发布。')
  } catch (reason: unknown) {
    if (reason instanceof Error) ElMessage.error(message(reason, '取消发布失败。'))
  }
}

async function removePage(): Promise<void> {
  if (!selectedPage.value) return
  try {
    await ElMessageBox.confirm('仅删除门户页面配置，不会删除任何知识对象。', '删除门户页面', {
      type: 'warning',
      confirmButtonText: '删除',
      cancelButtonText: '取消',
    })
    await deletePortalPage(selectedPage.value.id, selectedPage.value.concurrencyToken)
    selectedPage.value = null
    dirty.value = false
    await loadAll()
    ElMessage.success('门户页面已删除。')
  } catch (reason: unknown) {
    if (reason instanceof Error) ElMessage.error(message(reason, '页面删除失败。'))
  }
}

function openNodeEdit(node: PortalTreeNode): void {
  Object.assign(nodeDraft, {
    id: node.nodeId,
    title: node.title,
    nodeKind: node.nodeKind,
    parentId: node.parentNodeId,
    portalPageId: node.pageId,
    sortOrder: siblingNodes(node).findIndex((item) => item.nodeId === node.nodeId),
    concurrencyToken: node.concurrencyToken,
  })
  nodeDialogOpen.value = true
}

async function saveNode(): Promise<void> {
  if (!nodeDraft.title.trim()) return void ElMessage.warning('请输入节点名称。')
  const parentChanged =
    selectedNode.value?.nodeId === nodeDraft.id &&
    selectedNode.value.parentNodeId !== nodeDraft.parentId
  if (parentChanged)
    nodeDraft.sortOrder = (tree.value?.items ?? []).filter(
      (item) => item.parentNodeId === nodeDraft.parentId,
    ).length
  try {
    await updatePortalNode(nodeDraft.id, { ...nodeDraft, title: nodeDraft.title.trim() })
    nodeDialogOpen.value = false
    await loadAll(selectedPage.value?.id)
    ElMessage.success('节点已更新。')
  } catch (reason: unknown) {
    ElMessage.error(message(reason, '节点更新失败。'))
  }
}

function siblingNodes(node: PortalTreeNode): PortalTreeNode[] {
  return (tree.value?.items ?? []).filter((item) => item.parentNodeId === node.parentNodeId)
}

async function moveNodeOrder(node: PortalTreeNode, direction: -1 | 1): Promise<void> {
  const siblings = siblingNodes(node)
  const index = siblings.findIndex((item) => item.nodeId === node.nodeId)
  const destination = index + direction
  if (destination < 0 || destination >= siblings.length) return
  ;[siblings[index], siblings[destination]] = [siblings[destination]!, siblings[index]!]
  try {
    tree.value = await reorderPortalNodes(node.parentNodeId, siblings)
    ElMessage.success('节点顺序已更新。')
  } catch (reason: unknown) {
    ElMessage.error(message(reason, '节点排序失败。'))
  }
}

async function nodeCommand(command: string, node: PortalTreeNode): Promise<void> {
  try {
    if (command === 'edit') return openNodeEdit(node)
    if (command === 'up') return void moveNodeOrder(node, -1)
    if (command === 'down') return void moveNodeOrder(node, 1)
    if (command === 'publish') await publishPortalNode(node.nodeId, node.concurrencyToken)
    if (command === 'unpublish') await unpublishPortalNode(node.nodeId, node.concurrencyToken)
    if (command === 'delete') {
      await ElMessageBox.confirm('移除导航节点不会删除门户页面或知识内容。', '移除节点', {
        type: 'warning',
        confirmButtonText: '移除',
        cancelButtonText: '取消',
      })
      await deletePortalNode(node.nodeId, node.concurrencyToken)
    }
    await loadAll(selectedPage.value?.id)
  } catch (reason: unknown) {
    if (reason instanceof Error) ElMessage.error(message(reason, '节点操作失败。'))
  }
}

function handleNodeCommand(command: string | number | object, node: PortalTreeNode): void {
  void nodeCommand(String(command), node)
}

function beforeUnload(event: BeforeUnloadEvent): void {
  if (!dirty.value) return
  event.preventDefault()
  event.returnValue = ''
}

function message(reason: unknown, fallback: string): string {
  return reason instanceof Error ? reason.message : fallback
}

onBeforeRouteLeave(async () => await confirmDiscard())
onMounted(() => {
  window.addEventListener('beforeunload', beforeUnload)
  void loadAll()
})
onBeforeUnmount(() => window.removeEventListener('beforeunload', beforeUnload))
</script>

<template>
  <main class="portal-management skh-page">
    <header class="portal-management__header skh-page-header">
      <div>
        <nav>管理 / 知识门户管理</nav>
        <h1>知识门户管理</h1>
        <p>通过页面树、主知识对象和有序章节，把现有知识编排为完整阅读体系；编排不复制知识事实。</p>
      </div>
      <div class="portal-management__header-actions">
        <el-button :icon="Refresh" :loading="loading" @click="loadAll(selectedPage?.id)"
          >刷新</el-button
        >
        <el-button :icon="FolderAdd" @click="openNewFolder">新建目录</el-button>
        <el-button type="primary" :icon="DocumentAdd" @click="openNewPage">新建页面</el-button>
      </div>
    </header>

    <p v-if="error" class="portal-inline-error" role="alert">{{ error }}</p>
    <section v-loading="loading" class="portal-workbench" aria-label="知识门户编排工作台">
      <aside class="portal-tree-panel">
        <header>
          <div>
            <strong>Portal 页面树</strong><small>{{ tree?.total ?? 0 }} 个节点</small>
          </div>
        </header>
        <el-tree
          v-if="treeItems.length"
          :data="treeItems"
          node-key="nodeId"
          default-expand-all
          highlight-current
          :expand-on-click-node="false"
          @node-click="selectTreeNode"
        >
          <template #default="{ data }">
            <div
              class="portal-tree-node"
              :class="{ 'portal-tree-node--broken': !data.health.isHealthy }"
            >
              <span class="portal-tree-node__title">{{ data.title }}</span>
              <span
                class="portal-state"
                :class="data.isPublished ? 'portal-state--published' : ''"
                >{{ data.isPublished ? '已发布' : '未发布' }}</span
              >
              <el-dropdown trigger="click" @command="handleNodeCommand($event, data)">
                <el-button text :icon="MoreFilled" aria-label="节点更多操作" @click.stop />
                <template #dropdown
                  ><el-dropdown-menu>
                    <el-dropdown-item command="edit" :disabled="data.isPublished"
                      >重命名 / 移动</el-dropdown-item
                    >
                    <el-dropdown-item command="up" :disabled="data.isPublished"
                      >上移</el-dropdown-item
                    >
                    <el-dropdown-item command="down" :disabled="data.isPublished"
                      >下移</el-dropdown-item
                    >
                    <el-dropdown-item :command="data.isPublished ? 'unpublish' : 'publish'">{{
                      data.isPublished ? '取消发布' : '发布'
                    }}</el-dropdown-item>
                    <el-dropdown-item command="delete" divided :disabled="data.isPublished"
                      >移除</el-dropdown-item
                    >
                  </el-dropdown-menu></template
                >
              </el-dropdown>
            </div>
          </template>
        </el-tree>
        <div v-else class="portal-tree-empty">
          <p>尚未建立 Portal 页面树</p>
          <el-button type="primary" link @click="openNewFolder">先创建目录</el-button>
        </div>
        <section class="portal-page-library" aria-label="门户页面库">
          <header>
            <strong>页面库</strong><span>{{ pages?.total ?? 0 }}</span>
          </header>
          <el-input
            v-model="pageSearch"
            clearable
            :prefix-icon="Search"
            placeholder="搜索页面或主知识"
            @keyup.enter="
              () => {
                pageNumber = 1
                void loadPages()
              }
            "
            @clear="
              () => {
                pageNumber = 1
                void loadPages()
              }
            "
          />
          <button
            v-for="page in pages?.items ?? []"
            :key="page.id"
            type="button"
            class="portal-page-library__item"
            @click="selectPage(page.id)"
          >
            <span>{{ page.title }}</span
            ><small
              >{{ targetLabels[page.primaryTarget.type] }} · {{ page.primaryTarget.title }}</small
            >
          </button>
          <SkhPagination
            :total="pages?.total ?? 0"
            :current-page="pageNumber"
            :page-size="pageSize"
            aria-label="门户页面分页"
            @current-change="
              (value) => {
                pageNumber = value
                void loadPages()
              }
            "
            @size-change="
              (value) => {
                pageSize = value
                pageNumber = 1
                void loadPages()
              }
            "
          />
        </section>
      </aside>

      <section class="portal-composer">
        <div v-if="!selectedPage" class="portal-composer-empty">
          <DocumentAdd />
          <h2>选择或新建 Portal 页面</h2>
          <p>页面以一个主知识对象为主题，再按阅读顺序加入现有知识章节。</p>
        </div>
        <template v-else>
          <header class="portal-composer__titlebar">
            <div>
              <div class="portal-composer__status">
                <span
                  class="portal-state"
                  :class="selectedPage.isPublished ? 'portal-state--published' : ''"
                  >页面{{ selectedPage.publicationLabel }}</span
                ><span v-if="dirty" class="portal-dirty">未保存</span>
              </div>
              <h2>{{ selectedPage.title }}</h2>
            </div>
            <div>
              <el-button @click="showPreview">预览</el-button
              ><el-button v-if="selectedPage.isPublished" @click="unpublishPage">取消发布</el-button
              ><el-button v-else type="primary" @click="publishPage">发布页面</el-button
              ><el-dropdown
                ><el-button :icon="MoreFilled"
                  ><el-icon class="el-icon--right"><ArrowDown /></el-icon></el-button
                ><template #dropdown
                  ><el-dropdown-menu
                    ><el-dropdown-item
                      :disabled="selectedPage.isPublished || selectedPage.placements.length > 0"
                      @click="removePage"
                      >删除页面</el-dropdown-item
                    ></el-dropdown-menu
                  ></template
                ></el-dropdown
              >
            </div>
          </header>

          <section class="portal-composer__metadata">
            <label
              >页面标题<el-input
                v-model="editorTitle"
                maxlength="200"
                :disabled="!canEdit"
                @input="markDirty"
            /></label>
            <div class="portal-primary-target">
              <span>主知识对象</span
              ><strong>{{ targetLabels[editorPrimary!.type] }} · {{ editorPrimary!.title }}</strong
              ><small>{{ editorPrimary!.context || '当前 canonical 知识' }}</small
              ><el-button
                v-if="canEdit"
                type="primary"
                link
                @click="openPicker('primary-target', allTargetTypes)"
                >更换</el-button
              >
            </div>
          </section>

          <section
            class="portal-readiness"
            :class="selectedPage.referenceHealth.isHealthy ? '' : 'portal-readiness--warning'"
          >
            <div>
              <strong>发布检查</strong>
              <p>{{ selectedPage.referenceHealth.message }}</p>
            </div>
            <div>
              <span>页面状态：{{ selectedPage.publicationLabel }}</span
              ><span
                >导航位置：{{
                  selectedPage.placements.filter((item) => item.isEffectivelyPublished).length
                }}
                个已发布 / {{ selectedPage.placements.length }} 个位置</span
              >
            </div>
          </section>

          <section class="portal-sections">
            <header>
              <div>
                <h3>章节</h3>
                <p>按阅读顺序组合现有知识，不复制正文或业务事实。</p>
              </div>
              <el-button :icon="Plus" :disabled="!canEdit" @click="openSection()"
                >添加章节</el-button
              >
            </header>
            <div v-if="editorSections.length" class="portal-section-list">
              <article
                v-for="(section, index) in editorSections"
                :key="section.id ?? `new-${index}`"
                class="portal-section-row"
                :class="{ 'portal-section-row--broken': !section.isHealthy }"
              >
                <span class="portal-section-row__handle" aria-hidden="true">≡</span>
                <div>
                  <strong>{{ section.heading }}</strong
                  ><small
                    >{{ sourceLabels[section.sourceKind] }} ·
                    {{ section.referenceTarget?.title ?? editorPrimary?.title }} ·
                    {{ projectionLabel(section.projectionKind) }}</small
                  ><em v-if="!section.isHealthy">需要处理：{{ section.healthMessage }}</em>
                </div>
                <div class="portal-section-row__actions">
                  <el-button
                    text
                    :disabled="!canEdit || index === 0"
                    @click="moveSection(index, -1)"
                    >上移</el-button
                  ><el-button
                    text
                    :disabled="!canEdit || index === editorSections.length - 1"
                    @click="moveSection(index, 1)"
                    >下移</el-button
                  ><el-button text :disabled="!canEdit" @click="openSection(section)"
                    >编辑</el-button
                  ><el-button text type="danger" :disabled="!canEdit" @click="removeSection(index)"
                    >删除</el-button
                  >
                </div>
              </article>
            </div>
            <p v-else class="portal-sections__empty">尚未添加章节。</p>
          </section>

          <section class="portal-placements">
            <header>
              <h3>Portal 位置</h3>
              <span>页面发布与导航发布是两个独立状态。</span>
            </header>
            <p v-if="!selectedPage.placements.length">尚未放入页面树。</p>
            <ul v-else>
              <li v-for="placement in selectedPage.placements" :key="placement.nodeId">
                <span>{{ placement.path }}</span
                ><span>{{
                  placement.isEffectivelyPublished
                    ? 'Portal 可见'
                    : placement.isPublished
                      ? '上级目录未发布'
                      : '节点未发布'
                }}</span>
              </li>
            </ul>
          </section>

          <footer class="portal-composer__footer">
            <span>{{ dirty ? '有尚未保存的编排修改' : '所有修改已保存' }}</span
            ><el-button
              type="primary"
              :loading="saving"
              :disabled="!canEdit || !dirty"
              @click="saveComposition"
              >保存编排</el-button
            >
          </footer>
        </template>
      </section>
    </section>

    <el-dialog v-model="folderDialogOpen" title="新建目录" width="460px" append-to-body
      ><el-form label-position="top"
        ><el-form-item label="名称"
          ><el-input v-model="newFolder.title" maxlength="200" /></el-form-item
        ><el-form-item label="上级目录"
          ><el-select v-model="newFolder.parentId" clearable placeholder="根级"
            ><el-option
              v-for="folder in folderOptions"
              :key="folder.nodeId"
              :label="folder.title"
              :value="folder.nodeId" /></el-select></el-form-item></el-form
      ><template #footer
        ><el-button @click="folderDialogOpen = false">取消</el-button
        ><el-button type="primary" @click="createFolder">创建</el-button></template
      ></el-dialog
    >

    <el-dialog v-model="pageDialogOpen" title="新建 Portal 页面" width="560px" append-to-body
      ><el-form label-position="top"
        ><el-form-item label="页面标题"
          ><el-input v-model="newPage.title" maxlength="200" /></el-form-item
        ><el-form-item label="主知识对象"
          ><div class="portal-dialog-target">
            <span v-if="newPage.target"
              ><strong>{{ targetLabels[newPage.target.type] }} · {{ newPage.target.title }}</strong
              ><small>{{ newPage.target.context }}</small></span
            ><span v-else>尚未选择</span
            ><el-button @click="openPicker('new-page', allTargetTypes)">选择已有知识</el-button>
          </div></el-form-item
        ><el-form-item label="页面树位置"
          ><el-select v-model="newPage.parentId" clearable placeholder="根级"
            ><el-option
              v-for="folder in folderOptions"
              :key="folder.nodeId"
              :label="folder.title"
              :value="folder.nodeId" /></el-select></el-form-item></el-form
      ><template #footer
        ><el-button @click="pageDialogOpen = false">取消</el-button
        ><el-button type="primary" :loading="saving" @click="createPage"
          >创建并进入编排</el-button
        ></template
      ></el-dialog
    >

    <el-dialog
      v-model="sectionDialogOpen"
      :title="sectionDraft.id ? '编辑章节' : '添加章节'"
      width="620px"
      append-to-body
      ><el-form label-position="top"
        ><el-form-item label="章节标题"
          ><el-input v-model="sectionDraft.heading" maxlength="200" /></el-form-item
        ><el-form-item label="展示内容"
          ><el-select
            v-model="sectionDraft.projectionKind"
            @change="sectionDraft.referenceTarget = null"
            ><el-option
              v-for="(label, value) in projectionLabels"
              :key="value"
              :label="label"
              :value="value" /></el-select></el-form-item
        ><el-form-item label="内容来源"
          ><el-radio-group
            v-model="sectionDraft.sourceKind"
            @change="sectionDraft.referenceTarget = null"
            ><el-radio value="PrimaryTarget">主知识对象</el-radio
            ><el-radio value="ExplicitReference">选择已有知识</el-radio></el-radio-group
          ></el-form-item
        ><el-form-item v-if="sectionDraft.sourceKind === 'PrimaryTarget'" label="使用对象"
          ><p>
            {{
              editorPrimary
                ? `${targetLabels[editorPrimary.type]} · ${editorPrimary.title}`
                : '未选择主知识对象'
            }}
          </p></el-form-item
        ><el-form-item v-else label="引用知识"
          ><div class="portal-dialog-target">
            <span>{{
              sectionDraft.referenceTarget
                ? `${targetLabels[sectionDraft.referenceTarget.type]} · ${sectionDraft.referenceTarget.title}`
                : '尚未选择'
            }}</span
            ><el-button
              :disabled="!(sectionDraft.projectionKind in projectionLabels)"
              @click="
                openPicker(
                  'section-reference',
                  allowedTypesForProjection(sectionDraft.projectionKind as PortalProjectionKind),
                )
              "
              >选择已有知识</el-button
            >
          </div></el-form-item
        ></el-form
      ><template #footer
        ><el-button @click="sectionDialogOpen = false">取消</el-button
        ><el-button type="primary" @click="saveSectionDraft">保存章节</el-button></template
      ></el-dialog
    >

    <el-dialog v-model="nodeDialogOpen" title="重命名 / 移动节点" width="500px" append-to-body
      ><el-form label-position="top"
        ><el-form-item label="名称"
          ><el-input v-model="nodeDraft.title" maxlength="200" /></el-form-item
        ><el-form-item label="上级目录"
          ><el-select v-model="nodeDraft.parentId" clearable placeholder="根级"
            ><el-option
              v-for="folder in folderOptions"
              :key="folder.nodeId"
              :label="folder.title"
              :value="folder.nodeId" /></el-select></el-form-item></el-form
      ><template #footer
        ><el-button @click="nodeDialogOpen = false">取消</el-button
        ><el-button type="primary" @click="saveNode">保存</el-button></template
      ></el-dialog
    >

    <PortalTargetPickerDialog
      v-model="pickerOpen"
      :allowed-types="pickerTypes"
      :title="pickerTitle"
      @select="handleTargetSelected"
      @closed="restorePickerHost"
    />
    <PortalPreviewDialog v-model="previewOpen" :preview="preview" :loading="previewLoading" />
  </main>
</template>
