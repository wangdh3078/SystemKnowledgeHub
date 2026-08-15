<script setup lang="ts">
import { Connection, QuestionFilled } from '@element-plus/icons-vue'
import { useRoute } from 'vue-router'

const route = useRoute()
</script>

<template>
  <div class="context-rail-host">
    <div v-if="!route.meta.hasContextRail" class="context-rail-host__header">
      <span class="context-rail-host__eyebrow">上下文</span>
      <h2>关系与缺口</h2>
      <p>当前路由可在这里注入对象级关系摘要，不重复主内容。</p>
    </div>

    <section v-if="!route.meta.hasContextRail" class="context-rail-host__section">
      <h3>
        <el-icon><Connection /></el-icon>
        关系宿主
      </h3>
      <p>已准备接收当前对象的关系概览。</p>
    </section>

    <section v-if="!route.meta.hasContextRail" class="context-rail-host__section">
      <h3>
        <el-icon><QuestionFilled /></el-icon>
        缺口宿主
      </h3>
      <p>已准备接收当前对象的待确认事项摘要。</p>
    </section>
    <div id="context-rail-content"></div>
  </div>
</template>
