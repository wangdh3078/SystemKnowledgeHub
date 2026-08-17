<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import { Close, EditPen } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { getDatabaseObjectDetail, updateDatabaseObjectKnowledge } from '../api/databaseKnowledgeApi'
import type { DatabaseAccessMode, DatabaseObjectDetailResponse } from '../api/databaseKnowledgeContracts'

const props = defineProps<{ databaseObjectId: number | null }>()
const overlayStore = useOverlayStore()
const actorStore = useActorStore()
const detail = ref<DatabaseObjectDetailResponse | null>(null)
const loading = ref(false)
const errorMessage = ref<string | null>(null)
const saving = ref(false)
const saveError = ref<string | null>(null)
const form = reactive({ businessDescription: '', accessMode: 'Unknown' as DatabaseAccessMode, businessKeyColumns: '' })

async function load(): Promise<void> {
  if (props.databaseObjectId === null) return
  loading.value = true
  errorMessage.value = null
  try {
    detail.value = await getDatabaseObjectDetail(props.databaseObjectId)
    form.businessDescription = detail.value.overview.businessDescription ?? ''
    form.accessMode = detail.value.overview.accessMode
    form.businessKeyColumns = detail.value.metadata.businessKeyColumns.join(', ')
  } catch (error: unknown) {
    errorMessage.value = error instanceof Error ? error.message : '数据库对象加载失败。'
  } finally {
    loading.value = false
  }
}

function splitColumns(value: string): readonly string[] | null {
  const items = value.split(',').map((item) => item.trim()).filter(Boolean)
  return items.length === 0 ? null : items
}

async function save(): Promise<void> {
  if (!detail.value || saving.value) return
  saving.value = true
  saveError.value = null
  try {
    await updateDatabaseObjectKnowledge(detail.value.id, {
      businessDescription: form.businessDescription.trim() || null,
      accessMode: form.accessMode,
      businessKeyColumns: splitColumns(form.businessKeyColumns),
      actor: actorStore.actor,
      concurrencyToken: detail.value.concurrencyToken,
    })
    window.dispatchEvent(new Event('database-object:changed'))
    ElMessage.success('数据库对象业务知识已保存，知识状态未自动改变。')
    overlayStore.closeDrawer()
  } catch (error: unknown) {
    saveError.value = error instanceof ApiError ? error.message : error instanceof Error ? error.message : '保存失败。'
  } finally {
    saving.value = false
  }
}

watch(() => props.databaseObjectId, () => void load(), { immediate: true })
</script>

<template>
  <div class="column-drawer database-object-edit-drawer">
    <LoadingState v-if="loading" message="正在读取数据库对象…" />
    <ErrorState v-else-if="errorMessage" title="数据库对象加载失败" :message="errorMessage" @retry="load" />
    <template v-else-if="detail">
      <header class="column-drawer__header">
        <el-button class="column-drawer__close" text circle :icon="Close" aria-label="关闭编辑" @click="overlayStore.closeDrawer()" />
        <span class="column-drawer__eyebrow">编辑数据库知识</span>
        <h2 class="technical-text">{{ detail.overview.qualifiedName }}</h2>
        <p>只维护对象级业务知识；Schema、对象名、类型和技术元数据保持只读。</p>
      </header>
      <section class="column-drawer__sections database-object-edit-drawer__body">
        <div class="drawer-collapse-title"><span>对象级业务知识</span><el-icon><EditPen /></el-icon></div>
        <el-form label-position="top" @submit.prevent>
          <el-form-item label="业务说明"><el-input v-model="form.businessDescription" type="textarea" :rows="3" maxlength="500" show-word-limit placeholder="说明这个 Table 或 View 的业务含义" /></el-form-item>
          <el-form-item label="访问方式"><el-select v-model="form.accessMode"><el-option label="待确认" value="Unknown" /><el-option label="只读" value="Read" /><el-option label="只写" value="Write" /><el-option label="读 / 写" value="ReadWrite" /></el-select></el-form-item>
          <el-form-item label="业务唯一键"><el-input v-model="form.businessKeyColumns" class="technical-input" placeholder="多个字段用逗号分隔" /></el-form-item>
        </el-form>
        <p class="drawer-section-note">业务唯一键只能引用当前已登记字段。知识状态、字段、证据和关系均通过各自明确操作维护。</p>
        <p v-if="saveError" class="authoring-error" role="alert">{{ saveError }}</p>
      </section>
      <footer class="column-drawer__footer"><el-button @click="overlayStore.closeDrawer">取消</el-button><el-button type="primary" :loading="saving" @click="save">保存业务知识</el-button></footer>
    </template>
  </div>
</template>

<style scoped>
.database-object-edit-drawer__body { padding: 18px 24px; }
.database-object-edit-drawer__body :deep(.el-select) { width: 100%; }
</style>
