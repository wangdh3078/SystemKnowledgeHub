<script setup lang="ts">
import { Connection, QuestionFilled } from '@element-plus/icons-vue'
import type { BusinessRuleDetailResponse } from '../api/businessRuleContracts'
defineProps<{ detail: BusinessRuleDetailResponse }>()
</script>
<template>
  <aside class="rule-context-rail">
    <header><small>业务规则上下文</small><h2>关系与缺口</h2><p>仅展示当前规则的关系摘要与未确认项。</p></header>
    <section><h3><el-icon><Connection /></el-icon>关联业务功能 <b>{{ detail.relatedFunctions.length }}</b></h3><button v-for="item in detail.relatedFunctions" :key="item.relationshipId" class="technical-text">{{ item.name }}</button><p v-if="!detail.relatedFunctions.length">尚无明确应用此规则的业务功能。</p></section>
    <section><h3>关联字段 <b>{{ detail.relatedFields.length }}</b></h3><button v-for="item in detail.relatedFields" :key="item.relationshipId" class="technical-text">{{ item.name }}</button><p v-if="!detail.relatedFields.length">尚未记录字段关系。</p></section>
    <section><h3>关联集成 <b>{{ detail.integrations.length }}</b></h3><button v-for="item in detail.integrations" :key="item.relationshipId" class="technical-text">{{ item.name }}</button><p v-if="!detail.integrations.length">尚未记录集成依赖。</p></section>
    <section class="rule-context-rail__gaps"><h3><el-icon><QuestionFilled /></el-icon>开放待确认事项 <b>{{ detail.contextRail.openUnknownCount }}</b></h3><button v-for="item in detail.unknownItems" :key="item.id">{{ item.question }}</button><p v-if="!detail.unknownItems.length">当前没有开放缺口。</p></section>
  </aside>
</template>
