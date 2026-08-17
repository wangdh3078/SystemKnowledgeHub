<script setup lang="ts">
import { computed, onMounted, reactive, ref, watch } from 'vue'
import { ArrowLeft, Coin, Plus } from '@element-plus/icons-vue'
import type { FormInstance, FormRules } from 'element-plus'
import { ElMessage } from 'element-plus'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import { getSystemsList } from '../../systems/api/systemsApi'
import type { SystemSummary } from '../../systems/api/systemsContracts'
import {
  createDatabaseSource,
  getDatabaseObjectsList,
  registerDatabaseObject,
} from '../api/databaseKnowledgeApi'
import type {
  CreateDatabaseSourceResponse,
  DatabaseAccessMode,
  DatabaseObjectType,
  RegisterDatabaseObjectResponse,
} from '../api/databaseKnowledgeContracts'

const overlayStore = useOverlayStore()
const actorStore = useActorStore()
const router = useRouter()
const kind = computed(() => overlayStore.currentDialog?.kind)
const systemOptions = ref<readonly SystemSummary[]>([])
const sourceOptions = ref<readonly { readonly id: number; readonly name: string; readonly engine: string }[]>([])
const optionsError = ref<string | null>(null)
const sourceFormRef = ref<FormInstance>()
const objectFormRef = ref<FormInstance>()
const submitting = ref(false)
const submitError = ref<string | null>(null)
const fieldErrors = reactive<Record<string, string>>({})
const sourceForm = reactive({
  systemId: undefined as number | undefined,
  name: '',
  engine: '',
  environment: '',
  instanceName: '',
  serviceName: '',
  databaseName: '',
  description: '',
  isPrimary: false,
})
const objectForm = reactive({
  databaseSourceId: undefined as number | undefined,
  schemaName: '',
  objectName: '',
  objectType: 'Table' as DatabaseObjectType,
  estimatedRows: undefined as number | undefined,
  accessMode: 'Unknown' as DatabaseAccessMode,
  primaryKeyColumns: '',
  businessKeyColumns: '',
  businessDescription: '',
})

const sourceRules: FormRules<typeof sourceForm> = {
  systemId: [{ required: true, message: '请选择所属系统', trigger: 'change' }],
  name: [{ required: true, message: '请输入数据库来源名称', trigger: 'blur' }],
  engine: [{ required: true, message: '请输入数据库类型', trigger: 'blur' }],
}
const objectRules: FormRules<typeof objectForm> = {
  databaseSourceId: [{ required: true, message: '请选择数据库来源', trigger: 'change' }],
  schemaName: [{ required: true, message: '请输入 Schema 名称', trigger: 'blur' }],
  objectName: [{ required: true, message: '请输入对象名称', trigger: 'blur' }],
  objectType: [{ required: true, message: '请选择对象类型', trigger: 'change' }],
}

function goBack(): void {
  overlayStore.openDialog({ kind: 'create-database-knowledge', id: null, mode: 'create' })
}

function clearServerErrors(): void {
  submitError.value = null
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
}

function applyServerError(error: unknown, fallback: string): void {
  if (error instanceof ApiError) {
    submitError.value = error.message
    for (const [field, messages] of Object.entries(error.response.fieldErrors ?? {})) {
      const message = messages[0]
      if (message) fieldErrors[field] = message
    }
    return
  }
  submitError.value = error instanceof Error ? error.message : fallback
}

function splitColumns(value: string): readonly string[] | null {
  const values = value.split(',').map((item) => item.trim()).filter(Boolean)
  return values.length === 0 ? null : values
}

async function submitSource(): Promise<void> {
  clearServerErrors()
  const valid = await sourceFormRef.value?.validate().catch(() => false)
  if (!valid || submitting.value || sourceForm.systemId === undefined) return
  submitting.value = true
  try {
    const created = await createDatabaseSource({
      systemId: sourceForm.systemId,
      name: sourceForm.name.trim(),
      engine: sourceForm.engine.trim(),
      environment: sourceForm.environment.trim() || null,
      instanceName: sourceForm.instanceName.trim() || null,
      serviceName: sourceForm.serviceName.trim() || null,
      databaseName: sourceForm.databaseName.trim() || null,
      description: sourceForm.description.trim() || null,
      isPrimary: sourceForm.isPrimary,
      actor: actorStore.actor,
    })
    await handleSourceCreated(created)
  } catch (error: unknown) {
    applyServerError(error, '数据库来源登记失败。')
  } finally {
    submitting.value = false
  }
}

async function submitObject(): Promise<void> {
  clearServerErrors()
  const valid = await objectFormRef.value?.validate().catch(() => false)
  if (!valid || submitting.value || objectForm.databaseSourceId === undefined) return
  submitting.value = true
  try {
    const created = await registerDatabaseObject({
      databaseSourceId: objectForm.databaseSourceId,
      schemaName: objectForm.schemaName.trim(),
      objectName: objectForm.objectName.trim(),
      objectType: objectForm.objectType,
      estimatedRows: objectForm.estimatedRows ?? null,
      accessMode: objectForm.accessMode,
      primaryKeyColumns: splitColumns(objectForm.primaryKeyColumns),
      businessKeyColumns: splitColumns(objectForm.businessKeyColumns),
      businessDescription: objectForm.businessDescription.trim() || null,
      actor: actorStore.actor,
    })
    await handleObjectCreated(created)
  } catch (error: unknown) {
    applyServerError(error, '数据库对象登记失败。')
  } finally {
    submitting.value = false
  }
}

