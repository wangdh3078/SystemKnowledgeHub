import { computed, ref } from 'vue'
import { ElMessageBox } from 'element-plus'

export interface DocumentEditSnapshot {
  readonly title: string
  readonly summary: string
  readonly bodyMarkdown: string
}

const activeDirtyDocumentEdit = ref(false)

export const hasActiveDirtyDocumentEdit = computed(() => activeDirtyDocumentEdit.value)

export function setActiveDocumentEditDirty(isDirty: boolean): void {
  activeDirtyDocumentEdit.value = isDirty
}

export async function confirmDocumentEditDiscard(): Promise<boolean> {
  if (!activeDirtyDocumentEdit.value) return true
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

export function isDocumentEditDirty(
  current: DocumentEditSnapshot,
  initial: DocumentEditSnapshot,
): boolean {
  return (
    current.title !== initial.title ||
    current.summary !== initial.summary ||
    current.bodyMarkdown !== initial.bodyMarkdown
  )
}
