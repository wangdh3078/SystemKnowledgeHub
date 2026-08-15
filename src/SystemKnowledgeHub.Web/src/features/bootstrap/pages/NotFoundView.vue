<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()

function goHome(): void {
  void router.push({ name: 'foundation' })
}
</script>

<template>
  <div class="not-found-view">
    <span>404</span>
    <h1>没有找到这个页面</h1>
    <p>当前 Bootstrap 只提供基础工程验证页。</p>
    <el-button type="primary" @click="goHome">返回基础页</el-button>
  </div>
</template>
