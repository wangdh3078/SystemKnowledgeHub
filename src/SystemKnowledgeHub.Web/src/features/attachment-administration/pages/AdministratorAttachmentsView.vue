<script setup lang="ts">
import { onBeforeUnmount, onMounted, watch } from 'vue'
import { Document, Search } from '@element-plus/icons-vue'
import { ElRadioButton, ElRadioGroup } from 'element-plus'
import { formatDateTime } from '../../../app/formatters/dateTime'
import { useOverlayStore } from '../../../app/stores/overlays'
import EmptyState from '../../../components/feedback/EmptyState.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import type { AdministratorAttachmentListItem } from '../api/administratorAttachmentContracts'
import {
  administratorAttachmentStorageFilterOptions,
  administratorAttachmentStorageLabels,
  formatAdministratorAttachmentReferenceSummary,
} from '../attachmentAdministrationPresentation'
import AdministratorAttachmentDetailDrawer from '../components/AdministratorAttachmentDetailDrawer.vue'
import { useAdministratorAttachments } from '../composables/useAdministratorAttachments'

const overlays = useOverlayStore()
const {
  query,
  kind,
  extension,
  referenceStatus,
  storageState,
  page,
  data,
  statistics,
  loading,
  statisticsLoading,
  error,
  statisticsError,
  loadList,
  loadStatistics,
  resetPageAndLoad,
  clearFilters,
  refresh,
} = useAdministratorAttachments()
let queryTimer: ReturnType<typeof setTimeout> | null = null

watch(query, () => {
  if (queryTimer) clearTimeout(queryTimer)
  queryTimer = setTimeout(resetPageAndLoad, 280)
})

function formatFileSize(sizeBytes: number): string {
  if (sizeBytes < 1024) return `${sizeBytes} B`
  if (sizeBytes < 1024 * 1024) return `${formatNumber(sizeBytes / 1024)} KB`
  if (sizeBytes < 1024 * 1024 * 1024) return `${formatNumber(sizeBytes / (1024 * 1024))} MB`
  return `${formatNumber(sizeBytes / (1024 * 1024 * 1024))} GB`
}

function formatNumber(value: number): string {
  return new Intl.NumberFormat('zh-CN', { maximumFractionDigits: 1 }).format(value)
}

function openDetail(attachmentId: number): void {
  overlays.openDrawer({ kind: 'attachment-administration', id: attachmentId, mode: 'read' })
}

function handlePageChange(nextPage: number): void {
  page.value = nextPage
  void loadList()
}

function afterDetailChange(): void {
  void refresh()
}

function abbreviatedSha(hash: string): string {
  return `${hash.slice(0, 10)}…${hash.slice(-8)}`
}

function storageLabel(health: AdministratorAttachmentListItem['storageHealth']): string {
  return administratorAttachmentStorageLabels[health]
}

function openRow(row: AdministratorAttachmentListItem): void {
  openDetail(row.attachmentId)
}

onMounted(() => {
  void Promise.all([loadList(), loadStatistics()])
})

onBeforeUnmount(() => {
  if (queryTimer) clearTimeout(queryTimer)
})
</script>

