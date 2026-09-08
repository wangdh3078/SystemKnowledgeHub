<script setup lang="ts">
import { computed, onBeforeUnmount, ref, watch } from 'vue'
import { getKnowledgeDocuments } from '../../knowledge-documents/api/knowledgeDocumentsApi'
import {
  documentTypes,
  documentTypeLabels,
  lifecycleLabels,
  documentLifecycleStatuses,
  type DocumentType,
  type DocumentLifecycleStatus,
  type KnowledgeDocumentsListResponse,
} from '../../knowledge-documents/api/knowledgeDocumentContracts'
import { knowledgeStatusLabels } from '../../../api/contracts/knowledge'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
import type { AnalysisNode } from '../api/analysisWorkspaceContracts'

const props = defineProps<{ nodes: readonly AnalysisNode[]; disabled: boolean }>()
const emit = defineEmits<{ select: [id: number]; locate: [node: AnalysisNode] }>()
const query = ref('')
const documentType = ref<DocumentType | ''>('')
const lifecycleStatus = ref<DocumentLifecycleStatus | ''>('')
const page = ref(1)
const pageSize = ref(20)
const result = ref<KnowledgeDocumentsListResponse | null>(null)
const loading = ref(false)
const error = ref('')
const placements = computed(
  () =>
    new Map(
      props.nodes
        .filter((node) => node.nodeType === 'Document')
        .map((node) => [node.knowledgeDocumentId, node]),
    ),
)
let controller: AbortController | undefined
let sequence = 0
async function load(): Promise<void> {
  controller?.abort()
  controller = new AbortController()
  const current = ++sequence
  loading.value = true
  error.value = ''
  result.value = null
  try {
    const response = await getKnowledgeDocuments(
      {
        query: query.value.trim() || undefined,
        documentType: documentType.value || undefined,
        lifecycleStatus: lifecycleStatus.value || undefined,
        page: page.value,
        pageSize: pageSize.value,
      },
      controller.signal,
    )
    if (current === sequence) result.value = response
  } catch (reason) {
    if (current === sequence)
      error.value = reason instanceof Error ? reason.message : '无法读取已有文档。'
  } finally {
    if (current === sequence) loading.value = false
  }
}
function search(): void {
  if (page.value !== 1) page.value = 1
  else void load()
}
watch([documentType, lifecycleStatus, pageSize], search)
watch(page, () => void load(), { immediate: true })
onBeforeUnmount(() => {
  sequence++
  controller?.abort()
})
</script>

<template>
  <div class="analysis-candidates">
    <el-input
      v-model="query"
      aria-label="搜索已有文档"
      placeholder="搜索已有文档"
      :disabled="disabled"
      @keyup.enter.prevent="search"
    />
    <el-button :disabled="disabled" @click="search">搜索</el-button>
    <el-select
      v-model="documentType"
      aria-label="候选文档类型"
      placeholder="全部类型"
      :disabled="disabled"
    >
      <el-option label="全部类型" value="" />
      <el-option
        v-for="type in documentTypes"
        :key="type"
        :label="documentTypeLabels[type]"
        :value="type"
      />
    </el-select>
    <el-select
      v-model="lifecycleStatus"
      aria-label="候选生命周期"
      placeholder="未归档（草稿及已发布）"
      :disabled="disabled"
    >
      <el-option label="未归档（草稿及已发布）" value="" />
      <el-option
        v-for="status in documentLifecycleStatuses"
        :key="status"
        :label="lifecycleLabels[status]"
        :value="status"
      />
    </el-select>
    <p v-if="loading" role="status">正在读取已有文档…</p>
    <p v-if="error" role="alert">{{ error }}</p>
    <table v-if="result" aria-label="已有文档候选">
      <thead>
        <tr>
          <th>标题</th>
          <th>文档类型</th>
          <th>生命周期</th>
          <th>知识状态</th>
          <th>操作</th>
        </tr>
      </thead>
      <tbody>
        <tr v-for="item in result.items" :key="item.id">
          <td>{{ item.title }}</td>
          <td>{{ documentTypeLabels[item.documentType] }}</td>
          <td>{{ lifecycleLabels[item.lifecycleStatus] }}</td>
          <td>{{ knowledgeStatusLabels[item.knowledgeStatus] }}</td>
          <td>
            <template v-if="placements.has(item.id)"
              ><span>已在分析目录中</span
              ><el-button
                :disabled="disabled"
                size="small"
                @click="emit('locate', placements.get(item.id)!)"
                >定位</el-button
              ></template
            >
            <el-button v-else :disabled="disabled" size="small" @click="emit('select', item.id)"
              >加入</el-button
            >
          </td>
        </tr>
      </tbody>
    </table>
    <p v-if="result?.items.length === 0">未找到符合条件的文档</p>
    <SkhPagination
      v-if="result"
      v-model:current-page="page"
      v-model:page-size="pageSize"
      :total="result.total"
    />
  </div>
</template>