async function handleSourceCreated(created: CreateDatabaseSourceResponse): Promise<void> {
  overlayStore.closeDialog()
  ElMessage.success(`已登记数据库来源 ${created.name}。`)
  await router.replace({
    name: 'database-objects-list',
    query: { systemId: String(created.systemId), databaseSourceId: String(created.id) },
  })
}

async function handleObjectCreated(created: RegisterDatabaseObjectResponse): Promise<void> {
  overlayStore.closeDialog()
  ElMessage.success(`已登记 ${created.qualifiedName}，知识状态保持“未知”。`)
  await router.replace({
    name: 'database-objects-list',
    query: { databaseSourceId: String(created.databaseSourceId) },
  })
}

async function loadOptions(): Promise<void> {
  optionsError.value = null
  try {
    const [systems, objects] = await Promise.all([
      getSystemsList({ sort: 'name:asc', page: 1, pageSize: 100 }),
      getDatabaseObjectsList({ sort: 'objectName:asc', page: 1, pageSize: 1 }),
    ])
    systemOptions.value = systems.items
    sourceOptions.value = objects.browseContext.databaseSources
  } catch (error: unknown) {
    optionsError.value = error instanceof Error ? error.message : '创建上下文加载失败。'
  }
}

onMounted(() => void loadOptions())

watch(kind, (nextKind) => {
  if (nextKind === 'create-database-source' || nextKind === 'register-database-object') {
    void loadOptions()
  }
})
</script>

