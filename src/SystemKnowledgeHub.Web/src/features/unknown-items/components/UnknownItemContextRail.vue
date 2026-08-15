<script setup lang="ts">
import { WarningFilled } from '@element-plus/icons-vue'
import type { UnknownItemDetailResponse } from '../api/unknownItemContracts'
defineProps<{ detail: UnknownItemDetailResponse }>()
</script>
<template>
  <aside class="unknown-context-rail">
    <header><span>关系与缺口</span><small>事项级上下文</small></header>
    <section><h3>关联知识对象</h3><button v-for="item in detail.relatedObjects" :key="`${item.target.type}-${item.target.id}`"><strong class="technical-text">{{ item.display }}</strong><small>{{ item.primary ? '主要对象' : '相关对象' }}</small></button></section>
    <section><h3>知识影响</h3><p v-if="!detail.contextRail.knowledgeImpact.length">尚未形成知识更新预览。</p><div v-for="item in detail.contextRail.knowledgeImpact" :key="item" class="technical-text">{{ item }}</div></section>
    <section class="unknown-context-gap"><h3>开放缺口</h3><div><el-icon><WarningFilled /></el-icon><span><strong>{{ detail.contextRail.openGapCount }} 项</strong><small>{{ detail.resolution ? (detail.contextRail.openGapCount ? '仍有更新待处理' : '当前结论草稿已形成') : '尚未形成结论' }}</small></span></div></section>
    <footer><span>调查证据</span><strong>{{ detail.contextRail.evidenceCount }}</strong></footer>
  </aside>
</template>
