<script setup lang="ts">
import { watchEffect } from 'vue'
import { RouterLink } from 'vue-router'
import KnowledgeDocumentMarkdown from '../../knowledge-documents/markdown/KnowledgeDocumentMarkdown.vue'
import {
  knowledgeDocumentAttachmentDownloadUrl,
  knowledgeDocumentAttachmentPreviewPath,
} from '../../knowledge-documents/api/knowledgeDocumentAttachmentsApi'
import { environment } from '../../../app/config/env'
import { portalAttachmentUrl } from '../api/portalReadApi'
import type {
  PortalKnowledgeStatus,
  PortalPageSection,
  PortalTargetType,
} from '../api/portalReadContracts'

const props = withDefaults(
  defineProps<{ section: PortalPageSection; pageId?: number; previewMode?: boolean }>(),
  { pageId: undefined, previewMode: false },
)

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
  'AttachmentList',
  'TrustSummary',
  'RelatedKnowledge',
  'Traceability',
])

const statusLabels: Readonly<Record<PortalKnowledgeStatus, string>> = {
  Unknown: '未知',
  Inferred: '推断',
  Confirmed: '已确认',
}
const coverageLabels: Readonly<Record<string, string>> = {
  NoConfirmation: '尚未确认',
  LegacyConfirmationUnknown: '历史确认版本未知',
  CurrentRevisionConfirmed: '已覆盖当前版本',
  ChangedSinceConfirmation: '确认后内容已变更',
}
const traceTypeLabels: Readonly<Record<string, string>> = {
  Requirement: '需求',
  Specification: '规格',
  TestCase: '测试用例',
}

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

function fileSize(value: number): string {
  if (value < 1024) return `${value} B`
  if (value < 1024 * 1024) return `${(value / 1024).toFixed(1)} KB`
  return `${(value / 1024 / 1024).toFixed(1)} MB`
}

function attachmentUrl(
  documentId: number,
  attachmentId: number,
  action: 'preview' | 'download',
): string {
  if (!props.previewMode && props.pageId !== undefined)
    return portalAttachmentUrl(props.pageId, attachmentId, action)
  return action === 'download'
    ? knowledgeDocumentAttachmentDownloadUrl(documentId, attachmentId)
    : `${environment.apiBaseUrl}${knowledgeDocumentAttachmentPreviewPath(documentId, attachmentId)}`
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
      <KnowledgeDocumentMarkdown
        :markdown="section.content.bodyMarkdown"
        :attachment-image-context="{
          documentId: section.content.documentId,
          imageAttachmentIds: section.content.imageAttachmentIds ?? [],
          resolveImageUrl:
            !previewMode && pageId !== undefined
              ? (attachmentId: number) => portalAttachmentUrl(pageId!, attachmentId, 'content')
              : undefined,
        }"
      />
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

    <ul v-else-if="section.content.kind === 'AttachmentList'" class="portal-attachment-list">
      <li v-for="attachment in section.content.attachments" :key="attachment.attachmentId">
        <div>
          <strong>{{ attachment.displayName }}</strong
          ><small>{{ fileSize(attachment.sizeBytes) }}</small>
        </div>
        <div class="portal-attachment-list__actions">
          <a
            v-if="attachment.canPreview"
            :href="attachmentUrl(section.content.documentId, attachment.attachmentId, 'preview')"
            target="_blank"
            rel="noopener noreferrer"
            >预览</a
          >
          <a
            v-if="attachment.canDownload"
            :href="attachmentUrl(section.content.documentId, attachment.attachmentId, 'download')"
            >下载</a
          >
        </div>
      </li>
      <li v-if="section.content.attachments.length === 0" class="portal-muted">暂无附件</li>
    </ul>

    <dl v-else-if="section.content.kind === 'TrustSummary'" class="portal-definition-grid">
      <div>
        <dt>知识对象</dt>
        <dd>{{ section.content.targetTitle }}</dd>
      </div>
      <div>
        <dt>知识状态</dt>
        <dd>{{ statusLabels[section.content.knowledgeStatus] }}</dd>
      </div>
      <div>
        <dt>证据</dt>
        <dd>{{ section.content.evidenceCount }}</dd>
      </div>
      <div>
        <dt>人工确认</dt>
        <dd>{{ section.content.humanConfirmationCount }}</dd>
      </div>
      <div v-if="section.content.confirmationCoverage !== null">
        <dt>当前版本确认</dt>
        <dd>{{ coverageLabels[section.content.confirmationCoverage] ?? '未知' }}</dd>
      </div>
    </dl>

    <div v-else-if="section.content.kind === 'RelatedKnowledge'" class="portal-related-groups">
      <section
        v-for="group in section.content.groups"
        :key="`${group.relationType}-${group.direction}`"
      >
        <h3>
          {{ group.direction === 'Outgoing' ? group.relationLabel : `被${group.relationLabel}` }}
        </h3>
        <ul>
          <li v-for="item in group.items" :key="`${item.targetType}-${item.targetTitle}`">
            <RouterLink
              v-if="item.portalPageId"
              :to="{ name: 'portal-page', params: { id: item.portalPageId } }"
            >
              {{ item.targetTitle }}
            </RouterLink>
            <span v-else>{{ item.targetTitle }}</span>
            <small
              >{{ targetLabels[item.targetType] }} · {{ statusLabels[item.knowledgeStatus] }} · 证据
              {{ item.evidenceCount }}</small
            >
          </li>
        </ul>
      </section>
      <p v-if="section.content.groups.length === 0" class="portal-muted">暂无相关知识</p>
    </div>

    <div v-else-if="section.content.kind === 'Traceability'" class="portal-traceability">
      <p v-for="code in section.content.missingLinkCodes" :key="code" class="portal-trace-warning">
        {{ code === 'MissingSpecification' ? '缺少规格定义' : '缺少测试定义' }}
      </p>
      <ol v-if="section.content.paths.length" class="portal-trace-paths">
        <li v-for="(path, pathIndex) in section.content.paths" :key="`${path.kind}-${pathIndex}`">
          <template v-for="(node, nodeIndex) in path.nodes" :key="`${node.title}-${nodeIndex}`">
            <span v-if="nodeIndex" class="portal-trace-arrow" aria-hidden="true">→</span>
            <span class="portal-trace-node">
              <RouterLink
                v-if="node.portalPageId"
                :to="{ name: 'portal-page', params: { id: node.portalPageId } }"
                >{{ node.title }}</RouterLink
              >
              <span v-else>{{ node.title }}</span>
              <small
                >{{ traceTypeLabels[node.documentType] ?? node.documentType }} ·
                {{ statusLabels[node.knowledgeStatus] }}</small
              >
            </span>
          </template>
        </li>
      </ol>
      <p v-else class="portal-muted">暂无可展示的追溯路径</p>
      <p v-if="section.content.cycleDetected" class="portal-trace-warning">
        检测到循环关系，已安全停止展开。
      </p>
      <p v-if="section.content.isTruncated" class="portal-trace-warning">
        追溯内容已按安全上限截断。
      </p>
    </div>

    <p v-else class="portal-section-unsupported" role="status">该内容暂不可显示</p>
  </section>
</template>
