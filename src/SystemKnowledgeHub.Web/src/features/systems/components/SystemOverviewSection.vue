<script setup lang="ts">
import { computed, reactive, watch } from 'vue'
import { EditPen, RefreshRight, WarningFilled } from '@element-plus/icons-vue'
import type { SystemDetailOverview } from '../api/systemsContracts'
import type { SystemOverviewValues } from '../composables/useSystemDetail'

const props = defineProps<{
  overview: SystemDetailOverview
  mainDatabaseName: string | null
  canEdit: boolean
  saving: boolean
  saveError: string | null
  concurrencyConflict: boolean
}>()

const emit = defineEmits<{
  save: [values: SystemOverviewValues]
  reload: []
}>()

const editing = defineModel<boolean>('editing', { default: false })
const draft = reactive({
  displayName: '',
  systemType: '',
  purpose: '',
  mainUsersText: '',
  repositoryName: '',
  repositoryUrl: '',
  deploymentText: '',
  notes: '',
})
const displayNameError = computed(() => !draft.displayName.trim() ? '显示名称不能为空。' : null)
const systemTypeError = computed(() => !draft.systemType.trim() ? '系统类型不能为空。' : null)
const validationError = computed(() => {
  if (displayNameError.value) return displayNameError.value
  if (systemTypeError.value) return systemTypeError.value
  const repositoryUrl = draft.repositoryUrl.trim()
  if (repositoryUrl) {
    try {
      const parsed = new URL(repositoryUrl)
      if (parsed.protocol !== 'http:' && parsed.protocol !== 'https:') {
        return '仓库地址必须使用 HTTP 或 HTTPS。'
      }
    } catch {
      return '仓库地址格式无效。'
    }
  }
  const invalidDeployment = draft.deploymentText
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .some((line) => {
      const [environment, ...descriptionParts] = line.split('|')
      return !environment?.trim() || !descriptionParts.join('|').trim()
    })
  return invalidDeployment ? '部署信息请按“环境 | 说明”每行填写一项。' : null
})

function syncDraft(): void {
  draft.displayName = props.overview.displayName
  draft.systemType = props.overview.systemType
  draft.purpose = props.overview.purpose ?? ''
  draft.mainUsersText = props.overview.mainUsers.join('，')
  draft.repositoryName = props.overview.repository.name ?? ''
  draft.repositoryUrl = props.overview.repository.url ?? ''
  draft.deploymentText = props.overview.deployment
    .map((item) => `${item.environment} | ${item.description}`)
    .join('\n')
  draft.notes = props.overview.notes ?? ''
}

function startEdit(): void {
  syncDraft()
  editing.value = true
}

function cancelEdit(): void {
  syncDraft()
  editing.value = false
}

function splitValues(value: string): string[] {
  return value
    .split(/[,，\n]/)
    .map((item) => item.trim())
    .filter(Boolean)
}

function submit(): void {
  if (validationError.value) return
  const deployment = draft.deploymentText
    .split(/\r?\n/)
    .map((line) => line.trim())
    .filter(Boolean)
    .map((line) => {
      const [environment, ...descriptionParts] = line.split('|')
      return {
        environment: (environment ?? '').trim(),
        description: descriptionParts.join('|').trim(),
      }
    })

  emit('save', {
    displayName: draft.displayName.trim(),
    systemType: draft.systemType.trim(),
    purpose: draft.purpose.trim() || null,
    mainUsers: splitValues(draft.mainUsersText),
    repository: {
      name: draft.repositoryName.trim() || null,
      url: draft.repositoryUrl.trim() || null,
    },
    deployment,
    mainProjects: [],
    mainEntryPoints: [],
    notes: draft.notes.trim() || null,
  })
}

