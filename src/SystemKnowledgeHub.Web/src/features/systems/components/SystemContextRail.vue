<script setup lang="ts">
import { ArrowRight, Coin, Connection, QuestionFilled, WarningFilled } from '@element-plus/icons-vue'
import type { SystemContextRail } from '../api/systemsContracts'

defineProps<{
  systemName: string
  context: SystemContextRail
}>()
</script>

<template>
  <div class="system-context-rail">
    <header class="system-context-rail__header">
      <span>系统级上下文</span>
      <h2>关系与缺口</h2>
      <p>仅展示 {{ systemName }} 的系统级关系与尚未确认内容。</p>
    </header>

    <section class="system-rail-section">
      <div class="system-rail-section__title">
        <h3><el-icon><Connection /></el-icon>关联系统</h3>
        <span>{{ context.relatedSystems.length }}</span>
      </div>
      <ul v-if="context.relatedSystems.length" class="system-rail-list">
        <li v-for="system in context.relatedSystems" :key="system.id">
          <strong>{{ system.name }}</strong><el-icon><ArrowRight /></el-icon>
        </li>
      </ul>
      <p v-else class="system-rail-empty">暂无已登记的关联系统。</p>
    </section>

    <section class="system-rail-section">
      <div class="system-rail-section__title">
        <h3><el-icon><Connection /></el-icon>集成概况</h3>
        <span>{{ context.integrationCount }}</span>
      </div>
      <p class="system-rail-empty">{{ context.integrationCount ? '查看系统级集成摘要。' : '暂无集成关系记录。' }}</p>
    </section>

    <section class="system-rail-section">
      <div class="system-rail-section__title">
        <h3><el-icon><Coin /></el-icon>主数据库</h3>
        <span>{{ context.mainDatabase ? 1 : 0 }}</span>
      </div>
      <strong v-if="context.mainDatabase" class="system-rail-primary-database">
        {{ context.mainDatabase.name }}
      </strong>
      <p v-else class="system-rail-empty">尚未登记主数据库。</p>
    </section>

    <section class="system-rail-section">
      <div class="system-rail-section__title">
        <h3><el-icon><QuestionFilled /></el-icon>高优先级待确认事项</h3>
        <span>{{ context.highPriorityUnknownCount }}</span>
      </div>
      <p class="system-rail-empty">
        {{ context.highPriorityUnknownCount ? '存在需要优先调查的系统级问题。' : '暂无高优先级事项。' }}
      </p>
    </section>

    <section class="system-rail-section">
      <div class="system-rail-section__title">
        <h3><el-icon><WarningFilled /></el-icon>知识缺口</h3>
        <span>{{ context.knowledgeGaps.length }}</span>
      </div>
      <ul v-if="context.knowledgeGaps.length" class="system-rail-gaps">
        <li v-for="gap in context.knowledgeGaps" :key="gap">{{ gap }}</li>
      </ul>
      <p v-else class="system-rail-empty">当前没有可计算的系统级缺口。</p>
    </section>

    <p class="system-context-rail__note">
      业务功能、数据库对象与集成的完整内容保留在主内容区。
    </p>
  </div>
</template>
