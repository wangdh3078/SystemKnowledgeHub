import { computed, ref } from 'vue'
import { ElMessageBox } from 'element-plus'

const dirty = ref(false)

export const hasDirtyDrawer = computed(() => dirty.value)

export function markDrawerDirty(): void {
  dirty.value = true
}

export function resetDrawerDirty(): void {
  dirty.value = false
}

export async function confirmDrawerDiscard(): Promise<boolean> {
  if (!dirty.value) return true

  try {
    await ElMessageBox.confirm('尚有未保存的修改，确认放弃？', '放弃编辑', {
      confirmButtonText: '放弃修改',
      cancelButtonText: '继续编辑',
      type: 'warning',
    })
    return true
  } catch {
    return false
  }
}
