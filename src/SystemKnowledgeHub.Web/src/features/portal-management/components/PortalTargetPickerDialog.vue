<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Search } from '@element-plus/icons-vue'
import SkhPagination from '../../../components/data-display/SkhPagination.vue'
import { getPortalTargets } from '../api/portalManagementApi'
import type { PortalTargetSummary, PortalTargetType } from '../api/portalManagementContracts'

const props = defineProps<{
  modelValue: boolean
  allowedTypes: readonly PortalTargetType[]
  title?: string
}>()
const emit = defineEmits<{
  'update:modelValue': [value: boolean]
  select: [target: PortalTargetSummary]
  closed: []
}>()

const labels: Readonly<Record<PortalTargetType, string>> = {
  System: '系统',
  BusinessFunction: '业务功能',
  DatabaseObject: '数据库对象',
  KnowledgeDocument: '知识文档',
  Integration: '集成',
}
const type = ref<PortalTargetType>('System')
const search = ref('')
const page = ref(1)
const pageSize = ref(20)
const items = ref<readonly PortalTargetSummary[]>([])
const total = ref(0)
const loading = ref(false)
const error = ref<string | null>(null)
const dialogTitle = computed(() => props.title ?? '选择已有知识')

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try {
    const result = await getPortalTargets({
      type: type.value,
      search: search.value.trim(),
      page: page.value,
      pageSize: pageSize.value,
    })
    items.value = result.items
    total.value = result.total
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : '无法读取知识对象。'
  } finally {
    loading.value = false
  }
}

function select(target: PortalTargetSummary): void {
  emit('select', target)
  emit('update:modelValue', false)
}

function resetAndLoad(): void {
  page.value = 1
  void load()
}

watch(
  () => props.modelValue,
  (open) => {
    if (!open) return
    type.value = props.allowedTypes.includes(type.value) ? type.value : props.allowedTypes[0]!
    search.value = ''
    page.value = 1
    void load()
  },
)
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    :title="dialogTitle"
    width="min(920px, 90vw)"
    append-to-body
    destroy-on-close
    class="portal-target-picker"
    @close="emit('update:modelValue', false)"
    @closed="emit('closed')"
  >
    <div class="portal-target-picker__filters">
      <el-select v-model="type" aria-label="知识类型" @change="resetAndLoad">
        <el-option
          v-for="value in allowedTypes"
          :key="value"
          :value="value"
          :label="labels[value]"
        />
      </el-select>
      <el-input
        v-model="search"
        clearable
        :prefix-icon="Search"
        placeholder="搜索名称或业务标识"
        aria-label="搜索已有知识"
        @keyup.enter="resetAndLoad"
        @clear="resetAndLoad"
      />
      <el-button :loading="loading" @click="resetAndLoad">搜索</el-button>
    </div>
    <p v-if="error" class="portal-inline-error" role="alert">{{ error }}</p>
    <el-table v-loading="loading" :data="items" empty-text="未找到符合条件的知识">
      <el-table-column label="类型" width="110">
        <template #default="scope">{{ labels[scope.row.type as PortalTargetType] }}</template>
      </el-table-column>
      <el-table-column prop="title" label="名称" min-width="200" show-overflow-tooltip />
      <el-table-column label="上下文" min-width="220">
        <template #default="scope">{{ scope.row.context || '—' }}</template>
      </el-table-column>
      <el-table-column label="状态" width="150">
        <template #default="scope">
          <span
            >{{ scope.row.documentType ? `${scope.row.documentType} · ` : ''
            }}{{ scope.row.lifecycle || scope.row.status }}</span
          >
        </template>
      </el-table-column>
      <el-table-column label="" width="90" align="right">
        <template #default="scope"
          ><el-button type="primary" link @click="select(scope.row)">选择</el-button></template
        >
      </el-table-column>
    </el-table>
    <SkhPagination
      :total="total"
      :current-page="page"
      :page-size="pageSize"
      aria-label="知识对象分页"
      @current-change="
        (value) => {
          page = value
          void load()
        }
      "
      @size-change="
        (value) => {
          pageSize = value
          page = 1
          void load()
        }
      "
    />
  </el-dialog>
</template>
