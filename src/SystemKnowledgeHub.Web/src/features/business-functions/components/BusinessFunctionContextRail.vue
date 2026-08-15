<script setup lang="ts">
import { Connection, DataLine, Link, QuestionFilled } from '@element-plus/icons-vue'
import type { BusinessFunctionDetailResponse } from '../api/businessFunctionContracts'

defineProps<{
  functionName: string
  context: BusinessFunctionDetailResponse['contextRail']
  relatedDataCount: number
}>()
</script>

<template>
  <div class="business-function-rail">
    <header class="business-function-rail__header">
      <span>功能级上下文</span>
      <h2>关系与缺口</h2>
      <p>仅展示 {{ functionName }} 的调用关系摘要与未确认内容。</p>
    </header>

    <section class="business-function-rail__section">
      <h3><el-icon><Connection /></el-icon>调用方与入口 <span>{{ context.callers.length }}</span></h3>
      <ul v-if="context.callers.length">
        <li v-for="caller in context.callers" :key="caller"><strong>{{ caller }}</strong><small>调用 / 使用</small></li>
      </ul>
      <p v-else>尚未记录调用方或入口。</p>
    </section>

    <section class="business-function-rail__section">
      <h3><el-icon><DataLine /></el-icon>相邻业务功能 <span>{{ context.adjacentFunctions.length }}</span></h3>
      <ul v-if="context.adjacentFunctions.length">
        <li v-for="item in context.adjacentFunctions" :key="item"><strong>{{ item }}</strong><small>相邻功能</small></li>
      </ul>
      <p v-else>暂无已登记的相邻业务功能。</p>
    </section>

    <section class="business-function-rail__section">
      <h3><el-icon><Link /></el-icon>关系摘要</h3>
      <dl>
        <div><dt>关联数据</dt><dd>{{ relatedDataCount }}</dd></div>
        <div><dt>集成关系</dt><dd>{{ context.integrationCount }}</dd></div>
      </dl>
    </section>

    <section class="business-function-rail__section">
      <h3><el-icon><QuestionFilled /></el-icon>开放待确认事项 <span>{{ context.openUnknownCount }}</span></h3>
      <p v-if="context.openUnknownCount === 0">当前没有功能级开放待确认事项。</p>
      <p v-else>有 {{ context.openUnknownCount }} 项内容仍需继续调查。</p>
    </section>

    <p class="business-function-rail__note">此处只提供快速探索摘要；完整业务过程、证据与规则保留在主内容。</p>
  </div>
</template>
