<script setup lang="ts">
import {
  ArrowRight,
  Coin,
  Connection,
  DocumentAdd,
  Files,
  Grid,
  Tickets,
} from '@element-plus/icons-vue'
import { useOverlayStore } from '../../../app/stores/overlays'

type SupportedCreateKind = 'system' | 'business-function' | 'database-knowledge' | 'business-rule' | 'integration' | 'knowledge-document'

const props = withDefaults(defineProps<{
  enabledKinds?: readonly SupportedCreateKind[]
  systemContext?: string
}>(), {
  enabledKinds: () => ['system'],
  systemContext: '无',
})
const overlayStore = useOverlayStore()
const emit = defineEmits<{ chooseKnowledgeDocument: [] }>()

const choices = [
  { kind: 'system', label: '系统', description: '记录一个系统及其基本上下文', icon: Grid },
  { kind: 'business-function', label: '业务功能', description: '记录用户或系统可以完成的业务能力', icon: Files },
  { kind: 'database-knowledge', label: '数据库知识', description: '登记数据库来源、表或视图', icon: Coin },
  { kind: 'business-rule', label: '业务规则', description: '记录条件、结果及其依据', icon: Tickets },
  { kind: 'integration', label: '集成关系', description: '记录 API、MQ、文件或数据库依赖', icon: Connection },
  { kind: 'knowledge-document', label: '知识内容', description: '记录需求、规格、测试用例、SOP、故障排查和知识文章', icon: DocumentAdd },
] as const

function isEnabled(kind: SupportedCreateKind): boolean {
  return props.enabledKinds.includes(kind)
}

function choose(kind: SupportedCreateKind): void {
  if (!isEnabled(kind)) return
  if (kind === 'knowledge-document') {
    overlayStore.closeDialog()
    emit('chooseKnowledgeDocument')
    return
  }
  overlayStore.openDialog({ kind: `create-${kind}`, id: null, mode: 'create' })
}
</script>

<template>
  <section class="create-object-chooser" aria-labelledby="create-object-title">
    <header class="authoring-header">
      <div>
        <h2 id="create-object-title">新增知识对象</h2>
        <p>先选择要记录的知识类型；创建时只填写最小必要信息，关系、证据和业务知识可以后续逐步补充。</p>
      </div>
      <button class="authoring-close" type="button" aria-label="关闭" @click="overlayStore.closeDialog">×</button>
    </header>

    <div class="create-object-chooser__list" role="list">
      <button
        v-for="choice in choices"
        :key="choice.label"
        class="create-object-choice"
        :class="{ 'create-object-choice--disabled': !isEnabled(choice.kind) }"
        type="button"
        :disabled="!isEnabled(choice.kind)"
        :title="isEnabled(choice.kind) ? `新增${choice.label}` : `请从对应知识对象进入${choice.label}维护`"
        @click="choose(choice.kind)"
      >
        <el-icon :size="22"><component :is="choice.icon" /></el-icon>
        <strong>{{ choice.label }}</strong>
        <span>{{ choice.description }}</span>
        <small v-if="!isEnabled(choice.kind)">请从对象上下文进入</small>
        <el-icon v-else :size="16"><ArrowRight /></el-icon>
      </button>
    </div>

    <div class="create-object-chooser__context">
      <span>当前系统上下文：<strong>{{ systemContext }}</strong></span>
      <span>新对象默认知识状态：<strong>未知</strong></span>
    </div>

    <footer class="authoring-keyboard-hint">
      <span><kbd>Enter</kbd> 继续</span>
      <span><kbd>Esc</kbd> 取消</span>
    </footer>
  </section>
</template>