<template>
  <Teleport v-if="overlayStore.isDialogOpen" defer to="#dialog-feature-content">
    <section v-if="kind === 'create-database-knowledge'" class="create-database-knowledge-dialog" aria-labelledby="create-database-knowledge-title">
      <header class="authoring-header">
        <div>
          <h2 id="create-database-knowledge-title">新增数据库知识</h2>
          <p>先登记实际数据库来源，或手工登记一个 Table / View；字段在对象详情中后续补充。</p>
        </div>
        <button class="authoring-close" type="button" aria-label="关闭" @click="overlayStore.closeDialog">×</button>
      </header>
      <div class="database-create-choice-grid">
        <button type="button" class="database-create-choice" @click="overlayStore.openDialog({ kind: 'create-database-source', id: null, mode: 'create' })">
          <el-icon :size="24"><Coin /></el-icon>
          <strong>登记数据库来源</strong>
          <span>关联一个已登记系统，记录名称与数据库类型。</span>
          <el-icon :size="17"><ArrowLeft class="database-create-choice__arrow" /></el-icon>
        </button>
        <button type="button" class="database-create-choice" @click="overlayStore.openDialog({ kind: 'register-database-object', id: null, mode: 'create' })">
          <el-icon :size="24"><Plus /></el-icon>
          <strong>登记数据库对象</strong>
          <span>在已有数据库来源下手工登记 Table 或 View。</span>
          <el-icon :size="17"><ArrowLeft class="database-create-choice__arrow" /></el-icon>
        </button>
      </div>
      <p class="database-create-choice-note">最小信息保存后，业务说明、字段、关系和证据均可在后续上下文中渐进补充。</p>
    </section>

    <section v-else-if="kind === 'create-database-source'" class="create-database-form-dialog" aria-labelledby="create-database-source-title">
      <header class="authoring-header authoring-header--form">
        <div>
          <button class="authoring-back" type="button" @click="goBack"><el-icon><ArrowLeft /></el-icon>选择数据库知识类型</button>
          <h2 id="create-database-source-title">登记数据库来源</h2>
          <p>只记录可识别的数据库上下文，不登记连接串、密码或其他凭据。</p>
        </div>
        <button class="authoring-close" type="button" aria-label="关闭" @click="overlayStore.closeDialog">×</button>
      </header>
      <el-alert v-if="optionsError" type="warning" :title="optionsError" :closable="false" show-icon />
      <el-form ref="sourceFormRef" :model="sourceForm" :rules="sourceRules" label-position="top" @submit.prevent>
        <el-form-item label="所属系统" prop="systemId" :error="fieldErrors.systemId">
          <el-select v-model="sourceForm.systemId" filterable placeholder="选择已登记系统"><el-option v-for="item in systemOptions" :key="item.id" :label="item.name" :value="item.id" /></el-select>
        </el-form-item>
        <div class="create-database-form-dialog__row">
          <el-form-item label="数据库来源名称" prop="name" :error="fieldErrors.name"><el-input v-model="sourceForm.name" class="technical-input" placeholder="例如 MES 生产库" /></el-form-item>
          <el-form-item label="数据库类型" prop="engine" :error="fieldErrors.engine"><el-input v-model="sourceForm.engine" class="technical-input" placeholder="例如 Oracle" /></el-form-item>
        </div>
        <el-collapse>
          <el-collapse-item title="补充来源信息（可选）" name="optional-source">
            <div class="create-database-form-dialog__row">
              <el-form-item label="环境"><el-input v-model="sourceForm.environment" placeholder="例如 Production" /></el-form-item>
              <el-form-item label="实例名称"><el-input v-model="sourceForm.instanceName" class="technical-input" /></el-form-item>
              <el-form-item label="服务名"><el-input v-model="sourceForm.serviceName" class="technical-input" /></el-form-item>
              <el-form-item label="数据库名"><el-input v-model="sourceForm.databaseName" class="technical-input" /></el-form-item>
            </div>
            <el-form-item label="说明"><el-input v-model="sourceForm.description" type="textarea" :rows="2" maxlength="500" show-word-limit /></el-form-item>
            <el-checkbox v-model="sourceForm.isPrimary">设为该系统的主数据库来源</el-checkbox>
          </el-collapse-item>
        </el-collapse>
        <p v-if="submitError" class="authoring-error" role="alert">{{ submitError }}</p>
      </el-form>
      <footer class="authoring-actions"><p>创建人：{{ actorStore.displayName }}{{ actorStore.role ? ` · ${actorStore.role}` : '' }}</p><div><el-button @click="overlayStore.closeDialog">取消</el-button><el-button type="primary" :loading="submitting" @click="submitSource">登记数据库来源</el-button></div></footer>
    </section>

    <section v-else-if="kind === 'register-database-object'" class="create-database-form-dialog" aria-labelledby="register-database-object-title">
      <header class="authoring-header authoring-header--form">
        <div>
          <button class="authoring-back" type="button" @click="goBack"><el-icon><ArrowLeft /></el-icon>选择数据库知识类型</button>
          <h2 id="register-database-object-title">登记数据库对象</h2>
          <p>仅登记 Table 或 View 的最小元数据；不会自动导入字段或推进知识状态。</p>
        </div>
        <button class="authoring-close" type="button" aria-label="关闭" @click="overlayStore.closeDialog">×</button>
      </header>
      <el-alert v-if="optionsError" type="warning" :title="optionsError" :closable="false" show-icon />
      <el-form ref="objectFormRef" :model="objectForm" :rules="objectRules" label-position="top" @submit.prevent>
        <el-form-item label="数据库来源" prop="databaseSourceId" :error="fieldErrors.databaseSourceId">
          <el-select v-model="objectForm.databaseSourceId" filterable placeholder="选择数据库来源"><el-option v-for="item in sourceOptions" :key="item.id" :label="`${item.name} · ${item.engine}`" :value="item.id" /></el-select>
        </el-form-item>
        <div class="create-database-form-dialog__row">
          <el-form-item label="Schema" prop="schemaName" :error="fieldErrors.schemaName"><el-input v-model="objectForm.schemaName" class="technical-input" placeholder="例如 MES" /></el-form-item>
          <el-form-item label="对象名称" prop="objectName" :error="fieldErrors.objectName"><el-input v-model="objectForm.objectName" class="technical-input" placeholder="例如 TABLE_EQP" /></el-form-item>
          <el-form-item label="对象类型" prop="objectType" :error="fieldErrors.objectType"><el-select v-model="objectForm.objectType"><el-option label="表" value="Table" /><el-option label="视图" value="View" /></el-select></el-form-item>
        </div>
        <el-collapse>
          <el-collapse-item title="补充对象元数据（可选）" name="optional-object">
            <div class="create-database-form-dialog__row">
              <el-form-item label="估算行数" :error="fieldErrors.estimatedRows"><el-input-number v-model="objectForm.estimatedRows" :min="0" :precision="0" controls-position="right" /></el-form-item>
              <el-form-item label="读写方式" :error="fieldErrors.accessMode"><el-select v-model="objectForm.accessMode"><el-option label="待确认" value="Unknown" /><el-option label="只读" value="Read" /><el-option label="只写" value="Write" /><el-option label="读 / 写" value="ReadWrite" /></el-select></el-form-item>
              <el-form-item label="主键字段"><el-input v-model="objectForm.primaryKeyColumns" class="technical-input" placeholder="EQP_ID，多个用逗号分隔" /></el-form-item>
              <el-form-item label="业务唯一键"><el-input v-model="objectForm.businessKeyColumns" class="technical-input" placeholder="EQP_CODE，多个用逗号分隔" /></el-form-item>
            </div>
            <el-form-item label="业务说明"><el-input v-model="objectForm.businessDescription" type="textarea" :rows="2" maxlength="500" show-word-limit placeholder="可后续在对象详情中补充" /></el-form-item>
          </el-collapse-item>
        </el-collapse>
        <p v-if="submitError" class="authoring-error" role="alert">{{ submitError }}</p>
      </el-form>
      <footer class="authoring-actions"><p>创建后知识状态保持“未知”。</p><div><el-button @click="overlayStore.closeDialog">取消</el-button><el-button type="primary" :loading="submitting" @click="submitObject">登记对象</el-button></div></footer>
    </section>
  </Teleport>
</template>
