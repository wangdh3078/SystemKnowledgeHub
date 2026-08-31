<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, reactive, ref, watch } from 'vue'
import { ElMessage, ElMessageBox } from 'element-plus'
import { ArrowDown, Close, Plus, Refresh, VideoPlay } from '@element-plus/icons-vue'
import { useRouter } from 'vue-router'
import { ApiError } from '../../../api/errors/ApiError'
import { useActorStore } from '../../../app/stores/actor'
import { useOverlayStore } from '../../../app/stores/overlays'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import DiscoverySectionNav from '../components/DiscoverySectionNav.vue'
import * as api from '../api/databaseDiscoveryApi'
import type {
  ConnectionProfile,
  DatabaseProviderType,
  SourceOption,
} from '../api/databaseDiscoveryContracts'
import '../database-discovery.css'

const actorStore = useActorStore()
const overlayStore = useOverlayStore()
const router = useRouter()
const profiles = ref<readonly ConnectionProfile[]>([])
const sources = ref<readonly SourceOption[]>([])
const loading = ref(false)
const initialized = ref(false)
const sourceLoading = ref(false)
let sourceController: AbortController | undefined
let sourceSearchTimer: number | undefined
let sourceRequestId = 0
const saving = ref(false)
const actionKey = ref('')
const editing = ref<ConnectionProfile | null>(null)
const secretProfile = ref<ConnectionProfile | null>(null)
const testResult = ref('')
const error = ref('')
const fieldErrors = reactive<Record<string, string>>({})
const password = ref('')
const form = reactive({
  databaseSourceId: null as number | null,
  name: '',
  providerType: 'Oracle' as DatabaseProviderType,
  host: '',
  port: 1521,
  databaseName: '',
  serviceName: '',
  username: '',
  includedSchemasText: '',
  isEnabled: true,
  password: '',
})
const locatorLabel = computed(() => (form.providerType === 'Oracle' ? '服务名' : '数据库名'))
const profileDialogOpen = computed(
  () => overlayStore.currentDialog?.kind === 'database-discovery-connection-profile',
)
const secretDialogOpen = computed(
  () => overlayStore.currentDialog?.kind === 'database-discovery-connection-secret',
)
const availableSources = computed(() =>
  sources.value.filter((item) =>
    form.providerType === 'Oracle' ? item.engine === 'Oracle' : item.engine === 'PostgreSQL',
  ),
)
const message = (value: unknown) =>
  value instanceof ApiError ? value.message : '操作失败，请稍后重试。'
const format = (value: string | null) => (value ? new Date(value).toLocaleString('zh-CN') : '—')
const providerLabel = (value: DatabaseProviderType) =>
  value === 'PostgreSql' ? 'PostgreSQL' : 'Oracle'
const connectionStatusLabel: Record<ConnectionProfile['connectionStatus'], string> = {
  Unknown: '未测试',
  Succeeded: '测试成功',
  Failed: '测试失败',
}
const connectionStatusText = (value: ConnectionProfile['connectionStatus']) =>
  connectionStatusLabel[value]
