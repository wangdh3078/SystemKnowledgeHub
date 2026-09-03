<script setup lang="ts">
import { computed, ref, watch } from 'vue'
import { Close } from '@element-plus/icons-vue'
import type {
  SyncActionType,
  SyncPlan,
  SyncPreviewAction,
  SyncStructure,
} from '../api/databaseDiscoverySyncContracts'
import {
  formatSyncStructure,
  syncActionLabels,
  syncPlanStatusLabels,
} from '../databaseDiscoveryPresentation'

const props = defineProps<{
  plan: SyncPlan
  canEdit: boolean
  mutating: boolean
  confirmationChecked: boolean
}>()

const emit = defineEmits<{
  close: []
  confirm: []
  apply: []
  'update:confirmationChecked': [value: boolean]
}>()

type PlanTab = 'overview' | 'details' | 'result'
interface PreviewGroup {
  key: string
  logicalIdentity: string
  schemaName: string
  objectName: string
  objectType: string
  databaseComment: string | null
  objectActions: SyncPreviewAction[]
  columnActions: SyncPreviewAction[]
}

const actionTypes: readonly SyncActionType[] = [
  'CreateDatabaseObject',
  'LinkExistingDatabaseObject',
  'UpdateDatabaseObjectStructure',
  'MarkObjectSourceMissing',
  'ClearObjectSourceMissing',
  'CreateDatabaseColumn',
  'LinkExistingDatabaseColumn',
  'UpdateDatabaseColumnStructure',
  'MarkColumnSourceMissing',
  'ClearColumnSourceMissing',
]
const tabs: readonly { key: PlanTab; label: string }[] = [
  { key: 'overview', label: '概览' },
  { key: 'details', label: '变更明细' },
  { key: 'result', label: '应用结果' },
]
const activeTab = ref<PlanTab>(props.plan.status === 'Applied' ? 'overview' : 'details')

watch(
  () => props.plan.id,
  () => {
    activeTab.value = props.plan.status === 'Applied' ? 'overview' : 'details'
  },
)
watch(
  () => props.plan.status,
  (status, previousStatus) => {
    if (status === 'Applied' && previousStatus !== 'Applied') activeTab.value = 'result'
  },
)

const actionCounts = computed(() => {
  const counts = new Map<SyncActionType, number>()
  for (const action of props.plan.actions)
    counts.set(action.actionType, (counts.get(action.actionType) ?? 0) + 1)
  return counts
})

const previewGroups = computed<PreviewGroup[]>(() => {
  const groups = new Map<string, PreviewGroup>()
  for (const action of props.plan.preview?.actions ?? []) {
    const structure = action.after ?? action.before
    const key =
      action.entityKind === 'DatabaseObject'
        ? action.logicalIdentity
        : (action.parentLogicalIdentity ?? action.logicalIdentity)
    let group = groups.get(key)
    if (!group) {
      const newGroup: PreviewGroup = {
        key,
        logicalIdentity: key,
        schemaName:
          action.objectSchemaName ??
          (action.entityKind === 'DatabaseObject' ? structure?.schemaName : null) ??
          '架构待识别',
        objectName:
          action.objectName ??
          (action.entityKind === 'DatabaseObject' ? structure?.name : null) ??
          '历史对象',
        objectType:
          action.objectType ??
          (action.entityKind === 'DatabaseObject' ? structure?.objectType : null) ??
          'Unknown',
        databaseComment:
          action.objectDatabaseComment ??
          (action.entityKind === 'DatabaseObject' ? structure?.databaseComment : null) ??
          null,
        objectActions: [],
        columnActions: [],
      }
      groups.set(key, newGroup)
      group = newGroup
    } else if (action.entityKind === 'DatabaseObject') {
      group.schemaName = action.objectSchemaName ?? structure?.schemaName ?? group.schemaName
      group.objectName = action.objectName ?? structure?.name ?? group.objectName
      group.objectType = action.objectType ?? structure?.objectType ?? group.objectType
      group.databaseComment =
        action.objectDatabaseComment ?? structure?.databaseComment ?? group.databaseComment
    }
    if (action.entityKind === 'DatabaseObject') group.objectActions.push(action)
    else group.columnActions.push(action)
  }
  return [...groups.values()].sort((left, right) =>
    `${left.schemaName}.${left.objectName}`.localeCompare(
      `${right.schemaName}.${right.objectName}`,
      'zh-CN',
    ),
  )
})

const technicalActions = computed(() => props.plan.preview?.actions.slice(0, 20) ?? [])
const technicalOverflow = computed(() =>
  Math.max(0, (props.plan.preview?.actions.length ?? 0) - technicalActions.value.length),
)

function dateTime(value: string | null): string {
  return value ? new Date(value).toLocaleString('zh-CN') : '—'
}
function objectTypeLabel(value: string): string {
  return value === 'Table' ? '表' : value === 'View' ? '视图' : value === 'Unknown' ? '未知' : value
}
function actionStructure(action: SyncPreviewAction): SyncStructure | null {
  return action.after ?? action.before
}
function nullableLabel(value: boolean | null | undefined): string {
  return value === null || value === undefined ? '—' : value ? '是' : '否'
}
</script>

