<script setup lang="ts">
import { Files } from '@element-plus/icons-vue'

withDefaults(
  defineProps<{
    title?: string
    description?: string
  }>(),
  {
    title: '尚未加载业务数据',
    description: 'Bootstrap 阶段仅验证基础工程，不创建正式知识对象。',
  },
)
</script>

<template>
  <div class="feedback-state feedback-state--empty">
    <el-icon :size="22"><Files /></el-icon>
    <div>
      <strong>{{ title }}</strong>
      <p>{{ description }}</p>
    </div>
  </div>
</template>
