<script setup lang="ts">
import { RouterLink } from 'vue-router'
</script>

<template>
  <section class="portal-feedback portal-feedback--centered">
    <h1>页面未找到</h1>
    <p>该知识可能尚未发布、已取消发布，或地址不正确。</p>
    <RouterLink class="portal-link-button" :to="{ name: 'portal-home' }">返回知识首页</RouterLink>
  </section>
</template>