<template>
  <main class="attachment-admin-page skh-page">
    <header class="attachment-admin-page__header skh-page-header">
      <div>
        <nav>管理 / 附件管理</nav>
        <h1>附件管理</h1>
        <p>治理全局附件 metadata、全部修订引用和零引用 orphan；不提供批量、强制或级联删除。</p>
      </div>
      <el-button :icon="Document" :loading="loading || statisticsLoading" @click="refresh"
        >刷新</el-button
      >
    </header>

    <section
      class="attachment-admin-stats"
      aria-label="附件存储统计"
      :aria-busy="statisticsLoading"
    >
      <div>
        <span>附件总数</span><strong>{{ statistics?.totalCount ?? '—' }}</strong
        ><small>{{ statistics ? formatFileSize(statistics.totalSizeBytes) : '统计加载中' }}</small>
      </div>
      <div>
        <span>有引用</span><strong>{{ statistics?.referencedCount ?? '—' }}</strong
        ><small
          >当前 {{ statistics?.currentReferencedCount ?? '—' }} · 仅历史
          {{ statistics?.historicalOnlyCount ?? '—' }}</small
        >
      </div>
      <div class="attachment-admin-stats__orphan">
        <span>孤立附件</span><strong>{{ statistics?.orphanCount ?? '—' }}</strong
        ><small>{{ statistics ? formatFileSize(statistics.orphanSizeBytes) : '—' }}</small>
      </div>
      <div>
        <span>Image / File</span
        ><strong>{{
          statistics ? `${statistics.imageCount} / ${statistics.fileCount}` : '—'
        }}</strong
        ><small>{{
          statistics
            ? `${formatFileSize(statistics.imageSizeBytes)} / ${formatFileSize(statistics.fileSizeBytes)}`
            : '—'
        }}</small>
      </div>
      <div>
        <span>等待删除重试</span><strong>{{ statistics?.deletePendingCount ?? '—' }}</strong
        ><small>单项重试，不自动清理</small>
      </div>
    </section>
    <p v-if="statisticsError" class="attachment-admin-page__inline-error" role="alert">
      统计加载失败：{{ statisticsError }}
    </p>

    <section v-if="statistics" class="attachment-admin-highlights" aria-label="最大附件与最近上传">
      <p>
        <strong>最大附件</strong
        ><span v-if="statistics.largestAttachments[0]"
          >{{ statistics.largestAttachments[0].originalFileName }} ·
          {{ formatFileSize(statistics.largestAttachments[0].sizeBytes) }}</span
        ><span v-else>暂无附件</span>
      </p>
      <p>
        <strong>最近上传</strong
        ><span>{{ statistics.recentWindowDays }} 天内 {{ statistics.recentUploadCount }} 个</span
        ><span v-if="statistics.recentUploads[0]"
          >最新：{{ statistics.recentUploads[0].originalFileName }}</span
        >
      </p>
    </section>

    <section class="attachment-admin-filter" aria-label="附件筛选">
      <el-input
        v-model="query"
        clearable
        :prefix-icon="Search"
        placeholder="搜索文件名"
        aria-label="搜索附件文件名"
      />
      <el-radio-group
        v-model="referenceStatus"
        aria-label="引用状态快捷筛选"
        @change="resetPageAndLoad"
      >
        <el-radio-button value="">全部</el-radio-button>
        <el-radio-button value="Referenced">有引用</el-radio-button>
        <el-radio-button value="Orphan">孤立附件</el-radio-button>
      </el-radio-group>
      <el-select
        v-model="kind"
        aria-label="按附件 Kind 筛选"
        placeholder="Kind：全部"
        @change="resetPageAndLoad"
      >
        <el-option label="全部 Kind" value="" />
        <el-option label="Image" value="Image" />
        <el-option label="File" value="File" />
      </el-select>
      <el-input
        v-model="extension"
        clearable
        placeholder="扩展名，例如 .pdf"
        aria-label="按扩展名筛选"
        @change="resetPageAndLoad"
      />
      <el-select
        v-model="storageState"
        aria-label="按存储状态筛选"
        placeholder="存储状态：全部"
        @change="resetPageAndLoad"
      >
        <el-option
          v-for="option in administratorAttachmentStorageFilterOptions"
          :key="option.value || 'all'"
          :label="option.label"
          :value="option.value"
        />
      </el-select>
      <el-button
        v-if="query || kind || extension || referenceStatus || storageState"
        text
        type="primary"
        @click="clearFilters"
        >清除筛选</el-button
      >
      <span v-if="data">共 {{ data.total }} 个附件</span>
    </section>

    <LoadingState v-if="loading && !data" message="正在读取附件列表…" />
    <ErrorState
      v-else-if="error && !data"
      title="附件列表加载失败"
      :message="error"
      @retry="loadList"
    />
    <section v-else class="attachment-admin-table-section skh-table-section" :aria-busy="loading">
      <EmptyState
        v-if="data && data.items.length === 0"
        title="没有找到附件"
        description="调整文件名、Kind、扩展名、引用状态或存储状态筛选。"
      />
      <el-table
        v-else
        :data="data?.items ?? []"
        row-key="attachmentId"
        class="attachment-admin-table skh-data-table skh-data-table--comfortable"
        @row-click="openRow"
      >
        <el-table-column label="文件" min-width="210" fixed="left">
          <template #default="scope">
            <button
              class="attachment-admin-table__file skh-table-link"
              type="button"
              :title="scope.row.originalFileName"
              @click.stop="openDetail(scope.row.attachmentId)"
            >
              {{ scope.row.originalFileName }}
            </button>
            <small>#{{ scope.row.attachmentId }} · {{ formatFileSize(scope.row.sizeBytes) }}</small>
          </template>
        </el-table-column>
        <el-table-column label="Kind / 类型" min-width="135"
          ><template #default="scope"
            ><strong>{{ scope.row.kind }}</strong
            ><small>{{ scope.row.extension }} · {{ scope.row.previewMode }}</small></template
          ></el-table-column
        >
        <el-table-column label="上传" min-width="156"
          ><template #default="scope"
            ><span>{{ scope.row.createdByDisplayName }}</span
            ><small>{{ formatDateTime(scope.row.createdAt) }}</small></template
          ></el-table-column
        >
        <el-table-column label="所属文档" min-width="205">
          <template #default="scope">
            <div class="attachment-admin-table__owner">
              <span :title="scope.row.owner.title">{{ scope.row.owner.title }}</span>
              <el-tag v-if="scope.row.owner.isDeleted" type="danger" effect="plain" size="small"
                >已删除</el-tag
              >
              <small v-else
                >#{{ scope.row.owner.documentId }} · {{ scope.row.owner.lifecycleStatus }}</small
              >
            </div>
          </template>
        </el-table-column>
        <el-table-column label="引用" min-width="168"
          ><template #default="scope"
            ><span
              class="attachment-admin-table__status attachment-admin-table__reference-status"
              :class="{
                'attachment-admin-table__status--warning': scope.row.referenceStatus === 'Orphan',
                'attachment-admin-table__status--positive':
                  scope.row.referenceStatus === 'Referenced',
                'attachment-admin-table__status--historical':
                  scope.row.referenceStatus === 'HistoricalOnly',
              }"
              >{{ formatAdministratorAttachmentReferenceSummary(scope.row) }}</span
            ></template
          ></el-table-column
        >
        <el-table-column label="存储" min-width="130"
          ><template #default="scope"
            ><span
              class="attachment-admin-table__status attachment-admin-table__storage-status"
              :class="
                scope.row.storageHealth === 'Ready'
                  ? 'attachment-admin-table__status--positive'
                  : 'attachment-admin-table__status--danger'
              "
              >{{ storageLabel(scope.row.storageHealth) }}</span
            ></template
          ></el-table-column
        >
        <el-table-column label="SHA-256" min-width="165"
          ><template #default="scope"
            ><code :title="scope.row.sha256">{{ abbreviatedSha(scope.row.sha256) }}</code></template
          ></el-table-column
        >
        <el-table-column label="操作" width="92" fixed="right"
          ><template #default="scope"
            ><el-button
              text
              type="primary"
              :aria-label="`查看附件详情 ${scope.row.originalFileName}`"
              @click.stop="openDetail(scope.row.attachmentId)"
              >详情</el-button
            ></template
          ></el-table-column
        >
      </el-table>

      <footer v-if="data && data.total > 0" class="attachment-admin-pagination skh-pagination">
        <span
          >{{ (data.page - 1) * data.pageSize + 1 }}–{{
            Math.min(data.page * data.pageSize, data.total)
          }}
          / {{ data.total }}</span
        >
        <el-pagination
          background
          layout="prev, pager, next"
          :current-page="data.page"
          :page-size="data.pageSize"
          :total="data.total"
          @current-change="handlePageChange"
        />
      </footer>
      <p v-if="error && data" class="attachment-admin-page__inline-error" role="alert">
        刷新失败：{{ error }}
      </p>
    </section>

    <Teleport
      v-if="
        overlays.currentDrawer?.kind === 'attachment-administration' && overlays.currentDrawer.id
      "
      defer
      to="#drawer-feature-content"
    >
      <AdministratorAttachmentDetailDrawer
        :attachment-id="overlays.currentDrawer.id"
        @deleted="afterDetailChange"
        @changed="afterDetailChange"
      />
    </Teleport>
  </main>
</template>

<style src="../attachment-administration.css"></style>
