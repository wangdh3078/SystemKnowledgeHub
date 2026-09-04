<script setup lang="ts">
import { watchEffect } from 'vue'
import KnowledgeDocumentMarkdown from '../../knowledge-documents/markdown/KnowledgeDocumentMarkdown.vue'
import type { PortalPageSection, PortalTargetType } from '../api/portalReadContracts'

const props = defineProps<{ section: PortalPageSection }>()

const targetLabels: Readonly<Record<PortalTargetType, string>> = {
  System: '系统',
  BusinessFunction: '业务功能',
  DatabaseObject: '数据库对象',
  KnowledgeDocument: '知识文档',
  Integration: '集成关系',
}
const knownKinds = new Set([
  'Summary',
  'KnowledgeDocumentBody',
  'SystemOverview',
  'BusinessFunctionOverview',
  'DatabaseObjectOverview',
  'IntegrationOverview',
  'DatabaseStructure',
])

watchEffect(() => {
  if (!knownKinds.has(props.section.content.kind)) {
    console.warn('Portal section discriminator is unsupported.', {
      sectionId: props.section.id,
      projectionKind: props.section.projectionKind,
    })
  }
})

function display(value: string | null | undefined): string {
  return value?.trim() || '—'
}

function estimatedRows(value: number | null): string {
  return value === null ? '—' : new Intl.NumberFormat('zh-CN').format(value)
}
</script>

