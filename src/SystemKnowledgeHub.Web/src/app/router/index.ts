import { createRouter, createWebHistory, type Router, type RouterHistory } from 'vue-router'
import { routes } from './routes'
import { useActorStore } from '../stores/actor'

export function installApplicationRouteGuards(router: Router): void {
  router.afterEach((to) => {
    const pageTitle = to.meta.title
    document.title = pageTitle ? `${pageTitle} · 系统知识中心` : '系统知识中心'
  })

  router.beforeEach(async (to) => {
    if (to.meta.layout === 'portal') return true
    const actorStore = useActorStore()
    if (!actorStore.initialized) await actorStore.initialize()
    if (!actorStore.isAuthenticated) return true
    if (to.meta.minimumAccessLevel === 'Administrator' && !actorStore.isAdministrator) {
      return { name: 'access-forbidden' }
    }
    return true
  })
}

export function createApplicationRouter(history: RouterHistory): Router {
  const router = createRouter({ history, routes })
  installApplicationRouteGuards(router)
  return router
}

export default createApplicationRouter(createWebHistory())
