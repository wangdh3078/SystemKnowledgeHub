<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import {
  ArrowDown,
  ArrowRight,
  ArrowUp,
  Delete,
  EditPen,
  Plus,
  RefreshRight,
  WarningFilled,
} from '@element-plus/icons-vue'
import type { BusinessProcessStepInput } from '../api/businessFunctionContracts'

interface EditableStep {
  uid: number
  name: string
  description: string
}

const props = defineProps<{
  steps: readonly BusinessProcessStepInput[]
  canEdit: boolean
  saving: boolean
  saveError: string | null
  concurrencyConflict: boolean
}>()
const emit = defineEmits<{
  save: [steps: readonly BusinessProcessStepInput[]]
  reload: []
  startEdit: []
}>()
const editing = defineModel<boolean>('editing', { default: false })
const draft = ref<EditableStep[]>([])
let nextUid = 1
const validationError = computed(() =>
  draft.value.some(step => !step.name.trim()) ? '每个步骤都必须填写名称。' : null,
)

function syncDraft(): void {
  draft.value = props.steps.map(step => ({
    uid: nextUid++,
    name: step.name,
    description: step.description ?? '',
  }))
}

function startEdit(): void {
  syncDraft()
  emit('startEdit')
  editing.value = true
}

function cancelEdit(): void {
  syncDraft()
  editing.value = false
}

function addStep(): void {
  draft.value.push({ uid: nextUid++, name: '', description: '' })
}

function removeStep(index: number): void {
  draft.value.splice(index, 1)
}

function moveStep(index: number, direction: -1 | 1): void {
  const target = index + direction
  if (target < 0 || target >= draft.value.length) return
  const [step] = draft.value.splice(index, 1)
  if (step) draft.value.splice(target, 0, step)
}

function submit(): void {
  if (validationError.value) return
  emit('save', draft.value.map((step, index) => ({
    order: index + 1,
    name: step.name.trim(),
    description: step.description.trim() || null,
  })))
}

watch(
  () => props.steps,
  () => {
    if (!editing.value) syncDraft()
  },
  { immediate: true },
)

watch(
  () => props.saving,
  (saving, previous) => {
    if (previous && !saving && !props.saveError) editing.value = false
  },
)
</script>

<template>
  <section class="business-function-section business-process-section" :class="{ 'business-process-section--editing': editing }">
    <div class="business-function-section__heading">
      <h2>业务流程</h2>
      <el-button v-if="!editing && canEdit" text type="primary" :icon="EditPen" @click="startEdit">编辑流程</el-button>
      <span v-else-if="editing" class="business-function-editing-indicator">正在编辑 · {{ draft.length }} 个步骤</span>
      <span v-else>{{ steps.length }} 个步骤</span>
    </div>

    <div v-if="editing" class="business-process-editor">
      <ol>
        <li v-for="(step, index) in draft" :key="step.uid">
          <b>{{ index + 1 }}</b>
          <el-input v-model="step.name" maxlength="160" placeholder="步骤名称" />
          <el-input v-model="step.description" maxlength="300" placeholder="补充说明（可选）" />
          <div class="business-process-editor__row-actions">
            <el-button text :icon="ArrowUp" :disabled="index === 0" aria-label="上移步骤" @click="moveStep(index, -1)" />
            <el-button text :icon="ArrowDown" :disabled="index === draft.length - 1" aria-label="下移步骤" @click="moveStep(index, 1)" />
            <el-button text type="danger" :icon="Delete" aria-label="删除步骤" @click="removeStep(index)" />
          </div>
        </li>
      </ol>
      <el-button class="business-process-editor__add" plain :icon="Plus" @click="addStep">新增步骤</el-button>

      <div v-if="validationError || saveError" class="business-function-edit-error">
        <el-icon><WarningFilled /></el-icon>
        <div>
          <strong>{{ concurrencyConflict ? '检测到并发修改' : '业务流程尚未保存' }}</strong>
          <p>{{ validationError ?? saveError }}</p>
        </div>
        <el-button v-if="concurrencyConflict" text type="primary" :icon="RefreshRight" @click="emit('reload')">重新加载</el-button>
      </div>

      <footer class="business-function-edit-actions">
        <p>保存后整体替换当前流程步骤；关系与知识状态不会自动改变。</p>
        <div>
          <el-button :disabled="saving" @click="cancelEdit">取消</el-button>
          <el-button type="primary" :loading="saving" :disabled="Boolean(validationError)" @click="submit">保存流程</el-button>
        </div>
      </footer>
    </div>

    <div v-else-if="steps.length" class="business-process" aria-label="业务处理步骤">
      <template v-for="(step, index) in steps" :key="step.order">
        <div class="business-process__step">
          <b>{{ step.order }}</b><strong :class="{ 'technical-text': step.name.includes('.') }">{{ step.name }}</strong><small v-if="step.description">{{ step.description }}</small>
        </div>
        <el-icon v-if="index < steps.length - 1" class="business-process__arrow"><ArrowRight /></el-icon>
      </template>
    </div>
    <div v-else class="business-section-empty">尚未记录业务处理步骤。</div>
  </section>
</template>
