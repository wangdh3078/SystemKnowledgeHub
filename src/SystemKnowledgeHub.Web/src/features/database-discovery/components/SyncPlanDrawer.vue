<script setup lang="ts">
import { computed } from 'vue'
import { Close } from '@element-plus/icons-vue'
import type { SyncActionType, SyncPlan } from '../api/databaseDiscoverySyncContracts'
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

const actionCounts = computed(() => {
  const counts = new Map<SyncActionType, number>()
  for (const action of props.plan.actions)
    counts.set(action.actionType, (counts.get(action.actionType) ?? 0) + 1)
  return counts
})

function count(...types: SyncActionType[]): number {
  return types.reduce((sum, type) => sum + (actionCounts.value.get(type) ?? 0), 0)
}
</script>

<template>
  <section class="sync-plan-drawer" aria-labelledby="sync-plan-drawer-title">
    <header class="discovery-drawer__header">
      <div>
        <small>同步计划</small>
        <h2 id="sync-plan-drawer-title">计划 #{{ plan.id }}</h2>
        <p>{{ plan.profileName }} · 快照 #{{ plan.targetSnapshotId }}</p>
      </div>
      <el-button text circle :icon="Close" aria-label="关闭同步计划" @click="emit('close')" />
    </header>

    <div class="sync-plan-drawer__body">
      <dl class="sync-plan-drawer__summary">
        <div>
          <dt>当前状态</dt>
          <dd>
            <el-tag>{{ syncPlanStatusLabels[plan.status] }}</el-tag>
          </dd>
        </div>
        <div>
          <dt>连接配置</dt>
          <dd>{{ plan.profileName }}</dd>
        </div>
        <div>
          <dt>快照编号</dt>
          <dd>#{{ plan.targetSnapshotId }}</dd>
        </div>
        <div>
          <dt>创建对象</dt>
          <dd>{{ count('CreateDatabaseObject') }}</dd>
        </div>
        <div>
          <dt>关联对象</dt>
          <dd>{{ count('LinkExistingDatabaseObject') }}</dd>
        </div>
        <div>
          <dt>更新对象</dt>
          <dd>{{ count('UpdateDatabaseObjectStructure') }}</dd>
        </div>
        <div>
          <dt>创建字段</dt>
          <dd>{{ count('CreateDatabaseColumn') }}</dd>
        </div>
        <div>
          <dt>关联字段</dt>
          <dd>{{ count('LinkExistingDatabaseColumn') }}</dd>
        </div>
        <div>
          <dt>更新字段</dt>
          <dd>{{ count('UpdateDatabaseColumnStructure') }}</dd>
        </div>
        <div>
          <dt>标记来源缺失</dt>
          <dd>{{ count('MarkObjectSourceMissing', 'MarkColumnSourceMissing') }}</dd>
        </div>
        <div>
          <dt>清除来源缺失</dt>
          <dd>{{ count('ClearObjectSourceMissing', 'ClearColumnSourceMissing') }}</dd>
        </div>
      </dl>

      <section class="sync-plan-drawer__section">
        <h3>计划动作明细</h3>
        <ul>
          <li
            v-for="action in plan.actions"
            :key="`${action.actionType}:${action.logicalIdentity}:${action.targetId ?? ''}`"
          >
            <strong>{{ syncActionLabels[action.actionType] }}</strong
            ><span class="technical-text">{{ action.logicalIdentity }}</span>
          </li>
        </ul>
      </section>

      <section class="sync-plan-drawer__section">
        <h3>预览内容</h3>
        <template v-if="plan.preview"
          ><el-alert
            v-for="warning in plan.preview.warnings"
            :key="warning"
            :title="warning"
            type="warning"
            :closable="false"
            show-icon
          />
          <p class="discovery-hash">
            预览校验值：<code>{{ plan.preview.previewHash }}</code>
          </p>
          <el-table :data="plan.preview.actions" max-height="360"
            ><el-table-column label="操作" width="140"
              ><template #default="{ row }">{{
                syncActionLabels[row.actionType as SyncActionType]
              }}</template></el-table-column
            ><el-table-column prop="summary" label="范围" min-width="180" /><el-table-column
              label="变更前"
              min-width="190"
              ><template #default="{ row }">
                <pre>{{ formatSyncStructure(row.before) }}</pre>
              </template></el-table-column
            ><el-table-column label="变更后" min-width="190"
              ><template #default="{ row }">
                <pre>{{ formatSyncStructure(row.after) }}</pre>
              </template></el-table-column
            ></el-table
          ></template
        >
        <p v-else class="text-muted">该计划尚未生成预览。</p>
      </section>

      <section class="sync-plan-drawer__section">
        <h3>确认状态</h3>
        <p>
          {{
            plan.confirmedAt
              ? `已于 ${new Date(plan.confirmedAt).toLocaleString('zh-CN')} 确认`
              : '尚未确认'
          }}
        </p>
        <div
          v-if="canEdit && plan.status === 'Draft' && plan.preview"
          class="discovery-confirmation"
        >
          <el-checkbox
            :model-value="confirmationChecked"
            @change="emit('update:confirmationChecked', Boolean($event))"
            >我已核对预览内容与人工知识保护边界</el-checkbox
          ><el-button
            type="primary"
            :disabled="!confirmationChecked"
            :loading="mutating"
            @click="emit('confirm')"
            >确认当前预览</el-button
          >
        </div>
        <div v-if="canEdit && plan.status === 'Ready'" class="discovery-confirmation">
          <span>应用前服务端会再次验证最新快照、来源关联和目标并发状态。</span
          ><el-button type="danger" :loading="mutating" @click="emit('apply')"
            >应用已确认计划</el-button
          >
        </div>
      </section>

      <section class="sync-plan-drawer__section">
        <h3>应用结果</h3>
        <template v-if="plan.status === 'Applied' && plan.result"
          ><el-result
            icon="success"
            title="同步计划已应用"
            sub-title="所有操作已在一个短事务中提交。"
          />
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
          </dl></template
        >
        <p v-else class="text-muted">尚无应用结果。</p>
      </section>
    </div>
  </section>
</template>
