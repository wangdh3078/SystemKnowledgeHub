<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { EditPen, RefreshRight, WarningFilled } from '@element-plus/icons-vue'
import type { BusinessFunctionDetailResponse } from '../api/businessFunctionContracts'
import type { BusinessFunctionOverviewValues } from '../composables/useBusinessFunctionDetail'

const props = defineProps<{
  detail: BusinessFunctionDetailResponse
  canEdit: boolean
  saving: boolean
  saveError: string | null
  concurrencyConflict: boolean
}>()
const emit = defineEmits<{
  save: [values: BusinessFunctionOverviewValues]
  reload: []
  startEdit: []
}>()
const editing = defineModel<boolean>('editing', { default: false })
const draft = reactive({ purpose: '', caller: '', input: '', output: '' })
const validationError = computed(() => null)

function syncDraft(): void {
  draft.purpose = props.detail.overview.purpose ?? ''
  draft.caller = props.detail.overview.caller ?? ''
  draft.input = props.detail.overview.input ?? ''
  draft.output = props.detail.overview.output ?? ''
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

function normalize(value: string): string | null {
  const normalized = value.trim()
  return normalized || null
}

function submit(): void {
  emit('save', {
    name: props.detail.header.name,
    displayName: null,
    functionType: props.detail.header.functionType,
    purpose: normalize(draft.purpose),
    caller: normalize(draft.caller),
    input: normalize(draft.input),
    output: normalize(draft.output),
    rewriteStatus: props.detail.header.rewriteStatus,
  })
}

watch(
  () => props.detail,
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
  <section class="business-function-section business-function-overview" :class="{ 'business-function-overview--editing': editing }">
    <div class="business-function-section__heading">
      <h2>概览</h2>
      <el-button v-if="!editing && canEdit" text type="primary" :icon="EditPen" @click="startEdit">编辑概览</el-button>
      <span v-else-if="editing" class="business-function-editing-indicator">正在编辑概览</span>
    </div>

    <el-form v-if="editing" class="business-function-overview-form" label-position="left" label-width="118px" @submit.prevent>
      <el-form-item label="用途">
        <el-input v-model="draft.purpose" type="textarea" :rows="2" maxlength="500" show-word-limit placeholder="说明该功能解决什么问题" />
      </el-form-item>
      <el-form-item label="用户 / 调用方">
        <el-input v-model="draft.caller" maxlength="500" placeholder="例如 MES 设备监控页、生产看板" />
      </el-form-item>
      <el-form-item label="输入">
        <el-input v-model="draft.input" maxlength="500" class="technical-input" placeholder="例如 EQP_ID" />
      </el-form-item>
      <el-form-item label="输出">
        <el-input v-model="draft.output" maxlength="500" class="technical-input" placeholder="例如 EquipmentStatusDto" />
      </el-form-item>

      <div v-if="validationError || saveError" class="business-function-edit-error">
        <el-icon><WarningFilled /></el-icon>
        <div>
          <strong>{{ concurrencyConflict ? '检测到并发修改' : '概览尚未保存' }}</strong>
          <p>{{ validationError ?? saveError }}</p>
        </div>
        <el-button v-if="concurrencyConflict" text type="primary" :icon="RefreshRight" @click="emit('reload')">重新加载</el-button>
      </div>

      <footer class="business-function-edit-actions">
        <p>保存只更新功能概览，不改变业务流程、关系或知识状态。</p>
        <div>
          <el-button :disabled="saving" @click="cancelEdit">取消</el-button>
          <el-button type="primary" :loading="saving" @click="submit">保存概览</el-button>
        </div>
      </footer>
    </el-form>

    <dl v-else>
      <div><dt>用途</dt><dd>{{ detail.overview.purpose ?? '尚未记录' }}</dd></div>
      <div><dt>用户 / 调用方</dt><dd>{{ detail.overview.caller ?? '尚未记录' }}</dd></div>
      <div><dt>输入</dt><dd class="technical-text">{{ detail.overview.input ?? '尚未记录' }}</dd></div>
      <div><dt>输出</dt><dd class="technical-text">{{ detail.overview.output ?? '尚未记录' }}</dd></div>
    </dl>
  </section>
</template>
