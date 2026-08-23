<script setup lang="ts">
import { computed, reactive, ref, watch } from 'vue'
import { useRouter } from 'vue-router'
import { createKnowledgeDocument } from '../api/knowledgeDocumentsApi'
import {
  documentTypeLabels,
  documentTypes,
  type DocumentType,
} from '../api/knowledgeDocumentContracts'
import { documentTemplates } from '../documentTemplates'

const props = defineProps<{ readonly open: boolean }>()
const emit = defineEmits<{ close: []; created: [] }>()
const router = useRouter()
const submitting = ref(false)
const error = ref<string | null>(null)
const form = reactive({
  documentType: 'KnowledgeArticle' as DocumentType,
  title: '',
  summary: '',
  bodyMarkdown: documentTemplates.KnowledgeArticle,
})
const typeOptions = computed(() =>
  documentTypes.map((value) => ({ value, label: documentTypeLabels[value] })),
)

watch(
  () => props.open,
  (open) => {
    if (!open) return
    error.value = null
    form.documentType = 'KnowledgeArticle'
    form.title = ''
    form.summary = ''
    form.bodyMarkdown = documentTemplates.KnowledgeArticle
  },
)

function selectType(next: DocumentType): void {
  form.documentType = next
  form.bodyMarkdown = documentTemplates[next]
}
async function submit(): Promise<void> {
  if (!form.title.trim()) {
    error.value = '请填写文档标题。'
    return
  }
  submitting.value = true
  error.value = null
  try {
    const document = await createKnowledgeDocument({
      documentType: form.documentType,
      title: form.title,
      summary: form.summary.trim() || null,
      bodyMarkdown: form.bodyMarkdown,
    })
    emit('created')
    emit('close')
    await router.push({ name: 'knowledge-document-detail', params: { id: String(document.id) } })
  } catch (reason: unknown) {
    error.value = reason instanceof Error ? reason.message : '创建知识内容失败，请重试。'
  } finally {
    submitting.value = false
  }
}
</script>

<template>
  <el-dialog
    :model-value="open"
    title="新建知识内容"
    width="700px"
    :close-on-click-modal="false"
    @close="emit('close')"
  >
    <p class="knowledge-document-create__hint">
      选择类型后会载入对应 Markdown 模板；本阶段只创建草稿，不在此编辑正文。
    </p>
    <el-form label-position="top" class="knowledge-document-create__form">
      <el-form-item label="文档类型">
        <el-select :model-value="form.documentType" @update:model-value="selectType">
          <el-option
            v-for="item in typeOptions"
            :key="item.value"
            :label="item.label"
            :value="item.value"
          />
        </el-select>
      </el-form-item>
      <el-form-item label="标题" required
        ><el-input v-model="form.title" maxlength="300" show-word-limit
      /></el-form-item>
      <el-form-item label="摘要"
        ><el-input
          v-model="form.summary"
          type="textarea"
          :rows="2"
          maxlength="2000"
          show-word-limit
      /></el-form-item>
      <el-form-item label="Markdown 模板"
        ><el-input v-model="form.bodyMarkdown" type="textarea" :rows="12"
      /></el-form-item>
    </el-form>
    <p v-if="error" class="knowledge-document-error">{{ error }}</p>
    <template #footer
      ><el-button @click="emit('close')">取消</el-button
      ><el-button type="primary" :loading="submitting" @click="submit"
        >创建草稿</el-button
      ></template
    >
  </el-dialog>
</template>