<template>
  <section class="portal-reading-section" :aria-labelledby="`portal-section-${section.id}`">
    <h2 :id="`portal-section-${section.id}`">{{ section.heading }}</h2>

    <div v-if="section.content.kind === 'Summary'" class="portal-summary">
      <span class="portal-type-badge">{{ targetLabels[section.content.targetType] }}</span>
      <h3>{{ section.content.title }}</h3>
      <p>{{ display(section.content.summary) }}</p>
    </div>

    <div v-else-if="section.content.kind === 'KnowledgeDocumentBody'" class="portal-document-body">
      <p class="portal-section-context">
        {{ section.content.documentType }} · {{ section.content.title }}
      </p>
      <KnowledgeDocumentMarkdown :markdown="section.content.bodyMarkdown" />
    </div>

    <dl v-else-if="section.content.kind === 'SystemOverview'" class="portal-definition-grid">
      <div>
        <dt>系统名称</dt>
        <dd>{{ section.content.displayName }}</dd>
      </div>
      <div>
        <dt>系统标识</dt>
        <dd>{{ section.content.name }}</dd>
      </div>
      <div>
        <dt>系统类型</dt>
        <dd>{{ section.content.systemType }}</dd>
      </div>
      <div>
        <dt>生命周期</dt>
        <dd>{{ section.content.lifecycle }}</dd>
      </div>
      <div class="portal-definition-grid__wide">
        <dt>业务说明</dt>
        <dd>{{ display(section.content.purpose) }}</dd>
      </div>
    </dl>

    <dl
      v-else-if="section.content.kind === 'BusinessFunctionOverview'"
      class="portal-definition-grid"
    >
      <div>
        <dt>业务功能</dt>
        <dd>{{ section.content.displayName || section.content.name }}</dd>
      </div>
      <div>
        <dt>所属系统</dt>
        <dd>{{ section.content.systemName }}</dd>
      </div>
      <div>
        <dt>功能类型</dt>
        <dd>{{ section.content.functionType }}</dd>
      </div>
      <div>
        <dt>调用方</dt>
        <dd>{{ display(section.content.callerSummary) }}</dd>
      </div>
      <div>
        <dt>输入</dt>
        <dd>{{ display(section.content.inputDescription) }}</dd>
      </div>
      <div>
        <dt>输出</dt>
        <dd>{{ display(section.content.outputDescription) }}</dd>
      </div>
      <div class="portal-definition-grid__wide">
        <dt>业务说明</dt>
        <dd>{{ display(section.content.purpose) }}</dd>
      </div>
    </dl>

    <dl
      v-else-if="section.content.kind === 'DatabaseObjectOverview'"
      class="portal-definition-grid"
    >
      <div>
        <dt>数据库对象</dt>
        <dd class="portal-technical-text">
          {{ section.content.schemaName }}.{{ section.content.objectName }}
        </dd>
      </div>
      <div>
        <dt>对象类型</dt>
        <dd>{{ section.content.objectType }}</dd>
      </div>
      <div>
        <dt>估算行数</dt>
        <dd>{{ estimatedRows(section.content.estimatedRows) }}</dd>
      </div>
      <div>
        <dt>访问方式</dt>
        <dd>{{ section.content.accessMode }}</dd>
      </div>
      <div class="portal-definition-grid__wide">
        <dt>业务唯一键</dt>
        <dd class="portal-technical-text">
          {{ section.content.businessKeyColumns.join('、') || '—' }}
        </dd>
      </div>
      <div class="portal-definition-grid__wide">
        <dt>业务说明</dt>
        <dd>{{ display(section.content.businessDescription) }}</dd>
      </div>
      <div class="portal-definition-grid__wide">
        <dt>数据库注释</dt>
        <dd>{{ display(section.content.databaseComment) }}</dd>
      </div>
    </dl>

    <dl v-else-if="section.content.kind === 'IntegrationOverview'" class="portal-definition-grid">
      <div>
        <dt>集成名称</dt>
        <dd>{{ section.content.name }}</dd>
      </div>
      <div>
        <dt>集成类型</dt>
        <dd>{{ section.content.integrationType }}</dd>
      </div>
      <div>
        <dt>来源</dt>
        <dd>{{ section.content.sourcePartyName }}</dd>
      </div>
      <div>
        <dt>目标</dt>
        <dd>{{ section.content.targetPartyName }}</dd>
      </div>
      <div>
        <dt>方向</dt>
        <dd>{{ section.content.flowDirection }}</dd>
      </div>
      <div class="portal-definition-grid__wide">
        <dt>业务说明</dt>
        <dd>{{ display(section.content.purpose) }}</dd>
      </div>
    </dl>

    <div v-else-if="section.content.kind === 'DatabaseStructure'" class="portal-database-structure">
      <dl class="portal-definition-grid">
        <div>
          <dt>数据库对象</dt>
          <dd class="portal-technical-text">
            {{ section.content.schemaName }}.{{ section.content.objectName }}
          </dd>
        </div>
        <div>
          <dt>对象类型</dt>
          <dd>{{ section.content.objectType }}</dd>
        </div>
        <div>
          <dt>估算行数</dt>
          <dd>{{ estimatedRows(section.content.estimatedRows) }}</dd>
        </div>
        <div>
          <dt>访问方式</dt>
          <dd>{{ section.content.accessMode }}</dd>
        </div>
        <div class="portal-definition-grid__wide">
          <dt>业务唯一键</dt>
          <dd class="portal-technical-text">
            {{ section.content.businessKeyColumns.join('、') || '—' }}
          </dd>
        </div>
        <div class="portal-definition-grid__wide">
          <dt>业务说明</dt>
          <dd>{{ display(section.content.businessDescription) }}</dd>
        </div>
        <div class="portal-definition-grid__wide">
          <dt>数据库注释</dt>
          <dd>{{ display(section.content.databaseComment) }}</dd>
        </div>
      </dl>
      <div class="portal-table-wrap" tabindex="0" aria-label="数据库字段结构，可横向滚动">
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
              v-for="column in section.content.columns"
              :key="`${column.ordinal}-${column.columnName}`"
            >
              <td>{{ column.ordinal }}</td>
              <td class="portal-technical-text">{{ column.columnName }}</td>
              <td class="portal-technical-text">{{ column.nativeDataType }}</td>
              <td>{{ column.nullable ? '是' : '否' }}</td>
              <td>{{ display(column.databaseComment) }}</td>
            </tr>
          </tbody>
        </table>
      </div>
    </div>

    <p v-else class="portal-section-unsupported" role="status">该内容暂不可显示</p>
  </section>
</template>
