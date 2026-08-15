<script setup lang="ts">
import { computed, onBeforeUnmount, onMounted, ref, toRef } from 'vue'
import { Close, DocumentChecked, EditPen, Plus, QuestionFilled } from '@element-plus/icons-vue'
import { useOverlayStore } from '../../../app/stores/overlays'
import KnowledgeStatusBadge from '../../../components/data-display/KnowledgeStatusBadge.vue'
import ErrorState from '../../../components/feedback/ErrorState.vue'
import LoadingState from '../../../components/feedback/LoadingState.vue'
import { useDatabaseColumnDetail } from '../composables/useDatabaseColumnDetail'
import KnowledgeProgression from '../../../components/data-display/KnowledgeProgression.vue'

const props = defineProps<{ columnId: number | null }>()
const overlayStore = useOverlayStore()
const { detail, loading, errorMessage, reload } = useDatabaseColumnDetail(toRef(props, 'columnId'))
const activeSections = ref<string[]>(['businessKnowledge', 'evidence', 'unknownItems'])

function addEvidence(): void {
  if (!detail.value) return
  overlayStore.openDrawer({
    kind: 'add-evidence',
    id: null,
    mode: 'create',
    payload: {
      subject: { type: 'DatabaseColumn', id: detail.value.id },
      title: `${detail.value.parent.qualifiedName}.${detail.value.databaseMetadata.columnName}`,
      knowledgeStatus: detail.value.businessKnowledge.knowledgeStatus,
    },
  })
}

function openEvidence(evidenceId: number): void {
  overlayStore.openDrawer({ kind: 'evidence', id: evidenceId, mode: 'read' })
}

function evidenceChanged(): void {
  reload()
}

onMounted(() => window.addEventListener('evidence:changed', evidenceChanged))
onBeforeUnmount(() => window.removeEventListener('evidence:changed', evidenceChanged))

function createUnknownItem(): void {
  if (!detail.value) return
  overlayStore.openDialog({ kind: 'create-unknown-item', id: null, mode: 'create', payload: {
    systemId: detail.value.system.id,
    systemName: detail.value.system.name,
    target: { type: 'DatabaseColumn', id: detail.value.id },
    title: `${detail.value.parent.qualifiedName}.${detail.value.databaseMetadata.columnName}`,
  } })
}

const metadataRows = computed(() => {
  if (!detail.value) return []
  return [
    ['字段名', detail.value.databaseMetadata.columnName],
    ['数据类型', detail.value.databaseMetadata.dataType],
    ['允许为空', detail.value.databaseMetadata.nullable ? '是' : '否'],
    ['默认值', detail.value.databaseMetadata.defaultValue ?? '—'],
    ['字段顺序', String(detail.value.databaseMetadata.ordinalPosition)],
  ] as const
})
</script>