const sourceName = (profile: ConnectionProfile) => {
  return profile.databaseSourceName || `#${profile.databaseSourceId}`
}
function clearFieldErrors(): void {
  for (const key of Object.keys(fieldErrors)) delete fieldErrors[key]
}
function clearFieldError(key: string): void {
  delete fieldErrors[key]
}
function closeOwnedDialog(): void {
  if (profileDialogOpen.value || secretDialogOpen.value) overlayStore.closeDialog()
}
function closeProfileDialog(): void {
  form.password = ''
  closeOwnedDialog()
}
function closeSecretDialog(): void {
  password.value = ''
  closeOwnedDialog()
}
function validate(): boolean {
  clearFieldErrors()
  const schemas = form.includedSchemasText
    .split(/[\n,]/)
    .map((item) => item.trim())
    .filter(Boolean)
  if (!form.databaseSourceId) fieldErrors.databaseSourceId = '请选择数据库来源。'
  if (!form.name.trim()) fieldErrors.name = '请输入名称。'
  if (!form.host.trim()) fieldErrors.host = '请输入 Host。'
  if (!Number.isInteger(form.port) || form.port < 1 || form.port > 65535)
    fieldErrors.port = 'Port 必须在 1 到 65535 之间。'
  if (!form.username.trim()) fieldErrors.username = '请输入用户名。'
  if (form.providerType === 'Oracle' && !form.serviceName.trim())
    fieldErrors.serviceName = '请输入服务名。'
  if (form.providerType === 'PostgreSql' && !form.databaseName.trim())
    fieldErrors.databaseName = '请输入数据库名。'
  if (schemas.length === 0 || schemas.length > 128)
    fieldErrors.includedSchemas = '请输入 1 到 128 个 Schema。'
  else if (new Set(schemas).size !== schemas.length)
    fieldErrors.includedSchemas = 'Schema 不能重复。'
  if (!editing.value && !form.password) fieldErrors.password = '请设置连接密码。'
  return Object.keys(fieldErrors).length === 0
}
async function handleOperationError(value: unknown): Promise<void> {
  if (value instanceof ApiError && value.response.fieldErrors) {
    for (const [key, messages] of Object.entries(value.response.fieldErrors))
      fieldErrors[key] = messages[0] ?? value.message
  }
  if (value instanceof ApiError && value.status === 409) {
    closeOwnedDialog()
    password.value = ''
    form.password = ''
    await load()
    ElMessage.warning(`${value.message} 已重新加载最新状态。`)
    return
  }
  ElMessage.error(message(value))
}
async function load(): Promise<void> {
  loading.value = true
  error.value = ''
  try {
    ;[profiles.value, sources.value] = await Promise.all([
      api.listProfiles(),
      api.listSourceOptions(),
    ])
  } catch (e) {
    error.value = message(e)
  } finally {
    loading.value = false
    initialized.value = true
  }
}
async function runSourceSearch(term: string, requestId: number): Promise<void> {
  sourceController?.abort()
  sourceController = new AbortController()
  try {
    const result = await api.listSourceOptions(term, sourceController.signal)
    if (requestId === sourceRequestId) sources.value = result
  } catch (e) {
    if (!(e instanceof DOMException && e.name === 'AbortError')) ElMessage.error(message(e))
  } finally {
    if (requestId === sourceRequestId) sourceLoading.value = false
  }
}
function searchSources(term: string): void {
  if (sourceSearchTimer) window.clearTimeout(sourceSearchTimer)
  sourceController?.abort()
  const requestId = ++sourceRequestId
  sourceLoading.value = true
  sourceSearchTimer = window.setTimeout(() => void runSourceSearch(term, requestId), 250)
}
function reset(provider: DatabaseProviderType = 'Oracle'): void {
  Object.assign(form, {
    databaseSourceId: null,
    name: '',
    providerType: provider,
    host: '',
    port: provider === 'Oracle' ? 1521 : 5432,
    databaseName: '',
    serviceName: '',
    username: '',
    includedSchemasText: '',
    isEnabled: true,
    password: '',
  })
}
function openCreate(): void {
  clearFieldErrors()
  editing.value = null
  reset()
  overlayStore.openDialog({
    kind: 'database-discovery-connection-profile',
    id: null,
    mode: 'create',
  })
}
function openEdit(profile: ConnectionProfile): void {
  clearFieldErrors()
  editing.value = profile
  Object.assign(form, {
    databaseSourceId: profile.databaseSourceId,
    name: profile.name,
    providerType: profile.providerType,
    host: profile.host,
    port: profile.port,
    databaseName: profile.databaseName ?? '',
    serviceName: profile.serviceName ?? '',
    username: profile.username,
    includedSchemasText: profile.includedSchemas.join('\n'),
    isEnabled: profile.isEnabled,
  })
  overlayStore.openDialog({
    kind: 'database-discovery-connection-profile',
    id: profile.id,
    mode: 'edit',
  })
}
function providerChanged(): void {
  form.port = form.providerType === 'Oracle' ? 1521 : 5432
  form.databaseSourceId = null
  form.databaseName = ''
  form.serviceName = ''
}
function payload() {
  const includedSchemas = form.includedSchemasText
    .split(/[\n,]/)
    .map((x) => x.trim())
    .filter(Boolean)
  return {
    databaseSourceId: form.databaseSourceId ?? 0,
    name: form.name.trim(),
    providerType: form.providerType,
    host: form.host.trim(),
    port: form.port,
    databaseName: form.providerType === 'PostgreSql' ? form.databaseName.trim() : null,
    serviceName: form.providerType === 'Oracle' ? form.serviceName.trim() : null,
    authenticationMode: 'UsernamePassword' as const,
    username: form.username.trim(),
    providerSpecificOptions: { version: 1 as const },
    includedSchemas,
    isEnabled: form.isEnabled,
  }
}
async function save(): Promise<void> {
  if (!validate()) return
  saving.value = true
  testResult.value = ''
  try {
    if (editing.value)
      await api.updateProfile(editing.value.id, {
        ...payload(),
        concurrencyToken: editing.value.concurrencyToken,
      })
    else {
      const created = await api.createProfile(payload())
      if (form.password) {
        try {
          await api.setSecret(created, form.password)
        } catch (secretError) {
          closeOwnedDialog()
          ElMessage.warning(
            `连接配置已创建，但密码保存失败：${message(secretError)}。请在列表中重新设置密码。`,
          )
          await load()
          return
        } finally {
          form.password = ''
        }
      }
    }
    closeOwnedDialog()
    ElMessage.success(editing.value ? '连接配置已更新。' : '连接配置及密码已创建。')
    await load()
  } catch (e) {
    form.password = ''
    await handleOperationError(e)
  } finally {
    saving.value = false
  }
}
function openSecret(profile: ConnectionProfile): void {
  secretProfile.value = profile
  password.value = ''
  overlayStore.openDialog({
    kind: 'database-discovery-connection-secret',
    id: profile.id,
    mode: 'edit',
  })
}
async function saveSecret(): Promise<void> {
  if (!secretProfile.value || !password.value) return
  saving.value = true
  testResult.value = ''
  try {
    if (secretProfile.value.hasSecret) await api.replaceSecret(secretProfile.value, password.value)
    else await api.setSecret(secretProfile.value, password.value)
    password.value = ''
    closeOwnedDialog()
    ElMessage.success('连接密码已安全保存。')
    await load()
  } catch (e) {
    await handleOperationError(e)
  } finally {
    password.value = ''
    saving.value = false
  }
}
async function clear(profile: ConnectionProfile): Promise<void> {
  try {
    await ElMessageBox.confirm(
      '清除后，该连接将不能测试连接或执行发现；如需恢复，必须重新设置密码。',
      '确认清除连接密码',
      {
        type: 'warning',
        confirmButtonText: '清除密码',
        cancelButtonText: '取消',
        confirmButtonClass: 'el-button--danger',
      },
    )
    testResult.value = ''
    await api.clearSecret(profile)
    ElMessage.success('连接密码已清除。')
    await load()
  } catch (e) {
    if (e !== 'cancel' && e !== 'close') await handleOperationError(e)
  }
}
async function toggle(profile: ConnectionProfile): Promise<void> {
  try {
    testResult.value = ''
    await api.setProfileEnabled(profile, !profile.isEnabled)
    await load()
  } catch (e) {
    await handleOperationError(e)
  }
}
async function test(profile: ConnectionProfile): Promise<void> {
  if (actionKey.value) return
  actionKey.value = `test-${profile.id}`
  testResult.value = ''
  try {
    const result = await api.testConnection(profile)
    const target = result.serviceName ?? result.databaseName
    testResult.value = [result.summary, result.providerVersion, target, result.containerName]
      .filter((item): item is string => Boolean(item))
      .join(' · ')
    ElMessage.success('连接测试成功。')
    await load()
  } catch (e) {
    testResult.value = e instanceof ApiError ? `${e.response.code} · ${e.message}` : message(e)
    if (e instanceof ApiError && e.status === 409) await handleOperationError(e)
    else ElMessage.error(testResult.value)
  } finally {
    actionKey.value = ''
  }
}
async function trigger(profile: ConnectionProfile): Promise<void> {
  if (actionKey.value) return
  actionKey.value = `trigger-${profile.id}`
  try {
    const run = await api.triggerDiscovery(profile)
    ElMessage.success('发现任务已进入队列。')
    await router.push({ name: 'database-discovery-runs', query: { runId: String(run.id) } })
  } catch (e) {
    await handleOperationError(e)
  } finally {
    actionKey.value = ''
  }
}
function openHistory(profile: ConnectionProfile): void {
  void router.push({ name: 'database-discovery-runs', query: { profileId: String(profile.id) } })
}
type ProfileMoreCommand = 'edit' | 'history' | 'secret' | 'toggle' | 'clear'
function handleMoreCommand(profile: ConnectionProfile, command: ProfileMoreCommand): void {
  if (command === 'edit') openEdit(profile)
  else if (command === 'history') openHistory(profile)
  else if (command === 'secret') openSecret(profile)
  else if (command === 'toggle') void toggle(profile)
  else if (command === 'clear') void clear(profile)
}
onMounted(load)
watch(profileDialogOpen, (open, previous) => {
  if (!open && previous) form.password = ''
})
watch(secretDialogOpen, (open, previous) => {
  if (!open && previous) password.value = ''
})
onBeforeUnmount(() => {
  sourceController?.abort()
  if (sourceSearchTimer) window.clearTimeout(sourceSearchTimer)
  sourceRequestId += 1
  closeOwnedDialog()
  password.value = ''
  form.password = ''
})
</script>
<template>
  <main class="discovery-page skh-page">
    <header class="discovery-page__header skh-page-header">
      <div>
        <small class="discovery-eyebrow">数据库 / 数据库发现</small>
        <h1>数据库发现</h1>
        <p>管理安全连接并发起可审查的元数据发现。</p>
      </div>
      <div class="skh-page-header__actions">
        <el-button
          v-if="actorStore.isAdministrator"
          class="skh-page-primary-action"
          type="primary"
          :icon="Plus"
          @click="openCreate"
          >新增数据库连接</el-button
        >
      </div>
    </header>

    <DiscoverySectionNav />
    <el-alert title="可见性提示" type="warning" :closable="false" show-icon>
      发现结果只代表当前账号对配置范围的可见内容；权限不足可能造成对象显示为“来源中未发现”（DBDISC-GAP-004）。
    </el-alert>
    <p v-if="testResult" class="discovery-result" role="status">{{ testResult }}</p>

    <LoadingState v-if="loading && !initialized" message="正在读取数据库连接配置…" />
    <ErrorState
      v-else-if="error && !initialized"
      title="数据库连接配置加载失败"
      :message="error"
      @retry="load"
    />
    <section v-else class="discovery-table-section skh-table-section" :aria-busy="loading">
      <EmptyState
        v-if="profiles.length === 0"
        title="尚未配置数据库连接"
        description="新增连接后，可以安全设置密码、测试连接并开始发现。"
      />
      <el-table
        v-else
        :data="profiles"
        row-key="id"
        class="discovery-table skh-data-table skh-data-table--comfortable"
      >
        <el-table-column prop="name" label="名称" min-width="150" show-overflow-tooltip />
        <el-table-column label="数据库来源" min-width="170" show-overflow-tooltip>
          <template #default="{ row }">{{ sourceName(row) }}</template>
        </el-table-column>
        <el-table-column label="数据库类型" width="108">
          <template #default="{ row }">{{ providerLabel(row.providerType) }}</template>
        </el-table-column>
        <el-table-column label="连接目标" min-width="210" show-overflow-tooltip>
          <template #default="{ row }">
            <span class="technical-text"
              >{{ row.host }}:{{ row.port }} / {{ row.serviceName ?? row.databaseName }}</span
            >
          </template>
        </el-table-column>
        <el-table-column prop="username" label="用户名" min-width="120" show-overflow-tooltip />
        <el-table-column label="架构（Schema）" min-width="170" show-overflow-tooltip>
          <template #default="{ row }"
            ><span class="technical-text">{{ row.includedSchemas.join(', ') }}</span></template
          >
        </el-table-column>
        <el-table-column label="状态" width="178">
          <template #default="{ row }">
            <div class="discovery-tags">
              <el-tag :type="row.isEnabled ? 'success' : 'info'">{{
                row.isEnabled ? '启用' : '停用'
              }}</el-tag>
              <el-tag
                :type="
                  row.connectionStatus === 'Succeeded'
                    ? 'success'
                    : row.connectionStatus === 'Failed'
                      ? 'danger'
                      : 'info'
                "
                >{{ connectionStatusText(row.connectionStatus) }}</el-tag
              >
              <el-tag :type="row.hasSecret ? 'success' : 'warning'">{{
                row.hasSecret ? '已设置密码' : '未设置密码'
              }}</el-tag>
            </div>
          </template>
        </el-table-column>
        <el-table-column label="最近活动" min-width="190">
          <template #default="{ row }">
            <small>测试 {{ format(row.lastConnectionTestAt) }}</small
            ><br />
            <small>发现尝试 {{ format(row.lastDiscoveryAt) }}</small
            ><br />
            <small>发现成功 {{ format(row.lastSuccessfulDiscoveryAt) }}</small>
          </template>
        </el-table-column>
        <el-table-column label="操作" min-width="300" fixed="right">
          <template #default="{ row }">
            <div v-if="actorStore.isAdministrator" class="discovery-actions">
              <el-button
                size="small"
                type="primary"
                :icon="VideoPlay"
                :disabled="!row.isEnabled || !row.hasSecret"
                :loading="actionKey === `trigger-${row.id}`"
                @click="trigger(row)"
                >开始发现</el-button
              >
              <el-button
                size="small"
                :icon="Refresh"
                :disabled="!row.isEnabled || !row.hasSecret"
                :loading="actionKey === `test-${row.id}`"
                @click="test(row)"
                >测试连接</el-button
              >
              <el-dropdown
                trigger="click"
                @command="handleMoreCommand(row, $event as ProfileMoreCommand)"
              >
                <el-button size="small"
                  >更多<el-icon class="el-icon--right"><ArrowDown /></el-icon
                ></el-button>
                <template #dropdown>
                  <el-dropdown-menu>
                    <el-dropdown-item
                      v-if="!row.isEnabled"
                      command="toggle"
                      class="discovery-dropdown-enable"
                      >启用连接</el-dropdown-item
                    >
                    <el-dropdown-item command="edit">编辑连接</el-dropdown-item>
                    <el-dropdown-item command="history">运行历史</el-dropdown-item>
                    <el-dropdown-item command="secret">{{
                      row.hasSecret ? '替换密码' : '设置密码'
                    }}</el-dropdown-item>
                    <el-dropdown-item v-if="row.isEnabled" command="toggle"
                      >停用连接</el-dropdown-item
                    >
                    <el-dropdown-item
                      v-if="row.hasSecret"
                      command="clear"
                      divided
                      class="discovery-dropdown-danger"
                      >清除密码</el-dropdown-item
                    >
                  </el-dropdown-menu>
                </template>
              </el-dropdown>
            </div>
          </template>
        </el-table-column>
      </el-table>
      <p v-if="error" class="discovery-inline-error" role="alert">刷新失败：{{ error }}</p>
    </section>

    <Teleport v-if="profileDialogOpen" defer to="#dialog-feature-content">
      <section class="discovery-dialog" aria-labelledby="database-discovery-profile-dialog-title">
        <header class="discovery-dialog__header">
          <div>
            <small>数据库发现</small>
            <h2 id="database-discovery-profile-dialog-title">
              {{ editing ? '编辑数据库连接' : '新增数据库连接' }}
            </h2>
            <p>连接配置与密码分别提交；旧密码不会读取或回显。</p>
          </div>
          <el-button
            text
            circle
            :icon="Close"
            aria-label="关闭数据库连接对话框"
            :disabled="saving"
            @click="closeProfileDialog"
          />
        </header>
        <el-form label-position="top" @submit.prevent>
          <el-form-item label="数据库类型">
            <el-radio-group
              v-model="form.providerType"
              :disabled="!!editing"
              @change="providerChanged"
            >
              <el-radio-button value="Oracle">Oracle</el-radio-button>
              <el-radio-button value="PostgreSql">PostgreSQL</el-radio-button>
            </el-radio-group>
          </el-form-item>
          <el-form-item label="数据库来源" :error="fieldErrors.databaseSourceId" required>
            <el-select
              v-model="form.databaseSourceId"
              filterable
              remote
              :remote-method="searchSources"
              :loading="sourceLoading"
              placeholder="输入名称搜索数据库来源"
              :disabled="!!editing"
              @change="clearFieldError('databaseSourceId')"
            >
              <el-option
                v-for="source in availableSources"
                :key="source.id"
                :value="source.id"
                :label="`${source.systemName} · ${source.name}${source.hasConnectionProfile ? '（已有连接）' : ''}`"
                :disabled="source.hasConnectionProfile && source.id !== editing?.databaseSourceId"
              />
            </el-select>
          </el-form-item>
          <div class="discovery-form-grid">
            <el-form-item label="名称" :error="fieldErrors.name" required
              ><el-input v-model="form.name" @input="clearFieldError('name')"
            /></el-form-item>
            <el-form-item label="主机（Host）" :error="fieldErrors.host" required
              ><el-input
                v-model="form.host"
                class="technical-input"
                @input="clearFieldError('host')"
            /></el-form-item>
            <el-form-item label="端口（Port）" :error="fieldErrors.port" required
              ><el-input-number
                v-model="form.port"
                :min="1"
                :max="65535"
                @change="clearFieldError('port')"
            /></el-form-item>
            <el-form-item
              :label="locatorLabel"
              :error="fieldErrors.serviceName ?? fieldErrors.databaseName"
              required
            >
              <el-input
                v-if="form.providerType === 'Oracle'"
                v-model="form.serviceName"
                class="technical-input"
                @input="clearFieldError('serviceName')"
              />
              <el-input
                v-else
                v-model="form.databaseName"
                class="technical-input"
                @input="clearFieldError('databaseName')"
              />
            </el-form-item>
            <el-form-item label="用户名" :error="fieldErrors.username" required
              ><el-input
                v-model="form.username"
                class="technical-input"
                autocomplete="off"
                @input="clearFieldError('username')"
            /></el-form-item>
            <el-form-item label="启用"
              ><el-switch v-model="form.isEnabled" :disabled="!!editing"
            /></el-form-item>
          </div>
          <el-form-item
            label="包含的架构（Schema，保留大小写，每行一个）"
            :error="fieldErrors.includedSchemas"
            required
          >
            <el-input
              v-model="form.includedSchemasText"
              class="technical-input"
              type="textarea"
              :rows="4"
              @input="clearFieldError('includedSchemas')"
            />
          </el-form-item>
          <el-form-item
            v-if="!editing"
            label="连接密码（通过独立密钥接口保存）"
            :error="fieldErrors.password"
            required
          >
            <el-input
              v-model="form.password"
              type="password"
              show-password
              autocomplete="new-password"
              @input="clearFieldError('password')"
            />
          </el-form-item>
          <p v-if="!editing" class="discovery-hint">
            密码会在配置创建成功后通过独立密钥接口提交，不进入普通配置请求。
          </p>
        </el-form>
        <footer class="discovery-dialog__footer">
          <el-button :disabled="saving" @click="closeProfileDialog">取消</el-button>
          <el-button type="primary" :loading="saving" @click="save">保存</el-button>
        </footer>
      </section>
    </Teleport>

    <Teleport v-if="secretDialogOpen" defer to="#dialog-feature-content">
      <section
        class="discovery-dialog discovery-dialog--compact"
        aria-labelledby="database-discovery-secret-dialog-title"
      >
        <header class="discovery-dialog__header">
          <div>
            <small>写入专用密钥边界</small>
            <h2 id="database-discovery-secret-dialog-title">
              {{ secretProfile?.hasSecret ? '替换连接密码' : '设置连接密码' }}
            </h2>
            <p>旧密码不会回显，密码只通过独立密钥接口提交。</p>
          </div>
          <el-button
            text
            circle
            :icon="Close"
            aria-label="关闭连接密码对话框"
            :disabled="saving"
            @click="closeSecretDialog"
          />
        </header>
        <el-input
          v-model="password"
          type="password"
          show-password
          autocomplete="new-password"
          aria-label="连接密码"
        />
        <footer class="discovery-dialog__footer">
          <el-button :disabled="saving" @click="closeSecretDialog">取消</el-button>
          <el-button type="primary" :disabled="!password" :loading="saving" @click="saveSecret"
            >保存密码</el-button
          >
        </footer>
      </section>
    </Teleport>
  </main>
</template>
