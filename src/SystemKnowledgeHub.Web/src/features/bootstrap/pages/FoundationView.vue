<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref } from 'vue'
import { CircleCheck, Connection, DataBoard, DocumentChecked } from '@element-plus/icons-vue'
import { getBootstrapStatus, type BootstrapStatus } from '../../../app/bootstrap/bootstrapApi'
import { useOverlayStore } from '../../../app/stores/overlays'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'

const overlayStore = useOverlayStore()
const status = ref<BootstrapStatus | null>(null)
const errorMessage = ref<string | null>(null)
const loading = ref(false)
let activeRequest: AbortController | null = null

const backendReady = computed(() => status.value?.status === 'ok')
const databaseProvider = computed(() => status.value?.databaseProvider ?? '等待连接')

async function loadStatus(): Promise<void> {
  activeRequest?.abort()
  activeRequest = new AbortController()
  loading.value = true
  errorMessage.value = null

  try {
    status.value = await getBootstrapStatus(activeRequest.signal)
  } catch (error: unknown) {
    if (error instanceof DOMException && error.name === 'AbortError') {
      return
    }

    errorMessage.value = error instanceof Error ? error.message : '发生未知错误。'
  } finally {
    loading.value = false
  }
}

function openDrawerHost(): void {
  overlayStore.openDrawer({
    kind: '详情抽屉宿主',
    id: null,
    mode: 'read',
  })
}

function openDialogHost(): void {
  overlayStore.openDialog({
    kind: '对话框宿主',
    id: null,
    mode: 'create',
  })
}

onMounted(loadStatus)
onBeforeUnmount(() => activeRequest?.abort())
</script>

<template>
  <div class="foundation-view">
    <header class="foundation-view__header">
      <div>
        <p class="foundation-view__breadcrumb">系统知识中心 / 实现基础</p>
        <h1>企业应用基础工程</h1>
        <p class="foundation-view__summary">
          当前页面只验证 Application Shell 与前后端基础链路，不是正式总览页面。
        </p>
      </div>
      <div class="foundation-view__actions">
        <el-button @click="openDialogHost">验证对话框</el-button>
        <el-button :icon="DocumentChecked" @click="openDrawerHost"> 验证详情抽屉 </el-button>
      </div>
    </header>

    <section class="foundation-view__section" aria-labelledby="readiness-title">
      <div class="section-heading">
        <div>
          <span class="section-heading__eyebrow">运行状态</span>
          <h2 id="readiness-title">基础链路就绪情况</h2>
        </div>
        <el-tag type="success" effect="plain" round>基础工程</el-tag>
      </div>

      <LoadingState v-if="loading" />
      <ErrorState v-else-if="errorMessage" :message="errorMessage" @retry="loadStatus" />

      <div v-else class="readiness-grid">
        <article class="readiness-item readiness-item--ready">
          <div class="readiness-item__icon"><DataBoard /></div>
          <div>
            <span>前端</span>
            <strong>就绪</strong>
            <small>Vue 3 · TypeScript · Vite</small>
          </div>
          <el-icon color="var(--status-confirmed)"><CircleCheck /></el-icon>
        </article>

        <article class="readiness-item" :class="{ 'readiness-item--ready': backendReady }">
          <div class="readiness-item__icon"><Connection /></div>
          <div>
            <span>后端</span>
            <strong>{{ backendReady ? '就绪' : '等待中' }}</strong>
            <small>Vite 代理 → ASP.NET Core 控制器</small>
          </div>
          <el-icon v-if="backendReady" color="var(--status-confirmed)">
            <CircleCheck />
          </el-icon>
        </article>

        <article class="readiness-item" :class="{ 'readiness-item--ready': backendReady }">
          <div class="readiness-item__icon"><DocumentChecked /></div>
          <div>
            <span>数据库提供方</span>
            <strong class="technical-text">{{ databaseProvider }}</strong>
            <small>真实 EF Core SQLite 提供方</small>
          </div>
          <el-icon v-if="backendReady" color="var(--status-confirmed)">
            <CircleCheck />
          </el-icon>
        </article>
      </div>
    </section>

    <section class="foundation-view__section foundation-view__section--split">
      <div>
        <div class="section-heading">
          <div>
            <span class="section-heading__eyebrow">设计基线</span>
            <h2>共用工作区结构</h2>
          </div>
        </div>
        <dl class="foundation-view__facts">
          <div>
            <dt>主内容</dt>
            <dd>当前对象本身是什么</dd>
          </div>
          <div>
            <dt>关系与缺口</dt>
            <dd>当前对象与什么有关、还缺什么</dd>
          </div>
          <div>
            <dt>详情抽屉</dt>
            <dd>当前选中子对象的详细知识</dd>
          </div>
        </dl>
      </div>

      <EmptyState />
    </section>
  </div>
</template>