<template>
  <div class="column-drawer">
    <LoadingState v-if="loading" message="正在读取字段详情…" />
    <ErrorState
      v-else-if="errorMessage"
      title="字段详情加载失败"
      :message="errorMessage"
      @retry="reload"
    />
    <template v-else-if="detail">
      <header class="column-drawer__header">
        <el-button
          class="column-drawer__close"
          text
          circle
          :icon="Close"
          aria-label="关闭字段详情"
          @click="overlayStore.closeDrawer()"
        />
        <span class="column-drawer__eyebrow">字段详情</span>
        <h2 class="technical-text">{{ detail.databaseMetadata.columnName }}</h2>
        <p>
          <span class="technical-text">{{ detail.parent.qualifiedName }}</span>
          · <span class="technical-text">{{ detail.databaseMetadata.dataType }}</span>
        </p>
      </header>

      <section class="column-drawer__progression">
        <div class="drawer-section-title">
          <h3>知识进展</h3>
          <span>当前：<KnowledgeStatusBadge :status="detail.businessKnowledge.knowledgeStatus" /></span>
        </div>
        <KnowledgeProgression :status="detail.businessKnowledge.knowledgeStatus" />
        <p>状态只能通过明确操作改变，不能点击进展节点直接切换。</p>
      </section>

      <el-collapse v-model="activeSections" class="column-drawer__sections">
        <el-collapse-item name="businessKnowledge">
          <template #title>
            <div class="drawer-collapse-title">
              <span>业务知识</span>
              <el-button text type="primary" :icon="EditPen" disabled>编辑</el-button>
            </div>
          </template>
          <dl class="drawer-facts">
            <div>
              <dt>描述</dt>
              <dd>{{ detail.businessKnowledge.description ?? '尚未记录业务含义' }}</dd>
            </div>
            <div>
              <dt>知识状态</dt>
              <dd><KnowledgeStatusBadge :status="detail.businessKnowledge.knowledgeStatus" /></dd>
            </div>
          </dl>
          <p class="drawer-section-note">业务含义与支撑它的证据分开保存。</p>
        </el-collapse-item>

        <el-collapse-item name="evidence">
          <template #title>
            <div class="drawer-collapse-title">
              <span>证据 <b>{{ detail.evidence.length }}</b></span>
              <el-button text type="primary" :icon="Plus" @click.stop="addEvidence">添加</el-button>
            </div>
          </template>
          <div v-if="detail.evidence.length" class="drawer-evidence-list">
            <article
              v-for="item in detail.evidence"
              :key="item.id"
              role="button"
              tabindex="0"
              @click="openEvidence(item.id)"
              @keydown.enter="openEvidence(item.id)"
            >
              <el-icon><DocumentChecked /></el-icon>
              <div>
                <small>{{ item.evidenceType }}</small>
                <strong>{{ item.sourceTitle }}</strong>
                <p>{{ item.supportReason }}</p>
              </div>
            </article>
          </div>
          <div v-else class="drawer-empty-state">
            <el-icon><DocumentChecked /></el-icon>
            <div>
              <strong>尚无字段级证据</strong>
              <p>添加代码、SQL、数据库样本或人工确认，说明为什么相信这条知识。</p>
            </div>
          </div>
        </el-collapse-item>

        <el-collapse-item name="unknownItems">
          <template #title>
            <div class="drawer-collapse-title">
              <span>待确认事项 <b>{{ detail.unknownItems.length }}</b></span>
              <el-button text type="primary" :icon="Plus" @click.stop="createUnknownItem">添加</el-button>
            </div>
          </template>
          <div v-if="detail.unknownItems.length" class="drawer-unknown-list">
            <article v-for="item in detail.unknownItems" :key="item.id">
              <el-icon><QuestionFilled /></el-icon>
              <div><strong>{{ item.question }}</strong><span>{{ item.status }}</span></div>
            </article>
          </div>
          <div v-else class="drawer-empty-state drawer-empty-state--compact">
            <p>当前字段没有开放待确认事项。</p>
          </div>
        </el-collapse-item>

        <el-collapse-item name="databaseMetadata" title="数据库元数据">
          <dl class="drawer-facts drawer-facts--metadata">
            <div v-for="row in metadataRows" :key="row[0]">
              <dt>{{ row[0] }}</dt>
              <dd class="technical-text">{{ row[1] }}</dd>
            </div>
          </dl>
        </el-collapse-item>

        <el-collapse-item name="knownValues">
          <template #title>
            <span class="drawer-title-with-count">已知值 <b>{{ detail.knownValues.length }}</b></span>
          </template>
          <div v-if="detail.knownValues.length" class="known-values-list">
            <div v-for="item in detail.knownValues" :key="item.id">
              <code>{{ item.value }}</code><span>{{ item.meaning }}</span>
            </div>
          </div>
          <div v-else class="drawer-empty-state drawer-empty-state--compact"><p>尚无已知值。</p></div>
        </el-collapse-item>

        <el-collapse-item name="relations">
          <template #title>
            <span class="drawer-title-with-count">字段级关系 <b>{{ detail.relations.length }}</b></span>
          </template>
          <div v-if="detail.relations.length" class="drawer-relation-list">
            <div v-for="item in detail.relations" :key="item.id">
              <span>{{ item.relationType }}</span><strong>{{ item.otherObject.title }}</strong>
            </div>
          </div>
          <div v-else class="drawer-empty-state drawer-empty-state--compact">
            <p>尚未建立字段级关系。</p>
          </div>
        </el-collapse-item>
      </el-collapse>

      <footer class="column-drawer__footer">
        <el-button type="primary" :icon="Plus" @click="addEvidence">添加证据</el-button>
        <el-button :icon="QuestionFilled" @click="createUnknownItem">新建待确认事项</el-button>
      </footer>
    </template>
  </div>
</template>
