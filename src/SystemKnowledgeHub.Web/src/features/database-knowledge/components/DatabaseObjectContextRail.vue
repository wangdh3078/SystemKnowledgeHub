<script setup lang="ts">
import { ArrowRight, Connection, Document, QuestionFilled } from '@element-plus/icons-vue'
import type { DatabaseObjectDetailResponse } from '../api/databaseKnowledgeContracts'

defineProps<{
  detail: DatabaseObjectDetailResponse
}>()

const relationLabels: Readonly<Record<string, string>> = {
  Reads: '读取',
  Writes: '写入',
  UsesField: '使用字段',
}
</script>

<template>
  <div class="database-context-rail">
    <header class="database-context-rail__header">
      <span>表级上下文</span>
      <h2>关系与缺口</h2>
      <p>仅展示 {{ detail.overview.qualifiedName }} 的对象级关系与待确认缺口。</p>
    </header>

    <section class="rail-section rail-section--open">
      <div class="rail-section__title">
        <h3>被以下功能使用</h3>
        <span class="rail-count">{{ detail.contextRail.usedByFunctions.length }}</span>
      </div>
      <div v-if="detail.contextRail.usedByFunctions.length" class="rail-list">
        <article v-for="item in detail.contextRail.usedByFunctions" :key="item.id">
          <div class="rail-list__headline">
            <strong>{{ item.name }}</strong>
            <span>{{ relationLabels[item.relationType] ?? item.relationType }}</span>
          </div>
          <small>{{ detail.system.name }}</small>
          <code v-if="item.reference">{{ item.reference }}</code>
        </article>
      </div>
      <div v-else class="rail-empty">
        <el-icon><Connection /></el-icon>
        <p>尚未建立功能级读取或写入关系。</p>
      </div>
    </section>

    <section class="rail-section">
      <div class="rail-section__title">
        <h3><el-icon><Document /></el-icon>相关业务规则</h3>
        <span class="rail-count">{{ detail.contextRail.relatedRuleCount }}</span>
        <el-icon class="rail-section__arrow"><ArrowRight /></el-icon>
      </div>
    </section>

    <section class="rail-section">
      <div class="rail-section__title">
        <h3><el-icon><Connection /></el-icon>集成关系</h3>
        <span class="rail-count">{{ detail.contextRail.integrationCount }}</span>
        <el-icon class="rail-section__arrow"><ArrowRight /></el-icon>
      </div>
    </section>

    <section class="rail-section rail-section--open">
      <div class="rail-section__title">
        <h3><el-icon><QuestionFilled /></el-icon>开放待确认事项</h3>
        <span class="rail-count">{{ detail.contextRail.openUnknownCount }}</span>
      </div>
      <div class="rail-empty rail-empty--quiet">
        <p>当前表级上下文没有开放待确认事项。</p>
      </div>
    </section>

    <p class="database-context-rail__note">
      此处只显示表级摘要；字段级关系、证据和缺口在字段详情抽屉中查看。
    </p>
  </div>
</template>
