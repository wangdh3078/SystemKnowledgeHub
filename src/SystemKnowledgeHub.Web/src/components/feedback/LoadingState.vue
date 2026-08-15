<script setup lang="ts">
import { Loading } from '@element-plus/icons-vue'

withDefaults(
  defineProps<{
    message?: string
  }>(),
  {
    message: '正在读取基础状态…',
  },
)
</script>

<template>
  <div class="feedback-state feedback-state--compact" role="status">
    <el-icon class="is-loading" :size="20"><Loading /></el-icon>
    <span>{{ message }}</span>
  </div>
</template>
