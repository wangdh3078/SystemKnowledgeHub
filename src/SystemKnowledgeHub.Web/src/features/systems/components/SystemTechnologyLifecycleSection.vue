<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { EditPen, RefreshRight, WarningFilled } from '@element-plus/icons-vue'
import {
  systemLifecycleLabels,
  systemLifecycles,
  type SystemDetailOverview,
  type SystemLifecycle,
} from '../api/systemsContracts'
import { getSystemsList } from '../api/systemsApi'

const props = defineProps<{
  overview: SystemDetailOverview
  canEditTechnology: boolean
  canEditLifecycle: boolean
  saving: boolean
  saveError: string | null
  concurrencyConflict: boolean
}>()

const emit = defineEmits<{
  saveTechnology: [technologies: string[]]
  saveLifecycle: [lifecycle: SystemLifecycle]
  reload: []
}>()

const editingTechnology = ref(false)
const editingLifecycle = ref(false)
const technologyDraft = ref<string[]>([])
const technologyOptions = ref<string[]>([])
const lifecycleDraft = ref<SystemLifecycle>(props.overview.lifecycle)
const technologyValidationError = computed(() => {
  const normalized = technologyDraft.value.map((value) => value.trim()).filter(Boolean)
  return new Set(normalized.map((value) => value.toLocaleLowerCase('en-US'))).size !== normalized.length
    ? '技术标签不能重复。'
    : null
})

function syncTechnologyDraft(): void {
  technologyDraft.value = [...props.overview.technologies]
}

function normalizeOptions(values: readonly string[]): string[] {
  return [...new Set(values.map((value) => value.trim()).filter(Boolean))]
    .sort((left, right) => left.localeCompare(right, 'en-US', { sensitivity: 'base' }))
}

async function loadTechnologyOptions(): Promise<void> {
  try {
    const response = await getSystemsList({ page: 1, pageSize: 100, sort: 'name:asc' })
    technologyOptions.value = normalizeOptions([
      ...props.overview.technologies,
      ...response.items.flatMap((system) => system.technologies),
    ])
  } catch {
    technologyOptions.value = normalizeOptions(props.overview.technologies)
  }
}

function startTechnologyEdit(): void {
  syncTechnologyDraft()
  void loadTechnologyOptions()
  editingLifecycle.value = false
  editingTechnology.value = true
}

function cancelTechnologyEdit(): void {
  syncTechnologyDraft()
  editingTechnology.value = false
}

function saveTechnology(): void {
  if (technologyValidationError.value) return
  emit('saveTechnology', technologyDraft.value.map((value) => value.trim()).filter(Boolean))
}

function startLifecycleEdit(): void {
  lifecycleDraft.value = props.overview.lifecycle
  editingTechnology.value = false
  editingLifecycle.value = true
}

function cancelLifecycleEdit(): void {
  lifecycleDraft.value = props.overview.lifecycle
  editingLifecycle.value = false
}

function saveLifecycle(): void {
  if (lifecycleDraft.value === props.overview.lifecycle) return
  emit('saveLifecycle', lifecycleDraft.value)
}

watch(
  () => props.overview,
  () => {
    if (!editingTechnology.value) syncTechnologyDraft()
    if (!editingLifecycle.value) lifecycleDraft.value = props.overview.lifecycle
  },
  { immediate: true },
)

watch(
  () => props.saving,
  (saving, previous) => {
    if (previous && !saving && !props.saveError) {
      editingTechnology.value = false
      editingLifecycle.value = false
    }
  },
)
</script>

<template>
  <section class="system-detail-section system-technology-lifecycle">
    <div class="system-section-heading">
      <h2>技术与生命周期</h2>
      <span>分别维护，不影响知识状态</span>
    </div>

    <div class="system-technology-lifecycle__grid">
      <div class="system-inline-section" :class="{ 'system-inline-section--editing': editingTechnology }">
        <header>
          <div><h3>技术</h3><p>当前系统已知技术标签</p></div>
          <el-button v-if="!editingTechnology && canEditTechnology" text type="primary" :icon="EditPen" :disabled="saving" @click="startTechnologyEdit">编辑技术</el-button>
        </header>
        <template v-if="editingTechnology">
          <el-select v-model="technologyDraft" class="system-inline-section__select" multiple filterable allow-create default-first-option clearable :reserve-keyword="false" :disabled="saving" placeholder="搜索已有标签，或输入新标签后按 Enter">
            <el-option v-for="technology in technologyOptions" :key="technology" :label="technology" :value="technology" />
          </el-select>
          <p class="system-inline-section__hint">可搜索、添加或移除标签；保存仅替换技术集合，不会推进知识状态。</p>
          <div v-if="technologyValidationError || saveError" class="system-inline-section__error">
            <el-icon><WarningFilled /></el-icon><span>{{ technologyValidationError ?? saveError }}</span>
            <el-button v-if="concurrencyConflict" text type="primary" :icon="RefreshRight" @click="emit('reload')">重新加载</el-button>
          </div>
          <footer><el-button :disabled="saving" @click="cancelTechnologyEdit">取消</el-button><el-button type="primary" :loading="saving" :disabled="Boolean(technologyValidationError)" @click="saveTechnology">保存技术</el-button></footer>
        </template>
        <div v-else class="system-technology-tags">
          <span v-for="technology in overview.technologies" :key="technology" class="technical-text">{{ technology }}</span>
          <small v-if="overview.technologies.length === 0">尚未记录</small>
        </div>
      </div>

      <div class="system-inline-section" :class="{ 'system-inline-section--editing': editingLifecycle }">
        <header>
          <div><h3>生命周期</h3><p>系统当前所处阶段</p></div>
          <el-button v-if="!editingLifecycle && canEditLifecycle" text type="primary" :icon="EditPen" :disabled="saving" @click="startLifecycleEdit">编辑生命周期</el-button>
        </header>
        <template v-if="editingLifecycle">
          <el-select v-model="lifecycleDraft" :disabled="saving" class="system-inline-section__select">
            <el-option v-for="lifecycle in systemLifecycles" :key="lifecycle" :label="systemLifecycleLabels[lifecycle]" :value="lifecycle" />
          </el-select>
          <p class="system-inline-section__hint">生命周期独立于知识状态；“已退役”用于替代物理删除。</p>
          <div v-if="saveError" class="system-inline-section__error">
            <el-icon><WarningFilled /></el-icon><span>{{ saveError }}</span>
            <el-button v-if="concurrencyConflict" text type="primary" :icon="RefreshRight" @click="emit('reload')">重新加载</el-button>
          </div>
          <footer><el-button :disabled="saving" @click="cancelLifecycleEdit">取消</el-button><el-button type="primary" :loading="saving" :disabled="lifecycleDraft === overview.lifecycle" @click="saveLifecycle">保存生命周期</el-button></footer>
        </template>
        <div v-else class="system-lifecycle-value"><span>{{ systemLifecycleLabels[overview.lifecycle] }}</span><small>不会改变知识状态</small></div>
      </div>
    </div>
  </section>
</template>
