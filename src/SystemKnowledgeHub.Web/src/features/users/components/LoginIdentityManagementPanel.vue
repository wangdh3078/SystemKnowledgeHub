<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { LoginIdentity } from '../api/userContracts'
import { createLoginIdentity, getLoginIdentities, setLoginIdentityActiveState } from '../api/usersApi'

const props = defineProps<{
  userId: number
  setupAvailable: boolean
  approvedProvider: string | null
  globallyEnabled: boolean
}>()
const emit = defineEmits<{ changed: [] }>()
const identities = ref<readonly LoginIdentity[]>([])
const loading = ref(true)
const submitting = ref(false)
const showCreate = ref(false)
const error = ref<string | null>(null)
const form = reactive({ subject: '' })

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try { identities.value = await getLoginIdentities(props.userId) }
  catch (requestError: unknown) { error.value = requestError instanceof Error ? requestError.message : '企业统一登录映射加载失败。' }
  finally { loading.value = false }
}

async function create(): Promise<void> {
  if (!props.setupAvailable || !props.approvedProvider || !form.subject.trim() || submitting.value) return
  submitting.value = true
  try {
    await createLoginIdentity(props.userId, {
      provider: props.approvedProvider,
      subject: form.subject,
    })
    form.subject = ''
    showCreate.value = false
    ElMessage.success('企业统一登录映射已添加。')
    await load()
    emit('changed')
  } catch (requestError: unknown) {
    ElMessage.error(requestError instanceof Error ? requestError.message : '企业统一登录映射创建失败。')
  } finally { submitting.value = false }
}

async function toggle(identity: LoginIdentity): Promise<void> {
  if (submitting.value) return
  submitting.value = true
  try {
    await setLoginIdentityActiveState(props.userId, identity, !identity.isActive)
    ElMessage.success(identity.isActive ? '企业统一登录映射已停用。' : '企业统一登录映射已启用。')
    await load()
    emit('changed')
  } catch (requestError: unknown) {
    ElMessage.error(requestError instanceof Error ? requestError.message : '企业统一登录映射更新失败。')
    await load()
  } finally { submitting.value = false }
}

onMounted(() => void load())
</script>

<template>
  <article class="login-identities">
    <div class="login-identities__heading">
      <div><h4>企业统一登录（OIDC / SSO）</h4><p>使用身份提供方的稳定 Provider 与 Subject / sub 显式映射，不根据姓名、邮箱、工号或用户名自动绑定。</p></div>
      <el-tag v-if="identities.length" size="small" type="success">已配置 {{ identities.length }} 项</el-tag>
      <el-tag v-else size="small" type="info">未配置</el-tag>
    </div>
    <p v-if="loading" class="user-drawer__help">正在读取企业统一登录映射…</p>
    <el-alert v-else-if="error" type="error" :title="error" :closable="false" show-icon><template #default><el-button size="small" @click="load">重试</el-button></template></el-alert>
    <template v-else>
      <el-alert v-if="!globallyEnabled" type="warning" title="当前部署未启用企业统一登录" :closable="false" show-icon />
      <div v-if="identities.length" class="login-identities__list">
        <div v-for="identity in identities" :key="identity.id" class="login-identities__item">
          <dl>
            <div><dt>Provider</dt><dd class="technical-text">{{ identity.provider }}</dd></div>
            <div><dt>Subject / sub</dt><dd class="technical-text">{{ identity.subject }}</dd></div>
            <div><dt>映射状态</dt><dd>{{ identity.isActive ? '启用' : '停用' }}</dd></div>
            <div><dt>当前部署可用</dt><dd>{{ globallyEnabled && identity.provider === approvedProvider ? '是' : '否' }}</dd></div>
          </dl>
          <el-button
            :type="identity.isActive ? 'danger' : 'success'"
            plain
            :loading="submitting"
            @click="toggle(identity)"
          >{{ identity.isActive ? '停用' : '启用' }}</el-button>
        </div>
      </div>
      <p v-else class="user-drawer__help">尚未配置企业统一登录；现有 User 不会根据个人资料自动绑定外部身份。</p>

      <el-button v-if="setupAvailable && !showCreate" type="primary" plain @click="showCreate = true">添加企业统一登录</el-button>
      <div v-else-if="setupAvailable && approvedProvider" class="login-identities__create">
        <el-form-item label="Provider" required>
          <el-input :model-value="approvedProvider" readonly class="technical-input" />
        </el-form-item>
        <el-form-item label="Subject / sub" required>
          <el-input v-model="form.subject" maxlength="240" class="technical-input" placeholder="由身份提供方提供的稳定标识" />
        </el-form-item>
        <div class="user-login-methods__actions">
          <el-button @click="showCreate = false">取消</el-button>
          <el-button type="primary" :loading="submitting" :disabled="!form.subject.trim()" @click="create">确认添加</el-button>
        </div>
      </div>
      <p v-else-if="!setupAvailable" class="user-drawer__help">服务器未配置可用的身份提供方，因此不能添加新的企业统一登录映射。</p>
    </template>
  </article>
</template>
