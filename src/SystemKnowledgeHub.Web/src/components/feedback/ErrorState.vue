<script setup lang="ts">
import { RefreshRight, Warning } from '@element-plus/icons-vue'

defineProps<{
  message: string
  title?: string
}>()

defineEmits<{
  retry: []
}>()
</script>

<template>
  <div class="feedback-state feedback-state--error" role="alert">
    <el-icon :size="20"><Warning /></el-icon>
    <div>
      <strong>{{ title ?? '当前内容暂不可用' }}</strong>
      <p>{{ message }}</p>
    </div>
    <el-button :icon="RefreshRight" @click="$emit('retry')">重试</el-button>
  </div>
</template>
