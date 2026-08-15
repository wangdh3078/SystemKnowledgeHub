import { createRouter, createWebHistory } from 'vue-router'
import { routes } from './routes'

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.afterEach((to) => {
  const pageTitle = to.meta.title
  document.title = pageTitle ? `${pageTitle} · 系统知识中心` : '系统知识中心'
})

// Future authentication guard registration point. MVP Bootstrap has no auth.

export default router
