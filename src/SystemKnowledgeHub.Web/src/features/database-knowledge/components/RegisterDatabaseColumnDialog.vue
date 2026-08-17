<script setup lang="ts">
import { reactive, ref, watch } from 'vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { registerDatabaseColumn } from '../api/databaseKnowledgeApi'

const props = defineProps<{
  databaseObjectId: number
  concurrencyToken: string
  nextOrdinalPosition: number
}>()
const emit = defineEmits<{ registered: [] }>()
const overlayStore = useOverlayStore()
const actorStore = useActorStore()
const formRef = ref<FormInstance>()
const submitting = ref(false)
const submitError = ref<string | null>(null)
const form = reactive({
  ordinalPosition: props.nextOrdinalPosition,
  columnName: '',
  dataType: '',
  nullable: true,
  defaultValue: '',
  databaseComment: '',
  businessDescription: '',
})
const rules: FormRules<typeof form> = {
  ordinalPosition: [{ required: true, message: '请输入大于 0 的字段顺序', trigger: 'blur' }],
  columnName: [{ required: true, message: '请输入字段名称', trigger: 'blur' }],
  dataType: [{ required: true, message: '请输入数据类型', trigger: 'blur' }],
}

watch(
  () => overlayStore.currentDialog?.kind,
  (kind) => {
    if (kind === 'register-database-column') {
      form.ordinalPosition = props.nextOrdinalPosition
      form.columnName = ''
      form.dataType = ''
      form.nullable = true
      form.defaultValue = ''
      form.databaseComment = ''
      form.businessDescription = ''
      submitError.value = null
    }
  },
)

async function submit(): Promise<void> {
  const valid = await formRef.value?.validate().catch(() => false)
  if (!valid || submitting.value) return
  submitting.value = true
  submitError.value = null
  try {
    const created = await registerDatabaseColumn(props.databaseObjectId, {
      ordinalPosition: form.ordinalPosition,
      columnName: form.columnName.trim(),
      dataType: form.dataType.trim(),
      nullable: form.nullable,
      defaultValue: form.defaultValue.trim() || null,
      databaseComment: form.databaseComment.trim() || null,
      businessDescription: form.businessDescription.trim() || null,
      actor: actorStore.actor,
      concurrencyToken: props.concurrencyToken,
    })
    overlayStore.closeDialog()
    ElMessage.success(`已登记字段 ${created.column.columnName}，知识状态保持“未知”。`)
    emit('registered')
  } catch (error: unknown) {
    submitError.value = error instanceof ApiError
      ? error.message
      : error instanceof Error ? error.message : '字段登记失败。'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Teleport v-if="overlayStore.currentDialog?.kind === 'register-database-column'" defer to="#dialog-feature-content">
    <section class="register-column-dialog" aria-labelledby="register-column-title">
      <header class="authoring-header authoring-header--form">
        <div>
          <h2 id="register-column-title">登记字段</h2>
          <p>只登记字段元数据；业务知识、已知值、关系和证据都可在字段详情中后续补充。</p>
        </div>
        <button class="authoring-close" type="button" aria-label="关闭" @click="overlayStore.closeDialog">×</button>
      </header>
      <el-form ref="formRef" :model="form" :rules="rules" label-position="top" @submit.prevent>
        <div class="register-column-dialog__row">
          <el-form-item label="字段顺序" prop="ordinalPosition"><el-input-number v-model="form.ordinalPosition" :min="1" :precision="0" controls-position="right" /></el-form-item>
          <el-form-item label="字段名称" prop="columnName"><el-input v-model="form.columnName" class="technical-input" placeholder="例如 STATE_FLAG" /></el-form-item>
          <el-form-item label="数据类型" prop="dataType"><el-input v-model="form.dataType" class="technical-input" placeholder="例如 VARCHAR2(20)" /></el-form-item>
        </div>
        <el-form-item label="允许为空"><el-switch v-model="form.nullable" active-text="是" inactive-text="否" /></el-form-item>
        <el-collapse>
          <el-collapse-item title="补充字段信息（可选）" name="optional-column">
            <div class="register-column-dialog__row">
              <el-form-item label="默认值"><el-input v-model="form.defaultValue" class="technical-input" /></el-form-item>
              <el-form-item label="数据库注释"><el-input v-model="form.databaseComment" /></el-form-item>
            </div>
            <el-form-item label="初步业务说明"><el-input v-model="form.businessDescription" type="textarea" :rows="2" maxlength="500" show-word-limit /></el-form-item>
          </el-collapse-item>
        </el-collapse>
        <p v-if="submitError" class="authoring-error" role="alert">{{ submitError }}</p>
      </el-form>
      <footer class="authoring-actions">
        <p>登记后知识状态保持“未知”，不会自动新增证据或关系。</p>
        <div><el-button @click="overlayStore.closeDialog">取消</el-button><el-button type="primary" :loading="submitting" @click="submit">登记字段</el-button></div>
      </footer>
    </section>
  </Teleport>
</template>

<style scoped>
.register-column-dialog :deep(.el-form) { padding: 18px 28px 10px; }
.register-column-dialog__row { display: grid; grid-template-columns: 120px minmax(0, 1fr) minmax(0, 1fr); gap: 0 14px; }
@media (max-width: 720px) { .register-column-dialog__row { grid-template-columns: 1fr; } }
</style>