<template>
  <section class="sync-plan-dialog" aria-labelledby="sync-plan-dialog-title">
    <header class="sync-plan-dialog__header">
      <div>
        <div class="sync-plan-dialog__eyebrow">
          <span>同步计划 #{{ plan.id }}</span>
          <el-tag size="small">{{ syncPlanStatusLabels[plan.status] }}</el-tag>
        </div>
        <h2 id="sync-plan-dialog-title">
          {{ plan.profileName }} · 快照 #{{ plan.targetSnapshotId }}
        </h2>
        <p>
          创建于 {{ dateTime(plan.createdAt) }}
          <template v-if="plan.appliedAt"> · 应用于 {{ dateTime(plan.appliedAt) }}</template>
        </p>
      </div>
      <el-button text circle :icon="Close" aria-label="关闭同步计划" @click="emit('close')" />
    </header>

    <nav class="sync-plan-dialog__tabs" aria-label="同步计划内容" role="tablist">
      <button
        v-for="tab in tabs"
        :key="tab.key"
        type="button"
        role="tab"
        :aria-selected="activeTab === tab.key"
        :class="{ 'is-active': activeTab === tab.key }"
        @click="activeTab = tab.key"
      >
        {{ tab.label }}
      </button>
    </nav>

    <div class="sync-plan-dialog__body">
      <section v-if="activeTab === 'overview'" class="sync-plan-dialog__panel" role="tabpanel">
        <dl class="sync-plan-dialog__metadata">
          <div>
            <dt>连接配置</dt>
            <dd>{{ plan.profileName }}</dd>
          </div>
          <div>
            <dt>数据库来源</dt>
            <dd>{{ plan.databaseSourceName }}</dd>
          </div>
          <div>
            <dt>目标快照</dt>
            <dd>#{{ plan.targetSnapshotId }}</dd>
          </div>
          <div>
            <dt>确认时间</dt>
            <dd>{{ dateTime(plan.confirmedAt) }}</dd>
          </div>
        </dl>
        <section>
          <h3>操作统计</h3>
          <dl class="sync-plan-dialog__counts">
            <div v-for="actionType in actionTypes" :key="actionType">
              <dt>{{ syncActionLabels[actionType] }}</dt>
              <dd>{{ actionCounts.get(actionType) ?? 0 }}</dd>
            </div>
          </dl>
        </section>
        <details class="sync-plan-dialog__technical">
          <summary>技术信息</summary>
          <dl>
            <div>
              <dt>范围代次</dt>
              <dd>{{ plan.scopeGenerationId }}</dd>
            </div>
            <div>
              <dt>身份算法版本</dt>
              <dd>{{ plan.identityAlgorithmVersion }}</dd>
            </div>
            <div>
              <dt>预览校验值</dt>
              <dd>
                <code>{{ plan.preview?.previewHash ?? '—' }}</code>
              </dd>
            </div>
          </dl>
        </details>
      </section>

      <section v-else-if="activeTab === 'details'" class="sync-plan-dialog__panel" role="tabpanel">
        <template v-if="plan.preview">
          <el-alert
            v-for="warning in plan.preview.warnings"
            :key="warning"
            :title="warning"
            type="warning"
            :closable="false"
            show-icon
          />
          <div v-if="previewGroups.length" class="sync-plan-dialog__groups">
            <article v-for="group in previewGroups" :key="group.key" class="sync-plan-object">
              <header>
                <div>
                  <h3>{{ group.schemaName }}.{{ group.objectName }}</h3>
                  <p>
                    {{ objectTypeLabel(group.objectType) }} ·
                    {{ group.databaseComment || '暂无数据库注释' }}
                  </p>
                </div>
                <div class="sync-plan-object__actions">
                  <el-tag
                    v-for="action in group.objectActions"
                    :key="`${action.actionType}:${action.logicalIdentity}`"
                    size="small"
                    effect="plain"
                    >{{ syncActionLabels[action.actionType] }}</el-tag
                  >
                </div>
              </header>

              <div
                v-if="group.objectActions.some((action) => action.before && action.after)"
                class="sync-plan-object__comparison"
              >
                <details
                  v-for="action in group.objectActions.filter((item) => item.before && item.after)"
                  :key="`${action.actionType}:${action.logicalIdentity}:comparison`"
                >
                  <summary>{{ syncActionLabels[action.actionType] }}对照</summary>
                  <div>
                    <pre>{{ formatSyncStructure(action.before) }}</pre>
                    <pre>{{ formatSyncStructure(action.after) }}</pre>
                  </div>
                </details>
              </div>

              <section v-if="group.columnActions.length" class="sync-plan-fields">
                <h4>字段（{{ group.columnActions.length }}）</h4>
                <article
                  v-for="action in group.columnActions"
                  :key="`${action.actionType}:${action.logicalIdentity}`"
                  class="sync-plan-field"
                >
                  <header>
                    <strong>{{ actionStructure(action)?.name ?? '字段名称待识别' }}</strong>
                    <el-tag size="small" effect="plain">{{
                      syncActionLabels[action.actionType]
                    }}</el-tag>
                  </header>
                  <dl>
                    <div>
                      <dt>原生类型</dt>
                      <dd>{{ actionStructure(action)?.dataType ?? '—' }}</dd>
                    </div>
                    <div>
                      <dt>允许为空</dt>
                      <dd>{{ nullableLabel(actionStructure(action)?.isNullable) }}</dd>
                    </div>
                    <div>
                      <dt>字段顺序</dt>
                      <dd>{{ actionStructure(action)?.ordinalPosition ?? '—' }}</dd>
                    </div>
                    <div>
                      <dt>数据库注释</dt>
                      <dd>{{ actionStructure(action)?.databaseComment ?? '—' }}</dd>
                    </div>
                  </dl>
                  <details v-if="action.before && action.after">
                    <summary>查看变更前后</summary>
                    <div class="sync-plan-field__comparison">
                      <pre>{{ formatSyncStructure(action.before) }}</pre>
                      <pre>{{ formatSyncStructure(action.after) }}</pre>
                    </div>
                  </details>
                </article>
              </section>
            </article>
          </div>
          <p v-else class="text-muted">当前预览没有可展示的结构动作。</p>
          <details class="sync-plan-dialog__technical">
            <summary>技术信息</summary>
            <dl>
              <div>
                <dt>预览校验值</dt>
                <dd>
                  <code>{{ plan.preview.previewHash }}</code>
                </dd>
              </div>
              <div>
                <dt>范围代次</dt>
                <dd>{{ plan.scopeGenerationId }}</dd>
              </div>
            </dl>
            <ul>
              <li
                v-for="action in technicalActions"
                :key="`${action.actionType}:${action.logicalIdentity}`"
              >
                <strong>{{ syncActionLabels[action.actionType] }}</strong>
                <code>{{ action.logicalIdentity }}</code>
              </li>
            </ul>
            <p v-if="technicalOverflow">其余 {{ technicalOverflow }} 项已收起。</p>
          </details>
        </template>
        <p v-else class="text-muted">该计划尚未生成预览。</p>
      </section>

      <section v-else class="sync-plan-dialog__panel" role="tabpanel">
        <template v-if="plan.status === 'Applied' && plan.result">
          <el-result
            icon="success"
            title="同步计划已应用"
            sub-title="所有操作已在一个短事务中提交。"
          />
          <p class="sync-plan-dialog__applied-by">
            {{ dateTime(plan.result.appliedAt) }} · {{ plan.result.appliedByDisplayName }}
          </p>
          <dl class="discovery-sync-result">
            <div>
              <dt>创建对象</dt>
              <dd>{{ plan.result.createdObjects }}</dd>
            </div>
            <div>
              <dt>关联对象</dt>
              <dd>{{ plan.result.linkedObjects }}</dd>
            </div>
            <div>
              <dt>更新对象</dt>
              <dd>{{ plan.result.updatedObjects }}</dd>
            </div>
            <div>
              <dt>创建字段</dt>
              <dd>{{ plan.result.createdColumns }}</dd>
            </div>
            <div>
              <dt>关联字段</dt>
              <dd>{{ plan.result.linkedColumns }}</dd>
            </div>
            <div>
              <dt>更新字段</dt>
              <dd>{{ plan.result.updatedColumns }}</dd>
            </div>
            <div>
              <dt>标记来源缺失</dt>
              <dd>{{ plan.result.markedMissing }}</dd>
            </div>
            <div>
              <dt>清除来源缺失</dt>
              <dd>{{ plan.result.clearedMissing }}</dd>
            </div>
          </dl>
        </template>
        <p v-else class="text-muted">尚无应用结果。</p>
      </section>
    </div>

    <footer class="sync-plan-dialog__footer">
      <div
        v-if="canEdit && plan.status === 'Draft' && plan.preview"
        class="sync-plan-dialog__confirmation"
      >
        <el-checkbox
          :model-value="confirmationChecked"
          @change="emit('update:confirmationChecked', Boolean($event))"
          >我已核对预览内容与人工知识保护边界</el-checkbox
        >
        <el-button
          type="primary"
          :disabled="!confirmationChecked"
          :loading="mutating"
          @click="emit('confirm')"
          >确认当前预览</el-button
        >
      </div>
      <div v-else-if="canEdit && plan.status === 'Ready'" class="sync-plan-dialog__confirmation">
        <span>应用前会再次验证最新快照、来源关联和目标并发状态。</span>
        <el-button type="danger" :loading="mutating" @click="emit('apply')"
          >应用已确认计划</el-button
        >
      </div>
      <el-button @click="emit('close')">关闭</el-button>
    </footer>
  </section>
</template>
