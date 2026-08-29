<script setup lang="ts">
import { onMounted, reactive, ref } from 'vue'
import { ElMessage } from 'element-plus'
import type { LoginIdentity } from '../api/userContracts'
import { createLoginIdentity, getLoginIdentities, setLoginIdentityActiveState } from '../api/usersApi'

const props = defineProps<{ userId: number }>()
const identities = ref<readonly LoginIdentity[]>([])
const loading = ref(true)
const submitting = ref(false)
const error = ref<string | null>(null)
const form = reactive({ provider: '', subject: '' })

async function load(): Promise<void> {
  loading.value = true
  error.value = null
  try { identities.value = await getLoginIdentities(props.userId) }
  catch (requestError: unknown) { error.value = requestError instanceof Error ? requestError.message : '登录映射加载失败。' }
  finally { loading.value = false }
}

async function create(): Promise<void> {
  if (!form.provider.trim() || !form.subject.trim() || submitting.value) return
  submitting.value = true
  try {
    await createLoginIdentity(props.userId, { provider: form.provider.trim(), subject: form.subject.trim() })
    form.provider = ''
    form.subject = ''
    ElMessage.success('登录映射已添加。')
    await load()
  } catch (requestError: unknown) {
    ElMessage.error(requestError instanceof Error ? requestError.message : '登录映射创建失败。')
  } finally { submitting.value = false }
}

async function toggle(identity: LoginIdentity): Promise<void> {
  try {
    await setLoginIdentityActiveState(props.userId, identity, !identity.isActive)
    ElMessage.success(identity.isActive ? '登录映射已停用。' : '登录映射已启用。')
    await load()
  } catch (requestError: unknown) {
    ElMessage.error(requestError instanceof Error ? requestError.message : '登录映射更新失败。')
  }
}

onMounted(() => void load())
</script>

<template>
  <section class="user-drawer__section login-identities">
    <div class="user-drawer__section-title"><span>03</span><div><h3>登录身份映射（OIDC / SSO）</h3><p>用于把企业身份提供方的稳定登录身份映射到当前用户，不是知识身份或权限角色。</p></div></div>
    <p v-if="loading" class="user-drawer__help">正在读取登录映射…</p>
    <el-alert v-else-if="error" type="error" :title="error" :closable="false" show-icon><template #default><el-button size="small" @click="load">重试</el-button></template></el-alert>
    <template v-else>
      <div v-if="identities.length" class="login-identities__list">
        <div v-for="identity in identities" :key="identity.id" class="login-identities__item">
          <div><strong>{{ identity.provider }}</strong><span class="technical-text">{{ identity.subject }}</span></div>
          <el-button text :type="identity.isActive ? 'danger' : 'success'" @click="toggle(identity)">{{ identity.isActive ? '停用' : '启用' }}</el-button>
        </div>
      </div>
      <p v-else class="user-drawer__help">尚无登录映射；该用户不能通过 OIDC 建立系统会话。</p>
      <div class="login-identities__create">
        <el-input v-model="form.provider" maxlength="120" placeholder="身份提供方标识（使用管理员配置值）" />
        <el-input v-model="form.subject" maxlength="240" placeholder="稳定 Subject / sub（由身份提供方提供）" class="technical-input" />
        <el-button type="primary" :loading="submitting" @click="create">添加映射</el-button>
      </div>
      <p class="user-drawer__help">身份提供方标识必须使用部署中的已配置值；稳定 Subject / sub 必须复制身份提供方给出的值，不能用姓名或邮箱代替。</p>
    </template>
  </section>
</template>
