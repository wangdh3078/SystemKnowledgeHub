<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Close, Delete } from '@element-plus/icons-vue'
import { ElMessage } from 'element-plus'
import { ApiError } from '../../../api/errors/ApiError'
import { useOverlayStore } from '../../../app/stores/overlays'
import {
  readDeleteBlockers,
  readDeleteDialogPayload,
  type DeleteDependencyBlocker,
} from '../deleteDialog'

const overlays = useOverlayStore()
const payload = computed(() => readDeleteDialogPayload(overlays.currentDialog?.payload))
const submitting = ref(false)
const error = ref<string | null>(null)
const blockers = ref<readonly DeleteDependencyBlocker[]>([])

watch(() => overlays.currentDialog, () => {
  submitting.value = false
  error.value = null
  blockers.value = []
})

function close(): void {
  if (!submitting.value) overlays.closeDialog()
}

function validationMessage(reason: ApiError): string {
  const messages = Object.values(reason.response.fieldErrors ?? {}).flat()
  return messages[0] ?? reason.message
}

async function confirmDelete(): Promise<void> {
  const current = payload.value
  if (!current || submitting.value) return
  submitting.value = true
  error.value = null
  try {
    await current.execute()
    overlays.closeDialog()
    ElMessage.success(`已删除“${current.displayName}”`)
    await current.onDeleted()
  } catch (reason: unknown) {
    if (reason instanceof ApiError) {
      if (reason.status === 422 && reason.response.code === 'business_rule_violation') {
        blockers.value = readDeleteBlockers(reason.response.details)
        error.value = blockers.value.length === 0 ? reason.message : null
        return
      }
      if (reason.status === 403) {
        overlays.closeDialog()
        ElMessage.error('你没有权限删除此对象')
        await current.onRefresh()
        return
      }
      if (reason.status === 404) {
        overlays.closeDialog()
        ElMessage.warning('该对象已不存在或当前不可访问')
        await current.onUnavailable()
        return
      }
      if (reason.status === 409) {
        overlays.closeDialog()
        ElMessage.warning('数据已发生变化，请刷新后重试。')
        await current.onRefresh()
        return
      }
      error.value = reason.status === 400 ? validationMessage(reason) : reason.message
      return
    }
    error.value = reason instanceof Error ? reason.message : '删除请求失败，请检查网络后重试。'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <Teleport v-if="overlays.currentDialog?.kind === 'delete-root' && payload" defer to="#dialog-feature-content">
    <section class="delete-confirmation" aria-labelledby="delete-confirmation-title">
      <header class="delete-confirmation__header">
        <el-icon><Delete /></el-icon>
        <div>
          <small>{{ payload.objectTypeLabel }}</small>
          <h2 id="delete-confirmation-title">{{ blockers.length ? '无法删除，仍存在依赖项' : payload.actionLabel }}</h2>
        </div>
        <el-button text circle :icon="Close" aria-label="关闭删除对话框" :disabled="submitting" @click="close" />
      </header>

      <template v-if="blockers.length">
        <p>请先在对应功能中处理这些依赖项后再重试。</p>
        <ul class="delete-confirmation__blockers" aria-label="删除依赖项">
          <li v-for="item in blockers" :key="item.dependencyType">
            <span>{{ item.displayName }}</span><strong>{{ item.count }}</strong>
          </li>
        </ul>
      </template>
      <template v-else>
        <p>确认删除“<strong>{{ payload.displayName }}</strong>”？</p>
        <p>删除后将从列表、搜索及当前知识视图中隐藏。</p>
        <p class="delete-confirmation__recovery">系统不提供页面恢复功能。</p>
      </template>

      <el-alert v-if="error" type="error" :title="error" :closable="false" show-icon />
      <footer>
        <el-button :disabled="submitting" @click="close">{{ blockers.length ? '关闭' : '取消' }}</el-button>
        <el-button type="danger" :loading="submitting" @click="confirmDelete">
          {{ blockers.length ? '重新尝试删除' : '确认删除' }}
        </el-button>
      </footer>
    </section>
  </Teleport>
</template>

<style scoped>
.delete-confirmation { display: grid; gap: var(--space-4); }
.delete-confirmation__header { display: grid; grid-template-columns: auto minmax(0, 1fr) auto; gap: var(--space-3); align-items: start; }
.delete-confirmation__header > .el-icon { margin-top: 3px; color: var(--el-color-danger); }
.delete-confirmation__header small { color: var(--color-text-muted); }
.delete-confirmation__header h2 { margin: 2px 0 0; font-size: 20px; }
.delete-confirmation p { margin: 0; line-height: 1.6; }
.delete-confirmation__recovery { color: var(--color-text-muted); }
.delete-confirmation__blockers { display: grid; gap: var(--space-2); margin: 0; padding: 0; list-style: none; }
.delete-confirmation__blockers li { display: flex; justify-content: space-between; gap: var(--space-4); padding: 10px 12px; border: 1px solid var(--color-border); border-radius: var(--radius-md); }
.delete-confirmation__blockers strong { color: var(--el-color-danger); }
.delete-confirmation footer { display: flex; justify-content: flex-end; gap: var(--space-2); padding-top: var(--space-2); }
@media (max-width: 620px) { .delete-confirmation footer { flex-wrap: wrap; } }
</style>
