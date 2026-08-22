<script setup lang="ts">
import { useRouter } from 'vue-router'

const router = useRouter()
</script>

<template>
  <main class="security-gate security-gate--embedded">
    <section class="security-gate__card">
      <span>403</span>
      <h1>没有权限访问此页面</h1>
      <p>你已登录，但当前访问等级不允许使用该管理页面。</p>
      <div><el-button @click="router.back()">返回上一页</el-button><el-button type="primary" @click="router.push({ name: 'dashboard' })">返回首页</el-button></div>
    </section>
  </main>
</template>