watch(
  () => props.overview,
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
  <section class="system-overview" :class="{ 'system-overview--editing': editing }">
    <div class="system-section-heading">
      <h2>概览</h2>
      <el-button
        v-if="!editing && canEdit"
        text
        type="primary"
        :icon="EditPen"
        @click="startEdit"
      >编辑概览</el-button>
      <span v-else-if="editing" class="system-editing-indicator">正在编辑概览</span>
    </div>

    <el-form v-if="editing" label-position="top" class="system-overview-form" @submit.prevent>
      <div class="system-overview-form__grid">
        <el-form-item label="显示名称" :error="displayNameError ?? undefined" required>
          <el-input v-model="draft.displayName" maxlength="120" show-word-limit />
        </el-form-item>
        <el-form-item label="系统类型" :error="systemTypeError ?? undefined" required>
          <el-input v-model="draft.systemType" maxlength="120" />
        </el-form-item>
        <el-form-item label="用途">
          <el-input v-model="draft.purpose" type="textarea" :rows="1" maxlength="500" show-word-limit />
        </el-form-item>
        <el-form-item label="部署">
          <el-input
            v-model="draft.deploymentText"
            type="textarea"
            :rows="1"
            placeholder="每行一项：Production | MES-APP-01"
          />
        </el-form-item>
        <el-form-item label="主要用户">
          <el-input v-model="draft.mainUsersText" placeholder="使用中文逗号分隔，例如：生产操作员，设备工程师" />
        </el-form-item>
        <el-form-item label="备注">
          <el-input v-model="draft.notes" type="textarea" :rows="1" maxlength="1000" show-word-limit />
        </el-form-item>
        <el-form-item label="代码仓库名称">
          <el-input v-model="draft.repositoryName" class="technical-input" />
        </el-form-item>
        <el-form-item label="代码仓库地址">
          <el-input v-model="draft.repositoryUrl" class="technical-input" placeholder="https://…" />
        </el-form-item>
      </div>

      <div class="system-overview-form__readonly">
        <span>只读上下文</span>
        <strong>技术：{{ overview.technologies.join(' · ') || '尚未记录' }}</strong>
        <strong>数据库：{{ mainDatabaseName ?? '尚未登记' }}</strong>
        <small>生命周期和知识状态需通过各自明确操作修改，本 Slice 不开放。</small>
      </div>

      <div v-if="validationError || saveError" class="system-overview-form__error">
        <el-icon><WarningFilled /></el-icon>
        <div>
          <strong>{{ concurrencyConflict ? '检测到并发修改' : '概览尚未保存' }}</strong>
          <p>{{ validationError ?? saveError }}</p>
        </div>
        <el-button
          v-if="concurrencyConflict"
          text
          type="primary"
          :icon="RefreshRight"
          @click="emit('reload')"
        >重新加载</el-button>
      </div>

      <footer class="system-overview-form__actions">
        <p>保存只更新概览，不改变生命周期、技术或知识状态。</p>
        <div>
          <el-button :disabled="saving" @click="cancelEdit">取消</el-button>
          <el-button type="primary" :loading="saving" :disabled="Boolean(validationError)" @click="submit">
            保存概览
          </el-button>
        </div>
      </footer>
    </el-form>

    <dl v-else class="system-overview-readonly">
      <div><dt>用途</dt><dd>{{ overview.purpose ?? '尚未记录' }}</dd></div>
      <div><dt>部署</dt><dd>{{ overview.deployment.map((item) => `${item.environment} · ${item.description}`).join('；') || '尚未记录' }}</dd></div>
      <div><dt>主要用户</dt><dd>{{ overview.mainUsers.join('、') || '尚未记录' }}</dd></div>
      <div><dt>数据库</dt><dd>{{ mainDatabaseName ?? '尚未登记' }}</dd></div>
      <div><dt>技术</dt><dd class="technical-text">{{ overview.technologies.join(' · ') || '尚未记录' }}</dd></div>
      <div><dt>备注</dt><dd>{{ overview.notes ?? '尚未记录' }}</dd></div>
      <div><dt>代码仓库</dt><dd class="technical-text">{{ overview.repository.name ?? '尚未记录' }}</dd></div>
      <div><dt>仓库地址</dt><dd class="technical-text">{{ overview.repository.url ?? '尚未记录' }}</dd></div>
    </dl>
  </section>
</template>
