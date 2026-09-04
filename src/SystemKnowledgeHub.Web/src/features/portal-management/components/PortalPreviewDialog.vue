<script setup lang="ts">
import KnowledgeDocumentMarkdown from '../../knowledge-documents/markdown/KnowledgeDocumentMarkdown.vue'
import type { PortalPreview, PortalPreviewSection } from '../api/portalManagementContracts'

defineProps<{ modelValue: boolean; preview: PortalPreview | null; loading: boolean }>()
const emit = defineEmits<{ 'update:modelValue': [value: boolean] }>()

function text(content: Readonly<Record<string, unknown>>, key: string): string {
  const value = content[key]
  return typeof value === 'string' && value.trim() ? value : '—'
}

function number(content: Readonly<Record<string, unknown>>, key: string): string {
  const value = content[key]
  return typeof value === 'number' ? new Intl.NumberFormat('zh-CN').format(value) : '—'
}

function columns(section: PortalPreviewSection): readonly Readonly<Record<string, unknown>>[] {
  return Array.isArray(section.content.columns)
    ? section.content.columns.filter(
        (item): item is Readonly<Record<string, unknown>> =>
          item !== null && typeof item === 'object' && !Array.isArray(item),
      )
    : []
}
</script>

<template>
  <el-dialog
    :model-value="modelValue"
    width="min(1160px, 90vw)"
    class="portal-preview-dialog"
    append-to-body
    destroy-on-close
    @close="emit('update:modelValue', false)"
  >
    <template #header>
      <div class="portal-preview-dialog__header">
        <span class="portal-preview-dialog__marker">预览</span
        ><strong>{{ preview?.page?.title ?? '页面预览' }}</strong>
      </div>
    </template>
    <div v-loading="loading" class="portal-preview-dialog__body">
      <section
        v-if="preview && !preview.readiness.canPublish"
        class="portal-preview-blockers"
        aria-label="发布阻塞原因"
      >
        <h3>当前无法完整预览</h3>
        <p v-for="item in preview.readiness.blockers" :key="item.code">{{ item.message }}</p>
      </section>
      <article v-if="preview?.page" class="portal-preview-article">
        <header>
          <p>{{ preview.page.primaryTarget.type }} · {{ preview.page.primaryTarget.title }}</p>
          <h1>{{ preview.page.title }}</h1>
        </header>
        <section
          v-for="section in preview.page.sections"
          :key="section.id"
          class="portal-preview-section"
        >
          <h2>{{ section.heading }}</h2>
          <KnowledgeDocumentMarkdown
            v-if="section.content.kind === 'KnowledgeDocumentBody'"
            :markdown="text(section.content, 'bodyMarkdown')"
          />
          <div
            v-else-if="section.content.kind === 'DatabaseStructure'"
            class="portal-preview-structure"
          >
            <p>
              <strong
                >{{ text(section.content, 'schemaName') }}.{{
                  text(section.content, 'objectName')
                }}</strong
              >
              · {{ text(section.content, 'objectType') }}
            </p>
            <p>{{ text(section.content, 'businessDescription') }}</p>
            <p>
              估算行数：{{ number(section.content, 'estimatedRows') }} · 访问方式：{{
                text(section.content, 'accessMode')
              }}
            </p>
            <div class="portal-preview-table-wrap">
              <table>
                <thead>
                  <tr>
                    <th>序号</th>
                    <th>字段</th>
                    <th>类型</th>
                    <th>可空</th>
                    <th>数据库注释</th>
                  </tr>
                </thead>
                <tbody>
                  <tr
                    v-for="column in columns(section)"
                    :key="`${column.ordinal}-${column.columnName}`"
                  >
                    <td>{{ column.ordinal }}</td>
                    <td>{{ column.columnName }}</td>
                    <td>{{ column.nativeDataType }}</td>
                    <td>{{ column.nullable ? '是' : '否' }}</td>
                    <td>{{ column.databaseComment || '—' }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <dl v-else class="portal-preview-overview">
            <template v-for="(value, key) in section.content" :key="key">
              <template v-if="key !== 'kind' && key !== 'targetId' && !key.endsWith('Id')">
                <dt>{{ key }}</dt>
                <dd>{{ Array.isArray(value) ? value.join('、') : (value ?? '—') }}</dd>
              </template>
            </template>
          </dl>
        </section>
      </article>
    </div>
  </el-dialog>
</template>
