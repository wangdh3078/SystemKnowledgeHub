import { createRouter, createWebHistory } from 'vue-router'
import { routes } from './routes'
import { useActorStore } from '../stores/actor'

const router = createRouter({
  history: createWebHistory(),
  routes,
})

router.afterEach((to) => {
  const pageTitle = to.meta.title
  document.title = pageTitle ? `${pageTitle} · 系统知识中心` : '系统知识中心'
})

router.beforeEach((to) => {
  const actorStore = useActorStore()
  if (!actorStore.isAuthenticated) return true
  if (to.meta.minimumAccessLevel === 'Administrator' && !actorStore.isAdministrator) {
    return { name: 'access-forbidden' }
  }
  return true
})

export default router
